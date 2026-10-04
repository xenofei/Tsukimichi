using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Unique;

namespace Tsukimichi.Core.Characters;

/// <summary>Where a roster row's character is, which decides what this client may do with it (spec-1.21 P3).</summary>
public enum RosterPlace : byte
{
    /// <summary>Logged in on this game client: the only row this client acts for.</summary>
    Here,

    /// <summary>Logged in on another game client sharing this config folder (multibox heartbeat, D11).</summary>
    OtherClient,

    /// <summary>A stored snapshot in this config folder, logged in nowhere.</summary>
    Stored,

    /// <summary>A snapshot in another XIVLauncher roaming folder (Settings › Data › Characters), live there or not.</summary>
    OtherFolder,
}

/// <summary>The roster's columns, in display order (spec-1.21 P3).</summary>
public enum RosterColumn : byte
{
    Character,
    Job,
    Story,
    Goal,
    Ready,
    Today,
    Moonlit,
    LastSeen,
}

/// <summary>Where a character stands in the main scenario, for the roster's Story column.</summary>
/// <param name="Part">The journal part of the next story quest ("Dawntrail"), or of the last one once caught up.</param>
/// <param name="Next">The next story quest; null once caught up.</param>
/// <param name="LeftToLatest">Main scenario quests left to the latest story; 0 once caught up.</param>
/// <param name="LevelNeeded">The level the next story quest waits for; 0 when it waits for none.</param>
public sealed record RosterStory(string Part, QuestRecord? Next, int LeftToLatest, int LevelNeeded)
{
    public bool CaughtUp => Next is null;
}

/// <summary>
/// One character on the All characters roster (plan v7, 1.21.0 P3; spec-1.21 P3), as built from the live session or a
/// stored snapshot (this folder's or another launcher folder's). Null counts were not read (no snapshot, or the
/// capture predates the field), and the cell says so rather than guess.
/// </summary>
/// <param name="Live">Logged in somewhere: here, on another client, or in another folder's client (a fresh heartbeat).</param>
/// <param name="Folder">The other launcher folder the row was read from; null for this folder.</param>
/// <param name="Allowances">Allied society quest allowances left today (1.19's projection for a stored save).</param>
/// <param name="LeveAllowances">Leve allowances banked.</param>
/// <param name="MoonlitLeft">Moonlit rewards the save checked and found missing.</param>
public sealed record RosterRow(
    ulong ContentId,
    string Name,
    string WorldName,
    RosterPlace Place,
    bool Live,
    DateTime TakenUtc,
    byte Job,
    int JobLevel,
    RosterStory? Story,
    int? Ready,
    int? Allowances,
    int? LeveAllowances,
    int? MoonlitLeft)
{
    public string? Folder { get; init; }

    public bool Starred { get; init; }

    public string? Role { get; init; }

    public string? Nickname { get; init; }

    public AltGoal? Goal { get; init; }

    public AltGoalProgress? GoalProgress { get; init; }

    /// <summary>
    /// Every row but the character logged in here: this client never writes another client's character, never starts a
    /// hand-off for it (Send to Questionable, travel) and says where to act instead (spec-1.21 "Other clients are read-only").
    /// </summary>
    public bool ReadOnly => Place != RosterPlace.Here;

    /// <summary>Only the character logged in on this client can be handed to Questionable or travelled with.</summary>
    public bool CanHandOff => Place == RosterPlace.Here;
}

/// <summary>
/// The All characters roster (plan v7, 1.21.0 P3): the rows' rules, pure so they can be tested. The rows themselves
/// are built by the plugin from the live session and the stored snapshots (this folder's, and other launcher folders'
/// read-only); this class says where each one is, what it may do, how the table sorts and which columns a narrow window
/// drops first.
/// </summary>
public static class RosterBoard
{
    /// <summary>Below this window width (logical px) the Moonlit and Today columns hide, in that order, unless turned back on.</summary>
    public const float NarrowWidth = 1000f;

    /// <summary>Where a character is, from its live flags and the folder it was read from.</summary>
    public static RosterPlace PlaceOf(bool liveHere, bool liveElsewhere, bool otherFolder) =>
        liveHere ? RosterPlace.Here
        : otherFolder ? RosterPlace.OtherFolder
        : liveElsewhere ? RosterPlace.OtherClient
        : RosterPlace.Stored;

    /// <summary>The main scenario position for the Story column, from the character's states.</summary>
    /// <param name="levelNeeded">The level the next story quest waits for (<c>LevelAdvisor.MsqGate</c>); 0 for none.</param>
    public static RosterStory? StoryOf(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> states, int levelNeeded = 0)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(states);
        if (MsqLeft.For(catalog, states) is { } left)
        {
            return new RosterStory(left.Part, left.Next, left.LeftToLatest, levelNeeded);
        }

        var story = MsqGraph.For(catalog).Story;
        return story.Count == 0 ? null : new RosterStory(story[^1].Journal.GenreName, null, 0, 0);
    }

    /// <summary>The Ready quests, as Tonight counts them: every Ready quest of the catalog, removed ones never.</summary>
    public static int ReadyCount(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(states);
        var count = 0;
        foreach (var quest in catalog.All)
        {
            if (!quest.IsRemoved && states.TryGetValue(quest.RowId, out var evaluation) && evaluation.State == QuestState.Ready)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Moonlit rewards left: the stored kinds' unique rewards the save checked and found missing (each reward once).
    /// Null when the save holds no collectibles (a file from before 1.5).
    /// </summary>
    public static int? MoonlitLeft(IEnumerable<UniqueRewardEntry> entries, CollectibleLookup? lookup)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (lookup is null)
        {
            return null;
        }

        var seen = new HashSet<(RewardKind, uint)>();
        var left = 0;
        foreach (var entry in entries)
        {
            if (Collectibles.IsStored(entry.Kind) && entry.RewardId != 0 && seen.Add((entry.Kind, entry.RewardId))
                && lookup.Owns(entry.Kind, entry.RewardId) == false)
            {
                left++;
            }
        }

        return left;
    }

    /// <summary>How many rows there are, and how many are logged in on another client (the caption's "2 live in other clients").</summary>
    public static (int Total, int LiveElsewhere) Counts(IEnumerable<RosterRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var total = 0;
        var elsewhere = 0;
        foreach (var row in rows)
        {
            total++;
            elsewhere += row.Live && row.Place != RosterPlace.Here ? 1 : 0;
        }

        return (total, elsewhere);
    }

    /// <summary>
    /// Whether a column shows at <paramref name="widthLogical"/>: every column at 1,000 px and wider; below it Moonlit and
    /// Today hide (rather than every column squeezing) unless the header's menu turned them back on.
    /// </summary>
    public static bool Shows(RosterColumn column, float widthLogical, bool forcedOn = false) =>
        forcedOn || widthLogical >= NarrowWidth || column is not (RosterColumn.Moonlit or RosterColumn.Today);

    /// <summary>
    /// The rows sorted by <paramref name="column"/>. The default, Story, puts starred characters first, then the one
    /// furthest along (fewest quests to the latest story; caught up first), then by name. Every other column breaks ties
    /// by name, world and content id, so the order never depends on which save landed last.
    /// </summary>
    public static List<RosterRow> Sort(IEnumerable<RosterRow> rows, RosterColumn column = RosterColumn.Story, bool descending = false)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var list = rows.ToList();
        list.Sort((a, b) =>
        {
            if (column == RosterColumn.Story && a.Starred != b.Starred)
            {
                return a.Starred ? -1 : 1;
            }

            var by = Compare(a, b, column);
            if (by != 0)
            {
                return descending ? -by : by;
            }

            var name = StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
            if (name != 0)
            {
                return name;
            }

            var world = StringComparer.OrdinalIgnoreCase.Compare(a.WorldName, b.WorldName);
            return world != 0 ? world : a.ContentId.CompareTo(b.ContentId);
        });
        return list;
    }

    private static int Compare(RosterRow a, RosterRow b, RosterColumn column) => column switch
    {
        RosterColumn.Character => StringComparer.OrdinalIgnoreCase.Compare(a.Nickname ?? a.Name, b.Nickname ?? b.Name),
        RosterColumn.Job => b.JobLevel.CompareTo(a.JobLevel),
        RosterColumn.Story => StoryKey(a).CompareTo(StoryKey(b)),
        RosterColumn.Goal => GoalKey(a).CompareTo(GoalKey(b)),
        RosterColumn.Ready => (b.Ready ?? -1).CompareTo(a.Ready ?? -1),
        RosterColumn.Today => (b.Allowances ?? -1).CompareTo(a.Allowances ?? -1),
        RosterColumn.Moonlit => (b.MoonlitLeft ?? -1).CompareTo(a.MoonlitLeft ?? -1),
        RosterColumn.LastSeen => LastSeenKey(b).CompareTo(LastSeenKey(a)),
        _ => 0,
    };

    /// <summary>Quests to the latest story; a row without a story position last.</summary>
    private static int StoryKey(RosterRow row) => row.Story is { } story ? story.LeftToLatest : int.MaxValue;

    /// <summary>What is left of the goal; a reached goal first, no goal last.</summary>
    private static int GoalKey(RosterRow row) => row.GoalProgress is { Known: true } progress ? progress.Left : int.MaxValue;

    /// <summary>Live rows count as seen now.</summary>
    private static DateTime LastSeenKey(RosterRow row) => row.Live ? DateTime.MaxValue : row.TakenUtc;
}
