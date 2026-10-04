using System;
using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Ipc;

namespace Tsukimichi.Game;

/// <summary>
/// Walking through vnavmesh's IPC (feature plan v5, 1.6.0, decision 1), optional like <see cref="LifestreamIpc"/>.
/// The gates, as vnavmesh's <c>IPCProvider.cs</c> registers them ("vnavmesh." + name; checked against
/// awgil/ffxiv_navmesh 6fc8072, 2026-08-31):
/// <list type="bullet">
/// <item><c>Nav.IsReady() -> bool</c>: a navmesh is loaded for the current zone; <c>Nav.BuildProgress() -> float</c>:
/// negative when no build runs, else 0..1.</item>
/// <item><c>SimpleMove.PathfindAndMoveCloseTo(Vector3 destination, bool fly, float range) -> bool</c>: queues a path
/// and follows it; false while another pathfind is pending. <c>SimpleMove.PathfindInProgress() -> bool</c>.</item>
/// <item><c>Path.IsRunning() -> bool</c> (waypoints left), <c>Path.NumWaypoints() -> int</c> and <c>Path.Stop()</c>,
/// which clears the waypoints only: a pathfind still pending hands its path over later (vnavmesh 1.2.3.14's
/// <c>AsyncMoveRequest</c>), which <see cref="Core.Travel.PendingWalkStop"/> watches for.</item>
/// <item>Travel recovery and preflight (feature plan v7 A8, A9; same commit): <c>Nav.Reload() -> bool</c> (drops the
/// zone's navmesh and loads it again from vnavmesh's cache, the fix its developers prescribe), <c>Query.Mesh.PointOnFloor(Vector3 p,
/// bool allowUnlandable, float halfExtentXZ) -> Vector3?</c> (the highest mesh point under <c>p</c> within the extent;
/// unlandable false keeps to polygons reachable on foot) and <c>Query.Mesh.NearestPointReachable(Vector3 p, float
/// halfExtentXZ, float halfExtentY) -> Vector3?</c> for a landing spot, and <c>Path.GetMovementAllowed() -> bool</c> /
/// <c>Path.SetMovementAllowed(bool)</c>, the runtime switch another plugin can turn off to pause vnavmesh's movement.
/// Questionable subscribes to <c>PointOnFloor</c> with the same types.</item>
/// </list>
/// <see cref="Available"/> comes from Dalamud's plugin list (cached, refreshed when it changes); the state reads are
/// cached for <see cref="StateCacheMs"/> so the per-frame Walk / Stop button costs a few IPC calls a second. Every call
/// is wrapped: a gate that throws reads as not ready / not walking / refused, and the first failure is logged once. A
/// gate that is not registered is remembered per gate (<see cref="IpcGateHealth"/>) until the plugin list changes: only
/// a missing <c>PathfindAndMoveCloseTo</c> or <c>Nav.IsReady</c> makes vnavmesh unavailable; another missing gate only
/// turns its own answer off.
/// </summary>
public sealed class VnavmeshIpc : IDisposable
{
    public const string PluginInternalName = "vnavmesh";
    private const string IsReadyGate = "vnavmesh.Nav.IsReady";
    private const string BuildProgressGate = "vnavmesh.Nav.BuildProgress";
    private const string MoveCloseToGate = "vnavmesh.SimpleMove.PathfindAndMoveCloseTo";
    private const string PathfindInProgressGate = "vnavmesh.SimpleMove.PathfindInProgress";
    private const string IsRunningGate = "vnavmesh.Path.IsRunning";
    private const string NumWaypointsGate = "vnavmesh.Path.NumWaypoints";
    private const string StopGate = "vnavmesh.Path.Stop";
    private const string ReloadGate = "vnavmesh.Nav.Reload";
    private const string PointOnFloorGate = "vnavmesh.Query.Mesh.PointOnFloor";
    private const string NearestReachableGate = "vnavmesh.Query.Mesh.NearestPointReachable";
    private const string GetMovementAllowedGate = "vnavmesh.Path.GetMovementAllowed";
    private const string SetMovementAllowedGate = "vnavmesh.Path.SetMovementAllowed";

    /// <summary>How long a state answer (ready, progress, walking) is reused before vnavmesh is asked again.</summary>
    public const long StateCacheMs = 250;

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;
    private readonly IpcGateHealth gates = new(MoveCloseToGate, IsReadyGate);
    private readonly ICallGateSubscriber<bool>? isReady;
    private readonly ICallGateSubscriber<float>? buildProgress;
    private readonly ICallGateSubscriber<Vector3, bool, float, bool>? moveCloseTo;
    private readonly ICallGateSubscriber<bool>? pathfindInProgress;
    private readonly ICallGateSubscriber<bool>? isRunning;
    private readonly ICallGateSubscriber<int>? numWaypoints;
    private readonly ICallGateSubscriber<object>? stop;
    private readonly ICallGateSubscriber<bool>? reload;
    private readonly ICallGateSubscriber<Vector3, bool, float, Vector3?>? pointOnFloor;
    private readonly ICallGateSubscriber<Vector3, float, float, Vector3?>? nearestReachable;
    private readonly ICallGateSubscriber<bool>? getMovementAllowed;
    private readonly ICallGateSubscriber<bool, object>? setMovementAllowed;

    private bool? available;
    private long? checkedAt;
    private bool readyCached;
    private float progressCached = -1f;
    private bool runningCached;
    private bool pathfindingCached;
    private int? waypointsCached;
    private bool warned;

    public VnavmeshIpc(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.log = log ?? throw new ArgumentNullException(nameof(log));

        try
        {
            isReady = pluginInterface.GetIpcSubscriber<bool>(IsReadyGate);
            buildProgress = pluginInterface.GetIpcSubscriber<float>(BuildProgressGate);
            moveCloseTo = pluginInterface.GetIpcSubscriber<Vector3, bool, float, bool>(MoveCloseToGate);
            pathfindInProgress = pluginInterface.GetIpcSubscriber<bool>(PathfindInProgressGate);
            isRunning = pluginInterface.GetIpcSubscriber<bool>(IsRunningGate);
            numWaypoints = pluginInterface.GetIpcSubscriber<int>(NumWaypointsGate);
            stop = pluginInterface.GetIpcSubscriber<object>(StopGate);
            reload = pluginInterface.GetIpcSubscriber<bool>(ReloadGate);
            pointOnFloor = pluginInterface.GetIpcSubscriber<Vector3, bool, float, Vector3?>(PointOnFloorGate);
            nearestReachable = pluginInterface.GetIpcSubscriber<Vector3, float, float, Vector3?>(NearestReachableGate);
            getMovementAllowed = pluginInterface.GetIpcSubscriber<bool>(GetMovementAllowedGate);
            setMovementAllowed = pluginInterface.GetIpcSubscriber<bool, object>(SetMovementAllowedGate);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "vnavmesh IPC subscribers unavailable");
            isReady = null;
            buildProgress = null;
            moveCloseTo = null;
            pathfindInProgress = null;
            isRunning = null;
            numWaypoints = null;
            stop = null;
            reload = null;
            pointOnFloor = null;
            nearestReachable = null;
            getMovementAllowed = null;
            setMovementAllowed = null;
        }

        pluginInterface.ActivePluginsChanged += OnActivePluginsChanged;
    }

    public void Dispose()
    {
        pluginInterface.ActivePluginsChanged -= OnActivePluginsChanged;
    }

    /// <summary>
    /// True while vnavmesh is installed and loaded and its core gates are registered. Cached; refreshed when Dalamud's
    /// plugin list changes.
    /// </summary>
    public bool Available
    {
        get
        {
            if (moveCloseTo is null || isReady is null || gates.CoreMissing)
            {
                return false;
            }

            available ??= IsLoaded();
            return available.Value;
        }
    }

    /// <summary>True when vnavmesh has a navmesh for the current zone (cached).</summary>
    public bool IsReady
    {
        get
        {
            Refresh();
            return readyCached;
        }
    }

    /// <summary>The navmesh build's progress, 0..1, or a negative number when no build runs (cached).</summary>
    public float BuildProgress
    {
        get
        {
            Refresh();
            return progressCached;
        }
    }

    /// <summary>True while vnavmesh follows a path or is still finding one (cached).</summary>
    public bool IsWalking
    {
        get
        {
            Refresh();
            return runningCached || pathfindingCached;
        }
    }

    /// <summary>True while vnavmesh is still finding a path: its pathfind task is pending and nothing moves yet (cached).</summary>
    public bool IsPathfinding
    {
        get
        {
            Refresh();
            return pathfindingCached;
        }
    }

    /// <summary>How many waypoints the path being followed has left; null when vnavmesh cannot say (cached).</summary>
    public int? Waypoints
    {
        get
        {
            Refresh();
            return waypointsCached;
        }
    }

    /// <summary>
    /// Whether vnavmesh is finding a path and whether it follows one, asked now rather than from the cache: a stop that
    /// waits for a pending pathfind must catch its path the frame it starts to run.
    /// </summary>
    public (bool Pathfinding, bool Running) ReadMotion()
    {
        if (!Available)
        {
            return (false, false);
        }

        pathfindingCached = ReadPathfinding();
        runningCached = ReadRunning();
        return (pathfindingCached, runningCached);
    }

    /// <summary>
    /// Asks vnavmesh to find a path to <paramref name="destination"/> on foot (or, <paramref name="fly"/>, through the
    /// air) and follow it until within <paramref name="range"/>. A flying path needs the character on a mount already:
    /// vnavmesh takes off by jumping from the mount, and stands still on foot. It ends at the destination, which may
    /// leave the mount hovering; the caller lands it. False when it refused (a pathfind already pending), is absent or
    /// threw.
    /// </summary>
    public bool MoveCloseTo(Vector3 destination, float range, bool fly = false)
    {
        if (!Available || moveCloseTo is null)
        {
            return false;
        }

        var accepted = Invoke(moveCloseTo, MoveCloseToGate, gate => gate.InvokeFunc(destination, fly, range), false);
        checkedAt = null;
        return accepted;
    }

    /// <summary>Stops vnavmesh's movement.</summary>
    public void Stop()
    {
        if (!Available || stop is null || gates.IsMissing(StopGate))
        {
            return;
        }

        Invoke(stop, StopGate, static gate =>
        {
            gate.InvokeAction();
            return true;
        }, false);
        checkedAt = null;
    }

    /// <summary>
    /// Asks vnavmesh to drop the zone's navmesh and load it again (from its cache when it has one). The ready answer is
    /// read afresh afterwards. False when vnavmesh is absent, lacks the gate, refused (no zone) or threw.
    /// </summary>
    public bool Reload()
    {
        if (!Available || reload is null || gates.IsMissing(ReloadGate))
        {
            return false;
        }

        var accepted = Invoke(reload, ReloadGate, static gate => gate.InvokeFunc(), false);
        checkedAt = null;
        return accepted;
    }

    /// <summary>
    /// The highest point of the navmesh under <paramref name="probe"/> within <paramref name="halfExtentXZ"/> on the
    /// ground plane, on polygons reachable on foot (vnavmesh's <c>PointOnFloor</c> with unlandable spots left out); null
    /// when there is none or vnavmesh cannot say.
    /// </summary>
    public Vector3? PointOnFloor(Vector3 probe, float halfExtentXZ)
    {
        if (!Available || pointOnFloor is null || gates.IsMissing(PointOnFloorGate))
        {
            return null;
        }

        return Invoke<ICallGateSubscriber<Vector3, bool, float, Vector3?>, Vector3?>(pointOnFloor, PointOnFloorGate, gate => gate.InvokeFunc(probe, false, halfExtentXZ), null);
    }

    /// <summary>The navmesh point reachable on foot nearest <paramref name="point"/> within the extents; null when none or vnavmesh cannot say.</summary>
    public Vector3? NearestPointReachable(Vector3 point, float halfExtentXZ, float halfExtentY)
    {
        if (!Available || nearestReachable is null || gates.IsMissing(NearestReachableGate))
        {
            return null;
        }

        return Invoke<ICallGateSubscriber<Vector3, float, float, Vector3?>, Vector3?>(nearestReachable, NearestReachableGate, gate => gate.InvokeFunc(point, halfExtentXZ, halfExtentY), null);
    }

    /// <summary>
    /// vnavmesh's "movement allowed" switch: false while another plugin paused vnavmesh's movement (a path is found but
    /// the character stands still). Null when vnavmesh is absent or lacks the gate. Asked afresh: Setup reads it a few
    /// times a second at most.
    /// </summary>
    public bool? MovementAllowed
    {
        get
        {
            if (!Available || getMovementAllowed is null || gates.IsMissing(GetMovementAllowedGate))
            {
                return null;
            }

            return Invoke<ICallGateSubscriber<bool>, bool?>(getMovementAllowed, GetMovementAllowedGate, static gate => gate.InvokeFunc(), null);
        }
    }

    /// <summary>
    /// Sets vnavmesh's "movement allowed" switch (a runtime switch: vnavmesh does not save it). Only from Setup's
    /// explicit buttons: "Allow movement" (true) and its Undo (false). False when vnavmesh is absent, lacks the gate or
    /// threw.
    /// </summary>
    public bool SetMovementAllowed(bool allowed)
    {
        if (!Available || setMovementAllowed is null || gates.IsMissing(SetMovementAllowedGate))
        {
            return false;
        }

        return Invoke(setMovementAllowed, SetMovementAllowedGate, gate =>
        {
            gate.InvokeAction(allowed);
            return true;
        }, false);
    }

    /// <summary>
    /// Reads the state gates once per <see cref="StateCacheMs"/>; all false (progress -1) without vnavmesh. A missing
    /// optional gate leaves its own answer at its default (no progress, not walking by that gate).
    /// </summary>
    private void Refresh()
    {
        if (!Available || isReady is null)
        {
            checkedAt = null;
            readyCached = false;
            progressCached = -1f;
            runningCached = false;
            pathfindingCached = false;
            waypointsCached = null;
            return;
        }

        var now = Environment.TickCount64;
        if (checkedAt is { } at && now - at < StateCacheMs)
        {
            return;
        }

        checkedAt = now;
        readyCached = Invoke(isReady, IsReadyGate, static gate => gate.InvokeFunc(), false);
        progressCached = readyCached || buildProgress is null || gates.IsMissing(BuildProgressGate)
            ? -1f
            : Invoke(buildProgress, BuildProgressGate, static gate => gate.InvokeFunc(), -1f);
        runningCached = ReadRunning();
        pathfindingCached = ReadPathfinding();
        waypointsCached = runningCached && numWaypoints is not null && !gates.IsMissing(NumWaypointsGate)
            ? Invoke<ICallGateSubscriber<int>, int?>(numWaypoints, NumWaypointsGate, static gate => gate.InvokeFunc(), null)
            : null;
    }

    private bool ReadRunning() =>
        isRunning is not null && !gates.IsMissing(IsRunningGate) && Invoke(isRunning, IsRunningGate, static gate => gate.InvokeFunc(), false);

    private bool ReadPathfinding() =>
        pathfindInProgress is not null && !gates.IsMissing(PathfindInProgressGate) && Invoke(pathfindInProgress, PathfindInProgressGate, static gate => gate.InvokeFunc(), false);

    /// <summary>
    /// Calls a gate; a gate that is not registered is remembered as missing (only the core gates take vnavmesh down
    /// with them), any other failure is logged once.
    /// </summary>
    private T Invoke<TGate, T>(TGate gate, string name, Func<TGate, T> call, T fallback)
    {
        try
        {
            return call(gate);
        }
        catch (IpcNotReadyError)
        {
            if (!gates.IsMissing(name))
            {
                log.Information("vnavmesh does not offer {Gate}", name);
            }

            gates.MarkMissing(name);
            return fallback;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, name + " failed");
            return fallback;
        }
    }

    private void OnActivePluginsChanged(IActivePluginsChangedEventArgs args)
    {
        available = null;
        checkedAt = null;
        gates.Reset();
    }

    private bool IsLoaded()
    {
        try
        {
            foreach (var plugin in pluginInterface.InstalledPlugins)
            {
                if (plugin.IsLoaded && string.Equals(plugin.InternalName, PluginInternalName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Installed plugin list unavailable");
        }

        return false;
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
