using System.Text.Json.Nodes;
using Tsukimichi.Core.Discovery;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Storage;

namespace Tsukimichi.Tests.Discovery;

public sealed class DiscoverySettingsTests : IDisposable
{
    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    [Fact]
    public void Defaults_show_the_entry_hide_it_when_empty_and_include_other_jobs()
    {
        var settings = new DiscoverySettings();

        Assert.True(settings.ShowDtrEntry);
        Assert.False(settings.DtrShowWhenEmpty);
        Assert.True(settings.NearbyIncludeOtherJob);
    }

    [Fact]
    public void The_zones_board_view_chips_and_sort_round_trip_and_default_to_Here_every_kind_and_level_fit()
    {
        var defaults = new DiscoverySettings();
        Assert.False(defaults.NearbyEverywhere);
        Assert.Equal(Core.Query.ZoneKinds.All, defaults.NearbyKinds);
        Assert.Equal(Core.Query.ZoneSort.LevelFit, defaults.NearbySort);

        var path = tmp.File(Path.Combine("user", "discovery.json"));
        new DiscoverySettings { NearbyEverywhere = true, NearbyKinds = Core.Query.ZoneKinds.Ready | Core.Query.ZoneKinds.Rewards, NearbySort = Core.Query.ZoneSort.StoryOrder }.Save(path);
        var loaded = DiscoverySettings.Load(path);
        Assert.True(loaded.NearbyEverywhere);
        Assert.Equal(Core.Query.ZoneKinds.Ready | Core.Query.ZoneKinds.Rewards, loaded.NearbyKinds);
        Assert.Equal(Core.Query.ZoneSort.StoryOrder, loaded.NearbySort);
    }

    [Fact]
    public void PathFor_lives_under_the_user_directory()
    {
        var paths = new PluginPaths(tmp.Path, tmp.Path);

        Assert.Equal(Path.Combine(paths.UserDir, "discovery.json"), DiscoverySettings.PathFor(paths));
    }

    [Fact]
    public void Round_trip_keeps_every_value_and_creates_the_directory()
    {
        var path = tmp.File(Path.Combine("user", "discovery.json"));
        var settings = new DiscoverySettings { ShowDtrEntry = false, DtrShowWhenEmpty = true, NearbyIncludeOtherJob = false };

        settings.Save(path);
        var warnings = new List<string>();
        var loaded = DiscoverySettings.Load(path, warnings);

        Assert.Empty(warnings);
        Assert.False(loaded.ShowDtrEntry);
        Assert.True(loaded.DtrShowWhenEmpty);
        Assert.False(loaded.NearbyIncludeOtherJob);
        Assert.False(File.Exists(path + ".tmp"));

        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Assert.False((bool)json["showDtrEntry"]!);
        Assert.True((bool)json["dtrShowWhenEmpty"]!);
        Assert.False((bool)json["nearbyIncludeOtherJob"]!);
    }

    [Fact]
    public void Missing_file_yields_defaults_without_warnings()
    {
        var warnings = new List<string>();

        var loaded = DiscoverySettings.Load(tmp.File("nope.json"), warnings);

        Assert.True(loaded.ShowDtrEntry);
        Assert.True(loaded.NearbyIncludeOtherJob);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Partial_file_keeps_defaults_for_absent_and_unknown_properties()
    {
        var path = tmp.File("discovery.json");
        File.WriteAllText(path, """{ "nearbyIncludeOtherJob": false, "somethingNewer": 3 }""");

        var loaded = DiscoverySettings.Load(path);

        Assert.True(loaded.ShowDtrEntry);
        Assert.False(loaded.NearbyIncludeOtherJob);
    }

    [Fact]
    public void Corrupt_file_is_quarantined_and_defaults_apply()
    {
        var path = tmp.File("discovery.json");
        File.WriteAllText(path, "{{{");
        var warnings = new List<string>();

        var loaded = DiscoverySettings.Load(path, warnings);

        Assert.True(loaded.ShowDtrEntry);
        Assert.Single(warnings);
        Assert.False(File.Exists(path));
        Assert.Single(Directory.GetFiles(tmp.Path, "discovery.json.corrupt-*"));
    }
}
