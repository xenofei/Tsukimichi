using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Query;

/// <summary>
/// Where a query or tree count reads a quest's state from. Implemented by value types so the generic core methods
/// bind statically and no adapter allocates; missing rows read as <see cref="QuestState.Unknown"/> with no next step.
/// </summary>
internal interface IStateSource
{
    QuestState StateOf(uint rowId);

    string NextStepOf(uint rowId);

    /// <summary>Whether the quest leaves done/total counts; see <see cref="QuestEvaluation.LeavesTotals"/>.</summary>
    bool LeavesTotals(uint rowId);
}

/// <summary>Plain state map plus an optional next-step text map (the pre-evaluation shape).</summary>
internal readonly struct StateMapSource(IReadOnlyDictionary<uint, QuestState> states, IReadOnlyDictionary<uint, string>? nextSteps = null) : IStateSource
{
    public QuestState StateOf(uint rowId) => states.GetValueOrDefault(rowId, QuestState.Unknown);

    public string NextStepOf(uint rowId) => nextSteps?.GetValueOrDefault(rowId) ?? string.Empty;

    /// <summary>A plain state carries no requirement, so only <see cref="QuestState.Foreclosed"/> leaves the totals here.</summary>
    public bool LeavesTotals(uint rowId) => StateOf(rowId) == QuestState.Foreclosed;
}

/// <summary>Evaluator output: state and next-step detail come straight from each <see cref="QuestEvaluation"/>.</summary>
internal readonly struct EvaluationSource(IReadOnlyDictionary<uint, QuestEvaluation> evaluations) : IStateSource
{
    public QuestState StateOf(uint rowId) =>
        evaluations.TryGetValue(rowId, out var evaluation) ? evaluation.State : QuestState.Unknown;

    public string NextStepOf(uint rowId) =>
        evaluations.TryGetValue(rowId, out var evaluation) ? evaluation.NextStep?.Detail ?? string.Empty : string.Empty;

    public bool LeavesTotals(uint rowId) =>
        evaluations.TryGetValue(rowId, out var evaluation) && evaluation.LeavesTotals;
}
