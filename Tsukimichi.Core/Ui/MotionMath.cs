using System;
using System.Collections.Generic;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// The easing arithmetic behind <c>Ui.Motion</c> (ui-revamp §3): a frame-rate independent exponential approach
/// <c>v += (target − v) · (1 − e^(−dt·k))</c> and the progress of a one-shot pulse. Pure functions so they are tested
/// without ImGui.
/// </summary>
public static class MotionMath
{
    /// <summary>Rate for hover fills: <see cref="MotionTokens.HoverIn"/> (120 ms to 90 %).</summary>
    public const float HoverRate = MotionTokens.Ln10 / MotionTokens.HoverIn;

    /// <summary>Rate for a hover wash fading out: <see cref="MotionTokens.HoverOut"/> (180 ms to 90 %).</summary>
    public const float HoverOutRate = MotionTokens.Ln10 / MotionTokens.HoverOut;

    /// <summary>Rate for selection rings: <see cref="MotionTokens.Select"/> (150 ms to 90 %).</summary>
    public const float SelectRate = MotionTokens.Ln10 / MotionTokens.Select;

    /// <summary>Rate for chevrons and similar rotations: <see cref="MotionTokens.Chevron"/> (140 ms to 90 %).</summary>
    public const float ChevronRate = MotionTokens.Ln10 / MotionTokens.Chevron;

    /// <summary>Rate for a halo gauge's fill moving to a new fraction (about 500 ms, ease-out; accessibility B5).</summary>
    public const float GaugeRate = 6f;

    /// <summary>The reveal pulse: two rings of <see cref="RevealRingSeconds"/> each, back to back (ui-revamp §3, 2.2 flashes/s).</summary>
    public const float RevealPulseSeconds = 0.9f;

    /// <summary>One ring of the reveal pulse.</summary>
    public const float RevealRingSeconds = 0.45f;

    /// <summary>
    /// The orbit fill (Moon Road proposal §9, the mockup's 0.9 s): an orbit's arc and its road run from nothing to their
    /// fraction when first shown, and from the old fraction to the new one when the count changes, ease-out.
    /// </summary>
    public const float OrbitFillSeconds = 0.9f;

    /// <summary>The rail's active station lighting up after a tab change (the mockup's 0.5 s ease-out); since 1.13 the bead travels instead (<see cref="MotionTokens.Travel"/>).</summary>
    public const float StationLightSeconds = 0.5f;

    /// <summary>Closer than this to the target counts as there, so an eased value settles instead of creeping forever.</summary>
    public const float SnapEpsilon = 0.001f;

    /// <summary>
    /// The reveal pulse at <paramref name="progress"/> (0..1 over <see cref="RevealPulseSeconds"/>): which of its two
    /// rings is showing (0 or 1) and how far that ring has grown (0..1, eased out). -1 for the ring when the pulse is
    /// not running (a negative or non-finite progress, or 1 and beyond).
    /// </summary>
    public static (int Ring, float Grow) RevealRing(float progress)
    {
        if (!float.IsFinite(progress) || progress < 0f || progress >= 1f)
        {
            return (-1, 0f);
        }

        var rings = RevealPulseSeconds / RevealRingSeconds;
        var scaled = progress * rings;
        var ring = Math.Min((int)scaled, (int)rings - 1);
        return (ring, EaseOutCubic(scaled - ring));
    }

    /// <summary>
    /// One frame of the exponential approach from <paramref name="current"/> towards <paramref name="target"/> at rate
    /// <paramref name="rate"/> (per second) over <paramref name="deltaSeconds"/>. A non-positive or non-finite rate or
    /// time leaves the value where it is; a non-finite current value jumps to the target; within
    /// <see cref="SnapEpsilon"/> the result is the target itself.
    /// </summary>
    public static float Approach(float current, float target, float rate, float deltaSeconds)
    {
        if (!float.IsFinite(target))
        {
            return current;
        }

        if (!float.IsFinite(current))
        {
            return target;
        }

        if (!(rate > 0f) || !(deltaSeconds > 0f) || !float.IsFinite(rate) || !float.IsFinite(deltaSeconds))
        {
            return current;
        }

        var next = current + (target - current) * (1f - MathF.Exp(-deltaSeconds * rate));
        return MathF.Abs(target - next) < SnapEpsilon ? target : next;
    }

    /// <summary>Cubic ease-out of <paramref name="t"/> (clamped to 0..1): fast start, soft landing.</summary>
    public static float EaseOutCubic(float t)
    {
        t = float.IsFinite(t) ? Math.Clamp(t, 0f, 1f) : 1f;
        var u = 1f - t;
        return 1f - u * u * u;
    }

    /// <summary>
    /// Cubic ease-in-out of <paramref name="t"/> (clamped to 0..1): a soft start and a soft landing, for something that
    /// travels from one place to another (the rail's bead between stations).
    /// </summary>
    public static float EaseInOutCubic(float t)
    {
        t = float.IsFinite(t) ? Math.Clamp(t, 0f, 1f) : 1f;
        if (t < 0.5f)
        {
            return 4f * t * t * t;
        }

        var u = (-2f * t) + 2f;
        return 1f - (u * u * u * 0.5f);
    }

    /// <summary>
    /// One frame of <see cref="Approach"/> at <paramref name="rateUp"/> while the value rises and
    /// <paramref name="rateDown"/> while it falls: a hover wash comes in quicker than it leaves.
    /// </summary>
    public static float ApproachAsym(float current, float target, float rateUp, float rateDown, float deltaSeconds) =>
        Approach(current, target, float.IsFinite(current) && target < current ? rateDown : rateUp, deltaSeconds);

    /// <summary>
    /// Progress 0..1 of a pulse that started at <paramref name="startSeconds"/> and lasts <paramref name="durationSeconds"/>,
    /// at time <paramref name="nowSeconds"/>; -1 before it starts, once it has ended, or for a non-positive duration.
    /// </summary>
    public static float PulseProgress(double startSeconds, double nowSeconds, float durationSeconds)
    {
        if (!(durationSeconds > 0f) || !double.IsFinite(startSeconds) || !double.IsFinite(nowSeconds))
        {
            return -1f;
        }

        var elapsed = nowSeconds - startSeconds;
        if (elapsed < 0d || elapsed >= durationSeconds)
        {
            return -1f;
        }

        return (float)(elapsed / durationSeconds);
    }

    /// <summary>How many phases the loading moon steps through in one cycle (R3 #12).</summary>
    public const int LoadingMoonSteps = 8;

    /// <summary>One cycle of the loading moon, new to full and back, in seconds.</summary>
    public const float LoadingMoonCycleSeconds = 2f;

    /// <summary>The loading moon's lit fraction while it stands still (Reduce motion): the first quarter.</summary>
    public const float LoadingMoonStill = 0.5f;

    /// <summary>
    /// The loading moon's lit fraction at <paramref name="nowSeconds"/> (R3 #12, in place of the loading dots): it
    /// steps through <see cref="LoadingMoonSteps"/> phases per <see cref="LoadingMoonCycleSeconds"/>, waxing from new to
    /// full and waning back, so the loop has no jump. Under <paramref name="reduceMotion"/> (or at a non-finite time) it
    /// stands still at <see cref="LoadingMoonStill"/>. Always 0..1.
    /// </summary>
    public static float LoadingMoonFraction(double nowSeconds, bool reduceMotion)
    {
        if (reduceMotion || !double.IsFinite(nowSeconds))
        {
            return LoadingMoonStill;
        }

        var cycle = nowSeconds / LoadingMoonCycleSeconds;
        cycle -= Math.Floor(cycle);
        var step = Math.Min((int)(cycle * LoadingMoonSteps), LoadingMoonSteps - 1);
        var half = LoadingMoonSteps / 2;
        var lit = step <= half ? step : LoadingMoonSteps - step;
        return lit / (float)half;
    }
}

/// <summary>
/// Per-key motion state for <c>Ui.Motion</c>: eased values and pulse start times keyed by a <see cref="ulong"/> (an
/// ImGui id, a row id), each stamped with the time it was last touched. <see cref="Prune"/> drops keys untouched for
/// <see cref="StaleAfterSeconds"/> so a 5 000-row table cannot leak entries (ui-revamp §6.3). Nothing allocates once
/// the dictionary has grown to the working set: entries are structs and removal during enumeration is allowed.
/// </summary>
public sealed class MotionStore
{
    /// <summary>Keys not touched for this long are dropped by <see cref="Prune"/>.</summary>
    public const double StaleAfterSeconds = 2.0;

    private struct Entry
    {
        public float Value;
        public double Touched;
        public double PulseStart;
        public bool Pulsing;
        public float From;
        public float To;
        public double TweenStart;
        public bool Tweening;
        public uint Signature;
        public bool Signed;
    }

    private readonly Dictionary<ulong, Entry> entries = [];

    /// <summary>How many keys hold state.</summary>
    public int Count => entries.Count;

    /// <summary>Whether <paramref name="key"/> holds state.</summary>
    public bool Contains(ulong key) => entries.ContainsKey(key);

    /// <summary>
    /// Eases the value under <paramref name="key"/> towards <paramref name="target"/> and returns it. A key seen for the
    /// first time starts at the target (nothing animates into existence). With <paramref name="animate"/> false (Reduce
    /// motion, or the user is scrolling) the value jumps to the target, so no wake of fading rows is left behind.
    /// </summary>
    public float Lerp(ulong key, float target, float rate, double nowSeconds, float deltaSeconds, bool animate)
    {
        ref var entry = ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(entries, key, out var existed);
        entry.Touched = nowSeconds;
        entry.Value = existed && animate ? MotionMath.Approach(entry.Value, target, rate, deltaSeconds) : target;
        return entry.Value;
    }

    /// <summary>
    /// <see cref="Lerp"/> at <paramref name="rateUp"/> while the value rises and <paramref name="rateDown"/> while it
    /// falls (<see cref="MotionMath.ApproachAsym"/>): hover in 120 ms, out 180 ms.
    /// </summary>
    public float LerpAsym(ulong key, float target, float rateUp, float rateDown, double nowSeconds, float deltaSeconds, bool animate)
    {
        ref var entry = ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(entries, key, out var existed);
        entry.Touched = nowSeconds;
        entry.Value = existed && animate ? MotionMath.ApproachAsym(entry.Value, target, rateUp, rateDown, deltaSeconds) : target;
        return entry.Value;
    }

    /// <summary>
    /// A one-shot that plays when <paramref name="signature"/> changes (a pill's label, a count, a page): progress 0..1
    /// over <paramref name="seconds"/> after the change, or -1 when none is playing. A key seen for the first time only
    /// notes its signature, so nothing plays when an item first shows or comes back after being pruned. A change starts
    /// the one-shot only while <paramref name="start"/> is true (the caller's own condition, such as "a quest was just
    /// completed"); with <paramref name="animate"/> false (Reduce motion, the user scrolling) the change is noted and
    /// nothing plays. Allocation-free once the key exists.
    /// </summary>
    public float Changed(ulong key, uint signature, float seconds, double nowSeconds, bool animate, bool start = true)
    {
        ref var entry = ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(entries, key, out var existed);
        entry.Touched = nowSeconds;
        if (!existed || !entry.Signed)
        {
            entry.Signature = signature;
            entry.Signed = true;
            entry.Pulsing = false;
            return -1f;
        }

        if (entry.Signature != signature)
        {
            entry.Signature = signature;
            entry.Pulsing = animate && start;
            entry.PulseStart = nowSeconds;
        }

        if (!entry.Pulsing)
        {
            return -1f;
        }

        var progress = animate ? MotionMath.PulseProgress(entry.PulseStart, nowSeconds, seconds) : -1f;
        if (progress < 0f)
        {
            entry.Pulsing = false;
        }

        return progress;
    }

    /// <summary>
    /// A timed tween under <paramref name="key"/>: the value runs to <paramref name="target"/> over
    /// <paramref name="seconds"/> with a cubic ease-out, computed from the tween's start time rather than accumulated per
    /// frame. A key seen for the first time starts at <paramref name="from"/> (0: an orbit fills from empty when first
    /// shown); a new target restarts the tween from the value shown now, so a change mid-fill carries on smoothly. With
    /// <paramref name="animate"/> false (Reduce motion, the user scrolling) the value is the target at once, and a key
    /// first seen then does not replay its fill later. Allocation-free once the key exists.
    /// </summary>
    public float Tween(ulong key, float target, float seconds, double nowSeconds, bool animate, float from = 0f)
    {
        ref var entry = ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(entries, key, out var existed);
        entry.Touched = nowSeconds;
        if (!animate || !(seconds > 0f) || !float.IsFinite(target))
        {
            entry.Value = entry.From = entry.To = float.IsFinite(target) ? target : entry.Value;
            entry.Tweening = false;
            return entry.Value;
        }

        if (!existed)
        {
            entry.Value = float.IsFinite(from) ? from : 0f;
            StartTween(ref entry, target, nowSeconds);
        }
        else if (entry.To != target || (!entry.Tweening && entry.Value != target))
        {
            StartTween(ref entry, target, nowSeconds);
        }

        if (entry.Tweening)
        {
            var progress = MotionMath.PulseProgress(entry.TweenStart, nowSeconds, seconds);
            if (progress < 0f)
            {
                entry.Tweening = false;
                entry.Value = entry.To;
            }
            else
            {
                entry.Value = entry.From + ((entry.To - entry.From) * MotionMath.EaseOutCubic(progress));
            }
        }

        return entry.Value;
    }

    private static void StartTween(ref Entry entry, float target, double nowSeconds)
    {
        entry.From = entry.Value;
        entry.To = target;
        entry.TweenStart = nowSeconds;
        entry.Tweening = true;
    }

    /// <summary>
    /// Starts (or restarts) the pulse under <paramref name="key"/> at <paramref name="nowSeconds"/>; ignored when
    /// <paramref name="animate"/> is false, so a pulse never plays under Reduce motion.
    /// </summary>
    public void Trigger(ulong key, double nowSeconds, bool animate)
    {
        if (!animate)
        {
            entries.Remove(key);
            return;
        }

        ref var entry = ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(entries, key, out _);
        entry.Touched = nowSeconds;
        entry.PulseStart = nowSeconds;
        entry.Pulsing = true;
    }

    /// <summary>
    /// Progress 0..1 of the pulse under <paramref name="key"/>, or -1 when none is running (never triggered, finished,
    /// or <paramref name="animate"/> is false). A finished pulse is forgotten so the next read is cheap.
    /// </summary>
    public float Pulse(ulong key, float durationSeconds, double nowSeconds, bool animate)
    {
        ref var entry = ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrNullRef(entries, key);
        if (System.Runtime.CompilerServices.Unsafe.IsNullRef(ref entry) || !entry.Pulsing)
        {
            return -1f;
        }

        var progress = animate ? MotionMath.PulseProgress(entry.PulseStart, nowSeconds, durationSeconds) : -1f;
        if (progress < 0f)
        {
            entry.Pulsing = false;
            return -1f;
        }

        entry.Touched = nowSeconds;
        return progress;
    }

    /// <summary>Drops every key untouched since <paramref name="nowSeconds"/> − <see cref="StaleAfterSeconds"/>; returns how many went.</summary>
    public int Prune(double nowSeconds)
    {
        var removed = 0;
        foreach (var (key, entry) in entries)
        {
            if (nowSeconds - entry.Touched > StaleAfterSeconds)
            {
                entries.Remove(key);
                removed++;
            }
        }

        return removed;
    }

    /// <summary>Forgets every key (plugin unload, Reduce motion switched on).</summary>
    public void Clear() => entries.Clear();
}

/// <summary>
/// The scroll positions of a few watched windows (the Journal tree's pane), so motion can pause for any scroll, not
/// only the mouse wheel's: a scrollbar drag, a keyboard scroll, a reveal jump. Up to <see cref="Capacity"/> windows by
/// id; a window first seen is not a move. Fixed arrays, allocation-free.
/// </summary>
public sealed class ScrollWatch
{
    /// <summary>How many windows can be watched at once; further ones are ignored.</summary>
    public const int Capacity = 8;

    private readonly uint[] ids = new uint[Capacity];
    private readonly float[] positions = new float[Capacity];

    /// <summary>How many windows are watched.</summary>
    public int Count { get; private set; }

    /// <summary>The id of the <paramref name="index"/>th watched window.</summary>
    public uint IdAt(int index) => ids[index];

    /// <summary>
    /// Notes that window <paramref name="id"/> is scrolled to <paramref name="scrollY"/>; true when it moved since the
    /// last note (more than a hundredth of a pixel). A window seen for the first time is watched from here on and has not moved.
    /// </summary>
    public bool Moved(uint id, float scrollY)
    {
        if (!float.IsFinite(scrollY))
        {
            return false;
        }

        for (var i = 0; i < Count; i++)
        {
            if (ids[i] == id)
            {
                var moved = MathF.Abs(positions[i] - scrollY) > 0.01f;
                positions[i] = scrollY;
                return moved;
            }
        }

        if (Count < Capacity)
        {
            ids[Count] = id;
            positions[Count] = scrollY;
            Count++;
        }

        return false;
    }

    /// <summary>Forgets every window.</summary>
    public void Clear() => Count = 0;
}

/// <summary>
/// The fraction each orbit last showed, kept past <see cref="MotionStore.Prune"/> (Moon Road proposal §9: an orbit fills
/// from empty once, the first time it shows). A row scrolled out of view for two seconds loses its tween state; when
/// it comes back (a scrollbar drag, a keyboard scroll, a reveal jump, a return to the tab) its tween starts from the
/// fraction remembered here instead of from 0, so it does not replay its fill. Cleared when what the fractions mean
/// changes (another catalog, another character viewed). One entry per node; allocation-free once grown.
/// </summary>
public sealed class FillMemory
{
    private readonly Dictionary<ulong, float> last = [];

    /// <summary>How many keys are remembered.</summary>
    public int Count => last.Count;

    /// <summary>Whether the orbit under <paramref name="key"/> has shown before.</summary>
    public bool Knows(ulong key) => last.ContainsKey(key);

    /// <summary>Where a tween under <paramref name="key"/> starts when it has no state: the fraction it last showed, or 0 for an orbit never shown.</summary>
    public float StartFor(ulong key) => last.TryGetValue(key, out var fraction) ? fraction : 0f;

    /// <summary>Notes that the orbit under <paramref name="key"/> shows <paramref name="fraction"/> (clamped to 0..1; non-finite reads as 0).</summary>
    public void Remember(ulong key, float fraction) =>
        last[key] = float.IsFinite(fraction) ? Math.Clamp(fraction, 0f, 1f) : 0f;

    /// <summary>Forgets every orbit (another catalog or character).</summary>
    public void Clear() => last.Clear();
}
