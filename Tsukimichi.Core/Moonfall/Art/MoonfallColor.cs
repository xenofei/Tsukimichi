using System.Globalization;
using System.Numerics;

namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>
/// The colour arithmetic of the Moonfall grades, ported from the design kit (docs/design/v9/rich/src/rich_lib.py and
/// v8/art/src/artlib.py) so the runtime grade matches the approved renders: OKLab with Björn Ottosson's matrices in
/// float, <c>smooth</c> (smoothstep with either edge order), <c>screen</c>, <c>ramp</c> (numpy's <c>interp</c> per
/// channel) and <c>#RRGGBB</c> colours. sRGB transfer is a fine table with linear interpolation (error under 1e-4).
/// </summary>
public static class MoonfallColor
{
    private const int TableSize = 4096;
    private static readonly float[] ToLinearTable = BuildTable(static c => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f));
    private static readonly float[] ToSrgbTable = BuildTable(static l => l <= 0.0031308f ? l * 12.92f : (1.055f * MathF.Pow(l, 1f / 2.4f)) - 0.055f);

    private static float[] BuildTable(Func<float, float> f)
    {
        var t = new float[TableSize + 1];
        for (var i = 0; i <= TableSize; i++)
        {
            t[i] = f(i / (float)TableSize);
        }

        return t;
    }

    private static float Lookup(float[] table, float v)
    {
        v = Math.Clamp(v, 0f, 1f) * TableSize;
        var i = (int)v;
        if (i >= TableSize)
        {
            return table[TableSize];
        }

        var f = v - i;
        return table[i] + ((table[i + 1] - table[i]) * f);
    }

    /// <summary>sRGB (0..1, clipped) to linear light.</summary>
    public static float ToLinear(float c) => Lookup(ToLinearTable, c);

    /// <summary>Linear light (clipped to 0..1) to sRGB.</summary>
    public static float ToSrgb(float l) => Lookup(ToSrgbTable, l);

    /// <summary>An sRGB colour (each channel clipped to 0..1) in OKLab (L, a, b).</summary>
    public static Vector3 ToOklab(float r, float g, float b)
    {
        var lr = ToLinear(r);
        var lg = ToLinear(g);
        var lb = ToLinear(b);
        var l = MathF.Cbrt(MathF.Max((0.4122214708f * lr) + (0.5363325363f * lg) + (0.0514459929f * lb), 0f));
        var m = MathF.Cbrt(MathF.Max((0.2119034982f * lr) + (0.6806995451f * lg) + (0.1073969566f * lb), 0f));
        var s = MathF.Cbrt(MathF.Max((0.0883024619f * lr) + (0.2817188376f * lg) + (0.6299787005f * lb), 0f));
        return new Vector3(
            (0.2104542553f * l) + (0.7936177850f * m) - (0.0040720468f * s),
            (1.9779984951f * l) - (2.4285922050f * m) + (0.4505937099f * s),
            (0.0259040371f * l) + (0.7827717662f * m) - (0.8086757660f * s));
    }

    /// <inheritdoc cref="ToOklab(float, float, float)"/>
    public static Vector3 ToOklab(Vector3 rgb) => ToOklab(rgb.X, rgb.Y, rgb.Z);

    /// <summary>An OKLab colour in sRGB, its linear light clipped to 0..1 first (as the design's <c>oklab_to_srgb</c>).</summary>
    public static Vector3 ToSrgb(float l, float a, float b)
    {
        var lp = l + (0.3963377774f * a) + (0.2158037573f * b);
        var mp = l - (0.1055613458f * a) - (0.0638541728f * b);
        var sp = l - (0.0894841775f * a) - (1.2914855480f * b);
        lp = lp * lp * lp;
        mp = mp * mp * mp;
        sp = sp * sp * sp;
        var r = (4.0767416621f * lp) - (3.3077115913f * mp) + (0.2309699292f * sp);
        var g = (-1.2684380046f * lp) + (2.6097574011f * mp) - (0.3413193965f * sp);
        var bb = (-0.0041960863f * lp) - (0.7034186147f * mp) + (1.7076147010f * sp);
        return new Vector3(ToSrgb(r), ToSrgb(g), ToSrgb(bb));
    }

    /// <summary>A <c>#RRGGBB</c> colour as 0..1 sRGB; throws on anything else (recipes are checked by their loader first).</summary>
    public static Vector3 Hex(string hex)
    {
        if (!TryHex(hex, out var c))
        {
            throw new FormatException($"Not a #RRGGBB colour: {hex}");
        }

        return c;
    }

    /// <summary>A <c>#RRGGBB</c> colour as 0..1 sRGB.</summary>
    public static bool TryHex(string? hex, out Vector3 colour)
    {
        colour = default;
        if (hex is not { Length: 7 } || hex[0] != '#'
            || !uint.TryParse(hex.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
        {
            return false;
        }

        colour = new Vector3(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
        return true;
    }

    /// <summary>The design kit's <c>smooth</c>: smoothstep from <paramref name="e0"/> to <paramref name="e1"/> (either may be the larger).</summary>
    public static float Smooth(float e0, float e1, float x)
    {
        var t = Math.Clamp((x - e0) / (e1 - e0), 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    /// <summary>Screen: <c>1 - (1 - a)(1 - clip(b))</c>.</summary>
    public static float Screen(float a, float b) => 1f - ((1f - a) * (1f - Math.Clamp(b, 0f, 1f)));

    /// <summary>A colour on <paramref name="stops"/> (positions ascending) at <paramref name="t"/>, clipped to 0..1.</summary>
    public static Vector3 Ramp(float t, ReadOnlySpan<(float At, Vector3 Colour)> stops)
    {
        t = Math.Clamp(t, 0f, 1f);
        if (t <= stops[0].At)
        {
            return stops[0].Colour;
        }

        for (var i = 1; i < stops.Length; i++)
        {
            if (t <= stops[i].At)
            {
                var span = stops[i].At - stops[i - 1].At;
                var f = span > 0 ? (t - stops[i - 1].At) / span : 1f;
                return Vector3.Lerp(stops[i - 1].Colour, stops[i].Colour, f);
            }
        }

        return stops[^1].Colour;
    }

    /// <summary>numpy's <c>interp</c>: <paramref name="x"/> on the polyline (<paramref name="xs"/>, <paramref name="ys"/>), held flat past its ends.</summary>
    public static float Interp(float x, ReadOnlySpan<float> xs, ReadOnlySpan<float> ys)
    {
        if (x <= xs[0])
        {
            return ys[0];
        }

        for (var i = 1; i < xs.Length; i++)
        {
            if (x <= xs[i])
            {
                var span = xs[i] - xs[i - 1];
                return span > 0 ? ys[i - 1] + ((ys[i] - ys[i - 1]) * (x - xs[i - 1]) / span) : ys[i];
            }
        }

        return ys[^1];
    }

    /// <summary>Rec. 709 luma of sRGB values (the design's <c>LUM</c>).</summary>
    public static float Luma(float r, float g, float b) => (0.2126f * r) + (0.7152f * g) + (0.0722f * b);

    /// <summary>The unit (a, b) direction of a colour's OKLab hue (the design's <c>_dir</c>).</summary>
    public static Vector2 HueDirection(Vector3 rgb)
    {
        var lab = ToOklab(rgb);
        var n = MathF.Sqrt((lab.Y * lab.Y) + (lab.Z * lab.Z)) + 1e-6f;
        return new Vector2(lab.Y / n, lab.Z / n);
    }
}
