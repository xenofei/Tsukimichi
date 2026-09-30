using System.Globalization;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Localization;

namespace Tsukimichi.Core.Diff;

/// <summary>One quest completed on one character and not on the other, with its unlock value and why it has that value.</summary>
/// <param name="Value">See <see cref="CharacterDiff.ValueOf"/>.</param>
/// <param name="Reason">Short text naming the parts of the value, e.g. "Feature quest · 2 unique rewards".</param>
public sealed record DiffEntry(uint RowId, int Value, string Reason);

/// <summary>How many quests of a journal section are done on only one side; sections with no difference are left out.</summary>
public sealed record DiffSectionCount(uint SectionId, string SectionName, int OnlyA, int OnlyB);

/// <summary>
/// What the diff needs beyond the two state maps: which quests are feature quests, how many unique rewards a quest
/// gives, and whether a quest is main scenario. All three are keyed by Quest sheet row id.
/// </summary>
public sealed record DiffContext(IReadOnlySet<uint> FeatureQuestIds, Func<uint, int> UniqueRewardCount, Func<uint, bool> IsMsq)
{
    /// <summary>A context whose main-scenario test follows the catalog: journal sections 0 and 1, unlisted rows excluded.</summary>
    public static DiffContext For(QuestCatalog catalog, IReadOnlySet<uint> featureQuestIds, Func<uint, int> uniqueRewardCount)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(featureQuestIds);
        ArgumentNullException.ThrowIfNull(uniqueRewardCount);
        return new DiffContext(featureQuestIds, uniqueRewardCount, rowId => catalog.GetByRowId(rowId) is { } quest && FeaturePresets.IsMainScenario(quest));
    }
}

/// <summary>
/// Result of <see cref="CharacterDiff.Compute"/>. <see cref="OnlyA"/> and <see cref="OnlyB"/> are sorted by value
/// descending, then journal order, so the quests worth doing first on the other character come on top.
/// </summary>
/// <param name="OnlyA">Completed on A, not on B (and not foreclosed on B).</param>
/// <param name="OnlyB">Completed on B, not on A (and not foreclosed on A).</param>
/// <param name="SharedDone">Completed on both.</param>
/// <param name="NeitherDone">Completed on neither and still open to at least one of them.</param>
/// <param name="Sections">Per-section counts of <see cref="OnlyA"/> and <see cref="OnlyB"/>, in section id order; only sections with a difference.</param>
public sealed record DiffResult(
    IReadOnlyList<DiffEntry> OnlyA,
    IReadOnlyList<DiffEntry> OnlyB,
    int SharedDone,
    int NeitherDone,
    IReadOnlyList<DiffSectionCount> Sections)
{
    public static readonly DiffResult Empty = new([], [], 0, 0, []);

    /// <summary>Positive when A has completed more of the differing quests than B.</summary>
    public int Lead => OnlyA.Count - OnlyB.Count;
}

/// <summary>
/// Alt diff (V2-12): the quests one character has completed and another has not, ranked by unlock value so the
/// player sees which unlocks the alt is missing first. Pure; the caller memoizes per pair of snapshots.
/// <para>
/// A quest is "done" when it counts as done (<see cref="QuestEvaluation.CountsAsDone"/>: completed, or an allied
/// society daily turned in before). A quest done on one side and <see cref="QuestState.Foreclosed"/> on the other is
/// not missing (a Grand Company choice not taken, a seasonal run the other character already saw) and is left out of
/// both lists. Only quests that enter the counts (<see cref="QuestRecord.EntersCounts"/>) enter the diff: unlisted
/// quests, progress trackers and repeatables other than allied society dailies never do.
/// </para>
/// </summary>
public static class CharacterDiff
{
    public const int BaseValue = 1;
    public const int MainScenarioValue = 3;
    public const int FeatureValue = 5;
    public const int UniqueRewardValue = 2;

    public static string ReasonMainScenario => CoreText.T("Core.Diff.MainScenario", "Main scenario");
    public static string ReasonFeature => CoreText.T("Core.Diff.Feature", "Unlock quest");
    public static string ReasonSide => CoreText.T("Core.Diff.Side", "Side quest");
    public const string ReasonSeparator = " · ";

    /// <summary>
    /// Unlock value of a quest: <see cref="BaseValue"/> for every quest, plus <see cref="MainScenarioValue"/> when it is
    /// main scenario, plus <see cref="FeatureValue"/> when it is a feature quest, plus <see cref="UniqueRewardValue"/> per
    /// unique reward.
    /// </summary>
    public static int ValueOf(uint rowId, DiffContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var value = BaseValue;
        if (ctx.IsMsq(rowId))
        {
            value += MainScenarioValue;
        }

        if (ctx.FeatureQuestIds.Contains(rowId))
        {
            value += FeatureValue;
        }

        return value + UniqueRewardValue * Math.Max(0, ctx.UniqueRewardCount(rowId));
    }

    /// <summary>The parts of <see cref="ValueOf"/> as text: "Main scenario", "Unlock quest", "N unique rewards", joined by " · "; "Side quest" when none apply.</summary>
    public static string ReasonOf(uint rowId, DiffContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var msq = ctx.IsMsq(rowId);
        var feature = ctx.FeatureQuestIds.Contains(rowId);
        var rewards = Math.Max(0, ctx.UniqueRewardCount(rowId));
        if (!msq && !feature && rewards == 0)
        {
            return ReasonSide;
        }

        var parts = new List<string>(3);
        if (msq)
        {
            parts.Add(ReasonMainScenario);
        }

        if (feature)
        {
            parts.Add(ReasonFeature);
        }

        if (rewards > 0)
        {
            parts.Add(string.Format(
                CultureInfo.CurrentCulture,
                rewards == 1 ? CoreText.T("Core.Diff.UniqueRewardOne", "{0} unique reward") : CoreText.T("Core.Diff.UniqueRewards", "{0} unique rewards"),
                rewards));
        }

        return string.Join(ReasonSeparator, parts);
    }

    /// <summary>Diffs two evaluations of the same catalog. Rows missing from a map read as <see cref="QuestState.Unknown"/>, which is not done.</summary>
    public static DiffResult Compute(
        QuestCatalog catalog,
        IReadOnlyDictionary<uint, QuestEvaluation> statesA,
        IReadOnlyDictionary<uint, QuestEvaluation> statesB,
        DiffContext ctx)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(statesA);
        ArgumentNullException.ThrowIfNull(statesB);
        ArgumentNullException.ThrowIfNull(ctx);

        if (catalog.Count == 0)
        {
            return DiffResult.Empty;
        }

        var onlyA = new List<Ranked>();
        var onlyB = new List<Ranked>();
        var sharedDone = 0;
        var neitherDone = 0;
        var sections = new SortedDictionary<uint, DiffSectionCount>();

        var all = catalog.All;
        for (var i = 0; i < all.Count; i++)
        {
            var quest = all[i];
            if (quest.IsRemoved || !quest.EntersCounts)
            {
                continue;
            }

            statesA.TryGetValue(quest.RowId, out var evaluationA);
            statesB.TryGetValue(quest.RowId, out var evaluationB);
            var stateA = evaluationA?.State ?? QuestState.Unknown;
            var stateB = evaluationB?.State ?? QuestState.Unknown;
            var doneA = evaluationA is { CountsAsDone: true };
            var doneB = evaluationB is { CountsAsDone: true };

            if (doneA && doneB)
            {
                sharedDone++;
                continue;
            }

            if (!doneA && !doneB)
            {
                // A quest neither can do (foreclosed for both, or out of season for both) is not pending for either.
                if (evaluationA is not { LeavesTotals: true } || evaluationB is not { LeavesTotals: true })
                {
                    neitherDone++;
                }

                continue;
            }

            // Exactly one side has it. Foreclosed on the other side means the other never could: not missing.
            var otherState = doneA ? stateB : stateA;
            if (otherState == QuestState.Foreclosed)
            {
                continue;
            }

            var entry = new DiffEntry(quest.RowId, ValueOf(quest.RowId, ctx), ReasonOf(quest.RowId, ctx));
            (doneA ? onlyA : onlyB).Add(new Ranked(entry, i));

            var sectionId = quest.Journal.SectionId;
            if (!sections.TryGetValue(sectionId, out var section))
            {
                section = new DiffSectionCount(sectionId, quest.Journal.SectionName, 0, 0);
            }

            sections[sectionId] = doneA
                ? section with { OnlyA = section.OnlyA + 1 }
                : section with { OnlyB = section.OnlyB + 1 };
        }

        var sectionRows = new DiffSectionCount[sections.Count];
        sections.Values.CopyTo(sectionRows, 0);

        return new DiffResult(Sort(onlyA), Sort(onlyB), sharedDone, neitherDone, sectionRows);
    }

    /// <summary>Value descending, then journal order (the catalog index), which makes the sort deterministic.</summary>
    private static DiffEntry[] Sort(List<Ranked> ranked)
    {
        ranked.Sort(static (x, y) =>
        {
            var byValue = y.Entry.Value.CompareTo(x.Entry.Value);
            return byValue != 0 ? byValue : x.Index.CompareTo(y.Index);
        });

        var result = new DiffEntry[ranked.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = ranked[i].Entry;
        }

        return result;
    }

    private readonly record struct Ranked(DiffEntry Entry, int Index);
}
