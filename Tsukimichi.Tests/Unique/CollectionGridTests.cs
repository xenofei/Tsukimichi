using Tsukimichi.Core.Model;
using Tsukimichi.Core.Unique;

namespace Tsukimichi.Tests.Unique;

/// <summary>"Who has it" (1.8.0, R7 C, R5 F2): collectibles × characters from the saved answers, and unlock quests × characters.</summary>
public sealed class CollectionGridTests
{
    private static UniqueRewardEntry Reward(uint quest, RewardKind kind, uint id, string name) =>
        new(quest, kind, id, 0, name, Confidence.Static, "test");

    private static CollectibleLookup Saved(params (RewardKind Kind, uint Id, bool Owned)[] answers)
    {
        var sets = new Dictionary<string, CollectibleSet>();
        foreach (var group in answers.GroupBy(static a => a.Kind))
        {
            sets[group.Key.ToString()] = new CollectibleSet
            {
                Owned = group.Where(static a => a.Owned).Select(static a => a.Id).ToList(),
                Missing = group.Where(static a => !a.Owned).Select(static a => a.Id).ToList(),
            };
        }

        return CollectibleLookup.For(new CharacterSnapshot { ContentId = 1, Collectibles = sets })!;
    }

    private static readonly UniqueRewardEntry[] Entries =
    [
        Reward(100, RewardKind.Mount, 5, "Zu"),
        Reward(101, RewardKind.Mount, 7, "Arrow"),
        Reward(102, RewardKind.Minion, 3, "Wind-up Cid"),
        Reward(103, RewardKind.Mount, 5, "Zu"),            // the same mount from another quest: one row
        Reward(104, RewardKind.SystemUnlock, 0, "Retainers"), // not a collectible
        Reward(105, RewardKind.Emote, 9, "Dance"),
    ];

    [Fact]
    public void Rows_are_the_saved_kinds_once_each_with_one_cell_per_character()
    {
        var main = Saved((RewardKind.Mount, 5, true), (RewardKind.Mount, 7, false), (RewardKind.Minion, 3, true));
        var alt = Saved((RewardKind.Mount, 5, false));

        var rows = CollectionGrid.Rewards(Entries, [main, alt, null]);

        Assert.Equal(["Dance", "Wind-up Cid", "Arrow", "Zu"], rows.Select(static r => r.Name));
        var zu = rows.Single(static r => r.Name == "Zu");
        Assert.Equal([OwnedCell.Owned, OwnedCell.Missing, OwnedCell.Unknown], zu.Cells);
        Assert.Equal(1, zu.OwnedCount);
        Assert.Equal(100u, zu.QuestRowId);
        Assert.Equal([OwnedCell.Unknown, OwnedCell.Unknown, OwnedCell.Unknown], rows.Single(static r => r.Name == "Dance").Cells);
    }

    [Fact]
    public void Kind_missing_on_any_and_search_filter_the_rows()
    {
        var main = Saved((RewardKind.Mount, 5, true), (RewardKind.Mount, 7, false), (RewardKind.Minion, 3, true));
        var alt = Saved((RewardKind.Mount, 5, true), (RewardKind.Minion, 3, true));

        Assert.Equal(["Arrow", "Zu"], CollectionGrid.Rewards(Entries, [main, alt], kind: RewardKind.Mount).Select(static r => r.Name));
        // Unknown never counts as missing.
        Assert.Equal(["Arrow"], CollectionGrid.Rewards(Entries, [main, alt], missingOnAny: true).Select(static r => r.Name));
        Assert.Equal(["Wind-up Cid"], CollectionGrid.Rewards(Entries, [main, alt], search: "cid wind").Select(static r => r.Name));
    }

    [Fact]
    public void Kinds_lists_the_collectible_kinds_present_in_fixed_order()
    {
        Assert.Equal([RewardKind.Emote, RewardKind.Minion, RewardKind.Mount], CollectionGrid.Kinds(Entries));
    }

    [Fact]
    public void Quest_rows_carry_each_characters_state_and_unresolved_columns_read_null()
    {
        var a = new QuestRecord { RowId = 1, Name = "Retainers" };
        var b = new QuestRecord { RowId = 2, Name = "Chocobo" };
        var main = new Dictionary<uint, QuestState> { [1] = QuestState.Completed, [2] = QuestState.Completed };
        var alt = new Dictionary<uint, QuestState> { [1] = QuestState.Ready };

        var rows = CollectionGrid.Quests([a, b], [main, alt, null], static q => q.Name);

        Assert.Equal([QuestState.Completed, QuestState.Ready, null], rows[0].Cells);
        Assert.Equal([QuestState.Completed, QuestState.Unknown, null], rows[1].Cells);
        Assert.Equal(1, rows[0].DoneCount);

        var open = CollectionGrid.Quests([a, b], [main, alt, null], static q => q.Name, notDoneOnAny: true);
        Assert.Equal(["Retainers"], open.Select(static r => r.Name));
        Assert.Equal(["Chocobo"], CollectionGrid.Quests([a, b], [main], static q => q.Name, search: "choco").Select(static r => r.Name));
    }
}
