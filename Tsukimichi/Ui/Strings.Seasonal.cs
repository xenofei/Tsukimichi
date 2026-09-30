using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for "Seasonal now" (P11): the Characters dashboard section, the login chat line and its setting. The
/// overlay's section name and toggle live with the other todo strings. Constant names carry the <c>Seasonal</c> prefix
/// so this part of the partial class never collides with the others.
/// </summary>
static partial class Strings
{
    // ---- Characters dashboard ----
    /// <summary>{0} = number of running events.</summary>
    public static string SeasonalHeaderFormat => Loc.Get("SeasonalHeaderFormat");
    public static string SeasonalHeader => Loc.Get("SeasonalHeader");
    public static string SeasonalNoneRunning => Loc.Get("SeasonalNoneRunning");

    /// <summary>{0} = capture time. A stored character's events are the ones running when it was last captured.</summary>
    public static string SeasonalAtCaptureFormat => Loc.Get("SeasonalAtCaptureFormat");

    /// <summary>{0} = the announcement the end date comes from.</summary>
    public static string SeasonalEvidenceTooltipFormat => Loc.Get("SeasonalEvidenceTooltipFormat");
    public static string SeasonalRunningNowTooltip => Loc.Get("SeasonalRunningNowTooltip");
    /// <summary>{0} = NPC name.</summary>
    public static string SeasonalGiverFormat => Loc.Get("SeasonalGiverFormat");
    public static string SeasonalNoQuests => Loc.Get("SeasonalNoQuests");

    /// <summary>{0} = completed seasonal quests of the character.</summary>
    public static string SeasonalHistoryFormat => Loc.Get("SeasonalHistoryFormat");
    public static string SeasonalHistoryNone => Loc.Get("SeasonalHistoryNone");
    public static string SeasonalHistoryTooltip => Loc.Get("SeasonalHistoryTooltip");
    public static string SeasonalYearUnknown => Loc.Get("SeasonalYearUnknown");

    /// <summary>{0} = year (or "Year not known"), {1} = completed quests that year.</summary>
    public const string SeasonalYearFormat = "{0} ({1})";

    /// <summary>{0} = event name, {1} = completed quests of it.</summary>
    public const string SeasonalHistoryFestivalFormat = "{0} ({1})";

    // ---- Chat ----
    public const string SeasonalChatNextPrefix = " · ";
    public static string SeasonalConfigNotice => Loc.Get("SeasonalConfigNotice");
    public static string SeasonalConfigNoticeHint => Loc.Get("SeasonalConfigNoticeHint");
}
