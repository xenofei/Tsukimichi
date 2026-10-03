using System.Globalization;
using Tsukimichi.Core.Localization;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// The standing rule of feature plan v6, "show what is left, not tallies": a count column or caption says how many are
/// still to do ("3 left"), and "N of M done" moves into the tooltip (<see cref="Tally"/>). Phrases through
/// <see cref="CoreText"/> (<c>Core.Left.*</c>).
/// </summary>
public static class LeftText
{
    /// <summary>How many of <paramref name="total"/> are still to do, never below 0.</summary>
    public static int Count(int done, int total) => Math.Max(0, total - Math.Max(0, done));

    /// <summary>"3 left"; empty once nothing is left, so a finished row stays quiet.</summary>
    public static string Left(int done, int total)
    {
        var left = Count(done, total);
        return left == 0 ? string.Empty : F("Core.Left.Count", "{0:N0} left", left);
    }

    /// <summary>"3 left", or "all done" once nothing is (for a line that must always say something).</summary>
    public static string LeftOrDone(int done, int total) =>
        Count(done, total) == 0 ? CoreText.T("Core.Left.AllDone", "all done") : Left(done, total);

    /// <summary>The tally for a tooltip: "4 of 7 done".</summary>
    public static string Tally(int done, int total) => F("Core.Left.Tally", "{0:N0} of {1:N0} done", done, total);

    private static string F(string key, string english, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, CoreText.T(key, english), args);
}
