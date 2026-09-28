using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;

namespace Tsukimichi.Game;

/// <summary>
/// Teleport through Lifestream's IPC (<c>Lifestream.Teleport(uint aetheryteId, byte subIndex) -> bool</c>,
/// <c>Lifestream.IsBusy() -> bool</c>). <see cref="Available"/> is answered from Dalamud's plugin list, cached and
/// refreshed whenever the list changes, so the UI can hide the teleport item without an IPC call per frame. Every
/// call is wrapped: a gate that is not ready or throws reads as unavailable / not busy / failed, and the first
/// failure is logged once.
/// </summary>
public sealed class LifestreamIpc : IDisposable
{
    public const string PluginInternalName = "Lifestream";
    private const string TeleportGate = "Lifestream.Teleport";
    private const string IsBusyGate = "Lifestream.IsBusy";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;
    private readonly ICallGateSubscriber<uint, byte, bool>? teleport;
    private readonly ICallGateSubscriber<bool>? isBusy;

    private bool? available;
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

    /// <summary>True while Lifestream is executing a task; false when it is idle or cannot be asked.</summary>
    public bool IsBusy
    {
        get
        {
            if (!Available || isBusy is null)
            {
                return false;
            }

            try
            {
                return isBusy.InvokeFunc();
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
