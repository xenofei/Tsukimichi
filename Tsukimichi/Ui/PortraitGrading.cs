using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Tsukimichi.Core.Portraits;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The giver portraits' graded copies (1.15 design spec A3, A2.4), made the way <see cref="BannerGrading"/> grades
/// banners: the first time a face is drawn its crop is read back once in the background
/// (<see cref="Plugin.TextureReadback"/>, at the hr texture's own pixels), graded by its family's sRGB colour matrix
/// (<see cref="PortraitGrade"/>) and made a texture of its own; until it lands the source draws with the night multiply
/// alone as a tint, so a bright face never flashes ungraded. A face drawn small (an avatar, the Journal column, Plain)
/// takes a second, box-filtered copy <see cref="PortraitPlate.SmallCopySide"/> px across, so it never shimmers.
/// <para>
/// A custom delivery portrait is drawn only through its keep mask (<see cref="PortraitMask"/>): in the same pass, after
/// checking the game's texture file is the one the mask was keyed from, every pixel the mask does not keep becomes
/// transparent, so the client emblem's script never shows. Until that copy lands, and for good when the art changed in a
/// patch, <see cref="TryGet"/> says so and the plate draws the fallback; the unkeyed art is never drawn.
/// </para>
/// Copies are kept by icon, size and palette (<see cref="Key"/>); a few dozen are kept and the least recently drawn let go,
/// never one a frame's draw list may still hold. Nothing allocates on the draw thread once a copy exists.
/// </summary>
public static class PortraitGrading
{
    /// <summary>How many graded copies are kept (each at most a few hundred KB).</summary>
    private const int Capacity = 48;

    /// <summary>Frames an evicted copy is kept after it was last drawn, so a draw list never holds a freed texture.</summary>
    private const int RetireFrames = 2;

    /// <summary>DXGI_FORMAT_B8G8R8A8_UNORM.</summary>
    private const int BgraFormat = 87;

    /// <summary>
    /// Whether the grade's night multiply (step 2) applies: on every palette the plugin has today. A future light palette
    /// (Ishgard Snow) returns false here and keeps the desaturation and the black lift (the supervisor's ruling, Q5).
    /// </summary>
    public static bool NightMultiply => true;

    /// <summary>Which copy: the icon, the small or full-size copy, and whether the night multiply is in it.</summary>
    public readonly record struct Key(uint Icon, bool Small, bool Night);

    /// <summary>What <see cref="TryGet"/> found.</summary>
    public enum State : byte
    {
        /// <summary>The graded copy is ready.</summary>
        Ready,

        /// <summary>The copy is being made: draw the source with the tint (a delivery portrait: the fallback).</summary>
        Pending,

        /// <summary>No copy can be made (no readback, a failed read): draw the source with the tint (a delivery portrait: the fallback).</summary>
        Failed,

        /// <summary>A delivery portrait whose art no longer matches its keep mask: always the fallback.</summary>
        MaskMismatch,
    }

    private sealed class Entry
    {
        public Task? Work;
        public IDalamudTextureWrap? Graded;
        public int UsedFrame;
        public volatile bool Failed;
        public volatile bool Mismatch;
    }

    private sealed record MaskData(bool Matches, bool[] Keep, int Width);

    private static readonly Dictionary<Key, Entry> Entries = [];
    private static readonly Dictionary<uint, MaskData> Masks = [];
    private static readonly List<(IDalamudTextureWrap Wrap, int Frame)> Retired = [];
    private static readonly object Gate = new();
    private static bool disposed;

    /// <summary>
    /// The graded copy of <paramref name="portrait"/>'s crop from <paramref name="source"/> (its hr texture), the small one
    /// with <paramref name="small"/>; starts making it the first time it is asked for. The copy is the crop alone, drawn
    /// with UVs 0–1.
    /// </summary>
    public static State TryGet(IDalamudTextureWrap source, in PortraitRef portrait, bool small, out IDalamudTextureWrap? graded)
    {
        graded = null;
        var frame = ImGui.GetFrameCount();
        var key = new Key(portrait.Icon, small, NightMultiply);
        Entry? entry;
        lock (Gate)
        {
            FlushRetired(frame);
            if (!Entries.TryGetValue(key, out entry))
            {
                entry = new Entry();
                Entries[key] = entry;
                Start(entry, source, portrait, small, key.Night);
                Evict(frame);
            }

            entry.UsedFrame = frame;
        }

        if (entry.Graded is { } ready)
        {
            graded = ready;
            return State.Ready;
        }

        return entry.Mismatch ? State.MaskMismatch : entry.Failed ? State.Failed : State.Pending;
    }

    /// <summary>The tint the source draws with until its copy lands: the night multiply alone (spec A3).</summary>
    public static uint TintFor(PortraitSource source, float alpha)
    {
        var tint = PortraitGrade.TintFor(source, NightMultiply);
        return Theme.U32(new Vector4(tint, alpha));
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
            Masks.Clear();
            foreach (var (wrap, _) in Retired)
            {
                wrap.Dispose();
            }

            Retired.Clear();
        }
    }

    /// <summary>Reads the crop back at its own pixels, keys and grades it, and makes the copy. Under <see cref="Gate"/>.</summary>
    private static void Start(Entry entry, IDalamudTextureWrap source, PortraitRef portrait, bool small, bool night)
    {
        if (Plugin.TextureReadback is not { } readback || Plugin.TextureProvider is not { } textures)
        {
            entry.Failed = true;
            return;
        }

        int width = source.Width, height = source.Height;
        var crop = portrait.Crop;
        int x0 = Math.Clamp((int)MathF.Round(crop.U0 * width), 0, width - 1), y0 = Math.Clamp((int)MathF.Round(crop.V0 * height), 0, height - 1);
        int x1 = Math.Clamp((int)MathF.Round(crop.U1 * width), x0 + 1, width), y1 = Math.Clamp((int)MathF.Round(crop.V1 * height), y0 + 1, height);
        var mask = portrait.Mask;
        if (mask is not null && (width != mask.Width || height != mask.Height))
        {
            // Not the hr texture the mask was keyed on (the 1x one stood in, or the art was resized in a patch).
            entry.Mismatch = true;
            return;
        }

        var matrix = PortraitGrade.For(PortraitGrade.FamilyOf(portrait.Source), night);
        var icon = portrait.Icon;
        entry.Work = Task.Run(async () =>
        {
            try
            {
                MaskData? keep = null;
                if (mask is not null)
                {
                    keep = LoadMask(mask);
                    if (!keep.Matches)
                    {
                        entry.Mismatch = true;
                        Plugin.Log?.Debug("Giver portrait {Icon}: the game's art no longer matches its keep mask; the fallback shows until DataGen is rerun", icon);
                        return;
                    }
                }

                var args = new TextureModificationArgs
                {
                    DxgiFormat = BgraFormat,
                    Uv0 = new Vector2((float)x0 / width, (float)y0 / height),
                    Uv1 = new Vector2((float)x1 / width, (float)y1 / height),
                    NewWidth = x1 - x0,
                    NewHeight = y1 - y0,
                };
                var (spec, raw) = await readback.GetRawImageAsync(source, args, leaveWrapOpen: true);
                if (keep is not null && (spec.Width != x1 - x0 || spec.Height != y1 - y0))
                {
                    // The mask is pixel for pixel: a resampled read cannot be keyed safely.
                    entry.Mismatch = true;
                    return;
                }

                var bgra = spec.DxgiFormat == BgraFormat;
                PortraitGrade.GradeInPlace(raw, spec.Width, spec.Height, spec.Pitch, bgra, matrix, keep?.Keep ?? default, keep?.Width ?? 0, x0, y0);
                if (small && spec.Width > PortraitPlate.SmallCopySide)
                {
                    var side = PortraitPlate.SmallCopySide;
                    var smaller = PortraitGrade.Downsample(raw, spec.Width, spec.Height, spec.Pitch, side, side);
                    spec = new RawImageSpecification(side, side, side * 4, spec.DxgiFormat);
                    raw = smaller;
                }

                var wrap = await textures.CreateFromRawAsync(spec, raw, "Tsukimichi giver portrait (night grade)");
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
                Plugin.Log?.Debug(ex, "Giver portrait {Icon}: could not read the art back; it draws with the tint alone", icon);
            }
        });
    }

    /// <summary>The keep mask of a delivery portrait, read and checked against the game's file once per icon.</summary>
    private static MaskData LoadMask(PortraitMask mask)
    {
        lock (Gate)
        {
            if (Masks.TryGetValue(mask.Icon, out var known))
            {
                return known;
            }
        }

        var matches = false;
        bool[] keep = [];
        var keepWidth = 0;
        try
        {
            var file = Plugin.DataManager?.GetFile(mask.TexturePath);
            matches = file is not null && mask.Matches(file.Data)
                && PortraitMaskFile.TryRead(mask.File, out keepWidth, out var keepHeight, out keep)
                && keepWidth == mask.Width && keepHeight == mask.Height;
        }
        catch (Exception ex)
        {
            Plugin.Log?.Debug(ex, "Giver portrait {Icon}: the keep mask could not be checked", mask.Icon);
            matches = false;
        }

        var data = new MaskData(matches, matches ? keep : [], matches ? keepWidth : 0);
        lock (Gate)
        {
            Masks[mask.Icon] = data;
        }

        return data;
    }

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

            if (Entries[victim].Graded is { } gone)
            {
                Retired.Add((gone, frame));
            }

            Entries.Remove(victim);
        }
    }

    /// <summary>Lets the evicted copies go once <see cref="RetireFrames"/> have passed. Under <see cref="Gate"/>.</summary>
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
}
