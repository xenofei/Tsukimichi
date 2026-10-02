using System.Text.Json;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Export;
using Tsukimichi.Core.Links;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Unique;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Export;

/// <summary>
/// Copy table as TSV (1.8.0, <see cref="TableTsv"/>) and the export's added fields (state, displayLevel, patch, isMsq,
/// repeatable, lodestoneId; itemId and collectId), which stay additive: formatVersion is still 1 and every older
/// column keeps its place.
/// </summary>
public class TableTsvTests
{
    private static readonly DateTime Exported = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static readonly ExternalIds Ids = new(
        new Dictionary<uint, ExternalQuestIds> { [65621] = new("abc123def45", "Close to Home") },
        new Dictionary<(RewardKind Kind, uint RewardId), uint> { [(RewardKind.Mount, 15)] = 15 });

    private static QuestRecord Msq => Quest(65621, "Close to Home") with
    {
        Level = 1,
        LevelOffset = 0,
        AddedIn = "2.0",
        Journal = new JournalRef(1, "Main Scenario (A Realm Reborn through Endwalker)", 1, "Seventh Umbral Era Main Scenario Quests", 1, "Seventh Umbral Era", 1),
    };

    private static QuestRecord Daily => Quest(65800, "Tab\tand \"quote\"\nline") with
    {
        Level = 40,
        LevelOffset = 2,
        IsRepeatable = true,
        RepeatInterval = 1,
        Journal = new JournalRef(3, "Sidequests", 2, "Category", 2, "Genre", 2),
    };

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("plain, with comma", "plain, with comma")]
    [InlineData("tab\there", "\"tab\there\"")]
    [InlineData("two\nlines", "\"two\nlines\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("cr\rhere", "\"cr\rhere\"")]
    public void A_cell_is_quoted_only_when_it_holds_a_tab_a_break_or_a_quote(string? value, string expected) =>
        Assert.Equal(expected, TableTsv.Field(value));

    [Fact]
    public void The_quest_table_has_the_csv_columns_then_url_one_row_per_quest()
    {
        var rows = new List<QuestExportRow>
        {
            ExportWriter.Row(Msq, null, "Close to Home", _ => "A Realm Reborn", new QuestEvaluation(QuestState.Completed, [], null, null, null), Ids),
            ExportWriter.Row(Daily, null, Daily.Name, null, new QuestEvaluation(QuestState.Ready, [], null, null, null), Ids),
        };

        var tsv = TableTsv.Quests(rows, r => "https://example.org/" + r.RowId);
        var lines = tsv.Split("\r\n");

        Assert.Equal(string.Join('\t', ExportWriter.QuestColumns) + "\turl", lines[0]);
        Assert.Equal(
            "65621\t85\tClose to Home\tMain Scenario (A Realm Reborn through Endwalker)\tSeventh Umbral Era Main Scenario Quests\tSeventh Umbral Era\tA Realm Reborn\ttrue\t\t\tCompleted\t1\t2.0\ttrue\tfalse\tabc123def45\thttps://example.org/65621",
            lines[0 + 1]);

        // The daily's name holds a tab, a quote and a line break: one quoted cell, so the row stays one row.
        Assert.StartsWith("65800\t264\t\"Tab\tand \"\"quote\"\"\nline\"\t", tsv.Split("\r\n")[2], StringComparison.Ordinal);
        Assert.Contains("\tReady\t42\t\tfalse\ttrue\t\thttps://example.org/65800", tsv, StringComparison.Ordinal);
        Assert.EndsWith("\r\n", tsv, StringComparison.Ordinal);
    }

    [Fact]
    public void A_masked_quest_carries_no_link_as_in_copy_for_discord()
    {
        var rows = new List<QuestExportRow>
        {
            ExportWriter.Row(Msq, null, "???", _ => "A Realm Reborn", new QuestEvaluation(QuestState.Ready, [], null, null, null), Ids),
        };

        var shown = TableTsv.Quests(rows, r => "https://example.org/" + r.RowId).Split("\r\n")[1];
        var hidden = TableTsv.Quests(rows, r => "https://example.org/" + r.RowId, _ => true).Split("\r\n")[1];

        Assert.EndsWith("\tabc123def45\thttps://example.org/65621", shown, StringComparison.Ordinal);

        // Same columns, but lodestoneId and url are blank: either page would name the quest the shield hides.
        Assert.Equal(shown.Split('\t').Length, hidden.Split('\t').Length);
        Assert.EndsWith("\ttrue\tfalse\t\t", hidden, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123def45", hidden, StringComparison.Ordinal);
        Assert.Equal("abc123def45", rows[0].LodestoneId);
    }

    [Fact]
    public void The_moonlit_view_has_the_csv_columns_then_url()
    {
        var mount = new UniqueRewardEntry(65730, RewardKind.Mount, 15, 4552, "unicorn", Confidence.Static, "sheet");
        var action = new UniqueRewardEntry(65731, RewardKind.Action, 7, 0, "Sprint", Confidence.Static, "sheet");
        var rows = new List<MoonlitExportRow>
        {
            ExportWriter.Row(mount, true, RewardAvailability.GetNow, Ids),
            ExportWriter.Row(action, null, null, Ids),
        };

        var lines = TableTsv.Moonlit(rows, r => ExternalLinks.Reward(r.Kind, r.RewardId, r.ItemId, r.QuestRowId, r.RewardName, Ids)).Split("\r\n");

        Assert.Equal("kind\trewardId\trewardName\tquestRowId\tobtained\tavailability\titemId\tcollectId\turl", lines[0]);
        Assert.Equal("Mount\t15\tunicorn\t65730\ttrue\tgetNow\t4552\t15\thttps://ffxivcollect.com/mounts/15", lines[1]);
        Assert.Equal("Action\t7\tSprint\t65731\tunknown\t\t\t\thttps://www.garlandtools.org/db/#quest/65731", lines[2]);
    }

    [Fact]
    public void The_added_quest_fields_are_written_in_json_and_csv_and_left_out_when_unknown()
    {
        var known = ExportWriter.Row(Msq, null, Msq.Name, null, new QuestEvaluation(QuestState.Completed, [], null, null, null), Ids);
        var unknown = ExportWriter.Row(Daily with { AddedIn = string.Empty }, null, "Daily", null, null, null);
        Assert.Equal("Completed", known.State);
        Assert.Equal(1, known.DisplayLevel);
        Assert.Equal("2.0", known.Patch);
        Assert.True(known.IsMsq);
        Assert.False(known.Repeatable);
        Assert.Equal("abc123def45", known.LodestoneId);
        Assert.Equal(42, unknown.DisplayLevel);
        Assert.True(unknown.Repeatable);
        Assert.False(unknown.Completed);

        using var json = JsonDocument.Parse(ExportWriter.QuestsJson(new ExportHeader("1.8.0.0", "g", Exported), [known, unknown]));
        Assert.Equal(1, json.RootElement.GetProperty("formatVersion").GetInt32());
        var first = json.RootElement.GetProperty("quests")[0];
        Assert.Equal("Completed", first.GetProperty("state").GetString());
        Assert.Equal(1, first.GetProperty("displayLevel").GetInt32());
        Assert.Equal("2.0", first.GetProperty("patch").GetString());
        Assert.True(first.GetProperty("isMsq").GetBoolean());
        Assert.False(first.GetProperty("repeatable").GetBoolean());
        Assert.Equal("abc123def45", first.GetProperty("lodestoneId").GetString());

        var second = json.RootElement.GetProperty("quests")[1];
        Assert.False(second.TryGetProperty("state", out _));
        Assert.False(second.TryGetProperty("patch", out _));
        Assert.False(second.TryGetProperty("lodestoneId", out _));
        Assert.Equal(42, second.GetProperty("displayLevel").GetInt32());
        Assert.True(second.GetProperty("repeatable").GetBoolean());

        var csv = ExportWriter.QuestsCsv([known]).Split("\r\n");
        Assert.EndsWith(",true,,,Completed,1,2.0,true,false,abc123def45", csv[1], StringComparison.Ordinal);
    }

    [Fact]
    public void The_added_moonlit_fields_carry_the_item_and_the_collect_id_when_known()
    {
        var mount = new UniqueRewardEntry(65730, RewardKind.Mount, 15, 4552, "unicorn", Confidence.Static, "sheet");
        var title = new UniqueRewardEntry(65731, RewardKind.Title, 48, 0, "Seeker of Bounty", Confidence.Static, "sheet");
        var rows = ExportWriter.MoonlitRows([new UniqueRewardRow(mount, true), new UniqueRewardRow(title, null)], null, Ids);

        Assert.Equal(4552u, rows[0].ItemId);
        Assert.Equal(15u, rows[0].CollectId);
        Assert.Equal(0u, rows[1].ItemId);
        Assert.Null(rows[1].CollectId);

        using var json = JsonDocument.Parse(ExportWriter.MoonlitJson(new ExportHeader("1.8.0.0", "g", Exported), rows));
        var rewards = json.RootElement.GetProperty("rewards");
        Assert.Equal(4552u, rewards[0].GetProperty("itemId").GetUInt32());
        Assert.Equal(15u, rewards[0].GetProperty("collectId").GetUInt32());
        Assert.False(rewards[1].TryGetProperty("itemId", out _));
        Assert.False(rewards[1].TryGetProperty("collectId", out _));

        var csv = ExportWriter.MoonlitCsv(rows).Split("\r\n");
        Assert.Equal("Mount,15,unicorn,65730,true,,4552,15", csv[1]);
        Assert.Equal("Title,48,Seeker of Bounty,65731,unknown,,,", csv[2]);
    }

    [Fact]
    public void The_published_schema_names_every_field_the_writer_emits()
    {
        var schema = File.ReadAllText(Path.Combine(Localization.ResxFiles.RepositoryRoot(), "docs", "export-format.schema.json"));
        using var doc = JsonDocument.Parse(schema);
        foreach (var column in ExportWriter.QuestColumns.Concat(ExportWriter.MoonlitColumns))
        {
            Assert.Contains("\"" + column + "\"", schema, StringComparison.Ordinal);
        }

        Assert.Contains("\"formatVersion\"", schema, StringComparison.Ordinal);
    }
}
