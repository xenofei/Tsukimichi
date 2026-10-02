using Tsukimichi.Core.Discovery;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Evaluation;

/// <summary>
/// Allied society gates of feature plan v5 (1.5.0): rank-up quests need their rank's reputation maxed, dailies not
/// offered today are Blocked and leave every Ready-only surface, and repeat-flag quests read done this cycle.
/// </summary>
public class TribeGateTests
{
    private const byte Dwarves = 14;
    private const byte Trusted = 4;

    /// <summary>69434 "I Heard You Like Tanks": Trusted with the dwarves, reputation maxed.</summary>
    private static readonly QuestRecord RankUp = Quest(Target, "I Heard You Like Tanks") with { BeastTribe = Dwarves, BeastRank = Trusted, BeastReputationMaxed = true };

    private static CharacterSnapshot Standing(byte rank, ushort value) =>
        Snapshot() with { Tribes = new Dictionary<byte, TribeStanding> { [Dwarves] = new(rank, value) } };

    private static IReadOnlyList<RequirementResult> Eval(QuestRecord q, CharacterSnapshot s, EvalContext? ctx = null) =>
        RequirementEvaluator.Evaluate(q, s, Catalog(q), ctx ?? EvalContext.Default);

    [Fact]
    public void Rank_up_quest_is_blocked_until_the_rank_is_maxed()
    {
        var evaluation = StateResolver.Resolve(RankUp, Standing(Trusted, 0), Catalog(RankUp), EvalContext.Default);

        Assert.Equal(QuestState.Blocked, evaluation.State);
        var rep = Assert.IsType<TribeReputationRequirement>(evaluation.NextStep!.Req);
        Assert.Equal(Trusted, rep.MaxedRank);
        Assert.Equal(720, rep.RequiredValue);
        Assert.Equal(0, rep.ActualValue);
        Assert.Equal("Trusted 0/720 reputation", evaluation.NextStep.Detail);
    }

    [Fact]
    public void Rank_up_quest_reads_ready_at_the_rank_maximum_and_past_the_rank()
    {
        Assert.Equal(QuestState.Ready, StateResolver.Resolve(RankUp, Standing(Trusted, 720), Catalog(RankUp), EvalContext.Default).State);

        var maxed = Only(Eval(RankUp, Standing(Trusted, 720)), RequirementKind.TribeReputation);
        Assert.True(maxed.Met);
        Assert.Equal("Trusted reputation maxed", maxed.Detail);

        // Already Respected (the rank-up happened): met whatever the reputation within the new rank.
        Assert.True(Only(Eval(RankUp, Standing(5, 10)), RequirementKind.TribeReputation).Met);
    }

    [Fact]
    public void Below_the_rank_the_rank_is_the_next_step_and_the_reputation_reads_zero()
    {
        var evaluation = StateResolver.Resolve(RankUp, Standing(3, 500), Catalog(RankUp), EvalContext.Default);

        Assert.Equal(RequirementKind.TribeRank, evaluation.NextStep!.Req.Kind);
        var rep = Only(evaluation.Requirements, RequirementKind.TribeReputation);
        Assert.False(rep.Met);
        Assert.Equal("Trusted 0/720 reputation", rep.Detail);
    }

    [Fact]
    public void Gap_meter_blocker_and_callout_name_the_rank_and_both_numbers()
    {
        var rep = Only(Eval(RankUp, Standing(Trusted, 300)), RequirementKind.TribeReputation);
        var names = new BlockerNames { Catalog = Catalog(RankUp), Tribe = id => id == Dwarves ? "dwarves" : string.Empty };

        Assert.Equal("Trusted 300/720 reputation", rep.Detail);
        Assert.Equal(new RequirementGap(300, 720), NotYetText.Gap(rep.Req));
        Assert.Equal("Trusted 300/720 reputation", NotYetText.GapLabel(rep.Req, names));
        Assert.Equal("Trusted reputation maxed (300/720)", NotYetText.Clause(rep, RankUp, names));
        Assert.Equal(rep.Detail, RequirementDetail.Render(rep, names));
        var evaluation = StateResolver.Resolve(RankUp, Standing(Trusted, 300), Catalog(RankUp), EvalContext.Default);
        Assert.Equal("Reputation: Trusted 300/720 with the dwarves", BlockerText.For(evaluation, RankUp, names));
    }

    [Fact]
    public void Thousands_are_grouped_in_the_reputation_numbers()
    {
        var ctx = EvalContext.Default with { TribeRankReputation = _ => 3000 };
        var rep = Only(Eval(RankUp, Standing(Trusted, 1200), ctx), RequirementKind.TribeReputation);

        Assert.Equal("Trusted 1,200/3,000 reputation", rep.Detail);
        Assert.Equal(rep.Detail, RequirementDetail.Render(rep, new BlockerNames { Catalog = Catalog(RankUp) }));
    }

    [Fact]
    public void An_unknown_rank_maximum_is_listed_not_checked_and_never_blocks()
    {
        var ctx = EvalContext.Default with { TribeRankReputation = _ => null };

        var evaluation = StateResolver.Resolve(RankUp, Standing(Trusted, 0), Catalog(RankUp), ctx);

        Assert.Equal(QuestState.Ready, evaluation.State);
        var rep = Only(evaluation.Requirements, RequirementKind.TribeReputation);
        Assert.True(rep.Met);
        Assert.True(((TribeReputationRequirement)rep.Req).NotChecked);
        Assert.Equal("needs Trusted reputation maxed, not checked", rep.Detail);
        Assert.Null(NotYetText.Gap(rep.Req));
        Assert.Equal(rep.Detail, RequirementDetail.Render(rep, new BlockerNames { Catalog = Catalog(RankUp) }));
    }

    [Fact]
    public void A_story_opener_at_rank_None_lists_no_reputation()
    {
        var opener = RankUp with { BeastRank = 0 };
        Assert.DoesNotContain(Eval(opener, Snapshot()), r => r.Req.Kind == RequirementKind.TribeReputation);
    }

    [Fact]
    public void The_default_maximums_are_the_sheet_s()
    {
        Assert.Equal([0, 150, 360, 510, 720, 990, 1320, 1730, 0], Enumerable.Range(0, 9).Select(r => (int)TribeRanks.MaxReputation((byte)r)!.Value));
        Assert.Null(TribeRanks.MaxReputation(9));
    }

    // Dailies: two of the dwarves', one of the Qitari.
    private static readonly QuestRecord Offered = Quest(A, "Offered") with { BeastTribe = Dwarves, BeastRank = 3, IsRepeatable = true, RepeatInterval = 1, DailyPool = 2, Issuer = new Issuer(1033712, "Giver", 816, 0, 0, 0, 0) };
    private static readonly QuestRecord NotOffered = Offered with { RowId = B, QuestId = QuestRecord.ToQuestId(B), Name = "Not offered" };
    private static readonly QuestRecord OtherSociety = Offered with { RowId = C, QuestId = QuestRecord.ToQuestId(C), Name = "Qitari", BeastTribe = 13, Issuer = new Issuer(1032643, "Qitari giver", 816, 0, 0, 0, 0) };
    private static readonly QuestCatalog Dailies = Catalog(Offered, NotOffered, OtherSociety);

    private static CharacterSnapshot DailyStanding() => Snapshot() with
    {
        TribeAllowance = 12,
        Tribes = new Dictionary<byte, TribeStanding> { [Dwarves] = new(Trusted, 0), [13] = new(Trusted, 0) },
    };

    [Fact]
    public void A_daily_not_offered_today_is_blocked_only_for_a_society_whose_offer_is_known()
    {
        var offer = new DailyOffer(new HashSet<ushort> { Offered.QuestId }, new HashSet<byte> { Dwarves });
        var states = StateResolver.ResolveAll(Dailies, DailyStanding(), EvalContext.Default.WithDailyOffer(offer));

        Assert.Equal(QuestState.Ready, states[A].State);
        Assert.Equal(QuestState.Blocked, states[B].State);
        Assert.Equal("not offered today", states[B].NextStep!.Detail);
        Assert.Equal("Not offered today", BlockerText.For(states[B], NotOffered, new BlockerNames { Catalog = Dailies }));
        // The Qitari's offer is unknown: their daily is not held back.
        Assert.Equal(QuestState.Ready, states[C].State);
        Assert.DoesNotContain(states[C].Requirements, r => r.Req.Kind == RequirementKind.TribeDailyOffer);

        // No offer known: nothing held back, as before.
        var unknown = StateResolver.ResolveAll(Dailies, DailyStanding(), EvalContext.Default.WithDailyOffer(DailyOffer.None));
        Assert.All(unknown.Values, e => Assert.Equal(QuestState.Ready, e.State));
    }

    [Fact]
    public void An_unoffered_daily_leaves_nearby_the_ready_lists_and_ipc()
    {
        var offer = new DailyOffer(new HashSet<ushort> { Offered.QuestId }, new HashSet<byte> { Dwarves });
        var states = StateResolver.ResolveAll(Dailies, DailyStanding(), EvalContext.Default.WithDailyOffer(offer));
        var unknown = StateResolver.ResolveAll(Dailies, DailyStanding(), EvalContext.Default);

        // Nearby ("☾ N") and its list: the count drops from 3 to 2 once the offer is known.
        Assert.Equal(3, QuestDiscovery.StartableInZone(Dailies, unknown, 816).Count);
        Assert.Equal(["Offered", "Qitari"], QuestDiscovery.StartableInZone(Dailies, states, 816).Select(q => q.Name).OrderBy(n => n));

        var view = new IpcView(Dailies, states, new BlockerNames { Catalog = Dailies });
        Assert.True(view.IsQuestAvailable(Offered.QuestId));
        Assert.False(view.IsQuestAvailable(NotOffered.QuestId));
    }

    [Fact]
    public void Repeat_flag_quests_read_done_this_cycle_and_a_shared_flag_covers_its_siblings()
    {
        var relic = Quest(A, "One Man's Relic") with { IsRepeatable = true, RepeatInterval = 2, RepeatFlag = 11 };
        var komra1 = Quest(B, "All That Grinds Is Not Gloom") with { IsRepeatable = true, RepeatInterval = 2, RepeatFlag = 12 };
        var komra2 = Quest(C, "The Merchant of Komra") with { IsRepeatable = true, RepeatInterval = 2, RepeatFlag = 12 };
        var joy = Quest(D, "The Gift of Joy") with { IsRepeatable = true, RepeatInterval = 1, RepeatFlag = 3 };
        var catalog = Catalog(relic, komra1, komra2, joy);
        var snapshot = Snapshot(A, B, D) with { RepeatFlags = [3, 12] };

        var states = StateResolver.ResolveAll(catalog, snapshot, EvalContext.Default);

        Assert.Equal(QuestState.Ready, states[A].State);
        Assert.Equal(QuestState.DoneThisCycle, states[B].State);
        Assert.Equal(QuestState.DoneThisCycle, states[C].State);
        Assert.Equal(QuestState.DoneThisCycle, states[D].State);
        Assert.Equal("Done this week", Core.Ui.StateNames.Name(states[B].State, komra1));
        Assert.Equal("Done today", Core.Ui.StateNames.Name(states[D].State, joy));

        // An older snapshot holds no flags: the quests read as before.
        var old = StateResolver.ResolveAll(catalog, Snapshot(A, B, D), EvalContext.Default);
        Assert.Equal(QuestState.Ready, old[B].State);
        Assert.Equal(QuestState.Ready, old[D].State);
    }

    [Fact]
    public void Repeat_flag_lookup_ignores_flag_zero()
    {
        var snapshot = Snapshot() with { RepeatFlags = [3] };
        Assert.False(snapshot.IsRepeatFlagSet(0));
        Assert.True(snapshot.IsRepeatFlagSet(3));
        Assert.False(snapshot.IsDoneThisCycle(Quest(A)));
    }
}

/// <summary>The rank-up clause under a translation: installs a <see cref="CoreText"/> provider, so it runs alone.</summary>
[Collection(Tsukimichi.Tests.Localization.CoreTextCollection.Name)]
public sealed class TribeGateTextTests : IDisposable
{
    public TribeGateTextTests() => CoreText.Use(null);

    public void Dispose() => CoreText.Use(null);

    [Fact]
    public void Rank_up_requirement_details_stay_english_in_the_record_and_follow_the_language_on_screen()
    {
        var quest = Quest(Target) with { BeastTribe = 14, BeastRank = 4, BeastReputationMaxed = true };
        var snapshot = Snapshot() with { Tribes = new Dictionary<byte, TribeStanding> { [14] = new(4, 300) } };
        CoreText.Use(new Table(new() { ["Core.Req.RankReputation"] = "{0} {1:N0}/{2:N0} Ruf" }));

        var rep = Only(RequirementEvaluator.Evaluate(quest, snapshot, Catalog(quest), EvalContext.Default), RequirementKind.TribeReputation);

        Assert.Equal("Trusted 300/720 reputation", rep.Detail);
        Assert.Equal("Trusted 300/720 Ruf", RequirementDetail.Text(rep, new BlockerNames { Catalog = Catalog(quest) }));
    }

    private sealed class Table(Dictionary<string, string> values) : ITextProvider
    {
        public string? Find(string key) => values.TryGetValue(key, out var value) ? value : null;
    }
}
