using System;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Config;

/// <summary>
/// Plugin settings persisted by Dalamud (Newtonsoft under the hood). Window size and position are left to ImGui.
/// Load with <see cref="Load"/>, persist with <see cref="Save"/>.
/// </summary>
[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public const int CurrentVersion = 1;
    public const double MinPollIntervalSeconds = 0.5;
    public const double MaxPollIntervalSeconds = 5.0;
    public const int DefaultStalledDays = QueryContext.DefaultStalledDays;
    public const int MinStalledDays = 1;
    public const int MaxStalledDays = 90;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>How often the poller reads game state, clamped to 0.5–5 s when read.</summary>
    public double PollIntervalSeconds { get; set; } = 1.0;

    /// <summary>Print a chat line when a pinned or feature quest becomes available (see <c>Game.ChatNotifier</c>). Off by default.</summary>
    public bool ChatNoticeNewlyAvailable { get; set; }

    /// <summary>Whether main scenario quests are included in those notices.</summary>
    public bool IncludeMsqInNotices { get; set; }

    /// <summary>Print a chat line when a level-up opens the next job or role quest (see <c>Game.ChatNotifier</c>). On by default.</summary>
    public bool JobQuestNudge { get; set; } = true;

    /// <summary>Show the "Removed from the game" tree node (retired quests and those with no journal genre). Off by default.</summary>
    public bool ShowUnlisted { get; set; }

    // ---- 0.6.1: journal refiling ----
    /// <summary>
    /// Whether the catalog files the sheet's genre-less quests by the refiling rules (<see cref="JournalFiling.Refiled"/>,
    /// the default) or leaves them in the removed bucket as releases before 0.6.1 did (<see cref="JournalFiling.Legacy"/>,
    /// the in-field rollback). A change rebuilds the catalog.
    /// </summary>
    public JournalFiling JournalFiling { get; set; } = JournalFiling.Refiled;

    /// <summary>Register every quest and Moonlit reward with Wotsit when it is loaded (see <c>Game.WotsitIpc</c>). On by default.</summary>
    public bool WotsitIntegration { get; set; } = true;

    // ---- 0.5.0: todo overlay ----
    /// <summary>Show the small always-on todo overlay (pins, nearby feature quests, MSQ, job quests).</summary>
    public bool TodoOverlayEnabled { get; set; } = false;
    /// <summary>When true the overlay cannot be moved or resized and ignores clicks on its frame.</summary>
    public bool TodoOverlayLocked { get; set; } = false;
    /// <summary>Background opacity of the overlay, 0.2–1.0.</summary>
    public float TodoOverlayOpacity { get; set; } = 0.85f;
    public bool TodoShowPins { get; set; } = true;
    public bool TodoShowNearbyFeature { get; set; } = true;
    public bool TodoShowMsq { get; set; } = true;
    public bool TodoShowJobQuests { get; set; } = true;

    // ---- 0.5.0: item hints ----
    /// <summary>Show a small hint near the cursor when hovering an item that is a quest-exclusive reward.</summary>
    public bool ItemHintsEnabled { get; set; } = true;
    /// <summary>Add a "Tsukimichi: quest reward" entry to item context menus.</summary>
    public bool ItemContextMenuEnabled { get; set; } = true;

    /// <summary>Days an accepted quest sits untouched before the Stalled preset lists it; 1–90, default 7. Clamped by <see cref="StalledDaysClamped"/> when read.</summary>
    public int StalledDays { get; set; } = DefaultStalledDays;

    /// <summary><see cref="StalledDays"/> within the allowed bounds.</summary>
    public int StalledDaysClamped => Math.Clamp(StalledDays, MinStalledDays, MaxStalledDays);

    /// <summary>Last table filters, restored on load.</summary>
    public FilterSet Filters { get; set; } = new();

    /// <summary>Last table sort column, restored on load; <see cref="SortColumn.Journal"/> is journal order.</summary>
    public SortColumn SortColumn { get; set; } = SortColumn.Journal;

    public bool SortDescending { get; set; }

    /// <summary>Pinned quests sort to the top of the table whatever the sort column.</summary>
    public bool PinnedFirst { get; set; } = true;

    /// <summary>Character the user chose to view explicitly; null follows the live character.</summary>
    public ulong? ViewedContentId { get; set; }

    /// <summary>Open the help window by itself the first time the main window opens; cleared once that happened.</summary>
    public bool ShowHelpOnFirstRun { get; set; } = true;

    /// <summary>Set when the interactive tutorial was finished or declined; while false the welcome card offers the tour the first time the main window opens.</summary>
    public bool TutorialCompleted { get; set; }

    /// <summary>
    /// Text and layout scale of the main window on top of Dalamud's global scale; 0.9–1.6, default 1.15. Clamped by
    /// <see cref="ScaleMetrics.ClampUiScale"/> when read.
    /// </summary>
    public float UiScale { get; set; } = ScaleMetrics.DefaultUiScale;

    /// <summary>
    /// Size of moons, reward icons, banners and toolbar buttons relative to the scaled text; 0.8–2.0, default 1.25.
    /// Clamped by <see cref="ScaleMetrics.ClampIconScale"/> when read.
    /// </summary>
    public float IconScale { get; set; } = ScaleMetrics.DefaultIconScale;

    // ---- 0.6.0: what's new ----
    /// <summary>
    /// The plugin version whose "What's new" card was seen (or recorded silently on a fresh install); empty until the
    /// main window first opens. When it differs from the running version the card shows once (see <c>Ui.WhatsNewCard</c>).
    /// </summary>
    public string LastSeenVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether a configuration file existed before this load; set by <see cref="Load(IDalamudPluginInterface, IPluginLog?)"/>,
    /// never persisted. With <see cref="LastSeenVersion"/> empty it tells an update from a build that predates the
    /// card (every release before 0.6.0) apart from a fresh install (<c>Core.Ui.WhatsNew.Decide</c>).
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public bool HasPriorConfig { get; private set; }

    // ---- 0.6.0: Moonlit store re-sells ----
    /// <summary>
    /// Moonlit toolbar "Hide store re-sells": drop rewards the FFXIV Online Store also sells (entry OtherSources carries
    /// OnlineStore) from the rows and from the obtained/total counts. Off by default.
    /// </summary>
    public bool MoonlitHideStoreResells { get; set; }

    // ---- 0.5.1: motion ----
    /// <summary>
    /// Replace the hold-to-confirm arc with a text countdown (and, later, other animation with a cut). Off by default;
    /// a later release follows the OS animation setting instead.
    /// </summary>
    public bool ReduceMotion { get; set; }

    /// <summary>Poll interval as a <see cref="TimeSpan"/> within the allowed bounds.</summary>
    public TimeSpan PollInterval
    {
        get
        {
            var seconds = double.IsFinite(PollIntervalSeconds)
                ? Math.Clamp(PollIntervalSeconds, MinPollIntervalSeconds, MaxPollIntervalSeconds)
                : 1.0;
            return TimeSpan.FromSeconds(seconds);
        }
    }

    /// <summary>
    /// Reads the saved configuration or returns defaults when there is none, it is of another type, or reading it
    /// throws (a corrupt file must not stop the plugin from loading). Before defaults replace an unreadable file it
    /// is copied aside (see <see cref="ConfigRecovery"/>) and the failure is logged once when a log is given.
    /// </summary>
    public static Configuration Load(IDalamudPluginInterface pluginInterface) => Load(pluginInterface, null);

    /// <inheritdoc cref="Load(IDalamudPluginInterface)"/>
    public static Configuration Load(IDalamudPluginInterface pluginInterface, IPluginLog? log)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        // Read before the load: a file that exists but cannot be read still counts as a prior configuration.
        var hadFile = pluginInterface.ConfigFile.Exists;
        Configuration config;
        try
        {
            config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        }
        catch (Exception ex)
        {
            // The next Save overwrites the file, so the unreadable one is kept beside it first.
            var path = pluginInterface.ConfigFile.FullName;
            if (ConfigRecovery.TryCopyAside(path, out var copiedTo, out var copyError) && copiedTo is not null)
            {
                log?.Warning(ex, "Could not read the saved configuration; a copy was kept at {Path} and defaults are in use", copiedTo);
            }
            else if (copyError is not null)
            {
                log?.Warning(ex, "Could not read the saved configuration and could not copy it aside ({Error}); using defaults", copyError);
            }
            else
            {
                log?.Warning(ex, "Could not read the saved configuration; using defaults");
            }

            config = new Configuration();
        }

        config.Filters ??= new FilterSet();
        config.LastSeenVersion ??= string.Empty;
        if (!Enum.IsDefined(config.JournalFiling))
        {
            // A hand-edited integer, or a value a newer build wrote before a downgrade: the mapper would read it as
            // Legacy while neither radio button showed selected. The default filing stands.
            log?.Warning("Saved JournalFiling {Value} is not a known mode; using {Default}", (int)config.JournalFiling, JournalFiling.Refiled);
            config.JournalFiling = JournalFiling.Refiled;
        }

        config.HasPriorConfig = hadFile;
        return config;
    }

    public void Save(IDalamudPluginInterface pluginInterface)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        Version = CurrentVersion;
        pluginInterface.SavePluginConfig(this);
    }
}
