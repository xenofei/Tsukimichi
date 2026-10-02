using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Ipc;

/// <summary>
/// The 1.8.0 gates' payloads (docs/ipc.md): bulk states, state and zone lists, the structured blocker in its public
/// vocabulary, routes, item and duty lookups, Moonlit status, abandoned quests, the per-quest change list behind
/// <c>Tsukimichi.QuestStateChanged</c>, the gate list and the <c>/tsuki ipc</c> console's parsing.
/// </summary>
public class IpcViewListsTests
{
    private const uint Limsa = 128;
    private const uint Gridania = 132;

    private static QuestRecord Side(uint rowId, uint territory = 0, int sort = 0) =>
        Quest(rowId) with
        {
            Journal = new JournalRef(3, "Side Quests", 1, "Category", 1, "Genre", sort == 0 ? (int)rowId : sort),
            Issuer = territory == 0 ? null : new Issuer(1000 + rowId, "Giver", territory, 1, 0f, 0f, 0f),
        };

    private static IpcView View(CharacterSnapshot snapshot, IpcExtras? extras, params QuestRecord[] quests)
    {
        var catalog = Catalog(quests);
        return new IpcView(catalog, StateResolver.ResolveAll(catalog, snapshot, EvalContext.Default), BlockerNames.Default, extras);
    }

    private static UniqueRewardCatalog Rewards(params UniqueRewardEntry[] entries) =>
        UniqueRewardCatalog.Build(new UniqueRewardsData(string.Empty, default, entries), new Dictionary<uint, UniqueOverride>(), CuratedData.Empty);

    [Fact]
    public void GetStates_answers_each_id_in_order_and_nothing_for_null()
    {
        var view = View(Snapshot(A), null, Side(A), Side(B), Side(Target) with { Level = 80 });

        Assert.Equal(["Completed", "Ready", "", "Blocked", "Ready"], view.StatesOf([A, B, 0, Target, QuestRecord.ToQuestId(B)]));
        Assert.Empty(view.StatesOf(null));
        Assert.Empty(view.StatesOf([]));
        Assert.Equal(["", ""], IpcView.Empty.StatesOf([A, B]));
    }

    [Fact]
    public void GetQuestsInState_lists_a_state_in_journal_order_and_nothing_for_a_name_that_is_no_state()
    {
        var view = View(Snapshot(A), null, Side(A), Side(C, sort: 1), Side(B, sort: 2), Side(Target) with { Level = 80 });

        Assert.Equal([C, B], view.QuestsInState("Ready"));
        Assert.Equal([A], view.QuestsInState("Completed"));
        Assert.Equal([Target], view.QuestsInState("Blocked"));
        Assert.Empty(view.QuestsInState("ready"));
        Assert.Empty(view.QuestsInState("0"));
        Assert.Empty(view.QuestsInState("Moonlit"));
        Assert.Empty(view.QuestsInState(null));
        Assert.Empty(IpcView.Empty.QuestsInState("Ready"));
        Assert.True(IpcView.TryParseState("ReadyOnOtherJob", out var state));
        Assert.Equal(QuestState.ReadyOnOtherJob, state);
    }

    [Fact]
    public void GetQuestsInZone_lists_the_givers_of_a_territory_and_readyOnly_keeps_what_can_be_picked_up()
    {
        var view = View(
            Snapshot(A),
            null,
            Side(A, Gridania),
            Side(B, Gridania),
            Side(C, Gridania) with { Level = 80 },
            Side(D, Gridania) with { IsRetired = true },
            Side(E, Limsa));

        Assert.Equal([A, B, C], view.QuestsInZone(Gridania, readyOnly: false));
        Assert.Equal([B], view.QuestsInZone(Gridania, readyOnly: true));
        Assert.Equal([E], view.QuestsInZone(Limsa, readyOnly: true));
        Assert.Empty(view.QuestsInZone(999, readyOnly: false));
        Assert.Empty(view.QuestsInZone(0, readyOnly: false));
        Assert.Empty(IpcView.Empty.QuestsInZone(Gridania, readyOnly: false));
    }

    [Fact]
    public void GetFirstBlocker_speaks_the_public_vocabulary()
    {
        var view = View(
            Snapshot(A) with { JobLevels = Levels((Gladiator, 50), (Conjurer, 70)) },
            null,
            Side(A),
            Side(B),
            Side(C) with { Level = 60 },
            Side(D) with { Level = 80 },
            Side(E) with { PreviousQuests = new Prereq([A, Target], JoinKind.All) },
            Side(Target) with { Level = 99 });

        Assert.Equal(IpcBlocker.None, view.FirstBlocker(A));
        Assert.Equal(IpcBlocker.None, view.FirstBlocker(B));
        Assert.Equal(new IpcBlocker("job", Conjurer, 0, 0), view.FirstBlocker(C));
        Assert.Equal(("level", 0u, 80, 50), view.FirstBlocker(D).ToTuple());
        Assert.Equal(new IpcBlocker("quest", Target, 2, 1), view.FirstBlocker(E));
        Assert.Equal(IpcBlocker.NoAnswer, view.FirstBlocker(65599));
        Assert.Equal(IpcBlocker.NoAnswer, IpcView.Empty.FirstBlocker(A));
    }

    [Fact]
    public void The_blocker_vocabulary_is_frozen()
    {
        // Words are never renamed or removed within API version 1 (docs/ipc.md, "Blocker kinds"); a new one is appended.
        Assert.Equal(
            ["none", "level", "quest", "job", "jobCategory", "grandCompany", "grandCompanyRank", "alliedSocietyRank",
             "alliedSocietyReputation", "alliedSocietyAllowance", "notOfferedToday", "duty", "seasonal", "expansion",
             "levelCap", "otherPath", "lockedOut", "removed", "achievement", "mount", "house", "customDeliveryRank",
             "carrierLevel", "unchecked", "other"],
            IpcBlockerKinds.All);
    }

    [Fact]
    public void Every_requirement_kind_maps_to_a_word_of_the_vocabulary()
    {
        var catalog = Catalog(Side(A));
        Requirement[] requirements =
        [
            new RetiredRequirement(), new OtherPathRequirement(PathKind.StartCity, [], [A]), new ForeclosureRequirement([A], [A]),
            new ExpansionCapRequirement(5, 3), new LevelCapRequirement(80, 70), new ClassJobRequirement(0, 19, 1), new ClassJobRequirement(31, 0, 1),
            new LevelRequirement(50, 20), new PreviousQuestsRequirement([A], JoinKind.Any, 0), new GrandCompanyRequirement(1, 0),
            new GrandCompanyRankRequirement(1, 5, 2), new TribeRankRequirement(3, 4, 2), new TribeReputationRequirement(3, 720, 100),
            new TribeAllowanceRequirement(0), new TribeDailyOfferRequirement(64, false), new DutyCompletionRequirement([20001, 20002], JoinKind.All, 1),
            new SeasonalRequirement(5, false), new AcceptConditionRequirement([7]), new MountRequirement(null), new HouseRequirement(false),
            new AchievementRequirement(9, false), new CustomDeliveryRankRequirement(2, 3, null), new CarrierLevelRequirement(4, 1),
        ];

        foreach (var requirement in requirements)
        {
            var evaluation = new QuestEvaluation(QuestState.Blocked, [], new RequirementResult(requirement, false, string.Empty), null, null);
            var blocker = IpcBlocker.Of(evaluation, catalog, null);
            Assert.Contains(blocker.Kind, IpcBlockerKinds.All);
            Assert.NotEqual(IpcBlockerKinds.Other, blocker.Kind);
        }

        IpcBlocker Of(Requirement r) => IpcBlocker.Of(new QuestEvaluation(QuestState.Blocked, [], new RequirementResult(r, false, string.Empty), null, null), catalog, null);
        Assert.Equal(new IpcBlocker("duty", 20001, 2, 1), Of(new DutyCompletionRequirement([20001, 20002], JoinKind.All, 1)));
        Assert.Equal(new IpcBlocker("notOfferedToday", 65600, 1, 0), Of(new TribeDailyOfferRequirement(64, false)));
        Assert.Equal(new IpcBlocker("customDeliveryRank", 2, 3, -1), Of(new CustomDeliveryRankRequirement(2, 3, null)));
        Assert.Equal(new IpcBlocker("jobCategory", 31, 0, 0), Of(new ClassJobRequirement(31, 0, 1)));
        Assert.Equal(new IpcBlocker("job", 19, 0, 0), Of(new ClassJobRequirement(0, 19, 1)));
        Assert.Equal(new IpcBlocker("quest", A, 1, 0), Of(new PreviousQuestsRequirement([A], JoinKind.Any, 0)));
        Assert.Equal(new IpcBlocker("achievement", 9, 1, -1), Of(new AchievementRequirement(9, false)));
    }

    [Fact]
    public void GetRoute_lists_the_steps_to_the_target_and_hands_out_a_fresh_array()
    {
        var view = View(
            Snapshot(),
            null,
            Side(A),
            Side(B) with { PreviousQuests = new Prereq([A], JoinKind.All) },
            Side(Target) with { PreviousQuests = new Prereq([B], JoinKind.All) });

        var route = view.Route(Target);
        Assert.Equal([A, B, Target], route);
        route[0] = 1;
        Assert.Equal([A, B, Target], view.Route(QuestRecord.ToQuestId(Target)));
        Assert.Empty(View(Snapshot(Target), null, Side(Target)).Route(Target));
        Assert.Empty(view.Route(65599));
        Assert.Empty(IpcView.Empty.Route(Target));
    }

    [Fact]
    public void GetQuestsForItem_finds_sheet_rewards_and_Moonlit_items_and_folds_HQ_ids()
    {
        var rewarding = Side(A) with { Rewards = [new RewardRef(RewardKind.Item, 5000, 5000, 1, "Potion", 0)] };
        var optional = Side(B) with { Rewards = [new RewardRef(RewardKind.OptionalItem, 5000, 0, 1, "Potion", 0)] };
        var removed = Side(C) with { IsRetired = true, Rewards = [new RewardRef(RewardKind.Item, 5000, 5000, 1, "Potion", 0)] };
        var mountQuest = Side(D);
        var catalog = Catalog(rewarding, optional, removed, mountQuest);
        var lookup = new RewardLookup(Rewards(new UniqueRewardEntry(D, RewardKind.Mount, 15, 6000, "unicorn", Confidence.Static, "sheet")), catalog);
        var view = new IpcView(catalog, null, BlockerNames.Default, new IpcExtras { Rewards = lookup });

        // Data only: answered without a character.
        Assert.Equal([A, B], view.QuestsForItem(5000));
        Assert.Equal([A, B], view.QuestsForItem(1_005_000));
        Assert.Equal([D], view.QuestsForItem(6000));
        Assert.Empty(view.QuestsForItem(7000));
        Assert.Empty(view.QuestsForItem(0));
        Assert.Empty(IpcView.Empty.QuestsForItem(5000));
    }

    [Fact]
    public void GetMoonlitStatus_says_how_ownership_is_known()
    {
        var mount = new UniqueRewardEntry(A, RewardKind.Mount, 15, 6000, "unicorn", Confidence.Static, "sheet");
        var minion = new UniqueRewardEntry(B, RewardKind.Minion, 21, 6001, "wind-up gentleman", Confidence.Static, "sheet");
        var action = new UniqueRewardEntry(C, RewardKind.Action, 7, 6002, "Sprint", Confidence.Static, "sheet");
        var item = new UniqueRewardEntry(D, RewardKind.Item, 6003, 6003, "Thing", Confidence.Static, "sheet");
        var catalog = Catalog(Side(A), Side(B), Side(C), Side(D));
        var saved = CollectibleLookup.For(Snapshot() with
        {
            Collectibles = new Dictionary<string, CollectibleSet> { ["Minion"] = new() { Owned = [21] }, ["Mount"] = new() { Missing = [15] } },
        });
        var extras = new IpcExtras { Rewards = new RewardLookup(Rewards(mount, minion, action, item), catalog), SavedCollectibles = saved };
        var view = new IpcView(catalog, StateResolver.ResolveAll(catalog, Snapshot(C), EvalContext.Default), BlockerNames.Default, extras);

        Assert.Equal((true, true, MoonlitConfidence.Live), view.MoonlitStatus(6000, e => e.Kind == RewardKind.Mount ? true : null));
        Assert.Equal((true, false, MoonlitConfidence.Saved), view.MoonlitStatus(6000));
        Assert.Equal((true, true, MoonlitConfidence.Saved), view.MoonlitStatus(6001));
        Assert.Equal((true, true, MoonlitConfidence.Quest), view.MoonlitStatus(6002, _ => false));
        Assert.Equal((true, false, MoonlitConfidence.Unknown), view.MoonlitStatus(6003));
        Assert.Equal((false, false, string.Empty), view.MoonlitStatus(9999));
        Assert.Equal((false, false, string.Empty), IpcView.Empty.MoonlitStatus(6000));
    }

    [Fact]
    public void GetUnlockQuests_answers_from_the_duty_index_in_journal_order()
    {
        var catalog = Catalog(Side(A, sort: 2), Side(B, sort: 1), Side(C) with { IsRetired = true });
        var rewards = Rewards(
            new UniqueRewardEntry(A, RewardKind.DutyUnlock, 4, 0, "Sastasha", Confidence.Static, "sheet"),
            new UniqueRewardEntry(B, RewardKind.DutyUnlock, 4, 0, "Sastasha", Confidence.Static, "sheet"),
            new UniqueRewardEntry(C, RewardKind.DutyUnlock, 4, 0, "Sastasha", Confidence.Static, "sheet"));
        var view = new IpcView(catalog, null, BlockerNames.Default, new IpcExtras { DutyUnlocks = DutyUnlockIndex.Build(CuratedData.Empty, rewards) });

        Assert.Equal([B, A], view.UnlockQuests(4));
        Assert.Empty(view.UnlockQuests(5));
        Assert.Empty(view.UnlockQuests(0));
    }

    [Fact]
    public void GetAbandoned_lists_newest_first_with_the_step_and_unix_seconds()
    {
        var at = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
        var extras = new IpcExtras { Abandoned = [new AbandonedEntry(QuestRecord.ToQuestId(B), at, 3, 5), new AbandonedEntry(QuestRecord.ToQuestId(A), at.AddDays(-1), 0, 0)] };
        var view = View(Snapshot(), extras, Side(A), Side(B));

        Assert.Equal([(B, (byte)3, new DateTimeOffset(at).ToUnixTimeSeconds()), (A, (byte)0, new DateTimeOffset(at.AddDays(-1)).ToUnixTimeSeconds())], view.Abandoned());
        Assert.Empty(new IpcView(Catalog(Side(A)), null, BlockerNames.Default, extras).Abandoned());
        Assert.Equal(0u, view.NextJobQuest(1));
    }

    [Fact]
    public void State_changes_list_each_moved_quest_and_stay_silent_on_a_flood()
    {
        var catalog = Catalog(Side(A), Side(B), Side(C), Side(Target) with { PreviousQuests = new Prereq([A], JoinKind.All) });
        var before = StateResolver.ResolveAll(catalog, Snapshot(), EvalContext.Default);
        var after = StateResolver.ResolveAll(catalog, Snapshot(A), EvalContext.Default);

        Assert.Equal([(A, "Ready", "Completed"), (Target, "Blocked", "Ready")], IpcView.StateChanges(before, after));
        Assert.Empty(IpcView.StateChanges(before, before)!);
        Assert.Empty(IpcView.StateChanges(before, StateResolver.ResolveAll(catalog, Snapshot(), EvalContext.Default))!);
        Assert.Null(IpcView.StateChanges(before, after, max: 1));

        var fewer = new Dictionary<uint, QuestEvaluation>(after);
        fewer.Remove(C);
        Assert.Contains((C, "Ready", ""), IpcView.StateChanges(before, fewer)!);
        Assert.Contains((C, "", "Ready"), IpcView.StateChanges(fewer, before)!);
    }

    [Fact]
    public void The_gate_list_names_every_gate_once_with_the_1_8_additions()
    {
        var names = IpcChannels.Names();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        Assert.All(names, n => Assert.StartsWith("Tsukimichi.", n, StringComparison.Ordinal));
        Assert.Equal(25, names.Length);
        foreach (var gate in new[]
        {
            "Tsukimichi.GetGates", "Tsukimichi.Disposing", "Tsukimichi.GetStates", "Tsukimichi.GetQuestsInState", "Tsukimichi.GetQuestsInZone",
            "Tsukimichi.GetFirstBlocker", "Tsukimichi.GetRoute", "Tsukimichi.GetQuestsForItem", "Tsukimichi.GetMoonlitStatus",
            "Tsukimichi.GetUnlockQuests", "Tsukimichi.GetPins", "Tsukimichi.PinQuest", "Tsukimichi.GetAbandoned", "Tsukimichi.GetNextJobQuest",
            "Tsukimichi.QuestStateChanged", IpcChannels.StatesChangedGate, IpcChannels.OpenQuestGate,
        })
        {
            Assert.Contains(gate, names);
        }

        Assert.Equal(1, IpcChannels.ApiVersion);
        Assert.Equal(["Tsukimichi.StatesChanged", "Tsukimichi.QuestStateChanged", "Tsukimichi.Disposing"], IpcChannels.All.Where(g => g.IsMessage).Select(g => g.Name));
        Assert.NotSame(IpcChannels.Names(), IpcChannels.Names());
    }

    [Fact]
    public void The_console_reads_arguments_and_prints_answers()
    {
        Assert.True(IpcConsole.TryUInt("66236", out var id));
        Assert.Equal(66236u, id);
        Assert.True(IpcConsole.TryUInt("0x10000", out id));
        Assert.Equal(65536u, id);
        Assert.False(IpcConsole.TryUInt("-1", out _));
        Assert.False(IpcConsole.TryUInt("abc", out _));
        Assert.True(IpcConsole.TryBool("yes", out var flag) && flag);
        Assert.True(IpcConsole.TryBool("0", out flag) && !flag);
        Assert.False(IpcConsole.TryBool("maybe", out _));
        Assert.True(IpcConsole.TryUInts("1, 2;3  4", out var ids));
        Assert.Equal([1u, 2u, 3u, 4u], ids);
        Assert.False(IpcConsole.TryUInts("1 x", out _));

        Assert.Equal("(\"level\", 0, 80, 50)", IpcConsole.Format(("level", 0u, 80, 50)));
        Assert.Equal("[1, 2, 3]", IpcConsole.Format(new uint[] { 1, 2, 3 }));
        Assert.Equal("[(66236, 3, 1790000000)]", IpcConsole.Format(new[] { (66236u, (byte)3, 1790000000L) }));
        Assert.Equal("(true, false, \"saved\")", IpcConsole.Format((true, false, "saved")));
        Assert.Equal("null", IpcConsole.Format(null));
        Assert.Equal("[1, 2, … (5 in all)]", IpcConsole.Format(new[] { 1, 2, 3, 4, 5 }, maxItems: 2));
    }
}
