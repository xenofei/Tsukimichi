using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Runtime;

public sealed class LoginReadinessTests
{
    private static readonly DateTime T0 = new(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc);

    /// <summary>What the client hands out on the first tick after login: a loaded player, a content id, nothing else.</summary>
    private static CharacterSnapshot Unsettled() => Fixture.Snapshot() with { CompletedBits = new byte[8192] };

    [Fact]
    public void All_zero_mask_with_empty_journal_looks_empty()
    {
        Assert.True(LoginReadiness.LooksEmpty(Unsettled()));
        Assert.False(LoginReadiness.LooksEmpty(Fixture.Snapshot(Fixture.A)));
        Assert.False(LoginReadiness.LooksEmpty(Unsettled() with { Accepted = [Fixture.Accepted(Fixture.B)] }));
        Assert.False(LoginReadiness.LooksEmpty(Unsettled() with { DailyDone = new Dictionary<ushort, byte> { [7] = 1 } }));
    }

    [Fact]
    public void First_pass_with_all_zero_mask_and_empty_journal_is_not_ready()
    {
        var readiness = new LoginReadiness();

        Assert.Equal(LoginVerdict.NotReady, readiness.Check(Unsettled(), stored: null, T0));
        Assert.Equal(T0, readiness.WaitingSinceUtc);

        // A stored snapshot with real data says the character is not empty: keep waiting.
        Assert.Equal(LoginVerdict.NotReady, readiness.Check(Unsettled(), Fixture.Snapshot(Fixture.A), T0.AddSeconds(1)));
    }

    [Fact]
    public void Settled_capture_is_ready_and_ends_the_wait()
    {
        var readiness = new LoginReadiness();
        readiness.Check(Unsettled(), null, T0);

        Assert.Equal(LoginVerdict.Ready, readiness.Check(Fixture.Snapshot(Fixture.A), null, T0.AddSeconds(2)));
        Assert.Null(readiness.WaitingSinceUtc);
    }

    [Fact]
    public void Empty_capture_of_a_character_stored_as_empty_is_ready()
    {
        var readiness = new LoginReadiness();

        Assert.Equal(LoginVerdict.Ready, readiness.Check(Unsettled(), Unsettled(), T0));
        Assert.Null(readiness.WaitingSinceUtc);
    }

    [Fact]
    public void Empty_capture_is_committed_after_the_maximum_wait()
    {
        var readiness = new LoginReadiness(TimeSpan.FromSeconds(5));

        Assert.Equal(LoginVerdict.NotReady, readiness.Check(Unsettled(), null, T0));
        Assert.Equal(LoginVerdict.NotReady, readiness.Check(Unsettled(), null, T0.AddSeconds(4.9)));
        Assert.Equal(LoginVerdict.ReadyAfterTimeout, readiness.Check(Unsettled(), null, T0.AddSeconds(5)));
        Assert.Null(readiness.WaitingSinceUtc);

        // The next login starts a fresh wait.
        Assert.Equal(LoginVerdict.NotReady, readiness.Check(Unsettled(), null, T0.AddSeconds(6)));
        Assert.Equal(T0.AddSeconds(6), readiness.WaitingSinceUtc);
    }

    [Fact]
    public void Reset_forgets_a_wait_in_progress()
    {
        var readiness = new LoginReadiness(TimeSpan.FromSeconds(5));
        readiness.Check(Unsettled(), null, T0);

        readiness.Reset();

        Assert.Null(readiness.WaitingSinceUtc);
        Assert.Equal(LoginVerdict.NotReady, readiness.Check(Unsettled(), null, T0.AddSeconds(10)));
    }

    [Fact]
    public void Reconcile_is_never_reached_by_an_unsettled_capture()
    {
        // The poller checks readiness before AcceptedSince.Reconcile; an unsettled capture would otherwise drop
        // every stored accepted time. This pins the contract the two share.
        var since = new Dictionary<ushort, DateTime> { [QuestRecord.ToQuestId(Fixture.A)] = T0 };
        var capture = Unsettled();

        if (new LoginReadiness().Check(capture, Fixture.Snapshot(Fixture.A), T0) == LoginVerdict.Ready)
        {
            AcceptedSince.Reconcile(since, capture, T0);
        }

        Assert.Single(since);
    }
}
