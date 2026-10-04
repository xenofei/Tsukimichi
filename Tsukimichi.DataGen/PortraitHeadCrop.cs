namespace Tsukimichi.DataGen;

/// <summary>
/// Finds the head in a Garland Tools NPC photo (a front-facing, full-body render on transparency) and frames it by the
/// plugin's portrait rule, sitting high in its bands as spec-1.20 F4 asks of pack faces (eye line 43 %, chin 83 % of the
/// square, inside 1.15 A2's 42–46 % and 78–84 %), feature plan v7 F4.
/// <para>
/// The figure runs from the first to the last row with alpha. The head's height is a share of the figure's by race
/// (<see cref="HeadShare"/>: about a seventh for the tall races, a quarter for Lalafell). The crown is the first row as
/// wide as half a head is tall, so a raised weapon, a hat's peak or long ears above it do not count. The chin is the neck:
/// the narrowest row of the figure from mid-face to the upper chest, where the head meets the shoulders. The eyes are
/// about half a head above the chin, and the face's centre is the centroid of the opaque pixels between them. A frame
/// whose middle is mostly empty (it caught a weapon, a hat or ears) is dropped (<see cref="MinFilled"/>). Measured on
/// the contact sheets (<c>sheets/</c>); a frame that misses shows there, and <c>--skip</c> leaves it out.
/// </para>
/// </summary>
internal static class PortraitHeadCrop
{
    private const int Opaque = 40;

    /// <summary>The head's height as a share of the figure's, by ENpcBase race (1 Hyur … 8 Viera; 0 not a playable race).</summary>
    public static double HeadShare(byte race) => race switch
    {
        2 => 0.125, // Elezen
        3 => 0.25, // Lalafell
        5 => 0.12, // Roegadyn
        7 => 0.125, // Hrothgar
        8 => 0.13, // Viera (the ears are measured apart: see the neck band)
        0 => 0.2,
        _ => 0.135, // Hyur, Miqo'te, Au Ra
    };

    /// <summary>Where the eye line and the chin sit in the square (spec-1.20 F4: high in the 1.15 bands).</summary>
    public const double EyeLine = 0.43;

    /// <inheritdoc cref="EyeLine"/>
    public const double ChinLine = 0.83;

    /// <summary>
    /// The head framed in a <paramref name="side"/> px square (straight-alpha RGBA); null when no figure is found.
    /// <paramref name="box"/> is the square's side in the photo's own pixels (the pack keeps only boxes of 72 px and up,
    /// so a 72 px plate never upscales, and the hover never shows a face larger than it).
    /// </summary>
    public static byte[]? Crop(byte[] rgba, int width, int height, byte race, int side, out int box)
    {
        box = 0;
        var counts = new int[height];
        var top = -1;
        var bottom = -1;
        for (var y = 0; y < height; y++)
        {
            var n = 0;
            for (var x = 0; x < width; x++)
            {
                if (rgba[(((y * width) + x) * 4) + 3] >= Opaque)
                {
                    n++;
                }
            }

            counts[y] = n;
            if (n >= 3)
            {
                top = top < 0 ? y : top;
                bottom = y;
            }
        }

        if (top < 0 || bottom - top < 40)
        {
            return null;
        }

        var figure = bottom - top + 1;
        var head = HeadShare(race) * figure;
        double Smooth(int y) => (counts[Math.Max(top, y - 1)] + counts[y] + counts[Math.Min(bottom, y + 1)]) / 3d;

        // The crown: the first row as wide as half a head is tall (a head is about 0.7 as wide), so a spear tip, a
        // hat's peak or a Viera's ears above the head do not count.
        var crown = top;
        while (crown < bottom && Smooth(crown) < 0.55 * head)
        {
            crown++;
        }

        // The neck: the narrowest row from mid-face to the upper chest, where the head meets the shoulders.
        var from = crown + (int)(head * 0.45);
        var to = Math.Min(bottom - 1, crown + (int)(head * 1.6));
        var neck = from;
        var narrowest = double.MaxValue;
        for (var y = from; y <= to; y++)
        {
            var w = Smooth(y);
            if (w < narrowest)
            {
                narrowest = w;
                neck = y;
            }
        }

        // A Viera's neck is often under a scarf or long hair: their chin is measured from where the ears pass the crown
        // rule instead (about half way up the ears; the face below them).
        var chin = race == 8 ? crown + (1.3 * head) : neck;
        var eyes = chin - (0.48 * head);

        // The face's centre: the centroid of the opaque pixels from just above the eyes to the chin.
        double sum = 0, weight = 0;
        for (var y = Math.Max(0, (int)(eyes - (0.25 * head))); y <= Math.Min(height - 1, (int)chin); y++)
        {
            for (var x = 0; x < width; x++)
            {
                var a = rgba[(((y * width) + x) * 4) + 3];
                if (a >= Opaque)
                {
                    sum += x * (double)a;
                    weight += a;
                }
            }
        }

        if (weight <= 0)
        {
            return null;
        }

        var centreX = sum / weight;
        var square = (chin - eyes) / (ChinLine - EyeLine);
        var boxTop = eyes - (EyeLine * square);
        var boxLeft = centreX - (square / 2);
        box = (int)Math.Round(square);
        var crop = Resample(rgba, width, height, boxLeft, boxTop, square, side);

        // A face fills the middle of the plate. A frame that caught a raised weapon, a hat's peak or a pair of ears
        // leaves it mostly empty: no crop, so the giver keeps game art or a fallback rather than a wrong picture.
        return Filled(crop, side) >= MinFilled ? crop : null;
    }

    /// <summary>The share of the plate's middle (a disc of 35 % of the side) a face must cover.</summary>
    public const double MinFilled = 0.5;

    /// <summary>The opaque share of the disc of radius 0.35 × <paramref name="side"/> at the square's centre.</summary>
    public static double Filled(byte[] rgba, int side)
    {
        var r = side * 0.35;
        var c = (side - 1) / 2d;
        int inside = 0, opaque = 0;
        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                if (((x - c) * (x - c)) + ((y - c) * (y - c)) > r * r)
                {
                    continue;
                }

                inside++;
                opaque += rgba[(((y * side) + x) * 4) + 3] >= 128 ? 1 : 0;
            }
        }

        return inside == 0 ? 0 : (double)opaque / inside;
    }

    /// <summary>
    /// The square <paramref name="left"/>, <paramref name="top"/>, <paramref name="box"/> (source px; may run past the
    /// edges, which are transparent) at <paramref name="side"/> px: each output pixel the premultiplied average of a 4 × 4
    /// grid of bilinear taps over its footprint, so it scales up or down without fringes.
    /// </summary>
    private static byte[] Resample(byte[] rgba, int width, int height, double left, double top, double box, int side)
    {
        var output = new byte[side * side * 4];
        var step = box / side;
        const int Taps = 4;
        for (var oy = 0; oy < side; oy++)
        {
            for (var ox = 0; ox < side; ox++)
            {
                double r = 0, g = 0, b = 0, a = 0;
                for (var ty = 0; ty < Taps; ty++)
                {
                    for (var tx = 0; tx < Taps; tx++)
                    {
                        var sx = left + ((ox + ((tx + 0.5) / Taps)) * step) - 0.5;
                        var sy = top + ((oy + ((ty + 0.5) / Taps)) * step) - 0.5;
                        Sample(rgba, width, height, sx, sy, ref r, ref g, ref b, ref a);
                    }
                }

                var o = ((oy * side) + ox) * 4;
                a /= Taps * Taps;
                if (a > 0.5)
                {
                    output[o] = (byte)Math.Clamp(Math.Round(r / (Taps * Taps) / a * 255), 0, 255);
                    output[o + 1] = (byte)Math.Clamp(Math.Round(g / (Taps * Taps) / a * 255), 0, 255);
                    output[o + 2] = (byte)Math.Clamp(Math.Round(b / (Taps * Taps) / a * 255), 0, 255);
                    output[o + 3] = (byte)Math.Clamp(Math.Round(a), 0, 255);
                }
            }
        }

        return output;
    }

    /// <summary>Adds the bilinear premultiplied sample at (<paramref name="x"/>, <paramref name="y"/>): colour 0–1 times alpha 0–255.</summary>
    private static void Sample(byte[] rgba, int width, int height, double x, double y, ref double r, ref double g, ref double b, ref double a)
    {
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var fx = x - x0;
        var fy = y - y0;
        for (var j = 0; j < 2; j++)
        {
            for (var i = 0; i < 2; i++)
            {
                var px = x0 + i;
                var py = y0 + j;
                if (px < 0 || py < 0 || px >= width || py >= height)
                {
                    continue;
                }

                var w = (i == 0 ? 1 - fx : fx) * (j == 0 ? 1 - fy : fy);
                var o = ((py * width) + px) * 4;
                var alpha = rgba[o + 3] * w;
                r += rgba[o] / 255d * alpha;
                g += rgba[o + 1] / 255d * alpha;
                b += rgba[o + 2] / 255d * alpha;
                a += alpha;
            }
        }
    }
}
