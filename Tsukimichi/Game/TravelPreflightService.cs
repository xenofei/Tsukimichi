using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Config;
using Dalamud.Interface;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Travel;

namespace Tsukimichi.Game;

/// <summary>
/// The game side of the travel preflight (feature plan v7 A9): reads what <see cref="TravelPreflight"/> decides on and
/// runs its fixes, each only from an explicit button in Setup (Settings › Automation, under the companion plugins), each
/// with Undo where it changes a setting (the spec-1.18 rule: one setting per fix, undoable, nothing on its own).
/// <list type="bullet">
/// <item>Movement type: the game's <c>MoveMode</c> option (Dalamud <see cref="IGameConfig"/>, UiControl section; 0
/// Standard, 1 Legacy). "Switch to Standard" sets it on the framework thread and keeps the old value for Undo, which
/// restores it only for the character it was changed on (the option is kept per character; a logout or another
/// character drops it) and only while the option still reads what the fix set.</item>
/// <item>Camera: the game camera's zoom mode (FFXIVClientStructs <c>CameraManager.Instance()->Camera->ZoomMode</c>, a
/// read of game memory, so only while the shared <see cref="HookGate"/> allows game calls).</item>
/// <item>vnavmesh's movement switch: <c>vnavmesh.Path.GetMovementAllowed</c>, turned back on with
/// <c>Path.SetMovementAllowed(true)</c> (<see cref="VnavmeshIpc"/>); Undo pauses it again.</item>
/// <item>Known conflicts: <see cref="TravelPreflight.KnownConflicts"/> loaded, from Dalamud's plugin list
/// (<see cref="PluginPresence"/>, refreshed when the list changes). The fix opens Dalamud's plugin installer: Tsukimichi
/// never turns another plugin off.</item>
/// </list>
/// Reads are cached for <see cref="ReadCacheMs"/>, so Setup and the walk-start check cost a few reads a second at most.
/// While logged out the game settings and the camera read as unread.
/// </summary>
public sealed class TravelPreflightService : IDisposable
{
    /// <summary>How long one reading is reused before the game and vnavmesh are asked again.</summary>
    public const long ReadCacheMs = 500;

    /// <summary>vnavmesh's switch as a <see cref="PreflightChange"/> value.</summary>
    private const uint Paused = 0;

    private const uint Allowed = 1;

    /// <summary>How long Undo is offered after "Allow movement" (spec-1.18: "Undo for 8 s"); "Restore Legacy" stays.</summary>
    public const long UndoWindowMs = 8_000;

    private readonly IGameConfig gameConfig;
    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly VnavmeshIpc vnavmesh;
    private readonly IPluginLog log;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly PluginPresence[] conflicts;
    private readonly Dictionary<PreflightItem, PreflightChange> changes = [];
    private readonly Dictionary<PreflightItem, long> changedAt = [];
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
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        conflicts = TravelPreflight.KnownConflicts.Select(c => new PluginPresence(pluginInterface, log, c.InternalName)).ToArray();
        clientState.Logout += OnLogout;
    }

    /// <summary>The shared addon kill switch; the camera is read only while it allows game calls. Unset reads the camera as unread.</summary>
    public HookGate? Gate { get; set; }

    /// <summary>
    /// The content id of the character logged in, null while none is; set by the plugin. A fix is kept for that
    /// character's Undo only (the movement type is a per-character setting); unset, no Undo is offered.
    /// </summary>
    public Func<ulong?>? LiveContentId { get; set; }

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

    /// <summary>
    /// True while Undo would put back what the item's fix changed: the same character is logged in and the setting still
    /// reads what the fix set. A change is kept until Undo, a logout or another character, or the plugin unloads.
    /// </summary>
    public bool CanUndo(PreflightItem item)
    {
        Refresh();
        if (!changes.TryGetValue(item, out var change))
        {
            return false;
        }

        var who = CurrentCharacter();
        if (who != change.ContentId)
        {
            // Another character, or none: the change was the last one's, and is dropped.
            changes.Remove(item);
            changedAt.Remove(item);
            return false;
        }

        if (!change.CanUndo(Current(item), who))
        {
            return false;
        }

        // The movement type's "Restore Legacy" stays while that character is logged in; vnavmesh's Undo lasts a few seconds.
        return item == PreflightItem.MovementType
            || (changedAt.TryGetValue(item, out var at) && Environment.TickCount64 - at < UndoWindowMs);
    }

    /// <summary>Runs the fix of <paramref name="item"/>, from its button; false when it did nothing.</summary>
    public bool Fix(PreflightItem item)
    {
        readAt = null;
        Refresh();
        switch (item)
        {
            case PreflightItem.MovementType when reading.MoveMode is { } before && before != TravelPreflight.StandardMoveMode:
                return Change(item, before, TravelPreflight.StandardMoveMode);
            case PreflightItem.VnavmeshMovement when reading.VnavmeshMovementAllowed == false:
                return Change(item, Paused, Allowed);
            case PreflightItem.Conflicts:
                return OpenInstaller();
            default:
                return false;
        }
    }

    /// <summary>Puts back what the item's fix changed, while the setting still reads what the fix set; false otherwise.</summary>
    public bool Undo(PreflightItem item)
    {
        readAt = null;
        if (!CanUndo(item) || !changes.TryGetValue(item, out var change))
        {
            return false;
        }

        if (!Set(item, change.Before))
        {
            return false;
        }

        changes.Remove(item);
        changedAt.Remove(item);
        log.Information("Travel preflight: {Item} put back to {Before}", item, change.Before);
        return true;
    }

    private bool Change(PreflightItem item, uint before, uint after)
    {
        if (!Set(item, after))
        {
            return false;
        }

        if (CurrentCharacter() is not { } who)
        {
            // No character to tie the Undo to: the fix stands, with no Undo.
            changes.Remove(item);
            changedAt.Remove(item);
            log.Information("Travel preflight: {Item} changed from {Before} to {After} (no character, no Undo)", item, before, after);
            return true;
        }

        changes[item] = new PreflightChange(item, before, after, who);
        changedAt[item] = Environment.TickCount64;
        log.Information("Travel preflight: {Item} changed from {Before} to {After}", item, before, after);
        return true;
    }

    private bool Set(PreflightItem item, uint value)
    {
        readAt = null;
        return item switch
        {
            PreflightItem.MovementType => SetMoveMode(value),
            PreflightItem.VnavmeshMovement => vnavmesh.SetMovementAllowed(value == Allowed),
            _ => false,
        };
    }

    /// <summary>The item's setting as a number for <see cref="PreflightChange"/>: the movement type, or vnavmesh's switch (1 allowed).</summary>
    private uint? Current(PreflightItem item) => item switch
    {
        PreflightItem.MovementType => reading.MoveMode,
        PreflightItem.VnavmeshMovement => reading.VnavmeshMovementAllowed is { } allowed ? (allowed ? Allowed : Paused) : null,
        _ => null,
    };

    /// <summary>Dalamud's plugin installer at the installed plugins, searching the first loaded conflict: the player turns it off there.</summary>
    private bool OpenInstaller()
    {
        try
        {
            var name = reading.Conflicts.Count > 0 ? reading.Conflicts[0].InternalName : null;
            return pluginInterface.OpenPluginInstallerTo(PluginInstallerOpenKind.InstalledPlugins, name);
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Plugin installer could not be opened");
            return false;
        }
    }

    /// <summary>The character logged in, or null; 0 reads as none.</summary>
    private ulong? CurrentCharacter()
    {
        if (!clientState.IsLoggedIn)
        {
            return null;
        }

        try
        {
            return LiveContentId?.Invoke() is { } id and not 0 ? id : null;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Character unavailable");
            return null;
        }
    }

    /// <summary>A logout ends every Undo: the changes were the character's who left.</summary>
    private void OnLogout(int type, int code)
    {
        changes.Clear();
        changedAt.Clear();
    }

    public void Dispose()
    {
        clientState.Logout -= OnLogout;
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
