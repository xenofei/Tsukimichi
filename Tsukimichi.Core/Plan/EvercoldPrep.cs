using System.Runtime.CompilerServices;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Journal;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Plan;

/// <summary>The five lines of the Before Evercold card (spec-1.20 N7, "The five lines"), in the table's order.</summary>
public enum PrepLineKind : byte
{
    /// <summary>Finish the main story: done when the data's last main scenario quest is complete.</summary>
    Story,

    /// <summary>Room in your journal: done at <see cref="EvercoldPrep.JournalFreeSlots"/> or more free slots (C9's count).</summary>
    Journal,

    /// <summary>Job and role quests: done when no job or role quest is Ready for this character.</summary>
    Jobs,

    /// <summary>Duties for the roulettes: done when every dungeon, trial and raid of the data's newest expansion is unlocked.</summary>
    Duties,

    /// <summary>Flying in the newest expansion: done when flying is unlocked in every one of its areas.</summary>
    Flying,
}

/// <summary>How a line of the card reads: open, done because the game says so, or ticked by the player ("you said so").</summary>
public enum PrepCheck : byte
{
    Open,
    Game,
    You,
}

/// <summary>
/// One flying area of the newest expansion as the card reads it: its territory, its name as the sheet spells it, the
/// quests whose aether currents it needs, and whether flying there is unlocked (null when it cannot be read: a stored
/// character, whose field currents no capture saves).
/// </summary>
public sealed record PrepZone(uint TerritoryId, string Name, IReadOnlyList<uint> QuestRowIds, bool? Flying);

/// <summary>
/// One line of the card for one character: what it asks (<paramref name="Kind"/>) and whether the game says it is done.
/// The fields a kind does not use keep their defaults.
/// </summary>
public sealed record PrepLine(PrepLineKind Kind, bool Done)
{
    /// <summary><see cref="PrepLineKind.Story"/>: the next main scenario quest (null once complete).</summary>
    public QuestRecord? Next { get; init; }

    /// <summary><see cref="PrepLineKind.Story"/>: main scenario quests left to the end of the data's story.</summary>
    public int Left { get; init; }

    /// <summary><see cref="PrepLineKind.Story"/>: the data's last main scenario quest (<see cref="EvercoldPrep.StoryEnd"/>).</summary>
    public QuestRecord? StoryEnd { get; init; }

    /// <summary>
    /// <see cref="PrepLineKind.Story"/>: the expansion the character's story is in; below <see cref="EvercoldPrep.LatestExpansion"/>
    /// the line reads "You're in Stormblood" and the card has no lines of the newest expansion.
    /// </summary>
    public byte InExpansion { get; init; }

    /// <summary><see cref="PrepLineKind.Story"/>: the character is in an earlier expansion than the data's newest.</summary>
    public bool Earlier { get; init; }

    /// <summary><see cref="PrepLineKind.Journal"/>: the journal's slots (C9's count).</summary>
    public JournalSlots Slots { get; init; }

    /// <summary><see cref="PrepLineKind.Jobs"/>: the jobs with a job quest Ready, in ladder order.</summary>
    public IReadOnlyList<uint> JobIds { get; init; } = [];

    /// <summary><see cref="PrepLineKind.Jobs"/>: the roles with a role quest Ready.</summary>
    public IReadOnlyList<JobRole> Roles { get; init; } = [];

    /// <summary><see cref="PrepLineKind.Jobs"/>: every Ready job and role quest, in ladder order ("Show them").</summary>
    public IReadOnlyList<QuestRecord> Quests { get; init; } = [];

    /// <summary><see cref="PrepLineKind.Duties"/>: the newest expansion's roulette duties not unlocked, in the Duty Finder's order.</summary>
    public IReadOnlyList<DutyRunInfo> Duties { get; init; } = [];

    /// <summary><see cref="PrepLineKind.Duties"/> and <see cref="PrepLineKind.Flying"/>: the newest expansion (an ExVersion row).</summary>
    public byte Expansion { get; init; }

    /// <summary><see cref="PrepLineKind.Flying"/>: the areas without flying yet, in the flight index's order.</summary>
    public IReadOnlyList<PrepZone> Zones { get; init; } = [];
}

/// <summary>What <see cref="EvercoldPrep.Lines"/> reads for one character (live, or a stored one's snapshot and states).</summary>
/// <param name="Duties">The duty index; null while it is read, which leaves the duties line out.</param>
/// <param name="Zones">The newest expansion's flying areas; null while the flight index is read, which leaves the flying line out.</param>
public sealed record PrepInputs(
    QuestCatalog Catalog,
    CharacterSnapshot Snapshot,
    IReadOnlyDictionary<uint, QuestEvaluation> States,
    JobLadder Ladder,
    DutyRunIndex? Duties = null,
    IReadOnlyList<PrepZone>? Zones = null);

/// <summary>
/// The Before Evercold card (feature plan v7, 1.20.0, N7; spec-1.20 "N7. Before Evercold"): what the game's quests need
/// before Patch 8.0 "Evercold", from data Tsukimichi already has, one card per character. Five lines, only those that
/// apply (<see cref="Lines"/>): the main story, room in the journal, job and role quests, the newest expansion's duties
/// for the roulettes and flying in its areas. A character still in an earlier expansion gets "You're in Stormblood…"
/// and none of the newest expansion's lines. "Newest" is read from the data (<see cref="LatestExpansion"/>), never a
/// hard-coded id.
/// <para>
/// The card retires by itself (<see cref="IsRetired"/>) once the game data holds Evercold's expansion or on its early
/// access day, which curated data ships (<see cref="Launch"/>: <c>curated/expansion_launches.json</c>, rechecked after
/// the Tokyo Fan Fest). Pure.
/// </para>
/// </summary>
public static class EvercoldPrep
{
    /// <summary>The card's id in a character's hidden cards (<see cref="CharacterSettings.CardsDismissed"/>).</summary>
    public const string CardId = "beforeEvercold";

    /// <summary>Evercold's ExVersion row (Dawntrail is 5): game data holding a quest of it is 8.0 data.</summary>
    public const byte EvercoldExpansion = 6;

    /// <summary>Decision 9: the journal line is done at this many free slots or more.</summary>
    public const int JournalFreeSlots = 10;

    /// <summary>
    /// The early access the card falls back on when curated data has no entry for Evercold: Friday 22 January 2027, an
    /// estimate (Eorzean Tavern, 2026-09-17; PCGamesN, 2026-07-25). The shipped <c>expansion_launches.json</c> wins.
    /// </summary>
    public static readonly ExpansionLaunch FallbackLaunch = new(
        EvercoldExpansion,
        "Evercold",
        new DateTime(2027, 1, 22, 0, 0, 0, DateTimeKind.Utc),
        Expected: true,
        "https://eorzeantavern.com/evercold/",
        "Fallback when the curated file has no entry.");

    private static readonly ConditionalWeakTable<QuestCatalog, StrongBox<byte>> LatestCache = [];
    private static readonly ConditionalWeakTable<QuestCatalog, StrongBox<QuestRecord?>> EndCache = [];

    /// <summary>The line's id among a character's ticks (<see cref="CharacterSettings.EvercoldTicks"/>).</summary>
    public static string TickId(PrepLineKind kind) => kind switch
    {
        PrepLineKind.Story => "story",
        PrepLineKind.Journal => "journal",
        PrepLineKind.Jobs => "jobs",
        PrepLineKind.Duties => "duties",
        _ => "flying",
    };

    /// <summary>The lines a character ticked, from their tick ids (unknown ids ignored).</summary>
    public static IReadOnlySet<PrepLineKind> Ticked(IEnumerable<string>? ids)
    {
        var ticked = new HashSet<PrepLineKind>();
        if (ids is null)
        {
            return ticked;
        }

        foreach (var id in ids)
        {
            foreach (var kind in Enum.GetValues<PrepLineKind>())
            {
                if (string.Equals(TickId(kind), id, StringComparison.Ordinal))
                {
                    ticked.Add(kind);
                }
            }
        }

        return ticked;
    }

    /// <summary>Evercold's early access: the curated entry for <see cref="EvercoldExpansion"/>, else <see cref="FallbackLaunch"/>.</summary>
    public static ExpansionLaunch Launch(CuratedData? curated) =>
        curated?.ExpansionLaunches.GetValueOrDefault(EvercoldExpansion) ?? FallbackLaunch;

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
    /// The card is over: the game data holds the launching expansion (8.0 data), or the player's own date in
    /// <paramref name="zone"/> (<see cref="TimeZoneInfo.Local"/> in the plugin) has reached the early access day the card
    /// names (<see cref="EarlyAccessDay"/>). So "early access 22 Jan" shows through the player's 21 January wherever
    /// they are, never going at 16:00 on the 21st in UTC-8.
    /// </summary>
    public static bool IsRetired(QuestCatalog catalog, ExpansionLaunch launch, DateTime nowUtc, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(zone);
        return LatestExpansion(catalog) >= launch.Expansion
            || TimeZoneInfo.ConvertTimeFromUtc(Utc(nowUtc), zone).Date >= EarlyAccessDay(launch);
    }

    /// <summary>
    /// The early access day as a calendar day (<see cref="DateTimeKind.Unspecified"/>): the day the card prints and
    /// retires on, read in the player's own zone and never converted (curated data stores it as that day's UTC midnight).
    /// </summary>
    public static DateTime EarlyAccessDay(ExpansionLaunch launch)
    {
        ArgumentNullException.ThrowIfNull(launch);
        return DateTime.SpecifyKind(launch.EarlyAccessUtc.Date, DateTimeKind.Unspecified);
    }

    /// <summary>
    /// The last main scenario quest of the game data: the last quest of the story in journal order
    /// (<see cref="MsqGraph.Story"/>) that no other main scenario quest follows. On Patch 7.x data it is the end of 7.x.
    /// Null when the catalog has no main scenario. Cached per catalog.
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
    /// The lines that apply to one character, in the table's order (<see cref="PrepLineKind"/>), each with whether the
    /// game says it is done. The story line needs a main scenario; the duties line needs the duty index and the
    /// character's duty records; the flying line needs the flight areas; the duties and flying lines need the story to
    /// have reached the newest expansion.
    /// </summary>
    public static IReadOnlyList<PrepLine> Lines(PrepInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var lines = new List<PrepLine>(5);
        var story = StoryLine(inputs);
        if (story is not null)
        {
            lines.Add(story);
        }

        lines.Add(JournalLine(inputs));
        lines.Add(JobsLine(inputs));
        if (story is { Earlier: true })
        {
            return lines;
        }

        var latest = LatestExpansion(inputs.Catalog);
        if (DutiesLine(inputs, latest) is { } duties)
        {
            lines.Add(duties);
        }

        if (FlyingLine(inputs, latest) is { } flying)
        {
            lines.Add(flying);
        }

        return lines;
    }

    /// <summary>The story line: done once the data's last main scenario quest is complete; else the quests left and the next one.</summary>
    private static PrepLine? StoryLine(PrepInputs inputs)
    {
        var end = StoryEnd(inputs.Catalog);
        if (end is null || MsqProgress.Compute(inputs.Catalog, inputs.States) is not { } position)
        {
            return null;
        }

        var done = position.IsComplete || IsCompleted(inputs.States, end.RowId);
        var latest = LatestExpansion(inputs.Catalog);
        var expansion = done || position.Next is not { } next ? latest : next.Expansion;
        return new PrepLine(PrepLineKind.Story, done)
        {
            StoryEnd = end,
            Next = done ? null : position.Next,
            Left = done ? 0 : Math.Max(0, position.Total - position.Done),
            InExpansion = expansion,
            Earlier = expansion < latest,
        };
    }

    /// <summary>Room in the journal: done at <see cref="JournalFreeSlots"/> or more free slots.</summary>
    private static PrepLine JournalLine(PrepInputs inputs)
    {
        var slots = JournalSlots.Of(inputs.Snapshot, inputs.Catalog);
        return new PrepLine(PrepLineKind.Journal, slots.Left >= JournalFreeSlots) { Slots = slots };
    }

    /// <summary>
    /// Job and role quests: the jobs, then the roles, with a quest Ready for this character (on its current job or
    /// another: <see cref="QuestState.ReadyOnOtherJob"/> counts, since a job change takes it). Done when none is.
    /// </summary>
    private static PrepLine JobsLine(PrepInputs inputs)
    {
        var jobs = new List<uint>();
        var quests = new List<QuestRecord>();
        var seen = new HashSet<uint>();
        foreach (var entry in inputs.Ladder.Jobs)
        {
            var any = false;
            foreach (var rowId in entry.QuestRowIds)
            {
                if (IsReady(inputs.States, rowId) && inputs.Catalog.GetByRowId(rowId) is { } quest)
                {
                    any = true;
                    if (seen.Add(rowId))
                    {
                        quests.Add(quest);
                    }
                }
            }

            if (any)
            {
                jobs.Add(entry.Job.RowId);
            }
        }

        var roles = new List<JobRole>();
        foreach (var role in Enum.GetValues<JobRole>())
        {
            var any = false;
            foreach (var rowId in inputs.Ladder.RoleLadder(role))
            {
                if (IsReady(inputs.States, rowId) && inputs.Catalog.GetByRowId(rowId) is { } quest)
                {
                    any = true;
                    if (seen.Add(rowId))
                    {
                        quests.Add(quest);
                    }
                }
            }

            if (any)
            {
                roles.Add(role);
            }
        }

        return new PrepLine(PrepLineKind.Jobs, jobs.Count == 0 && roles.Count == 0) { JobIds = jobs, Roles = roles, Quests = quests };
    }

    /// <summary>
    /// The newest expansion's dungeons, trials and raids that a roulette draws from (high-end ones aside), and which of
    /// them the character has not unlocked (a cleared duty is unlocked). Null without the duty index, the character's
    /// duty records or any such duty.
    /// </summary>
    private static PrepLine? DutiesLine(PrepInputs inputs, byte latest)
    {
        if (inputs.Duties is not { Count: > 0 } index || inputs.Snapshot.DutyRecords is not { } records)
        {
            return null;
        }

        var unlocked = new HashSet<uint>(records.Unlocked);
        unlocked.UnionWith(records.Cleared);
        var any = false;
        var missing = new List<DutyRunInfo>();
        foreach (var duty in RouletteDuties(index, latest))
        {
            any = true;
            if (!unlocked.Contains(duty.InstanceContentId))
            {
                missing.Add(duty);
            }
        }

        return any ? new PrepLine(PrepLineKind.Duties, missing.Count == 0) { Duties = missing, Expansion = latest } : null;
    }

    /// <summary>The duties the duties line counts: <paramref name="expansion"/>'s dungeons, trials and raids in a roulette, not high-end, Duty Finder order.</summary>
    public static IEnumerable<DutyRunInfo> RouletteDuties(DutyRunIndex index, byte expansion)
    {
        ArgumentNullException.ThrowIfNull(index);
        return index.All
            .Where(d => d.Expansion == expansion
                && d.InstanceContentId != 0
                && d.InDutyFinder
                && !d.HighEnd
                && d.Roulettes != DutyRoulettes.None
                && d.ContentTypeId is DutyRunInfo.Dungeons or DutyRunInfo.Trials or DutyRunInfo.Raids)
            .OrderBy(static d => d.ContentTypeId)
            .ThenBy(static d => d.LevelRequired)
            .ThenBy(static d => d.SortKey)
            .ThenBy(static d => d.ContentFinderConditionId);
    }

    /// <summary>
    /// Flying in the newest expansion's areas: an area is left while flying there reads false, or, where it cannot be
    /// read, while a quest whose current it needs is not complete. Null without the areas.
    /// </summary>
    private static PrepLine? FlyingLine(PrepInputs inputs, byte latest)
    {
        if (inputs.Zones is not { Count: > 0 } zones)
        {
            return null;
        }

        var left = new List<PrepZone>();
        foreach (var zone in zones)
        {
            var open = zone.Flying switch
            {
                true => false,
                false => true,
                null => zone.QuestRowIds.Any(rowId => !IsCompleted(inputs.States, rowId)),
            };
            if (open)
            {
                left.Add(zone);
            }
        }

        return new PrepLine(PrepLineKind.Flying, left.Count == 0) { Zones = left, Expansion = latest };
    }

    private static bool IsCompleted(IReadOnlyDictionary<uint, QuestEvaluation> states, uint rowId) =>
        states.TryGetValue(rowId, out var evaluation) && evaluation.State == QuestState.Completed;

    private static bool IsReady(IReadOnlyDictionary<uint, QuestEvaluation> states, uint rowId) =>
        states.TryGetValue(rowId, out var evaluation) && evaluation.State is QuestState.Ready or QuestState.ReadyOnOtherJob;

    private static DateTime Utc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Local => value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value,
    };
}

/// <summary>One open line of a built card: the line as it was when the card was built, and how it reads now.</summary>
public sealed record PrepCardLine(PrepLine Line, PrepCheck Check);

/// <summary>
/// The Before Evercold card's shape (spec-1.20 N7, "Check-offs"): the lines left open when it was built, each with its
/// check, and the done lines folded into one "Done: …" line in the table's order. Nothing moves while the player looks:
/// <see cref="Update"/> changes only the checks of the open lines (a tick reads "you said so", a line the game says is
/// done reads checked without it); only <see cref="Build"/> (a new selection, session or character) folds what the game
/// says is done. A ticked line never folds: it stays open, checked, so it can be unticked.
/// No tallies. Immutable.
/// </summary>
public sealed class PrepCard
{
    public static readonly PrepCard Empty = new([], []);

    private PrepCard(IReadOnlyList<PrepCardLine> open, IReadOnlyList<PrepLineKind> done)
    {
        Open = open;
        Done = done;
    }

    /// <summary>The lines not done by the game when the card was built (ticked ones included), in the table's order.</summary>
    public IReadOnlyList<PrepCardLine> Open { get; }

    /// <summary>The lines the game said were done when the card was built, in the table's order: the fold line.</summary>
    public IReadOnlyList<PrepLineKind> Done { get; }

    /// <summary>Nothing left when the card was built: "Ready for Evercold. Nothing left on Michiru."</summary>
    public bool AllDone => Open.Count == 0;

    /// <summary>
    /// Builds the card: a line the game says is done folds; the rest stay open, a ticked one checked "you said so". A
    /// tick never folds, so a mis-tick can always be unticked where it is (spec-1.20 N7: "Clicking again unticks it").
    /// </summary>
    public static PrepCard Build(IReadOnlyList<PrepLine> lines, IReadOnlySet<PrepLineKind> ticked)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(ticked);
        var open = new List<PrepCardLine>();
        var done = new List<PrepLineKind>();
        foreach (var line in lines.OrderBy(static l => l.Kind))
        {
            if (line.Done)
            {
                done.Add(line.Kind);
            }
            else
            {
                open.Add(new PrepCardLine(line, ticked.Contains(line.Kind) ? PrepCheck.You : PrepCheck.Open));
            }
        }

        return new PrepCard(open, done);
    }

    /// <summary>
    /// The same card with each open line's check from <paramref name="lines"/> and <paramref name="ticked"/>: the
    /// game's word first, then the player's. Lines keep their place, their words and their order; nothing folds. A
    /// line the new data no longer has keeps the game's word it had. Returns this card when no check changed.
    /// </summary>
    public PrepCard Update(IReadOnlyList<PrepLine> lines, IReadOnlySet<PrepLineKind> ticked)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(ticked);
        PrepCardLine[]? changed = null;
        for (var i = 0; i < Open.Count; i++)
        {
            var current = Open[i];
            var fresh = lines.FirstOrDefault(l => l.Kind == current.Line.Kind);
            var game = fresh is null ? current.Check == PrepCheck.Game : fresh.Done;
            var check = game ? PrepCheck.Game
                : ticked.Contains(current.Line.Kind) ? PrepCheck.You
                : PrepCheck.Open;
            if (check == current.Check)
            {
                continue;
            }

            changed ??= [.. Open];
            changed[i] = current with { Check = check };
        }

        return changed is null ? this : new PrepCard(changed, Done);
    }
}
