using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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
/// Reads are lazy and happen on change, on the framework tick (<see cref="Tick"/>), never in a draw: a caller asking
/// for <see cref="Setups"/> gets the last read at once and marks the settings as wanted; the tick then reads them again
/// when one is due. A read is due after a companion's file in <c>pluginConfigs</c> changed (a watcher on the folder,
/// <see cref="CompanionSetupCatalog.Concerns"/>) and then stayed quiet for <see cref="SettleMs"/> (a plugin that saves
/// several times in a row, or through a temporary file, costs one read and is never read half-written), after Dalamud's
/// plugin list changed or an apply, and otherwise at most every <see cref="RefreshMs"/> as a backstop for the settings
/// read through IPC (every <see cref="UnwatchedRefreshMs"/> when the folder cannot be watched). Nothing is read while
/// nobody asks. Only the very first ask reads at once, so it never answers with nothing, and a decision about to act
/// on the settings (a stop, a hand-off) reads at once when they may be out of date (<see cref="ReadNowIfStale"/>). Each file is checked once per
/// read and parsed again only when its write time moved, and <see cref="Version"/> moves only when something read
/// differently, so the reasons composed from it are not built again. A per-frame caller (a hand-off button's disabled
/// reason) costs a field read. Framework thread only, apart from the watcher's flag.
/// </para>
/// </summary>
public sealed class CompanionSetupService : IDisposable
{
    /// <summary>How long a read is reused while the folder is watched: the backstop for the settings read through IPC.</summary>
    public const long RefreshMs = 30_000;

    /// <summary>How long a read is reused when the folder cannot be watched (1.10 and earlier: always).</summary>
    public const long UnwatchedRefreshMs = 5_000;

    /// <summary>How long a companion's file must stay unchanged before it is read again after the watcher saw it change.</summary>
    public const long SettleMs = 300;

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

    // The files checked in the read under way (null: not there), so a file several settings live in is stat'ed once.
    private readonly Dictionary<string, CachedFile?> checkedThisRead = new(StringComparer.OrdinalIgnoreCase);
    private readonly FileSystemWatcher? watcher;

    private IReadOnlyList<PluginSetup> setups = [];
    private CompanionSetupSummary summary = new([], [], 0);
    private long readAt;
    private int readGeneration = -1;
    private int commandVersion = -1;
    private string? commandAfterStop;

    // Raised by the folder watcher, which runs on a worker thread; consumed on the next read.
    private volatile bool stale = true;

    // When the watcher last saw a companion's file change (Environment.TickCount64); written on its thread.
    private long changedAt;

    // Someone read the settings since the last read: the tick reads again only for them.
    private bool asked;

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

        watcher = Watch(configDirectory);
    }

    /// <summary>Moves whenever the settings were read again, so a cached answer (a composed reason) knows to ask again.</summary>
    public int Version { get; private set; }

    /// <summary>
    /// <see cref="Version"/> of the last read, and a request for a new one: the next <see cref="Tick"/> reads again when
    /// a file changed or the read is older than <see cref="RefreshMs"/>.
    /// </summary>
    public int FreshVersion
    {
        get
        {
            Ask();
            return Version;
        }
    }

    /// <summary>Every companion's setup, in <see cref="CompanionCatalog.All"/> order, as last read.</summary>
    public IReadOnlyList<PluginSetup> Setups
    {
        get
        {
            Ask();
            return setups;
        }
    }

    /// <summary>The line at the top of Companion plugins.</summary>
    public CompanionSetupSummary Summary
    {
        get
        {
            Ask();
            return summary;
        }
    }

    /// <summary>How long the last read took, in milliseconds (the 13 IPC getters and the companions' files).</summary>
    public double LastReadMs { get; private set; }

    /// <summary>How many reads were made since the plugin loaded.</summary>
    public int Reads { get; private set; }

    /// <summary>
    /// Framework tick: reads the settings again when someone asked for them since the last read and a read is due (a
    /// file changed and settled, the plugin list changed, <see cref="Invalidate"/>, or the backstop interval passed).
    /// </summary>
    public void Tick()
    {
        var due = SetupReadSchedule.IsDue(
            asked,
            readGeneration != companions.Generation,
            stale,
            Environment.TickCount64,
            Interlocked.Read(ref changedAt),
            readAt,
            watcher is null ? UnwatchedRefreshMs : RefreshMs,
            SettleMs);
        if (due)
        {
            Refresh();
        }
    }

    /// <summary>
    /// For a decision about to act on the settings (a Questionable stop deciding whether to ask first, a hand-off):
    /// reads them again at once when what was last read may be out of date (never read, the plugin list changed, a
    /// companion's file changed, invalidated, or older than the backstop), without waiting for a changed file to settle
    /// or for the tick, so the decision never acts on a stale read. Otherwise the last read stands. Framework thread.
    /// </summary>
    public void ReadNowIfStale()
    {
        var due = SetupReadSchedule.IsDueForDecision(
            Reads == 0,
            readGeneration != companions.Generation,
            stale,
            Environment.TickCount64,
            readAt,
            watcher is null ? UnwatchedRefreshMs : RefreshMs);
        if (due)
        {
            Refresh();
        }
    }

    /// <summary>
    /// <see cref="QuestionableCommandAfterStop"/> for the stop about to happen: the settings are read again first when
    /// they may be out of date (<see cref="ReadNowIfStale"/>), so a stop never skips its confirmation on an old read.
    /// </summary>
    public string? QuestionableCommandAfterStopNow()
    {
        ReadNowIfStale();
        return QuestionableCommandAfterStop();
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
    /// and Esc), so Tsukimichi's Stop says so. Read again whenever the settings are.
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

    /// <summary>Reads everything again on the next tick (the Setup list's Refresh, or after an apply), without waiting for a file to settle.</summary>
    public void Invalidate()
    {
        Interlocked.Exchange(ref changedAt, 0);
        stale = true;
        asked = true;
    }

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

        // Read again, now: something may have changed while the confirmation was open.
        Refresh();
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
                log.Information("Set {Plugin} setting {Key} to {Value} (Settings › Automation › Apply recommended settings)", plugin, key, value);
            }
            else
            {
                failed++;
            }
        }

        Invalidate();
        return (applied, failed);
    }

    /// <summary>Stops watching the folder and releases the parsed configuration files.</summary>
    public void Dispose()
    {
        if (watcher is not null)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Changed -= OnFileEvent;
            watcher.Created -= OnFileEvent;
            watcher.Deleted -= OnFileEvent;
            watcher.Renamed -= OnFileRenamed;
            watcher.Error -= OnWatchError;
            watcher.Dispose();
        }

        foreach (var file in files.Values)
        {
            file.Document?.Dispose();
        }

        files.Clear();
    }

    /// <summary>A reader wants the settings: the very first ask reads at once, any later one waits for the tick.</summary>
    private void Ask()
    {
        if (Reads == 0)
        {
            Refresh();
            return;
        }

        asked = true;
    }

    private void Refresh()
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var now = Environment.TickCount64;
        var all = companions.All;
        stale = false;
        asked = false;
        readAt = now;
        readGeneration = companions.Generation;
        checkedThisRead.Clear();
        var result = new PluginSetup[all.Count];
        for (var i = 0; i < all.Count; i++)
        {
            var status = all[i];
            result[i] = CompanionSetupEvaluator.Evaluate(status, CompanionSetupCatalog.For(status.Plugin), requirement => Read(status, requirement));
        }

        // The command after stop sits beside a setting, not in one: read it again after every read, changed or not.
        commandVersion = -1;
        var read = CompanionSetupEvaluator.ApplyCoverage(result);
        Reads++;
        var changed = Version == 0 || !CompanionSetupEvaluator.Same(setups, read);
        if (changed)
        {
            setups = read;
            summary = CompanionSetupEvaluator.Summarize(setups);
            Version++;
        }

        // Otherwise nothing read differently: the version stays, so the reasons and notes composed from it stay.
        LastReadMs = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        log.Debug("Companion settings read {Reads} in {Ms:F1} ms on the framework tick ({Changed})", Reads, LastReadMs, changed ? "changed" : "unchanged");
    }

    /// <summary>
    /// Watches <c>pluginConfigs</c> (its subfolders too: Lifestream and AutoDuty keep theirs in one) for the companions'
    /// files; null when it cannot, and the reads then fall back to <see cref="UnwatchedRefreshMs"/>.
    /// </summary>
    private FileSystemWatcher? Watch(string? directory)
    {
        if (directory is null)
        {
            return null;
        }

        try
        {
            var folder = new FileSystemWatcher(directory)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                InternalBufferSize = 64 * 1024,
            };
            folder.Changed += OnFileEvent;
            folder.Created += OnFileEvent;
            folder.Deleted += OnFileEvent;
            folder.Renamed += OnFileRenamed;
            folder.Error += OnWatchError;
            folder.EnableRaisingEvents = true;
            return folder;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Plugin configuration folder cannot be watched; companion settings are read again every {Seconds} s", UnwatchedRefreshMs / 1000);
            return null;
        }
    }

    // The watcher's events arrive on a worker thread: they only stamp the change and raise the flag the tick consumes
    // once the file has been quiet for SettleMs.
    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        if (e.Name is { } name && CompanionSetupCatalog.Concerns(name))
        {
            MarkChanged();
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        // Plugins that save through a temporary file land their settings with a rename.
        if ((e.Name is { } name && CompanionSetupCatalog.Concerns(name)) || (e.OldName is { } old && CompanionSetupCatalog.Concerns(old)))
        {
            MarkChanged();
        }
    }

    // Too many changes at once (the buffer overflowed) or the folder went away: read again rather than miss one.
    private void OnWatchError(object sender, ErrorEventArgs e) => MarkChanged();

    private void MarkChanged()
    {
        Interlocked.Exchange(ref changedAt, Environment.TickCount64);
        stale = true;
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
        if (checkedThisRead.TryGetValue(full, out var seen))
        {
            // Checked already in this read (another setting of the same file): no second stat.
            return seen is null ? SetupReading.Missing
                : seen.Document is { } parsed ? CompanionConfigJson.Read(parsed.RootElement, path)
                : SetupReading.Unread;
        }

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

                checkedThisRead[full] = null;
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

            checkedThisRead[full] = cached;
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
