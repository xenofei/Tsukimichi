using System.Globalization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Unique;

namespace Tsukimichi.Tests.Unique;

/// <summary>
/// The Moonlit table's column sort (owner request after 1.22.0): every column sorts by what it means, both directions,
/// ties keep the usual order, the third header click goes back to it, and a masked name sorts by what it prints.
/// </summary>
public class MoonlitSortTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static MoonlitSortKey Key(
        bool? obtained = null,
        string reward = "Reward",
        RewardKind kind = RewardKind.Item,
        string quest = "Quest",
        int maskedLevel = -1,
        QuestState state = QuestState.Ready,
        Confidence confidence = Confidence.Static,
        RewardAvailabilityInfo availability = default) =>
        new(obtained, reward, kind, quest, maskedLevel, state, confidence, availability);

    /// <summary>The row indices (0..n-1, the usual order) as the sort leaves them.</summary>
    private static int[] Sorted(MoonlitSortKey[] keys, MoonlitSortColumn column, bool descending = false)
    {
        var items = Enumerable.Range(0, keys.Length).ToList();
        MoonlitSort.Apply(items, i => keys[i], new MoonlitSortSpec(column, descending), Culture);
        return [.. items];
    }

    [Fact]
    public void The_usual_order_leaves_the_rows_as_they_are_whatever_the_direction()
    {
        MoonlitSortKey[] keys = [Key(reward: "Zeta"), Key(reward: "Alpha"), Key(reward: "Mu")];

        Assert.Equal([0, 1, 2], Sorted(keys, MoonlitSortColumn.Default));
        Assert.Equal([0, 1, 2], Sorted(keys, MoonlitSortColumn.Default, descending: true));
        Assert.True(MoonlitSortSpec.Default.IsDefault);
    }

    [Fact]
    public void Obtained_puts_owned_first_then_unreadable_then_missing_and_reverses()
    {
        MoonlitSortKey[] keys = [Key(obtained: false), Key(obtained: null), Key(obtained: true), Key(obtained: false), Key(obtained: true)];

        Assert.Equal([2, 4, 1, 0, 3], Sorted(keys, MoonlitSortColumn.Obtained));
        // Reversed, ties still keep the usual order: 0 before 3, 2 before 4.
        Assert.Equal([0, 3, 1, 2, 4], Sorted(keys, MoonlitSortColumn.Obtained, descending: true));
    }

    [Fact]
    public void Reward_sorts_by_name_ignoring_case_and_culture_aware()
    {
        MoonlitSortKey[] keys = [Key(reward: "cherry"), Key(reward: "Éclair"), Key(reward: "apple"), Key(reward: "Banana"), Key(reward: "Eden")];

        // Case does not split the names; "Éclair" sits with the e's, not after "z" as an ordinal compare would put it.
        Assert.Equal([2, 3, 0, 1, 4], Sorted(keys, MoonlitSortColumn.Reward));
        Assert.Equal([4, 1, 0, 3, 2], Sorted(keys, MoonlitSortColumn.Reward, descending: true));
    }

    [Fact]
    public void Quest_sorts_by_name_and_two_masked_quests_by_their_placeholder_level_as_a_number()
    {
        MoonlitSortKey[] keys =
        [
            Key(quest: "Main scenario quest (Lv 100)", maskedLevel: 100),
            Key(quest: "Main scenario quest (Lv 90)", maskedLevel: 90),
            Key(quest: "A Realm Reborn"),
            Key(quest: "the Ultimate Weapon"),
        ];

        // Lv 90 before Lv 100 (as text '1' < '9' would put 100 first); placeholders among real names as text.
        Assert.Equal([2, 1, 0, 3], Sorted(keys, MoonlitSortColumn.Quest));
        Assert.Equal([3, 0, 1, 2], Sorted(keys, MoonlitSortColumn.Quest, descending: true));
    }

    [Fact]
    public void Masked_names_do_not_leak_their_real_order()
    {
        // Rows 0 and 1 are masked: their real names ("Zenith" and "Aether") never reach the key, only the placeholder
        // they print. Equal placeholders tie and keep the usual order in both directions, so the real names' order
        // (Aether before Zenith) cannot show through.
        MoonlitSortKey[] keys =
        [
            Key(reward: "A mount", quest: "Main scenario quest (Lv 70)", maskedLevel: 70),
            Key(reward: "A mount", quest: "Main scenario quest (Lv 70)", maskedLevel: 70),
            Key(reward: "Chocobo Barding", quest: "Bird in Hand"),
        ];

        Assert.Equal([0, 1, 2], Sorted(keys, MoonlitSortColumn.Reward));
        Assert.Equal([2, 0, 1], Sorted(keys, MoonlitSortColumn.Reward, descending: true));
        Assert.Equal([2, 0, 1], Sorted(keys, MoonlitSortColumn.Quest));
        Assert.Equal([0, 1, 2], Sorted(keys, MoonlitSortColumn.Quest, descending: true));
    }

    [Fact]
    public void Kind_sorts_in_the_kinds_list_order_not_by_name()
    {
        // Mount comes before Achievement in RewardKind (the kinds list's order), though "A" < "M" by name.
        MoonlitSortKey[] keys = [Key(kind: RewardKind.Achievement), Key(kind: RewardKind.Mount), Key(kind: RewardKind.Item)];

        Assert.Equal([2, 1, 0], Sorted(keys, MoonlitSortColumn.Kind));
        Assert.Equal([0, 1, 2], Sorted(keys, MoonlitSortColumn.Kind, descending: true));
    }

    [Fact]
    public void State_sorts_in_the_quest_tables_state_order()
    {
        MoonlitSortKey[] keys =
        [
            Key(state: QuestState.Completed),
            Key(state: QuestState.Unknown),
            Key(state: QuestState.Ready),
            Key(state: QuestState.Blocked),
            Key(state: QuestState.Accepted),
        ];

        Assert.Equal([2, 4, 3, 0, 1], Sorted(keys, MoonlitSortColumn.State));
        Assert.Equal([1, 0, 3, 4, 2], Sorted(keys, MoonlitSortColumn.State, descending: true));
    }

    [Fact]
    public void Confidence_sorts_by_its_ordinal()
    {
        MoonlitSortKey[] keys = [Key(confidence: Confidence.UserOverride), Key(confidence: Confidence.Static), Key(confidence: Confidence.Curated), Key(confidence: Confidence.Community)];

        Assert.Equal([1, 3, 2, 0], Sorted(keys, MoonlitSortColumn.Confidence));
        Assert.Equal([0, 2, 3, 1], Sorted(keys, MoonlitSortColumn.Confidence, descending: true));
    }

    [Fact]
    public void Availability_sorts_by_kind_then_the_soonest_announced_end()
    {
        var soon = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
        var later = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        MoonlitSortKey[] keys =
        [
            Key(availability: new RewardAvailabilityInfo(RewardAvailability.GoneForGood)),
            Key(availability: new RewardAvailabilityInfo(RewardAvailability.EventRunning)),
            Key(availability: new RewardAvailabilityInfo(RewardAvailability.EventRunning, later)),
            Key(availability: new RewardAvailabilityInfo(RewardAvailability.GetNow)),
            Key(availability: new RewardAvailabilityInfo(RewardAvailability.EventRunning, soon)),
        ];

        // Get now, then the running events (soonest end first, an end not announced last), then gone for good.
        Assert.Equal([3, 4, 2, 1, 0], Sorted(keys, MoonlitSortColumn.Availability));
        Assert.Equal([0, 1, 2, 4, 3], Sorted(keys, MoonlitSortColumn.Availability, descending: true));
    }

    [Fact]
    public void Ties_keep_the_usual_order_in_both_directions()
    {
        MoonlitSortKey[] keys = [Key(reward: "Same"), Key(reward: "same"), Key(reward: "Other"), Key(reward: "SAME")];

        Assert.Equal([2, 0, 1, 3], Sorted(keys, MoonlitSortColumn.Reward));
        Assert.Equal([0, 1, 3, 2], Sorted(keys, MoonlitSortColumn.Reward, descending: true));
    }

    [Fact]
    public void Sorting_moves_the_rows_given_not_their_positions()
    {
        // The list holds row indices of a filtered view: they move, the indices themselves are kept.
        var keys = new Dictionary<int, MoonlitSortKey> { [7] = Key(reward: "B"), [3] = Key(reward: "C"), [12] = Key(reward: "A") };
        var items = new List<int> { 7, 3, 12 };

        MoonlitSort.Apply(items, i => keys[i], new MoonlitSortSpec(MoonlitSortColumn.Reward, false), Culture);

        Assert.Equal([12, 7, 3], items);
    }

    [Fact]
    public void Header_clicks_cycle_ascending_descending_and_back_to_the_usual_order()
    {
        // ImGui's tristate: the first click sorts ascending, the second descending, the third leaves no spec.
        Assert.Equal(new MoonlitSortSpec(MoonlitSortColumn.Reward, false), MoonlitSort.FromHeader(1, 1, descending: false));
        Assert.Equal(new MoonlitSortSpec(MoonlitSortColumn.Reward, true), MoonlitSort.FromHeader(1, 1, descending: true));
        Assert.Equal(MoonlitSortSpec.Default, MoonlitSort.FromHeader(0, 1, descending: true));

        MoonlitSortKey[] keys = [Key(reward: "Zeta"), Key(reward: "Alpha")];
        Assert.Equal([1, 0], Sorted(keys, MoonlitSort.FromHeader(1, 1, false).Column));
        var third = MoonlitSort.FromHeader(0, 1, false);
        Assert.Equal([0, 1], Sorted(keys, third.Column, third.Descending));
    }

    [Fact]
    public void Table_columns_map_to_sort_columns_and_back_in_declaration_order()
    {
        MoonlitSortColumn[] expected =
        [
            MoonlitSortColumn.Obtained,
            MoonlitSortColumn.Reward,
            MoonlitSortColumn.Kind,
            MoonlitSortColumn.Quest,
            MoonlitSortColumn.State,
            MoonlitSortColumn.Confidence,
            MoonlitSortColumn.Availability,
        ];

        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], MoonlitSort.ColumnAt(i));
            Assert.Equal(i, MoonlitSort.TableColumnOf(expected[i]));
        }

        Assert.Equal(MoonlitSortColumn.Default, MoonlitSort.ColumnAt(-1));
        Assert.Equal(MoonlitSortColumn.Default, MoonlitSort.ColumnAt(expected.Length));
        Assert.Equal(-1, MoonlitSort.TableColumnOf(MoonlitSortColumn.Default));
        Assert.Equal(MoonlitSortSpec.Default, MoonlitSort.FromHeader(1, 99, descending: true));
    }
}
