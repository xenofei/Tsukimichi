using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Umbra;

namespace Tsukimichi.Tests.Umbra;

/// <summary>
/// Setting up Tsukimichi for Umbra (<see cref="UmbraAddonSetup"/>): when the card shows and hides, that it never shows
/// alongside another popup and stays answered, the step plan for each state of Umbra, the undo plan (only Tsukimichi's
/// own changes), the record, and the outcome mapping.
/// </summary>
public sealed class UmbraAddonSetupTests
{
    private static readonly WhatsNewMoment Quiet = new(true, WhatsNew.SettleSeconds, false, false, false, false, false);

    // Umbra running, its settings read, the add-on neither listed nor answering: the card's one reason to show.
    private static readonly UmbraOfferState Owed = new(false, true, true, false, false, false, false);

    [Fact]
    public void The_card_shows_at_a_quiet_moment_to_a_player_running_umbra_without_the_addon()
    {
        Assert.Equal(UmbraOfferStep.Show, UmbraAddonSetup.Next(Owed, Quiet));
    }

    [Fact]
    public void Once_answered_it_never_shows_again()
    {
        Assert.Equal(UmbraOfferStep.Nothing, UmbraAddonSetup.Next(Owed with { Answered = true }, Quiet));
        Assert.Equal(UmbraOfferStep.Nothing, UmbraAddonSetup.Next(Owed with { Answered = true, AddonHello = true }, Quiet));
    }

    [Fact]
    public void A_running_addon_retires_the_card()
    {
        Assert.Equal(UmbraOfferStep.Retire, UmbraAddonSetup.Next(Owed with { AddonHello = true, AddonPresent = true }, Quiet));
    }

    [Theory]
    [InlineData(false, true, false, false)] // Umbra not loaded
    [InlineData(true, false, false, false)] // its settings not read yet
    [InlineData(true, true, true, false)] // listed with custom plugins on: about to answer
    [InlineData(true, true, false, true)] // being set up from Settings
    public void It_waits_while_umbra_is_unknown_the_addon_is_listed_or_a_setup_runs(bool loaded, bool read, bool present, bool busy)
    {
        var state = Owed with { UmbraLoaded = loaded, SettingsRead = read, AddonPresent = present, Busy = busy };
        Assert.Equal(UmbraOfferStep.Wait, UmbraAddonSetup.Next(state, Quiet));
    }

    [Fact]
    public void It_never_shows_alongside_whats_new_the_tour_or_the_portrait_pack_offer()
    {
        Assert.Equal(UmbraOfferStep.Wait, UmbraAddonSetup.Next(Owed with { OtherFirst = true }, Quiet));

        // Open, it steps aside while one of them is on screen.
        Assert.False(UmbraAddonSetup.Visible(Quiet, otherFirst: true));
        Assert.True(UmbraAddonSetup.Visible(Quiet, otherFirst: false));
    }

    public static TheoryData<WhatsNewMoment> NotQuiet => new()
    {
        Quiet with { InWorld = false, SecondsInWorld = 0 },
        Quiet with { SecondsInWorld = WhatsNew.SettleSeconds - 1 },
        Quiet with { InCombat = true },
        Quiet with { InDuty = true },
        Quiet with { InCutscene = true },
        Quiet with { GroupPose = true },
        Quiet with { Loading = true },
    };

    [Theory]
    [MemberData(nameof(NotQuiet))]
    public void It_waits_for_a_quiet_moment(WhatsNewMoment moment)
    {
        Assert.Equal(UmbraOfferStep.Wait, UmbraAddonSetup.Next(Owed, moment));
    }

    public static TheoryData<WhatsNewMoment> Hidden => new()
    {
        Quiet with { InWorld = false },
        Quiet with { InCombat = true },
        Quiet with { InDuty = true },
        Quiet with { InCutscene = true },
        Quiet with { GroupPose = true },
        Quiet with { Loading = true },
    };

    [Theory]
    [MemberData(nameof(Hidden))]
    public void Open_it_hides_in_combat_duties_cutscenes_and_while_not_logged_in(WhatsNewMoment moment)
    {
        Assert.False(UmbraAddonSetup.Visible(moment, otherFirst: false));
    }

    [Fact]
    public void Custom_plugins_off_and_nothing_added_plans_every_step()
    {
        var plan = UmbraAddonSetup.Plan(new UmbraLook(false, false, false, false, 0));
        Assert.Equal([UmbraSetupStep.TurnOnCustomPlugins, UmbraSetupStep.AddRepository, UmbraSetupStep.RestartUmbra, UmbraSetupStep.PlaceWidget], plan);
    }

    [Fact]
    public void Custom_plugins_already_on_are_left_as_they_are()
    {
        var plan = UmbraAddonSetup.Plan(new UmbraLook(true, false, false, false, 2));
        Assert.Equal([UmbraSetupStep.AddRepository, UmbraSetupStep.RestartUmbra, UmbraSetupStep.PlaceWidget], plan);
    }

    [Fact]
    public void A_listed_repository_is_not_added_again_but_custom_plugins_off_still_turn_on()
    {
        // Listed while custom plugins were off: Umbra loads it once they are on and it restarts.
        var plan = UmbraAddonSetup.Plan(new UmbraLook(false, true, false, false, 0));
        Assert.Equal([UmbraSetupStep.TurnOnCustomPlugins, UmbraSetupStep.RestartUmbra, UmbraSetupStep.PlaceWidget], plan);
    }

    [Fact]
    public void A_listed_repository_that_isnt_loaded_yet_needs_only_the_restart_and_the_widget()
    {
        var plan = UmbraAddonSetup.Plan(new UmbraLook(true, true, false, false, 0));
        Assert.Equal([UmbraSetupStep.RestartUmbra, UmbraSetupStep.PlaceWidget], plan);
    }

    [Fact]
    public void A_loaded_addon_without_its_widget_gets_only_the_widget()
    {
        var plan = UmbraAddonSetup.Plan(new UmbraLook(true, true, true, false, 0));
        Assert.Equal([UmbraSetupStep.PlaceWidget], plan);
    }

    [Fact]
    public void A_widget_already_on_the_bar_is_never_placed_twice()
    {
        Assert.Empty(UmbraAddonSetup.Plan(new UmbraLook(true, true, true, true, 0)));
        Assert.DoesNotContain(UmbraSetupStep.PlaceWidget, UmbraAddonSetup.Plan(new UmbraLook(false, false, false, true, 0)));
    }

    [Fact]
    public void Undo_reverses_everything_tsukimichi_did_when_nothing_else_uses_it()
    {
        var record = new UmbraSetupRecord(true, true, ["w1"]);
        var plan = UmbraAddonSetup.UndoPlan(record, new UmbraLook(true, true, true, true, 0));
        Assert.Equal([UmbraUndoStep.RemoveWidgets, UmbraUndoStep.RemoveRepository, UmbraUndoStep.TurnOffCustomPlugins, UmbraUndoStep.RestartUmbra], plan);
    }

    [Fact]
    public void Undo_leaves_what_the_player_had_alone()
    {
        // The player had custom plugins on and the repository listed; Tsukimichi only placed the widget.
        var plan = UmbraAddonSetup.UndoPlan(new UmbraSetupRecord(false, false, ["w1"]), new UmbraLook(true, true, true, true, 3));
        Assert.Equal([UmbraUndoStep.RemoveWidgets], plan);
    }

    [Fact]
    public void Custom_plugins_tsukimichi_turned_on_stay_on_while_another_addon_uses_them()
    {
        var record = new UmbraSetupRecord(true, true, []);
        var look = new UmbraLook(true, true, true, false, 1);
        Assert.Equal([UmbraUndoStep.RemoveRepository, UmbraUndoStep.RestartUmbra], UmbraAddonSetup.UndoPlan(record, look));
        Assert.True(UmbraAddonSetup.KeepsCustomPluginsOn(record, look));
        Assert.False(UmbraAddonSetup.KeepsCustomPluginsOn(record, look with { OtherAddons = 0 }));
        Assert.Null(UmbraAddonSetup.KeepsCustomPluginsOn(record with { TurnedOnCustomPlugins = false }, look));
        Assert.Null(UmbraAddonSetup.KeepsCustomPluginsOn(record, look with { CustomPluginsOn = false }));
    }

    [Fact]
    public void A_repository_the_player_already_removed_is_not_removed_again()
    {
        var plan = UmbraAddonSetup.UndoPlan(new UmbraSetupRecord(false, true, []), new UmbraLook(true, false, false, false, 0));
        Assert.Empty(plan);
    }

    [Fact]
    public void An_empty_record_undoes_nothing()
    {
        Assert.Empty(UmbraAddonSetup.UndoPlan(UmbraSetupRecord.Empty, new UmbraLook(true, true, true, true, 0)));
        Assert.True(UmbraSetupRecord.Empty.IsEmpty);
    }

    [Fact]
    public void The_record_merges_runs_and_drops_widgets_one_by_one()
    {
        var first = new UmbraSetupRecord(true, false, ["a"]);
        var merged = first.Merge(new UmbraSetupRecord(false, true, ["a", "b"]));
        Assert.True(merged.TurnedOnCustomPlugins);
        Assert.True(merged.AddedRepository);
        Assert.Equal(["a", "b"], merged.WidgetIds);

        var less = merged.WithoutWidget("a");
        Assert.Equal(["b"], less.WidgetIds);
        Assert.Equal(["b", "c"], less.WithWidget("c").WithWidget("c").WidgetIds);
        Assert.False(less.IsEmpty);
        Assert.True((less with { TurnedOnCustomPlugins = false, AddedRepository = false }).WithoutWidget("b").IsEmpty);
    }

    [Theory]
    [InlineData(UmbraFailure.None, true, UmbraHelloWait.Answered, UmbraSetupOutcome.Added)]
    [InlineData(UmbraFailure.None, true, UmbraHelloWait.TimedOut, UmbraSetupOutcome.NotConfirmed)]
    [InlineData(UmbraFailure.ReleaseUnreachable, true, UmbraHelloWait.Waiting, UmbraSetupOutcome.Failed)]
    [InlineData(UmbraFailure.NotLoaded, false, UmbraHelloWait.Waiting, UmbraSetupOutcome.FailedPartly)]
    [InlineData(UmbraFailure.UnknownUmbra, true, UmbraHelloWait.Answered, UmbraSetupOutcome.Failed)]
    public void The_outcome_follows_the_failure_the_put_back_and_the_hello(UmbraFailure failure, bool putBack, UmbraHelloWait hello, UmbraSetupOutcome expected)
    {
        Assert.Equal(expected, UmbraAddonSetup.OutcomeOf(failure, putBack, hello));
    }

    [Fact]
    public void Still_waiting_for_the_hello_has_no_outcome_yet()
    {
        Assert.Null(UmbraAddonSetup.OutcomeOf(UmbraFailure.None, true, UmbraHelloWait.Waiting));
    }

    [Fact]
    public void The_hello_wait_times_out()
    {
        Assert.Equal(UmbraHelloWait.Waiting, UmbraAddonSetup.HelloWait(false, UmbraAddonSetup.HelloTimeoutSeconds - 0.1));
        Assert.Equal(UmbraHelloWait.TimedOut, UmbraAddonSetup.HelloWait(false, UmbraAddonSetup.HelloTimeoutSeconds));
        Assert.Equal(UmbraHelloWait.Answered, UmbraAddonSetup.HelloWait(true, 999));
    }

    [Fact]
    public void Every_outcome_but_added_shows_the_manual_steps()
    {
        foreach (var outcome in Enum.GetValues<UmbraSetupOutcome>())
        {
            Assert.Equal(outcome != UmbraSetupOutcome.Added, UmbraAddonSetup.ShowsManualSteps(outcome));
        }
    }

    [Fact]
    public void Try_again_is_offered_only_for_failures_that_may_pass()
    {
        Assert.True(UmbraAddonSetup.CanTryAgain(UmbraFailure.ReleaseUnreachable));
        Assert.True(UmbraAddonSetup.CanTryAgain(UmbraFailure.TimedOut));
        Assert.True(UmbraAddonSetup.CanTryAgain(UmbraFailure.NotRunning));
        Assert.False(UmbraAddonSetup.CanTryAgain(UmbraFailure.UnknownUmbra));
        Assert.False(UmbraAddonSetup.CanTryAgain(UmbraFailure.ReleaseRefused));
    }

    [Fact]
    public void The_addon_is_the_one_tsukimichi_links_to()
    {
        Assert.Equal("xenofei/Tsukimichi.Umbra", UmbraAddon.Repository);
        Assert.Equal(UmbraSettings.AddonName, UmbraAddon.RepositoryName);
        Assert.Contains(UmbraAddon.WidgetPanel, new[] { "Left", "Center", "Right" });
    }

    [Fact]
    public void Putting_back_a_failed_run_always_reverts_the_switch_it_turned_on()
    {
        // Dormant add-ons loaded when it turned custom plugins on; they were off before the run, so off they go again.
        var record = new UmbraSetupRecord(true, true, []) { OtherAddonsAtTurnOn = 3 };
        var look = new UmbraLook(true, true, false, false, 3);
        Assert.Contains(UmbraUndoStep.TurnOffCustomPlugins, UmbraAddonSetup.UndoPlan(record, look, sameRun: true));
        Assert.False(UmbraAddonSetup.KeepsCustomPluginsOn(record, look with { OtherAddons = 9 }, sameRun: true));
    }

    [Fact]
    public void A_later_remove_keeps_custom_plugins_on_only_if_addons_were_added_since()
    {
        var record = new UmbraSetupRecord(true, false, []) { OtherAddonsAtTurnOn = 2 };
        Assert.False(UmbraAddonSetup.KeepsCustomPluginsOn(record, new UmbraLook(true, false, false, false, 2)));
        Assert.Contains(UmbraUndoStep.TurnOffCustomPlugins, UmbraAddonSetup.UndoPlan(record, new UmbraLook(true, false, false, false, 2)));
        Assert.True(UmbraAddonSetup.KeepsCustomPluginsOn(record, new UmbraLook(true, false, false, false, 3)));
        Assert.DoesNotContain(UmbraUndoStep.TurnOffCustomPlugins, UmbraAddonSetup.UndoPlan(record, new UmbraLook(true, false, false, false, 3)));
    }

    [Fact]
    public void Anything_that_restarts_umbra_waits_for_a_quiet_moment()
    {
        Assert.True(UmbraAddonSetup.CanChangeNow(Quiet));
        Assert.True(UmbraAddonSetup.CanChangeNow(Quiet with { SecondsInWorld = 0 }));
        foreach (var moment in Hidden)
        {
            Assert.False(UmbraAddonSetup.CanChangeNow(moment));
        }
    }

    [Fact]
    public void Records_merge_on_one_profile_and_keep_the_count_of_the_run_that_turned_on()
    {
        var earlier = new UmbraSetupRecord(true, false, ["a"]) { Profile = "Main", CharacterId = 1, OtherAddonsAtTurnOn = 1 };
        var later = earlier.Merge(new UmbraSetupRecord(false, true, ["b"]) { Profile = "Main", CharacterId = 2 });
        Assert.Equal(1, later.OtherAddonsAtTurnOn);
        Assert.Equal(2UL, later.CharacterId);
        Assert.Equal(["a", "b"], later.WidgetIds);

        var turnedOnAgain = UmbraSetupRecord.For("Main").Merge(new UmbraSetupRecord(true, false, []) { Profile = "Main", OtherAddonsAtTurnOn = 4 });
        Assert.Equal(4, turnedOnAgain.OtherAddonsAtTurnOn);
    }

    [Fact]
    public void The_book_keeps_one_record_per_umbra_profile()
    {
        var book = UmbraSetupBook.Empty
            .With(new UmbraSetupRecord(true, true, ["a"]) { Profile = "Main" })
            .With(new UmbraSetupRecord(false, false, ["b"]) { Profile = "Alt" });
        Assert.Equal(2, book.Records.Count);
        Assert.Equal(["a"], book.For("Main").WidgetIds);
        Assert.True(book.For("Other").IsEmpty);
        Assert.Equal("Other", book.For("Other").Profile);
        Assert.Equal(["Alt"], book.OtherProfiles("Main"));
        Assert.Equal(["Main", "Alt"], book.OtherProfiles(null));

        // An emptied record leaves the book.
        var less = book.With(UmbraSetupRecord.For("Alt"));
        Assert.Single(less.Records);
        Assert.True(less.With(UmbraSetupRecord.For("Main")).IsEmpty);
    }

    [Fact]
    public void The_book_survives_its_saved_form_and_a_hand_edited_one()
    {
        var book = UmbraSetupBook.Empty.With(new UmbraSetupRecord(true, true, ["a"]) { Profile = "Main", CharacterId = 9, OtherAddonsAtTurnOn = 2 });
        var back = UmbraSetupBook.FromData(book.ToData());
        var record = back.For("Main");
        Assert.True(record.TurnedOnCustomPlugins);
        Assert.True(record.AddedRepository);
        Assert.Equal(["a"], record.WidgetIds);
        Assert.Equal(9UL, record.CharacterId);
        Assert.Equal(2, record.OtherAddonsAtTurnOn);

        // Nulls, a missing profile, empty widget ids and a negative count read as nothing or zero.
        var odd = UmbraSetupBook.FromData(
        [
            null,
            new UmbraSetupRecordData { Profile = null, AddedRepository = true },
            new UmbraSetupRecordData { Profile = "Main", WidgetIds = null, AddedRepository = true, OtherAddonsAtTurnOn = -5 },
            new UmbraSetupRecordData { Profile = "Empty", WidgetIds = [""] },
        ]);
        Assert.Single(odd.Records);
        Assert.Equal(0, odd.For("Main").OtherAddonsAtTurnOn);
        Assert.Empty(odd.For("Main").WidgetIds);
        Assert.True(UmbraSetupBook.FromData(null).IsEmpty);
    }
}
