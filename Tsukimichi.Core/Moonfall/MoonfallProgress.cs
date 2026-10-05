using System.Text.Json;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Moonfall;

/// <summary>
/// How far the account has got in Moonfall (plan v9 G9: "progress is saved per account"), in
/// <c>&lt;config&gt;/user/moonfall.json</c> beside the other files every character shares (pins, verdicts, Nearby's
/// settings): the number of levels won, in order, in each campaign. Several game clients share the folder
/// (docs/multibox.md), so <see cref="Record"/> takes the file's lock, reads it again and keeps the further of the two
/// counts: progress made in either client is never lost, and a save can only move forward.
/// </summary>
public sealed class MoonfallProgress
{
    public const string FileName = "moonfall.json";

    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Base campaign levels won, in order: the next level to play is this one (0-based).</summary>
    public int BaseCleared { get; set; }

    /// <summary>Expansion levels won, in order.</summary>
    public int ExpansionCleared { get; set; }

    public int Cleared(MoonfallCampaignKind kind) => kind == MoonfallCampaignKind.Expansion ? ExpansionCleared : BaseCleared;

    /// <summary>Where the file lives: <c>&lt;config&gt;/user/moonfall.json</c>.</summary>
    public static string PathFor(PluginPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return Path.Combine(paths.UserDir, FileName);
    }

    /// <summary>
    /// Loads the progress; a missing file is a fresh start, an unreadable one is left in place and reported in
    /// <paramref name="warnings"/>, a corrupt one is quarantined and reported. Negative counts read as 0.
    /// </summary>
    public static MoonfallProgress Load(string path, IList<string>? warnings = null) =>
        Clean(UserFile.Load<MoonfallProgress>(path, warnings) ?? new MoonfallProgress());

    /// <summary>
    /// Records that <paramref name="cleared"/> levels of <paramref name="kind"/> are won: under the file's lock, merged
    /// with what is on disk (the higher count wins), then written atomically. Returns the merged progress. Throws
    /// <see cref="IOException"/> when the lock or the file cannot be had; meant for a background thread.
    /// </summary>
    public static MoonfallProgress Record(string path, MoonfallCampaignKind kind, int cleared, TimeSpan? lockTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using (SharedFile.Lock(path, lockTimeout))
        {
            var disk = Clean(UserFile.LoadForMerge<MoonfallProgress>(path, null, out _) ?? new MoonfallProgress());
            if (kind == MoonfallCampaignKind.Expansion)
            {
                disk.ExpansionCleared = Math.Max(disk.ExpansionCleared, cleared);
            }
            else
            {
                disk.BaseCleared = Math.Max(disk.BaseCleared, cleared);
            }

            disk.Version = CurrentVersion;
            AtomicFile.Write(path, JsonSerializer.Serialize(disk, StorageJson.Options));
            return disk;
        }
    }

    private static MoonfallProgress Clean(MoonfallProgress progress)
    {
        progress.BaseCleared = Math.Max(0, progress.BaseCleared);
        progress.ExpansionCleared = Math.Max(0, progress.ExpansionCleared);
        return progress;
    }
}
