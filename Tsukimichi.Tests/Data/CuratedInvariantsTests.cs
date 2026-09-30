using System.Text.Json;
using System.Text.Json.Nodes;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// Invariants over the shipped curated files (<c>Tsukimichi/Data/curated</c>) against the frozen catalog fixture and
/// the shipped <c>unique_quests.json</c>. They run everywhere (no game files needed) and carry the Curated trait so CI
/// runs them as their own step.
/// </summary>
[Trait("Category", "Curated")]
public sealed class CuratedInvariantsTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private static string CuratedDir => Path.Combine(FixtureCatalog.ShippedDataDir(), "curated");

    private static CuratedData Curated() => CuratedData.Load(CuratedDir);

    private static UniqueRewardsData Unique() => UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));

    [Fact]
    public void Feature_quests_json_equals_the_set_DataGen_derives()
    {
        // feature_quests.json is written by DataGen from FeaturePresets.Derive over the catalog, the other curated
        // files and the unique-reward entries; a hand edit, or a regen against a different game version than the
        // fixture, shows up here as a set difference.
        var curated = Curated();
        var unique = Unique();
        Assert.NotEmpty(unique.Entries);
        Assert.Equal(fixture.GameVersion, unique.GameVersion);

        var derived = FeaturePresets.Derive(fixture.Bundle.Catalog, curated.WithoutFeatureQuests(), unique.Entries);
        var shipped = curated.FeatureQuests;

        var missing = derived.Where(id => !shipped.Contains(id)).OrderBy(id => id).ToList();
        var extra = shipped.Where(id => !derived.Contains(id)).OrderBy(id => id).ToList();
        Assert.True(missing.Count == 0 && extra.Count == 0,
            $"feature_quests.json differs from the derived set: missing {missing.Count} [{string.Join(", ", missing.Take(10))}], extra {extra.Count} [{string.Join(", ", extra.Take(10))}]; regenerate with tools/regen.ps1");

        // The file itself: an object with a note and questRowIds sorted ascending without duplicates.
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(CuratedDir, CuratedData.FeatureQuestsFileName)), documentOptions: CuratedData.StrictOptions)!.AsObject();
        var ids = root["questRowIds"]!.AsArray().Select(n => n!.GetValue<uint>()).ToList();
        Assert.Equal(ids.OrderBy(id => id).Distinct(), ids);
        Assert.Equal(derived.Count, ids.Count);
        Assert.Contains("DataGen", (string?)root["note"]);
    }

    [Fact]
    public void Every_curated_file_parses_as_strict_json_and_loads_without_warnings()
    {
        var files = Directory.GetFiles(CuratedDir, "*.json");
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            // The README's rule: no comments, no trailing commas. Both loaders now reject them; this pins the files.
            using var doc = JsonDocument.Parse(File.ReadAllText(file), CuratedData.StrictOptions);
            Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        }

        var known = new[]
        {
            CuratedData.SystemUnlocksFileName, CuratedData.DutyUnlocksFileName, CuratedData.FeatureQuestsFileName,
            CuratedData.FestivalsFileName, CuratedData.ChainsFileName, CuratedData.OnlineStoreFileName,
            CuratedData.RefileOverridesFileName, CuratedData.RetiredQuestsFileName, CuratedData.QuirksFileName, CuratedData.VersionFileName,
        };
        Assert.Equal(known.OrderBy(n => n, StringComparer.Ordinal), files.Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Empty(Curated().Warnings);
    }

    [Fact]
    public void Every_curated_quest_id_is_a_named_quest_in_the_catalog()
    {
        var curated = Curated();
        var catalog = fixture.Bundle.Catalog;
        var ids = curated.SystemUnlocks.Keys.Select(id => (File: CuratedData.SystemUnlocksFileName, Id: id))
            .Concat(curated.DutyUnlocks.Keys.Select(id => (File: CuratedData.DutyUnlocksFileName, Id: id)))
            .Concat(curated.FeatureQuests.Select(id => (File: CuratedData.FeatureQuestsFileName, Id: id)))
            .ToList();
        Assert.NotEmpty(ids);

        var unknown = ids.Where(x => catalog.GetByRowId(x.Id) is null).ToList();
        Assert.True(unknown.Count == 0, "curated quest ids missing from the catalog: " + string.Join(", ", unknown.Select(x => $"{x.File}:{x.Id}")));
        Assert.All(ids, x => Assert.True(x.Id >= 65536, $"{x.File}:{x.Id} is not a Quest sheet row id"));
    }

    [Fact]
    public void System_and_duty_unlock_entries_each_carry_a_note_and_duty_unlocks_have_content_ids()
    {
        var curated = Curated();
        Assert.All(curated.SystemUnlocks, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value.Note), $"system_unlocks {kv.Key} has no note"));
        Assert.All(curated.SystemUnlocks, kv => Assert.Equal("system", kv.Value.Kind));
        Assert.All(curated.DutyUnlocks, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value.Note), $"duty_unlocks {kv.Key} has no note"));
        Assert.All(curated.DutyUnlocks, kv => Assert.NotEmpty(kv.Value.ContentFinderConditionIds));
        Assert.All(curated.DutyUnlocks, kv => Assert.All(kv.Value.ContentFinderConditionIds, cfc => Assert.NotEqual(0u, cfc)));
    }

    [Fact]
    public void Online_store_entries_carry_evidence_and_each_matches_an_entry_marked_OnlineStore()
    {
        var curated = Curated();
        var unique = Unique();
        Assert.Equal(69, curated.OnlineStore.Count);

        // Raw file: every entry has name, kind, rewardId, evidence (an https URL) and a note; keys ascend.
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(CuratedDir, CuratedData.OnlineStoreFileName)), documentOptions: CuratedData.StrictOptions)!.AsObject();
        Assert.Equal(1, (int)root["schema"]!);
        Assert.False(string.IsNullOrWhiteSpace((string?)root["note"]));
        var entries = root["entries"]!.AsObject();
        var keys = entries.Select(kv => uint.Parse(kv.Key)).ToList();
        Assert.Equal(keys.OrderBy(k => k), keys);
        foreach (var (key, node) in entries)
        {
            var obj = node!.AsObject();
            foreach (var field in new[] { "name", "kind", "rewardId", "evidence", "note" })
            {
                Assert.True(obj.ContainsKey(field), $"online_store {key} lacks {field}");
            }

            Assert.StartsWith("https://", (string?)obj["evidence"], StringComparison.Ordinal);
        }

        // Every store item resolves to at least one shipped entry (by its item id, or by the collectible it unlocks)
        // and every such entry carries OnlineStore; otherwise a regen silently orphaned the curated entry.
        var byItem = unique.Entries.Where(e => e.ItemId != 0).ToLookup(e => e.ItemId);
        var byReward = unique.Entries.ToLookup(e => (e.Kind, e.RewardId));
        var orphans = new List<string>();
        var unmarked = new List<string>();
        foreach (var (itemId, store) in curated.OnlineStore)
        {
            var matched = byItem[itemId].Concat(byReward[(store.Kind, store.RewardId)]).Distinct().ToList();
            if (matched.Count == 0)
            {
                orphans.Add($"{itemId} {store.Name}");
            }

            unmarked.AddRange(matched.Where(e => !e.SoldOnOnlineStore).Select(e => $"{e.QuestRowId} {e.Kind} {e.RewardId}"));
        }

        Assert.True(orphans.Count == 0, "online_store items matching no unique_quests.json entry: " + string.Join(", ", orphans));
        Assert.True(unmarked.Count == 0, "entries the store sells but not marked OnlineStore (stale regen?): " + string.Join(", ", unmarked));

        var marked = unique.Entries.Where(e => e.SoldOnOnlineStore).ToList();
        Assert.Equal(69, marked.Count);
        Assert.All(marked, e => Assert.True(curated.OnlineStore.ContainsKey(e.ItemId) || curated.OnlineStore.Values.Any(s => s.Kind == e.Kind && s.RewardId == e.RewardId),
            $"{e.QuestRowId} {e.Kind} {e.RewardId} is marked OnlineStore but no curated store item explains it"));
        Assert.Equal(
            new Dictionary<RewardKind, int> { [RewardKind.Minion] = 25, [RewardKind.Emote] = 21, [RewardKind.Mount] = 11, [RewardKind.Barding] = 5, [RewardKind.Orchestrion] = 4, [RewardKind.Ornament] = 2, [RewardKind.Hairstyle] = 1 },
            marked.GroupBy(e => e.Kind).ToDictionary(g => g.Key, g => g.Count()));
    }

    [Fact]
    public void Refiling_files_name_fixture_rows_with_evidence_and_override_genres_a_listed_quest_holds()
    {
        var curated = Curated();
        // The sheet's own filing: the refiler builds its genre templates from these listed rows, and an id must exist
        // whatever the refiler does with it.
        var sheet = fixture.LegacyBundle.Catalog;
        Assert.NotEmpty(curated.RefileOverrides);
        Assert.NotEmpty(curated.RetiredQuests);

        foreach (var (rowId, entry) in curated.RefileOverrides)
        {
            Assert.True(sheet.GetByRowId(rowId) is not null, $"refile_overrides {rowId} is not a row of the catalog fixture");
            Assert.StartsWith("https://", entry.Evidence, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(entry.Note), $"refile_overrides {rowId} has no note");

            // JournalRefiler.Index.Assign needs a listed quest holding the genre for the section, category and names;
            // without one the quest stays unlisted, and the main scenario sections are never a refiling target.
            var holders = sheet.All.Where(q => !q.IsUnlisted && q.Journal.GenreId == entry.GenreId).ToList();
            Assert.True(holders.Count > 0, $"refile_overrides {rowId} names genre {entry.GenreId}, which no listed quest holds");
            Assert.All(holders, q => Assert.True(q.Journal.SectionId is not (0 or 1), $"refile_overrides {rowId} names genre {entry.GenreId}, a main scenario genre"));
        }

        foreach (var (rowId, entry) in curated.RetiredQuests)
        {
            Assert.True(sheet.GetByRowId(rowId) is not null, $"retired_quests {rowId} is not a row of the catalog fixture");
            Assert.StartsWith("https://", entry.Evidence, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(entry.Note), $"retired_quests {rowId} has no note");
        }

        Assert.Empty(curated.RefileOverrides.Keys.Intersect(curated.RetiredQuests.Keys));

        // Raw files: keys ascend, and the loader dropped nothing (every entry made it into the maps).
        foreach (var (file, count) in new[] { (CuratedData.RefileOverridesFileName, curated.RefileOverrides.Count), (CuratedData.RetiredQuestsFileName, curated.RetiredQuests.Count) })
        {
            var root = JsonNode.Parse(File.ReadAllText(Path.Combine(CuratedDir, file)), documentOptions: CuratedData.StrictOptions)!.AsObject();
            Assert.Equal(1, (int)root["schema"]!);
            Assert.False(string.IsNullOrWhiteSpace((string?)root["note"]));
            var keys = root["entries"]!.AsObject().Select(kv => uint.Parse(kv.Key, System.Globalization.CultureInfo.InvariantCulture)).ToList();
            Assert.Equal(keys.OrderBy(k => k), keys);
            Assert.Equal(count, keys.Count);
        }
    }

    [Fact]
    public void Quirks_name_live_fixture_quests_with_a_note_and_https_evidence()
    {
        var curated = Curated();
        var catalog = fixture.Bundle.Catalog;
        Assert.NotEmpty(curated.Quirks);

        foreach (var (rowId, quirk) in curated.Quirks)
        {
            var quest = catalog.GetByRowId(rowId);
            Assert.True(quest is not null, $"quirks {rowId} is not a row of the catalog fixture");
            Assert.False(quest!.IsRemoved, $"quirks {rowId} {quest.Name} is a removed quest; a note on it is never shown");
            Assert.False(string.IsNullOrWhiteSpace(quirk.Note), $"quirks {rowId} has no note");
            Assert.StartsWith("https://", quirk.Evidence, StringComparison.Ordinal);
        }

        // The three quirks the research threads name (docs/research/player-gripes-2026.md section 3 P2).
        var upInArms = Assert.Single(catalog.All, q => q.Name == "Up in Arms");
        Assert.Contains("Zenith", curated.Quirks[upInArms.RowId].Note);

        var nestOfHonor = Assert.Single(catalog.All, q => q.Name == "The Nest of Honor");
        Assert.Equal(7, nestOfHonor.BeastRank);
        Assert.Contains("Bloodsworn", curated.Quirks[nestOfHonor.RowId].Note);
        Assert.Contains("Allied", curated.Quirks[nestOfHonor.RowId].Note);

        var inscrutableTastes = Assert.Single(catalog.All, q => q.Name == "Inscrutable Tastes");
        var repointed = curated.Quirks.Where(kv => kv.Value.Note.Contains("Inscrutable Tastes", StringComparison.Ordinal)).ToList();
        Assert.Equal(11, repointed.Count);
        foreach (var (rowId, quirk) in repointed)
        {
            Assert.Contains(inscrutableTastes.RowId, catalog.ByRowId[rowId].PreviousQuests.QuestIds);
            Assert.StartsWith("https://na.finalfantasyxiv.com/lodestone/topics/detail/", quirk.Evidence, StringComparison.Ordinal);
        }

        Assert.Contains(repointed, kv => catalog.ByRowId[kv.Key].Name == "Expanding House of Splendors");
        Assert.Contains(repointed, kv => catalog.ByRowId[kv.Key].Name == "The Seaweed Is Always Greener");

        // Raw file: schema 1, a note, keys ascending, and the loader dropped nothing.
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(CuratedDir, CuratedData.QuirksFileName)), documentOptions: CuratedData.StrictOptions)!.AsObject();
        Assert.Equal(1, (int)root["schema"]!);
        Assert.False(string.IsNullOrWhiteSpace((string?)root["note"]));
        var keys = root["entries"]!.AsObject().Select(kv => uint.Parse(kv.Key, System.Globalization.CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(keys.OrderBy(k => k), keys);
        Assert.Equal(curated.Quirks.Count, keys.Count);
    }

    [Fact]
    public void Chains_reference_existing_genres_in_the_catalog()
    {
        var curated = Curated();
        var genres = fixture.Bundle.Catalog.ByGenre;
        Assert.NotEmpty(curated.Chains);
        foreach (var chain in curated.Chains)
        {
            Assert.False(string.IsNullOrWhiteSpace(chain.Note), $"chain '{chain.Name}' has no note");
            foreach (var genreId in chain.GenreIds)
            {
                Assert.True(genres.ContainsKey(genreId), $"chain '{chain.Name}' references genre {genreId}, which holds no quest in the catalog");
            }
        }

        var names = curated.Chains.Select(c => c.Name).ToList();
        Assert.Equal(names.Distinct(StringComparer.Ordinal), names);
    }

    [Fact]
    public void Asphodelos_first_circle_is_unlocked_by_Where_Familiars_Dare_not_The_Crystal_from_Beyond()
    {
        // docs/data/verification-report-2.md section 2 row 5: the wiki duty page names Where Familiars Dare as the
        // unlock quest and Garland lists instance 30111 on quest 70012; 70011 only starts the Pandaemonium chain.
        var curated = Curated();

        Assert.True(curated.DutyUnlocks.TryGetValue(70012, out var unlock), "70012 Where Familiars Dare has no duty unlock entry");
        Assert.Contains(808u, unlock!.ContentFinderConditionIds);
        Assert.False(curated.DutyUnlocks.ContainsKey(70011), "70011 The Crystal from Beyond must not carry the Asphodelos unlock");
        Assert.DoesNotContain(70011u, curated.SystemUnlocks.Keys);
    }

    [Fact]
    public void The_Crystal_from_Beyond_stays_an_unlock_quest_under_the_EventIconType_rule()
    {
        // 70011 lost its curated duty unlock, but the sheet draws it with the blue "+" journal icon (EventIconType 8),
        // so it remains a feature quest through FeaturePresets alone: the curated files are not needed for that.
        var quest = fixture.Bundle.Catalog.ByRowId[70011];
        Assert.Equal("The Crystal from Beyond", quest.Name);
        Assert.Equal(FeaturePresets.FeatureEventIconType, quest.EventIconType);
        Assert.True(FeaturePresets.IsFeatureQuest(quest, CuratedData.Empty), "70011 should be a feature quest by its journal icon alone");

        var successor = fixture.Bundle.Catalog.ByRowId[70012];
        Assert.Equal("Where Familiars Dare", successor.Name);
        Assert.Equal(FeaturePresets.FeatureEventIconType, successor.EventIconType);
    }
}
