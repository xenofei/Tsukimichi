using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Companions;

namespace Tsukimichi.Game;

/// <summary>
/// The companion plugins' recommended settings (<see cref="CompanionSetupCatalog"/>), read and, on the player's
/// confirmed request, applied (companion setup). A setting is read from the plugin's own configuration file in
/// Dalamud's <c>pluginConfigs</c> folder (read only: Tsukimichi never writes another plugin's file) or through a getter
/// gate of its IPC (<see cref="ICompanionSettingGates"/>); it is set only through that plugin's own IPC setter, and only
/// the settings the Setup list names.
/// <para>
/// Reads are lazy: a caller asking for <see cref="Setups"/> re-reads at most every <see cref="RefreshMs"/> (or at once
/// after Dalamud's plugin list changed, or an apply); a file is parsed again only when its write time moved. So a
/// per-frame caller (a hand-off button's disabled reason) costs a field read between refreshes. Framework thread only.
/// </para>
/// </summary>
public sealed class CompanionSetupService : IDisposable
{
    /// <summary>How long a read is reused.</summary>
    public const long RefreshMs = 5_000;

    /// <summary>Questionable's "Run command after stop" in <see cref="CompanionSetupCatalog"/>.</summary>
    private const string CommandAfterStopId = "questionable.command-after-stop";

    /// <summary>The command it runs, beside that setting in Questionable's file.</summary>
    private const string CommandAfterStopPath = "Stop.CommandAfterStop";

    private readonly CompanionPlugins companions;
    private readonly IPluginLog log;
    private readonly string? configDirectory;
    private readonly Dictionary<CompanionPlugin, ICompanionSettingGates> gates = [];
    private readonly Dictionary<string, CachedFile> files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> warned = new(StringComparer.Ordinal);

    private IReadOnlyList<PluginSetup> setups = [];
    private CompanionSetupSummary summary = new([], [], 0);
    private long readAt;
    private int readGeneration = -1;
    private bool stale = true;
    private int commandVersion = -1;
    private string? commandAfterStop;

    public CompanionSetupService(IDalamudPluginInterface pluginInterface, CompanionPlugins companions, IPluginLog log)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        this.companions = companions ?? throw new ArgumentNullException(nameof(companions));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        try
        {
            // pluginConfigs/Tsukimichi.json: its folder holds every plugin's configuration.
            configDirectory = pluginInterface.ConfigFile.DirectoryName;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Plugin configuration folder unavailable; companion settings read as unknown");
        }
    }

    /// <summary>Moves whenever the settings were read again, so a cached answer (a composed reason) knows to ask again.</summary>
    public int Version { get; private set; }

    /// <summary><see cref="Version"/> after re-reading the settings when they are older than <see cref="RefreshMs"/>.</summary>
    public int FreshVersion
    {
        get
        {
            Refresh();
            return Version;
        }
    }

    /// <summary>Every companion's setup, in <see cref="CompanionCatalog.All"/> order.</summary>
    public IReadOnlyList<PluginSetup> Setups
    {
        get
        {
            Refresh();
            return setups;
        }
    }

    /// <summary>The line at the top of Companion plugins.</summary>
    public CompanionSetupSummary Summary
    {
        get
        {
            Refresh();
            return summary;
        }
    }

    /// <summary>One companion's setup.</summary>
    public PluginSetup For(CompanionPlugin plugin)
    {
        foreach (var setup in Setups)
        {
            if (setup.Plugin == plugin)
            {
                return setup;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(plugin), plugin, "Unknown companion plugin");
    }

    /// <summary>
    /// The command Questionable runs after it stops (its "Run command after stop", default <c>/li auto</c>), read from
    /// its file: null while that setting is off, unknown or Questionable is not loaded; empty when it is on but the
    /// command cannot be read. Questionable runs it on any stop asked over IPC (it exempts only its own window's stop
    /// and Esc), so Tsukimichi's Stop says so. Read again with the settings (<see cref="RefreshMs"/>).
    /// </summary>
    public string? QuestionableCommandAfterStop()
    {
        var version = FreshVersion;
        if (commandVersion == version)
        {
            return commandAfterStop;
        }

        commandVersion = version;
        commandAfterStop = null;
        var setup = For(CompanionPlugin.Questionable);
        foreach (var result in setup.Results)
        {
            if (!string.Equals(result.Requirement.Id, CommandAfterStopId, StringComparison.Ordinal))
            {
                continue;
            }

            if (result.Value is { } value && bool.TryParse(value.Trim(), out var on) && on)
            {
                var command = ReadFile(result.Requirement.File ?? setup.Status.Variant.InternalName + ".json", CommandAfterStopPath);
                commandAfterStop = command is { Read: true, Value: { } text } && !string.IsNullOrWhiteSpace(text) ? text.Trim() : string.Empty;
            }

            break;
        }

        return commandAfterStop;
    }

    /// <summary>The first blocking setting not as recommended that affects <paramref name="handOff"/>; null when none.</summary>
    public SetupResult? BlockingFor(CompanionPlugin handOff) => CompanionSetupEvaluator.BlockingFor(handOff, Setups);

    /// <summary>Registers the getter and setter gates of one plugin's IPC.</summary>
    public void UseGates(CompanionPlugin plugin, ICompanionSettingGates pluginGates)
    {
        gates[plugin] = pluginGates ?? throw new ArgumentNullException(nameof(pluginGates));
        stale = true;
    }

    /// <summary>The plugin has setter gates and they may be used now (AutoDuty refuses while it runs).</summary>
    public bool CanApplyNow(CompanionPlugin plugin) => gates.TryGetValue(plugin, out var pluginGates) && pluginGates.CanSetNow;

    /// <summary>Reads everything again on the next ask (the Setup list's Refresh, or after an apply).</summary>
    public void Invalidate() => stale = true;

    /// <summary>
    /// Sets the settings of <paramref name="plugin"/> the player confirmed (<paramref name="confirmed"/>, the ids the
    /// confirmation listed) that are still not as recommended when read again now
    /// (<see cref="PluginSetup.ApplicableOf"/>), through the plugin's own IPC, and nothing else. Each set is read back.
    /// Returns how many were set and how many the plugin refused, did not take or could not be asked about.
    /// </summary>
    public (int Applied, int Failed) Apply(CompanionPlugin plugin, IReadOnlyCollection<string> confirmed)
    {
        ArgumentNullException.ThrowIfNull(confirmed);
        var applied = 0;
        var failed = 0;

        // Read again: something may have changed while the confirmation was open.
        stale = true;
        var settable = For(plugin).ApplicableOf(confirmed);
        if (!gates.TryGetValue(plugin, out var pluginGates))
        {
            return (0, settable.Count);
        }

        foreach (var result in settable)
        {
            var requirement = result.Requirement;
            if (requirement.SetterKey is { } key && requirement.ApplyValue is { } value && requirement.Path is { } readKey && pluginGates.Set(key, value, readKey))
            {
                applied++;
                log.Information("Set {Plugin} setting {Key} to {Value} (Settings › Integrations › Apply recommended settings)", plugin, key, value);
            }
            else
            {
                failed++;
            }
        }

        stale = true;
        return (applied, failed);
    }

    /// <summary>Releases the parsed configuration files.</summary>
    public void Dispose()
    {
        foreach (var file in files.Values)
        {
            file.Document?.Dispose();
        }

        files.Clear();
    }

    private void Refresh()
    {
        var now = Environment.TickCount64;
        var all = companions.All;
        if (!stale && readGeneration == companions.Generation && now - readAt < RefreshMs)
        {
            return;
        }

        stale = false;
        readAt = now;
        readGeneration = companions.Generation;
        var result = new PluginSetup[all.Count];
        for (var i = 0; i < all.Count; i++)
        {
            var status = all[i];
            result[i] = CompanionSetupEvaluator.Evaluate(status, CompanionSetupCatalog.For(status.Plugin), requirement => Read(status, requirement));
        }

        setups = CompanionSetupEvaluator.ApplyCoverage(result);
        summary = CompanionSetupEvaluator.Summarize(setups);
        Version++;
    }

    private SetupReading Read(CompanionStatus status, SetupRequirement requirement)
    {
        switch (requirement.Source)
        {
            case SetupSource.Ipc when requirement.Path is { } key:
                return gates.TryGetValue(requirement.Plugin, out var pluginGates) ? pluginGates.Get(key) : SetupReading.Unread;
            case SetupSource.ConfigFile when requirement.Path is { } path:
                return ReadFile(requirement.File ?? status.Variant.InternalName + ".json", path);
            default:
                return SetupReading.Unread;
        }
    }

    private SetupReading ReadFile(string relative, string path)
    {
        if (configDirectory is null)
        {
            return SetupReading.Unread;
        }

        var full = Path.Combine(configDirectory, relative);
        try
        {
            var info = new FileInfo(full);
            if (!info.Exists)
            {
                // Never saved: the plugin runs on its defaults.
                if (files.Remove(full, out var gone))
                {
                    gone.Document?.Dispose();
                }

                return SetupReading.Missing;
            }

            if (!files.TryGetValue(full, out var cached) || cached.WrittenUtc != info.LastWriteTimeUtc || cached.Length != info.Length)
            {
                string text;
                using (var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream))
                {
                    text = reader.ReadToEnd();
                }

                cached?.Document?.Dispose();
                cached = new CachedFile(info.LastWriteTimeUtc, info.Length, CompanionConfigJson.Parse(text));
                files[full] = cached;
            }

            return cached.Document is { } document ? CompanionConfigJson.Read(document.RootElement, path) : SetupReading.Unread;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            if (warned.Add(full))
            {
                log.Warning(ex, "Could not read {File}; its settings read as unknown", relative);
            }

            return SetupReading.Unread;
        }
    }

    private sealed record CachedFile(DateTime WrittenUtc, long Length, System.Text.Json.JsonDocument? Document);
}

/// <summary>A plugin's own IPC for reading and setting its configuration, by key.</summary>
public interface ICompanionSettingGates
{
    /// <summary>The setting's value as text; <see cref="SetupReading.Unread"/> when the plugin cannot be asked.</summary>
    SetupReading Get(string key);

    /// <summary>
    /// Sets the setting through the plugin's own setter (<paramref name="key"/>), then reads it back through
    /// <paramref name="readKey"/> (the getter's key; the same for a plugin whose gates share one): true only when the
    /// value read back is <paramref name="value"/>. False when it refused, did not take or cannot be asked.
    /// </summary>
    bool Set(string key, string value, string readKey);

    /// <summary>A setting may be set now (AutoDuty: only while it is stopped).</summary>
    bool CanSetNow { get; }
}
