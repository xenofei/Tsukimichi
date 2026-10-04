using Tsukimichi.Core.Companions;

namespace Tsukimichi.Tests.Companions;

/// <summary>The automation level (1.18, A10): which buttons each level shows, Custom, the fine-tuning and the migration.</summary>
public class AutomationLevelTests
{
    [Fact]
    public void Tracker_only_shows_no_hand_off_button()
    {
        Assert.Equal(AutomationButtons.None, AutomationLevels.ButtonsOf(AutomationLevel.TrackerOnly));
    }

    [Fact]
    public void Each_level_adds_its_own_buttons_to_those_below()
    {
        Assert.Equal(AutomationButtons.Teleport | AutomationButtons.Gather, AutomationLevels.ButtonsOf(AutomationLevel.Travel));
        Assert.Equal(
            AutomationButtons.Teleport | AutomationButtons.Gather | AutomationButtons.Walk | AutomationButtons.GoTo,
            AutomationLevels.ButtonsOf(AutomationLevel.TravelAndWalking));
        Assert.Equal(AutomationLevels.Every, AutomationLevels.ButtonsOf(AutomationLevel.FullHandOffs));
    }

    [Fact]
    public void Full_hand_offs_adds_questionable_autoduty_and_artisan()
    {
        var full = AutomationLevels.ButtonsOf(AutomationLevel.FullHandOffs);
        foreach (var button in new[] { AutomationButtons.Questionable, AutomationButtons.AutoDuty, AutomationButtons.Artisan })
        {
            Assert.True(AutomationLevels.Shows(full, button));
            Assert.False(AutomationLevels.Shows(AutomationLevels.ButtonsOf(AutomationLevel.TravelAndWalking), button));
            Assert.Equal(AutomationLevel.FullHandOffs, AutomationLevels.LevelOfButton(button));
        }
    }

    [Fact]
    public void Every_button_belongs_to_exactly_one_level()
    {
        var seen = AutomationButtons.None;
        foreach (var level in AutomationLevels.All)
        {
            var adds = AutomationLevels.Adds(level);
            Assert.Equal(AutomationButtons.None, seen & adds);
            seen |= adds;
        }

        Assert.Equal(AutomationLevels.Every, seen);
        foreach (var button in AutomationLevels.Buttons)
        {
            Assert.True((AutomationLevels.Every & button) == button);
        }
    }

    [Fact]
    public void A_levels_buttons_read_back_as_that_level()
    {
        foreach (var level in AutomationLevels.All)
        {
            Assert.Equal(level, AutomationLevels.LevelOf(AutomationLevels.ButtonsOf(level)));
        }
    }

    [Fact]
    public void A_mix_no_level_shows_reads_as_custom()
    {
        // Full hand-offs without Walk: the 1.17 player who turned Walk off.
        var noWalk = AutomationLevels.With(AutomationLevels.Every, AutomationButtons.Walk, on: false);
        Assert.Null(AutomationLevels.LevelOf(noWalk));

        // Questionable alone, with no travel.
        Assert.Null(AutomationLevels.LevelOf(AutomationButtons.Questionable));
    }

    [Fact]
    public void Fine_tuning_turns_one_button_on_or_off_and_drops_unknown_bits()
    {
        var travel = AutomationLevels.ButtonsOf(AutomationLevel.Travel);
        var withArtisan = AutomationLevels.With(travel, AutomationButtons.Artisan, on: true);
        Assert.True(AutomationLevels.Shows(withArtisan, AutomationButtons.Artisan));
        Assert.True(AutomationLevels.Shows(withArtisan, AutomationButtons.Teleport));
        Assert.Equal(travel, AutomationLevels.With(withArtisan, AutomationButtons.Artisan, on: false));

        Assert.Equal(travel, AutomationLevels.With(travel | (AutomationButtons)1024, AutomationButtons.Artisan, on: false));
    }

    [Fact]
    public void A_fresh_install_starts_at_travel()
    {
        Assert.Equal(AutomationLevel.Travel, AutomationLevels.NewUserDefault);
        var shown = AutomationLevels.Migrate(saved: null, hadFile: false, legacyWalk: true, legacyGoTo: true);
        Assert.Equal(AutomationLevels.ButtonsOf(AutomationLevel.Travel), shown);
        Assert.False(AutomationLevels.Shows(shown, AutomationButtons.Questionable));
    }

    [Fact]
    public void An_update_keeps_every_button_1_17_showed()
    {
        // Defaults before 1.18: every hand-off showed, Walk and Go to giver on.
        var shown = AutomationLevels.Migrate(saved: null, hadFile: true, legacyWalk: true, legacyGoTo: true);
        Assert.Equal(AutomationLevels.Every, shown);
        Assert.Equal(AutomationLevel.FullHandOffs, AutomationLevels.LevelOf(shown));
    }

    [Fact]
    public void An_update_keeps_walk_and_go_to_giver_as_the_player_had_them()
    {
        var noWalk = AutomationLevels.Migrate(saved: null, hadFile: true, legacyWalk: false, legacyGoTo: true);
        Assert.False(AutomationLevels.Shows(noWalk, AutomationButtons.Walk));
        Assert.True(AutomationLevels.Shows(noWalk, AutomationButtons.GoTo));
        Assert.True(AutomationLevels.Shows(noWalk, AutomationButtons.Questionable | AutomationButtons.AutoDuty | AutomationButtons.Artisan | AutomationButtons.Teleport));
        Assert.Null(AutomationLevels.LevelOf(noWalk));

        var neither = AutomationLevels.Migrate(saved: null, hadFile: true, legacyWalk: false, legacyGoTo: false);
        Assert.Equal(AutomationLevels.Every & ~(AutomationButtons.Walk | AutomationButtons.GoTo), neither);
    }

    [Fact]
    public void A_saved_choice_stands_and_the_migration_runs_once()
    {
        var tracker = AutomationLevels.Migrate(saved: AutomationButtons.None, hadFile: true, legacyWalk: true, legacyGoTo: true);
        Assert.Equal(AutomationButtons.None, tracker);

        var saved = AutomationButtons.Teleport | AutomationButtons.Artisan;
        Assert.Equal(saved, AutomationLevels.Migrate(saved, hadFile: true, legacyWalk: false, legacyGoTo: false));

        // A value a newer build wrote keeps the buttons this build knows.
        Assert.Equal(saved, AutomationLevels.Migrate(saved | (AutomationButtons)4096, hadFile: true, legacyWalk: true, legacyGoTo: true));
    }
}
