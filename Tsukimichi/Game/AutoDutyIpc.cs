using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Companions;

namespace Tsukimichi.Game;

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
/// a quest's single clear. Whenever the overrides were pushed and AutoDuty is still stopped afterwards (Run refused or
/// threw, or AutoDuty cannot be asked) they are popped again (<see cref="AutoDutyRunSteps"/>).
/// </para>
/// <para>
/// AutoDuty's leveling mode (a runtime choice, not a setting) makes its <c>Plugin.Run</c> ignore the territory it is
/// given. It needs no handling here: the <c>Meta.AutoDutyModeEnum</c> and <c>Meta.DutyModeEnum</c> setters reset
/// <c>Plugin.LevelingModeEnum</c> to None (<c>ConfigurationProfileV2.MetaConfig</c>), and both the override push and the
/// IPC Run itself (which sets <c>AutoDutyModeEnum</c> to Looping before it calls <c>Plugin.Run</c>) go through them.
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

    // A run Run started, so /tsuki stop (1.11.0) stops it and not one the player started in AutoDuty's own window.
    private readonly HandOffClaim claim = new();

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

        // Insertion order is the order AutoDuty applies them in.
        var overrides = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, setting) in settings)
        {
            overrides[key] = setting;
        }

        var outcome = AutoDutyRunSteps.Run(
            () => pushOverrides.InvokeFunc(overrides),
            // Loops 0 leaves AutoDuty's loop count alone (the override holds it at 1); bare mode skips its pre-loop,
            // between-loop and termination actions.
            () => run.InvokeAction(territoryType, 0, true),
            () =>
            {
                stoppedCheckedAt = null;
                return QueryStopped();
            },
            Pop,
            ex =>
            {
                if (ex is not IpcNotReadyError)
                {
                    WarnOnce(ex, "AutoDuty.Run failed");
                }
            });

        switch (outcome)
        {
            case AutoDutyStart.ModeRefused:
                log.Information("AutoDuty refused the duty mode {Mode}; nothing was run", value);
                break;
            case AutoDutyStart.NotStarted:
                log.Information("AutoDuty stayed stopped after Run for territory {Territory}", territoryType);
                break;
            case AutoDutyStart.Unavailable:
                log.Information("AutoDuty could not be asked to run territory {Territory}; its settings were restored", territoryType);
                break;
            default:
                log.Information("AutoDuty started territory {Territory} in {Mode}", territoryType, value);
                claim.Claim(Environment.TickCount64);
                break;
        }

        return outcome;
    }

    /// <summary>True while a run Tsukimichi started may still be under way (<see cref="TrackHandOff"/> keeps it current).</summary>
    public bool HandOffClaimed => claim.Claimed;

    /// <summary>
    /// True while AutoDuty runs the duty Tsukimichi started; ends the claim once that run is over. Called each frame
    /// while <see cref="HandOffClaimed"/>, and by <c>/tsuki stop</c>.
    /// </summary>
    public bool TrackHandOff() => claim.Observe(!IsStopped, Environment.TickCount64);

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
            claim.Release();
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
