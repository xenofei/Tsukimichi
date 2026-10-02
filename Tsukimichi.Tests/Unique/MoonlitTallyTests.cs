using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Unique;

namespace Tsukimichi.Tests.Unique;

/// <summary>
/// Feature plan v5, decision 4: a reward several quests give counts once, quests on another path are hidden, a relic or
/// special weapon quest counts once with its repeatable twin, and gone-for-good rewards leave the totals unless counted.
/// </summary>
public class MoonlitTallyTests
{
    private const uint Limsa = 66968;
    private const uint Gridania = 66969;
    private const uint Uldah = 66970;
    private const uint ClassA = 65612;
    private const uint ClassB = 65691;
    private const uint ClassC = 65801;
    private const uint Relic = 69380;
    private const uint RelicAgain = 69381;
    private const uint OtherRelic = 69506;
    private const uint MountQuest = 66001;
    private const uint EventQuest = 66002;

    private static UniqueRewardEntry Entry(uint quest, RewardKind kind, uint rewardId, string name, uint itemId = 0, string source = "test") =>
        new(quest, kind, rewardId, itemId, name, Confidence.Static, source);

    private static UniqueRewardEntry Unlock(uint quest, string label) =>
        new(quest, RewardKind.SystemUnlock, 0, 0, label, Confidence.Curated, "curated/system_unlocks.json");

    private static UniqueRewardEntry Gear(uint quest, uint item, string name) => Entry(quest, RewardKind.ArtifactGear, item, name, item);

    private static readonly UniqueRewardEntry[] Entries =
    [
        Unlock(Limsa, "Retainers"),
        Unlock(Gridania, "Retainers"),
        Unlock(Uldah, "retainers "),
        Entry(ClassA, RewardKind.Achievement, 313, "This One Time, at Level Thirty..."),
        Entry(ClassB, RewardKind.Achievement, 313, "This One Time, at Level Thirty..."),
        Entry(ClassC, RewardKind.Achievement, 313, "This One Time, at Level Thirty..."),
        Gear(Relic, 1001, "Honorbound"),
        Gear(Relic, 1002, "Samsara"),
        Gear(Relic, 1003, "Skullrender"),
        Gear(RelicAgain, 1001, "Honorbound"),
        Gear(RelicAgain, 1002, "Samsara"),
        Gear(RelicAgain, 1003, "Skullrender"),
        Gear(OtherRelic, 2001, "Augmented Honorbound"),
        Gear(OtherRelic, 2002, "Augmented Samsara"),
        Entry(MountQuest, RewardKind.Mount, 10, "Company Chocobo"),
        Entry(EventQuest, RewardKind.Minion, 30, "Wind-up Moogle"),
    ];

    private static readonly MoonlitGroups Groups = MoonlitGroups.Build(Entries, id => id == RelicAgain);

    private static MoonlitGroup GroupOf(uint quest, RewardKind kind) => Groups.All.Single(g => g.Kind == kind && g.Quests.Contains(quest));

    private static QuestEvaluation State(QuestState state) => new(state, [], null, null, null);

    private static QuestEvaluation OtherPath()
    {
        var path = new RequirementResult(new OtherPathRequirement(PathKind.StartCity, [], []), false, "another city");
        return new QuestEvaluation(QuestState.Foreclosed, [path], path, null, null);
    }

    private static RewardAvailabilityInfo GetNow(UniqueRewardEntry entry, QuestEvaluation? evaluation) => new(RewardAvailability.GetNow);

    [Fact]
    public void Entries_of_the_same_reward_fold_into_one_group_per_kind_and_id_or_name()
    {
        // Retainers ×3 (by name, case and spaces aside), achievement 313 ×3, two relic sets (the repeatable joins its
        // base quest), the mount and the minion.
        Assert.Equal(6, Groups.All.Count);
        Assert.Equal(Entries.Length, Groups.EntryCount);

        var retainers = GroupOf(Limsa, RewardKind.SystemUnlock);
        Assert.Equal([Limsa, Gridania, Uldah], retainers.Quests);
        Assert.False(retainers.IsChoice);

        var achievement = GroupOf(ClassB, RewardKind.Achievement);
        Assert.Equal([ClassA, ClassB, ClassC], achievement.Quests);
        Assert.Equal(3, achievement.Entries.Count);

        // Every entry sits in exactly one group.
        Assert.Equal(Entries.Length, Groups.All.Sum(g => g.Entries.Count));
    }

    [Fact]
    public void A_relic_quest_is_one_choice_set_and_its_repeatable_twin_joins_it()
    {
        var relic = GroupOf(Relic, RewardKind.ArtifactGear);
        Assert.True(relic.IsChoice);
        Assert.Equal(3, relic.Choices);
        Assert.Equal([Relic, RelicAgain], relic.Quests);
        Assert.Equal(6, relic.Entries.Count);
        Assert.Same(relic, GroupOf(RelicAgain, RewardKind.ArtifactGear));

        var other = GroupOf(OtherRelic, RewardKind.ArtifactGear);
        Assert.Equal(2, other.Choices);
        Assert.Equal([OtherRelic], other.Quests);
    }

    [Fact]
    public void The_home_quest_of_a_choice_set_is_the_one_that_is_not_repeatable()
    {
        // Listed repeatable first: the base quest still leads.
        var groups = MoonlitGroups.Build([Gear(RelicAgain, 1001, "Honorbound"), Gear(Relic, 1001, "Honorbound")], id => id == RelicAgain);
        Assert.Equal([Relic, RelicAgain], Assert.Single(groups.All).Quests);
    }

    [Fact]
    public void User_entries_are_never_folded_together_by_their_note()
    {
        var a = new UniqueRewardEntry(70001, RewardKind.Other, 0, 0, UniqueRewardCatalog.DefaultUserRewardName, Confidence.UserOverride, UniqueRewardCatalog.UserSource);
        var b = a with { QuestRowId = 70002 };
        Assert.Equal(2, MoonlitGroups.Build([a, b]).All.Count);
    }

    [Fact]
    public void Other_path_quests_are_hidden_and_the_reward_shows_the_quest_on_the_characters_path()
    {
        var retainers = GroupOf(Limsa, RewardKind.SystemUnlock);
        var evaluations = new Dictionary<uint, QuestEvaluation>
        {
            [Limsa] = OtherPath(),
            [Gridania] = State(QuestState.Ready),
            [Uldah] = OtherPath(),
        };

        var state = MoonlitTally.Evaluate(retainers, _ => false, id => evaluations.GetValueOrDefault(id), GetNow);
        Assert.True(state.OnPath);
        Assert.Equal(Gridania, retainers.Quests[state.Representative]);
        Assert.False(state.Obtained);
    }

    [Fact]
    public void A_reward_whose_every_quest_is_on_another_path_is_not_listed_unless_obtained()
    {
        var retainers = GroupOf(Limsa, RewardKind.SystemUnlock);
        QuestEvaluation? AllOff(uint id) => OtherPath();

        var hidden = MoonlitTally.Evaluate(retainers, _ => false, AllOff, GetNow);
        Assert.False(hidden.OnPath);
        Assert.False(MoonlitTally.Counts(retainers, hidden, default));

        var kept = MoonlitTally.Evaluate(retainers, e => e.QuestRowId == Uldah, AllOff, GetNow);
        Assert.True(kept.OnPath);
        Assert.Equal(Uldah, retainers.Quests[kept.Representative]);
    }

    [Fact]
    public void A_spare_alternative_counts_as_another_path()
    {
        var spare = State(QuestState.Ready) with { IsSpareAlternative = true };
        Assert.True(MoonlitTally.IsOffPath(spare));
        Assert.True(MoonlitTally.IsOffPath(OtherPath()));
        Assert.False(MoonlitTally.IsOffPath(State(QuestState.Blocked)));
        Assert.False(MoonlitTally.IsOffPath(null));
    }

    [Fact]
    public void A_reward_is_obtained_when_any_of_its_quests_gave_it_and_known_only_when_every_entry_is()
    {
        var achievement = GroupOf(ClassA, RewardKind.Achievement);
        QuestEvaluation? None(uint id) => null;

        var viaB = MoonlitTally.Evaluate(achievement, e => e.QuestRowId == ClassB, None, GetNow);
        Assert.True(viaB.Obtained);
        Assert.Equal(ClassB, achievement.Quests[viaB.Representative]);

        Assert.False(MoonlitTally.Evaluate(achievement, _ => false, None, GetNow).Obtained);
        Assert.Null(MoonlitTally.Evaluate(achievement, e => e.QuestRowId == ClassA ? null : false, None, GetNow).Obtained);
    }

    [Fact]
    public void The_shown_quest_is_the_most_available_then_the_furthest_along()
    {
        var achievement = GroupOf(ClassA, RewardKind.Achievement);
        var evaluations = new Dictionary<uint, QuestEvaluation>
        {
            [ClassA] = State(QuestState.Blocked),
            [ClassB] = State(QuestState.Accepted),
            [ClassC] = State(QuestState.Ready),
        };

        var state = MoonlitTally.Evaluate(achievement, _ => false, id => evaluations.GetValueOrDefault(id), GetNow);
        Assert.Equal(ClassB, achievement.Quests[state.Representative]);

        // A quest whose event is over loses to one that can still be done, however far along it is.
        RewardAvailabilityInfo GoneForB(UniqueRewardEntry entry, QuestEvaluation? evaluation) =>
            new(entry.QuestRowId == ClassB ? RewardAvailability.GoneForGood : RewardAvailability.GetNow);
        var available = MoonlitTally.Evaluate(achievement, _ => false, id => evaluations.GetValueOrDefault(id), GoneForB);
        Assert.Equal(ClassC, achievement.Quests[available.Representative]);
        Assert.Equal(RewardAvailability.GetNow, available.Availability.Kind);
    }

    [Fact]
    public void Totals_count_each_reward_once_and_leave_out_gone_rewards_unless_asked()
    {
        QuestEvaluation? None(uint id) => null;
        bool? Obtained(UniqueRewardEntry e) => e.QuestRowId switch
        {
            Gridania => true,
            MountQuest => true,
            EventQuest => false,
            _ => e.Kind == RewardKind.ArtifactGear ? null : false,
        };

        RewardAvailabilityInfo Availability(UniqueRewardEntry entry, QuestEvaluation? evaluation) =>
            new(entry.QuestRowId == EventQuest ? RewardAvailability.GoneForGood : RewardAvailability.GetNow);

        var (states, totals) = MoonlitTally.Run(Groups, Obtained, None, Availability, default);

        // Six rewards, the gone minion left out: retainers and the mount obtained, both relic sets unknown.
        Assert.Equal(new UniqueRewardCounts(2, 5, 2), totals.All);
        Assert.Equal(1, totals.Missed);
        Assert.Equal(new UniqueRewardCounts(1, 1, 0), totals.For(RewardKind.SystemUnlock));
        Assert.Equal(new UniqueRewardCounts(0, 2, 2), totals.For(RewardKind.ArtifactGear));
        Assert.Equal(default, totals.For(RewardKind.Minion));

        var counted = MoonlitTally.Totals(Groups.All, states, new MoonlitCountOptions(CountGone: true));
        Assert.Equal(new UniqueRewardCounts(2, 6, 2), counted.All);
        Assert.Equal(new UniqueRewardCounts(0, 1, 0), counted.For(RewardKind.Minion));
        Assert.Equal(1, counted.Missed);
    }

    [Fact]
    public void An_obtained_reward_whose_event_is_over_still_counts()
    {
        var minion = GroupOf(EventQuest, RewardKind.Minion);
        var state = MoonlitTally.Evaluate(minion, _ => true, _ => null, (_, _) => new RewardAvailabilityInfo(RewardAvailability.GoneForGood));
        Assert.False(state.Missed);
        Assert.True(MoonlitTally.Counts(minion, state, default));
    }

    [Fact]
    public void A_gone_reward_is_missed_only_when_known_not_obtained_and_its_quest_was_not_completed()
    {
        var minion = GroupOf(EventQuest, RewardKind.Minion);
        static RewardAvailabilityInfo Gone(UniqueRewardEntry entry, QuestEvaluation? evaluation) => new(RewardAvailability.GoneForGood);

        // Known not obtained, quest never done: missed, and out of the totals unless gone rewards count.
        var missed = MoonlitTally.Evaluate(minion, _ => false, _ => State(QuestState.Foreclosed), Gone);
        Assert.True(missed.Missed);
        Assert.False(MoonlitTally.Counts(minion, missed, default));
        Assert.True(MoonlitTally.Counts(minion, missed, new MoonlitCountOptions(CountGone: true)));

        // Owned state unreadable: not called missed; it stays in the totals as unknown.
        var unknown = MoonlitTally.Evaluate(minion, _ => null, _ => State(QuestState.Foreclosed), Gone);
        Assert.False(unknown.Missed);
        Assert.True(MoonlitTally.Counts(minion, unknown, default));
        var totals = MoonlitTally.Totals([minion], [unknown], default);
        Assert.Equal(new UniqueRewardCounts(0, 1, 1), totals.All);
        Assert.Equal(0, totals.Missed);

        // The quest was completed: it gave the reward, whatever the flag reads now.
        var completed = MoonlitTally.Evaluate(minion, _ => false, _ => State(QuestState.Completed), Gone);
        Assert.True(completed.QuestCompleted);
        Assert.False(completed.Missed);
        Assert.Equal(0, MoonlitTally.Totals([minion], [completed], default).Missed);
    }

    [Fact]
    public void Found_elsewhere_rewards_leave_the_totals_when_asked()
    {
        var sold = Entry(MountQuest, RewardKind.Mount, 10, "Company Chocobo").WithOtherSource(OtherSource.OnlineStore);
        var groups = MoonlitGroups.Build([sold, Entry(EventQuest, RewardKind.Minion, 30, "Wind-up Moogle")]);
        Assert.True(groups.All[0].FoundElsewhere);
        var (states, all) = MoonlitTally.Run(groups, _ => false, _ => null, GetNow, default);
        Assert.Equal(2, all.All.Total);
        Assert.Equal(1, MoonlitTally.Totals(groups.All, states, new MoonlitCountOptions(ExcludeFoundElsewhere: true)).All.Total);
    }

    [Fact]
    public void The_state_filter_keeps_the_states_it_names()
    {
        Assert.True(MoonlitStateFilters.Passes(MoonlitStateFilter.Any, null));
        Assert.True(MoonlitStateFilters.Passes(MoonlitStateFilter.ReadyNow, QuestState.ReadyOnOtherJob));
        Assert.False(MoonlitStateFilters.Passes(MoonlitStateFilter.ReadyNow, QuestState.Accepted));
        Assert.True(MoonlitStateFilters.Passes(MoonlitStateFilter.InJournal, QuestState.Accepted));
        Assert.True(MoonlitStateFilters.Passes(MoonlitStateFilter.Blocked, QuestState.Blocked));
        Assert.True(MoonlitStateFilters.Passes(MoonlitStateFilter.Done, QuestState.DoneThisCycle));
        Assert.True(MoonlitStateFilters.Passes(MoonlitStateFilter.Done, QuestState.Completed));
        Assert.False(MoonlitStateFilters.Passes(MoonlitStateFilter.Done, QuestState.Foreclosed));
    }
}
