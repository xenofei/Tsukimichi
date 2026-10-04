using Tsukimichi.Core.Releases;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Releases;

/// <summary>
/// The What's new popup's geometry and its pictures (spec-1.22 W1 "Anatomy" and "Text size", W2 "Cost"): the notes
/// block keeps the tallest page's height so ‹ › and the footer never move, capped at 80 % of the screen; a missing
/// picture is no picture and never moves the layout; Plain loads none; the shipped pictures keep the recipe's size.
/// </summary>
public sealed class WhatsNewLayoutTests
{
    [Fact]
    public void The_notes_block_is_the_tallest_page_plus_its_room()
    {
        // At UI scale 1: the tallest page is 300 px, so the block is 314 px, whichever page is showing.
        var block = WhatsNewLayout.NotesBlock(300f, WhatsNewLayout.FixedHeight(Flair.Full, artBand: true, 1f), 1080f, 1f);
        Assert.Equal(314f, block);

        // At UI scale 2 the room doubles too.
        Assert.Equal(628f, WhatsNewLayout.NotesBlock(600f, WhatsNewLayout.FixedHeight(Flair.Full, artBand: true, 2f), 2160f, 2f));
    }

    [Fact]
    public void A_tall_page_is_capped_so_the_popup_stays_within_80_percent_of_the_screen()
    {
        // 150 % text on a 1080 px screen: header 48 + art 220 + footer 56 = 324; the cap is 864 - 324 = 540.
        var fixedHeight = WhatsNewLayout.FixedHeight(Flair.Full, artBand: true, 1f);
        Assert.Equal(324f, fixedHeight);
        var block = WhatsNewLayout.NotesBlock(900f, fixedHeight, 1080f, 1f);
        Assert.Equal(540f, block);
        Assert.True(fixedHeight + block <= 1080f * WhatsNewLayout.ScreenShare);

        // A tiny screen still leaves a line of notes.
        Assert.Equal(WhatsNewLayout.MinBlock, WhatsNewLayout.NotesBlock(900f, fixedHeight, 300f, 1f));
    }

    [Fact]
    public void Plain_and_a_missing_picture_never_draw_an_art_band_and_the_band_is_on_every_page_or_none()
    {
        // No page has its picture: no band, at any level; the popup is header, notes and footer.
        Assert.False(WhatsNewLayout.ArtBand(Flair.Full, [false, false, false]));
        Assert.False(WhatsNewLayout.ArtBand(Flair.Quiet, []));
        Assert.Equal(48f + 56f, WhatsNewLayout.FixedHeight(Flair.Full, artBand: false, 1f));

        // One page has it: the band is there on every page (a page without one shows the flat sky), so paging never
        // moves the footer.
        Assert.True(WhatsNewLayout.ArtBand(Flair.Full, [false, true, false]));
        Assert.True(WhatsNewLayout.ArtBand(Flair.Quiet, [true]));

        // Plain never has one, and its header and footer are the ledger's bands.
        Assert.False(WhatsNewLayout.ArtBand(Flair.Plain, [true, true]));
        Assert.Equal(26f + 30f, WhatsNewLayout.FixedHeight(Flair.Plain, artBand: false, 1f));
    }

    [Fact]
    public void A_missing_picture_falls_back_to_none()
    {
        const string dir = "plugin";
        var none = new Func<string, bool>(static _ => false);
        Assert.Null(ReleaseArt.Find(dir, "1.22.0", Flair.Full, "ishgard-glass", none));

        var shipped = Path.Combine(dir, "assets", "whatsnew", "1.22.0", "ishgard-glass.jpg");
        var classic = Path.Combine(dir, "assets", "whatsnew", "1.22.0", "classic.jpg");
        var only = new Func<string, bool>(path => path == shipped || path == classic);

        // Full shows the theme's own picture; Quiet shows Classic's (the painting as painted); Plain loads nothing.
        Assert.Equal(shipped, ReleaseArt.Find(dir, "1.22.0.0", Flair.Full, "ishgard-glass", only));
        Assert.Equal(classic, ReleaseArt.Find(dir, "1.22.0", Flair.Quiet, "ishgard-glass", only));
        Assert.Null(ReleaseArt.Find(dir, "1.22.0", Flair.Plain, "ishgard-glass", static _ => true));

        // Another theme's picture is never borrowed, and a version or key that is not one is no picture.
        Assert.Null(ReleaseArt.Find(dir, "1.22.0", Flair.Full, "aether-crystal", only));
        Assert.Null(ReleaseArt.Find(dir, "Unreleased", Flair.Full, "ishgard-glass", static _ => true));
        Assert.Null(ReleaseArt.Find(dir, "1.22.0", Flair.Full, "../../secrets", static _ => true));
    }

    [Fact]
    public void The_one_picture_held_fits_the_12_MB_texture_budget()
    {
        Assert.Equal(1120L * 440 * 4, ReleaseArt.TextureBytes);
        Assert.Equal(560L * 220 * 4, ReleaseArt.TextureBytes1x);

        // At UI scale 1 the 536 px band keeps the 1x copy; only a band wider than 560 px keeps the full picture.
        Assert.Equal((560, 220), ReleaseArt.TierFor(536f));
        Assert.Equal((560, 220), ReleaseArt.TierFor(560f));
        Assert.Equal((1120, 440), ReleaseArt.TierFor(804f));
    }

    [Fact]
    public void Every_shipped_picture_is_a_1120_by_440_jpeg_for_a_release_with_notes_and_a_registered_theme()
    {
        // The art pipeline ships the pictures once they are approved; until then the folder may be absent or empty.
        var root = Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", ReleaseArt.Folder);
        if (!Directory.Exists(root))
        {
            return;
        }

        var notes = ReleaseNotes.Load(Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Data", "curated", ReleaseNotes.FileName));
        foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        {
            var version = Path.GetFileName(Path.GetDirectoryName(file))!;
            var theme = Path.GetFileNameWithoutExtension(file);
            Assert.Equal(ReleaseArt.Extension, Path.GetExtension(file));
            Assert.True(notes.Find(version) is not null, $"{file}: {version} has no whats_new.json entry");
            Assert.True(ThemePresets.TryGet(theme, out _), $"{file}: {theme} is not a theme id");
            Assert.Equal((ReleaseArt.Width, ReleaseArt.Height), JpegSize(file));
        }
    }

    /// <summary>A baseline or progressive JPEG's width and height, from its first start-of-frame marker.</summary>
    internal static (int Width, int Height) JpegSize(string path)
    {
        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > 4 && bytes[0] == 0xFF && bytes[1] == 0xD8, $"{path} is not a JPEG");
        var i = 2;
        while (i + 9 < bytes.Length)
        {
            if (bytes[i] != 0xFF)
            {
                i++;
                continue;
            }

            var marker = bytes[i + 1];
            var length = (bytes[i + 2] << 8) | bytes[i + 3];
            if (marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC))
            {
                return ((bytes[i + 7] << 8) | bytes[i + 8], (bytes[i + 5] << 8) | bytes[i + 6]);
            }

            i += 2 + length;
        }

        Assert.Fail($"{path} has no start-of-frame marker");
        return default;
    }
}
