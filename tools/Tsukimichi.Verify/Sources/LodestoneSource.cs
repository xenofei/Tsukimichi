using System.Text.RegularExpressions;
using Tsukimichi.Verify.Net;
using Tsukimichi.Verify.Verify;

namespace Tsukimichi.Verify.Sources;

/// <summary>One row of a Lodestone category listing.</summary>
internal sealed record LodestoneListing(string LodestoneId, string Name, string Area, int Level, uint SectionId, uint CategoryId);

/// <summary>Facts parsed from one Lodestone quest page. <see cref="Parsed"/> is false when the page lacks the name or level (layout drift).</summary>
internal sealed record LodestonePage(
    string LodestoneId,
    string Url,
    bool Parsed,
    string Name,
    int Level,
    string ContentType,
    string StartingClass,
    string ClassJobText,
    int ClassJobLevel,
    string GrandCompany,
    IReadOnlyList<string> QuestDuty,
    bool AllOfTheAbove,
    IReadOnlyList<string> Rewards,
    IReadOnlyList<string> OptionalRewards,
    bool SeasonalEnded);

/// <summary>An item page's "Obtained From" tables: the table heading (Quest, Duty, Shop, ...) and the linked names under it.</summary>
internal sealed record LodestoneItem(string LodestoneId, string Url, string Name, IReadOnlyList<(string Kind, string Name)> Sources)
{
    /// <summary>Source tables other than Quest.</summary>
    public IReadOnlyList<(string Kind, string Name)> NonQuest => Sources.Where(s => !s.Kind.Equals("Quest", StringComparison.OrdinalIgnoreCase)).ToList();
}

/// <summary>
/// The Lodestone Eorzea Database (official). Ids are enumerated from the category listings
/// (<c>?category2=&lt;JournalSection&gt;&amp;category3=&lt;JournalCategory&gt;&amp;page=n</c>, 50 rows per page) and
/// details come from the quest page. Parsing is tolerant regex over the served HTML; a page that yields fewer than the
/// minimum fields is reported as unparsed rather than matched.
/// </summary>
internal sealed partial class LodestoneSource(PoliteHttp http, TextWriter log)
{
    public const string Base = "https://na.finalfantasyxiv.com/lodestone/playguide/db/quest/";
    private const int PageSize = 50;

    [GeneratedRegex(@"<span class=""total"">(\d+)</span>")]
    private static partial Regex Total();

    [GeneratedRegex(@"<a href=""/lodestone/playguide/db/quest/([0-9a-f]+)/"" class=""db_popup db-table__txt--detail_link"">(.*?)</a>.*?db-table__quest__area"">(.*?)</td>\s*<td class=""db-table__body--light db-table__body--center"">(\d+)</td>", RegexOptions.Singleline)]
    private static partial Regex ListingRow();

    [GeneratedRegex(@"db-view__detail__lname_name[^>]*>\s*(.*?)\s*</h2>", RegexOptions.Singleline)]
    private static partial Regex PageName();

    [GeneratedRegex(@"db-view__detail__level"">Lv\.\s*(\d+)</span>")]
    private static partial Regex PageLevel();

    [GeneratedRegex(@"db-view__detail__content_type"">(.*?)</span>", RegexOptions.Singleline)]
    private static partial Regex PageContentType();

    [GeneratedRegex(@"<dt class=""db-view__data__detail_list__header"">(.*?)</dt>\s*<dd>(.*?)</dd>", RegexOptions.Singleline)]
    private static partial Regex DetailList();

    [GeneratedRegex(@"<strong>(.*?)</strong>", RegexOptions.Singleline)]
    private static partial Regex Strong();

    [GeneratedRegex(@"\s*Lv\.\s*(\d+)\s*$")]
    private static partial Regex TrailingLevel();

    public const string ItemBase = "https://na.finalfantasyxiv.com/lodestone/playguide/db/item/";

    [GeneratedRegex(@"<a href=""/lodestone/playguide/db/item/([0-9a-f]+)/"" class=""db_popup db-table__txt--detail_link"">(.*?)</a>", RegexOptions.Singleline)]
    private static partial Regex ItemLink();

    [GeneratedRegex(@"<th[^>]*>(.*?)</th>", RegexOptions.Singleline)]
    private static partial Regex TableHead();

    [GeneratedRegex(@"class=""db_popup db-table__txt--detail_link"">(.*?)</a>", RegexOptions.Singleline)]
    private static partial Regex DetailLink();

    public static string PageUrl(string lodestoneId) => $"{Base}{lodestoneId}/";

    public static string ItemSearchUrl(string name) => $"{ItemBase}?q={Uri.EscapeDataString(name)}";

    public static string ItemUrl(string lodestoneId) => $"{ItemBase}{lodestoneId}/";

    /// <summary>
    /// The item page for an exact item name, found through the database search: null when the search returned no
    /// response, an item with no <see cref="LodestoneItem.Sources"/> and an empty id when no result carries that name.
    /// </summary>
    public async Task<LodestoneItem?> FindItemAsync(string name, CancellationToken ct)
    {
        var search = await http.GetAsync(ItemSearchUrl(name), ct);
        if (!search.Ok)
        {
            return null;
        }

        var id = ItemLink().Matches(search.Body).Where(m => Names.Canon(m.Groups[2].Value) == Names.Canon(name)).Select(m => m.Groups[1].Value).FirstOrDefault();
        if (id is null)
        {
            return new LodestoneItem(string.Empty, ItemSearchUrl(name), name, []);
        }

        var page = await http.GetAsync(ItemUrl(id), ct);
        return page.Ok ? ParseItem(id, ItemUrl(id), name, page.Body) : null;
    }

    /// <summary>Each <c>db-item__source</c> table under "Obtained From": its first column heading and the detail links in it.</summary>
    public static LodestoneItem ParseItem(string lodestoneId, string url, string name, string html)
    {
        var sources = new List<(string Kind, string Name)>();
        var chunks = html.Split("db-item__source");
        for (var i = 1; i < chunks.Length; i++)
        {
            var end = chunks[i].IndexOf("</table>", StringComparison.Ordinal);
            var table = end < 0 ? chunks[i] : chunks[i][..end];
            var kind = TableHead().Match(table) is { Success: true } h ? Names.Clean(h.Groups[1].Value) : string.Empty;
            foreach (Match m in DetailLink().Matches(table))
            {
                sources.Add((kind, Names.Clean(m.Groups[1].Value)));
            }
        }

        return new LodestoneItem(lodestoneId, url, name, sources);
    }

    public static string ListingUrl(uint section, uint category, int page) => $"{Base}?category2={section}&category3={category}&page={page}";

    /// <summary>Every quest the Lodestone lists under one (section, category), across all pages. Null when the first page could not be fetched.</summary>
    public async Task<(List<LodestoneListing>? Rows, int Total, string FirstUrl)> ListCategoryAsync(uint section, uint category, CancellationToken ct)
    {
        var rows = new List<LodestoneListing>();
        var total = 0;
        var page = 1;
        var firstUrl = ListingUrl(section, category, 1);
        while (true)
        {
            var url = ListingUrl(section, category, page);
            var fetched = await http.GetAsync(url, ct);
            if (!fetched.Ok)
            {
                if (page == 1)
                {
                    log.WriteLine($"lodestone: listing {section}/{category} unavailable (status {fetched.Status})");
                    return (null, 0, firstUrl);
                }

                log.WriteLine($"lodestone: listing {section}/{category} page {page} unavailable (status {fetched.Status}); partial");
                break;
            }

            if (page == 1)
            {
                var m = Total().Match(fetched.Body);
                total = m.Success ? int.Parse(m.Groups[1].Value) : 0;
            }

            foreach (Match m in ListingRow().Matches(fetched.Body))
            {
                rows.Add(new LodestoneListing(m.Groups[1].Value, Names.Clean(m.Groups[2].Value), Names.Clean(m.Groups[3].Value), int.Parse(m.Groups[4].Value), section, category));
            }

            if (page * PageSize >= total)
            {
                break;
            }

            page++;
        }

        return (rows, total, firstUrl);
    }

    public async Task<LodestonePage?> GetPageAsync(string lodestoneId, CancellationToken ct)
    {
        var url = PageUrl(lodestoneId);
        var fetched = await http.GetAsync(url, ct);
        if (!fetched.Ok)
        {
            return fetched.NoResponse ? null : new LodestonePage(lodestoneId, url, false, string.Empty, 0, string.Empty, string.Empty, string.Empty, 0, string.Empty, [], false, [], [], false);
        }

        return Parse(lodestoneId, url, fetched.Body);
    }

    public static LodestonePage Parse(string lodestoneId, string url, string html)
    {
        var start = html.IndexOf("db__l_main__view", StringComparison.Ordinal);
        var end = html.IndexOf("db-tools", start < 0 ? 0 : start, StringComparison.Ordinal);
        var body = start < 0 ? html : html[start..(end < 0 ? html.Length : end)];

        var name = PageName().Match(body) is { Success: true } nm ? Names.Clean(nm.Groups[1].Value) : string.Empty;
        var level = PageLevel().Match(body) is { Success: true } lm ? int.Parse(lm.Groups[1].Value) : -1;
        var contentType = PageContentType().Match(body) is { Success: true } cm ? Names.Clean(cm.Groups[1].Value) : string.Empty;

        var startingClass = string.Empty;
        var classJobText = string.Empty;
        var classJobLevel = -1;
        var grandCompany = string.Empty;
        var questDuty = new List<string>();
        var allOfTheAbove = false;
        foreach (Match m in DetailList().Matches(body))
        {
            var header = Names.Clean(m.Groups[1].Value);
            var lines = m.Groups[2].Value.Split(["<br>", "<br/>", "<br />"], StringSplitOptions.None)
                .Select(Names.Clean)
                .Where(l => l.Length > 0)
                .ToList();
            var value = lines.Count == 0 ? string.Empty : lines[0];
            var specified = !value.Equals("Not specified", StringComparison.OrdinalIgnoreCase);
            switch (header)
            {
                case "Starting Class":
                    startingClass = specified ? value : string.Empty;
                    break;
                case "Class/Job":
                    if (specified)
                    {
                        var lvl = TrailingLevel().Match(value);
                        classJobLevel = lvl.Success ? int.Parse(lvl.Groups[1].Value) : -1;
                        classJobText = lvl.Success ? value[..lvl.Index].Trim() : value;
                    }

                    break;
                case "Grand Company":
                    grandCompany = specified ? value : string.Empty;
                    break;
                case "Quest/Duty":
                    if (specified)
                    {
                        foreach (var line in lines)
                        {
                            if (line.StartsWith("All of the above", StringComparison.OrdinalIgnoreCase))
                            {
                                allOfTheAbove = true;
                            }
                            else if (line.StartsWith("Any of the above", StringComparison.OrdinalIgnoreCase) || line.StartsWith("One of the above", StringComparison.OrdinalIgnoreCase))
                            {
                                allOfTheAbove = false;
                            }
                            else
                            {
                                questDuty.Add(line);
                            }
                        }
                    }

                    break;
            }
        }

        var rewards = new List<string>();
        var optional = new List<string>();
        var rewardStart = body.IndexOf("db-view__data__inner--quest_reward", StringComparison.Ordinal);
        var optionalStart = body.IndexOf("db-view__data__inner--select_reward", StringComparison.Ordinal);
        if (rewardStart >= 0)
        {
            var rewardEnd = optionalStart > rewardStart ? optionalStart : body.Length;
            foreach (Match m in Strong().Matches(body[rewardStart..rewardEnd]))
            {
                rewards.Add(Names.Clean(m.Groups[1].Value));
            }
        }

        if (optionalStart >= 0)
        {
            foreach (Match m in Strong().Matches(body[optionalStart..]))
            {
                optional.Add(Names.Clean(m.Groups[1].Value));
            }
        }

        var seasonalEnded = body.Contains("db-view__quest__past_season_event", StringComparison.Ordinal);
        var parsed = name.Length > 0 && level >= 0;
        return new LodestonePage(lodestoneId, url, parsed, name, level, contentType, startingClass, classJobText, classJobLevel, grandCompany, questDuty, allOfTheAbove, rewards, optional, seasonalEnded);
    }
}
