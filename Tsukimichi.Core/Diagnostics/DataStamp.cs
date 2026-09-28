using System.Globalization;

namespace Tsukimichi.Core.Diagnostics;

/// <summary>
/// The data version stamp Settings › About and the status bar tooltip show (feature plan v3 T18), so support triage
/// asks "which stamp" instead of "which build": the game version the unique reward data was generated from, how
/// many entries, when, and the curated overlay's revision; plus the one-line warning when that game version is not
/// the one the client runs.
/// </summary>
public static class DataStamp
{
    public const string Prefix = "Data: game ";
    public const string Separator = " · ";
    private const string Unknown = "unknown";
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>"Data: game 2026.09.15 · unique rewards 3,464 (generated 2026-09-28) · curated 573d225".</summary>
    public static string Line(string gameVersion, int entries, DateTime? generatedUtc, string curatedRevision)
    {
        var line = Prefix + ShortGameVersion(gameVersion)
            + Separator + "unique rewards " + entries.ToString("N0", CultureInfo.InvariantCulture);
        if (generatedUtc is { } generated && generated != default)
        {
            line += " (generated " + generated.ToString(DateFormat, CultureInfo.InvariantCulture) + ")";
        }

        return line + Separator + "curated " + (string.IsNullOrWhiteSpace(curatedRevision) ? Unknown : curatedRevision);
    }

    /// <summary>
    /// "Reward data was generated for game X; you are on Y" when the two versions differ; null when they match or
    /// either is unknown (nothing to warn about yet).
    /// </summary>
    public static string? MismatchWarning(string dataGameVersion, string clientGameVersion)
    {
        if (string.IsNullOrWhiteSpace(dataGameVersion) || string.IsNullOrWhiteSpace(clientGameVersion)
            || string.Equals(dataGameVersion.Trim(), clientGameVersion.Trim(), StringComparison.Ordinal))
        {
            return null;
        }

        return "Reward data was generated for game " + ShortGameVersion(dataGameVersion) + "; you are on " + ShortGameVersion(clientGameVersion);
    }

    /// <summary>The date part of a game version: "2026.09.15.0000.0000" reads "2026.09.15"; shorter strings are left as they are.</summary>
    public static string ShortGameVersion(string gameVersion)
    {
        if (string.IsNullOrWhiteSpace(gameVersion))
        {
            return Unknown;
        }

        var trimmed = gameVersion.Trim();
        var third = -1;
        var dots = 0;
        for (var i = 0; i < trimmed.Length; i++)
        {
            if (trimmed[i] == '.' && ++dots == 3)
            {
                third = i;
                break;
            }
        }

        return third < 0 ? trimmed : trimmed[..third];
    }
}
