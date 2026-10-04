using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Storage;

/// <summary>Loading <c>curated/story_required.json</c> (feature plan v7 N3): the shape, each required field, and what is skipped.</summary>
[Trait("Category", "Curated")]
public sealed class StoryRequiredFileTests : IDisposable
{
    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private CuratedData Load(string json)
    {
        var dir = tmp.File("curated");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, CuratedData.StoryRequiredFileName), json);
        return CuratedData.Load(dir);
    }

    [Fact]
    public void Entries_parse_their_quests_and_join()
    {
        var data = Load(
            """
            {
              "schema": 1,
              "note": "header",
              "entries": {
                "69186": { "anyOf": [68784, 68808], "evidence": "https://e.org/a", "note": "one line" },
                "65961": { "allOf": [66031], "evidence": "https://e.org/b", "note": "every one" }
              }
            }
            """);

        Assert.Empty(data.Warnings);
        var any = data.StoryRequired[69186];
        Assert.Equal(JoinKind.Any, any.Join);
        Assert.Equal([68784u, 68808u], any.Quests);
        Assert.Equal("https://e.org/a", any.Evidence);
        Assert.Equal("one line", any.Note);
        Assert.Equal(JoinKind.All, data.StoryRequired[65961].Join);

        // DataGen's copy without feature quests keeps them.
        Assert.Equal(2, data.WithoutFeatureQuests().StoryRequired.Count);
    }

    [Theory]
    [InlineData("""{ "anyOf": [1], "allOf": [2], "evidence": "https://e.org", "note": "n" }""")]
    [InlineData("""{ "evidence": "https://e.org", "note": "n" }""")]
    [InlineData("""{ "anyOf": [], "evidence": "https://e.org", "note": "n" }""")]
    [InlineData("""{ "anyOf": [68784, 68784], "evidence": "https://e.org", "note": "n" }""")]
    [InlineData("""{ "anyOf": [69186], "evidence": "https://e.org", "note": "n" }""")]
    [InlineData("""{ "anyOf": [68784], "evidence": "http://e.org", "note": "n" }""")]
    [InlineData("""{ "anyOf": [68784], "note": "n" }""")]
    [InlineData("""{ "anyOf": [68784], "evidence": "https://e.org" }""")]
    public void A_malformed_entry_is_skipped_with_a_warning(string entry)
    {
        var data = Load($$"""{ "schema": 1, "entries": { "69186": {{entry}} } }""");
        Assert.Empty(data.StoryRequired);
        Assert.Single(data.Warnings);
    }

    [Fact]
    public void A_missing_file_reads_as_none()
    {
        var dir = tmp.File("empty");
        Directory.CreateDirectory(dir);
        Assert.Empty(CuratedData.Load(dir).StoryRequired);
    }
}
