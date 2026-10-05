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

    /// <summary>The most bests kept (a level's id each); a file with more keeps the first this many.</summary>
    public const int MaxBests = 512;

    /// <summary>
    /// Each level's best winning total, by the level's id (the tally's NEW BEST). Saved with the counts and merged the
    /// same way: the higher of the two always wins, so a best set in either client is never lost.
    /// </summary>
    public Dictionary<string, long> Best { get; set; } = new(StringComparer.Ordinal);

    /// <summary>The best total on <paramref name="levelId"/>, or 0 before its first win.</summary>
    public long BestFor(string levelId) => levelId is not null && Best.TryGetValue(levelId, out var best) ? best : 0;

    /// <summary>Records a win's total; true when it beats the level's best (a first win sets one but is not a new best).</summary>
    public bool RecordBest(string levelId, long total)
    {
        ArgumentException.ThrowIfNullOrEmpty(levelId);
        var had = Best.TryGetValue(levelId, out var best);
        if (total <= best)
        {
            return false;
        }

        if (had || Best.Count < MaxBests)
        {
            Best[levelId] = total;
        }

        return had;
    }

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
    /// Saves <paramref name=known/> (this client's progress): under the file's lock it is merged with what is on disk,
    /// each count the higher of the two, then written atomically. Returns the merged progress, which may be further on
    /// than <paramref name=known/> when another client has won more. A corrupt file is quarantined and reported in
    /// <paramref name=warnings/>, and <paramref name=known/> is written in its place, so a damaged file never costs the
    /// player an unlock. Throws <see cref="IOException"/> when the lock or the file cannot be had (the caller tries again
    /// later); meant for a background thread.
    /// </summary>
    public static MoonfallProgress Record(string path, MoonfallProgress known, IList<string>? warnings = null, TimeSpan? lockTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(known);
        using (SharedFile.Lock(path, lockTimeout))
        {
            var disk = Clean(UserFile.LoadForMerge<MoonfallProgress>(path, warnings, out _) ?? new MoonfallProgress());
            disk.BaseCleared = Math.Max(disk.BaseCleared, known.BaseCleared);
            disk.ExpansionCleared = Math.Max(disk.ExpansionCleared, known.ExpansionCleared);
            disk.MergeBests(known);
            disk.Version = CurrentVersion;
            AtomicFile.Write(path, JsonSerializer.Serialize(disk, StorageJson.Options));
            return disk;
        }
    }

    /// <summary>A copy of the counts, for a save on another thread.</summary>
    public MoonfallProgress Copy() => new() { BaseCleared = BaseCleared, ExpansionCleared = ExpansionCleared, Best = new Dictionary<string, long>(Best, StringComparer.Ordinal) };

    /// <summary>Moves each count up to <paramref name="other"/>'s where that is further; true when anything moved.</summary>
    public bool Absorb(MoonfallProgress other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var moved = other.BaseCleared > BaseCleared || other.ExpansionCleared > ExpansionCleared;
        BaseCleared = Math.Max(BaseCleared, other.BaseCleared);
        ExpansionCleared = Math.Max(ExpansionCleared, other.ExpansionCleared);
        moved |= MergeBests(other);
        return moved;
    }

    /// <summary>Takes each of <paramref name="other"/>'s bests that is higher than this one's; true when any moved.</summary>
    private bool MergeBests(MoonfallProgress other)
    {
        var moved = false;
        foreach (var (id, total) in other.Best)
        {
            if (total > BestFor(id) && (Best.ContainsKey(id) || Best.Count < MaxBests))
            {
                Best[id] = total;
                moved = true;
            }
        }

        return moved;
    }

    private static MoonfallProgress Clean(MoonfallProgress progress)
    {
        progress.BaseCleared = Math.Max(0, progress.BaseCleared);
        progress.ExpansionCleared = Math.Max(0, progress.ExpansionCleared);
        // A damaged or hand-edited file's bests: only level ids with a positive total, and no more than the cap.
        var best = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var (id, total) in progress.Best ?? [])
        {
            if (best.Count < MaxBests && total > 0 && MoonfallLevelLoader.IsSceneName(id))
            {
                best[id] = total;
            }
        }

        progress.Best = best;
        return progress;
    }
}
