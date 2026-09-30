using System.Buffers.Binary;
using Lumina.Data;
using Lumina.Data.Files.Excel;
using Lumina.Data.Structs.Excel;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace Tsukimichi.GameData;

/// <summary>One line of a quest's text sheet: its number (<c>SEQ_03</c> is 3) and its text, macros unevaluated.</summary>
/// <param name="Index">The number after <c>SEQ_</c> or <c>TODO_</c>.</param>
/// <param name="Text">The line as the sheet holds it; evaluate it before showing it (<see cref="QuestTextNeutral"/> or the game's evaluator).</param>
/// <param name="Sequence">For an objective, the journal sequence it is completed in (<c>Quest.ToDoCompleteSeq</c>); 0 when unknown, and for a journal entry.</param>
public sealed record QuestTextLine(int Index, ReadOnlySeString Text, byte Sequence = 0);

/// <summary>
/// A quest's journal as its text sheet holds it (P9): the journal entries (<c>SEQ_00</c>, <c>SEQ_01</c>, …, one per
/// step plus the one on taking the quest and the one on turning it in) and the objective lines (<c>TODO_00</c>, …), each
/// in number order with empty lines left out. See <see cref="Core.Text.JournalVisibility"/> for how much of it a
/// character has seen.
/// </summary>
public sealed record QuestText(string SheetName, IReadOnlyList<QuestTextLine> Journal, IReadOnlyList<QuestTextLine> Objectives)
{
    /// <summary>The sheet exists but holds no journal entry and no objective (most class-starting quests).</summary>
    public bool IsEmpty => Journal.Count == 0 && Objectives.Count == 0;
}

/// <summary>
/// Reads one quest's text sheet (P9). The sheet is <c>quest/&lt;first three digits of the number&gt;/&lt;Quest.Id&gt;</c>
/// ("ClsGla001_00177" is <c>quest/001/ClsGla001_00177</c>): two string columns, a key and the text, with rows
/// <c>TEXT_&lt;ID&gt;_SEQ_00</c> to <c>_SEQ_23</c> (the journal entries), <c>TEXT_&lt;ID&gt;_TODO_00</c> to
/// <c>_TODO_23</c> (the objectives) and then the dialogue, which is never read here. The files are read through
/// <see cref="QuestTextFiles"/> so nothing stays cached. A missing sheet (a removed quest, a malformed id) reads as
/// null; a sheet with nothing in it reads as an empty <see cref="QuestText"/>.
/// </summary>
public static class QuestTextReader
{
    public const string JournalMarker = "_SEQ_";
    public const string ObjectiveMarker = "_TODO_";

    /// <summary>
    /// The sheet name for a quest's script id: "ClsGla001_00177" to "quest/001/ClsGla001_00177". Null when the id has no
    /// "_" followed by at least three digits, as a quest without text has.
    /// </summary>
    public static string? SheetName(string? internalId)
    {
        if (string.IsNullOrWhiteSpace(internalId))
        {
            return null;
        }

        var under = internalId.LastIndexOf('_');
        if (under < 1 || internalId.Length - under - 1 < 3)
        {
            return null;
        }

        var folder = internalId.AsSpan(under + 1, 3);
        foreach (var c in folder)
        {
            if (!char.IsAsciiDigit(c))
            {
                return null;
            }
        }

        foreach (var c in internalId)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '_')
            {
                return null;
            }
        }

        return $"quest/{folder}/{internalId}";
    }

    /// <summary>
    /// Whether a row key is a journal entry or an objective, and its number. The key must end in <c>_SEQ_nn</c> or
    /// <c>_TODO_nn</c>; a dialogue key that merely contains the marker (<c>…_TODO_23_SYSTEM_000_000</c>) is neither.
    /// </summary>
    public static bool TryParseKey(ReadOnlySpan<char> key, out bool isObjective, out int index)
    {
        isObjective = false;
        index = 0;
        var digits = 0;
        while (digits < key.Length && char.IsAsciiDigit(key[key.Length - 1 - digits]))
        {
            digits++;
        }

        if (digits is 0 or > 4)
        {
            return false;
        }

        var head = key[..^digits];
        if (head.EndsWith(JournalMarker, StringComparison.Ordinal))
        {
            isObjective = false;
        }
        else if (head.EndsWith(ObjectiveMarker, StringComparison.Ordinal))
        {
            isObjective = true;
        }
        else
        {
            return false;
        }

        index = int.Parse(key[^digits..], System.Globalization.CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>
    /// Reads a quest's text in <paramref name="language"/>. Null when the sheet does not exist, is not a two-string
    /// sheet, or has no page in that language.
    /// </summary>
    /// <param name="objectiveSequences">
    /// <c>Quest.TodoParams[i].ToDoCompleteSeq</c> by objective number (<see cref="ObjectiveSequences"/>), so each
    /// objective knows its step; empty leaves every objective's sequence unknown.
    /// </param>
    public static QuestText? Read(QuestTextFiles files, string? internalId, Language language, ReadOnlySpan<byte> objectiveSequences = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (SheetName(internalId) is not { } sheet || files.Header(sheet) is not { } header)
        {
            return null;
        }

        if (!TryColumns(header, out var keyColumn, out var textColumn))
        {
            return null;
        }

        var pageLanguage = header.Languages.Contains(language) ? language
            : header.Languages.Contains(Language.None) ? Language.None
            : (Language?)null;
        if (pageLanguage is not { } lang)
        {
            return null;
        }

        var journal = new List<QuestTextLine>();
        var objectives = new List<QuestTextLine>();
        var anyPage = false;
        foreach (var page in header.DataPages)
        {
            if (files.Page(sheet, page.StartId, lang) is not { } data)
            {
                continue;
            }

            anyPage = true;
            ReadPage(data, header.Header.DataOffset, keyColumn, textColumn, journal, objectives, objectiveSequences);
        }

        if (!anyPage)
        {
            return null;
        }

        journal.Sort(static (a, b) => a.Index.CompareTo(b.Index));
        objectives.Sort(static (a, b) => a.Index.CompareTo(b.Index));
        return new QuestText(sheet, journal, objectives);
    }

    /// <summary>
    /// <c>Quest.TodoParams[i].ToDoCompleteSeq</c> for every objective slot of a quest row, the step each <c>TODO_i</c>
    /// line belongs to; empty when the row does not exist. Reads the typed Quest sheet the catalog already loaded.
    /// </summary>
    public static byte[] ObjectiveSequences(ExcelModule excel, uint questRowId, Language? language = null)
    {
        ArgumentNullException.ThrowIfNull(excel);
        if (excel.GetSheet<Quest>(language).GetRowOrDefault(questRowId) is not { } quest)
        {
            return [];
        }

        var todos = quest.TodoParams;
        var result = new byte[todos.Count];
        for (var i = 0; i < todos.Count; i++)
        {
            result[i] = todos[i].ToDoCompleteSeq;
        }

        return result;
    }

    /// <summary>The key and text columns: the first two string columns (in today's sheets, the only two).</summary>
    private static bool TryColumns(ExcelHeaderFile header, out ExcelColumnDefinition key, out ExcelColumnDefinition text)
    {
        key = default;
        text = default;
        var found = 0;
        foreach (var column in header.ColumnDefinitions)
        {
            if (column.Type != ExcelColumnDataType.String)
            {
                continue;
            }

            if (found == 0)
            {
                key = column;
            }
            else
            {
                text = column;
                return true;
            }

            found++;
        }

        return false;
    }

    /// <summary>
    /// One <c>.exd</c> page: each row is a 6-byte header (size, subrow count; big-endian) then the fixed columns, whose
    /// string columns hold a big-endian offset into the string block that follows the fixed part. Out-of-range offsets
    /// skip the row rather than throw.
    /// </summary>
    private static void ReadPage(
        ExcelDataFile data,
        ushort fixedSize,
        ExcelColumnDefinition keyColumn,
        ExcelColumnDefinition textColumn,
        List<QuestTextLine> journal,
        List<QuestTextLine> objectives,
        ReadOnlySpan<byte> objectiveSequences)
    {
        var bytes = data.Data;
        foreach (var (_, offset) in data.RowData)
        {
            var rowStart = (long)offset.Offset + 6;
            if (rowStart + fixedSize > bytes.Length)
            {
                continue;
            }

            var keyBytes = ReadString(bytes, rowStart, fixedSize, keyColumn.Offset);
            if (keyBytes.IsEmpty)
            {
                continue;
            }

            var key = System.Text.Encoding.UTF8.GetString(keyBytes);
            if (!TryParseKey(key, out var isObjective, out var index))
            {
                continue;
            }

            var textBytes = ReadString(bytes, rowStart, fixedSize, textColumn.Offset);
            if (textBytes.IsEmpty)
            {
                continue;
            }

            var text = new ReadOnlySeString(textBytes.ToArray());
            if (isObjective)
            {
                var sequence = index < objectiveSequences.Length ? objectiveSequences[index] : (byte)0;
                objectives.Add(new QuestTextLine(index, text, sequence));
            }
            else
            {
                journal.Add(new QuestTextLine(index, text));
            }
        }
    }

    private static ReadOnlySpan<byte> ReadString(byte[] bytes, long rowStart, ushort fixedSize, ushort columnOffset)
    {
        var at = rowStart + columnOffset;
        if (at < 0 || at + 4 > bytes.Length)
        {
            return default;
        }

        var stringOffset = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan((int)at, 4));
        var start = rowStart + fixedSize + stringOffset;
        if (start < 0 || start >= bytes.Length)
        {
            return default;
        }

        var rest = bytes.AsSpan((int)start);
        var end = rest.IndexOf((byte)0);
        return end < 0 ? default : rest[..end];
    }
}
