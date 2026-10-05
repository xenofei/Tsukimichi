using System.Text;
using Tsukimichi.Core.Portraits;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Portraits;

/// <summary>
/// The portrait pack's pure rules (feature plan v7 F4): the manifest as the plugin reads it, the image format, and which
/// face a giver wears with the photos that ship with the plugin. No network and no game.
/// </summary>
public sealed class PortraitPackTests
{
    private const uint Alphinaud = 1001000;
    private const uint Hnaanza = 1002000;
    private const uint GrahaTia = 1003000;
    private const uint Stranger = 1009999;

    // ------------------------------------------------------------------ the manifest

    [Fact]
    public void A_manifest_reads_back_what_was_written()
    {
        var manifest = Manifest(("1002000.png", [Hnaanza, 1012527]), ("1003000.png", [GrahaTia]));
        var read = PortraitPackManifest.TryParse(manifest.ToJson(), out var error);

        Assert.Null(error);
        Assert.NotNull(read);
        Assert.Equal(PortraitPackManifest.CurrentFormat, read.Format);
        Assert.Equal("2026.09.15.0000.0000", read.GameVersion);
        Assert.Equal(PortraitPackManifest.ImageSide, read.Side);
        Assert.Equal("1002000.png", read.Entries[1012527]);
        Assert.Equal(3, read.Entries.Count);
        Assert.Equal(manifest.Files["1003000.png"], read.Files["1003000.png"]);
        Assert.Equal(new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc), read.BuiltUtc);
    }

    [Theory]
    [InlineData("1001000.png", true)]
    [InlineData("a_b-c.png", true)]
    [InlineData("../1001000.png", false)]
    [InlineData("portraits/1001000.png", false)]
    [InlineData("..\\1001000.png", false)]
    [InlineData("/1001000.png", false)]
    [InlineData("C:1001000.png", false)]
    [InlineData("1001000.PNG", false)]
    [InlineData("1001000.png.exe", false)]
    [InlineData("1001000.exe", false)]
    [InlineData("1001000.png\n", false)]
    [InlineData(".png", false)]
    [InlineData("", false)]
    public void Image_names_are_flat_png_names(string name, bool safe) =>
        Assert.Equal(safe, PortraitPackManifest.IsSafeFileName(name));

    [Fact]
    public void A_manifest_from_a_newer_format_is_refused_with_a_reason()
    {
        var json = Encoding.UTF8.GetString(Manifest(("1.png", [Alphinaud])).ToJson()).Replace("\"format\": 1", "\"format\": 2", StringComparison.Ordinal);
        Assert.Null(PortraitPackManifest.TryParse(Encoding.UTF8.GetBytes(json), out var error));
        Assert.Contains("newer Tsukimichi", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "format": 1, "files": { "../evil.png": "SHA" }, "entries": { "1": "../evil.png" } }""", "not allowed")]
    [InlineData("""{ "format": 1, "files": { "a.png": "abc" }, "entries": { "1": "a.png" } }""", "SHA-256")]
    [InlineData("""{ "format": 1, "files": { "a.png": "SHA" }, "entries": { "1": "b.png" } }""", "does not list")]
    [InlineData("""{ "format": 1, "files": { "a.png": "SHA" }, "entries": { "x": "a.png" } }""", "not a number")]
    [InlineData("""{ "format": 1, "files": {}, "entries": {} }""", "no portraits")]
    [InlineData("""{ "files": {} }""", "no format")]
    [InlineData("""[1, 2]""", "not a JSON object")]
    [InlineData("""not json""", "not a JSON object")]
    public void A_manifest_that_cannot_be_trusted_is_refused(string json, string reason)
    {
        json = json.Replace("SHA", new string('a', 64), StringComparison.Ordinal);
        Assert.Null(PortraitPackManifest.TryParse(Encoding.UTF8.GetBytes(json), out var error));
        Assert.Contains(reason, error, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ the image format

    [Fact]
    public void Images_round_trip_as_rgba_and_as_a_palette()
    {
        var rgba = Gradient(16, 16);
        Assert.True(PackPng.TryDecode(PackPng.EncodeRgba(rgba, 16, 16), out var w, out var h, out var back));
        Assert.Equal((16, 16), (w, h));
        Assert.Equal(rgba, back);

        byte[] palette = [0, 0, 0, 0, 255, 0, 0, 255, 0, 0, 255, 128];
        byte[] indices = [0, 1, 2, 1, 0, 2, 2, 1, 0];
        Assert.True(PackPng.TryDecode(PackPng.EncodeIndexed(indices, 3, 3, palette), out _, out _, out var paletted));
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, paletted[4..8]);
        Assert.Equal(new byte[] { 0, 0, 255, 128 }, paletted[8..12]);
        Assert.Equal(0, paletted[3]);
    }

    [Fact]
    public void A_damaged_or_oversized_image_does_not_decode()
    {
        var png = PackPng.EncodeRgba(Gradient(16, 16), 16, 16);

        var badCrc = (byte[])png.Clone();
        badCrc[30] ^= 0xFF;
        Assert.False(PackPng.TryDecode(badCrc, out _, out _, out _));

        Assert.False(PackPng.TryDecode(png.AsSpan(0, png.Length - 12), out _, out _, out _));
        Assert.False(PackPng.TryDecode(png, out _, out _, out _, maxSide: 8));
        Assert.False(PackPng.TryDecode("MZ this is a program"u8, out _, out _, out _));
        Assert.False(PackPng.TryDecode([], out _, out _, out _));
    }

    [Fact]
    public void A_pack_image_may_hold_only_the_chunks_the_pack_writes()
    {
        var png = PackPng.EncodeRgba(Gradient(16, 16), 16, 16);
        Assert.True(PackPng.TryDecode(png, out _, out _, out _, strict: true));

        // Bytes after IEND: fine for a photo, refused for a pack image.
        var trailing = png.Concat(Encoding.ASCII.GetBytes("<script>")).ToArray();
        Assert.True(PackPng.TryDecode(trailing, out _, out _, out _));
        Assert.False(PackPng.TryDecode(trailing, out _, out _, out _, strict: true));

        // An ancillary chunk (a text chunk, say) after the header: refused strictly.
        var text = Chunk("tEXt", Encoding.ASCII.GetBytes("Comment\0hello"));
        var withText = png[..33].Concat(text).Concat(png[33..]).ToArray();
        Assert.True(PackPng.TryDecode(withText, out _, out _, out _));
        Assert.False(PackPng.TryDecode(withText, out _, out _, out _, strict: true));
    }

    [Fact]
    public void A_manifest_that_repeats_a_key_is_refused_not_thrown()
    {
        var sha = new string('a', 64);
        var json = $$"""{ "format": 1, "files": { "a.png": "{{sha}}", "a.png": "{{sha}}" }, "entries": { "1": "a.png" } }""";
        Assert.Null(PortraitPackManifest.TryParse(Encoding.UTF8.GetBytes(json), out var error));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("2026.09.15.0000.0000", 2026, 9, 15)]
    [InlineData("2027.01.20.0000.0001", 2027, 1, 20)]
    public void A_pack_is_stamped_with_its_game_versions_date_so_a_rebuild_is_the_same(string gameVersion, int year, int month, int day)
    {
        var built = PortraitPackManifest.BuiltDateOf(gameVersion);
        Assert.Equal(new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc), built);
        Assert.Equal(DateTimeKind.Utc, built.Kind);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("unknown")]
    [InlineData("2026.13.40.0000.0000")]
    public void A_game_version_without_a_date_gives_no_stamp(string? gameVersion) =>
        Assert.Equal(default, PortraitPackManifest.BuiltDateOf(gameVersion));

    // ------------------------------------------------------------------ which face a giver wears

    [Fact]
    public void The_pack_goes_first_and_game_art_stands_in_where_it_has_no_photo()
    {
        // Alphinaud has a Duty Support bust: with his photo in the pack, the photo goes first (spec-1.20 F4, A1) ...
        Assert.Equal(PortraitSource.Pack, Index().WithPack(Installed(new string('a', 64), "portraits-1", Alphinaud)).For(Alphinaud, 70001).Source);

        // ... and without it, the bust stands in.
        var index = Index().WithPack(Installed(new string('a', 64), "portraits-1", Hnaanza, GrahaTia, Stranger));
        Assert.Equal(PortraitSource.TrustBust, index.For(Alphinaud, 70001).Source);

        // H'naanza has no game art: the pack's photo, its icon the NPC id, the whole image, the quest's era.
        var pack = index.For(Hnaanza, 65010);
        Assert.Equal(PortraitSource.Pack, pack.Source);
        Assert.Equal(Hnaanza, pack.Icon);
        Assert.Equal(PortraitCrop.Full, pack.Crop);
        Assert.Equal(0, pack.Era);
        Assert.True(pack.HasArt);
        Assert.Null(pack.Mask);

        // The fallback rides along unchanged, for the spoiler shield and while the texture loads.
        Assert.Equal(Index().For(Hnaanza, 65010).Fallback, pack.Fallback);
    }

    [Fact]
    public void A_giver_whose_game_faces_are_all_later_wears_the_pack_photo_of_their_quest()
    {
        var plain = Index();
        var withPack = plain.WithPack(Installed(new string('a', 64), "portraits-1", GrahaTia));

        // G'raha Tia's only game face is from Shadowbringers: an A Realm Reborn quest gets none from the game ...
        Assert.False(plain.For(GrahaTia, 65020).HasArt);

        // ... and the pack's photo of that exact NPC instead, at the quest's own era.
        var face = withPack.For(GrahaTia, 65020);
        Assert.Equal(PortraitSource.Pack, face.Source);
        Assert.Equal(0, face.Era);

        // His Shadowbringers quest wears the pack's photo too (the pack goes first); without the pack, the bust.
        Assert.Equal(PortraitSource.Pack, withPack.For(GrahaTia, 70020).Source);
        Assert.Equal(PortraitSource.TrustBust, plain.For(GrahaTia, 70020).Source);
    }

    [Fact]
    public void A_seasonal_quest_wears_the_pack_photo_at_the_givers_first_era()
    {
        var index = Index().WithPack(Installed(new string('a', 64), "portraits-1", Hnaanza));
        var face = index.For(Hnaanza, PortraitIndex.SeasonalEra, 0);
        Assert.Equal(PortraitSource.Pack, face.Source);
        Assert.Equal(0, face.Era);
    }

    [Fact]
    public void Without_a_pack_or_without_that_giver_nothing_changes()
    {
        var plain = Index();
        Assert.Same(plain, plain.WithPack(null));
        var pack = Installed(new string('a', 64), "portraits-1", Alphinaud);
        var withPack = plain.WithPack(pack);
        Assert.Same(withPack, withPack.WithPack(pack));
        Assert.Same(pack, withPack.Pack);
        Assert.Equal(plain.For(Hnaanza, 65010), withPack.For(Hnaanza, 65010));
        Assert.Equal(PortraitSource.None, withPack.For(Hnaanza, 65010).Source);
    }

    [Fact]
    public void A_giver_the_index_does_not_know_still_wears_a_pack_photo()
    {
        var face = Index().WithPack(Installed(new string('a', 64), "portraits-1", Stranger)).For(Stranger, 3, 0);
        Assert.Equal(PortraitSource.Pack, face.Source);
        Assert.Equal(3, face.Era);
        Assert.Equal(PortraitFallbackKind.Moon, face.Fallback.Kind);
    }

    [Fact]
    public void The_pack_ranks_before_every_uncurated_game_family_and_is_never_a_curated_family()
    {
        // The 1.22.1 portrait audit (C1): the pack goes first only over game art nobody measured or chose by hand.
        var crops = new PortraitCrops(null, new Dictionary<uint, PortraitCrop> { [72626] = PortraitCrop.FromBox(PortraitSource.TrustBust, 10, 60, 140) });
        Assert.True(PortraitIndex.PackWins(default, crops));
        foreach (var family in PortraitSources.Priority)
        {
            Assert.True(PortraitIndex.PackWins(new PortraitVariant(73999, family, 0, "Someone"), crops));
            Assert.True(PortraitSources.Rank(PortraitSource.Pack) < PortraitSources.Rank(family));
            Assert.False(PortraitIndex.PackWins(new PortraitVariant(73999, family, 0, "Someone", Curated: true), crops));
            Assert.False(PortraitIndex.PackWins(new PortraitVariant(73999, family, 0, "Someone", Crop: PortraitCrop.Full), crops));
        }

        Assert.False(PortraitIndex.PackWins(new PortraitVariant(72626, PortraitSource.TrustBust, 3, "Y'shtola"), crops));

        Assert.False(PortraitSources.TryParse("Pack", out _));
        Assert.Equal((PortraitPackManifest.ImageSide, PortraitPackManifest.ImageSide), PortraitSources.TextureSize(PortraitSource.Pack));
    }

    [Fact]
    public void A_pack_face_is_never_hovered_larger_than_its_head_box()
    {
        var files = new Dictionary<string, string> { ["1002000.png"] = new string('a', 64), ["1003000.png"] = new string('b', 64) };
        var manifest = PortraitPackManifest.Create(
            "2026.09.15.0000.0000",
            new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc),
            "test",
            files,
            new Dictionary<uint, string> { [Hnaanza] = "1002000.png", [GrahaTia] = "1003000.png" },
            boxes: new Dictionary<string, int> { ["1002000.png"] = 92 });
        Assert.Equal(92, PortraitPackManifest.TryParse(manifest.ToJson(), out _)?.Boxes["1002000.png"]);

        var index = Index().WithPack(new PortraitPack(Path.GetTempPath(), manifest, new string('a', 64), "portraits-1"));
        var boxed = index.For(Hnaanza, 65010);
        Assert.Equal(92, boxed.SourceBox);
        Assert.Equal(92f, Core.Ui.PortraitPlate.TooltipSize(boxed, face: true));
        Assert.Equal(Core.Ui.PortraitPlate.TooltipMax, Core.Ui.PortraitPlate.TooltipSize(index.For(GrahaTia, 65020), face: true));
    }

    // ------------------------------------------------------------------ helpers

    internal static PortraitPackManifest Manifest(params (string File, uint[] Ids)[] images)
    {
        var files = images.ToDictionary(i => i.File, i => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(i.File))));
        var entries = images.SelectMany(i => i.Ids.Select(id => (id, i.File))).ToDictionary(e => e.id, e => e.File);
        return PortraitPackManifest.Create("2026.09.15.0000.0000", new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc), "test", files, entries);
    }

    private static PortraitPack Installed(string sha, string tag, params uint[] ids)
    {
        var manifest = Manifest(ids.Length == 0 ? [("1.png", [Hnaanza])] : ids.Select(id => ($"{id}.png", new[] { id })).ToArray());
        return new PortraitPack(Path.Combine(Path.GetTempPath(), "tsukimichi-pack-test"), manifest, sha, tag);
    }

    /// <summary>Alphinaud with a Heavensward bust (era 1 … his quests are era 3), H'naanza without art, G'raha with a Shadowbringers bust.</summary>
    private static PortraitIndex Index() => PortraitIndex.Build(new PortraitInputs(
        [
            new PortraitFace(72621, PortraitSource.TrustBust, "Alphinaud", 1),
            new PortraitFace(72640, PortraitSource.TrustBust, "G'raha Tia", 3),
        ],
        [
            new PortraitGiver(Alphinaud, "Alphinaud", 2, 0),
            new PortraitGiver(Hnaanza, "H'naanza", 4, 1),
            new PortraitGiver(GrahaTia, "G'raha Tia", 4, 0),
        ],
        [
            new PortraitQuest(70001, Alphinaud, 3, 0),
            new PortraitQuest(65010, Hnaanza, 0, 0),
            new PortraitQuest(65020, GrahaTia, 0, 0),
            new PortraitQuest(70020, GrahaTia, 3, 0),
        ],
        new Dictionary<byte, uint>()));

    private static byte[] Chunk(string type, byte[] body)
    {
        var typed = Encoding.ASCII.GetBytes(type).Concat(body).ToArray();
        var length = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, body.Length);
        var crc = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(crc, PortraitMaskFile.Crc32(typed));
        return length.Concat(typed).Concat(crc).ToArray();
    }

    internal static byte[] Gradient(int width, int height)
    {
        var rgba = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            rgba[i * 4] = (byte)(i * 7);
            rgba[(i * 4) + 1] = (byte)(i * 3);
            rgba[(i * 4) + 2] = (byte)(255 - i);
            rgba[(i * 4) + 3] = (byte)(i % 5 == 0 ? 0 : 255);
        }

        return rgba;
    }
}
