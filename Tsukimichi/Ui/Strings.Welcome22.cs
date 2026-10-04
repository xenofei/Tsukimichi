using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Strings for 1.22.0 updates and Umbra (plan v8 U1, M1, M3): the status bar's update note and Settings › About ›
/// Updates, the server info bar entry and its tooltip, and Settings › About › Umbra with the Follow Umbra palette. Every
/// constant is prefixed <c>Update</c>, <c>ServerBar</c>, <c>Umbra</c> or <c>FollowUmbra</c> (and two Settings headings).
/// </summary>
static partial class Strings
{
    /// <summary>Settings › About block heading (1.22 U1).</summary>
    public static string SettingsUpdatesHeading => Loc.Get("SettingsUpdatesHeading");

    /// <summary>The status bar's update note (1.22 U1). {0} = the new version.</summary>
    public static string UpdateReadyFormat => Loc.Get("UpdateReadyFormat");

    /// <summary>The update note's button: opens Dalamud's plugin installer on Can be updated (1.22 U1).</summary>
    public static string UpdateButton => Loc.Get("UpdateButton");

    /// <summary>Tooltip on Update (1.22 U1).</summary>
    public static string UpdateButtonTooltip => Loc.Get("UpdateButtonTooltip");

    /// <summary>Tooltip on the update note's × (1.22 U1).</summary>
    public static string UpdateLaterTooltip => Loc.Get("UpdateLaterTooltip");

    /// <summary>First line of the update note's hover (1.22 U1). {0} = the new version.</summary>
    public static string UpdateHoverTitleFormat => Loc.Get("UpdateHoverTitleFormat");

    /// <summary>Last line of the update note's hover and of Settings › About › What's in it (1.22 U1).</summary>
    public static string UpdateHoverFoot => Loc.Get("UpdateHoverFoot");

    /// <summary>The update hover when Dalamud has no changelog (1.22 U1).</summary>
    public static string UpdateNoNotes => Loc.Get("UpdateNoNotes");

    /// <summary>The opt-in chat line when a version is ready (1.22 U1, decision 6). {0} = the new version.</summary>
    public static string UpdateChatFormat => Loc.Get("UpdateChatFormat");

    /// <summary>Settings › About › Updates switch (1.22 U1). At most 40 characters.</summary>
    public static string UpdateCheckLabel => Loc.Get("UpdateCheckLabel");

    /// <summary>Hint under the update switch (1.22 U1). At most 110 characters.</summary>
    public static string UpdateCheckHint => Loc.Get("UpdateCheckHint");

    /// <summary>Settings › About › Updates sub-switch (1.22 U1, decision 6).</summary>
    public static string UpdateChatLabel => Loc.Get("UpdateChatLabel");

    /// <summary>Hint under Also say it in chat (1.22 U1).</summary>
    public static string UpdateChatHint => Loc.Get("UpdateChatHint");

    /// <summary>Shown in place of the hint while the update switch is off (1.22 U1).</summary>
    public static string UpdateChatOffReason => Loc.Get("UpdateChatOffReason");

    /// <summary>Settings › About › Updates status line while a version waits (1.22 U1). {0} = the new version.</summary>
    public static string UpdateSettingsReadyFormat => Loc.Get("UpdateSettingsReadyFormat");

    /// <summary>Hint under the ready status line (1.22 U1).</summary>
    public static string UpdateSettingsReadyHint => Loc.Get("UpdateSettingsReadyHint");

    /// <summary>Settings › About › Updates status line (1.22 U1). {0} = an age ("6 min ago").</summary>
    public static string UpdateSettingsUpToDateFormat => Loc.Get("UpdateSettingsUpToDateFormat");

    /// <summary>Settings › About › Updates status line before the first answer (1.22 U1).</summary>
    public static string UpdateSettingsWaiting => Loc.Get("UpdateSettingsWaiting");

    /// <summary>Settings › About › Updates status line while the switch is off (1.22 U1).</summary>
    public static string UpdateSettingsOff => Loc.Get("UpdateSettingsOff");

    /// <summary>Settings › About › Updates: opens the new version's plain notes (1.22 U1).</summary>
    public static string UpdateWhatsInIt => Loc.Get("UpdateWhatsInIt");

    /// <summary>Settings › About › Updates: the notes are open (1.22 U1).</summary>
    public static string UpdateWhatsInItOpen => Loc.Get("UpdateWhatsInItOpen");

    /// <summary>The server info bar entry after the moon (1.22 M1). {0} = quests Ready on the current job.</summary>
    public static string ServerBarReadyFormat => Loc.Get("ServerBarReadyFormat");

    /// <summary>The server info bar entry counting this zone (1.22 M1). {0} = quests that can start here.</summary>
    public static string ServerBarHereFormat => Loc.Get("ServerBarHereFormat");

    /// <summary>The entry at zero with Show at zero on (1.22 M1).</summary>
    public static string ServerBarNothingReady => Loc.Get("ServerBarNothingReady");

    /// <summary>The zone entry at zero with Show at zero on (1.22 M1).</summary>
    public static string ServerBarNothingHere => Loc.Get("ServerBarNothingHere");

    /// <summary>Server info bar tooltip line (1.22 M1). {0} = Up next's quest, through the spoiler shield.</summary>
    public static string ServerBarUpNextFormat => Loc.Get("ServerBarUpNextFormat");

    /// <summary>Server info bar tooltip (1.22 M1). {0} = count; {1} = job abbreviation.</summary>
    public static string ServerBarTipReadyFormat => Loc.Get("ServerBarTipReadyFormat");

    /// <summary>Server info bar tooltip (1.22 M1). {0} = job abbreviation.</summary>
    public static string ServerBarTipReadyOneFormat => Loc.Get("ServerBarTipReadyOneFormat");

    /// <summary>Server info bar tooltip (1.22 M1). {0} = job abbreviation.</summary>
    public static string ServerBarTipNothingReadyFormat => Loc.Get("ServerBarTipNothingReadyFormat");

    /// <summary>Server info bar tooltip, after the Ready count (1.22 M1). {0} = quests that can start in this zone.</summary>
    public static string ServerBarTipHereFormat => Loc.Get("ServerBarTipHereFormat");

    /// <summary>Server info bar tooltip, after the Ready count, when no quest starts in this zone (1.22 M1).</summary>
    public static string ServerBarTipHereNone => Loc.Get("ServerBarTipHereNone");

    /// <summary>Server info bar tooltip (1.22 M1). {0} = used; {1} = slots; {2} = left.</summary>
    public static string ServerBarJournalFormat => Loc.Get("ServerBarJournalFormat");

    /// <summary>Server info bar tooltip (1.22 M1). {0} = used; {1} = slots.</summary>
    public static string ServerBarJournalOneFormat => Loc.Get("ServerBarJournalOneFormat");

    /// <summary>Server info bar tooltip (1.22 M1). {0} = used; {1} = slots.</summary>
    public static string ServerBarJournalFullFormat => Loc.Get("ServerBarJournalFullFormat");

    /// <summary>Server info bar tooltip (1.22 M1). {0} = event; {1} = days left.</summary>
    public static string ServerBarEndsInFormat => Loc.Get("ServerBarEndsInFormat");

    /// <summary>Server info bar tooltip (1.22 M1). {0} = event.</summary>
    public static string ServerBarEndsTomorrowFormat => Loc.Get("ServerBarEndsTomorrowFormat");

    /// <summary>Server info bar tooltip (1.22 M1). {0} = event.</summary>
    public static string ServerBarEndsTodayFormat => Loc.Get("ServerBarEndsTodayFormat");

    /// <summary>Server info bar tooltip's last line (1.22 M1).</summary>
    public static string ServerBarClicksReady => Loc.Get("ServerBarClicksReady");

    /// <summary>Server info bar tooltip's last line while the entry counts this zone (1.22 M1).</summary>
    public static string ServerBarClicksZone => Loc.Get("ServerBarClicksZone");

    /// <summary>Hint under Count in the server info bar (1.22 M1). At most 110 characters.</summary>
    public static string ServerBarShowHint => Loc.Get("ServerBarShowHint");

    /// <summary>Settings › In game › Server info bar choice (1.22 M1).</summary>
    public static string ServerBarCountsLabel => Loc.Get("ServerBarCountsLabel");

    /// <summary>Hint under The entry counts (1.22 M1).</summary>
    public static string ServerBarCountsHint => Loc.Get("ServerBarCountsHint");

    /// <summary>The entry counts: Ready quests (1.22 M1, the default).</summary>
    public static string ServerBarCountsReady => Loc.Get("ServerBarCountsReady");

    /// <summary>The entry counts: quests that can start in this zone (1.22 M1).</summary>
    public static string ServerBarCountsZone => Loc.Get("ServerBarCountsZone");

    /// <summary>Settings › About block heading (1.22 M3).</summary>
    public static string SettingsUmbraHeading => Loc.Get("SettingsUmbraHeading");

    /// <summary>Settings › About › Umbra, without Umbra (1.22 M3).</summary>
    public static string UmbraNotInstalled => Loc.Get("UmbraNotInstalled");

    /// <summary>Under Umbra isn't installed (1.22 M3).</summary>
    public static string UmbraNotInstalledHint => Loc.Get("UmbraNotInstalledHint");

    /// <summary>Settings › About › Umbra status label (1.22 M3).</summary>
    public static string UmbraRunning => Loc.Get("UmbraRunning");

    /// <summary>Under Umbra is running (1.22 M3).</summary>
    public static string UmbraBarTop => Loc.Get("UmbraBarTop");

    /// <summary>Under Umbra is running (1.22 M3).</summary>
    public static string UmbraBarBottom => Loc.Get("UmbraBarBottom");

    /// <summary>Under Umbra is running: a floating or auto-hidden bar (1.22 M3).</summary>
    public static string UmbraBarFloats => Loc.Get("UmbraBarFloats");

    /// <summary>Under Umbra is running: the bar is off (1.22 M3).</summary>
    public static string UmbraBarHidden => Loc.Get("UmbraBarHidden");

    /// <summary>Under Umbra is running when its settings can't be read (1.22 M3). {0} = the assumed height.</summary>
    public static string UmbraUnreadFormat => Loc.Get("UmbraUnreadFormat");

    /// <summary>Settings › About › Umbra, while Umbra's settings can't be read (1.22 M3).</summary>
    public static string UmbraAssumedLabel => Loc.Get("UmbraAssumedLabel");

    /// <summary>Hint under Assumed toolbar height (1.22 M3).</summary>
    public static string UmbraAssumedHint => Loc.Get("UmbraAssumedHint");

    /// <summary>Slider text of Assumed toolbar height (1.22 M3): a printf specifier, keep %d.</summary>
    public static string UmbraAssumedFormat => Loc.Get("UmbraAssumedFormat");

    /// <summary>Settings › About › Umbra: the add-on (1.22 M3).</summary>
    public static string UmbraAddonLabel => Loc.Get("UmbraAddonLabel");

    /// <summary>Under Tsukimichi for Umbra (1.22 M3). {0} = the add-on's version.</summary>
    public static string UmbraAddonAddedFormat => Loc.Get("UmbraAddonAddedFormat");

    /// <summary>Under Tsukimichi for Umbra when Umbra lists it but it has not said hello yet (1.22 M3).</summary>
    public static string UmbraAddonListed => Loc.Get("UmbraAddonListed");

    /// <summary>Under Tsukimichi for Umbra (1.22 M3).</summary>
    public static string UmbraAddonMissing => Loc.Get("UmbraAddonMissing");

    /// <summary>Opens the three steps (1.22 M3).</summary>
    public static string UmbraHowToAdd => Loc.Get("UmbraHowToAdd");

    /// <summary>The three steps are open (1.22 M3).</summary>
    public static string UmbraHowToAddOpen => Loc.Get("UmbraHowToAddOpen");

    /// <summary>Before the three steps (1.22 M3).</summary>
    public static string UmbraHowLead => Loc.Get("UmbraHowLead");

    /// <summary>Step 1 (1.22 M3).</summary>
    public static string UmbraHowStep1 => Loc.Get("UmbraHowStep1");

    /// <summary>Step 2 (1.22 M3). {0} = the repository.</summary>
    public static string UmbraHowStep2Format => Loc.Get("UmbraHowStep2Format");

    /// <summary>Step 3 (1.22 M3).</summary>
    public static string UmbraHowStep3 => Loc.Get("UmbraHowStep3");

    /// <summary>Copies xenofei/Tsukimichi.Umbra (1.22 M3).</summary>
    public static string UmbraCopyRepository => Loc.Get("UmbraCopyRepository");

    /// <summary>Settings › About › Umbra: the palette row (1.22 M3, decision 4).</summary>
    public static string FollowUmbraLabel => Loc.Get("FollowUmbraLabel");

    /// <summary>Under Follow Umbra (1.22 M3).</summary>
    public static string FollowUmbraInUse => Loc.Get("FollowUmbraInUse");

    /// <summary>Under Follow Umbra (1.22 M3).</summary>
    public static string FollowUmbraNotInUse => Loc.Get("FollowUmbraNotInUse");

    /// <summary>Under Follow Umbra (1.22 M3).</summary>
    public static string FollowUmbraFellBack => Loc.Get("FollowUmbraFellBack");

    /// <summary>Opens Settings › Themes (1.22 M3).</summary>
    public static string FollowUmbraThemesLink => Loc.Get("FollowUmbraThemesLink");

    /// <summary>Palette tile name (1.22 M3, decision 4).</summary>
    public static string PaletteNameFollowUmbra => Loc.Get("PaletteNameFollowUmbra");

    /// <summary>Follow Umbra tile hover (1.22 M3). {0} = Umbra's colour profile name.</summary>
    public static string FollowUmbraTileFormat => Loc.Get("FollowUmbraTileFormat");

    /// <summary>Follow Umbra tile hover, when the contrast clamp moved the text (1.22 M3).</summary>
    public static string FollowUmbraTileClamped => Loc.Get("FollowUmbraTileClamped");

    /// <summary>Follow Umbra tile hover (1.22 M3).</summary>
    public static string FollowUmbraTileUnread => Loc.Get("FollowUmbraTileUnread");

    /// <summary>Follow Umbra tile hover (1.22 M3).</summary>
    public static string FollowUmbraTileNoUmbra => Loc.Get("FollowUmbraTileNoUmbra");
}
