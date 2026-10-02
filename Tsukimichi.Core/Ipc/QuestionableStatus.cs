using System.Globalization;

namespace Tsukimichi.Core.Ipc;

/// <summary>
/// What Questionable is doing, as Tsukimichi shows it (feature plan v5, 1.6.0 live status): running or not, and the
/// quest and step it works on when that quest is an ordinary quest.
/// </summary>
/// <param name="Running">Questionable's <c>IsRunning</c>: its automation is on (any mode but manual) or a step runs.</param>
/// <param name="RowId">The Quest row id of its current quest; null when it has none or it is not a quest (an allied society daily "A12", an unlock link "U5"…).</param>
/// <param name="Sequence">The quest's journal sequence Questionable is on (255 the last); null without step data for the quest.</param>
/// <param name="Step">The step within the sequence, 0-based as Questionable counts; null without step data.</param>
/// <param name="TerritoryId">The territory of the step; 0 when unknown.</param>
public sealed record QuestionableStatus(bool Running, uint? RowId, byte? Sequence, int? Step, uint TerritoryId)
{
    /// <summary>Not running and nothing known: what an absent or silent Questionable reads as.</summary>
    public static readonly QuestionableStatus Idle = new(false, null, null, null, 0);

    /// <summary>
    /// The status from Questionable's three gates: <paramref name="running"/> (<c>IsRunning</c>), the current quest id
    /// (<c>GetCurrentQuestId</c>, a string such as "428" or "A12") and, when <c>GetCurrentStepData</c> answered, its
    /// quest id, sequence, step and territory. A non-numeric id is ignored (no row), and the step data only counts when
    /// it names the same quest.
    /// </summary>
    public static QuestionableStatus From(bool running, string? currentQuestId, string? stepQuestId, byte? sequence, int? step, uint territoryId)
    {
        var rowId = QuestionableList.RowIdOf(currentQuestId?.Trim());
        var stepRowId = QuestionableList.RowIdOf(stepQuestId?.Trim());
        if (rowId is null && stepRowId is not null && string.IsNullOrWhiteSpace(currentQuestId))
        {
            // The quest id gate is missing or said nothing while the step data names a quest: take the step data's.
            rowId = stepRowId;
        }

        if (rowId is null || stepRowId != rowId)
        {
            return new QuestionableStatus(running, rowId, null, null, 0);
        }

        return new QuestionableStatus(running, rowId, sequence, step, territoryId);
    }

    /// <summary>"428" for a status on quest row 65964, empty without one: for logs and the diagnostic block.</summary>
    public string QuestIdText => RowId is { } rowId ? (rowId & 0xFFFF).ToString(CultureInfo.InvariantCulture) : string.Empty;
}
