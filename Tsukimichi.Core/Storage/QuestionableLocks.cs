using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Tsukimichi.Core.Storage;

/// <summary>
/// <c>docs/data/questionable-locks.json</c> (1.22.0): the quests Questionable holds back on a check of its own besides
/// the quest prerequisites (the <c>questPrereqs</c> switch of its <c>IsQuestLocked</c>: an achievement, a mount
/// collection, an unlock link, a Chocobo Racing rank), as Quest sheet row ids with the kind and ids of the check, at one
/// commit of its source, with no code or text of it. A <c>curated/game_gates.json</c> entry may cite Questionable as a
/// source (<c>"questionable": true</c>) only for a quest this file lists; <c>GameGatesDataTests</c> checks it. Shape:
/// <code>{ "$schema_note": "...", "source": "https://github.com/...", "ref": "new-main", "commit": "db49ec1...", "file": "Questionable/Functions/QuestFunctions.cs", "extracted": "2026-10-04", "locks": [ [69617, "achievement", [2819]] ] }</code>
/// </summary>
/// <param name="Source">The repository the locks were read from.</param>
/// <param name="Ref">The branch read.</param>
/// <param name="Commit">The full commit hash they were read at.</param>
/// <param name="File">The file of the repository the switch sits in.</param>
/// <param name="Extracted">The day they were read (yyyy-MM-dd).</param>
/// <param name="Locks">One per switch arm, sorted by quest row id, no quest twice.</param>
public sealed partial record QuestionableLocks(string Source, string Ref, string Commit, string File, string Extracted, IReadOnlyList<QuestionableLock> Locks)
{
    public const string FileName = "questionable-locks.json";

    /// <summary>The kinds of check an arm makes, by the function it calls.</summary>
    public const string Achievement = "achievement";

    public const string Mounts = "mounts";

    public const string UnlockLink = "unlockLink";

    public const string RaceChocoboRank = "raceChocoboRank";

    /// <summary>The note written at the top of the file.</summary>
    public const string SchemaNote =
        "locks: [quest row id, kind, [ids]], one per arm of the questPrereqs switch in IsQuestLocked at the commit, the runtime quest id converted to a Quest sheet row id (+65536). kind is the check the arm calls: achievement (IsAchievementComplete), mounts (AllMountsUnlocked, every mount), unlockLink (IsUnlockLinkUnlocked) or raceChocoboRank (RaceChocoboRankN, the rank). Ids only: no code or text of the source is copied. A curated/game_gates.json entry may cite Questionable (\"questionable\": true) only for a quest listed here; GameGatesDataTests checks it.";

    private const uint RowIdOffset = 65536;

    private static readonly string[] Kinds = [Achievement, Mounts, UnlockLink, RaceChocoboRank];

    private static readonly JsonSerializerOptions QuoteOptions = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The lock of <paramref name="questRowId"/>, or null when Questionable holds it back on nothing of its own.</summary>
    public QuestionableLock? Of(uint questRowId) => Locks.FirstOrDefault(l => l.QuestRowId == questRowId);

    /// <summary>Reads the file; a missing field, a malformed lock or an unsorted list throws <see cref="InvalidDataException"/> naming it.</summary>
    public static QuestionableLocks Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var name = Path.GetFileName(path);
        JsonObject root;
        try
        {
            root = JsonNode.Parse(System.IO.File.ReadAllText(path), documentOptions: CuratedData.StrictOptions)?.AsObject()
                   ?? throw new InvalidDataException($"{name} is empty");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{name} could not be parsed: {ex.Message}", ex);
        }

        string Text(string key) => StorageJson.ReadString(root, key) is { Length: > 0 } value ? value : throw new InvalidDataException($"{name}: {key} is missing");

        var commit = Text("commit");
        if (commit.Length != 40 || !commit.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException($"{name}: commit '{commit}' is not a full git hash");
        }

        if (root["locks"] is not JsonArray array)
        {
            throw new InvalidDataException($"{name}: locks is missing");
        }

        var locks = new List<QuestionableLock>(array.Count);
        for (var i = 0; i < array.Count; i++)
        {
            if (array[i] is not JsonArray { Count: 3 } entry
                || !StorageJson.TryReadId(entry[0], out var quest) || quest < RowIdOffset
                || entry[1] is not JsonValue kindValue || !kindValue.TryGetValue<string>(out var kind) || !Kinds.Contains(kind)
                || entry[2] is not JsonArray { Count: > 0 } idArray)
            {
                throw new InvalidDataException($"{name}: locks[{i}] is not [quest row id, kind, [ids]]");
            }

            var ids = new List<uint>(idArray.Count);
            foreach (var element in idArray)
            {
                if (!StorageJson.TryReadId(element, out var id) || id == 0)
                {
                    throw new InvalidDataException($"{name}: locks[{i}] has an id that is no positive integer");
                }

                ids.Add(id);
            }

            locks.Add(new QuestionableLock(quest, kind, ids));
        }

        if (!locks.Select(l => l.QuestRowId).SequenceEqual(locks.Select(l => l.QuestRowId).Order().Distinct()))
        {
            throw new InvalidDataException($"{name}: locks are not sorted by quest row id without repeats");
        }

        return new QuestionableLocks(Text("source"), Text("ref"), commit, Text("file"), Text("extracted"), locks);
    }

    /// <summary>Writes the file in the shape <see cref="Load"/> reads, one lock per line, sorted by quest.</summary>
    public void Write(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string Quote(string value) => JsonSerializer.Serialize(value, QuoteOptions);
        var lines = Locks.OrderBy(l => l.QuestRowId).Select(l => $"    [{l.QuestRowId}, {Quote(l.Kind)}, [{string.Join(", ", l.Ids)}]]");
        var text = "{\n"
                   + $"  \"$schema_note\": {Quote(SchemaNote)},\n"
                   + $"  \"source\": {Quote(Source)},\n"
                   + $"  \"ref\": {Quote(Ref)},\n"
                   + $"  \"commit\": {Quote(Commit)},\n"
                   + $"  \"file\": {Quote(File)},\n"
                   + $"  \"extracted\": {Quote(Extracted)},\n"
                   + "  \"locks\": [\n"
                   + string.Join(",\n", lines) + "\n"
                   + "  ]\n"
                   + "}\n";
        AtomicFile.Write(path, text);
    }

    /// <summary>
    /// The locks of a local copy of Questionable's <c>Questionable/Functions/QuestFunctions.cs</c> (fetched by the
    /// maintainer; the tools never fetch it): every arm <c>&lt;quest id&gt; =&gt; &lt;check&gt;(…)</c> of the switch assigned to
    /// <c>questPrereqs</c>, comments removed, as sorted locks. An arm whose check is none of the four known kinds throws
    /// <see cref="InvalidDataException"/> naming it, so a refresh never drops a new kind of lock silently; a file with no
    /// such switch throws too.
    /// </summary>
    public static List<QuestionableLock> Extract(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        var text = LineComment().Replace(BlockComment().Replace(code, " "), string.Empty);
        var start = PrereqSwitch().Match(text);
        if (!start.Success)
        {
            throw new InvalidDataException("no `questPrereqs = ... switch` found");
        }

        var end = text.IndexOf("_ =>", start.Index, StringComparison.Ordinal);
        if (end < 0)
        {
            throw new InvalidDataException("the questPrereqs switch has no default arm");
        }

        var body = text[(start.Index + start.Length)..end];
        var locks = new SortedDictionary<uint, QuestionableLock>();
        foreach (Match m in Arm().Matches(body))
        {
            var quest = uint.Parse(m.Groups["quest"].Value, CultureInfo.InvariantCulture) + RowIdOffset;
            var call = m.Groups["call"].Value;
            var args = Number().Matches(m.Groups["args"].Value).Select(n => uint.Parse(n.Value, CultureInfo.InvariantCulture)).ToList();
            var rank = RankCall().Match(call);
            var (kind, ids) = call switch
            {
                "IsAchievementComplete" => (Achievement, args),
                "AllMountsUnlocked" => (Mounts, args),
                "IsUnlockLinkUnlocked" => (UnlockLink, args),
                _ when rank.Success => (RaceChocoboRank, [uint.Parse(rank.Groups[1].Value, CultureInfo.InvariantCulture)]),
                _ => throw new InvalidDataException($"questPrereqs arm for quest {quest - RowIdOffset} calls {call}, which is no known kind of lock"),
            };

            if (ids.Count == 0 || !locks.TryAdd(quest, new QuestionableLock(quest, kind, ids)))
            {
                throw new InvalidDataException($"questPrereqs arm for quest {quest - RowIdOffset} names no id or repeats the quest");
            }
        }

        return [.. locks.Values];
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex BlockComment();

    [GeneratedRegex(@"//[^\n]*", RegexOptions.CultureInvariant)]
    private static partial Regex LineComment();

    [GeneratedRegex(@"\bquestPrereqs\s*=\s*[\w.]+\s+switch\s*\{", RegexOptions.CultureInvariant)]
    private static partial Regex PrereqSwitch();

    [GeneratedRegex(@"(?<quest>\d+)\s*=>\s*(?<call>\w+)\((?<args>[^;]*?)\)\s*,", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex Arm();

    [GeneratedRegex(@"\d+", RegexOptions.CultureInvariant)]
    private static partial Regex Number();

    [GeneratedRegex(@"^RaceChocoboRank(\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex RankCall();
}

/// <summary>One quest Questionable holds back on a check of its own (<see cref="QuestionableLocks"/>).</summary>
/// <param name="QuestRowId">The Quest sheet row id.</param>
/// <param name="Kind">One of <see cref="QuestionableLocks.Achievement"/>, <see cref="QuestionableLocks.Mounts"/>, <see cref="QuestionableLocks.UnlockLink"/>, <see cref="QuestionableLocks.RaceChocoboRank"/>.</param>
/// <param name="Ids">The achievement, the mounts (every one), the unlock link or the rank.</param>
public sealed record QuestionableLock(uint QuestRowId, string Kind, IReadOnlyList<uint> Ids);
