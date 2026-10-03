using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Companions;
using Tsukimichi.Localization;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// The companion plugin registry (feature plan v5, decision 1): which of the plugins Tsukimichi works with
/// (<see cref="CompanionCatalog"/>) are loaded, turned off, outdated or missing, read from Dalamud's installed plugin
/// list by internal name and version and re-read after Dalamud's <c>ActivePluginsChanged</c>. Settings › Integrations
/// lists them; every button that hands work to one asks <see cref="DisabledReason(CompanionPlugin)"/> for its disabled
/// tooltip ("Needs Lifestream — see Settings › Integrations"), so a button never disappears for a missing plugin.
/// <para>
/// The IPC wrappers stay independent of it: each keeps its own gates and its own "loaded" check, and asks the registry
/// only for the reason text. The list is read lazily on the framework thread at most once per change (the event may
/// arrive on another thread and only raises a flag); every read in between is a field read, so a per-frame caller costs
/// nothing.
/// </para>
/// </summary>
public sealed class CompanionPlugins : IDisposable
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;
    private readonly List<InstalledPlugin> installed = [];

    private IReadOnlyList<CompanionStatus> statuses;
    private string?[] reasons;
    private int reasonsGeneration = -1;
    private int reasonsLanguage = -1;
    private int reasonsSetup = -1;
    private string?[]? notes;
    private bool disposed;
    private bool warned;

    // Raised by ActivePluginsChanged, which may arrive off the framework thread; consumed on the next read.
    private volatile bool dirty = true;

    public CompanionPlugins(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        statuses = CompanionResolver.ResolveAll([]);
        reasons = new string?[CompanionCatalog.All.Count];
        pluginInterface.ActivePluginsChanged += OnActivePluginsChanged;
        Current = this;
    }

    /// <summary>The registry the plugin created, for <see cref="DisabledReason(CompanionPlugin)"/>; null before load and after unload.</summary>
    public static CompanionPlugins? Current { get; private set; }

    /// <summary>Moves whenever Dalamud's plugin list changed and was read again, so a cached answer knows to ask again.</summary>
    public int Generation { get; private set; }

    /// <summary>Every companion in <see cref="CompanionCatalog.All"/> order (listed ones first).</summary>
    public IReadOnlyList<CompanionStatus> All
    {
        get
        {
            Refresh();
            return statuses;
        }
    }

    /// <summary>One companion's state.</summary>
    public CompanionStatus Status(CompanionPlugin plugin)
    {
        var all = All;
        for (var i = 0; i < all.Count; i++)
        {
            if (all[i].Plugin == plugin)
            {
                return all[i];
            }
        }

        throw new ArgumentOutOfRangeException(nameof(plugin), plugin, "Unknown companion plugin");
    }

    /// <summary>The companion is loaded and recent enough.</summary>
    public bool IsLoaded(CompanionPlugin plugin) => Status(plugin).IsLoaded;

    /// <summary>
    /// The companions' recommended settings (companion setup); set by the plugin. Null leaves the reasons to the
    /// installed list alone.
    /// </summary>
    public CompanionSetupService? Setup { get; set; }

    /// <summary>
    /// Why a button that needs <paramref name="plugin"/> is disabled, or null when the plugin is loaded and set up:
    /// "Needs Lifestream — see Settings › Integrations", "Lifestream is installed but turned off…", "Needs a newer
    /// AutoDuty…", and for a loaded plugin whose hand-off a setting blocks (<see cref="SetupImpact.Blocking"/>), "Questionable
    /// needs TextAdvance's quest accept on — see Settings › Integrations". Safe to call every frame from any button;
    /// before the plugin finished loading it names the plugin as missing.
    /// </summary>
    public static string? DisabledReason(CompanionPlugin plugin) =>
        Current is { } registry ? registry.ReasonFor(plugin) : ReasonText(CompanionResolver.Resolve(CompanionCatalog.Get(plugin), []));

    /// <summary>
    /// A note for an enabled hand-off button whose plugin's recommended settings are not all as recommended ("AutoDuty:
    /// 2 recommended settings are set otherwise — see Settings › Integrations"); null when none is, or nothing can tell.
    /// </summary>
    public static string? SetupNote(CompanionPlugin plugin) => Current?.NoteFor(plugin);

    /// <summary>
    /// Before a hand-off acts (a click, a command): reads the companions' settings again at once when the last read may
    /// be out of date (<see cref="CompanionSetupService.ReadNowIfStale"/>), so <see cref="DisabledReason(CompanionPlugin)"/>
    /// answers from the settings as they are now. Framework thread; does nothing before the plugin finished loading.
    /// </summary>
    public static void ReadSetupNow() => Current?.Setup?.ReadNowIfStale();

    /// <summary>The instance form of <see cref="DisabledReason(CompanionPlugin)"/>; the text is composed once per list change, settings read and language.</summary>
    public string? ReasonFor(CompanionPlugin plugin)
    {
        var all = All;

        var setupVersion = Setup?.FreshVersion ?? 0;
        if (reasonsGeneration != Generation || reasonsLanguage != Loc.Version || reasonsSetup != setupVersion)
        {
            notes ??= new string?[all.Count];
            for (var i = 0; i < all.Count; i++)
            {
                reasons[i] = ReasonText(all[i]) ?? (Setup?.BlockingFor(all[i].Plugin) is { } blocking ? BlockingText(all[i], blocking) : null);
                notes[i] = Setup is { } setup && all[i].IsLoaded ? NoteText(all[i], setup.Setups) : null;
            }

            reasonsGeneration = Generation;
            reasonsLanguage = Loc.Version;
            reasonsSetup = setupVersion;
        }

        for (var i = 0; i < all.Count; i++)
        {
            if (all[i].Plugin == plugin)
            {
                return reasons[i];
            }
        }

        return null;
    }

    /// <summary>The instance form of <see cref="SetupNote(CompanionPlugin)"/>.</summary>
    public string? NoteFor(CompanionPlugin plugin)
    {
        // Composes the notes with the reasons.
        ReasonFor(plugin);
        if (notes is null)
        {
            return null;
        }

        var all = All;
        for (var i = 0; i < all.Count && i < notes.Length; i++)
        {
            if (all[i].Plugin == plugin)
            {
                return notes[i];
            }
        }

        return null;
    }

    /// <summary>"Questionable needs TextAdvance's quest accept on — see Settings › Integrations".</summary>
    public static string BlockingText(CompanionStatus status, SetupResult blocking)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(blocking);
        return string.Format(CultureInfo.CurrentCulture, Strings.CompanionSetupBlockedFormat, status.DisplayName, Strings.CompanionSetupNeed(blocking.Requirement.Id));
    }

    /// <summary>How many settings that affect <paramref name="status"/>'s hand-off are set otherwise, as a note; null when none.</summary>
    private static string? NoteText(CompanionStatus status, IReadOnlyList<PluginSetup> setups)
    {
        var count = 0;
        foreach (var setup in setups)
        {
            foreach (var result in setup.Results)
            {
                if (result.Check == SetupCheck.NeedsChange && result.Requirement.HandOffs.Contains(status.Plugin))
                {
                    count++;
                }
            }
        }

        return count switch
        {
            0 => null,
            1 => string.Format(CultureInfo.CurrentCulture, Strings.CompanionSetupNoteOneFormat, status.DisplayName),
            _ => string.Format(CultureInfo.CurrentCulture, Strings.CompanionSetupNoteFormat, status.DisplayName, count),
        };
    }

    /// <summary>The disabled-button reason for a status; null when it is loaded.</summary>
    public static string? ReasonText(CompanionStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        var name = NeedName(status);
        return status.State switch
        {
            CompanionState.Loaded => null,
            CompanionState.Disabled => string.Format(CultureInfo.CurrentCulture, Strings.CompanionDisabledReasonFormat, status.DisplayName),
            CompanionState.Outdated => status.MinimumVersion is { } minimum
                ? string.Format(CultureInfo.CurrentCulture, Strings.CompanionOutdatedMinimumReasonFormat, status.DisplayName, minimum)
                : string.Format(CultureInfo.CurrentCulture, Strings.CompanionOutdatedReasonFormat, status.DisplayName),
            _ => string.Format(CultureInfo.CurrentCulture, Strings.CompanionMissingReasonFormat, name),
        };
    }

    /// <summary>"GatherBuddy or GatherBuddy Reborn" for a companion with several builds, else its name.</summary>
    public static string NeedName(CompanionStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        var variants = status.Definition.Variants;
        return variants.Count switch
        {
            1 => variants[0].DisplayName,
            2 => string.Format(CultureInfo.CurrentCulture, Strings.CompanionEitherFormat, variants[0].DisplayName, variants[1].DisplayName),
            _ => status.Definition.DisplayName,
        };
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        pluginInterface.ActivePluginsChanged -= OnActivePluginsChanged;
        if (ReferenceEquals(Current, this))
        {
            Current = null;
        }
    }

    private void OnActivePluginsChanged(IActivePluginsChangedEventArgs args) => dirty = true;

    private void Refresh()
    {
        if (!dirty || disposed)
        {
            return;
        }

        dirty = false;
        installed.Clear();
        try
        {
            foreach (var plugin in pluginInterface.InstalledPlugins)
            {
                installed.Add(new InstalledPlugin(plugin.InternalName, plugin.Version, plugin.IsLoaded, plugin.IsOutdated));
            }
        }
        catch (Exception ex)
        {
            if (!warned)
            {
                warned = true;
                log.Warning(ex, "Installed plugin list unavailable; companion plugins read as missing");
            }
        }

        statuses = CompanionResolver.ResolveAll(installed);
        Generation++;
    }
}
