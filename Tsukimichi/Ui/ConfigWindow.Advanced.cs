using System;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Config;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.GameData;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Advanced (feature plan v6 U7): the keyboard (what is always bound, and the opt-in shortcuts, off by
/// default), chat command aliases (<c>ConfigWindow.Commands.cs</c>), safety (how changes are confirmed, the hold length,
/// two clicks instead), the refresh rate with the measured cost of a refresh, hidden quest filing, running the game
/// panels on an untested patch, confirming Stop while Questionable runs a command, and Diagnostics (folded; the plugin,
/// data and quest list stamps, the refresh timing and Retry), which is About folded in.
/// </summary>
public sealed partial class ConfigWindow
{
    private static readonly LocArray FilingOptions = new(static () => [Strings.ConfigJournalFilingRefiled, Strings.ConfigJournalFilingLegacy]);

    private CatalogBundle? aboutBundle;
    private int aboutLanguage = -1;
    private string catalogLine = Strings.ConfigCatalogLoading;
    private string? catalogError;
    private int catalogErrorLanguage = -1;
    private string? catalogRebuildLine;
    private bool diagnosticsOpen;

    // Refresh timing lines, rebuilt only when another refresh completed.
    private int pollTimingCount = -1;
    private int pollTimingLanguage = -1;
    private string pollTimingLine = Strings.ConfigPollTimingNone;
    private string pollCostLine = Strings.ConfigPollCostUnknown;

    /// <summary>
    /// Settings › Advanced › Keyboard (T17, accessibility A7): what is always bound, the warning that the game sees the
    /// keys too, and the opt-in shortcuts, all off by default. Saved at once.
    /// </summary>
    private void DrawKeyboard()
    {
        Header(Strings.ConfigSectionKeyboard);
        Note(Strings.SettingsKeysAlwaysOn, Strings.ConfigKeyboardAlwaysOn, "keys shortcuts hotkeys keybinds always escape search");
        Note(Strings.SettingsKeysGameSees, Strings.ConfigKeyboardGameSeesKeys, "keys shortcuts hotkeys game hotbar");

        var tabs = settings.ShortcutTabs;
        if (Toggle(Strings.ConfigShortcutTabs, Strings.ConfigShortcutTabsHint, ref tabs, "shortcut keys hotkey tabs"))
        {
            settings.ShortcutTabs = tabs;
            Save();
        }

        var flag = settings.ShortcutFlag;
        if (Toggle(Strings.ConfigShortcutFlag, Strings.ConfigShortcutFlagHint, ref flag, "shortcut keys hotkey map flag"))
        {
            settings.ShortcutFlag = flag;
            Save();
        }

        var reveal = settings.ShortcutReveal;
        if (Toggle(Strings.ConfigShortcutReveal, Strings.ConfigShortcutRevealHint, ref reveal, "shortcut keys hotkey reveal journal"))
        {
            settings.ShortcutReveal = reveal;
            Save();
        }

        var pin = settings.ShortcutPin;
        if (Toggle(Strings.ConfigShortcutPin, Strings.ConfigShortcutPinHint, ref pin, "shortcut keys hotkey pin"))
        {
            settings.ShortcutPin = pin;
            Save();
        }
    }

    /// <summary>
    /// Settings › Advanced › Safety (feature plan v6 S2): how actions that change data are confirmed, said once in plain
    /// words, then the press-and-hold length (saved once the slider is still) and, for hand strain, two clicks in place
    /// of a held key or button (saved at once). There is no "off": the owner asked for the safety.
    /// </summary>
    private void DrawSafety()
    {
        Header(Strings.ConfigSafetyHeading);
        Note(Strings.ConfigSafetyRules, Strings.ConfigSafetyRulesHint, "safety confirm undo shift ctrl control hold");

        if (Setting(Strings.ConfigSafetyHold, Strings.ConfigSafetyHoldHint, "safety hold press length seconds duration delete forget", enabled: !settings.SafetyTwoClick, reason: Strings.ConfigSafetyHoldOffHint))
        {
            var hold = settings.SafetyHoldSecondsClamped;
            ImGui.SetNextItemWidth(ControlWidth);
            if (ImGui.SliderFloat("##hold", ref hold, SafetyRules.MinHoldSeconds, SafetyRules.MaxHoldSeconds, "%.1f s", ImGuiSliderFlags.AlwaysClamp))
            {
                settings.SafetyHoldSeconds = SafetyRules.ClampHoldSeconds(hold);
                SaveSoon();
            }

            EndSetting();
        }

        var twoClick = settings.SafetyTwoClick;
        if (Toggle(Strings.ConfigSafetyTwoClick, Strings.ConfigSafetyTwoClickHint, ref twoClick, "safety two clicks double hand strain accessibility"))
        {
            settings.SafetyTwoClick = twoClick;
            Save();
        }
    }

    /// <summary>Settings › Advanced › Refresh: how often the live character is re-read (saved once the slider is still) and what a refresh costs.</summary>
    private void DrawPolling()
    {
        Header(Strings.ConfigSectionPolling);
        if (!Setting(Strings.ConfigPollInterval, Strings.ConfigPollIntervalHint, "polling poll refresh rate frequency seconds"))
        {
            return;
        }

        var seconds = (float)settings.PollInterval.TotalSeconds;
        ImGui.SetNextItemWidth(ControlWidth);
        if (ImGui.SliderFloat("##poll", ref seconds, (float)Configuration.MinPollIntervalSeconds, (float)Configuration.MaxPollIntervalSeconds, "%.1f s", ImGuiSliderFlags.AlwaysClamp))
        {
            settings.PollIntervalSeconds = Math.Round(seconds, 1);
            SaveSoon();
        }

        RefreshPollTiming();
        SettingNote(pollCostLine);
        EndSetting();
    }

    /// <summary>
    /// Settings › Advanced › Hidden quest filing: Sorted (the 0.6.1 rules) or Legacy (the genre-less quests stay in the
    /// removed bucket, as before). Saved at once; the plugin rebuilds the catalog through <see cref="JournalFilingChanged"/>.
    /// </summary>
    private void DrawJournalFiling()
    {
        Header(Strings.ConfigJournalFiling);
        var filing = settings.JournalFiling == JournalFiling.Legacy ? 1 : 0;
        if (!Choice(Strings.SettingsFilingLabel, Strings.ConfigJournalFilingHint, ref filing, FilingOptions.Value, "refiled sorted legacy catalog hidden quests filing"))
        {
            return;
        }

        var next = filing == 1 ? JournalFiling.Legacy : JournalFiling.Refiled;
        if (next == settings.JournalFiling)
        {
            return;
        }

        settings.JournalFiling = next;
        Save();
        JournalFilingChanged?.Invoke(next);
    }

    /// <summary>
    /// Settings › Advanced › After a game patch (T20): "Run game panels on an untested patch", scoped to the running
    /// game version, which re-registers them at once through <see cref="HookGate.Changed"/>.
    /// </summary>
    private void DrawHookGate()
    {
        Header(Strings.ConfigSectionHooks);
        if (HookGate is { } gate)
        {
            // On only when the stored override names the running version: one from an earlier patch reads off. Turning it
            // on stores the running version, off clears it. Without a known running version there is nothing to store
            // (and the gate already allows the hooks), so the toggle is disabled.
            var anyway = gate.EnableAnywayApplies;
            if (ToggleSetting(Strings.HooksEnableUntested, Strings.HooksEnableUntestedHint, "hooks patch paused override untested game version panels", gate.RunningVersion.Length > 0, reason: Strings.SettingsHooksNoVersion))
            {
                if (RowToggle(ref anyway))
                {
                    var version = anyway ? gate.RunningVersion : string.Empty;
                    settings.EnableHooksOnUntestedVersion = version;
                    Save();
                    gate.SetEnableAnyway(version);
                }

                if (gate.IsPaused)
                {
                    SettingNote(Strings.SettingsHooksPausedHint, Theme.EclipseText);
                }
                else if (gate.Decision.Verdict == HookGateVerdict.Overridden)
                {
                    SettingNote(Strings.HooksRunningUntested);
                }

                EndSetting();
            }
        }
    }

    /// <summary>
    /// Settings › Advanced › Diagnostics (About, folded in): the plugin's version, the reward data's stamp, the game
    /// version warning, the curated lists, the quest list with Retry after a failed build, and the refresh timing. Folded
    /// until asked for.
    /// </summary>
    private void DrawDiagnostics()
    {
        Header(Strings.SettingsDiagnosticsHeading);
        if (!Setting(Strings.SettingsDiagnostics, Strings.SettingsDiagnosticsHint, "version catalog data stamp poll timing diagnostics about bug report"))
        {
            return;
        }

        if (ImGui.Button(diagnosticsOpen ? Strings.SettingsHideButton : Strings.SettingsShowButton))
        {
            diagnosticsOpen = !diagnosticsOpen;
        }

        if (diagnosticsOpen)
        {
            SettingBelow();
            RefreshCatalogLine();
            using (Typography.Caption())
            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                ImGui.TextUnformatted(pluginVersionLine.Value);
                ImGui.TextUnformatted(dataStampLine);
                HintOnHover(Strings.ConfigDataStampTooltip);
                ImGui.TextUnformatted(curatedLine.Value);
                ImGui.TextUnformatted(catalogLine);
                RefreshPollTiming();
                ImGui.TextUnformatted(pollTimingLine);
            }

            if (diagnostics.VersionMismatchWarning is { } warning)
            {
                using var eclipse = Theme.PushText(Theme.EclipseText);
                ImGui.TextWrapped(warning);
            }

            DrawCatalogFailure();
        }

        EndSetting();
    }

    /// <summary>"Last poll … · average … · n polls" and the cost line, once per completed refresh.</summary>
    private void RefreshPollTiming()
    {
        var count = session.PollCount;
        if (count == pollTimingCount && pollTimingLanguage == Loc.Version)
        {
            return;
        }

        pollTimingCount = count;
        pollTimingLanguage = Loc.Version;
        if (count == 0)
        {
            pollTimingLine = Strings.ConfigPollTimingNone;
            pollCostLine = Strings.ConfigPollCostUnknown;
            return;
        }

        pollTimingLine = string.Format(CultureInfo.InvariantCulture, Strings.ConfigPollTimingFormat, session.LastPollMs, session.AveragePollMs, count);
        pollCostLine = string.Format(CultureInfo.InvariantCulture, Strings.ConfigPollCostFormat, session.AveragePollMs);
    }

    /// <summary>
    /// Under the quest list's line once a build failed and none is running: with an older list still in use, the
    /// rebuild's failure; then Retry, as on the main window's panel.
    /// </summary>
    private void DrawCatalogFailure()
    {
        if (session.CatalogError is null || session.CatalogLoading)
        {
            return;
        }

        if (catalogRebuildLine is { } line)
        {
            using var eclipse = Theme.PushText(Theme.EclipseText);
            ImGui.TextWrapped(line);
        }

        if (RetryCatalog is { } retry && ImGui.SmallButton(Strings.Retry + "##catalogRetry"))
        {
            retry();
        }
    }

    /// <summary>The quest list's language and size, rebuilt when the bundle (or the error) changes.</summary>
    private void RefreshCatalogLine()
    {
        var bundle = session.Bundle;
        if (bundle is not null)
        {
            if (!ReferenceEquals(bundle, aboutBundle) || aboutLanguage != Loc.Version)
            {
                aboutBundle = bundle;
                aboutLanguage = Loc.Version;
                catalogLine = string.Format(CultureInfo.CurrentCulture, Strings.ConfigCatalogFormat, bundle.Catalog.Count, bundle.Language);
            }

            // A rebuild that failed leaves this catalog in use; the failure gets its own line under this one.
            if (session.CatalogError is not { } rebuildError)
            {
                catalogRebuildLine = null;
            }
            else if (catalogRebuildLine is null || rebuildError != catalogError || catalogErrorLanguage != Loc.Version)
            {
                catalogError = rebuildError;
                catalogErrorLanguage = Loc.Version;
                catalogRebuildLine = string.Format(CultureInfo.CurrentCulture, Strings.CatalogRebuildFailedFormat, rebuildError);
            }

            return;
        }

        catalogRebuildLine = null;

        if (session.CatalogError is { } error)
        {
            if (error != catalogError || catalogErrorLanguage != Loc.Version)
            {
                catalogError = error;
                catalogErrorLanguage = Loc.Version;
                catalogLine = string.Format(CultureInfo.CurrentCulture, Strings.ConfigCatalogUnavailableFormat, error);
            }

            return;
        }

        catalogLine = Strings.ConfigCatalogLoading;
    }
}
