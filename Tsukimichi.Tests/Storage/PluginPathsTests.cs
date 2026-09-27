using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Storage;

public sealed class PluginPathsTests
{
    [Fact]
    public void Yields_every_path_from_the_storage_layout()
    {
        var config = Path.Combine("C:", "cfg");
        var plugin = Path.Combine("C:", "plug");

        var paths = new PluginPaths(config, plugin);

        Assert.Equal(config, paths.ConfigDir);
        Assert.Equal(plugin, paths.PluginDir);
        Assert.Equal(Path.Combine(config, "config.json"), paths.ConfigFile);
        Assert.Equal(Path.Combine(config, "characters"), paths.CharactersDir);
        Assert.Equal(Path.Combine(config, "user", "pins.json"), paths.PinsFile);
        Assert.Equal(Path.Combine(config, "user", "overrides.json"), paths.OverridesFile);
        Assert.Equal(Path.Combine(plugin, "unique_quests.json"), paths.UniqueRewardsFile);
        Assert.Equal(Path.Combine(plugin, "curated"), paths.CuratedDir);
        Assert.Equal(Path.Combine(plugin, "curated", "system_unlocks.json"), paths.SystemUnlocksFile);
        Assert.Equal(Path.Combine(plugin, "curated", "duty_unlocks.json"), paths.DutyUnlocksFile);
        Assert.Equal(Path.Combine(plugin, "curated", "festivals.json"), paths.FestivalsFile);
        Assert.Equal(Path.Combine(plugin, "curated", "feature_quests.json"), paths.FeatureQuestsFile);
    }

    [Fact]
    public void Snapshot_path_is_characters_contentid_json()
    {
        var paths = new PluginPaths("cfg", "plug");

        Assert.Equal(Path.Combine("cfg", "characters", "18014398509481985.json"), paths.SnapshotFile(18014398509481985UL));
    }

    [Fact]
    public void Rejects_empty_directories()
    {
        Assert.Throws<ArgumentException>(() => new PluginPaths("", "plug"));
        Assert.Throws<ArgumentException>(() => new PluginPaths("cfg", " "));
    }
}
