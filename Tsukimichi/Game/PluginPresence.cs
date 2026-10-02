using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Tsukimichi.Game;

/// <summary>
/// Whether one other plugin is installed and loaded, answered from Dalamud's plugin list and cached until the list
/// changes (<see cref="IDalamudPluginInterface.ActivePluginsChanged"/>, which may arrive off the framework thread; the
/// flag it raises is consumed on the next read), so a button can ask every frame. <see cref="Generation"/> moves with
/// each change, for caches that should be dropped when the plugin comes or goes.
/// </summary>
public sealed class PluginPresence : IDisposable
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;
    private readonly string internalName;
    private volatile bool dirty = true;
    private bool loaded;
    private bool disposed;

    public PluginPresence(IDalamudPluginInterface pluginInterface, IPluginLog log, string internalName)
    {
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        ArgumentException.ThrowIfNullOrWhiteSpace(internalName);
        this.internalName = internalName;
        pluginInterface.ActivePluginsChanged += OnActivePluginsChanged;
    }

    /// <summary>The plugin's internal name (its manifest's <c>InternalName</c>).</summary>
    public string InternalName => internalName;

    /// <summary>Moves whenever Dalamud's plugin list changed since the last read.</summary>
    public int Generation { get; private set; }

    /// <summary>True while the plugin is installed and loaded.</summary>
    public bool Loaded
    {
        get
        {
            if (dirty && !disposed)
            {
                dirty = false;
                loaded = IsLoaded();
                Generation++;
            }

            return loaded;
        }
    }

    /// <summary>Forgets the cached answer, as a plugin list change does (an IPC gate answered "not ready").</summary>
    public void Invalidate() => dirty = true;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        pluginInterface.ActivePluginsChanged -= OnActivePluginsChanged;
    }

    private void OnActivePluginsChanged(IActivePluginsChangedEventArgs args) => dirty = true;

    private bool IsLoaded()
    {
        try
        {
            foreach (var plugin in pluginInterface.InstalledPlugins)
            {
                if (plugin.IsLoaded && string.Equals(plugin.InternalName, internalName, StringComparison.OrdinalIgnoreCase))
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
}
