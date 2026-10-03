using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// Event-driven motion (ui-revamp §3, accessibility B5): eased values and one-shot pulses keyed by a <see cref="ulong"/>
/// (an ImGui id, a row id) on <see cref="ImGui.GetTime"/>. Nothing here runs on its own: a value moves only because
/// its target changed (hover, selection), a pulse plays only because something called <see cref="Trigger"/>, and
/// there is no idle or breathing animation anywhere. Everything is skipped (values jump to their target, pulses do
/// not play) while <see cref="UiMetrics.ReduceMotion"/> is on and while the user is scrolling, so a scrolled table
/// leaves no wake of fading rows. Keys untouched for two seconds are pruned (<see cref="MotionStore"/>, easing in
/// <see cref="MotionMath"/>). <see cref="BeginFrame"/> runs once per frame before the windows draw. Allocation-free.
/// </summary>
public static class Motion
{
    /// <summary>After the last wheel tick, motion stays off this long so a fling does not flicker back on between ticks.</summary>
    private const double ScrollQuietSeconds = 0.15;

    /// <summary>How often stale keys are pruned.</summary>
    private const double PruneEverySeconds = 0.5;

    /// <summary>The waxing moon's key tag ("WAX"), with the quest's row id in the low half.</summary>
    private const uint WaxTag = 0x0057_4158;

    private static readonly MotionStore Store = new();
    private static readonly ScrollWatch Watched = new();
    private static readonly CompletionCues Cues = new();
    private static readonly System.Collections.Generic.List<uint> JustCompleted = new(8);
    private static double lastPrune;
    private static double scrollQuietUntil;
    private static bool scrolling;

    /// <summary>Whether motion plays this frame: Reduce motion off and the user not scrolling.</summary>
    public static bool Enabled => !UiMetrics.ReduceMotion && !scrolling;

    /// <summary>Whether the user is scrolling or scrolled a moment ago (the wheel, a scrollbar, a watched window's scroll), Reduce motion or not.</summary>
    public static bool Scrolling => scrolling;

    /// <summary>
    /// Once per frame, after <see cref="UiMetrics.Update"/> and before any window draws: notes whether the user is
    /// scrolling (the mouse wheel, a scrollbar held, or a watched window's scroll moved or about to jump; motion pauses
    /// meanwhile), drops every key when Reduce motion is on, and prunes stale keys.
    /// </summary>
    public static void BeginFrame()
    {
        var now = ImGui.GetTime();
        var io = ImGui.GetIO();
        if (io.MouseWheel != 0f || io.MouseWheelH != 0f || ScrollbarHeld() || WatchedWindowScrolled())
        {
            scrollQuietUntil = now + ScrollQuietSeconds;
        }

        scrolling = now < scrollQuietUntil;
        if (UiMetrics.ReduceMotion)
        {
            if (Store.Count > 0)
            {
                Store.Clear();
            }

            return;
        }

        if (now - lastPrune >= PruneEverySeconds || now < lastPrune)
        {
            lastPrune = now;
            Store.Prune(now);
        }
    }

    /// <summary>
    /// Watches the current window's scroll: call it inside a scrolling pane before its rows draw, every frame. When the
    /// scroll moved since the last frame (a scrollbar drag, a keyboard scroll, a reveal jump landing) motion pauses from
    /// here on, so rows coming into view do not animate; <see cref="BeginFrame"/> checks the watched windows too.
    /// </summary>
    public static void WatchScroll()
    {
        if (Watched.Moved(ImGuiP.GetCurrentWindow().ID, ImGui.GetScrollY()))
        {
            scrollQuietUntil = ImGui.GetTime() + ScrollQuietSeconds;
            scrolling = true;
        }
    }

    /// <summary>Whether a scrollbar of any window is held (the active item is a window's scrollbar).</summary>
    private static bool ScrollbarHeld()
    {
        var active = ImGuiP.GetActiveID();
        if (active == 0)
        {
            return false;
        }

        var window = ImGui.GetCurrentContext().ActiveIdWindow;
        return !window.IsNull && (active == ImGuiP.GetWindowScrollbarID(window, ImGuiAxis.Y) || active == ImGuiP.GetWindowScrollbarID(window, ImGuiAxis.X));
    }

    /// <summary>Whether a watched window scrolled since it was last seen, or has a scroll jump pending (a reveal, a keyboard scroll).</summary>
    private static bool WatchedWindowScrolled()
    {
        var any = false;
        for (var i = 0; i < Watched.Count; i++)
        {
            var id = Watched.IdAt(i);
            var window = ImGuiP.FindWindowByID(id);
            if (window.IsNull)
            {
                continue;
            }

            // Every window is noted, so each keeps its position even when an earlier one already moved.
            any |= Watched.Moved(id, window.Scroll.Y);
            any |= window.ScrollTarget.Y < float.MaxValue;
        }

        return any;
    }

    /// <summary>
    /// The value under <paramref name="key"/> eased towards <paramref name="target"/> at <paramref name="rate"/> per
    /// second (<see cref="MotionMath.HoverRate"/> ≈ 120 ms by default). A new key starts at its target; with motion
    /// off the target comes back at once.
    /// </summary>
    public static float Lerp(ulong key, float target, float rate = MotionMath.HoverRate) =>
        Store.Lerp(key, target, rate, ImGui.GetTime(), ImGui.GetIO().DeltaTime, Enabled);

    /// <summary>Starts (or restarts) the pulse under <paramref name="key"/> now; nothing happens with motion off.</summary>
    public static void Trigger(ulong key) => Store.Trigger(key, ImGui.GetTime(), Enabled);

    /// <summary>
    /// Progress 0..1 of the pulse under <paramref name="key"/> lasting <paramref name="seconds"/>, or -1 when none is
    /// playing (never triggered, over, or motion off). Callers draw nothing on -1.
    /// </summary>
    public static float Pulse(ulong key, float seconds) => Store.Pulse(key, seconds, ImGui.GetTime(), Enabled);

    /// <summary>
    /// A key made of a <paramref name="tag"/> in the high half and an id (an ImGui id, a row id, an index) in the low
    /// half, so the same item can carry several motions (hover, chevron, gauge, reveal) without their keys meeting.
    /// </summary>
    public static ulong Key(uint tag, uint id) => ((ulong)tag << 32) | id;

    /// <summary>
    /// A halo gauge's shown fraction: it moves to <paramref name="fraction"/> over about 500 ms when the fraction
    /// changes (a quest completed, another character viewed) and sits still otherwise. A gauge seen for the first time,
    /// or after two seconds out of view, starts at its fraction; with motion off it jumps.
    /// </summary>
    public static float Gauge(ulong key, float fraction) =>
        Lerp(key, float.IsFinite(fraction) ? System.Math.Clamp(fraction, 0f, 1f) : 0f, MotionMath.GaugeRate);

    /// <summary>
    /// An orbit's shown fraction (Moon Road proposal §9): under Full flair with motion on, the arc fills from empty the
    /// first time the orbit shows and runs from the old fraction to the new one when the count changes, each over
    /// <see cref="MotionMath.OrbitFillSeconds"/> with an ease-out computed from its start time. At Quiet and Plain it is
    /// <see cref="Gauge"/> (the 1.3 behaviour); under Reduce motion, or while scrolling, it is the fraction itself.
    /// <paramref name="from"/> is where a key without state starts: 0 for an orbit never shown, the fraction it last
    /// showed for one whose state was pruned while out of view (<see cref="FillMemory"/>), so it does not refill.
    /// </summary>
    public static float Fill(ulong key, float fraction, float from = 0f)
    {
        if (!Theme.FlairMotion)
        {
            return Gauge(key, fraction);
        }

        var f = float.IsFinite(fraction) ? System.Math.Clamp(fraction, 0f, 1f) : 0f;
        return Store.Tween(key, f, MotionMath.OrbitFillSeconds, ImGui.GetTime(), Enabled, from);
    }

    /// <summary>
    /// Draws the reveal pulse under <paramref name="key"/> around the rectangle, if one is playing: a Moon ring that
    /// grows 0 → 6 px outward while fading 0.7 → 0, twice in 900 ms (ui-revamp §3, accessibility B5: 2.2 flashes/s on
    /// a small area). Nothing is drawn with motion off or once the pulse has ended.
    /// </summary>
    public static void DrawRevealPulse(ImDrawListPtr dl, ulong key, System.Numerics.Vector2 min, System.Numerics.Vector2 max, float rounding)
    {
        var (ring, grow) = MotionMath.RevealRing(Pulse(key, MotionMath.RevealPulseSeconds));
        if (ring < 0)
        {
            return;
        }

        var outset = UiMetrics.Px(6f) * grow;
        var pad = new System.Numerics.Vector2(outset);
        dl.AddRect(min - pad, max + pad, Theme.WithAlpha(Theme.Moon, 0.7f * (1f - grow)), rounding + outset, ImDrawFlags.None, System.MathF.Max(1.5f, UiMetrics.Px(2f)));
    }

    /// <summary>
    /// Once per frame after <see cref="BeginFrame"/>: starts the waxing moon (<see cref="Wax"/>) of every quest the live
    /// character completed since the last frame, while that character is the one on screen (<paramref name="shown"/>).
    /// Nothing plays under Reduce motion or while scrolling, and the first look at a character replays nothing.
    /// </summary>
    public static void NoteCompletions(ulong? liveCharacter, System.Collections.Generic.IReadOnlyList<Core.Runtime.QuestEvent> recentEvents, bool shown)
    {
        JustCompleted.Clear();
        if (Cues.Take(liveCharacter, recentEvents, shown, JustCompleted) == 0)
        {
            return;
        }

        foreach (var rowId in JustCompleted)
        {
            Trigger(Key(WaxTag, rowId));
        }
    }

    /// <summary>
    /// The waxing moon of quest <paramref name="rowId"/>: its lit fraction (from the half moon to full over
    /// <see cref="MotionTokens.Wax"/>, eased out) while the one-shot plays after a completion, or -1 when none is playing.
    /// The glyph drawer reads it (<see cref="MoonWax"/>); a row that is not drawn simply lets it run out unseen.
    /// </summary>
    public static float Wax(uint rowId) => MotionTokens.WaxFraction(Pulse(Key(WaxTag, rowId), MotionTokens.Wax));

    /// <summary>Forgets every key (plugin unload).</summary>
    public static void Reset() => Store.Clear();
}
