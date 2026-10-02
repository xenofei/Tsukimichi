using System.Globalization;
using System.Text;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Links;

/// <summary>The sites "Open on…" offers (1.8.0).</summary>
public enum ExternalSite : byte
{
    Lodestone,
    GarlandTools,
    ConsoleGamesWiki,
    Teamcraft,
    FfxivCollect,
}

/// <summary>
/// Browser links for "Open on…", Copy for Discord, Copy table as TSV and the export (feature plan v5, side track;
/// research C9). Every link is built from ids the plugin already holds (<see cref="ExternalIds"/>, row ids, item ids);
/// nothing here fetches anything (decision 8). A quest the link table does not name falls back to the site's search:
/// the Lodestone's <c>?q=</c> and the wiki's <c>Special:Search?search=…&amp;go=Go</c>, which jumps straight to a page
/// whose title matches. Pure.
/// <list type="bullet">
/// <item>Lodestone: <c>https://{na|jp|de|fr}.finalfantasyxiv.com/lodestone/playguide/db/quest/{hash}/</c>; the hash is the same in every region, and the region picks the page's language.</item>
/// <item>Garland Tools: <c>https://www.garlandtools.org/db/#quest/{rowId}</c> and <c>#item/{itemId}</c>.</item>
/// <item>Console Games Wiki: <c>https://ffxiv.consolegameswiki.com/wiki/{Title}</c> (English only).</item>
/// <item>Teamcraft: <c>https://ffxivteamcraft.com/db/{lang}/quest/{rowId}</c>.</item>
/// <item>FFXIV Collect: <c>https://ffxivcollect.com/{path}/{collectId}</c>, or the kind's list filtered by English name.</item>
/// </list>
/// </summary>
public static class ExternalLinks
{
    public const string GarlandBase = "https://www.garlandtools.org/db/#";
    public const string WikiBase = "https://ffxiv.consolegameswiki.com/wiki/";
    public const string TeamcraftBase = "https://ffxivteamcraft.com/db/";
    public const string CollectBase = "https://ffxivcollect.com/";

    /// <summary>
    /// The sites' language code for the client's (the catalog's Lumina language name, <c>CatalogBundle.Language</c>):
    /// "en", "ja", "de" or "fr"; English for anything else.
    /// </summary>
    public static string SiteLanguage(string? clientLanguage) => clientLanguage?.Trim().ToLowerInvariant() switch
    {
        "japanese" or "ja" => "ja",
        "german" or "de" => "de",
        "french" or "fr" => "fr",
        _ => "en",
    };

    /// <summary>The Lodestone host for a site language: na (English), jp, de or fr.</summary>
    public static string LodestoneHost(string language) => "https://" + (SiteLanguage(language) switch
    {
        "ja" => "jp",
        "de" => "de",
        "fr" => "fr",
        _ => "na",
    }) + ".finalfantasyxiv.com/lodestone/playguide/db/";

    /// <summary>The quest's Lodestone page; null for anything that is not a database hash.</summary>
    public static string? LodestoneQuest(string? hash, string language) =>
        ExternalIds.IsLodestoneHash(hash) ? LodestoneHost(language) + "quest/" + hash + "/" : null;

    /// <summary>The Lodestone's quest search for a name (in the client's language, which the region matches).</summary>
    public static string LodestoneSearch(string name, string language) =>
        LodestoneHost(language) + "quest/?q=" + Uri.EscapeDataString(name ?? string.Empty);

    public static string GarlandQuest(uint rowId) => GarlandBase + "quest/" + rowId.ToString(CultureInfo.InvariantCulture);

    public static string GarlandItem(uint itemId) => GarlandBase + "item/" + itemId.ToString(CultureInfo.InvariantCulture);

    /// <summary>Teamcraft's quest page in the site language (Teamcraft also reads ko, zh and ru; the plugin never needs them).</summary>
    public static string TeamcraftQuest(uint rowId, string language) =>
        TeamcraftBase + SiteLanguage(language) + "/quest/" + rowId.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// A wiki page: spaces become underscores and everything else is percent-encoded except the characters MediaWiki
    /// titles keep readable (<c>/ ( ) ' ,</c>), exactly as <c>Tsukimichi.Verify</c> wrote the URLs it checked.
    /// </summary>
    public static string WikiPage(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        return WikiBase + Uri.EscapeDataString(title.Trim().Replace(' ', '_'))
            .Replace("%2F", "/", StringComparison.Ordinal)
            .Replace("%28", "(", StringComparison.Ordinal)
            .Replace("%29", ")", StringComparison.Ordinal)
            .Replace("%27", "'", StringComparison.Ordinal)
            .Replace("%2C", ",", StringComparison.Ordinal);
    }

    /// <summary>The wiki's search, which opens the page outright when a title matches (<c>go=Go</c>).</summary>
    public static string WikiSearch(string name) =>
        WikiBase + "Special:Search?search=" + Uri.EscapeDataString(name ?? string.Empty) + "&go=Go";

    /// <summary>
    /// FFXIV Collect's path for a reward kind (the research's verified mapping; the same paths as
    /// <c>Tsukimichi.Verify</c>'s dumps): mounts, minions, emotes, orchestrions, bardings, hairstyles, fashions,
    /// achievements and triad/cards. Null for a kind the site does not list.
    /// </summary>
    public static string? CollectPath(RewardKind kind) => kind switch
    {
        RewardKind.Mount => "mounts",
        RewardKind.Minion => "minions",
        RewardKind.Emote => "emotes",
        RewardKind.Orchestrion => "orchestrions",
        RewardKind.Barding => "bardings",
        RewardKind.Hairstyle => "hairstyles",
        RewardKind.Ornament => "fashions",
        RewardKind.Achievement => "achievements",
        RewardKind.TripleTriadCard => "triad/cards",
        _ => null,
    };

    /// <summary>The reward's FFXIV Collect page; null for a kind the site does not list.</summary>
    public static string? CollectEntry(RewardKind kind, uint collectId) =>
        CollectPath(kind) is { } path && collectId != 0 ? CollectBase + path + "/" + collectId.ToString(CultureInfo.InvariantCulture) : null;

    /// <summary>The kind's FFXIV Collect list filtered by the reward's English name; null for a kind the site does not list.</summary>
    public static string? CollectSearch(RewardKind kind, string englishName) =>
        CollectPath(kind) is { } path ? CollectBase + path + "?q%5Bname_en_cont%5D=" + Uri.EscapeDataString(englishName ?? string.Empty) : null;

    /// <summary>The reward's FFXIV Collect page when the table names it, else the name search; null for a kind the site does not list.</summary>
    public static string? Collect(UniqueRewardEntry entry, ExternalIds ids)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return Collect(entry.Kind, entry.RewardId, entry.RewardName, ids);
    }

    /// <inheritdoc cref="Collect(UniqueRewardEntry, ExternalIds)"/>
    public static string? Collect(RewardKind kind, uint rewardId, string englishName, ExternalIds ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return ids.CollectId(kind, rewardId) is { } id ? CollectEntry(kind, id) : CollectSearch(kind, englishName);
    }

    /// <summary>
    /// The one link a copied reward row carries: its FFXIV Collect page (or name search) for a kind the site lists,
    /// else its item on Garland Tools, else its quest there.
    /// </summary>
    public static string Reward(RewardKind kind, uint rewardId, uint itemId, uint questRowId, string englishName, ExternalIds ids) =>
        Collect(kind, rewardId, englishName, ids) ?? (itemId != 0 ? GarlandItem(itemId) : GarlandQuest(questRowId));

    /// <summary>
    /// The quest's page on <paramref name="site"/>: the Lodestone and the wiki by the link table, else their search for
    /// <paramref name="name"/> (the quest's own name; the wiki's search reads English); Garland Tools and Teamcraft by
    /// row id. Null for FFXIV Collect, which lists no quests.
    /// </summary>
    public static string? Quest(ExternalSite site, uint rowId, string name, ExternalIds ids, string language)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return site switch
        {
            ExternalSite.Lodestone => LodestoneQuest(ids.LodestoneId(rowId), language) ?? LodestoneSearch(name, language),
            ExternalSite.GarlandTools => GarlandQuest(rowId),
            ExternalSite.ConsoleGamesWiki => ids.WikiTitle(rowId) is { } title ? WikiPage(title) : WikiSearch(name),
            ExternalSite.Teamcraft => TeamcraftQuest(rowId, language),
            _ => null,
        };
    }

    /// <summary>
    /// The one link a copied line or table row carries: the quest's Lodestone page when the table names it, else its
    /// Garland Tools page (every row id has one).
    /// </summary>
    public static string Preferred(uint rowId, ExternalIds ids, string language)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return LodestoneQuest(ids.LodestoneId(rowId), language) ?? GarlandQuest(rowId);
    }

    /// <summary>The database hash of a Lodestone quest page URL (any region); false for any other URL. The link table's generator reads the verification CSV with it.</summary>
    public static bool TryParseLodestoneQuest(string? url, out string hash)
    {
        hash = string.Empty;
        const string Marker = ".finalfantasyxiv.com/lodestone/playguide/db/quest/";
        var at = url?.IndexOf(Marker, StringComparison.Ordinal) ?? -1;
        if (at < 0 || !url!.StartsWith("https://", StringComparison.Ordinal))
        {
            return false;
        }

        var candidate = url[(at + Marker.Length)..].TrimEnd('/');
        if (!ExternalIds.IsLodestoneHash(candidate))
        {
            return false;
        }

        hash = candidate;
        return true;
    }

    /// <summary>The page title of a Console Games Wiki page URL (percent-decoded, underscores as spaces); false for any other URL or a search.</summary>
    public static bool TryParseWikiPage(string? url, out string title)
    {
        title = string.Empty;
        if (url is null || !url.StartsWith(WikiBase, StringComparison.Ordinal))
        {
            return false;
        }

        var raw = url[WikiBase.Length..];
        if (raw.Length == 0 || raw.Contains('?', StringComparison.Ordinal) || raw.Contains('#', StringComparison.Ordinal)
            || raw.StartsWith("Special:", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            title = Uri.UnescapeDataString(raw).Replace('_', ' ').Trim();
        }
        catch (UriFormatException)
        {
            return false;
        }

        return title.Length > 0;
    }

    /// <summary>The kind path and id of an FFXIV Collect entry URL ("mounts", 23); false for any other URL.</summary>
    public static bool TryParseCollectEntry(string? url, out string path, out uint id)
    {
        path = string.Empty;
        id = 0;
        if (url is null || !url.StartsWith(CollectBase, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = url[CollectBase.Length..];
        var slash = rest.LastIndexOf('/');
        if (slash <= 0 || !uint.TryParse(rest.AsSpan(slash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out id) || id == 0)
        {
            id = 0;
            return false;
        }

        path = rest[..slash];
        return true;
    }

    /// <summary>The reward kind FFXIV Collect lists under <paramref name="path"/> (the reverse of <see cref="CollectPath"/>); null for none.</summary>
    public static RewardKind? CollectKind(string? path)
    {
        foreach (var kind in Enum.GetValues<RewardKind>())
        {
            if (CollectPath(kind) is { } known && string.Equals(known, path, StringComparison.Ordinal))
            {
                return kind;
            }
        }

        return null;
    }

    /// <summary>
    /// A URL made safe for a Markdown link target: the characters that would end or confuse one (spaces, parentheses,
    /// angle brackets) are percent-encoded. A URL with none of them is returned as is.
    /// </summary>
    public static string ForMarkdown(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (url.AsSpan().IndexOfAny(" ()<>") < 0)
        {
            return url;
        }

        var sb = new StringBuilder(url.Length + 8);
        foreach (var c in url)
        {
            sb.Append(c switch
            {
                ' ' => "%20",
                '(' => "%28",
                ')' => "%29",
                '<' => "%3C",
                '>' => "%3E",
                _ => c.ToString(),
            });
        }

        return sb.ToString();
    }
}
