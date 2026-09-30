using System.Globalization;
using System.Text.Json;
using Tsukimichi.Core.Export;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.Tests.Data;
using Tsukimichi.Tests.Storage;

namespace Tsukimichi.Tests.Export;

/// <summary>
/// P12 exports over the frozen catalog and the anonymised character in <c>Fixtures/snapshot-v1.json</c>, given a
/// distinctive content id, world and name so a leak of any of them would be found in the text.
/// </summary>
public sealed class ExportWriterTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>, IDisposable
{
    private const ulong ContentId = 0x0040_0000_0DEA_DBEEUL;
    private const uint World = 4217;
    private const string WorldName = "Kujata";
    private const string CharacterName = "Zyxwa Qorvel";
    private static readonly DateTime Exported = new(2026, 9, 29, 18, 30, 0, DateTimeKind.Utc);

    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private static CharacterSnapshot LoadSnapshot()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(Path.Combine(dir.Path, "characters"));
        File.Copy(Path.Combine(FixtureCatalog.FixturesDir, "snapshot-v1.json"), Path.Combine(dir.Path, "characters", "1.json"));
        var snapshot = new JsonSnapshotStore(dir.Path).Load(1);
        Assert.NotNull(snapshot);
        return snapshot with { ContentId = ContentId, World = World, Name = CharacterName };
    }

    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    private string Expansion(byte id) => fixture.Bundle.Names.Expansion(id);

    private IReadOnlyList<UniqueRewardRow> MoonlitView(CharacterSnapshot snapshot)
    {
        var shipped = UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        var rewards = UniqueRewardCatalog.Build(shipped, new Dictionary<uint, UniqueOverride>(), fixture.Curated);
        // What RewardUnlockReader answers for a stored character: quest-following kinds from the completion bit,
        // flag-backed kinds unknown.
        return rewards.View(null, e => e.Kind is RewardKind.Action or RewardKind.Trait or RewardKind.SystemUnlock
            ? snapshot.IsCompleted(QuestRecord.ToQuestId(e.QuestRowId))
            : null);
    }

    private IEnumerable<(string Name, string Text)> EveryOutput(CharacterSnapshot snapshot, bool includeName)
    {
        var header = ExportWriter.Header("0.7.0.0", "2026.09.15.0000.0000", Exported, snapshot, includeName);
        var quests = ExportWriter.QuestRows(Catalog, snapshot, Expansion);
        var all = ExportWriter.QuestRows(Catalog, snapshot, Expansion, includeIncomplete: true);
        var moonlit = ExportWriter.MoonlitRows(MoonlitView(snapshot));
        yield return ("quests.json", ExportWriter.QuestsJson(header, quests));
        yield return ("all-quests.json", ExportWriter.QuestsJson(header, all));
        yield return ("quests.csv", ExportWriter.QuestsCsv(quests));
        yield return ("moonlit.json", ExportWriter.MoonlitJson(header, moonlit));
        yield return ("moonlit.csv", ExportWriter.MoonlitCsv(moonlit));
        foreach (var kind in Enum.GetValues<ExportKind>())
        {
            foreach (var format in Enum.GetValues<ExportFormat>())
            {
                yield return ("file name", ExportWriter.FileName(kind, format, header));
            }
        }
    }

    [Fact]
    public void Quest_export_counts_equal_the_snapshots_completed_quests()
    {
        var snapshot = LoadSnapshot();
        var expected = Catalog.All.Count(q => snapshot.IsCompleted(q.QuestId));
        Assert.True(expected > 100, $"the fixture character has completed {expected} catalog quests");

        var rows = ExportWriter.QuestRows(Catalog, snapshot, Expansion);
        Assert.Equal(expected, rows.Count);
        Assert.All(rows, r => Assert.True(r.Completed));
        Assert.Equal(rows.Count, rows.Select(r => r.RowId).Distinct().Count());

        var header = ExportWriter.Header("0.7.0.0", "2026.09.15.0000.0000", Exported, snapshot, includeCharacterName: false);
        using var json = JsonDocument.Parse(ExportWriter.QuestsJson(header, rows));
        var root = json.RootElement;
        Assert.Equal(ExportWriter.FormatName, root.GetProperty("format").GetString());
        Assert.Equal("quests", root.GetProperty("kind").GetString());
        Assert.Equal("0.7.0.0", root.GetProperty("pluginVersion").GetString());
        Assert.Equal("2026.09.15.0000.0000", root.GetProperty("gameVersion").GetString());
        Assert.Equal("2026-09-29T18:30:00Z", root.GetProperty("exportedUtc").GetString());
        Assert.Equal(expected, root.GetProperty("count").GetInt32());
        Assert.Equal(expected, root.GetProperty("completedCount").GetInt32());
        Assert.Equal(expected, root.GetProperty("quests").GetArrayLength());

        var first = root.GetProperty("quests")[0];
        var quest = Catalog.GetByRowId(first.GetProperty("rowId").GetUInt32())!;
        Assert.Equal(quest.QuestId, first.GetProperty("questId").GetUInt16());
        Assert.Equal(quest.Name, first.GetProperty("name").GetString());
        Assert.Equal(quest.Journal.SectionName, first.GetProperty("section").GetString());
        Assert.Equal(quest.Journal.CategoryName, first.GetProperty("category").GetString());
        Assert.Equal(quest.Journal.GenreName, first.GetProperty("genre").GetString());
        Assert.Equal(Expansion(quest.Expansion), first.GetProperty("expansion").GetString());
        Assert.True(first.GetProperty("completed").GetBoolean());

        // CSV: byte order mark, one header row, one row per quest.
        var csv = ExportWriter.QuestsCsv(rows);
        Assert.StartsWith("﻿rowId,questId,name,section,category,genre,expansion,completed\r\n", csv, StringComparison.Ordinal);
        Assert.Equal(expected + 1, csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void Including_incomplete_quests_lists_the_whole_catalog_with_the_same_completed_count()
    {
        var snapshot = LoadSnapshot();
        var rows = ExportWriter.QuestRows(Catalog, snapshot, Expansion, includeIncomplete: true);

        Assert.Equal(Catalog.Count, rows.Count);
        Assert.Equal(Catalog.All.Count(q => snapshot.IsCompleted(q.QuestId)), rows.Count(r => r.Completed));
    }

    [Fact]
    public void Moonlit_export_has_one_row_per_reward_with_true_false_or_unknown()
    {
        var snapshot = LoadSnapshot();
        var view = MoonlitView(snapshot);
        var rows = ExportWriter.MoonlitRows(view);
        Assert.Equal(view.Count, rows.Count);
        Assert.Contains(rows, r => r.Obtained is null);
        Assert.Contains(rows, r => r.Obtained == true);

        var header = ExportWriter.Header("0.7.0.0", "g", Exported, snapshot, includeCharacterName: false);
        using var json = JsonDocument.Parse(ExportWriter.MoonlitJson(header, rows));
        var root = json.RootElement;
        Assert.Equal("moonlit", root.GetProperty("kind").GetString());
        Assert.Equal(rows.Count, root.GetProperty("rewards").GetArrayLength());
        Assert.Equal(rows.Count(r => r.Obtained == true), root.GetProperty("obtainedCount").GetInt32());
        Assert.Equal(rows.Count(r => r.Obtained is null), root.GetProperty("unknownCount").GetInt32());
        var unknown = root.GetProperty("rewards").EnumerateArray().First(r => r.GetProperty("obtained").ValueKind == JsonValueKind.Null);
        Assert.False(string.IsNullOrEmpty(unknown.GetProperty("kind").GetString()));

        var csv = ExportWriter.MoonlitCsv(rows);
        Assert.StartsWith("﻿kind,rewardId,rewardName,questRowId,obtained\r\n", csv, StringComparison.Ordinal);
        Assert.Contains("," + ExportWriter.Unknown + "\r\n", csv, StringComparison.Ordinal);
        Assert.Contains(",true\r\n", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void No_output_carries_the_content_id_the_account_or_the_world()
    {
        var snapshot = LoadSnapshot();
        var forbidden = new[]
        {
            ContentId.ToString(CultureInfo.InvariantCulture),
            ContentId.ToString("X", CultureInfo.InvariantCulture),
            ContentId.ToString("x", CultureInfo.InvariantCulture),
            WorldName,
        };

        foreach (var includeName in new[] { false, true })
        {
            foreach (var (name, text) in EveryOutput(snapshot, includeName))
            {
                foreach (var value in forbidden)
                {
                    Assert.False(text.Contains(value, StringComparison.OrdinalIgnoreCase), $"{name} carries {value}");
                }

                Assert.DoesNotContain("contentId", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("\"account", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("\"world\"", text, StringComparison.OrdinalIgnoreCase);
            }
        }

        // The world id is a short number that also appears as quest ids, so it is checked against the header fields.
        var header = ExportWriter.Header("0.7.0.0", "g", Exported, snapshot, includeCharacterName: true);
        using var json = JsonDocument.Parse(ExportWriter.QuestsJson(header, []));
        var names = json.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(["format", "formatVersion", "kind", "pluginVersion", "gameVersion", "exportedUtc", "character", "count", "completedCount", "quests"], names);
        Assert.DoesNotContain(json.RootElement.EnumerateObject(), p => p.Value.ValueKind == JsonValueKind.Number && p.Value.GetRawText() == World.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void The_character_name_appears_only_with_the_option()
    {
        var snapshot = LoadSnapshot();
        var nameInFile = ExportWriter.SafeFileName(CharacterName);

        foreach (var (name, text) in EveryOutput(snapshot, includeName: false))
        {
            Assert.False(text.Contains(CharacterName, StringComparison.Ordinal), $"{name} carries the name without the option");
            Assert.False(text.Contains(nameInFile, StringComparison.Ordinal), $"{name} carries the name without the option");
            Assert.False(text.Contains("\"character\"", StringComparison.Ordinal), $"{name} has a character field without the option");
        }

        var withName = EveryOutput(snapshot, includeName: true).ToList();
        Assert.Contains(withName, o => o.Name == "quests.json" && o.Text.Contains("\"character\": \"" + CharacterName + "\"", StringComparison.Ordinal));
        Assert.Contains(withName, o => o.Name == "moonlit.json" && o.Text.Contains(CharacterName, StringComparison.Ordinal));
        Assert.All(withName.Where(o => o.Name == "file name"), o => Assert.Contains(nameInFile, o.Text, StringComparison.Ordinal));

        // The CSV rows never carry it; the file name does when opted in.
        Assert.All(withName.Where(o => o.Name.EndsWith(".csv", StringComparison.Ordinal)), o => Assert.DoesNotContain(CharacterName, o.Text, StringComparison.Ordinal));
    }

    [Fact]
    public void Csv_fields_with_commas_or_quotes_are_quoted_and_names_stay_unescaped_in_json()
    {
        var rows = new List<QuestExportRow>
        {
            new(65536, 0, "Ul'dah, \"Steps\" of Thal", "Main Scenario", "Seventh Umbral Era", "Seventh Umbral Era", "A Realm Reborn", true),
        };

        var csv = ExportWriter.QuestsCsv(rows);
        Assert.Contains("65536,0,\"Ul'dah, \"\"Steps\"\" of Thal\",Main Scenario,", csv, StringComparison.Ordinal);

        var json = ExportWriter.QuestsJson(new ExportHeader("v", "g", Exported), rows);
        Assert.Contains("Ul'dah, \\\"Steps\\\" of Thal", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_puts_the_file_in_the_folder_and_names_it_by_kind_format_and_time()
    {
        var header = ExportWriter.Header("v", "g", Exported, null, includeCharacterName: true);
        var fileName = ExportWriter.FileName(ExportKind.Moonlit, ExportFormat.Csv, header);
        Assert.Matches(@"^tsukimichi-moonlit-\d{8}-\d{6}\.csv$", fileName);
        Assert.Matches(@"^tsukimichi-quests-\d{8}-\d{6}\.json$", ExportWriter.FileName(ExportKind.Quests, ExportFormat.Json, header));

        var dir = tmp.File("exports");
        var path = ExportWriter.Write(dir, fileName, "a,b\r\n");

        Assert.Equal(Path.Combine(Path.GetFullPath(dir), fileName), path);
        Assert.Equal("a,b\r\n", File.ReadAllText(path));
    }

    [Theory]
    [InlineData("", @"C:\cfg\exports")]
    [InlineData("   ", @"C:\cfg\exports")]
    [InlineData(@"E:\My exports", @"E:\My exports")]
    [InlineData(@"\\server\share\x", @"\\server\share\x")]
    [InlineData("mine", @"C:\cfg\mine")]
    [InlineData(@"\saved", @"C:\cfg\saved")]
    [InlineData(@"D:saved", @"C:\cfg\saved")]
    [InlineData(@"D:", @"C:\cfg\exports")]
    public void Folder_resolves_rooted_but_relative_paths_under_the_config_directory(string configured, string expected)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.Equal(expected, ExportWriter.ResolveFolder(configured, @"C:\cfg", @"C:\cfg\exports"));
    }
}
