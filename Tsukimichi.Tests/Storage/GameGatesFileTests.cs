using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Storage;

/// <summary>Loading <c>curated/game_gates.json</c>: the shape, each required field, and the gates the catalog builders take.</summary>
[Trait("Category", "Curated")]
public sealed class GameGatesFileTests : IDisposable
{
    private const string Good = """{ "gate": "a relic weapon nexus equipped", "evidence": "https://e.org/a", "note": "n" }""";

    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private CuratedData Load(string json)
    {
        var dir = tmp.File("curated");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, CuratedData.GameGatesFileName), json);
        return CuratedData.Load(dir);
    }

    [Fact]
    public void Entries_parse_their_gate_after_quests_and_text_keys()
    {
        var data = Load(
            """
            {
              "schema": 1,
              "note": "header",
              "entries": {
                "65897": { "gate": " a relic weapon nexus equipped ", "after": [65742], "gameTextKey": "TEXT_JOBREL015_00361_SYSTEM_000_000", "afterTextKey": "TEXT_JOBREL007_00206_SYSTEM_000_100", "evidence": "https://e.org/a", "note": "n" },
                "68478": { "gate": "Eureka progress", "evidence": "https://e.org/b", "note": "n2" }
              }
            }
            """);

        Assert.Empty(data.Warnings);
        Assert.Equal([65897u, 68478u], data.GameGates.Keys.Order());
        var materia = data.GameGates[65897];
        Assert.Equal("a relic weapon nexus equipped", materia.Gate);
        Assert.Equal([65742u], materia.After);
        Assert.Equal("TEXT_JOBREL015_00361_SYSTEM_000_000", materia.GameTextKey);
        Assert.Equal("TEXT_JOBREL007_00206_SYSTEM_000_100", materia.AfterTextKey);
        Assert.Equal("https://e.org/a", materia.Evidence);
        Assert.Equal("n", materia.Note);

        var pagos = data.GameGates[68478];
        Assert.Empty(pagos.After);
        Assert.Null(pagos.GameTextKey);
        Assert.Null(pagos.AfterTextKey);

        // What the catalog builders take, and DataGen's copy without feature quests keeps them.
        Assert.Equal(new QuestGate("a relic weapon nexus equipped", [65742]).Gate, data.GameGateIds[65897].Gate);
        Assert.Equal([65742u], data.GameGateIds[65897].After);
        Assert.Equal(2, data.WithoutFeatureQuests().GameGates.Count);
    }

    [Theory]
    [InlineData("\"x\"", Good, "key is not a quest row id")]
    [InlineData("\"65897\"", "[65742]", "value is not an object")]
    [InlineData("\"65897\"", """{ "evidence": "https://e.org", "note": "n" }""", "gate must say what the game wants")]
    [InlineData("\"65897\"", """{ "gate": " ", "evidence": "https://e.org", "note": "n" }""", "gate must say what the game wants")]
    [InlineData("\"65897\"", """{ "gate": "g", "after": 65742, "afterTextKey": "TEXT_A", "evidence": "https://e.org", "note": "n" }""", "after must be an array")]
    [InlineData("\"65897\"", """{ "gate": "g", "after": [65897], "afterTextKey": "TEXT_A", "evidence": "https://e.org", "note": "n" }""", "names the quest itself or repeats")]
    [InlineData("\"65897\"", """{ "gate": "g", "after": [65742, 65742], "afterTextKey": "TEXT_A", "evidence": "https://e.org", "note": "n" }""", "names the quest itself or repeats")]
    [InlineData("\"65897\"", """{ "gate": "g", "after": [65742], "evidence": "https://e.org", "note": "n" }""", "afterTextKey must be the TEXT_ key")]
    [InlineData("\"65897\"", """{ "gate": "g", "afterTextKey": "TEXT_A", "evidence": "https://e.org", "note": "n" }""", "afterTextKey must be the TEXT_ key")]
    [InlineData("\"65897\"", """{ "gate": "g", "after": [65742], "afterTextKey": "SYSTEM_1", "evidence": "https://e.org", "note": "n" }""", "afterTextKey must be the TEXT_ key")]
    [InlineData("\"65897\"", """{ "gate": "g", "gameTextKey": "SYSTEM_1", "evidence": "https://e.org", "note": "n" }""", "gameTextKey must be a TEXT_ key")]
    [InlineData("\"65897\"", """{ "gate": "g", "evidence": "https://e.org" }""", "note is missing")]
    [InlineData("\"65897\"", """{ "gate": "g", "note": "n" }""", "evidence is missing")]
    [InlineData("\"65897\"", """{ "gate": "g", "evidence": "http://e.org", "note": "n" }""", "is not an https URL")]
    public void A_bad_entry_is_skipped_with_a_warning_naming_the_file_and_the_key(string key, string entry, string reason)
    {
        var data = Load($$"""{ "schema": 1, "entries": { {{key}}: {{entry}}, "68478": {{Good}} } }""");

        Assert.Equal([68478u], data.GameGates.Keys);
        var warning = Assert.Single(data.Warnings);
        Assert.StartsWith(CuratedData.GameGatesFileName + ": entry " + key, warning, StringComparison.Ordinal);
        Assert.Contains(reason, warning, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_file_adds_no_gate()
    {
        var dir = tmp.File("empty");
        Directory.CreateDirectory(dir);
        var data = CuratedData.Load(dir);

        Assert.Empty(data.Warnings);
        Assert.Empty(data.GameGates);
        Assert.Empty(CuratedData.Empty.GameGateIds);
    }
}
