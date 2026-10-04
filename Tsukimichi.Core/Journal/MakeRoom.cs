using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Core.Journal;

/// <summary>
/// The groups of Make room (spec-1.19 C9, "Make room"), in the order they show: what finishing a journal quest takes,
/// then the quests safe to drop.
/// </summary>
public enum RoomGroup : byte
{
    /// <summary>"Finish now": at its last step, which is a talk or a delivery of items the character holds.</summary>
    FinishNow,

    /// <summary>"Needs a duty": a solo duty, or one Duty Support or Trust fills with NPCs.</summary>
    NeedsDuty,

    /// <summary>"Needs an item": an item to hand in the character doesn't hold enough of yet.</summary>
    NeedsItem,

    /// <summary>"Needs a group": a duty with other players.</summary>
    NeedsGroup,

    /// <summary>"Safe to drop": still on its first step, with nothing above to say (<see cref="MakeRoom.IsSafeToDrop"/>).</summary>
    SafeToDrop,
}

/// <summary>One journal quest in Make room: its group, whether it is safe to drop, and what finishing it takes.</summary>
/// <param name="Quest">The catalog's quest.</param>
/// <param name="Entry">The journal entry (quest id and step).</param>
/// <param name="Group">The group it shows under.</param>
/// <param name="SafeToDrop">Whether it wears "safe to drop" (<see cref="MakeRoom.IsSafeToDrop"/>), in any group.</param>
public sealed record RoomEntry(QuestRecord Quest, AcceptedQuest Entry, RoomGroup Group, bool SafeToDrop)
{
    /// <summary>For <see cref="RoomGroup.FinishNow"/>: the last step hands items over (a delivery) rather than a talk.</summary>
    public bool IsDelivery { get; init; }

    /// <summary>For <see cref="RoomGroup.NeedsDuty"/> and <see cref="RoomGroup.NeedsGroup"/>: the duty.</summary>
    public DutyRunInfo? Duty { get; init; }

    /// <summary>For <see cref="RoomGroup.NeedsItem"/>: the first item the character lacks.</summary>
    public HandInItem? Item { get; init; }

    /// <summary>For <see cref="RoomGroup.NeedsItem"/>: how many of <see cref="Item"/> count toward the hand-in now.</summary>
    public int Held { get; init; }
}

/// <summary>
/// "Make room" (feature plan v7, C9; spec-1.19 C9): the character's journal quests grouped by what finishing them
/// takes (Finish now, Needs a duty, Needs an item, Needs a group), then the ones safe to drop. A quest the groups have
/// nothing to say about is left out: Make room lists the cheap ways to free a slot, never a guess. Tsukimichi never
/// abandons a quest; this is guidance, the game's own Abandon (and its confirmation) is the action, and the Abandoned
/// list keeps the step a dropped quest had reached. Pure.
/// </summary>
public static class MakeRoom
{
    /// <summary>The journal sequence the game gives a quest's last step.</summary>
    public const byte LastStep = 255;

    /// <summary>
    /// Every quest in <paramref name="snapshot"/>'s journal that holds a slot and that a group speaks for, in group
    /// order, then lowest level first, then by row id. Allied society dailies are kept apart by the game (no slot), and
    /// a quest the catalog does not know is left out.
    /// </summary>
    /// <param name="dutyOf">The duty a journal quest is cleared in (the one it unlocks); null, or a null answer, for none known.</param>
    /// <param name="held">How many of a hand-in item count toward it now; null, or a null answer, when it can't be counted
    /// (a stored character), which leaves the item out of the judgement.</param>
    public static List<RoomEntry> Plan(
        CharacterSnapshot snapshot,
        QuestCatalog catalog,
        Func<QuestRecord, DutyRunInfo?>? dutyOf = null,
        Func<HandInItem, int?>? held = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(catalog);
        var entries = new List<RoomEntry>(snapshot.Accepted.Count);
        foreach (var entry in snapshot.Accepted)
        {
            if (catalog.GetByRowId(0x10000u | entry.QuestId) is not { } quest || !JournalSlots.UsesSlot(quest))
            {
                continue;
            }

            if (Place(quest, entry, dutyOf, held) is { } placed)
            {
                entries.Add(placed);
            }
        }

        entries.Sort(static (a, b) =>
        {
            var c = a.Group.CompareTo(b.Group);
            if (c == 0)
            {
                c = a.Quest.Level.CompareTo(b.Quest.Level);
            }

            return c != 0 ? c : a.Quest.RowId.CompareTo(b.Quest.RowId);
        });
        return entries;
    }

    /// <summary>
    /// Whether dropping the quest costs nothing (spec-1.19 C9, "What safe to drop means"): it is on its first step, so
    /// the game's restart loses nothing; it is not the main scenario (the game won't abandon it); not a repeatable, an
    /// allied society quest or a seasonal one (dropping those can cost an allowance or the event); and its giver is
    /// known, so it can be taken again.
    /// </summary>
    public static bool IsSafeToDrop(QuestRecord quest, AcceptedQuest entry)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return entry.Sequence <= 1
            && !FeaturePresets.IsMainScenario(quest)
            && !quest.IsRepeatable
            && quest.BeastTribe == 0
            && quest.Festival == 0
            && quest.Issuer is { Name.Length: > 0 };
    }

    /// <summary>The group one journal quest shows under; null when no group speaks for it.</summary>
    private static RoomEntry? Place(QuestRecord quest, AcceptedQuest entry, Func<QuestRecord, DutyRunInfo?>? dutyOf, Func<HandInItem, int?>? held)
    {
        var safe = IsSafeToDrop(quest, entry);

        // An item to hand in comes before the last step: a delivery the character can't make yet can't be finished now.
        if (held is not null && Lacking(quest, held) is { } lacking)
        {
            return new RoomEntry(quest, entry, RoomGroup.NeedsItem, safe) { Item = lacking.Item, Held = lacking.Held };
        }

        if (entry.Sequence == LastStep)
        {
            return new RoomEntry(quest, entry, RoomGroup.FinishNow, safe) { IsDelivery = quest.HandInItems.Count > 0 };
        }

        if (dutyOf?.Invoke(quest) is { } duty)
        {
            var group = DutyClear.WithoutOthers(duty) ? RoomGroup.NeedsDuty : RoomGroup.NeedsGroup;
            return new RoomEntry(quest, entry, group, safe) { Duty = duty };
        }

        return safe ? new RoomEntry(quest, entry, RoomGroup.SafeToDrop, true) : null;
    }

    /// <summary>
    /// The first hand-in item the character holds too few of, with the count. An item every job hands in is judged on
    /// its own; items meant for some jobs only (a miner's ore beside a botanist's log) only together, since one of them
    /// is the character's: they lack an item only when they lack every one. An item that can't be counted says nothing.
    /// </summary>
    private static (HandInItem Item, int Held)? Lacking(QuestRecord quest, Func<HandInItem, int?> held)
    {
        (HandInItem Item, int Held)? firstJobItem = null;
        var jobItems = 0;
        var jobItemsLacking = 0;
        foreach (var item in quest.HandInItems)
        {
            var count = held(item);
            if (item.ClassJobCategories.Length == 0)
            {
                if (count is { } c && c < item.Needed)
                {
                    return (item, Math.Max(0, c));
                }

                continue;
            }

            jobItems++;
            if (count is { } own && own < item.Needed)
            {
                jobItemsLacking++;
                firstJobItem ??= (item, Math.Max(0, own));
            }
        }

        return jobItems > 0 && jobItemsLacking == jobItems ? firstJobItem : null;
    }
}
