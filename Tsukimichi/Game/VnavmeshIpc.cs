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
/// <item><c>Path.IsRunning() -> bool</c> (waypoints left) and <c>Path.Stop()</c>.</item>
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
    private const string StopGate = "vnavmesh.Path.Stop";

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
    private readonly ICallGateSubscriber<object>? stop;

    private bool? available;
    private long? checkedAt;
    private bool readyCached;
    private float progressCached = -1f;
    private bool walkingCached;
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
            stop = pluginInterface.GetIpcSubscriber<object>(StopGate);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "vnavmesh IPC subscribers unavailable");
            isReady = null;
            buildProgress = null;
            moveCloseTo = null;
            pathfindInProgress = null;
            isRunning = null;
            stop = null;
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
            return walkingCached;
        }
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
            walkingCached = false;
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
        walkingCached = (isRunning is not null && !gates.IsMissing(IsRunningGate) && Invoke(isRunning, IsRunningGate, static gate => gate.InvokeFunc(), false))
            || (pathfindInProgress is not null && !gates.IsMissing(PathfindInProgressGate) && Invoke(pathfindInProgress, PathfindInProgressGate, static gate => gate.InvokeFunc(), false));
    }

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
