using Tsukimichi.Core.Moonfall;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>
/// Moonfall's screens (plan v9 G7, spec-rich2.md §4): the screen state machine (its transitions, Back and Esc, the
/// locked entries), hold-to-confirm, the spoiler states as the menus show them, and the draw watch that holds the sound
/// while the window is collapsed.
/// </summary>
public sealed class MoonfallScreensTests
{
    private static MoonfallStory StoryWithout(params string[] unmet) => new(name => !unmet.Contains(name, StringComparer.Ordinal));

    private static MoonfallCampaigns Full()
    {
        var shape = MoonfallCampaigns.LoadBuiltIn().Base.Levels[0];
        MoonfallCampaign Make(MoonfallCampaignKind kind) =>
            new(kind, Enumerable.Range(0, MoonfallStages.LevelCount(kind)).Select(i => shape with { Id = MoonfallStages.LevelId(kind, i) }).ToList());
        return new MoonfallCampaigns(Make(MoonfallCampaignKind.Base), Make(MoonfallCampaignKind.Expansion), []);
    }

    private static MoonfallModes Modes(MoonfallProgress progress, MoonfallStory? story = null) =>
        new(Full(), progress, story ?? MoonfallStory.Everyone, MoonfallChallenges.LoadBuiltIn().Challenges);

    // ---- The flow ----

    [Fact]
    public void It_opens_on_the_title_and_esc_there_closes_the_window()
    {
        var flow = new MoonfallScreenFlow();
        Assert.Equal(MoonfallScreen.Title, flow.Current);
        Assert.Null(flow.Previous);
        Assert.Equal(MoonfallBack.Close, flow.Back(paused: false, levelOver: false));
        Assert.Equal(MoonfallScreen.Title, flow.Current);
    }

    [Theory]
    [InlineData(MoonfallScreen.Map)]
    [InlineData(MoonfallScreen.Characters)]
    [InlineData(MoonfallScreen.QuickPlay)]
    [InlineData(MoonfallScreen.Duel)]
    [InlineData(MoonfallScreen.Options)]
    public void Each_menu_opens_from_the_title_and_back_returns_to_it(MoonfallScreen screen)
    {
        var flow = new MoonfallScreenFlow();
        var version = flow.Version;
        Assert.True(flow.Open(screen, challengesOpen: false));
        Assert.Equal(screen, flow.Current);
        Assert.NotEqual(version, flow.Version);
        Assert.Equal(MoonfallScreen.Title, flow.Previous);
        Assert.Equal(MoonfallBack.Screen, flow.Back(paused: false, levelOver: false));
        Assert.Equal(MoonfallScreen.Title, flow.Current);
    }

    [Fact]
    public void Challenges_stay_locked_until_the_moon_road_is_won()
    {
        var flow = new MoonfallScreenFlow();
        Assert.False(MoonfallScreenFlow.CanOpen(MoonfallScreen.Challenges, challengesOpen: false));
        Assert.False(flow.Open(MoonfallScreen.Challenges, challengesOpen: false));
        Assert.Equal(MoonfallScreen.Title, flow.Current);

        // The modes say when: every level of The Moon Road won.
        var progress = new MoonfallProgress { BaseCleared = MoonfallStages.BaseLevels - 1 };
        Assert.False(Modes(progress).ChallengesOpen);
        progress.BaseCleared = MoonfallStages.BaseLevels;
        Assert.True(Modes(progress).ChallengesOpen);
        Assert.True(flow.Open(MoonfallScreen.Challenges, challengesOpen: true));
        Assert.Equal(MoonfallScreen.Challenges, flow.Current);
    }

    [Fact]
    public void The_board_is_entered_only_by_play_never_opened_from_a_menu()
    {
        var flow = new MoonfallScreenFlow();
        Assert.False(MoonfallScreenFlow.CanOpen(MoonfallScreen.Play, challengesOpen: true));
        Assert.False(flow.Open(MoonfallScreen.Play, challengesOpen: true));
        Assert.Equal(MoonfallScreen.Title, flow.Current);
        Assert.False(flow.InPlay);
    }

    [Fact]
    public void The_map_leads_to_level_select_and_back_walks_them_in_order()
    {
        var flow = new MoonfallScreenFlow();
        flow.Open(MoonfallScreen.Map, false);
        flow.Open(MoonfallScreen.Levels, false);
        Assert.Equal(2, flow.Depth);
        Assert.Equal(MoonfallBack.Screen, flow.Back(false, false));
        Assert.Equal(MoonfallScreen.Map, flow.Current);
        Assert.Equal(MoonfallBack.Screen, flow.Back(false, false));
        Assert.Equal(MoonfallScreen.Title, flow.Current);
    }

    [Fact]
    public void Opening_a_screen_already_under_returns_to_it_rather_than_looping()
    {
        var flow = new MoonfallScreenFlow();
        flow.Open(MoonfallScreen.Map, false);
        flow.Open(MoonfallScreen.Levels, false);
        Assert.True(flow.Open(MoonfallScreen.Map, false));
        Assert.Equal(MoonfallScreen.Map, flow.Current);
        Assert.Equal(1, flow.Depth);
        Assert.False(flow.Open(MoonfallScreen.Map, false));
    }

    [Fact]
    public void In_play_esc_pauses_then_resumes_and_over_the_tally_it_leaves_for_the_map()
    {
        var flow = new MoonfallScreenFlow();
        flow.Open(MoonfallScreen.Map, false);
        flow.Open(MoonfallScreen.Levels, false);
        flow.Play(MoonfallPlayKind.Adventure);
        Assert.Equal(MoonfallScreen.Play, flow.Current);
        Assert.True(flow.InPlay);

        Assert.Equal(MoonfallBack.Pause, flow.Back(paused: false, levelOver: false));
        Assert.Equal(MoonfallScreen.Play, flow.Current);
        Assert.Equal(MoonfallBack.Resume, flow.Back(paused: true, levelOver: false));
        Assert.Equal(MoonfallScreen.Play, flow.Current);

        // The tally: Esc is the Map button.
        Assert.Equal(MoonfallBack.Leave, flow.Back(paused: false, levelOver: true));
        Assert.Equal(MoonfallScreen.Map, flow.Current);
        Assert.Equal(MoonfallScreen.Title, flow.Previous);
        Assert.False(flow.InPlay);
    }

    [Theory]
    [InlineData(MoonfallPlayKind.Adventure, MoonfallScreen.Map)]
    [InlineData(MoonfallPlayKind.QuickPlay, MoonfallScreen.QuickPlay)]
    [InlineData(MoonfallPlayKind.Challenge, MoonfallScreen.Challenges)]
    [InlineData(MoonfallPlayKind.Duel, MoonfallScreen.Duel)]
    public void Leaving_the_board_goes_to_its_modes_screen_over_the_title(MoonfallPlayKind kind, MoonfallScreen home)
    {
        var flow = new MoonfallScreenFlow();
        // Started from the title (Continue), from deep in the menus, or from the board itself (Next, Replay).
        flow.Play(kind);
        flow.Play(kind);
        flow.Leave();
        Assert.Equal(home, flow.Current);
        Assert.Equal(1, flow.Depth);
        Assert.Equal(MoonfallScreen.Title, flow.Previous);
        Assert.Equal(MoonfallBack.Screen, flow.Back(false, false));
        Assert.Equal(MoonfallScreen.Title, flow.Current);
    }

    [Fact]
    public void Options_from_the_pause_menu_comes_back_to_the_paused_board()
    {
        var flow = new MoonfallScreenFlow();
        flow.Play(MoonfallPlayKind.QuickPlay);
        Assert.True(flow.Open(MoonfallScreen.Options, false));
        Assert.True(flow.InPlay);
        Assert.Equal(MoonfallBack.Screen, flow.Back(paused: true, levelOver: false));
        Assert.Equal(MoonfallScreen.Play, flow.Current);

        // Restarting from Options' way back never stacks the board twice.
        flow.Open(MoonfallScreen.Options, false);
        flow.Play(MoonfallPlayKind.QuickPlay);
        Assert.Equal(MoonfallScreen.Play, flow.Current);
        Assert.Equal(1, flow.Depth);
    }

    [Fact]
    public void Starting_again_from_the_board_moves_the_version_on_without_stacking()
    {
        var flow = new MoonfallScreenFlow();
        flow.Open(MoonfallScreen.QuickPlay, false);
        flow.Play(MoonfallPlayKind.QuickPlay);
        var depth = flow.Depth;
        var version = flow.Version;
        flow.Play(MoonfallPlayKind.QuickPlay);
        Assert.Equal(depth, flow.Depth);
        Assert.NotEqual(version, flow.Version);
    }

    [Fact]
    public void The_stack_never_overflows()
    {
        var flow = new MoonfallScreenFlow();
        ReadOnlySpan<MoonfallScreen> loop = [MoonfallScreen.Map, MoonfallScreen.Levels, MoonfallScreen.Characters, MoonfallScreen.QuickPlay, MoonfallScreen.Duel, MoonfallScreen.Options];
        for (var i = 0; i < 100; i++)
        {
            flow.Open(loop[i % loop.Length], false);
            Assert.InRange(flow.Depth, 0, MoonfallScreenFlow.MaxDepth);
        }

        for (var i = 0; i < 20 && flow.Current != MoonfallScreen.Title; i++)
        {
            flow.Back(false, false);
        }

        Assert.Equal(MoonfallScreen.Title, flow.Current);
    }

    // ---- Hold to confirm ----

    [Fact]
    public void A_hold_confirms_only_after_its_full_time_and_once()
    {
        var hold = default(MoonfallHold);
        var frames = 0;
        var fired = 0;
        while (frames < 200)
        {
            frames++;
            if (hold.Update(down: true, 1 / 60.0))
            {
                fired++;
            }
        }

        Assert.Equal(1, fired);
        Assert.Equal(1f, hold.Progress);

        // It waits for a release before it can fire again.
        Assert.False(hold.Update(true, 1.0));
        Assert.False(hold.Update(false, 1 / 60.0));
        Assert.Equal(0f, hold.Progress);
    }

    [Fact]
    public void Letting_go_early_starts_the_hold_over()
    {
        var hold = default(MoonfallHold);
        var t = 0.0;
        while (t < MoonfallHold.Seconds * 0.9)
        {
            Assert.False(hold.Update(true, 1 / 60.0));
            t += 1 / 60.0;
        }

        Assert.InRange(hold.Progress, 0.85f, 0.95f);
        Assert.True(hold.Holding);
        Assert.False(hold.Update(false, 1 / 60.0));
        Assert.Equal(0f, hold.Progress);
        Assert.False(hold.Holding);

        // A tap is never enough, and the full hold still is.
        Assert.False(hold.Update(true, 0.1));
        Assert.False(hold.Update(false, 0.1));
        Assert.False(hold.Update(true, MoonfallHold.Seconds - 0.01));
        Assert.True(hold.Update(true, 0.02));
    }

    [Fact]
    public void A_reset_hold_never_fires_on_a_stale_press()
    {
        var hold = default(MoonfallHold);
        hold.Update(true, MoonfallHold.Seconds * 0.8);
        hold.Reset();
        Assert.Equal(0f, hold.Progress);
        Assert.False(hold.Update(true, MoonfallHold.Seconds * 0.5));
    }

    // ---- The spoiler states as the menus show them ----

    [Fact]
    public void A_companion_not_met_shows_the_card_back_unnamed_with_the_power_still_named()
    {
        var modes = Modes(new MoonfallProgress { BaseCleared = 50 }, StoryWithout("Raubahn"));
        var info = MoonfallCompanions.Get(MoonfallCompanion.Raubahn);
        var look = MoonfallLooks.Companion(info, modes.CompanionState(MoonfallCompanion.Raubahn));
        Assert.Equal(MoonfallCardFace.Back, look.Face);
        Assert.False(look.Named);
        Assert.True(look.PowerNamed);
        Assert.False(look.Playable);
        Assert.False(look.ShowsStage);
    }

    [Fact]
    public void A_companion_met_but_not_reached_is_dimmed_with_their_stage()
    {
        var modes = Modes(new MoonfallProgress { BaseCleared = 12 });
        var info = MoonfallCompanions.Get(MoonfallCompanion.Raubahn);
        var look = MoonfallLooks.Companion(info, modes.CompanionState(MoonfallCompanion.Raubahn));
        Assert.Equal(MoonfallCardFace.Dimmed, look.Face);
        Assert.True(look.Named);
        Assert.True(look.ShowsStage);
        Assert.Equal(4, look.Stage);
        Assert.False(look.FarShore);
        Assert.False(look.Playable);

        // The moogle's stage is The Far Shore's.
        var moogle = MoonfallLooks.Companion(MoonfallCompanions.Get(MoonfallCompanion.Moogle), modes.CompanionState(MoonfallCompanion.Moogle));
        Assert.Equal(MoonfallCardFace.Dimmed, moogle.Face);
        Assert.True(moogle.FarShore);
    }

    [Fact]
    public void A_companion_met_and_reached_is_face_up_and_playable()
    {
        var modes = Modes(new MoonfallProgress { BaseCleared = 12 });
        var look = MoonfallLooks.Companion(MoonfallCompanions.Get(MoonfallCompanion.Cid), modes.CompanionState(MoonfallCompanion.Cid));
        Assert.Equal(MoonfallCardFace.Up, look.Face);
        Assert.True(look.Named);
        Assert.True(look.Playable);
        Assert.False(look.ShowsStage);
    }

    [Fact]
    public void The_twins_stay_face_down_until_alisaie_is_met_however_far_moonfall_has_gone()
    {
        var progress = new MoonfallProgress { BaseCleared = MoonfallStages.BaseLevels };
        var twins = MoonfallCompanions.Get(MoonfallCompanion.Twins);
        var before = MoonfallLooks.Companion(twins, Modes(progress, StoryWithout("Alisaie")).CompanionState(MoonfallCompanion.Twins));
        Assert.Equal(MoonfallCardFace.Back, before.Face);
        Assert.False(before.Named);

        // Alphinaud alone is not enough either way round.
        var alphinaudUnmet = MoonfallLooks.Companion(twins, Modes(progress, StoryWithout("Alphinaud")).CompanionState(MoonfallCompanion.Twins));
        Assert.Equal(MoonfallCardFace.Back, alphinaudUnmet.Face);

        var after = MoonfallLooks.Companion(twins, Modes(progress).CompanionState(MoonfallCompanion.Twins));
        Assert.Equal(MoonfallCardFace.Up, after.Face);
        Assert.True(after.Named);
    }

    [Fact]
    public void The_maps_stops_show_the_four_states_and_the_free_choice_star()
    {
        // The mocks' state: stage 3 here, 1 and 2 won, the twins not met.
        var progress = new MoonfallProgress { BaseCleared = 12 };
        for (var i = 0; i < 12; i++)
        {
            progress.Levels[MoonfallStages.LevelId(MoonfallCampaignKind.Base, i)] = new MoonfallLevelRecord { Cleared = true, Best = 100_000 };
        }

        var stages = Modes(progress, StoryWithout("Alisaie")).Stages(MoonfallCampaignKind.Base);
        var won = MoonfallLooks.Stop(stages[0]);
        Assert.Equal(new MoonfallStopLook(MoonfallStopFace.Portrait, Drained: false, Padlock: false, Pip: true, Glow: false, Dim: false), won);

        // Stage 2 is won, but its companion is not met: the card back, with its pip.
        var twins = MoonfallLooks.Stop(stages[1]);
        Assert.Equal(MoonfallStopFace.CardBack, twins.Face);
        Assert.True(twins.Pip);

        var here = MoonfallLooks.Stop(stages[2]);
        Assert.Equal(new MoonfallStopLook(MoonfallStopFace.Portrait, Drained: false, Padlock: false, Pip: false, Glow: true, Dim: false), here);

        var sealedStop = MoonfallLooks.Stop(stages[3]);
        Assert.Equal(new MoonfallStopLook(MoonfallStopFace.Portrait, Drained: true, Padlock: true, Pip: false, Glow: false, Dim: true), sealedStop);

        // Stage 11, your pick: its own star, dim and without a padlock while it is not reached.
        var pick = MoonfallLooks.Stop(stages[10]);
        Assert.Equal(MoonfallStopFace.PickStar, pick.Face);
        Assert.False(pick.Padlock);
        Assert.True(pick.Dim);
        Assert.False(pick.Drained);
    }

    [Fact]
    public void A_stop_whose_companion_is_not_met_never_shows_their_face_even_when_sealed()
    {
        var stages = Modes(new MoonfallProgress(), StoryWithout("Raubahn")).Stages(MoonfallCampaignKind.Base);
        var look = MoonfallLooks.Stop(stages[3]);
        Assert.Equal(MoonfallStopFace.CardBack, look.Face);
        Assert.False(look.Drained);
        Assert.True(look.Padlock);
    }

    [Fact]
    public void A_reached_stage_whose_levels_are_not_built_yet_is_coming_not_sealed()
    {
        // The Moon Road won and The Far Shore open, but none of its levels ship yet.
        var shape = MoonfallCampaigns.LoadBuiltIn().Base.Levels[0];
        var baseCampaign = new MoonfallCampaign(MoonfallCampaignKind.Base,
            Enumerable.Range(0, MoonfallStages.BaseLevels).Select(i => shape with { Id = MoonfallStages.LevelId(MoonfallCampaignKind.Base, i) }).ToList());
        var campaigns = new MoonfallCampaigns(baseCampaign, new MoonfallCampaign(MoonfallCampaignKind.Expansion, []), []);
        var progress = new MoonfallProgress { BaseCleared = MoonfallStages.BaseLevels };
        var modes = new MoonfallModes(campaigns, progress, MoonfallStory.Everyone, MoonfallChallenges.LoadBuiltIn().Challenges);
        Assert.True(modes.CampaignOpen(MoonfallCampaignKind.Expansion));
        var far = modes.Stages(MoonfallCampaignKind.Expansion);
        Assert.Equal(MoonfallStageState.Sealed, far[0].State);

        // Its first stage is where the road has come to: no padlock, not drained or dimmed.
        Assert.True(MoonfallLooks.Coming(far[0], progress.ExpansionCleared));
        var look = MoonfallLooks.Stop(far[0], coming: true);
        Assert.False(look.Padlock);
        Assert.False(look.Drained);
        Assert.False(look.Dim);

        // The stages after it are not reached yet, built or not.
        Assert.False(MoonfallLooks.Coming(far[1], progress.ExpansionCleared));
        Assert.True(MoonfallLooks.Stop(far[1]).Padlock);

        // A sealed stage whose levels ship is never "coming".
        var road = Modes(new MoonfallProgress { BaseCleared = 12 }).Stages(MoonfallCampaignKind.Base);
        Assert.Equal(MoonfallStageState.Sealed, road[3].State);
        Assert.False(MoonfallLooks.Coming(road[3], 12));
    }

    [Fact]
    public void Every_companion_has_words_for_the_detail_panel()
    {
        foreach (var info in MoonfallCompanions.All)
        {
            var lore = MoonfallLooks.LoreOf(info.Companion);
            Assert.False(string.IsNullOrWhiteSpace(lore.Role));
            Assert.False(string.IsNullOrWhiteSpace(lore.Line));
            Assert.False(string.IsNullOrWhiteSpace(lore.Does));
            Assert.False(string.IsNullOrWhiteSpace(lore.Lasts));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => MoonfallLooks.LoreOf(MoonfallCompanion.None));
    }

    // ---- The draw watch (the collapsed window) ----

    [Fact]
    public void A_frame_without_a_draw_is_seen_so_the_sound_holds_while_collapsed()
    {
        var watch = default(MoonfallDrawWatch);
        Assert.True(watch.Missed(10));

        // Drawn on frame 10: frame 11's check (before its draw) sees it drawn.
        watch.Drew(10);
        Assert.False(watch.Missed(11));

        // Collapsed: Dalamud skips Draw from frame 11 on, so frame 12's check sees the miss, and every frame after.
        Assert.True(watch.Missed(12));
        Assert.True(watch.Missed(500));

        // Shown again.
        watch.Drew(500);
        Assert.False(watch.Missed(501));
    }
}
