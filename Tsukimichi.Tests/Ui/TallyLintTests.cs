using System.Text.RegularExpressions;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The standing rule of feature plan v6 (owner point 8, "831 steps · 830 done" is weird): show what is left, not
/// tallies. No English string says "N of M done" or "Step N of M" unless it is a tooltip's tally or the number is the
/// point; those are listed here by key, so a new one is a decision, not an accident.
/// </summary>
public sealed class TallyLintTests
{
    // "{0} of {1} … done / completed / attuned" within one clause.
    private static readonly Regex DoneTally = new(@"\{\d[^}]*\}\s+of\s+\{\d[^}]*\}[^.·]*\b(done|completed|attuned)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "Step {0} of {1}".
    private static readonly Regex StepTally = new(@"\bsteps?\s+\{\d[^}]*\}\s+of\s+\{\d", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "{0} steps · {1} done": a total and a done count side by side.
    private static readonly Regex SideBySide = new(@"\{\d[^}]*\}[^{}.]*·\s*\{\d[^}]*\}\s+(done|completed)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static bool IsTally(string value) => DoneTally.IsMatch(value) || StepTally.IsMatch(value) || SideBySide.IsMatch(value);

    /// <summary>Tallies that only ever show in a tooltip, and positions where the number is the point.</summary>
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "Core.Path.EarlierDone",          // the Path header's hover
        "Core.Chain.Tally",               // the chain bar's hover
        "Core.Left.Tally",                // count cells' hover
        "FlightZoneTooltipFormat",        // a zone's hover
        "FlightZoneTooltipUnknownFormat", // a zone's hover
        "MsqProgressFormat",              // the status bar's hover
        "Core.Blocker.StepOf",            // where you are inside a quest in your journal
        "Tutorial.ProgressFormat",        // the tour's own page count
        "DiffCountsFormat",               // comparing two characters: the counts are the point
    };

    [Fact]
    public void No_English_string_tallies_what_is_done()
    {
        var offenders = ResxFiles.Load(string.Empty)
            .Where(static e => !Allowed.Contains(e.Key) && IsTally(e.Value))
            .Select(static e => $"{e.Key}: {e.Value}")
            .ToList();

        Assert.True(offenders.Count == 0, "Say what is left or what is next; put the tally in a tooltip:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Theory]
    [InlineData("{0} steps · {1} done", true)]
    [InlineData("Chain: {0} · {1} of {2} done", true)]
    [InlineData("Step {0} of {1} in {2}", true)]
    [InlineData("{0} of {1} prerequisites done, one needed", true)]
    [InlineData("{0:N0} of {1:N0} earlier quests done", true)]
    [InlineData("{0} quests before this one", false)]
    [InlineData("{0} of {1} left to do. Click to select the first one.", false)]
    public void The_patterns_catch_tallies(string value, bool tally)
    {
        Assert.Equal(tally, IsTally(value));
    }
}
