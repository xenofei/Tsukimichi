using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;

namespace Tsukimichi.Core.Export;

/// <summary>What an export holds.</summary>
public enum ExportKind
{
    /// <summary>The character's completed quests (optionally every quest with its completed flag).</summary>
    Quests,

    /// <summary>The Moonlit collection: every quest-exclusive reward with its obtained state.</summary>
    Moonlit,
}

/// <summary>The file format of an export.</summary>
public enum ExportFormat
{
    Json,
    Csv,
}

/// <summary>
/// What an export says about itself: the plugin and game versions, when it was written and, only when the user opted
/// in, the character's name. Never a content id, account id or world: those identify a player, and the file is meant
/// to be shared with trackers and spreadsheets.
/// </summary>
public sealed record ExportHeader(string PluginVersion, string GameVersion, DateTime ExportedUtc, string? CharacterName = null);

/// <summary>One quest of a quest export.</summary>
public sealed record QuestExportRow(uint RowId, ushort QuestId, string Name, string Section, string Category, string Genre, string Expansion, bool Completed);

/// <summary>One reward of a Moonlit export; <see cref="Obtained"/> null means the plugin cannot tell.</summary>
public sealed record MoonlitExportRow(RewardKind Kind, uint RewardId, string RewardName, uint QuestRowId, bool? Obtained);

/// <summary>
/// Builds the P12 exports (docs/export-format.md): the completed quests of a character and its Moonlit obtained state,
/// as JSON (a small header, then the rows) or CSV (a header row, then the rows; UTF-8 with a byte order mark so
/// spreadsheets read accented names). Pure apart from <see cref="Write"/>; nothing here uploads anything, and nothing
/// identifies the player unless <see cref="ExportHeader.CharacterName"/> is set.
/// </summary>
public static class ExportWriter
{
    /// <summary>The <c>format</c> value every JSON export carries.</summary>
    public const string FormatName = "tsukimichi-export";

    /// <summary>Bumped when a field changes meaning or goes away; new fields are additive.</summary>
    public const int FormatVersion = 1;

    /// <summary>The CSV value of an obtained state the plugin cannot read.</summary>
    public const string Unknown = "unknown";

    private static readonly string[] QuestColumns = ["rowId", "questId", "name", "section", "category", "genre", "expansion", "completed"];
    private static readonly string[] MoonlitColumns = ["kind", "rewardId", "rewardName", "questRowId", "obtained"];

    /// <summary>
    /// The header for an export of <paramref name="snapshot"/>'s character: its name only when
    /// <paramref name="includeCharacterName"/> is set.
    /// </summary>
    public static ExportHeader Header(string pluginVersion, string gameVersion, DateTime exportedUtc, CharacterSnapshot? snapshot, bool includeCharacterName) =>
        new(
            pluginVersion ?? string.Empty,
            gameVersion ?? string.Empty,
            exportedUtc.Kind == DateTimeKind.Utc ? exportedUtc : exportedUtc.ToUniversalTime(),
            includeCharacterName && snapshot is { Name.Length: > 0 } s ? s.Name : null);

    /// <summary>
    /// One row per catalog quest the snapshot has completed, in journal order; with <paramref name="includeIncomplete"/>
    /// every catalog quest, each with its completed flag. <paramref name="expansionName"/> names an expansion id
    /// ("Endwalker"); without it the id is written.
    /// </summary>
    public static List<QuestExportRow> QuestRows(QuestCatalog catalog, CharacterSnapshot snapshot, Func<byte, string>? expansionName = null, bool includeIncomplete = false)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(snapshot);

        var quests = new List<QuestRecord>(catalog.All);
        quests.Sort(static (a, b) =>
        {
            var byJournal = a.Journal.SortKey.CompareTo(b.Journal.SortKey);
            return byJournal != 0 ? byJournal : a.RowId.CompareTo(b.RowId);
        });

        var rows = new List<QuestExportRow>();
        foreach (var quest in quests)
        {
            var completed = snapshot.IsCompleted(quest.QuestId);
            if (!completed && !includeIncomplete)
            {
                continue;
            }

            var expansion = expansionName?.Invoke(quest.Expansion) is { Length: > 0 } name
                ? name
                : quest.Expansion.ToString(CultureInfo.InvariantCulture);
            rows.Add(new QuestExportRow(
                quest.RowId,
                quest.QuestId,
                quest.Name,
                quest.Journal.SectionName,
                quest.Journal.CategoryName,
                quest.Journal.GenreName,
                expansion,
                completed));
        }

        return rows;
    }

    /// <summary>One row per reward of the Moonlit unique view with the caller's obtained verdict, in the view's order.</summary>
    public static List<MoonlitExportRow> MoonlitRows(IReadOnlyList<UniqueRewardRow> view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var rows = new List<MoonlitExportRow>(view.Count);
        foreach (var row in view)
        {
            var e = row.Entry;
            rows.Add(new MoonlitExportRow(e.Kind, e.RewardId, e.RewardName, e.QuestRowId, row.Obtained));
        }

        return rows;
    }

    /// <summary>The quest export as JSON: header, counts, then <c>quests</c>.</summary>
    public static string QuestsJson(ExportHeader header, IReadOnlyList<QuestExportRow> rows)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(rows);
        var completed = 0;
        foreach (var row in rows)
        {
            if (row.Completed)
            {
                completed++;
            }
        }

        return Json(header, ExportKind.Quests, w =>
        {
            w.WriteNumber("count", rows.Count);
            w.WriteNumber("completedCount", completed);
            w.WriteStartArray("quests");
            foreach (var row in rows)
            {
                w.WriteStartObject();
                w.WriteNumber("rowId", row.RowId);
                w.WriteNumber("questId", row.QuestId);
                w.WriteString("name", row.Name);
                w.WriteString("section", row.Section);
                w.WriteString("category", row.Category);
                w.WriteString("genre", row.Genre);
                w.WriteString("expansion", row.Expansion);
                w.WriteBoolean("completed", row.Completed);
                w.WriteEndObject();
            }

            w.WriteEndArray();
        });
    }

    /// <summary>The Moonlit export as JSON: header, counts, then <c>rewards</c>; an unknown obtained state is <c>null</c>.</summary>
    public static string MoonlitJson(ExportHeader header, IReadOnlyList<MoonlitExportRow> rows)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(rows);
        int obtained = 0, unknown = 0;
        foreach (var row in rows)
        {
            if (row.Obtained is null)
            {
                unknown++;
            }
            else if (row.Obtained.Value)
            {
                obtained++;
            }
        }

        return Json(header, ExportKind.Moonlit, w =>
        {
            w.WriteNumber("count", rows.Count);
            w.WriteNumber("obtainedCount", obtained);
            w.WriteNumber("unknownCount", unknown);
            w.WriteStartArray("rewards");
            foreach (var row in rows)
            {
                w.WriteStartObject();
                w.WriteString("kind", row.Kind.ToString());
                w.WriteNumber("rewardId", row.RewardId);
                w.WriteString("rewardName", row.RewardName);
                w.WriteNumber("questRowId", row.QuestRowId);
                if (row.Obtained is { } has)
                {
                    w.WriteBoolean("obtained", has);
                }
                else
                {
                    w.WriteNull("obtained");
                }

                w.WriteEndObject();
            }

            w.WriteEndArray();
        });
    }

    /// <summary>The quest export as CSV: one header row, then one row per quest.</summary>
    public static string QuestsCsv(IReadOnlyList<QuestExportRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var sb = Csv(QuestColumns);
        foreach (var row in rows)
        {
            Line(sb,
                Number(row.RowId),
                Number(row.QuestId),
                row.Name,
                row.Section,
                row.Category,
                row.Genre,
                row.Expansion,
                row.Completed ? "true" : "false");
        }

        return sb.ToString();
    }

    /// <summary>The Moonlit export as CSV: one header row, then one row per reward; obtained is true, false or unknown.</summary>
    public static string MoonlitCsv(IReadOnlyList<MoonlitExportRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var sb = Csv(MoonlitColumns);
        foreach (var row in rows)
        {
            Line(sb,
                row.Kind.ToString(),
                Number(row.RewardId),
                row.RewardName,
                Number(row.QuestRowId),
                row.Obtained switch { true => "true", false => "false", null => Unknown });
        }

        return sb.ToString();
    }

    /// <summary>
    /// "tsukimichi-quests-20260929-201500.json"; the character's name goes in only when the header carries it
    /// (the user opted in), made safe for a file name.
    /// </summary>
    public static string FileName(ExportKind kind, ExportFormat format, ExportHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        var sb = new StringBuilder("tsukimichi-");
        sb.Append(kind == ExportKind.Quests ? "quests" : "moonlit");
        if (header.CharacterName is { Length: > 0 } name)
        {
            sb.Append('-').Append(SafeFileName(name));
        }

        sb.Append('-').Append(header.ExportedUtc.ToLocalTime().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        sb.Append(format == ExportFormat.Json ? ".json" : ".csv");
        return sb.ToString();
    }

    /// <summary>
    /// The folder exports go to: <paramref name="defaultDirectory"/> when <paramref name="configured"/> is blank, the
    /// configured folder when it is a fully qualified path, else the configured folder under
    /// <paramref name="configDirectory"/>. <c>\exports</c> and <c>D:exports</c> are rooted but not fully qualified
    /// (they depend on the current drive or directory), so they count as relative and never resolve against the
    /// game's working directory.
    /// </summary>
    public static string ResolveFolder(string? configured, string configDirectory, string defaultDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultDirectory);
        var folder = configured?.Trim();
        if (string.IsNullOrEmpty(folder))
        {
            return defaultDirectory;
        }

        if (Path.IsPathFullyQualified(folder))
        {
            return folder;
        }

        // Path.Combine would keep a rooted second part as is, so the partial root ("\", "D:") is dropped first.
        var relative = folder[(Path.GetPathRoot(folder)?.Length ?? 0)..].TrimStart('\\', '/');
        return relative.Length == 0 ? defaultDirectory : Path.Combine(configDirectory, relative);
    }

    /// <summary>Writes an export into <paramref name="directory"/> (created when missing) and returns the full path.</summary>
    public static string Write(string directory, string fileName, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);
        var path = Path.GetFullPath(Path.Combine(directory, fileName));
        AtomicFile.Write(path, content);
        return path;
    }

    /// <summary>Replaces spaces and characters a file name cannot hold with underscores.</summary>
    public static string SafeFileName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] == ' ' || Array.IndexOf(invalid, chars[i]) >= 0)
            {
                chars[i] = '_';
            }
        }

        var result = new string(chars).Trim('_');
        return result.Length == 0 ? "character" : result;
    }

    private static string Json(ExportHeader header, ExportKind kind, Action<Utf8JsonWriter> body)
    {
        // Relaxed escaping keeps "Ul'dah" and accented names readable; the file is data, never embedded in a page.
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartObject();
            w.WriteString("format", FormatName);
            w.WriteNumber("formatVersion", FormatVersion);
            w.WriteString("kind", kind == ExportKind.Quests ? "quests" : "moonlit");
            w.WriteString("pluginVersion", header.PluginVersion);
            w.WriteString("gameVersion", header.GameVersion);
            w.WriteString("exportedUtc", header.ExportedUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
            if (header.CharacterName is { Length: > 0 } name)
            {
                w.WriteString("character", name);
            }

            body(w);
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static StringBuilder Csv(string[] columns)
    {
        // A byte order mark, so Excel reads the file as UTF-8 rather than the system code page.
        var sb = new StringBuilder("﻿");
        Line(sb, columns);
        return sb;
    }

    private static void Line(StringBuilder sb, params string[] fields)
    {
        for (var i = 0; i < fields.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            Field(sb, fields[i]);
        }

        sb.Append("\r\n");
    }

    /// <summary>RFC 4180: a field with a comma, quote or line break is quoted, quotes doubled.</summary>
    private static void Field(StringBuilder sb, string value)
    {
        if (value.AsSpan().IndexOfAny(",\"\r\n") < 0)
        {
            sb.Append(value);
            return;
        }

        sb.Append('"').Append(value.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
    }

    private static string Number(uint value) => value.ToString(CultureInfo.InvariantCulture);
}
