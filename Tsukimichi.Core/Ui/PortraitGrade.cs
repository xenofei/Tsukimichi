using System.Numerics;
using Tsukimichi.Core.Portraits;

namespace Tsukimichi.Core.Ui;

/// <summary>The two portrait grades (1.15 design spec A3): the colour families, and the sepia battle-talk faces.</summary>
public enum PortraitGradeFamily : byte
{
    /// <summary>Duty Support busts and strips, Triple Triad cards, delivery portraits and (later) the portrait pack.</summary>
    Colour,

    /// <summary>The painted sepia battle-talk faces: desaturated further and multiplied a little less.</summary>
    BattleTalk,
}

/// <summary>
/// A 3 × 4 colour matrix in sRGB: each output channel is a weighted sum of the input's red, green and blue plus an
/// offset, all on 0–1. Applied per pixel to a portrait's graded copy (<see cref="PortraitGrade.GradeInPlace"/>).
/// </summary>
public readonly record struct ColorMatrix(
    float Rr, float Rg, float Rb, float Ro,
    float Gr, float Gg, float Gb, float Go,
    float Br, float Bg, float Bb, float Bo)
{
    /// <summary>The matrix applied to one colour (0–1 per channel), unclamped.</summary>
    public Vector3 Apply(Vector3 c) => new(
        (Rr * c.X) + (Rg * c.Y) + (Rb * c.Z) + Ro,
        (Gr * c.X) + (Gg * c.Y) + (Gb * c.Z) + Go,
        (Br * c.X) + (Bg * c.Y) + (Bb * c.Z) + Bo);
}

/// <summary>
/// The giver portraits' night grade (1.15 design spec A3), which makes Duty Support colour, card colour, delivery colour
/// and the sepia battle-talk faces read as one set: a 3 × 4 sRGB colour matrix per family, built from four steps,
/// <list type="number">
/// <item>desaturate toward Rec. 709 luma by <c>d</c>;</item>
/// <item>multiply by the night tint <see cref="NightTintHex"/> at strength <c>m</c>;</item>
/// <item>scale by <see cref="Scale"/>;</item>
/// <item>lift the blacks by Night <see cref="NightHex"/> × <see cref="Lift"/>.</item>
/// </list>
/// The grade is colour normalisation, not ornament, so it applies at every Decoration level (the supervisor's ruling,
/// Q5). A future light palette skips step 2 (<c>nightMultiply: false</c>) and keeps the desaturation and the lift.
/// Until a graded copy lands the source draws with step 2 alone as an image tint (<see cref="Tint"/>), so a bright
/// face never flashes ungraded.
/// </summary>
public static class PortraitGrade
{
    /// <summary>The night tint the multiply goes toward (#2A3768).</summary>
    public const uint NightTintHex = 0x2A3768;

    /// <summary>Night (#0F1424), the colour the blacks are lifted by.</summary>
    public const uint NightHex = 0x0F1424;

    /// <summary>The overall scale after the multiply.</summary>
    public const float Scale = 0.94f;

    /// <summary>How much of Night is added to every pixel.</summary>
    public const float Lift = 0.25f;

    /// <summary>The colour families' desaturation and multiply strength.</summary>
    public const float ColourDesaturation = 0.25f;
    public const float ColourMultiply = 0.22f;

    /// <summary>The battle-talk faces' desaturation and multiply strength.</summary>
    public const float BattleTalkDesaturation = 0.50f;
    public const float BattleTalkMultiply = 0.18f;

    /// <summary>Rec. 709 luma weights.</summary>
    public static readonly Vector3 Luma709 = new(0.2126f, 0.7152f, 0.0722f);

    private static readonly ColorMatrix ColourNight = Build(ColourDesaturation, ColourMultiply, nightMultiply: true);
    private static readonly ColorMatrix ColourLight = Build(ColourDesaturation, ColourMultiply, nightMultiply: false);
    private static readonly ColorMatrix BattleTalkNight = Build(BattleTalkDesaturation, BattleTalkMultiply, nightMultiply: true);
    private static readonly ColorMatrix BattleTalkLight = Build(BattleTalkDesaturation, BattleTalkMultiply, nightMultiply: false);

    /// <summary>The family a portrait source is graded as: battle-talk faces apart, every other family as colour.</summary>
    public static PortraitGradeFamily FamilyOf(PortraitSource source) =>
        source == PortraitSource.BattleTalk ? PortraitGradeFamily.BattleTalk : PortraitGradeFamily.Colour;

    /// <summary>The family's matrix; without <paramref name="nightMultiply"/> (a light palette) step 2 is skipped.</summary>
    public static ColorMatrix For(PortraitGradeFamily family, bool nightMultiply = true) => family switch
    {
        PortraitGradeFamily.BattleTalk => nightMultiply ? BattleTalkNight : BattleTalkLight,
        _ => nightMultiply ? ColourNight : ColourLight,
    };

    /// <summary>
    /// The matrix of the four steps for desaturation <paramref name="desaturation"/> and multiply strength
    /// <paramref name="multiply"/>: rows are (1 − d)·I + d·luma, times the per-channel multiply and the scale, with
    /// Night × <see cref="Lift"/> as the offset.
    /// </summary>
    public static ColorMatrix Build(float desaturation, float multiply, bool nightMultiply = true)
    {
        var tint = nightMultiply ? Tint(multiply) : Vector3.One;
        var keep = 1f - desaturation;
        var l = Luma709 * desaturation;
        var night = ColorMath.FromHex(NightHex);
        Vector3 Row(int channel, float factor)
        {
            var row = l;
            row = channel switch
            {
                0 => row with { X = row.X + keep },
                1 => row with { Y = row.Y + keep },
                _ => row with { Z = row.Z + keep },
            };
            return row * factor * Scale;
        }

        var r = Row(0, tint.X);
        var g = Row(1, tint.Y);
        var b = Row(2, tint.Z);
        return new ColorMatrix(
            r.X, r.Y, r.Z, night.X * Lift,
            g.X, g.Y, g.Z, night.Y * Lift,
            b.X, b.Y, b.Z, night.Z * Lift);
    }

    /// <summary>Step 2 alone, per channel: (1 − m) + m × the night tint. The image tint drawn until the graded copy lands.</summary>
    public static Vector3 Tint(float multiply)
    {
        var t = ColorMath.FromHex(NightTintHex);
        return new Vector3(1f - multiply + (multiply * t.X), 1f - multiply + (multiply * t.Y), 1f - multiply + (multiply * t.Z));
    }

    /// <summary>The image tint of a source's family (spec: .816, .827, .870 for colour; .850, .859, .893 for battle talk); white without the night multiply.</summary>
    public static Vector3 TintFor(PortraitSource source, bool nightMultiply = true) =>
        !nightMultiply ? Vector3.One
        : FamilyOf(source) == PortraitGradeFamily.BattleTalk ? Tint(BattleTalkMultiply) : Tint(ColourMultiply);

    /// <summary>
    /// Grades a <paramref name="width"/> × <paramref name="height"/> straight-alpha image in place (B8G8R8A8 when
    /// <paramref name="bgra"/>, else R8G8B8A8; rows <paramref name="pitch"/> bytes apart) by <paramref name="matrix"/>.
    /// With <paramref name="keep"/> (a delivery portrait's keep mask, row-major, <paramref name="keepWidth"/> wide), the
    /// image's pixel (x, y) is mask pixel (x + <paramref name="keepX"/>, y + <paramref name="keepY"/>), and a pixel the
    /// mask does not keep (or one outside the mask) becomes fully transparent: the emblem script is never drawn.
    /// </summary>
    public static void GradeInPlace(Span<byte> pixels, int width, int height, int pitch, bool bgra, in ColorMatrix matrix,
        ReadOnlySpan<bool> keep = default, int keepWidth = 0, int keepX = 0, int keepY = 0)
    {
        var masked = !keep.IsEmpty && keepWidth > 0;
        var keepHeight = masked ? keep.Length / keepWidth : 0;
        for (var y = 0; y < height; y++)
        {
            var row = y * pitch;
            for (var x = 0; x < width; x++)
            {
                var o = row + (x * 4);
                if (o + 3 >= pixels.Length)
                {
                    return;
                }

                if (masked)
                {
                    int mx = x + keepX, my = y + keepY;
                    if (mx < 0 || my < 0 || mx >= keepWidth || my >= keepHeight || !keep[(my * keepWidth) + mx])
                    {
                        pixels[o] = pixels[o + 1] = pixels[o + 2] = pixels[o + 3] = 0;
                        continue;
                    }
                }

                var (ri, bi) = bgra ? (o + 2, o) : (o, o + 2);
                var c = matrix.Apply(new Vector3(pixels[ri] / 255f, pixels[o + 1] / 255f, pixels[bi] / 255f));
                pixels[ri] = Byte(c.X);
                pixels[o + 1] = Byte(c.Y);
                pixels[bi] = Byte(c.Z);
            }
        }
    }

    /// <summary>
    /// A box-filtered (area-averaged) copy of a 4-byte-per-pixel straight-alpha image at
    /// <paramref name="toWidth"/> × <paramref name="toHeight"/>, tightly packed (pitch = 4 × width). Colour is averaged
    /// premultiplied, so transparent pixels never darken an edge. For the small graded copy the avatars draw: image
    /// textures have one mip level, and a 140 px face drawn at 24 px would otherwise shimmer. Channel order is kept.
    /// </summary>
    public static byte[] Downsample(ReadOnlySpan<byte> pixels, int width, int height, int pitch, int toWidth, int toHeight)
    {
        if (width <= 0 || height <= 0 || toWidth <= 0 || toHeight <= 0)
        {
            return [];
        }

        // Horizontal pass into float rows (premultiplied), then vertical.
        var mid = new float[toWidth * height * 4];
        var sx = (float)width / toWidth;
        for (var y = 0; y < height; y++)
        {
            for (var i = 0; i < toWidth; i++)
            {
                float a0 = i * sx, a1 = (i + 1) * sx;
                float r = 0f, g = 0f, b = 0f, a = 0f;
                for (var x = (int)a0; x < width && x < a1; x++)
                {
                    var w = MathF.Min(a1, x + 1) - MathF.Max(a0, x);
                    if (w <= 0f)
                    {
                        continue;
                    }

                    var o = (y * pitch) + (x * 4);
                    if (o + 3 >= pixels.Length)
                    {
                        continue;
                    }

                    var alpha = pixels[o + 3] / 255f;
                    r += pixels[o] / 255f * alpha * w;
                    g += pixels[o + 1] / 255f * alpha * w;
                    b += pixels[o + 2] / 255f * alpha * w;
                    a += alpha * w;
                }

                var m = ((y * toWidth) + i) * 4;
                mid[m] = r / sx;
                mid[m + 1] = g / sx;
                mid[m + 2] = b / sx;
                mid[m + 3] = a / sx;
            }
        }

        var result = new byte[toWidth * toHeight * 4];
        var sy = (float)height / toHeight;
        for (var j = 0; j < toHeight; j++)
        {
            float b0 = j * sy, b1 = (j + 1) * sy;
            for (var i = 0; i < toWidth; i++)
            {
                float r = 0f, g = 0f, b = 0f, a = 0f;
                for (var y = (int)b0; y < height && y < b1; y++)
                {
                    var w = MathF.Min(b1, y + 1) - MathF.Max(b0, y);
                    if (w <= 0f)
                    {
                        continue;
                    }

                    var m = ((y * toWidth) + i) * 4;
                    r += mid[m] * w;
                    g += mid[m + 1] * w;
                    b += mid[m + 2] * w;
                    a += mid[m + 3] * w;
                }

                a /= sy;
                var o = ((j * toWidth) + i) * 4;
                if (a <= 1e-6f)
                {
                    continue;
                }

                result[o] = Byte(r / sy / a);
                result[o + 1] = Byte(g / sy / a);
                result[o + 2] = Byte(b / sy / a);
                result[o + 3] = Byte(a);
            }
        }

        return result;
    }

    private static byte Byte(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);
}
