using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Storage;

/// <summary>Loading <c>curated/payoff_gates.json</c> (P5): both forms of <c>before</c>, and each required field.</summary>
public sealed class PayoffGatesFileTests : IDisposable
{
    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private CuratedData Load(string json)
    {
        var dir = tmp.File("curated");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, CuratedData.PayoffGatesFileName), json);
        return CuratedData.Load(dir);
    }

    [Fact]
    public void Gates_parse_ids_or_a_chain_name_in_file_order()
    {
        var data = Load(
            """
            {
              "schema": 1,
              "review": "pending",
              "note": "header",
              "entries": {
                "eden": { "milestone": 70286, "before": "Eden", "instruction": "Finish the Eden raid series first.", "why": "spoiler", "evidence": ["https://example.org/a"], "note": "n" },
                "roles": { "milestone": "69543", "before": [68784, "68808", 68784], "instruction": "Do the role quests first.", "why": "w", "evidence": ["https://example.org/b", "https://example.org/c"], "note": "n2" }
              }
            }
            """);

        Assert.Empty(data.Warnings);
        Assert.Equal(["eden", "roles"], data.PayoffGates.Select(g => g.Id));
        var eden = data.PayoffGates[0];
        Assert.Equal(70286u, eden.MilestoneRowId);
        Assert.Equal("Eden", eden.BeforeChain);
        Assert.Empty(eden.BeforeRowIds);
        Assert.Equal("Finish the Eden raid series first.", eden.Instruction);
        Assert.Equal("spoiler", eden.Why);
        Assert.Equal(["https://example.org/a"], eden.Evidence);

        var roles = data.PayoffGates[1];
        Assert.Null(roles.BeforeChain);
        Assert.Equal([68784u, 68808u], roles.BeforeRowIds);
        Assert.Equal(2, roles.Evidence.Count);
    }

    [Theory]
    [InlineData("""{ "before": [1], "instruction": "i", "why": "w", "evidence": ["https://e.org"], "note": "n" }""", "milestone")]
    [InlineData("""{ "milestone": 70286, "instruction": "i", "why": "w", "evidence": ["https://e.org"], "note": "n" }""", "before")]
    [InlineData("""{ "milestone": 70286, "before": [], "instruction": "i", "why": "w", "evidence": ["https://e.org"], "note": "n" }""", "before")]
    [InlineData("""{ "milestone": 70286, "before": [0], "instruction": "i", "why": "w", "evidence": ["https://e.org"], "note": "n" }""", "before id")]
    [InlineData("""{ "milestone": 70286, "before": "Eden", "why": "w", "evidence": ["https://e.org"], "note": "n" }""", "instruction")]
    [InlineData("""{ "milestone": 70286, "before": "Eden", "instruction": "i", "evidence": ["https://e.org"], "note": "n" }""", "instruction")]
    [InlineData("""{ "milestone": 70286, "before": "Eden", "instruction": "i", "why": "w", "evidence": ["https://e.org"] }""", "instruction")]
    [InlineData("""{ "milestone": 70286, "before": "Eden", "instruction": "i", "why": "w", "note": "n" }""", "evidence")]
    [InlineData("""{ "milestone": 70286, "before": "Eden", "instruction": "i", "why": "w", "evidence": ["http://e.org"], "note": "n" }""", "evidence")]
    public void An_entry_missing_a_required_field_is_skipped_with_a_warning(string entry, string reason)
    {
        var data = Load($$"""{ "schema": 1, "entries": { "bad": {{entry}}, "good": { "milestone": 70286, "before": "Eden", "instruction": "i", "why": "w", "evidence": ["https://e.org"], "note": "n" } } }""");

        Assert.Equal(["good"], data.PayoffGates.Select(g => g.Id));
        var warning = Assert.Single(data.Warnings);
        Assert.Contains("\"bad\"", warning, StringComparison.Ordinal);
        Assert.Contains(reason, warning, StringComparison.Ordinal);
    }

    [Fact]
    public void A_file_with_a_trailing_comma_is_rejected_whole()
    {
        var data = Load("""{ "entries": { "eden": { "milestone": 70286, "before": "Eden", "instruction": "i", "why": "w", "evidence": ["https://e.org"], "note": "n" }, } }""");

        Assert.Empty(data.PayoffGates);
        Assert.Single(data.Warnings);
    }

    [Fact]
    public void Without_the_file_there_are_no_gates()
    {
        Assert.Empty(CuratedData.Load(tmp.File("curated")).PayoffGates);
        Assert.Empty(CuratedData.Empty.PayoffGates);
    }
}
