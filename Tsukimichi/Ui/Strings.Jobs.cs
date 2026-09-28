using Tsukimichi.Core.Jobs;

namespace Tsukimichi.Ui;

/// <summary>
/// Strings for the job ladder (V2-11): the dashboard's Job quests and Story chains sections, the level-up chat nudge
/// and its setting. Every constant is prefixed <c>Jobs</c> so the partial halves never collide.
/// </summary>
static partial class Strings
{
    // ---- Characters dashboard: Job quests ----
    public const string JobsSection = "Job quests";
    public const string JobsNone = "Job quests appear once the catalog is built and a character with job levels is evaluated.";
    public const string JobsColumnJob = "Job";
    public const string JobsColumnLevel = "Level";
    public const string JobsColumnDone = "Done";
    public const string JobsColumnNext = "Next";
    /// <summary>{0} = quest name, {1} = its level; the quest can be taken now.</summary>
    public const string JobsNextReadyFormat = "next: {0} · Lv {1}";
    /// <summary>{0} = quest name, {1} = its level; the quest is not open yet and nothing more precise is known.</summary>
    public const string JobsNextLaterFormat = "next: {0} · at Lv {1}";
    /// <summary>{0} = quest name, {1} = its decisive blocker (<see cref="Core.Evaluation.BlockerText"/>).</summary>
    public const string JobsNextBlockedFormat = "next: {0} · {1}";
    public const string JobsAllDone = "all done";
    /// <summary>{0} = role name.</summary>
    public const string JobsRoleRowFormat = "{0} role quests";
    /// <summary>{0} = done, {1} = total.</summary>
    public const string JobsCountFormat = "{0}/{1}";

    public static string JobsRoleName(JobRole role) => role switch
    {
        JobRole.Tank => "Tank",
        JobRole.Healer => "Healer",
        JobRole.Melee => "Melee DPS",
        JobRole.PhysicalRanged => "Physical ranged DPS",
        JobRole.MagicalRanged => "Magical ranged DPS",
        _ => "Role",
    };

    // ---- Characters dashboard: Story chains ----
    public const string JobsChainsSection = "Story chains";
    public const string JobsChainsNone = "Story chains appear once the catalog is built and a character is evaluated.";
    public const string JobsColumnChain = "Chain";
    /// <summary>{0} = done, {1} = total.</summary>
    public const string JobsChainCountFormat = "{0} of {1}";
    public const string JobsChainComplete = "complete";
    public const string JobsChainNextPrefix = "next: ";
    /// <summary>{0} = number of chains with nothing done yet.</summary>
    public const string JobsChainsNotStartedFormat = "Not started ({0})###notStartedChains";

    // ---- Chat: level-up nudge ----
    /// <summary>{0} = level reached, {1} = job name; followed by the quest link and <see cref="JobsNudgeSuffix"/>.</summary>
    public const string JobsNudgePrefixFormat = "Level {0} {1}: ";
    public const string JobsNudgeSuffix = " is available";
    /// <summary>After the quest link when the level was reached but the quest is still blocked and no blocker could be named.</summary>
    public const string JobsNudgeBlockedSuffix = " is not open yet";

    // ---- Settings › Notices ----
    public const string JobsConfigNudge = "Chat notice when a job or role quest becomes available after a level-up";
}
