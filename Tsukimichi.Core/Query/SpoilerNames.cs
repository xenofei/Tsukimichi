using System.Collections.Frozen;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Unlocks;

namespace Tsukimichi.Core.Query;

/// <summary>The kinds of name the wider spoiler shield (plan v7, 1.20.0 N6) places in the story.</summary>
public enum SpoilerKind : byte
{
    /// <summary>A zone, a world-map region, a zone's region, an aetheryte or shard, flying in a zone.</summary>
    Area,

    /// <summary>A dungeon, trial, raid, field operation or any other duty.</summary>
    Duty,

    /// <summary>A quest reward (an item, a mount, an emote, a title) or any other thing a quest opens (a job, a feature, an action).</summary>
    Reward,

    /// <summary>A person: a quest's giver.</summary>
    Npc,
}

/// <summary>
/// Where in the main scenario each zone, duty, reward and NPC name stops being a spoiler (plan v7, 1.20.0 N6): the
/// data the wider spoiler shield reads, built once per catalog and unlock index, so new zones, duties and people of a
/// later patch are placed by the same rule after a data refresh.
/// <para>
/// <b>Story anchor of a quest.</b> A main scenario quest is its own anchor. Any other quest is anchored at the latest
/// main scenario quest it needs: through its previous quests (<see cref="QuestCatalog.PrerequisitesOf"/>, walked to the
/// end; an Any join takes its earliest alternative), and through the zone its giver stands in (the first main scenario
/// quest that opens that zone). A quest nothing places in the story has no anchor.
/// </para>
/// <para>
/// <b>Introducing quests of a name.</b> An area: the quests the unlock index says open it (a zone, a world map, an
/// aetheryte, flying in it), and the region of a zone with it; a zone no quest opens is introduced by the quests given
/// in it. A duty: the quests that open it. A reward: the quests that give it, and those that open it (a job, a feature,
/// an action). A person: the quests they give. Names are matched whatever their case.
/// </para>
/// <para>
/// <b>The rule</b> (<see cref="SpoilerMask.IsNameMasked"/>): a name is masked when every quest that introduces it is
/// anchored and every anchor is a quest the shield masks. A name introduced by any unanchored quest, or by one whose
/// anchor the character has done, has in the journal or is within "names ahead", is shown; so is a name the data does
/// not place (no quest introduces it): the shield never guesses. Because a completed quest's prerequisites are done,
/// a name the character has met through its quests is never masked.
/// </para>
/// Immutable; lookups allocate nothing.
/// </summary>
public sealed class SpoilerNames
{
    /// <summary>Places nothing: no catalog or unlock index yet.</summary>
    public static readonly SpoilerNames Empty = new(
        [FrozenDictionary<string, Placed>.Empty, FrozenDictionary<string, Placed>.Empty, FrozenDictionary<string, Placed>.Empty, FrozenDictionary<string, Placed>.Empty],
        FrozenDictionary<uint, uint>.Empty);

    private readonly FrozenDictionary<string, Placed>[] byKind;
    private readonly FrozenDictionary<uint, uint> anchors;

    private SpoilerNames(FrozenDictionary<string, Placed>[] byKind, FrozenDictionary<uint, uint> anchors)
    {
        this.byKind = byKind;
        this.anchors = anchors;
        foreach (var names in byKind)
        {
            Count += names.Count;
        }
    }

    /// <summary>
    /// Where one name sits in the story: the anchors of its introducing quests (distinct, earliest first) and the
    /// display level of the earliest, which its placeholder prints.
    /// </summary>
    public readonly record struct Placed(uint[] Anchors, byte Level);

    /// <summary>How many names are placed in the story, every kind together.</summary>
    public int Count { get; }

    /// <summary>Where a name sits; false for a name no anchored quest alone introduces (it is always shown).</summary>
    public bool TryGet(SpoilerKind kind, string name, out Placed placed)
    {
        placed = default;
        return !string.IsNullOrEmpty(name) && (uint)kind < (uint)byKind.Length && byKind[(int)kind].TryGetValue(name, out placed);
    }

    /// <summary>The quest's story anchor (a main scenario quest's row id); 0 when nothing places it.</summary>
    public uint AnchorOf(uint rowId) => anchors.GetValueOrDefault(rowId);

    /// <summary>The kind an unlock row's name is placed under; null for a next quest (the quest shield names it).</summary>
    public static SpoilerKind? KindOf(UnlockTarget target) => UnlockTargets.GroupOf(target) switch
    {
        UnlockGroup.Area or UnlockGroup.Aetheryte => SpoilerKind.Area,
        UnlockGroup.Duty => SpoilerKind.Duty,
        UnlockGroup.NextQuest => null,
        _ => target == UnlockTarget.Flying ? SpoilerKind.Area : SpoilerKind.Reward,
    };

    /// <summary>
    /// Places every name the catalog and its unlock index introduce.
    /// </summary>
    /// <param name="catalog">The quest catalog.</param>
    /// <param name="unlocks">What every quest opens; <see cref="QuestUnlocks.Empty"/> places only rewards and people.</param>
    /// <param name="zones">Every town and field zone (<see cref="UnlockLinks.Zones"/>), for the zone a giver stands in and a zone's region.</param>
    public static SpoilerNames Build(QuestCatalog catalog, QuestUnlocks unlocks, IReadOnlyList<UnlockZone>? zones = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(unlocks);
        if (catalog.Count == 0)
        {
            return Empty;
        }

        var graph = MsqGraph.For(catalog);
        var storyIndex = new Dictionary<uint, int>(graph.Story.Count);
        for (var i = 0; i < graph.Story.Count; i++)
        {
            storyIndex.TryAdd(graph.Story[i].RowId, i);
        }

        var zoneById = new Dictionary<uint, UnlockZone>();
        foreach (var zone in zones ?? [])
        {
            zoneById.TryAdd(zone.TerritoryId, zone);
        }

        // The first main scenario quest that opens each zone: where a quest given there sits at the earliest.
        var zoneAnchor = new Dictionary<uint, int>();
        foreach (var quest in graph.Story)
        {
            foreach (var entry in unlocks.IncludingRewards(quest.RowId))
            {
                if (entry.Target == UnlockTarget.Zone && entry.TargetId != 0 && !zoneAnchor.ContainsKey(entry.TargetId))
                {
                    zoneAnchor[entry.TargetId] = storyIndex[quest.RowId];
                }
            }
        }

        var anchorIndex = new Anchors(catalog, storyIndex, zoneAnchor);
        var anchorRows = new Dictionary<uint, uint>();
        foreach (var quest in catalog.All)
        {
            var at = anchorIndex.Of(quest);
            if (at >= 0)
            {
                anchorRows[quest.RowId] = graph.Story[at].RowId;
            }
        }

        var builders = new Dictionary<string, Introduced>[4];
        for (var i = 0; i < builders.Length; i++)
        {
            builders[i] = new Dictionary<string, Introduced>(StringComparer.OrdinalIgnoreCase);
        }

        void Add(SpoilerKind kind, string name, QuestRecord quest)
        {
            if (name.Length == 0)
            {
                return;
            }

            var names = builders[(int)kind];
            if (!names.TryGetValue(name, out var introduced))
            {
                names[name] = introduced = new Introduced();
            }

            introduced.Add(anchorIndex.Of(quest));
        }

        var opened = new HashSet<uint>();
        foreach (var quest in catalog.All)
        {
            foreach (var entry in unlocks.IncludingRewards(quest.RowId))
            {
                if (KindOf(entry.Target) is not { } kind)
                {
                    continue;
                }

                Add(kind, entry.Name, quest);
                if (entry.Target == UnlockTarget.Zone)
                {
                    opened.Add(entry.TargetId);
                    if (zoneById.TryGetValue(entry.TargetId, out var zone))
                    {
                        Add(SpoilerKind.Area, zone.Region, quest);
                    }
                }
                else if (entry.Target is UnlockTarget.Aetheryte or UnlockTarget.AethernetShard && entry.Detail.Length > 0)
                {
                    // The aetheryte's caption names its zone.
                    Add(SpoilerKind.Area, entry.Detail, quest);
                }
            }

            foreach (var entry in unlocks.ExtraRewards(quest.RowId))
            {
                Add(SpoilerKind.Reward, entry.Name, quest);
            }

            foreach (var reward in quest.Rewards)
            {
                Add(SpoilerKind.Reward, reward.Name, quest);
            }

            if (quest.Issuer is { } issuer)
            {
                Add(SpoilerKind.Npc, issuer.Name, quest);
            }
        }

        // A zone no quest opens (a small town, an instanced area) is introduced by the quests given in it.
        foreach (var quest in catalog.All)
        {
            if (quest.Issuer is { } issuer && !opened.Contains(issuer.TerritoryId) && zoneById.TryGetValue(issuer.TerritoryId, out var zone))
            {
                Add(SpoilerKind.Area, zone.Name, quest);
            }
        }

        var byKind = new FrozenDictionary<string, Placed>[builders.Length];
        for (var i = 0; i < builders.Length; i++)
        {
            var placed = new Dictionary<string, Placed>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, introduced) in builders[i])
            {
                if (introduced.Unanchored || introduced.Indexes.Count == 0)
                {
                    continue;
                }

                introduced.Indexes.Sort();
                var rows = new uint[introduced.Indexes.Count];
                for (var j = 0; j < rows.Length; j++)
                {
                    rows[j] = graph.Story[introduced.Indexes[j]].RowId;
                }

                placed[name] = new Placed(rows, graph.Story[introduced.Indexes[0]].DisplayLevel);
            }

            byKind[i] = placed.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        }

        return new SpoilerNames(byKind, anchorRows.ToFrozenDictionary());
    }

    /// <summary>The story anchors of one name's introducing quests, as story indexes; unanchored once any quest has none.</summary>
    private sealed class Introduced
    {
        public List<int> Indexes { get; } = new(1);

        public bool Unanchored { get; private set; }

        public void Add(int index)
        {
            if (index < 0)
            {
                Unanchored = true;
            }
            else if (!Unanchored && !Indexes.Contains(index))
            {
                Indexes.Add(index);
            }
        }
    }

    /// <summary>Each quest's story anchor as an index into <see cref="MsqGraph.Story"/>, memoized; -1 for none.</summary>
    private sealed class Anchors(QuestCatalog catalog, Dictionary<uint, int> storyIndex, Dictionary<uint, int> zoneAnchor)
    {
        private const int Visiting = int.MinValue;
        private readonly Dictionary<uint, int> memo = [];

        public int Of(QuestRecord quest)
        {
            if (storyIndex.TryGetValue(quest.RowId, out var own))
            {
                return own;
            }

            if (memo.TryGetValue(quest.RowId, out var known))
            {
                // A cycle in the prerequisites places nothing more.
                return known == Visiting ? -1 : known;
            }

            memo[quest.RowId] = Visiting;
            var at = quest.Issuer is { } issuer && zoneAnchor.TryGetValue(issuer.TerritoryId, out var zone) ? zone : -1;
            var prerequisites = catalog.PrerequisitesOf(quest);
            if (prerequisites.Join == JoinKind.Any)
            {
                // Any one alternative will do: the earliest; the quests needed beside it count in full.
                var earliest = int.MaxValue;
                foreach (var id in prerequisites.QuestIds)
                {
                    var of = OfRow(id, quest.RowId);
                    if (prerequisites.IsRequired(id))
                    {
                        at = Math.Max(at, of);
                    }
                    else
                    {
                        earliest = Math.Min(earliest, of);
                    }
                }

                if (earliest != int.MaxValue)
                {
                    at = Math.Max(at, earliest);
                }
            }
            else
            {
                foreach (var id in prerequisites.QuestIds)
                {
                    at = Math.Max(at, OfRow(id, quest.RowId));
                }
            }

            memo[quest.RowId] = at;
            return at;
        }

        private int OfRow(uint rowId, uint self) =>
            rowId != self && catalog.GetByRowId(rowId) is { } quest ? Of(quest) : -1;
    }
}
