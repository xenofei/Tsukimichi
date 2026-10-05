using Tsukimichi.Core.Umbra;

namespace Tsukimichi.Tests.Umbra;

/// <summary>
/// "Add to Umbra" and "Remove from Umbra" (<see cref="UmbraSetupRunner"/>) against a fake Umbra: only what is missing
/// changes, every change is recorded as it is made, any failed step puts back that run's changes (and only those), and
/// Remove reverses only Tsukimichi's own changes.
/// </summary>
public sealed class UmbraSetupRunnerTests
{
    [Fact]
    public async Task From_nothing_it_turns_on_adds_restarts_and_places_and_records_each_change()
    {
        var umbra = new FakeUmbra();
        var records = new List<UmbraSetupRecord>();
        var steps = new List<UmbraSetupStep>();

        var result = await UmbraSetupRunner.Add(umbra, steps.Add, _ => { }, records.Add);

        Assert.Equal(UmbraFailure.None, result.Failure);
        Assert.Equal([UmbraSetupStep.TurnOnCustomPlugins, UmbraSetupStep.AddRepository, UmbraSetupStep.RestartUmbra, UmbraSetupStep.PlaceWidget], steps);
        Assert.True(umbra.CustomPluginsOn);
        Assert.True(umbra.Listed);
        Assert.True(umbra.AddonLoaded);
        Assert.Single(umbra.Widgets);

        // Recorded the moment each change was made, so a crash mid-run still leaves something to undo.
        Assert.Equal(3, records.Count);
        Assert.True(records[0].TurnedOnCustomPlugins);
        Assert.False(records[0].AddedRepository);
        Assert.True(records[1].AddedRepository);
        Assert.Equal([umbra.Widgets[0].Id], records[2].WidgetIds);
        Assert.True(result.Left.TurnedOnCustomPlugins);
        Assert.True(result.Left.AddedRepository);
        Assert.Equal(records[2].WidgetIds, result.Left.WidgetIds);
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
        Assert.Equal(["Prepare", "Look"], umbra.Calls);
    }

    [Theory]
    [InlineData(UmbraFailure.NotRunning)]
    [InlineData(UmbraFailure.UnknownUmbra)]
    public async Task An_umbra_that_cannot_be_driven_is_never_changed(UmbraFailure failure)
    {
        var umbra = new FakeUmbra { PrepareResult = failure };
        var result = await UmbraSetupRunner.Add(umbra, _ => { }, _ => { }, _ => throw new InvalidOperationException("nothing may be recorded"));

        Assert.Equal(failure, result.Failure);
        Assert.True(result.PutBack);
        Assert.Equal(["Prepare"], umbra.Calls);
    }

    [Fact]
    public async Task Prepare_throwing_is_an_unknown_umbra_and_changes_nothing()
    {
        var umbra = new FakeUmbra { ThrowOn = "Prepare" };
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
    public async Task Remove_reverses_only_tsukimichis_own_changes()
    {
        // The player: custom plugins on, another add-on, and their own Tsukimichi widget. Tsukimichi: the repository and one widget.
        var umbra = new FakeUmbra { CustomPluginsOn = true, Listed = true, AddonLoaded = true, OtherAddons = 1 };
        umbra.Widgets.Add(("players", UmbraAddon.WidgetId));
        umbra.Widgets.Add(("ours", UmbraAddon.WidgetId));
        var records = new List<UmbraSetupRecord>();

        var result = await UmbraSetupRunner.Remove(umbra, new UmbraSetupRecord(false, true, ["ours"]), _ => { }, records.Add);

        Assert.Equal(UmbraFailure.None, result.Failure);
        Assert.True(result.PutBack);
        Assert.Equal(["players"], umbra.Widgets.Select(w => w.Id));
        Assert.False(umbra.Listed);
        Assert.True(umbra.CustomPluginsOn);
        Assert.Equal(1, umbra.OtherAddons);
        Assert.True(records[^1].IsEmpty);
    }

    [Fact]
    public async Task Remove_turns_custom_plugins_off_only_when_tsukimichi_turned_them_on_and_nothing_else_uses_them()
    {
        var alone = new FakeUmbra { CustomPluginsOn = true, Listed = true, AddonLoaded = true };
        var result = await UmbraSetupRunner.Remove(alone, new UmbraSetupRecord(true, true, []), _ => { }, _ => { });
        Assert.False(alone.CustomPluginsOn);
        Assert.False(result.KeptCustomPluginsOn);

        var shared = new FakeUmbra { CustomPluginsOn = true, Listed = true, AddonLoaded = true, OtherAddons = 2 };
        result = await UmbraSetupRunner.Remove(shared, new UmbraSetupRecord(true, true, []), _ => { }, _ => { });
        Assert.True(shared.CustomPluginsOn);
        Assert.True(result.KeptCustomPluginsOn);
        Assert.True(result.Left.IsEmpty);

        var players = new FakeUmbra { CustomPluginsOn = true, Listed = true, AddonLoaded = true };
        await UmbraSetupRunner.Remove(players, new UmbraSetupRecord(false, false, []), _ => { }, _ => { });
        Assert.True(players.CustomPluginsOn);
        Assert.True(players.Listed);
    }

    [Fact]
    public async Task Remove_of_a_widget_already_gone_still_clears_the_record()
    {
        var umbra = new FakeUmbra { CustomPluginsOn = true, Listed = true, AddonLoaded = true };
        var result = await UmbraSetupRunner.Remove(umbra, new UmbraSetupRecord(false, false, ["gone"]), _ => { }, _ => { });

        Assert.Equal(UmbraFailure.None, result.Failure);
        Assert.True(result.Left.IsEmpty);
        Assert.True(umbra.Listed);
    }

    [Fact]
    public async Task Remove_restarts_umbra_only_when_the_repository_or_the_switch_changed()
    {
        var widgetOnly = new FakeUmbra { CustomPluginsOn = true, Listed = true, AddonLoaded = true };
        widgetOnly.Widgets.Add(("ours", UmbraAddon.WidgetId));
        await UmbraSetupRunner.Remove(widgetOnly, new UmbraSetupRecord(false, false, ["ours"]), _ => { }, _ => { });
        Assert.DoesNotContain("Restart", widgetOnly.Calls);

        var repository = new FakeUmbra { CustomPluginsOn = true, Listed = true, AddonLoaded = true };
        await UmbraSetupRunner.Remove(repository, new UmbraSetupRecord(false, true, []), _ => { }, _ => { });
        Assert.Contains("Restart", repository.Calls);
        Assert.False(repository.AddonLoaded);
    }

    [Fact]
    public async Task Remove_on_an_umbra_that_cannot_be_driven_keeps_the_record()
    {
        var umbra = new FakeUmbra { PrepareResult = UmbraFailure.UnknownUmbra };
        var record = new UmbraSetupRecord(true, true, ["ours"]);
        var result = await UmbraSetupRunner.Remove(umbra, record, _ => { }, _ => { });

        Assert.Equal(UmbraFailure.UnknownUmbra, result.Failure);
        Assert.Same(record, result.Left);
    }

    [Fact]
    public async Task A_failed_restart_after_removing_still_clears_the_saved_changes()
    {
        // The changes are saved in Umbra by then; Umbra applies them when it next starts.
        var umbra = new FakeUmbra { CustomPluginsOn = true, Listed = true, AddonLoaded = true, RestartResult = UmbraFailure.TimedOut };
        var result = await UmbraSetupRunner.Remove(umbra, new UmbraSetupRecord(false, true, []), _ => { }, _ => { });

        Assert.Equal(UmbraFailure.TimedOut, result.Failure);
        Assert.True(result.Left.IsEmpty);
        Assert.False(umbra.Listed);
    }

    /// <summary>
    /// Umbra as the runner sees it: custom plugins, the add-on's repository entry, whether the add-on is loaded (only a
    /// restart loads or unloads it, as Umbra's own), and the widgets on the active toolbar profile, which Umbra shows
    /// only while the add-on is loaded.
    /// </summary>
    private sealed class FakeUmbra : IUmbraControl
    {
        private int next;
        private bool undoing;

        public bool CustomPluginsOn { get; set; }

        public bool Listed { get; set; }

        public bool AddonLoaded { get; set; }

        public int OtherAddons { get; set; }

        public List<(string Id, string Widget)> Widgets { get; } = [];

        public UmbraFailure PrepareResult { get; init; }

        public UmbraFailure AddResult { get; init; }

        public bool AddListsAnyway { get; init; }

        public UmbraFailure RestartResult { get; init; }

        public bool RestartLoads { get; init; } = true;

        public bool PlaceFails { get; init; }

        /// <summary>A call that throws while adding.</summary>
        public string? ThrowOn { get; init; }

        /// <summary>A call that throws while putting back.</summary>
        public string? ThrowOnUndo { get; init; }

        public List<string> Calls { get; } = [];

        public Task<UmbraFailure> Prepare()
        {
            Call("Prepare");
            return Task.FromResult(PrepareResult);
        }

        public Task<UmbraLook> Look()
        {
            if (Calls.Count == 0 || Calls[^1] != "Look")
            {
                Calls.Add("Look");
            }

            var placed = AddonLoaded && Widgets.Any(w => w.Widget == UmbraAddon.WidgetId);
            return Task.FromResult(new UmbraLook(CustomPluginsOn, Listed, AddonLoaded, placed, OtherAddons));
        }

        public Task SetCustomPlugins(bool on)
        {
            if (!on)
            {
                undoing = true;
            }

            Call($"SetCustomPlugins({on})");
            CustomPluginsOn = on;
            return Task.CompletedTask;
        }

        public Task<UmbraFailure> AddRepository()
        {
            Call("AddRepository");
            if (AddResult != UmbraFailure.None)
            {
                Listed = AddListsAnyway;
                return Task.FromResult(AddResult);
            }

            Listed = true;
            return Task.FromResult(UmbraFailure.None);
        }

        public Task<int> RemoveRepository()
        {
            undoing = true;
            Call("RemoveRepository");
            var was = Listed;
            Listed = false;
            return Task.FromResult(was ? 1 : 0);
        }

        public Task<UmbraFailure> Restart()
        {
            Call("Restart");
            if (RestartResult != UmbraFailure.None)
            {
                return Task.FromResult(RestartResult);
            }

            AddonLoaded = Listed && CustomPluginsOn && RestartLoads;
            return Task.FromResult(UmbraFailure.None);
        }

        public Task<string?> PlaceWidget()
        {
            Call("PlaceWidget");
            if (PlaceFails || !AddonLoaded)
            {
                return Task.FromResult<string?>(null);
            }

            var id = $"w{next++}";
            Widgets.Add((id, UmbraAddon.WidgetId));
            return Task.FromResult<string?>(id);
        }

        public Task<bool> RemoveWidget(string widgetId)
        {
            undoing = true;
            Call("RemoveWidget");
            return Task.FromResult(Widgets.RemoveAll(w => w.Id == widgetId) > 0);
        }

        private void Call(string name)
        {
            Calls.Add(name);
            var throwing = undoing ? ThrowOnUndo : ThrowOn;
            if (throwing is not null && name.StartsWith(throwing, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Umbra threw in {name}");
            }
        }
    }
}
