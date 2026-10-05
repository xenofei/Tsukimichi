using Tsukimichi.Core.Umbra;

namespace Tsukimichi.Tests.Umbra;

/// <summary>
/// "Add to Umbra" and "Remove from Umbra" (<see cref="UmbraSetupRunner"/>) against a fake Umbra: only what is missing
/// changes, every change is recorded as it is made (also one a throwing step made), any failed step puts back that run's
/// changes (its own switch included), a restart that timed out changes nothing more, and Remove reverses only
/// Tsukimichi's own changes, on its own Umbra profile only.
/// </summary>
public sealed class UmbraSetupRunnerTests
{
    private static UmbraSetupBook Book(params UmbraSetupRecord[] records) => records.Aggregate(UmbraSetupBook.Empty, (book, record) => book.With(record));

    private static UmbraSetupRecord On(string profile, bool turnedOn, bool added, params string[] widgets) =>
        new UmbraSetupRecord(turnedOn, added, widgets) { Profile = profile };

    [Fact]
    public async Task From_nothing_it_turns_on_adds_restarts_and_places_and_records_each_change()
    {
        var umbra = new FakeUmbra { Profile = "Main", CharacterId = 42 };
        var records = new List<UmbraSetupRecord>();
        var steps = new List<UmbraSetupStep>();

        var result = await UmbraSetupRunner.Add(umbra, steps.Add, _ => { }, records.Add);

        Assert.Equal(UmbraFailure.None, result.Failure);
        Assert.Equal([UmbraSetupStep.TurnOnCustomPlugins, UmbraSetupStep.AddRepository, UmbraSetupStep.RestartUmbra, UmbraSetupStep.PlaceWidget], steps);
        Assert.True(umbra.CustomPluginsOn);
        Assert.True(umbra.Listed);
        Assert.True(umbra.AddonLoaded);
        Assert.Single(umbra.Widgets);

        // Recorded the moment each change was made, for the profile and character it was made on.
        Assert.Equal(3, records.Count);
        Assert.True(records[0].TurnedOnCustomPlugins);
        Assert.False(records[0].AddedRepository);
        Assert.True(records[1].AddedRepository);
        Assert.Equal([umbra.Widgets[0].Id], records[2].WidgetIds);
        Assert.All(records, r => Assert.Equal("Main", r.Profile));
        Assert.All(records, r => Assert.Equal(42UL, r.CharacterId));
        Assert.True(result.Left.TurnedOnCustomPlugins);
        Assert.True(result.Left.AddedRepository);
        Assert.Equal(records[2].WidgetIds, result.Left.WidgetIds);
    }

    [Fact]
    public async Task Turning_on_records_the_addons_umbra_lists_then()
    {
        var umbra = new FakeUmbra { DormantAddons = 2 };
        var result = await UmbraSetupRunner.Add(umbra, _ => { }, _ => { }, _ => { });

        Assert.Equal(UmbraFailure.None, result.Failure);
        Assert.Equal(2, result.Left.OtherAddonsAtTurnOn);
    }

    [Fact]
    public async Task Custom_plugins_already_on_are_not_touched_or_recorded()
    {
        var umbra = new FakeUmbra { CustomPluginsOn = true };
        var result = await UmbraSetupRunner.Add(umbra, _ => { }, _ => { }, _ => { });

        Assert.Equal(UmbraFailure.None, result.Failure);
        Assert.False(result.Left.TurnedOnCustomPlugins);
        Assert.DoesNotContain("SetCustomPlugins(True)", umbra.Calls);
    }

    [Fact]
    public async Task A_listed_repository_is_not_added_or_recorded()
    {
        var umbra = new FakeUmbra { CustomPluginsOn = true, Listed = true };
        var result = await UmbraSetupRunner.Add(umbra, _ => { }, _ => { }, _ => { });

        Assert.Equal(UmbraFailure.None, result.Failure);
        Assert.False(result.Left.AddedRepository);
        Assert.DoesNotContain("AddRepository", umbra.Calls);
        Assert.Contains("Restart", umbra.Calls);
        Assert.Single(result.Left.WidgetIds);
    }

    [Fact]
    public async Task A_widget_the_player_already_has_is_not_placed_again()
    {
        // The player's own widget is stored; Umbra shows it once the add-on loads after the restart.
        var umbra = new FakeUmbra { CustomPluginsOn = true, Listed = true };
        umbra.Widgets.Add(("players", UmbraAddon.WidgetId));

        var result = await UmbraSetupRunner.Add(umbra, _ => { }, _ => { }, _ => { });

        Assert.Equal(UmbraFailure.None, result.Failure);
        Assert.Empty(result.Left.WidgetIds);
        Assert.DoesNotContain("PlaceWidget", umbra.Calls);
        Assert.Single(umbra.Widgets);
    }

    [Fact]
    public async Task Everything_in_place_changes_nothing()
    {
        var umbra = new FakeUmbra { CustomPluginsOn = true, Listed = true, AddonLoaded = true };
        umbra.Widgets.Add(("players", UmbraAddon.WidgetId));

        var result = await UmbraSetupRunner.Add(umbra, _ => { }, _ => { }, _ => { });

        Assert.Equal(UmbraFailure.None, result.Failure);
        Assert.True(result.Left.IsEmpty);
        Assert.Equal(["Open", "Look"], umbra.Calls);
    }

    [Theory]
    [InlineData(UmbraFailure.NotRunning)]
    [InlineData(UmbraFailure.UnknownUmbra)]
    [InlineData(UmbraFailure.SeveralUmbras)]
    public async Task An_umbra_that_cannot_be_driven_is_never_changed(UmbraFailure failure)
    {
        var umbra = new FakeUmbra { PrepareResult = failure };
        var result = await UmbraSetupRunner.Add(umbra, _ => { }, _ => { }, _ => throw new InvalidOperationException("nothing may be recorded"));

        Assert.Equal(failure, result.Failure);
        Assert.True(result.PutBack);
        Assert.Equal(["Open"], umbra.Calls);
    }

    [Fact]
    public async Task Open_throwing_is_an_unknown_umbra_and_changes_nothing()
    {
        var umbra = new FakeUmbra { ThrowOn = "Open" };
        var result = await UmbraSetupRunner.Add(umbra, _ => { }, _ => { }, _ => { });

        Assert.Equal(UmbraFailure.UnknownUmbra, result.Failure);
        Assert.False(umbra.CustomPluginsOn);
    }

    [Theory]
    [InlineData(UmbraFailure.ReleaseUnreachable)]
    [InlineData(UmbraFailure.ReleaseRefused)]
    [InlineData(UmbraFailure.TimedOut)]
    public async Task A_failed_download_turns_custom_plugins_back_off(UmbraFailure failure)
    {
        var umbra = new FakeUmbra { AddResult = failure };
        var records = new List<UmbraSetupRecord>();
        var undo = new List<UmbraUndoStep>();

        var result = await UmbraSetupRunner.Add(umbra, _ => { }, undo.Add, records.Add);

        Assert.Equal(failure, result.Failure);
        Assert.True(result.PutBack);
        Assert.False(umbra.CustomPluginsOn);
        Assert.False(umbra.Listed);
        Assert.Contains(UmbraUndoStep.TurnOffCustomPlugins, undo);
        Assert.True(records[^1].IsEmpty);
    }

    [Fact]
    public async Task Putting_back_turns_custom_plugins_off_even_when_dormant_addons_loaded_with_them()
    {
        // The player once had add-ons and turned custom plugins off; turning them on loads those stored entries.
        var umbra = new FakeUmbra { DormantAddons = 2, AddResult = UmbraFailure.ReleaseUnreachable };
        var result = await UmbraSetupRunner.Add(umbra, _ => { }, _ => { }, _ => { });

        Assert.Equal(UmbraFailure.ReleaseUnreachable, result.Failure);
        Assert.True(result.PutBack);
        Assert.False(result.KeptCustomPluginsOn);
        Assert.False(umbra.CustomPluginsOn);
        Assert.Equal(UmbraSetupOutcome.Failed, UmbraAddonSetup.OutcomeOf(result.Failure, result.PutBack, UmbraHelloWait.Waiting));
    }

    [Fact]
    public async Task A_download_that_lists_and_then_fails_is_removed_too()
    {
        var umbra = new FakeUmbra { AddResult = UmbraFailure.Unexpected, AddListsAnyway = true };
        var result = await UmbraSetupRunner.Add(umbra, _ => { }, _ => { }, _ => { });

        Assert.Equal(UmbraFailure.Unexpected, result.Failure);
        Assert.True(result.PutBack);
        Assert.False(umbra.Listed);
        Assert.False(umbra.CustomPluginsOn);
    }

    [Fact]
    public async Task An_addon_umbra_does_not_load_is_removed_and_custom_plugins_go_back_off()
    {
        var umbra = new FakeUmbra { RestartLoads = false };
        var result = await UmbraSetupRunner.Add(umbra, _ => { }, _ => { }, _ => { });

        Assert.Equal(UmbraFailure.NotLoaded, result.Failure);
        Assert.True(result.PutBack);
        Assert.False(umbra.Listed);
        Assert.False(umbra.CustomPluginsOn);
        Assert.Empty(umbra.Widgets);
    }

    [Fact]
    public async Task A_widget_umbra_does_not_place_puts_everything_back()
    {
        var umbra = new FakeUmbra { PlaceFails = true };
        var result = await UmbraSetupRunner.Add(umbra, _ => { }, _ => { }, _ => { });

        Assert.Equal(UmbraFailure.WidgetNotPlaced, result.Failure);
        Assert.True(result.PutBack);
        Assert.False(umbra.Listed);
        Assert.False(umbra.CustomPluginsOn);
        Assert.False(umbra.AddonLoaded);
    }

    [Fact]
    public async Task Umbra_throwing_mid_run_puts_back_only_what_this_run_changed()
    {
        // The player had custom plugins on and their own add-on listed.
        var umbra = new FakeUmbra { CustomPluginsOn = true, OtherAddons = 1, ThrowOn = "Restart" };
        var result = await UmbraSetupRunner.Add(umbra, _ => { }, _ => { }, _ => { });

        Assert.Equal(UmbraFailure.Unexpected, result.Failure);
        Assert.True(result.PutBack);
        Assert.False(umbra.Listed);
        Assert.True(umbra.CustomPluginsOn);
        Assert.Equal(1, umbra.OtherAddons);
    }

    [Theory]
    [InlineData("SetCustomPlugins(True)")]
    [InlineData("AddRepository")]
    [InlineData("PlaceWidget")]
    public async Task A_step_that_throws_after_changing_umbra_is_recorded_and_put_back(string step)
    {
        var umbra = new FakeUmbra { ThrowAfter = step };
        var records = new List<UmbraSetupRecord>();

        var result = await UmbraSetupRunner.Add(umbra, _ => { }, _ => { }, records.Add);

        Assert.Equal(UmbraFailure.Unexpected, result.Failure);
        Assert.True(result.PutBack);
        Assert.False(umbra.CustomPluginsOn);
        Assert.False(umbra.Listed);
        Assert.Empty(umbra.Widgets);

        // The change the throwing step made was recorded before it was put back.
        var salvaged = records.First(r => !r.IsEmpty);
        switch (step)
        {
            case "SetCustomPlugins(True)":
                Assert.True(salvaged.TurnedOnCustomPlugins);
                break;
            case "AddRepository":
                Assert.Contains(records, r => r.AddedRepository);
                break;
            default:
                Assert.Contains(records, r => r.WidgetIds.Count == 1);
                break;
        }

        Assert.True(records[^1].IsEmpty);
    }

    [Fact]
    public async Task When_putting_back_fails_too_what_is_left_stays_recorded()
    {
        var umbra = new FakeUmbra { RestartLoads = false, ThrowOnUndo = "RemoveRepository" };
        var records = new List<UmbraSetupRecord>();
        var result = await UmbraSetupRunner.Add(umbra, _ => { }, _ => { }, records.Add);

        Assert.Equal(UmbraFailure.NotLoaded, result.Failure);
        Assert.False(result.PutBack);
        Assert.True(result.Left.AddedRepository);
        Assert.True(records[^1].AddedRepository);
        Assert.Equal(UmbraSetupOutcome.FailedPartly, UmbraAddonSetup.OutcomeOf(result.Failure, result.PutBack, UmbraHelloWait.Waiting));
    }

    [Fact]
    public async Task A_restart_that_times_out_changes_nothing_more_and_keeps_the_record()
    {
        var umbra = new FakeUmbra { RestartResult = UmbraFailure.RestartTimedOut };
        var undo = new List<UmbraUndoStep>();
        var result = await UmbraSetupRunner.Add(umbra, _ => { }, undo.Add, _ => { });

        Assert.Equal(UmbraFailure.RestartTimedOut, result.Failure);
        Assert.Empty(undo);
        Assert.False(result.PutBack);
        Assert.True(result.Left.TurnedOnCustomPlugins);
        Assert.True(result.Left.AddedRepository);
        Assert.True(umbra.CustomPluginsOn);
        Assert.True(umbra.Listed);
        Assert.Single(umbra.Calls, "Restart");
        Assert.Equal(UmbraSetupOutcome.FailedPartly, UmbraAddonSetup.OutcomeOf(result.Failure, result.PutBack, UmbraHelloWait.Waiting));
    }

    [Fact]
    public async Task Remove_reverses_only_tsukimichis_own_changes()
    {
        // The player: custom plugins on, another add-on, and their own Tsukimichi widget. Tsukimichi: the repository and one widget.
        var umbra = new FakeUmbra { CustomPluginsOn = true, Listed = true, AddonLoaded = true, OtherAddons = 1 };
        umbra.Widgets.Add(("players", UmbraAddon.WidgetId));
        umbra.Widgets.Add(("ours", UmbraAddon.WidgetId));
        var records = new List<UmbraSetupRecord>();

        var result = await UmbraSetupRunner.Remove(umbra, Book(On(umbra.Profile, false, true, "ours")), _ => { }, records.Add);

        Assert.Equal(UmbraFailure.None, result.Failure);
        Assert.True(result.PutBack);
        Assert.Equal(["players"], umbra.Widgets.Select(w => w.Id));
        Assert.False(umbra.Listed);
        Assert.True(umbra.CustomPluginsOn);
        Assert.Equal(1, umbra.OtherAddons);
        Assert.True(records[^1].IsEmpty);
    }

    [Fact]
    public async Task Remove_turns_custom_plugins_off_unless_the_player_added_addons_since()
    {
        // Two add-ons loaded when Tsukimichi turned custom plugins on: still two, so they go back off.
        var same = new FakeUmbra { CustomPluginsOn = true, Listed = true, AddonLoaded = true, OtherAddons = 2 };
        var result = await UmbraSetupRunner.Remove(same, Book(On(same.Profile, true, true) with { OtherAddonsAtTurnOn = 2 }), _ => { }, _ => { });
        Assert.False(same.CustomPluginsOn);
        Assert.False(result.KeptCustomPluginsOn);

        // A third since: they stay on for it.
        var grown = new FakeUmbra { CustomPluginsOn = true, Listed = true, AddonLoaded = true, OtherAddons = 3 };
        result = await UmbraSetupRunner.Remove(grown, Book(On(grown.Profile, true, true) with { OtherAddonsAtTurnOn = 2 }), _ => { }, _ => { });
        Assert.True(grown.CustomPluginsOn);
        Assert.True(result.KeptCustomPluginsOn);
        Assert.True(result.Left.IsEmpty);

        // The player's own switch is never touched.
        var players = new FakeUmbra { CustomPluginsOn = true, Listed = true, AddonLoaded = true };
        await UmbraSetupRunner.Remove(players, Book(On(players.Profile, false, false, "gone")), _ => { }, _ => { });
        Assert.True(players.CustomPluginsOn);
        Assert.True(players.Listed);
    }

    [Fact]
    public async Task Remove_of_a_widget_already_gone_still_clears_the_record()
    {
        var umbra = new FakeUmbra { CustomPluginsOn = true, Listed = true, AddonLoaded = true };
        var result = await UmbraSetupRunner.Remove(umbra, Book(On(umbra.Profile, false, false, "gone")), _ => { }, _ => { });

        Assert.Equal(UmbraFailure.None, result.Failure);
        Assert.True(result.Left.IsEmpty);
        Assert.True(umbra.Listed);
    }

    [Fact]
    public async Task Remove_restarts_umbra_only_when_the_repository_or_the_switch_changed()
    {
        var widgetOnly = new FakeUmbra { CustomPluginsOn = true, Listed = true, AddonLoaded = true };
        widgetOnly.Widgets.Add(("ours", UmbraAddon.WidgetId));
        await UmbraSetupRunner.Remove(widgetOnly, Book(On(widgetOnly.Profile, false, false, "ours")), _ => { }, _ => { });
        Assert.DoesNotContain("Restart", widgetOnly.Calls);

        var repository = new FakeUmbra { CustomPluginsOn = true, Listed = true, AddonLoaded = true };
        await UmbraSetupRunner.Remove(repository, Book(On(repository.Profile, false, true)), _ => { }, _ => { });
        Assert.Contains("Restart", repository.Calls);
        Assert.False(repository.AddonLoaded);
    }

    [Fact]
    public async Task Remove_on_another_umbra_profile_changes_nothing()
    {
        // Tsukimichi's changes were made on profile "Main"; this character's Umbra runs "Alt", where the player has
        // the repository listed and custom plugins on themselves.
        var umbra = new FakeUmbra { Profile = "Alt", CustomPluginsOn = true, Listed = true, AddonLoaded = true };
        var book = Book(On("Main", true, true, "ours"));
        var records = new List<UmbraSetupRecord>();

        var result = await UmbraSetupRunner.Remove(umbra, book, _ => { }, records.Add);

        Assert.Equal(UmbraFailure.OtherProfile, result.Failure);
        Assert.Empty(records);
        Assert.True(umbra.CustomPluginsOn);
        Assert.True(umbra.Listed);
        Assert.Equal(["Open", "Look"], umbra.Calls);

        // On "Main" it undoes them.
        umbra.Profile = "Main";
        result = await UmbraSetupRunner.Remove(umbra, book, _ => { }, records.Add);
        Assert.Equal(UmbraFailure.None, result.Failure);
        Assert.False(umbra.Listed);
        Assert.All(records, r => Assert.Equal("Main", r.Profile));
    }

    [Fact]
    public async Task Remove_on_an_umbra_that_cannot_be_driven_keeps_the_book()
    {
        var umbra = new FakeUmbra { PrepareResult = UmbraFailure.UnknownUmbra };
        var records = new List<UmbraSetupRecord>();
        var result = await UmbraSetupRunner.Remove(umbra, Book(On(umbra.Profile, true, true, "ours")), _ => { }, records.Add);

        Assert.Equal(UmbraFailure.UnknownUmbra, result.Failure);
        Assert.Empty(records);
    }

    [Fact]
    public async Task A_failed_restart_after_removing_still_clears_the_saved_changes()
    {
        // The changes are saved in Umbra by then; Umbra applies them when it next starts.
        var umbra = new FakeUmbra { CustomPluginsOn = true, Listed = true, AddonLoaded = true, RestartResult = UmbraFailure.RestartTimedOut };
        var result = await UmbraSetupRunner.Remove(umbra, Book(On(umbra.Profile, false, true)), _ => { }, _ => { });

        Assert.Equal(UmbraFailure.RestartTimedOut, result.Failure);
        Assert.True(result.Left.IsEmpty);
        Assert.False(umbra.Listed);
    }
}
