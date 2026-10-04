using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the 1.22.0 moon icon (feature plan v8 H1; spec-1.22 H1): its menu, its Hide toast, its hints, its quick
/// card and Settings › In game › Moon icon. English only until localization reopens.
/// </summary>
static partial class Strings
{
    /// <summary>The command that shows the icon again, as the menu and the Hide toast print it.</summary>
    public const string MoonIconCommand = "/tsuki icon";

    // ---- The menu (words only: a padlock would mean Blocked, and a cog adds nothing a word doesn't say) ----

    public static string MoonIconMenuLock => Loc.Get("MoonIconMenuLock");
    public static string MoonIconMenuUnlock => Loc.Get("MoonIconMenuUnlock");
    public static string MoonIconMenuHide => Loc.Get("MoonIconMenuHide");
    public static string MoonIconMenuSettings => Loc.Get("MoonIconMenuSettings");

    // ---- Hints and the Hide toast ----

    public static string MoonIconFirstRunHint => Loc.Get("MoonIconFirstRunHint");
    public static string MoonIconLockedHint => Loc.Get("MoonIconLockedHint");
    public static string MoonIconHidden => Loc.Get("MoonIconHidden");
    public static string MoonIconShowsAgain => Loc.Get("MoonIconShowsAgain");

    // ---- The quick card ----

    public static string MoonIconCardHint => Loc.Get("MoonIconCardHint");

    /// <summary>{0} = the character's first name, {1} = job abbreviation, {2} = level.</summary>
    public static string MoonIconCardWhoFormat => Loc.Get("MoonIconCardWhoFormat");

    /// <summary>{0} = Ready quests (two or more).</summary>
    public static string MoonIconCardReadyFormat => Loc.Get("MoonIconCardReadyFormat");

    public static string MoonIconCardReadyOne => Loc.Get("MoonIconCardReadyOne");
    public static string MoonIconCardReadyNone => Loc.Get("MoonIconCardReadyNone");

    /// <summary>{0} = job abbreviation.</summary>
    public static string MoonIconCardOnJobFormat => Loc.Get("MoonIconCardOnJobFormat");

    /// <summary>{0} = the version ready ("1.23.0").</summary>
    public static string MoonIconCardUpdateFormat => Loc.Get("MoonIconCardUpdateFormat");

    public static string MoonIconCardUpdate => Loc.Get("MoonIconCardUpdate");
    public static string MoonIconCardShow => Loc.Get("MoonIconCardShow");

    // ---- Settings › In game › Moon icon ----

    public static string ConfigSectionMoonIcon => Loc.Get("ConfigSectionMoonIcon");
    public static string ConfigMoonIconShow => Loc.Get("ConfigMoonIconShow");
    public static string ConfigMoonIconShowHint => Loc.Get("ConfigMoonIconShowHint");
    public static string ConfigMoonIconSize => Loc.Get("ConfigMoonIconSize");
    public static string ConfigMoonIconSizeHint => Loc.Get("ConfigMoonIconSizeHint");
    public static string ConfigMoonIconSizeSmall => Loc.Get("ConfigMoonIconSizeSmall");
    public static string ConfigMoonIconSizeMedium => Loc.Get("ConfigMoonIconSizeMedium");
    public static string ConfigMoonIconSizeLarge => Loc.Get("ConfigMoonIconSizeLarge");
    public static string ConfigMoonIconLock => Loc.Get("ConfigMoonIconLock");
    public static string ConfigMoonIconLockHint => Loc.Get("ConfigMoonIconLockHint");
    public static string ConfigMoonIconHideCutscenes => Loc.Get("ConfigMoonIconHideCutscenes");
    public static string ConfigMoonIconHideCutscenesHint => Loc.Get("ConfigMoonIconHideCutscenesHint");
    public static string ConfigMoonIconHideGroupPose => Loc.Get("ConfigMoonIconHideGroupPose");
    public static string ConfigMoonIconHideGroupPoseHint => Loc.Get("ConfigMoonIconHideGroupPoseHint");
    public static string ConfigMoonIconHideDuties => Loc.Get("ConfigMoonIconHideDuties");
    public static string ConfigMoonIconHideDutiesHint => Loc.Get("ConfigMoonIconHideDutiesHint");
    public static string ConfigMoonIconOffReason => Loc.Get("ConfigMoonIconOffReason");
    public static string ConfigMoonIconReset => Loc.Get("ConfigMoonIconReset");
    public static string ConfigMoonIconResetHint => Loc.Get("ConfigMoonIconResetHint");
}
