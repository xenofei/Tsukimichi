using System.Text.Json;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Discovery;

/// <summary>
/// Settings for the Nearby quests window and the server info bar entry, persisted as <c>user/discovery.json</c>.
/// Kept apart from the main configuration so the discovery surfaces could ship without touching it; the
/// values could move into <c>Configuration</c> later, in which case <see cref="Load"/> becomes the
/// migration path. Missing or corrupt files yield defaults; a corrupt file is quarantined like the other user files.
/// </summary>
public sealed class DiscoverySettings
{
    public const string FileName = "discovery.json";

    /// <summary>Show the "☾ N" entry in the server info bar at all.</summary>
    public bool ShowDtrEntry { get; set; } = true;

    /// <summary>Keep the entry visible when nothing can be started in the current zone; off hides it at zero.</summary>
    public bool DtrShowWhenEmpty { get; set; }

    /// <summary>List quests that are Ready on another job next to the ones ready on the current job.</summary>
    public bool NearbyIncludeOtherJob { get; set; } = true;

    /// <summary>Nearby shows Everywhere (the zones board, 1.21.0 P7) rather than Here.</summary>
    public bool NearbyEverywhere { get; set; }

    /// <summary>Nearby's kind chips that are on (<see cref="Query.ZoneKinds"/>); every kind by default.</summary>
    public Query.ZoneKinds NearbyKinds { get; set; } = Query.ZoneKinds.All;

    /// <summary>Nearby's sort (<see cref="Query.ZoneSort"/>).</summary>
    public Query.ZoneSort NearbySort { get; set; } = Query.ZoneSort.LevelFit;

    /// <summary>The settings schema of the 1.22 server info bar entry (<see cref="ServerInfoBar.Migrate"/>); 0 before 1.22.</summary>
    public const int CurrentServerInfoBarSchema = 1;

    /// <summary>Which server info bar schema this file was migrated to; 0 for a file written before 1.22.</summary>
    public int ServerInfoBarSchema { get; set; }

    /// <summary>
    /// The player set the entry's switch (1.22, spec-1.22 M1): <see cref="ShowDtrEntry"/> is then theirs. Until then the
    /// entry follows <see cref="ServerInfoBar.Shown"/>'s default (on while Umbra is installed without its add-on).
    /// </summary>
    public bool DtrEntryChosen { get; set; }

    /// <summary>What the entry counts (1.22): Ready quests by default; Quests in this zone for players who had 1.x's entry on.</summary>
    public DtrCounts DtrCounts { get; set; } = DtrCounts.Ready;

    /// <summary>Where the file lives: <c>&lt;config&gt;/user/discovery.json</c>.</summary>
    public static string PathFor(PluginPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return Path.Combine(paths.UserDir, FileName);
    }

    /// <summary>
    /// Loads the settings; a missing file yields defaults silently, an unreadable one is left in place and reported in
    /// <paramref name="warnings"/>, a corrupt one is quarantined and reported. Unknown properties are ignored.
    /// </summary>
    public static DiscoverySettings Load(string path, IList<string>? warnings = null) =>
        UserFile.Load<DiscoverySettings>(path, warnings) ?? new DiscoverySettings();

    /// <summary>Writes the settings atomically, creating the parent directory; <paramref name="attempts"/> renames at most (<see cref="AtomicFile.QuickAttempts"/> on the framework thread).</summary>
    public void Save(string path, int attempts = AtomicFile.DefaultAttempts) => AtomicFile.Write(path, JsonSerializer.Serialize(this, StorageJson.Options), attempts);
}
