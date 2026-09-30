namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the journal text reader and search (P9): the detail pane's Journal card and Settings › Journal text.
/// Constant names carry the <c>JournalText</c> prefix so this part of the partial class never collides with the others.
/// None of these quote the game's text; the card shows the client's own sheets at runtime.
/// </summary>
static partial class Strings
{
    // ---- Detail pane: Journal card ----
    public const string JournalTextCard = "Journal";

    public const string JournalTextRead = "Read the journal";

    public const string JournalTextHide = "Hide the journal";

    public const string JournalTextClosedCompleted = "The journal entries of this quest, as the game wrote them while you played it.";

    public const string JournalTextClosedAccepted = "The journal entries so far, up to the step you are on. Later entries appear as the quest goes on.";

    public const string JournalTextReadTooltip = "Shows the text the game's journal showed for this quest. Nothing beyond the step you have reached.";

    public const string JournalTextNoText = "The game has no journal text for this quest.";

    public const string JournalTextCopy = "Copy entry";

    public const string JournalTextCopyTooltip = "Copies this entry to the clipboard.";

    public const string JournalTextCopied = "Copied";

    public const string JournalTextObjectives = "Objectives";

    public const string JournalTextLater = "Later entries appear as the quest goes on.";

    public const string JournalTextNeutral = "Shown for a stored character: where the text depends on who reads it, both versions are shown (he/she).";

    // ---- Settings › Journal text ----
    public const string JournalTextSettingsSection = "Journal text";

    public const string JournalTextSearchSetting = "Search journal text of completed quests";

    public const string JournalTextSearchHint = "The search box also finds quests by the words of their journal entries, among the quests the character shown has completed; never a quest it has not played. The first time, the plugin reads every quest's text from the game files in the background (a few seconds) and keeps a word index, not the text, in its config folder; it is rebuilt after a game patch.";

    public const string JournalTextStatusWaiting = "Waiting for the quest catalog…";

    public const string JournalTextStatusLoading = "Reading the saved index…";

    /// <summary>{0} = percent done.</summary>
    public const string JournalTextStatusBuildingFormat = "Building the index… {0}%";

    /// <summary>{0} = quests indexed, {1} = size on disk in KiB.</summary>
    public const string JournalTextStatusReadyFormat = "Index ready: {0:N0} quests, {1:N0} KiB on disk.";

    /// <summary>{0} = seconds.</summary>
    public const string JournalTextStatusBuiltFormat = " Built in {0:0.0} s.";

    /// <summary>{0} = the error.</summary>
    public const string JournalTextStatusFailedFormat = "The index could not be built: {0}. Turn the setting off and on to try again.";
}
