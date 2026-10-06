using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Art;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>
/// The Far Shore follows Tsukimichi's spoiler shield (the owner's decision, plan v9): every Far Shore stage and every
/// shipped scene is set on the shield's era scale; a stage set past the player's story is veiled (its name the shield's
/// placeholder, its levels nameless, sceneless and closed to every mode) until the story reaches it or its place is
/// revealed; a scene set past the story is never drawn. The shield here is a stand-in that hides an area past a story
/// era (the real one, over the game's data, is <c>MoonfallShieldGameDataTests</c>).
/// </summary>
public sealed class MoonfallShieldTests
{
    private static readonly IReadOnlyDictionary<string, byte> EraOfZone = BuildEras();

    private static Dictionary<string, byte> BuildEras()
    {
        var eras = new Dictionary<string, byte>(StringComparer.Ordinal);
        foreach (var stage in MoonfallStages.Of(MoonfallCampaignKind.Expansion))
        {
            if (MoonfallPlaces.OfStage(stage) is { Zone: { } zone } place)
            {
                eras[zone] = place.Era;
            }
        }

        foreach (var place in MoonfallPlaces.Scenes.Values.Append(MoonfallPlaces.OfBackdrop(MoonfallBackdrop.Title)))
        {
            if (place.Zone is { } zone)
            {
                eras[zone] = place.Era;
            }
        }

        return eras;
    }

    /// <summary>A shield whose story has reached <paramref name="reach"/>: it hides an area of a later era, unless revealed.</summary>
    private static MoonfallShield StoryAt(byte reach, params string[] revealed) => new(
        zone => EraOfZone.TryGetValue(zone, out var era) && era > reach && !revealed.Contains(zone, StringComparer.Ordinal),
        zone => $"Era {EraOfZone[zone]} area",
        () => reach + (revealed.Length * 100));

    private static MoonfallCampaigns Full()
    {
        var shape = MoonfallCampaigns.LoadBuiltIn().Base.Levels[0];
        MoonfallCampaign Make(MoonfallCampaignKind kind) =>
            new(kind, Enumerable.Range(0, MoonfallStages.LevelCount(kind)).Select(i => shape with { Id = MoonfallStages.LevelId(kind, i) }).ToList());
        return new MoonfallCampaigns(Make(MoonfallCampaignKind.Base), Make(MoonfallCampaignKind.Expansion), []);
    }

    /// <summary>Every level of both campaigns reached (all but the last won), so only the shield can close one.</summary>
    private static MoonfallModes Everything(MoonfallShield shield)
    {
        var progress = new MoonfallProgress { BaseCleared = MoonfallStages.BaseLevels, ExpansionCleared = MoonfallStages.ExpansionLevels - 1 };
        for (var i = 0; i < MoonfallStages.ExpansionLevels - 1; i++)
        {
            progress.Levels[MoonfallStages.LevelId(MoonfallCampaignKind.Expansion, i)] = new MoonfallLevelRecord { Cleared = true, Best = 50_000 };
        }

        for (var i = 0; i < MoonfallStages.BaseLevels; i++)
        {
            progress.Levels[MoonfallStages.LevelId(MoonfallCampaignKind.Base, i)] = new MoonfallLevelRecord { Cleared = true, Best = 50_000 };
        }

        return new MoonfallModes(Full(), progress, MoonfallStory.Everyone, MoonfallChallenges.LoadBuiltIn().Challenges) { Shield = shield };
    }

    [Fact]
    public void Every_far_shore_stage_is_set_on_the_shields_era_scale_and_the_moon_roads_nowhere()
    {
        var far = MoonfallStages.Of(MoonfallCampaignKind.Expansion);
        Assert.Equal(12, far.Count);
        foreach (var stage in far)
        {
            var place = MoonfallPlaces.OfStage(stage);
            Assert.InRange(place.Era, MoonfallPlaces.ARealmReborn, MoonfallPlaces.Dawntrail);
            Assert.True(place.Zone is not null || stage.Number == 6, $"stage {stage.Number} {stage.Name} names no place");
        }

        // The owner's examples: Sharlayan's domes and the archons are Endwalker; the Sea of Sorrows is Endwalker's moon.
        Assert.Equal(new MoonfallPlace(MoonfallPlaces.Endwalker, "Old Sharlayan"), MoonfallPlaces.OfStage(far[8]));
        Assert.Equal(MoonfallPlaces.Endwalker, MoonfallPlaces.OfStage(far[9]).Era);
        Assert.Equal(new MoonfallPlace(MoonfallPlaces.Endwalker, "Mare Lamentorum"), MoonfallPlaces.OfStage(far[11]));
        Assert.Equal("The Floating Market", far[7].Name);

        foreach (var stage in MoonfallStages.Of(MoonfallCampaignKind.Base))
        {
            Assert.Equal(MoonfallPlace.Nowhere, MoonfallPlaces.OfStage(stage));
        }
    }

    [Fact]
    public void Every_shipped_scene_is_set_on_the_shields_era_scale()
    {
        var recipes = MoonfallSceneRecipeLoader.LoadBuiltIn();
        Assert.NotEmpty(recipes);
        foreach (var name in recipes.Keys)
        {
            Assert.True(MoonfallPlaces.OfScene(name) is { } place && place.Era <= MoonfallPlaces.Dawntrail, $"scene {name} has no place");
        }

        // Kugane's painting is Stormblood's.
        Assert.Equal(new MoonfallPlace(MoonfallPlaces.Stormblood, "Kugane"), MoonfallPlaces.OfScene("lantern-night"));
    }

    [Theory]
    [InlineData(MoonfallPlaces.ARealmReborn)]
    [InlineData(MoonfallPlaces.Heavensward)]
    [InlineData(MoonfallPlaces.Stormblood)]
    [InlineData(MoonfallPlaces.Shadowbringers)]
    [InlineData(MoonfallPlaces.Endwalker)]
    [InlineData(MoonfallPlaces.Dawntrail)]
    public void A_stage_set_past_the_story_is_veiled_and_closed_to_every_mode_at_each_era_boundary(byte reach)
    {
        var modes = Everything(StoryAt(reach));
        var views = modes.Stages(MoonfallCampaignKind.Expansion);
        var quick = modes.QuickPlayLevels();
        foreach (var view in views)
        {
            var place = MoonfallPlaces.OfStage(view.Stage);
            var past = place.Zone is not null && place.Era > reach;
            Assert.Equal(past, modes.StageVeiled(view.Stage));
            Assert.Equal(past, view.State == MoonfallStageState.Veiled);
            Assert.Equal(past, MoonfallLooks.Stop(view).Veiled);
            Assert.False(MoonfallLooks.Stop(view).Padlock && past, "a veiled stop shows the shield's mark, not the padlock");
            foreach (var slot in view.Levels)
            {
                Assert.Equal(past, slot.State == MoonfallLevelState.Veiled);
                Assert.Equal(!past, slot.Reached);
                Assert.Equal(!past, modes.Adventure(slot.Place.Campaign, slot.Place.Index, MoonfallCompanion.Minfilia) is not null);
                Assert.Equal(!past, modes.QuickPlay(slot.Id, MoonfallCompanion.None) is not null);
                Assert.Equal(!past, modes.StartDuel(slot.Id, MoonfallCompanion.None, MoonfallCompanion.Minfilia, MoonfallAiDifficulty.Novice, 1) is not null);
                Assert.Equal(!past, quick.Any(q => q.Id == slot.Id));
            }
        }
    }

    [Fact]
    public void Revealing_a_place_opens_its_stage_and_no_other()
    {
        var far = MoonfallStages.Of(MoonfallCampaignKind.Expansion);
        var before = Everything(StoryAt(MoonfallPlaces.Shadowbringers));
        Assert.True(before.StageVeiled(far[8]));
        Assert.True(before.StageVeiled(far[9]));

        // "Reveal this name" on Old Sharlayan's placeholder: the Domes open, the Archon's Crossing stays veiled.
        var after = Everything(StoryAt(MoonfallPlaces.Shadowbringers, "Old Sharlayan"));
        Assert.False(after.StageVeiled(far[8]));
        Assert.Equal("The Domes of Sharlayan", after.StageName(far[8]));
        Assert.True(after.Adventure(MoonfallCampaignKind.Expansion, far[8].FirstLevelIndex, MoonfallCompanion.None) is not null);
        Assert.True(after.StageVeiled(far[9]));
        Assert.NotEqual(before.Shield.Version, after.Shield.Version);
    }

    [Fact]
    public void No_veiled_stages_name_or_place_reaches_a_string_the_screens_draw()
    {
        var modes = Everything(StoryAt(MoonfallPlaces.ARealmReborn));
        foreach (var stage in MoonfallStages.Of(MoonfallCampaignKind.Expansion))
        {
            var place = MoonfallPlaces.OfStage(stage);
            var shown = modes.StageName(stage);
            if (!modes.StageVeiled(stage))
            {
                Assert.Equal(stage.Name, shown);
                continue;
            }

            // The name the map, level select and the companions' panel print is the shield's placeholder, and the zone
            // the reveal needs is the only other trace of it, never printed.
            Assert.Equal($"Era {place.Era} area", shown);
            Assert.DoesNotContain(stage.Name, shown, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(place.Zone!, shown, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(place.Zone, modes.VeiledZone(stage));
            foreach (var id in stage.LevelIds)
            {
                Assert.True(modes.LevelVeiled(id));
            }
        }

        // The Moon Road is never veiled.
        foreach (var stage in MoonfallStages.Of(MoonfallCampaignKind.Base))
        {
            Assert.False(modes.StageVeiled(stage));
            Assert.Equal(stage.Name, modes.StageName(stage));
        }
    }

    [Fact]
    public void A_scene_set_past_the_story_is_veiled_and_a_veiled_stages_level_shows_no_scene()
    {
        var level = MoonfallCampaigns.LoadBuiltIn().Base.Levels[0];
        Assert.True(Everything(StoryAt(MoonfallPlaces.Heavensward)).SceneVeiled(level, "lantern-night"));
        Assert.False(Everything(StoryAt(MoonfallPlaces.Stormblood)).SceneVeiled(level, "lantern-night"));
        Assert.False(Everything(StoryAt(MoonfallPlaces.ARealmReborn)).SceneVeiled(level, "moon-road-night"));

        // A level of a veiled stage shows no scene, whatever the scene's own place.
        var sharlayan = level with { Id = MoonfallStages.LevelId(MoonfallCampaignKind.Expansion, MoonfallStages.Of(MoonfallCampaignKind.Expansion)[8].FirstLevelIndex) };
        Assert.True(Everything(StoryAt(MoonfallPlaces.Shadowbringers)).SceneVeiled(sharlayan, "moon-road-night"));
        Assert.False(Everything(StoryAt(MoonfallPlaces.Endwalker)).SceneVeiled(sharlayan, "moon-road-night"));
    }

    /// <summary>The Moon Road won and nothing of the Far Shore yet, every Far Shore level built.</summary>
    private static MoonfallModes FarShoreStart(MoonfallShield shield) =>
        new(Full(), new MoonfallProgress { BaseCleared = MoonfallStages.BaseLevels }, MoonfallStory.Everyone, MoonfallChallenges.LoadBuiltIn().Challenges) { Shield = shield };

    /// <summary>Plays Adventure as Continue leads it, winning each level, until it has nothing left to play.</summary>
    private static void WalkTheRoad(MoonfallModes modes)
    {
        for (var guard = 0; guard < 200 && modes.Continue() is { } next; guard++)
        {
            var start = modes.Adventure(next.Campaign, next.Index, MoonfallCompanion.Minfilia);
            Assert.NotNull(start);
            modes.Progress.RecordLevel(start.LevelId, won: true, score: 100_000);
        }
    }

    [Theory]
    [InlineData(MoonfallPlaces.ARealmReborn)]
    [InlineData(MoonfallPlaces.Heavensward)]
    [InlineData(MoonfallPlaces.Stormblood)]
    [InlineData(MoonfallPlaces.Shadowbringers)]
    [InlineData(MoonfallPlaces.Endwalker)]
    [InlineData(MoonfallPlaces.Dawntrail)]
    public void Adventure_steps_over_a_veiled_stage_so_every_stage_the_story_allows_is_reached_at_each_era(byte reach)
    {
        // The owner's "step over it": walking the Far Shore with nothing revealed wins every stage not set past the
        // story, whatever veiled stage stands before it; the veiled ones wait, unwon.
        var modes = FarShoreStart(StoryAt(reach));
        WalkTheRoad(modes);
        var veiledLeft = new List<int>();
        foreach (var view in modes.Stages(MoonfallCampaignKind.Expansion))
        {
            var past = MoonfallPlaces.OfStage(view.Stage) is { Zone: not null } place && place.Era > reach;
            if (past)
            {
                Assert.Equal(MoonfallStageState.Veiled, view.State);
                Assert.All(view.Levels, slot => Assert.False(modes.Progress.IsCleared(slot.Id)));
                veiledLeft.Add(view.Stage.Number);
            }
            else
            {
                Assert.True(view.State == MoonfallStageState.Done, $"stage {view.Stage.Number} {view.Stage.Name} was not reached at era {reach}");
            }
        }

        // Before Endwalker, from Heavensward, the moogle's Storm Post (stage 11, the Churning Mists) is reached.
        if (reach is >= MoonfallPlaces.Heavensward and < MoonfallPlaces.Endwalker)
        {
            Assert.DoesNotContain(11, veiledLeft);
        }

        // The Far Shore is not complete while a veiled stage waits, and Continue's sibling points at the first of them.
        Assert.Equal(veiledLeft.Count == 0, modes.Progress.ExpansionCleared == MoonfallStages.ExpansionLevels);
        var waiting = modes.Next();
        if (veiledLeft.Count > 0)
        {
            Assert.Equal(new MoonfallNext(new MoonfallLevelPlace(MoonfallCampaignKind.Expansion, (veiledLeft[0] - 1) * MoonfallCharacters.LevelsPerStage), true), waiting);
            Assert.Null(modes.Continue());
            Assert.True(modes.Stages(MoonfallCampaignKind.Expansion)[veiledLeft[0] - 1].Here);
        }
        else
        {
            Assert.Null(waiting);
        }
    }

    [Fact]
    public void Continue_points_at_the_next_playable_stage_past_a_veiled_one_and_the_tally_steps_over_it()
    {
        // At Stormblood, stage 7 (Il Mheg) is past the story: with stages 1 to 6 won, Continue goes on to stage 8.
        var modes = FarShoreStart(StoryAt(MoonfallPlaces.Stormblood));
        for (var i = 0; i < 6 * MoonfallCharacters.LevelsPerStage; i++)
        {
            modes.Progress.RecordLevel(MoonfallStages.LevelId(MoonfallCampaignKind.Expansion, i), won: true, score: 100_000);
        }

        Assert.Equal(new MoonfallLevelPlace(MoonfallCampaignKind.Expansion, 7 * MoonfallCharacters.LevelsPerStage), modes.Continue());
        var views = modes.Stages(MoonfallCampaignKind.Expansion);
        Assert.Equal(MoonfallStageState.Veiled, views[6].State);
        Assert.True(views[6].Reached);
        Assert.False(MoonfallLooks.Stop(views[6]).Padlock);
        Assert.True(views[7].Here);

        // The tally after 6-5 offers 8-1, stepping over stage 7.
        Assert.Equal(7 * MoonfallCharacters.LevelsPerStage, modes.NextLevel(MoonfallCampaignKind.Expansion, (6 * MoonfallCharacters.LevelsPerStage) - 1, out var steppedOver));
        Assert.Equal(7, steppedOver?.Number);

        // A veiled stage the road has not come to yet carries the padlock beside the shield's mark.
        Assert.False(views[8].Reached);
        Assert.True(MoonfallLooks.Stop(views[8]).Veiled);
        Assert.True(MoonfallLooks.Stop(views[8]).Padlock);
    }

    [Fact]
    public void A_veiled_stage_at_the_end_of_the_road_is_what_continue_reports_not_road_goes_on()
    {
        // At Shadowbringers with all but the Endwalker stages won, nothing is left to play: Continue has no level, and
        // its sibling reports the first veiled stage (9), so the title says the road waits past the story.
        var modes = FarShoreStart(StoryAt(MoonfallPlaces.Shadowbringers));
        WalkTheRoad(modes);
        Assert.Null(modes.Continue());
        Assert.Equal(new MoonfallNext(new MoonfallLevelPlace(MoonfallCampaignKind.Expansion, 8 * MoonfallCharacters.LevelsPerStage), true), modes.Next());

        // After 8-5 the tally steps over stages 9 and 10 to 11-1 (Storm Post, reached). After 11-5 it has no Next: it
        // steps over stage 12 to the road's end, and names the veiled stage.
        Assert.Equal(10 * MoonfallCharacters.LevelsPerStage, modes.NextLevel(MoonfallCampaignKind.Expansion, (8 * MoonfallCharacters.LevelsPerStage) - 1, out var over));
        Assert.Equal(9, over?.Number);
        Assert.Null(modes.NextLevel(MoonfallCampaignKind.Expansion, (11 * MoonfallCharacters.LevelsPerStage) - 1, out var veil));
        Assert.Equal(12, veil?.Number);

        // A reveal of Old Sharlayan opens stage 9: Continue goes there.
        var revealed = new MoonfallModes(modes.Campaigns, modes.Progress, MoonfallStory.Everyone, modes.Challenges) { Shield = StoryAt(MoonfallPlaces.Shadowbringers, "Old Sharlayan") };
        Assert.Equal(new MoonfallLevelPlace(MoonfallCampaignKind.Expansion, 8 * MoonfallCharacters.LevelsPerStage), revealed.Continue());
    }

    [Fact]
    public void A_scene_whose_place_alone_is_past_the_story_falls_back_and_a_veiled_stages_scene_hides()
    {
        var level = MoonfallCampaigns.LoadBuiltIn().Base.Levels[0];
        var modes = Everything(StoryAt(MoonfallPlaces.ARealmReborn));
        Assert.Equal(MoonfallSceneHide.Fallback, modes.SceneHide(level, "lantern-night"));
        Assert.Equal(MoonfallSceneHide.Shown, modes.SceneHide(level, "moon-road-night"));
        var sharlayan = level with { Id = MoonfallStages.LevelId(MoonfallCampaignKind.Expansion, MoonfallStages.Of(MoonfallCampaignKind.Expansion)[8].FirstLevelIndex) };
        Assert.Equal(MoonfallSceneHide.Hidden, modes.SceneHide(sharlayan, "moon-road-night"));

        // Every scene the shield can hide declares a story-safe fallback of no place.
        foreach (var (name, recipe) in MoonfallSceneRecipeLoader.LoadBuiltIn())
        {
            if (MoonfallPlaces.OfScene(name) is { Zone: not null })
            {
                Assert.True(recipe.Fallback is { } fallback && MoonfallPlaces.OfScene(fallback) is null or { Zone: null }, $"{name} has no story-safe fallback");
            }
        }

        // The title's backdrop is tagged too (Sohm Al, Heavensward): the menus follow the same rule.
        Assert.Equal(MoonfallPlaces.Heavensward, MoonfallPlaces.OfBackdrop(MoonfallBackdrop.Title).Era);
        Assert.Equal(MoonfallPlace.Nowhere, MoonfallPlaces.OfBackdrop(MoonfallBackdrop.Chart));
    }

    [Fact]
    public void The_open_shield_hides_nothing()
    {
        var modes = Everything(MoonfallShield.Open);
        Assert.All(modes.Stages(MoonfallCampaignKind.Expansion), view => Assert.NotEqual(MoonfallStageState.Veiled, view.State));
    }
}
