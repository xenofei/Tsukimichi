using System.Numerics;
using Tsukimichi.Core.Companions;

namespace Tsukimichi.Tests.Companions;

/// <summary>
/// "Needs you" alerts (plan v7, 1.18.0, A5): <see cref="NeedsYouWatch"/> raises death on the fall, stuck once per
/// stall, a duty pop and a tell per event; only while a hand-off runs, only for kinds left on, once a minute per kind,
/// and with the sound at most every ten seconds.
/// </summary>
public class NeedsYouWatchTests
{
    private static readonly Vector3 Here = new(10, 0, 10);

    private static NeedsYouFrame Frame(double now, bool handOff = true, bool dead = false, bool moving = false, Vector3? position = null) =>
        new(handOff, dead, moving, position ?? Here, now);

    [Fact]
    public void Death_is_raised_when_the_character_falls_not_while_it_lies()
    {
        var watch = new NeedsYouWatch();
        Assert.Equal(NeedsYouKind.None, watch.Tick(Frame(0), NeedsYouKind.All));
        Assert.Equal(NeedsYouKind.Death, watch.Tick(Frame(1, dead: true), NeedsYouKind.All));
        Assert.Equal(NeedsYouKind.None, watch.Tick(Frame(2, dead: true), NeedsYouKind.All));
        Assert.Equal(NeedsYouKind.None, watch.Tick(Frame(30, dead: false), NeedsYouKind.All));
    }

    [Fact]
    public void A_death_loop_alerts_once_a_minute()
    {
        var watch = new NeedsYouWatch();
        Assert.Equal(NeedsYouKind.Death, watch.Tick(Frame(0, dead: true), NeedsYouKind.All));
        watch.Tick(Frame(10), NeedsYouKind.All);
        Assert.Equal(NeedsYouKind.None, watch.Tick(Frame(20, dead: true), NeedsYouKind.All));
        watch.Tick(Frame(40), NeedsYouKind.All);
        Assert.Equal(NeedsYouKind.Death, watch.Tick(Frame(NeedsYouWatch.RepeatSeconds + 1, dead: true), NeedsYouKind.All));
    }

    [Fact]
    public void Nothing_is_raised_without_a_hand_off()
    {
        var watch = new NeedsYouWatch();
        Assert.Equal(NeedsYouKind.None, watch.Tick(Frame(0, handOff: false, dead: true), NeedsYouKind.All));

        // Dead before the hand-off began: no fall is seen.
        Assert.Equal(NeedsYouKind.None, watch.Tick(Frame(1, dead: true), NeedsYouKind.All));
        Assert.False(watch.Event(NeedsYouKind.Tell, handOff: false, NeedsYouKind.All, 2));
        Assert.False(watch.Event(NeedsYouKind.DutyPop, handOff: false, NeedsYouKind.All, 2));
    }

    [Fact]
    public void A_kind_turned_off_is_not_raised()
    {
        var watch = new NeedsYouWatch();
        var allButDeath = NeedsYouKind.All & ~NeedsYouKind.Death;
        Assert.Equal(NeedsYouKind.None, watch.Tick(Frame(0, dead: true), allButDeath));
        Assert.False(watch.Event(NeedsYouKind.Tell, handOff: true, NeedsYouKind.DutyPop, 1));
        Assert.True(watch.Event(NeedsYouKind.DutyPop, handOff: true, NeedsYouKind.DutyPop, 1));
    }

    [Fact]
    public void Stuck_is_raised_once_per_stall_after_the_threshold()
    {
        var watch = new NeedsYouWatch();
        var nudge = Here + new Vector3(NeedsYouWatch.StuckMove / 2, 0, 0);
        Assert.Equal(NeedsYouKind.None, watch.Tick(Frame(0, moving: true), NeedsYouKind.All));

        // Inching less than StuckMove does not count as progress.
        Assert.Equal(NeedsYouKind.None, watch.Tick(Frame(20, moving: true, position: nudge), NeedsYouKind.All));
        Assert.Equal(NeedsYouKind.Stuck, watch.Tick(Frame(NeedsYouWatch.StuckSeconds, moving: true, position: nudge), NeedsYouKind.All));
        Assert.Equal(NeedsYouKind.None, watch.Tick(Frame(NeedsYouWatch.StuckSeconds + 5, moving: true), NeedsYouKind.All));
    }

    [Fact]
    public void Moving_or_stopping_rearms_the_stall_clock()
    {
        var watch = new NeedsYouWatch();
        var away = Here + new Vector3(0, 0, NeedsYouWatch.StuckMove + 1);
        watch.Tick(Frame(0, moving: true), NeedsYouKind.All);
        Assert.Equal(NeedsYouKind.None, watch.Tick(Frame(25, moving: true, position: away), NeedsYouKind.All));
        Assert.Equal(NeedsYouKind.None, watch.Tick(Frame(50, moving: true, position: away), NeedsYouKind.All));

        // vnavmesh stops (a cutscene, a talk, the hand-off ending): the next stall starts from scratch.
        watch.Tick(Frame(54, moving: false, position: away), NeedsYouKind.All);
        Assert.Equal(NeedsYouKind.None, watch.Tick(Frame(55, moving: true, position: away), NeedsYouKind.All));
        Assert.Equal(NeedsYouKind.None, watch.Tick(Frame(80, moving: true, position: away), NeedsYouKind.All));
        Assert.Equal(NeedsYouKind.Stuck, watch.Tick(Frame(85, moving: true, position: away), NeedsYouKind.All));
    }

    [Fact]
    public void A_stall_without_a_position_or_while_dead_is_not_stuck()
    {
        var watch = new NeedsYouWatch();
        watch.Tick(new NeedsYouFrame(true, false, true, null, 0), NeedsYouKind.All);
        Assert.Equal(NeedsYouKind.None, watch.Tick(new NeedsYouFrame(true, false, true, null, 60), NeedsYouKind.All));

        var lying = new NeedsYouWatch();
        lying.Tick(Frame(0, dead: true, moving: true), NeedsYouKind.Stuck);
        Assert.Equal(NeedsYouKind.None, lying.Tick(Frame(60, dead: true, moving: true), NeedsYouKind.Stuck));
    }

    [Fact]
    public void A_burst_of_tells_alerts_once_a_minute_per_kind()
    {
        var watch = new NeedsYouWatch();
        Assert.True(watch.Event(NeedsYouKind.Tell, handOff: true, NeedsYouKind.All, 0));
        Assert.False(watch.Event(NeedsYouKind.Tell, handOff: true, NeedsYouKind.All, 5));
        Assert.False(watch.Event(NeedsYouKind.Tell, handOff: true, NeedsYouKind.All, 59));

        // Another kind is not held back by the tells.
        Assert.True(watch.Event(NeedsYouKind.DutyPop, handOff: true, NeedsYouKind.All, 6));
        Assert.True(watch.Event(NeedsYouKind.Tell, handOff: true, NeedsYouKind.All, NeedsYouWatch.RepeatSeconds));
    }

    [Theory]
    [InlineData(NeedsYouKind.Death)]
    [InlineData(NeedsYouKind.Stuck)]
    [InlineData(NeedsYouKind.None)]
    public void Only_pops_and_tells_are_events(NeedsYouKind kind) =>
        Assert.False(new NeedsYouWatch().Event(kind, handOff: true, NeedsYouKind.All, 0));

    [Fact]
    public void The_sound_keeps_a_gap()
    {
        var watch = new NeedsYouWatch();
        Assert.True(watch.TakeSound(0));
        Assert.False(watch.TakeSound(NeedsYouWatch.SoundGapSeconds - 1));
        Assert.True(watch.TakeSound(NeedsYouWatch.SoundGapSeconds));
        Assert.False(watch.TakeSound(double.NaN));
    }
}
