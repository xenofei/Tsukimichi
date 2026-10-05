using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin.Services;

namespace Tsukimichi.Ui;

/// <summary>The game's three faces Moonfall sets its chrome in (spec-rich2.md §1, "Type").</summary>
public enum MoonfallFace : byte
{
    /// <summary>Titles, names, banners, primary buttons.</summary>
    Jupiter,

    /// <summary>The game's window sans: labels, body text, secondary buttons, and every "×" and "·".</summary>
    Axis,

    /// <summary>Score, counts, stage numbers, ACED.</summary>
    Trump,
}

/// <summary>Where Moonfall's chrome finds its fonts: the game's faces through Dalamud in the plugin, stand-ins offline.</summary>
public interface IMoonfallFonts : IDisposable
{
    /// <summary>
    /// The font to set <paramref name="face"/> in at <paramref name="px"/> ImGui pixels (the native size nearest at or
    /// above it); false while it is still being built (the window's own font stands in).
    /// </summary>
    bool TryGet(MoonfallFace face, float px, out ImFontPtr font);
}

/// <summary>
/// The game faces' metrics as the design set type with them (r2font.py): a size is the face's cell (its line height,
/// the "em"), and the caps' ink is a share of it. Measured from the game's own glyph tables (common/font/*.fdt, patch
/// 2026.09.15): the cell is Jupiter's size × 1.14, AXIS's × 1.0 and TrumpGothic's × 0.93 in Dalamud's pixels.
/// </summary>
public static class MoonfallFaceMetrics
{
    /// <summary>ImGui pixels per pixel of the face's cell.</summary>
    public static float EmScale(MoonfallFace face) => face switch { MoonfallFace.Jupiter => 0.876f, MoonfallFace.Trump => 1.08f, _ => 1f };

    /// <summary>The caps' ink height as a share of the cell (the text floors measure this).</summary>
    public static float CapHeight(MoonfallFace face) => face switch { MoonfallFace.Jupiter => 0.51f, MoonfallFace.Trump => 0.62f, _ => 0.583f };

    /// <summary>The caps' middle below the line's top, as a share of the cell.</summary>
    public static float CapMiddle(MoonfallFace face) => face switch { MoonfallFace.Jupiter => 0.464f, _ => 0.5f };
}

/// <summary>
/// The game's own fonts for Moonfall (decision 24: Jupiter, AXIS and TrumpGothic through Dalamud, nothing bundled): one
/// handle per face and native size, built by the plugin's font atlas when the window first draws and kept until unload.
/// AXIS carries Dalamud's punctuation merged in, so a "×" or "·" is never a missing-glyph box; Jupiter and TrumpGothic
/// are the game's glyphs only. Each frame the font is read once per handle (a pooled push), so drawing allocates nothing.
/// </summary>
internal sealed class MoonfallFonts : IMoonfallFonts
{
    private static readonly (MoonfallFace Face, GameFontFamilyAndSize Size)[] Sizes =
    [
        (MoonfallFace.Jupiter, GameFontFamilyAndSize.Jupiter23),
        (MoonfallFace.Jupiter, GameFontFamilyAndSize.Jupiter46),
        (MoonfallFace.Axis, GameFontFamilyAndSize.Axis14),
        (MoonfallFace.Axis, GameFontFamilyAndSize.Axis18),
        (MoonfallFace.Axis, GameFontFamilyAndSize.Axis36),
        (MoonfallFace.Trump, GameFontFamilyAndSize.TrumpGothic34),
        (MoonfallFace.Trump, GameFontFamilyAndSize.TrumpGothic68),
    ];

    private static readonly ushort[] PunctuationRanges = [0x00A0, 0x00FF, 0x2010, 0x205E, 0];

    private readonly IFontAtlas atlas;
    private readonly IPluginLog? log;
    private IFontHandle?[]? handles;
    private readonly float[] native = new float[Sizes.Length];
    private readonly ImFontPtr[] fonts = new ImFontPtr[Sizes.Length];
    private readonly int[] readFrame = new int[Sizes.Length];

    public MoonfallFonts(IFontAtlas atlas, IPluginLog? log)
    {
        this.atlas = atlas ?? throw new ArgumentNullException(nameof(atlas));
        this.log = log;
        Array.Fill(readFrame, -1);
    }

    public bool TryGet(MoonfallFace face, float px, out ImFontPtr font)
    {
        font = default;
        if (handles is null)
        {
            Build();
        }

        var best = Nearest(face, px, Sizes.Length, i => Sizes[i].Face, i => native[i]);
        if (best < 0 || handles?[best] is not { Available: true } handle)
        {
            return false;
        }

        var frame = ImGui.GetFrameCount();
        if (readFrame[best] != frame)
        {
            readFrame[best] = frame;
            using (handle.Push())
            {
                fonts[best] = ImGui.GetFont();
            }
        }

        font = fonts[best];
        return true;
    }

    /// <summary>
    /// Of the <paramref name="count"/> fonts, the one of <paramref name="face"/> to draw <paramref name="px"/> pixels with:
    /// the smallest native size at or above 85% of it (scaled down, never far up), else the largest.
    /// </summary>
    public static int Nearest(MoonfallFace face, float px, int count, Func<int, MoonfallFace> faceOf, Func<int, float> sizeOf)
    {
        ArgumentNullException.ThrowIfNull(faceOf);
        ArgumentNullException.ThrowIfNull(sizeOf);
        var best = -1;
        for (var i = 0; i < count; i++)
        {
            if (faceOf(i) != face)
            {
                continue;
            }

            var enough = sizeOf(i) >= px * 0.85f;
            var bestEnough = best >= 0 && sizeOf(best) >= px * 0.85f;
            if (best < 0 || (enough && (!bestEnough || sizeOf(i) < sizeOf(best))) || (!enough && !bestEnough && sizeOf(i) > sizeOf(best)))
            {
                best = i;
            }
        }

        return best;
    }

    private void Build()
    {
        handles = new IFontHandle?[Sizes.Length];
        for (var i = 0; i < Sizes.Length; i++)
        {
            var style = new GameFontStyle(Sizes[i].Size);
            native[i] = style.SizePx;
            try
            {
                handles[i] = Sizes[i].Face == MoonfallFace.Axis
                    ? atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
                    {
                        var f = tk.AddGameGlyphs(style, null, default);
                        tk.AddDalamudAssetFont(Dalamud.DalamudAsset.NotoSansCjkMedium, new SafeFontConfig { SizePx = style.SizePx, MergeFont = f, GlyphRanges = PunctuationRanges });
                        tk.Font = f;
                    }))
                    : atlas.NewGameFontHandle(style);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                log?.Warning(ex, $"Moonfall: the game font {Sizes[i].Size} could not be built; the window's font stands in");
            }
        }
    }

    public void Dispose()
    {
        if (handles is null)
        {
            return;
        }

        foreach (var handle in handles)
        {
            handle?.Dispose();
        }

        handles = null;
    }
}
