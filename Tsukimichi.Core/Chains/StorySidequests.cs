using System.Collections.Frozen;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Chains;

/// <summary>
/// Story sidequests: the sidequests that carry journal artwork, the tell players use for "part of a small story",
/// and the per-zone side stories they form. A story sidequest sits in the Sidequests section of the journal
/// (<see cref="SidequestSectionId"/>: not the main scenario, not Chronicles, allied societies, class quests or
/// seasonal events), has a banner (<see cref="QuestRecord.Icon"/>), and is not an unlock quest (the feature set),
/// not repeatable and not removed.
/// <para>
/// A side story is a maximal connected set of at least <see cref="ChainCatalog.MinChainLength"/> story sidequests
/// linked by previous-quest requirements, where each link joins two quests of the same journal genre or given in the
/// same territory. The typical Shadowbringers-to-Dawntrail zone has two short lines joined by a last quest that
/// requires both; that is one story. Its quests are in play order (a quest after everything it requires, ties in
/// journal order) and it is named after its first quest, "Story: &lt;name&gt;".
/// </para>
/// <para>
/// A heuristic, and it says so: a one-quest vignette with art is a story sidequest outside any chain; a line whose
/// later quests have no art, or whose opening quest is an unlock (the aether current quests that open most zone
/// stories since Shadowbringers), keeps only its art-bearing, non-unlock part.
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

    /// <summary>Whether a quest is a story sidequest: the section, artwork, unlock, repeatable and removed rules of the class summary.</summary>
    /// <param name="featureQuestIds">The derived unlock quests (<c>FeaturePresets.Derive</c>).</param>
    public static bool IsStorySidequest(QuestRecord quest, IReadOnlySet<uint> featureQuestIds)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(featureQuestIds);
        return !quest.IsRemoved
            && !quest.IsRepeatable
            && quest.Icon != 0
            && quest.Journal.SectionId == SidequestSectionId
            && !featureQuestIds.Contains(quest.RowId);
    }

    /// <summary>Finds every story sidequest and derives the side stories. Computed once per catalog; the result is immutable.</summary>
    /// <param name="featureQuestIds">The derived unlock quests (<c>FeaturePresets.Derive</c>); they are never story sidequests.</param>
    public static StorySidequests Build(QuestCatalog catalog, IReadOnlySet<uint> featureQuestIds)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(featureQuestIds);

        var stories = new Dictionary<uint, QuestRecord>();
        foreach (var quest in catalog.All)
        {
            if (IsStorySidequest(quest, featureQuestIds))
            {
                stories.Add(quest.RowId, quest);
            }
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
            if (component.Count < ChainCatalog.MinChainLength)
            {
                order[component[0].RowId] = order.Count;
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
