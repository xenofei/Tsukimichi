using System.Text.Json;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Moonfall;

/// <summary>One level's record (progress version 2).</summary>
public sealed class MoonfallLevelRecord
{
    /// <summary>Won at least once (in Adventure or Quick Play).</summary>
    public bool Cleared { get; set; }

    /// <summary>The best score, in any mode that plays the level alone (Adventure, Quick Play).</summary>
    public long Best { get; set; }

    /// <summary>The level's Ace score was beaten once (<see cref="MoonfallAces"/>); it stays aced.</summary>
    public bool Aced { get; set; }
}

/// <summary>One challenge's record (progress version 2).</summary>
public sealed class MoonfallChallengeRecord
{
    /// <summary>Completed at least once.</summary>
    public bool Done { get; set; }

    /// <summary>The best total score of a run of it (for a duel challenge, the best of the player's scores).</summary>
    public long Best { get; set; }
}

/// <summary>The duels against one opponent at one difficulty (progress version 2).</summary>
public sealed class MoonfallDuelRecord
{
    public int Wins { get; set; }

    public int Losses { get; set; }

    public int Draws { get; set; }
}

/// <summary>
/// How far the account has got in Moonfall (plan v9 G9: "progress is saved per account"), in
/// <c>&lt;config&gt;/user/moonfall.json</c> beside the other files every character shares (pins, verdicts, Nearby's
/// settings). Several game clients share the folder (docs/multibox.md), so <see cref="Record"/> takes the file's lock,
/// reads it again and merges: progress made in either client is never lost, and a save can only move forward.
/// <para>
/// <b>Version 2</b> (plan v9 G7) keeps version 1's two counts (the levels won in order in each campaign, which open
/// the next level) and adds, all optional: <see cref="Levels"/> (cleared, best score, aced, by level id),
/// <see cref="Challenges"/> (done and best, by challenge id) and <see cref="Duels"/> (wins, losses and draws, by
/// opponent and difficulty, <see cref="DuelKey"/>). A version 1 file reads as it is: its counts still say which levels
/// are won (<see cref="IsCleared"/>), so nothing is made up for the scores it never held, and the next save writes
/// version 2. The merge keeps the further of each: counts and best scores the higher, cleared, aced and done if either
/// says so, and each duel count the higher (two clients that both win a duel at once count it once: the record never
/// goes back, and may count one result short).
/// </para>
/// </summary>
public sealed class MoonfallProgress
{
    public const string FileName = "moonfall.json";

    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Base campaign levels won, in order: the next level to play is this one (0-based).</summary>
    public int BaseCleared { get; set; }

    /// <summary>Expansion levels won, in order.</summary>
    public int ExpansionCleared { get; set; }

    /// <summary>Each level played to a win or a score, by level id (version 2).</summary>
    [OmitWhenEmpty]
    public Dictionary<string, MoonfallLevelRecord> Levels { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Each challenge tried, by challenge id (version 2).</summary>
    [OmitWhenEmpty]
    public Dictionary<string, MoonfallChallengeRecord> Challenges { get; set; } = new(StringComparer.Ordinal);

    /// <summary>The duel record by <see cref="DuelKey"/> ("louisoix/adept") (version 2).</summary>
    [OmitWhenEmpty]
    public Dictionary<string, MoonfallDuelRecord> Duels { get; set; } = new(StringComparer.Ordinal);

    public int Cleared(MoonfallCampaignKind kind) => kind == MoonfallCampaignKind.Expansion ? ExpansionCleared : BaseCleared;

    /// <summary>
    /// The furthest level The Moon Road's frontier has opened (stepping over stages past the story): a high-water mark,
    /// so a reveal or the story moving on (which can pull the frontier back) never closes the level the road had come
    /// to. Optional; 0 in older files.
    /// </summary>
    public int BaseReach { get; set; }

    /// <summary>The furthest level The Far Shore's frontier has opened (<see cref="BaseReach"/>).</summary>
    public int ExpansionReach { get; set; }

    /// <summary>The campaign's high-water mark of the frontier.</summary>
    public int Reach(MoonfallCampaignKind kind) => kind == MoonfallCampaignKind.Expansion ? ExpansionReach : BaseReach;

    /// <summary>Raises the campaign's high-water mark to <paramref name="index"/> when that is further; true when it moved.</summary>
    public bool RecordReach(MoonfallCampaignKind kind, int index)
    {
        if (index <= Reach(kind))
        {
            return false;
        }

        if (kind == MoonfallCampaignKind.Expansion)
        {
            ExpansionReach = index;
        }
        else
        {
            BaseReach = index;
        }

        return true;
    }

    /// <summary>The key of the duels against <paramref name="opponent"/> at <paramref name="difficulty"/>: "louisoix/adept".</summary>
    public static string DuelKey(MoonfallCompanion opponent, MoonfallAiDifficulty difficulty) =>
        (MoonfallCompanions.TryGet(opponent, out var info) ? info.Key : "none") + "/" + difficulty.ToString().ToLowerInvariant();

    /// <summary>Whether the level has been won: its record says so, or it lies within its campaign's count (a version 1 file's only record).</summary>
    public bool IsCleared(string id)
    {
        if (Levels.TryGetValue(id, out var record) && record.Cleared)
        {
            return true;
        }

        return MoonfallStages.TryPlace(id, out var place) && place.Index < Cleared(place.Campaign);
    }

    /// <summary>The level's best score (0 when none is known).</summary>
    public long Best(string id) => Levels.TryGetValue(id, out var record) ? record.Best : 0;

    /// <summary>Whether the level's Ace score has been beaten.</summary>
    public bool IsAced(string id) => Levels.TryGetValue(id, out var record) && record.Aced;

    /// <summary>
    /// Notes a level played to its end in Adventure or Quick Play: its best score, cleared when won, aced when the score
    /// beat <paramref name="ace"/> (null for a level with no Ace score). A win in Adventure's order also moves its
    /// campaign's count on past every level won from the start (<see cref="IsCleared"/>). True when anything moved.
    /// </summary>
    public bool RecordLevel(string id, bool won, long score, long? ace = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        var record = Level(id);
        var moved = false;
        if (score > record.Best)
        {
            record.Best = score;
            moved = true;
        }

        if (won && !record.Cleared)
        {
            record.Cleared = true;
            moved = true;
        }

        if (won && ace is { } target && score >= target && !record.Aced)
        {
            record.Aced = true;
            moved = true;
        }

        return MoveCounts() | moved;
    }

    /// <summary>Notes a run of a challenge: its score, and done when it was met. True when anything moved.</summary>
    public bool RecordChallenge(string id, bool done, long score)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        if (!Challenges.TryGetValue(id, out var record))
        {
            record = new MoonfallChallengeRecord();
            Challenges[id] = record;
        }

        var moved = false;
        if (score > record.Best)
        {
            record.Best = score;
            moved = true;
        }

        if (done && !record.Done)
        {
            record.Done = true;
            moved = true;
        }

        return moved;
    }

    /// <summary>Whether the challenge has been completed.</summary>
    public bool IsChallengeDone(string id) => Challenges.TryGetValue(id, out var record) && record.Done;

    /// <summary>Notes a duel's result against <paramref name="opponent"/> at <paramref name="difficulty"/>.</summary>
    public void RecordDuel(MoonfallCompanion opponent, MoonfallAiDifficulty difficulty, MoonfallDuelOutcome outcome)
    {
        var key = DuelKey(opponent, difficulty);
        if (!Duels.TryGetValue(key, out var record))
        {
            record = new MoonfallDuelRecord();
            Duels[key] = record;
        }

        switch (outcome)
        {
            case MoonfallDuelOutcome.Won:
                record.Wins++;
                break;
            case MoonfallDuelOutcome.Lost:
                record.Losses++;
                break;
            case MoonfallDuelOutcome.Drawn:
                record.Draws++;
                break;
        }
    }

    /// <summary>The duels against <paramref name="opponent"/> at <paramref name="difficulty"/> (all zero when none).</summary>
    public MoonfallDuelRecord DuelRecord(MoonfallCompanion opponent, MoonfallAiDifficulty difficulty) =>
        Duels.TryGetValue(DuelKey(opponent, difficulty), out var record) ? record : new MoonfallDuelRecord();

    /// <summary>Where the file lives: <c>&lt;config&gt;/user/moonfall.json</c>.</summary>
    public static string PathFor(PluginPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return Path.Combine(paths.UserDir, FileName);
    }

    /// <summary>
    /// Loads the progress; a missing file is a fresh start, an unreadable one is left in place and reported in
    /// <paramref name="warnings"/>, a corrupt one is quarantined and reported. Negative counts and scores read as 0; a
    /// version 1 file reads as it is (<see cref="IsCleared"/>).
    /// </summary>
    public static MoonfallProgress Load(string path, IList<string>? warnings = null) =>
        Clean(UserFile.Load<MoonfallProgress>(path, warnings) ?? new MoonfallProgress());

    /// <summary>
    /// Saves <paramref name=known/> (this client's progress): under the file's lock it is merged with what is on disk
    /// (<see cref="Absorb"/>: each the further of the two), then written atomically as version 2. Returns the merged
    /// progress, which may be further on than <paramref name=known/> when another client has won more. A corrupt file is
    /// quarantined and reported in <paramref name=warnings/>, and <paramref name=known/> is written in its place, so a
    /// damaged file never costs the player an unlock. Throws <see cref="IOException"/> when the lock or the file cannot be
    /// had (the caller tries again later); meant for a background thread.
    /// </summary>
    public static MoonfallProgress Record(string path, MoonfallProgress known, IList<string>? warnings = null, TimeSpan? lockTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(known);
        using (SharedFile.Lock(path, lockTimeout))
        {
            var disk = Clean(UserFile.LoadForMerge<MoonfallProgress>(path, warnings, out _) ?? new MoonfallProgress());
            disk.Absorb(known);
            disk.Version = CurrentVersion;
            AtomicFile.Write(path, JsonSerializer.Serialize(disk, StorageJson.Options));
            return disk;
        }
    }

    /// <summary>A deep copy, for a save on another thread.</summary>
    public MoonfallProgress Copy()
    {
        var copy = new MoonfallProgress { BaseCleared = BaseCleared, ExpansionCleared = ExpansionCleared };
        copy.Absorb(this);
        return copy;
    }

    /// <summary>Moves everything up to <paramref name="other"/>'s where that is further (<see cref="Record"/>'s merge); true when anything moved.</summary>
    public bool Absorb(MoonfallProgress other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var moved = other.BaseCleared > BaseCleared || other.ExpansionCleared > ExpansionCleared || other.BaseReach > BaseReach || other.ExpansionReach > ExpansionReach;
        BaseCleared = Math.Max(BaseCleared, other.BaseCleared);
        ExpansionCleared = Math.Max(ExpansionCleared, other.ExpansionCleared);
        BaseReach = Math.Max(BaseReach, other.BaseReach);
        ExpansionReach = Math.Max(ExpansionReach, other.ExpansionReach);
        foreach (var (id, theirs) in other.Levels)
        {
            var mine = Level(id);
            moved |= theirs.Best > mine.Best || (theirs.Cleared && !mine.Cleared) || (theirs.Aced && !mine.Aced);
            mine.Best = Math.Max(mine.Best, theirs.Best);
            mine.Cleared |= theirs.Cleared;
            mine.Aced |= theirs.Aced;
        }

        foreach (var (id, theirs) in other.Challenges)
        {
            if (!Challenges.TryGetValue(id, out var mine))
            {
                mine = new MoonfallChallengeRecord();
                Challenges[id] = mine;
            }

            moved |= theirs.Best > mine.Best || (theirs.Done && !mine.Done);
            mine.Best = Math.Max(mine.Best, theirs.Best);
            mine.Done |= theirs.Done;
        }

        foreach (var (key, theirs) in other.Duels)
        {
            if (!Duels.TryGetValue(key, out var mine))
            {
                mine = new MoonfallDuelRecord();
                Duels[key] = mine;
            }

            moved |= theirs.Wins > mine.Wins || theirs.Losses > mine.Losses || theirs.Draws > mine.Draws;
            mine.Wins = Math.Max(mine.Wins, theirs.Wins);
            mine.Losses = Math.Max(mine.Losses, theirs.Losses);
            mine.Draws = Math.Max(mine.Draws, theirs.Draws);
        }

        return MoveCounts() | moved;
    }

    private MoonfallLevelRecord Level(string id)
    {
        if (!Levels.TryGetValue(id, out var record))
        {
            record = new MoonfallLevelRecord();
            Levels[id] = record;
        }

        return record;
    }

    /// <summary>Each campaign's count moves on past every level won from its start (the records of version 2); true when one moved.</summary>
    private bool MoveCounts()
    {
        var moved = false;
        foreach (var campaign in (ReadOnlySpan<MoonfallCampaignKind>)[MoonfallCampaignKind.Base, MoonfallCampaignKind.Expansion])
        {
            var count = Cleared(campaign);
            var total = MoonfallStages.LevelCount(campaign);
            while (count < total && Levels.TryGetValue(MoonfallStages.LevelId(campaign, count), out var record) && record.Cleared)
            {
                count++;
            }

            if (count > Cleared(campaign))
            {
                moved = true;
                if (campaign == MoonfallCampaignKind.Expansion)
                {
                    ExpansionCleared = count;
                }
                else
                {
                    BaseCleared = count;
                }
            }
        }

        return moved;
    }

    private static MoonfallProgress Clean(MoonfallProgress progress)
    {
        progress.BaseCleared = Math.Max(0, progress.BaseCleared);
        progress.ExpansionCleared = Math.Max(0, progress.ExpansionCleared);
        progress.BaseReach = Math.Clamp(progress.BaseReach, 0, MoonfallStages.BaseLevels);
        progress.ExpansionReach = Math.Clamp(progress.ExpansionReach, 0, MoonfallStages.ExpansionLevels);

        // A hand-edited or damaged entry (null, a negative score or count, an empty key) reads as nothing.
        progress.Levels = Rebuild(progress.Levels, static r => r is null ? null : r.Best >= 0 ? r : new MoonfallLevelRecord { Cleared = r.Cleared, Aced = r.Aced });
        progress.Challenges = Rebuild(progress.Challenges, static r => r is null ? null : r.Best >= 0 ? r : new MoonfallChallengeRecord { Done = r.Done });
        progress.Duels = Rebuild(progress.Duels, static r => r is null ? null : new MoonfallDuelRecord { Wins = Math.Max(0, r.Wins), Losses = Math.Max(0, r.Losses), Draws = Math.Max(0, r.Draws) });
        return progress;
    }

    private static Dictionary<string, T> Rebuild<T>(Dictionary<string, T>? source, Func<T, T?> clean)
        where T : class
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        if (source is null)
        {
            return result;
        }

        foreach (var (key, value) in source)
        {
            if (!string.IsNullOrWhiteSpace(key) && clean(value) is { } kept)
            {
                result[key] = kept;
            }
        }

        return result;
    }
}
