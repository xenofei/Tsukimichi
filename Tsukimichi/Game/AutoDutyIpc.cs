using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Tsukimichi.Core.Companions;

namespace Tsukimichi.Game;

/// <summary>What became of a "Run with AutoDuty" press.</summary>
public enum AutoDutyStart
{
    /// <summary>AutoDuty took the run and is no longer stopped.</summary>
    Started,

    /// <summary>AutoDuty is not loaded or a gate is missing.</summary>
    Unavailable,

    /// <summary>AutoDuty refused the duty mode, so nothing was run (it would have queued in whatever mode it had).</summary>
    ModeRefused,

    /// <summary>AutoDuty took the call but stayed stopped (it does not know the duty, or another plugin holds it).</summary>
    NotStarted,
}

/// <summary>
/// AutoDuty's IPC (decision 1: "Run with AutoDuty" for the duties a quest needs). The gates are AutoDuty's own
/// (erdelf/AutoDuty <c>AutoDuty/IPC/IPCProvider.cs</c> at commit 39b9a877a466871e8a25b3af6e12dae33abd2d6c, release
/// 0.0.0.375, 2026-10-01), registered by ECommons' EzIPC under the internal name: <c>AutoDuty.ContentHasPath(uint
/// territoryType) -> bool</c>, <c>AutoDuty.Run(uint territoryType, int loops, bool bareMode)</c>,
/// <c>AutoDuty.Stop()</c>, <c>AutoDuty.IsStopped() -> bool</c>, <c>AutoDuty.PushConfigOverrides(object
/// Dictionary&lt;string, string&gt;) -> bool</c> and <c>AutoDuty.PopConfigOverrides() -> bool</c>.
/// <para>
/// A run first pushes temporary overrides (<see cref="AutoDutyPlan.Overrides"/>): <c>Meta.AutoDutyModeEnum</c> Looping
/// (which Run sets anyway), <c>Meta.DutyModeEnum</c> (Duty Support, Trust, or with the setting the regular Duty Finder)
/// and <c>Meta.LoopTimes</c> 1. AutoDuty restores every override when it stops (<c>StopAndResetAll</c> calls
/// <c>ConfigOverrideHelper.Pop</c>), so the player's own run mode, queue and loop count come back after the run. If
/// AutoDuty refuses them nothing runs. Run is then called with 0 loops (which leaves the loop count to the override) in
/// bare mode, so AutoDuty's pre-loop, between-loop and termination actions (inn trips, repairs, logging out) stay off for
/// a quest's single clear. A call that leaves AutoDuty stopped pops the overrides again.
/// </para>
/// <para>
/// Read answers are cached: <see cref="IsStopped"/> for <see cref="StateCacheMs"/>, <see cref="HasPath"/> per
/// territory until the plugin list changes or <see cref="PathCacheMs"/> passes (AutoDuty downloads paths at run time).
/// Every call is wrapped: a gate that is not registered or throws reads as no answer, and the first failure is logged once.
/// </para>
/// </summary>
public sealed class AutoDutyIpc
{
    public const string ContentHasPathGate = "AutoDuty.ContentHasPath";
    public const string RunGate = "AutoDuty.Run";
    public const string StopGate = "AutoDuty.Stop";
    public const string IsStoppedGate = "AutoDuty.IsStopped";
    public const string PushConfigOverridesGate = "AutoDuty.PushConfigOverrides";
    public const string PopConfigOverridesGate = "AutoDuty.PopConfigOverrides";

    /// <summary>How long an <see cref="IsStopped"/> answer is reused.</summary>
    public const long StateCacheMs = 250;

    /// <summary>How long a path answer is reused.</summary>
    public const long PathCacheMs = 60_000;

    private readonly CompanionPlugins companions;
    private readonly IPluginLog log;
    private readonly ICallGateSubscriber<uint, bool>? contentHasPath;
    private readonly ICallGateSubscriber<uint, int, bool, object>? run;
    private readonly ICallGateSubscriber<object>? stop;
    private readonly ICallGateSubscriber<bool>? isStopped;
    private readonly ICallGateSubscriber<object, bool>? pushOverrides;
    private readonly ICallGateSubscriber<bool>? popOverrides;

    private readonly Dictionary<uint, bool?> paths = [];
    private int pathsGeneration = -1;
    private long pathsReadAt;

    private bool stoppedCached = true;
    private long? stoppedCheckedAt;
    private bool warned;

    public AutoDutyIpc(IDalamudPluginInterface pluginInterface, CompanionPlugins companions, IPluginLog log)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        this.companions = companions ?? throw new ArgumentNullException(nameof(companions));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        try
        {
            contentHasPath = pluginInterface.GetIpcSubscriber<uint, bool>(ContentHasPathGate);
            run = pluginInterface.GetIpcSubscriber<uint, int, bool, object>(RunGate);
            stop = pluginInterface.GetIpcSubscriber<object>(StopGate);
            isStopped = pluginInterface.GetIpcSubscriber<bool>(IsStoppedGate);
            pushOverrides = pluginInterface.GetIpcSubscriber<object, bool>(PushConfigOverridesGate);
            popOverrides = pluginInterface.GetIpcSubscriber<bool>(PopConfigOverridesGate);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "AutoDuty IPC subscribers unavailable");
        }
    }

    /// <summary>AutoDuty is loaded and recent enough (the registry's answer) and its gates could be subscribed.</summary>
    public bool Available => run is not null && companions.IsLoaded(CompanionPlugin.AutoDuty);

    /// <summary>The registry generation the path cache follows; a caller caching per quest compares it too.</summary>
    public int Generation => companions.Generation;

    /// <summary>
    /// AutoDuty has a path for the territory; null when it cannot be asked. Cached per territory until the plugin list
    /// changes or <see cref="PathCacheMs"/> passes.
    /// </summary>
    public bool? HasPath(uint territoryType)
    {
        if (territoryType == 0 || !Available || contentHasPath is null)
        {
            return null;
        }

        var now = Environment.TickCount64;
        if (pathsGeneration != companions.Generation || now - pathsReadAt > PathCacheMs)
        {
            paths.Clear();
            pathsGeneration = companions.Generation;
            pathsReadAt = now;
        }

        if (paths.TryGetValue(territoryType, out var cached))
        {
            return cached;
        }

        bool? answer;
        try
        {
            answer = contentHasPath.InvokeFunc(territoryType);
        }
        catch (IpcNotReadyError)
        {
            answer = null;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "AutoDuty.ContentHasPath failed");
            answer = null;
        }

        paths[territoryType] = answer;
        return answer;
    }

    /// <summary>
    /// True while AutoDuty is stopped, or cannot be asked. Cached for <see cref="StateCacheMs"/>, so a per-frame read
    /// costs at most a few IPC calls a second.
    /// </summary>
    public bool IsStopped
    {
        get
        {
            if (!Available || isStopped is null)
            {
                stoppedCheckedAt = null;
                return true;
            }

            var now = Environment.TickCount64;
            if (stoppedCheckedAt is { } at && now - at < StateCacheMs)
            {
                return stoppedCached;
            }

            stoppedCached = QueryStopped();
            stoppedCheckedAt = now;
            return stoppedCached;
        }
    }

    /// <summary>
    /// Runs the duty once in <paramref name="mode"/>: pushes the mode as a temporary AutoDuty setting, then calls Run
    /// for one bare loop. Nothing runs when the mode is refused; a call that leaves AutoDuty stopped pops the override.
    /// </summary>
    public AutoDutyStart Run(uint territoryType, AutoDutyMode mode)
    {
        var settings = AutoDutyPlan.Overrides(mode);
        if (territoryType == 0 || !Available || run is null || pushOverrides is null || settings.Count == 0)
        {
            return AutoDutyStart.Unavailable;
        }

        var value = AutoDutyPlan.SettingValue(mode) ?? string.Empty;
        try
        {
            // Insertion order is the order AutoDuty applies them in.
            var overrides = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, setting) in settings)
            {
                overrides[key] = setting;
            }

            if (!pushOverrides.InvokeFunc(overrides))
            {
                log.Information("AutoDuty refused the duty mode {Mode}; nothing was run", value);
                return AutoDutyStart.ModeRefused;
            }

            // Loops 0 leaves AutoDuty's loop count alone (the override holds it at 1); bare mode skips its pre-loop,
            // between-loop and termination actions.
            run.InvokeAction(territoryType, 0, true);
            stoppedCheckedAt = null;
            if (QueryStopped())
            {
                Pop();
                log.Information("AutoDuty stayed stopped after Run for territory {Territory}", territoryType);
                return AutoDutyStart.NotStarted;
            }

            log.Information("AutoDuty started territory {Territory} in {Mode}", territoryType, value);
            return AutoDutyStart.Started;
        }
        catch (IpcNotReadyError)
        {
            return AutoDutyStart.Unavailable;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "AutoDuty.Run failed");
            return AutoDutyStart.Unavailable;
        }
    }

    /// <summary>Asks AutoDuty to stop (it restores its settings as it does). False when it could not be asked.</summary>
    public bool Stop()
    {
        if (!Available || stop is null)
        {
            return false;
        }

        try
        {
            stop.InvokeAction();
            stoppedCheckedAt = null;
            return true;
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "AutoDuty.Stop failed");
            return false;
        }
    }

    /// <summary>
    /// Whether the logged-in character has the InstanceContent row unlocked (the game's own
    /// <c>UIState.IsInstanceContentUnlocked</c>, as the Duty Finder hint reads it); null when it cannot be read.
    /// Framework thread only.
    /// </summary>
    public static bool? IsInstanceUnlocked(uint instanceContentId)
    {
        if (instanceContentId == 0)
        {
            return null;
        }

        try
        {
            return UIState.IsInstanceContentUnlocked(instanceContentId);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private bool QueryStopped()
    {
        if (isStopped is null)
        {
            return true;
        }

        try
        {
            return isStopped.InvokeFunc();
        }
        catch (IpcNotReadyError)
        {
            return true;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "AutoDuty.IsStopped failed");
            return true;
        }
    }

    private void Pop()
    {
        try
        {
            popOverrides?.InvokeFunc();
        }
        catch (Exception ex)
        {
            log.Debug(ex, "AutoDuty.PopConfigOverrides failed");
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
