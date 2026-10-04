using Tsukimichi.Core.Companions;

namespace Tsukimichi.Tests.Companions;

/// <summary>
/// The duty guard (plan v7, 1.18.0, A3): <see cref="DutyGuard.Decide"/> stops or warns at a Questionable duty step
/// whose duty has no Duty Support or Trust, narrows a quest naming several duties to those not cleared, and says when
/// it cannot tell; <see cref="DutyGuardWatch"/> acts once per step.
/// </summary>
public class DutyGuardTests
{
    private static readonly DutyRunInfo DeadEnds = new(792, 82, 1000, DutyRunInfo.Dungeons, "the Dead Ends", OffersDutySupport: true, OffersTrust: true);
    private static readonly DutyRunInfo FinalDay = new(796, 20090, 1001, DutyRunInfo.Trials, "the Final Day", false, false);
    private static readonly DutyRunInfo TrustOnly = new(900, 95, 1200, DutyRunInfo.Dungeons, "Trust only", OffersDutySupport: false, OffersTrust: true);
    private static readonly DutyRunInfo BrayfloxHard = new(20, 20, 362, DutyRunInfo.Dungeons, "Brayflox's Longstop (Hard)", false, false);

    private const string Duty = DutyGuard.DutyInteraction;

    [Fact]
    public void Duty_Support_or_Trust_makes_a_duty_NPC_runnable()
    {
        Assert.True(DutyGuard.NpcRunnable(DeadEnds));
        Assert.True(DutyGuard.NpcRunnable(TrustOnly));
        Assert.False(DutyGuard.NpcRunnable(FinalDay));
    }

    [Fact]
    public void A_duty_with_other_players_is_stopped_or_warned_per_the_mode()
    {
        var stop = DutyGuard.Decide(DutyGuardMode.Stop, Duty, [BrayfloxHard]);
        Assert.Equal(DutyGuardAction.Stop, stop.Action);
        Assert.Same(BrayfloxHard, stop.Duty);
        Assert.True(stop.Certain);

        var warn = DutyGuard.Decide(DutyGuardMode.Warn, Duty, [BrayfloxHard]);
        Assert.Equal(DutyGuardAction.Warn, warn.Action);
        Assert.True(warn.Certain);

        Assert.Equal(DutyGuardVerdict.None, DutyGuard.Decide(DutyGuardMode.Nothing, Duty, [BrayfloxHard]));
    }

    [Fact]
    public void An_NPC_runnable_duty_is_left_alone()
    {
        Assert.Equal(DutyGuardVerdict.None, DutyGuard.Decide(DutyGuardMode.Stop, Duty, [DeadEnds]));
        Assert.Equal(DutyGuardVerdict.None, DutyGuard.Decide(DutyGuardMode.Stop, Duty, [TrustOnly, DeadEnds]));
    }

    [Theory]
    [InlineData("SinglePlayerDuty")]
    [InlineData("Interact")]
    [InlineData("duty")]
    [InlineData("")]
    [InlineData(null)]
    public void Only_Questionables_Duty_step_counts(string? interaction)
    {
        // SinglePlayerDuty is a solo quest battle; the name is matched exactly, as Questionable's enum spells it.
        Assert.Equal(DutyGuardVerdict.None, DutyGuard.Decide(DutyGuardMode.Stop, interaction, [BrayfloxHard]));
    }

    [Fact]
    public void A_duty_step_naming_no_known_duty_is_unsure_whatever_the_mode()
    {
        Assert.Equal(new DutyGuardVerdict(DutyGuardAction.Unsure, null, false), DutyGuard.Decide(DutyGuardMode.Stop, Duty, []));
        Assert.Equal(DutyGuardAction.Unsure, DutyGuard.Decide(DutyGuardMode.Warn, Duty, []).Action);
        Assert.Equal(DutyGuardVerdict.None, DutyGuard.Decide(DutyGuardMode.Nothing, Duty, []));
    }

    [Fact]
    public void A_quest_naming_a_dungeon_and_a_trial_only_warns_even_when_set_to_stop()
    {
        // Endwalker's finale names the Dead Ends (Duty Support) and the Final Day (other players); nothing cleared yet.
        // The step may be the dungeon the NPCs run: Questionable is never stopped before it, only warned about.
        var verdict = DutyGuard.Decide(DutyGuardMode.Stop, Duty, [DeadEnds, FinalDay], static _ => false);
        Assert.Equal(DutyGuardAction.Warn, verdict.Action);
        Assert.Same(FinalDay, verdict.Duty);
        Assert.False(verdict.Certain);

        // Unknown clears read the same, and Warn mode warns alike.
        Assert.Equal(verdict, DutyGuard.Decide(DutyGuardMode.Stop, Duty, [DeadEnds, FinalDay]));
        Assert.Equal(verdict, DutyGuard.Decide(DutyGuardMode.Warn, Duty, [DeadEnds, FinalDay]));
    }

    [Theory]
    [InlineData(true, null, null, true)] // the step data cannot be read at all
    [InlineData(true, 2, "Duty", true)]
    [InlineData(false, 2, null, true)] // a step read without its kind: renamed or dropped
    [InlineData(false, 2, " ", true)]
    [InlineData(false, 2, "Interact", false)]
    [InlineData(false, null, null, false)] // between steps: nothing to read yet
    public void The_guard_knows_when_it_cannot_see_the_step(bool unreadable, int? step, string? interaction, bool blind) =>
        Assert.Equal(blind, DutyGuard.Blind(unreadable, step, interaction));

    [Fact]
    public void Cleared_duties_narrow_the_step_to_the_one_ahead()
    {
        // The dungeon is behind: the step is the trial, for certain.
        var trialAhead = DutyGuard.Decide(DutyGuardMode.Stop, Duty, [DeadEnds, FinalDay], duty => duty == DeadEnds);
        Assert.Equal(new DutyGuardVerdict(DutyGuardAction.Stop, FinalDay, true), trialAhead);

        // The trial is behind (cleared before): the step is the dungeon, which has Duty Support.
        Assert.Equal(DutyGuardVerdict.None, DutyGuard.Decide(DutyGuardMode.Stop, Duty, [DeadEnds, FinalDay], duty => duty == FinalDay));

        // Both cleared (a repeat, New Game+): either may be next, as with nothing cleared.
        var both = DutyGuard.Decide(DutyGuardMode.Warn, Duty, [DeadEnds, FinalDay], static _ => true);
        Assert.Equal(new DutyGuardVerdict(DutyGuardAction.Warn, FinalDay, false), both);
    }

    [Fact]
    public void The_watch_acts_once_per_step_and_again_on_another()
    {
        var watch = new DutyGuardWatch();
        var asked = 0;
        IReadOnlyList<DutyRunInfo> Duties(uint row)
        {
            asked++;
            return row == 70000 ? [DeadEnds, FinalDay] : [BrayfloxHard];
        }

        Assert.Equal(DutyGuardAction.Stop, watch.Observe(DutyGuardMode.Stop, 65000, 2, 0, Duty, Duties).Action);

        // The same step again, every second while Questionable stays on it, or after the player starts it again.
        Assert.Equal(DutyGuardVerdict.None, watch.Observe(DutyGuardMode.Stop, 65000, 2, 0, Duty, Duties));
        Assert.Equal(DutyGuardVerdict.None, watch.Observe(DutyGuardMode.Stop, 65000, 2, 0, Duty, Duties));
        Assert.Equal(1, asked);

        // Another step, then a duty step of another quest.
        Assert.Equal(DutyGuardVerdict.None, watch.Observe(DutyGuardMode.Stop, 65000, 3, 0, "Interact", Duties));
        Assert.Equal(DutyGuardAction.Stop, watch.Observe(DutyGuardMode.Stop, 70000, 5, 1, Duty, Duties, duty => duty == DeadEnds).Action);
        Assert.Equal(2, asked);

        // The same quest's next duty step with nothing cleared: may be the dungeon, so a warning only.
        Assert.Equal(DutyGuardAction.Warn, watch.Observe(DutyGuardMode.Stop, 70000, 5, 2, Duty, Duties).Action);

        // Back on the first step later: it is new again.
        Assert.Equal(DutyGuardAction.Stop, watch.Observe(DutyGuardMode.Stop, 65000, 2, 0, Duty, Duties).Action);
    }

    [Fact]
    public void The_watch_needs_the_step_and_reset_forgets_it()
    {
        var watch = new DutyGuardWatch();
        IReadOnlyList<DutyRunInfo> Duties(uint _) => [BrayfloxHard];

        // No step data (the fork, a broken gate): nothing to go on.
        Assert.Equal(DutyGuardVerdict.None, watch.Observe(DutyGuardMode.Stop, 65000, null, null, Duty, Duties));
        Assert.Equal(DutyGuardVerdict.None, watch.Observe(DutyGuardMode.Stop, null, 1, 0, Duty, Duties));

        Assert.Equal(DutyGuardAction.Warn, watch.Observe(DutyGuardMode.Warn, 65000, 1, 0, Duty, Duties).Action);
        watch.Reset();
        Assert.Equal(DutyGuardAction.Warn, watch.Observe(DutyGuardMode.Warn, 65000, 1, 0, Duty, Duties).Action);
    }

    [Fact]
    public void Do_nothing_never_asks_for_the_quests_duties()
    {
        var watch = new DutyGuardWatch();
        Assert.Equal(DutyGuardVerdict.None, watch.Observe(DutyGuardMode.Nothing, 65000, 1, 0, Duty, static _ => throw new InvalidOperationException("asked")));
    }
}
