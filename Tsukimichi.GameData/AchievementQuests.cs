using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Tsukimichi.GameData;

/// <summary>
/// Which quests an achievement that names quests can honestly be credited to, and whether it reads as earned from quest
/// completion alone. Shared by DataGen's unique-reward data (which quest carries a Title or Achievement entry) and the
/// plugin's fallback when the game's achievement list is not loaded.
/// <para>
/// <c>Achievement.Type</c> decides what <c>Key</c> and <c>Data</c> mean:
/// <list type="bullet">
/// <item>6: complete every quest listed (Key plus Data). One quest is the common case; with several ("Tales of War",
/// "The War Still Wageth On") no single quest earns it, so it is credited only to the quest that has all the others
/// as prerequisites, and dropped when none does.</item>
/// <item>9: complete any one quest listed; each listed quest earns it.</item>
/// <item>24: obtain a relic weapon: Key is the relic step quest and Data[0] the job it must be done as. Completing the
/// quest once says nothing about the other jobs' weapons, so these are never credited to a quest.</item>
/// </list>
/// </para>
/// </summary>
public static class AchievementQuests
{
    /// <summary>Achievement type: complete every listed quest.</summary>
    public const byte TypeAllQuests = 6;

    /// <summary>Achievement type: complete any one listed quest.</summary>
    public const byte TypeAnyQuest = 9;

    /// <summary>Achievement type: obtain a relic weapon (quest in Key, job in Data[0]).</summary>
    public const byte TypeRelicWeapon = 24;

    /// <summary>
    /// The quests the achievement is credited to: none for a relic weapon achievement; for an all-of set, the one
    /// quest whose prerequisites (transitively, through <paramref name="previousQuests"/>) include every other quest
    /// in the set, or none; otherwise every listed quest.
    /// </summary>
    public static IReadOnlyList<uint> CreditedQuests(byte type, IReadOnlyList<uint> quests, Func<uint, IEnumerable<uint>> previousQuests)
    {
        ArgumentNullException.ThrowIfNull(quests);
        ArgumentNullException.ThrowIfNull(previousQuests);

        var distinct = quests.Where(q => q != 0).Distinct().ToList();
        if (type == TypeRelicWeapon || distinct.Count == 0)
        {
            return [];
        }

        if (type != TypeAllQuests || distinct.Count == 1)
        {
            return distinct;
        }

        foreach (var candidate in distinct)
        {
            var before = Prerequisites(candidate, previousQuests);
            if (distinct.All(q => q == candidate || before.Contains(q)))
            {
                return [candidate];
            }
        }

        return [];
    }

    /// <summary>
    /// Whether the achievement reads as earned from quest completion alone: every listed quest done (type 6), any one
    /// done (type 9); null when completion cannot tell (a relic weapon achievement needs the job, other types count
    /// something else).
    /// </summary>
    public static bool? EarnedFromQuests(byte type, IReadOnlyList<uint> quests, Func<uint, bool> isComplete)
    {
        ArgumentNullException.ThrowIfNull(quests);
        ArgumentNullException.ThrowIfNull(isComplete);

        var listed = quests.Where(q => q != 0).Distinct().ToList();
        if (listed.Count == 0)
        {
            return null;
        }

        return type switch
        {
            TypeAllQuests => listed.All(isComplete),
            TypeAnyQuest => listed.Any(isComplete),
            _ => null,
        };
    }

    /// <summary>
    /// The achievements that need several quests (feature plan v5, collector extras; R5 F6): every "complete every
    /// listed quest" achievement (<see cref="TypeAllQuests"/>) that names at least two quests, each of them a live quest
    /// of <paramref name="catalog"/> (an achievement naming a removed quest can no longer be followed). With the 7.x
    /// sheets five: Tales of War, Tales of Magic, Tales of the Hand, Tales of the Land and The War Still Wageth On.
    /// </summary>
    public static Core.Chains.AchievementLadders Ladders(ExcelModule excel, Language language, Core.Model.QuestCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(excel);
        ArgumentNullException.ThrowIfNull(catalog);
        var ladders = new List<Core.Chains.AchievementLadder>();
        foreach (var row in excel.GetSheet<Achievement>(language))
        {
            if (row.Type != TypeAllQuests)
            {
                continue;
            }

            var quests = QuestsOf(row);
            if (quests.Count < 2 || quests.Any(q => catalog.GetByRowId(q) is not { IsRemoved: false }))
            {
                continue;
            }

            var name = row.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(name))
            {
                ladders.Add(new Core.Chains.AchievementLadder(row.RowId, name, quests));
            }
        }

        return Core.Chains.AchievementLadders.Build(ladders);
    }

    /// <summary>The quest row ids an Achievement row names in <c>Key</c> and <c>Data</c>, in sheet order, each once.</summary>
    public static IReadOnlyList<uint> QuestsOf(Achievement achievement)
    {
        var quests = new List<uint>(2);
        if (achievement.Key.RowId != 0 && achievement.Key.Is<Quest>())
        {
            quests.Add(achievement.Key.RowId);
        }

        foreach (var data in achievement.Data)
        {
            if (data.RowId != 0 && data.Is<Quest>() && !quests.Contains(data.RowId))
            {
                quests.Add(data.RowId);
            }
        }

        return quests;
    }

    /// <summary><see cref="CreditedQuests(byte, IReadOnlyList{uint}, Func{uint, IEnumerable{uint}})"/> read against the Quest sheet.</summary>
    public static IReadOnlyList<uint> CreditedQuests(Achievement achievement, ExcelSheet<Quest> quests)
    {
        ArgumentNullException.ThrowIfNull(quests);
        return CreditedQuests(achievement.Type, QuestsOf(achievement), id => RequiredPrevious(quests, id));
    }

    /// <summary>
    /// The prerequisites a quest certainly needs: all of them under the "all" join, the lone one under the "any" join
    /// (the sheet stores single prerequisites that way), none when the "any" join offers a choice.
    /// </summary>
    private static IEnumerable<uint> RequiredPrevious(ExcelSheet<Quest> quests, uint id)
    {
        if (quests.GetRowOrDefault(id) is not { } quest)
        {
            return [];
        }

        var previous = quest.PreviousQuest.Select(p => p.RowId).Where(p => p != 0).ToList();
        return CatalogMapper.ToJoin(quest.PreviousQuestJoin) == Tsukimichi.Core.Model.JoinKind.Any && previous.Count > 1 ? [] : previous;
    }

    private static HashSet<uint> Prerequisites(uint quest, Func<uint, IEnumerable<uint>> previousQuests)
    {
        var seen = new HashSet<uint>();
        var stack = new Stack<uint>();
        stack.Push(quest);
        while (stack.Count > 0)
        {
            foreach (var previous in previousQuests(stack.Pop()))
            {
                if (previous != 0 && seen.Add(previous))
                {
                    stack.Push(previous);
                }
            }
        }

        return seen;
    }
}
