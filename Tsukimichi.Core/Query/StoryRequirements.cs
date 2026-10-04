using System.Runtime.CompilerServices;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Query;

/// <summary>Where a <see cref="StoryRequirement"/> comes from.</summary>
public enum StoryRequirementSource : byte
{
    /// <summary>The main scenario quest's previous quests (the sheet's, with the curated extra prerequisites) name a side quest.</summary>
    Prerequisite,

    /// <summary>The main scenario quest needs a duty cleared that only a side quest unlocks.</summary>
    Duty,

    /// <summary><c>curated/story_required.json</c> names it: the game asks for it in a way the sheets do not record.</summary>
    Curated,
}

/// <summary>
/// Side quests one main scenario quest needs (feature plan v7 N3): one or more options, each a line of side quests in
/// play order (the named quest and every side quest before it). <see cref="JoinKind.All"/> needs every option's
/// quests; <see cref="JoinKind.Any"/> one option, and the one closest to done counts.
/// </summary>
/// <param name="StoryQuest">The main scenario quest that needs them.</param>
/// <param name="Source">Where the requirement comes from.</param>
/// <param name="Join">Every option, or one.</param>
/// <param name="Options">The lines of side quests (row ids); never empty, no option empty.</param>
public sealed record StoryRequirement(QuestRecord StoryQuest, StoryRequirementSource Source, JoinKind Join, IReadOnlyList<IReadOnlyList<uint>> Options);

/// <summary>A character's standing on the story's side quests.</summary>
/// <param name="Done">Required side quests completed.</param>
/// <param name="Total">Required side quests the character has to do: for each requirement the option closest to done, each quest once.</param>
/// <param name="LeftFor">Per main scenario quest row id, the side quests still to do before it; only for requirements with quests left.</param>
public sealed record StoryRequirementsProgress(int Done, int Total, IReadOnlyDictionary<uint, IReadOnlyList<uint>> LeftFor)
{
    public static readonly StoryRequirementsProgress None = new(0, 0, new Dictionary<uint, IReadOnlyList<uint>>());
}

/// <summary>
/// The side quests the main scenario requires (feature plan v7 N3, "a true story meter"): the Crystal Tower series, the
/// hard primals, the Shadowbringers role quests. Three sources, read once per catalog and duty source:
/// <list type="number">
/// <item>the main scenario quests' previous quests (<see cref="QuestCatalog.PrerequisitesOf"/>) outside the story, past its start;</item>
/// <item>the duties a main scenario quest needs cleared (<see cref="QuestRecord.InstanceContentRequired"/>) when no
/// main scenario quest unlocks them: their unlock quests, from the duty unlock data
/// (<see cref="CatchUpDutySource.QuestsUnlocking"/>);</item>
/// <item><c>curated/story_required.json</c> for what the sheets do not record (<see cref="StoryRequiredEntry"/>).</item>
/// </list>
/// Each named side quest brings its own side-quest prerequisites (an Any join among them takes the first quest the game
/// has not removed, none when a story quest is among them). Removed and repeatable quests never count. Immutable.
/// </summary>
public sealed class StoryRequirements
{
    public static readonly StoryRequirements Empty = new([]);

    private static readonly ConditionalWeakTable<QuestCatalog, Holder> Cache = [];

    private StoryRequirements(IReadOnlyList<StoryRequirement> all)
    {
        All = all;
        var quests = new HashSet<uint>();
        foreach (var requirement in all)
        {
            foreach (var option in requirement.Options)
            {
                quests.UnionWith(option);
            }
        }

        SideQuests = quests;
    }

    /// <summary>Every requirement, in story order.</summary>
    public IReadOnlyList<StoryRequirement> All { get; }

    /// <summary>Every side quest any requirement names.</summary>
    public IReadOnlySet<uint> SideQuests { get; }

    /// <summary>
    /// The requirements for <paramref name="catalog"/> with <paramref name="duties"/>' unlock data and curated entries;
    /// without a duty source only the previous quests count. Cached per catalog for the last duty source.
    /// </summary>
    public static StoryRequirements For(QuestCatalog catalog, CatchUpDutySource? duties)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var holder = Cache.GetValue(catalog, static _ => new Holder());
        lock (holder)
        {
            if (holder.Value is null || !ReferenceEquals(holder.Duties, duties))
            {
                holder.Duties = duties;
                holder.Value = Build(catalog, duties?.ConditionOf, duties?.QuestsUnlocking, duties?.StoryRequired);
            }

            return holder.Value;
        }
    }

    /// <summary>
    /// Builds the requirements.
    /// </summary>
    /// <param name="conditionOf">InstanceContent id to its ContentFinderCondition id; null leaves the duty source out.</param>
    /// <param name="questsUnlocking">ContentFinderCondition id to the quests that unlock it; null leaves the duty source out.</param>
    /// <param name="curated">Curated entries by main scenario quest; null for none.</param>
    public static StoryRequirements Build(
        QuestCatalog catalog,
        Func<uint, uint>? conditionOf,
        Func<uint, IReadOnlyList<uint>>? questsUnlocking,
        IReadOnlyDictionary<uint, StoryRequiredEntry>? curated)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var graph = MsqGraph.For(catalog);
        if (graph.Story.Count == 0)
        {
            return Empty;
        }

        var story = new HashSet<uint>();
        foreach (var quest in graph.Story)
        {
            story.Add(quest.RowId);
        }

        var list = new List<StoryRequirement>();
        foreach (var quest in graph.Story)
        {
            // 1. Previous quests outside the story. A story quest none of whose previous quests is a story quest starts
            // the story (Close to Home after a city's Coming to …): what comes before it is the character's start, not a
            // side quest the story asks for on the way. An Any join a story quest meets needs no side quest either.
            var previous = catalog.PrerequisitesOf(quest);
            var outside = previous.QuestIds.Where(id => !story.Contains(id)).ToArray();
            if (outside.Length > 0 && outside.Length < previous.QuestIds.Length && previous.Join == JoinKind.All)
            {
                Add(list, quest, StoryRequirementSource.Prerequisite, JoinKind.All, [Line(catalog, story, outside)]);
            }

            // 2. Duties to clear that only side quests unlock.
            if (conditionOf is not null && questsUnlocking is not null && quest.InstanceContentRequired.Length > 0)
            {
                var anyDuty = quest.InstanceJoin == JoinKind.Any;
                var pooled = new List<IReadOnlyList<uint>>();
                var storyOpensOne = false;
                foreach (var instance in quest.InstanceContentRequired)
                {
                    var condition = instance == 0 ? 0 : conditionOf(instance);
                    var unlockers = condition == 0 ? [] : questsUnlocking(condition)
                        .Where(id => catalog.GetByRowId(id) is { IsRemoved: false, IsRepeatable: false })
                        .ToArray();
                    if (unlockers.Length == 0 || unlockers.Any(story.Contains))
                    {
                        // The story opens it (or nothing known does): no side quest needed for this duty.
                        storyOpensOne = true;
                        continue;
                    }

                    var options = unlockers.Select(id => Line(catalog, story, [id])).ToArray();
                    if (anyDuty)
                    {
                        pooled.AddRange(options);
                    }
                    else
                    {
                        Add(list, quest, StoryRequirementSource.Duty, JoinKind.Any, options);
                    }
                }

                if (anyDuty && pooled.Count > 0 && !storyOpensOne)
                {
                    Add(list, quest, StoryRequirementSource.Duty, JoinKind.Any, pooled);
                }
            }

            // 3. The curated overlay.
            if (curated is not null && curated.TryGetValue(quest.RowId, out var entry))
            {
                var named = entry.Quests.Where(id => !story.Contains(id) && catalog.GetByRowId(id) is { IsRemoved: false }).ToArray();
                if (named.Length > 0)
                {
                    IReadOnlyList<IReadOnlyList<uint>> options = entry.Join == JoinKind.All
                        ? [Line(catalog, story, named)]
                        : named.Select(id => Line(catalog, story, [id])).ToArray();
                    Add(list, quest, StoryRequirementSource.Curated, entry.Join, options);
                }
            }
        }

        return list.Count == 0 ? Empty : new StoryRequirements(list);
    }

    /// <summary>
    /// Where the character stands: per requirement whose main scenario quest still counts for it, the option with the
    /// fewest quests left (the first among equals); quests that leave the totals (another Grand Company's, out of season)
    /// are not counted.
    /// </summary>
    public StoryRequirementsProgress Progress(IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        if (All.Count == 0)
        {
            return StoryRequirementsProgress.None;
        }

        var counted = new HashSet<uint>();
        var done = 0;
        var leftFor = new Dictionary<uint, List<uint>>();
        foreach (var requirement in All)
        {
            if (states.GetValueOrDefault(requirement.StoryQuest.RowId) is { LeavesTotals: true })
            {
                continue;
            }

            IEnumerable<IReadOnlyList<uint>> chosen = requirement.Options;
            if (requirement.Join == JoinKind.Any)
            {
                IReadOnlyList<uint>? best = null;
                var bestLeft = int.MaxValue;
                foreach (var option in requirement.Options)
                {
                    var left = option.Count(id => Counts(states, id) && !IsDone(states, id));
                    if (left < bestLeft)
                    {
                        best = option;
                        bestLeft = left;
                    }
                }

                chosen = best is null ? [] : [best];
            }

            foreach (var option in chosen)
            {
                foreach (var id in option)
                {
                    if (!Counts(states, id))
                    {
                        continue;
                    }

                    var isDone = IsDone(states, id);
                    if (counted.Add(id) && isDone)
                    {
                        done++;
                    }

                    if (!isDone)
                    {
                        if (!leftFor.TryGetValue(requirement.StoryQuest.RowId, out var left))
                        {
                            left = [];
                            leftFor[requirement.StoryQuest.RowId] = left;
                        }

                        if (!left.Contains(id))
                        {
                            left.Add(id);
                        }
                    }
                }
            }
        }

        return new StoryRequirementsProgress(done, counted.Count, leftFor.ToDictionary(static kv => kv.Key, static kv => (IReadOnlyList<uint>)kv.Value));
    }

    private static bool Counts(IReadOnlyDictionary<uint, QuestEvaluation> states, uint rowId) =>
        states.GetValueOrDefault(rowId) is not { LeavesTotals: true };

    private static bool IsDone(IReadOnlyDictionary<uint, QuestEvaluation> states, uint rowId) =>
        states.GetValueOrDefault(rowId)?.State == QuestState.Completed;

    private static void Add(List<StoryRequirement> list, QuestRecord quest, StoryRequirementSource source, JoinKind join, IReadOnlyList<IReadOnlyList<uint>> options)
    {
        var kept = options.Where(static o => o.Count > 0).ToArray();
        if (kept.Length == 0)
        {
            return;
        }

        list.Add(new StoryRequirement(quest, source, join, kept));
    }

    /// <summary>
    /// The side quests <paramref name="named"/> needs, in play order (prerequisites first): each named quest and, through
    /// its previous quests, every side quest before it. Story, removed and repeatable quests are left out.
    /// </summary>
    private static IReadOnlyList<uint> Line(QuestCatalog catalog, HashSet<uint> story, IReadOnlyList<uint> named)
    {
        var order = new List<uint>();
        var seen = new HashSet<uint>();
        foreach (var id in named)
        {
            Visit(catalog, story, id, seen, order);
        }

        return order;
    }

    private static void Visit(QuestCatalog catalog, HashSet<uint> story, uint rowId, HashSet<uint> seen, List<uint> order)
    {
        if (story.Contains(rowId) || !seen.Add(rowId) || catalog.GetByRowId(rowId) is not { IsRemoved: false, IsRepeatable: false } quest)
        {
            return;
        }

        var previous = catalog.PrerequisitesOf(quest);
        if (previous.Join == JoinKind.All)
        {
            foreach (var id in previous.QuestIds)
            {
                Visit(catalog, story, id, seen, order);
            }
        }
        else if (!previous.QuestIds.Any(story.Contains))
        {
            // One of them will do: the first the game has not removed.
            foreach (var id in previous.QuestIds)
            {
                if (catalog.GetByRowId(id) is { IsRemoved: false })
                {
                    Visit(catalog, story, id, seen, order);
                    break;
                }
            }
        }

        order.Add(rowId);
    }

    private sealed class Holder
    {
        public CatchUpDutySource? Duties { get; set; }

        public StoryRequirements? Value { get; set; }
    }
}

/// <summary>
/// The true story meter (feature plan v7 N3): main scenario quests done and in all, as <see cref="MsqPosition"/> counts
/// them, plus the side quests the story needs (<see cref="StoryRequirements"/>), so "94%" means what is really left.
/// </summary>
/// <param name="StoryDone">Main scenario quests done (<see cref="MsqPosition.Done"/>).</param>
/// <param name="StoryTotal">Main scenario quests in all (<see cref="MsqPosition.Total"/>).</param>
/// <param name="SideDone">Required side quests done.</param>
/// <param name="SideTotal">Required side quests in all.</param>
public sealed record StoryMeter(int StoryDone, int StoryTotal, int SideDone, int SideTotal)
{
    public int Done => StoryDone + SideDone;

    public int Total => StoryTotal + SideTotal;

    /// <summary>Whole percent done, rounded down, so it reads 100 only when nothing is left.</summary>
    public int Percent => Total == 0 ? 100 : (int)(100L * Done / Total);

    /// <summary>The meter for a character; null when the catalog has no main scenario.</summary>
    public static StoryMeter? Compute(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> states, CatchUpDutySource? duties)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(states);
        if (MsqProgress.Compute(catalog, states) is not { } position)
        {
            return null;
        }

        var side = StoryRequirements.For(catalog, duties).Progress(states);
        return new StoryMeter(position.Done, position.Total, side.Done, side.Total);
    }
}
