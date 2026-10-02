using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Companions;

namespace Tsukimichi.Game;

/// <summary>
/// AutoDuty's own configuration gates (erdelf/AutoDuty <c>AutoDuty/IPC/IPCProvider.cs</c> at 39b9a87, release
/// 0.0.0.375): <c>AutoDuty.GetConfig(string key) -&gt; string</c>, the active profile's value as text ("True",
/// "Support"; "" when the key is unknown), and <c>AutoDuty.SetConfig(string key, object value)</c>, which converts the
/// text to the setting's type and saves the profile. Keys are dotted paths (<c>DutyConfig.AutoExitDuty</c>).
/// <para>
/// Set only from "Apply recommended settings", after the player confirmed the list, and never while AutoDuty runs: a
/// run holds Tsukimichi's temporary overrides, and AutoDuty saves nothing while overrides are held, so a change then
/// would last only until it stops. Each set is read back to see whether it took.
/// </para>
/// </summary>
public sealed class AutoDutySettingGates : ICompanionSettingGates
{
    public const string GetConfigGate = "AutoDuty.GetConfig";
    public const string SetConfigGate = "AutoDuty.SetConfig";

    private readonly AutoDutyIpc autoDuty;
    private readonly IPluginLog log;
    private readonly ICallGateSubscriber<string, string>? getConfig;
    private readonly ICallGateSubscriber<string, object, object>? setConfig;
    private bool warned;

    public AutoDutySettingGates(IDalamudPluginInterface pluginInterface, AutoDutyIpc autoDuty, IPluginLog log)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        this.autoDuty = autoDuty ?? throw new ArgumentNullException(nameof(autoDuty));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        try
        {
            getConfig = pluginInterface.GetIpcSubscriber<string, string>(GetConfigGate);
            setConfig = pluginInterface.GetIpcSubscriber<string, object, object>(SetConfigGate);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "AutoDuty configuration gates unavailable");
        }
    }

    public SetupReading Get(string key)
    {
        if (!autoDuty.Available || getConfig is null)
        {
            return SetupReading.Unread;
        }

        try
        {
            var value = getConfig.InvokeFunc(key);

            // AutoDuty answers "" for a key it does not know (a renamed setting): unknown, not a wrong value.
            return string.IsNullOrEmpty(value) ? SetupReading.Unread : SetupReading.Of(value);
        }
        catch (IpcNotReadyError)
        {
            return SetupReading.Unread;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "AutoDuty.GetConfig failed");
            return SetupReading.Unread;
        }
    }

    public bool CanSetNow => autoDuty.Available && setConfig is not null && autoDuty.IsStopped;

    public bool Set(string key, string value, string readKey)
    {
        if (!CanSetNow)
        {
            return false;
        }

        try
        {
            setConfig!.InvokeAction(key, value);
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "AutoDuty.SetConfig failed");
            return false;
        }

        // SetConfig answers nothing; read the value back.
        return Get(readKey) is { Read: true, Value: { } now } && string.Equals(now, value, StringComparison.OrdinalIgnoreCase);
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

/// <summary>
/// A plugin's bool setting gates, named <c>&lt;prefix&gt;&lt;key&gt;</c>: a getter <c>() -&gt; bool</c> and, where the
/// plugin has one, a setter <c>(bool)</c>. Used for TextAdvance (NightmareXIV/TextAdvance
/// <c>TextAdvance/Services/IPCProvider.cs</c> at 9dee627, EzIPC under the internal name: <c>GetEnableQuestAccept</c>,
/// <c>GetEnableQuestComplete</c>, <c>GetEnableTalkSkip</c>, each the value in force, and <c>IsPaused</c>; read only)
/// and vnavmesh (awgil/ffxiv_navmesh <c>vnavmesh/IPCProvider.cs</c> at 6fc8072: <c>Nav.IsAutoLoad</c> and
/// <c>Nav.SetAutoLoad(bool)</c>, which saves vnavmesh's configuration). A set is read back through the getter.
/// </summary>
public sealed class BoolSettingGates : ICompanionSettingGates
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly CompanionPlugins companions;
    private readonly CompanionPlugin plugin;
    private readonly string prefix;
    private readonly IPluginLog log;
    private readonly Dictionary<string, ICallGateSubscriber<bool>?> getters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ICallGateSubscriber<bool, object>?> setters = new(StringComparer.Ordinal);
    private bool warned;

    /// <param name="pluginInterface">For the gates.</param>
    /// <param name="companions">Whether the plugin is loaded.</param>
    /// <param name="plugin">Whose gates.</param>
    /// <param name="prefix">The gates' common prefix ("TextAdvance.", "vnavmesh.").</param>
    /// <param name="log">For the first failure.</param>
    public BoolSettingGates(IDalamudPluginInterface pluginInterface, CompanionPlugins companions, CompanionPlugin plugin, string prefix, IPluginLog log)
    {
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.companions = companions ?? throw new ArgumentNullException(nameof(companions));
        this.plugin = plugin;
        this.prefix = prefix ?? throw new ArgumentNullException(nameof(prefix));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public bool CanSetNow => companions.IsLoaded(plugin);

    public SetupReading Get(string key)
    {
        if (!companions.IsLoaded(plugin) || Gate(getters, key, static (pi, name) => pi.GetIpcSubscriber<bool>(name)) is not { } gate)
        {
            return SetupReading.Unread;
        }

        try
        {
            return SetupReading.Of(gate.InvokeFunc() ? "true" : "false");
        }
        catch (IpcNotReadyError)
        {
            return SetupReading.Unread;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, prefix + key + " failed");
            return SetupReading.Unread;
        }
    }

    public bool Set(string key, string value, string readKey)
    {
        if (!bool.TryParse(value, out var on)
            || !companions.IsLoaded(plugin)
            || Gate(setters, key, static (pi, name) => pi.GetIpcSubscriber<bool, object>(name)) is not { } gate)
        {
            return false;
        }

        try
        {
            gate.InvokeAction(on);
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, prefix + key + " failed");
            return false;
        }

        // The setter answers nothing (vnavmesh's Nav.SetAutoLoad); read the value back through the getter.
        return Get(readKey) is { Read: true, Value: { } now } && bool.TryParse(now, out var taken) && taken == on;
    }

    private T? Gate<T>(Dictionary<string, T?> cache, string key, Func<IDalamudPluginInterface, string, T> subscribe)
        where T : class
    {
        if (cache.TryGetValue(key, out var gate))
        {
            return gate;
        }

        try
        {
            gate = subscribe(pluginInterface, prefix + key);
        }
        catch (Exception ex)
        {
            WarnOnce(ex, prefix + key + " unavailable");
            gate = null;
        }

        cache[key] = gate;
        return gate;
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
