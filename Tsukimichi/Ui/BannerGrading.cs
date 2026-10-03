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
/// (<see cref="Plugin.TextureReadback"/>, scaled down to the size it is drawn at), its mean colour and 99.5th-percentile
/// lightness measured in one pass, the multiply strength chosen from them, and the multiply and the desaturation applied
/// to that copy; the scrim, the wash and the moon road are drawn over it as quads (<see cref="DrawOver"/>). Until the
/// copy is ready the art draws with the multiply alone, as a tint at the default strength, so a daylight banner never
/// shows ungraded. When the banner comes to be drawn much wider than its copy (<see cref="BannerGrade.Regrade"/>) it is
/// graded again at the new width, the old copy drawn until the new one lands. Copies are kept by the banner they show
/// (<see cref="Key"/>: the game icon, the zone image's path or the bundled art), never by the texture's address, which
/// the GPU may hand to another banner once the first is let go. The art the game and the plugin ship is never graded
/// itself. A few copies are kept; the oldest unused one is let go.
/// </summary>
public static class BannerGrading
{
    /// <summary>How many graded copies are kept.</summary>
    private const int Capacity = 6;

    /// <summary>Frames a replaced or evicted copy is kept after it was last drawn, so a draw list never holds a freed texture.</summary>
    private const int RetireFrames = 2;

    /// <summary>
    /// Which banner a texture shows: its source kind, and the icon id, the game path or the bundled art that names it.
    /// Built by <see cref="KeyFor"/>; a record struct, so it compares by value and allocates nothing.
    /// </summary>
    public readonly record struct Key(BannerSource Source, uint IconId, string? GamePath, BannerArt Art);

    private sealed class Entry
    {
        public Task? Work;
        public IDalamudTextureWrap? Graded;
        public IDalamudTextureWrap? Next;
        public int Width;
        public float Strength = BannerGrade.DefaultStrength;
        public int UsedFrame;
        public bool Failed;
    }

    private static readonly Dictionary<Key, Entry> Entries = [];
    private static readonly List<(IDalamudTextureWrap Wrap, int Frame)> Retired = [];
    private static readonly object Gate = new();
    private static bool disposed;

    /// <summary>
    /// The identity of the banner <see cref="BannerArtwork.TryGetWrap"/> drew for <paramref name="choice"/>, given the
    /// source it reports as <paramref name="shown"/> (the bundled art when a game texture failed and it stood in).
    /// </summary>
    public static Key KeyFor(in BannerChoice choice, BannerSource shown) => shown switch
    {
        BannerSource.Own or BannerSource.Sibling or BannerSource.Duty => new Key(BannerSource.Own, choice.IconId, null, default),
        BannerSource.Zone => new Key(BannerSource.Zone, 0, choice.GamePath, default),
        _ => new Key(BannerSource.Category, 0, null, choice.Art),
    };

    /// <summary>
    /// Draws <paramref name="source"/>, the banner named by <paramref name="key"/>, cover-cropped into
    /// <paramref name="min"/>..<paramref name="max"/>, night-graded: the graded copy once it is ready, the source
    /// multiplied by the default tint until then.
    /// </summary>
    public static void DrawImage(ImDrawListPtr dl, IDalamudTextureWrap source, in Key key, Vector2 min, Vector2 max, float rounding)
    {
        var frame = ImGui.GetFrameCount();
        var drawWidth = (int)MathF.Ceiling(max.X - min.X);
        Entry? entry;
        lock (Gate)
        {
            FlushRetired(frame);
            if (!Entries.TryGetValue(key, out entry))
            {
                entry = new Entry();
                Entries[key] = entry;
                Start(entry, source, drawWidth);
                Evict(frame);
            }
            else if (entry.Next is { } next)
            {
                // A wider copy landed: swap it in on the draw thread, and let the old one go once no frame draws it.
                Retire(entry.Graded, frame);
                entry.Graded = next;
                entry.Next = null;
            }
            else if (entry.Work is { IsCompleted: true } && !entry.Failed && BannerGrade.Regrade(entry.Width, drawWidth, source.Width))
            {
                Start(entry, source, drawWidth);
            }

            entry.UsedFrame = frame;
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
                entry.Next?.Dispose();
                entry.Next = null;
            }

            Entries.Clear();
            foreach (var (wrap, _) in Retired)
            {
                wrap.Dispose();
            }

            Retired.Clear();
        }
    }

    /// <summary>Reads <paramref name="source"/> back at about <paramref name="drawWidth"/> (<see cref="BannerGrade.CopyWidth"/>), keeping its aspect, and grades it. Under <see cref="Gate"/>.</summary>
    private static void Start(Entry entry, IDalamudTextureWrap source, int drawWidth)
    {
        if (Plugin.TextureReadback is not { } readback || Plugin.TextureProvider is not { } textures)
        {
            entry.Failed = true;
            return;
        }

        var width = BannerGrade.CopyWidth(drawWidth, source.Width);
        var height = Math.Max(16, (int)MathF.Round(width * (float)source.Height / Math.Max(1, source.Width)));
        entry.Width = width;
        entry.Work = Task.Run(async () =>
        {
            try
            {
                var args = new TextureModificationArgs { DxgiFormat = BgraFormat, NewWidth = width, NewHeight = height };
                var (spec, raw) = await readback.GetRawImageAsync(source, args, leaveWrapOpen: true);
                var bgra = spec.DxgiFormat == BgraFormat;
                var histogram = new int[BannerGrade.HistogramBins];
                var mean = BannerGrade.MeanRgb(raw, spec.Width, spec.Height, spec.Pitch, bgra, step: 2, lumaHistogram: histogram);
                var strength = BannerGrade.Strength(mean, BannerGrade.LumaPercentile(histogram, BannerGrade.PeakFraction));
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

                    // The first copy shows at once; a wider one waits for the draw thread to swap it in.
                    if (entry.Graded is null)
                    {
                        entry.Graded = wrap;
                    }
                    else
                    {
                        entry.Next?.Dispose();
                        entry.Next = wrap;
                    }
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
            Key? oldest = null;
            var oldestFrame = int.MaxValue;
            foreach (var (key, entry) in Entries)
            {
                if (entry.UsedFrame < oldestFrame && frame - entry.UsedFrame > RetireFrames && entry.Work is not { IsCompleted: false })
                {
                    oldest = key;
                    oldestFrame = entry.UsedFrame;
                }
            }

            if (oldest is not { } victim)
            {
                return;
            }

            var gone = Entries[victim];
            gone.Graded?.Dispose();
            gone.Next?.Dispose();
            Entries.Remove(victim);
        }
    }

    /// <summary>Keeps a replaced copy until no frame's draw list can still hold it. Under <see cref="Gate"/>.</summary>
    private static void Retire(IDalamudTextureWrap? wrap, int frame)
    {
        if (wrap is not null)
        {
            Retired.Add((wrap, frame));
        }
    }

    /// <summary>Lets the replaced copies go once <see cref="RetireFrames"/> have passed. Under <see cref="Gate"/>.</summary>
    private static void FlushRetired(int frame)
    {
        for (var i = Retired.Count - 1; i >= 0; i--)
        {
            if (frame - Retired[i].Frame > RetireFrames)
            {
                Retired[i].Wrap.Dispose();
                Retired.RemoveAt(i);
            }
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
