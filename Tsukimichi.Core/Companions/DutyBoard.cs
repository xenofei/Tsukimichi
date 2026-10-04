using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Companions;

/// <summary>Why a Duty Roulette is closed to a character, or that it is open.</summary>
public enum RouletteLock : byte
{
    /// <summary>The roulette is open: its duties and level are met.</summary>
    Open,

    /// <summary>The account does not own the expansion the roulette needs.</summary>
    NeedsExpansion,

    /// <summary>No job has the roulette's level yet.</summary>
    NeedsLevel,

    /// <summary>Duties in the roulette are still locked (<see cref="RouletteLine.Missing"/>).</summary>
    NeedsDuties,

    /// <summary>
    /// Not known: the stored capture read another duty list (<see cref="DutyBoardModel.Stale"/>) and its records do not
    /// prove the roulette open. A login on the character reads it again.
    /// </summary>
    Unknown,
}

/// <summary>A duty the board names, with the quests that unlock it (those the character can still do first).</summary>
public sealed record BoardDuty(DutyRunInfo Duty, IReadOnlyList<QuestRecord> UnlockQuests);

/// <summary>One roulette on the board.</summary>
/// <param name="Roulette">The roulette.</param>
/// <param name="Lock">Open, or the first thing that keeps it closed.</param>
/// <param name="Unlocked">How many of its duties the character has unlocked.</param>
/// <param name="Needed">How many unlocked duties open it (every one, or <see cref="DutyBoard.MinimumUnlocked"/>).</param>
/// <param name="Missing">
/// Every duty in the roulette the character has not unlocked, lowest level first, whatever the lock: a closed
/// roulette's are what opens it, an open one's what is left in it ("open · 1 raid not unlocked"). Empty when the capture
/// read another duty list (<see cref="DutyBoardModel.Stale"/>): a duty it has no record of may simply not have been read.
/// </param>
public sealed record RouletteLine(RouletteInfo Roulette, RouletteLock Lock, int Unlocked, int Needed, IReadOnlyList<BoardDuty> Missing)
{
    /// <summary>Duties still to unlock before the roulette opens; 0 once enough are.</summary>
    public int Left => Math.Max(0, Needed - Unlocked);

    /// <summary>Whether every duty in the roulette must be unlocked (the reason then reads "every duty in it").</summary>
    public bool NeedsEvery { get; init; }
}

/// <summary>The duties unlocked and never cleared, of one Duty Finder category.</summary>
/// <param name="ContentType">The category: <see cref="DutyRunInfo.Dungeons"/>, <see cref="DutyRunInfo.Guildhests"/>, <see cref="DutyRunInfo.Trials"/> or <see cref="DutyRunInfo.Raids"/> (Ultimate and Chaotic raids join the raids).</param>
/// <param name="Duties">Lowest level first, then the Duty Finder's order.</param>
public sealed record NeverClearedGroup(uint ContentType, IReadOnlyList<DutyRunInfo> Duties);

/// <summary>The Duties board for one character (feature plan v7 N4).</summary>
/// <param name="Roulettes">Every roulette the board judges, in the Duty Finder's order.</param>
/// <param name="NeverCleared">Unlocked but never cleared, per category; categories with none are left out.</param>
public sealed record DutyBoardModel(IReadOnlyList<RouletteLine> Roulettes, IReadOnlyList<NeverClearedGroup> NeverCleared)
{
    public static readonly DutyBoardModel Empty = new([], []);

    /// <summary>
    /// The capture's duty list (<see cref="DutyRecordCapture.Watch"/>) is not the one this index watches: a stored
    /// character read before the list changed. Its records still prove what is unlocked and cleared, but a duty they
    /// do not name is unknown, not locked.
    /// </summary>
    public bool Stale { get; init; }

    /// <summary>The roulettes known to be closed (a roulette the board cannot read is not counted).</summary>
    public IEnumerable<RouletteLine> Locked => Roulettes.Where(static r => r.Lock is not (RouletteLock.Open or RouletteLock.Unknown));

    /// <summary>The roulettes with something left: closed, or open with duties in them not unlocked.</summary>
    public IEnumerable<RouletteLine> WithSomethingLeft => Roulettes.Where(static r => r.Lock != RouletteLock.Open || r.Missing.Count > 0);
}

/// <summary>
/// The Duties board (feature plan v7 N4): why each Duty Roulette is closed, and the duties unlocked but never
/// cleared, which mentors and completionists track by hand. Built from the duty index (the sheets' roulettes and the
/// roulette columns of every Duty Finder entry) and the character's own duty records
/// (<see cref="CharacterSnapshot.DutyRecords"/>), so a stored character keeps its board. Pure.
/// <para>
/// <b>When a roulette opens.</b> The sheet's open rule says which roulettes ask for every duty in them
/// (<see cref="RouletteInfo.RequiresEveryDuty"/>: Expert and Level Cap, the source of the "why is my Level Cap
/// roulette still locked" question). The others open once a few of their duties are unlocked; how many is not in the
/// sheets, so <see cref="MinimumUnlocked"/> carries the Console Games Wiki's numbers
/// (https://ffxiv.consolegameswiki.com/wiki/Duty_Roulette): two for Leveling, High-level Dungeons, Guildhests and
/// Trials, all three for Main Scenario, one for the two raid roulettes. The Mentor roulette is left out: mentor status,
/// not a duty, opens it. A roulette's level is the highest of the jobs that enter duties (<see cref="DutyJobs"/>: a
/// crafter at 100 opens no Level Cap roulette).
/// </para>
/// </summary>
public static class DutyBoard
{
    /// <summary>The roulette whose opening is a matter of mentor status, not duties.</summary>
    public const DutyRoulettes MentorRoulette = DutyRoulettes.Mentor;

    /// <summary>The Duty Finder categories the never-cleared list covers, in the order it lists them.</summary>
    public static readonly IReadOnlyList<uint> NeverClearedTypes = [DutyRunInfo.Dungeons, DutyRunInfo.Trials, DutyRunInfo.Raids, DutyRunInfo.Guildhests];

    /// <summary>
    /// How many unlocked duties open a roulette whose open rule does not ask for all of them; every duty for Main
    /// Scenario (its three), and for a roulette the wiki does not cover, one.
    /// </summary>
    public static int MinimumUnlocked(RouletteInfo roulette, int duties)
    {
        ArgumentNullException.ThrowIfNull(roulette);
        if (roulette.RequiresEveryDuty || roulette.Flag == DutyRoulettes.MainScenario)
        {
            return duties;
        }

        var minimum = roulette.Flag switch
        {
            DutyRoulettes.Leveling or DutyRoulettes.HighLevel or DutyRoulettes.Guildhests or DutyRoulettes.Trials => 2,
            _ => 1,
        };
        return Math.Min(minimum, duties);
    }

    /// <summary>
    /// The duties whose records the board reads (InstanceContent ids, ascending): every duty in a roulette, and every
    /// dungeon, guildhest, trial and raid (Ultimate and Chaotic included) the never-cleared list may name.
    /// </summary>
    public static uint[] Watched(DutyRunIndex index)
    {
        ArgumentNullException.ThrowIfNull(index);
        var ids = new SortedSet<uint>();
        foreach (var duty in index.All)
        {
            if (duty.InstanceContentId != 0 && (duty.Roulettes != DutyRoulettes.None || CategoryOf(duty) != 0))
            {
                ids.Add(duty.InstanceContentId);
            }
        }

        return [.. ids];
    }

    /// <summary>The never-cleared category a duty files under; 0 for one the list leaves out.</summary>
    public static uint CategoryOf(DutyRunInfo duty)
    {
        ArgumentNullException.ThrowIfNull(duty);
        return duty.ContentTypeId switch
        {
            DutyRunInfo.Dungeons or DutyRunInfo.Trials or DutyRunInfo.Guildhests => duty.ContentTypeId,
            DutyRunInfo.Raids or DutyRunInfo.UltimateRaids or DutyRunInfo.ChaoticAllianceRaid => DutyRunInfo.Raids,
            _ => 0,
        };
    }

    /// <summary>
    /// What the Duty Finder hint says for the roulette selected there (feature plan v7 N4: "the Duty Finder hint also
    /// answers when a roulette row is selected"): the board's line for the <c>ContentRoulette</c> row
    /// <paramref name="rouletteId"/> when it has something left (closed, or open with duties in it not unlocked), as the
    /// Duties card shows it; null for an open roulette with nothing left, one the board leaves out (Mentor) or an
    /// unknown id.
    /// </summary>
    public static RouletteLine? HintFor(DutyBoardModel board, uint rouletteId)
    {
        ArgumentNullException.ThrowIfNull(board);
        if (rouletteId == 0)
        {
            return null;
        }

        foreach (var line in board.Roulettes)
        {
            if (line.Roulette.Id == rouletteId)
            {
                return line.Lock != RouletteLock.Open || line.Missing.Count > 0 ? line : null;
            }
        }

        return null;
    }

    /// <summary>
    /// The board for <paramref name="snapshot"/>; <see cref="DutyBoardModel.Empty"/> while the capture holds no duty
    /// records or the index no roulettes.
    /// </summary>
    /// <param name="unlockQuests">The quests that unlock a duty (by ContentFinderCondition id), those the character can still do first; null names none.</param>
    /// <param name="queues">Which jobs enter duties, for the roulette's level (<see cref="DutyJobs.From"/>); null reads <see cref="DutyJobs.ByRowId"/>.</param>
    public static DutyBoardModel Build(DutyRunIndex index, CharacterSnapshot? snapshot, Func<uint, IReadOnlyList<QuestRecord>>? unlockQuests = null, Func<byte, bool>? queues = null)
    {
        ArgumentNullException.ThrowIfNull(index);
        if (snapshot?.DutyRecords is not { } records || index.Count == 0)
        {
            return DutyBoardModel.Empty;
        }

        var unlocked = new HashSet<uint>(records.Unlocked);
        var cleared = new HashSet<uint>(records.Cleared);
        var stale = records.Watch != GateItemCapture.Fingerprint(Watched(index));

        // A cleared duty is unlocked, whatever the unlock flag said at capture.
        unlocked.UnionWith(cleared);
        var level = CombatLevel(snapshot, queues);

        var lines = new List<RouletteLine>(index.Roulettes.Count);
        foreach (var roulette in index.Roulettes)
        {
            if (roulette.Flag == MentorRoulette || roulette.Flag == DutyRoulettes.None)
            {
                continue;
            }

            var duties = index.All
                .Where(d => (d.Roulettes & roulette.Flag) != 0 && d.InstanceContentId != 0)
                .OrderBy(static d => d.LevelRequired)
                .ThenBy(static d => d.SortKey)
                .ThenBy(static d => d.ContentFinderConditionId)
                .ToArray();
            if (duties.Length == 0)
            {
                continue;
            }

            var have = duties.Count(d => unlocked.Contains(d.InstanceContentId));
            var needed = MinimumUnlocked(roulette, duties.Length);
            var state = snapshot.MaxExpansion != 0 && roulette.RequiredExpansion > snapshot.MaxExpansion ? RouletteLock.NeedsExpansion
                : level < roulette.RequiredLevel ? RouletteLock.NeedsLevel
                : have >= needed ? RouletteLock.Open
                : stale ? RouletteLock.Unknown
                : RouletteLock.NeedsDuties;
            var missing = new List<BoardDuty>();
            foreach (var duty in duties)
            {
                // A capture of another list may not have read this duty: only a fresh one says it is not unlocked.
                if (!stale && !unlocked.Contains(duty.InstanceContentId))
                {
                    missing.Add(new BoardDuty(duty, unlockQuests?.Invoke(duty.ContentFinderConditionId) ?? []));
                }
            }

            lines.Add(new RouletteLine(roulette, state, have, needed, missing) { NeedsEvery = needed == duties.Length });
        }

        var groups = new List<NeverClearedGroup>(NeverClearedTypes.Count);
        foreach (var type in NeverClearedTypes)
        {
            var never = index.All
                .Where(d => d.InstanceContentId != 0 && CategoryOf(d) == type && unlocked.Contains(d.InstanceContentId) && !cleared.Contains(d.InstanceContentId))
                .OrderBy(static d => d.LevelRequired)
                .ThenBy(static d => d.SortKey)
                .ThenBy(static d => d.ContentFinderConditionId)
                .ToArray();
            if (never.Length > 0)
            {
                groups.Add(new NeverClearedGroup(type, never));
            }
        }

        return new DutyBoardModel(lines, groups) { Stale = stale };
    }

    /// <summary>
    /// The highest level of the character's jobs that enter duties (<paramref name="queues"/>, <see cref="DutyJobs.ByRowId"/>
    /// when null), with the job; (0, 0) when none is levelled. A roulette's level and its "best is BLM 98" read it.
    /// </summary>
    public static (byte Job, int Level) CombatJob(CharacterSnapshot snapshot, Func<byte, bool>? queues = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        queues ??= DutyJobs.ByRowId;
        byte best = 0;
        var level = 0;
        foreach (var (job, jobLevel) in snapshot.JobLevels)
        {
            if (queues(job) && (jobLevel > level || (jobLevel == level && level > 0 && job < best)))
            {
                best = job;
                level = jobLevel;
            }
        }

        return (best, level);
    }

    private static int CombatLevel(CharacterSnapshot snapshot, Func<byte, bool>? queues) => CombatJob(snapshot, queues).Level;
}
