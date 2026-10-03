using System.Numerics;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Todo;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The 1.13.0 motion (feature plan v6 U8, M1–M3): the new easing helpers, the moments' tokens, the Ready cue, the
/// panels beside game windows (presence, size, sticky side) and the calmer Todo overlay (hiding rules, completion beat).
/// </summary>
public sealed class MotionMomentsTests
{
    private const float Frame = 1f / 60f;

    [Fact]
    public void Ease_in_out_starts_and_lands_softly_and_is_symmetric()
    {
        Assert.Equal(0f, MotionMath.EaseInOutCubic(0f));
        Assert.Equal(1f, MotionMath.EaseInOutCubic(1f));
        Assert.Equal(0.5f, MotionMath.EaseInOutCubic(0.5f), 5);
        Assert.Equal(1f - MotionMath.EaseInOutCubic(0.2f), MotionMath.EaseInOutCubic(0.8f), 5);
        Assert.True(MotionMath.EaseInOutCubic(0.1f) < 0.1f);
        Assert.Equal(1f, MotionMath.EaseInOutCubic(float.NaN));
        Assert.Equal(0f, MotionMath.EaseInOutCubic(-3f));
    }

    [Fact]
    public void A_hover_wash_comes_in_quicker_than_it_leaves()
    {
        var rising = MotionMath.ApproachAsym(0f, 1f, MotionMath.HoverRate, MotionMath.HoverOutRate, Frame);
        var falling = 1f - MotionMath.ApproachAsym(1f, 0f, MotionMath.HoverRate, MotionMath.HoverOutRate, Frame);

        Assert.True(rising > falling);
        Assert.Equal(MotionMath.Approach(0f, 1f, MotionMath.HoverRate, Frame), rising);
    }

    [Fact]
    public void The_store_eases_asymmetrically_and_starts_new_keys_at_their_target()
    {
        var store = new MotionStore();
        Assert.Equal(1f, store.LerpAsym(1, 1f, MotionMath.HoverRate, MotionMath.HoverOutRate, 0d, Frame, animate: true));

        var down = store.LerpAsym(1, 0f, MotionMath.HoverRate, MotionMath.HoverOutRate, Frame, Frame, animate: true);
        Assert.InRange(down, 0.5f, 0.99f);

        Assert.Equal(0f, store.LerpAsym(1, 0f, MotionMath.HoverRate, MotionMath.HoverOutRate, 2 * Frame, Frame, animate: false));
    }

    [Fact]
    public void A_change_plays_once_but_never_on_first_sight()
    {
        var store = new MotionStore();
        Assert.Equal(-1f, store.Changed(9, 1u, 0.2f, 0d, animate: true));
        Assert.Equal(-1f, store.Changed(9, 1u, 0.2f, 0.1d, animate: true));

        Assert.Equal(0f, store.Changed(9, 2u, 0.2f, 1d, animate: true));
        Assert.Equal(0.5f, store.Changed(9, 2u, 0.2f, 1.1d, animate: true), 4);
        Assert.Equal(-1f, store.Changed(9, 2u, 0.2f, 1.25d, animate: true));
        Assert.Equal(-1f, store.Changed(9, 2u, 0.2f, 1.3d, animate: true));
    }

    [Fact]
    public void A_change_the_caller_does_not_allow_is_noted_without_playing()
    {
        var store = new MotionStore();
        store.Changed(9, 1u, 0.2f, 0d, animate: true);

        Assert.Equal(-1f, store.Changed(9, 2u, 0.2f, 1d, animate: true, start: false));
        Assert.Equal(-1f, store.Changed(9, 2u, 0.2f, 1.05d, animate: true));
        Assert.Equal(-1f, store.Changed(9, 3u, 0.2f, 2d, animate: false));
        Assert.Equal(-1f, store.Changed(9, 3u, 0.2f, 2.05d, animate: true));
    }

    [Fact]
    public void Every_moment_is_a_one_shot_under_a_second_at_a_soft_gold()
    {
        foreach (var seconds in MotionTokens.Moments)
        {
            Assert.InRange(seconds, 0.2f, 1f);
        }

        Assert.InRange(MotionTokens.MomentPeak, 0.2f, 0.6f);
        for (var p = 0f; p < 1f; p += 0.05f)
        {
            Assert.InRange(MotionTokens.MomentAlpha(p), 0f, MotionTokens.MomentPeak);
        }

        Assert.Equal(MotionTokens.MomentPeak, MotionTokens.MomentAlpha(0f));
        Assert.Equal(0f, MotionTokens.MomentAlpha(1f));
        Assert.Equal(0f, MotionTokens.MomentAlpha(-1f));
        Assert.Equal(0f, MotionTokens.MomentAlpha(float.NaN));
    }

    [Fact]
    public void A_swap_dips_the_content_and_brings_it_back()
    {
        Assert.Equal(1f, MotionTokens.SwapAlpha(0f));
        Assert.Equal(MotionTokens.SwapDip, MotionTokens.SwapAlpha(0.5f), 5);
        Assert.Equal(1f, MotionTokens.SwapAlpha(1f));
        Assert.Equal(1f, MotionTokens.SwapAlpha(-1f));
        Assert.True(MotionTokens.SwapAlpha(0.25f) < 1f);
        Assert.True(MotionTokens.Leave <= MotionTokens.Rise);
    }

    private static readonly DateTime T0 = new(2026, 10, 3, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_quest_becoming_ready_is_reported_beside_the_completions()
    {
        var cues = new CompletionCues();
        var done = new List<uint>();
        var ready = new List<uint>();
        var events = new List<QuestEvent> { new(QuestEventKind.NewlyAvailable, 1, T0) };
        Assert.Equal(0, cues.Take(7, events, shown: true, done, ready));
        Assert.Empty(ready);

        events.InsertRange(0, [new(QuestEventKind.Completed, 2, T0.AddSeconds(4)), new(QuestEventKind.NewlyAvailable, 3, T0.AddSeconds(4))]);
        Assert.Equal(1, cues.Take(7, events, shown: true, done, ready));
        Assert.Equal([2u], done);
        Assert.Equal([3u], ready);

        // Another character on screen (or combat): noted, never played later.
        events.Insert(0, new(QuestEventKind.NewlyAvailable, 4, T0.AddSeconds(8)));
        ready.Clear();
        Assert.Equal(0, cues.Take(7, events, shown: false, done, ready));
        Assert.Equal(0, cues.Take(7, events, shown: true, done, ready));
        Assert.Empty(ready);
    }

    private static readonly ScreenRect Screen = new(Vector2.Zero, new Vector2(1920, 1080));

    [Fact]
    public void A_panel_keeps_the_side_it_stands_on_while_it_fits_there()
    {
        // Room on both sides: the usual order would pick the right.
        var window = ScreenRect.FromSize(new Vector2(700, 200), new Vector2(400, 500));
        var panel = new Vector2(300, 120);

        Assert.True(BesidePlacement.TryPlace(window, panel, Screen, 6f, CardSide.Left, out var pos, out var side));
        Assert.Equal(CardSide.Left, side);
        Assert.Equal(new Vector2(700 - 6 - 300, 200), pos);

        // No room left any more: it goes where the usual order says.
        var wide = new Vector2(720, 120);
        Assert.True(BesidePlacement.TryPlace(window, wide, Screen, 6f, CardSide.Left, out _, out side));
        Assert.Equal(CardSide.Right, side);
    }

    [Fact]
    public void Without_a_preference_the_placement_is_unchanged()
    {
        var window = ScreenRect.FromSize(new Vector2(1100, 200), new Vector2(700, 500));
        var panel = new Vector2(300, 120);

        Assert.True(BesidePlacement.TryPlace(window, panel, Screen, 6f, null, out var withNull, out var sideNull));
        Assert.True(BesidePlacement.TryPlace(window, panel, Screen, 6f, out var classic, out var sideClassic));
        Assert.Equal(classic, withNull);
        Assert.Equal(sideClassic, sideNull);
        Assert.False(BesidePlacement.TryPlace(window, new Vector2(2000, 10), Screen, 6f, CardSide.Right, out _, out _));
    }

    [Fact]
    public void A_panel_is_measured_unseen_once_then_rises_in()
    {
        var presence = new PanelPresence();
        presence.Show(10d, changed: true);
        Assert.Equal(PanelPhase.Measuring, presence.Phase);
        Assert.Equal(0f, presence.Alpha(10d, animate: true));
        Assert.False(presence.Interactive);

        presence.Show(10.016d, changed: false);
        Assert.Equal(PanelPhase.Shown, presence.Phase);
        Assert.True(presence.Interactive);
        Assert.Equal(0f, presence.Alpha(10.016d, animate: true));
        Assert.Equal(1f, presence.Rise(10.016d, animate: true));

        var mid = 10.016d + (MotionTokens.Rise / 2d);
        Assert.InRange(presence.Alpha(mid, animate: true), 0.5f, 0.95f);
        Assert.InRange(presence.Rise(mid, animate: true), 0.05f, 0.5f);
        Assert.Equal(1f, presence.Alpha(11d, animate: true));
        Assert.Equal(0f, presence.Rise(11d, animate: true));

        // Under Reduce motion it is simply there.
        Assert.Equal(1f, presence.Alpha(10.02d, animate: false));
        Assert.Equal(0f, presence.Rise(10.02d, animate: false));
    }

    [Fact]
    public void A_new_subject_swaps_in_place_instead_of_blinking()
    {
        var presence = Risen(out var now);
        presence.Show(now, changed: true);

        Assert.Equal(PanelPhase.Shown, presence.Phase);
        Assert.Equal(1f, presence.Alpha(now, animate: true));
        Assert.Equal(MotionTokens.SwapDip, presence.ContentAlpha(now + (MotionTokens.Swap / 2d), animate: true), 3);
        Assert.Equal(1f, presence.ContentAlpha(now + MotionTokens.Swap, animate: true));
        Assert.Equal(1f, presence.ContentAlpha(now + (MotionTokens.Swap / 2d), animate: false));
    }

    [Fact]
    public void A_lost_subject_lingers_then_fades_and_a_new_one_takes_over_in_place()
    {
        var presence = Risen(out var now);
        Assert.True(presence.Lose(now, animate: true));
        Assert.Equal(PanelPhase.Lingering, presence.Phase);
        Assert.False(presence.Interactive);
        Assert.Equal(1f, presence.Alpha(now + (MotionTokens.Linger / 2d), animate: true));

        // Arrowing on: the next line has a panel again within the hold.
        Assert.True(presence.Lose(now + 0.1d, animate: true));
        presence.Show(now + 0.12d, changed: true);
        Assert.Equal(PanelPhase.Shown, presence.Phase);
        Assert.Equal(1f, presence.Alpha(now + 0.12d, animate: true));

        // Gone for good: it fades after the hold and is hidden once the fade is over.
        var lost = now + 1d;
        Assert.True(presence.Lose(lost, animate: true));
        var fading = lost + MotionTokens.Linger + (MotionTokens.Leave / 2d);
        Assert.True(presence.Lose(fading, animate: true));
        Assert.InRange(presence.Alpha(fading, animate: true), 0.01f, 0.5f);
        Assert.False(presence.Lose(lost + MotionTokens.Linger + MotionTokens.Leave + 0.01d, animate: true));
        Assert.Equal(PanelPhase.Hidden, presence.Phase);
        Assert.Equal(0f, presence.Alpha(lost + 2d, animate: true));
    }

    [Fact]
    public void Under_reduce_motion_a_lost_panel_goes_after_the_hold_without_a_fade()
    {
        var presence = Risen(out var now);
        Assert.True(presence.Lose(now, animate: false));
        Assert.Equal(1f, presence.Alpha(now + 0.1d, animate: false));
        Assert.False(presence.Lose(now + MotionTokens.Linger + 0.001d, animate: false));
    }

    [Fact]
    public void A_never_seen_panel_has_nothing_to_linger()
    {
        var presence = new PanelPresence();
        Assert.False(presence.Lose(1d, animate: true));
        presence.Show(2d, changed: true);
        Assert.False(presence.Lose(2.1d, animate: true));
        Assert.Equal(PanelPhase.Hidden, presence.Phase);
    }

    private static PanelPresence Risen(out double now)
    {
        var presence = new PanelPresence();
        presence.Show(0d, changed: true);
        presence.Show(0.016d, changed: false);
        now = 5d;
        return presence;
    }

    [Fact]
    public void A_panel_takes_its_first_size_at_once_and_eases_to_the_next()
    {
        var size = new PanelSize();
        Assert.False(size.Known);
        size.Measure(new Vector2(200.2f, 100f), measuring: true);
        Assert.True(size.Known);
        Assert.Equal(new Vector2(201f, 100f), size.Shown(Frame, animate: true));

        size.Measure(new Vector2(300f, 160f), measuring: false);
        var step = size.Shown(Frame, animate: true);
        Assert.InRange(step.X, 202f, 299f);
        Assert.InRange(step.Y, 101f, 159f);
        Assert.Equal(MathF.Round(step.X), step.X);

        for (var i = 0; i < 120; i++)
        {
            step = size.Shown(Frame, animate: true);
        }

        Assert.Equal(new Vector2(300f, 160f), step);

        size.Measure(new Vector2(120f, 80f), measuring: false);
        Assert.Equal(new Vector2(120f, 80f), size.Shown(Frame, animate: false));
    }

    [Fact]
    public void A_panel_size_ignores_nonsense_and_forgets_on_reset()
    {
        var size = new PanelSize();
        size.Measure(new Vector2(float.NaN, 10f), measuring: true);
        size.Measure(new Vector2(0f, 10f), measuring: true);
        Assert.False(size.Known);

        size.Measure(new Vector2(50f, 50f), measuring: false);
        size.Reset();
        Assert.False(size.Known);
        size.Measure(new Vector2(80f, 40f), measuring: false);
        Assert.Equal(new Vector2(80f, 40f), size.Shown(Frame, animate: true));
    }

    [Fact]
    public void Nothing_hides_the_todo_overlay_by_default()
    {
        var everything = new TodoContext(InCombat: true, Talking: true, GroupPose: true);
        Assert.False(TodoHideRules.None.Hides(everything));
        Assert.False(default(TodoHideRules).Hides(everything));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Each_hiding_option_answers_to_its_own_context(bool combat, bool talking, bool pose)
    {
        var rules = new TodoHideRules(combat, talking, pose);
        Assert.Equal(combat, rules.Hides(new TodoContext(InCombat: true, Talking: false, GroupPose: false)));
        Assert.Equal(talking, rules.Hides(new TodoContext(InCombat: false, Talking: true, GroupPose: false)));
        Assert.Equal(pose, rules.Hides(new TodoContext(InCombat: false, Talking: false, GroupPose: true)));
        Assert.False(rules.Hides(default));
    }

    [Fact]
    public void Only_rows_that_left_because_their_quest_was_completed_become_ghosts()
    {
        uint[] before = [10, 11, 12, 13];
        uint[] after = [10, 13, 14];
        var completed = new HashSet<uint> { 11 };
        var ghosts = new List<(int Index, uint RowId)>();

        Assert.Equal(1, TodoBeat.Ghosts(before, after, completed.Contains, ghosts));
        Assert.Equal([(1, 11u)], ghosts);

        ghosts.Clear();
        Assert.Equal(0, TodoBeat.Ghosts(before, before, static _ => true, ghosts));
        Assert.Empty(ghosts);
    }

    [Fact]
    public void The_beat_fills_the_moon_rings_once_and_fades_without_collapsing()
    {
        var (lit, halo, alpha) = TodoBeat.Look(0f);
        Assert.Equal(MotionTokens.WaxFrom, lit);
        Assert.Equal(-1f, halo);
        Assert.Equal(1f, alpha);

        (lit, halo, alpha) = TodoBeat.Look(0.5f);
        Assert.Equal(1f, lit, 3);
        Assert.InRange(halo, 0f, 1f);
        Assert.Equal(1f, alpha);

        (_, _, alpha) = TodoBeat.Look(0.9f);
        Assert.InRange(alpha, 0f, 0.5f);

        (lit, halo, alpha) = TodoBeat.Look(1f);
        Assert.Equal(1f, lit);
        Assert.Equal(-1f, halo);
        Assert.Equal(0f, alpha);
    }
}
