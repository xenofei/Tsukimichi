using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>Adventure's stages, the companions and the spoiler shield, Quick Play and the menus' view of the modes (plan v9 G7).</summary>
public sealed class MoonfallModesTests
{
    /// <summary>A story where everyone is met but the names in <paramref name="unmet"/>.</summary>
    private static MoonfallStory StoryWithout(params string[] unmet) => new(name => !unmet.Contains(name, StringComparer.Ordinal));

    /// <summary>Campaigns where every Adventure id has a level (the shipped base-01, under each id), so a test can walk the whole road.</summary>
    private static MoonfallCampaigns Full()
    {
        var shape = MoonfallCampaigns.LoadBuiltIn().Base.Levels[0];
        MoonfallCampaign Make(MoonfallCampaignKind kind) =>
            new(kind, Enumerable.Range(0, MoonfallStages.LevelCount(kind)).Select(i => shape with { Id = MoonfallStages.LevelId(kind, i) }).ToList());
        return new MoonfallCampaigns(Make(MoonfallCampaignKind.Base), Make(MoonfallCampaignKind.Expansion), []);
    }

    private static MoonfallModes Modes(MoonfallProgress progress, MoonfallStory? story = null, MoonfallCampaigns? campaigns = null) =>
        new(campaigns ?? Full(), progress, story ?? MoonfallStory.Everyone, MoonfallChallenges.LoadBuiltIn().Challenges);

    // ---- Stages ----

    [Fact]
    public void The_Moon_Road_is_eleven_stages_of_five_carried_in_the_casts_order_with_the_specs_names()
    {
        var stages = MoonfallStages.Of(MoonfallCampaignKind.Base);
        Assert.Equal(11, stages.Count);
        Assert.All(stages, static s => Assert.Equal(MoonfallCharacters.LevelsPerStage, s.LevelIds.Count));
        Assert.Equal(55, stages.SelectMany(static s => s.LevelIds).Distinct().Count());
        Assert.Equal(
            [MoonfallCompanion.Minfilia, MoonfallCompanion.Twins, MoonfallCompanion.Cid, MoonfallCompanion.Raubahn, MoonfallCompanion.Merlwyb,
             MoonfallCompanion.Urianger, MoonfallCompanion.KanESenna, MoonfallCompanion.Tataru, MoonfallCompanion.Yshtola, MoonfallCompanion.Louisoix,
             MoonfallCompanion.None],
            stages.Select(static s => s.Companion));
        Assert.Equal("The Waking Sands", stages[0].Name);
        Assert.Equal("The Night Skyway", stages[2].Name);
        Assert.Equal("The Shroud by Night", stages[6].Name);
        Assert.Equal("Your Pick", stages[10].Name);
        Assert.True(stages[10].PlayerPicks);
        Assert.Equal("base-01", stages[0].LevelIds[0]);
        Assert.Equal("base-55", stages[10].LevelIds[4]);

        // Each stage's companion carries the power Adventure gives its levels.
        for (var i = 0; i < MoonfallStages.BaseLevels; i++)
        {
            var companion = MoonfallStages.AdventureCompanion(MoonfallCampaignKind.Base, i);
            Assert.Equal(MoonfallCharacters.AdventurePower(MoonfallCampaignKind.Base, i), companion == MoonfallCompanion.None ? MoonfallPower.None : MoonfallCompanions.Get(companion).Power);
        }
    }

    [Fact]
    public void The_Far_Shore_is_twelve_stages_the_moogle_eleventh_and_ends_on_the_moon()
    {
        var stages = MoonfallStages.Of(MoonfallCampaignKind.Expansion);
        Assert.Equal(12, stages.Count);
        Assert.Equal(60, stages.SelectMany(static s => s.LevelIds).Distinct().Count());
        Assert.Equal(MoonfallCompanion.Moogle, stages[10].Companion);
        Assert.True(stages[11].PlayerPicks);
        Assert.Equal("The Sea of Sorrows", stages[11].Name);
        Assert.Equal("expansion-60", stages[11].LevelIds[4]);
        Assert.Equal(stages.Count, stages.Select(static s => s.Name).Distinct().Count());
    }

    [Theory]
    [InlineData("base-01", MoonfallCampaignKind.Base, 0)]
    [InlineData("base-55", MoonfallCampaignKind.Base, 54)]
    [InlineData("expansion-01", MoonfallCampaignKind.Expansion, 0)]
    [InlineData("expansion-60", MoonfallCampaignKind.Expansion, 59)]
    public void Level_ids_name_their_place(string id, MoonfallCampaignKind campaign, int index)
    {
        Assert.True(MoonfallStages.TryPlace(id, out var place));
        Assert.Equal(new MoonfallLevelPlace(campaign, index), place);
        Assert.Equal(id, MoonfallStages.LevelId(campaign, index));
    }

    [Theory]
    [InlineData("base-00")]
    [InlineData("base-56")]
    [InlineData("expansion-61")]
    [InlineData("base-1")]
    [InlineData("exp-01")]
    [InlineData(null)]
    public void Other_ids_are_not_Adventures(string? id) => Assert.False(MoonfallStages.TryPlace(id, out _));

    // ---- Companions and the spoiler shield ----

    [Fact]
    public void A_companion_not_met_in_the_story_is_the_card_back_whatever_Moonfall_has_reached()
    {
        var progress = new MoonfallProgress { BaseCleared = 30 };
        var story = StoryWithout("Raubahn");
        Assert.Equal(MoonfallCompanionState.NotMet, MoonfallCompanions.State(MoonfallCompanion.Raubahn, progress, story));
        Assert.Equal(MoonfallCompanionState.Available, MoonfallCompanions.State(MoonfallCompanion.Cid, progress, story));
        Assert.DoesNotContain(MoonfallCompanion.Raubahn, MoonfallCompanions.QuickPlay(progress, story));
    }

    [Fact]
    public void A_companion_met_but_not_reached_is_dimmed_with_their_stage()
    {
        var progress = new MoonfallProgress { BaseCleared = 10 };
        Assert.Equal(MoonfallCompanionState.Available, MoonfallCompanions.State(MoonfallCompanion.Cid, progress, MoonfallStory.Everyone));
        Assert.Equal(MoonfallCompanionState.MetNotReached, MoonfallCompanions.State(MoonfallCompanion.Raubahn, progress, MoonfallStory.Everyone));
        Assert.Equal(4, MoonfallCompanions.Get(MoonfallCompanion.Raubahn).Stage);
        Assert.Equal(
            [MoonfallCompanion.Minfilia, MoonfallCompanion.Twins, MoonfallCompanion.Cid],
            MoonfallCompanions.QuickPlay(progress, MoonfallStory.Everyone));
    }

    [Fact]
    public void The_twins_stay_face_down_until_Alisaie_is_met()
    {
        var progress = new MoonfallProgress { BaseCleared = 55 };
        Assert.Equal(MoonfallCompanionState.NotMet, MoonfallCompanions.State(MoonfallCompanion.Twins, progress, StoryWithout("Alisaie")));
        Assert.Equal(MoonfallCompanionState.Available, MoonfallCompanions.State(MoonfallCompanion.Twins, progress, StoryWithout()));
    }

    [Fact]
    public void The_moogle_is_met_from_the_start_and_reached_on_The_Far_Shore()
    {
        var everyoneUnmet = new MoonfallStory(static _ => false);
        Assert.Equal(MoonfallCompanionState.MetNotReached, MoonfallCompanions.State(MoonfallCompanion.Moogle, new MoonfallProgress(), everyoneUnmet));
        Assert.Equal(MoonfallCompanionState.MetNotReached, MoonfallCompanions.State(MoonfallCompanion.Moogle, new MoonfallProgress { BaseCleared = 55, ExpansionCleared = 49 }, everyoneUnmet));
        Assert.Equal(MoonfallCompanionState.Available, MoonfallCompanions.State(MoonfallCompanion.Moogle, new MoonfallProgress { BaseCleared = 55, ExpansionCleared = 50 }, everyoneUnmet));
    }

    [Fact]
    public void The_shield_off_meets_everyone_and_the_cast_is_complete()
    {
        var story = MoonfallStory.FromShield(SpoilerMask.None);
        Assert.All(MoonfallCompanions.All, c => Assert.True(story.HasMet(c.Companion), c.Name));
        Assert.Equal(MoonfallCompanions.Count, MoonfallCompanions.All.Count);
        Assert.Equal(MoonfallCompanions.Count, MoonfallCompanions.All.Select(static c => c.Power).Distinct().Count());
        Assert.All(MoonfallCompanions.All, static c => Assert.Equal(c.Companion, MoonfallCompanions.ByKey(c.Key)));
        Assert.All(MoonfallCompanions.All, static c => Assert.Equal(c.Companion, MoonfallCompanions.Carrying(c.Power)));
        Assert.Equal(MoonfallCompanion.None, MoonfallCompanions.ByKey("gyobo"));
    }

    // ---- The menus' view ----

    [Fact]
    public void Level_select_opens_level_by_level_and_a_missing_level_stops_the_road()
    {
        var modes = Modes(new MoonfallProgress { BaseCleared = 2 });
        Assert.Equal(MoonfallLevelState.Cleared, modes.Slot(MoonfallCampaignKind.Base, 1).State);
        Assert.Equal(MoonfallLevelState.Open, modes.Slot(MoonfallCampaignKind.Base, 2).State);
        Assert.Equal(MoonfallLevelState.Sealed, modes.Slot(MoonfallCampaignKind.Base, 3).State);
        Assert.Equal(new MoonfallLevelPlace(MoonfallCampaignKind.Base, 2), modes.Continue());
        Assert.Equal(MoonfallLevelState.Sealed, modes.Slot(MoonfallCampaignKind.Expansion, 0).State);

        // The shipped campaign, where most ids are not authored yet.
        var shipped = Modes(new MoonfallProgress { BaseCleared = 4 }, campaigns: MoonfallCampaigns.LoadBuiltIn());
        Assert.Equal(MoonfallLevelState.Missing, shipped.Slot(MoonfallCampaignKind.Base, 4).State);
        Assert.Null(shipped.Continue());
        Assert.Null(shipped.Adventure(MoonfallCampaignKind.Base, 4));
    }

    [Fact]
    public void The_map_shows_each_stage_done_open_or_sealed_with_here_on_the_next_level()
    {
        var modes = Modes(new MoonfallProgress { BaseCleared = 7 }, StoryWithout("Alisaie"));
        var stages = modes.Stages(MoonfallCampaignKind.Base);
        Assert.Equal(MoonfallStageState.Done, stages[0].State);
        Assert.Equal(MoonfallStageState.Open, stages[1].State);
        Assert.True(stages[1].Here);
        Assert.False(stages[0].Here);
        Assert.Equal(MoonfallCompanionState.NotMet, stages[1].Companion);
        Assert.Equal(MoonfallStageState.Sealed, stages[2].State);
        Assert.Equal(MoonfallCompanionState.MetNotReached, stages[2].Companion);
        Assert.Equal(MoonfallCompanionState.Available, stages[10].Companion);
    }

    [Fact]
    public void Adventure_gives_the_stages_companion_and_the_last_stage_takes_an_available_pick()
    {
        var modes = Modes(new MoonfallProgress { BaseCleared = 52 }, StoryWithout("Tataru"));
        var stageOne = modes.Adventure(MoonfallCampaignKind.Base, 0)!;
        Assert.Equal(MoonfallCompanion.Minfilia, stageOne.Companion);
        Assert.Equal(MoonfallPower.SuperGuide, stageOne.Power);
        Assert.Equal(1, stageOne.LevelNumber);
        Assert.Equal(MoonfallMode.Adventure, stageOne.Mode);

        Assert.Equal(MoonfallCompanion.Yshtola, modes.Adventure(MoonfallCampaignKind.Base, 52, MoonfallCompanion.Yshtola)!.Companion);
        Assert.Null(modes.Adventure(MoonfallCampaignKind.Base, 52, MoonfallCompanion.Tataru));
        Assert.Null(modes.Adventure(MoonfallCampaignKind.Base, 53));
        Assert.Equal(53, modes.Adventure(MoonfallCampaignKind.Base, 52)!.LevelNumber);
        var game = stageOne.Create(7);
        Assert.Equal(MoonfallPower.SuperGuide, game.Power);
        Assert.Equal(MoonfallRules.BallsPerLevel, game.BallsLeft);
    }

    [Fact]
    public void Quick_Play_offers_levels_reached_and_companions_available()
    {
        var modes = Modes(new MoonfallProgress { BaseCleared = 6 });
        Assert.Equal(7, modes.QuickPlayLevels().Count);
        Assert.Equal([MoonfallCompanion.Minfilia, MoonfallCompanion.Twins], modes.QuickPlayCompanions());
        Assert.NotNull(modes.QuickPlay("base-07", MoonfallCompanion.Twins));
        Assert.NotNull(modes.QuickPlay("base-03", MoonfallCompanion.None));
        Assert.Null(modes.QuickPlay("base-08", MoonfallCompanion.Twins));
        Assert.Null(modes.QuickPlay("base-03", MoonfallCompanion.Cid));
        Assert.Null(modes.QuickPlay("nowhere", MoonfallCompanion.None));
    }

    [Fact]
    public void Finishing_a_level_records_its_best_opens_the_next_and_aces_it_with_a_bonus()
    {
        var progress = new MoonfallProgress();
        var modes = Modes(progress, campaigns: MoonfallCampaigns.LoadBuiltIn());
        var start = modes.Adventure(MoonfallCampaignKind.Base, 0)!;
        var game = start.Create(5);
        MoonfallTestKit.PlayOut(game, [-40, 10, 33, -12, 55, -70, 0, 21, -28, 44]);
        var result = modes.FinishLevel(start, game);
        Assert.Equal(game.Phase == MoonfallPhase.Won, result.Won);
        Assert.Equal(result.Score, progress.Best("base-01"));
        Assert.True(result.NewBest);
        var ace = MoonfallAces.For("base-01")!.Value;
        Assert.Equal(result.Won && game.Score >= ace ? MoonfallRules.AceBonus : 0, result.AceBonus);
        Assert.Equal(game.Score + result.AceBonus, result.Score);
        if (result.Won)
        {
            Assert.True(result.Unlocked);
            Assert.Equal(1, progress.BaseCleared);
            Assert.Equal(MoonfallLevelState.Open, modes.Slot(MoonfallCampaignKind.Base, 1).State);
        }

        // An Ace: a won level at its Ace score is aced, and stays aced.
        progress.RecordLevel("base-02", won: true, ace, ace);
        Assert.True(progress.IsAced("base-02"));
        progress.RecordLevel("base-02", won: true, 1, ace);
        Assert.True(progress.IsAced("base-02"));
        Assert.Equal(ace, progress.Best("base-02"));
    }

    [Fact]
    public void Duels_offer_every_companion_met_and_the_title_offers_Louisoix()
    {
        var modes = Modes(new MoonfallProgress(), StoryWithout("Alisaie"));
        var opponents = modes.DuelOpponents();
        Assert.DoesNotContain(MoonfallCompanion.Twins, opponents);
        Assert.Contains(MoonfallCompanion.Louisoix, opponents);
        Assert.Equal(MoonfallCompanion.Louisoix, modes.TitleOpponent());
        Assert.Equal(MoonfallCompanion.Minfilia, Modes(new MoonfallProgress(), StoryWithout("Louisoix")).TitleOpponent());

        var duel = modes.StartDuel("base-01", MoonfallCompanion.Minfilia, MoonfallCompanion.Louisoix, MoonfallAiDifficulty.Adept, 3)!;
        Assert.Equal(MoonfallCompanion.Louisoix, duel.Companion(MoonfallDuel.OpponentSide));
        Assert.Equal(MoonfallRuleSet.Duel, duel.Game.RuleSet);
        Assert.Null(modes.StartDuel("base-01", MoonfallCompanion.None, MoonfallCompanion.Twins, MoonfallAiDifficulty.Adept, 3));
        Assert.Null(modes.StartDuel("base-02", MoonfallCompanion.None, MoonfallCompanion.Louisoix, MoonfallAiDifficulty.Adept, 3));
    }

    [Fact]
    public void The_Far_Shore_opens_after_all_of_The_Moon_Road()
    {
        Assert.False(Modes(new MoonfallProgress { BaseCleared = 54 }).CampaignOpen(MoonfallCampaignKind.Expansion));
        var open = Modes(new MoonfallProgress { BaseCleared = 55 });
        Assert.True(open.CampaignOpen(MoonfallCampaignKind.Expansion));
        Assert.Equal(new MoonfallLevelPlace(MoonfallCampaignKind.Expansion, 0), open.Continue());
        Assert.Equal(MoonfallCompanion.Minfilia, open.Adventure(MoonfallCampaignKind.Expansion, 0)!.Companion);
        Assert.Equal(MoonfallCompanion.Moogle, MoonfallStages.AdventureCompanion(MoonfallCampaignKind.Expansion, 50));
        Assert.Null(open.Adventure(MoonfallCampaignKind.Expansion, 50));
        var moogle = Modes(new MoonfallProgress { BaseCleared = 55, ExpansionCleared = 50 });
        Assert.Equal(MoonfallCompanion.Moogle, moogle.Adventure(MoonfallCampaignKind.Expansion, 50)!.Companion);
        Assert.Equal(MoonfallPower.Bolt, moogle.Adventure(MoonfallCampaignKind.Expansion, 50)!.Power);
    }
}
