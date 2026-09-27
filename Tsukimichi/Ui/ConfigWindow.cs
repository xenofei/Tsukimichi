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
/// Settings (spec §7): poll interval, chat notices, the Unlisted bucket, data deletion with a double confirm, and an
/// About section with the plugin, reward-data and catalog stamps. Every change is saved as it happens; the slider
/// saves when released.
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
    private bool openSecondConfirm;
    private string? toast;
    private DateTime toastUntilUtc;

    public ConfigWindow(Configuration settings, SessionState session, IDalamudPluginInterface pluginInterface, Action<bool> onShowUnlistedChanged)
        : base("Tsukimichi Settings###TsukimichiConfig")
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.onShowUnlistedChanged = onShowUnlistedChanged ?? throw new ArgumentNullException(nameof(onShowUnlistedChanged));

        Size = new Vector2(480f, 560f);
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

        pollSeconds = (float)settings.PollInterval.TotalSeconds;
    }

    public override void OnOpen()
    {
        pollSeconds = (float)settings.PollInterval.TotalSeconds;
        pollDirty = false;
    }

    public override void OnClose()
    {
        if (pollDirty)
        {
            pollDirty = false;
            Save();
        }
    }

    public override void Draw()
    {
        DrawPolling();
        ImGui.Spacing();
        DrawNotices();
        ImGui.Spacing();
        DrawJournal();
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
