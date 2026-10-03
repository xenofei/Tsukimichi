using System.Collections.Frozen;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Unlocks;

/// <summary>
/// The areas and aetherytes a quest opens that no sheet column states (feature plan v6 K1, unlocks spec §2.2): the game
/// has no "zone unlocked" field, so the zones and aetherytes are inferred from where each quest's objectives lead.
/// <list type="number">
/// <item>A quest's <b>touched zones</b> are the town and field zones its issuer and objectives stand in
/// (<see cref="UnlockLinks.Touches"/>), and its <b>objective points</b> the same places with their coordinates.</item>
/// <item>A main-scenario quest (<see cref="MainScenarioIconType"/>) <b>opens</b> a zone it touches when none of its
/// main-scenario ancestors (the closure of <see cref="QuestCatalog.PrerequisitesOf"/>, main scenario only) touched it.
/// Per ancestor set, so each start city and Grand Company gets its own opener, and The Lominsan Envoy never reopens
/// Limsa Lominsa.</item>
/// <item>It opens a teleportable aetheryte when one of its objective points lies within <see cref="DefaultRadius"/>
/// yalms of it in the same zone and no main-scenario ancestor's did.</item>
/// <item>A quest-gated warp (<see cref="UnlockLinks.Warps"/>, any quest) opens its destination unless an ancestor (any
/// quest this time) already touched or warped there, or the quest's giver already stands in it: a way back ("to Old
/// Gridania" after the Lavender Beds), or a door inside the giver's own town (the Rogues' Guild), is no unlock. A quest
/// without any prerequisite (a level or rank gate only) has no ancestors to ask: a destination the main scenario first
/// reaches below its level counts as reached.</item>
/// </list>
/// Feature quests are left out of rules 2 and 3: their objectives roam everywhere and would claim every zone. Pure and
/// allocation-light: the zone and aetheryte sets are bit sets, memoized per quest.
/// </summary>
public static class UnlockAreas
{
    /// <summary>How near an objective must be to an aetheryte for the quest to open it, in yalms.</summary>
    public const float DefaultRadius = 40f;

    /// <summary><c>Quest.EventIconType</c> of a main scenario quest.</summary>
    public const byte MainScenarioIconType = 3;

    /// <summary>What <see cref="Derive"/> found, by quest row id; each array in the order the links listed them.</summary>
    /// <param name="Zones">Zones (TerritoryType row ids) a main-scenario quest first reaches.</param>
    /// <param name="Aetherytes">Aetherytes (Aetheryte row ids) a main-scenario quest first stands beside.</param>
    /// <param name="Warps">Warp destinations (TerritoryType row ids) a quest opens that no ancestor reached before.</param>
    public sealed record Result(
        IReadOnlyDictionary<uint, uint[]> Zones,
        IReadOnlyDictionary<uint, uint[]> Aetherytes,
        IReadOnlyDictionary<uint, uint[]> Warps)
    {
        public static readonly Result Empty = new(
            FrozenDictionary<uint, uint[]>.Empty,
            FrozenDictionary<uint, uint[]>.Empty,
            FrozenDictionary<uint, uint[]>.Empty);
    }

    /// <summary>Whether the quest counts for the first-visit rule: a main scenario quest still in the game.</summary>
    public static bool IsMainScenario(QuestRecord quest) => quest.EventIconType == MainScenarioIconType && !quest.IsRemoved;

    /// <summary>Applies the rules of the class summary to the catalog.</summary>
    public static Result Derive(QuestCatalog catalog, UnlockLinks links, float radius = DefaultRadius)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(links);
        if (catalog.Count == 0 || (links.Touches.Count == 0 && links.Warps.Count == 0))
        {
            return Result.Empty;
        }

        // Indexes for the bit sets: every zone the links name, and every teleportable aetheryte.
        var zoneIndex = new Dictionary<uint, int>();
        var zoneIds = new List<uint>();
        void IndexZone(uint territoryId)
        {
            if (territoryId != 0 && zoneIndex.TryAdd(territoryId, zoneIds.Count))
            {
                zoneIds.Add(territoryId);
            }
        }

        foreach (var zone in links.Zones)
        {
            IndexZone(zone.TerritoryId);
        }

        foreach (var touch in links.Touches)
        {
            IndexZone(touch.TerritoryId);
        }

        foreach (var warp in links.Warps)
        {
            IndexZone(warp.TerritoryId);
        }

        var aetherytes = new List<UnlockAetheryte>();
        var byTerritory = new Dictionary<uint, List<int>>();
        foreach (var aetheryte in links.Aetherytes)
        {
            if (!aetheryte.IsAetheryte || aetheryte.TerritoryId == 0)
            {
                continue;
            }

            if (!byTerritory.TryGetValue(aetheryte.TerritoryId, out var list))
            {
                list = [];
                byTerritory[aetheryte.TerritoryId] = list;
            }

            list.Add(aetherytes.Count);
            aetherytes.Add(aetheryte);
        }

        var zoneWords = Bits.Words(zoneIds.Count);
        var aetheryteWords = Bits.Words(aetherytes.Count);
        var limit = radius * radius;

        // Each quest's own touched zones and nearby aetherytes.
        var touched = new Dictionary<uint, ulong[]>();
        var near = new Dictionary<uint, ulong[]>();
        foreach (var touch in links.Touches)
        {
            if (!touched.TryGetValue(touch.QuestRowId, out var zones))
            {
                zones = new ulong[zoneWords];
                touched[touch.QuestRowId] = zones;
            }

            Bits.Set(zones, zoneIndex[touch.TerritoryId]);
            if (!byTerritory.TryGetValue(touch.TerritoryId, out var candidates))
            {
                continue;
            }

            foreach (var i in candidates)
            {
                var dx = aetherytes[i].X - touch.X;
                var dz = aetherytes[i].Z - touch.Z;
                if ((dx * dx) + (dz * dz) > limit)
                {
                    continue;
                }

                if (!near.TryGetValue(touch.QuestRowId, out var set))
                {
                    set = new ulong[aetheryteWords];
                    near[touch.QuestRowId] = set;
                }

                Bits.Set(set, i);
            }
        }

        // Each quest's warp destinations, as zone bits, for the ancestors' "already reached".
        var warpsOf = new Dictionary<uint, List<uint>>();
        var warpBits = new Dictionary<uint, ulong[]>();
        foreach (var warp in links.Warps)
        {
            if (warp.TerritoryId == 0 || !catalog.ByRowId.ContainsKey(warp.QuestRowId))
            {
                continue;
            }

            if (!warpsOf.TryGetValue(warp.QuestRowId, out var list))
            {
                list = [];
                warpsOf[warp.QuestRowId] = list;
                warpBits[warp.QuestRowId] = new ulong[zoneWords];
            }

            if (!list.Contains(warp.TerritoryId))
            {
                list.Add(warp.TerritoryId);
            }

            Bits.Set(warpBits[warp.QuestRowId], zoneIndex[warp.TerritoryId]);
        }

        var zonesOut = new Dictionary<uint, uint[]>();
        var aetherytesOut = new Dictionary<uint, uint[]>();

        // Rules 2 and 3: the main scenario's first visits.
        var msqZones = new Ancestry(catalog, zoneWords, mainScenarioOnly: true, id => touched.GetValueOrDefault(id));
        var msqNear = new Ancestry(catalog, aetheryteWords, mainScenarioOnly: true, id => near.GetValueOrDefault(id));
        foreach (var quest in catalog.All)
        {
            if (!IsMainScenario(quest))
            {
                continue;
            }

            if (touched.TryGetValue(quest.RowId, out var own))
            {
                var seen = msqZones.Seen(quest.RowId);
                var opened = new List<uint>();
                for (var i = 0; i < zoneIds.Count; i++)
                {
                    if (Bits.Has(own, i) && !Bits.Has(seen, i))
                    {
                        opened.Add(zoneIds[i]);
                    }
                }

                if (opened.Count > 0)
                {
                    zonesOut[quest.RowId] = [.. opened];
                }
            }

            if (near.TryGetValue(quest.RowId, out var beside))
            {
                var seen = msqNear.Seen(quest.RowId);
                var opened = new List<uint>();
                for (var i = 0; i < aetherytes.Count; i++)
                {
                    if (Bits.Has(beside, i) && !Bits.Has(seen, i))
                    {
                        opened.Add(aetherytes[i].AetheryteId);
                    }
                }

                if (opened.Count > 0)
                {
                    aetherytesOut[quest.RowId] = [.. opened];
                }
            }
        }

        // Rule 4: warps, over every quest's ancestors (touched or warped to).
        var reachedOwn = new Dictionary<uint, ulong[]>(touched);
        foreach (var (rowId, bits) in warpBits)
        {
            if (reachedOwn.TryGetValue(rowId, out var zones))
            {
                var both = (ulong[])zones.Clone();
                Bits.Or(both, bits);
                reachedOwn[rowId] = both;
            }
            else
            {
                reachedOwn[rowId] = bits;
            }
        }

        // The lowest level at which the main scenario first reaches each zone, for quests without any prerequisite.
        var firstLevel = new Dictionary<uint, byte>();
        foreach (var (rowId, territories) in zonesOut)
        {
            var level = catalog.ByRowId[rowId].DisplayLevel;
            foreach (var territory in territories)
            {
                if (!firstLevel.TryGetValue(territory, out var known) || level < known)
                {
                    firstLevel[territory] = level;
                }
            }
        }

        var reached = new Ancestry(catalog, zoneWords, mainScenarioOnly: false, id => reachedOwn.GetValueOrDefault(id));
        var warpsOut = new Dictionary<uint, uint[]>();
        foreach (var (rowId, destinations) in warpsOf)
        {
            var quest = catalog.ByRowId[rowId];
            var seen = reached.Seen(rowId);
            var orphan = catalog.PrerequisitesOf(quest).IsEmpty;
            var giverZone = quest.Issuer?.TerritoryId ?? 0;
            var opened = new List<uint>();
            foreach (var destination in destinations)
            {
                // A quest without prerequisites (a level or rank gate only) has no ancestors to ask: a zone the main
                // scenario reaches below its level is one every character taking it has seen (the way home from a
                // housing district).
                var seenBelow = orphan && firstLevel.TryGetValue(destination, out var level) && level < quest.DisplayLevel;
                if (destination != giverZone && !seenBelow && !Bits.Has(seen, zoneIndex[destination]))
                {
                    opened.Add(destination);
                }
            }

            if (opened.Count > 0)
            {
                warpsOut[rowId] = [.. opened];
            }
        }

        return new Result(zonesOut.ToFrozenDictionary(), aetherytesOut.ToFrozenDictionary(), warpsOut.ToFrozenDictionary());
    }

    /// <summary>
    /// What a quest's ancestors reached, memoized per quest: the union, over its prerequisites (main scenario ones only
    /// when asked), of each prerequisite's own set and its ancestors'. Walked without recursion; a cycle in the data
    /// reads as no further ancestors.
    /// </summary>
    private sealed class Ancestry(QuestCatalog catalog, int words, bool mainScenarioOnly, Func<uint, ulong[]?> own)
    {
        private readonly Dictionary<uint, ulong[]> memo = [];
        private readonly HashSet<uint> visiting = [];
        private readonly Stack<(uint Id, bool Expanded)> stack = new();

        public ulong[] Seen(uint rowId)
        {
            if (memo.TryGetValue(rowId, out var known))
            {
                return known;
            }

            stack.Push((rowId, false));
            while (stack.Count > 0)
            {
                var (id, expanded) = stack.Pop();
                if (expanded)
                {
                    var bits = new ulong[words];
                    foreach (var parent in Parents(id))
                    {
                        if (memo.TryGetValue(parent, out var above))
                        {
                            Bits.Or(bits, above);
                        }

                        if (own(parent) is { } mine)
                        {
                            Bits.Or(bits, mine);
                        }
                    }

                    memo[id] = bits;
                    visiting.Remove(id);
                    continue;
                }

                if (memo.ContainsKey(id) || !visiting.Add(id))
                {
                    continue;
                }

                stack.Push((id, true));
                foreach (var parent in Parents(id))
                {
                    if (!memo.ContainsKey(parent) && !visiting.Contains(parent))
                    {
                        stack.Push((parent, false));
                    }
                }
            }

            return memo[rowId];
        }

        private IEnumerable<uint> Parents(uint rowId)
        {
            if (!catalog.TryGetByRowId(rowId, out var quest))
            {
                yield break;
            }

            foreach (var parent in catalog.PrerequisitesOf(quest).QuestIds)
            {
                if (parent == rowId || !catalog.TryGetByRowId(parent, out var prerequisite))
                {
                    continue;
                }

                if (!mainScenarioOnly || IsMainScenario(prerequisite))
                {
                    yield return parent;
                }
            }
        }
    }

    /// <summary>Bit sets over ulong words.</summary>
    private static class Bits
    {
        public static int Words(int count) => Math.Max(1, (count + 63) / 64);

        public static void Set(ulong[] bits, int index) => bits[index >> 6] |= 1UL << (index & 63);

        public static bool Has(ulong[] bits, int index) => (bits[index >> 6] & (1UL << (index & 63))) != 0;

        public static void Or(ulong[] into, ulong[] from)
        {
            for (var i = 0; i < into.Length && i < from.Length; i++)
            {
                into[i] |= from[i];
            }
        }
    }
}
