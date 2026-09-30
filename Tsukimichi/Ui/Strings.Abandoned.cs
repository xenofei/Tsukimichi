using Tsukimichi.Core.Query;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the abandoned-quest ledger (P10): the Characters dashboard section, the Abandoned filter and its
/// chip, the chat line and its setting. Constant names carry the <c>Abandoned</c> prefix so this part of the partial
/// class never collides with the others.
/// </summary>
static partial class Strings
{
    // ---- Characters dashboard ----
    /// <summary>{0} = number of abandoned quests.</summary>
    public const string AbandonedHeaderFormat = "Abandoned ({0})";
    public const string AbandonedNone = "No abandoned quests recorded. When a quest leaves your journal without being completed, it is listed here with the step it had reached.";
    public const string AbandonedShowInJournal = "Show in Journal";
    public const string AbandonedShowInJournalTooltip = "Open the Journal filtered to these quests";
    public const string AbandonedFlag = "Flag";
    public const string AbandonedFlagTooltip = "Flag the quest giver on the map";
    public const string AbandonedTeleport = "Teleport";
    public const string AbandonedReveal = "Reveal";
    public const string AbandonedRevealTooltip = "Show in the Journal";
    public const string AbandonedGiverPrefix = "Giver: ";
    public const string AbandonedAtPrefix = "Abandoned ";

    // ---- Filter ----
    public const string AbandonedOnly = "Abandoned only";
    public const string AbandonedOnlyTooltip = "Keep only quests this character abandoned and has not taken up again";
    public const string AbandonedChip = FilterNames.Abandoned;

    // ---- Chat ----
    public const string AbandonedChatPrefix = "Abandoned: ";

    /// <summary>{0} = "step 3 of 5".</summary>
    public const string AbandonedChatStepFormat = " ({0})";

    public const string AbandonedConfigNotice = "Chat line when you abandon a quest";
    public const string AbandonedConfigNoticeHint = "\"Abandoned: [quest] (step 3 of 5)\", once per quest per session, so a mis-click is noticed. The Characters tab lists every abandoned quest either way.";
}
