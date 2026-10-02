using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lumina.Data;
using Lumina.Excel;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The shipped <c>curated/extra_prerequisites.json</c> against the frozen catalog and the two-source rule: every entry
/// cites at least two sources, and each cited source is checked offline where it can be (Questionable's committed
/// links, the wiki infobox the verifier recorded in <c>docs/data/quest-verification.csv</c>); the game's own text is
/// checked by <see cref="ExtraPrerequisitesGameTextTests"/> where the game is installed.
/// </summary>
[Trait("Category", "Curated")]
public sealed partial class ExtraPrerequisitesDataTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private const string File = CuratedData.ExtraPrerequisitesFileName;

    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    private IReadOnlyDictionary<uint, ExtraPrerequisite> Extras => fixture.Curated.ExtraPrerequisites;

    /// <summary>The repository's <c>docs/data</c> directory.</summary>
    internal static string DocsDataDir() => Path.Combine(FixtureCatalog.ShippedDataDir(), "..", "..", "docs", "data");

    /// <summary>A quest name as the wiki writes it: no private-use icon glyph, no "(Quest)" disambiguation, any case.</summary>
    internal static string Canon(string name) => QuestSuffix().Replace(IconGlyph().Replace(name, string.Empty), string.Empty).Trim().ToLowerInvariant();

    [Fact]
    public void The_file_is_sorted_and_every_entry_adds_a_gate_the_catalog_did_not_have()
    {
        var root = JsonNode.Parse(System.IO.File.ReadAllText(Path.Combine(FixtureCatalog.CuratedDir(), File)), documentOptions: CuratedData.StrictOptions)!.AsObject();
        Assert.Equal(1, (int)root["schema"]!);
        Assert.False(string.IsNullOrWhiteSpace((string?)root["note"]));
        var keys = root["entries"]!.AsObject().Select(kv => uint.Parse(kv.Key, CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(keys.Order().Distinct(), keys);
        Assert.Equal(keys.Count, Extras.Count);

        var problems = new List<string>();
        foreach (var (rowId, entry) in Extras)
        {
            if (Catalog.GetByRowId(rowId) is not { } quest)
            {
                problems.Add($"{File}:{rowId} is not a quest of the catalog fixture");
                continue;
            }

            foreach (var id in entry.Requires)
            {
                if (Catalog.GetByRowId(id) is null)
                {
                    problems.Add($"{File}:{rowId} requires {id}, which is not a quest of the catalog fixture");
                }
                else if (quest.PreviousQuests.QuestIds.Contains(id) || quest.AcceptConditions.Contains(id))
                {
                    problems.Add($"{File}:{rowId} requires {id}, which the sheet already records; remove it");
                }
                else if (PrerequisiteCoverage.Requires(Catalog, id, rowId))
                {
                    problems.Add($"{File}:{rowId} requires {id}, which itself waits for {rowId}: a cycle");
                }
            }

            if (!entry.Evidence.StartsWith("https://", StringComparison.Ordinal))
            {
                problems.Add($"{File}:{rowId} evidence is not an https URL");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Entries_citing_Questionable_match_its_committed_links()
    {
        var links = PrerequisiteLinks.Load(Path.Combine(DocsDataDir(), PrerequisiteLinks.FileName)).Pairs.ToHashSet();
        var missing = Extras
            .Where(kv => kv.Value.Sources.Contains(CuratedData.QuestionableSource))
            .SelectMany(kv => kv.Value.Requires.Where(id => !links.Contains((kv.Key, id))).Select(id => $"{File}:{kv.Key} cites questionable, but {PrerequisiteLinks.FileName} has no link {kv.Key} -> {id}"))
            .ToList();
        Assert.True(missing.Count == 0, string.Join("\n", missing));
    }

    [Fact]
    public void Entries_citing_the_wiki_match_the_infobox_the_verifier_recorded()
    {
        var wiki = WikiPrerequisites();
        var problems = new List<string>();
        foreach (var (rowId, entry) in Extras.Where(kv => kv.Value.Sources.Contains(CuratedData.WikiSource)))
        {
            if (!wiki.TryGetValue(rowId, out var row))
            {
                problems.Add($"{File}:{rowId} cites wiki, but quest-verification.csv has no wiki prerequisite row for it");
                continue;
            }

            problems.AddRange(entry.Requires
                .Where(id => !row.Names.Contains(Canon(Catalog.GetByRowId(id)?.Name ?? string.Empty)))
                .Select(id => $"{File}:{rowId} cites wiki, but the wiki infobox does not name {id} {Catalog.GetByRowId(id)?.Name}"));
            if (entry.Evidence != row.Url)
            {
                problems.Add($"{File}:{rowId} cites wiki; its evidence should be the page the verifier read, {row.Url}");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Game_text_keys_name_a_quest_of_the_catalog()
    {
        // The key's script id (TEXT_LUCKBA131_03246_...) is a quest's InternalId; the text sits in that quest's sheet.
        var byScript = Catalog.All.Where(q => q.InternalId.Length > 0).GroupBy(q => q.InternalId.ToUpperInvariant()).ToDictionary(g => g.Key, g => g.First());
        var problems = Extras
            .Where(kv => kv.Value.GameTextKey is not null)
            .Where(kv => ExtraPrerequisitesGameTextTests.ScriptOf(kv.Value.GameTextKey!) is not { } script || !byScript.ContainsKey(script))
            .Select(kv => $"{File}:{kv.Key} gameTextKey {kv.Value.GameTextKey} names no quest script of the catalog")
            .ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>quest-verification.csv's wiki prerequisite rows: quest row id to the infobox names (canonical) and the page.</summary>
    private static Dictionary<uint, (HashSet<string> Names, string Url)> WikiPrerequisites()
    {
        var rows = new Dictionary<uint, (HashSet<string>, string)>();
        foreach (var line in System.IO.File.ReadLines(Path.Combine(DocsDataDir(), "quest-verification.csv")))
        {
            if (!line.Contains(",prereqs,", StringComparison.Ordinal) || !line.Contains(",wiki,", StringComparison.Ordinal))
            {
                continue;
            }

            // rowId,name,fact,catalogValue,source,sourceValue,sourceRef,verdict,reason,fixedIn
            var fields = SplitCsv(line);
            if (fields.Count < 7 || fields[2] != "prereqs" || fields[4] != "wiki")
            {
                continue;
            }

            var names = fields[5].Split(';', StringSplitOptions.RemoveEmptyEntries).Select(Canon).ToHashSet(StringComparer.Ordinal);
            rows[uint.Parse(fields[0], CultureInfo.InvariantCulture)] = (names, fields[6]);
        }

        return rows;
    }

    private static List<string> SplitCsv(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }

    [GeneratedRegex(@"[-]", RegexOptions.CultureInvariant)]
    private static partial Regex IconGlyph();

    [GeneratedRegex(@"\s*\(Quest\)$", RegexOptions.CultureInvariant)]
    private static partial Regex QuestSuffix();
}

/// <summary>
/// The <c>gameText</c> source of <c>curated/extra_prerequisites.json</c> against the installed game: the quest text row
/// each entry names exists and names every required quest. The text is read, compared and never written anywhere.
/// </summary>
public sealed partial class ExtraPrerequisitesGameTextTests(GameDataFixture game) : IClassFixture<GameDataFixture>
{
    /// <summary>The script id of a text key, upper-case ("TEXT_LUCKBA131_03246_SYSTEM_100_001" to "LUCKBA131_03246"); null when the key has none.</summary>
    internal static string? ScriptOf(string key)
    {
        var m = ScriptId().Match(key);
        return m.Success ? m.Groups[1].Value : null;
    }

    [Theory]
    [InlineData("TEXT_LUCKBA131_03246_SYSTEM_100_001", "LUCKBA131_03246")]
    [InlineData("TEXT_BANPIX003_03685_SYSTEM_000_150", "BANPIX003_03685")]
    [InlineData("SYSTEM_100_001", null)]
    public void The_script_id_is_read_from_the_key(string key, string? script)
    {
        Assert.Equal(script, ScriptOf(key));
    }

    [GameDataFact]
    public void Every_game_text_key_names_each_required_quest()
    {
        var catalog = game.Bundle.Catalog;
        var curated = CuratedData.Load(FixtureCatalog.CuratedDir());
        var byScript = catalog.All.Where(q => q.InternalId.Length > 0).GroupBy(q => q.InternalId.ToUpperInvariant()).ToDictionary(g => g.Key, g => g.First());
        var problems = new List<string>();
        var checkedCount = 0;
        foreach (var (rowId, entry) in curated.ExtraPrerequisites.Where(kv => kv.Value.GameTextKey is not null))
        {
            var key = entry.GameTextKey!;
            if (ScriptOf(key) is not { } script || !byScript.TryGetValue(script, out var owner) || QuestTextReader.SheetName(owner.InternalId) is not { } sheetName)
            {
                problems.Add($"{CuratedData.ExtraPrerequisitesFileName}:{rowId} gameTextKey {key} names no quest text sheet");
                continue;
            }

            string? text = null;
            foreach (var row in game.Game.Excel.GetSheet<RawRow>(Language.English, sheetName))
            {
                if (row.ReadStringColumn(0).ExtractText() == key)
                {
                    text = row.ReadStringColumn(1).ExtractText();
                    break;
                }
            }

            if (text is null)
            {
                problems.Add($"{CuratedData.ExtraPrerequisitesFileName}:{rowId} gameTextKey {key} is not a row of {sheetName}");
                continue;
            }

            foreach (var id in entry.Requires)
            {
                var name = IconGlyph().Replace(catalog.GetByRowId(id)?.Name ?? string.Empty, string.Empty).Trim();
                if (name.Length == 0 || !text.Contains(name, StringComparison.Ordinal))
                {
                    problems.Add($"{CuratedData.ExtraPrerequisitesFileName}:{rowId} gameTextKey {key} does not name {id} {name}");
                }
            }

            checkedCount++;
        }

        Assert.True(checkedCount > 0, "no entry cites gameText");
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [GeneratedRegex(@"^TEXT_([A-Z0-9]+_\d{5})_", RegexOptions.CultureInvariant)]
    private static partial Regex ScriptId();

    [GeneratedRegex(@"[-]", RegexOptions.CultureInvariant)]
    private static partial Regex IconGlyph();
}
