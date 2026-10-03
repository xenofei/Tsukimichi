using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the Settings window's frame (feature plan v5, 1.7.0 "Settings"): the search box, Help &amp; tour, the
/// empty search state and the section and block headings added with the section index. English only until
/// localization reopens.
/// </summary>
static partial class Strings
{
    // ---- Sections and blocks ----
    public static string ConfigSectionAdvanced => Loc.Get("ConfigSectionAdvanced");
    public static string ConfigSectionTravel => Loc.Get("ConfigSectionTravel");
    public static string ConfigSectionAutoDuty => Loc.Get("ConfigSectionAutoDuty");
    public static string ConfigSectionWotsit => Loc.Get("ConfigSectionWotsit");
    public static string ConfigSectionNearby => Loc.Get("ConfigSectionNearby");
    public static string ConfigSectionGameWindows => Loc.Get("ConfigSectionGameWindows");
    public static string ConfigSectionHooks => Loc.Get("ConfigSectionHooks");
    public static string DiscoveryShowDtrHint => Loc.Get("DiscoveryShowDtrHint");

    // ---- Search and help ----
    public static string SettingsSearchHint => Loc.Get("SettingsSearchHint");
    public static string SettingsSearchTooltip => Loc.Get("SettingsSearchTooltip");
    public static string SettingsHelpAndTour => Loc.Get("SettingsHelpAndTour");
    public static string SettingsHelpAndTourTooltip => Loc.Get("SettingsHelpAndTourTooltip");
    public static string SettingsNoMatchHeading => Loc.Get("SettingsNoMatchHeading");
    public static string SettingsNoMatchBody => Loc.Get("SettingsNoMatchBody");
    public static string SettingsClearSearch => Loc.Get("SettingsClearSearch");

    // ---- The 1.13.0 rebuild (feature plan v6 U7): pages, intros, new rows and reasons ----
    public static string SettingsPageGeneral => Loc.Get("Settings.Page.General");
    public static string SettingsPageJournal => Loc.Get("Settings.Page.Journal");
    public static string SettingsPageOverlay => Loc.Get("Settings.Page.Overlay");
    public static string SettingsPageAlerts => Loc.Get("Settings.Page.Alerts");
    public static string SettingsPageInGame => Loc.Get("Settings.Page.InGame");
    public static string SettingsPageAutomation => Loc.Get("Settings.Page.Automation");
    public static string SettingsPageCharacters => Loc.Get("Settings.Page.Characters");
    public static string SettingsIntroGeneral => Loc.Get("Settings.Intro.General");
    public static string SettingsIntroJournal => Loc.Get("Settings.Intro.Journal");
    public static string SettingsIntroOverlay => Loc.Get("Settings.Intro.Overlay");
    public static string SettingsIntroAlerts => Loc.Get("Settings.Intro.Alerts");
    public static string SettingsIntroSpoilers => Loc.Get("Settings.Intro.Spoilers");
    public static string SettingsIntroInGame => Loc.Get("Settings.Intro.InGame");
    public static string SettingsIntroAutomation => Loc.Get("Settings.Intro.Automation");
    public static string SettingsIntroCharacters => Loc.Get("Settings.Intro.Characters");
    public static string SettingsIntroAdvanced => Loc.Get("Settings.Intro.Advanced");
    public static string SettingsSizeHeading => Loc.Get("Settings.SizeHeading");
    public static string SettingsTextSize => Loc.Get("Settings.TextSize");
    public static string SettingsTextSizeHint => Loc.Get("Settings.TextSizeHint");
    public static string SettingsResetSizes => Loc.Get("Settings.ResetSizes");
    public static string SettingsResetSizesHint => Loc.Get("Settings.ResetSizesHint");
    public static string SettingsResetButton => Loc.Get("Settings.ResetButton");
    public static string SettingsGameFontsPlainReason => Loc.Get("Settings.GameFontsPlainReason");
    public static string SettingsMovingNightSky => Loc.Get("Settings.MovingNightSky");
    public static string SettingsMovingNightSkyHint => Loc.Get("Settings.MovingNightSkyHint");
    public static string SettingsCompletionMeteor => Loc.Get("Settings.CompletionMeteor");
    public static string SettingsCompletionMeteorHint => Loc.Get("Settings.CompletionMeteorHint");
    public static string SettingsMilkyWay => Loc.Get("Settings.MilkyWay");
    public static string SettingsMilkyWayHint => Loc.Get("Settings.MilkyWayHint");
    public static string SettingsSkyFullReason => Loc.Get("Settings.SkyFullReason");
    public static string SettingsSkyMotionReason => Loc.Get("Settings.SkyMotionReason");
    public static string SettingsMainWindowHeading => Loc.Get("Settings.MainWindowHeading");
    public static string SettingsStartTutorialHint => Loc.Get("Settings.StartTutorialHint");
    public static string SettingsStartButton => Loc.Get("Settings.StartButton");
    public static string SettingsTableHeading => Loc.Get("Settings.TableHeading");
    public static string SettingsTreeHeading => Loc.Get("Settings.TreeHeading");
    public static string SettingsShowInOverlayHeading => Loc.Get("Settings.ShowInOverlayHeading");
    public static string SettingsOverlayOffReason => Loc.Get("Settings.OverlayOffReason");
    public static string TodoConfigOpacityHint => Loc.Get("TodoConfigOpacityHint");
    public static string TodoConfigShowPinsHint => Loc.Get("TodoConfigShowPinsHint");
    public static string TodoConfigShowNearbyHint => Loc.Get("TodoConfigShowNearbyHint");
    public static string TodoConfigShowMsqHint => Loc.Get("TodoConfigShowMsqHint");
    public static string TodoConfigShowJobQuestsHint => Loc.Get("TodoConfigShowJobQuestsHint");
    public static string ConfigChatNoticeHint => Loc.Get("ConfigChatNoticeHint");
    public static string ConfigIncludeMsqHint => Loc.Get("ConfigIncludeMsqHint");
    public static string SettingsNoticeOffReason => Loc.Get("Settings.NoticeOffReason");
    public static string JobsConfigNudgeHint => Loc.Get("JobsConfigNudgeHint");
    public static string SettingsPayoffOffReason => Loc.Get("Settings.PayoffOffReason");
    public static string SettingsWelcomeBackHeading => Loc.Get("Settings.WelcomeBackHeading");
    public static string SettingsHooksPaused => Loc.Get("Settings.HooksPaused");
    public static string SettingsHooksPausedHint => Loc.Get("Settings.HooksPausedHint");
    public static string DiscoveryDtrShowWhenEmptyHint => Loc.Get("DiscoveryDtrShowWhenEmptyHint");
    public static string SettingsDtrOffReason => Loc.Get("Settings.DtrOffReason");
    public static string DiscoveryIncludeOtherJobHint => Loc.Get("DiscoveryIncludeOtherJobHint");
    public static string SettingsMountOffReason => Loc.Get("Settings.MountOffReason");
    public static string SettingsQuestionableStatus => Loc.Get("Settings.QuestionableStatus");
    public static string SettingsQuestionableStartOffReason => Loc.Get("Settings.QuestionableStartOffReason");
    public static string SettingsCompanionsLabel => Loc.Get("Settings.CompanionsLabel");
    public static string SettingsSpoilerAheadOffReason => Loc.Get("Settings.SpoilerAheadOffReason");
    public static string SettingsDataKept => Loc.Get("Settings.DataKept");
    public static string SettingsVerdicts => Loc.Get("Settings.Verdicts");
    public static string SettingsVerdictsHint => Loc.Get("Settings.VerdictsHint");
    public static string SettingsExportFormatHint => Loc.Get("Settings.ExportFormatHint");
    public static string SettingsExportFolderHint => Loc.Get("Settings.ExportFolderHint");
    public static string SettingsExportHint => Loc.Get("Settings.ExportHint");
    public static string SettingsDangerZoneHeading => Loc.Get("Settings.DangerZoneHeading");
    public static string SettingsDeleteButton => Loc.Get("Settings.DeleteButton");
    public static string SettingsKeysAlwaysOn => Loc.Get("Settings.KeysAlwaysOn");
    public static string SettingsKeysGameSees => Loc.Get("Settings.KeysGameSees");
    public static string SettingsFilingLabel => Loc.Get("Settings.FilingLabel");
    public static string SettingsHooksNoVersion => Loc.Get("Settings.HooksNoVersion");
    public static string SettingsDiagnosticsHeading => Loc.Get("Settings.DiagnosticsHeading");
    public static string SettingsDiagnostics => Loc.Get("Settings.Diagnostics");
    public static string SettingsDiagnosticsHint => Loc.Get("Settings.DiagnosticsHint");
    public static string SettingsShowButton => Loc.Get("Settings.ShowButton");
    public static string SettingsHideButton => Loc.Get("Settings.HideButton");
}
