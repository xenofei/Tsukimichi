using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace Tsukimichi.Ui;

/// <summary>
/// What the "Why it stopped" card (feature plan v7, 1.18.0, A2) asks of the game besides the existing links: the quest
/// the last Walk or Go to giver headed for (for Teleport closer), a map flag where the character stood (Flag the spot)
/// and the map coordinates of a place for Copy report. Read-only UI calls, under the addon kill switch.
/// </summary>
public sealed partial class GameLinks
{
    /// <summary>The quest whose giver the last Walk or Go to giver Tsukimichi started headed for; 0 before the first.</summary>
    public uint JourneyQuestRowId { get; private set; }

    /// <summary>The map coordinates of a world position in a territory ("18.2, 24.7"), or null without a map.</summary>
    public Vector2? MapCoordinatesAt(uint territoryId, Vector3 position)
    {
        if (MapOfTerritory(territoryId) is not (> 0 and var mapId) || Map(mapId) is not { } map)
        {
            return null;
        }

        return new Vector2(ToMapCoordinate(position.X, map.OffsetX, map.SizeFactor), ToMapCoordinate(position.Z, map.OffsetY, map.SizeFactor));
    }

    /// <summary><see cref="MapCoordinatesAt"/> as text with one decimal, invariant ("18.2, 24.7"); empty without a map.</summary>
    public string MapCoordinateText(uint territoryId, Vector3 position) =>
        MapCoordinatesAt(territoryId, position) is { } at
            ? string.Format(CultureInfo.InvariantCulture, "{0:0.0}, {1:0.0}", at.X, at.Y)
            : string.Empty;

    /// <summary>Whether <see cref="FlagSpot"/> can open the map on the territory.</summary>
    public bool CanFlagSpot(uint territoryId) => CallsAllowed && MapOfTerritory(territoryId) != 0;

    /// <summary>
    /// Opens the game's map with a flag at <paramref name="position"/> in <paramref name="territoryId"/> (Flag the spot:
    /// where travel got stuck). False when the territory has no map, game calls are paused or the call failed.
    /// </summary>
    public bool FlagSpot(uint territoryId, Vector3 position)
    {
        if (!CallsAllowed || MapOfTerritory(territoryId) is not (> 0 and var mapId) || MapCoordinatesAt(territoryId, position) is not { } at)
        {
            return false;
        }

        try
        {
            return gameGui.OpenMapWithMapLink(new MapLinkPayload(territoryId, mapId, at.X, at.Y));
        }
        catch (Exception ex)
        {
            WarnUnlockCall(ex, "Flag the spot in territory {0} failed", territoryId);
            return false;
        }
    }
}
