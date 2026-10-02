using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Travel;
using Tsukimichi.GameData;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// The game side of travel (feature plan v5, 1.6.0): which aetherytes the player has attuned (from Dalamud's
/// <see cref="IAetheryteList"/>, with each one's gil cost and favourite flag) and which aethernet shards
/// (<c>UIState.IsAetheryteUnlocked</c>, a game call, so only while the shared <see cref="HookGate"/> allows), where the
/// player stands, and the <see cref="GoToGiver"/> chain driven from the framework update. Attunement is read on the
/// framework thread after login and every territory change, and again every <see cref="AttunementRefreshMs"/> (a new
/// aetheryte is attuned without a zone change); the UI reads the cached answers. A walk or chain this service started
/// is stopped when the player leaves the zone (the chain's own check), on logout and when the plugin unloads.
/// </summary>
public sealed class TravelService : ITravelPorts, IDisposable
{
    /// <summary>How often attunement is read again while logged in.</summary>
    public const long AttunementRefreshMs = 10_000;

    /// <summary>How close (raw units) vnavmesh is asked to bring the player to the giver.</summary>
    public const float WalkRange = 3f;

    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly IObjectTable objects;
    private readonly IAetheryteList aetheryteList;
    private readonly IPluginLog log;
    private readonly GoToGiver journey;

    private Dictionary<uint, (uint Gil, bool Favourite)> attuned = [];
    private HashSet<uint> attunedShards = [];
    private bool attunementKnown;
    private bool shardsKnown;
    private bool attunementDirty = true;
    private long attunementReadAt;
    private Vector3? position;
    private bool journeyIsWalkOnly;
    private bool warned;

    public TravelService(IFramework framework, IClientState clientState, ICondition condition, IObjectTable objects, IAetheryteList aetheryteList, LifestreamIpc lifestream, VnavmeshIpc vnavmesh, IPluginLog log)
    {
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
        this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
        this.objects = objects ?? throw new ArgumentNullException(nameof(objects));
        this.aetheryteList = aetheryteList ?? throw new ArgumentNullException(nameof(aetheryteList));
        Lifestream = lifestream ?? throw new ArgumentNullException(nameof(lifestream));
        Vnavmesh = vnavmesh ?? throw new ArgumentNullException(nameof(vnavmesh));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        journey = new GoToGiver(this);

        framework.Update += OnUpdate;
        clientState.TerritoryChanged += OnTerritoryChanged;
        clientState.Login += OnLogin;
        clientState.Logout += OnLogout;
    }

    public LifestreamIpc Lifestream { get; }

    public VnavmeshIpc Vnavmesh { get; }

    /// <summary>The shared addon kill switch; the shard attunement read (a game call) runs only while it allows hooks.</summary>
    public HookGate? Gate { get; set; }

    /// <summary>The aetheryte and shard index (the plugin points it at <see cref="GameLinks.Aetherytes"/>); shards are read against it.</summary>
    public Func<AetheryteIndex>? Index { get; set; }

    /// <summary>Prints one chat line under the plugin's tag; set by the plugin.</summary>
    public Action<string>? Print { get; set; }

    public void Dispose()
    {
        framework.Update -= OnUpdate;
        clientState.TerritoryChanged -= OnTerritoryChanged;
        clientState.Login -= OnLogin;
        clientState.Logout -= OnLogout;

        // Nothing this plugin started keeps moving the character once it is gone.
        journey.Cancel();
    }

    // ------------------------------------------------------------------ attunement

    /// <summary>
    /// True when the aetheryte can be teleported to: it is in the attuned list, or the list has not been read yet
    /// (right after load, logged out), when every aetheryte counts as attuned so nothing is wrongly greyed.
    /// </summary>
    public bool IsAttuned(uint aetheryteId) => !attunementKnown || attuned.ContainsKey(aetheryteId);

    /// <summary>The teleport's gil cost and favourite flag, when the attuned list holds the aetheryte.</summary>
    public bool TryGetCost(uint aetheryteId, out uint gil, out bool favourite)
    {
        if (attuned.TryGetValue(aetheryteId, out var entry))
        {
            gil = entry.Gil;
            favourite = entry.Favourite;
            return true;
        }

        gil = 0;
        favourite = false;
        return false;
    }

    /// <summary>True when the aethernet shard is attuned; false when it is not or cannot be read (no hop is offered then).</summary>
    public bool IsShardAttuned(uint shardId) => shardsKnown && attunedShards.Contains(shardId);

    private void RefreshAttunement(long now)
    {
        attunementDirty = false;
        attunementReadAt = now;
        try
        {
            var read = new Dictionary<uint, (uint Gil, bool Favourite)>();
            foreach (var entry in aetheryteList)
            {
                // Housing entries share their aetheryte ids with a sub-index; only the plain destinations count.
                if (entry.SubIndex != 0 || entry.Ward != 0 || entry.Plot != 0 || entry.IsApartment || entry.IsSharedHouse)
                {
                    continue;
                }

                read.TryAdd(entry.AetheryteId, (entry.GilCost, entry.IsFavourite));
            }

            attuned = read;
            attunementKnown = true;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Attuned aetheryte list unavailable");
        }

        RefreshShards();
    }

    private unsafe void RefreshShards()
    {
        if (Gate is not { HooksAllowed: true } || Index?.Invoke() is not { } index)
        {
            shardsKnown = false;
            return;
        }

        try
        {
            var state = UIState.Instance();
            if (state == null)
            {
                shardsKnown = false;
                return;
            }

            var read = new HashSet<uint>();
            foreach (var shard in index.Shards)
            {
                if (state->IsAetheryteUnlocked(shard.RowId))
                {
                    read.Add(shard.RowId);
                }
            }

            attunedShards = read;
            shardsKnown = true;
        }
        catch (Exception ex)
        {
            shardsKnown = false;
            WarnOnce(ex, "Aethernet shard attunement unavailable");
        }
    }

    // ------------------------------------------------------------------ the player

    /// <inheritdoc />
    public uint Territory => clientState.IsLoggedIn ? clientState.TerritoryType : 0;

    /// <summary>The player's world position (x, height, z), read each framework tick; null without a player.</summary>
    public Vector3? Position3 => position;

    /// <inheritdoc />
    public (float X, float Z)? Position => position is { } p ? (p.X, p.Z) : null;

    /// <inheritdoc />
    public bool BetweenAreas => condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51];

    public bool InCombat => condition[ConditionFlag.InCombat];

    public bool InCutscene => condition[ConditionFlag.OccupiedInCutSceneEvent] || condition[ConditionFlag.WatchingCutscene] || condition[ConditionFlag.WatchingCutscene78];

    /// <summary>
    /// Why the game would not let a teleport start now, as a clause for "Teleport to X did not start: …"; Lifestream's
    /// own reasons (its TeleportService.CanTeleport: no player, Teleport action unusable, animation lock) map onto
    /// these conditions. <see cref="Strings.TravelReasonDeclined"/> when none explains it.
    /// </summary>
    public string RefusalReason()
    {
        if (condition[ConditionFlag.InCombat])
        {
            return Strings.TravelReasonCombat;
        }

        if (condition[ConditionFlag.Casting] || condition[ConditionFlag.Casting87])
        {
            return Strings.TravelReasonCasting;
        }

        if (BetweenAreas)
        {
            return Strings.TravelReasonLoading;
        }

        if (InCutscene)
        {
            return Strings.TravelReasonCutscene;
        }

        if (condition[ConditionFlag.BoundByDuty] || condition[ConditionFlag.BoundByDuty56] || condition[ConditionFlag.BoundByDuty95])
        {
            return Strings.TravelReasonDuty;
        }

        if (condition[ConditionFlag.OccupiedInQuestEvent] || condition[ConditionFlag.OccupiedInEvent] || condition[ConditionFlag.Occupied]
            || condition[ConditionFlag.Occupied30] || condition[ConditionFlag.Occupied33] || condition[ConditionFlag.Occupied38]
            || condition[ConditionFlag.Occupied39] || condition[ConditionFlag.OccupiedSummoningBell] || condition[ConditionFlag.Crafting])
        {
            return Strings.TravelReasonOccupied;
        }

        return Strings.TravelReasonDeclined;
    }

    /// <summary>Teleports through Lifestream; when it refuses, one chat line says why ("Teleport to X did not start: you are in combat.").</summary>
    public bool Teleport(uint aetheryteId, string name)
    {
        if (Lifestream.Teleport(aetheryteId))
        {
            return true;
        }

        PrintLine(string.Format(CultureInfo.CurrentCulture, Strings.TravelTeleportFailedFormat, name, RefusalReason()));
        return false;
    }

    // ------------------------------------------------------------------ the chain

    /// <summary>True while a Go to giver or a Walk this service started is under way.</summary>
    public bool JourneyActive => journey.IsActive;

    public GoToGiverStep JourneyStep => journey.Step;

    /// <summary>True while vnavmesh moves the character, whoever asked it to.</summary>
    public bool Walking => Vnavmesh.IsWalking;

    /// <summary>Starts a Go to giver chain (or, with <see cref="GoToGiverPlan.WalkOnly"/>, a lone walk), replacing any under way.</summary>
    public void Start(GoToGiverPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        journeyIsWalkOnly = plan.Teleport is null && plan.Hop is null;
        Report(journey.Start(plan, Environment.TickCount64));
    }

    /// <summary>The one Stop: cancels the chain this service runs, or stops vnavmesh when something else made it walk.</summary>
    public void Stop()
    {
        if (journey.IsActive)
        {
            journey.Cancel();
            return;
        }

        if (Vnavmesh.IsWalking)
        {
            Vnavmesh.Stop();
        }
    }

    private void Report(GoToGiverOutcome? outcome)
    {
        if (outcome is not { Step: GoToGiverStep.Failed } failed)
        {
            return;
        }

        log.Information("Go to giver ended: {Failure}", failed.Failure);
        var reason = failed.Failure switch
        {
            GoToGiverFailure.NotInZone => Strings.TravelFailNotInZone,
            GoToGiverFailure.TeleportRefused => RefusalReason(),
            GoToGiverFailure.TeleportDidNotStart => Strings.TravelFailTeleportDidNotStart,
            GoToGiverFailure.TeleportTimedOut => Strings.TravelFailTeleportTimedOut,
            GoToGiverFailure.HopRefused => Strings.TravelFailHopRefused,
            GoToGiverFailure.HopDidNotStart => Strings.TravelFailHopDidNotStart,
            GoToGiverFailure.HopTimedOut => Strings.TravelFailHopTimedOut,
            GoToGiverFailure.PathNotReady => Strings.TravelFailPathNotReady,
            GoToGiverFailure.WalkRefused => Strings.TravelFailWalkRefused,
            GoToGiverFailure.WalkDidNotStart => Strings.TravelFailWalkDidNotStart,
            GoToGiverFailure.WalkTimedOut => Strings.TravelFailWalkTimedOut,
            GoToGiverFailure.WalkStoppedShort => Strings.TravelFailWalkStoppedShort,
            GoToGiverFailure.LeftZone => Strings.TravelFailLeftZone,
            _ => Strings.TravelReasonDeclined,
        };
        var format = journeyIsWalkOnly ? Strings.TravelWalkStoppedFormat : Strings.TravelGoToStoppedFormat;
        PrintLine(string.Format(CultureInfo.CurrentCulture, format, reason));
    }

    // ------------------------------------------------------------------ ports for the chain

    /// <inheritdoc />
    public bool LifestreamBusy => Lifestream.IsBusy;

    /// <inheritdoc />
    public bool NavReady => Vnavmesh.IsReady;

    /// <inheritdoc />
    public bool StartTeleport(uint aetheryteId) => Lifestream.Teleport(aetheryteId);

    /// <inheritdoc />
    public bool StartHop(uint shardId) =>
        shardId == GoToGiverPlan.FirmamentHop ? Lifestream.AethernetTeleportToFirmament() : Lifestream.AethernetTeleport(shardId);

    /// <inheritdoc />
    public bool StartWalk(GoToGiverPlan plan) => Vnavmesh.MoveCloseTo(new Vector3(plan.GoalX, plan.GoalY, plan.GoalZ), WalkRange);

    /// <inheritdoc />
    public void StopWalk() => Vnavmesh.Stop();

    /// <inheritdoc />
    public void AbortLifestream() => Lifestream.Abort();

    // ------------------------------------------------------------------ events

    private void OnUpdate(IFramework _)
    {
        try
        {
            position = clientState.IsLoggedIn ? objects.LocalPlayer?.Position : null;
            var now = Environment.TickCount64;
            if (clientState.IsLoggedIn && !BetweenAreas && (attunementDirty || now - attunementReadAt >= AttunementRefreshMs))
            {
                RefreshAttunement(now);
            }

            Report(journey.Tick(now));
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Travel update failed");
        }
    }

    private void OnTerritoryChanged(uint territory) => attunementDirty = true;

    private void OnLogin() => attunementDirty = true;

    private void OnLogout(int type, int code)
    {
        journey.Cancel();
        attuned = [];
        attunedShards = [];
        attunementKnown = false;
        shardsKnown = false;
        attunementDirty = true;
        position = null;
    }

    private void PrintLine(string line)
    {
        try
        {
            Print?.Invoke(line);
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Travel chat line failed");
        }
    }

    private void WarnOnce(Exception ex, string message)
    {
        if (warned)
        {
            log.Debug(ex, message);
            return;
        }

        warned = true;
        log.Warning(ex, message);
    }
}
