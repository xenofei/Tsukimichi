using System.Runtime.CompilerServices;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Evaluation;

/// <summary>What a choice group chooses between, in the order a reason is picked when a quest is on several.</summary>
public enum PathKind : byte
{
    /// <summary>The start city: Gridania, Limsa Lominsa or Ul'dah.</summary>
    StartCity,

    /// <summary>The A Realm Reborn starting class (its "Close to Home" and its starter "Way of" track).</summary>
    StartClass,

    /// <summary>A Grand Company's own version of a quest (The Company You Keep, My Little Chocobo, …).</summary>
    GrandCompany,

    /// <summary>Any other set of quests only one of which can be done (QuestLock sets, same-name variants).</summary>
    Choice,
}

/// <summary>A name in a path phrase: a label, or a Grand Company printed through the sheet's name for it.</summary>
/// <param name="Text">The curated label or quest name; the fallback when <paramref name="GrandCompany"/> has no name.</param>
/// <param name="GrandCompany">Grand Company row id; 0 when the label is plain text.</param>
public readonly record struct PathLabel(string Text, byte GrandCompany = 0);

/// <summary>
/// One choice group a quest belongs to, as its <see cref="OtherPathRequirement"/> reports it.
/// </summary>
/// <param name="Kind">What the group chooses between.</param>
/// <param name="Options">The options the quest belongs to (a single one for "Ul'dah"); empty when <paramref name="Except"/> says it.</param>
/// <param name="Except">Set when the quest belongs to every option but one (a class's switcher track: "not Lancer").</param>
/// <param name="Chosen">The character's option; null while the group is undecided.</param>
/// <param name="Excludes">Whether the character's option is not one of the quest's: the quest is on another path.</param>
public sealed record PathFacet(PathKind Kind, IReadOnlyList<PathLabel> Options, PathLabel? Except, PathLabel? Chosen, bool Excludes);

/// <summary>One option of a choice group.</summary>
/// <param name="Index">Its place in the group (its bit in a <see cref="PathTag"/> mask).</param>
/// <param name="Label">What the phrases call it: a curated city or class name, or the option's quest name.</param>
/// <param name="Anchors">Quest row ids whose completion picks this option (a city's first quest, a class's "Close to Home" and starter, a set's member).</param>
/// <param name="GrandCompany">The Grand Company the option belongs to; 0 for none.</param>
/// <param name="ClassJob">The starting class the option is; 0 for none.</param>
public sealed record PathOption(int Index, string Label, IReadOnlyList<uint> Anchors, byte GrandCompany, byte ClassJob)
{
    public PathLabel ToLabel() => new(Label, GrandCompany);
}

/// <summary>A set of paths only one of which a character takes.</summary>
public sealed record PathGroup(int Id, PathKind Kind, IReadOnlyList<PathOption> Options)
{
    /// <summary>Every option's bit.</summary>
    public ulong AllOptions => Options.Count >= 64 ? ulong.MaxValue : (1UL << Options.Count) - 1;
}

/// <summary>A quest's place in one group: the bit set of the options it can be done on.</summary>
public readonly record struct PathTag(int Group, ulong Options);

/// <summary>An A Realm Reborn class track pair found by rule: the starter "Way of" (no previous quest) and the switcher line, rejoined by an Any join.</summary>
public sealed record ClassTrack(uint Starter, uint Switcher, uint Join, uint GenreId);

/// <summary>
/// The choice groups of one catalog (feature plan v4 D1): sets of paths of which a character takes only one, found from
/// the sheets and carried forward through the previous-quest graph, so every quest knows the options it can be done on.
/// Built once per catalog and cached with it, like <see cref="Query.MsqGraph"/>.
/// <para>
/// <b>Groups.</b>
/// <list type="bullet">
/// <item>Start city: quests with no previous quest one of whose successors is a level-1 main scenario quest (the three
/// "Coming to" quests), kept only where the curated pin (<see cref="PathChoices.Cities"/>) agrees, which also names them.</item>
/// <item>Starting class: per curated class, its "Close to Home" row (found by rule as a sibling set: same name, same
/// previous quests, joined again by one Any join) and its starter "Way of" quest; the class's switcher track (the
/// class intro through "My First …", found by rule as the other side of an Any join in the Class &amp; Job section)
/// belongs to every class but that one.</item>
/// <item>Grand Company and other choices: every set of live quests that lock one another (QuestLock), and every
/// other sibling set. A set whose members each carry a different Grand Company (the sheet's column or a curated tag)
/// is a Grand Company group: The Company You Keep, My Little Chocobo, Call of the Wild and the rest.</item>
/// </list>
/// </para>
/// <para>
/// <b>Propagation.</b> Each group's options are carried forward in topological order: a quest whose previous quests
/// join with All can be done on the options all of them allow (the sets intersect), one whose previous quests join
/// with Any on the options any of them allows (the sets unite). Per group; a quest a group does not constrain carries
/// no tag for it.
/// </para>
/// <para>
/// <b>A character's choice</b> (<see cref="Resolve"/>): a group is decided by the one option whose anchor quest the
/// character completed (a start city also by the city's first quest in the journal). Two completed options make the
/// group not exclusive for that character. An undecided group presumes an option (the character's Grand Company or
/// current class where that picks one, else the first open option in journal order).
/// </para>
/// </summary>
public sealed class PathIndex
{
    private static readonly ConditionalWeakTable<QuestCatalog, PathChoices> Attached = [];
    private static readonly ConditionalWeakTable<QuestCatalog, PathIndex> Cache = [];
    private static readonly PathTag[] NoTags = [];

    private readonly QuestCatalog catalog;
    private readonly Dictionary<uint, PathTag[]> tags;
    private readonly HashSet<uint> anchors = [];
    private readonly Dictionary<uint, int> order = [];
    private readonly ConditionalWeakTable<CharacterSnapshot, PathChoice> resolved = [];

    private PathIndex(QuestCatalog catalog, PathChoices choices)
    {
        this.catalog = catalog;
        for (var i = 0; i < catalog.All.Count; i++)
        {
            order.TryAdd(catalog.All[i].RowId, i);
        }

        var successors = Successors(catalog);
        RuleCityRoots = FindCityRoots(catalog, successors);
        SiblingSets = FindSiblingSets(catalog);
        ClassTracks = FindClassTracks(catalog);

        var groups = new List<PathGroup>();
        var seeds = new Dictionary<uint, Dictionary<int, ulong>>();
        void Seed(uint rowId, int group, ulong options)
        {
            if (!seeds.TryGetValue(rowId, out var byGroup))
            {
                seeds[rowId] = byGroup = [];
            }

            byGroup[group] = byGroup.TryGetValue(group, out var known) ? known & options : options;
        }

        AddCityGroup(choices, groups, Seed);
        var classAnchors = AddClassGroup(choices, groups, Seed);
        var lockSets = AddLockGroups(choices, groups, Seed);
        foreach (var set in SiblingSets)
        {
            // The class sets are the starting-class group; a set that is also a lock set is already a group.
            if (set.All(classAnchors.Contains) || lockSets.Any(l => l.SetEquals(set)))
            {
                continue;
            }

            AddGroup(PathKind.Choice, set.Select(id => catalog.ByRowId[id]).ToList(), choices, groups, Seed);
        }

        Groups = groups;
        tags = Propagate(catalog, seeds, groups);
    }

    /// <summary>Every choice group, in the order they were found (city, class, lock sets, other sibling sets).</summary>
    public IReadOnlyList<PathGroup> Groups { get; }

    /// <summary>The start-city roots the rule finds, in journal order, before the curated pin is applied.</summary>
    public IReadOnlyList<uint> RuleCityRoots { get; }

    /// <summary>The sibling sets the rule finds (same name, same previous quests, joined again by one Any join), each ascending.</summary>
    public IReadOnlyList<IReadOnlyList<uint>> SiblingSets { get; }

    /// <summary>The class track pairs the rule finds, in journal order of their join.</summary>
    public IReadOnlyList<ClassTrack> ClassTracks { get; }

    /// <summary>Every anchor quest: a completion that can change a character's choice.</summary>
    public IReadOnlySet<uint> Anchors => anchors;

    /// <summary>How many quests carry at least one tag.</summary>
    public int TaggedCount => tags.Count;

    /// <summary>
    /// Attaches the curated labels and guards to a catalog before its index is first built (the catalog builders call
    /// it); a catalog without them gets the rule-found groups that need no curation (lock sets and sibling sets) only.
    /// Replaces an index already built for the catalog.
    /// </summary>
    public static void Attach(QuestCatalog catalog, PathChoices? choices)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Attached.AddOrUpdate(catalog, choices ?? PathChoices.Empty);
        Cache.Remove(catalog);
    }

    /// <summary>The index for a catalog, built on first use and cached with it.</summary>
    public static PathIndex For(QuestCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return Cache.GetValue(catalog, static c => new PathIndex(c, Attached.TryGetValue(c, out var choices) ? choices : PathChoices.Empty));
    }

    /// <summary>A fresh index (not cached) with its own curated data.</summary>
    public static PathIndex Build(QuestCatalog catalog, PathChoices? choices)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return new PathIndex(catalog, choices ?? PathChoices.Empty);
    }

    /// <summary>The groups a quest belongs to with the options it can be done on; empty for a quest no group constrains.</summary>
    public IReadOnlyList<PathTag> TagsOf(uint rowId) => tags.TryGetValue(rowId, out var list) ? list : NoTags;

    /// <summary>The quest's place in journal order; past the end for a row the catalog does not hold.</summary>
    internal int OrderOf(uint rowId) => order.TryGetValue(rowId, out var at) ? at : int.MaxValue;

    /// <summary>Whether a completion change of <paramref name="rowId"/> can change a character's choice.</summary>
    public bool IsAnchor(uint rowId) => anchors.Contains(rowId);

    /// <summary>The character's choices over this catalog, computed once per snapshot instance.</summary>
    public PathChoice Resolve(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return resolved.GetValue(snapshot, s => new PathChoice(this, catalog, s));
    }

    internal IReadOnlyDictionary<uint, PathTag[]> AllTags => tags;

    private static Dictionary<uint, List<uint>> Successors(QuestCatalog catalog)
    {
        var map = new Dictionary<uint, List<uint>>();
        foreach (var quest in catalog.All)
        {
            foreach (var id in quest.PreviousQuests.QuestIds)
            {
                if (!map.TryGetValue(id, out var list))
                {
                    map[id] = list = [];
                }

                list.Add(quest.RowId);
            }
        }

        return map;
    }

    /// <summary>No previous quest, not retired, and a successor that is a live level-1 main scenario quest (section 0).</summary>
    private static List<uint> FindCityRoots(QuestCatalog catalog, Dictionary<uint, List<uint>> successors)
    {
        var roots = new List<uint>();
        foreach (var quest in catalog.All)
        {
            if (!quest.PreviousQuests.IsEmpty || quest.IsRetired || !successors.TryGetValue(quest.RowId, out var after))
            {
                continue;
            }

            foreach (var id in after)
            {
                if (catalog.GetByRowId(id) is { IsRemoved: false, Level: <= 1 } next && next.Journal.SectionId == 0)
                {
                    roots.Add(quest.RowId);
                    break;
                }
            }
        }

        return roots;
    }

    /// <summary>Two or more live quests of one name and one previous-quest set, listed together in one Any join.</summary>
    private static List<IReadOnlyList<uint>> FindSiblingSets(QuestCatalog catalog)
    {
        var sets = new List<IReadOnlyList<uint>>();
        foreach (var quest in catalog.All)
        {
            if (quest.PreviousQuests.Join != JoinKind.Any || quest.PreviousQuests.QuestIds.Length < 2)
            {
                continue;
            }

            var members = new List<QuestRecord>();
            foreach (var id in quest.PreviousQuests.QuestIds.Distinct())
            {
                if (catalog.GetByRowId(id) is { IsRemoved: false } member)
                {
                    members.Add(member);
                }
            }

            foreach (var group in members.GroupBy(m => m.Name + "|" + string.Join(',', m.PreviousQuests.QuestIds.Order())).Where(g => g.Count() >= 2))
            {
                var ids = group.Select(m => m.RowId).Order().ToArray();
                if (!sets.Any(s => s.SequenceEqual(ids)))
                {
                    sets.Add(ids);
                }
            }
        }

        return sets;
    }

    /// <summary>Any joins of exactly two in the Class &amp; Job section (6) where one side has no previous quest and the other has.</summary>
    private static List<ClassTrack> FindClassTracks(QuestCatalog catalog)
    {
        var tracks = new List<ClassTrack>();
        foreach (var quest in catalog.All)
        {
            if (quest.IsRemoved || quest.Journal.SectionId != 6 || quest.PreviousQuests.Join != JoinKind.Any || quest.PreviousQuests.QuestIds.Length != 2)
            {
                continue;
            }

            var a = catalog.GetByRowId(quest.PreviousQuests.QuestIds[0]);
            var b = catalog.GetByRowId(quest.PreviousQuests.QuestIds[1]);
            if (a is null || b is null)
            {
                continue;
            }

            if (a.PreviousQuests.IsEmpty && !b.PreviousQuests.IsEmpty)
            {
                tracks.Add(new ClassTrack(a.RowId, b.RowId, quest.RowId, quest.Journal.GenreId));
            }
            else if (b.PreviousQuests.IsEmpty && !a.PreviousQuests.IsEmpty)
            {
                tracks.Add(new ClassTrack(b.RowId, a.RowId, quest.RowId, quest.Journal.GenreId));
            }
        }

        return tracks;
    }

    private void AddCityGroup(PathChoices choices, List<PathGroup> groups, Action<uint, int, ulong> seed)
    {
        // The pin is a guard: a root counts only when the rule finds it too, and the pin names it.
        var options = new List<PathOption>();
        foreach (var pin in choices.Cities)
        {
            if (RuleCityRoots.Contains(pin.Root))
            {
                options.Add(new PathOption(options.Count, pin.Label, [pin.Root], 0, 0));
            }
        }

        if (options.Count < 2)
        {
            return;
        }

        var group = new PathGroup(groups.Count, PathKind.StartCity, options);
        groups.Add(group);
        foreach (var option in options)
        {
            seed(option.Anchors[0], group.Id, 1UL << option.Index);
            anchors.Add(option.Anchors[0]);
        }
    }

    /// <summary>The starting-class group; returns every "Close to Home" row it holds, so the sibling-set pass skips them.</summary>
    private HashSet<uint> AddClassGroup(PathChoices choices, List<PathGroup> groups, Action<uint, int, ulong> seed)
    {
        var homes = new HashSet<uint>();
        var options = new List<PathOption>();
        var pins = new List<ClassPin>();
        foreach (var pin in choices.Classes)
        {
            var list = new List<uint>(2);
            if (catalog.ByRowId.ContainsKey(pin.CloseToHome))
            {
                list.Add(pin.CloseToHome);
            }

            if (catalog.ByRowId.ContainsKey(pin.Starter))
            {
                list.Add(pin.Starter);
            }

            if (list.Count == 0)
            {
                continue;
            }

            options.Add(new PathOption(options.Count, pin.Label, list, 0, pin.ClassJob));
            pins.Add(pin);
            homes.Add(pin.CloseToHome);
        }

        if (options.Count < 2)
        {
            return [];
        }

        var group = new PathGroup(groups.Count, PathKind.StartClass, options);
        groups.Add(group);
        for (var i = 0; i < options.Count; i++)
        {
            var bit = 1UL << i;
            foreach (var anchor in options[i].Anchors)
            {
                seed(anchor, group.Id, bit);
                anchors.Add(anchor);
            }

            // The switcher track, from its "My First …" back through the class intro: every class but this one.
            foreach (var track in ClassTracks)
            {
                if (track.Starter != pins[i].Starter)
                {
                    continue;
                }

                var current = catalog.GetByRowId(track.Switcher);
                var seen = new HashSet<uint>();
                while (current is not null && seen.Add(current.RowId))
                {
                    seed(current.RowId, group.Id, group.AllOptions & ~bit);
                    current = current.PreviousQuests.QuestIds.Length == 1
                        && catalog.GetByRowId(current.PreviousQuests.QuestIds[0]) is { } before
                        && before.Journal.GenreId == current.Journal.GenreId
                            ? before
                            : null;
                }
            }
        }

        return homes;
    }

    /// <summary>Every set of two or more live quests each of which locks all the others; returns the sets found.</summary>
    private List<HashSet<uint>> AddLockGroups(PathChoices choices, List<PathGroup> groups, Action<uint, int, ulong> seed)
    {
        var found = new List<HashSet<uint>>();
        var seen = new HashSet<uint>();
        foreach (var quest in catalog.All)
        {
            if (quest.IsRemoved || quest.QuestLocks.Length == 0 || seen.Contains(quest.RowId))
            {
                continue;
            }

            // The connected set over lock edges between live quests, either direction.
            var set = new HashSet<uint> { quest.RowId };
            var stack = new Stack<QuestRecord>();
            stack.Push(quest);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                foreach (var id in current.QuestLocks)
                {
                    if (catalog.GetByRowId(id) is { IsRemoved: false } other && set.Add(other.RowId))
                    {
                        stack.Push(other);
                    }
                }

                foreach (var other in catalog.All)
                {
                    if (!other.IsRemoved && Array.IndexOf(other.QuestLocks, current.RowId) >= 0 && set.Add(other.RowId))
                    {
                        stack.Push(other);
                    }
                }
            }

            seen.UnionWith(set);
            if (set.Count < 2 || set.Count > 63 || set.Any(anchors.Contains))
            {
                continue;
            }

            // Only a set where every member locks every other is a choice; a one-way lock (a later version closing an
            // earlier one) stays a plain lock.
            var complete = set.All(id => set.All(other => other == id || Array.IndexOf(catalog.ByRowId[id].QuestLocks, other) >= 0));
            if (!complete)
            {
                continue;
            }

            var members = set.Select(id => catalog.ByRowId[id]).OrderBy(q => q.Journal.SortKey).ThenBy(q => q.RowId).ToList();
            var gcs = members.Select(m => GrandCompanyOf(m, choices)).ToList();
            var kind = gcs.All(gc => gc != 0) && gcs.Distinct().Count() == gcs.Count ? PathKind.GrandCompany : PathKind.Choice;
            AddGroup(kind, members, choices, groups, seed);
            found.Add(set);
        }

        return found;
    }

    private void AddGroup(PathKind kind, List<QuestRecord> members, PathChoices choices, List<PathGroup> groups, Action<uint, int, ulong> seed)
    {
        var options = new List<PathOption>(members.Count);
        foreach (var member in members.OrderBy(q => q.Journal.SortKey).ThenBy(q => q.RowId))
        {
            options.Add(new PathOption(options.Count, member.Name, [member.RowId], kind == PathKind.GrandCompany ? GrandCompanyOf(member, choices) : (byte)0, 0));
        }

        var group = new PathGroup(groups.Count, kind, options);
        groups.Add(group);
        foreach (var option in options)
        {
            seed(option.Anchors[0], group.Id, 1UL << option.Index);
            anchors.Add(option.Anchors[0]);
        }
    }

    private static byte GrandCompanyOf(QuestRecord quest, PathChoices choices) =>
        choices.GrandCompanies.TryGetValue(quest.RowId, out var tag) ? tag.GrandCompany : quest.GrandCompany;

    /// <summary>
    /// Carries every group's options forward in topological order (Kahn's algorithm over previous quests, journal order
    /// between equals; a cycle is broken by taking what is left in journal order).
    /// </summary>
    private static Dictionary<uint, PathTag[]> Propagate(QuestCatalog catalog, Dictionary<uint, Dictionary<int, ulong>> seeds, List<PathGroup> groups)
    {
        var order = TopologicalOrder(catalog);
        var masks = new Dictionary<uint, Dictionary<int, ulong>>();
        var groupsSeen = new HashSet<int>();
        foreach (var quest in order)
        {
            Dictionary<int, ulong>? mine = null;
            var prereqs = quest.PreviousQuests.QuestIds;
            if (prereqs.Length > 0)
            {
                if (quest.PreviousQuests.Join == JoinKind.All)
                {
                    // Intersect: every constrained previous quest narrows the options.
                    foreach (var id in prereqs)
                    {
                        if (id == quest.RowId || !masks.TryGetValue(id, out var before))
                        {
                            continue;
                        }

                        mine ??= [];
                        foreach (var (group, options) in before)
                        {
                            mine[group] = mine.TryGetValue(group, out var known) ? known & options : options;
                        }
                    }
                }
                else
                {
                    // Unite: a group constrains the quest only when every previous quest it lists is constrained by it.
                    groupsSeen.Clear();
                    Dictionary<int, ulong>? union = null;
                    var first = true;
                    foreach (var id in prereqs)
                    {
                        if (id == quest.RowId)
                        {
                            continue;
                        }

                        if (!masks.TryGetValue(id, out var before))
                        {
                            union = null;
                            break;
                        }

                        if (first)
                        {
                            union = new Dictionary<int, ulong>(before);
                            first = false;
                            continue;
                        }

                        foreach (var group in union!.Keys.ToList())
                        {
                            if (before.TryGetValue(group, out var options))
                            {
                                union[group] |= options;
                            }
                            else
                            {
                                union.Remove(group);
                            }
                        }
                    }

                    mine = union is { Count: > 0 } ? union : null;
                }
            }

            if (seeds.TryGetValue(quest.RowId, out var own))
            {
                mine = mine is null ? new Dictionary<int, ulong>(own) : new Dictionary<int, ulong>(mine);
                foreach (var (group, options) in own)
                {
                    mine[group] = mine.TryGetValue(group, out var known) ? known & options : options;
                }
            }

            if (mine is { Count: > 0 })
            {
                masks[quest.RowId] = mine;
            }
        }

        // A quest every option of a group can reach (an Any join that brings the lines together) is not constrained by it.
        var result = new Dictionary<uint, PathTag[]>(masks.Count);
        foreach (var (rowId, byGroup) in masks)
        {
            var list = byGroup.Where(p => p.Value != groups[p.Key].AllOptions).OrderBy(p => p.Key).Select(p => new PathTag(p.Key, p.Value)).ToArray();
            if (list.Length > 0)
            {
                result[rowId] = list;
            }
        }

        return result;
    }

    private static List<QuestRecord> TopologicalOrder(QuestCatalog catalog)
    {
        var index = new Dictionary<uint, int>(catalog.Count);
        for (var i = 0; i < catalog.All.Count; i++)
        {
            index[catalog.All[i].RowId] = i;
        }

        var pending = new int[catalog.Count];
        var dependents = new Dictionary<uint, List<int>>();
        for (var i = 0; i < catalog.All.Count; i++)
        {
            var quest = catalog.All[i];
            foreach (var id in quest.PreviousQuests.QuestIds.Distinct())
            {
                if (id == quest.RowId || !index.ContainsKey(id))
                {
                    continue;
                }

                pending[i]++;
                if (!dependents.TryGetValue(id, out var list))
                {
                    dependents[id] = list = [];
                }

                list.Add(i);
            }
        }

        var ready = new PriorityQueue<int, int>();
        for (var i = 0; i < pending.Length; i++)
        {
            if (pending[i] == 0)
            {
                ready.Enqueue(i, i);
            }
        }

        var placed = new bool[catalog.Count];
        var order = new List<QuestRecord>(catalog.Count);
        var scan = 0;
        while (order.Count < catalog.Count)
        {
            if (ready.Count == 0)
            {
                // A cycle: take the first quest not placed yet.
                while (placed[scan])
                {
                    scan++;
                }

                ready.Enqueue(scan, scan);
            }

            var next = ready.Dequeue();
            if (placed[next])
            {
                continue;
            }

            placed[next] = true;
            order.Add(catalog.All[next]);
            if (!dependents.TryGetValue(catalog.All[next].RowId, out var after))
            {
                continue;
            }

            foreach (var dependent in after)
            {
                if (--pending[dependent] == 0 && !placed[dependent])
                {
                    ready.Enqueue(dependent, dependent);
                }
            }
        }

        return order;
    }
}

/// <summary>
/// One character's choices over a <see cref="PathIndex"/>: which option each group is decided on, which quests lie on
/// a path not taken (<see cref="OtherPath"/>), and, for groups still undecided, which option is presumed and which
/// quests are spare alternatives (<see cref="IsSpare"/>). A completed quest, or one in the journal, is never on
/// another path and never spare.
/// </summary>
public sealed class PathChoice
{
    /// <summary>No group decided or presumed: nothing excluded, nothing spare.</summary>
    public static readonly PathChoice None = new();

    private readonly Dictionary<uint, OtherPathRequirement> excluded = [];
    private readonly HashSet<uint> spare = [];
    private readonly Dictionary<uint, int> choiceCounts = [];

    private readonly int[] chosen;
    private readonly int[] presumed;
    private readonly bool[] notExclusive;

    private PathChoice()
    {
        chosen = [];
        presumed = [];
        notExclusive = [];
    }

    internal PathChoice(PathIndex index, QuestCatalog catalog, CharacterSnapshot s)
    {
        var groups = index.Groups;
        chosen = new int[groups.Count];
        presumed = new int[groups.Count];
        notExclusive = new bool[groups.Count];
        var evidence = new uint[groups.Count][];

        bool Done(uint rowId) => s.IsCompleted(QuestRecord.ToQuestId(rowId));
        bool InJournal(uint rowId)
        {
            var questId = QuestRecord.ToQuestId(rowId);
            foreach (var accepted in s.Accepted)
            {
                if (accepted.QuestId == questId)
                {
                    return true;
                }
            }

            return false;
        }

        for (var g = 0; g < groups.Count; g++)
        {
            chosen[g] = -1;
            presumed[g] = -1;
            evidence[g] = [];
            var group = groups[g];
            var done = new List<int>();
            var proof = new List<uint>();
            foreach (var option in group.Options)
            {
                var any = false;
                foreach (var anchor in option.Anchors)
                {
                    if (Done(anchor))
                    {
                        any = true;
                        proof.Add(anchor);
                    }
                }

                if (any)
                {
                    done.Add(option.Index);
                }
            }

            if (done.Count >= 2)
            {
                // Both sides done: whatever the data says, the game let this character do more than one.
                notExclusive[g] = true;
                continue;
            }

            if (done.Count == 1)
            {
                chosen[g] = done[0];
                evidence[g] = [.. proof];
                continue;
            }

            if (group.Kind == PathKind.StartCity)
            {
                // A new character holds its city's first quest in the journal from the start.
                var held = group.Options.Where(o => o.Anchors.Any(InJournal)).ToList();
                if (held.Count == 1)
                {
                    chosen[g] = held[0].Index;
                    evidence[g] = [.. held[0].Anchors];
                }
            }
        }

        // Quests on a path the decided groups did not take.
        foreach (var (rowId, tags) in index.AllTags)
        {
            if (Done(rowId) || InJournal(rowId) || catalog.GetByRowId(rowId) is not { IsRemoved: false })
            {
                continue;
            }

            PathKind? decisive = null;
            var decisiveGroup = -1;
            foreach (var tag in tags)
            {
                var pick = chosen[tag.Group];
                if (pick >= 0 && (tag.Options & (1UL << pick)) == 0 && (decisive is null || groups[tag.Group].Kind < decisive))
                {
                    decisive = groups[tag.Group].Kind;
                    decisiveGroup = tag.Group;
                }
            }

            if (decisive is { } kind)
            {
                excluded[rowId] = new OtherPathRequirement(kind, Facets(groups, tags), evidence[decisiveGroup]);
            }
        }

        // Undecided groups, in group order (city, then class, then the rest): presume an option still open and in
        // keeping with the options presumed before it (a Gridania start presumes a Gridania class), then every quest
        // off it is a spare alternative.
        var presumedOff = new HashSet<uint>();

        // The character's company: the one it is in, else the one its decided company choice (The Company You Keep)
        // names; a company version of a quest presumes it.
        var company = s.GrandCompany;
        for (var g = 0; g < groups.Count && company == 0; g++)
        {
            if (groups[g].Kind == PathKind.GrandCompany && chosen[g] >= 0)
            {
                company = groups[g].Options[chosen[g]].GrandCompany;
            }
        }

        for (var g = 0; g < groups.Count; g++)
        {
            if (chosen[g] >= 0 || notExclusive[g])
            {
                continue;
            }

            var group = groups[g];
            // An option is open while its first anchor (the city's first quest, the class's "Close to Home", the
            // set's member) is not on a path already excluded.
            var open = group.Options.Where(o => !excluded.ContainsKey(o.Anchors[0])).ToList();
            var consistent = open.Where(o => !presumedOff.Contains(o.Anchors[0])).ToList();
            if (consistent.Count == 0)
            {
                consistent = open;
            }

            if (consistent.Count < 2)
            {
                continue;
            }

            // The cities keep the pin's order; every other group goes by journal order of its options.
            if (group.Kind != PathKind.StartCity)
            {
                consistent = [.. consistent.OrderBy(o => index.OrderOf(o.Anchors[0]))];
            }

            var guess = consistent.FirstOrDefault(o => o.Anchors.Any(InJournal))
                ?? (group.Kind == PathKind.GrandCompany && company != 0 ? consistent.FirstOrDefault(o => o.GrandCompany == company) : null)
                ?? (group.Kind == PathKind.StartClass ? consistent.FirstOrDefault(o => o.ClassJob == s.CurrentJob) : null)
                ?? consistent[0];
            presumed[g] = guess.Index;
            foreach (var option in consistent)
            {
                foreach (var anchor in option.Anchors)
                {
                    if (!excluded.ContainsKey(anchor))
                    {
                        choiceCounts[anchor] = consistent.Count;
                    }
                }
            }

            var bit = 1UL << guess.Index;
            foreach (var (rowId, tags) in index.AllTags)
            {
                foreach (var tag in tags)
                {
                    if (tag.Group == g && (tag.Options & bit) == 0)
                    {
                        presumedOff.Add(rowId);
                    }
                }
            }
        }

        foreach (var (rowId, tags) in index.AllTags)
        {
            if (excluded.ContainsKey(rowId) || Done(rowId) || InJournal(rowId))
            {
                continue;
            }

            foreach (var tag in tags)
            {
                var guess = presumed[tag.Group];
                if (guess >= 0 && (tag.Options & (1UL << guess)) == 0)
                {
                    spare.Add(rowId);
                    break;
                }
            }
        }

        Evidence = evidence;
    }

    /// <summary>Per group, the option the character's completed quests decided; -1 while undecided or not exclusive.</summary>
    public IReadOnlyList<int> Chosen => chosen;

    /// <summary>Per group, the option presumed while the group is undecided; -1 when decided, not exclusive or nothing to presume.</summary>
    public IReadOnlyList<int> Presumed => presumed;

    /// <summary>Per group, whether the character completed two or more of its options, so it excludes nothing for them.</summary>
    public IReadOnlyList<bool> NotExclusive => notExclusive;

    /// <summary>Per group, the completed (or, for a city, held) anchor quests that decided it.</summary>
    public IReadOnlyList<uint[]> Evidence { get; } = [];

    /// <summary>How many quests lie on another path.</summary>
    public int OtherPathCount => excluded.Count;

    /// <summary>How many quests are spare alternatives.</summary>
    public int SpareCount => spare.Count;

    /// <summary>The requirement a quest on another path carries; null for any other quest.</summary>
    public OtherPathRequirement? OtherPath(uint rowId) => excluded.GetValueOrDefault(rowId);

    /// <summary>Whether the quest lies off the presumed option of an undecided group (and on no path already excluded).</summary>
    public bool IsSpare(uint rowId) => spare.Contains(rowId);

    /// <summary>For an option quest of an undecided group, how many options are still open ("Choose one of 3"); 0 otherwise.</summary>
    public int ChoiceCount(uint rowId) => choiceCounts.GetValueOrDefault(rowId);

    private IReadOnlyList<PathFacet> Facets(IReadOnlyList<PathGroup> groups, PathTag[] tags)
    {
        var facets = new PathFacet[tags.Length];
        for (var i = 0; i < tags.Length; i++)
        {
            var group = groups[tags[i].Group];
            var mask = tags[i].Options;
            var picked = chosen[group.Id];
            var labels = new List<PathLabel>();
            PathLabel? except = null;
            var count = System.Numerics.BitOperations.PopCount(mask);
            if (group.Options.Count > 2 && count == group.Options.Count - 1)
            {
                except = group.Options.First(o => (mask & (1UL << o.Index)) == 0).ToLabel();
            }
            else
            {
                foreach (var option in group.Options)
                {
                    if ((mask & (1UL << option.Index)) != 0)
                    {
                        labels.Add(option.ToLabel());
                    }
                }
            }

            facets[i] = new PathFacet(
                group.Kind,
                labels,
                except,
                picked >= 0 ? group.Options[picked].ToLabel() : null,
                picked >= 0 && (mask & (1UL << picked)) == 0);
        }

        return facets;
    }
}
