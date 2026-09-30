using System.Text;
using System.Text.RegularExpressions;

namespace Tsukimichi.Core.Localization;

/// <summary>
/// Pseudo-localization (V2-19): the language code "qps" is English stretched by at least 40 % and wrapped in brackets,
/// so a layout check shows where a long translation would be cut ("[Çháráçtérs ····]": the closing bracket must stay
/// visible) and any text that bypasses the resource tables stands out unbracketed. Placeholders, printf specifiers,
/// ImGui ids and combo separators are kept intact, so every format call and window id still works.
/// </summary>
public static partial class PseudoText
{
    /// <summary>How much longer a stretched string is than its English: German runs about a third longer.</summary>
    public const double Expansion = 0.4;

    /// <summary>The pad character that makes up the stretch.</summary>
    public const char Pad = '·';

    /// <summary>
    /// Keys whose values are read by code rather than shown (a culture name, .NET date formats, the translation's
    /// status); pseudo-localization leaves them English.
    /// </summary>
    public static bool IsMachineKey(string key) =>
        key is "Core.Culture" or "Core.Seasonal.DateFormat" or "Core.Seasonal.DateYearFormat" or "Meta.TranslationStatus";

    // Spans kept as they are: composite format items, printf specifiers, ImGui ids, the combo separator, line breaks.
    [GeneratedRegex(@"\{[^{}]*\}|%[-+#0]*\d*(?:\.\d+)?[dfsuxXi%]|##.*$|\0|\n", RegexOptions.Singleline)]
    private static partial Regex Protected();

    /// <summary>
    /// <paramref name="english"/> stretched: letters swap to accented look-alikes, a run of <see cref="Pad"/> brings the
    /// visible text to at least 1.4 times its length, and brackets close it. An empty string stays empty; an ImGui
    /// "##id" or "###id" suffix stays outside the brackets; NUL-separated combo items stretch one by one.
    /// </summary>
    public static string Stretch(string english)
    {
        ArgumentNullException.ThrowIfNull(english);
        if (english.Length == 0)
        {
            return english;
        }

        var visible = english;
        var id = string.Empty;
        var idAt = english.IndexOf("##", StringComparison.Ordinal);
        if (idAt >= 0)
        {
            visible = english[..idAt];
            id = english[idAt..];
        }

        if (visible.Length == 0)
        {
            return english;
        }

        if (visible.Contains('\0', StringComparison.Ordinal))
        {
            var parts = visible.Split('\0');
            for (var i = 0; i < parts.Length; i++)
            {
                parts[i] = parts[i].Length == 0 ? parts[i] : Stretch(parts[i]);
            }

            return string.Join('\0', parts) + id;
        }

        var sb = new StringBuilder(visible.Length * 2);
        sb.Append('[');
        var last = 0;
        foreach (Match match in Protected().Matches(visible))
        {
            Accent(sb, visible, last, match.Index);
            sb.Append(match.Value);
            last = match.Index + match.Length;
        }

        Accent(sb, visible, last, visible.Length);

        // "[", the text, " ", the pad and "]": at least 1.4 times the English, however short.
        var pad = Math.Max(1, (int)Math.Ceiling(visible.Length * Expansion) - 2);
        sb.Append(' ').Append(Pad, pad).Append(']');
        sb.Append(id);
        return sb.ToString();
    }

    private static void Accent(StringBuilder sb, string text, int from, int to)
    {
        for (var i = from; i < to; i++)
        {
            var c = text[i];
            sb.Append(c switch
            {
                'a' => 'á',
                'e' => 'é',
                'i' => 'í',
                'o' => 'ö',
                'u' => 'ü',
                'c' => 'ç',
                'n' => 'ñ',
                'y' => 'ý',
                'A' => 'Å',
                'E' => 'É',
                'I' => 'Î',
                'O' => 'Ø',
                'U' => 'Ü',
                'C' => 'Ç',
                'N' => 'Ñ',
                _ => c,
            });
        }
    }
}
