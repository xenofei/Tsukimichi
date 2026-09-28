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
}
