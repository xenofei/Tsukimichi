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

        foreach (var place in MoonfallPlaces.Scenes.Values.Append(MoonfallPlaces.OfBackdrop(MoonfallBackdrop.Title)).Append(MoonfallPlaces.OfBackdrop(MoonfallBackdrop.TitleEarly)))
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
            // Our own two paintings name no place: the Ferry in the Stars (6) and the Courier's Wake (11, the owner's
            // answer of 6 October 2026, so the moogle opens at every era).
            Assert.True((place.Zone is not null) == (stage.Number is not (6 or 11)), $"stage {stage.Number} {stage.Name}: only 6 and 11 name no place");
        }

        // The owner's examples: Sharlayan's domes and the archons are Endwalker; the Sea of Sorrows is Endwalker's moon.
        Assert.Equal(new MoonfallPlace(MoonfallPlaces.Endwalker, "Old Sharlayan"), MoonfallPlaces.OfStage(far[8]));
        Assert.Equal(MoonfallPlaces.Endwalker, MoonfallPlaces.OfStage(far[9]).Era);
        Assert.Equal(new MoonfallPlace(MoonfallPlaces.Endwalker, "Mare Lamentorum"), MoonfallPlaces.OfStage(far[11]));
        Assert.Equal("The Floating Market", far[7].Name);
        Assert.Equal(MoonfallPlace.Nowhere, MoonfallPlaces.OfStage(far[10]));

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

    /// <summary>The Moon Road built and won, and the Far Shore's first <paramref name=stagesBuilt/> stages built.</summary>
    private static MoonfallModes PartlyBuilt(MoonfallShield shield, int stagesBuilt)
    {
        var shape = MoonfallCampaigns.LoadBuiltIn().Base.Levels[0];
        var full = Full();
        var far = Enumerable.Range(0, stagesBuilt * MoonfallCharacters.LevelsPerStage).Select(i => shape with { Id = MoonfallStages.LevelId(MoonfallCampaignKind.Expansion, i) }).ToList();
        var campaigns = new MoonfallCampaigns(full.Base, new MoonfallCampaign(MoonfallCampaignKind.Expansion, far), []);
        return new MoonfallModes(campaigns, new MoonfallProgress { BaseCleared = MoonfallStages.BaseLevels }, MoonfallStory.Everyone, []) { Shield = shield };
    }

    [Fact]
    public void Nothing_built_yet_is_never_called_past_the_story()
    {
        // The Moon Road won and no Far Shore level built, at A Realm Reborn: the road goes on, no reveal is asked for.
        var modes = PartlyBuilt(StoryAt(MoonfallPlaces.ARealmReborn), 0);
        Assert.Null(modes.Next());
        Assert.Null(modes.Continue());
    }

    [Fact]
    public void A_partly_built_far_shore_waits_on_its_unbuilt_stages_as_coming_not_veiled()
    {
        // Heavensward, stages 1 to 3 built and won: stage 4 (the Ruby Sea, Stormblood) is veiled but not built, stage 5
        // not built. Nothing waits past the story; stage 5 is "levels on their way".
        var modes = PartlyBuilt(StoryAt(MoonfallPlaces.Heavensward), 3);
        WalkTheRoad(modes);
        Assert.Null(modes.Next());
        var stage5 = modes.Stages(MoonfallCampaignKind.Expansion)[4];
        Assert.True(MoonfallLooks.Coming(stage5, modes.Frontier(MoonfallCampaignKind.Expansion)));
        Assert.Null(modes.NextLevel(MoonfallCampaignKind.Expansion, (3 * MoonfallCharacters.LevelsPerStage) - 1, out var veil));
        Assert.Null(veil);

        // Stage 4 is veiled and the road has come to it, but none of its levels is built: the map offers no reveal there
        // (a reveal would spend a later expansion's name on "levels on their way"; m14). The map's rule is
        // Veiled && Reached && StageBuilt (the lint holds the map to it).
        var stage4 = modes.Stages(MoonfallCampaignKind.Expansion)[3];
        Assert.Equal(MoonfallStageState.Veiled, stage4.State);
        Assert.True(stage4.Reached);
        Assert.False(modes.StageBuilt(stage4.Stage));
        Assert.True(modes.StageBuilt(modes.Stages(MoonfallCampaignKind.Expansion)[2].Stage));
    }

    [Fact]
    public void A_level_the_road_stepped_to_stays_open_when_the_story_moves_on_unplayed()
    {
        // Heavensward, walked until Continue is 11-1 (stages 4, 7 to 10 and 12 stepped over), 11-1 not played. The story
        // reaches Stormblood: stages 4 and 8 unveil and the frontier returns to 4-1, but 11-1 stays open (the road's
        // high-water mark) and the moogle stays reached (m15).
        var heavensward = FarShoreStart(StoryAt(MoonfallPlaces.Heavensward));
        var stormPost = 10 * MoonfallCharacters.LevelsPerStage;
        for (var guard = 0; guard < 200 && heavensward.Continue() is { } next && next.Index < stormPost; guard++)
        {
            heavensward.Progress.RecordLevel(MoonfallStages.LevelId(next.Campaign, next.Index), won: true, score: 100_000);
            heavensward.NoteReach();
        }

        Assert.Equal(new MoonfallLevelPlace(MoonfallCampaignKind.Expansion, stormPost), heavensward.Continue());
        Assert.Equal(stormPost, heavensward.Progress.Reach(MoonfallCampaignKind.Expansion));

        var stormblood = new MoonfallModes(heavensward.Campaigns, heavensward.Progress, MoonfallStory.Everyone, []) { Shield = StoryAt(MoonfallPlaces.Stormblood) };
        Assert.Equal(3 * MoonfallCharacters.LevelsPerStage, stormblood.Frontier(MoonfallCampaignKind.Expansion));
        Assert.Equal(MoonfallLevelState.Open, stormblood.Slot(MoonfallCampaignKind.Expansion, stormPost).State);
        Assert.Equal(MoonfallCompanionState.Available, stormblood.CompanionState(MoonfallCompanion.Moogle));

        // Only the level the road came to: the one after it waits for a win, as ever.
        Assert.Equal(MoonfallLevelState.Sealed, stormblood.Slot(MoonfallCampaignKind.Expansion, stormPost + 1).State);

        // The mark is saved and merged like the counts: a copy keeps it, and a merge keeps the further.
        Assert.Equal(stormPost, heavensward.Progress.Copy().Reach(MoonfallCampaignKind.Expansion));
        var behind = new MoonfallProgress { ExpansionReach = 3 };
        behind.Absorb(heavensward.Progress);
        Assert.Equal(stormPost, behind.Reach(MoonfallCampaignKind.Expansion));
    }

    [Fact]
    public void Revealing_a_stepped_over_stage_never_closes_the_level_continue_had_offered()
    {
        // Heavensward after 3-5: the road steps over stage 4 (The Ruby Sea) and Continue offers 5-1. Revealing stage 4
        // from its own pill pulls the frontier back to 4-1, and 5-1 stays open (m15, case 1b).
        var heavensward = FarShoreStart(StoryAt(MoonfallPlaces.Heavensward));
        var five = 4 * MoonfallCharacters.LevelsPerStage;
        for (var i = 0; i < 3 * MoonfallCharacters.LevelsPerStage; i++)
        {
            heavensward.Progress.RecordLevel(MoonfallStages.LevelId(MoonfallCampaignKind.Expansion, i), won: true, score: 100_000);
            heavensward.NoteReach();
        }

        Assert.Equal(new MoonfallLevelPlace(MoonfallCampaignKind.Expansion, five), heavensward.Continue());
        var revealed = new MoonfallModes(heavensward.Campaigns, heavensward.Progress, MoonfallStory.Everyone, []) { Shield = StoryAt(MoonfallPlaces.Heavensward, "The Ruby Sea") };
        Assert.Equal(3 * MoonfallCharacters.LevelsPerStage, revealed.Frontier(MoonfallCampaignKind.Expansion));
        Assert.Equal(MoonfallLevelState.Open, revealed.Slot(MoonfallCampaignKind.Expansion, five).State);
        Assert.Equal(MoonfallStageState.Open, revealed.Stages(MoonfallCampaignKind.Expansion)[4].State);
    }

    [Fact]
    public void A_companion_is_reached_only_once_its_stage_has_a_level_built_and_reached()
    {
        // The Moon Road's stage 1 built and won, stage 2 not built: the frontier stands on 2-1 (Missing), and the twins
        // stay dimmed, as the map says "Levels on their way" (GD m18). Shipping 2-1 opens it, and the twins with it.
        var shape = MoonfallCampaigns.LoadBuiltIn().Base.Levels[0];
        MoonfallModes Built(int baseLevels)
        {
            var levels = Enumerable.Range(0, baseLevels).Select(i => shape with { Id = MoonfallStages.LevelId(MoonfallCampaignKind.Base, i) }).ToList();
            var campaigns = new MoonfallCampaigns(new MoonfallCampaign(MoonfallCampaignKind.Base, levels), new MoonfallCampaign(MoonfallCampaignKind.Expansion, []), []);
            var progress = new MoonfallProgress();
            for (var i = 0; i < MoonfallCharacters.LevelsPerStage; i++)
            {
                progress.RecordLevel(MoonfallStages.LevelId(MoonfallCampaignKind.Base, i), won: true, score: 100_000);
            }

            return new MoonfallModes(campaigns, progress, MoonfallStory.Everyone, []);
        }

        var stageOne = Built(MoonfallCharacters.LevelsPerStage);
        Assert.Equal(MoonfallCharacters.LevelsPerStage, stageOne.Frontier(MoonfallCampaignKind.Base));
        Assert.Equal(MoonfallLevelState.Missing, stageOne.Slot(MoonfallCampaignKind.Base, MoonfallCharacters.LevelsPerStage).State);
        Assert.Equal(MoonfallCompanionState.MetNotReached, stageOne.CompanionState(MoonfallCompanion.Twins));

        var twoOne = Built(MoonfallCharacters.LevelsPerStage + 1);
        Assert.Equal(MoonfallLevelState.Open, twoOne.Slot(MoonfallCampaignKind.Base, MoonfallCharacters.LevelsPerStage).State);
        Assert.Equal(MoonfallCompanionState.Available, twoOne.CompanionState(MoonfallCompanion.Twins));
    }

    [Fact]
    public void Continue_goes_on_at_the_level_the_road_came_to_when_the_frontier_falls_back_onto_an_unbuilt_stage()
    {
        // Far Shore stages 1, 2, 5, 6 and 11 built; walked at A Realm Reborn to 11-1 (stages 3, 4 and 7 to 10 stepped
        // over), 11-1 not played. The story reaches Heavensward: stage 3 unveils, unbuilt, and the frontier falls back
        // onto 3-1 (Missing). 11-1 is still open by the reach mark, and Continue goes there (GD n15).
        var shape = MoonfallCampaigns.LoadBuiltIn().Base.Levels[0];
        var built = new[] { 1, 2, 5, 6, 11 };
        var far = Enumerable.Range(0, MoonfallStages.ExpansionLevels)
            .Where(i => built.Contains((i / MoonfallCharacters.LevelsPerStage) + 1))
            .Select(i => shape with { Id = MoonfallStages.LevelId(MoonfallCampaignKind.Expansion, i) }).ToList();
        var campaigns = new MoonfallCampaigns(Full().Base, new MoonfallCampaign(MoonfallCampaignKind.Expansion, far), []);
        var arr = new MoonfallModes(campaigns, new MoonfallProgress { BaseCleared = MoonfallStages.BaseLevels }, MoonfallStory.Everyone, []) { Shield = StoryAt(MoonfallPlaces.ARealmReborn) };
        var stormPost = 10 * MoonfallCharacters.LevelsPerStage;
        for (var guard = 0; guard < 200 && arr.Continue() is { } next && next.Index < stormPost; guard++)
        {
            arr.Progress.RecordLevel(MoonfallStages.LevelId(next.Campaign, next.Index), won: true, score: 100_000);
            arr.NoteReach();
        }

        Assert.Equal(new MoonfallLevelPlace(MoonfallCampaignKind.Expansion, stormPost), arr.Continue());
        Assert.Equal(stormPost, arr.Progress.Reach(MoonfallCampaignKind.Expansion));
        var heavensward = new MoonfallModes(campaigns, arr.Progress, MoonfallStory.Everyone, []) { Shield = StoryAt(MoonfallPlaces.Heavensward) };
        Assert.Equal(2 * MoonfallCharacters.LevelsPerStage, heavensward.Frontier(MoonfallCampaignKind.Expansion));
        Assert.Equal(MoonfallLevelState.Missing, heavensward.Slot(MoonfallCampaignKind.Expansion, 2 * MoonfallCharacters.LevelsPerStage).State);
        Assert.Equal(new MoonfallNext(new MoonfallLevelPlace(MoonfallCampaignKind.Expansion, stormPost), false), heavensward.Next());
        Assert.Equal(new MoonfallLevelPlace(MoonfallCampaignKind.Expansion, stormPost), heavensward.Continue());
    }

    [Fact]
    public void Winning_the_moon_roads_last_level_leads_on_to_the_far_shore_not_to_the_last_level_note()
    {
        // Base-55 won with the Far Shore built: Adventure's next within The Moon Road is none, but Next() is FS 1-1,
        // playable, so the tally offers it (m16) and the "last level for now" note is not drawn.
        var modes = FarShoreStart(StoryAt(MoonfallPlaces.ARealmReborn));
        Assert.Null(modes.NextLevel(MoonfallCampaignKind.Base, MoonfallStages.BaseLevels - 1, out var over));
        Assert.Null(over);
        Assert.Equal(new MoonfallNext(new MoonfallLevelPlace(MoonfallCampaignKind.Expansion, 0), false), modes.Next());
    }

    [Fact]
    public void The_courier_stages_shipped_levels_show_scenes_of_no_place()
    {
        // Stage 11 is set nowhere so the moogle opens at every era; its levels' scenes must name no place either, or the
        // stage would show a scene past an early story (the owner's answer of 6 October 2026).
        var stage = MoonfallStages.Of(MoonfallCampaignKind.Expansion)[10];
        foreach (var level in MoonfallCampaigns.LoadBuiltIn().Expansion.Levels.Where(l => stage.LevelIds.Contains(l.Id)))
        {
            Assert.True(MoonfallPlaces.OfScene(level.Scene) is null or { Zone: null }, $"{level.Id}'s scene {level.Scene} names a place");
        }
    }

    [Fact]
    public void A_built_veiled_stage_before_the_frontier_is_where_the_road_waits()
    {
        // Stormblood, stages 1 to 8 built: the road steps over stage 7 (Il Mheg) to play 8; with 8 won and 9 on not built,
        // the road waits at 7.
        var modes = PartlyBuilt(StoryAt(MoonfallPlaces.Stormblood), 8);
        WalkTheRoad(modes);
        Assert.Equal(new MoonfallNext(new MoonfallLevelPlace(MoonfallCampaignKind.Expansion, 6 * MoonfallCharacters.LevelsPerStage), true), modes.Next());
    }

    [Fact]
    public void Advancing_the_story_or_revealing_never_closes_a_level_already_opened()
    {
        // Heavensward, the road walked up to 11-3 (11-1 and 11-2 won). The story reaches Stormblood: stage 4 unveils
        // and the frontier returns to 4-1, but 11-3 stays open (the level before it is won), and the moogle stays reached.
        var heavensward = FarShoreStart(StoryAt(MoonfallPlaces.Heavensward));
        for (var guard = 0; guard < 200 && heavensward.Continue() is { } next && next.Index < (10 * MoonfallCharacters.LevelsPerStage) + 2; guard++)
        {
            heavensward.Progress.RecordLevel(MoonfallStages.LevelId(next.Campaign, next.Index), won: true, score: 100_000);
        }

        Assert.Equal(MoonfallLevelState.Open, heavensward.Slot(MoonfallCampaignKind.Expansion, (10 * MoonfallCharacters.LevelsPerStage) + 2).State);
        var stormblood = new MoonfallModes(heavensward.Campaigns, heavensward.Progress, MoonfallStory.Everyone, []) { Shield = StoryAt(MoonfallPlaces.Stormblood) };
        Assert.Equal(MoonfallLevelState.Open, stormblood.Slot(MoonfallCampaignKind.Expansion, (10 * MoonfallCharacters.LevelsPerStage) + 2).State);
        Assert.Equal(MoonfallLevelState.Open, stormblood.Slot(MoonfallCampaignKind.Expansion, 3 * MoonfallCharacters.LevelsPerStage).State);
        Assert.Equal(MoonfallCompanionState.Available, stormblood.CompanionState(MoonfallCompanion.Moogle));
    }

    /// <summary>Plays Adventure as Continue leads it, winning each level, until it has nothing left to play.</summary>
    private static void WalkTheRoad(MoonfallModes modes)
    {
        for (var guard = 0; guard < 200 && modes.Continue() is { } next; guard++)
        {
            var start = modes.Adventure(next.Campaign, next.Index, MoonfallCompanion.Minfilia);
            Assert.NotNull(start);
            modes.Progress.RecordLevel(start.LevelId, won: true, score: 100_000);

            // As FinishLevel does after every level: the road's high-water mark follows the frontier.
            modes.NoteReach();
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

        // At every era the moogle's Storm Post (stage 11, the Courier's Wake, set nowhere: the owner's answer of 6 October
        // 2026) is reached. The companion follows its stage: the moogle is Available, offered by Quick Play, and plays
        // Storm Post's levels.
        Assert.DoesNotContain(11, veiledLeft);
        Assert.Equal(MoonfallCompanionState.Available, modes.CompanionState(MoonfallCompanion.Moogle));
        Assert.Contains(MoonfallCompanion.Moogle, modes.QuickPlayCompanions());
        Assert.NotNull(modes.QuickPlay("expansion-51", MoonfallCompanion.Moogle));

        // The rule itself: a Far Shore companion is reached when its stage is not veiled and the stage's first level is
        // open by the road's rule, or a level of it is won.
        foreach (var view in modes.Stages(MoonfallCampaignKind.Expansion))
        {
            if (view.Stage.Companion is var who && who != MoonfallCompanion.None && MoonfallCompanions.Get(who).Campaign == MoonfallCampaignKind.Expansion)
            {
                var reached = view.State != MoonfallStageState.Veiled && (view.Levels[0].Reached || view.Levels.Any(slot => modes.Progress.IsCleared(slot.Id)));
                Assert.Equal(reached, modes.CompanionReached(who));
            }
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
    public void The_Moon_Roads_stage_names_that_name_a_place_follow_the_shield_while_their_stages_stay_open()
    {
        // The owner's standing answer: Moonfall follows the shield (critic runtime round 2, n-e). "The Waking Sands" and
        // "Vesper Bay" name Western Thanalan, "Mor Dhona's Glass" and "Silvertear by Night" Mor Dhona: before the story
        // reaches the place the name prints as the shield's placeholder, and the stage stays open, never veiled.
        var stages = MoonfallStages.Of(MoonfallCampaignKind.Base);
        var shield = new MoonfallShield(static zone => zone is "Western Thanalan" or "Mor Dhona", static zone => "Placeholder " + zone);
        var modes = new MoonfallModes(MoonfallCampaigns.LoadBuiltIn(), new MoonfallProgress(), MoonfallStory.Everyone, []) { Shield = shield };
        foreach (var (number, place) in new[] { (1, "Western Thanalan"), (2, "Western Thanalan"), (9, "Mor Dhona"), (10, "Mor Dhona") })
        {
            var stage = stages[number - 1];
            Assert.Equal("Placeholder " + place, modes.StageName(stage));
            Assert.False(modes.StageVeiled(stage));
            Assert.Equal(new MoonfallPlace(MoonfallPlaces.ARealmReborn, place), MoonfallPlaces.OfStageName(stage));
        }

        Assert.Equal(stages[2].Name, modes.StageName(stages[2]));
        Assert.Equal(MoonfallLevelState.Open, modes.Slot(MoonfallCampaignKind.Base, 0).State);

        // An open shield prints the names.
        var open = new MoonfallModes(MoonfallCampaigns.LoadBuiltIn(), new MoonfallProgress(), MoonfallStory.Everyone, []);
        Assert.Equal("The Waking Sands", open.StageName(stages[0]));
        Assert.Equal("Vesper Bay", open.StageName(stages[1]));

        // The Far Shore's names follow their stages' places, as before.
        var far = MoonfallStages.Of(MoonfallCampaignKind.Expansion)[0];
        Assert.Equal(MoonfallPlaces.OfStage(far), MoonfallPlaces.OfStageName(far));
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

        // The title's backdrop is tagged too (Sohm Al, Heavensward): the menus follow the same rule. Before Heavensward the
        // title is Ul'dah's painting, A Realm Reborn's (m13); the chart, of no place, is the last fallback.
        Assert.Equal(MoonfallPlaces.Heavensward, MoonfallPlaces.OfBackdrop(MoonfallBackdrop.Title).Era);
        Assert.Equal(MoonfallPlaces.ARealmReborn, MoonfallPlaces.OfBackdrop(MoonfallBackdrop.TitleEarly).Era);
        Assert.NotNull(MoonfallPlaces.OfBackdrop(MoonfallBackdrop.TitleEarly).Zone);
        Assert.Equal(MoonfallPlace.Nowhere, MoonfallPlaces.OfBackdrop(MoonfallBackdrop.Chart));
        var early = Everything(StoryAt(MoonfallPlaces.ARealmReborn)).Shield;
        Assert.True(early.Hides(MoonfallPlaces.OfBackdrop(MoonfallBackdrop.Title)));
        Assert.False(early.Hides(MoonfallPlaces.OfBackdrop(MoonfallBackdrop.TitleEarly)));
    }

    [Fact]
    public void The_open_shield_hides_nothing()
    {
        var modes = Everything(MoonfallShield.Open);
        Assert.All(modes.Stages(MoonfallCampaignKind.Expansion), view => Assert.NotEqual(MoonfallStageState.Veiled, view.State));
    }
}
