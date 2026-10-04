namespace Tsukimichi.Core.Ipc;

/// <summary>Where "Do this next" left the quest on Questionable's list, read back with <c>ExportQuestPriority</c>.</summary>
public enum QuestionableInsertKind
{
    /// <summary>The quest is first on the list.</summary>
    First,

    /// <summary>
    /// The quest was on the list already, further down: Questionable's <c>QuestPriorityManager.Insert</c> leaves a quest
    /// it holds where it is, so it stays at <see cref="QuestionableInsertOutcome.Position"/>.
    /// </summary>
    AlreadyThere,

    /// <summary>The quest is on the list but not first, and was not there before (another plugin moved the list meanwhile).</summary>
    Lower,

    /// <summary>The quest is not on the list afterwards: Questionable did not take it (no path, or it refused).</summary>
    Missing,

    /// <summary>The list could not be read back; the gate answered, nothing more is known.</summary>
    Unverified,
}

/// <summary>The outcome of "Do this next" and the quest's 1-based place on the list (0 when it is not there or not read).</summary>
public readonly record struct QuestionableInsertOutcome(QuestionableInsertKind Kind, int Position);

/// <summary>
/// "Do this next" (feature plan v7 A6): <c>Questionable.InsertQuestPriority(0, id)</c> answers true even for a quest
/// Questionable has no path for, and leaves a quest already on the list where it is, so the list is read before and
/// after and the place the quest holds afterwards is what the chat line reports. Pure.
/// </summary>
public static class QuestionableInsert
{
    /// <summary>The outcome for <paramref name="rowId"/> from the list before the insert (null when not read) and after it (null when not read back).</summary>
    public static QuestionableInsertOutcome Read(uint rowId, IReadOnlyList<string>? before, IReadOnlyList<string>? after)
    {
        if (after is null)
        {
            return new QuestionableInsertOutcome(QuestionableInsertKind.Unverified, 0);
        }

        if (!QuestionableList.Positions(after).TryGetValue(rowId, out var position))
        {
            return new QuestionableInsertOutcome(QuestionableInsertKind.Missing, 0);
        }

        if (position == 1)
        {
            return new QuestionableInsertOutcome(QuestionableInsertKind.First, 1);
        }

        var wasThere = before is not null && QuestionableList.Positions(before).ContainsKey(rowId);
        return new QuestionableInsertOutcome(wasThere ? QuestionableInsertKind.AlreadyThere : QuestionableInsertKind.Lower, position);
    }
}
