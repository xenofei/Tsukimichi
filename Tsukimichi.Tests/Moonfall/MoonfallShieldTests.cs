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

        foreach (var (_, place) in MoonfallPlaces.Scenes)
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

    [Fact]
    public void The_open_shield_hides_nothing()
    {
        var modes = Everything(MoonfallShield.Open);
        Assert.All(modes.Stages(MoonfallCampaignKind.Expansion), view => Assert.NotEqual(MoonfallStageState.Veiled, view.State));
    }
}
