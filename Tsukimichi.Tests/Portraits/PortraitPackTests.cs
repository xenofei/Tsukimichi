using System.Text;
using Tsukimichi.Core.Portraits;

namespace Tsukimichi.Tests.Portraits;

/// <summary>
/// The optional portrait pack's pure rules (feature plan v7 F4, decision 8): the manifest and the offer as the plugin
/// reads them, the image format, where the pack stands for the Settings row (an update, a pack for an older game), and
/// which face a giver wears once a pack is installed. No network and no game.
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

    // ------------------------------------------------------------------ the offer

    [Fact]
    public void The_offer_names_the_pinned_release_asset_and_reads_back()
    {
        var offer = Offer();
        Assert.Equal(new Uri("https://github.com/xenofei/Tsukimichi/releases/download/v1.20.0/Tsukimichi-portraits.zip"), offer.DownloadUri);
        Assert.Equal(offer, PortraitPackOffer.Parse(offer.ToJson(), out var warning));
        Assert.Null(warning);
    }

    [Fact]
    public void An_offer_without_a_tag_offers_nothing_quietly()
    {
        Assert.Null(PortraitPackOffer.Parse("""{ "schema": 1, "tag": "" }"""u8, out var warning));
        Assert.Null(warning);
    }

    [Theory]
    [InlineData("../../evil/v1.0.0", "Tsukimichi-portraits.zip", 100)]
    [InlineData("v1.20.0/../..", "Tsukimichi-portraits.zip", 100)]
    [InlineData("main", "Tsukimichi-portraits.zip", 100)]
    [InlineData("v1.20.0", "evil.exe", 100)]
    [InlineData("v1.20.0", "a/b.zip", 100)]
    [InlineData("v1.20.0", "https://evil.example/x.zip", 100)]
    [InlineData("v1.20.0", "Tsukimichi-portraits.zip", 0)]
    [InlineData("v1.20.0", "Tsukimichi-portraits.zip", PortraitPackOffer.MaxSize + 1)]
    public void An_offer_naming_anything_but_a_release_asset_is_refused(string tag, string asset, long size)
    {
        var json = $$"""{ "tag": "{{tag}}", "asset": "{{asset}}", "size": {{size}}, "sha256": "{{new string('a', 64)}}", "format": 1 }""";
        Assert.Null(PortraitPackOffer.Parse(Encoding.UTF8.GetBytes(json), out var warning));
        Assert.NotNull(warning);
    }

    [Theory]
    [InlineData("https://github.com/xenofei/Tsukimichi/releases/download/v1.20.0/Tsukimichi-portraits.zip", true)]
    [InlineData("https://objects.githubusercontent.com/github-production-release-asset/1?sig=x", true)]
    [InlineData("https://release-assets.githubusercontent.com/github-production-release-asset/1?sig=x", true)]
    [InlineData("http://github.com/xenofei/Tsukimichi/releases/download/v1.20.0/Tsukimichi-portraits.zip", false)]
    [InlineData("https://github.com/xenofei/Tsukimichi/releases/download/v1.19.0/Tsukimichi-portraits.zip", false)]
    [InlineData("https://github.com/someone/else/releases/download/v1.20.0/Tsukimichi-portraits.zip", false)]
    [InlineData("https://objects.githubusercontent.com.evil.example/x", false)]
    [InlineData("https://evil.example/objects.githubusercontent.com", false)]
    [InlineData("http://objects.githubusercontent.com/x", false)]
    [InlineData("https://objects.githubusercontent.com:8443/x", false)]
    [InlineData("https://user:pass@objects.githubusercontent.com/x", false)]
    public void Only_the_pinned_address_and_GitHub_asset_hosts_are_allowed(string uri, bool allowed) =>
        Assert.Equal(allowed, Offer().Allows(new Uri(uri)));

    [Theory]
    [InlineData(15_000_000L, "14.3 MB")]
    [InlineData(2048L, "2 KB")]
    [InlineData(10L, "1 KB")]
    public void Sizes_read_in_megabytes_or_kilobytes(long bytes, string text) =>
        Assert.Equal(text, PortraitPackOffer.SizeText(bytes));

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

    // ------------------------------------------------------------------ where the pack stands

    [Fact]
    public void The_row_offers_download_update_or_remove_by_what_is_installed()
    {
        var offer = Offer();
        Assert.Equal(PortraitPackState.NotOffered, PortraitPackStatus.Of(null, null, damaged: false));
        Assert.Equal(PortraitPackState.Available, PortraitPackStatus.Of(offer, null, damaged: false));
        Assert.Equal(PortraitPackState.Damaged, PortraitPackStatus.Of(offer, null, damaged: true));
        Assert.Equal(PortraitPackState.Installed, PortraitPackStatus.Of(offer, Installed(offer.Sha256, "v1.20.0"), damaged: false));
        Assert.Equal(PortraitPackState.Installed, PortraitPackStatus.Of(null, Installed(offer.Sha256, "v1.20.0"), damaged: false));

        // A plugin update shipping a different pack offers it; nothing downloads by itself.
        Assert.Equal(PortraitPackState.UpdateAvailable, PortraitPackStatus.Of(offer, Installed(new string('b', 64), "v1.19.0"), damaged: false));

        // Back on an older plugin, a newer installed pack is kept, never "updated" to the older one.
        Assert.Equal(PortraitPackState.Installed, PortraitPackStatus.Of(offer, Installed(new string('b', 64), "v1.21.0"), damaged: false));
    }

    [Theory]
    [InlineData("2026.09.15.0000.0000", "2026.11.03.0000.0000", true)]
    [InlineData("2026.09.15.0000.0000", "2026.09.15.0000.0000", false)]
    [InlineData("2026.09.15", "2026.09.15.0000.0000", false)]
    [InlineData("2026.11.03.0000.0000", "2026.09.15.0000.0000", false)]
    [InlineData("", "2026.09.15.0000.0000", false)]
    [InlineData("2026.09.15.0000.0000", "", false)]
    public void A_pack_for_an_older_game_is_still_used_and_said_so(string pack, string client, bool older) =>
        Assert.Equal(older, PortraitPackStatus.ForOlderGame(pack, client));

    [Theory]
    [InlineData("v1.20.0", "v1.21.0", -1)]
    [InlineData("v1.21.0", "v1.20.0", 1)]
    [InlineData("v1.20.0", "v1.20.0", 0)]
    [InlineData("v1.9.0", "v1.10.0", -1)]
    [InlineData("junk", "v1.0.0", -1)]
    public void Release_tags_compare_as_versions(string a, string b, int sign) =>
        Assert.Equal(sign, Math.Sign(PortraitPackStatus.CompareTags(a, b)));

    // ------------------------------------------------------------------ which face a giver wears

    [Fact]
    public void Game_art_is_kept_and_the_pack_fills_only_where_the_game_has_none()
    {
        var index = Index().WithPack(Installed(new string('a', 64), "v1.20.0", Alphinaud, Hnaanza, GrahaTia, Stranger));

        // Alphinaud has a Duty Support bust from his era: kept, hand-framed game art wins.
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
        var withPack = plain.WithPack(Installed(new string('a', 64), "v1.20.0", GrahaTia));

        // G'raha Tia's only game face is from Shadowbringers: an A Realm Reborn quest gets none from the game ...
        Assert.False(plain.For(GrahaTia, 65020).HasArt);

        // ... and the pack's photo of that exact NPC instead, at the quest's own era.
        var face = withPack.For(GrahaTia, 65020);
        Assert.Equal(PortraitSource.Pack, face.Source);
        Assert.Equal(0, face.Era);

        // His Shadowbringers quest keeps the Duty Support bust.
        Assert.Equal(PortraitSource.TrustBust, withPack.For(GrahaTia, 70020).Source);
    }

    [Fact]
    public void A_seasonal_quest_wears_the_pack_photo_at_the_givers_first_era()
    {
        var index = Index().WithPack(Installed(new string('a', 64), "v1.20.0", Hnaanza));
        var face = index.For(Hnaanza, PortraitIndex.SeasonalEra, 0);
        Assert.Equal(PortraitSource.Pack, face.Source);
        Assert.Equal(0, face.Era);
    }

    [Fact]
    public void Without_a_pack_or_without_that_giver_nothing_changes()
    {
        var plain = Index();
        Assert.Same(plain, plain.WithPack(null));
        var pack = Installed(new string('a', 64), "v1.20.0", Alphinaud);
        var withPack = plain.WithPack(pack);
        Assert.Same(withPack, withPack.WithPack(pack));
        Assert.Same(pack, withPack.Pack);
        Assert.Equal(plain.For(Hnaanza, 65010), withPack.For(Hnaanza, 65010));
        Assert.Equal(PortraitSource.None, withPack.For(Hnaanza, 65010).Source);
    }

    [Fact]
    public void A_giver_the_index_does_not_know_still_wears_a_pack_photo()
    {
        var face = Index().WithPack(Installed(new string('a', 64), "v1.20.0", Stranger)).For(Stranger, 3, 0);
        Assert.Equal(PortraitSource.Pack, face.Source);
        Assert.Equal(3, face.Era);
        Assert.Equal(PortraitFallbackKind.Moon, face.Fallback.Kind);
    }

    [Fact]
    public void The_pack_ranks_after_every_game_family_and_is_never_a_curated_family()
    {
        Assert.True(PortraitIndex.PackWins(PortraitSource.None));
        foreach (var family in PortraitSources.Priority)
        {
            Assert.False(PortraitIndex.PackWins(family));
            Assert.True(PortraitSources.Rank(family) < PortraitSources.Rank(PortraitSource.Pack));
        }

        Assert.False(PortraitSources.TryParse("Pack", out _));
        Assert.Equal((PortraitPackManifest.ImageSide, PortraitPackManifest.ImageSide), PortraitSources.TextureSize(PortraitSource.Pack));
    }

    // ------------------------------------------------------------------ helpers

    internal static PortraitPackOffer Offer(string sha = "", long size = 1000) =>
        new("v1.20.0", "Tsukimichi-portraits.zip", size, sha.Length > 0 ? sha : new string('a', 64), 1, "2026.09.15.0000.0000", 3);

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
