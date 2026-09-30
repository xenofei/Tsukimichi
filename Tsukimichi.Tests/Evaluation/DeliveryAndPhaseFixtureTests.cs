using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Tests.Data;

namespace Tsukimichi.Tests.Evaluation;

/// <summary>
/// The requirement kinds 0.6.2 added, over the frozen catalog (docs/data/verification-report-2.md section 2 rows
/// 2 to 4): the 16 custom delivery quests gate on the client's satisfaction rank, the 17 postmoogle quests on the
/// carrier level, and a later chapter of a phased event on the running event's phase.
/// </summary>
public class DeliveryAndPhaseFixtureTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private const byte Mnaago = 2;
    private const uint NotWhileTheirNamesAreStillSpoken = 68542;
    private const uint SweetWordsShadowyDealings = 65569;
    private const uint EggsigentCircumstances = 66953;
    private const uint NothingToYolkAbout = 66954;
    private const ushort HatchingTide2014 = 10;

    /// <summary>Row ids of every quest with a satisfaction rank gate (verification-report-2 section 2 row 2).</summary>
    private static readonly uint[] SatisfactionQuests =
        [68542, 68676, 68714, 69266, 69267, 69426, 69427, 69616, 70060, 70061, 70252, 70253, 70352, 70776, 70777, 70997];

    /// <summary>Row ids of every postmoogle quest (verification-report-2 section 2 row 3).</summary>
    private static readonly uint[] PostmoogleQuests =
        [65569, 65572, 65776, 65777, 65778, 65779, 65780, 65898, 66032, 67106, 67107, 67108, 67109, 67110, 67111, 67112, 67113];

    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    private EvalContext Context => new()
    {
        ClassJobs = fixture.Bundle.Jobs,
        SatisfactionNpcName = id => fixture.Bundle.Names.SatisfactionNpc(id),
    };

    /// <summary>A level-100 character on a job the quest admits, with its prerequisites done.</summary>
    private CharacterSnapshot Eligible(QuestRecord quest)
    {
        var job = quest.ClassJobRequired != 0
            ? (byte)quest.ClassJobRequired
            : quest.ClassJobCategory != 0 ? fixture.Bundle.Jobs.JobsIn(quest.ClassJobCategory).First() : Fixture.Gladiator;
        return Fixture.Snapshot(quest.PreviousQuests.QuestIds) with
        {
            CurrentJob = job,
            JobLevels = Fixture.Levels((job, 100)),
        };
    }

    [Fact]
    public void The_sixteen_custom_delivery_quests_gate_on_the_clients_satisfaction_rank()
    {
        var gated = Catalog.All.Where(q => q.SatisfactionLevel > 0).Select(q => q.RowId).OrderBy(id => id).ToArray();
        Assert.Equal(SatisfactionQuests.OrderBy(id => id), gated);

        foreach (var rowId in SatisfactionQuests)
        {
            var quest = Catalog.GetByRowId(rowId)!;
            Assert.NotEqual(0, quest.SatisfactionNpc);
            Assert.NotEmpty(fixture.Bundle.Names.SatisfactionNpc(quest.SatisfactionNpc));
            var eligible = Eligible(quest);

            var below = eligible with { SatisfactionRanks = new Dictionary<byte, byte> { [quest.SatisfactionNpc] = (byte)(quest.SatisfactionLevel - 1) } };
            var blocked = StateResolver.Resolve(quest, below, Catalog, Context);
            Assert.True(blocked.State == QuestState.Blocked, $"{rowId} {quest.Name} should be Blocked one rank short, was {blocked.State}: {blocked.NextStep?.Detail}");
            Assert.Equal(RequirementKind.CustomDeliveryRank, blocked.NextStep!.Req.Kind);

            var at = eligible with { SatisfactionRanks = new Dictionary<byte, byte> { [quest.SatisfactionNpc] = quest.SatisfactionLevel } };
            var ready = StateResolver.Resolve(quest, at, Catalog, Context);
            Assert.True(ready.State == QuestState.Ready, $"{rowId} {quest.Name} should be Ready at rank {quest.SatisfactionLevel}, was {ready.State}: {ready.NextStep?.Detail}");
        }
    }

    [Fact]
    public void Not_While_Their_Names_Are_Still_Spoken_needs_rank_4_with_Mnaago()
    {
        var quest = Catalog.GetByRowId(NotWhileTheirNamesAreStillSpoken)!;
        Assert.Equal("Not While Their Names Are Still Spoken", quest.Name);
        Assert.Equal(Mnaago, quest.SatisfactionNpc);
        Assert.Equal(4, quest.SatisfactionLevel);
        Assert.Equal("M'naago", fixture.Bundle.Names.SatisfactionNpc(Mnaago));

        var names = fixture.Bundle.BlockerNames();
        var eligible = Eligible(quest);
        var rank3 = StateResolver.Resolve(quest, eligible with { SatisfactionRanks = new Dictionary<byte, byte> { [Mnaago] = 3 } }, Catalog, Context);
        Assert.Equal(QuestState.Blocked, rank3.State);
        Assert.Equal("needs satisfaction rank 4 with M'naago, you are rank 3", rank3.NextStep!.Detail);
        Assert.Equal("Custom delivery: rank 4 with M'naago", BlockerText.For(rank3, quest, names));
        Assert.Equal("Blocked · Custom delivery: rank 4 with M'naago", BlockerText.StatusText(rank3, quest, names));

        var rank4 = StateResolver.Resolve(quest, eligible with { SatisfactionRanks = new Dictionary<byte, byte> { [Mnaago] = 4 } }, Catalog, Context);
        Assert.Equal(QuestState.Ready, rank4.State);

        // A capture without ranks (a file from before 0.6.2) lists the gate without judging it.
        var unread = StateResolver.Resolve(quest, eligible, Catalog, Context);
        Assert.Equal(QuestState.Ready, unread.State);
        Assert.Contains(unread.Requirements, r => r.Req is CustomDeliveryRankRequirement { ActualRank: null } && r.Met);
    }

    [Fact]
    public void The_seventeen_postmoogle_quests_gate_on_the_carrier_level()
    {
        var gated = Catalog.All.Where(q => q.CarrierLevel > 0).Select(q => q.RowId).OrderBy(id => id).ToArray();
        Assert.Equal(PostmoogleQuests.OrderBy(id => id), gated);

        foreach (var rowId in PostmoogleQuests)
        {
            var quest = Catalog.GetByRowId(rowId)!;
            var eligible = Eligible(quest);

            var blocked = StateResolver.Resolve(quest, eligible with { CarrierLevel = (byte)(quest.CarrierLevel - 1) }, Catalog, Context);
            Assert.True(blocked.State == QuestState.Blocked, $"{rowId} {quest.Name} should be Blocked one carrier level short, was {blocked.State}: {blocked.NextStep?.Detail}");
            Assert.Equal(RequirementKind.CarrierLevel, blocked.NextStep!.Req.Kind);

            var ready = StateResolver.Resolve(quest, eligible with { CarrierLevel = quest.CarrierLevel }, Catalog, Context);
            Assert.True(ready.State == QuestState.Ready, $"{rowId} {quest.Name} should be Ready at carrier level {quest.CarrierLevel}, was {ready.State}: {ready.NextStep?.Detail}");
        }
    }

    [Fact]
    public void Sweet_Words_Shadowy_Dealings_needs_carrier_level_7()
    {
        var quest = Catalog.GetByRowId(SweetWordsShadowyDealings)!;
        Assert.Equal("Sweet Words, Shadowy Dealings", quest.Name);
        Assert.Equal(7, quest.CarrierLevel);
        Assert.True(quest.PreviousQuests.IsEmpty, "the sheet lists no previous quest: the carrier level is the whole gate");

        var names = fixture.Bundle.BlockerNames();
        var level6 = StateResolver.Resolve(quest, Eligible(quest) with { CarrierLevel = 6 }, Catalog, Context);
        Assert.Equal(QuestState.Blocked, level6.State);
        Assert.Equal("Delivery Moogle: carrier level 7", BlockerText.For(level6, quest, names));
        Assert.Equal("Blocked · Delivery Moogle: carrier level 7", BlockerText.StatusText(level6, quest, names));

        Assert.Equal(QuestState.Ready, StateResolver.Resolve(quest, Eligible(quest) with { CarrierLevel = 7 }, Catalog, Context).State);

        // A level 50 character who never unlocked the Delivery Moogle: the live capture reads carrier level 0, a
        // real level, so the quest is Blocked (with no previous quest nothing else would hold it back).
        var never = StateResolver.Resolve(quest, Eligible(quest) with { CarrierLevel = 0 }, Catalog, Context);
        Assert.Equal(QuestState.Blocked, never.State);
        Assert.Equal("Blocked · Delivery Moogle: carrier level 7", BlockerText.StatusText(never, quest, names));

        // A file written before 0.6.2 has no carrier level: listed, not judged.
        var unread = StateResolver.Resolve(quest, Eligible(quest) with { CarrierLevel = null }, Catalog, Context);
        Assert.Equal(QuestState.Ready, unread.State);
        Assert.Contains(unread.Requirements, r => r.Req is CarrierLevelRequirement { ActualLevel: null } && r.Met);
    }

    [Fact]
    public void Hatching_tide_2014_chapter_2_waits_for_phase_2()
    {
        var quest = Catalog.GetByRowId(NothingToYolkAbout)!;
        Assert.Equal("Nothing to Yolk About", quest.Name);
        Assert.Equal(HatchingTide2014, quest.Festival);
        Assert.Equal(2, quest.FestivalBegin);
        Assert.Equal(5, quest.FestivalEnd);
        Assert.Equal([EggsigentCircumstances], quest.PreviousQuests.QuestIds);

        var names = fixture.Bundle.BlockerNames();
        var eligible = Eligible(quest) with { ActiveFestivals = [HatchingTide2014] };

        var phase1 = StateResolver.Resolve(quest, eligible with { ActiveFestivalPhases = [1] }, Catalog, Context);
        Assert.Equal(QuestState.Blocked, phase1.State);
        Assert.Equal(RequirementKind.Seasonal, phase1.NextStep!.Req.Kind);
        Assert.Equal("Seasonal: chapter not open yet", BlockerText.For(phase1, quest, names));
        Assert.True(phase1.IsOutOfSeason);

        var phase2 = StateResolver.Resolve(quest, eligible with { ActiveFestivalPhases = [2] }, Catalog, Context);
        Assert.Equal(QuestState.Ready, phase2.State);

        var phase6 = StateResolver.Resolve(quest, eligible with { ActiveFestivalPhases = [6] }, Catalog, Context);
        Assert.Equal(QuestState.Blocked, phase6.State);
        Assert.Equal("Seasonal: chapter over", BlockerText.For(phase6, quest, names));

        // No phase captured: the window is not applied, as before 0.6.2.
        Assert.Equal(QuestState.Ready, StateResolver.Resolve(quest, eligible, Catalog, Context).State);

        // Chapter 1 opens with the event, so phase 1 is enough for it.
        var chapter1 = Catalog.GetByRowId(EggsigentCircumstances)!;
        Assert.Equal(1, chapter1.FestivalBegin);
        var first = Eligible(chapter1) with { ActiveFestivals = [HatchingTide2014], ActiveFestivalPhases = [1] };
        Assert.Equal(QuestState.Ready, StateResolver.Resolve(chapter1, first, Catalog, Context).State);
    }

    [Fact]
    public void Every_windowed_quest_is_seasonal_and_every_seasonal_row_carries_an_end()
    {
        // The sheet facts the window logic relies on: 37 rows open at a later phase, all of them festival quests,
        // and every festival row carries a FestivalEnd (so Begin/End are never both 0 on a seasonal quest).
        var windowed = Catalog.All.Where(q => q.FestivalBegin > 0).ToArray();
        Assert.Equal(37, windowed.Length);
        Assert.All(windowed, q => Assert.NotEqual(0, q.Festival));
        Assert.All(windowed, q => Assert.True(q.FestivalEnd == 0 || q.FestivalEnd >= q.FestivalBegin, $"{q.RowId} {q.Name}: end {q.FestivalEnd} before begin {q.FestivalBegin}"));
        Assert.All(Catalog.All.Where(q => q.Festival != 0), q => Assert.NotEqual(0, q.FestivalEnd));
        Assert.All(Catalog.All.Where(q => q.Festival == 0), q => Assert.Equal(0, q.FestivalBegin));
    }

    [Fact]
    public void Only_festivals_with_two_or_more_windows_are_phased()
    {
        // 120 festivals; only these carry more than one distinct (Begin, End) window among their quests, so only
        // their windows are judged. The rest share one window ((0,1) for most) and keep the id-only check.
        Assert.Equal(new ushort[] { 3, 10, 11, 20, 22, 100, 104 }, Catalog.PhasedFestivals.Order());
        Assert.Equal(39, Catalog.All.Count(q => Catalog.PhasedFestivals.Contains(q.Festival)));
        Assert.Equal(310, Catalog.All.Count(q => q.Festival != 0));
        Assert.Contains(HatchingTide2014, Catalog.PhasedFestivals);
    }
}
