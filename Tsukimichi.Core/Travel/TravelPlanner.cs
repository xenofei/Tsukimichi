namespace Tsukimichi.Core.Travel;

/// <summary>
/// A place travel starts from or lands at: an aetheryte (a teleport destination) or an aethernet shard, at raw world
/// (x, z) in its territory. <paramref name="Group"/> is the Aetheryte sheet's <c>AethernetGroup</c>: the city network
/// the node belongs to, 0 for a field aetheryte with no aethernet.
/// </summary>
public readonly record struct TravelNode(uint RowId, uint TerritoryId, float X, float Z, uint Group = 0);

/// <summary>A place in raw world units: a territory and a position in it (<paramref name="Y"/> the height).</summary>
public readonly record struct TravelPlace(uint TerritoryId, float X, float Y, float Z);

/// <summary>
/// The way into an interior: a building, a private room or a story area that holds no aetheryte and no aethernet shard
/// (the Waking Sands, Fortemps Manor, Zero's Domain). <paramref name="Interior"/> is the territory the giver stands
/// in; <paramref name="TerritoryId"/> the outside territory its door (or the NPC who takes you in) stands in. When
/// <paramref name="Placed"/> the game data places the door at (<paramref name="X"/>, <paramref name="Y"/>,
/// <paramref name="Z"/>); otherwise only the outside territory is known and the position means nothing.
/// </summary>
public readonly record struct InteriorEntrance(uint Interior, uint TerritoryId, float X, float Y, float Z, bool Placed);

/// <summary>
/// Where travel aims for one giver: the giver itself, or, while the player is outside an interior the giver stands
/// in, that interior's door (<see cref="Entrance"/> set). <see cref="Placed"/> is false when only the outside territory
/// of the door is known: a teleport still gets close, a walk cannot.
/// </summary>
public readonly record struct TravelGoal(TravelPlace Place, bool Placed, InteriorEntrance? Entrance)
{
    /// <summary>True when the goal is an interior's door rather than the giver.</summary>
    public bool AtEntrance => Entrance is not null;
}

/// <summary>
/// Which aetheryte a teleport toward a goal lands at: <paramref name="Nearest"/> is the one nearest the goal, attuned
/// or not; <paramref name="Target"/> the attuned one Teleport uses (null when none is). They differ when the nearest is
/// not attuned (<see cref="Substituted"/>), which the tooltip says.
/// </summary>
public readonly record struct ArrivalChoice(TravelNode? Nearest, TravelNode? Target)
{
    /// <summary>True when the nearest aetheryte is not attuned and Teleport lands at another one.</summary>
    public bool Substituted => Nearest is { } nearest && Target is { } target && nearest.RowId != target.RowId;
}

/// <summary>
/// The choices behind Teleport, "Already here" and the aethernet hop (feature plan v5, 1.6.0): which attuned aetheryte
/// lands closest to a quest giver, whether the player already stands closer than that, and which aethernet shard of
/// the arrival city is worth one more hop. Pure, so tests decide every case without the game.
/// </summary>
public static class TravelPlanner
{
    /// <summary>
    /// How much closer (raw world units, about yalms) a shard must stand to the giver than the city aetheryte before a
    /// hop is offered: a shard a few steps nearer is not worth a loading screen.
    /// </summary>
    public const float HopMargin = 30f;

    /// <summary>Straight-line distance on the ground plane between two raw (x, z) positions.</summary>
    public static float Distance(float ax, float az, float bx, float bz)
    {
        var dx = ax - bx;
        var dz = az - bz;
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }

    /// <summary>
    /// The attuned aetheryte nearest to (<paramref name="x"/>, <paramref name="z"/>) among those standing in the
    /// giver's territory; without one, <paramref name="fallback"/> (the aetheryte the zone's TerritoryType row names,
    /// for a city sub-zone without its own) when it is attuned; null when none of them is.
    /// </summary>
    public static TravelNode? NearestAttuned(IReadOnlyList<TravelNode> inTerritory, TravelNode? fallback, float x, float z, Func<uint, bool> isAttuned) =>
        ChooseArrival(inTerritory, fallback, x, z, isAttuned).Target;

    /// <summary>
    /// Distances (raw units) closer than this count as a tie, which the aetheryte the game itself links to the zone
    /// (its TerritoryType row) wins.
    /// </summary>
    public const float TieMargin = 5f;

    /// <summary>
    /// Where a teleport toward a goal lands. <paramref name="inTerritory"/> are the aetherytes standing in the goal's
    /// territory; <paramref name="preferred"/> is the aetheryte the game links to the zone (its TerritoryType row): it
    /// wins a tie (<see cref="TieMargin"/>), it is the reference point when the goal's position is unknown
    /// (<paramref name="x"/> or <paramref name="z"/> null: an interior whose door the data does not place), and it is
    /// the fallback when the territory holds no aetheryte of its own (a city sub-zone) or none there is attuned.
    /// <see cref="ArrivalChoice.Nearest"/> ignores attunement; <see cref="ArrivalChoice.Target"/> is attuned or null.
    /// </summary>
    public static ArrivalChoice ChooseArrival(IReadOnlyList<TravelNode> inTerritory, TravelNode? preferred, float? x, float? z, Func<uint, bool> isAttuned)
    {
        ArgumentNullException.ThrowIfNull(inTerritory);
        ArgumentNullException.ThrowIfNull(isAttuned);

        // Without a position, measure from the preferred aetheryte when it stands among the candidates.
        var refX = x;
        var refZ = z;
        if ((refX is null || refZ is null) && preferred is { } p && Contains(inTerritory, p.RowId))
        {
            refX = p.X;
            refZ = p.Z;
        }

        var nearest = Pick(inTerritory, preferred, refX, refZ, static _ => true) ?? preferred;
        var target = Pick(inTerritory, preferred, refX, refZ, isAttuned);
        if (target is null && preferred is { } fallback && isAttuned(fallback.RowId))
        {
            target = fallback;
        }

        return new ArrivalChoice(nearest, target);
    }

    private static bool Contains(IReadOnlyList<TravelNode> nodes, uint rowId)
    {
        foreach (var node in nodes)
        {
            if (node.RowId == rowId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The candidate nearest the reference point that passes <paramref name="allowed"/>, the preferred one winning a
    /// tie; without a reference point, the preferred one when allowed and among them, else the first allowed.
    /// </summary>
    private static TravelNode? Pick(IReadOnlyList<TravelNode> candidates, TravelNode? preferred, float? x, float? z, Func<uint, bool> allowed)
    {
        TravelNode? best = null;
        var bestDistance = float.MaxValue;
        var bestIsPreferred = false;
        foreach (var candidate in candidates)
        {
            if (!allowed(candidate.RowId))
            {
                continue;
            }

            var isPreferred = preferred is { } pref && candidate.RowId == pref.RowId;
            if (x is not { } px || z is not { } pz)
            {
                if (isPreferred)
                {
                    return candidate;
                }

                best ??= candidate;
                continue;
            }

            var distance = Distance(candidate.X, candidate.Z, px, pz);
            var better = isPreferred
                ? distance < bestDistance + TieMargin
                : bestIsPreferred ? distance + TieMargin <= bestDistance : distance < bestDistance;
            if (better)
            {
                bestDistance = distance;
                best = candidate;
                bestIsPreferred = isPreferred;
            }
        }

        return best;
    }

    /// <summary>
    /// Where travel aims for a giver at <paramref name="giver"/>: the giver itself, or, when the giver stands inside an
    /// interior (<paramref name="entrance"/> for the giver's territory) and the player is not inside it already, the
    /// interior's door outside.
    /// </summary>
    public static TravelGoal Goal(TravelPlace giver, InteriorEntrance? entrance, uint playerTerritory)
    {
        if (entrance is { } door && door.Interior == giver.TerritoryId && door.TerritoryId != 0 && playerTerritory != giver.TerritoryId)
        {
            return new TravelGoal(new TravelPlace(door.TerritoryId, door.X, door.Y, door.Z), door.Placed, door);
        }

        return new TravelGoal(giver, true, null);
    }

    /// <summary>
    /// True when the player already stands in the giver's territory and a teleport would not bring them closer: the
    /// arrival aetheryte is in another territory (a city sub-zone's), there is none, or the player is nearer the giver
    /// than <paramref name="arrival"/> is. False in any other territory.
    /// </summary>
    public static bool IsAlreadyHere(uint playerTerritory, float playerX, float playerZ, uint giverTerritory, float giverX, float giverZ, TravelNode? arrival)
    {
        if (playerTerritory == 0 || playerTerritory != giverTerritory)
        {
            return false;
        }

        if (arrival is not { } a || a.TerritoryId != giverTerritory)
        {
            return true;
        }

        return Distance(playerX, playerZ, giverX, giverZ) < Distance(a.X, a.Z, giverX, giverZ);
    }

    /// <summary>
    /// The aethernet shard of <paramref name="arrival"/>'s city that is worth a hop toward the giver: attuned, in the
    /// giver's territory, the nearest such shard, and closer to the giver than the arrival aetheryte by more than
    /// <paramref name="margin"/>. When the arrival aetheryte stands in another territory (Old Gridania's giver,
    /// New Gridania's aetheryte), any shard in the giver's territory beats it. Null for a field aetheryte (group 0) or
    /// when no shard is better.
    /// </summary>
    public static TravelNode? ChooseShard(TravelNode arrival, IReadOnlyList<TravelNode> shards, uint giverTerritory, float giverX, float giverZ, Func<uint, bool> isAttuned, float margin = HopMargin)
    {
        ArgumentNullException.ThrowIfNull(shards);
        ArgumentNullException.ThrowIfNull(isAttuned);
        if (arrival.Group == 0)
        {
            return null;
        }

        var baseline = arrival.TerritoryId == giverTerritory
            ? Distance(arrival.X, arrival.Z, giverX, giverZ)
            : float.PositiveInfinity;

        TravelNode? best = null;
        var bestDistance = float.MaxValue;
        foreach (var shard in shards)
        {
            if (shard.Group != arrival.Group || shard.RowId == arrival.RowId || shard.TerritoryId != giverTerritory || !isAttuned(shard.RowId))
            {
                continue;
            }

            var distance = Distance(shard.X, shard.Z, giverX, giverZ);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = shard;
            }
        }

        return best is not null && bestDistance + margin < baseline ? best : null;
    }

    /// <summary>
    /// The node nearest (<paramref name="x"/>, <paramref name="z"/>) among <paramref name="nodes"/> standing in
    /// <paramref name="territory"/> and passing <paramref name="allowed"/>: where a walk to the aetheryte before an
    /// aethernet hop heads (feature plan v7 A8). Null when none does.
    /// </summary>
    public static TravelNode? NearestNode(IEnumerable<TravelNode> nodes, uint territory, float x, float z, Func<uint, bool> allowed)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(allowed);
        TravelNode? best = null;
        var bestDistance = float.MaxValue;
        foreach (var node in nodes)
        {
            if (node.TerritoryId != territory || !allowed(node.RowId))
            {
                continue;
            }

            var distance = Distance(node.X, node.Z, x, z);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = node;
            }
        }

        return best;
    }

    /// <summary>
    /// True when walking to the network's nearest node and hopping beats walking straight to the goal: the walk to the
    /// node (<paramref name="toNode"/>) plus the shard's distance to the goal (<paramref name="shardToGoal"/>) is shorter
    /// than the player's own distance to the goal (<paramref name="toGoal"/>) by more than <paramref name="margin"/>.
    /// </summary>
    public static bool WalkThenHopPays(float toNode, float shardToGoal, float toGoal, float margin = HopMargin) =>
        toNode + shardToGoal + margin < toGoal;
}
