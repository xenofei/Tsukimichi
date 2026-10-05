namespace Tsukimichi.Core.Moonfall.Sound;

/// <summary>
/// A sound to play for an engine event: which, its variant, where across the board (−1 left … 1 right), and what it
/// ends (the drum roll, when Full Moon is hit or missed) or starts (the finale).
/// </summary>
public readonly record struct MoonfallCue(MoonfallSound Sound, int Variant = 0, float Pan = 0, MoonfallSound Stops = MoonfallSound.None, bool StartsFinale = false)
{
    public static readonly MoonfallCue Silent = new(MoonfallSound.None);
}

/// <summary>
/// The one table from engine events to sounds (plan v9 G8). The engine never hears about it: the window reads the
/// events as it always has and hands each one here, so sound cannot change a game. A value type with one flag of state
/// (a bucket catch's free ball is already in the catch's sound); allocation-free.
/// </summary>
public struct MoonfallSoundCues
{
    /// <summary>How far a sound pans at the board's edges.</summary>
    public const float PanWidth = 0.6f;

    private bool caught;

    /// <summary>The cue for <paramref name="e"/>; <see cref="MoonfallCue.Silent"/> for events with no sound.</summary>
    public MoonfallCue For(in MoonfallEvent e)
    {
        var afterCatch = caught;
        caught = e.Kind == MoonfallEventKind.BucketCatch;
        var pan = PanAt(e.X);
        return e.Kind switch
        {
            // The shot's peg count, from 1: the first peg is step 0 [M 10].
            MoonfallEventKind.PegHit => new(MoonfallSound.PegHit, PegStep(e.Count), pan),
            MoonfallEventKind.PegCleared or MoonfallEventKind.StuckClear => new(MoonfallSound.PegClear, 0, pan),
            MoonfallEventKind.WallBounce => new(MoonfallSound.WallTap, 0, pan),
            MoonfallEventKind.BucketBounce => new(MoonfallSound.RimTap, 0, pan),
            MoonfallEventKind.BucketCatch => new(MoonfallSound.BucketCatch, 0, pan),

            // The bucket's free ball comes straight after its catch, whose sound already says so.
            MoonfallEventKind.FreeBall => afterCatch && e.Value == 0 ? MoonfallCue.Silent : new(MoonfallSound.FreeBall),
            MoonfallEventKind.StyleShot => new(MoonfallSound.StyleShot, 0, pan),
            MoonfallEventKind.PowerTriggered => new(MoonfallSound.Power, PowerVariant(e.Value), pan),
            MoonfallEventKind.FeverApproach => new(MoonfallSound.FeverRoll),
            MoonfallEventKind.FeverApproachEnded => new(MoonfallSound.None, 0, 0, MoonfallSound.FeverRoll),

            // FULL MOON: the roll stops, the hit sounds and the finale begins under it, playing on through the tally.
            MoonfallEventKind.FeverHit => new(MoonfallSound.FeverHit, 0, 0, MoonfallSound.FeverRoll, StartsFinale: true),
            MoonfallEventKind.FeverLanded => new(MoonfallSound.CupLanding, CupVariant(e.Value), pan),
            MoonfallEventKind.LevelLost => new(MoonfallSound.LevelLost),
            _ => MoonfallCue.Silent,
        };
    }

    /// <summary>The chime step for the shot's <paramref name="pegCount"/>th peg: 0 for the first, held at the top step.</summary>
    public static int PegStep(int pegCount) => Math.Clamp(pegCount - 1, 0, MoonfallSounds.PegSteps - 1);

    /// <summary>Where across the board <paramref name="x"/> is, as a pan within ±<see cref="PanWidth"/>/2.</summary>
    public static float PanAt(double x) =>
        double.IsFinite(x) ? (float)(Math.Clamp((x / MoonfallRules.Width) - 0.5, -0.5, 0.5) * PanWidth) : 0f;

    /// <summary>A Fever cup's bonus as a <see cref="MoonfallSound.CupLanding"/> variant.</summary>
    public static int CupVariant(long bonus) => bonus >= 100_000 ? 2 : bonus >= 50_000 ? 1 : 0;

    private static int PowerVariant(long power) => (int)Math.Clamp(power, 0, MoonfallPowers.Count);
}

/// <summary>
/// The count-up's ticks (plan v9 G8): one tick at most every <see cref="Interval"/> seconds while the shown score
/// climbs (the counter itself steps every 10 ms [M 9], too fast to tick each step), pitched by how far it still has to
/// go, and a last tick when it lands. Pure, allocation-free.
/// </summary>
public struct MoonfallCountTicker
{
    /// <summary>The shortest gap between ticks: 20 a second.</summary>
    public const double Interval = 0.05;

    private long last;
    private double since;
    private bool started;

    /// <summary>
    /// The score shown now and the one it is counting to, after <paramref name="seconds"/>: true with the tick's
    /// <see cref="MoonfallSound.CountTick"/> variant when a tick is due. A score that went down (a new level) starts afresh.
    /// </summary>
    public bool Next(long shown, long target, double seconds, out int variant)
    {
        variant = 0;
        since += Math.Max(0, seconds);
        if (!started || shown < last)
        {
            started = true;
            last = shown;
            since = Interval;
            return false;
        }

        if (shown == last)
        {
            return false;
        }

        last = shown;
        var landed = shown >= target;
        if (since < Interval && !landed)
        {
            return false;
        }

        since = 0;
        var left = target - shown;
        variant = left >= 10_000 ? 0 : left >= 1_000 ? 1 : left >= 100 ? 2 : 3;
        return true;
    }
}
