using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Journal;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Rewards;

namespace Tsukimichi.Core.Jobs;

/// <summary>A journal quest at its turn-in step whose EXP the current job would lose, with the advice that says so.</summary>
public sealed record CappedTurnIn(QuestRecord Quest, ExpAdvice Advice);

/// <summary>
/// "Turn in on a job that isn't capped" (feature plan v7, C8; spec-1.19 "The Todo row"): the quests in the character's
/// journal at their last step (sequence 255, <see cref="MakeRoom.LastStep"/>) that give a known amount of EXP while the
/// current job is at the character's level cap and another of its jobs that may hand the quest in would get some
/// (<see cref="ExpAdvisor"/>'s <see cref="ExpWarning.Capped"/>). Nothing when every such job is capped, the quest gives
/// no EXP or no known amount (allied society and seasonal quests), the current job may not take the quest (that is the
/// EXP line's "Hand in on", not a loss), or the snapshot lacks the current job or the level cap (a stored character
/// whose capture did not read them). Journal order. Pure.
/// </summary>
public static class CappedTurnIns
{
    /// <summary>The capped turn-ins of <paramref name="snapshot"/>'s journal, in journal order.</summary>
    /// <param name="isLimited">Whether a ClassJob row is a limited job (never suggested); null treats none as limited.</param>
    public static List<CappedTurnIn> Find(QuestCatalog catalog, CharacterSnapshot snapshot, QuestExpTable table, EvalContext context, Func<byte, bool>? isLimited = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(context);

        var found = new List<CappedTurnIn>();
        if (snapshot.CurrentJob == 0 || snapshot.LevelCap == 0)
        {
            return found;
        }

        foreach (var entry in snapshot.Accepted)
        {
            if (entry.Sequence != MakeRoom.LastStep || catalog.GetByQuestId(entry.QuestId) is not { } quest)
            {
                continue;
            }

            if (ExpAdvisor.Advise(quest, snapshot, table, context, isLimited) is { Warning: ExpWarning.Capped, Best.Exp: > 0 } advice)
            {
                found.Add(new CappedTurnIn(quest, advice));
            }
        }

        return found;
    }
}

/// <summary>
/// When the optional chat line for a capped turn-in speaks (feature plan v7, C8): once per quest per character each
/// time the quest reaches its turn-in step, the first scan that finds it capped. A quest is forgotten once a scan of
/// its character finds it no longer at its turn-in step (handed in, abandoned, or set back), so reaching the step again
/// speaks again; switching jobs back and forth while it waits does not. Not thread-safe; the plugin calls it on the
/// framework thread.
/// </summary>
public sealed class CappedTurnInNotice
{
    private readonly HashSet<(ulong Character, uint RowId)> said = [];

    /// <summary>
    /// The turn-ins of <paramref name="capped"/> not said yet for <paramref name="character"/>, now marked said; first
    /// forgets that character's quests <paramref name="snapshot"/> no longer holds at their turn-in step.
    /// </summary>
    public List<CappedTurnIn> Fresh(ulong character, CharacterSnapshot snapshot, IReadOnlyList<CappedTurnIn> capped)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(capped);

        if (said.Count > 0)
        {
            var atTurnIn = new HashSet<ushort>();
            foreach (var entry in snapshot.Accepted)
            {
                if (entry.Sequence == MakeRoom.LastStep)
                {
                    atTurnIn.Add(entry.QuestId);
                }
            }

            said.RemoveWhere(s => s.Character == character && !atTurnIn.Contains(QuestRecord.ToQuestId(s.RowId)));
        }

        var fresh = new List<CappedTurnIn>();
        foreach (var turnIn in capped)
        {
            if (said.Add((character, turnIn.Quest.RowId)))
            {
                fresh.Add(turnIn);
            }
        }

        return fresh;
    }
}
