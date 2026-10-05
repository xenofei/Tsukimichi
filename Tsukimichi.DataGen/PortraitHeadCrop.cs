namespace Tsukimichi.DataGen;

/// <summary>
/// A square in a Garland photo's own pixels: <paramref name="Left"/>, <paramref name="Top"/> and <paramref name="Side"/>
/// (fractional; the pack resamples it to <see cref="Core.Portraits.PortraitPackManifest.ImageSide"/> px).
/// </summary>
internal readonly record struct PhotoBox(double Left, double Top, double Side)
{
    /// <summary>
    /// This box, or one of <paramref name="min"/> px when it is smaller: the eye line and the centre stay where they are
    /// and the face simply sits smaller in the square (the 72 px plate never upscales: spec-1.20 F4, <c>MinBox</c>).
    /// </summary>
    public PhotoBox AtLeast(double min)
    {
        if (Side >= min)
        {
            return this;
        }

        var eyes = Top + (PortraitHeadCrop.EyeLine * Side);
        var centre = Left + (Side / 2);
        return new PhotoBox(centre - (min / 2), eyes - (PortraitHeadCrop.EyeLine * min), min);
    }

    /// <summary>Whether the square lies wholly inside a <paramref name="width"/> × <paramref name="height"/> photo.</summary>
    public bool Inside(int width, int height) =>
        Side > 0 && Left >= 0 && Top >= 0 && Left + Side <= width + 1e-6 && Top + Side <= height + 1e-6;
}

/// <summary>
/// Finds the head in a Garland Tools NPC photo (a front-facing, full-body render on transparency) and frames it by the
/// plugin's portrait rule: eye line at 44 % of the square, chin at 81 % (the A2 framing the game-art crops use,
/// <c>PortraitFraming</c>), feature plan v7 F4 and the portrait audit's change C3.
/// <para>
/// Landmarks, all from the silhouette (the opaque pixels, alpha 40 and up):
/// <list type="bullet">
/// <item><b>Axis:</b> the median column of the figure's middle (35–60 % of its height: hips and thighs), where a weapon
/// held to one side barely counts.</item>
/// <item><b>Crown:</b> the top of the head. Each row splits into runs of opaque pixels (gaps of up to 2 px close); the
/// run on or nearest the axis is the head's candidate. The crown is the first row whose candidate is at least 0.35 of a
/// head wide and centred within 0.4 of a head of the axis for 12 % of a head running (at least 3 rows), and which
/// connects downward, through overlapping runs, to a mass at least 0.55 of a head wide within 0.6 of a head. A raised
/// staff, lance or scythe is off the axis, thin, or does not join a head-wide mass so soon; a lance beside the head is
/// another run; Viera ears and plumes are too narrow. The figure's height, and so the head's (<see cref="HeadShare"/>),
/// is then measured from the crown, not from a weapon's tip, and the crown found again with that head.</item>
/// <item><b>Neck:</b> the narrowest row from mid-face to the upper chest (row counts, smoothed over 3 rows).</item>
/// <item><b>Eyes:</b> mostly <see cref="Race.EyeBelowCrown"/> below the crown, nudged by the neck
/// (<see cref="Race.EyeAboveNeck"/>, weight <see cref="Race.NeckWeight"/>); the chin <see cref="Race.FaceSpan"/> below
/// the eyes; the face's centre the centroid of the axis runs from just above the eyes to the chin.</item>
/// </list>
/// The race constants are the medians measured on 1,262 Garland photos against the portrait audit's eye and chin
/// landmarks (<c>docs/research/portrait-audit/reconciled/reconciled.json</c>, the owner's boxes where he set one): the
/// eye line's median comes to 0.44 (it was 0.574 when the chin was taken at the neck's narrowest row) and the chin's to
/// 0.82 (was 0.87). A frame whose middle is mostly empty (no head in it) is dropped (<see cref="MinFilled"/>); a photo the
/// rule still misses gets a per-NPC box (<see cref="PortraitPackOverrides"/>).
/// </para>
/// </summary>
internal static class PortraitHeadCrop
{
    private const int Opaque = 40;

    /// <summary>Where the eye line sits in the square (A2: 0.42–0.46).</summary>
    public const double EyeLine = 0.44;

    /// <summary>Where the chin sits in the square (A2: about 0.78–0.84).</summary>
    public const double ChinLine = 0.81;

    /// <summary>The share of the plate's middle (a disc of 35 % of the side) a face must cover.</summary>
    public const double MinFilled = 0.5;

    /// <summary>
    /// Per-race constants, in heads (<see cref="HeadShare"/> of the figure from the crown down): how far the eyes sit
    /// below the crown and above the neck, how much the neck counts, and the eye-to-chin span.
    /// </summary>
    public readonly record struct Race(double HeadShare, double EyeBelowCrown, double EyeAboveNeck, double NeckWeight, double FaceSpan);

    /// <summary>The constants by ENpcBase race (1 Hyur … 8 Viera; 0 not a playable race).</summary>
    public static Race For(byte race) => race switch
    {
        2 => new(0.125, 0.50, 0.30, 0.2, 0.37), // Elezen
        3 => new(0.25, 0.66, 0.29, 0.2, 0.33), // Lalafell
        4 => new(0.135, 0.58, 0.31, 0.2, 0.37), // Miqo'te (the ears sit above the crown more often than not)
        5 => new(0.12, 0.61, 0.23, 0.2, 0.47), // Roegadyn
        6 => new(0.135, 0.57, 0.35, 0.2, 0.38), // Au Ra
        7 => new(0.125, 0.72, 0, 0, 0.59), // Hrothgar: the mane hides the neck
        8 => new(0.13, 0.53, 0, 0, 0.39), // Viera: a scarf or long hair hides the neck
        0 => new(0.2, 0.53, 0.31, 0.2, 0.38), // not a playable race: Hyur's face in an unknown body
        _ => new(0.135, 0.53, 0.31, 0.2, 0.38), // Hyur
    };

    /// <summary>The head's height as a share of the figure's, by ENpcBase race.</summary>
    public static double HeadShare(byte race) => For(race).HeadShare;

    /// <summary>
    /// The head <paramref name="found"/> (<see cref="Find"/>) framed in a <paramref name="side"/> px square
    /// (straight-alpha RGBA); null when the frame's middle is mostly empty. <paramref name="box"/> is the square in the
    /// photo's own pixels, held at <paramref name="minBox"/> px at least (<see cref="PhotoBox.AtLeast"/>).
    /// </summary>
    public static byte[]? Crop(byte[] rgba, int width, int height, PhotoBox found, int side, double minBox, out PhotoBox box)
    {
        box = found.AtLeast(minBox);
        var crop = Render(rgba, width, height, box, side);

        // A face fills the middle of the plate. A frame that caught a raised weapon, a hat's peak or a pair of ears
        // leaves it mostly empty: no crop, so the giver keeps game art or a fallback rather than a wrong picture.
        return Filled(crop, side) >= MinFilled ? crop : null;
    }

    /// <summary>The head's square by the framing rule, in the photo's pixels; null when the photo holds no head.</summary>
    public static PhotoBox? Find(byte[] rgba, int width, int height, byte race)
    {
        var counts = new int[height];
        var runs = new List<(int Start, int End)>[height];
        var top = -1;
        var bottom = -1;
        for (var y = 0; y < height; y++)
        {
            runs[y] = Runs(rgba, width, y, out counts[y]);
            if (counts[y] >= 3)
            {
                top = top < 0 ? y : top;
                bottom = y;
            }
        }

        if (top < 0 || bottom - top < 40)
        {
            return null;
        }

        var axis = Axis(rgba, width, top, bottom);
        var constants = For(race);

        // The crown, then again with the head measured from it (a weapon's tip above the head made the figure too tall).
        var crown = Crown(runs, top, bottom, axis, constants.HeadShare * (bottom - top + 1));
        if (crown is not { } found)
        {
            return null;
        }

        for (var pass = 0; pass < 2; pass++)
        {
            if (Crown(runs, top, bottom, axis, constants.HeadShare * (bottom - found + 1)) is { } again)
            {
                found = again;
            }
        }

        var head = constants.HeadShare * (bottom - found + 1);
        double Smooth(int y) => (counts[Math.Max(top, y - 1)] + counts[y] + counts[Math.Min(bottom, y + 1)]) / 3d;

        // The neck: the narrowest row from mid-face to the upper chest, where the head meets the shoulders.
        var from = found + (int)(head * 0.45);
        var to = Math.Min(bottom - 1, found + (int)(head * 1.6));
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

        var eyes = ((1 - constants.NeckWeight) * (found + (constants.EyeBelowCrown * head)))
            + (constants.NeckWeight * (neck - (constants.EyeAboveNeck * head)));
        var span = constants.FaceSpan * head;

        // The face's centre: the centroid of the axis runs from just above the eyes to the chin (a weapon or a hand
        // beside the head is another run and does not pull it).
        double sum = 0, weight = 0;
        for (var y = Math.Max(0, (int)(eyes - (0.25 * head))); y <= Math.Min(height - 1, (int)(eyes + span)); y++)
        {
            if (Nearest(runs[y], axis) is { } run)
            {
                var n = run.End - run.Start + 1;
                sum += (run.Start + run.End) / 2d * n;
                weight += n;
            }
        }

        if (weight <= 0)
        {
            return null;
        }

        var square = span / (ChinLine - EyeLine);
        return new PhotoBox((sum / weight) - (square / 2), eyes - (EyeLine * square), square);
    }

    /// <summary>The first row of the head (see the class summary), or null.</summary>
    private static int? Crown(List<(int Start, int End)>[] runs, int top, int bottom, double axis, double head)
    {
        var hold = Math.Max(3, (int)(0.12 * head));
        for (var y = top; y < bottom - hold; y++)
        {
            var ok = true;
            for (var j = 0; j < hold && ok; j++)
            {
                ok = Nearest(runs[y + j], axis) is { } run
                    && run.End - run.Start + 1 >= 0.35 * head
                    && Math.Abs(((run.Start + run.End) / 2d) - axis) <= 0.4 * head;
            }

            if (ok && Nearest(runs[y], axis) is { } first && JoinsHead(runs, y, first, (int)(0.6 * head), 0.55 * head))
            {
                return y;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether <paramref name="run"/> in row <paramref name="y"/>, followed down through the runs that overlap it, is
    /// <paramref name="wide"/> px across within <paramref name="rows"/> rows.
    /// </summary>
    private static bool JoinsHead(List<(int Start, int End)>[] runs, int y, (int Start, int End) run, int rows, double wide)
    {
        var current = run;
        for (var row = y; row < Math.Min(runs.Length, y + rows); row++)
        {
            if (row > y)
            {
                int start = int.MaxValue, end = int.MinValue;
                foreach (var next in runs[row])
                {
                    if (next.Start <= current.End && next.End >= current.Start)
                    {
                        start = Math.Min(start, next.Start);
                        end = Math.Max(end, next.End);
                    }
                }

                if (start > end)
                {
                    return false;
                }

                current = (start, end);
            }

            if (current.End - current.Start + 1 >= wide)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The run on <paramref name="axis"/>, else the closest to it; null for an empty row.</summary>
    private static (int Start, int End)? Nearest(List<(int Start, int End)> runs, double axis)
    {
        (int Start, int End)? best = null;
        var distance = double.MaxValue;
        foreach (var run in runs)
        {
            var d = run.Start <= axis && axis <= run.End ? 0 : Math.Min(Math.Abs(run.Start - axis), Math.Abs(run.End - axis));
            if (d < distance)
            {
                distance = d;
                best = run;
            }
        }

        return best;
    }

    /// <summary>The row's runs of opaque pixels, gaps of up to 2 px closed; <paramref name="count"/> its opaque pixels.</summary>
    private static List<(int Start, int End)> Runs(byte[] rgba, int width, int y, out int count)
    {
        var runs = new List<(int Start, int End)>();
        count = 0;
        int start = -1, last = -1;
        for (var x = 0; x < width; x++)
        {
            if (rgba[(((y * width) + x) * 4) + 3] < Opaque)
            {
                continue;
            }

            count++;
            if (start < 0)
            {
                start = x;
            }
            else if (x - last > 3)
            {
                runs.Add((start, last));
                start = x;
            }

            last = x;
        }

        if (start >= 0)
        {
            runs.Add((start, last));
        }

        return runs;
    }

    /// <summary>The median column of the opaque pixels in the figure's rows at 35–60 % of its height.</summary>
    private static double Axis(byte[] rgba, int width, int top, int bottom)
    {
        var figure = bottom - top + 1;
        var columns = new int[width];
        var total = 0;
        for (var y = (int)(top + (0.35 * figure)); y < (int)(top + (0.6 * figure)); y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (rgba[(((y * width) + x) * 4) + 3] >= Opaque)
                {
                    columns[x]++;
                    total++;
                }
            }
        }

        if (total == 0)
        {
            return (width - 1) / 2d;
        }

        // The median as numpy takes it: the middle pixel, or the mean of the middle two.
        int Nth(int n)
        {
            for (var x = 0; x < width; x++)
            {
                n -= columns[x];
                if (n < 0)
                {
                    return x;
                }
            }

            return width - 1;
        }

        return total % 2 == 1 ? Nth(total / 2) : (Nth((total / 2) - 1) + Nth(total / 2)) / 2d;
    }

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
    /// The square <paramref name="box"/> (source px; may run past the edges, which are transparent) at
    /// <paramref name="side"/> px: each output pixel the premultiplied average of a 4 × 4 grid of bilinear taps over its
    /// footprint, so it scales up or down without fringes.
    /// </summary>
    public static byte[] Render(byte[] rgba, int width, int height, PhotoBox box, int side)
    {
        var output = new byte[side * side * 4];
        var step = box.Side / side;
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
                        var sx = box.Left + ((ox + ((tx + 0.5) / Taps)) * step) - 0.5;
                        var sy = box.Top + ((oy + ((ty + 0.5) / Taps)) * step) - 0.5;
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
