using Lumina.Data;
using Lumina.Data.Files.Excel;

namespace Tsukimichi.GameData;

/// <summary>
/// Raw access to the quest text sheets' files (<c>exd/quest/NNN/Name_NNNNN.exh</c> and its <c>.exd</c> pages) through
/// the game's file reader rather than the Excel module. The module caches every sheet it opens for the lifetime of the
/// game data (<c>GetRawSheet</c> included), so reading five thousand quest sheets through it would keep them all;
/// a file read here is dropped once the caller lets go of it. Standalone: tests pass a <see cref="Lumina.GameData"/>,
/// the plugin passes <c>IDataManager.GetFile</c>. Reads are thread-safe.
/// </summary>
public sealed class QuestTextFiles
{
    private readonly Func<string, ExcelHeaderFile?> header;
    private readonly Func<string, ExcelDataFile?> data;

    /// <param name="header">Reads an <c>.exh</c> by its full path; null when the file does not exist.</param>
    /// <param name="data">Reads an <c>.exd</c> by its full path; null when the file does not exist.</param>
    public QuestTextFiles(Func<string, ExcelHeaderFile?> header, Func<string, ExcelDataFile?> data)
    {
        this.header = header ?? throw new ArgumentNullException(nameof(header));
        this.data = data ?? throw new ArgumentNullException(nameof(data));
    }

    /// <summary>Files from a standalone Lumina game data (tests, tools).</summary>
    public static QuestTextFiles From(Lumina.GameData game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return new QuestTextFiles(game.GetFile<ExcelHeaderFile>, game.GetFile<ExcelDataFile>);
    }

    /// <summary>The header of <paramref name="sheet"/> ("quest/001/ClsGla001_00177"); null when there is none or it does not read.</summary>
    public ExcelHeaderFile? Header(string sheet) => Safe(header, "exd/" + sheet + ".exh");

    /// <summary>One page of <paramref name="sheet"/> in <paramref name="language"/>; null when there is none or it does not read.</summary>
    public ExcelDataFile? Page(string sheet, uint startId, Language language)
    {
        var suffix = LanguageSuffix(language);
        return Safe(data, suffix.Length == 0 ? $"exd/{sheet}_{startId}.exd" : $"exd/{sheet}_{startId}_{suffix}.exd");
    }

    /// <summary>The language code of a sheet page's file name ("en" for English); empty for <see cref="Language.None"/>.</summary>
    public static string LanguageSuffix(Language language) => language switch
    {
        Language.Japanese => "ja",
        Language.English => "en",
        Language.German => "de",
        Language.French => "fr",
        Language.ChineseSimplified => "chs",
        Language.ChineseTraditional => "cht",
        Language.Korean => "ko",
        _ => string.Empty,
    };

    private static T? Safe<T>(Func<string, T?> read, string path)
        where T : class
    {
        try
        {
            return read(path);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or IndexOutOfRangeException or InvalidOperationException or EndOfStreamException or NotSupportedException)
        {
            return null;
        }
    }
}
