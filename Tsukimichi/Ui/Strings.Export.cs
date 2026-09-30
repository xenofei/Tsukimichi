namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the exports (P12): Settings › Data › Export and <c>/tsuki export</c>. Constant names carry the
/// <c>Export</c> prefix so this part of the partial class never collides with the others.
/// </summary>
static partial class Strings
{
    public const string ExportHeader = "Export";
    public const string ExportIntro = "Write the viewed character's completed quests or Moonlit collection to a file for a spreadsheet or a collection tracker. The file stays on your computer; Tsukimichi never uploads anything.";
    public const string ExportQuests = "Export completed quests";
    public const string ExportMoonlit = "Export Moonlit collection";
    public const string ExportFormatLabel = "Format:";
    public const string ExportFormatJson = "JSON";
    public const string ExportFormatCsv = "CSV";
    public const string ExportIncludeName = "Include character name";
    public const string ExportIncludeNameHint = "Off by default. When on, the character's name goes in the JSON header and in the file name. Content id, account and world are never written.";
    public const string ExportIncludeIncomplete = "List every quest with a completed flag";
    public const string ExportIncludeIncompleteHint = "Off: only completed quests. On: every quest the journal knows, each marked completed true or false.";
    public const string ExportFolderLabel = "Output folder";
    public const string ExportFolderDefault = "Default";
    public const string ExportFolderDefaultTooltip = "Use exports in the plugin's config directory";
    public const string ExportOpenFolder = "Open folder";
    public const string ExportOpenFolderFailed = "Could not open the folder: ";
    public const string ExportWrittenPrefix = "Written: ";
    public const string ExportFailedPrefix = "Export failed: ";
    public const string ExportNoCharacter = "No character to export: log in, or pick a stored character on the Characters tab.";
    public const string ExportCatalogLoading = "The quest catalog is still loading; try again in a moment.";

    /// <summary>{0} = file path.</summary>
    public const string ExportChatWrittenFormat = "Exported to {0}";

    /// <summary>{0} = the word not understood.</summary>
    public const string ExportChatUsageFormat = "Unknown export option \"{0}\". Use /tsuki export [quests|moonlit] [json|csv].";
}
