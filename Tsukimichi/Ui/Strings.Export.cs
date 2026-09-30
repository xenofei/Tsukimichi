using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the exports (P12): Settings › Data › Export and <c>/tsuki export</c>. Constant names carry the
/// <c>Export</c> prefix so this part of the partial class never collides with the others.
/// </summary>
static partial class Strings
{
    public static string ExportHeader => Loc.Get("ExportHeader");
    public static string ExportIntro => Loc.Get("ExportIntro");
    public static string ExportQuests => Loc.Get("ExportQuests");
    public static string ExportMoonlit => Loc.Get("ExportMoonlit");
    public static string ExportFormatLabel => Loc.Get("ExportFormatLabel");
    public const string ExportFormatJson = "JSON";
    public const string ExportFormatCsv = "CSV";
    public static string ExportIncludeName => Loc.Get("ExportIncludeName");
    public static string ExportIncludeNameHint => Loc.Get("ExportIncludeNameHint");
    public static string ExportIncludeIncomplete => Loc.Get("ExportIncludeIncomplete");
    public static string ExportIncludeIncompleteHint => Loc.Get("ExportIncludeIncompleteHint");
    public static string ExportFolderLabel => Loc.Get("ExportFolderLabel");
    public static string ExportFolderDefault => Loc.Get("ExportFolderDefault");
    public static string ExportFolderDefaultTooltip => Loc.Get("ExportFolderDefaultTooltip");
    public static string ExportOpenFolder => Loc.Get("ExportOpenFolder");
    public static string ExportOpenFolderFailed => Loc.Get("ExportOpenFolderFailed");
    public static string ExportWrittenPrefix => Loc.Get("ExportWrittenPrefix");
    public static string ExportFailedPrefix => Loc.Get("ExportFailedPrefix");
    public static string ExportNoCharacter => Loc.Get("ExportNoCharacter");
    public static string ExportCatalogLoading => Loc.Get("ExportCatalogLoading");

    /// <summary>{0} = file path.</summary>
    public static string ExportChatWrittenFormat => Loc.Get("ExportChatWrittenFormat");

    /// <summary>{0} = the word not understood.</summary>
    public static string ExportChatUsageFormat => Loc.Get("ExportChatUsageFormat");
}
