using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Ipc;

namespace Tsukimichi.Game;

/// <summary>
/// Travel through Lifestream's IPC, the only way Tsukimichi teleports (feature plan v5, decision 2). The gates, as
/// Lifestream's <c>IPC/IPCProvider.cs</c> registers them through EzIPC ("Lifestream." + method name; checked against
/// NightmareXIV/Lifestream ef759e9, 2026-09-23):
/// <list type="bullet">
/// <item><c>Teleport(uint aetheryteId, byte subIndex) -> bool</c>: false when the player cannot teleport now (combat,
/// casting, animation lock: its TeleportService.CanTeleport) or the aetheryte is not attuned.</item>
/// <item><c>IsBusy() -> bool</c>, <c>Abort()</c>.</item>
/// <item><c>AethernetTeleportById(uint aetheryteSheetRow) -> bool</c> and <c>AethernetTeleportToFirmament() -> bool</c>:
/// false only while busy; the hop itself needs the player at an aetheryte or shard of that network (Lifestream walks
/// to one a few steps away), and otherwise ends with Lifestream's own chat error.</item>
/// <item><c>GetActiveAetheryte() -> uint</c>: the aetheryte or shard (Aetheryte sheet row) the player stands at, 0 when none.</item>
/// <item><c>ExecuteCommand(string arguments)</c>: runs <c>/li arguments</c>.</item>
/// </list>
/// <see cref="Available"/> is answered from Dalamud's plugin list, cached and refreshed whenever the list changes, so
/// the UI can name Lifestream on a disabled button without an IPC call per frame; <see cref="IsBusy"/> and
/// <see cref="ActiveAetheryte"/> are cached for <see cref="IsBusyCacheMs"/> so a per-frame read costs at most a few IPC
/// calls a second. Every call is wrapped: a gate that throws reads as not busy / failed, and the first failure is logged
/// once. A gate that is not registered is remembered per gate (<see cref="IpcGateHealth"/>) until the plugin list
/// changes: only a missing <c>Teleport</c> makes Lifestream unavailable; another missing gate only turns its own feature
/// off, so an older Lifestream without <c>GetActiveAetheryte</c> still teleports. An aethernet hop or <c>/li</c> command
/// Tsukimichi hands Lifestream is remembered while Lifestream works on it (<see cref="HandOffClaim"/>), so
/// <c>/tsuki stop</c> (1.11.0) aborts that task and not one the player started; a teleport is a cast, not a task, and is
/// not claimed.
/// </summary>
public sealed class LifestreamIpc : IDisposable
{
    public const string PluginInternalName = "Lifestream";
    private const string TeleportGate = "Lifestream.Teleport";
    private const string IsBusyGate = "Lifestream.IsBusy";
    private const string AbortGate = "Lifestream.Abort";
    private const string AethernetByIdGate = "Lifestream.AethernetTeleportById";
    private const string FirmamentGate = "Lifestream.AethernetTeleportToFirmament";
    private const string ActiveAetheryteGate = "Lifestream.GetActiveAetheryte";
    private const string ExecuteCommandGate = "Lifestream.ExecuteCommand";

    /// <summary>How long an <see cref="IsBusy"/> or <see cref="ActiveAetheryte"/> answer is reused before Lifestream is asked again.</summary>
    public const long IsBusyCacheMs = 250;

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;
    private readonly IpcGateHealth gates = new(TeleportGate);
    private readonly ICallGateSubscriber<uint, byte, bool>? teleport;
    private readonly ICallGateSubscriber<bool>? isBusy;
    private readonly ICallGateSubscriber<object>? abort;
    private readonly ICallGateSubscriber<uint, bool>? aethernetById;
    private readonly ICallGateSubscriber<bool>? firmament;
    private readonly ICallGateSubscriber<uint>? activeAetheryte;
    private readonly ICallGateSubscriber<string, object>? executeCommand;

    private readonly HandOffClaim claim = new();
    private bool? available;
    private bool busyCached;
    private long? busyCheckedAt;
    private uint activeCached;
    private long? activeCheckedAt;
    private bool warned;

    public LifestreamIpc(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.log = log ?? throw new ArgumentNullException(nameof(log));

        try
        {
            teleport = pluginInterface.GetIpcSubscriber<uint, byte, bool>(TeleportGate);
            isBusy = pluginInterface.GetIpcSubscriber<bool>(IsBusyGate);
            abort = pluginInterface.GetIpcSubscriber<object>(AbortGate);
            aethernetById = pluginInterface.GetIpcSubscriber<uint, bool>(AethernetByIdGate);
            firmament = pluginInterface.GetIpcSubscriber<bool>(FirmamentGate);
            activeAetheryte = pluginInterface.GetIpcSubscriber<uint>(ActiveAetheryteGate);
            executeCommand = pluginInterface.GetIpcSubscriber<string, object>(ExecuteCommandGate);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Lifestream IPC subscribers unavailable");
            teleport = null;
            isBusy = null;
            abort = null;
            aethernetById = null;
            firmament = null;
            activeAetheryte = null;
            executeCommand = null;
        }

        pluginInterface.ActivePluginsChanged += OnActivePluginsChanged;
    }

    public void Dispose()
    {
        pluginInterface.ActivePluginsChanged -= OnActivePluginsChanged;
    }

    /// <summary>
    /// True while Lifestream is installed and loaded and its <c>Teleport</c> gate is registered. Cached; refreshed when
    /// Dalamud's plugin list changes.
    /// </summary>
    public bool Available
    {
        get
        {
            if (teleport is null || gates.CoreMissing)
            {
                return false;
            }

            available ??= IsLoaded();
            return available.Value;
        }
    }

    /// <summary>
    /// True when Lifestream can say which aetheryte the player stands at (<see cref="ActiveAetheryte"/>); false while it
    /// is unavailable or its build lacks <c>GetActiveAetheryte</c>.
    /// </summary>
    public bool CanReadActiveAetheryte => Available && activeAetheryte is not null && !gates.IsMissing(ActiveAetheryteGate);

    /// <summary>
    /// True while Lifestream is executing a task; false when it is idle or cannot be asked. The answer is cached for
    /// <see cref="IsBusyCacheMs"/> (by <see cref="Environment.TickCount64"/>) and refreshed on the first read after that.
    /// </summary>
    public bool IsBusy
    {
        get
        {
            if (!Available || isBusy is null || gates.IsMissing(IsBusyGate))
            {
                busyCheckedAt = null;
                return false;
            }

            var now = Environment.TickCount64;
            if (busyCheckedAt is { } checkedAt && now - checkedAt < IsBusyCacheMs)
            {
                return busyCached;
            }

            busyCached = Invoke(isBusy, IsBusyGate, static gate => gate.InvokeFunc(), false);
            busyCheckedAt = now;
            return busyCached;
        }
    }

    /// <summary>
    /// The aetheryte or aethernet shard (Aetheryte sheet row) the player stands at as Lifestream sees it, 0 when none
    /// or when Lifestream cannot be asked. Cached like <see cref="IsBusy"/>.
    /// </summary>
    public uint ActiveAetheryte
    {
        get
        {
            if (!CanReadActiveAetheryte)
            {
                activeCheckedAt = null;
                return 0;
            }

            var now = Environment.TickCount64;
            if (activeCheckedAt is { } checkedAt && now - checkedAt < IsBusyCacheMs)
            {
                return activeCached;
            }

            activeCached = Invoke(activeAetheryte!, ActiveAetheryteGate, static gate => gate.InvokeFunc(), 0u);
            activeCheckedAt = now;
            return activeCached;
        }
    }

    /// <summary>Asks Lifestream to teleport to an Aetheryte sheet row. False when it refused, is absent or threw.</summary>
    public bool Teleport(uint aetheryteId)
    {
        if (aetheryteId == 0 || !Available || teleport is null)
        {
            return false;
        }

        var accepted = Invoke(teleport, TeleportGate, gate => gate.InvokeFunc(aetheryteId, 0), false);
        if (!accepted)
        {
            log.Debug("Lifestream declined teleport to aetheryte {AetheryteId}", aetheryteId);
        }

        // Lifestream is busy from now on; drop the cached idle answer so the next frame sees it.
        busyCheckedAt = null;
        return accepted;
    }

    /// <summary>
    /// Asks Lifestream for an aethernet hop to a shard (or city aetheryte) by Aetheryte sheet row. False when it
    /// refused (busy), is absent, lacks the gate or threw; true only means the hop was queued.
    /// </summary>
    public bool AethernetTeleport(uint aetheryteId)
    {
        if (aetheryteId == 0 || !Available || aethernetById is null || gates.IsMissing(AethernetByIdGate))
        {
            return false;
        }

        var accepted = Invoke(aethernetById, AethernetByIdGate, gate => gate.InvokeFunc(aetheryteId), false);
        busyCheckedAt = null;
        ClaimIf(accepted);
        return accepted;
    }

    /// <summary>Asks Lifestream for the aethernet hop from the Foundation to the Firmament. As <see cref="AethernetTeleport"/>.</summary>
    public bool AethernetTeleportToFirmament()
    {
        if (!Available || firmament is null || gates.IsMissing(FirmamentGate))
        {
            return false;
        }

        var accepted = Invoke(firmament, FirmamentGate, static gate => gate.InvokeFunc(), false);
        busyCheckedAt = null;
        ClaimIf(accepted);
        return accepted;
    }

    /// <summary>Runs <c>/li <paramref name="arguments"/></c> through Lifestream. False when it is absent, lacks the gate or the call threw.</summary>
    public bool ExecuteCommand(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments) || !Available || executeCommand is null || gates.IsMissing(ExecuteCommandGate))
        {
            return false;
        }

        var sent = Invoke(executeCommand, ExecuteCommandGate, gate =>
        {
            gate.InvokeAction(arguments);
            return true;
        }, false);
        busyCheckedAt = null;
        ClaimIf(sent);
        return sent;
    }

    /// <summary>True while a hop or command Tsukimichi handed off may still be under way (<see cref="TrackHandOff"/> keeps it current).</summary>
    public bool HandOffClaimed => claim.Claimed;

    /// <summary>
    /// True while Lifestream works on a task Tsukimichi handed it; ends the claim once that task is over. Called each
    /// frame while <see cref="HandOffClaimed"/>, and by <c>/tsuki stop</c>.
    /// </summary>
    public bool TrackHandOff() => claim.Observe(IsBusy, Environment.TickCount64);

    private void ClaimIf(bool accepted)
    {
        if (accepted)
        {
            claim.Claim(Environment.TickCount64);
        }
    }

    /// <summary>Stops Lifestream's running task, if any.</summary>
    public void Abort()
    {
        if (!Available || abort is null || gates.IsMissing(AbortGate))
        {
            return;
        }

        Invoke(abort, AbortGate, static gate =>
        {
            gate.InvokeAction();
            return true;
        }, false);
        busyCheckedAt = null;
        claim.Release();
    }

    /// <summary>
    /// Calls a gate; a gate that is not registered is remembered as missing (only <c>Teleport</c> takes Lifestream
    /// down with it), any other failure is logged once.
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
                log.Information("Lifestream does not offer {Gate}; {Effect}", name, name == TeleportGate ? "travel through it is off" : "that feature is off");
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
