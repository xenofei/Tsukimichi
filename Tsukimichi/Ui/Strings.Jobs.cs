using Tsukimichi.Core.Jobs;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Strings for the job ladder (V2-11): the dashboard's Job quests and Story chains sections, the level-up chat nudge
/// and its setting. Every constant is prefixed <c>Jobs</c> so the partial halves never collide.
/// </summary>
static partial class Strings
{
    // ---- Characters dashboard: Job quests ----
    public static string JobsSection => Loc.Get("JobsSection");
    public static string JobsNone => Loc.Get("JobsNone");
    public static string JobsColumnJob => Loc.Get("JobsColumnJob");
    public static string JobsColumnLevel => Loc.Get("JobsColumnLevel");
    public static string JobsColumnDone => Loc.Get("JobsColumnDone");
    public static string JobsColumnNext => Loc.Get("JobsColumnNext");
    /// <summary>{0} = quest name, {1} = its level; the quest can be taken now.</summary>
    public static string JobsNextReadyFormat => Loc.Get("JobsNextReadyFormat");
    /// <summary>{0} = quest name, {1} = its level; the quest is not open yet and nothing more precise is known.</summary>
    public static string JobsNextLaterFormat => Loc.Get("JobsNextLaterFormat");
    /// <summary>{0} = quest name, {1} = its decisive blocker (<see cref="Core.Evaluation.BlockerText"/>).</summary>
    public static string JobsNextBlockedFormat => Loc.Get("JobsNextBlockedFormat");
    public static string JobsAllDone => Loc.Get("JobsAllDone");
    /// <summary>{0} = role name.</summary>
    public static string JobsRoleRowFormat => Loc.Get("JobsRoleRowFormat");
    /// <summary>{0} = done, {1} = total.</summary>
    public const string JobsCountFormat = "{0}/{1}";

    public static string JobsRoleName(JobRole role) => role switch
    {
        JobRole.Tank => Loc.Get("JobsRoleName.Tank"),
        JobRole.Healer => Loc.Get("JobsRoleName.Healer"),
        JobRole.Melee => Loc.Get("JobsRoleName.Melee"),
        JobRole.PhysicalRanged => Loc.Get("JobsRoleName.PhysicalRanged"),
        JobRole.MagicalRanged => Loc.Get("JobsRoleName.MagicalRanged"),
        _ => Loc.Get("JobsRoleName.Default"),
    };

    // ---- Characters dashboard: Story chains ----
    public static string JobsChainsSection => Loc.Get("JobsChainsSection");
    public static string JobsChainsNone => Loc.Get("JobsChainsNone");
    public static string JobsColumnChain => Loc.Get("JobsColumnChain");
    /// <summary>{0} = done, {1} = total.</summary>
    public static string JobsChainCountFormat => Loc.Get("JobsChainCountFormat");
    public static string JobsChainComplete => Loc.Get("JobsChainComplete");
    /// <summary>{0} = the next quest of the chain.</summary>
    public static string JobsChainNextFormat => Loc.Get("JobsChainNextFormat");
    /// <summary>{0} = number of chains with nothing done yet.</summary>
    public static string JobsChainsNotStartedFormat => Loc.Get("JobsChainsNotStartedFormat");

    // ---- Chat: level-up nudge ----
    /// <summary>{0} = level reached, {1} = job name; followed by the quest link and <see cref="JobsNudgeSuffix"/>.</summary>
    public static string JobsNudgeFormat => Loc.Get("JobsNudgeFormat");
    public static string JobsNudgeBlockerFormat => Loc.Get("JobsNudgeBlockerFormat");
    /// <summary>After the quest link when the level was reached but the quest is still blocked and no blocker could be named.</summary>
    public static string JobsNudgeBlockedFormat => Loc.Get("JobsNudgeBlockedFormat");

    // ---- Settings › Notices ----
    public static string JobsConfigNudge => Loc.Get("JobsConfigNudge");
}
