using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Tsukimichi.Config;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings (spec §7): poll interval (with the measured cost of a poll under it), display scale sliders
/// (<see cref="Configuration.UiScale"/>, <see cref="Configuration.IconScale"/>), chat notices, the Unlisted bucket,
/// the todo overlay (on/off, lock, opacity, sections, reset position), item hints, the Wotsit integration, help
/// (open it, start the tutorial, offer it on first run), data deletion with a double confirm, and an About
/// section with the plugin, reward-data and catalog stamps plus the poll timing. Every change is saved as it
/// happens; sliders save when released.
/// </summary>
public sealed class ConfigWindow : Window
{
    private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(8);

    private readonly Configuration settings;
    private readonly SessionState session;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly Action<bool> onShowUnlistedChanged;

    private readonly string pluginVersionLine;
    private readonly string gameDataLine;
    private readonly string entriesLine;
    private readonly string curatedLine;

    private CatalogBundle? aboutBundle;
    private string catalogLine = Strings.ConfigCatalogLoading;
    private string? catalogError;

    private float pollSeconds;
    private bool pollDirty;
    private bool scaleDirty;
    private bool todoDirty;
    private bool openSecondConfirm;
    private string? toast;
    private DateTime toastUntilUtc;

    // Poll timing lines, rebuilt only when another poll completed.
    private int pollTimingCount = -1;
    private string pollTimingLine = Strings.ConfigPollTimingNone;
    private string pollCostLine = Strings.ConfigPollCostUnknown;

    public ConfigWindow(Configuration settings, SessionState session, IDalamudPluginInterface pluginInterface, Action<bool> onShowUnlistedChanged)
        : base("Tsukimichi Settings###TsukimichiConfig")
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.onShowUnlistedChanged = onShowUnlistedChanged ?? throw new ArgumentNullException(nameof(onShowUnlistedChanged));

        Size = new Vector2(480f, 640f);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(400f, 320f) };

        var version = typeof(ConfigWindow).Assembly.GetName().Version;
        pluginVersionLine = Strings.ConfigPluginVersionPrefix + (version?.ToString() ?? "unknown");

        var rewards = session.UniqueRewards;
        gameDataLine = string.IsNullOrEmpty(rewards.GameVersion)
            ? Strings.ConfigGameDataMissing
            : Strings.ConfigGameDataPrefix + rewards.GameVersion
              + (rewards.GeneratedUtc == default ? string.Empty : Strings.ConfigGeneratedPrefix + rewards.GeneratedUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        entriesLine = rewards.Entries.Count.ToString(CultureInfo.InvariantCulture) + Strings.ConfigUniqueEntriesSuffix;

        var curated = session.Curated;
        curatedLine = Strings.ConfigCuratedPrefix
                      + curated.SystemUnlocks.Count.ToString(CultureInfo.InvariantCulture) + Strings.ConfigCuratedSystemSuffix
                      + curated.DutyUnlocks.Count.ToString(CultureInfo.InvariantCulture) + Strings.ConfigCuratedDutySuffix
                      + curated.FeatureQuests.Count.ToString(CultureInfo.InvariantCulture) + Strings.ConfigCuratedFeatureSuffix
                      + curated.Festivals.Count.ToString(CultureInfo.InvariantCulture) + Strings.ConfigCuratedFestivalSuffix;

        ReadSettings();
    }

    /// <summary>Opens the help window; set by the plugin once the help window exists. Null hides the button.</summary>
    public Action? ShowHelp { get; set; }

    /// <summary>Starts the interactive tutorial; set by the plugin once the overlay exists. Null hides the button.</summary>
    public Action? StartTutorial { get; set; }

    /// <summary>Called with the new value after <see cref="Configuration.WotsitIntegration"/> is toggled and saved; the plugin points it at the Wotsit IPC.</summary>
    public Action<bool>? WotsitToggled { get; set; }

    /// <summary>Moves the todo overlay back to its default place; set by the plugin once the overlay exists. Null hides the button.</summary>
    public Action? ResetTodoPosition { get; set; }

    /// <summary>Called with the new value after <see cref="Configuration.ItemHintsEnabled"/> is toggled and saved; the item-hint feature wires it.</summary>
    public Action<bool>? ItemHintsToggled { get; set; }

    /// <summary>Called with the new value after <see cref="Configuration.ItemContextMenuEnabled"/> is toggled and saved; the item-hint feature wires it.</summary>
    public Action<bool>? ItemContextMenuToggled { get; set; }

    public override void OnOpen()
    {
        ReadSettings();
    }

    public override void OnClose()
    {
        if (pollDirty || scaleDirty || todoDirty)
        {
            pollDirty = false;
            scaleDirty = false;
            todoDirty = false;
            Save();
        }
    }

    public override void Draw()
    {
        DrawPolling();
        ImGui.Spacing();
        DrawDisplay();
        ImGui.Spacing();
        DrawNotices();
        ImGui.Spacing();
        DrawJournal();
        ImGui.Spacing();
        DrawTodoOverlay();
        ImGui.Spacing();
        DrawItemHints();
        ImGui.Spacing();
        DrawIntegrations();
        ImGui.Spacing();
        DrawHelp();
        ImGui.Spacing();
        DrawData();
        ImGui.Spacing();
        DrawAbout();
    }

    private void DrawPolling()
    {
        Header(Strings.ConfigSectionPolling);
        ImGui.SetNextItemWidth(220f * ImGuiHelpers.GlobalScale);
        if (ImGui.SliderFloat(Strings.ConfigPollInterval, ref pollSeconds, (float)Configuration.MinPollIntervalSeconds, (float)Configuration.MaxPollIntervalSeconds, "%.1f s", ImGuiSliderFlags.AlwaysClamp))
        {
            settings.PollIntervalSeconds = Math.Round(pollSeconds, 1);
            pollDirty = true;
        }

        if (pollDirty && ImGui.IsItemDeactivatedAfterEdit())
        {
            pollDirty = false;
            Save();
        }

        ImGui.TextDisabled(Strings.ConfigPollIntervalHint);
        RefreshPollTiming();
        ImGui.TextDisabled(pollCostLine);
    }

    /// <summary>Copies the slider-backed values out of the configuration (on construction and each time the window opens).</summary>
    private void ReadSettings()
    {
        pollSeconds = (float)settings.PollInterval.TotalSeconds;
        pollDirty = false;
        scaleDirty = false;
    }

    /// <summary>
    /// Two sliders written to the configuration as they move and saved when released. They read the configuration
    /// every frame (through the same clamps as the main window's filter panel, which edits the same values), so the
    /// two places never disagree and a corrupt value shows as the default rather than NaN.
    /// </summary>
    private void DrawDisplay()
    {
        Header(Strings.ConfigSectionDisplay);
        var width = 220f * ImGuiHelpers.GlobalScale;

        var uiScale = ScaleMetrics.ClampUiScale(settings.UiScale);
        ImGui.SetNextItemWidth(width);
        if (ImGui.SliderFloat(Strings.ConfigUiScale, ref uiScale, ScaleMetrics.MinUiScale, ScaleMetrics.MaxUiScale, "%.2f", ImGuiSliderFlags.AlwaysClamp))
        {
            settings.UiScale = uiScale;
            scaleDirty = true;
        }

        SaveWhenReleased();
        ImGui.TextDisabled(Strings.ConfigUiScaleHint);

        var iconScale = ScaleMetrics.ClampIconScale(settings.IconScale);
        ImGui.SetNextItemWidth(width);
        if (ImGui.SliderFloat(Strings.ConfigIconScale, ref iconScale, ScaleMetrics.MinIconScale, ScaleMetrics.MaxIconScale, "%.2f", ImGuiSliderFlags.AlwaysClamp))
        {
            settings.IconScale = iconScale;
            scaleDirty = true;
        }

        SaveWhenReleased();
        ImGui.TextDisabled(Strings.ConfigIconScaleHint);
    }

    private void SaveWhenReleased()
    {
        if (scaleDirty && ImGui.IsItemDeactivatedAfterEdit())
        {
            scaleDirty = false;
            Save();
        }
    }

    private void DrawHelp()
    {
        Header(Strings.ConfigSectionHelp);
        if (StartTutorial is { } startTutorial)
        {
            if (ImGui.Button(Strings.ConfigStartTutorial))
            {
                startTutorial();
            }

            ImGui.SameLine();
        }

        if (ShowHelp is { } showHelp)
        {
            if (ImGui.Button(Strings.ConfigShowHelp))
            {
                showHelp();
            }
        }

        var offer = !settings.TutorialCompleted;
        if (ImGui.Checkbox(Strings.ConfigOfferTutorial, ref offer))
        {
            settings.TutorialCompleted = !offer;
            Save();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Strings.ConfigOfferTutorialHint);
        }
    }

    /// <summary>"Last poll … · average … · n polls" and the slider hint, once per completed poll.</summary>
    private void RefreshPollTiming()
    {
        var count = session.PollCount;
        if (count == pollTimingCount)
        {
            return;
        }

        pollTimingCount = count;
        if (count == 0)
        {
            pollTimingLine = Strings.ConfigPollTimingNone;
            pollCostLine = Strings.ConfigPollCostUnknown;
            return;
        }

        pollTimingLine = string.Format(CultureInfo.InvariantCulture, Strings.ConfigPollTimingFormat, session.LastPollMs, session.AveragePollMs, count);
        pollCostLine = string.Format(CultureInfo.InvariantCulture, Strings.ConfigPollCostFormat, session.AveragePollMs);
    }

    private void DrawNotices()
    {
        Header(Strings.ConfigSectionNotices);
        var notice = settings.ChatNoticeNewlyAvailable;
        if (ImGui.Checkbox(Strings.ConfigChatNotice, ref notice))
        {
            settings.ChatNoticeNewlyAvailable = notice;
            Save();
        }

        using (ImRaii.PushIndent())
        using (ImRaii.Disabled(!notice))
        {
            var msq = settings.IncludeMsqInNotices;
            if (ImGui.Checkbox(Strings.ConfigIncludeMsq, ref msq))
            {
                settings.IncludeMsqInNotices = msq;
                Save();
            }
        }

        var nudge = settings.JobQuestNudge;
        if (ImGui.Checkbox(Strings.JobsConfigNudge, ref nudge))
        {
            settings.JobQuestNudge = nudge;
            Save();
        }
    }

    private void DrawJournal()
    {
        Header(Strings.ConfigSectionJournal);
        var unlisted = settings.ShowUnlisted;
        if (ImGui.Checkbox(Strings.ConfigShowUnlisted, ref unlisted))
        {
            settings.ShowUnlisted = unlisted;
            Save();
            onShowUnlistedChanged(unlisted);
        }

        ImGui.TextDisabled(Strings.ConfigShowUnlistedHint);
    }

    /// <summary>
    /// The todo overlay: on/off, lock, an opacity slider (saved when released), the four section toggles and a
    /// "Reset position" button. The overlay reads the configuration every frame, so every change shows at once.
    /// </summary>
    private void DrawTodoOverlay()
    {
        Header(Strings.TodoConfigSection);
        var enabled = settings.TodoOverlayEnabled;
        if (ImGui.Checkbox(Strings.TodoConfigEnabled, ref enabled))
        {
            settings.TodoOverlayEnabled = enabled;
            Save();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Strings.TodoConfigEnabledHint);
        }

        using var indent = ImRaii.PushIndent();
        using var disabled = ImRaii.Disabled(!enabled);

        var locked = settings.TodoOverlayLocked;
        if (ImGui.Checkbox(Strings.TodoConfigLocked, ref locked))
        {
            settings.TodoOverlayLocked = locked;
            Save();
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(Strings.TodoConfigLockedHint);
        }

        var opacity = TodoOverlay.ClampOpacity(settings.TodoOverlayOpacity);
        ImGui.SetNextItemWidth(220f * ImGuiHelpers.GlobalScale);
        if (ImGui.SliderFloat(Strings.TodoConfigOpacity, ref opacity, TodoOverlay.MinOpacity, TodoOverlay.MaxOpacity, "%.2f", ImGuiSliderFlags.AlwaysClamp))
        {
            settings.TodoOverlayOpacity = opacity;
            todoDirty = true;
        }

        if (todoDirty && ImGui.IsItemDeactivatedAfterEdit())
        {
            todoDirty = false;
            Save();
        }

        ImGui.TextDisabled(Strings.TodoConfigSectionsLabel);
        var pins = settings.TodoShowPins;
        if (ImGui.Checkbox(Strings.TodoConfigShowPins, ref pins))
        {
            settings.TodoShowPins = pins;
            Save();
        }

        var nearby = settings.TodoShowNearbyFeature;
        if (ImGui.Checkbox(Strings.TodoConfigShowNearby, ref nearby))
        {
            settings.TodoShowNearbyFeature = nearby;
            Save();
        }

        var msq = settings.TodoShowMsq;
        if (ImGui.Checkbox(Strings.TodoConfigShowMsq, ref msq))
        {
            settings.TodoShowMsq = msq;
            Save();
        }

        var jobs = settings.TodoShowJobQuests;
        if (ImGui.Checkbox(Strings.TodoConfigShowJobQuests, ref jobs))
        {
            settings.TodoShowJobQuests = jobs;
            Save();
        }

        if (ResetTodoPosition is not { } reset)
        {
            return;
        }

        if (ImGui.Button(Strings.TodoConfigResetPosition))
        {
            reset();
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(Strings.TodoConfigResetPositionHint);
        }
    }

    /// <summary>Item hints: the hover hint and the context-menu entry. The feature itself listens through the two callbacks.</summary>
    private void DrawItemHints()
    {
        Header(Strings.ConfigSectionItemHints);
        var hints = settings.ItemHintsEnabled;
        if (ImGui.Checkbox(Strings.ConfigItemHints, ref hints))
        {
            settings.ItemHintsEnabled = hints;
            Save();
            ItemHintsToggled?.Invoke(hints);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Strings.ConfigItemHintsHint);
        }

        var contextMenu = settings.ItemContextMenuEnabled;
        if (ImGui.Checkbox(Strings.ConfigItemContextMenu, ref contextMenu))
        {
            settings.ItemContextMenuEnabled = contextMenu;
            Save();
            ItemContextMenuToggled?.Invoke(contextMenu);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Strings.ConfigItemContextMenuHint);
        }
    }

    private void DrawIntegrations()
    {
        Header(Strings.ConfigSectionIntegrations);
        var wotsit = settings.WotsitIntegration;
        if (ImGui.Checkbox(Strings.ConfigWotsitIntegration, ref wotsit))
        {
            settings.WotsitIntegration = wotsit;
            Save();
            WotsitToggled?.Invoke(wotsit);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Strings.ConfigWotsitIntegrationHint);
        }
    }

    private void DrawData()
    {
        Header(Strings.ConfigSectionData);
        ImGui.TextWrapped(Strings.ConfigDataRetention);
        ImGui.Spacing();

        using (Theme.PushDestructiveButton())
        {
            if (ImGui.Button(Strings.ConfigDeleteAll))
            {
                ImGui.OpenPopup(Strings.ConfigDeleteStep1Popup);
            }
        }

        DrawDeleteConfirms();
        DrawToast();
    }

    /// <summary>Two modals in a row: the first explains, the second asks again; only the second deletes.</summary>
    private void DrawDeleteConfirms()
    {
        using (var first = ImRaii.PopupModal(Strings.ConfigDeleteStep1Popup, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (first)
            {
                ImGui.TextWrapped(Strings.ConfigDeleteStep1Text);
                ImGui.Spacing();
                if (ImGui.Button(Strings.ConfigDeleteContinue))
                {
                    openSecondConfirm = true;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();
                if (ImGui.Button(Strings.ConfigCancel))
                {
                    ImGui.CloseCurrentPopup();
                }
            }
        }

        if (openSecondConfirm)
        {
            openSecondConfirm = false;
            ImGui.OpenPopup(Strings.ConfigDeleteStep2Popup);
        }

        using var second = ImRaii.PopupModal(Strings.ConfigDeleteStep2Popup, ImGuiWindowFlags.AlwaysAutoResize);
        if (!second)
        {
            return;
        }

        ImGui.TextWrapped(Strings.ConfigDeleteStep2Text);
        ImGui.Spacing();
        using (Theme.PushDestructiveButton())
        {
            if (ImGui.Button(Strings.ConfigDeleteConfirm))
            {
                session.DeleteAllData();
                toast = Strings.ConfigDeleteDone;
                toastUntilUtc = DateTime.UtcNow + ToastDuration;
                ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();
        if (ImGui.Button(Strings.ConfigCancel))
        {
            ImGui.CloseCurrentPopup();
        }
    }

    private void DrawToast()
    {
        if (toast is null)
        {
            return;
        }

        if (DateTime.UtcNow >= toastUntilUtc)
        {
            toast = null;
            return;
        }

        using (Theme.PushText(Theme.Moon))
        {
            ImGui.TextUnformatted(toast);
        }
    }

    private void DrawAbout()
    {
        Header(Strings.ConfigSectionAbout);
        RefreshCatalogLine();
        ImGui.TextUnformatted(pluginVersionLine);
        ImGui.TextUnformatted(gameDataLine);
        ImGui.TextUnformatted(entriesLine);
        ImGui.TextUnformatted(curatedLine);
        ImGui.TextUnformatted(catalogLine);
        RefreshPollTiming();
        ImGui.TextUnformatted(pollTimingLine);
    }

    /// <summary>Catalog language and size, rebuilt when the bundle (or the error) changes.</summary>
    private void RefreshCatalogLine()
    {
        var bundle = session.Bundle;
        if (bundle is not null)
        {
            if (!ReferenceEquals(bundle, aboutBundle))
            {
                aboutBundle = bundle;
                catalogLine = Strings.ConfigCatalogPrefix + bundle.Catalog.Count.ToString(CultureInfo.InvariantCulture) + Strings.ConfigCatalogQuestsSuffix + bundle.Language;
            }

            return;
        }

        if (session.CatalogError is { } error)
        {
            if (error != catalogError)
            {
                catalogError = error;
                catalogLine = Strings.ConfigCatalogUnavailable + error;
            }

            return;
        }

        catalogLine = Strings.ConfigCatalogLoading;
    }

    private static void Header(string title)
    {
        ImGui.TextDisabled(title);
        ImGui.Separator();
    }

    private void Save() => settings.Save(pluginInterface);
}
