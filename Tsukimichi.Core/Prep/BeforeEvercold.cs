using System.Runtime.CompilerServices;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Journal;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Seasonal;

namespace Tsukimichi.Core.Prep;

/// <summary>What one line of the Before Evercold card asks of the character.</summary>
public enum PrepKind : byte
{
    /// <summary>The main scenario caught up to the last quest of today's game data (the end of Patch 7.x).</summary>
    MainScenario,

    /// <summary>The job and role quests up to the level cap, for every job at the cap.</summary>
    JobQuests,

    /// <summary>Room in the 30-quest journal (<see cref="JournalSlots"/>).</summary>
    JournalRoom,

    /// <summary>A running seasonal event whose known end falls before Evercold, with quests left.</summary>
    Event,
}

/// <summary>
/// One job (or one role's quests) at the level cap with quests left up to the cap.
/// </summary>
/// <param name="JobId">The ClassJob row id; 0 for a role's quests (<paramref name="Role"/>).</param>
/// <param name="Role">The role whose role quests these are; null for a job's own ladder.</param>
/// <param name="Left">Quests left up to the cap.</param>
/// <param name="Next">The first of them in ladder order.</param>
public sealed record PrepJob(uint JobId, JobRole? Role, int Left, QuestRecord? Next);

/// <summary>
/// One line of the Before Evercold card: what it asks (<paramref name="Kind"/>) and whether the character has done it.
/// The fields a kind does not use keep their defaults.
/// </summary>
/// <param name="Done">Checked off: the line stays, with its check, so the card never jumps as lines are done.</param>
public sealed record PrepLine(PrepKind Kind, bool Done)
{
    /// <summary>The quest a click selects: the next main scenario quest, the first job quest left, the first journal quest to hand in or drop, the event quest in the journal or to take. Null when there is none.</summary>
    public QuestRecord? Target { get; init; }

    /// <summary>Main scenario quests left, journal slots left, or event quests left (in the journal or to take).</summary>
    public int Left { get; init; }

    /// <summary><see cref="PrepKind.MainScenario"/>: the last main scenario quest of the game data (<see cref="BeforeEvercold.StoryEnd"/>).</summary>
    public QuestRecord? StoryEnd { get; init; }

    /// <summary><see cref="PrepKind.JobQuests"/>: the jobs, then the roles, with quests left up to the cap; empty once done.</summary>
    public IReadOnlyList<PrepJob> Jobs { get; init; } = [];

    /// <summary><see cref="PrepKind.JobQuests"/>: the character's level cap.</summary>
    public byte LevelCap { get; init; }

    /// <summary><see cref="PrepKind.JournalRoom"/>: the journal's slots.</summary>
    public JournalSlots Slots { get; init; }

    /// <summary><see cref="PrepKind.Event"/>: the event.</summary>
    public RunningFestival? Festival { get; init; }
}

/// <summary>What <see cref="BeforeEvercold.Lines"/> reads for one character (live, or a stored alt's snapshot and states).</summary>
/// <param name="Running">The events running on the server for this character (<see cref="SeasonalNow.Running(QuestCatalog, ServerFestivals, IReadOnlyDictionary{uint, QuestEvaluation}, IReadOnlyDictionary{ushort, Storage.FestivalInfo}, DateTime, IReadOnlyDictionary{ushort, DateTime}?)"/>).</param>
public sealed record PrepInputs(
    QuestCatalog Catalog,
    CharacterSnapshot Snapshot,
    IReadOnlyDictionary<uint, QuestEvaluation> States,
    JobLadder Ladder,
    IReadOnlyList<RunningFestival> Running);

/// <summary>
/// The Before Evercold card (feature plan v7, 1.20.0, N7): what each character should finish before Patch 8.0
/// "Evercold" lands. Every line comes from data the plugin already reads and from a cited reason
/// (<c>docs/research/plan-v7/feature-ideas.md</c>, idea 4 and its Evercold facts):
/// <list type="bullet">
/// <item>the main scenario through the last quest of today's game data (<see cref="StoryEnd"/>, read from the sheet,
/// never a hard-coded id): the Japanese 8.0 prep list (trozo.hateblo.jp, 2026-04-08) says to finish the 7.x story
/// through 7.56;</item>
/// <item>job and role quests up to the cap, for jobs at the cap: the same list's "finish class quests" (optional there),
/// with 8.0 raising the cap to 110 (Eorzean Tavern, 2026-09-17);</item>
/// <item>room in the journal (<see cref="JournalSlots"/>): the same list's "clear the quests in your journal, there's
/// no time once 8.0 starts";</item>
/// <item>running events whose known end (<see cref="SeasonalNow.ResolveEnd"/>: the Lodestone, a dated run or the
/// player's own date, never a guess) falls before Evercold, with quests left (1.19's C10 data).</item>
/// </list>
/// Nothing is listed for tomestones or daily roulettes: the sources say Adventurer Activity replaces them, but none
/// says what becomes of what a player holds, so any advice would be a guess.
/// <para>
/// The card retires by itself (<see cref="IsRetired"/>): once the game data holds Evercold's expansion, or once the
/// expected early-access date (<see cref="ExpectedUtc"/>, an estimate) has passed. The player can hide it per character.
/// Pure.
/// </para>
/// </summary>
public static class BeforeEvercold
{
    /// <summary>The card's id in a character's hidden cards (<see cref="Storage.CharacterSettings.CardsDismissed"/>).</summary>
    public const string CardId = "beforeEvercold";

    /// <summary>Evercold's ExVersion row (Dawntrail is 5): game data holding a quest of it is 8.0 data.</summary>
    public const byte EvercoldExpansion = 6;

    /// <summary>
    /// Evercold's expected early access, the one date this card keeps: Friday 22 January 2027, an estimate (Eorzean
    /// Tavern, updated 2026-09-17; PCGamesN, 2026-07-25). Recheck after the Tokyo Fan Fest (31 Oct to 1 Nov 2026) and
    /// change it here. Taken as the start of that day in UTC, so the card is gone everywhere on the day.
    /// </summary>
    public static readonly DateTime ExpectedUtc = new(2027, 1, 22, 0, 0, 0, DateTimeKind.Utc);

    private static readonly ConditionalWeakTable<QuestCatalog, StrongBox<byte>> LatestCache = [];
    private static readonly ConditionalWeakTable<QuestCatalog, StrongBox<QuestRecord?>> EndCache = [];

    /// <summary>The newest expansion any live quest of <paramref name="catalog"/> belongs to (an ExVersion row). Cached per catalog.</summary>
    public static byte LatestExpansion(QuestCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return LatestCache.GetValue(catalog, static c =>
        {
            byte latest = 0;
            foreach (var quest in c.All)
            {
                if (!quest.IsRemoved && quest.Expansion > latest)
                {
                    latest = quest.Expansion;
                }
            }

            return new StrongBox<byte>(latest);
        }).Value;
    }

    /// <summary>
    /// The card is over: the game data is Evercold's (<see cref="EvercoldExpansion"/>), or <paramref name="nowUtc"/> is
    /// on or past <see cref="ExpectedUtc"/>.
    /// </summary>
    public static bool IsRetired(QuestCatalog catalog, DateTime nowUtc) =>
        LatestExpansion(catalog) >= EvercoldExpansion || Utc(nowUtc) >= ExpectedUtc;

    /// <summary>Whether the card shows for a character: not retired, and not hidden for it.</summary>
    public static bool Shows(QuestCatalog catalog, DateTime nowUtc, bool dismissed) => !dismissed && !IsRetired(catalog, nowUtc);

    /// <summary>
    /// The last main scenario quest of the game data: the last quest of the story in journal order
    /// (<see cref="MsqGraph.Story"/>) that no other main scenario quest follows. On Patch 7.x data it is the end of 7.x
    /// (7.56's last quest). Null when the catalog has no main scenario. Cached per catalog.
    /// </summary>
    public static QuestRecord? StoryEnd(QuestCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return EndCache.GetValue(catalog, static c =>
        {
            var graph = MsqGraph.For(c);
            var story = graph.Story;
            for (var i = story.Count - 1; i >= 0; i--)
            {
                if (graph.Successors(story[i].RowId).Count == 0)
                {
                    return new StrongBox<QuestRecord?>(story[i]);
                }
            }

            return new StrongBox<QuestRecord?>(null);
        }).Value;
    }

    /// <summary>
    /// The card's lines for one character, in a fixed order: the main scenario, the job quests (only with a job at the
    /// level cap), the journal's room, then the events ending before Evercold, soonest first. A line the character has
    /// done stays, checked (<see cref="PrepLine.Done"/>).
    /// </summary>
    public static IReadOnlyList<PrepLine> Lines(PrepInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var lines = new List<PrepLine>(5);
        if (MainScenario(inputs) is { } story)
        {
            lines.Add(story);
        }

        if (JobQuests(inputs) is { } jobs)
        {
            lines.Add(jobs);
        }

        lines.Add(JournalLine(inputs));
        lines.AddRange(Events(inputs));
        return lines;
    }

    /// <summary>
    /// The main scenario through <see cref="StoryEnd"/>: done once the story is complete or its last quest is; else
    /// the quests left and the next one (the first route's inside a branch region).
    /// </summary>
    private static PrepLine? MainScenario(PrepInputs inputs)
    {
        var end = StoryEnd(inputs.Catalog);
        if (end is null || MsqProgress.Compute(inputs.Catalog, inputs.States) is not { } position)
        {
            return null;
        }

        var endDone = inputs.States.TryGetValue(end.RowId, out var evaluation) && evaluation.State == QuestState.Completed;
        var done = position.IsComplete || endDone;
        return new PrepLine(PrepKind.MainScenario, done)
        {
            StoryEnd = end,
            Target = done ? null : position.Next,
            Left = done ? 0 : Math.Max(0, position.Total - position.Done),
        };
    }

    /// <summary>
    /// The job quests up to the cap for every job at the cap (<see cref="CharacterSnapshot.LevelCap"/>), and the role
    /// quests of their roles. A class whose job is unlocked speaks through its job, and a job not yet unlocked through
    /// its class, as on the dashboard. Null without a known cap or a job at it.
    /// </summary>
    private static PrepLine? JobQuests(PrepInputs inputs)
    {
        var cap = inputs.Snapshot.LevelCap;
        if (cap == 0)
        {
            return null;
        }

        var ladder = inputs.Ladder;
        var levels = inputs.Snapshot.JobLevels;
        var atCap = false;
        var jobs = new List<PrepJob>();
        var roles = new SortedSet<JobRole>();
        foreach (var entry in ladder.Jobs)
        {
            var job = entry.Job;
            if (Level(levels, job) < cap || !SpeaksForItself(job, ladder, inputs.States))
            {
                continue;
            }

            atCap = true;
            if (ladder.RoleOf(job.RowId) is { } role)
            {
                roles.Add(role);
            }

            var (left, next) = LeftUpTo(entry.QuestRowIds, cap, inputs);
            if (left > 0)
            {
                jobs.Add(new PrepJob(job.RowId, null, left, next));
            }
        }

        if (!atCap)
        {
            return null;
        }

        foreach (var role in roles)
        {
            var (left, next) = LeftUpTo(ladder.RoleLadder(role), cap, inputs);
            if (left > 0)
            {
                jobs.Add(new PrepJob(0, role, left, next));
            }
        }

        QuestRecord? target = null;
        foreach (var job in jobs)
        {
            if (job.Next is not null)
            {
                target = job.Next;
                break;
            }
        }

        return new PrepLine(PrepKind.JobQuests, jobs.Count == 0) { Jobs = jobs, LevelCap = cap, Target = target };
    }

    /// <summary>A job's level: its own, else its class's (the game shares one level between them).</summary>
    private static short Level(IReadOnlyDictionary<byte, short> levels, LadderJob job)
    {
        if (job.RowId <= byte.MaxValue && levels.TryGetValue((byte)job.RowId, out var own))
        {
            return own;
        }

        return job.ParentRowId is > 0 and <= byte.MaxValue && levels.TryGetValue((byte)job.ParentRowId, out var parent) ? parent : (short)0;
    }

    /// <summary>
    /// Whether the job's own ladder is the one to read: a job grown from a class once unlocked; a class (or a job
    /// without one) unless one of its jobs is unlocked. Unlocked means the job's unlock quest is completed.
    /// </summary>
    private static bool SpeaksForItself(LadderJob job, JobLadder ladder, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        if (job.ParentRowId != 0 && job.ParentRowId != job.RowId)
        {
            return IsUnlocked(job, states);
        }

        foreach (var other in ladder.Jobs)
        {
            if (other.Job.RowId != job.RowId && other.Job.ParentRowId == job.RowId && IsUnlocked(other.Job, states))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsUnlocked(LadderJob job, IReadOnlyDictionary<uint, QuestEvaluation> states) =>
        job.UnlockQuestRowId == 0 || (states.TryGetValue(job.UnlockQuestRowId, out var unlock) && unlock.State == QuestState.Completed);

    /// <summary>Quests of a ladder up to <paramref name="cap"/> not completed (foreclosed and out-of-season ones aside), and the first.</summary>
    private static (int Left, QuestRecord? Next) LeftUpTo(IReadOnlyList<uint> rowIds, byte cap, PrepInputs inputs)
    {
        var left = 0;
        QuestRecord? next = null;
        foreach (var rowId in rowIds)
        {
            if (inputs.Catalog.GetByRowId(rowId) is not { } quest || quest.Level > cap)
            {
                continue;
            }

            inputs.States.TryGetValue(rowId, out var evaluation);
            if (evaluation is { LeavesTotals: true } || evaluation?.State == QuestState.Completed)
            {
                continue;
            }

            left++;
            next ??= quest;
        }

        return (left, next);
    }

    /// <summary>Room in the journal: done while more than <see cref="JournalSlots.NearLeft"/> slots are free; the first quest to hand in or safe to drop is the target.</summary>
    private static PrepLine JournalLine(PrepInputs inputs)
    {
        var slots = JournalSlots.Of(inputs.Snapshot, inputs.Catalog);
        var done = slots.Room == JournalRoom.Room;
        QuestRecord? target = null;
        if (!done)
        {
            foreach (var row in MakeRoom.Rank(inputs.Snapshot, inputs.Catalog))
            {
                if (row.Advice != RoomAdvice.Keep && row.Quest is { } quest)
                {
                    target = quest;
                    break;
                }
            }
        }

        return new PrepLine(PrepKind.JournalRoom, done) { Slots = slots, Left = slots.Left, Target = target };
    }

    /// <summary>
    /// The running events whose known end falls before <see cref="ExpectedUtc"/> and that the character takes part in:
    /// a quest in the journal or to take (left), or one done. Done once nothing is left. Soonest first.
    /// </summary>
    private static List<PrepLine> Events(PrepInputs inputs)
    {
        var lines = new List<(DateTime End, PrepLine Line)>();
        foreach (var festival in inputs.Running)
        {
            if (festival.AnnouncedEndUtc is not { } end || Utc(end) >= ExpectedUtc)
            {
                continue;
            }

            var left = 0;
            var any = false;
            QuestRecord? target = null;
            var targetInJournal = false;
            foreach (var quest in festival.Quests)
            {
                if (quest.IsSpareAlternative)
                {
                    continue;
                }

                if (quest.IsActionable)
                {
                    left++;
                    any = true;

                    // The first quest in the journal (the game takes it away when the event ends), else the first to take.
                    var inJournal = quest.State == QuestState.Accepted;
                    if (target is null || (inJournal && !targetInJournal))
                    {
                        target = quest.Quest;
                        targetInJournal = inJournal;
                    }
                }
                else if (quest.State is QuestState.Completed or QuestState.DoneThisCycle)
                {
                    any = true;
                }
            }

            if (any)
            {
                lines.Add((Utc(end), new PrepLine(PrepKind.Event, left == 0) { Festival = festival, Left = left, Target = target }));
            }
        }

        lines.Sort(static (a, b) => a.End.CompareTo(b.End));
        return lines.ConvertAll(static l => l.Line);
    }

    private static DateTime Utc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Local => value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value,
    };
}
