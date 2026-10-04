using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Storage;

/// <summary>1.21.0 (P5, N10): chains.json's questIds, startQuest and ongoing, and story_cast.json.</summary>
public sealed class CuratedStorylinesTests : IDisposable
{
    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private CuratedData Load(string name, string json)
    {
        var dir = tmp.File("curated");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, name), json);
        return CuratedData.Load(dir);
    }

    [Fact]
    public void Chains_read_the_quest_and_start_forms_and_the_ongoing_flag()
    {
        var data = Load("chains.json",
            """
            {
              "chains": [
                { "name": "By genre", "genreIds": [97], "ongoing": true, "note": "n" },
                { "name": "By quest", "questIds": [70119, "70186", 70119], "note": "n" },
                { "name": "From a start", "startQuest": 70556, "note": "n" }
              ]
            }
            """);

        Assert.Empty(data.Warnings);
        Assert.Equal(3, data.Chains.Count);
        Assert.True(data.Chains[0].Ongoing);
        Assert.Equal([97u], data.Chains[0].GenreIds);
        Assert.Equal([70119u, 70186u], data.Chains[1].QuestIds);
        Assert.Empty(data.Chains[1].GenreIds);
        Assert.False(data.Chains[1].Ongoing);
        Assert.Equal(70556u, data.Chains[2].StartQuest);
        Assert.Empty(data.Chains[2].QuestIds);
    }

    [Fact]
    public void Chains_with_two_forms_none_or_a_bad_start_are_skipped_with_a_warning_each()
    {
        var data = Load("chains.json",
            """
            {
              "chains": [
                { "name": "Good", "questIds": [1] },
                { "name": "Two forms", "genreIds": [1], "questIds": [2] },
                { "name": "None" },
                { "name": "Zero start", "startQuest": 0 },
                { "name": "Empty quests", "questIds": [] }
              ]
            }
            """);

        Assert.Equal("Good", Assert.Single(data.Chains).Name);
        Assert.Equal(4, data.Warnings.Count);
        Assert.All(data.Warnings, w => Assert.StartsWith("chains.json: chains[", w));
    }

    [Fact]
    public void Story_cast_reads_aliases_and_blocks_and_skips_entries_without_a_note()
    {
        var data = Load(CuratedData.StoryCastFileName,
            """
            {
              "$schema_note": "ignored",
              "aliases": {
                "Nero tol Scaeva": { "name": "Nero", "note": "same person" },
                "No note": { "name": "Someone" },
                "Self": { "name": "Self", "note": "onto itself" }
              },
              "blocks": {
                "Ul'dahn guard": { "note": "a role, not a character" },
                "Bare": {}
              }
            }
            """);

        Assert.Equal("Nero", Assert.Single(data.StoryCast.Aliases).Value);
        Assert.Equal(["Ul'dahn guard"], data.StoryCast.Blocks);
        Assert.Equal(3, data.Warnings.Count);
    }

    [Fact]
    public void The_shipped_story_cast_and_chains_load_without_warnings()
    {
        var data = CuratedData.Load(Path.Combine(Data.FixtureCatalog.ShippedDataDir(), "curated"));

        Assert.DoesNotContain(data.Warnings, w => w.StartsWith(CuratedData.StoryCastFileName, StringComparison.Ordinal));
        Assert.DoesNotContain(data.Warnings, w => w.StartsWith(CuratedData.ChainsFileName, StringComparison.Ordinal));
        Assert.Contains(data.Chains, c => c.StartQuest != 0);
        Assert.Contains(data.Chains, c => c.Ongoing);
        Assert.All(data.Chains, c => Assert.False(string.IsNullOrWhiteSpace(c.Note), $"chain '{c.Name}' has no note"));
    }
}
