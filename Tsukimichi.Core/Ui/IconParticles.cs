using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Core.Ui;

/// <summary>What a moon icon particle is (spec-1.22 H2), so the icon knows how to draw it.</summary>
public enum IconParticleKind : byte
{
    /// <summary>Medallion: a gold mote, r 1 px #FFE9BE with a faint warm halo of its own.</summary>
    Mote = 0,

    /// <summary>Classic: a small round star that breathes; it never moves and is never a cross.</summary>
    Star = 1,

    /// <summary>Ishgard Glass: a 28° frost glint running along the rim, with one round sparkle at its head.</summary>
    Glint = 2,

    /// <summary>Aether Crystal: a shard ringing the icon's foot, flashing (as light) when its face meets the light.</summary>
    Shard = 3,

    /// <summary>Astrologian's Orrery: a bead on a tilted orbit; on the far half it passes behind the icon.</summary>
    Bead = 4,

    /// <summary>Sumi to Kinpaku: a gold-leaf fleck drifting down past the right side, flashing when it tilts to the light.</summary>
    Fleck = 5,
}

/// <summary>
/// One particle at one moment. Positions and sizes are in icon radii from the icon's centre (+Y down), so the icon
/// draws the same at every size and scale.
/// </summary>
/// <param name="Kind">What it is.</param>
/// <param name="X">The centre's offset to the right, in radii (a glint's head).</param>
/// <param name="Y">The centre's offset down, in radii.</param>
/// <param name="Size">Its radius (a mote's, a star's, a bead's) or half-length (a shard's, a fleck's), in radii.</param>
/// <param name="Alpha">Its alpha, never above <see cref="IconParticles.MaxAlpha"/>.</param>
/// <param name="Angle">A glint's centre on the rim, or a shard's or fleck's turn, in radians (screen axes, +Y down).</param>
/// <param name="Flash">How much it catches the light now, 0 to 1 (a shard, a fleck).</param>
/// <param name="Squash">A fleck's height against its width as it tumbles, 0 to 1.</param>
/// <param name="Behind">Drawn before the face, which covers it (the Orrery's bead on its far half).</param>
public readonly record struct IconParticle(
    IconParticleKind Kind,
    float X,
    float Y,
    float Size,
    float Alpha,
    float Angle = 0f,
    float Flash = 0f,
    float Squash = 1f,
    bool Behind = false);

/// <summary>
/// The moon icon's theme particles (feature plan v8 H2; spec-1.22 H2, icon-particles-1.22.png), as pure functions of
/// time, so the icon draws them with no allocation and the tests hold them to the spec's limits: Full only with Reduce
/// motion off, at most <see cref="Max"/> at once, never above <see cref="MaxAlpha"/>, never further than
/// <see cref="MaxReach"/> radii from the centre, and lit from the upper left. The recipes are the mock's
/// (<c>docs/design/v8/mock-src/v722.js</c>, <c>fx22</c>), in its 64-unit box where the icon's radius is 32.
/// <list type="bullet">
/// <item>Medallion: 3 gold motes, each 3.6 s (fade in 0.6 s, rise about 3 px/s with a slight sway, fade out 1.2 s), staggered 1.2 s.</item>
/// <item>Classic: 3 fixed stars breathing on 7, 9.5 and 12 s (the 1.14 twinkle curve), .27 to .49.</item>
/// <item>Ishgard Glass: every 6 s a 28° glint runs the lit upper-left quarter of the rim in 1.2 s.</item>
/// <item>Aether Crystal: 2 shards on a 16 s orbit round the icon's foot (rx 1.25 R, ry 0.45 R, centred 0.58 R below the
/// centre); a shard shows only where the icon does not cover it, so none is ever drawn over the face. Each turns every
/// 3.2 s and flashes for about 0.4 s when its face meets the light.</item>
/// <item>Astrologian's Orrery: 1 bead on a 12 s orbit (rx 1.3 R, ry 0.4 R, tilted 28°), behind the icon on its far half;
/// the orbit's hairline is at <see cref="OrbitAlpha"/>.</item>
/// <item>Sumi to Kinpaku: 2 gold-leaf flecks, 4.5 s each, drifting down past the right side with a flutter.</item>
/// </list>
/// </summary>
public static class IconParticles
{
    /// <summary>The most particles at once.</summary>
    public const int Max = 3;

    /// <summary>The highest alpha any particle draws at.</summary>
    public const float MaxAlpha = 0.55f;

    /// <summary>The furthest a particle's centre goes from the icon's centre, in icon radii.</summary>
    public const float MaxReach = 1.4f;

    /// <summary>The face's radius in icon radii: the well inside the rim (27 of the mock's 32). No shard is drawn over it.</summary>
    public const float FaceRadius = 27f / Unit;

    /// <summary>The Orrery orbit hairline's alpha.</summary>
    public const float OrbitAlpha = 0.14f;

    /// <summary>The Orrery orbit's half-width in icon radii.</summary>
    public const float OrbitRx = 42f / Unit;

    /// <summary>The Orrery orbit's half-height in icon radii.</summary>
    public const float OrbitRy = 13f / Unit;

    /// <summary>The Orrery orbit's tilt, in radians (28°, its right end up).</summary>
    public const float OrbitTilt = -28f * MathF.PI / 180f;

    /// <summary>A frost glint's half-span along the rim, in radians (14°, so 28° in all).</summary>
    public const float GlintHalfSpan = 14f * MathF.PI / 180f;

    /// <summary>The rim radius a glint runs along, in icon radii.</summary>
    public const float GlintRadius = 30.4f / Unit;

    // The mock's box: 64 units across, the icon's radius 32.
    private const float Unit = 32f;

    private const double MoteLife = 3.6;
    private const double MoteStagger = 1.2;
    private const double GlintPeriod = 6.0;
    private const double GlintRun = 1.2;
    private const double ShardOrbit = 16.0;
    private const double ShardTurn = 3.2;
    private const double BeadOrbit = 12.0;
    private const double FleckLife = 4.5;

    // Classic's three stars: x and y in the mock's box, radius, breathing period and phase.
    private static readonly (float X, float Y, float R, float Period, float Phase)[] Stars =
    [
        (-6f, 10f, 1.1f, 7f, 0f),
        (70f, 18f, 1.3f, 9.5f, 2f),
        (64f, 62f, 0.9f, 12f, 5f),
    ];

    /// <summary>Whether the icon draws particles at all: at Full, with Reduce motion off (Quiet and Plain draw none).</summary>
    public static bool Enabled(Flair flair, bool reduceMotion) => flair == Flair.Full && !reduceMotion;

    /// <summary>Whether <paramref name="theme"/> draws the Orrery's orbit hairline with its bead.</summary>
    public static bool HasOrbit(ThemeId theme) => theme == ThemeId.Orrery;

    /// <summary>
    /// Writes <paramref name="theme"/>'s particles at <paramref name="seconds"/> (the icon's own clock, which runs only
    /// while it shows) into <paramref name="into"/> and returns how many: none unless <see cref="Enabled"/>, and never more
    /// than <see cref="Max"/> or the span's length. Allocates nothing.
    /// </summary>
    public static int At(ThemeId theme, Flair flair, bool reduceMotion, double seconds, Span<IconParticle> into)
    {
        if (!Enabled(flair, reduceMotion) || into.IsEmpty || !double.IsFinite(seconds))
        {
            return 0;
        }

        var t = Math.Max(0.0, seconds);
        var count = theme switch
        {
            ThemeId.Medallion => Motes(t, into),
            ThemeId.Classic => StarsAt(t, into),
            ThemeId.IshgardGlass => Glint(t, into),
            ThemeId.AetherCrystal => Shards(t, into),
            ThemeId.Orrery => Bead(t, into),
            ThemeId.Sumi => Flecks(t, into),
            _ => 0,
        };
        return Math.Min(count, Max);
    }

    /// <summary>A point on the Orrery's orbit at <paramref name="angle"/> (radians), in icon radii from the centre.</summary>
    public static (float X, float Y) OrbitPoint(float angle)
    {
        var ox = OrbitRx * MathF.Cos(angle);
        var oy = OrbitRy * MathF.Sin(angle);
        var (sin, cos) = MathF.SinCos(OrbitTilt);
        return ((ox * cos) - (oy * sin), (ox * sin) + (oy * cos));
    }

    private static int Motes(double t, Span<IconParticle> into)
    {
        var n = 0;
        for (var i = 0; i < 3 && n < into.Length; i++)
        {
            var s = t + (i * MoteStagger);
            var cycle = Math.Floor(s / MoteLife);
            var u = (float)((s - (cycle * MoteLife)) / MoteLife);
            var a0 = (200f + (140f * Hash(i, cycle))) * MathF.PI / 180f;
            var r0 = 27f + (9f * Hash(i + 7, cycle));
            var life = (float)MoteLife;
            var x = (r0 * MathF.Cos(a0)) + (1.6f * u * life) + (1.5f * MathF.Sin(u * 5f));
            var y = (-r0 * MathF.Sin(a0) * 0.2f) + 18f - (4.5f * u * life);
            var alpha = MaxAlpha * Envelope(u, 0.17f, 0.33f);
            into[n++] = new IconParticle(IconParticleKind.Mote, x / Unit, y / Unit, 1.05f / Unit, Cap(alpha));
        }

        return n;
    }

    private static int StarsAt(double t, Span<IconParticle> into)
    {
        var n = 0;
        foreach (var star in Stars)
        {
            if (n >= into.Length)
            {
                break;
            }

            var phase = (float)(((t + star.Phase) % star.Period) / star.Period);
            var curve = phase < 0.3f ? phase / 0.3f : phase < 0.7f ? 1f - ((phase - 0.3f) / 0.4f * 2f) : -1f + ((phase - 0.7f) / 0.3f);
            var alpha = 0.38f * (1f + (0.28f * curve));
            into[n++] = new IconParticle(IconParticleKind.Star, (star.X - Unit) / Unit, (star.Y - Unit) / Unit, star.R / Unit, Cap(alpha));
        }

        return n;
    }

    private static int Glint(double t, Span<IconParticle> into)
    {
        var u = (float)((t % GlintPeriod) / GlintRun);
        if (u > 1f)
        {
            return 0;
        }

        // From 195° to 255° on the screen's clock (+Y down): the lit upper-left quarter of the rim.
        var centre = (195f + (60f * u)) * MathF.PI / 180f;
        var head = centre + GlintHalfSpan;
        var alpha = MathF.Sin(MathF.PI * u) * MaxAlpha;
        into[0] = new IconParticle(IconParticleKind.Glint, GlintRadius * MathF.Cos(head), GlintRadius * MathF.Sin(head), 1f / Unit, Cap(alpha), centre);
        return 1;
    }

    private static int Shards(double t, Span<IconParticle> into)
    {
        var n = 0;
        for (var i = 0; i < 2 && n < into.Length; i++)
        {
            var angle = (float)(t / ShardOrbit * 2.0 * Math.PI) + (i * MathF.PI) + 0.6f;
            // rx 1.25 R and ry 0.45 R; centred 0.58 R below the centre (the spec's 0.6 R, nudged so the orbit's widest
            // point stays within MaxReach).
            var x = 40f * MathF.Cos(angle);
            var y = 18.56f + (14.4f * MathF.Sin(angle));
            var distance = MathF.Sqrt((x * x) + (y * y));

            // The near half passes in front, below the rim; on the far half the icon covers a shard until it is clear of
            // the rim, so no shard is ever drawn over the face, let alone the lit crescent.
            var visible = MathF.Sin(angle) >= 0f ? 1f : Math.Clamp((distance - 33f) / 4f, 0f, 1f);
            if (visible <= 0f)
            {
                continue;
            }

            var turn = (float)(((t / ShardTurn * 360.0) + (i * 110.0)) % 360.0);
            var flash = MathF.Pow(MathF.Max(0f, MathF.Cos((turn - 315f) * MathF.PI / 180f)), 6f);
            var rotation = ((turn * 0.25f) - 20f) * MathF.PI / 180f;
            into[n++] = new IconParticle(IconParticleKind.Shard, x / Unit, y / Unit, 4.2f / Unit, Cap(visible * MaxAlpha), rotation, flash);
        }

        return n;
    }

    private static int Bead(double t, Span<IconParticle> into)
    {
        var angle = (float)(t / BeadOrbit * 2.0 * Math.PI) + 0.3f;
        var (x, y) = OrbitPoint(angle);
        into[0] = new IconParticle(IconParticleKind.Bead, x, y, 1.7f / Unit, MaxAlpha, Behind: MathF.Sin(angle) < 0f);
        return 1;
    }

    private static int Flecks(double t, Span<IconParticle> into)
    {
        var n = 0;
        for (var i = 0; i < 2 && n < into.Length; i++)
        {
            var s = t + (i * (FleckLife * 0.5));
            var cycle = Math.Floor(s / FleckLife);
            var u = (float)((s - (cycle * FleckLife)) / FleckLife);

            // Down past the right side, kept within MaxReach: the mock's drift, started a little lower and nearer in.
            var x = 13f + (9f * Hash(i, cycle)) + (2.5f * MathF.Sin((u * 7f) + i));
            var y = -36f + (64f * u);
            var tilt = MathF.Sin((u * 9f) + (i * 2f)) * 60f;
            var flash = MathF.Pow(MathF.Max(0f, MathF.Cos((tilt + 40f) * MathF.PI / 180f)), 8f);
            var squash = 0.45f + (0.55f * MathF.Abs(MathF.Cos(tilt * MathF.PI / 90f)));
            var alpha = MaxAlpha * Envelope(u, 0.15f, 0.25f);
            into[n++] = new IconParticle(IconParticleKind.Fleck, x / Unit, y / Unit, 1.9f / Unit, Cap(alpha), tilt * MathF.PI / 180f, flash, squash);
        }

        return n;
    }

    /// <summary>Fades in over the first <paramref name="fadeIn"/> of a life and out over its last <paramref name="fadeOut"/>.</summary>
    private static float Envelope(float u, float fadeIn, float fadeOut) =>
        u < fadeIn ? u / fadeIn : u > 1f - fadeOut ? MathF.Max(0f, (1f - u) / fadeOut) : 1f;

    /// <summary>A steady pseudo-random 0 to 1 for a particle and its cycle (the mock's hash), so a cycle never changes mid-flight.</summary>
    private static float Hash(int i, double cycle)
    {
        var x = Math.Sin((i * 127.1) + (cycle * 311.7)) * 43758.5453;
        return (float)(x - Math.Floor(x));
    }

    private static float Cap(float alpha) => float.IsFinite(alpha) ? Math.Clamp(alpha, 0f, MaxAlpha) : 0f;
}
