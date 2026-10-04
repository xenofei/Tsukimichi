using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Tsukimichi.Verify.Net;
using Tsukimichi.Verify.Verify;

namespace Tsukimichi.Verify.Sources;

/// <summary>One wiki page as fetched: wikitext plus the parsed infobox when it is a quest (or item) page.</summary>
internal sealed record WikiPage(string Title, string Url, bool Missing, string Text)
{
    public bool IsDisambiguation => Text.Contains("{{disambig", StringComparison.OrdinalIgnoreCase) || Text.Contains("{{Disambig", StringComparison.Ordinal);

    public string? RetiredPatch
    {
        get
        {
            var m = WikiSource.RetiredTemplate().Match(Text);
            return m.Success ? (m.Groups[1].Success && m.Groups[1].Value.Length > 0 ? m.Groups[1].Value.Trim() : "unknown") : null;
        }
    }

    /// <summary>The first <c>{{Quest infobox ...}}</c> as field → value; empty when the page is not a quest page.</summary>
    public IReadOnlyDictionary<string, string> QuestInfobox => WikiSource.Infobox(Text, "Quest infobox");

    /// <summary>The first item-family infobox (<c>{{Item infobox</c>, <c>{{Weapon infobox</c>, <c>{{Armor infobox</c>, <c>{{Accessory infobox</c>, …); empty on quest pages and non-item pages.</summary>
    public IReadOnlyDictionary<string, string> ItemInfobox
    {
        get
        {
            var m = WikiSource.AnyInfobox().Match(Text);
            if (!m.Success || m.Groups[1].Value.Trim().Equals("Quest", StringComparison.OrdinalIgnoreCase))
            {
                return new Dictionary<string, string>();
            }

            return WikiSource.Infobox(Text, m.Groups[1].Value.Trim() + " infobox");
        }
    }

    /// <summary><c>id-gt</c> of the first infobox on the page (quest row id on quest pages, item id on item pages), or 0.</summary>
    public uint IdGt
    {
        get
        {
            var box = QuestInfobox.Count > 0 ? QuestInfobox : ItemInfobox;
            return box.TryGetValue("id-gt", out var gt) && uint.TryParse(gt.Trim(), out var id) ? id : 0;
        }
    }

    /// <summary><c>{{Limited Time Event Ended|startdate=…|enddate=…}}</c> when present.</summary>
    public (string Start, string End)? EventWindow
    {
        get
        {
            var box = WikiSource.Infobox(Text, "Limited Time Event Ended");
            if (box.Count == 0)
            {
                box = WikiSource.Infobox(Text, "Limited Time Event");
            }

            return box.Count == 0 ? null : (box.GetValueOrDefault("startdate", string.Empty), box.GetValueOrDefault("enddate", string.Empty));
        }
    }

    /// <summary>Titles a disambiguation page points at.</summary>
    public IReadOnlyList<string> DisambiguationTargets()
    {
        var titles = new List<string>();
        foreach (Match m in WikiSource.ILink().Matches(Text))
        {
            titles.Add(Names.Clean(m.Groups[1].Value));
        }

        foreach (Match m in WikiSource.QuestListRow().Matches(Text))
        {
            var t = Names.Clean(m.Groups[1].Value);
            if (!titles.Contains(t))
            {
                titles.Add(t);
            }
        }

        foreach (Match m in WikiSource.WikiLink().Matches(Text))
        {
            var t = Names.Clean(m.Groups[1].Value);
            if (!t.Contains(':') && !titles.Contains(t))
            {
                titles.Add(t);
            }
        }

        return titles;
    }
}

/// <summary>
/// consolegameswiki through the MediaWiki API: <c>prop=revisions&amp;rvprop=content</c> for up to 50 titles per call,
/// which returns the same wikitext as <c>?action=raw</c> with fifty times fewer requests. Redirects are followed by the
/// API (<c>redirects=1</c>); disambiguation pages are detected and their targets fetched in a second batch by the caller.
/// </summary>
internal sealed partial class WikiSource(PoliteHttp http, TextWriter log)
{
    public const string PageBase = "https://ffxiv.consolegameswiki.com/wiki/";
    private const string Api = "https://ffxiv.consolegameswiki.com/mediawiki/api.php";
    private const int BatchSize = 50;

    [GeneratedRegex(@"\{\{\s*Retired\s*(?:\|\s*patch\s*=\s*([^}|]*))?[^}]*\}\}", RegexOptions.IgnoreCase)]
    internal static partial Regex RetiredTemplate();

    [GeneratedRegex(@"\{\{\s*i\s*\|([^|}]+)")]
    internal static partial Regex ILink();

    [GeneratedRegex(@"\{\{\s*([A-Za-z][A-Za-z ]*?)\s+[Ii]nfobox\b")]
    internal static partial Regex AnyInfobox();

    [GeneratedRegex(@"\{\{\s*quest list row\s*\|([^|}]+)", RegexOptions.IgnoreCase)]
    internal static partial Regex QuestListRow();

    [GeneratedRegex(@"\[\[([^\]|#]+)(?:[|#][^\]]*)?\]\]")]
    internal static partial Regex WikiLink();

    public static string PageUrl(string title) => PageBase + Uri.EscapeDataString(title.Replace(' ', '_')).Replace("%2F", "/").Replace("%28", "(").Replace("%29", ")").Replace("%27", "'").Replace("%2C", ",");

    public static string RawUrl(string title) => PageUrl(title) + "?action=raw";

    /// <summary>Requested title → page, built once from every cached batch so a title is found whichever batch fetched it.</summary>
    private Dictionary<string, WikiPage>? index;

    /// <summary>
    /// Fetches every title (batched). The result is keyed by the requested title; normalized/redirected titles are
    /// resolved back. Titles already in the cache index are served from it; only the misses go out in batches, so the
    /// cache stays valid when the set of titles changes between runs.
    /// </summary>
    public async Task<Dictionary<string, WikiPage>> GetPagesAsync(IEnumerable<string> titles, CancellationToken ct)
    {
        index ??= BuildIndex();
        var result = new Dictionary<string, WikiPage>(StringComparer.Ordinal);
        var distinct = titles.Select(Names.WikiTitle).Where(t => t.Length > 0).Distinct(StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal).ToList();
        var misses = new List<string>();
        foreach (var t in distinct)
        {
            if (index.TryGetValue(t, out var cached))
            {
                result[t] = cached;
                result.TryAdd(cached.Title, cached);
                http.CountCacheHit();
            }
            else
            {
                misses.Add(t);
            }
        }

        foreach (var chunk in misses.Chunk(BatchSize))
        {
            var query = string.Join("|", chunk);
            var url = BatchUrl(query);
            var fetched = await http.GetAsync(url, ct);
            if (!fetched.Ok)
            {
                log.WriteLine($"wiki: batch of {chunk.Length} titles unavailable (status {fetched.Status})");
                continue;
            }

            var pages = ParseBatch(fetched.Body, out var error);
            if (pages is null)
            {
                log.WriteLine($"wiki: {error}");
                continue;
            }

            foreach (var (requested, page) in pages)
            {
                result[requested] = page;
                index[requested] = page;
                if (!string.Equals(requested, page.Title, StringComparison.Ordinal))
                {
                    result.TryAdd(page.Title, page);
                    index.TryAdd(page.Title, page);
                }
            }
        }

        // Titles the API did not echo back at all count as missing.
        foreach (var t in distinct)
        {
            result.TryAdd(t, new WikiPage(t, PageUrl(t), true, string.Empty));
        }

        return result;
    }

    private static string BatchUrl(string query) => $"{Api}?action=query&format=json&formatversion=2&prop=revisions&rvprop=content&rvslots=main&redirects=1&maxlag=5&titles={Uri.EscapeDataString(query)}";

    /// <summary>Every page the fetch cache holds, once each (by title); never fetches.</summary>
    public IEnumerable<WikiPage> CachedPages()
    {
        index ??= BuildIndex();
        return index.Values.DistinctBy(p => p.Title, StringComparer.Ordinal);
    }

    private Dictionary<string, WikiPage> BuildIndex()
    {
        var built = new Dictionary<string, WikiPage>(StringComparer.Ordinal);
        var batches = 0;
        foreach (var (_, body, _) in http.EnumerateCached("ffxiv.consolegameswiki.com", Api + "?action=query"))
        {
            var pages = ParseBatch(body, out _);
            if (pages is null)
            {
                continue;
            }

            batches++;
            foreach (var (requested, page) in pages)
            {
                // Newest batch first, so the first sighting of a title wins.
                built.TryAdd(requested, page);
                built.TryAdd(page.Title, page);
            }
        }

        if (batches > 0)
        {
            log.WriteLine($"wiki: {built.Count} titles indexed from {batches} cached batches");
        }

        return built;
    }

    /// <summary>One API batch response → (requested title, page) pairs, with normalization and redirects mapped back to the requested spelling.</summary>
    private static List<(string Requested, WikiPage Page)>? ParseBatch(string body, out string error)
    {
        error = string.Empty;
        JsonObject? root;
        try
        {
            root = JsonNode.Parse(body)?.AsObject();
        }
        catch (System.Text.Json.JsonException)
        {
            error = "batch response was not JSON";
            return null;
        }

        if (root?["query"] is not JsonObject q)
        {
            error = root?["error"] is JsonObject err ? $"API error {err["code"]}: {err["info"]}" : "batch response has no query object";
            return null;
        }

        var back = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var n in (q["normalized"] as JsonArray ?? []).OfType<JsonObject>())
        {
            back[n["to"]!.GetValue<string>()] = n["from"]!.GetValue<string>();
        }

        foreach (var r in (q["redirects"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var from = r["from"]!.GetValue<string>();
            var to = r["to"]!.GetValue<string>();
            back[to] = back.TryGetValue(from, out var orig) ? orig : from;
        }

        var list = new List<(string, WikiPage)>();
        foreach (var p in (q["pages"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var served = p["title"]!.GetValue<string>();
            var requested = back.TryGetValue(served, out var orig) ? orig : served;
            var missing = p["missing"] is JsonValue mv && mv.TryGetValue<bool>(out var b) && b;
            var content = p["revisions"]?[0]?["slots"]?["main"]?["content"]?.GetValue<string>() ?? string.Empty;
            list.Add((requested, new WikiPage(served, PageUrl(served), missing, content)));
        }

        return list;
    }

    /// <summary>
    /// Resolves each requested title to the page whose <c>id-gt</c> equals the wanted id, following disambiguation
    /// pages one level (their targets are fetched in one extra batch). A title whose page is not a disambiguation is
    /// returned as fetched; a disambiguation with no target carrying the id yields the disambiguation page itself.
    /// </summary>
    public async Task<Dictionary<string, WikiPage>> GetPagesByIdAsync(IEnumerable<(string Title, uint Id)> wanted, bool questPages, CancellationToken ct)
    {
        var list = wanted.ToList();
        var pages = await GetPagesAsync(list.Select(w => w.Title), ct);
        var targets = new List<string>();
        foreach (var (title, _) in list)
        {
            if (!pages.TryGetValue(Names.WikiTitle(title), out var p) || p.Missing)
            {
                continue;
            }

            if (p.IsDisambiguation)
            {
                targets.AddRange(p.DisambiguationTargets());
            }
            else if (questPages ? p.QuestInfobox.Count == 0 : p.ItemInfobox.Count == 0)
            {
                // The plain title belongs to something else (an expansion page, an action); the wiki files the page as "Name (Quest)" / "Name (Item)".
                targets.Add($"{title} ({(questPages ? "Quest" : "Item")})");
            }
        }

        targets = targets.Distinct(StringComparer.Ordinal).Where(t => !pages.ContainsKey(Names.WikiTitle(t))).ToList();
        if (targets.Count > 0)
        {
            foreach (var (k, v) in await GetPagesAsync(targets, ct))
            {
                pages.TryAdd(k, v);
            }
        }

        var byId = new Dictionary<uint, WikiPage>();
        foreach (var p in pages.Values)
        {
            if (!p.Missing && p.IdGt != 0)
            {
                byId.TryAdd(p.IdGt, p);
            }
        }

        var result = new Dictionary<string, WikiPage>(StringComparer.Ordinal);
        foreach (var (title, id) in list)
        {
            var key = Names.WikiTitle(title);
            if (id != 0 && byId.TryGetValue(id, out var exact))
            {
                result[key] = exact;
                continue;
            }

            pages.TryGetValue(key, out var p);
            var right = p is not null && !p.Missing && !p.IsDisambiguation && (questPages ? p.QuestInfobox.Count > 0 : p.ItemInfobox.Count > 0);
            if (!right)
            {
                // Fall back to the "(Quest)"/"(Item)" page, then to any disambiguation target of the same base name (a same-name variant's page).
                var suffixed = Names.WikiTitle($"{title} ({(questPages ? "Quest" : "Item")})");
                if (pages.TryGetValue(suffixed, out var s) && !s.Missing && (questPages ? s.QuestInfobox.Count > 0 : s.ItemInfobox.Count > 0))
                {
                    p = s;
                }
                else if (p is { IsDisambiguation: true })
                {
                    foreach (var t in p.DisambiguationTargets())
                    {
                        if (pages.TryGetValue(Names.WikiTitle(t), out var v) && !v.Missing && (questPages ? v.QuestInfobox.Count > 0 : v.ItemInfobox.Count > 0))
                        {
                            p = v;
                            break;
                        }
                    }
                }
            }

            if (p is not null)
            {
                result[key] = p;
            }
        }

        return result;
    }

    /// <summary>
    /// Parses the first <c>{{Name ...}}</c> template into field → value, brace-aware so nested templates such as
    /// <c>{{i|The Navel (Hard)}}</c> stay inside their value. Keys are lower-cased and trimmed.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> Infobox(string text, string templateName)
    {
        var start = text.IndexOf("{{" + templateName, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return new Dictionary<string, string>();
        }

        var depth = 0;
        var end = -1;
        for (var i = start; i < text.Length - 1; i++)
        {
            if (text[i] == '{' && text[i + 1] == '{')
            {
                depth++;
                i++;
            }
            else if (text[i] == '}' && text[i + 1] == '}')
            {
                depth--;
                i++;
                if (depth == 0)
                {
                    end = i - 1;
                    break;
                }
            }
        }

        if (end < 0)
        {
            end = text.Length;
        }

        var inner = text[(start + 2)..end];
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        var d = 0;
        var link = 0;
        for (var i = 0; i < inner.Length; i++)
        {
            var c = inner[i];
            if (c == '{' && i + 1 < inner.Length && inner[i + 1] == '{')
            {
                d++;
                current.Append("{{");
                i++;
                continue;
            }

            if (c == '}' && i + 1 < inner.Length && inner[i + 1] == '}')
            {
                d--;
                current.Append("}}");
                i++;
                continue;
            }

            if (c == '[' && i + 1 < inner.Length && inner[i + 1] == '[')
            {
                link++;
                current.Append("[[");
                i++;
                continue;
            }

            if (c == ']' && i + 1 < inner.Length && inner[i + 1] == ']')
            {
                link--;
                current.Append("]]");
                i++;
                continue;
            }

            if (c == '|' && d == 0 && link <= 0)
            {
                parts.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        parts.Add(current.ToString());
        foreach (var part in parts.Skip(1))
        {
            var eq = part.IndexOf('=');
            if (eq < 0)
            {
                continue;
            }

            var key = part[..eq].Trim().ToLowerInvariant();
            var value = part[(eq + 1)..].Trim();
            if (key.Length > 0)
            {
                fields[key] = value;
            }
        }

        return fields;
    }

    /// <summary>Splits a comma-separated infobox value of names (<c>prev-quest</c>, <c>next-quest</c>), restoring <c>&amp;comma;</c>, stripping links and templates.</summary>
    public static List<string> SplitNames(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var list = new List<string>();
        foreach (var raw in value.Split(','))
        {
            var t = raw.Trim();
            if (t.Length == 0)
            {
                continue;
            }

            // "Target{{!}}Label" is the wiki's escaped link pipe inside a template argument; the target is the page name.
            var pipe = t.IndexOf("{{!}}", StringComparison.Ordinal);
            if (pipe > 0)
            {
                t = t[..pipe];
            }

            list.Add(StripMarkup(t).Replace('_', ' '));
        }

        return list.Where(v => v.Length > 0).ToList();
    }

    /// <summary>Names inside <c>{{i|Name|…}}</c> templates and <c>[[links]]</c> in a value.</summary>
    public static List<string> LinkedNames(string? value)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return list;
        }

        foreach (Match m in ILink().Matches(value))
        {
            list.Add(Names.Clean(m.Groups[1].Value));
        }

        foreach (Match m in WikiLink().Matches(value))
        {
            list.Add(Names.Clean(m.Groups[1].Value));
        }

        return list;
    }

    /// <summary>
    /// The <c>unlocks</c> field: entries such as <c>dg The Praetorium, tr The Porta Decumana, ra Asphodelos: The First Circle, ach …, ac …, ms …, ngp …</c>.
    /// Returns (code, name) pairs; duties are dg (dungeon), tr (trial), ra (raid), ar (alliance raid), ud (ultimate), gb (guildhest) and similar.
    /// </summary>
    public static List<(string Code, string Name)> Unlocks(string? value)
    {
        var list = new List<(string, string)>();
        foreach (var entry in SplitNames(value))
        {
            var space = entry.IndexOf(' ');
            if (space > 0 && space <= 4)
            {
                list.Add((entry[..space].ToLowerInvariant(), entry[(space + 1)..].Trim()));
            }
            else
            {
                list.Add((string.Empty, entry));
            }
        }

        return list;
    }

    public static readonly HashSet<string> DutyCodes = new(StringComparer.OrdinalIgnoreCase) { "dg", "tr", "ra", "ar", "ud", "gb", "du", "pvp", "dd", "fd", "ex", "qb", "vc" };

    public static string StripMarkup(string s)
    {
        var t = s;
        t = Regex.Replace(t, @"\{\{\s*i\s*\|([^|}]+)[^}]*\}\}", "$1");
        t = Regex.Replace(t, @"\[\[([^\]|]+)\|([^\]]+)\]\]", "$2");
        t = Regex.Replace(t, @"\[\[([^\]]+)\]\]", "$1");
        t = Regex.Replace(t, @"'{2,}", string.Empty);
        t = Regex.Replace(t, @"\{\{[^}]*\}\}", string.Empty);
        return Names.Clean(t);
    }

    /// <summary>Wiki <c>release</c>/<c>patch</c> → Expansion row id (0 ARR … 5 Dawntrail), or −1 when unknown.</summary>
    public static int ExpansionOf(string? release, string? patch)
    {
        var r = Names.Canon(release);
        if (r.Length > 0)
        {
            if (r.Contains("realm reborn") || r == "arr")
            {
                return 0;
            }

            if (r.Contains("heavensward"))
            {
                return 1;
            }

            if (r.Contains("stormblood"))
            {
                return 2;
            }

            if (r.Contains("shadowbringers"))
            {
                return 3;
            }

            if (r.Contains("endwalker"))
            {
                return 4;
            }

            if (r.Contains("dawntrail"))
            {
                return 5;
            }
        }

        var p = Names.Canon(patch);
        if (p.Length > 0 && char.IsDigit(p[0]))
        {
            return p[0] switch
            {
                '2' => 0,
                '3' => 1,
                '4' => 2,
                '5' => 3,
                '6' => 4,
                '7' => 5,
                _ => -1,
            };
        }

        return -1;
    }

    /// <summary>Acquisition summary of an item page: which <c>===Subsection===</c> headings sit under <c>==Acquisition==</c> plus store/quest markers.</summary>
    public static ItemAcquisition Acquisition(string text)
    {
        var start = text.IndexOf("==Acquisition==", StringComparison.OrdinalIgnoreCase);
        var sections = new List<string>();
        var questRows = new List<string>();
        var onlineStore = text.Contains("{{onlinestore", StringComparison.OrdinalIgnoreCase) || text.Contains("| online-store-id =", StringComparison.OrdinalIgnoreCase) && !Regex.IsMatch(text, @"\|\s*online-store-id\s*=\s*(\n|\|)");
        if (start < 0)
        {
            return new ItemAcquisition(false, sections, questRows, onlineStore);
        }

        var next = Regex.Match(text[(start + 15)..], @"(?m)^==[^=]");
        var body = next.Success ? text.Substring(start + 15, next.Index) : text[(start + 15)..];
        foreach (Match m in Regex.Matches(body, @"(?m)^===+\s*(.*?)\s*===+\s*$"))
        {
            sections.Add(StripMarkup(m.Groups[1].Value));
        }

        foreach (Match m in Regex.Matches(body, @"\{\{\s*quest list row\s*\|([^|}]+)", RegexOptions.IgnoreCase))
        {
            questRows.Add(Names.Clean(m.Groups[1].Value));
        }

        foreach (Match m in Regex.Matches(body, @"\{\{\s*quest\s*\|([^|}]+)", RegexOptions.IgnoreCase))
        {
            questRows.Add(Names.Clean(m.Groups[1].Value));
        }

        return new ItemAcquisition(true, sections, questRows, onlineStore);
    }
}

internal sealed record ItemAcquisition(bool HasSection, IReadOnlyList<string> Sections, IReadOnlyList<string> QuestRows, bool OnlineStore);
