namespace Tsukimichi.GameData;

/// <summary>
/// Keys the client emblem's lettered ring out of a custom delivery portrait, figure first (1.15 design spec A2.4): a
/// faithful port of <c>docs/design/v7/ui/1.15/art/key_delivery.py</c>. All steps run on the hr texture.
/// <list type="number">
/// <item>Opaque: alpha &gt; 40.</item>
/// <item>Script colour: alpha &gt; 8, saturation &gt; .35, value &gt; .12, hue 40–170°, G &gt; B + .06, and not skin-red
/// (R &gt; G + .05 and R &gt; .6): the green letters, their dark-green shading and the gold seal.</item>
/// <item>Figure: the 4-connected flood from the curated face seed over opaque pixels not within 2 px of the script, then
/// grown back 2 px into opaque pixels that are not script colour. No figure pixel is ever removed.</item>
/// <item>Islands (8-connected) of the other opaque pixels go when they are mostly script (half or more in the key or its
/// halo) or tiny (under 12 px); the rest are kept and listed for review.</item>
/// <item>Script-colour pixels outside the figure go, dilated 1 px but never into the figure, and so do the low-alpha
/// pixels within the script's halo (the letters' soft edges).</item>
/// <item>Removed pixels take the backdrop's local colour and alpha (a normalised blur of the pixels that are neither
/// figure nor removed); the delivery backdrop is transparent, so the fill is alpha 0. Only the preview uses the fill:
/// the shipped mask multiplies alpha to 0 there, the same result.</item>
/// </list>
/// </summary>
public static class DeliveryKey
{
    /// <summary>The outcome for one portrait.</summary>
    /// <param name="Keep">Row-major keep mask: false where the script was removed.</param>
    /// <param name="Figure">Row-major figure: what the flood from the seed protects.</param>
    /// <param name="KeptIslands">Opaque islands outside the figure that were kept (area, script share), for review.</param>
    public sealed record Result(bool[] Keep, bool[] Figure, int Removed, int RemovedInsideFigure, IReadOnlyList<(int Area, double ScriptShare)> KeptIslands);

    /// <param name="bgra">The hr texture's pixels as Lumina decodes them: B, G, R, A per pixel, row-major.</param>
    public static Result Run(byte[] bgra, int width, int height, int seedX, int seedY)
    {
        var n = width * height;
        var opaque = new bool[n];
        var script = new bool[n];
        for (var i = 0; i < n; i++)
        {
            opaque[i] = bgra[(i * 4) + 3] > 40;
            script[i] = IsScript(bgra[(i * 4) + 0], bgra[(i * 4) + 1], bgra[(i * 4) + 2], bgra[(i * 4) + 3]);
        }

        var halo = Dilate(script, width, height, 2);
        var candidates = new bool[n];
        for (var i = 0; i < n; i++)
        {
            candidates[i] = opaque[i] && !halo[i];
        }

        var figure = Flood(candidates, width, height, seedX, seedY);
        var grown = Dilate(figure, width, height, 2);
        for (var i = 0; i < n; i++)
        {
            figure[i] = figure[i] || (grown[i] && opaque[i] && !script[i]);
        }

        var other = new bool[n];
        for (var i = 0; i < n; i++)
        {
            other[i] = opaque[i] && !figure[i];
        }

        var remove = new bool[n];
        var kept = new List<(int, double)>();
        foreach (var island in Islands(other, width, height))
        {
            var share = island.Count(i => script[i] || halo[i]) / (double)island.Count;
            if (island.Count < 12 || share >= .5)
            {
                foreach (var i in island)
                {
                    remove[i] = true;
                }
            }
            else
            {
                kept.Add((island.Count, Math.Round(share, 2)));
            }
        }

        var outsideScript = new bool[n];
        for (var i = 0; i < n; i++)
        {
            outsideScript[i] = script[i] && !figure[i];
        }

        var spread = Dilate(outsideScript, width, height, 1);
        var haloEdge = Dilate(halo, width, height, 1);
        for (var i = 0; i < n; i++)
        {
            if (figure[i])
            {
                continue;
            }

            if (spread[i] || (haloEdge[i] && !opaque[i] && bgra[(i * 4) + 3] > 0))
            {
                remove[i] = true;
            }
        }

        var keep = new bool[n];
        int removed = 0, insideFigure = 0;
        for (var i = 0; i < n; i++)
        {
            keep[i] = !remove[i];
            if (remove[i])
            {
                removed++;
                if (figure[i])
                {
                    insideFigure++;
                }
            }
        }

        return new Result(keep, figure, removed, insideFigure, kept);
    }

    /// <summary>Step 2, the script colour key, for one pixel: the green letters, their dark-green shading and the gold seal.</summary>
    public static bool IsScript(byte blue, byte green, byte red, byte alpha)
    {
        double b = blue / 255.0, g = green / 255.0, r = red / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        var sat = (max - min) / (max + 1e-6);
        var d = max - min > 1e-6 ? max - min : 1;
        double hue;
        if (max == r)
        {
            var h6 = (g - b) / d % 6;
            hue = (h6 < 0 ? h6 + 6 : h6) * 60;
        }
        else if (max == g)
        {
            hue = (((b - r) / d) + 2) * 60;
        }
        else
        {
            hue = (((r - g) / d) + 4) * 60;
        }

        return alpha > 8 && sat > .35 && max > .12 && hue >= 40 && hue <= 170 && g > b + .06 && !(r > g + .05 && r > .6);
    }

    /// <summary>A square max filter of radius <paramref name="px"/> (PIL's <c>MaxFilter(2 px + 1)</c>).</summary>
    private static bool[] Dilate(bool[] mask, int width, int height, int px)
    {
        // Separable: rows, then columns.
        var rows = new bool[mask.Length];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var hit = false;
                for (var dx = -px; dx <= px && !hit; dx++)
                {
                    var xx = x + dx;
                    hit = xx >= 0 && xx < width && mask[(y * width) + xx];
                }

                rows[(y * width) + x] = hit;
            }
        }

        var result = new bool[mask.Length];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var hit = false;
                for (var dy = -px; dy <= px && !hit; dy++)
                {
                    var yy = y + dy;
                    hit = yy >= 0 && yy < height && rows[(yy * width) + x];
                }

                result[(y * width) + x] = hit;
            }
        }

        return result;
    }

    /// <summary>The 4-connected region of <paramref name="candidates"/> reached from the seed (the seed itself always counts).</summary>
    private static bool[] Flood(bool[] candidates, int width, int height, int seedX, int seedY)
    {
        var seen = new bool[candidates.Length];
        if (seedX < 0 || seedY < 0 || seedX >= width || seedY >= height)
        {
            return seen;
        }

        var queue = new Queue<int>();
        var start = (seedY * width) + seedX;
        seen[start] = true;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            int x = at % width, y = at / width;
            foreach (var (dx, dy) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
            {
                int xx = x + dx, yy = y + dy;
                if (xx < 0 || yy < 0 || xx >= width || yy >= height)
                {
                    continue;
                }

                var next = (yy * width) + xx;
                if (candidates[next] && !seen[next])
                {
                    seen[next] = true;
                    queue.Enqueue(next);
                }
            }
        }

        return seen;
    }

    /// <summary>The 8-connected islands of <paramref name="mask"/>, each as its pixel indices.</summary>
    private static List<List<int>> Islands(bool[] mask, int width, int height)
    {
        var label = new bool[mask.Length];
        var islands = new List<List<int>>();
        for (var start = 0; start < mask.Length; start++)
        {
            if (!mask[start] || label[start])
            {
                continue;
            }

            var island = new List<int>();
            var queue = new Queue<int>();
            label[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                island.Add(at);
                int x = at % width, y = at / width;
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= width || yy >= height)
                        {
                            continue;
                        }

                        var next = (yy * width) + xx;
                        if (mask[next] && !label[next])
                        {
                            label[next] = true;
                            queue.Enqueue(next);
                        }
                    }
                }
            }

            islands.Add(island);
        }

        return islands;
    }
}
