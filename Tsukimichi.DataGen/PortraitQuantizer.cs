using Tsukimichi.Core.Portraits;

namespace Tsukimichi.DataGen;

/// <summary>
/// Packs a portrait crop small (feature plan v7 F4): a 256-colour palette PNG by median cut in RGBA, with fully
/// transparent pixels on one entry of their own and no dithering (dither noise packs badly and shimmers when the plate
/// scales it). A 128 px face needs far fewer than 256 colours to look right, so this keeps the pack near a third of the
/// size of plain RGBA. Falls back to RGBA when that is smaller.
/// </summary>
internal static class PortraitQuantizer
{
    public static byte[] Encode(byte[] rgba, int width, int height)
    {
        var rgbaPng = PackPng.EncodeRgba(rgba, width, height);
        var indexed = Quantize(rgba, width * height, out var palette);
        var indexedPng = PackPng.EncodeIndexed(indexed, width, height, palette);
        return indexedPng.Length < rgbaPng.Length ? indexedPng : rgbaPng;
    }

    private static byte[] Quantize(byte[] rgba, int pixels, out byte[] palette)
    {
        // Colours with their counts; alpha snapped to 16 steps, colour kept whole.
        var counts = new Dictionary<uint, int>();
        for (var i = 0; i < pixels; i++)
        {
            var a = rgba[(i * 4) + 3];
            if (a == 0)
            {
                continue;
            }

            var key = Key(rgba[i * 4], rgba[(i * 4) + 1], rgba[(i * 4) + 2], SnapAlpha(a));
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        var colours = counts.Select(kv => (Colour: kv.Key, Count: kv.Value)).ToList();
        var boxes = new List<List<(uint Colour, int Count)>>();
        var scores = new List<(double Score, int Channel)>();
        void Add(List<(uint Colour, int Count)> box)
        {
            boxes.Add(box);
            scores.Add(Score(box));
        }

        if (colours.Count > 0)
        {
            Add(colours);
        }

        // Median cut to 255 boxes (entry 0 is transparent): split the box with the widest weighted range.
        while (boxes.Count < 255)
        {
            var best = -1;
            for (var b = 0; b < boxes.Count; b++)
            {
                if (scores[b].Score > 0 && (best < 0 || scores[b].Score > scores[best].Score))
                {
                    best = b;
                }
            }

            if (best < 0)
            {
                break;
            }

            var channel = scores[best].Channel;
            var box = boxes[best].OrderBy(c => Channel(c.Colour, channel)).ToList();
            var half = box.Sum(c => (long)c.Count) / 2;
            long running = 0;
            var cut = 1;
            for (var i = 0; i < box.Count - 1; i++)
            {
                running += box[i].Count;
                if (running >= half)
                {
                    cut = i + 1;
                    break;
                }
            }

            var low = box.GetRange(0, cut);
            boxes[best] = low;
            scores[best] = Score(low);
            Add(box.GetRange(cut, box.Count - cut));
        }

        palette = new byte[(boxes.Count + 1) * 4];
        for (var b = 0; b < boxes.Count; b++)
        {
            double r = 0, g = 0, bl = 0, a = 0, n = 0;
            foreach (var (colour, count) in boxes[b])
            {
                r += Channel(colour, 0) * (double)count;
                g += Channel(colour, 1) * (double)count;
                bl += Channel(colour, 2) * (double)count;
                a += Channel(colour, 3) * (double)count;
                n += count;
            }

            var o = (b + 1) * 4;
            palette[o] = (byte)Math.Round(r / n);
            palette[o + 1] = (byte)Math.Round(g / n);
            palette[o + 2] = (byte)Math.Round(bl / n);
            palette[o + 3] = (byte)Math.Round(a / n);
        }

        var indices = new byte[pixels];
        var nearest = new Dictionary<uint, byte>();
        for (var i = 0; i < pixels; i++)
        {
            var a = rgba[(i * 4) + 3];
            if (a == 0)
            {
                continue;
            }

            var key = Key(rgba[i * 4], rgba[(i * 4) + 1], rgba[(i * 4) + 2], SnapAlpha(a));
            if (!nearest.TryGetValue(key, out var index))
            {
                index = Nearest(palette, key);
                nearest[key] = index;
            }

            indices[i] = index;
        }

        return indices;
    }

    private static byte Nearest(byte[] palette, uint key)
    {
        int r = Channel(key, 0), g = Channel(key, 1), b = Channel(key, 2), a = Channel(key, 3);
        var best = 1;
        var bestDistance = long.MaxValue;
        for (var e = 1; e < palette.Length / 4; e++)
        {
            int dr = palette[e * 4] - r, dg = palette[(e * 4) + 1] - g, db = palette[(e * 4) + 2] - b, da = palette[(e * 4) + 3] - a;

            // Colour differences matter in proportion to how opaque the pixel is.
            var distance = (((long)dr * dr * 3) + ((long)dg * dg * 4) + ((long)db * db * 2)) * (a + 1) / 256 + ((long)da * da * 4);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = e;
            }
        }

        return (byte)best;
    }

    private static (double Score, int Channel) Score(List<(uint Colour, int Count)> box)
    {
        if (box.Count < 2)
        {
            return (0, 0);
        }

        var (channel, range) = WidestChannel(box);
        return (range * Math.Sqrt(box.Sum(c => (double)c.Count)), channel);
    }

    private static (int Channel, int Range) WidestChannel(List<(uint Colour, int Count)> box)
    {
        var best = (Channel: 0, Range: -1);
        for (var c = 0; c < 4; c++)
        {
            int min = 255, max = 0;
            foreach (var (colour, _) in box)
            {
                var v = Channel(colour, c);
                min = Math.Min(min, v);
                max = Math.Max(max, v);
            }

            // Luminance-weighted: green differences show most, alpha edges least once snapped.
            var range = (max - min) * c switch { 0 => 3, 1 => 4, 2 => 2, _ => 2 };
            if (range > best.Range)
            {
                best = (c, range);
            }
        }

        return best;
    }

    private static byte SnapAlpha(byte a) => a >= 248 ? (byte)255 : (byte)(((a >> 4) << 4) | 8);

    private static uint Key(byte r, byte g, byte b, byte a) => (uint)(r | (g << 8) | (b << 16) | (a << 24));

    private static int Channel(uint key, int channel) => (int)((key >> (channel * 8)) & 0xFF);
}
