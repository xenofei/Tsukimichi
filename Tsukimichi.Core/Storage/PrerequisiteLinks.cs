using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Tsukimichi.Core.Storage;

/// <summary>
/// <c>docs/data/questionable-prerequisites.json</c>: the prerequisite links Questionable adds by hand on top of the game
/// sheets (its <c>AddPreviousQuest</c> calls), as Quest sheet row id pairs at one commit of its source, with no code or
/// text of it. <c>Tsukimichi.Verify questionable</c> writes and checks it; <c>QuestionableLinksTests</c> holds the
/// catalog to it offline, so a link neither the sheet, the accept conditions nor <c>curated/extra_prerequisites.json</c>
/// implies fails CI unless the verification allowlist names it. Shape:
/// <code>{ "$schema_note": "...", "source": "https://github.com/...", "ref": "new-main", "commit": "0bd61ef...", "file": "Questionable/Data/QuestData.cs", "extracted": "2026-10-01", "pairs": [ [68782, 68850] ] }</code>
/// </summary>
/// <param name="Source">The repository the links were read from.</param>
/// <param name="Ref">The branch read.</param>
/// <param name="Commit">The full commit hash they were read at.</param>
/// <param name="File">The file of the repository the calls sit in.</param>
/// <param name="Extracted">The day they were read (yyyy-MM-dd).</param>
/// <param name="Pairs">Quest row id and the quest row id it waits for, sorted, without repeats.</param>
public sealed partial record PrerequisiteLinks(string Source, string Ref, string Commit, string File, string Extracted, IReadOnlyList<(uint QuestRowId, uint RequiredRowId)> Pairs)
{
    public const string FileName = "questionable-prerequisites.json";

    /// <summary>The note written at the top of the file.</summary>
    public const string SchemaNote =
        "pairs: [quest row id, required quest row id], one per AddPreviousQuest call that is not commented out in the file at the commit, the runtime quest ids converted to Quest sheet row ids (+65536). Ids only: no code or text of the source is copied. Read by Tsukimichi.Verify questionable and by QuestionableLinksTests.";

    /// <summary>Questionable's QuestId is the runtime quest id: the Quest sheet row id less this.</summary>
    private const uint RowIdOffset = 65536;

    private static readonly JsonSerializerOptions QuoteOptions = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Reads the file; a missing field, a pair that is not two row ids or an unsorted list throws <see cref="InvalidDataException"/> naming it.</summary>
    public static PrerequisiteLinks Load(string path)
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

        if (root["pairs"] is not JsonArray array)
        {
            throw new InvalidDataException($"{name}: pairs is missing");
        }

        var pairs = new List<(uint, uint)>(array.Count);
        for (var i = 0; i < array.Count; i++)
        {
            if (array[i] is not JsonArray { Count: 2 } pair
                || !StorageJson.TryReadId(pair[0], out var quest) || quest < RowIdOffset
                || !StorageJson.TryReadId(pair[1], out var required) || required < RowIdOffset)
            {
                throw new InvalidDataException($"{name}: pairs[{i}] is not two Quest sheet row ids");
            }

            pairs.Add((quest, required));
        }

        if (!pairs.SequenceEqual(pairs.Order().Distinct()))
        {
            throw new InvalidDataException($"{name}: pairs are not sorted ascending without repeats");
        }

        return new PrerequisiteLinks(Text("source"), Text("ref"), commit, Text("file"), Text("extracted"), pairs);
    }

    /// <summary>Writes the file in the shape <see cref="Load"/> reads, one pair per line, pairs sorted.</summary>
    public void Write(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var lines = Pairs.Order().Distinct().Select(p => $"    [{p.QuestRowId}, {p.RequiredRowId}]");
        string Quote(string value) => JsonSerializer.Serialize(value, QuoteOptions);
        var text = "{\n"
                   + $"  \"$schema_note\": {Quote(SchemaNote)},\n"
                   + $"  \"source\": {Quote(Source)},\n"
                   + $"  \"ref\": {Quote(Ref)},\n"
                   + $"  \"commit\": {Quote(Commit)},\n"
                   + $"  \"file\": {Quote(File)},\n"
                   + $"  \"extracted\": {Quote(Extracted)},\n"
                   + "  \"pairs\": [\n"
                   + string.Join(",\n", lines) + "\n"
                   + "  ]\n"
                   + "}\n";
        AtomicFile.Write(path, text);
    }

    /// <summary>
    /// The links of a local copy of Questionable's <c>Questionable/Data/QuestData.cs</c> (fetched by the maintainer; the
    /// tools never fetch it): every <c>AddPreviousQuest(quest, required)</c> call that is not commented out, with named
    /// integer constants resolved, as (quest row id, required quest row id), sorted, without repeats. Only the two ids of
    /// each call are kept; a call with an empty or unknown id is skipped.
    /// </summary>
    public static List<(uint QuestRowId, uint RequiredRowId)> Extract(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        var text = LineComment().Replace(BlockComment().Replace(code, " "), string.Empty);
        var constants = new Dictionary<string, uint>(StringComparer.Ordinal);
        foreach (Match m in ConstInt().Matches(text))
        {
            constants[m.Groups[1].Value] = uint.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        }

        bool TryResolve(string token, out uint id)
        {
            if (uint.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out id))
            {
                return id > 0;
            }

            return constants.TryGetValue(token, out id) && id > 0;
        }

        var links = new SortedSet<(uint, uint)>();
        foreach (Match m in AddPreviousQuest().Matches(text))
        {
            if (TryResolve(m.Groups[1].Value, out var quest) && TryResolve(m.Groups[2].Value, out var required))
            {
                links.Add((quest + RowIdOffset, required + RowIdOffset));
            }
        }

        return [.. links];
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex BlockComment();

    [GeneratedRegex(@"//[^\n]*", RegexOptions.CultureInvariant)]
    private static partial Regex LineComment();

    [GeneratedRegex(@"\bconst\s+(?:int|uint|ushort)\s+(\w+)\s*=\s*(\d+)\s*;", RegexOptions.CultureInvariant)]
    private static partial Regex ConstInt();

    [GeneratedRegex(@"\bAddPreviousQuest\(\s*new(?:\s+QuestId)?\(\s*(\w*)\s*\)\s*,\s*new(?:\s+QuestId)?\(\s*(\w*)\s*\)\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex AddPreviousQuest();
}
