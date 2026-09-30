using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Text;

/// <summary>
/// How much of a quest's journal the reader (P9) may show, so it never shows text the game has not shown the
/// character. The client's quest text sheet holds the journal as <c>SEQ_00</c>, <c>SEQ_01</c>, …: entry 0 when the
/// quest is taken, entry <c>n</c> once the quest reaches step <c>n</c>, and after the last step one more entry for the
/// turn-in. A completed quest shows everything. A quest in the journal at step <c>n</c> (its sequence, 255 meaning the
/// last step, <see cref="BlockerText.StepText"/>) shows entries 0 to <c>n</c> and the objectives of steps up to
/// <c>n</c>, never beyond. Anything else shows nothing: not even the first entry of a quest the character has not
/// taken, since that is text the game has not shown.
/// </summary>
public static class JournalVisibility
{
    /// <summary>No entry is shown.</summary>
    public const int None = -1;

    /// <summary>Every entry is shown.</summary>
    public const int All = int.MaxValue;

    /// <summary>States whose journal the game has shown in full: completed, or a repeatable done this cycle.</summary>
    public static bool IsCompleted(QuestState state) => state is QuestState.Completed or QuestState.DoneThisCycle;

    /// <summary>The reader is offered for this state: completed, or in the journal (up to the current step).</summary>
    public static bool IsReadable(QuestState state) => IsCompleted(state) || state == QuestState.Accepted;

    /// <summary>
    /// The current step of a quest in the journal: 1 to <paramref name="stepCount"/>, the final sequence (255) being
    /// the last; 0 when the sequence is unknown or 0. Without a step count the raw sequence is kept (the sheet may
    /// still hold that many entries; the reader shows only those).
    /// </summary>
    public static int CurrentStep(byte? sequence, byte stepCount)
    {
        if (sequence is not { } seq || seq == 0)
        {
            return 0;
        }

        if (stepCount == 0)
        {
            return seq == BlockerText.FinalSequence ? 0 : seq;
        }

        return seq == BlockerText.FinalSequence ? stepCount : Math.Clamp(seq, (byte)1, stepCount);
    }

    /// <summary>
    /// The highest journal entry index (<c>SEQ_nn</c>) the reader may show: <see cref="All"/> for a completed quest,
    /// the current step for one in the journal (0, the first entry only, when the step is unknown), and
    /// <see cref="None"/> otherwise.
    /// </summary>
    public static int VisibleThrough(QuestState state, byte? sequence, byte stepCount)
    {
        if (IsCompleted(state))
        {
            return All;
        }

        return state == QuestState.Accepted ? CurrentStep(sequence, stepCount) : None;
    }

    /// <summary>
    /// Whether an objective (<c>TODO_nn</c>) completed in journal sequence <paramref name="objectiveSequence"/>
    /// (<c>Quest.ToDoCompleteSeq</c>) may be shown. A completed quest shows every objective; one in the journal shows
    /// the objectives of its current step and the steps before. An objective whose sequence is unknown (0) is shown
    /// only for a completed quest.
    /// </summary>
    public static bool ObjectiveVisible(QuestState state, byte? sequence, byte stepCount, byte objectiveSequence)
    {
        if (IsCompleted(state))
        {
            return true;
        }

        if (state != QuestState.Accepted || objectiveSequence == 0)
        {
            return false;
        }

        var current = CurrentStep(sequence, stepCount);
        var step = CurrentStep(objectiveSequence, stepCount);
        return current > 0 && step > 0 && step <= current;
    }
}
