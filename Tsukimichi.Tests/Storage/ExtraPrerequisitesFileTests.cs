using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Storage;

/// <summary>Loading <c>curated/extra_prerequisites.json</c> (1.5.0 Gates): the shape, the two-source rule and each required field.</summary>
[Trait("Category", "Curated")]
public sealed class ExtraPrerequisitesFileTests : IDisposable
{
    private const string Good = """{ "requires": [68850], "sources": ["questionable", "wiki"], "evidence": "https://e.org/a", "note": "n" }""";

    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private CuratedData Load(string json)
    {
        var dir = tmp.File("curated");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, CuratedData.ExtraPrerequisitesFileName), json);
        return CuratedData.Load(dir);
    }

    [Fact]
    public void Entries_parse_their_ids_sources_and_game_text_key()
    {
        var data = Load(
            """
            {
              "schema": 1,
              "note": "header",
              "entries": {
                "68782": { "requires": [68850], "sources": ["gameText", "questionable", "wiki"], "gameTextKey": "TEXT_LUCKBA131_03246_SYSTEM_100_001", "evidence": "https://e.org/a", "note": "n" },
                "69522": { "requires": [67651, "67652"], "sources": ["questionable", "wiki", "wiki"], "evidence": "https://e.org/b", "note": "n2" }
              }
            }
            """);

        Assert.Empty(data.Warnings);
        Assert.Equal([68782u, 69522u], data.ExtraPrerequisites.Keys.Order());
        var princess = data.ExtraPrerequisites[68782];
        Assert.Equal([68850u], princess.Requires);
        Assert.Equal(["gameText", "questionable", "wiki"], princess.Sources);
        Assert.Equal("TEXT_LUCKBA131_03246_SYSTEM_100_001", princess.GameTextKey);
        Assert.Equal("https://e.org/a", princess.Evidence);
        Assert.Equal("n", princess.Note);

        var hero = data.ExtraPrerequisites[69522];
        Assert.Equal([67651u, 67652u], hero.Requires);
        Assert.Equal(["questionable", "wiki"], hero.Sources);
        Assert.Null(hero.GameTextKey);

        // What the catalog builders take.
        Assert.Equal([68850u], data.ExtraPrerequisiteIds[68782]);
        Assert.Equal([67651u, 67652u], data.ExtraPrerequisiteIds[69522]);

        // DataGen's copy without feature quests keeps them.
        Assert.Equal(2, data.WithoutFeatureQuests().ExtraPrerequisites.Count);
    }

    [Theory]
    [InlineData("\"x\"", Good, "key is not a quest row id")]
    [InlineData("\"0\"", Good, "key is not a quest row id")]
    [InlineData("\"68782\"", "[68850]", "value is not an object")]
    [InlineData("\"68782\"", """{ "sources": ["questionable", "wiki"], "evidence": "https://e.org", "note": "n" }""", "requires must be a non-empty array")]
    [InlineData("\"68782\"", """{ "requires": [], "sources": ["questionable", "wiki"], "evidence": "https://e.org", "note": "n" }""", "requires must be a non-empty array")]
    [InlineData("\"68782\"", """{ "requires": [0], "sources": ["questionable", "wiki"], "evidence": "https://e.org", "note": "n" }""", "requires id '0' is not a quest row id")]
    [InlineData("\"68782\"", """{ "requires": [68782], "sources": ["questionable", "wiki"], "evidence": "https://e.org", "note": "n" }""", "requires names the quest itself")]
    [InlineData("\"68782\"", """{ "requires": [68850, 68850], "sources": ["questionable", "wiki"], "evidence": "https://e.org", "note": "n" }""", "requires lists 68850 twice")]
    [InlineData("\"68782\"", """{ "requires": [68850], "sources": ["wiki"], "evidence": "https://e.org", "note": "n" }""", "sources must name at least two")]
    [InlineData("\"68782\"", """{ "requires": [68850], "sources": ["wiki", "wiki"], "evidence": "https://e.org", "note": "n" }""", "sources must name at least two")]
    [InlineData("\"68782\"", """{ "requires": [68850], "evidence": "https://e.org", "note": "n" }""", "sources must name at least two")]
    [InlineData("\"68782\"", """{ "requires": [68850], "sources": ["wiki", "forum"], "evidence": "https://e.org", "note": "n" }""", "source 'forum' is not one of")]
    [InlineData("\"68782\"", """{ "requires": [68850], "sources": ["gameText", "wiki"], "evidence": "https://e.org", "note": "n" }""", "gameTextKey must be the TEXT_ key")]
    [InlineData("\"68782\"", """{ "requires": [68850], "sources": ["gameText", "wiki"], "gameTextKey": "SYSTEM_100", "evidence": "https://e.org", "note": "n" }""", "gameTextKey must be the TEXT_ key")]
    [InlineData("\"68782\"", """{ "requires": [68850], "sources": ["questionable", "wiki"], "gameTextKey": "TEXT_X", "evidence": "https://e.org", "note": "n" }""", "does not cite gameText")]
    [InlineData("\"68782\"", """{ "requires": [68850], "sources": ["questionable", "wiki"], "evidence": "https://e.org" }""", "note is missing")]
    [InlineData("\"68782\"", """{ "requires": [68850], "sources": ["questionable", "wiki"], "note": "n" }""", "evidence is missing")]
    [InlineData("\"68782\"", """{ "requires": [68850], "sources": ["questionable", "wiki"], "evidence": "http://e.org", "note": "n" }""", "is not an https URL")]
    public void A_bad_entry_is_skipped_with_a_warning_naming_the_file_and_the_key(string key, string entry, string reason)
    {
        var data = Load($$"""{ "schema": 1, "entries": { {{key}}: {{entry}}, "69522": {{Good}} } }""");

        Assert.Equal([69522u], data.ExtraPrerequisites.Keys);
        var warning = Assert.Single(data.Warnings);
        Assert.StartsWith(CuratedData.ExtraPrerequisitesFileName + ": entry " + key, warning, StringComparison.Ordinal);
        Assert.Contains(reason, warning, StringComparison.Ordinal);
    }

    [Fact]
    public void A_file_with_a_trailing_comma_is_rejected_whole()
    {
        var data = Load($$"""{ "entries": { "68782": {{Good}}, } }""");

        Assert.Empty(data.ExtraPrerequisites);
        Assert.Contains(CuratedData.ExtraPrerequisitesFileName, Assert.Single(data.Warnings), StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_file_adds_no_prerequisite()
    {
        var dir = tmp.File("empty");
        Directory.CreateDirectory(dir);
        var data = CuratedData.Load(dir);

        Assert.Empty(data.Warnings);
        Assert.Empty(data.ExtraPrerequisites);
        Assert.Empty(CuratedData.Empty.ExtraPrerequisiteIds);
    }
}
