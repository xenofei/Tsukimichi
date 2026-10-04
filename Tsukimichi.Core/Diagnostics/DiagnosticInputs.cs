using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Diagnostics;

/// <summary>
/// Everything <see cref="QuestDiagnostic.Compose"/> prints from, as plain values so a test can build one without the
/// plugin: the versions of the plugin, the client and the shipped data, the quest, its evaluation with the name
/// lookups the blocker line uses, and the character inputs the evaluation was made from. The composer reads only the
/// gameplay fields of <see cref="Snapshot"/>; its content id, name and world never reach the block.
/// </summary>
public sealed record DiagnosticInputs
{
    /// <summary>The filing text when the caller does not say (older callers and tests).</summary>
    public const string NoFilingRule = "n/a";

    /// <summary>
    /// The filing text for a quest: "sheet" when the sheet's own genre stands, "rule N" for a refiled quest, "retired
    /// (rule 1)" or "retired (curated)" for a removed one, "curated" for an override, followed by the filing mode
    /// ("rule 4, Refiled"; "sheet, Legacy"), so a bug report says which rule and which setting produced the genre. A
    /// hidden progress tracker reads "tracker (rule 9)".
    /// </summary>
    public static string DescribeFiling(QuestRecord quest, JournalFiling filing)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var rule = quest.RefiledFrom switch
        {
            0 => "sheet",
            1 => "retired (rule 1)",
            8 when quest.IsRetired => "retired (curated)",
            8 => "curated",
            QuestRecord.ProgressTrackerRule => "tracker (rule 9)",
            var n => "rule " + n.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        return rule + ", " + filing;
    }

    /// <summary>Assembly version of the plugin, e.g. "0.6.0.0".</summary>
    public string PluginVersion { get; init; } = string.Empty;

    /// <summary>The game version the client is running, from the ffxiv repository or <c>ffxivgame.ver</c>.</summary>
    public string ClientGameVersion { get; init; } = string.Empty;

    /// <summary>The game version <c>unique_quests.json</c> was generated from.</summary>
    public string DataGameVersion { get; init; } = string.Empty;

    /// <summary>When <c>unique_quests.json</c> was generated; null when the file carries no valid timestamp.</summary>
    public DateTime? DataGeneratedUtc { get; init; }

    /// <summary>Short git hash of the curated overlay (<c>curated/VERSION.json</c>); empty when the file is absent.</summary>
    public string CuratedRevision { get; init; } = string.Empty;

    /// <summary>Schema version of the character snapshot the inputs came from.</summary>
    public int SnapshotSchema { get; init; } = CharacterSnapshot.CurrentSchemaVersion;

    /// <summary>The quest being reported.</summary>
    public required QuestRecord Quest { get; init; }

    /// <summary>Which refiling rule placed the quest in its journal genre and under which filing mode (<see cref="DescribeFiling"/>); <see cref="NoFilingRule"/> when not given.</summary>
    public string FilingRule { get; init; } = NoFilingRule;

    /// <summary>The quest's evaluation for the viewed character; null when no character is viewed or the catalog is not built.</summary>
    public QuestEvaluation? Evaluation { get; init; }

    /// <summary>Name lookups for quests, tribes, ranks, jobs and duties; <see cref="BlockerNames.Default"/> prints ids.</summary>
    public BlockerNames Names { get; init; } = BlockerNames.Default;

    /// <summary>The curated quirk note for the quest (<c>curated/quirks.json</c>), printed as a "quirk:" line; null or empty when it has none.</summary>
    public string? QuirkNote { get; init; }

    /// <summary>Every quest's evaluation for the same character, when at hand: refines the blocker line and gives the main scenario position.</summary>
    public IReadOnlyDictionary<uint, QuestEvaluation>? States { get; init; }

    /// <summary>The character the evaluation was made from; null when none is viewed.</summary>
    public CharacterSnapshot? Snapshot { get; init; }

    /// <summary>The context the evaluation ran with: today's daily offer, mount and house, when known.</summary>
    public EvalContext Context { get; init; } = EvalContext.Default;

    /// <summary>True when <see cref="Snapshot"/> is the poller's live capture rather than a stored one.</summary>
    public bool IsLive { get; init; }

    /// <summary>
    /// Questionable's answer for the quest compared with Tsukimichi's (V2-17), printed as a "questionable:" line; null
    /// when Questionable is not loaded, which leaves the line out.
    /// </summary>
    public Ipc.CrossCheckResult? Questionable { get; init; }

    /// <summary>
    /// The wider Questionable cross-check (feature plan v5, 1.6.0): its path, the quest's place on its list, its
    /// unobtainable and active-events answers, printed as a "questionable more:" line; null, or nothing known, leaves
    /// the line out.
    /// </summary>
    public Ipc.QuestionableWider? QuestionableMore { get; init; }

    /// <summary>
    /// What the game's own offers say about the quest (feature plan v7, C1), printed as an "in game:" line; null, or a
    /// check with nothing to say, leaves the line out.
    /// </summary>
    public GameOfferCheck? GameOffer { get; init; }
}
