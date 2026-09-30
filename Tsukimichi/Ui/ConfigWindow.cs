using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Tsukimichi.Config;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings (spec §7): poll interval (with the measured cost of a poll under it), display scale sliders
/// (<see cref="Configuration.UiScale"/>, <see cref="Configuration.IconScale"/>), Reduce motion and the journal
/// filing, chat notices, the Removed from the game node, the spoiler shield (T19: names ahead, how many to reveal,
/// artwork, the viewed character's override), the todo overlay (on/off, lock, opacity, sections, reset position), item hints, the Wotsit
/// integration, help (open it, start the tutorial, offer it on first run), the user's Moonlit verdicts with Restore
/// and a hold-to-confirm Restore all, data deletion with a double confirm, and an About section with the plugin,
/// reward-data and catalog stamps plus the poll timing. Every change is saved as it happens; sliders save when
/// released.
/// </summary>
public sealed class ConfigWindow : Window
{
    private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(8);
    private static readonly string RestoreAllLabel = Strings.ConfigVerdictRestoreAll + Chrome.HoldIdSuffix;

    private readonly Configuration settings;
    private readonly SessionState session;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly Action<bool> onShowUnlistedChanged;

    private readonly string pluginVersionLine;
    private readonly string dataStampLine;
    private readonly string? dataVersionWarning;
    private readonly string curatedLine;

    private CatalogBundle? aboutBundle;
    private string catalogLine = Strings.ConfigCatalogLoading;
    private string? catalogError;

    private float pollSeconds;
    private bool pollDirty;
    private bool scaleDirty;
    private bool todoDirty;
    private bool spoilerAheadDirty;
    private bool openSecondConfirm;

    // Spoilers: the "N names hidden" line, rebuilt once per session version.
    private int spoilerCountVersion = -1;
    private string spoilerCountLine = string.Empty;

    // Spoilers: the "Shield for {name}" label, rebuilt when the viewed character or its name changes.
    private ulong spoilerLabelContentId;
    private string? spoilerLabelName;
    private string spoilerLabel = string.Empty;
    private string? toast;
    private DateTime toastUntilUtc;

    // "Your Moonlit verdicts": one row per override, rebuilt when the overrides or the quest catalog change.
    private readonly ConfirmGate restoreAllGate = new();
    private VerdictRow[] verdictRows = [];
    private int verdictVersion = -1;
    private CatalogBundle? verdictBundle;
    private int verdictSpoilers;
    private string verdictsHeader = string.Empty;

    // Poll timing lines, rebuilt only when another poll completed.
    private int pollTimingCount = -1;
    private string pollTimingLine = Strings.ConfigPollTimingNone;
    private string pollCostLine = Strings.ConfigPollCostUnknown;

    /// <param name="diagnostics">Owns the data stamp and the game-version warning the About section shows.</param>
    public ConfigWindow(Configuration settings, SessionState session, IDalamudPluginInterface pluginInterface, DiagnosticBuilder diagnostics, Action<bool> onShowUnlistedChanged)
        : base("Tsukimichi Settings###TsukimichiConfig")
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        ArgumentNullException.ThrowIfNull(diagnostics);
        this.onShowUnlistedChanged = onShowUnlistedChanged ?? throw new ArgumentNullException(nameof(onShowUnlistedChanged));

        Size = new Vector2(480f, 640f);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(400f, 320f) };

        pluginVersionLine = Strings.ConfigPluginVersionPrefix + (diagnostics.PluginVersion.Length > 0 ? diagnostics.PluginVersion : "unknown");
        dataStampLine = diagnostics.DataStampLine;
        dataVersionWarning = diagnostics.VersionMismatchWarning;

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

    /// <summary>Called with the new value after <see cref="Configuration.JournalFiling"/> changes and is saved; the plugin rebuilds the catalog.</summary>
    public Action<JournalFiling>? JournalFilingChanged { get; set; }

    /// <summary>Moves the todo overlay back to its default place; set by the plugin once the overlay exists. Null hides the button.</summary>
    public Action? ResetTodoPosition { get; set; }

    /// <summary>Called with the new value after <see cref="Configuration.ItemHintsEnabled"/> is toggled and saved; the item-hint feature wires it.</summary>
    public Action<bool>? ItemHintsToggled { get; set; }

    /// <summary>Called with the new value after <see cref="Configuration.ItemContextMenuEnabled"/> is toggled and saved; the item-hint feature wires it.</summary>
    public Action<bool>? ItemContextMenuToggled { get; set; }

    /// <summary>Called with the new value after <see cref="Configuration.NpcContextMenuEnabled"/> is toggled and saved; the NPC menu hook wires it.</summary>
    public Action<bool>? NpcContextMenuToggled { get; set; }

    /// <summary>
    /// The shared addon kill switch (T20) behind Integrations' paused notice and "Enable game hooks on this untested
    /// version"; set by the plugin. Null hides both.
    /// </summary>
    public HookGate? HookGate { get; set; }

    /// <summary>Settings › Data › Export (P12); set by the plugin once the export service exists. Null hides it.</summary>
    public ExportSection? Export { get; set; }

    /// <summary>The user's Moonlit verdicts for the Data section; set by the plugin once the Moonlit pane exists. Null shows a placeholder.</summary>
    public IUniqueOverrides? Overrides { get; set; }

    public override void OnOpen()
    {
        ReadSettings();
    }

    public override void OnClose()
    {
        if (pollDirty || scaleDirty || todoDirty || spoilerAheadDirty)
        {
            pollDirty = false;
            scaleDirty = false;
            todoDirty = false;
            spoilerAheadDirty = false;
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
        DrawSpoilers();
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

        // Density: the quest table's row height only (T12).
        ImGui.TextUnformatted(Strings.ConfigDensity);
        ImGui.SameLine();
        if (ImGui.RadioButton(Strings.ConfigDensityComfortable, settings.Density == RowDensity.Comfortable))
        {
            settings.Density = RowDensity.Comfortable;
            Save();
        }

        ImGui.SameLine();
        if (ImGui.RadioButton(Strings.ConfigDensityDense, settings.Density == RowDensity.Dense))
        {
            settings.Density = RowDensity.Dense;
            Save();
        }

        ImGui.TextDisabled(Strings.ConfigDensityHint);

        var reduceMotion = settings.ReduceMotion;
        if (ImGui.Checkbox(Strings.ConfigReduceMotion, ref reduceMotion))
        {
            settings.ReduceMotion = reduceMotion;
            settings.ReduceMotionChosen = true;
            Save();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Strings.ConfigReduceMotionHint);
        }

        // One layout, two palettes (T13): Night, or the surfaces mapped from the user's Dalamud style.
        var followDalamud = settings.FollowDalamudColours;
        if (ImGui.Checkbox(Strings.ConfigFollowDalamudColours, ref followDalamud))
        {
            settings.FollowDalamudColours = followDalamud;
            Save();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Strings.ConfigFollowDalamudColoursHint);
        }

        DrawJournalFiling();
    }

    /// <summary>
    /// The journal filing radio: Refiled (the 0.6.1 rules) or Legacy (the genre-less quests stay in the removed
    /// bucket, as before). Saved at once; the plugin rebuilds the catalog through <see cref="JournalFilingChanged"/>.
    /// </summary>
    private void DrawJournalFiling()
    {
        ImGui.Spacing();
        ImGui.TextUnformatted(Strings.ConfigJournalFiling);
        var filing = settings.JournalFiling;
        var changed = false;
        if (ImGui.RadioButton(Strings.ConfigJournalFilingRefiled, filing == JournalFiling.Refiled))
        {
            filing = JournalFiling.Refiled;
            changed = true;
        }

        ImGui.SameLine();
        if (ImGui.RadioButton(Strings.ConfigJournalFilingLegacy, filing == JournalFiling.Legacy))
        {
            filing = JournalFiling.Legacy;
            changed = true;
        }

        if (changed && filing != settings.JournalFiling)
        {
            settings.JournalFiling = filing;
            Save();
            JournalFilingChanged?.Invoke(filing);
        }

        ImGui.TextDisabled(Strings.ConfigJournalFilingHint);
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

        var abandoned = settings.ChatNoticeAbandoned;
        if (ImGui.Checkbox(Strings.AbandonedConfigNotice, ref abandoned))
        {
            settings.ChatNoticeAbandoned = abandoned;
            Save();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Strings.AbandonedConfigNoticeHint);
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
    /// Spoilers (T19): hide main scenario names ahead of the character, how many quests ahead keep their names (saved
    /// when the slider is released), hide journal artwork until a quest is in the journal, and an override for the
    /// character shown. Every change bumps the session so each surface re-reads the mask at once.
    /// </summary>
    private void DrawSpoilers()
    {
        Header(Strings.SettingsSpoilers);
        var hideNames = settings.SpoilerHideMsqNames;
        if (ImGui.Checkbox(Strings.SpoilerHideNames, ref hideNames))
        {
            settings.SpoilerHideMsqNames = hideNames;
            SpoilersChanged();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.SpoilerHideNamesHelp);
        }

        // The slider applies whenever the viewed character's effective options hide names: the global setting, or
        // that character's override (Always shield hides them even with the global setting off). Mirrors
        // Configuration.SpoilerOptionsFor without building the options record each frame.
        var effectiveHide = session.ViewedContentId is { } viewedId && settings.SpoilerShieldByCharacter.TryGetValue(viewedId, out var shielded)
            ? shielded
            : hideNames;
        using (ImRaii.PushIndent())
        using (ImRaii.Disabled(!effectiveHide))
        {
            var ahead = Math.Clamp(settings.SpoilerRevealAhead, 0, SpoilerOptions.MaxAhead);
            ImGui.SetNextItemWidth(160f * ImGuiHelpers.GlobalScale);
            if (ImGui.SliderInt(Strings.SpoilerAhead, ref ahead, 0, SpoilerOptions.MaxAhead, "%d", ImGuiSliderFlags.AlwaysClamp))
            {
                settings.SpoilerRevealAhead = ahead;
                spoilerAheadDirty = true;
                session.RefreshSpoilers();
            }

            if (spoilerAheadDirty && ImGui.IsItemDeactivatedAfterEdit())
            {
                spoilerAheadDirty = false;
                Save();
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.SpoilerAheadHelp);
            }
        }

        var hideArtwork = settings.SpoilerHideArtwork;
        if (ImGui.Checkbox(Strings.SpoilerHideArtwork, ref hideArtwork))
        {
            settings.SpoilerHideArtwork = hideArtwork;
            SpoilersChanged();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.SpoilerHideArtworkHelp);
        }

        DrawSpoilerOverride();
        if (spoilerCountVersion != session.Version)
        {
            spoilerCountVersion = session.Version;
            spoilerCountLine = session.Bundle is null
                ? string.Empty
                : string.Format(CultureInfo.CurrentCulture, Strings.SpoilerMaskedCountFormat, session.Spoilers.MaskedCount);
        }

        if (spoilerCountLine.Length > 0)
        {
            ImGui.TextDisabled(spoilerCountLine);
        }
    }

    /// <summary>The viewed character's own shield: follow the settings, always shield, or show everything.</summary>
    private void DrawSpoilerOverride()
    {
        if (session.ViewedContentId is not { } contentId)
        {
            ImGui.TextDisabled(Strings.SpoilerCharacterNone);
            return;
        }

        var name = session.ViewedSnapshot?.Name;
        if (spoilerLabel.Length == 0 || spoilerLabelContentId != contentId || !string.Equals(spoilerLabelName, name, StringComparison.Ordinal))
        {
            spoilerLabelContentId = contentId;
            spoilerLabelName = name;
            spoilerLabel = string.IsNullOrEmpty(name) ? Strings.SpoilerCharacterLabel : string.Format(CultureInfo.CurrentCulture, Strings.SpoilerCharacterFormat, name);
        }

        ImGui.TextUnformatted(spoilerLabel);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.SpoilerCharacterHelp);
        }

        var current = settings.SpoilerShieldByCharacter.TryGetValue(contentId, out var shielded) ? (shielded ? 1 : 2) : 0;
        using var indent = ImRaii.PushIndent();
        var choice = current;
        if (ImGui.RadioButton(Strings.SpoilerCharacterDefault, current == 0))
        {
            choice = 0;
        }

        ImGui.SameLine();
        if (ImGui.RadioButton(Strings.SpoilerCharacterOn, current == 1))
        {
            choice = 1;
        }

        ImGui.SameLine();
        if (ImGui.RadioButton(Strings.SpoilerCharacterOff, current == 2))
        {
            choice = 2;
        }

        if (choice == current)
        {
            return;
        }

        if (choice == 0)
        {
            settings.SpoilerShieldByCharacter.Remove(contentId);
        }
        else
        {
            settings.SpoilerShieldByCharacter[contentId] = choice == 1;
        }

        SpoilersChanged();
    }

    /// <summary>Saves a spoiler setting and makes every surface re-read the mask.</summary>
    private void SpoilersChanged()
    {
        Save();
        session.RefreshSpoilers();
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

        var compact = settings.TodoOverlayCompact;
        if (ImGui.Checkbox(Strings.TodoConfigCompact, ref compact))
        {
            settings.TodoOverlayCompact = compact;
            Save();
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(Strings.TodoConfigCompactHint);
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

        var npcMenu = settings.NpcContextMenuEnabled;
        if (ImGui.Checkbox(Strings.ConfigNpcContextMenu, ref npcMenu))
        {
            settings.NpcContextMenuEnabled = npcMenu;
            Save();
            NpcContextMenuToggled?.Invoke(npcMenu);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Strings.ConfigNpcContextMenuHint);
        }

        DrawHookGate();
    }

    /// <summary>
    /// The addon kill switch (T20): the paused notice while the game hooks wait for a tested update, and "Enable game
    /// hooks on this untested version", scoped to the running game version, which re-registers them at once through
    /// <see cref="HookGate.Changed"/>.
    /// </summary>
    private void DrawHookGate()
    {
        if (HookGate is not { } gate)
        {
            return;
        }

        if (gate.IsPaused)
        {
            using var eclipse = Theme.PushText(Theme.Eclipse);
            ImGui.TextWrapped(Strings.HooksPausedNotice);
        }

        // Ticked only when the stored override names the running version: one from an earlier patch reads unticked.
        // Ticking stores the running version, unticking clears it. Without a known running version there is nothing
        // to store (and the gate already allows the hooks), so the box is disabled.
        var anyway = gate.EnableAnywayApplies;
        using (ImRaii.Disabled(gate.RunningVersion.Length == 0))
        {
            if (ImGui.Checkbox(Strings.HooksEnableUntested, ref anyway))
            {
                var version = anyway ? gate.RunningVersion : string.Empty;
                settings.EnableHooksOnUntestedVersion = version;
                Save();
                gate.SetEnableAnyway(version);
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(Strings.HooksEnableUntestedHint);
        }

        if (gate.Decision.Verdict == HookGateVerdict.Overridden)
        {
            ImGui.TextDisabled(Strings.HooksRunningUntested);
        }
    }

    private void DrawData()
    {
        Header(Strings.ConfigSectionData);
        ImGui.TextWrapped(Strings.ConfigDataRetention);
        ImGui.Spacing();
        DrawVerdicts();
        ImGui.Spacing();
        if (Export is { } export)
        {
            export.Draw();
            ImGui.Spacing();
        }


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

    /// <summary>
    /// "Your Moonlit verdicts (N)": quest, verdict, note and date per stored override with a Restore button each, and
    /// Restore all behind the same hold-to-confirm gate the verdict popups use. Restoring goes through the Moonlit
    /// pane, so its catalog rebuilds exactly as after a verdict.
    /// </summary>
    private void DrawVerdicts()
    {
        if (Overrides is not { } overrides)
        {
            ImGui.TextDisabled(Strings.ConfigVerdictsUnavailable);
            return;
        }

        RefreshVerdictRows(overrides);
        ImGui.TextUnformatted(verdictsHeader);
        if (verdictRows.Length == 0)
        {
            ImGui.TextDisabled(Strings.ConfigVerdictsNone);
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var restoreWidth = ImGui.CalcTextSize(Strings.ConfigVerdictRestore).X + ImGui.GetStyle().FramePadding.X * 2f + 4f * scale;
        const ImGuiTableFlags Flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp;
        using (var table = ImRaii.Table("##verdicts", 5, Flags))
        {
            if (table)
            {
                ImGui.TableSetupColumn(Strings.ConfigVerdictColumnQuest, ImGuiTableColumnFlags.WidthStretch, 3f);
                ImGui.TableSetupColumn(Strings.ConfigVerdictColumnVerdict, ImGuiTableColumnFlags.WidthFixed, 80f * scale);
                ImGui.TableSetupColumn(Strings.ConfigVerdictColumnNote, ImGuiTableColumnFlags.WidthStretch, 3f);
                ImGui.TableSetupColumn(Strings.ConfigVerdictColumnDate, ImGuiTableColumnFlags.WidthFixed, 80f * scale);
                ImGui.TableSetupColumn(Strings.ConfigVerdictColumnRestore, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoHeaderLabel, restoreWidth);
                ImGui.TableHeadersRow();

                foreach (var row in verdictRows)
                {
                    using var id = ImRaii.PushId((int)row.RowId);
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(row.QuestName);
                    ImGui.TableNextColumn();
                    using (Theme.PushText(row.Color))
                    {
                        ImGui.TextUnformatted(row.Verdict);
                    }

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(row.Note);
                    ImGui.TableNextColumn();
                    ImGui.TextDisabled(row.Date);
                    ImGui.TableNextColumn();
                    if (ImGui.SmallButton(Strings.ConfigVerdictRestore))
                    {
                        // The row cache refreshes next frame from the bumped version; the array is not touched here.
                        overrides.Clear(row.RowId);
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(Strings.ConfigVerdictRestoreTooltip);
                    }
                }
            }
        }

        if (Chrome.HoldButton(RestoreAllLabel, restoreAllGate))
        {
            overrides.ClearAll();
            toast = Strings.ConfigVerdictsRestored;
            toastUntilUtc = DateTime.UtcNow + ToastDuration;
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Strings.ConfigVerdictRestoreAllTooltip);
        }
    }

    /// <summary>Rebuilds the verdict rows (sorted by quest name) when the overrides or the catalog changed.</summary>
    private void RefreshVerdictRows(IUniqueOverrides overrides)
    {
        var bundle = session.Bundle;
        var spoilers = session.Spoilers;
        if (overrides.Version == verdictVersion && ReferenceEquals(bundle, verdictBundle) && spoilers.Fingerprint == verdictSpoilers)
        {
            return;
        }

        verdictVersion = overrides.Version;
        verdictBundle = bundle;
        verdictSpoilers = spoilers.Fingerprint;
        var all = overrides.All;
        var list = new List<VerdictRow>(all.Count);
        foreach (var (rowId, stored) in all)
        {
            var name = bundle?.Catalog.GetByRowId(rowId) is { } quest ? spoilers.DisplayName(quest) : Strings.MoonlitQuestPrefix + rowId.ToString(CultureInfo.InvariantCulture);
            list.Add(new VerdictRow(
                rowId,
                name,
                stored.Unique ? Strings.ConfigVerdictUnique : Strings.ConfigVerdictNotUnique,
                stored.Unique ? Theme.Silver : Theme.Dusk,
                stored.Note ?? string.Empty,
                stored.MarkedUtc?.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty));
        }

        list.Sort(static (a, b) => string.Compare(a.QuestName, b.QuestName, StringComparison.CurrentCultureIgnoreCase));
        verdictRows = list.ToArray();
        verdictsHeader = string.Format(CultureInfo.InvariantCulture, Strings.ConfigVerdictsHeaderFormat, verdictRows.Length);
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

        using (Theme.PushText(Theme.Silver))
        {
            ImGui.TextUnformatted(toast);
        }
    }

    private void DrawAbout()
    {
        Header(Strings.ConfigSectionAbout);
        RefreshCatalogLine();
        ImGui.TextUnformatted(pluginVersionLine);
        ImGui.TextUnformatted(dataStampLine);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.ConfigDataStampTooltip);
        }

        if (dataVersionWarning is { } warning)
        {
            using var eclipse = Theme.PushText(Theme.Eclipse);
            ImGui.TextWrapped(warning);
        }

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

    private readonly record struct VerdictRow(uint RowId, string QuestName, string Verdict, Vector4 Color, string Note, string Date);
}
