using System.Text.Json.Serialization;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Diff;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Core.Characters;

/// <summary>What an alt goal catches the character up to (plan v7, 1.21.0 N11; spec-1.21 "Set a goal").</summary>
public enum AltGoalKind : byte
{
    /// <summary>Not a goal (a hand-edited file's unknown kind reads as this and is dropped).</summary>
    None,

    /// <summary>The main scenario up to a patch's last quest (<see cref="AltGoal.Patch"/>).</summary>
    Story,

    /// <summary>The unlock quests another character has done (<see cref="AltGoal.Other"/>; the Compare diff).</summary>
    MatchCharacter,

    /// <summary>Flying in every zone of an expansion (<see cref="AltGoal.Expansion"/>).</summary>
    Flying,

    /// <summary>Every duty roulette open (the 1.19 Duties board).</summary>
    Roulettes,
}

/// <summary>
/// One character's goal as <c>user/characters.json</c> keeps it (<see cref="Storage.CharacterSettings.Goal"/>): the kind
/// and its one argument. Any game client may set a goal for any character; progress is read from that character's own
/// states. Immutable; a change replaces the whole goal.
/// </summary>
public sealed record AltGoal
{
    /// <summary>The kind, written by name; a name this build does not know (a newer build's goal) reads as <see cref="AltGoalKind.None"/> and the goal is dropped, never the whole file.</summary>
    [JsonConverter(typeof(AltGoalKindConverter))]
    public AltGoalKind Kind { get; init; }

    /// <summary>For <see cref="AltGoalKind.Story"/>: the patch, as the game writes it ("7.0", "6.0").</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Patch { get; init; }

    /// <summary>For <see cref="AltGoalKind.MatchCharacter"/>: the other character's content id.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ulong? Other { get; init; }

    /// <summary>For <see cref="AltGoalKind.Flying"/>: the expansion (ExVersion row id, 0 = A Realm Reborn).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? Expansion { get; init; }

    public static AltGoal Story(string patch) => new() { Kind = AltGoalKind.Story, Patch = patch };

    public static AltGoal Match(ulong other) => new() { Kind = AltGoalKind.MatchCharacter, Other = other };

    public static AltGoal Flying(byte expansion) => new() { Kind = AltGoalKind.Flying, Expansion = expansion };

    public static AltGoal Roulettes() => new() { Kind = AltGoalKind.Roulettes };

    /// <summary>
    /// Whether the goal can be evaluated: a known kind with its argument (a patch for the story, another character for a
    /// match, an expansion for flying). A hand-edited file's other shapes are dropped on load.
    /// </summary>
    [JsonIgnore]
    public bool IsValid => Kind switch
    {
        AltGoalKind.Story => !string.IsNullOrWhiteSpace(Patch),
        AltGoalKind.MatchCharacter => Other is > 0,
        AltGoalKind.Flying => Expansion is not null,
        AltGoalKind.Roulettes => true,
        _ => false,
    };
}

/// <summary>Reads <see cref="AltGoalKind"/> by name, an unknown name or a number as <see cref="AltGoalKind.None"/>; writes the name.</summary>
public sealed class AltGoalKindConverter : JsonConverter<AltGoalKind>
{
    public override AltGoalKind Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType != System.Text.Json.JsonTokenType.String)
        {
            reader.Skip();
            return AltGoalKind.None;
        }

        var name = reader.GetString();
        return name is { Length: > 0 } && char.IsLetter(name[0]) && Enum.TryParse<AltGoalKind>(name, ignoreCase: true, out var kind) && Enum.IsDefined(kind)
            ? kind
            : AltGoalKind.None;
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, AltGoalKind value, System.Text.Json.JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(JsonNamingPolicy(value.ToString()));
    }

    private static string JsonNamingPolicy(string name) => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(name);
}

/// <summary>A flying zone as the flying goal reads it: the zone and the quests whose aether currents it needs.</summary>
/// <param name="CurrentQuestRowIds">The quests that award the zone's quest currents (field currents are found with the compass and never counted).</param>
public sealed record AltGoalZone(uint TerritoryId, string Name, byte Expansion, IReadOnlyList<uint> CurrentQuestRowIds);

/// <summary>
/// How far a character is from its goal: what is left in the goal's own unit (quests for the story and a match, zones
/// for flying, duties for the roulettes), the quests that close it with the ones the character can do now first, and
/// how many of those can be done now. Counts say what is left, never done/total (spec-1.21 principle).
/// </summary>
/// <param name="Left">What is left, in the goal's unit; 0 once reached.</param>
/// <param name="Quests">The quests left, Ready (and in the journal) first, then the rest in catalog order.</param>
/// <param name="Doable">Of <paramref name="Quests"/>, those Ready, in the journal or Ready on another job: what "Send to Questionable" sends and "9 Ready" counts.</param>
/// <param name="Known">False when the character's data cannot answer (no duty records for the roulettes, the other character unreadable).</param>
public sealed record AltGoalProgress(AltGoalKind Kind, int Left, IReadOnlyList<QuestRecord> Quests, int Doable, bool Known = true)
{
    public static AltGoalProgress Unknown(AltGoalKind kind) => new(kind, 0, [], 0, Known: false);

    /// <summary>
    /// For the roulettes goal: the highest level a closed roulette still asks of the character's best job (0 for none).
    /// A level is not a duty, so it is never counted in <see cref="Left"/>; the goal is not reached while one is needed.
    /// </summary>
    public int LevelNeeded { get; init; }

    /// <summary>Nothing is left: "Goal reached".</summary>
    public bool Reached => Known && Left == 0 && LevelNeeded == 0;

    /// <summary>The quests left that wait on something (the story, a level): "4 wait for Kiri's story".</summary>
    public int Waiting => Quests.Count - Doable;
}

/// <summary>
/// Alt goals (plan v7, 1.21.0 N11; spec-1.21 N11): what each of the four goals leaves for a character, from that
/// character's own quest states. Pure; the caller memoizes per state map.
/// <list type="bullet">
/// <item><b>The story up to a patch:</b> the main scenario quests (<see cref="MsqGraph.Story"/>) up to the last one added
/// in that patch or before (<see cref="QuestRecord.AddedIn"/>; a quest with no known patch takes the one before it),
/// not completed, as the story's own count leaves them (<see cref="MsqCatchUp"/>): not out of the totals (locked out,
/// another Grand Company's, a spare alternative of a choice not made yet) and not a leftover of a met join.</item>
/// <item><b>Another character's unlocks:</b> the unlock quests (<see cref="FeaturePresets"/>) the other character has done
/// and this one has not, as Compare's diff finds them (<see cref="CharacterDiff"/>).</item>
/// <item><b>Flying in an expansion:</b> its zones with an aether current quest not done.</item>
/// <item><b>Every duty roulette open:</b> the duties left to unlock before each closed roulette opens
/// (<see cref="DutyBoard"/>), each duty once however many roulettes it opens, and the level a roulette still needs
/// (<see cref="AltGoalProgress.LevelNeeded"/>, never counted as a duty); a roulette the account cannot buy into, or one
/// the capture cannot judge, is not counted.</item>
/// </list>
/// </summary>
public static class AltGoals
{
    /// <summary>The story goal.</summary>
    public static AltGoalProgress Story(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> states, string patch)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(states);
        var graph = MsqGraph.For(catalog);
        var story = graph.Story;
        var joins = graph.Joins(new EvaluationSource(states));
        var end = StoryEnd(story, patch);
        var left = new List<QuestRecord>();
        for (var i = 0; i <= end; i++)
        {
            var quest = story[i];
            // As MsqCatchUp counts the story: a quest out of the totals (another Grand Company's, a spare alternative) or
            // a leftover of a join the character met is not left to do.
            if (IsDone(states, quest.RowId) || states.GetValueOrDefault(quest.RowId) is { LeavesTotals: true } || joins.IsLeftover(quest.RowId))
            {
                continue;
            }

            left.Add(quest);
        }

        return Progress(AltGoalKind.Story, left.Count, left, states);
    }

    /// <summary>The match goal: <paramref name="otherStates"/> are the other character's.</summary>
    public static AltGoalProgress Match(
        QuestCatalog catalog,
        IReadOnlyDictionary<uint, QuestEvaluation> states,
        IReadOnlyDictionary<uint, QuestEvaluation>? otherStates,
        IReadOnlySet<uint> featureQuestIds)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(featureQuestIds);
        if (otherStates is null)
        {
            return AltGoalProgress.Unknown(AltGoalKind.MatchCharacter);
        }

        var diff = CharacterDiff.Compute(catalog, states, otherStates, DiffContext.For(catalog, featureQuestIds, static _ => 0));
        var left = new List<QuestRecord>();
        foreach (var entry in diff.OnlyB)
        {
            if (featureQuestIds.Contains(entry.RowId) && catalog.GetByRowId(entry.RowId) is { } quest)
            {
                left.Add(quest);
            }
        }

        // Catalog order under the Ready-first sort, not the diff's value order.
        left.Sort(static (a, b) => a.RowId.CompareTo(b.RowId));
        return Progress(AltGoalKind.MatchCharacter, left.Count, left, states);
    }

    /// <summary>The flying goal over <paramref name="zones"/> (every flying zone; those of other expansions are skipped).</summary>
    public static AltGoalProgress Flying(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> states, IEnumerable<AltGoalZone> zones, byte expansion)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(zones);
        var zonesLeft = 0;
        var left = new List<QuestRecord>();
        var seen = new HashSet<uint>();
        foreach (var zone in zones)
        {
            if (zone.Expansion != expansion)
            {
                continue;
            }

            var open = false;
            foreach (var rowId in zone.CurrentQuestRowIds)
            {
                // A stored character's attunement cannot be read: the awarding quest's completion stands in.
                if (FlightProgress.QuestCurrentDone(null, StateOf(states, rowId)))
                {
                    continue;
                }

                open = true;
                if (seen.Add(rowId) && catalog.GetByRowId(rowId) is { } quest)
                {
                    left.Add(quest);
                }
            }

            zonesLeft += open ? 1 : 0;
        }

        return Progress(AltGoalKind.Flying, zonesLeft, left, states);
    }

    /// <summary>The roulettes goal from the character's Duties board; unknown while it holds no duty records.</summary>
    public static AltGoalProgress Roulettes(IReadOnlyDictionary<uint, QuestEvaluation> states, DutyBoardModel board)
    {
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(board);
        if (board.Roulettes.Count == 0 || board.Stale)
        {
            return AltGoalProgress.Unknown(AltGoalKind.Roulettes);
        }

        var closed = board.Roulettes.Where(static l => l.Lock is RouletteLock.NeedsDuties or RouletteLock.NeedsLevel).ToList();
        var level = closed.Where(static l => l.Lock == RouletteLock.NeedsLevel).Select(static l => (int)l.Roulette.RequiredLevel).DefaultIfEmpty(0).Max();

        // Each duty once: a roulette that needs every duty in it needs each one missing, and those count toward a
        // roulette that needs only some (a duty in two roulettes opens both); such a roulette then adds only what it
        // still lacks after them.
        var every = new HashSet<uint>();
        foreach (var line in closed.Where(static l => l.NeedsEvery && l.Left > 0))
        {
            foreach (var missing in line.Missing)
            {
                every.Add(missing.Duty.ContentFinderConditionId);
            }
        }

        var dutiesLeft = every.Count;
        foreach (var line in closed.Where(static l => !l.NeedsEvery && l.Left > 0))
        {
            var covered = line.Missing.Count(m => every.Contains(m.Duty.ContentFinderConditionId));
            dutiesLeft += Math.Max(0, line.Left - covered);
        }

        var left = new List<QuestRecord>();
        var seen = new HashSet<uint>();
        foreach (var line in closed)
        {
            foreach (var missing in line.Missing)
            {
                if (missing.UnlockQuests.Count > 0 && seen.Add(missing.UnlockQuests[0].RowId))
                {
                    left.Add(missing.UnlockQuests[0]);
                }
            }
        }

        left.Sort(static (a, b) => a.RowId.CompareTo(b.RowId));
        return Progress(AltGoalKind.Roulettes, dutiesLeft, left, states) with { LevelNeeded = level };
    }

    /// <summary>
    /// The patches the story goal can aim at, oldest first: every patch a main scenario quest was added in, each with
    /// its journal part ("7.0", "Dawntrail"). Empty when the catalog carries no patches.
    /// </summary>
    public static IReadOnlyList<(string Patch, string Part)> StoryPatches(QuestCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var list = new List<(string Patch, string Part)>();
        foreach (var quest in MsqGraph.For(catalog).Story)
        {
            if (PatchVersion.IsPatch(quest.AddedIn) && seen.Add(quest.AddedIn))
            {
                list.Add((quest.AddedIn, quest.Journal.GenreName));
            }
        }

        list.Sort(static (a, b) => PatchVersion.Compare(a.Patch, b.Patch));
        return list;
    }

    /// <summary>
    /// The index in <paramref name="story"/> of the last quest within <paramref name="patch"/>: the last one whose patch
    /// (its own, or the one before it when unknown) is that patch or older; -1 when none is.
    /// </summary>
    internal static int StoryEnd(IReadOnlyList<QuestRecord> story, string patch)
    {
        var end = -1;
        var current = string.Empty;
        for (var i = 0; i < story.Count; i++)
        {
            if (PatchVersion.IsPatch(story[i].AddedIn))
            {
                current = story[i].AddedIn;
            }

            if (current.Length > 0 && PatchVersion.Compare(current, patch) <= 0)
            {
                end = i;
            }
        }

        return end;
    }

    /// <summary>The goal's progress with its quests ordered Ready (and in the journal) first.</summary>
    private static AltGoalProgress Progress(AltGoalKind kind, int left, List<QuestRecord> quests, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        var ordered = quests
            .Select((quest, index) => (Quest: quest, Index: index, Rank: Rank(StateOf(states, quest.RowId))))
            .OrderBy(static q => q.Rank)
            .ThenBy(static q => q.Index)
            .Select(static q => q.Quest)
            .ToArray();
        var doable = ordered.Count(q => IsDoable(StateOf(states, q.RowId)));
        return new AltGoalProgress(kind, left, ordered, doable);
    }

    /// <summary>Ready, in the journal, or Ready on another job: what the character can do now.</summary>
    public static bool IsDoable(QuestState? state) => state is QuestState.Ready or QuestState.Accepted or QuestState.ReadyOnOtherJob;

    private static int Rank(QuestState? state) => state switch
    {
        QuestState.Ready => 0,
        QuestState.Accepted => 1,
        QuestState.ReadyOnOtherJob => 2,
        _ => 3,
    };

    private static QuestState? StateOf(IReadOnlyDictionary<uint, QuestEvaluation> states, uint rowId) =>
        states.TryGetValue(rowId, out var evaluation) ? evaluation.State : null;

    private static bool IsDone(IReadOnlyDictionary<uint, QuestEvaluation> states, uint rowId) =>
        StateOf(states, rowId) is QuestState.Completed or QuestState.DoneThisCycle or QuestState.Foreclosed;
}
