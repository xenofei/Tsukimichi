using Tsukimichi.Core.Model;
using Tsukimichi.Core.Travel;

namespace Tsukimichi.GameData;

/// <summary>
/// Where travel toward a quest giver aims and which aetheryte it lands at, from the sheets' aetherytes
/// (<see cref="AetheryteIndex"/>) and the interiors' doors (<see cref="EntranceIndex"/>). Teleport, Go to giver, the
/// aethernet hop and the stop lists all ask here, so they agree; tests run it for every giver.
/// </summary>
public static class GiverTravel
{
    /// <summary>The giver's place as a travel place.</summary>
    public static TravelPlace Place(Issuer issuer)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        return new TravelPlace(issuer.TerritoryId, issuer.X, issuer.Y, issuer.Z);
    }

    /// <summary>
    /// The goal for a giver: the giver, or the door of the interior it stands in while the player
    /// (<paramref name="playerTerritory"/>, 0 for "anywhere else") is not inside.
    /// </summary>
    public static TravelGoal Goal(Issuer issuer, EntranceIndex entrances, uint playerTerritory)
    {
        ArgumentNullException.ThrowIfNull(entrances);
        return Goal(issuer, entrances.For(issuer.TerritoryId), playerTerritory);
    }

    /// <summary>
    /// The goal for a giver, given the way into its territory as looked up already (<see cref="EntranceIndex.Peek"/>);
    /// a null <paramref name="entrance"/> aims at the giver itself, whose zone's aetheryte Teleport then picks.
    /// </summary>
    public static TravelGoal Goal(Issuer issuer, InteriorEntrance? entrance, uint playerTerritory) =>
        TravelPlanner.Goal(Place(issuer), entrance, playerTerritory);

    /// <summary>
    /// The aetheryte the game links to the goal's zone (a TerritoryType row's): the giver's zone's when the goal is the
    /// giver; for an interior's door, the interior's own when it stands in the door's territory, else the outside
    /// territory's.
    /// </summary>
    public static AetheryteInfo? Preferred(AetheryteIndex index, uint giverTerritory, TravelGoal goal)
    {
        ArgumentNullException.ThrowIfNull(index);
        var own = index.TerritoryDefault(giverTerritory);
        if (!goal.AtEntrance)
        {
            return own;
        }

        return own is { } aetheryte && aetheryte.TerritoryId == goal.Place.TerritoryId ? aetheryte : index.TerritoryDefault(goal.Place.TerritoryId) ?? own;
    }

    /// <summary>
    /// Where a teleport toward <paramref name="goal"/> lands: the aetheryte nearest the goal (or, for a door the data
    /// does not place, the one the zone names) and the nearest attuned one.
    /// </summary>
    public static ArrivalChoice Arrival(AetheryteIndex index, uint giverTerritory, TravelGoal goal, Func<uint, bool> isAttuned)
    {
        ArgumentNullException.ThrowIfNull(index);
        var preferred = Preferred(index, giverTerritory, goal);
        float? x = goal.Placed ? goal.Place.X : null;
        float? z = goal.Placed ? goal.Place.Z : null;
        return TravelPlanner.ChooseArrival(index.NodesInTerritory(goal.Place.TerritoryId), preferred?.Node, x, z, isAttuned);
    }
}
