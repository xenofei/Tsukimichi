using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Tsukimichi.Config;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings (spec §7): poll interval (with the measured cost of a poll under it), display scale sliders
/// (<see cref="Configuration.UiScale"/>, <see cref="Configuration.IconScale"/>), chat notices, the Unlisted bucket,
/// help (open it, start the tutorial, offer it on first run), data deletion with a double confirm, and an About
/// section with the plugin, reward-data and catalog stamps plus the poll timing. Every change is saved as it
/// happens; sliders save when released.
/// </summary>
public sealed class ConfigWindow : Window
{
    private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(8);

    private const float MinUiScale = 0.9f;
    private const float MaxUiScale = 1.6f;
    private const float MinIconScale = 0.8f;
    private const float MaxIconScale = 2.0f;

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
    private float uiScale;
    private float iconScale;
    private bool scaleDirty;
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

    public override void OnOpen()
    {
        ReadSettings();
    }

    public override void OnClose()
    {
        if (pollDirty || scaleDirty)
        {
            pollDirty = false;
            scaleDirty = false;
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
        uiScale = Math.Clamp(settings.UiScale, MinUiScale, MaxUiScale);
        iconScale = Math.Clamp(settings.IconScale, MinIconScale, MaxIconScale);
        pollDirty = false;
        scaleDirty = false;
    }

    /// <summary>Two sliders written to the configuration as they move and saved when released.</summary>
    private void DrawDisplay()
    {
        Header(Strings.ConfigSectionDisplay);
        var width = 220f * ImGuiHelpers.GlobalScale;

        ImGui.SetNextItemWidth(width);
        if (ImGui.SliderFloat(Strings.ConfigUiScale, ref uiScale, MinUiScale, MaxUiScale, "%.2f", ImGuiSliderFlags.AlwaysClamp))
        {
            settings.UiScale = uiScale;
            scaleDirty = true;
        }

        SaveWhenReleased();
        ImGui.TextDisabled(Strings.ConfigUiScaleHint);

        ImGui.SetNextItemWidth(width);
        if (ImGui.SliderFloat(Strings.ConfigIconScale, ref iconScale, MinIconScale, MaxIconScale, "%.2f", ImGuiSliderFlags.AlwaysClamp))
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
