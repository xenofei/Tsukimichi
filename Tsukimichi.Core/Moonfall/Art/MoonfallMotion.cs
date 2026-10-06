using System.Numerics;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>How much ambient motion the board shows (spec-rich2.md §4, the motion rules 4 and 5).</summary>
public enum MoonfallMotionLevel : byte
{
    /// <summary>Everything still: Reduce motion (which overrides Decoration), or Decoration Plain.</summary>
    Still,

    /// <summary>Decoration Simple (Tsukimichi's Quiet): the beams, the halos and the lantern only.</summary>
    Simple,

    /// <summary>Decoration Full: the beams, dust, fireflies, stars, mist, lamps, the lantern, the medallion's glow and the glints.</summary>
    Full,
}

/// <summary>
/// The ambient motion of the in-play board (spec-rich2.md §4–5), as pure functions of the board clock so the drawing
/// allocates nothing and the tests can ask where anything is at any time. What may move is light (beams, halos, glints,
/// flicker), particles (dust, fireflies, stars) and atmosphere (mist); never a peg, a brick, a frame or a label.
/// Clearance (rule 3): nothing moves within <see cref="PegKeepOut"/> units of a piece, and particles keep
/// <see cref="ParticleKeepOut"/> units, with their whole path and halo, from every piece's edge (F5).
/// </summary>
public static class MoonfallMotion
{
    /// <summary>No moving layer shows within this many units of a piece's edge.</summary>
    public const float PegKeepOut = 2.5f;

    /// <summary>Particles (dust, fireflies, stars), with their path and halo, keep this far from every piece's edge.</summary>
    public const float ParticleKeepOut = 8f;

    /// <summary>A firefly's wander: across and up and down (units), once every <see cref="Loop"/> seconds.</summary>
    public const float WanderX = 9f;

    /// <inheritdoc cref="WanderX"/>
    public const float WanderY = 4f;

    /// <summary>The motion's loop, seconds (the fireflies' wander, the dust's life).</summary>
    public const float Loop = 6f;

    /// <summary>
    /// The beams' loop, seconds: their drift and breath, slower than the rest so the light swells calmly (game designer
    /// runtime round 1, m3: the ±15% over 6 s could not be seen).
    /// </summary>
    public const float BeamLoop = 9.5f;

    /// <summary>A glint's halo radius, units (its soft light reaches about 2.2 of it).</summary>
    public const float GlintHalo = 1.8f;

    /// <summary>How far a glint's light reaches from its centre, units.</summary>
    public const float GlintReach = GlintHalo * 2.2f;

    /// <summary>The firefly's core and halo radii (units) at size 1; the halo reaches about 2.2 of its radius.</summary>
    public const float FireflyCore = 1.5f;

    /// <inheritdoc cref="FireflyCore"/>
    public const float FireflyHalo = 4.5f;

    /// <summary>Dust motes' drift, units a second (spec: particles 4–10 px/s at 1x).</summary>
    public static readonly Vector2 DustDrift = new(6f, 9f);

    /// <summary>A dust mote's radius, units.</summary>
    public const float DustRadius = 0.8f;

    /// <summary>The level of motion for a Decoration setting and Reduce motion (which always means still).</summary>
    public static MoonfallMotionLevel For(Flair flair, bool reduceMotion) =>
        reduceMotion || flair == Flair.Plain ? MoonfallMotionLevel.Still : flair == Flair.Quiet ? MoonfallMotionLevel.Simple : MoonfallMotionLevel.Full;

    /// <summary>A firefly's halo reach, units.</summary>
    public static float HaloReach(float size) => FireflyHalo * size * 2.2f;

    /// <summary>Where a firefly is at <paramref name="seconds"/> (its rest place when still): a small closed loop, 9 × 4 units.</summary>
    public static Vector2 FireflyAt(in MoonfallFirefly f, double seconds, bool still)
    {
        if (still)
        {
            return new Vector2(f.X, f.Y);
        }

        var a = (float)(2 * Math.PI * (seconds % Loop) / Loop) + f.Phase;
        return new Vector2(f.X + (MathF.Cos(a) * WanderX), f.Y + (MathF.Sin(2 * a) * WanderY));
    }

    /// <summary>A firefly's light, 0.10 to 1 (1 steady when still).</summary>
    public static float FireflyPulse(in MoonfallFirefly f, double seconds, bool still) =>
        still ? 0.8f : 0.55f + (0.45f * MathF.Sin((float)(2 * Math.PI * (seconds % f.Period) / f.Period) + f.Phase));

    /// <summary>
    /// The least clearance round a firefly's whole wander at (x, y) of <paramref name="size"/>, less what it needs
    /// (<see cref="ParticleKeepOut"/> plus its halo), sampled at <paramref name="samples"/> points: at least 0 to place it.
    /// </summary>
    public static float FireflyClearance(MoonfallClearance clearance, float x, float y, float size, int samples)
    {
        ArgumentNullException.ThrowIfNull(clearance);
        var need = ParticleKeepOut + HaloReach(size);
        var least = float.MaxValue;
        for (var k = 0; k < samples; k++)
        {
            var a = 2 * MathF.PI * k / samples;
            least = MathF.Min(least, clearance.At(x + (MathF.Cos(a) * WanderX), y + (MathF.Sin(2 * a) * WanderY)) - need);
        }

        return least;
    }

    /// <summary>A breath: 1 ± <paramref name="amplitude"/> over <paramref name="period"/> seconds (1 when still).</summary>
    public static float Breath(double seconds, float period, float amplitude, bool still) =>
        still ? 1f : 1f + (amplitude * MathF.Sin((float)(2 * Math.PI * (seconds % period) / period)));

    /// <summary>
    /// A lamp's flicker, ±10%: two sines at 1.3 and 1.9 Hz (never above 2 Hz) whose beat makes it irregular, each lamp
    /// shifted by its phase; both make whole cycles in 10 s, so the loop joins without a step. 1, steady, when still.
    /// </summary>
    public static float Flicker(double seconds, float phase, bool still) =>
        still ? 1f : 1f + (0.06f * MathF.Sin((float)(2 * Math.PI * 1.3 * (seconds % 10)) + phase)) + (0.04f * MathF.Sin((float)(2 * Math.PI * 1.9 * (seconds % 10)) + 1f + (1.7f * phase)));

    /// <summary>A star's twinkle lift, 0 to 0.35 (none when still).</summary>
    public static float Twinkle(in MoonfallStar s, double seconds, bool still) =>
        still ? 0f : MathF.Max(0f, 0.35f * MathF.Sin((float)(2 * Math.PI * (seconds % s.Period) / s.Period) + s.Phase));

    /// <summary>
    /// The beams' two drift layers and their breath (the break-up drifting between two places every
    /// <see cref="BeamLoop"/> s, breathing ±<see cref="MoonfallSceneBuilder.BeamBreath"/>): each layer's alpha, 0..1, of
    /// its texture (which holds the moving share). Still: the first layer at half, so the beams stand exactly as baked at rest.
    /// </summary>
    public static (float A, float B) Beams(double seconds, bool still)
    {
        if (still)
        {
            return (0.5f, 0f);
        }

        var theta = (float)(2 * Math.PI * (seconds % BeamLoop) / BeamLoop);
        var wa = 0.5f + (0.5f * MathF.Cos(theta));
        var breath = 0.5f + (0.5f * MathF.Sin(theta));
        return (wa * breath, (1 - wa) * breath);
    }

    /// <summary>How far a mist layer has scrolled, wrapped at its tile's width (so its loop joins without a seam).</summary>
    public static float MistOffset(double seconds, float speed, float tileWidth) =>
        tileWidth <= 0 ? 0f : (float)((seconds * speed) % tileWidth);

    /// <summary>A glint's progress (0..1) along its path, at most one every <paramref name="every"/> seconds, 0.8 s long; -1 between.</summary>
    public static float Glint(double seconds, float every, float offset, bool still)
    {
        if (still)
        {
            return -1f;
        }

        var t = ((seconds + offset) % every) / 0.8;
        return t is >= 0 and <= 1 ? (float)t : -1f;
    }

    /// <summary>
    /// How far along its track a glint is at <paramref name="seconds"/> (units), or -1 between passes and when still: one
    /// pass of <paramref name="length"/> at <paramref name="speed"/> every <paramref name="every"/> seconds (or as soon as the
    /// last has run its length).
    /// </summary>
    public static float GlintAlong(double seconds, float length, float speed, float every, float offset, bool still)
    {
        if (still || length <= 0 || speed <= 0)
        {
            return -1f;
        }

        var cycle = Math.Max(every, (length / speed) + 1);
        var d = (float)(((seconds + offset) % cycle) * speed);
        return d <= length ? d : -1f;
    }

    /// <summary>A glint's light at <paramref name="along"/> units of its track: rising over its first 24 units and falling over its last.</summary>
    public static float GlintFade(float along, float length) =>
        along < 0 ? 0f : MoonfallColor.Smooth(0, 24, along) * MoonfallColor.Smooth(length, length - 24, along);

    /// <summary>How much of a glint shows at <paramref name="at"/>: none unless its whole light keeps <see cref="PegKeepOut"/> from every piece, full 3 units further.</summary>
    public static float GlintClear(MoonfallClearance clearance, Vector2 at)
    {
        ArgumentNullException.ThrowIfNull(clearance);
        var keep = PegKeepOut + GlintReach;
        return MoonfallFramingCheck.Opening(at.X, at.Y) ? MoonfallColor.Smooth(keep, keep + 3, clearance.At(at.X, at.Y)) : 0f;
    }

    /// <summary>
    /// Dust mote <paramref name="index"/> at <paramref name="seconds"/>: its place over the
    /// opening and its fade (0..1, highest mid-life). Each mote is born at a hashed place and drifts down and right for
    /// one loop; it is drawn only where the beams are and where it keeps <see cref="ParticleKeepOut"/> from every piece.
    /// </summary>
    public static (Vector2 At, float Fade) Dust(int index, double seconds)
    {
        var x0 = 120f + (480f * MoonfallNoise.Lattice(index, 1, 977));
        var y0 = 80f + (340f * MoonfallNoise.Lattice(index, 2, 977));
        var life = (float)(((MoonfallNoise.Lattice(index, 3, 977) + (seconds / Loop)) % 1.0 + 1.0) % 1.0);
        var at = new Vector2(x0, y0) + (DustDrift * (life * Loop));
        var fade = MathF.Sin(life * MathF.PI);
        return (at, fade * fade);
    }

    /// <summary>Whether a dust mote at <paramref name="at"/> may show: inside the opening and <see cref="ParticleKeepOut"/> clear of every piece.</summary>
    public static bool DustVisible(MoonfallClearance clearance, Vector2 at)
    {
        ArgumentNullException.ThrowIfNull(clearance);
        return MoonfallFramingCheck.Opening(at.X, at.Y) && clearance.At(at.X, at.Y) >= ParticleKeepOut + DustRadius;
    }

    /// <summary>Whether a mist band keeps <see cref="PegKeepOut"/> from every piece across the whole board's width.</summary>
    public static bool MistBandClear(MoonfallClearance clearance, float y0, float y1)
    {
        ArgumentNullException.ThrowIfNull(clearance);
        for (var y = (int)MathF.Floor(y0); y < (int)MathF.Ceiling(y1); y++)
        {
            for (var x = 75; x < 725; x++)
            {
                if (clearance.At(x + 0.5f, y + 0.5f) < PegKeepOut)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
