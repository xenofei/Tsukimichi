using System.Globalization;
using System.Text;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ipc;

/// <summary>How Tsukimichi's Locked out compares with Questionable's <c>IsQuestUnobtainable</c>.</summary>
public enum UnobtainableOutcome
{
    /// <summary>Not compared: no answer, a stored character, or a state the gate does not speak to (done, in the journal, out of season, removed by the game).</summary>
    NotCompared,

    /// <summary>Both say it can still be done, or both say it cannot.</summary>
    Agrees,

    /// <summary>Questionable says it can no longer be done; Tsukimichi has it open (Ready or Blocked).</summary>
    QuestionableUnobtainable,

    /// <summary>Tsukimichi has it Locked out; Questionable says it can still be done.</summary>
    QuestionableObtainable,
}

/// <summary>How the seasonal events Tsukimichi sees running compare with Questionable's <c>GetCurrentlyActiveEventQuests</c> for one quest.</summary>
public enum EventOutcome
{
    /// <summary>Not an event quest for either, or no answer.</summary>
    NotCompared,

    /// <summary>Questionable lists it as an active event quest, and its event is running.</summary>
    Agrees,

    /// <summary>Questionable lists it as an active event quest; the game's festival flags say its event is not running.</summary>
    QuestionableListsInactive,

    /// <summary>Its event is running and Questionable does not list it (it leaves out a whole event once one of its quests is done, so this is not a disagreement).</summary>
    NotListed,
}

/// <summary>
/// The wider Questionable cross-check for one quest (feature plan v5, 1.6.0 F6), beside the lock comparison of
/// <see cref="QuestionableCrossCheck"/>: whether Questionable has a path, where the quest is on its list, its
/// unobtainable answer and its active-events answer.
/// </summary>
/// <param name="HasPath">From the reason gate (<see cref="QuestionableBadges.HasPath"/>); null when unknown (the fork).</param>
/// <param name="ListPosition">The quest's place on Questionable's priority list; null when it is not on it.</param>
/// <param name="ListKnown">The list was read (the export gate answered); without it <paramref name="ListPosition"/> says nothing.</param>
/// <param name="Unobtainable">Questionable's <c>IsQuestUnobtainable</c>; null when the gate is absent (the fork) or threw.</param>
/// <param name="UnobtainableOutcome">The comparison with Tsukimichi's Locked out.</param>
/// <param name="EventOutcome">The comparison of the active events.</param>
public sealed record QuestionableWider(
    bool? HasPath,
    int? ListPosition,
    bool ListKnown,
    bool? Unobtainable,
    UnobtainableOutcome UnobtainableOutcome,
    EventOutcome EventOutcome)
{
    /// <summary>One of the two comparisons differs.</summary>
    public bool Disagrees => UnobtainableOutcome is UnobtainableOutcome.QuestionableUnobtainable or UnobtainableOutcome.QuestionableObtainable
                             || EventOutcome == EventOutcome.QuestionableListsInactive;
}

/// <summary>
/// The comparisons behind <see cref="QuestionableWider"/>. Pure.
/// <para>
/// <c>QuestFunctions.IsQuestUnobtainable</c> (github.com/PunishXIV/Questionable commit
/// 0bd61efe8a6806a7a8010741c0c46b7dea153709; the WigglyMuffin fork does not register the gate) says unobtainable for a
/// quest of an expansion the account does not own, a quest whose exclusive quests are done (its quest locks), a class
/// quest of a locked class, a quest of another starting city, and a few starting-class quests. It throws for a quest
/// id it has no data for, which <c>Game.QuestionableIpc</c> reads as no answer. It does not look at whether the quest is
/// done, in the journal or out of season, so only Ready, Available on another job, Blocked and Locked out are compared,
/// and an unobtainable answer for a quest Tsukimichi holds back only by the account's expansion agrees.
/// </para>
/// <para>
/// <c>GetCurrentlyActiveEventQuests</c> lists the quests of the events in Questionable's own calendar
/// (<c>EventInfoComponent</c>, hand-entered end dates) while no quest of the event is done or unobtainable. Tsukimichi's
/// running events are the game's festival flags (<see cref="QuestRecord.Festival"/>). A quest Questionable lists while
/// its festival is not running is the one disagreement; the other way round is expected.
/// </para>
/// </summary>
public static class QuestionableWiderCheck
{
    /// <summary>
    /// Compares Questionable's unobtainable answer with Tsukimichi's state; <paramref name="live"/> false (a stored
    /// character) is never compared.
    /// </summary>
    public static UnobtainableOutcome CompareUnobtainable(QuestEvaluation? ours, bool? unobtainable, bool live = true)
    {
        if (!live || unobtainable is not { } theirs || ours is null || ours.IsOutOfSeason || HasUnmet(ours, RequirementKind.Retired))
        {
            return UnobtainableOutcome.NotCompared;
        }

        switch (ours.State)
        {
            case QuestState.Foreclosed:
                return theirs ? UnobtainableOutcome.Agrees : UnobtainableOutcome.QuestionableObtainable;
            case QuestState.Ready or QuestState.ReadyOnOtherJob or QuestState.Blocked:
                if (!theirs)
                {
                    return UnobtainableOutcome.Agrees;
                }

                // Questionable counts an expansion the account does not own as unobtainable; Tsukimichi blocks it on the cap.
                return ours.State == QuestState.Blocked && HasUnmet(ours, RequirementKind.ExpansionCap)
                    ? UnobtainableOutcome.Agrees
                    : UnobtainableOutcome.QuestionableUnobtainable;
            default:
                return UnobtainableOutcome.NotCompared;
        }
    }

    /// <summary>
    /// Compares one quest's event answers: <paramref name="festivalRunning"/> is whether the game's flags have the
    /// quest's festival running (false for a quest of no festival), <paramref name="listed"/> whether Questionable's
    /// active-events list holds it (null when the gate is absent or failed).
    /// </summary>
    public static EventOutcome CompareEvent(QuestRecord quest, bool festivalRunning, bool? listed)
    {
        ArgumentNullException.ThrowIfNull(quest);
        if (listed is not { } theirs)
        {
            return EventOutcome.NotCompared;
        }

        if (theirs)
        {
            return festivalRunning ? EventOutcome.Agrees : quest.Festival != 0 ? EventOutcome.QuestionableListsInactive : EventOutcome.NotCompared;
        }

        return festivalRunning && quest.Festival != 0 ? EventOutcome.NotListed : EventOutcome.NotCompared;
    }

    /// <summary>
    /// The quests of <paramref name="activeIds"/> (Questionable's element ids) as Quest row ids; the other kinds are
    /// left out.
    /// </summary>
    public static IReadOnlySet<uint> EventRowIds(IEnumerable<string>? activeIds)
    {
        var set = new HashSet<uint>();
        if (activeIds is null)
        {
            return set;
        }

        foreach (var id in activeIds)
        {
            if (QuestionableList.RowIdOf(id?.Trim()) is { } rowId)
            {
                set.Add(rowId);
            }
        }

        return set;
    }

    /// <summary>
    /// The diagnostic block's line after "questionable more: ", every part that is known:
    /// <code>
    /// path yes; list #3; unobtainable no, agrees; event listed, agrees
    /// path no; not on list; unobtainable yes, disagrees (tsukimichi Ready)
    /// </code>
    /// Empty when nothing is known.
    /// </summary>
    public static string DiagnosticText(QuestionableWider wider, QuestState? state)
    {
        ArgumentNullException.ThrowIfNull(wider);
        var parts = new List<string>(4);
        if (wider.HasPath is { } path)
        {
            parts.Add(path ? "path yes" : "path no");
        }

        if (wider.ListKnown)
        {
            parts.Add(wider.ListPosition is { } position ? "list #" + position.ToString(CultureInfo.InvariantCulture) : "not on list");
        }

        if (wider.Unobtainable is { } unobtainable)
        {
            var text = new StringBuilder("unobtainable ").Append(unobtainable ? "yes" : "no");
            switch (wider.UnobtainableOutcome)
            {
                case UnobtainableOutcome.Agrees:
                    text.Append(", agrees");
                    break;
                case UnobtainableOutcome.QuestionableUnobtainable or UnobtainableOutcome.QuestionableObtainable:
                    text.Append(", disagrees (tsukimichi ").Append(state?.ToString() ?? "none").Append(')');
                    break;
            }

            parts.Add(text.ToString());
        }

        switch (wider.EventOutcome)
        {
            case EventOutcome.Agrees:
                parts.Add("event listed, agrees");
                break;
            case EventOutcome.QuestionableListsInactive:
                parts.Add("event listed, disagrees (festival not running)");
                break;
            case EventOutcome.NotListed:
                parts.Add("event running, not listed");
                break;
        }

        return string.Join("; ", parts);
    }

    private static bool HasUnmet(QuestEvaluation evaluation, RequirementKind kind)
    {
        foreach (var result in evaluation.Requirements)
        {
            if (!result.Met && result.Req.Kind == kind)
            {
                return true;
            }
        }

        return false;
    }
}
