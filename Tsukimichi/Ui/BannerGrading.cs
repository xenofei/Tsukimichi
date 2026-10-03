using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// Full's banner night grade (docs/design/flair-v13/spec.md §1.2, <see cref="BannerGrade"/>) for the textures the
/// banner chain hands out. The first time a banner is drawn its texture is read back once in the background
/// (<see cref="Plugin.TextureReadback"/>, scaled down to the size it is drawn at), its mean colour measured, the multiply
/// strength chosen from it, and the multiply and the 35 % desaturation applied to that copy; the scrim, the wash and the
/// moon road are drawn over it as quads (<see cref="DrawOver"/>). Until the copy is ready the art draws with the
/// multiply alone, as a tint at the default strength, so a daylight banner never shows ungraded. The art the game and
/// the plugin ship is never graded itself. A few copies are kept; the oldest unused one is let go.
/// </summary>
public static class BannerGrading
{
    /// <summary>How wide a graded copy is at most, in pixels: the banner is never drawn wider than this.</summary>
    private const int MaxWidth = 640;

    /// <summary>How many graded copies are kept.</summary>
    private const int Capacity = 6;

    private sealed class Entry
    {
        public Task? Work;
        public IDalamudTextureWrap? Graded;
        public float Strength = BannerGrade.DefaultStrength;
        public int UsedFrame;
        public bool Failed;
    }

    private static readonly Dictionary<(nint Handle, int Width, int Height), Entry> Entries = [];
    private static readonly object Gate = new();
    private static bool disposed;

    /// <summary>
    /// Draws <paramref name="source"/> cover-cropped into <paramref name="min"/>..<paramref name="max"/>, night-graded:
    /// the graded copy once it is ready, the source multiplied by the default tint until then.
    /// </summary>
    public static void DrawImage(ImDrawListPtr dl, IDalamudTextureWrap source, Vector2 min, Vector2 max, float rounding)
    {
        var key = ((nint)source.Handle.Handle, source.Width, source.Height);
        Entry? entry;
        lock (Gate)
        {
            if (!Entries.TryGetValue(key, out entry))
            {
                entry = new Entry();
                Entries[key] = entry;
                Start(entry, source, (int)MathF.Ceiling(max.X - min.X), (int)MathF.Ceiling(max.Y - min.Y));
                Evict(ImGui.GetFrameCount());
            }

            entry.UsedFrame = ImGui.GetFrameCount();
        }

        if (entry.Graded is { } graded)
        {
            Chrome.ImageCoverAt(dl, graded.Handle, min, max, new Vector2(graded.Width, graded.Height), rounding);
            return;
        }

        Chrome.ImageCoverAt(dl, source.Handle, min, max, new Vector2(source.Width, source.Height), Theme.U32(BannerGrade.Tint(entry.Strength)), rounding);
    }

    /// <summary>
    /// The passes over the art, in order (spec §1.2): the scrim to Night (0 at the top, 0.25 at 45 %, 0.85 at the foot),
    /// the MoonHigh wash from the upper-left corner, and the faint moon road of six dashes along the bottom-right edge,
    /// widening toward the viewer. Under the high-contrast palette (never Full, but a caller may ask) only the scrim.
    /// </summary>
    public static void DrawOver(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding)
    {
        var size = max - min;
        if (!(size.X > 1f) || !(size.Y > 1f))
        {
            return;
        }

        var night = Theme.Surface.Window;
        var stops = BannerGrade.ScrimStops;
        for (var i = 1; i < stops.Length; i++)
        {
            var (a0, v0) = stops[i - 1];
            var (a1, v1) = stops[i];
            var top = new Vector2(min.X, min.Y + (size.Y * a0));
            var foot = new Vector2(max.X, min.Y + (size.Y * a1));
            var from = Theme.WithAlpha(night, v0);
            var to = Theme.WithAlpha(night, v1);
            if (i == stops.Length - 1 && rounding > 0f)
            {
                GradientRounded(dl, top, foot, rounding, night with { W = v0 }, night with { W = v1 });
            }
            else
            {
                dl.AddRectFilledMultiColor(top, foot, from, from, to, to);
            }
        }

        if (Theme.Glyphs.HighContrast)
        {
            return;
        }

        // The moonlight wash: MoonHigh from the upper-left corner, gone by the far side.
        var wash = Theme.WithAlpha(Theme.MoonHigh, BannerGrade.WashAlpha);
        var clear = Theme.WithAlpha(Theme.MoonHigh, 0f);
        dl.AddRectFilledMultiColor(min, new Vector2(min.X + (size.X * 0.7f), min.Y + (size.Y * 0.8f)), wash, clear, clear, clear);

        // The moon road on the water: six MoonHigh dashes, faint, widening toward the viewer.
        var roadTop = max.Y - (size.Y * BannerGrade.RoadHeightFraction);
        var roadHeight = size.Y * BannerGrade.RoadHeightFraction;
        var cx = min.X + (size.X * BannerGrade.RoadCenterFraction);
        var thickness = MathF.Max(1f, UiMetrics.Px(1.2f));
        foreach (var (y, width, alpha, nudge) in BannerGrade.RoadDashes)
        {
            var w = MathF.Max(2f, size.X * width);
            var x = cx + (size.X * nudge) - (w * 0.5f);
            var at = MathF.Round(roadTop + (roadHeight * y));
            dl.AddRectFilled(new Vector2(x, at), new Vector2(x + w, at + thickness), Theme.WithAlpha(Theme.MoonHigh, alpha), thickness * 0.5f);
        }
    }

    /// <summary>Lets every graded copy go (the plugin unloading).</summary>
    public static void Dispose()
    {
        lock (Gate)
        {
            disposed = true;
            foreach (var entry in Entries.Values)
            {
                entry.Graded?.Dispose();
                entry.Graded = null;
            }

            Entries.Clear();
        }
    }

    private static void Start(Entry entry, IDalamudTextureWrap source, int drawWidth, int drawHeight)
    {
        if (Plugin.TextureReadback is not { } readback || Plugin.TextureProvider is not { } textures)
        {
            entry.Failed = true;
            return;
        }

        // Read back at about the size it is drawn (never wider than MaxWidth), keeping the art's aspect.
        var width = Math.Clamp(Math.Max(drawWidth, 64), 64, Math.Min(MaxWidth, Math.Max(64, source.Width)));
        var height = Math.Max(16, (int)MathF.Round(width * (float)source.Height / Math.Max(1, source.Width)));
        entry.Work = Task.Run(async () =>
        {
            try
            {
                var args = new TextureModificationArgs { DxgiFormat = BgraFormat, NewWidth = width, NewHeight = height };
                var (spec, raw) = await readback.GetRawImageAsync(source, args, leaveWrapOpen: true);
                var bgra = spec.DxgiFormat == BgraFormat;
                var mean = BannerGrade.MeanRgb(raw, spec.Width, spec.Height, spec.Pitch, bgra, step: 2);
                var strength = BannerGrade.Strength(mean);
                entry.Strength = strength;
                BannerGrade.GradeInPlace(raw, spec.Width, spec.Height, spec.Pitch, bgra, strength);
                var wrap = await textures.CreateFromRawAsync(spec, raw, "Tsukimichi banner (night grade)");
                lock (Gate)
                {
                    if (disposed)
                    {
                        wrap.Dispose();
                        return;
                    }

                    entry.Graded = wrap;
                }
            }
            catch (Exception ex)
            {
                entry.Failed = true;
                Plugin.Log?.Debug(ex, "Banner night grade: could not read the banner back; it draws with the tint alone");
            }
        });
    }

    /// <summary>DXGI_FORMAT_B8G8R8A8_UNORM.</summary>
    private const int BgraFormat = 87;

    /// <summary>Lets the least recently drawn copies go while there are more than <see cref="Capacity"/>, never one drawn in the last two frames.</summary>
    private static void Evict(int frame)
    {
        while (Entries.Count > Capacity)
        {
            (nint, int, int)? oldest = null;
            var oldestFrame = int.MaxValue;
            foreach (var (key, entry) in Entries)
            {
                if (entry.UsedFrame < oldestFrame && frame - entry.UsedFrame > 2 && entry.Work is not { IsCompleted: false })
                {
                    oldest = key;
                    oldestFrame = entry.UsedFrame;
                }
            }

            if (oldest is not { } victim)
            {
                return;
            }

            Entries[victim].Graded?.Dispose();
            Entries.Remove(victim);
        }
    }

    /// <summary>A rounded rectangle with its bottom corners rounded, filled top to bottom from one colour to another.</summary>
    private static void GradientRounded(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding, Vector4 from, Vector4 to)
    {
        var first = dl.VtxBuffer.Size;
        dl.AddRectFilled(min, max, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersBottom);
        var vertices = dl.VtxBuffer;
        var span = MathF.Max(1f, max.Y - min.Y);
        for (var i = first; i < vertices.Size; i++)
        {
            var vertex = vertices[i];
            var c = Vector4.Lerp(from, to, Math.Clamp((vertex.Pos.Y - min.Y) / span, 0f, 1f));
            c.W *= (vertex.Col >> 24) / 255f;
            vertex.Col = Theme.U32(c);
            vertices[i] = vertex;
        }
    }
}
