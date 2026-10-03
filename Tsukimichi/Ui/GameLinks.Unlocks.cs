using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// What an unlock row of the detail pane can do in the game (feature plan v6 K2): teleport to an aetheryte through
/// Lifestream (the only teleport path), flag an aetheryte on the map, open the map on a zone, and open the Duty Finder on
/// a duty. Opening the Duty Finder is a read-only UI call that selects the duty and never queues (decision 1); the map
/// and the Duty Finder calls follow the addon kill switch (<see cref="GameCallsAllowed"/>). Every call is wrapped; a
/// failure logs one warning and the pane carries on.
/// </summary>
public sealed partial class GameLinks
{
    private readonly Dictionary<uint, uint> territoryMaps = [];
    private bool unlockCallWarned;

    /// <summary>Whether game UI calls are allowed now (the hook gate); allowed when unset.</summary>
    public Func<bool>? GameCallsAllowed { get; set; }

    private bool CallsAllowed => GameCallsAllowed?.Invoke() ?? true;

    /// <summary>Whether the game confirms the live character is attuned to the aetheryte (never the optimistic default).</summary>
    public bool IsAttunedConfirmed(uint aetheryteId) => Travel?.IsAttunedConfirmed(aetheryteId) == true;

    /// <summary>
    /// Whether Teleport to the aetheryte can start now: Lifestream loaded, set up and idle, and the game confirms the
    /// character is attuned to it.
    /// </summary>
    public bool CanTeleportTo(uint aetheryteId) =>
        aetheryteId != 0 && TeleportAvailable && LifestreamReason() is null && !TeleportBusy && IsAttunedConfirmed(aetheryteId);

    /// <summary>Teleports to the aetheryte through Lifestream; false when nothing started.</summary>
    public bool TeleportTo(uint aetheryteId, string name)
    {
        ForgetTravelFrame();
        if (!CanTeleportTo(aetheryteId) || Lifestream is not { } lifestream || ClickHeld)
        {
            return false;
        }

        var started = Travel?.Teleport(aetheryteId, name) ?? lifestream.Teleport(aetheryteId);
        if (started)
        {
            MarkStarted();
        }

        return started;
    }

    /// <summary>Why Teleport to an aetheryte cannot start now, for its tooltip; null when it can.</summary>
    public string? TeleportToBlocked(uint aetheryteId)
    {
        if (!TeleportAvailable)
        {
            return Strings.UnlocksTeleportNoLifestream;
        }

        if (LifestreamReason() is { } reason)
        {
            return reason;
        }

        if (!IsAttunedConfirmed(aetheryteId))
        {
            return Strings.UnlocksTeleportNotAttuned;
        }

        return TeleportBusy ? Strings.UnlocksTeleportBusy : null;
    }

    /// <summary>The Map row a territory opens on; 0 when unknown.</summary>
    public uint MapOfTerritory(uint territoryId)
    {
        if (territoryId == 0)
        {
            return 0;
        }

        if (territoryMaps.TryGetValue(territoryId, out var known))
        {
            return known;
        }

        var map = 0u;
        try
        {
            map = data.GetExcelSheet<TerritoryType>()?.GetRowOrDefault(territoryId)?.Map.RowId ?? 0;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "TerritoryType lookup for {TerritoryId} failed", territoryId);
        }

        territoryMaps[territoryId] = map;
        return map;
    }

    /// <summary>Opens the game's map with a flag on the aetheryte; false when it has no known place or the call failed.</summary>
    public bool FlagAetheryte(uint aetheryteId)
    {
        if (Aetherytes.Find(aetheryteId) is not { } aetheryte || MapOfTerritory(aetheryte.TerritoryId) is not (> 0 and var mapId) || Map(mapId) is not { } map)
        {
            return false;
        }

        try
        {
            var x = ToMapCoordinate(aetheryte.X, map.OffsetX, map.SizeFactor);
            var y = ToMapCoordinate(aetheryte.Z, map.OffsetY, map.SizeFactor);
            return gameGui.OpenMapWithMapLink(new MapLinkPayload(aetheryte.TerritoryId, mapId, x, y));
        }
        catch (Exception ex)
        {
            WarnUnlockCall(ex, "Flag aetheryte {0} failed", aetheryteId);
            return false;
        }
    }

    /// <summary>"Place (x.x, y.y)" of an aetheryte for the clipboard; null without a known place.</summary>
    public string? AetheryteCoordinateText(uint aetheryteId)
    {
        if (Aetherytes.Find(aetheryteId) is not { } aetheryte || MapOfTerritory(aetheryte.TerritoryId) is not (> 0 and var mapId) || Map(mapId) is not { } map)
        {
            return null;
        }

        return string.Format(
            CultureInfo.CurrentCulture,
            Strings.CoordinateClipboardFormat,
            aetheryte.Name,
            ToMapCoordinate(aetheryte.X, map.OffsetX, map.SizeFactor),
            ToMapCoordinate(aetheryte.Z, map.OffsetY, map.SizeFactor));
    }

    /// <summary>Whether the map can be opened on the territory (it has a map and game calls are allowed).</summary>
    public bool CanOpenMap(uint territoryId) => CallsAllowed && MapOfTerritory(territoryId) != 0;

    /// <summary>Opens the game's map on a zone (a read-only UI call); false when it has no map or the call failed.</summary>
    public unsafe bool OpenMap(uint territoryId)
    {
        var mapId = MapOfTerritory(territoryId);
        if (mapId == 0 || !CallsAllowed)
        {
            return false;
        }

        try
        {
            var agent = AgentMap.Instance();
            if (agent == null)
            {
                return false;
            }

            agent->OpenMapByMapId(mapId, territoryId);
            return true;
        }
        catch (Exception ex)
        {
            WarnUnlockCall(ex, "Open map on territory {0} failed", territoryId);
            return false;
        }
    }

    /// <summary>Whether the Duty Finder can be opened on a duty (game calls allowed).</summary>
    public bool CanOpenDutyFinder(uint contentFinderConditionId) => contentFinderConditionId != 0 && CallsAllowed;

    /// <summary>
    /// Opens the Duty Finder with the duty selected (decision 1): a read-only UI call, the same as picking the duty in
    /// the window; it never queues. False when the call failed.
    /// </summary>
    public unsafe bool OpenDutyFinder(uint contentFinderConditionId)
    {
        if (!CanOpenDutyFinder(contentFinderConditionId))
        {
            return false;
        }

        try
        {
            var agent = AgentContentsFinder.Instance();
            if (agent == null)
            {
                return false;
            }

            agent->OpenRegularDuty(contentFinderConditionId);
            return true;
        }
        catch (Exception ex)
        {
            WarnUnlockCall(ex, "Open the Duty Finder on duty {0} failed", contentFinderConditionId);
            return false;
        }
    }

    private void WarnUnlockCall(Exception ex, string what, uint id)
    {
        var text = string.Format(CultureInfo.InvariantCulture, what, id);
        if (unlockCallWarned)
        {
            log.Debug(ex, "{What}", text);
            return;
        }

        unlockCallWarned = true;
        log.Warning(ex, "{What}; later failures are logged at debug level", text);
    }

    /// <summary>The aetheryte a zone's TerritoryType row names (a city sub-zone names its city's); null when none.</summary>
    public AetheryteInfo? ZoneAetheryte(uint territoryId) => territoryId == 0 ? null : Aetherytes.TerritoryDefault(territoryId);
}
