using System.Collections.Frozen;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Chains;

/// <summary>
/// Story sidequests: the sidequests that carry journal artwork, the tell players use for "part of a small story",
/// and the per-zone side stories they form. A story sidequest sits in the Sidequests section of the journal
/// (<see cref="SidequestSectionId"/>: not the main scenario, not Chronicles, allied societies, class quests or
/// seasonal events), has a banner (<see cref="QuestRecord.Icon"/>), and is not an unlock quest (the feature set),
/// not repeatable and not removed.
/// <para>
/// One kind of unlock quest is let in: the aether current story lines. Since Shadowbringers most zones tell their
/// side stories in lines drawn blue, where the opening quest grants an aether current and the quests after it unlock
/// nothing of their own. A blue quest with artwork whose only unlocks are aether currents
/// (<see cref="FeaturePresets.UnlocksOnlyAetherCurrents"/>) is a story sidequest when it grants a current or is
/// linked, in the same genre or territory, through such quests to one that does. Every other unlock quest (duties,
/// systems, jobs, actions) stays out.
/// </para>
/// <para>
/// A side story is a maximal connected set of at least <see cref="ChainCatalog.MinChainLength"/> story sidequests
/// linked by previous-quest requirements, where each link joins two quests of the same journal genre or given in the
/// same territory. The typical Shadowbringers-to-Dawntrail zone has two short lines joined by a last quest that
/// requires both; that is one story. Its quests are in play order (a quest after everything it requires, ties in
/// journal order) and it is named after its first quest, "Story: &lt;name&gt;".
/// </para>
/// <para>
/// A heuristic, and it says so: a one-quest vignette with art is a story sidequest outside any chain; a line whose
/// later quests have no art, or that passes through another kind of unlock quest, keeps only its art-bearing part.
/// </para>
/// </summary>
public sealed class StorySidequests
{
    /// <summary>JournalSection row of "Sidequests" (regional sidequests, Side Story Quests, Hildibrand, Records of Unusual Endeavors, relic lines).</summary>
    public const uint SidequestSectionId = 3;

    /// <summary>Prefix of a side story's name, followed by its first quest's name.</summary>
    public const string ChainNamePrefix = "Story: ";

    public static readonly StorySidequests Empty = new(FrozenSet<uint>.Empty, [], FrozenDictionary<uint, (Chain, int)>.Empty, FrozenDictionary<uint, int>.Empty);

    private readonly FrozenDictionary<uint, (Chain Chain, int Index)> chainOf;
    private readonly FrozenDictionary<uint, int> order;

    private StorySidequests(
        FrozenSet<uint> rowIds,
        IReadOnlyList<Chain> chains,
        FrozenDictionary<uint, (Chain, int)> chainOf,
        FrozenDictionary<uint, int> order)
    {
        RowIds = rowIds;
        Chains = chains;
        this.chainOf = chainOf;
        this.order = order;
    }

    /// <summary>Row ids of every story sidequest, chained or not.</summary>
    public IReadOnlySet<uint> RowIds { get; }

    /// <summary>The side stories, ordered by the journal position of their earliest quest; each is a <see cref="Chain.IsStory"/> chain.</summary>
    public IReadOnlyList<Chain> Chains { get; }

    public int Count => RowIds.Count;

    public bool Contains(uint rowId) => RowIds.Contains(rowId);

    /// <summary>The side story a quest belongs to, or null (not a story sidequest, or a lone one).</summary>
    public Chain? ChainOf(uint rowId) => chainOf.TryGetValue(rowId, out var entry) ? entry.Chain : null;

    /// <summary>
    /// Reading order of a story sidequest: by zone (journal genre order), each side story in play order at the place
    /// of its earliest quest, lone ones at their own place; <see cref="int.MaxValue"/> for any other quest.
    /// </summary>
    public int OrderOf(uint rowId) => order.TryGetValue(rowId, out var index) ? index : int.MaxValue;

    /// <summary>
    /// Whether a quest is a story sidequest by the base rule: the section, artwork, unlock, repeatable and removed
    /// rules of the class summary. The four-argument <c>Build</c> also lets in the aether current story lines, which
    /// this test alone does not know.
    /// </summary>
    /// <param name="featureQuestIds">The derived unlock quests (<c>FeaturePresets.Derive</c>).</param>
    public static bool IsStorySidequest(QuestRecord quest, IReadOnlySet<uint> featureQuestIds)
    {
        ArgumentNullException.ThrowIfNull(featureQuestIds);
        return IsStoryShaped(quest) && !featureQuestIds.Contains(quest.RowId);
    }

    /// <summary>Finds the story sidequests by the base rule alone (every unlock quest left out) and derives the side stories.</summary>
    /// <param name="featureQuestIds">The derived unlock quests (<c>FeaturePresets.Derive</c>); they are never story sidequests.</param>
    public static StorySidequests Build(QuestCatalog catalog, IReadOnlySet<uint> featureQuestIds) => Build(catalog, featureQuestIds, null, null);

    /// <summary>
    /// Finds every story sidequest, the aether current story lines included, and derives the side stories. Computed
    /// once per catalog; the result is immutable.
    /// </summary>
    /// <param name="featureQuestIds">The derived unlock quests (<c>FeaturePresets.Derive</c>).</param>
    /// <param name="curated">The curated overlay, for its system and duty unlocks; null lets no unlock quest in.</param>
    /// <param name="uniqueRewards">The shipped unique-reward entries: which quests grant a current and which unlock something else. Null reads the quests' own rewards only.</param>
    /// <param name="minChainLength">Fewest quests a side story holds; a smaller connected set stays story sidequests outside any chain. <see cref="ChainCatalog.MinChainLength"/> unless a test says otherwise.</param>
    public static StorySidequests Build(
        QuestCatalog catalog,
        IReadOnlySet<uint> featureQuestIds,
        CuratedData? curated,
        IEnumerable<UniqueRewardEntry>? uniqueRewards,
        int minChainLength = ChainCatalog.MinChainLength)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(featureQuestIds);
        ArgumentOutOfRangeException.ThrowIfLessThan(minChainLength, 1);

        var stories = new Dictionary<uint, QuestRecord>();
        List<QuestRecord>? blue = null;
        foreach (var quest in catalog.All)
        {
            if (!IsStoryShaped(quest))
            {
                continue;
            }

            if (!featureQuestIds.Contains(quest.RowId))
            {
                stories.Add(quest.RowId, quest);
            }
            else if (curated is not null)
            {
                (blue ??= []).Add(quest);
            }
        }

        if (blue is not null)
        {
            AddAetherCurrentLines(catalog, blue, curated!, uniqueRewards ?? [], stories);
        }

        if (stories.Count == 0)
        {
            return Empty;
        }

        // Union-find over the story links; each quest keeps the links to the quests it requires for the ordering.
        var parent = new Dictionary<uint, uint>(stories.Count);
        foreach (var rowId in stories.Keys)
        {
            parent[rowId] = rowId;
        }

        var requires = new Dictionary<uint, List<uint>>();
        foreach (var quest in stories.Values)
        {
            foreach (var previousId in quest.PreviousQuests.QuestIds)
            {
                if (previousId == quest.RowId || !stories.TryGetValue(previousId, out var previous) || !SameStoryPlace(quest, previous))
                {
                    continue;
                }

                if (!requires.TryGetValue(quest.RowId, out var list))
                {
                    requires[quest.RowId] = list = [];
                }

                list.Add(previousId);
                parent[Find(parent, quest.RowId)] = Find(parent, previousId);
            }
        }

        // Components in journal order of their earliest quest (catalog.All is journal order).
        var members = new Dictionary<uint, List<QuestRecord>>();
        var roots = new List<uint>();
        foreach (var quest in catalog.All)
        {
            if (!stories.ContainsKey(quest.RowId))
            {
                continue;
            }

            var root = Find(parent, quest.RowId);
            if (!members.TryGetValue(root, out var list))
            {
                members[root] = list = [];
                roots.Add(root);
            }

            list.Add(quest);
        }

        var chains = new List<Chain>();
        var chainOf = new Dictionary<uint, (Chain, int)>();
        var order = new Dictionary<uint, int>(stories.Count);
        foreach (var root in roots)
        {
            var component = members[root];
            if (component.Count < minChainLength)
            {
                // Too few for a story: each quest keeps its own journal place (the component is in journal order), so
                // every story sidequest has a distinct OrderOf key, which QuestQuery's story sort relies on.
                foreach (var quest in component)
                {
                    order[quest.RowId] = order.Count;
                }

                continue;
            }

            var rowIds = PlayOrder(component, requires);
            var chain = new Chain(ChainNamePrefix + catalog.ByRowId[rowIds[0]].Name, rowIds) { IsStory = true };
            chains.Add(chain);
            for (var i = 0; i < rowIds.Length; i++)
            {
                chainOf[rowIds[i]] = (chain, i);
                order[rowIds[i]] = order.Count;
            }
        }

        return new StorySidequests(stories.Keys.ToFrozenSet(), chains, chainOf.ToFrozenDictionary(), order.ToFrozenDictionary());
    }

    /// <summary>Removed, repeatable, art-less and non-Sidequests quests are never story sidequests, unlock or not.</summary>
    private static bool IsStoryShaped(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return !quest.IsRemoved
            && !quest.IsRepeatable
            && quest.Icon != 0
            && quest.Journal.SectionId == SidequestSectionId;
    }

    /// <summary>
    /// Lets in the blue quests of the aether current story lines: those whose only unlocks are currents, when they
    /// grant one or are linked (a previous-quest link in the same genre or territory, either way) through such quests
    /// to one that does.
    /// </summary>
    private static void AddAetherCurrentLines(
        QuestCatalog catalog,
        List<QuestRecord> blue,
        CuratedData curated,
        IEnumerable<UniqueRewardEntry> uniqueRewards,
        Dictionary<uint, QuestRecord> stories)
    {
        var entries = uniqueRewards as IReadOnlyCollection<UniqueRewardEntry> ?? uniqueRewards.ToArray();
        var currents = FeaturePresets.AetherCurrentQuests(catalog, entries);
        var otherUnlocks = FeaturePresets.NonCurrentUnlockQuests(entries);
        blue.RemoveAll(quest => !FeaturePresets.UnlocksOnlyAetherCurrents(quest, curated, currents, otherUnlocks));

        // Links among the remaining blue quests, both ways: a line may open with a quest before the one that grants
        // the current (Thavnair's "What's in a Parent" precedes "Curing What Ails").
        var byId = new Dictionary<uint, QuestRecord>(blue.Count);
        foreach (var quest in blue)
        {
            byId[quest.RowId] = quest;
        }

        var links = new Dictionary<uint, List<uint>>();
        foreach (var quest in blue)
        {
            foreach (var previousId in quest.PreviousQuests.QuestIds)
            {
                if (previousId != quest.RowId && byId.TryGetValue(previousId, out var previous) && SameStoryPlace(quest, previous))
                {
                    Link(links, quest.RowId, previousId);
                    Link(links, previousId, quest.RowId);
                }
            }
        }

        var admitted = new HashSet<uint>();
        var pending = new Queue<uint>();
        foreach (var quest in blue)
        {
            if (currents.Contains(quest.RowId) && admitted.Add(quest.RowId))
            {
                pending.Enqueue(quest.RowId);
            }
        }

        while (pending.TryDequeue(out var rowId))
        {
            if (!links.TryGetValue(rowId, out var neighbours))
            {
                continue;
            }

            foreach (var neighbour in neighbours)
            {
                if (admitted.Add(neighbour))
                {
                    pending.Enqueue(neighbour);
                }
            }
        }

        foreach (var quest in blue)
        {
            if (admitted.Contains(quest.RowId))
            {
                stories.Add(quest.RowId, quest);
            }
        }
    }

    private static void Link(Dictionary<uint, List<uint>> links, uint from, uint to)
    {
        if (!links.TryGetValue(from, out var list))
        {
            links[from] = list = [];
        }

        list.Add(to);
    }

    /// <summary>A link counts when both quests share a journal genre or their givers stand in the same territory.</summary>
    private static bool SameStoryPlace(QuestRecord a, QuestRecord b) =>
        a.Journal.GenreId == b.Journal.GenreId
        || (a.Issuer is { TerritoryId: not 0 } issuerA && b.Issuer is { } issuerB && issuerA.TerritoryId == issuerB.TerritoryId);

    /// <summary>
    /// The component in play order: a quest after every quest of the component it requires, ties broken by journal
    /// order (the component arrives in journal order). The sheet's previous-quest links form no cycle; were one to
    /// appear, its quests follow in journal order rather than being dropped.
    /// </summary>
    private static uint[] PlayOrder(List<QuestRecord> component, Dictionary<uint, List<uint>> requires)
    {
        var position = new Dictionary<uint, int>(component.Count);
        for (var i = 0; i < component.Count; i++)
        {
            position[component[i].RowId] = i;
        }

        var pending = new int[component.Count];
        var dependents = new List<int>[component.Count];
        for (var i = 0; i < component.Count; i++)
        {
            if (!requires.TryGetValue(component[i].RowId, out var previous))
            {
                continue;
            }

            foreach (var previousId in previous)
            {
                var from = position[previousId];
                (dependents[from] ??= []).Add(i);
                pending[i]++;
            }
        }

        var ready = new PriorityQueue<int, int>();
        for (var i = 0; i < component.Count; i++)
        {
            if (pending[i] == 0)
            {
                ready.Enqueue(i, i);
            }
        }

        var result = new List<uint>(component.Count);
        var placed = new bool[component.Count];
        while (result.Count < component.Count)
        {
            if (!ready.TryDequeue(out var next, out _))
            {
                // A cycle: release the earliest quest still waiting.
                next = Array.FindIndex(placed, p => !p);
            }

            if (placed[next])
            {
                continue;
            }

            placed[next] = true;
            result.Add(component[next].RowId);
            if (dependents[next] is not { } after)
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

        return result.ToArray();
    }

    private static uint Find(Dictionary<uint, uint> parent, uint rowId)
    {
        while (parent[rowId] != rowId)
        {
            var grandparent = parent[parent[rowId]];
            parent[rowId] = grandparent;
            rowId = grandparent;
        }

        return rowId;
    }
}
