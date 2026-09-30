using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the journal text reader and search (P9): the detail pane's Journal card and Settings › Journal text.
/// Constant names carry the <c>JournalText</c> prefix so this part of the partial class never collides with the others.
/// None of these quote the game's text; the card shows the client's own sheets at runtime.
/// </summary>
static partial class Strings
{
    // ---- Detail pane: Journal card ----
    public static string JournalTextCard => Loc.Get("JournalTextCard");

    public static string JournalTextRead => Loc.Get("JournalTextRead");

    public static string JournalTextHide => Loc.Get("JournalTextHide");

    public static string JournalTextClosedCompleted => Loc.Get("JournalTextClosedCompleted");

    public static string JournalTextClosedAccepted => Loc.Get("JournalTextClosedAccepted");

    public static string JournalTextReadTooltip => Loc.Get("JournalTextReadTooltip");

    public static string JournalTextNoText => Loc.Get("JournalTextNoText");

    public static string JournalTextCopy => Loc.Get("JournalTextCopy");

    public static string JournalTextCopyTooltip => Loc.Get("JournalTextCopyTooltip");

    public static string JournalTextCopied => Loc.Get("JournalTextCopied");

    public static string JournalTextObjectives => Loc.Get("JournalTextObjectives");

    public static string JournalTextLater => Loc.Get("JournalTextLater");

    public static string JournalTextNeutral => Loc.Get("JournalTextNeutral");

    // ---- Settings › Journal text ----
    public static string JournalTextSettingsSection => Loc.Get("JournalTextSettingsSection");

    public static string JournalTextSearchSetting => Loc.Get("JournalTextSearchSetting");

    public static string JournalTextSearchHint => Loc.Get("JournalTextSearchHint");

    public static string JournalTextStatusWaiting => Loc.Get("JournalTextStatusWaiting");

    public static string JournalTextStatusLoading => Loc.Get("JournalTextStatusLoading");

    /// <summary>{0} = percent done.</summary>
    public static string JournalTextStatusBuildingFormat => Loc.Get("JournalTextStatusBuildingFormat");

    /// <summary>{0} = quests indexed, {1} = size on disk in KiB.</summary>
    public static string JournalTextStatusReadyFormat => Loc.Get("JournalTextStatusReadyFormat");

    /// <summary>{0} = seconds.</summary>
    public static string JournalTextStatusBuiltFormat => Loc.Get("JournalTextStatusBuiltFormat");

    /// <summary>{0} = the error.</summary>
    public static string JournalTextStatusFailedFormat => Loc.Get("JournalTextStatusFailedFormat");
}
