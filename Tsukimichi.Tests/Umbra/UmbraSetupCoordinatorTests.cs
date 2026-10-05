using Tsukimichi.Core.Umbra;

namespace Tsukimichi.Tests.Umbra;

/// <summary>
/// The setup's state as the card and Settings see it (<see cref="UmbraSetupCoordinator"/>): the wait for the add-on's
/// answer and its timeout, the saved book merging each run with what was there, the outcome after a failure, one run at
/// a time, a restart timeout left alone, and records kept and undone per Umbra profile.
/// </summary>
public sealed class UmbraSetupCoordinatorTests
{
    private static (UmbraSetupCoordinator Coordinator, List<UmbraSetupBook> Saved) Make(FakeUmbra umbra, UmbraSetupBook? book = null)
    {
        var saved = new List<UmbraSetupBook>();
        return (new UmbraSetupCoordinator(umbra, book ?? UmbraSetupBook.Empty, saved.Add, _ => { }), saved);
    }

    private static async Task Run(Task? run)
    {
        Assert.NotNull(run);
        await run;
    }

    [Fact]
    public async Task After_every_step_it_waits_for_the_addon_and_times_out()
    {
        var (coordinator, _) = Make(new FakeUmbra());
        await coordinator.RefreshPreview();
        await Run(coordinator.AddToUmbra());
        Assert.Equal(UmbraSetupActivity.Waiting, coordinator.View.Activity);

        coordinator.Tick(false, 100);
        coordinator.Tick(false, 100 + UmbraAddonSetup.HelloTimeoutSeconds - 0.5);
        Assert.Equal(UmbraSetupActivity.Waiting, coordinator.View.Activity);
        Assert.Null(coordinator.View.Outcome);

        coordinator.Tick(false, 100 + UmbraAddonSetup.HelloTimeoutSeconds);
        Assert.Equal(UmbraSetupActivity.Idle, coordinator.View.Activity);
        Assert.Equal(UmbraSetupOutcome.NotConfirmed, coordinator.View.Outcome);
    }

    [Fact]
    public async Task The_addons_answer_ends_the_wait_as_added()
    {
        var (coordinator, _) = Make(new FakeUmbra());
        await Run(coordinator.AddToUmbra());
        coordinator.Tick(false, 5);
        coordinator.Tick(true, 6);
        Assert.Equal(UmbraSetupOutcome.Added, coordinator.View.Outcome);
        Assert.False(coordinator.Busy);
    }

    [Fact]
    public async Task Each_change_is_saved_merged_with_what_the_profile_already_held()
    {
        // An earlier run on "Main" turned custom plugins on (two add-ons then) and placed a widget the player still has.
        var earlier = new UmbraSetupRecord(true, false, ["old"]) { Profile = "Main", OtherAddonsAtTurnOn = 2 };
        var umbra = new FakeUmbra { Profile = "Main", CustomPluginsOn = true };
        var (coordinator, saved) = Make(umbra, UmbraSetupBook.Empty.With(earlier));

        await Run(coordinator.AddToUmbra());

        var record = coordinator.Book.For("Main");
        Assert.True(record.TurnedOnCustomPlugins);
        Assert.True(record.AddedRepository);
        Assert.Equal(2, record.OtherAddonsAtTurnOn);
        Assert.Equal(2, record.WidgetIds.Count);
        Assert.Equal("old", record.WidgetIds[0]);
        Assert.Same(coordinator.Book, saved[^1]);
        Assert.Equal(2, saved.Count);
    }

    [Theory]
    [InlineData(false, UmbraSetupOutcome.Failed)]
    [InlineData(true, UmbraSetupOutcome.FailedPartly)]
    public async Task A_failure_ends_with_its_outcome_and_reason(bool undoFails, UmbraSetupOutcome expected)
    {
        var umbra = new FakeUmbra { RestartLoads = false, ThrowOnUndo = undoFails ? "RemoveRepository" : null };
        var (coordinator, _) = Make(umbra);

        await Run(coordinator.AddToUmbra());

        Assert.Equal(UmbraSetupActivity.Idle, coordinator.View.Activity);
        Assert.Equal(expected, coordinator.View.Outcome);
        Assert.Equal(UmbraFailure.NotLoaded, coordinator.View.Failure);
        Assert.Equal(undoFails, !coordinator.Book.IsEmpty);
    }

    [Fact]
    public async Task Only_one_run_at_a_time()
    {
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var umbra = new FakeUmbra { CustomPluginsOn = true, Listed = true, HoldRestart = hold };
        var (coordinator, _) = Make(umbra, UmbraSetupBook.Empty.With(new UmbraSetupRecord(false, false, ["w"]) { Profile = umbra.Profile }));

        var run = coordinator.AddToUmbra();
        Assert.NotNull(run);
        Assert.True(coordinator.Busy);
        Assert.Null(coordinator.AddToUmbra());
        Assert.Null(coordinator.RemoveFromUmbra());

        hold.SetResult();
        await run;
        Assert.Equal(UmbraSetupActivity.Waiting, coordinator.View.Activity);
        Assert.Null(coordinator.AddToUmbra());
    }

    [Fact]
    public async Task A_restart_that_timed_out_is_left_alone_and_stays_removable()
    {
        var umbra = new FakeUmbra { RestartResult = UmbraFailure.RestartTimedOut };
        var (coordinator, _) = Make(umbra);

        await Run(coordinator.AddToUmbra());

        Assert.Equal(UmbraSetupOutcome.FailedPartly, coordinator.View.Outcome);
        Assert.Equal(UmbraFailure.RestartTimedOut, coordinator.View.Failure);
        Assert.DoesNotContain("SetCustomPlugins(False)", umbra.Calls);
        Assert.DoesNotContain("RemoveRepository", umbra.Calls);
        var record = coordinator.Book.For(umbra.Profile);
        Assert.True(record.TurnedOnCustomPlugins);
        Assert.True(record.AddedRepository);

        // Once Umbra is back, Remove from Umbra undoes it.
        umbra.RestartResult = UmbraFailure.None;
        await Run(coordinator.RemoveFromUmbra());
        Assert.True(coordinator.Book.IsEmpty);
        Assert.False(umbra.CustomPluginsOn);
        Assert.False(umbra.Listed);
    }

    [Fact]
    public async Task Records_are_kept_and_removed_per_umbra_profile()
    {
        // Character one, Umbra profile "Main": Tsukimichi sets everything up.
        var umbra = new FakeUmbra { Profile = "Main", CharacterId = 1 };
        var (coordinator, _) = Make(umbra);
        await Run(coordinator.AddToUmbra());
        coordinator.Tick(true, 1);
        Assert.Equal(["Main"], coordinator.Book.Records.Select(r => r.Profile));
        Assert.Equal(1UL, coordinator.Book.For("Main").CharacterId);

        // Character two, profile "Alt", where the player set up the add-on themselves: Remove changes nothing there.
        umbra.Profile = "Alt";
        umbra.CharacterId = 2;
        var calls = umbra.Calls.Count;
        await Run(coordinator.RemoveFromUmbra());
        Assert.True(coordinator.View.RemoveDone);
        Assert.Equal(UmbraFailure.OtherProfile, coordinator.View.RemoveFailure);
        Assert.Equal(["Open", "Look"], umbra.Calls.Skip(calls));
        Assert.True(umbra.CustomPluginsOn);
        Assert.True(umbra.Listed);
        Assert.Equal(["Main"], coordinator.Book.OtherProfiles("Alt"));

        // Back on "Main", it is undone and the book empties.
        umbra.Profile = "Main";
        await Run(coordinator.RemoveFromUmbra());
        Assert.Equal(UmbraFailure.None, coordinator.View.RemoveFailure);
        Assert.True(coordinator.Book.IsEmpty);
        Assert.False(umbra.CustomPluginsOn);
    }

    [Fact]
    public async Task An_add_on_a_second_profile_gets_its_own_record()
    {
        var umbra = new FakeUmbra { Profile = "Main" };
        var (coordinator, _) = Make(umbra, UmbraSetupBook.Empty.With(new UmbraSetupRecord(true, true, ["m"]) { Profile = "Main" }));

        umbra.Profile = "Alt";
        await Run(coordinator.AddToUmbra());

        Assert.Equal(2, coordinator.Book.Records.Count);
        Assert.Equal(["m"], coordinator.Book.For("Main").WidgetIds);
        Assert.Single(coordinator.Book.For("Alt").WidgetIds);
    }

    [Fact]
    public async Task The_preview_names_the_plan_and_the_profile_and_changes_nothing()
    {
        var umbra = new FakeUmbra { Profile = "Main", CustomPluginsOn = true };
        var (coordinator, saved) = Make(umbra);

        await coordinator.RefreshPreview();

        Assert.True(coordinator.Preview.Ready);
        Assert.Equal("Main", coordinator.Preview.Profile);
        Assert.Equal([UmbraSetupStep.AddRepository, UmbraSetupStep.RestartUmbra, UmbraSetupStep.PlaceWidget], coordinator.Preview.Plan);
        Assert.Equal(["Open", "Look"], umbra.Calls);
        Assert.Empty(saved);
    }

    [Fact]
    public async Task Nothing_starts_once_stopped_or_with_nothing_to_remove()
    {
        var (coordinator, _) = Make(new FakeUmbra());
        Assert.Null(coordinator.RemoveFromUmbra());
        coordinator.Stop();
        Assert.Null(coordinator.AddToUmbra());
        await coordinator.RefreshPreview();
        Assert.False(coordinator.Preview.Ready);
    }
}
