using Tsukimichi.Core.Umbra;

namespace Tsukimichi.Tests.Umbra;

/// <summary>
/// Reading Umbra's saved settings, read-only (plan v8 M3, decision 4; <see cref="UmbraSettings"/>): which profile file a
/// character uses, the toolbar's keys, the colour profile's encoding (base64 of raw deflate of JSON, colours 0xAABBGGRR),
/// and that a corrupt, cut-off or foreign file is reported, never thrown. The sample fixture carries Umbra's own
/// built-in "Umbra" and "YoRHa Light" profiles as Umbra stores them; the corrupt one is the sample cut off mid-write.
/// </summary>
public sealed class UmbraSettingsTests
{
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void The_sample_reads_its_toolbar_and_colour_profile()
    {
        var read = UmbraSettings.Parse(Fixture("umbra-sample.profile.json"));

        Assert.Null(read.Problem);
        Assert.Equal(new UmbraToolbar(Enabled: true, TopAligned: true, AutoHide: false, Stretched: true, Height: 32, YOffset: 0, UiScalePercent: 100), read.Toolbar);
        Assert.True(read.Toolbar!.HoldsEdge);
        Assert.Equal(1f, read.Toolbar.Scale);
        Assert.False(read.CustomPluginsOn);
        Assert.False(read.AddonListed);

        var colors = read.Colors!;
        Assert.Equal("Umbra (built-in)", colors.Name);
        Assert.Equal(87, colors.Colors.Count);

        // Stored 0xAABBGGRR: Umbra's default window is #212021, its text #DCDCDC, its accent the bronze #B98E4C.
        Assert.Equal(0xFF212021u, colors.Colors["Window.Background"]);
        Assert.Equal(new System.Numerics.Vector4(0x21 / 255f, 0x20 / 255f, 0x21 / 255f, 1f), colors.Get("Window.Background", default));
        Assert.Equal(0xB98E4Cu, Core.Ui.ColorMath.ToHex(UmbraSettings.FromAbgr(colors.Colors["Window.AccentColor"])));
        Assert.Equal(0xDCDCDCu, Core.Ui.ColorMath.ToHex(UmbraSettings.FromAbgr(colors.Colors["Window.Text"])));
    }

    [Fact]
    public void The_profile_in_use_is_the_one_ColorProfileName_names()
    {
        var json = Fixture("umbra-sample.profile.json").Replace("\"Umbra (built-in)\"", "\"YoRHa Light (built-in)\"", StringComparison.Ordinal);
        var colors = UmbraSettings.Parse(json).Colors!;
        Assert.Equal("YoRHa Light (built-in)", colors.Name);

        // YoRHa Light's toolbar is the warm paper #DCD8C0, as the 1.22 renders decode it.
        Assert.Equal(0xDCD8C0u, Core.Ui.ColorMath.ToHex(UmbraSettings.FromAbgr(colors.Colors["Toolbar.Background1"])));
    }

    [Fact]
    public void A_corrupt_file_is_reported_not_thrown()
    {
        var read = UmbraSettings.Parse(Fixture("umbra-corrupt.profile.json"));
        Assert.Null(read.Toolbar);
        Assert.Null(read.Colors);
        Assert.Equal("not JSON", read.Problem);

        Assert.Equal("empty", UmbraSettings.Parse(null).Problem);
        Assert.Equal("empty", UmbraSettings.Parse("   ").Problem);
        Assert.Equal("not an object", UmbraSettings.Parse("[1, 2]").Problem);
    }

    [Fact]
    public void Unreadable_colour_data_keeps_the_toolbar()
    {
        var sample = Fixture("umbra-sample.profile.json");
        foreach (var data in new[] { "not base64 !!", Convert.ToBase64String("plain text, not deflate"u8.ToArray()), UmbraSettings.EncodeProfiles(new Dictionary<string, IReadOnlyDictionary<string, uint>>()) })
        {
            var json = System.Text.RegularExpressions.Regex.Replace(sample, "\"ColorProfileData\": \"[^\"]*\"", "\"ColorProfileData\": \"" + data + "\"");
            var read = UmbraSettings.Parse(json);
            Assert.NotNull(read.Toolbar);
            Assert.Null(read.Colors);
            Assert.NotNull(read.Problem);
        }

        // A profile name the data does not hold.
        var missing = UmbraSettings.Parse(sample.Replace("\"Umbra (built-in)\"", "\"Gone\"", StringComparison.Ordinal));
        Assert.Equal("colour profile not found", missing.Problem);
    }

    [Fact]
    public void Missing_keys_read_as_Umbras_defaults_and_odd_values_are_held()
    {
        var read = UmbraSettings.Parse("﻿{ \"Toolbar.IsTopAligned\": false, \"Toolbar.Height\": 99999, \"General.UiScale\": \"big\" }");
        Assert.Equal(new UmbraToolbar(Enabled: true, TopAligned: false, AutoHide: false, Stretched: true, Height: 512, YOffset: 0, UiScalePercent: 100), read.Toolbar);
        Assert.Equal("no colour profile", read.Problem);

        Assert.Equal("no toolbar settings", UmbraSettings.Parse("{ \"Something.Else\": 1 }").Problem);
    }

    [Fact]
    public void The_add_on_and_custom_plugins_are_read_from_Umbras_plugin_list()
    {
        var read = UmbraSettings.Parse("{ \"Toolbar.Height\": 32, \"CustomPlugins.Enabled\": true, \"PluginEntries\": \"[{\\\"Repo\\\":\\\"xenofei/Tsukimichi.Umbra\\\"}]\" }");
        Assert.True(read.CustomPluginsOn);
        Assert.True(read.AddonListed);
    }

    [Fact]
    public void A_character_uses_its_mapped_profile_or_Default()
    {
        const string map = "{ \"18014498000000001\": \"Raid\", \"18014498000000002\": \"..\\\\escape\", \"18014498000000003\": \"\" }";
        Assert.Equal("Raid.profile.json", UmbraSettings.ProfileFileName(map, 18014498000000001));
        Assert.Equal("Default.profile.json", UmbraSettings.ProfileFileName(map, 18014498000000009));
        Assert.Equal("Default.profile.json", UmbraSettings.ProfileFileName(map, null));
        Assert.Equal("Default.profile.json", UmbraSettings.ProfileFileName(null, 18014498000000001));
        Assert.Equal("Default.profile.json", UmbraSettings.ProfileFileName("{ not json", 18014498000000001));

        // A name that would leave Umbra's folder, or an empty one, is never used.
        Assert.Equal("Default.profile.json", UmbraSettings.ProfileFileName(map, 18014498000000002));
        Assert.Equal("Default.profile.json", UmbraSettings.ProfileFileName(map, 18014498000000003));
    }

    [Fact]
    public void Profiles_round_trip_through_Umbras_encoding()
    {
        var profiles = new Dictionary<string, IReadOnlyDictionary<string, uint>>
        {
            ["Mine"] = new Dictionary<string, uint> { ["Window.Background"] = UmbraSettings.ToAbgr(0x102030), ["Window.Text"] = UmbraSettings.ToAbgr(0xF0E0D0, 0x80) },
        };
        var decoded = UmbraSettings.DecodeProfiles(UmbraSettings.EncodeProfiles(profiles))!;
        Assert.Equal(UmbraSettings.ToAbgr(0x102030), decoded["Mine"]["Window.Background"]);
        Assert.Equal(0x80D0E0F0u, decoded["Mine"]["Window.Text"]);
        Assert.Equal(0x102030u, Core.Ui.ColorMath.ToHex(UmbraSettings.FromAbgr(decoded["Mine"]["Window.Background"])));
    }
}
