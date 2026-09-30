using System.Collections.Frozen;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Tsukimichi.GameData;

/// <summary>How <see cref="AetherCurrentQuests"/> settled on a current's awarding quest.</summary>
public enum AetherCurrentQuestSource : byte
{
    /// <summary><c>AetherCurrent.Quest</c> carries the Aether Current reward itself.</summary>
    Listed,

    /// <summary>The listed quest does not; the single one of its <c>PreviousQuest</c> entries that does.</summary>
    PreviousQuest,

    /// <summary>Neither does; a curated id from <see cref="AetherCurrentQuests.Overrides"/>.</summary>
    Override,

    /// <summary>Nothing carries the reward (A Realm Reborn's The Ultimate Weapon); the listed quest is kept.</summary>
    ListedUnflagged,
}

/// <summary>A quest current: the AetherCurrent row, the quest whose completion awards it, and the quest the sheet lists.</summary>
/// <param name="AetherCurrentId">AetherCurrent sheet row id.</param>
/// <param name="QuestRowId">The quest that awards the current.</param>
/// <param name="ListedQuestRowId">The quest <c>AetherCurrent.Quest</c> names, kept for diagnostics.</param>
/// <param name="Source">Which rule chose <paramref name="QuestRowId"/>.</param>
public readonly record struct AetherCurrentQuest(uint AetherCurrentId, uint QuestRowId, uint ListedQuestRowId, AetherCurrentQuestSource Source);

/// <summary>
/// Resolves the quest that awards each aether current. <c>AetherCurrent.Quest</c> names the wrong quest for five
/// currents (four Heavensward ones name the follow-up of the awarding quest, Thavnair's names an unrelated quest in the
/// zone), unchanged since 2017. The quests that really award a current carry <c>Quest.OtherReward</c> =
/// <see cref="OtherRewardAetherCurrent"/>; exactly 150 do. So: keep the listed quest when it carries that reward, else
/// take the single one of its <c>PreviousQuest</c> entries that does, else a curated override, else keep the listed
/// quest (The Ultimate Weapon, which opens flight in A Realm Reborn, carries no such reward). Shared by the Flight
/// index and DataGen's unique-reward data. See docs/data/v4/flight-currents.md.
/// </summary>
public static class AetherCurrentQuests
{
    /// <summary>QuestRewardOther row "Aether Current".</summary>
    public const uint OtherRewardAetherCurrent = 2;

    /// <summary>
    /// Curated awarding quests for currents neither the listed quest nor its prerequisites resolve: Thavnair's 2818328
    /// lists 70030 Curing What Ails, but In Agama's Footsteps (69793) awards it (the Console Games Wiki and Garland Tools agree).
    /// </summary>
    public static readonly FrozenDictionary<uint, uint> Overrides = new Dictionary<uint, uint>
    {
        [2818328] = 69793,
    }.ToFrozenDictionary();

    /// <summary>
    /// The rule on plain ids, for tests and callers without sheets. <paramref name="awardsCurrent"/> answers whether a
    /// quest carries the Aether Current reward; <paramref name="previousQuests"/> lists a quest's prerequisites.
    /// </summary>
    public static AetherCurrentQuest Resolve(
        uint aetherCurrentId,
        uint listedQuestRowId,
        Func<uint, bool> awardsCurrent,
        Func<uint, IEnumerable<uint>> previousQuests)
    {
        ArgumentNullException.ThrowIfNull(awardsCurrent);
        ArgumentNullException.ThrowIfNull(previousQuests);

        if (listedQuestRowId == 0 || awardsCurrent(listedQuestRowId))
        {
            return new AetherCurrentQuest(aetherCurrentId, listedQuestRowId, listedQuestRowId, AetherCurrentQuestSource.Listed);
        }

        uint single = 0;
        var count = 0;
        foreach (var previous in previousQuests(listedQuestRowId))
        {
            if (previous != 0 && previous != single && awardsCurrent(previous))
            {
                single = previous;
                count++;
            }
        }

        if (count == 1)
        {
            return new AetherCurrentQuest(aetherCurrentId, single, listedQuestRowId, AetherCurrentQuestSource.PreviousQuest);
        }

        if (Overrides.TryGetValue(aetherCurrentId, out var curated))
        {
            return new AetherCurrentQuest(aetherCurrentId, curated, listedQuestRowId, AetherCurrentQuestSource.Override);
        }

        return new AetherCurrentQuest(aetherCurrentId, listedQuestRowId, listedQuestRowId, AetherCurrentQuestSource.ListedUnflagged);
    }

    /// <summary>The awarding quest of one AetherCurrent row, read against the Quest sheet; null when the row lists no quest (a field current).</summary>
    public static AetherCurrentQuest? Resolve(AetherCurrent current, ExcelSheet<Quest> quests)
    {
        ArgumentNullException.ThrowIfNull(quests);
        if (current.Quest.RowId == 0)
        {
            return null;
        }

        return Resolve(
            current.RowId,
            current.Quest.RowId,
            id => quests.GetRowOrDefault(id)?.OtherReward.RowId == OtherRewardAetherCurrent,
            id => quests.GetRowOrDefault(id) is { } quest ? quest.PreviousQuest.Select(p => p.RowId) : []);
    }

    /// <summary>Every quest current in the AetherCurrent sheet, keyed by AetherCurrent row id.</summary>
    public static IReadOnlyDictionary<uint, AetherCurrentQuest> ResolveAll(ExcelSheet<AetherCurrent> currents, ExcelSheet<Quest> quests)
    {
        ArgumentNullException.ThrowIfNull(currents);
        ArgumentNullException.ThrowIfNull(quests);
        var resolved = new Dictionary<uint, AetherCurrentQuest>();
        foreach (var current in currents)
        {
            if (Resolve(current, quests) is { } quest)
            {
                resolved[current.RowId] = quest;
            }
        }

        return resolved;
    }
}
