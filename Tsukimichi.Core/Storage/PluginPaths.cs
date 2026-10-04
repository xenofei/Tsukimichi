using System.Globalization;

namespace Tsukimichi.Core.Storage;

/// <summary>
/// Every file and folder Tsukimichi touches, derived from the two directories Dalamud hands the plugin
/// (the writable config directory and the read-only plugin directory). See spec §6.
/// </summary>
public sealed class PluginPaths
{
    public PluginPaths(string configDir, string pluginDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configDir);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDir);
        ConfigDir = configDir;
        PluginDir = pluginDir;
    }

    /// <summary>Writable per-plugin config directory.</summary>
    public string ConfigDir { get; }

    /// <summary>Read-only directory the plugin was loaded from; shipped data lives here.</summary>
    public string PluginDir { get; }

    public string ConfigFile => Path.Combine(ConfigDir, "config.json");
    public string CharactersDir => Path.Combine(ConfigDir, "characters");
    public string UserDir => Path.Combine(ConfigDir, "user");
    public string PinsFile => Path.Combine(UserDir, "pins.json");
    public string OverridesFile => Path.Combine(UserDir, "overrides.json");

    /// <summary>Per-character settings every game client shares (1.8.0): spoiler overrides, notices, hidden, not tracked, Compare.</summary>
    public string CharacterSettingsFile => Path.Combine(UserDir, "characters.json");

    /// <summary>Default folder of Settings › Data › Export and <c>/tsuki export</c>.</summary>
    public string ExportsDir => Path.Combine(ConfigDir, "exports");

    public string UniqueRewardsFile => Path.Combine(PluginDir, "unique_quests.json");

    /// <summary>The patch each quest was added in (P8), read at catalog build.</summary>
    public string QuestPatchesFile => Path.Combine(PluginDir, QuestPatches.FileName);
    /// <summary>The "Open on…" link table (1.8.0): Lodestone hashes, wiki titles and FFXIV Collect ids.</summary>
    public string ExternalIdsFile => Path.Combine(PluginDir, Links.ExternalIds.FileName);

    public string CuratedDir => Path.Combine(PluginDir, "curated");
    public string SystemUnlocksFile => Path.Combine(CuratedDir, CuratedData.SystemUnlocksFileName);
    public string DutyUnlocksFile => Path.Combine(CuratedDir, CuratedData.DutyUnlocksFileName);
    public string FestivalsFile => Path.Combine(CuratedDir, CuratedData.FestivalsFileName);
    public string FeatureQuestsFile => Path.Combine(CuratedDir, CuratedData.FeatureQuestsFileName);

    /// <summary>The portrait pack this build offers (feature plan v7 F4): its release, size and hash.</summary>
    public string PortraitPackOfferFile => Path.Combine(PluginDir, Portraits.PortraitPackOffer.FileName);

    /// <summary>Where a downloaded portrait pack is installed (<see cref="Portraits.PortraitPackStore"/>).</summary>
    public string PortraitPackDir => Path.Combine(ConfigDir, "portraits");

    /// <summary>The snapshot file for one character, as <see cref="JsonSnapshotStore"/> names it.</summary>
    public string SnapshotFile(ulong contentId) =>
        Path.Combine(CharactersDir, contentId.ToString(CultureInfo.InvariantCulture) + ".json");
}
