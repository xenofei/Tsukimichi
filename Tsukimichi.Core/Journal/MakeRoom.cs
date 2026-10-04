using System.Globalization;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Core.Journal;

/// <summary>What to do with one journal quest to free its slot (feature plan v7, C9).</summary>
public enum RoomAdvice : byte
{
    /// <summary>At its last step: handing it in frees the slot and loses nothing.</summary>
    HandIn,

    /// <summary>Barely started and easy to take again from its giver: abandoning it costs one conversation.</summary>
    SafeToDrop,

    /// <summary>Keep it: abandoning it loses progress, today's allowance, or the chance to take it again.</summary>
    Keep,
}

/// <summary>One journal quest with its advice and the reason, in plain words.</summary>
/// <param name="Quest">The catalog's quest; null for a quest the catalog does not know.</param>
/// <param name="Entry">The journal entry (quest id and step).</param>
public sealed record RoomRow(QuestRecord? Quest, AcceptedQuest Entry, RoomAdvice Advice, string Reason)
{
    /// <summary>"step 3 of 5", or empty when the step is unknown.</summary>
    public string StepText => Entry.Sequence == 0 ? string.Empty : BlockerText.StepText(Entry.Sequence, Quest?.StepCount ?? 0);
}

/// <summary>
/// "Make room" (feature plan v7, C9): the character's journal quests ranked by what freeing their slot costs. Hand in
/// first (a quest at its last step, the main scenario's included), then safe to drop, then keep. Safe to drop means all of: not the main scenario
/// (the game will not abandon it), at its first step (nothing done yet to lose), not repeatable (an allied society or
/// other repeatable quest keeps today's allowance spent), not seasonal (the event may end before it can be taken again)
/// and a giver the catalog knows (so it can be found again). Tsukimichi never abandons a quest; this is guidance, and
/// the Abandoned list keeps any quest the player drops. Pure.
/// </summary>
public static class MakeRoom
{
    /// <summary>The journal sequence the game gives a quest's last step.</summary>
    public const byte LastStep = 255;

    /// <summary>
    /// Every quest in <paramref name="snapshot"/>'s journal that holds a journal slot (allied society dailies are kept
    /// apart by the game, so they are left out), with its advice: hand in, then safe to drop, then keep; within a group
    /// the lowest level first, then by row id.
    /// </summary>
    public static List<RoomRow> Rank(CharacterSnapshot snapshot, QuestCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(catalog);
        var rows = new List<RoomRow>(snapshot.Accepted.Count);
        foreach (var entry in snapshot.Accepted)
        {
            var quest = catalog.GetByRowId(0x10000u | entry.QuestId);
            if (quest is { IsAlliedSocietyDaily: true })
            {
                continue;
            }

            var (advice, reason) = Advise(quest, entry);
            rows.Add(new RoomRow(quest, entry, advice, reason));
        }

        rows.Sort(static (a, b) =>
        {
            var c = a.Advice.CompareTo(b.Advice);
            if (c == 0)
            {
                c = (a.Quest?.Level ?? byte.MaxValue).CompareTo(b.Quest?.Level ?? byte.MaxValue);
            }

            return c != 0 ? c : (a.Quest?.RowId ?? (0x10000u | a.Entry.QuestId)).CompareTo(b.Quest?.RowId ?? (0x10000u | b.Entry.QuestId));
        });
        return rows;
    }

    /// <summary>The advice for one journal quest and the reason, in the order the rules are listed on <see cref="MakeRoom"/>.</summary>
    public static (RoomAdvice Advice, string Reason) Advise(QuestRecord? quest, AcceptedQuest entry)
    {
        if (quest is null)
        {
            return (RoomAdvice.Keep, CoreText.T("Core.MakeRoom.Unknown", "Not in Tsukimichi's quest list, so it can't say what dropping it costs."));
        }

        if (entry.Sequence == LastStep)
        {
            return (RoomAdvice.HandIn, CoreText.T("Core.MakeRoom.HandIn", "At its last step: hand it in to free the slot."));
        }

        if (FeaturePresets.IsMainScenario(quest))
        {
            return (RoomAdvice.Keep, CoreText.T("Core.MakeRoom.MainScenario", "Main scenario: the game won't let you abandon it."));
        }

        if (quest.IsRepeatable)
        {
            return (RoomAdvice.Keep, CoreText.T("Core.MakeRoom.Repeatable", "Repeatable: dropping it doesn't give back today's allowance."));
        }

        if (quest.Festival != 0)
        {
            return (RoomAdvice.Keep, CoreText.T("Core.MakeRoom.Seasonal", "Seasonal: the event may end before you can take it again."));
        }

        if (entry.Sequence > 1)
        {
            var step = BlockerText.StepText(entry.Sequence, quest.StepCount);
            return (RoomAdvice.Keep, string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.MakeRoom.Started", "Started ({0}): you'd do those steps again."), step));
        }

        if (quest.Issuer is not { Name.Length: > 0 } giver)
        {
            return (RoomAdvice.Keep, CoreText.T("Core.MakeRoom.NoGiver", "No known giver to take it from again."));
        }

        return (RoomAdvice.SafeToDrop, string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.MakeRoom.SafeToDrop", "Not started: take it again from {0} any time."), giver.Name));
    }
}
