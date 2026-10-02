using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for 1.7.0 "In the game" (feature plan v5): the clickable chat actions, the "Opened:" line, quest toasts,
/// Chat 2's menu entry, nameplate marks and their settings. English only until localization reopens.
/// </summary>
static partial class Strings
{
    // ---- Chat actions ----

    /// <summary>Between a chat line's text and its actions.</summary>
    public const string ChatActionGap = "  ";

    public static string ChatActionOpen => Loc.Get("ChatActionOpen");
    public static string ChatActionPin => Loc.Get("ChatActionPin");
    public static string ChatActionRoute => Loc.Get("ChatActionRoute");
    public static string ChatActionShow => Loc.Get("ChatActionShow");
    public static string ChatActionShowGone => Loc.Get("ChatActionShowGone");

    /// <summary>{0} = quest name.</summary>
    public static string ChatActionPinnedFormat => Loc.Get("ChatActionPinnedFormat");

    /// <summary>{0} = quest name.</summary>
    public static string ChatActionAlreadyPinnedFormat => Loc.Get("ChatActionAlreadyPinnedFormat");

    public static string ChatActionPinNeedsLogin => Loc.Get("ChatActionPinNeedsLogin");

    // ---- "Opened by that" ----

    /// <summary>{0} = the counts ("2 unlock quests, 8 side quests (3 with a story)").</summary>
    public static string OpenedChatFormat => Loc.Get("OpenedChatFormat");

    public static string OpenedScopeName => Loc.Get("OpenedScopeName");

    // ---- Quest toasts ----

    /// <summary>{0} = reward name.</summary>
    public static string ToastMoonlitFormat => Loc.Get("ToastMoonlitFormat");

    /// <summary>{0} = duty name.</summary>
    public static string ToastDutyFormat => Loc.Get("ToastDutyFormat");

    /// <summary>{0} = how many more rewards.</summary>
    public static string ToastMoreFormat => Loc.Get("ToastMoreFormat");

    // ---- Chat 2 ----

    /// <summary>{0} = the quest's state.</summary>
    public static string ChatTwoOpenQuestFormat => Loc.Get("ChatTwoOpenQuestFormat");

    /// <summary>{0} = quest name, {1} = its state.</summary>
    public static string ChatTwoOpenItemQuestFormat => Loc.Get("ChatTwoOpenItemQuestFormat");

    // ---- Nameplate marks ----
    public static string NamePlatePinned => Loc.Get("NamePlatePinned");
    public static string NamePlateMoonlit => Loc.Get("NamePlateMoonlit");

    /// <summary>{0} = job abbreviation.</summary>
    public static string NamePlateReadyOnFormat => Loc.Get("NamePlateReadyOnFormat");

    public static string NamePlateReadyOnOtherJob => Loc.Get("NamePlateReadyOnOtherJob");

    // ---- Settings › Notices › Chat actions; Settings › Integrations › Chat 2 and nameplates ----
    public static string ConfigSectionChatActions => Loc.Get("ConfigSectionChatActions");
    public static string ConfigSectionChatTwoNamePlates => Loc.Get("ConfigSectionChatTwoNamePlates");
    public static string ConfigChatLinkActions => Loc.Get("ConfigChatLinkActions");
    public static string ConfigChatLinkActionsHint => Loc.Get("ConfigChatLinkActionsHint");
    public static string ConfigChatNoticeOpened => Loc.Get("ConfigChatNoticeOpened");
    public static string ConfigChatNoticeOpenedHint => Loc.Get("ConfigChatNoticeOpenedHint");
    public static string ConfigChatTwo => Loc.Get("ConfigChatTwo");
    public static string ConfigChatTwoHint => Loc.Get("ConfigChatTwoHint");
    public static string ConfigNamePlateMarks => Loc.Get("ConfigNamePlateMarks");
    public static string ConfigNamePlateMarksHint => Loc.Get("ConfigNamePlateMarksHint");
    public static string ConfigQuestToasts => Loc.Get("ConfigQuestToasts");
    public static string ConfigQuestToastsHint => Loc.Get("ConfigQuestToastsHint");
}
