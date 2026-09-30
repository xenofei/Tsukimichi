using System.Globalization;

namespace Tsukimichi.Core.Model;

/// <summary>
/// Patch numbers as the game writes them: "2.0", "2.55", "3.07", "7.5", "7.51", "7.55". The part after the dot is a
/// decimal fraction, not an integer: 7.5 &lt; 7.51 &lt; 7.55 &lt; 7.6, and 3.07 &lt; 3.1. The first digit after the dot
/// names the series ("7.5x" holds 7.5, 7.51, 7.55); the rest are the series' follow-up patches. The empty string is
/// "unknown" and sorts before every patch; text that is not a patch number sorts after unknown and before every
/// patch, ordinal among itself.
/// </summary>
public static class PatchVersion
{
    /// <summary>Ascending order under <see cref="Compare"/>; newest last.</summary>
    public static IComparer<string> Comparer { get; } = new PatchComparer();

    /// <summary>Descending order: newest first, unknown last.</summary>
    public static IComparer<string> NewestFirst { get; } = Comparer<string>.Create((a, b) => Compare(b, a));

    /// <summary>Negative when <paramref name="a"/> is the older patch, zero when the same ("7.5" and "7.50"), positive when newer.</summary>
    public static int Compare(string? a, string? b)
    {
        var ka = Rank(a, out var ma, out var fa);
        var kb = Rank(b, out var mb, out var fb);
        if (ka != kb)
        {
            return ka.CompareTo(kb);
        }

        switch (ka)
        {
            case Kind.Unknown:
                return 0;
            case Kind.Other:
                return string.CompareOrdinal(a, b);
        }

        if (ma != mb)
        {
            return ma.CompareTo(mb);
        }

        // Fractions compare digit by digit once padded to the same length: "5" is 0.50, so 7.5 < 7.51.
        var width = Math.Max(fa.Length, fb.Length);
        return string.CompareOrdinal(fa.PadRight(width, '0'), fb.PadRight(width, '0'));
    }

    /// <summary>True for "major.digits" (one or more digits each side, nothing else).</summary>
    public static bool IsPatch(string? text) => Rank(text, out _, out _) == Kind.Patch;

    /// <summary>
    /// The series a patch belongs to: its major number and the first digit of the fraction ("7.55" → "7.5",
    /// "3.07" → "3.0", "2.0" → "2.0"). Empty for unknown or unparseable text.
    /// </summary>
    public static string Series(string? patch)
    {
        if (Rank(patch, out var major, out var fraction) != Kind.Patch)
        {
            return string.Empty;
        }

        return major.ToString(CultureInfo.InvariantCulture) + "." + fraction[0];
    }

    /// <summary>
    /// Whether <paramref name="patch"/> belongs to <paramref name="series"/> ("7.55" is in "7.5"; "7.5" is in "7.5";
    /// "17.5" is not). A full patch as the series matches itself and its longer spellings ("7.55" holds only 7.55x).
    /// Both are expected in <see cref="Normalize"/>d form; allocation-free, for the per-row filter.
    /// </summary>
    public static bool InSeries(string? patch, string? series) =>
        !string.IsNullOrEmpty(patch) && !string.IsNullOrEmpty(series) && patch.StartsWith(series, StringComparison.Ordinal);

    /// <summary>
    /// The canonical spelling: a lone major gains ".0" ("2" → "2.0", as a JSON number 2.0 reads back), trailing zeros
    /// past the first fraction digit go ("7.50" → "7.5"), surrounding space is trimmed. Unknown stays empty; text that
    /// is not a patch number is returned trimmed.
    /// </summary>
    public static string Normalize(string? text)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length > 0 && trimmed.All(char.IsAsciiDigit))
        {
            trimmed += ".0";
        }

        if (Rank(trimmed, out var major, out var fraction) != Kind.Patch)
        {
            return trimmed;
        }

        var kept = fraction.TrimEnd('0');
        if (kept.Length == 0)
        {
            kept = "0";
        }

        return major.ToString(CultureInfo.InvariantCulture) + "." + kept;
    }

    /// <summary>The newest patch in <paramref name="patches"/> (unknown ignored); empty when there is none.</summary>
    public static string Newest(IEnumerable<string> patches)
    {
        ArgumentNullException.ThrowIfNull(patches);
        var newest = string.Empty;
        foreach (var p in patches)
        {
            if (IsPatch(p) && (newest.Length == 0 || Compare(p, newest) > 0))
            {
                newest = p;
            }
        }

        return newest;
    }

    private enum Kind
    {
        Unknown = 0,
        Other = 1,
        Patch = 2,
    }

    private static Kind Rank(string? text, out int major, out string fraction)
    {
        major = 0;
        fraction = string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            return Kind.Unknown;
        }

        var dot = text.IndexOf('.', StringComparison.Ordinal);
        if (dot <= 0 || dot == text.Length - 1)
        {
            return Kind.Other;
        }

        var head = text.AsSpan(0, dot);
        var tail = text.AsSpan(dot + 1);
        foreach (var c in head)
        {
            if (!char.IsAsciiDigit(c))
            {
                return Kind.Other;
            }
        }

        foreach (var c in tail)
        {
            if (!char.IsAsciiDigit(c))
            {
                return Kind.Other;
            }
        }

        if (!int.TryParse(head, NumberStyles.None, CultureInfo.InvariantCulture, out major))
        {
            return Kind.Other;
        }

        fraction = tail.ToString();
        return Kind.Patch;
    }

    private sealed class PatchComparer : IComparer<string>
    {
        public int Compare(string? x, string? y) => PatchVersion.Compare(x, y);
    }
}
