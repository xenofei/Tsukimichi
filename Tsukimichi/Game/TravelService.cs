using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
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
/// aetheryte is attuned without a zone change); the UI reads the cached answers. Right after login Dalamud's list is
/// empty until the player object exists, so an empty read (or no player) is not taken as "nothing attuned": the list
/// stays unknown and is read again next frame. <see cref="AttunementRevision"/> moves whenever the answers change. A
/// walk or chain this service started is stopped when the player leaves the zone (the chain's own check), on logout and
/// when the plugin unloads; a walk another plugin started is never stopped from here. A stopped walk whose pathfind is
/// still pending in vnavmesh is stopped again the moment its path starts to run (<see cref="PendingWalkStop"/>). When
/// Questionable or AutoDuty is seen running during a trip (started from their own windows), the trip ends without
/// touching vnavmesh: the walk is theirs now.
/// <para>Mounting, Sprint and landing a flying mount (travel review, 1.10) are the game's general actions through
/// FFXIVClientStructs' <c>ActionManager</c> (Mount Roulette or the chosen mount, Sprint, and Dismount, which brings a
/// flying mount down), only while the shared <see cref="HookGate"/> allows game calls and only during a Walk or Go to
/// giver the player clicked; whether the zone's flying is unlocked is Dalamud's <see cref="IUnlockState"/>
/// (<c>PlayerState.IsAetherCurrentZoneComplete</c>). The character is never dismounted.</para>
/// </summary>
public sealed class TravelService : ITravelPorts, IDisposable
{
    /// <summary>How often attunement is read again while logged in.</summary>
    public const long AttunementRefreshMs = 10_000;

    /// <summary>How close (raw units) vnavmesh is asked to bring the player to the giver.</summary>
    public const float WalkRange = 3f;

    private const uint SprintAction = TravelActions.Sprint;
    private const uint MountRouletteAction = TravelActions.MountRoulette;
    private const uint DismountAction = TravelActions.Dismount;

    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly IObjectTable objects;
    private readonly IAetheryteList aetheryteList;
    private readonly IDataManager data;
    private readonly IUnlockState unlocks;
    private readonly IPluginLog log;
    private readonly GoToGiver journey;
    private readonly PendingWalkStop pendingStop = new();

    private Dictionary<uint, (uint Gil, bool Favourite)> attuned = [];
    private HashSet<uint> attunedShards = [];
    private bool attunementKnown;
    private bool shardsKnown;
    private bool attunementDirty = true;
    private long attunementReadAt;
    private Vector3? position;
    private bool journeyIsWalkOnly;
    private bool warned;
    private bool actionWarned;
    private uint zoneRead;
    private bool zoneMount;
    private uint zoneCurrents;
    private string? journeyArrivalNote;

    public TravelService(IFramework framework, IClientState clientState, ICondition condition, IObjectTable objects, IAetheryteList aetheryteList, IDataManager data, IUnlockState unlocks, LifestreamIpc lifestream, VnavmeshIpc vnavmesh, IPluginLog log)
    {
        this.data = data ?? throw new ArgumentNullException(nameof(data));
        this.unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));
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

    /// <summary>Settings › Integrations › Travel: how walks move; on foot when unset.</summary>
    public Func<TravelOptions>? Options { get; set; }

    /// <summary>Settings › Integrations › Travel › Mount: the chosen mount's row, 0 for Mount Roulette; Roulette when unset.</summary>
    public Func<uint>? MountChoice { get; set; }

    /// <summary>Whether Questionable runs (its cached status); a trip under way then ends and leaves vnavmesh to it. Unset reads as not running.</summary>
    public Func<bool>? QuestionableRunning { get; set; }

    /// <summary>Whether AutoDuty is not stopped (its cached state); a trip under way then ends and leaves vnavmesh to it. Unset reads as stopped.</summary>
    public Func<bool>? AutoDutyRunning { get; set; }

    /// <summary>The settings' movement options now (<see cref="TravelOptions.OnFoot"/> when unset); every new plan carries them.</summary>
    public TravelOptions CurrentOptions => Options?.Invoke() ?? TravelOptions.OnFoot;

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

    /// <summary>
    /// True only when the game's attuned list has been read and holds the aetheryte: the unlock rows' check mark
    /// (feature plan v6 K2) says "attuned" only when the game confirms it, never on the optimistic default.
    /// </summary>
    public bool IsAttunedConfirmed(uint aetheryteId) => attunementKnown && attuned.ContainsKey(aetheryteId);

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

    /// <summary>
    /// Moves whenever an answer of <see cref="IsAttuned"/>, <see cref="TryGetCost"/> or <see cref="IsShardAttuned"/> may
    /// have changed (a read that differs from the last, the list becoming known, logout); a cache of anything built from
    /// attunement keys on it.
    /// </summary>
    public int AttunementRevision { get; private set; }

    private void RefreshAttunement(long now)
    {
        attunementDirty = false;
        attunementReadAt = now;
        try
        {
            if (objects.LocalPlayer is null)
            {
                // Dalamud's list reads empty until the player object exists: try again next frame.
                attunementDirty = true;
                return;
            }

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

            if (read.Count == 0)
            {
                // Every character has attuned at least its starting city's aetheryte: an empty list is one not filled
                // yet, not an answer. Keep what was known and read again next frame.
                attunementDirty = true;
                return;
            }

            if (!attunementKnown || !SameAttunement(attuned, read))
            {
                AttunementRevision++;
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

    private static bool SameAttunement(Dictionary<uint, (uint Gil, bool Favourite)> before, Dictionary<uint, (uint Gil, bool Favourite)> after)
    {
        if (before.Count != after.Count)
        {
            return false;
        }

        foreach (var (id, entry) in after)
        {
            if (!before.TryGetValue(id, out var old) || old != entry)
            {
                return false;
            }
        }

        return true;
    }

    private unsafe void RefreshShards()
    {
        if (Gate is not { HooksAllowed: true } || Index?.Invoke() is not { } index)
        {
            ForgetShards();
            return;
        }

        try
        {
            var state = UIState.Instance();
            if (state == null)
            {
                ForgetShards();
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

            if (!shardsKnown || !attunedShards.SetEquals(read))
            {
                AttunementRevision++;
            }

            attunedShards = read;
            shardsKnown = true;
        }
        catch (Exception ex)
        {
            ForgetShards();
            WarnOnce(ex, "Aethernet shard attunement unavailable");
        }
    }

    private void ForgetShards()
    {
        if (shardsKnown)
        {
            AttunementRevision++;
        }

        shardsKnown = false;
    }

    // ------------------------------------------------------------------ the player

    /// <inheritdoc />
    public uint Territory => clientState.IsLoggedIn ? clientState.TerritoryType : 0;

    /// <summary>The player's world position (x, height, z), read each framework tick; null without a player.</summary>
    public Vector3? Position3 => position;

    /// <inheritdoc />
    public (float X, float Z)? Position => position is { } p ? (p.X, p.Z) : null;

    /// <inheritdoc />
    public float? Height => position?.Y;

    /// <inheritdoc />
    public bool BetweenAreas => condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51];

    public bool InCombat => condition[ConditionFlag.InCombat];

    /// <summary>True while the character is bound by a duty (a dungeon, trial or raid, solo duties included).</summary>
    public bool InDuty => condition[ConditionFlag.BoundByDuty] || condition[ConditionFlag.BoundByDuty56] || condition[ConditionFlag.BoundByDuty95];

    /// <summary>True while the character casts (a teleport among others); a teleport or a walk would cut it or be refused.</summary>
    public bool Casting => condition[ConditionFlag.Casting] || condition[ConditionFlag.Casting87];

    /// <summary>
    /// True while a teleport may be under way: a cast, a loading screen, or the Go to giver chain asked for a teleport
    /// and no loading screen has followed yet. Teleport buttons treat it as busy.
    /// </summary>
    public bool TeleportInProgress => Casting || BetweenAreas || journey.TeleportCastPending;

    /// <summary>True while the Go to giver chain waits for its teleport cast, which a Stop cannot cut short.</summary>
    public bool JourneyCastPending => journey.TeleportCastPending;

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

        if (InDuty)
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

    /// <summary>True while the walk under way flies.</summary>
    public bool JourneyFlying => journey.IsActive && journey.Flying;

    /// <summary>The plan of the run under way or the last one; null before the first.</summary>
    public GoToGiverPlan? JourneyPlan => journey.Plan;

    /// <summary>Who or what the run under way heads for (the giver's name, or the way into a zone), for the status line.</summary>
    public string JourneyTarget { get; private set; } = string.Empty;

    /// <summary>True while vnavmesh moves the character, whoever asked it to.</summary>
    public bool Walking => Vnavmesh.IsWalking;

    /// <inheritdoc />
    public bool Pathfinding => Vnavmesh.IsPathfinding;

    /// <inheritdoc />
    public int? Waypoints => Vnavmesh.Waypoints;

    /// <summary>
    /// Starts a Go to giver chain (or, with <see cref="GoToGiverPlan.WalkOnly"/>, a lone walk), replacing any under way.
    /// <paramref name="target"/> names where it heads for the status line; <paramref name="arrivalNote"/> is the chat
    /// line printed when the run ends well (at an interior's door: "go in to find …").
    /// </summary>
    public void Start(GoToGiverPlan plan, string target = "", string? arrivalNote = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        journeyIsWalkOnly = plan.Teleport is null && plan.Hop is null;
        JourneyTarget = target ?? string.Empty;
        journeyArrivalNote = arrivalNote;
        Report(journey.Start(plan, Environment.TickCount64));
    }

    /// <summary>
    /// The one Stop: cancels the walk or chain this service runs, a walk whose path is still being found included
    /// (<see cref="StopWalk"/>). A walk another plugin (Questionable, AutoDuty, vnavmesh's own window) started is left
    /// alone; it is that plugin's to stop. True when a run was under way.
    /// </summary>
    public bool Stop() => journey.Cancel() is not null;

    /// <summary>True when the run under way (or the last one) is a lone Walk to giver, not a Go to giver chain.</summary>
    public bool JourneyIsWalkOnly => journeyIsWalkOnly;

    private void Report(GoToGiverOutcome? outcome)
    {
        if (outcome is { Step: GoToGiverStep.Done })
        {
            if (journey.MountGaveUp)
            {
                log.Information("Go to giver: the mount did not come; walked on foot");
            }

            if (journeyArrivalNote is { } note)
            {
                PrintLine(note);
            }

            return;
        }

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
            GoToGiverFailure.Stuck => Strings.TravelFailStuck,
            GoToGiverFailure.LandingFailed => Strings.TravelFailLanding,
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
    public bool StartWalk(GoToGiverPlan plan, bool fly)
    {
        if (!Vnavmesh.MoveCloseTo(new Vector3(plan.GoalX, plan.GoalY, plan.GoalZ), WalkRange, fly))
        {
            // Refused (a pathfind still pending among the reasons): a stop still waiting for one keeps waiting.
            return false;
        }

        // vnavmesh took a new walk, so no pathfind of a stopped one is left to catch.
        pendingStop.Disarm();
        return true;
    }

    /// <inheritdoc />
    public bool Mounted => condition[ConditionFlag.Mounted];

    /// <inheritdoc />
    public bool InFlight => condition[ConditionFlag.InFlight];

    /// <inheritdoc />
    public TravelMoveContext MoveContext
    {
        get
        {
            ReadZone();
            var calls = Gate is { HooksAllowed: true };
            var (type, id) = MountAction();
            var mountAllowed = calls && zoneMount && !InCombat && CanUse(type, id);
            var flight = calls && zoneCurrents != 0 && FlightUnlocked(zoneCurrents);
            var sprint = calls && CanUse(ActionType.GeneralAction, SprintAction);
            return new TravelMoveContext(Mounted, mountAllowed, flight, !zoneMount, sprint);
        }
    }

    /// <inheritdoc />
    public bool StartMount()
    {
        var (type, id) = MountAction();
        return Gate is { HooksAllowed: true } && UseAction(type, id);
    }

    /// <inheritdoc />
    /// <remarks>Only while the mount is in the air, read again right before the press: on the ground Dismount would dismount.</remarks>
    public bool StartLanding() =>
        Gate is { HooksAllowed: true } && InFlight && CanUse(ActionType.GeneralAction, DismountAction) && UseAction(ActionType.GeneralAction, DismountAction);

    /// <inheritdoc />
    public bool StartSprint() => Gate is { HooksAllowed: true } && UseAction(ActionType.GeneralAction, SprintAction);

    /// <summary>The chosen mount when the character owns it, else Mount Roulette.</summary>
    private (ActionType Type, uint Id) MountAction()
    {
        var chosen = MountChoice?.Invoke() ?? 0;
        if (chosen != 0)
        {
            try
            {
                if (data.GetExcelSheet<Mount>().GetRowOrDefault(chosen) is { } mount && unlocks.IsMountUnlocked(mount))
                {
                    return (ActionType.Mount, chosen);
                }
            }
            catch (Exception ex)
            {
                WarnActionOnce(ex, "Mount unlock unavailable");
            }
        }

        return (ActionType.GeneralAction, MountRouletteAction);
    }

    /// <summary>The current zone's TerritoryType flags (mounts allowed, its aether current set), read once per zone.</summary>
    private void ReadZone()
    {
        var territory = Territory;
        if (territory == zoneRead)
        {
            return;
        }

        zoneRead = territory;
        zoneMount = false;
        zoneCurrents = 0;
        try
        {
            if (data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territory) is { } row)
            {
                zoneMount = row.Mount;
                zoneCurrents = row.AetherCurrentCompFlgSet.RowId;
            }
        }
        catch (Exception ex)
        {
            WarnActionOnce(ex, "Zone flags unavailable");
        }
    }

    /// <summary>True when every aether current of the zone's set is attuned: the game's own flying flag.</summary>
    private bool FlightUnlocked(uint set)
    {
        try
        {
            return data.GetExcelSheet<AetherCurrentCompFlgSet>().GetRowOrDefault(set) is { } row && unlocks.IsAetherCurrentCompFlgSetUnlocked(row);
        }
        catch (Exception ex)
        {
            WarnActionOnce(ex, "Flying state unavailable");
            return false;
        }
    }

    /// <summary>True when the game would take the action now (<c>ActionManager.GetActionStatus</c> answers 0).</summary>
    private unsafe bool CanUse(ActionType type, uint id)
    {
        try
        {
            var actions = ActionManager.Instance();
            return actions != null && actions->GetActionStatus(type, id) == 0;
        }
        catch (Exception ex)
        {
            WarnActionOnce(ex, "Action status unavailable");
            return false;
        }
    }

    private unsafe bool UseAction(ActionType type, uint id)
    {
        try
        {
            var actions = ActionManager.Instance();
            return actions != null && actions->UseAction(type, id);
        }
        catch (Exception ex)
        {
            WarnActionOnce(ex, "Action use failed");
            return false;
        }
    }

    private void WarnActionOnce(Exception ex, string message)
    {
        if (actionWarned)
        {
            log.Debug(ex, message);
            return;
        }

        actionWarned = true;
        log.Warning(ex, message);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Called by the chain only for a walk it asked for. vnavmesh's stop clears the path being followed but not a
    /// pathfind still pending, so the stop stays armed for that one (<see cref="PendingWalkStop"/>).
    /// </remarks>
    public void StopWalk()
    {
        Vnavmesh.Stop();
        pendingStop.Arm(Environment.TickCount64);
    }

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

            if ((journey.IsActive || pendingStop.Armed) && TakenOver() is { } takenBy)
            {
                // Questionable or AutoDuty, started from its own window, drives the character through vnavmesh now: end
                // the trip without stopping vnavmesh (that would stop their walk), and no pending stop either.
                pendingStop.Disarm();
                if (journey.Abandon() is not null)
                {
                    log.Information("Go to giver ended: {Plugin} took over", takenBy);
                    PrintLine(string.Format(CultureInfo.CurrentCulture, journeyIsWalkOnly ? Strings.TravelWalkStoppedFormat : Strings.TravelGoToStoppedFormat, string.Format(CultureInfo.CurrentCulture, Strings.TravelFailTakenOverFormat, takenBy)));
                }
            }

            Report(journey.Tick(now));
            TickPendingStop(now);
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Travel update failed");
        }
    }

    /// <summary>The plugin that now drives the character ("Questionable", "AutoDuty"), or null when neither runs.</summary>
    private string? TakenOver() =>
        QuestionableRunning?.Invoke() == true ? "Questionable"
        : AutoDutyRunning?.Invoke() == true ? "AutoDuty"
        : null;

    /// <summary>While a stopped walk's pathfind is pending, stops its path the frame it starts to run.</summary>
    private void TickPendingStop(long now)
    {
        if (!pendingStop.Armed)
        {
            return;
        }

        var (pathfinding, running) = Vnavmesh.ReadMotion();
        if (pendingStop.Tick(now, pathfinding, running))
        {
            log.Information("Stopped a walk whose path was still being found when it was stopped");
            Vnavmesh.Stop();
        }
    }

    private void OnTerritoryChanged(uint territory) => attunementDirty = true;

    private void OnLogin() => attunementDirty = true;

    private void OnLogout(int type, int code)
    {
        journey.Cancel();
        if (attunementKnown || shardsKnown)
        {
            AttunementRevision++;
        }

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
