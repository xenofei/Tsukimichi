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

    /// <summary>Writes the settings atomically, creating the parent directory.</summary>
    public void Save(string path) => AtomicFile.Write(path, JsonSerializer.Serialize(this, StorageJson.Options));
}
