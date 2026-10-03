namespace Tsukimichi.Core.Ui;

/// <summary>
/// The Full sky's clocks (docs/design/v7/ui/spec.md §3.3, §3.6 and Revision 3), kept apart from drawing so they are
/// tested without ImGui:
/// <list type="bullet">
/// <item><see cref="Time"/>, the twinkle clock, runs only while the sky animates (Full, Reduce motion off) and the main
/// window is focused, so an unfocused window holds still and nothing jumps when focus returns.</item>
/// <item><see cref="DriftTime"/> runs on the same terms and only while "Moving night sky" is on; the drift is
/// <see cref="Offset"/>.</item>
/// <item>The ambient meteor falls due every 3 to 6 minutes of drift time (seeded), and is skipped within
/// <see cref="MotionTokens.MeteorCooldown"/> of a completion meteor.</item>
/// <item>A completion meteor plays at most once in <see cref="MotionTokens.MeteorCooldown"/> seconds of real time.</item>
/// </list>
/// Nothing here allocates.
/// </summary>
public sealed class SkyClock
{
    /// <summary>The longest step a frame may advance the clocks: a hitch (a loading screen) is not caught up on.</summary>
    public const double MaxStepSeconds = 0.1;

    private uint state;
    private double lastCompletionMeteor = double.NegativeInfinity;

    /// <param name="seed">Seeds the ambient meteor's intervals.</param>
    public SkyClock(int seed = 29)
    {
        state = (uint)seed & 0x7FFFFFFFu;
        NextAmbient = NextInterval();
    }

    /// <summary>The twinkle clock, in seconds.</summary>
    public double Time { get; private set; }

    /// <summary>The drift clock, in seconds.</summary>
    public double DriftTime { get; private set; }

    /// <summary>When (on <see cref="DriftTime"/>) the next ambient meteor falls due.</summary>
    public double NextAmbient { get; private set; }

    /// <summary>
    /// Advances the clocks by one frame of <paramref name="deltaSeconds"/> (at most <see cref="MaxStepSeconds"/>):
    /// only while the sky <paramref name="animates"/> and the main window is <paramref name="focused"/>, and the drift
    /// only while it <paramref name="drifts"/> too.
    /// </summary>
    public void Advance(double deltaSeconds, bool animates, bool focused, bool drifts)
    {
        if (!animates || !focused || !double.IsFinite(deltaSeconds) || deltaSeconds <= 0)
        {
            return;
        }

        var step = Math.Min(deltaSeconds, MaxStepSeconds);
        Time += step;
        if (drifts)
        {
            DriftTime += step;
        }
    }

    /// <summary>The drift in px at <paramref name="pxPerMinute"/> (the field moves left by it, wrapping on its tile).</summary>
    public float Offset(float pxPerMinute) => (float)(DriftTime * pxPerMinute / 60.0);

    /// <summary>
    /// Whether an ambient meteor plays now (<paramref name="now"/> is real time, as the completion meteor's): true once
    /// <see cref="DriftTime"/> passes <see cref="NextAmbient"/>, unless a completion meteor played in the last
    /// <see cref="MotionTokens.MeteorCooldown"/> seconds. Either way the next one is scheduled 3 to 6 minutes on.
    /// </summary>
    public bool TakeAmbient(double now)
    {
        if (DriftTime < NextAmbient)
        {
            return false;
        }

        NextAmbient = DriftTime + NextInterval();
        return !(now - lastCompletionMeteor < MotionTokens.MeteorCooldown);
    }

    /// <summary>
    /// Whether a completion meteor plays now (<paramref name="now"/> in real seconds): at most once in
    /// <see cref="MotionTokens.MeteorCooldown"/>.
    /// </summary>
    public bool TakeCompletion(double now)
    {
        if (!double.IsFinite(now) || now - lastCompletionMeteor < MotionTokens.MeteorCooldown)
        {
            return false;
        }

        lastCompletionMeteor = now;
        return true;
    }

    /// <summary>3 to 6 minutes, from the seeded generator.</summary>
    private double NextInterval()
    {
        state = unchecked((state * 1103515245u) + 12345u) & 0x7FFFFFFFu;
        var r = state / (double)0x7FFFFFFF;
        return MotionTokens.AmbientMeteorMinSeconds + ((MotionTokens.AmbientMeteorMaxSeconds - MotionTokens.AmbientMeteorMinSeconds) * r);
    }
}
