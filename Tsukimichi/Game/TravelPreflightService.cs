using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Config;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Travel;

namespace Tsukimichi.Game;

/// <summary>
/// The game side of the travel preflight (feature plan v7 A9): reads what <see cref="TravelPreflight"/> decides on and
/// runs its fixes, each only from an explicit button in Settings › Automation › Travel.
/// <list type="bullet">
/// <item>Movement type: the game's <c>MoveMode</c> option (Dalamud <see cref="IGameConfig"/>, UiControl section; 0
/// Standard, 1 Legacy). "Switch to Standard" sets it on the framework thread and keeps the old value for Undo, which
/// restores it only while the option still reads what the fix set.</item>
/// <item>Camera: the game camera's zoom mode (FFXIVClientStructs <c>CameraManager.Instance()->Camera->ZoomMode</c>, a
/// read of game memory, so only while the shared <see cref="HookGate"/> allows game calls).</item>
/// <item>vnavmesh's movement switch: <c>vnavmesh.Path.GetMovementAllowed</c>, turned back on with
/// <c>Path.SetMovementAllowed(true)</c> (<see cref="VnavmeshIpc"/>).</item>
/// <item>Known conflicts: <see cref="TravelPreflight.KnownConflicts"/> loaded, from Dalamud's plugin list
/// (<see cref="PluginPresence"/>, refreshed when the list changes).</item>
/// </list>
/// Reads are cached for <see cref="ReadCacheMs"/>, so Setup and the walk-start check cost a few reads a second at most.
/// While logged out the game settings and the camera read as unread.
/// </summary>
public sealed class TravelPreflightService : IDisposable
{
    /// <summary>How long one reading is reused before the game and vnavmesh are asked again.</summary>
    public const long ReadCacheMs = 500;

    private readonly IGameConfig gameConfig;
    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly VnavmeshIpc vnavmesh;
    private readonly IPluginLog log;
    private readonly PluginPresence[] conflicts;
    private IReadOnlyList<PreflightResult> results = TravelPreflight.Evaluate(TravelPreflightReading.Unread);
    private TravelPreflightReading reading = TravelPreflightReading.Unread;
    private long? readAt;
    private bool warned;

    public TravelPreflightService(IGameConfig gameConfig, IFramework framework, IClientState clientState, VnavmeshIpc vnavmesh, IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        this.gameConfig = gameConfig ?? throw new ArgumentNullException(nameof(gameConfig));
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
        this.vnavmesh = vnavmesh ?? throw new ArgumentNullException(nameof(vnavmesh));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        ArgumentNullException.ThrowIfNull(pluginInterface);
        conflicts = TravelPreflight.KnownConflicts.Select(c => new PluginPresence(pluginInterface, log, c.InternalName)).ToArray();
    }

    /// <summary>The shared addon kill switch; the camera is read only while it allows game calls. Unset reads the camera as unread.</summary>
    public HookGate? Gate { get; set; }

    /// <summary>The movement type change "Switch to Standard" made, for Undo; null before one or after Undo.</summary>
    public PreflightChange? LastChange { get; private set; }

    /// <summary>Every check as read now (cached for <see cref="ReadCacheMs"/>).</summary>
    public IReadOnlyList<PreflightResult> Results
    {
        get
        {
            Refresh();
            return results;
        }
    }

    /// <summary>The checks that make a walk run the wrong way or not move, for the chat line when one starts.</summary>
    public IReadOnlyList<PreflightItem> WalkWarnings()
    {
        readAt = null;
        return TravelPreflight.WalkWarnings(Results);
    }

    /// <summary>True while Undo would put the old movement type back: the option still reads what the switch set.</summary>
    public bool CanUndo
    {
        get
        {
            Refresh();
            return LastChange is { } change && change.CanUndo(reading.MoveMode);
        }
    }

    /// <summary>Sets the game's movement type to Standard (on the framework thread) and remembers the old value for Undo.</summary>
    public bool SwitchToStandardMovement()
    {
        Refresh();
        if (reading.MoveMode is not { } before || before == TravelPreflight.StandardMoveMode)
        {
            return false;
        }

        if (!SetMoveMode(TravelPreflight.StandardMoveMode))
        {
            return false;
        }

        LastChange = new PreflightChange(PreflightItem.MovementType, before, TravelPreflight.StandardMoveMode);
        log.Information("Travel preflight: movement type set to Standard (was {Before})", before);
        return true;
    }

    /// <summary>Puts the movement type back as it was before the switch, when it still reads Standard; false otherwise.</summary>
    public bool UndoMovement()
    {
        readAt = null;
        if (LastChange is not { } change || !CanUndo)
        {
            return false;
        }

        if (!SetMoveMode(change.Before))
        {
            return false;
        }

        LastChange = null;
        log.Information("Travel preflight: movement type put back to {Before}", change.Before);
        return true;
    }

    /// <summary>Turns vnavmesh's movement switch back on; false when vnavmesh could not be asked.</summary>
    public bool AllowVnavmeshMovement()
    {
        readAt = null;
        var done = vnavmesh.AllowMovement();
        log.Information("Travel preflight: vnavmesh movement allowed again ({Result})", done ? "done" : "failed");
        return done;
    }

    public void Dispose()
    {
        foreach (var presence in conflicts)
        {
            presence.Dispose();
        }
    }

    /// <summary>
    /// Sets the game's movement type on the framework thread: at once when called there (the settings window draws
    /// there), else queued, never waited on. False when it failed at once.
    /// </summary>
    private bool SetMoveMode(uint value)
    {
        readAt = null;
        try
        {
            var set = framework.RunOnFrameworkThread(() => gameConfig.Set(UiControlOption.MoveMode, value));
            if (set.IsFaulted)
            {
                WarnOnce(set.Exception!.GetBaseException(), "Movement type could not be set");
                return false;
            }

            if (!set.IsCompleted)
            {
                set.ContinueWith(t => WarnOnce(t.Exception!.GetBaseException(), "Movement type could not be set"), System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
            }

            return true;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Movement type could not be set");
            return false;
        }
    }

    private void Refresh()
    {
        var now = Environment.TickCount64;
        if (readAt is { } at && now - at < ReadCacheMs)
        {
            return;
        }

        readAt = now;
        var loggedIn = clientState.IsLoggedIn;
        var loaded = new List<KnownConflict>();
        for (var i = 0; i < conflicts.Length; i++)
        {
            if (conflicts[i].Loaded)
            {
                loaded.Add(TravelPreflight.KnownConflicts[i]);
            }
        }

        reading = new TravelPreflightReading(
            loggedIn ? ReadMoveMode() : null,
            loggedIn ? ReadFirstPerson() : null,
            vnavmesh.MovementAllowed,
            loaded);
        results = TravelPreflight.Evaluate(reading);
    }

    private uint? ReadMoveMode()
    {
        try
        {
            return gameConfig.TryGet(UiControlOption.MoveMode, out uint mode) ? mode : null;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Movement type unavailable");
            return null;
        }
    }

    private unsafe bool? ReadFirstPerson()
    {
        if (Gate is not { HooksAllowed: true })
        {
            return null;
        }

        try
        {
            var manager = CameraManager.Instance();
            if (manager == null || manager->Camera == null)
            {
                return null;
            }

            return manager->Camera->ZoomMode == CameraZoomMode.FirstPerson;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Camera unavailable");
            return null;
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
