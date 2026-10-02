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
}
