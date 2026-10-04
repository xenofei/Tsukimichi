using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Links;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Runtime;
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
/// <param name="CompletionDatesSinceUtc">
/// When completion dates started being recorded for the character (<see cref="CharacterSnapshot.CompletionDatesSinceUtc"/>);
/// quests completed before it carry no date. Null when the snapshot records none. Written in quest exports only.
/// </param>
public sealed record ExportHeader(string PluginVersion, string GameVersion, DateTime ExportedUtc, string? CharacterName = null, DateTime? CompletionDatesSinceUtc = null);

/// <summary>One quest of a quest export.</summary>
/// <param name="CompletedAt">When the plugin first saw the quest completed (decision 9); null when not known.</param>
/// <param name="CompletedAfter">
/// For a quest found completed at a login: the capture before it. The quest was completed between this and
/// <paramref name="CompletedAt"/>. Null for a quest seen being completed, or with no date.
/// </param>
public sealed record QuestExportRow(
    uint RowId,
    ushort QuestId,
    string Name,
    string Section,
    string Category,
    string Genre,
    string Expansion,
    bool Completed,
    DateTime? CompletedAt = null,
    DateTime? CompletedAfter = null)
{
    /// <summary>The character's state of the quest, as <see cref="QuestState"/>'s name ("Ready", "Blocked"); empty when not evaluated. Added in 1.8.0.</summary>
    public string State { get; init; } = string.Empty;

    /// <summary>The level the journal prints (<see cref="QuestRecord.DisplayLevel"/>). Added in 1.8.0.</summary>
    public byte DisplayLevel { get; init; }

    /// <summary>The patch the quest was added in ("6.55"); empty when unknown. Added in 1.8.0.</summary>
    public string Patch { get; init; } = string.Empty;

    /// <summary>A main scenario quest. Added in 1.8.0.</summary>
    public bool IsMsq { get; init; }

    /// <summary>A repeatable quest (daily, weekly, allied society). Added in 1.8.0.</summary>
    public bool Repeatable { get; init; }

    /// <summary>The quest's Lodestone Eorzea Database id ("a7cbeb1c618"); empty when the link table has none. Added in 1.8.0.</summary>
    public string LodestoneId { get; init; } = string.Empty;
}

/// <summary>
/// One reward of a Moonlit export; <see cref="Obtained"/> null means the plugin cannot tell. <see cref="Availability"/>
/// (added in 1.5, an additive field) is whether the reward can still be had through its quest; null when the caller
/// did not classify it.
/// </summary>
public sealed record MoonlitExportRow(RewardKind Kind, uint RewardId, string RewardName, uint QuestRowId, bool? Obtained, RewardAvailability? Availability = null)
{
    /// <summary>The item that teaches or holds the reward; 0 when it comes without one. Added in 1.8.0.</summary>
    public uint ItemId { get; init; }

    /// <summary>The reward's FFXIV Collect id; null when the link table has none. Added in 1.8.0.</summary>
    public uint? CollectId { get; init; }
}

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

    // New columns go at the end (formatVersion stays 1): a reader that takes columns by position keeps working.
    private static readonly string[] QuestColumnNames =
        ["rowId", "questId", "name", "section", "category", "genre", "expansion", "completed", "completedAt", "completedAfter", "state", "displayLevel", "patch", "isMsq", "repeatable", "lodestoneId"];

    private static readonly string[] MoonlitColumnNames = ["kind", "rewardId", "rewardName", "questRowId", "obtained", "availability", "itemId", "collectId"];

    /// <summary>The quest CSV's columns, in order (Copy table as TSV adds <c>url</c> after them).</summary>
    public static IReadOnlyList<string> QuestColumns => QuestColumnNames;

    /// <summary>The Moonlit CSV's columns, in order (Copy table as TSV adds <c>url</c> after them).</summary>
    public static IReadOnlyList<string> MoonlitColumns => MoonlitColumnNames;

    /// <summary>
    /// The header for an export of <paramref name="snapshot"/>'s character: its name only when
    /// <paramref name="includeCharacterName"/> is set.
    /// </summary>
    public static ExportHeader Header(string pluginVersion, string gameVersion, DateTime exportedUtc, CharacterSnapshot? snapshot, bool includeCharacterName) =>
        new(
            pluginVersion ?? string.Empty,
            gameVersion ?? string.Empty,
            exportedUtc.Kind == DateTimeKind.Utc ? exportedUtc : exportedUtc.ToUniversalTime(),
            includeCharacterName && snapshot is { Name.Length: > 0 } s ? s.Name : null,
            snapshot?.CompletionDatesSinceUtc);

    /// <summary>
    /// One row per catalog quest the snapshot has completed, in journal order; with <paramref name="includeIncomplete"/>
    /// every catalog quest, each with its completed flag. <paramref name="expansionName"/> names an expansion id
    /// ("Endwalker"); without it the id is written. <paramref name="states"/> (the character's evaluations) fills
    /// <c>state</c>, <paramref name="ids"/> (the link table) <c>lodestoneId</c>; both are left empty without them.
    /// </summary>
    public static List<QuestExportRow> QuestRows(
        QuestCatalog catalog,
        CharacterSnapshot snapshot,
        Func<byte, string>? expansionName = null,
        bool includeIncomplete = false,
        IReadOnlyDictionary<uint, QuestEvaluation>? states = null,
        ExternalIds? ids = null)
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

            rows.Add(Row(quest, snapshot, quest.Name, expansionName, states?.GetValueOrDefault(quest.RowId), ids));
        }

        return rows;
    }

    /// <summary>
    /// One quest's row: <paramref name="name"/> as given (the export writes the quest's own name, Copy table as TSV the
    /// spoiler shield's), completed from <paramref name="snapshot"/> (from <paramref name="evaluation"/> without one),
    /// the completion date from the snapshot, <c>state</c> from the evaluation and <c>lodestoneId</c> from the table.
    /// With <paramref name="spoilers"/> (Copy table as TSV) the journal's section, category and genre are the shield's
    /// too (<see cref="SpoilerMask.NodeName"/>): a genre named after a hidden area reads "Dawntrail area 5 Sidequests".
    /// </summary>
    public static QuestExportRow Row(QuestRecord quest, CharacterSnapshot? snapshot, string name, Func<byte, string>? expansionName, QuestEvaluation? evaluation, ExternalIds? ids, SpoilerMask? spoilers = null)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var expansion = expansionName?.Invoke(quest.Expansion) is { Length: > 0 } expansionText
            ? expansionText
            : quest.Expansion.ToString(CultureInfo.InvariantCulture);
        var completed = snapshot?.IsCompleted(quest.QuestId) ?? evaluation is { State: QuestState.Completed };

        // A quest already complete when dates started has none (its kind is Before): nothing is guessed.
        var date = snapshot is null ? null : CompletionDates.For(snapshot, quest.QuestId);
        var dated = date is { Kind: not CompletionDateKind.Before } d ? d : (QuestCompletionDate?)null;
        return new QuestExportRow(
            quest.RowId,
            quest.QuestId,
            name ?? quest.Name,
            spoilers is null ? quest.Journal.SectionName : spoilers.NodeName(quest.Journal.SectionName),
            spoilers is null ? quest.Journal.CategoryName : spoilers.NodeName(quest.Journal.CategoryName),
            spoilers is null ? quest.Journal.GenreName : spoilers.NodeName(quest.Journal.GenreName),
            expansion,
            completed,
            dated?.Utc,
            dated?.AfterUtc)
        {
            State = evaluation?.State.ToString() ?? string.Empty,
            DisplayLevel = quest.DisplayLevel,
            Patch = quest.AddedIn,
            IsMsq = FeaturePresets.IsMainScenario(quest),
            Repeatable = quest.IsRepeatable,
            LodestoneId = ids?.LodestoneId(quest.RowId) ?? string.Empty,
        };
    }

    /// <summary>One row per reward of the Moonlit unique view with the caller's obtained verdict, in the view's order.</summary>
    public static List<MoonlitExportRow> MoonlitRows(IReadOnlyList<UniqueRewardRow> view) => MoonlitRows(view, null);

    /// <summary>
    /// One row per reward of the Moonlit unique view with the caller's obtained verdict and, when
    /// <paramref name="availability"/> is given, each entry's availability through its quest (the <c>availability</c>
    /// field), in the view's order.
    /// </summary>
    public static List<MoonlitExportRow> MoonlitRows(IReadOnlyList<UniqueRewardRow> view, Func<UniqueRewardEntry, RewardAvailability>? availability) =>
        MoonlitRows(view, availability, null);

    /// <summary>
    /// As <see cref="MoonlitRows(IReadOnlyList{UniqueRewardRow}, Func{UniqueRewardEntry, RewardAvailability}?)"/>, with each
    /// reward's FFXIV Collect id from <paramref name="ids"/> (the link table) where it names one.
    /// </summary>
    public static List<MoonlitExportRow> MoonlitRows(IReadOnlyList<UniqueRewardRow> view, Func<UniqueRewardEntry, RewardAvailability>? availability, ExternalIds? ids)
    {
        ArgumentNullException.ThrowIfNull(view);
        var rows = new List<MoonlitExportRow>(view.Count);
        foreach (var row in view)
        {
            rows.Add(Row(row.Entry, row.Obtained, availability?.Invoke(row.Entry), ids));
        }

        return rows;
    }

    /// <summary>One reward's row: the entry's ids and English name, the caller's obtained verdict and availability, the Collect id from the table.</summary>
    public static MoonlitExportRow Row(UniqueRewardEntry entry, bool? obtained, RewardAvailability? availability, ExternalIds? ids)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new MoonlitExportRow(entry.Kind, entry.RewardId, entry.RewardName, entry.QuestRowId, obtained, availability)
        {
            ItemId = entry.ItemId,
            CollectId = ids?.CollectId(entry.Kind, entry.RewardId),
        };
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
            if (header.CompletionDatesSinceUtc is { } since)
            {
                w.WriteString("completionDatesSinceUtc", IsoUtc(since));
            }

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
                if (row.CompletedAt is { } at)
                {
                    w.WriteString("completedAt", IsoUtc(at));
                }

                if (row.CompletedAfter is { } after)
                {
                    w.WriteString("completedAfter", IsoUtc(after));
                }

                // Added in 1.8.0; an unknown state, patch or Lodestone id is left out.
                if (row.State.Length > 0)
                {
                    w.WriteString("state", row.State);
                }

                w.WriteNumber("displayLevel", row.DisplayLevel);
                if (row.Patch.Length > 0)
                {
                    w.WriteString("patch", row.Patch);
                }

                w.WriteBoolean("isMsq", row.IsMsq);
                w.WriteBoolean("repeatable", row.Repeatable);
                if (row.LodestoneId.Length > 0)
                {
                    w.WriteString("lodestoneId", row.LodestoneId);
                }

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

                if (row.Availability is { } availability)
                {
                    w.WriteString("availability", RewardAvailabilities.ExportName(availability));
                }

                // Added in 1.8.0; a reward without an item, or one the link table has no Collect id for, leaves them out.
                if (row.ItemId != 0)
                {
                    w.WriteNumber("itemId", row.ItemId);
                }

                if (row.CollectId is { } collectId)
                {
                    w.WriteNumber("collectId", collectId);
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
        var sb = Csv(QuestColumnNames);
        foreach (var row in rows)
        {
            Line(sb, Fields(row));
        }

        return sb.ToString();
    }

    /// <summary>A quest row's values in <see cref="QuestColumns"/> order, as the CSV and the TSV write them.</summary>
    public static string[] Fields(QuestExportRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return
        [
            Number(row.RowId),
            Number(row.QuestId),
            row.Name,
            row.Section,
            row.Category,
            row.Genre,
            row.Expansion,
            Bool(row.Completed),
            row.CompletedAt is { } at ? IsoUtc(at) : string.Empty,
            row.CompletedAfter is { } after ? IsoUtc(after) : string.Empty,
            row.State,
            Number(row.DisplayLevel),
            row.Patch,
            Bool(row.IsMsq),
            Bool(row.Repeatable),
            row.LodestoneId,
        ];
    }

    /// <summary>A reward row's values in <see cref="MoonlitColumns"/> order, as the CSV and the TSV write them.</summary>
    public static string[] Fields(MoonlitExportRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return
        [
            row.Kind.ToString(),
            Number(row.RewardId),
            row.RewardName,
            Number(row.QuestRowId),
            row.Obtained switch { true => "true", false => "false", null => Unknown },
            row.Availability is { } availability ? RewardAvailabilities.ExportName(availability) : string.Empty,
            row.ItemId == 0 ? string.Empty : Number(row.ItemId),
            row.CollectId is { } collectId ? Number(collectId) : string.Empty,
        ];
    }

    private static string Bool(bool value) => value ? "true" : "false";

    /// <summary>ISO 8601 in UTC with a Z, as <c>exportedUtc</c> is written.</summary>
    private static string IsoUtc(DateTime time) =>
        (time.Kind == DateTimeKind.Local ? time.ToUniversalTime() : time).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>The Moonlit export as CSV: one header row, then one row per reward; obtained is true, false or unknown.</summary>
    public static string MoonlitCsv(IReadOnlyList<MoonlitExportRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var sb = Csv(MoonlitColumnNames);
        foreach (var row in rows)
        {
            Line(sb, Fields(row));
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

    /// <summary>
    /// Writes an export into <paramref name="directory"/> (created when missing) and returns the full path. The file is
    /// first written to a uniquely named temporary file in <paramref name="stagingDirectory"/> (the plugin's config
    /// directory; <paramref name="directory"/> itself when null) and then moved into place, so the user's folder never
    /// holds a half-written export or a leftover <c>.tmp</c>: a failed write or move deletes the temporary file
    /// before the exception propagates.
    /// </summary>
    public static string Write(string directory, string fileName, string content, string? stagingDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);
        var path = Path.GetFullPath(Path.Combine(directory, fileName));
        var staging = Path.GetFullPath(string.IsNullOrWhiteSpace(stagingDirectory) ? directory : stagingDirectory);
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var tmp = Path.Combine(staging, "tsukimichi-export-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            // A single BOM comes from the content itself (the CSV writers add one); the file is written without another.
            File.WriteAllText(tmp, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            TryDelete(tmp);
            throw;
        }

        return path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: a temporary file left in the plugin's own directory harms nothing.
        }
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
