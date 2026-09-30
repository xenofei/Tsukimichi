using Lumina.Data;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Data;

namespace Tsukimichi.Tests.Plan;

/// <summary>The ContentFinderCondition sheet read as plan duty kinds, and the plan over the live duty index.</summary>
public class DutyIndexTests(GameDataFixture game) : IClassFixture<GameDataFixture>
{
    [Theory]
    [InlineData(DutyIndex.Dungeons, 1, UnlockKind.Dungeon)]
    [InlineData(DutyIndex.Trials, 1, UnlockKind.Trial)]
    [InlineData(DutyIndex.Raids, 1, UnlockKind.NormalRaid)]
    [InlineData(DutyIndex.Raids, 3, UnlockKind.AllianceRaid)]
    [InlineData(DutyIndex.UltimateRaids, 1, UnlockKind.NormalRaid)]
    [InlineData(DutyIndex.ChaoticAllianceRaid, 3, UnlockKind.AllianceRaid)]
    [InlineData(DutyIndex.Eureka, 144, UnlockKind.FieldOperation)]
    [InlineData(DutyIndex.SaveTheQueen, 72, UnlockKind.FieldOperation)]
    [InlineData(DutyIndex.OccultCrescent, 72, UnlockKind.FieldOperation)]
    [InlineData(DutyIndex.DeepDungeons, 1, UnlockKind.System)]
    [InlineData(DutyIndex.Guildhests, 1, UnlockKind.Other)]
    public void Content_types_map_to_plan_kinds(uint contentType, int partyCount, UnlockKind expected)
    {
        Assert.Equal(expected, DutyIndex.KindOf(contentType, partyCount));
    }

    [GameDataFact]
    public void The_sheet_types_known_duties()
    {
        var duties = DutyIndex.Build(game.Game.Excel, Language.English);
        Assert.True(duties.Count > 500, $"only {duties.Count} duties");

        void Expect(uint cfc, UnlockKind kind, string name)
        {
            Assert.True(duties.TryGetCondition(cfc, out var duty), $"CFC {cfc} missing");
            Assert.Equal(kind, duty.Kind);
            Assert.Equal(name, duty.Name);
        }

        Expect(2, UnlockKind.Dungeon, "the Tam-Tara Deepcroft");
        Expect(7, UnlockKind.Dungeon, "Halatali");
        Expect(56, UnlockKind.Trial, "the Bowl of Embers");
        Expect(93, UnlockKind.NormalRaid, "the Binding Coil of Bahamut - Turn 1");
        Expect(92, UnlockKind.AllianceRaid, "the Labyrinth of the Ancients");
        Expect(111, UnlockKind.AllianceRaid, "the World of Darkness");
        Expect(1010, UnlockKind.AllianceRaid, "the Cloud of Darkness (Chaotic)");
        Expect(283, UnlockKind.FieldOperation, "the Forbidden Land, Eureka Anemos");
        Expect(174, UnlockKind.System, "the Palace of the Dead (Floors 1-10)");

        Assert.True(duties.TryGetByName("Labyrinth of the Ancients", out var byName));
        Assert.Equal(92u, byName.ContentFinderConditionId);
        Assert.True(duties.TryGetInstance(byName.InstanceContentId, out var byInstance));
        Assert.Same(byName, byInstance);
    }

    [GameDataFact]
    public void Over_the_live_sheets_a_fresh_characters_first_dungeon_is_Halatali()
    {
        var bundle = game.Bundle;
        var curated = CuratedData.Load(FixtureCatalog.CuratedDir());
        var unique = UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        var features = FeaturePresets.Derive(bundle.Catalog, curated, unique.Entries);
        var rewards = UniqueRewardCatalog.Build(unique, new Dictionary<uint, UniqueOverride>(), curated);
        var names = bundle.BlockerNames();
        var tags = UnlockTags.Build(bundle.Catalog, features, rewards, DutyIndex.Build(game.Game.Excel, Language.English), names.Tribe);

        var states = StateResolver.ResolveAll(bundle.Catalog, PlanFixture.Fresh(), new EvalContext { ClassJobs = bundle.Jobs });
        var plan = UnlockPlan.Build(tags, states, names);
        var dungeons = plan.Filter(new PlanFilter(UnlockKinds.Mask(UnlockKind.Dungeon)));

        Assert.Equal(0, dungeons.Expansions[0].Expansion);
        var first = dungeons.Entries[0];
        Assert.Equal("Hallo Halatali", first.Quest.Name);
        Assert.Equal(new PlanUnlock(UnlockKind.Dungeon, "Halatali"), first.Unlocks[0]);

        // Every tagged kind is represented somewhere in the plan.
        foreach (var kind in UnlockKinds.All)
        {
            Assert.Contains(plan.Entries, e => e.PrimaryKind == kind);
        }
    }

    [GameDataFact]
    public void Over_the_live_sheets_no_plan_quest_that_opens_a_dungeon_trial_or_raid_is_only_Other()
    {
        var bundle = game.Bundle;
        var curated = CuratedData.Load(FixtureCatalog.CuratedDir());
        var unique = UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        var features = FeaturePresets.Derive(bundle.Catalog, curated, unique.Entries);
        var rewards = UniqueRewardCatalog.Build(unique, new Dictionary<uint, UniqueOverride>(), curated);
        var duties = DutyIndex.Build(game.Game.Excel, Language.English);
        var tags = UnlockTags.Build(bundle.Catalog, features, rewards, duties, bundle.BlockerNames().Tribe);

        var wrong = new List<string>();
        var dutyQuests = 0;
        foreach (var quest in tags.Quests)
        {
            var kinds = rewards.ForQuest(quest.RowId)
                .Where(e => e.Kind == Tsukimichi.Core.Model.RewardKind.DutyUnlock && duties.TryGetCondition(e.RewardId, out var d) && d.Kind <= UnlockKind.AllianceRaid)
                .Select(e => { duties.TryGetCondition(e.RewardId, out var d); return d.Kind; })
                .Distinct()
                .ToList();
            if (kinds.Count == 0)
            {
                continue;
            }

            dutyQuests++;
            var unlocks = tags.For(quest.RowId);
            if (unlocks.All(u => u.Kind == UnlockKind.Other) || kinds.Any(k => !unlocks.Any(u => u.Kind == k)))
            {
                wrong.Add($"{quest.RowId} {quest.Name}: opens {string.Join(", ", kinds)} but is tagged {string.Join("; ", unlocks.Select(u => u.Label))}");
            }
        }

        Assert.True(dutyQuests > 150, $"only {dutyQuests} plan quests open a dungeon, trial or raid");
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));

        // A quasi-quest is never only Other; A Relic Reborn is Trial and Job.
        Assert.All(tags.Quests.Where(q => q.EventIconType == FeaturePresets.QuasiQuestEventIconType), q =>
            Assert.Contains(tags.For(q.RowId), u => u.Kind != UnlockKind.Other));
        var relic = tags.For(66655);
        Assert.Contains(relic, u => u.Kind == UnlockKind.Trial);
        Assert.Contains(relic, u => u.Kind == UnlockKind.Job);
    }
}
