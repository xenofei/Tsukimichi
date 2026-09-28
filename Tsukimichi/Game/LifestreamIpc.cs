using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;

namespace Tsukimichi.Game;

/// <summary>
/// Teleport through Lifestream's IPC (<c>Lifestream.Teleport(uint aetheryteId, byte subIndex) -> bool</c>,
/// <c>Lifestream.IsBusy() -> bool</c>). <see cref="Available"/> is answered from Dalamud's plugin list, cached and
/// refreshed whenever the list changes, so the UI can hide the teleport item without an IPC call per frame;
/// <see cref="IsBusy"/> is cached for <see cref="IsBusyCacheMs"/> so a per-frame read costs at most a few IPC calls a
/// second. Every call is wrapped: a gate that is not ready or throws reads as unavailable / not busy / failed, and
/// the first failure is logged once.
/// </summary>
public sealed class LifestreamIpc : IDisposable
{
    public const string PluginInternalName = "Lifestream";
    private const string TeleportGate = "Lifestream.Teleport";
    private const string IsBusyGate = "Lifestream.IsBusy";

    /// <summary>How long an <see cref="IsBusy"/> answer is reused before Lifestream is asked again.</summary>
    public const long IsBusyCacheMs = 250;

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;
    private readonly ICallGateSubscriber<uint, byte, bool>? teleport;
    private readonly ICallGateSubscriber<bool>? isBusy;

    private bool? available;
    private bool busyCached;
    private long? busyCheckedAt;
    private bool warned;

    public LifestreamIpc(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.log = log ?? throw new ArgumentNullException(nameof(log));

        try
        {
            teleport = pluginInterface.GetIpcSubscriber<uint, byte, bool>(TeleportGate);
            isBusy = pluginInterface.GetIpcSubscriber<bool>(IsBusyGate);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Lifestream IPC subscribers unavailable");
            teleport = null;
            isBusy = null;
        }

        pluginInterface.ActivePluginsChanged += OnActivePluginsChanged;
    }

    public void Dispose()
    {
        pluginInterface.ActivePluginsChanged -= OnActivePluginsChanged;
    }

    /// <summary>True while Lifestream is installed and loaded. Cached; refreshed when Dalamud's plugin list changes.</summary>
    public bool Available
    {
        get
        {
            if (teleport is null)
            {
                return false;
            }

            available ??= IsLoaded();
            return available.Value;
        }
    }

    /// <summary>
    /// True while Lifestream is executing a task; false when it is idle or cannot be asked. The answer is cached for
    /// <see cref="IsBusyCacheMs"/> (by <see cref="Environment.TickCount64"/>) and refreshed on the first read after that.
    /// </summary>
    public bool IsBusy
    {
        get
        {
            if (!Available || isBusy is null)
            {
                busyCheckedAt = null;
                return false;
            }

            var now = Environment.TickCount64;
            if (busyCheckedAt is { } checkedAt && now - checkedAt < IsBusyCacheMs)
            {
                return busyCached;
            }

            busyCached = QueryBusy(isBusy);
            busyCheckedAt = now;
            return busyCached;
        }
    }

    private bool QueryBusy(ICallGateSubscriber<bool> gate)
    {
        try
        {
            return gate.InvokeFunc();
        }
        catch (IpcNotReadyError)
        {
            available = false;
            return false;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Lifestream.IsBusy failed");
            return false;
        }
    }

    /// <summary>Asks Lifestream to teleport to an Aetheryte sheet row. False when it refused, is absent or threw.</summary>
    public bool Teleport(uint aetheryteId)
    {
        if (aetheryteId == 0 || !Available || teleport is null)
        {
            return false;
        }

        try
        {
            var accepted = teleport.InvokeFunc(aetheryteId, 0);
            if (!accepted)
            {
                log.Debug("Lifestream declined teleport to aetheryte {AetheryteId}", aetheryteId);
            }

            // Lifestream is busy from now on; drop the cached idle answer so the next frame sees it.
            busyCheckedAt = null;
            return accepted;
        }
        catch (IpcNotReadyError)
        {
            available = false;
            return false;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Lifestream.Teleport failed");
            return false;
        }
    }

    private void OnActivePluginsChanged(IActivePluginsChangedEventArgs args) => available = null;

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
