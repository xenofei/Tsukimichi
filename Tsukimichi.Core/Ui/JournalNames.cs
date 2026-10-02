using System.Text.RegularExpressions;
using Tsukimichi.Core.Localization;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// Short names for the Journal tree's nodes (feature plan v4 L3, UI audit §2, design v4 §7.2.3): the game's section,
/// category and genre names repeat their parent and a kind word ("Chronicles of a New Era - Eden", "Hildibrand
/// Sidequests", "Amalj'aa Quests"), which costs the tree half its width. <see cref="Short"/> strips what the row's
/// place in the tree already says; the full name and the journal path stay in the node's tooltip, and scope labels and
/// chips elsewhere keep the full names (the 40 "Main" / "Daily" genres are ambiguous without their parent).
/// <para>
/// English rules only: localization is frozen, so a client in another language keeps the game's full names. The
/// before/after table for every node is <c>docs/data/v4/journal-short-names.tsv</c>, which the tests check.
/// Pure and allocation-light; the tree calls it once per node when it builds its nodes, never per frame.
/// </para>
/// </summary>
public static class JournalNames
{
    /// <summary>Between a short name and its expansion abbreviation: "Main Scenario · ARR–EW", "Tank · ShB".</summary>
    public const string SuffixSeparator = " · ";

    /// <summary>Between the two ends of an expansion range: "ARR–EW".</summary>
    public const string RangeSeparator = "–";

    /// <summary>A genre that is its category again ("Omega Quests" under Omega) in a story section.</summary>
    public static string Story => CoreText.T("Core.Journal.Story", "Story");

    /// <summary>A genre that is its category again ("Studium Quests" under Studium) elsewhere.</summary>
    public static string General => CoreText.T("Core.Journal.General", "General");

    private const string ChroniclesSection = "Chronicles of a New Era";
    private const string SidequestsSection = "Sidequests";

    /// <summary>Names whose short form no rule gives, by the game's English name.</summary>
    private static readonly Dictionary<string, string> Overrides = new(StringComparer.Ordinal)
    {
        ["Weapon Enhancement Sidequests"] = "Relic Weapons",
        ["Records of Unusual Endeavors"] = "Unusual Endeavors",
        ["Side Story Quests"] = "Side Stories",
        ["Thavnairian Sidequests"] = "Thavnair",
        ["Order of the Twin Adder Quests"] = "Twin Adder",
        ["The Forbidden Land, Eureka"] = "Eureka",
        ["Collaboration Quests"] = "Collaborations",
    };

    /// <summary>Kind suffixes, longest first, and what each becomes.</summary>
    private static readonly (string Suffix, string Replacement)[] KindSuffixes =
    [
        (" Main Scenario Quests", string.Empty),
        (" Allied Society Quests", string.Empty),
        (" Main Quests", " (Main)"),
        (" Job Quests", " Jobs"),
        (" Sidequests", string.Empty),
        (" Quests", string.Empty),
        (" Events", string.Empty),
    ];

    /// <summary>What may follow a parent's name that a child's name starts with.</summary>
    private static readonly string[] PrefixSeparators = [" - ", ": ", " "];

    private static readonly string[] KindPrefixes = ["Facet of ", "Faculty of "];

    private static readonly Regex RoleGenre = new(@"^(?<role>.+) Role Quests \((?<exp>[^()]+)\)$", RegexOptions.CultureInvariant);

    private static readonly Regex Parenthesised = new(@"^(?<head>.+) \((?<inner>[^()]+)\)$", RegexOptions.CultureInvariant);

    /// <summary>Whether a catalog language (Lumina's name, "English", or a code, "en") gets the short names.</summary>
    public static bool IsEnglish(string? language) =>
        string.Equals(language, "English", StringComparison.OrdinalIgnoreCase)
        || string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The tree label for a journal node. Rules, in order: the override map; role genres ("Tank Role Quests
    /// (Shadowbringers)" → "Tank · ShB"); for a section, the expansion in parentheses as " · abbreviation" and
    /// "Allied Society Quests" as "Allied Societies"; the parent's full name as a prefix ("Chronicles of a New Era -
    /// Eden" → "Eden"); the kind suffix (Main Scenario Quests, Allied Society Quests, Sidequests, Quests, Events; "Main
    /// Quests" becomes "(Main)", "Job Quests" "Jobs", "Weapons" under the relic weapons), keeping a trailing "II"; the
    /// "Facet of" and "Faculty of" prefixes; the parent's short name as a prefix, or as a word inside the name
    /// ("Further Hildibrand Adventures" → "Further Adventures"). A child left equal to its parent becomes "Story" in the
    /// Chronicles, keeps the region's name under Sidequests (the tooltip tells the zone from the region), and becomes
    /// "General" elsewhere. Any other language returns <paramref name="name"/> unchanged.
    /// </summary>
    /// <param name="name">The node's full name as the game gives it.</param>
    /// <param name="parentName">The parent node's full name; null or empty for a section (a top-level node).</param>
    /// <param name="sectionName">The full name of the section the node is under (the node's own for a section).</param>
    /// <param name="language">The catalog's language (<see cref="IsEnglish"/>).</param>
    public static string Short(string name, string? parentName, string? sectionName, string? language)
    {
        if (string.IsNullOrEmpty(name) || !IsEnglish(language))
        {
            return name ?? string.Empty;
        }

        return ShortEnglish(name.Trim(), string.IsNullOrWhiteSpace(parentName) ? null : parentName.Trim(), sectionName?.Trim());
    }

    /// <summary>
    /// Splits an expansion suffix off a short name: "Main Scenario · ARR–EW" is ("Main Scenario", "ARR–EW"), so the
    /// tree can draw the suffix as a pill that yields to the name. A name without one is (<paramref name="shortName"/>, "").
    /// </summary>
    public static (string Head, string Suffix) SplitExpansion(string shortName)
    {
        ArgumentNullException.ThrowIfNull(shortName);
        var at = shortName.LastIndexOf(SuffixSeparator, StringComparison.Ordinal);
        if (at <= 0)
        {
            return (shortName, string.Empty);
        }

        var tail = shortName[(at + SuffixSeparator.Length)..];
        return IsExpansionText(tail) ? (shortName[..at], tail) : (shortName, string.Empty);
    }

    private static string ShortEnglish(string name, string? parent, string? section)
    {
        if (Overrides.TryGetValue(name, out var fixedName))
        {
            return fixedName;
        }

        if (RoleGenre.Match(name) is { Success: true } role && Abbreviation(role.Groups["exp"].Value) is { } roleExp)
        {
            return role.Groups["role"].Value + SuffixSeparator + roleExp;
        }

        if (parent is null)
        {
            return SectionShort(name);
        }

        // The parent's own short name: a category's parent is the section; a genre's is a category under the section.
        var parentIsSection = section is null || string.Equals(parent, section, StringComparison.Ordinal);
        var parentShort = parentIsSection ? ShortEnglish(parent, null, parent) : ShortEnglish(parent, section, section);

        var result = StripPrefix(name, parent) ?? name;

        var numeral = string.Empty;
        if (result.EndsWith(" II", StringComparison.Ordinal) && result.Length > 3)
        {
            numeral = " II";
            result = result[..^3];
        }

        result = StripKindSuffix(result, parent);
        foreach (var prefix in KindPrefixes)
        {
            if (result.StartsWith(prefix, StringComparison.Ordinal) && result.Length > prefix.Length)
            {
                result = result[prefix.Length..];
                break;
            }
        }

        result = StripPrefix(result, parentShort) ?? StripInnerWord(result, parentShort);
        result += numeral;

        if (!string.Equals(result, parentShort, StringComparison.OrdinalIgnoreCase))
        {
            return result;
        }

        if (string.Equals(section, SidequestsSection, StringComparison.Ordinal))
        {
            return result;
        }

        return section is not null && section.StartsWith(ChroniclesSection, StringComparison.Ordinal) ? Story : General;
    }

    /// <summary>A section: "Allied Society Quests (Dawntrail)" → "Allied Societies · DT"; "Other Quests" → "Other".</summary>
    private static string SectionShort(string name)
    {
        var head = name;
        var suffix = string.Empty;
        if (Parenthesised.Match(name) is { Success: true } match && Abbreviation(match.Groups["inner"].Value) is { } range)
        {
            head = match.Groups["head"].Value;
            suffix = SuffixSeparator + range;
        }

        if (head == "Allied Society Quests")
        {
            head = "Allied Societies";
        }

        return StripKindSuffix(head, parent: null) + suffix;
    }

    private static string StripKindSuffix(string name, string? parent)
    {
        foreach (var (suffix, replacement) in KindSuffixes)
        {
            // "Disciple of War Job Quests" → "Disciple of War Jobs", but the section "Class & Job Quests" → "Class & Job".
            if (parent is null && replacement.Length > 0)
            {
                continue;
            }

            if (name.EndsWith(suffix, StringComparison.Ordinal) && name.Length > suffix.Length)
            {
                return name[..^suffix.Length] + replacement;
            }
        }

        // "Zodiac Weapons" under the relic weapons; "Manderville Weapons" under Hildibrand keeps its word.
        const string Weapons = " Weapons";
        if (parent is not null && parent.Contains("Weapon", StringComparison.Ordinal)
            && name.EndsWith(Weapons, StringComparison.Ordinal) && name.Length > Weapons.Length)
        {
            return name[..^Weapons.Length];
        }

        return name;
    }

    /// <summary><paramref name="name"/> without <paramref name="prefix"/> and a separator after it, or null when it does not start so.</summary>
    private static string? StripPrefix(string name, string prefix)
    {
        if (prefix.Length == 0 || !name.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = name.AsSpan(prefix.Length);
        foreach (var separator in PrefixSeparators)
        {
            if (rest.StartsWith(separator, StringComparison.Ordinal) && rest.Length > separator.Length)
            {
                return rest[separator.Length..].ToString();
            }
        }

        return null;
    }

    /// <summary>A one-word parent name inside the name, neither first nor last, taken out: "Further Hildibrand Adventures".</summary>
    private static string StripInnerWord(string name, string word)
    {
        if (word.Length == 0 || word.Contains(' ', StringComparison.Ordinal))
        {
            return name;
        }

        var inner = " " + word + " ";
        var at = name.IndexOf(inner, StringComparison.Ordinal);
        return at > 0 ? string.Concat(name.AsSpan(0, at), " ", name.AsSpan(at + inner.Length)) : name;
    }

    /// <summary>"Shadowbringers" → "ShB"; "A Realm Reborn through Endwalker" → "ARR–EW"; anything else null.</summary>
    private static string? Abbreviation(string text)
    {
        const string Through = " through ";
        var at = text.IndexOf(Through, StringComparison.Ordinal);
        if (at > 0)
        {
            var from = Expansion(text[..at]);
            var to = Expansion(text[(at + Through.Length)..]);
            return from is not null && to is not null ? from + RangeSeparator + to : null;
        }

        return Expansion(text);
    }

    /// <summary>The short code of an expansion by its English name (<see cref="Evaluation.Expansions"/>, the one table); null for none.</summary>
    private static string? Expansion(string name) =>
        Evaluation.Expansions.FromEnglishName(name) is { } id ? Evaluation.Expansions.ShortCode(id) : null;

    private static bool IsExpansionText(string text)
    {
        var dash = text.IndexOf(RangeSeparator, StringComparison.Ordinal);
        return dash < 0 ? IsAbbreviation(text) : IsAbbreviation(text[..dash]) && IsAbbreviation(text[(dash + RangeSeparator.Length)..]);
    }

    private static bool IsAbbreviation(string text) => Evaluation.Expansions.IsShortCode(text);
}
