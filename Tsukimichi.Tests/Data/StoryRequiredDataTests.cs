using System.Text.Json.Nodes;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.Tests.Companions;
using Tsukimichi.Tests.Evaluation;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The side quests the story requires (feature plan v7 N3) over the frozen catalog with the shipped data: the
/// curated file's entries name main scenario quests and live side quests, and the requirements match the Console
/// Games Wiki's lists (Seventh Astral Era: four hard primal clears before Good Intentions, the four Crystal Tower raids
/// before A Time to Every Purpose; Shadowbringers: one role quest line before The Light of Inspiration is finished).
/// The duty part maps instances to Duty Finder entries through the game's sheets.
/// </summary>
public sealed class StoryRequiredDataTests(FixtureCatalog fixture, DutyRunFixture duties, ITestOutputHelper output)
    : IClassFixture<FixtureCatalog>, IClassFixture<DutyRunFixture>
{
    private const uint GoodIntentions = 65899;
    private const uint ATimeToEveryPurpose = 65961;
    private const uint TheLightOfInspiration = 69186;

    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    private CatchUpDutySource Source(Func<uint, uint>? conditionOf)
    {
        var data = UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        return CatchUpDutySource.From(fixture.Curated, UniqueRewardCatalog.Build(data, new Dictionary<uint, UniqueOverride>(), fixture.Curated), conditionOf);
    }

    [Fact]
    public void The_curated_entries_name_story_quests_and_live_side_quests()
    {
        var story = MsqGraph.For(Catalog).Story.Select(q => q.RowId).ToHashSet();
        var entries = fixture.Curated.StoryRequired;
        Assert.NotEmpty(entries);
        foreach (var (rowId, entry) in entries)
        {
            Assert.Contains(rowId, story);
            Assert.All(entry.Quests, id =>
            {
                Assert.True(Catalog.ByRowId.TryGetValue(id, out var quest), $"{id} is not in the catalog");
                Assert.False(quest.IsRemoved, $"{id} {quest.Name} is removed");
                Assert.DoesNotContain(id, story);
            });
        }

        // Raw file: schema 1, a note, keys ascending, and the loader dropped nothing.
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(FixtureCatalog.CuratedDir(), CuratedData.StoryRequiredFileName)), documentOptions: CuratedData.StrictOptions)!.AsObject();
        Assert.Equal(1, (int)root["schema"]!);
        Assert.False(string.IsNullOrWhiteSpace((string?)root["note"]));
        var keys = root["entries"]!.AsObject().Select(kv => uint.Parse(kv.Key, System.Globalization.CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(keys.Order(), keys);
        Assert.Equal(entries.Count, keys.Count);
    }

    [Fact]
    public void The_sheets_and_the_curated_file_give_the_story_its_side_quests()
    {
        var required = StoryRequirements.For(Catalog, Source(null));
        foreach (var r in required.All)
        {
            output.WriteLine($"{r.StoryQuest.RowId} {r.StoryQuest.Name} ({r.Source}, {r.Join}): " + string.Join(" | ", r.Options.Select(o => string.Join(", ", o.Select(id => Catalog.ByRowId[id].Name)))));
        }

        // A Time to Every Purpose: the Crystal Tower series, eight quests from Legacy of Allag to The Light of Hope.
        var crystal = Assert.Single(required.All, r => r.StoryQuest.RowId == ATimeToEveryPurpose);
        Assert.Equal(StoryRequirementSource.Prerequisite, crystal.Source);
        var tower = Assert.Single(crystal.Options);
        Assert.Equal(8, tower.Count);
        Assert.Equal("Legacy of Allag", Catalog.ByRowId[tower[0]].Name);
        Assert.Contains(tower, id => Catalog.ByRowId[id].Name == "Syrcus Tower");
        Assert.Contains(tower, id => Catalog.ByRowId[id].Name == "The World of Darkness");
        Assert.Equal("The Light of Hope", Catalog.ByRowId[tower[^1]].Name);
        Assert.Equal(tower.Distinct().Count(), tower.Count);

        // The Light of Inspiration: any one of the four role lines, six quests each, level 70 to 80.
        var roles = Assert.Single(required.All, r => r.StoryQuest.RowId == TheLightOfInspiration);
        Assert.Equal(StoryRequirementSource.Curated, roles.Source);
        Assert.Equal(JoinKind.Any, roles.Join);
        Assert.Equal(4, roles.Options.Count);
        Assert.All(roles.Options, line =>
        {
            Assert.Equal(6, line.Count);
            Assert.Equal(70, Catalog.ByRowId[line[0]].DisplayLevel);
            Assert.Equal(80, Catalog.ByRowId[line[^1]].DisplayLevel);
        });

        // Every requirement names live, non-repeatable side quests only.
        var story = MsqGraph.For(Catalog).Story.Select(q => q.RowId).ToHashSet();
        Assert.All(required.SideQuests, id =>
        {
            var quest = Catalog.ByRowId[id];
            Assert.False(quest.IsRemoved || quest.IsRepeatable, quest.Name);
            Assert.DoesNotContain(id, story);
        });
    }

    [GameDataFact]
    public void Good_Intentions_needs_the_three_hard_primals_unlocked_by_side_quests()
    {
        var required = StoryRequirements.For(Catalog, Source(instance => duties.Index.ByInstance(instance)?.ContentFinderConditionId ?? 0u));
        var primals = required.All.Where(r => r.StoryQuest.RowId == GoodIntentions).ToArray();
        Assert.All(primals, r => Assert.Equal(StoryRequirementSource.Duty, r.Source));
        // Each line ends with the quest that unlocks the duty; Ifrit's starts one quest earlier.
        var names = primals.Select(r => Catalog.ByRowId[Assert.Single(r.Options)[^1]].Name).Order().ToArray();
        output.WriteLine(string.Join(", ", names));
        Assert.Equal(["Ifrit Bleeds, We Can Kill It", "In a Titan Spot", "In for Garuda Awakening"], names);
        // The lines share their start (A Recurring Problem, then Ifrit Bleeds): four quests in all.
        Assert.Equal(4, primals.SelectMany(r => r.Options[0]).Distinct().Count());

        // The hard primals and the Crystal Tower raids wear Story-required; the Extreme trials do not.
        bool Story(string name) => required.IsStoryDuty(Assert.Single(duties.Index.All, d => d.Name == name).ContentFinderConditionId, Assert.Single(duties.Index.All, d => d.Name == name).InstanceContentId);
        Assert.True(Story("the Bowl of Embers (Hard)"));
        Assert.True(Story("Syrcus Tower"));
        Assert.True(Story("Sastasha"));
        Assert.False(Story("the Bowl of Embers (Extreme)"));
    }

    [GameDataFact]
    public void A_fresh_characters_catch_up_and_meter_count_them()
    {
        var source = Source(instance => duties.Index.ByInstance(instance)?.ContentFinderConditionId ?? 0u);
        var snapshot = Fixture.Snapshot() with { JobLevels = Fixture.Levels((1, 1)), LevelCap = 100, MaxExpansion = 5 };
        var states = StateResolver.ResolveAll(Catalog, snapshot, EvalContext.Default);
        var summary = MsqCatchUp.Compute(Catalog, states, snapshot, source);
        Assert.NotNull(summary);
        foreach (var part in summary.Expansions)
        {
            output.WriteLine($"expansion {part.Expansion}: {part.Quests} story quests, {part.SideQuests} side quests");
        }

        // A Realm Reborn: the Crystal Tower's eight and the four hard primal quests (Ifrit's line is two), the twelve of the
        // wiki's "92 quests if mandatory sidequests are included" over 80. Shadowbringers: one role quest line, six.
        var arr = summary.Expansions.Single(e => e.Expansion == 0);
        Assert.Equal(12, arr.SideQuests);
        Assert.Equal(6, summary.Expansions.Single(e => e.Expansion == 3).SideQuests);

        var meter = StoryMeter.Compute(Catalog, states, source);
        Assert.NotNull(meter);
        Assert.Equal(summary.SideQuests, meter.SideTotal - meter.SideDone);
        Assert.Equal(0, meter.Percent);
    }
}
