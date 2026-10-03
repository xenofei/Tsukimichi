using System.Numerics;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// A small CPU rasterizer for the vector medals, as the GPU draws them (positions × size/128 + offsets, colours per
/// vertex interpolated across each triangle, straight-alpha "over" in draw order), and the round-5 salience metric on
/// top of it: the mean of |L − background| (0–255 greyscale) over the medal's box at a true pixel size.
/// </summary>
internal static class MedalRaster
{
    private const int Samples = 4;

    /// <summary>The salience of <paramref name="state"/>'s medal in <paramref name="tokens"/> at <paramref name="px"/> on <paramref name="ground"/>.</summary>
    public static float Salience(QuestState state, MedalTokens tokens, int px, Vector4 ground)
    {
        var mesh = MedalArt.Medal(state, tokens, px);
        var k = px / 128f;
        var under = Rgb(ground);
        var background = Luma(under);
        var total = 0f;
        for (var y = 0; y < px; y++)
        {
            for (var x = 0; x < px; x++)
            {
                var pixel = Vector3.Zero;
                for (var sy = 0; sy < Samples; sy++)
                {
                    for (var sx = 0; sx < Samples; sx++)
                    {
                        var p = new Vector2(x + ((sx + 0.5f) / Samples), y + ((sy + 0.5f) / Samples));
                        pixel += Shade(mesh, px, k, p, under);
                    }
                }

                total += MathF.Abs(Luma(pixel / (Samples * Samples)) - background);
            }
        }

        return total / (px * px);
    }

    /// <summary>The colour at <paramref name="p"/> (device px) with every triangle composited over <paramref name="ground"/> in draw order.</summary>
    public static Vector3 Shade(MedalMesh mesh, float sizePx, float k, Vector2 p, Vector3 ground)
    {
        var color = ground;
        foreach (var part in mesh.Parts)
        {
            if (sizePx < part.MinSizePx)
            {
                continue;
            }

            for (var t = 0; t + 2 < part.Indices.Length; t += 3)
            {
                var (a, b, c) = (part.Indices[t], part.Indices[t + 1], part.Indices[t + 2]);
                var p0 = (part.Positions[a] * k) + part.Offsets[a];
                var p1 = (part.Positions[b] * k) + part.Offsets[b];
                var p2 = (part.Positions[c] * k) + part.Offsets[c];
                var area = ((p1.X - p0.X) * (p2.Y - p0.Y)) - ((p2.X - p0.X) * (p1.Y - p0.Y));
                if (MathF.Abs(area) < 1e-9f)
                {
                    continue;
                }

                var w0 = (((p1.X - p.X) * (p2.Y - p.Y)) - ((p2.X - p.X) * (p1.Y - p.Y))) / area;
                var w1 = (((p2.X - p.X) * (p0.Y - p.Y)) - ((p0.X - p.X) * (p2.Y - p.Y))) / area;
                var w2 = 1f - w0 - w1;
                if (w0 < -1e-6f || w1 < -1e-6f || w2 < -1e-6f)
                {
                    continue;
                }

                var v = (w0 * Unpack(part.Colors[a])) + (w1 * Unpack(part.Colors[b])) + (w2 * Unpack(part.Colors[c]));
                var alpha = Math.Clamp(v.W, 0f, 1f);
                color = Vector3.Lerp(color, new Vector3(v.X, v.Y, v.Z), alpha);
            }
        }

        return color;
    }

    public static Vector3 Rgb(Vector4 c) => new(c.X, c.Y, c.Z);

    public static float Luma(Vector3 c) => 255f * ((0.299f * c.X) + (0.587f * c.Y) + (0.114f * c.Z));

    private static Vector4 Unpack(uint c) => new((c & 0xFF) / 255f, ((c >> 8) & 0xFF) / 255f, ((c >> 16) & 0xFF) / 255f, (c >> 24) / 255f);
}
