using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.GameFonts;
using Lumina.Data.Files;
using Tsukimichi.Ui;
using LuminaGameData = Lumina.GameData;

namespace Tsukimichi.MoonfallRender;

/// <summary>
/// The game's own fonts for the offline render (Jupiter, AXIS and TrumpGothic at the plugin's native sizes), loaded
/// from the install's glyph tables (<c>common/font/*.fdt</c>, read with Dalamud's <see cref="FdtReader"/>) and font
/// textures into the headless ImGui atlas as custom glyphs, as Dalamud builds its game fonts. AXIS also takes ImGui's
/// default glyphs for what the game's face lacks, as the plugin merges Dalamud's punctuation into it.
/// </summary>
internal sealed unsafe class GameFonts : IMoonfallFonts
{
    private static readonly (MoonfallFace Face, string File)[] Files =
    [
        (MoonfallFace.Jupiter, "Jupiter_23"),
        (MoonfallFace.Jupiter, "Jupiter_46"),
        (MoonfallFace.Axis, "AXIS_14"),
        (MoonfallFace.Axis, "AXIS_18"),
        (MoonfallFace.Axis, "AXIS_36"),
        (MoonfallFace.Trump, "TrumpGothic_34"),
        (MoonfallFace.Trump, "TrumpGothic_68"),
    ];

    private readonly List<Pending> pending = [];
    private readonly List<(MoonfallFace Face, float Px, ImFontPtr Font)> fonts = [];
    private MoonfallFace[] faces = [];
    private float[] sizes = [];

    private sealed record Pending(MoonfallFace Face, float Px, ImFontPtr Font, FdtReader Fdt, List<(int Rect, FdtReader.FontTableEntry Glyph)> Rects);

    /// <summary>Adds every face to <paramref name="atlas"/> before it is built.</summary>
    public void Add(ImFontAtlasPtr atlas, LuminaGameData game)
    {
        foreach (var (face, file) in Files)
        {
            var bytes = game.GetFile($"common/font/{file}.fdt")?.Data ?? throw new InvalidOperationException($"common/font/{file}.fdt is missing");
            var fdt = new FdtReader(bytes);
            var px = fdt.FontHeader.Size * 4f / 3f;
            var config = ImGui.ImFontConfig();
            config.SizePixels = px;
            // A container font: ImGui's default glyphs for AXIS (what the game's face lacks), else a single unused one.
            var ranges = (ushort*)NativeMemory.Alloc(3, sizeof(ushort));
            ranges[0] = face == MoonfallFace.Axis ? (ushort)0x20 : (ushort)0x7F;
            ranges[1] = face == MoonfallFace.Axis ? (ushort)0xFF : (ushort)0x7F;
            ranges[2] = 0;
            config.GlyphRanges = ranges;
            var font = atlas.AddFontDefault(config);
            var rects = new List<(int, FdtReader.FontTableEntry)>();
            foreach (var glyph in fdt.Glyphs)
            {
                var cp = glyph.CharInt;
                if (cp is <= 0 or > 0xFFFF || glyph.BoundingWidth == 0 && cp != ' ')
                {
                    continue;
                }

                var id = atlas.AddCustomRectFontGlyph(font, (ushort)cp, Math.Max(1, (int)glyph.BoundingWidth), Math.Max(1, (int)glyph.BoundingHeight),
                    glyph.AdvanceWidth, new System.Numerics.Vector2(0, glyph.CurrentOffsetY));
                rects.Add((id, glyph));
            }

            pending.Add(new Pending(face, px, font, fdt, rects));
        }
    }

    /// <summary>After the atlas is built: copies each glyph's pixels from the game's font textures into it, and sets each font's metrics.</summary>
    public void Fill(ImFontAtlasPtr atlas, LuminaGameData game, IReadOnlyList<(nint Pixels, int Width, int Height)> pages)
    {
        var textures = new Dictionary<int, TexFile>();
        foreach (var p in pending)
        {
            foreach (var (rect, glyph) in p.Rects)
            {
                var r = atlas.GetCustomRectByIndex(rect);
                var (page, width, _) = pages[(int)r->TextureIndex];
                var pixels = (byte*)page;
                if (!textures.TryGetValue(glyph.TextureFileIndex, out var tex))
                {
                    tex = game.GetFile<TexFile>($"common/font/font{glyph.TextureFileIndex + 1}.tex") ?? throw new InvalidOperationException("a font texture is missing");
                    textures[glyph.TextureFileIndex] = tex;
                }

                var src = tex.ImageData;
                var tw = tex.Header.Width;
                var channel = glyph.TextureChannelByteIndex;
                for (var y = 0; y < glyph.BoundingHeight; y++)
                {
                    for (var x = 0; x < glyph.BoundingWidth; x++)
                    {
                        var s = ((((glyph.TextureOffsetY + y) * tw) + glyph.TextureOffsetX + x) * 4) + channel;
                        var d = (((r->Y + y) * width) + r->X + x) * 4;
                        pixels[d] = pixels[d + 1] = pixels[d + 2] = 255;
                        pixels[d + 3] = src[s];
                    }
                }
            }

            // The caps' ink height (of "H", rows over 40%) against the font's size, as the design measured it (r2font.cap_box).
            if (Environment.GetEnvironmentVariable("MOONFALL_CAPS") is not null && p.Rects.FirstOrDefault(r => r.Glyph.CharInt == 'H') is { Glyph.BoundingHeight: > 0 } hr)
            {
                var g = hr.Glyph;
                var tex = textures[g.TextureFileIndex];
                int top = -1, bottom = -1;
                for (var y = 0; y < g.BoundingHeight; y++)
                {
                    for (var x = 0; x < g.BoundingWidth; x++)
                    {
                        if (tex.ImageData[((((g.TextureOffsetY + y) * tex.Header.Width) + g.TextureOffsetX + x) * 4) + g.TextureChannelByteIndex] > 102)
                        {
                            top = top < 0 ? y : top;
                            bottom = y;
                            break;
                        }
                    }
                }

                Console.WriteLine($"{p.Face} {p.Px:0.0}px: caps {bottom - top + 1} px, ratio {(bottom - top + 1) / p.Px:0.000}, top {top + g.CurrentOffsetY}, ascent {p.Fdt.FontHeader.Ascent}, line {p.Fdt.FontHeader.LineHeight}, size {p.Fdt.FontHeader.Size}, em {p.Rects.Max(r => r.Glyph.BoundingHeight)}");
            }

            p.Font.Ascent = p.Fdt.FontHeader.Ascent;
            p.Font.Descent = -p.Fdt.FontHeader.Descent;
            p.Font.FontSize = p.Px;
            fonts.Add((p.Face, p.Px, p.Font));
        }

        faces = fonts.Select(static f => f.Face).ToArray();
        sizes = fonts.Select(static f => f.Px).ToArray();
    }

    public bool TryGet(MoonfallFace face, float px, out ImFontPtr font)
    {
        var best = MoonfallFonts.Nearest(face, px, faces.AsSpan(0, fonts.Count), sizes.AsSpan(0, fonts.Count));
        font = best >= 0 ? fonts[best].Font : default;
        return best >= 0;
    }

    public void Dispose()
    {
    }
}
