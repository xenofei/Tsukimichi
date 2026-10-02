using System;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Game;

/// <summary>
/// Moves the game's single map flag to a quest giver without opening the map (1.6.0, C3 C): what a followed route
/// does on its own when a step is turned in, so a cutscene's end is not followed by a map window. A click on "Flag"
/// or "Flag next stop" opens the map with the flag instead (<c>GameLinks.FlagMap</c>). It calls a game function, so it
/// follows the same kill switch as the hooks (<see cref="Gate"/>); a failure logs one warning and nothing moves.
/// </summary>
public sealed class MapFlag(IPluginLog log)
{
    private bool warned;

    /// <summary>The addon kill switch; while it pauses the hooks nothing is flagged. Null (allowed) until the plugin sets it.</summary>
    public HookGate? Gate { get; set; }

    /// <summary>Places the flag on <paramref name="quest"/>'s giver; false when it has no mappable giver, the gate pauses game calls or the call failed.</summary>
    public unsafe bool Set(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        if (quest.Issuer is not { TerritoryId: > 0, MapId: > 0 } issuer || Gate is { HooksAllowed: false })
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

            agent->SetFlagMapMarker(issuer.TerritoryId, issuer.MapId, new Vector3(issuer.X, issuer.Y, issuer.Z));
            return true;
        }
        catch (Exception ex)
        {
            if (!warned)
            {
                warned = true;
                log.Warning(ex, "The map flag could not be moved to quest {RowId}'s giver; later failures are logged at debug level", quest.RowId);
            }
            else
            {
                log.Debug(ex, "The map flag could not be moved to quest {RowId}'s giver", quest.RowId);
            }

            return false;
        }
    }
}
