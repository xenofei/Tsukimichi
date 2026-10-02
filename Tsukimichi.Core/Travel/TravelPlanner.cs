namespace Tsukimichi.Core.Travel;

/// <summary>
/// A place travel starts from or lands at: an aetheryte (a teleport destination) or an aethernet shard, at raw world
/// (x, z) in its territory. <paramref name="Group"/> is the Aetheryte sheet's <c>AethernetGroup</c>: the city network
/// the node belongs to, 0 for a field aetheryte with no aethernet.
/// </summary>
public readonly record struct TravelNode(uint RowId, uint TerritoryId, float X, float Z, uint Group = 0);

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
    public static TravelNode? NearestAttuned(IReadOnlyList<TravelNode> inTerritory, TravelNode? fallback, float x, float z, Func<uint, bool> isAttuned)
    {
        ArgumentNullException.ThrowIfNull(inTerritory);
        ArgumentNullException.ThrowIfNull(isAttuned);

        TravelNode? best = null;
        var bestDistance = float.MaxValue;
        foreach (var candidate in inTerritory)
        {
            if (!isAttuned(candidate.RowId))
            {
                continue;
            }

            var distance = Distance(candidate.X, candidate.Z, x, z);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        if (best is not null)
        {
            return best;
        }

        return fallback is { } f && isAttuned(f.RowId) ? f : null;
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
}
