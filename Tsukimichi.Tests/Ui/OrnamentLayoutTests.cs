using System.Buffers.Binary;
using System.Text.Json;
using Tsukimichi.Core.Ui;
using Tsukimichi.Tests.Data;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The ornament atlas (Moon Road proposal §5): the const layout in <see cref="OrnamentLayout"/> against the
/// <c>ornaments.json</c> that <c>gen_atlas.py</c> writes, the PNGs' sizes, and the sources the glyphs come from.
/// </summary>
public sealed class OrnamentLayoutTests
{
    public static string RepoRoot() => Path.GetFullPath(Path.Combine(FixtureCatalog.ShippedDataDir(), "..", ".."));

    public static string AssetsDir() => Path.Combine(RepoRoot(), "Tsukimichi", "assets", "ui");

    /// <summary>Width and height from a PNG's IHDR chunk.</summary>
    public static (int Width, int Height) PngSize(string path)
    {
        var header = new byte[24];
        using (var stream = File.OpenRead(path))
        {
            stream.ReadExactly(header);
        }

        Assert.Equal(0x89, header[0]);
        Assert.Equal((byte)'P', header[1]);
        return (BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20)));
    }

    [Fact]
    public void Layout_matches_the_generated_json()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AssetsDir(), "ornaments.json")));
        var root = json.RootElement;
        Assert.Equal(OrnamentLayout.Width, root.GetProperty("size")[0].GetInt32());
        Assert.Equal(OrnamentLayout.Height, root.GetProperty("size")[1].GetInt32());
        var sprites = root.GetProperty("sprites");

        void Check(string key, AtlasRect rect)
        {
            Assert.True(sprites.TryGetProperty(key, out var r), $"ornaments.json has no sprite {key}");
            Assert.Equal(new AtlasRect(r[0].GetInt32(), r[1].GetInt32(), r[2].GetInt32(), r[3].GetInt32()), rect);
        }

        foreach (var glyph in Enum.GetValues<OrnamentGlyph>().Where(g => g != OrnamentGlyph.None))
        {
            Check(OrnamentLayout.GlyphKey(glyph), OrnamentLayout.Glyph(glyph));
        }

        foreach (var sprite in Enum.GetValues<OrnamentSprite>())
        {
            Check(OrnamentLayout.SpriteKey(sprite), OrnamentLayout.Sprite(sprite));
        }

        Assert.Equal(OrnamentLayout.GlyphCount + Enum.GetValues<OrnamentSprite>().Length, sprites.EnumerateObject().Count());
    }

    [Fact]
    public void Atlas_pngs_are_the_layout_size_and_2x()
    {
        Assert.Equal((OrnamentLayout.Width, OrnamentLayout.Height), PngSize(Path.Combine(AssetsDir(), "ornaments.png")));
        Assert.Equal((OrnamentLayout.Width * 2, OrnamentLayout.Height * 2), PngSize(Path.Combine(AssetsDir(), "ornaments@2x.png")));
    }

    [Fact]
    public void Sprites_fit_the_atlas_and_never_touch()
    {
        var rects = Enum.GetValues<OrnamentGlyph>().Where(g => g != OrnamentGlyph.None).Select(OrnamentLayout.Glyph)
            .Concat(Enum.GetValues<OrnamentSprite>().Select(OrnamentLayout.Sprite)).ToArray();
        Assert.Equal(17, OrnamentLayout.GlyphCount);
        foreach (var r in rects)
        {
            Assert.True(r.X >= 1 && r.Y >= 1 && r.X + r.Width <= OrnamentLayout.Width - 1 && r.Y + r.Height <= OrnamentLayout.Height - 1, r.ToString());
        }

        // At least one transparent pixel between neighbours, so bilinear sampling never bleeds.
        for (var i = 0; i < rects.Length; i++)
        {
            for (var j = i + 1; j < rects.Length; j++)
            {
                var a = rects[i];
                var b = rects[j];
                var apart = a.X + a.Width + 1 <= b.X || b.X + b.Width + 1 <= a.X || a.Y + a.Height + 1 <= b.Y || b.Y + b.Height + 1 <= a.Y;
                Assert.True(apart, $"{a} and {b} touch");
            }
        }

        Assert.Equal(default, OrnamentLayout.Glyph(OrnamentGlyph.None));
    }

    [Fact]
    public void Every_glyph_has_its_svg_source_at_1x_and_2x()
    {
        var dir = Path.Combine(RepoRoot(), "docs", "design", "moon-road", "ornaments");
        foreach (var glyph in Enum.GetValues<OrnamentGlyph>().Where(g => g != OrnamentGlyph.None))
        {
            var key = OrnamentLayout.GlyphKey(glyph);
            Assert.True(File.Exists(Path.Combine(dir, key + ".svg")), key);
            Assert.True(File.Exists(Path.Combine(dir, key + "@2x.svg")), key);
        }
    }

    [Fact]
    public void Uvs_span_the_rectangle()
    {
        var crest = OrnamentLayout.Sprite(OrnamentSprite.Crest);
        Assert.Equal((2f / 256f, 2f / 128f), crest.Uv0);
        Assert.Equal((42f / 256f, 42f / 128f), crest.Uv1);
    }
}
