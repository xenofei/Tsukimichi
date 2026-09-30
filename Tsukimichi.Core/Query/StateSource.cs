using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Query;

/// <summary>
/// Where a query or tree count reads a quest's state from. Implemented by value types so the generic core methods
/// bind statically and no adapter allocates; missing rows read as <see cref="QuestState.Unknown"/> with no status text.
/// </summary>
internal interface IStateSource
{
    QuestState StateOf(uint rowId);

    /// <summary>Whether the quest leaves done/total counts; see <see cref="QuestEvaluation.LeavesTotals"/>.</summary>
    bool LeavesTotals(uint rowId);

    /// <summary>Whether done/total counts the quest as done; see <see cref="QuestEvaluation.CountsAsDone"/>.</summary>
    bool CountsAsDone(uint rowId);

    /// <summary>The Status column text for a quest: <see cref="BlockerText.StatusText"/>, or empty when the quest has no evaluation.</summary>
    string StatusOf(QuestRecord quest);
}

/// <summary>Plain state map plus an optional status text map (the pre-evaluation shape).</summary>
internal readonly struct StateMapSource(IReadOnlyDictionary<uint, QuestState> states, IReadOnlyDictionary<uint, string>? statusTexts = null) : IStateSource
{
    public QuestState StateOf(uint rowId) => states.GetValueOrDefault(rowId, QuestState.Unknown);

    /// <summary>A plain state carries no requirement, so only <see cref="QuestState.Foreclosed"/> leaves the totals here.</summary>
    public bool LeavesTotals(uint rowId) => StateOf(rowId) == QuestState.Foreclosed;

    /// <summary>A plain state carries no completion history, so only <see cref="QuestState.Completed"/> is done here.</summary>
    public bool CountsAsDone(uint rowId) => StateOf(rowId) == QuestState.Completed;

    public string StatusOf(QuestRecord quest) => statusTexts?.GetValueOrDefault(quest.RowId) ?? string.Empty;
}

/// <summary>
/// Evaluator output: the state comes straight from each <see cref="QuestEvaluation"/>, the status text from
/// <see cref="BlockerText"/> with <paramref name="names"/> (null names quests only; counters that never read the text pass nothing).
/// </summary>
internal readonly struct EvaluationSource(IReadOnlyDictionary<uint, QuestEvaluation> evaluations, BlockerNames? names = null) : IStateSource
{
    public QuestState StateOf(uint rowId) =>
        evaluations.TryGetValue(rowId, out var evaluation) ? evaluation.State : QuestState.Unknown;

    public bool LeavesTotals(uint rowId) =>
        evaluations.TryGetValue(rowId, out var evaluation) && evaluation.LeavesTotals;

    public bool CountsAsDone(uint rowId) =>
        evaluations.TryGetValue(rowId, out var evaluation) && evaluation.CountsAsDone;

    public string StatusOf(QuestRecord quest) =>
        evaluations.TryGetValue(quest.RowId, out var evaluation) ? BlockerText.StatusText(evaluation, quest, names ?? BlockerNames.Default, evaluations) : string.Empty;
}
