using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Tsukimichi.Verify.Verify;

/// <summary>
/// Canonical text for comparisons: private-use glyphs stripped, NFC, HTML entities decoded, typographic quotes and
/// dashes straightened, the wiki's <c>&amp;comma;</c> restored, whitespace collapsed, case folded. Display strings
/// keep their case; only <see cref="Canon"/> folds.
/// </summary>
internal static partial class Names
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();

    /// <summary>Human-readable cleanup without case folding.</summary>
    public static string Clean(string? s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.Empty;
        }

        var text = WebUtility.HtmlDecode(Tags().Replace(s, string.Empty));
        text = text.Replace("&comma;", ",", StringComparison.Ordinal);
        text = text.Normalize(NormalizationForm.FormC);
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch >= '' && ch <= '')
            {
                continue;
            }

            sb.Append(ch switch
            {
                '‘' or '’' or 'ʼ' => '\'',
                '“' or '”' => '"',
                '–' or '—' or '─' => '-',
                ' ' => ' ',
                _ => ch,
            });
        }

        return Whitespace().Replace(sb.ToString(), " ").Trim();
    }

    /// <summary>Comparison key.</summary>
    public static string Canon(string? s) => Clean(s).ToLowerInvariant();

    /// <summary>Sorted, distinct, canonical, joined with <c>;</c> for the CSV.</summary>
    public static string Join(IEnumerable<string> values)
        => string.Join(";", values.Select(Clean).Where(v => v.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(v => v, StringComparer.OrdinalIgnoreCase));

    public static string Join(IEnumerable<uint> ids) => string.Join(";", ids.Distinct().OrderBy(i => i));

    public static bool SameSet(IEnumerable<string> a, IEnumerable<string> b)
    {
        var sa = a.Select(Canon).Where(v => v.Length > 0).ToHashSet();
        var sb = b.Select(Canon).Where(v => v.Length > 0).ToHashSet();
        return sa.SetEquals(sb);
    }

    /// <summary>MediaWiki page title for a quest name: the API normalizes case and spaces, so only the characters MediaWiki forbids are handled.</summary>
    public static string WikiTitle(string name) => Clean(name).Replace('#', '-').Replace('|', '-').Replace('{', '(').Replace('}', ')').Replace('[', '(').Replace(']', ')');

    public static string TitleCase(string s) => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s);

    /// <summary>Name without a trailing parenthetical ("Close to Home (Gridania)" → "Close to Home"; "Ambulario (Item)" → "Ambulario").</summary>
    public static string Base(string name) => Regex.Replace(name, @"\s*\([^)]*\)\s*$", string.Empty);

    /// <summary>Comparison key with every non-ASCII letter dropped, for names the game spells with glyphs the wiki transliterates ("Α Test of Wιll").</summary>
    public static string AsciiKey(string? s)
    {
        var c = Canon(s);
        var sb = new StringBuilder(c.Length);
        foreach (var ch in c)
        {
            if (ch < 128)
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Loosest comparison key: case folded, Greek and Cyrillic look-alikes the game uses for glitch titles mapped to
    /// Latin ("Α Test of Wιll"), then everything but ASCII letters and digits dropped ("Best-laid Schemes" = "Best Laid
    /// Schemes", "Treasured_Bonds"). Used only after the exact and base-name lookups fail.
    /// </summary>
    public static string LooseKey(string? s)
    {
        var c = Canon(s);
        var sb = new StringBuilder(c.Length);
        foreach (var ch in c)
        {
            var mapped = ch switch
            {
                'α' or 'а' => 'a',
                'ε' or 'е' => 'e',
                'ι' or 'і' => 'i',
                'ο' or 'о' => 'o',
                'ρ' or 'р' => 'p',
                'τ' => 't',
                'υ' or 'у' => 'u',
                'ν' => 'v',
                'κ' or 'к' => 'k',
                'χ' or 'х' => 'x',
                'с' => 'c',
                _ => ch,
            };
            if (mapped is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(mapped);
            }
        }

        return sb.ToString();
    }
}

/// <summary>Duty names as the sources spell them versus ContentFinderCondition: leading "the", "(Duty)" suffixes and series names ("Frontline" for "the Borderland Ruins (Secure)").</summary>
internal static class DutyNames
{
    public static string Key(string s)
    {
        var k = Names.Canon(s).Replace("(duty)", string.Empty).Replace("  ", " ").Trim();
        return k.StartsWith("the ", StringComparison.Ordinal) ? k[4..] : k;
    }

    public static string Prefix(string s)
    {
        var k = Key(s);
        var paren = k.IndexOf('(');
        return (paren > 0 ? k[..paren] : k).Trim();
    }

    public static bool Same(string a, string b) => Key(a) == Key(b);

    /// <summary>
    /// Family names the wiki uses for a set of ContentFinderCondition rows that share no prefix with them: the PvP
    /// modes (Frontline, Rival Wings) and the Alexander tiers ("Alexander: Gordias" for the four "of the Father" turns).
    /// Keys and members are <see cref="Key"/> forms; a member matches when the duty key contains it.
    /// </summary>
    private static readonly Dictionary<string, string[]> Families = new(StringComparer.Ordinal)
    {
        ["frontline"] = ["borderland ruins", "seal rock", "fields of glory", "onsal hakair"],
        ["rival wings"] = ["astragalos", "hidden gorge"],
        ["alexander: gordias"] = ["of the father"],
        ["alexander: midas"] = ["of the son"],
        ["alexander: the creator"] = ["of the creator"],
    };

    /// <summary>True when one name is the series of the other ("Palace of the Dead" for "the Palace of the Dead (Floors 1-10)", "Frontline" for "the Borderland Ruins (Secure)").</summary>
    public static bool Series(string a, string b)
    {
        var pa = Prefix(a);
        var pb = Prefix(b);
        if (pa.Length > 0 && pb.Length > 0 && (pa.StartsWith(pb, StringComparison.Ordinal) || pb.StartsWith(pa, StringComparison.Ordinal)))
        {
            return true;
        }

        return InFamily(a, b) || InFamily(b, a);
    }

    private static bool InFamily(string family, string duty)
    {
        var f = Prefix(family);
        var d = Key(duty);
        var savage = Key(family).Contains("(savage)", StringComparison.Ordinal);
        return Families.TryGetValue(f, out var members) && members.Any(m => d.Contains(m, StringComparison.Ordinal)) && (!savage || d.Contains("(savage)", StringComparison.Ordinal));
    }
}
