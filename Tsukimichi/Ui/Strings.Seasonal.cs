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
    public const string SeasonalHeaderFormat = "Seasonal events ({0} running)";
    public const string SeasonalHeader = "Seasonal events";
    public const string SeasonalNoneRunning = "No seasonal event is running.";

    /// <summary>{0} = capture time. A stored character's events are the ones running when it was last captured.</summary>
    public const string SeasonalAtCaptureFormat = "As of this character's last capture ({0}).";

    public const string SeasonalEvidenceTooltipPrefix = "Announced on the Lodestone: ";
    public const string SeasonalRunningNowTooltip = "The game says this event is running. Tsukimichi shows an end date only when the Lodestone announced one; it never guesses.";
    public const string SeasonalGiverPrefix = "Giver: ";
    public const string SeasonalNoQuests = "No quests of this event are listed in the journal.";

    /// <summary>{0} = completed seasonal quests of the character.</summary>
    public const string SeasonalHistoryFormat = "Completed seasonal quests by year ({0})";
    public const string SeasonalHistoryNone = "No seasonal quests completed yet.";
    public const string SeasonalHistoryTooltip = "Each event's year is the one the Lodestone gave its edition (\"Moonfire Faire (2014)\"). An edition without an announcement is counted from the nearest announced edition of the same event, one per year; when that is not certain, or for collaboration events that come back again and again, it is listed under \"Year not known\".";
    public const string SeasonalYearUnknown = "Year not known";

    /// <summary>{0} = year (or "Year not known"), {1} = completed quests that year.</summary>
    public const string SeasonalYearFormat = "{0} ({1})";

    /// <summary>{0} = event name, {1} = completed quests of it.</summary>
    public const string SeasonalHistoryFestivalFormat = "{0} ({1})";

    // ---- Chat ----
    public const string SeasonalChatNextPrefix = " · ";
    public const string SeasonalConfigNotice = "Chat line at login when a seasonal event has quests ready";
    public const string SeasonalConfigNoticeHint = "\"Moonfire Faire is running: 2 quests ready (ends Aug 28)\", once per login per event, with a link to the first quest. The end date shows only when the Lodestone announced it.";
}
