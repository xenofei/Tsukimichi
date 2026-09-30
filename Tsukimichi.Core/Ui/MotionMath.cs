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
    /// <summary>Rate for hover fills (about 120 ms to settle).</summary>
    public const float HoverRate = 18f;

    /// <summary>Rate for selection rings (about 150 ms).</summary>
    public const float SelectRate = 12f;

    /// <summary>Rate for chevrons and similar rotations (about 140 ms).</summary>
    public const float ChevronRate = 10f;

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

    /// <summary>The rail's active station lighting up after a tab change (the mockup's 0.5 s ease-out).</summary>
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
