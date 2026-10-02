using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Runtime;

/// <summary>
/// The Wotsit registration (B7): Wotsit keeps the first 26 fuzzy matches per plugin in registration order, so the
/// entries match on their name alone and the quests a player is after are registered first.
/// </summary>
public sealed class WotsitOrderTests
{
    private const uint Gridania = 1;
    private const uint Mercy = 2;
    private const uint Hunt = 3;
    private const uint Retired = 4;
    private const uint Finale = 5;

    // Journal order: 1, 2, 3, 5 (4 has no journal genre, so it is removed from the game).
    private static readonly QuestCatalog Catalog = QuestCatalog.Build(
    [
        Quest(Gridania, "Coming to Gridania", section: 0, category: 1, genre: 1, sortKey: 10, level: 1),
        Quest(Mercy, "The Gift of Mercy", section: 0, category: 1, genre: 1, sortKey: 20, level: 83, expansion: 4),
        Quest(Hunt, "Hunt Billing", section: 2, category: 10, genre: 100, sortKey: 30, level: 50, expansion: 1),
        Quest(Retired, "Old Hunt Billing", genre: 0, sortKey: 40),
        Quest(Finale, "Secret Finale", section: 0, category: 1, genre: 1, sortKey: 50, level: 100, expansion: 5),
    ]);

    private static readonly UniqueRewardCatalog Rewards = UniqueRewardCatalog.Build(
        new UniqueRewardsData("1.0", default,
        [
            new UniqueRewardEntry(Hunt, RewardKind.Minion, 30, 0, "Wind-up Cid", Confidence.Static, "test"),
            new UniqueRewardEntry(Mercy, RewardKind.Emote, 20, 0, "", Confidence.Static, "test"),
        ]),
        new Dictionary<uint, UniqueOverride>(),
        CuratedData.Empty);

    private static string KindName(RewardKind kind) => kind.ToString();

    private static List<WotsitItem> Items(SpoilerMask? spoilers = null) =>
        WotsitOrder.Items(Catalog, Rewards, KindName, RewardNames.ShippedLanguage, spoilers);

    private static Dictionary<uint, QuestEvaluation> Evaluated(params (uint RowId, QuestState State)[] states) =>
        states.ToDictionary(s => s.RowId, s => new QuestEvaluation(s.State, [], null, null, null));

    private static string[] Names(IReadOnlyList<WotsitItem> items, IEnumerable<int> order) => order.Select(i => items[i].Name).ToArray();

    [Fact]
    public void A_quest_matches_on_its_name_alone()
    {
        var items = Items();

        var mercy = Assert.Single(items, i => !i.IsReward && i.Quest.RowId == Mercy);
        Assert.Equal("The Gift of Mercy", mercy.SearchText);
        Assert.Equal("The Gift of Mercy", mercy.Name);

        // No genre or expansion word: "Genre 1" or "Endwalker" would let every quest of that genre match a short query.
        Assert.All(items.Where(i => !i.IsReward), i => Assert.Equal(i.Quest.Name, i.SearchText));
        Assert.DoesNotContain(items, i => i.SearchText.Contains("Genre", StringComparison.Ordinal));
    }

    [Fact]
    public void A_reward_matches_on_its_own_name_not_its_quest_or_kind()
    {
        var items = Items();

        var cid = Assert.Single(items, i => i.Reward is { Kind: RewardKind.Minion });
        Assert.Equal("Wind-up Cid", cid.SearchText);
        Assert.Equal(Hunt, cid.Quest.RowId);

        // No reward name: the kind is the name it shows, and so the one it matches.
        var emote = Assert.Single(items, i => i.Reward is { Kind: RewardKind.Emote });
        Assert.Equal("Emote", emote.SearchText);
        Assert.Equal("Emote", emote.Name);
    }

    [Fact]
    public void Removed_quests_are_skipped_and_quests_come_before_rewards_in_catalog_order()
    {
        var items = Items();

        Assert.Equal(["Coming to Gridania", "The Gift of Mercy", "Hunt Billing", "Secret Finale", "Wind-up Cid", "Emote"], items.Select(i => i.Name));
    }

    [Fact]
    public void A_masked_quest_matches_on_its_placeholder_only()
    {
        var states = States((Gridania, QuestState.Ready), (Mercy, QuestState.Blocked), (Finale, QuestState.Blocked));
        var mask = SpoilerMask.Build(Catalog, states, SpoilerOptions.Default with { Ahead = 0 });
        Assert.True(mask.IsMasked(Finale));

        var finale = Assert.Single(Items(mask), i => !i.IsReward && i.Quest.RowId == Finale);

        Assert.Equal(mask.DisplayName(finale.Quest), finale.SearchText);
        Assert.DoesNotContain("Secret", finale.SearchText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(QuestState.Accepted, WotsitPriority.Active)]
    [InlineData(QuestState.Ready, WotsitPriority.Active)]
    [InlineData(QuestState.ReadyOnOtherJob, WotsitPriority.Active)]
    [InlineData(QuestState.Blocked, WotsitPriority.Open)]
    [InlineData(QuestState.DoneThisCycle, WotsitPriority.Open)]
    [InlineData(QuestState.Unknown, WotsitPriority.Open)]
    [InlineData(QuestState.Completed, WotsitPriority.Finished)]
    [InlineData(QuestState.Foreclosed, WotsitPriority.Finished)]
    public void Each_state_falls_in_its_group(QuestState state, WotsitPriority expected)
    {
        Assert.Equal(expected, WotsitOrder.ForState(state));
    }

    [Fact]
    public void Every_state_has_a_group_and_a_missing_one_counts_as_open()
    {
        foreach (var state in Enum.GetValues<QuestState>())
        {
            Assert.True(Enum.IsDefined(WotsitOrder.ForState(state)));
        }

        Assert.Equal(WotsitPriority.Open, WotsitOrder.ForState(null));
    }

    [Fact]
    public void In_journal_and_Ready_come_first_then_rewards_then_open_then_finished()
    {
        var items = Items();
        var states = Evaluated((Gridania, QuestState.Completed), (Mercy, QuestState.Blocked), (Hunt, QuestState.Foreclosed), (Finale, QuestState.Accepted));

        var priorities = WotsitOrder.Priorities(items, i => i, states);
        var order = WotsitOrder.Order(priorities);

        Assert.Equal(
            ["Secret Finale", "Wind-up Cid", "Emote", "The Gift of Mercy", "Coming to Gridania", "Hunt Billing"],
            Names(items, order));
    }

    [Fact]
    public void Within_a_group_the_catalog_order_is_kept()
    {
        var items = Items();
        var states = Evaluated((Gridania, QuestState.Ready), (Mercy, QuestState.ReadyOnOtherJob), (Hunt, QuestState.Accepted), (Finale, QuestState.Ready));

        var order = WotsitOrder.Order(WotsitOrder.Priorities(items, i => i, states));

        Assert.Equal(["Coming to Gridania", "The Gift of Mercy", "Hunt Billing", "Secret Finale", "Wind-up Cid", "Emote"], Names(items, order));
    }

    [Fact]
    public void Before_any_state_is_known_rewards_lead_and_quests_follow_in_catalog_order()
    {
        var items = Items();

        var order = WotsitOrder.Order(WotsitOrder.Unevaluated(items, i => i));

        Assert.Equal(["Wind-up Cid", "Emote", "Coming to Gridania", "The Gift of Mercy", "Hunt Billing", "Secret Finale"], Names(items, order));
    }

    [Fact]
    public void Only_a_move_to_another_group_asks_for_a_new_order()
    {
        var items = Items();
        var before = WotsitOrder.Priorities(items, i => i, Evaluated((Gridania, QuestState.Ready), (Mercy, QuestState.Blocked)));

        // Accepting a Ready quest, or a daily done for today: same groups, nothing to re-register.
        var accepted = WotsitOrder.Priorities(items, i => i, Evaluated((Gridania, QuestState.Accepted), (Mercy, QuestState.DoneThisCycle)));
        Assert.False(WotsitOrder.GroupsChanged(before, accepted));

        // Turning it in moves it to Finished.
        var completed = WotsitOrder.Priorities(items, i => i, Evaluated((Gridania, QuestState.Completed), (Mercy, QuestState.Blocked)));
        Assert.True(WotsitOrder.GroupsChanged(before, completed));

        // A list of another shape is always a change.
        Assert.True(WotsitOrder.GroupsChanged(before, before[..^1]));
    }

    [Fact]
    public void A_quest_that_becomes_Ready_is_re_registered_ahead_of_the_rewards()
    {
        var items = Items();
        var registry = new AppendOnlyRegistry(items.Count);
        var before = WotsitOrder.Priorities(items, i => i, Evaluated((Gridania, QuestState.Ready), (Mercy, QuestState.Blocked), (Hunt, QuestState.Completed), (Finale, QuestState.Blocked)));
        registry.RegisterAll(WotsitOrder.Order(before));

        // The Gift of Mercy unlocks: it moves from Open to Active, ahead of both rewards.
        var after = WotsitOrder.Priorities(items, i => i, Evaluated((Gridania, QuestState.Ready), (Mercy, QuestState.Ready), (Hunt, QuestState.Completed), (Finale, QuestState.Blocked)));
        Assert.True(WotsitOrder.GroupsChanged(before, after));
        var order = WotsitOrder.Order(after);
        var kept = RegistrationDiff.KeptPrefix(order, registry.Sequence, _ => true);
        registry.Replace(order.Skip(kept));

        // Coming to Gridania and The Gift of Mercy already sit in that order; the rewards and what follows them move
        // behind The Gift of Mercy.
        Assert.Equal(2, kept);
        Assert.Equal(Names(items, order), Names(items, registry.Order));
        Assert.Equal(["Coming to Gridania", "The Gift of Mercy", "Wind-up Cid", "Emote", "Secret Finale", "Hunt Billing"], Names(items, registry.Order));
    }

    /// <summary>Wotsit's per-plugin list as far as order goes: registering appends; unregistering removes.</summary>
    private sealed class AppendOnlyRegistry(int count)
    {
        private long next;

        public long[] Sequence { get; } = new long[count];

        public IEnumerable<int> Order => Enumerable.Range(0, Sequence.Length).Where(p => Sequence[p] > 0).OrderBy(p => Sequence[p]);

        public void RegisterAll(IEnumerable<int> positions)
        {
            foreach (var position in positions)
            {
                Sequence[position] = ++next;
            }
        }

        public void Replace(IEnumerable<int> positions)
        {
            foreach (var position in positions)
            {
                Sequence[position] = 0;
                Sequence[position] = ++next;
            }
        }
    }
}
