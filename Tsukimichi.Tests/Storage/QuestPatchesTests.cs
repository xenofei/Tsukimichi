using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Storage;

/// <summary>quest_patches.json (P8): parsing, the canonical text, the DataGen diff and the overlay onto records.</summary>
public class QuestPatchesTests
{
    private static QuestPatches Sample() => new(
        "2026.09.15.0000.0000",
        new Dictionary<uint, string> { [65537] = "2.0", [70979] = "7.5", [70998] = "7.55", [69000] = "" },
        [new QuestPatchesRun("2026.09.15.0000.0000", QuestPatches.SourceGarland, "7.5", 3)]);

    [Fact]
    public void Text_round_trips_with_one_quest_per_line_in_row_order()
    {
        var patches = Sample();

        var json = patches.ToJson();
        var back = QuestPatches.Parse(json);

        Assert.Empty(back.Warnings);
        Assert.Equal("2026.09.15.0000.0000", back.GameVersion);
        Assert.Equal(patches.ByRowId.OrderBy(kv => kv.Key), back.ByRowId.OrderBy(kv => kv.Key));
        Assert.Equal(patches.History, back.History);
        Assert.Equal(json, back.ToJson());
        Assert.Contains("\n    \"65537\": \"2.0\",\n    \"69000\": \"\",\n    \"70979\": \"7.5\",\n", json, StringComparison.Ordinal);
        Assert.EndsWith("}\n", json, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', json);
    }

    [Fact]
    public void Newest_known_and_lookups()
    {
        var patches = Sample();

        Assert.Equal("7.55", patches.Newest);
        Assert.Equal(3, patches.KnownCount);
        Assert.Equal("7.5", patches.For(70979));
        Assert.Equal(string.Empty, patches.For(69000));
        Assert.True(patches.Lists(69000));
        Assert.Equal(string.Empty, patches.For(1));
        Assert.False(patches.Lists(1));
    }

    [Fact]
    public void Bad_entries_are_skipped_or_read_as_unknown_with_a_warning()
    {
        const string json = """
            {
              "gameVersion": "x",
              "patches": { "65537": "2.0", "abc": "2.0", "65538": 2.0, "65539": "soon", "65540": "7.50" }
            }
            """;

        var patches = QuestPatches.Parse(json);

        Assert.Equal(3, patches.Warnings.Count);
        Assert.Equal("2.0", patches.For(65537));
        Assert.False(patches.Lists(65538));
        Assert.True(patches.Lists(65539));
        Assert.Equal(string.Empty, patches.For(65539));
        Assert.Equal("7.5", patches.For(65540));
    }

    [Fact]
    public void A_missing_or_broken_file_reads_as_empty_with_a_warning()
    {
        var missing = QuestPatches.Load(Path.Combine(Path.GetTempPath(), $"tsukimichi-missing-{Guid.NewGuid():N}.json"));
        Assert.Empty(missing.ByRowId);
        Assert.Single(missing.Warnings);

        var broken = QuestPatches.Parse("{ nope");
        Assert.Empty(broken.ByRowId);
        Assert.Single(broken.Warnings);
        Assert.Equal(string.Empty, broken.Newest);
    }

    [Fact]
    public void Diff_with_nothing_new_changes_nothing()
    {
        var patches = Sample();

        var diff = patches.Diff([65537, 70979, 70998, 69000], "2026.09.15.0000.0000", null);

        Assert.Empty(diff.NewRowIds);
        Assert.Same(patches, diff.Updated);
    }

    [Fact]
    public void Diff_stamps_every_unlisted_id_with_the_patch_and_records_the_run()
    {
        var patches = Sample();

        var diff = patches.Diff([65537, 70979, 71100, 71101, 71101], "2026.11.01.0000.0000", "7.6");

        Assert.Equal(new uint[] { 71100, 71101 }, diff.NewRowIds);
        Assert.Equal("7.6", diff.Updated.For(71100));
        Assert.Equal("7.6", diff.Updated.For(71101));
        Assert.Equal("7.6", diff.Updated.Newest);
        Assert.Equal("2026.11.01.0000.0000", diff.Updated.GameVersion);
        Assert.Equal(new QuestPatchesRun("2026.11.01.0000.0000", QuestPatches.SourceDiff, "7.6", 2), diff.Updated.History[^1]);

        // Ids the new catalog no longer has keep their line, and known values never change.
        Assert.Equal("7.55", diff.Updated.For(70998));
        Assert.True(diff.Updated.Lists(69000));
        Assert.Equal(string.Empty, diff.Updated.For(69000));
    }

    [Fact]
    public void Diff_with_new_ids_and_no_patch_refuses()
    {
        var patches = Sample();

        var ex = Assert.Throws<InvalidOperationException>(() => patches.Diff([65537, 71100], "2026.11.01.0000.0000", null));
        Assert.Contains("--patch", ex.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => patches.Diff([71100], "v", "soon"));
    }

    [Fact]
    public void Apply_sets_added_in_and_leaves_unknown_quests_empty()
    {
        var patches = Sample();
        QuestRecord[] records = [new() { RowId = 65537, Name = "A" }, new() { RowId = 69000, Name = "B" }, new() { RowId = 1, Name = "C" }];

        var dated = patches.Apply(records);

        Assert.Equal(["2.0", "", ""], dated.Select(q => q.AddedIn));
        Assert.Same(records[1], dated[1]);
        Assert.Same(records, QuestPatches.Empty.Apply(records));
    }

    [Fact]
    public void The_schema_note_reads_as_sentences()
    {
        // 0.9.0 shipped "detail pane.Every quest ..." in the file's note.
        var note = (string?)System.Text.Json.Nodes.JsonNode.Parse(Sample().ToJson())!["$schema_note"];

        Assert.NotNull(note);
        Assert.Contains("the detail pane. Every quest id", note, StringComparison.Ordinal);
        Assert.Contains("quest-patch-corrections.json", note, StringComparison.Ordinal);
    }

    [Fact]
    public void Corrections_keep_complete_entries_and_skip_the_rest_with_a_warning()
    {
        const string json = """
            {
              "schema": 1,
              "entries": {
                "65557": { "name": "Way of the Archer", "patch": "2.0", "reason": "wiki says 2.0", "evidence": "https://ffxiv.consolegameswiki.com/wiki/Way_of_the_Archer" },
                "65558": { "name": "Way of the Conjurer", "patch": "2.00", "reason": "r", "evidence": "https://example.org/a" },
                "abc": { "patch": "2.0", "reason": "r", "evidence": "https://example.org/b" },
                "65559": { "patch": "soon", "reason": "r", "evidence": "https://example.org/c" },
                "65789": { "patch": "2.0", "evidence": "https://example.org/d" },
                "65846": { "patch": "2.0", "reason": "r", "evidence": "http://example.org/e" }
              }
            }
            """;

        var corrections = QuestPatchCorrections.Parse(json);

        Assert.Equal([65557u, 65558u], corrections.ByRowId.Keys.Order());
        Assert.Equal(new QuestPatchCorrection("Way of the Archer", "2.0", "wiki says 2.0", "https://ffxiv.consolegameswiki.com/wiki/Way_of_the_Archer"), corrections.ByRowId[65557]);
        Assert.Equal("2.0", corrections.ByRowId[65558].Patch);
        Assert.Equal(4, corrections.Warnings.Count);
        Assert.Single(QuestPatchCorrections.Parse("{ \"entries\": 1 }").Warnings);
        Assert.Single(QuestPatchCorrections.Parse("{ \"entries\": {}, }").Warnings);
        Assert.Empty(QuestPatchCorrections.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json")).ByRowId);
    }
}
