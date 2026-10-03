using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Seasonal;
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

    [GitHistoryFact]
    public void Version_json_names_the_last_commit_that_changed_the_curated_data()
    {
        // tools/regen.ps1 stamps VERSION.json with `git log -1 --format=%h` over the curated data files (VERSION.json
        // and README.md excluded). Settings > About, the status bar and every "Report this quest" block name that
        // revision, so a data commit without a fresh stamp would send bug reports against the wrong data.
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(CuratedDir, CuratedData.VersionFileName)), documentOptions: CuratedData.StrictOptions)!.AsObject();
        var stamp = (string?)root[CuratedData.CuratedRevisionKey];
        Assert.False(string.IsNullOrWhiteSpace(stamp), "VERSION.json has no curatedRevision; run tools/regen.ps1");
        Assert.DoesNotContain("-dirty", stamp, StringComparison.Ordinal);

        // The full hash, compared by prefix: the abbreviation's length depends on the clone's object count.
        var head = GitHistoryFactAttribute.Git(
            "log", "-1", "--format=%H", "--",
            "Tsukimichi/Data/curated", ":!Tsukimichi/Data/curated/VERSION.json", ":!Tsukimichi/Data/curated/README.md");
        Assert.False(string.IsNullOrEmpty(head), "git log found no commit touching Tsukimichi/Data/curated");
        Assert.True(
            stamp!.Length >= 7 && head!.StartsWith(stamp, StringComparison.OrdinalIgnoreCase),
            $"VERSION.json says curatedRevision {stamp}, but the curated data was last changed in {head![..7]}; run tools/regen.ps1 (-NoXivApi is enough) and commit VERSION.json and docs/data/DATA-VERSION.md");
    }

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
            CuratedData.FestivalsFileName, CuratedData.ChainsFileName, CuratedData.OnlineStoreFileName, CuratedData.OtherSourcesFileName,
            CuratedData.RefileOverridesFileName, CuratedData.RetiredQuestsFileName, CuratedData.QuirksFileName, CuratedData.PayoffGatesFileName,
            CuratedData.PathChoicesFileName, CuratedData.ExtraPrerequisitesFileName, CuratedData.GameGatesFileName, CuratedData.VersionFileName,
            CuratedData.AetheryteUnlocksFileName,
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
            .Concat(curated.AetheryteUnlocks.Values.SelectMany(a => a.Quests).Select(id => (File: CuratedData.AetheryteUnlocksFileName, Id: id)))
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
    public void Other_sources_entries_carry_evidence_and_each_marks_its_unique_quests_entries_with_the_duties()
    {
        var curated = Curated();
        var unique = Unique();

        // The 44 Darklight and Hero's accessories verification-full.md (T2a) found also dropping in A Realm Reborn dungeons.
        Assert.Equal(44, curated.OtherSources.Count);
        Assert.All(curated.OtherSources.Values, o => Assert.Equal(OtherSource.DungeonDrop, o.Source));

        // Raw file: schema 1, a note, keys ascending, every entry with name, source, where, an https evidence URL and a
        // note, and the loader dropped nothing.
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(CuratedDir, CuratedData.OtherSourcesFileName)), documentOptions: CuratedData.StrictOptions)!.AsObject();
        Assert.Equal(1, (int)root["schema"]!);
        Assert.False(string.IsNullOrWhiteSpace((string?)root["note"]));
        var entries = root["entries"]!.AsObject();
        var keys = entries.Select(kv => uint.Parse(kv.Key, System.Globalization.CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(keys.OrderBy(k => k).Distinct(), keys);
        Assert.Equal(curated.OtherSources.Count, keys.Count);
        foreach (var (key, node) in entries)
        {
            var obj = node!.AsObject();
            foreach (var field in new[] { "name", "source", "where", "evidence", "note" })
            {
                Assert.True(obj.ContainsKey(field), $"other_sources {key} lacks {field}");
            }

            Assert.StartsWith("https://", (string?)obj["evidence"], StringComparison.Ordinal);
        }

        // Every item is a reward in unique_quests.json under the same name, and every entry delivered as it carries the
        // source with the where text; nothing else carries DungeonDrop.
        var byItem = unique.Entries.Where(e => e.ItemId != 0).ToLookup(e => e.ItemId);
        foreach (var (itemId, other) in curated.OtherSources)
        {
            var matched = byItem[itemId].ToList();
            Assert.True(matched.Count > 0, $"other_sources {itemId} {other.Name} matches no unique_quests.json entry");
            Assert.All(matched, e =>
            {
                Assert.Equal(other.Name, e.RewardName);
                Assert.True(e.HasOtherSource(other.Source), $"{e.QuestRowId} {e.RewardName} is not marked {other.Source} (stale regen?)");
                Assert.Equal(other.Where, e.OtherSourceNote(other.Source));
            });
        }

        var drops = unique.Entries.Where(e => e.DropsInDuty).ToList();
        Assert.Equal(44, drops.Count);
        Assert.All(drops, e => Assert.True(curated.OtherSources.ContainsKey(e.ItemId), $"{e.QuestRowId} {e.RewardName} is marked DungeonDrop but no curated entry explains it"));
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

    /// <summary>Collaboration events the game runs again under the same Festival id (the wiki's list of runs).</summary>
    private static readonly ushort[] RerunFestivals = [39, 84, 148];

    [Fact]
    public void Festival_entries_are_catalog_festivals_with_https_evidence_a_note_and_parseable_dates()
    {
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(CuratedDir, CuratedData.FestivalsFileName)), documentOptions: CuratedData.StrictOptions)!.AsObject();
        Assert.False(string.IsNullOrWhiteSpace((string?)root["$schema_note"]));
        var entries = root["entries"]!.AsObject();
        var keys = entries.Select(kv => ushort.Parse(kv.Key, CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(keys.OrderBy(k => k), keys);

        var catalogFestivals = fixture.Bundle.Catalog.All.Where(q => q.Festival != 0).Select(q => q.Festival).ToHashSet();
        var loaded = Curated().Festivals;
        Assert.Equal(entries.Count, loaded.Count);
        Assert.Equal(100, loaded.Count);
        Assert.Equal(83, loaded.Values.Count(f => f.End is not null));

        foreach (var (key, node) in entries)
        {
            var obj = node!.AsObject();
            var id = ushort.Parse(key, CultureInfo.InvariantCulture);
            Assert.True(catalogFestivals.Contains(id), $"festivals.json {key} is not the Festival of any quest in the catalog");
            foreach (var field in new[] { "name", "evidence", "note" })
            {
                Assert.False(string.IsNullOrWhiteSpace((string?)obj[field]), $"festivals.json {key} lacks {field}");
            }

            var info = loaded[id];
            Assert.True(Uri.TryCreate(info.Evidence, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps, $"festivals.json {key} evidence is not an https URL");

            // A date needs the Lodestone announcement; a name alone may also come from the wiki's event page (an
            // edition not announced yet).
            var lodestone = uri!.Host.EndsWith(".finalfantasyxiv.com", StringComparison.Ordinal) && uri.AbsolutePath.Contains("/lodestone/", StringComparison.Ordinal);
            var wiki = uri.Host == "ffxiv.consolegameswiki.com" && uri.AbsolutePath.StartsWith("/wiki/", StringComparison.Ordinal);
            Assert.True(lodestone || (wiki && info.End is null), $"festivals.json {key} evidence is neither a Lodestone page nor, for an undated entry, a wiki page: {info.Evidence}");

            // Dates come in pairs, parse as UTC and run forwards; a dated entry names its edition's year, which the
            // end falls in (or the year after, for an edition held late: All Saints' Wake 2021 ran in January 2022).
            Assert.Equal(obj.ContainsKey("start"), obj.ContainsKey("end"));
            if (info.End is not { } end)
            {
                continue;
            }

            Assert.Equal(DateTimeKind.Utc, end.Kind);
            Assert.True(info.Start is { } start && start <= end, $"festivals.json {key} runs backwards");
            var year = Regex.Match(info.Name, @"\((\d{4})\)$");
            Assert.True(year.Success, $"festivals.json {key} ({info.Name}) has an end date but no edition year");
            var edition = int.Parse(year.Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.InRange(end.Year - edition, 0, 1);
        }
    }

    [Fact]
    public void No_end_date_is_shipped_for_a_festival_id_the_game_reruns()
    {
        // A past end turns a festival's undone quests Locked out whenever it is not running. Collaboration events come
        // back under the same Festival id (A Nocturne for Heroes, id 84: 2019, 2021, 2024 and September 2026), so a
        // date for one run would lock the quests out between runs and show a stale end while the next one runs.
        var curated = Curated();
        var catalog = fixture.Bundle.Catalog;
        foreach (var id in RerunFestivals)
        {
            Assert.True(curated.Festivals.TryGetValue(id, out var info), $"festival {id} should keep its name");
            Assert.Null(info!.End);
            Assert.Null(info.Start);
        }

        foreach (var (id, info) in curated.Festivals)
        {
            var genres = catalog.All.Where(q => q.Festival == id).Select(q => q.Journal.GenreName).Distinct().ToList();
            if (genres.SequenceEqual(["Collaboration Quests"]))
            {
                Assert.True(info.End is null, $"festival {id} ({info.Name}) is a collaboration event and must not carry an end date");
            }
        }

        // The seasonal events take a new Festival id every edition: no two ids carry the same edition.
        Assert.Equal(curated.Festivals.Count, curated.Festivals.Values.Select(f => f.Name).Distinct().Count());

        // A rerun (never locked out) is exactly an undated entry without an edition year, and only collaborations are
        // that: an undated seasonal edition keeps its year in the name, so the completed-quest rule still applies to it.
        foreach (var (id, info) in curated.Festivals)
        {
            var collaboration = catalog.All.Where(q => q.Festival == id).All(q => q.Journal.GenreName == "Collaboration Quests");
            Assert.True(collaboration == info.IsRerun, $"festival {id} ({info.Name}): a collaboration must be undated with no year in its name, and nothing else may be");
        }
    }

    /// <summary>The event names a journal genre gives right ("Moonfire Faire Events" is Moonfire Faire).</summary>
    private static readonly string[] GenreNamesThatAreEventNames =
        ["Heavensturn", "Valentione's Day", "Little Ladies' Day", "Hatching-tide", "Moonfire Faire", "All Saints' Wake", "Starlight Celebration"];

    [Fact]
    public void Every_festival_reads_its_event_name_not_a_genre_that_only_files_it()
    {
        // A Festival id without a curated entry is named from its journal genre. That is right for most seasonal
        // events and wrong for "Gold Saucer Festivities" (The Make It Rain Campaign), "Rising Events" (The Rising),
        // "Collaboration Quests" and the combined "Little Ladies' & Hatching-tide Events": those need an undated entry
        // with the event's name, or the running-now lines and the login notice read the genre. A new edition of one
        // after a game update fails here until it is curated.
        var catalog = fixture.Bundle.Catalog;
        var curated = Curated().Festivals;
        foreach (var id in catalog.All.Where(q => q.Festival != 0).Select(q => q.Festival).Distinct().Order())
        {
            if (curated.ContainsKey(id))
            {
                continue;
            }

            var name = SeasonalNow.Name(id, catalog, curated);
            Assert.True(GenreNamesThatAreEventNames.Contains(name), $"festival {id} has no curated entry and reads \"{name}\"; add an undated entry with its event name to festivals.json");
        }

        Assert.Equal("The Make It Rain Campaign", SeasonalNow.Name(139, catalog, curated));
        Assert.Equal("Little Ladies' Day & Hatching-tide", SeasonalNow.Name(145, catalog, curated));
        Assert.Equal("The Make It Rain Campaign", SeasonalNow.Name(146, catalog, curated));
        Assert.Equal("The Rising", SeasonalNow.Name(151, catalog, curated));
        Assert.Equal("The Make It Rain Campaign", SeasonalNow.Name(161, catalog, curated));
        Assert.Equal("The Rising", SeasonalNow.Name(162, catalog, curated));
        Assert.Equal("Keybound Brawler", SeasonalNow.Name(257, catalog, curated));

        // The named editions anchor their year for the seasonal history.
        var years = SeasonalNow.EditionYears(catalog, curated);
        Assert.Equal(2024, years[145]);
        Assert.Equal(2025, years[162]);
        Assert.Equal(2026, years[176]);
        Assert.False(years.ContainsKey(257));
    }

    [Fact]
    public void Curated_ends_lock_out_past_editions_but_never_a_rerun_or_a_running_event()
    {
        var catalog = fixture.Bundle.Catalog;
        var now = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);
        var context = EvalContextBuilder.Build(Curated().Festivals, fixture.Bundle.Jobs, () => now);
        var fresh = Evaluation.Fixture.Snapshot() with { JobLevels = Evaluation.Fixture.Levels((Evaluation.Fixture.Gladiator, 100)) };

        // Moonfire Faire 2014 (id 11) ended in 2014: its quests are Locked out for a character who never saw it.
        var moonfire = catalog.All.First(q => q.Festival == 11);
        Assert.Equal(QuestState.Foreclosed, StateResolver.Resolve(moonfire, fresh, catalog, context).State);

        // A Nocturne for Heroes (id 84) reruns: between runs it is only Blocked, and while it runs it is not locked out.
        var nocturne = catalog.All.First(q => q.Festival == 84);
        Assert.Equal(QuestState.Blocked, StateResolver.Resolve(nocturne, fresh, catalog, context).State);
        Assert.NotEqual(QuestState.Foreclosed, StateResolver.Resolve(nocturne, fresh with { ActiveFestivals = [84] }, catalog, context).State);

        // Even a dated festival the game switches on again reads by the live flag, not by its old end.
        Assert.NotEqual(QuestState.Foreclosed, StateResolver.Resolve(moonfire, fresh with { ActiveFestivals = [11] }, catalog, context).State);

        // An undated edition (The Rising 2024, id 151) is not a rerun: once part of it was done, the rest is Locked out.
        var rising = fresh with { CompletedBits = Evaluation.Fixture.Bits(70551) };
        Assert.Equal(QuestState.Foreclosed, StateResolver.Resolve(catalog.ByRowId[70552], rising, catalog, context).State);
        Assert.Equal(QuestState.Blocked, StateResolver.Resolve(catalog.ByRowId[70552], fresh, catalog, context).State);

        // A character who did part of a rerun is not locked out of the rest between runs: the curated rerun entry is
        // checked before the completed-quest heuristic. The Man in Black (68694) done, 84 not running.
        var partial = fresh with { CompletedBits = Evaluation.Fixture.Bits(68694) };
        Assert.True(partial.IsCompleted(QuestRecord.ToQuestId(68694)));
        foreach (var rowId in new uint[] { 68695, 68696 })
        {
            var rest = catalog.ByRowId[rowId];
            Assert.Equal(84, rest.Festival);
            Assert.NotEqual(QuestState.Foreclosed, StateResolver.Resolve(rest, partial, catalog, context).State);
            Assert.NotEqual(QuestState.Foreclosed, StateResolver.ResolveAll(catalog, partial, context)[rowId].State);
        }
    }

    [Fact]
    public void Path_choices_pin_the_cities_the_rule_finds_and_name_real_class_and_company_quests()
    {
        // Feature plan v4 D1: path_choices.json only names and guards what PathIndex finds in the sheets.
        var choices = Curated().PathChoices;
        var catalog = fixture.Bundle.Catalog;
        var index = PathIndex.For(catalog);

        // The three cities: the pin and the rule agree exactly, and each root opens a level-1 main scenario line.
        Assert.Equal(["Gridania", "Limsa Lominsa", "Ul'dah"], choices.Cities.Select(c => c.Label));
        Assert.Equal(choices.Cities.Select(c => c.Root).Order(), index.RuleCityRoots.Order());
        Assert.All(choices.Cities, c => Assert.StartsWith("Coming to ", catalog.ByRowId[c.Root].Name, StringComparison.Ordinal));

        // The eight classes: each "Close to Home" follows one pinned root and sits in a sibling set, each starter has no
        // previous quest, opens a class track and is the class's own "Way of".
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 26], choices.Classes.Select(c => (int)c.ClassJob).Order());
        foreach (var pin in choices.Classes)
        {
            var home = catalog.ByRowId[pin.CloseToHome];
            Assert.Equal("Close to Home", home.Name);
            Assert.Equal(0u, home.Journal.SectionId);
            Assert.Contains(Assert.Single(home.PreviousQuests.QuestIds), choices.Cities.Select(c => c.Root));
            Assert.Contains(index.SiblingSets, set => set.Contains(pin.CloseToHome));

            var starter = catalog.ByRowId[pin.Starter];
            Assert.Empty(starter.PreviousQuests.QuestIds);
            Assert.Equal("Way of the " + pin.Label, starter.Name);
            Assert.Contains(index.ClassTracks, t => t.Starter == pin.Starter);
        }

        Assert.Equal(8, choices.Classes.Select(c => c.CloseToHome).Distinct().Count());

        // The company tags: only where the sheet's column says nothing, on quests named after that company, each in a
        // Grand Company choice group.
        var companyNames = new Dictionary<byte, string> { [1] = "(Maelstrom)", [2] = "(Twin Adder)", [3] = "(Immortal Flames)" };
        Assert.Equal(6, choices.GrandCompanies.Count);
        foreach (var (rowId, tag) in choices.GrandCompanies)
        {
            var quest = catalog.ByRowId[rowId];
            Assert.Equal(0, quest.GrandCompany);
            Assert.EndsWith(companyNames[tag.GrandCompany], quest.Name, StringComparison.Ordinal);
            Assert.Contains(index.Groups, g => g.Kind == PathKind.GrandCompany && g.Options.Any(o => o.Anchors.Contains(rowId) && o.GrandCompany == tag.GrandCompany));
        }

        // The raw file: every entry carries a note, keys ascend in each section.
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(CuratedDir, CuratedData.PathChoicesFileName)), documentOptions: CuratedData.StrictOptions)!.AsObject();
        Assert.Equal(1, (int)root["schema"]!);
        foreach (var section in new[] { "cities", "classes", "grandCompanies" })
        {
            var keys = root[section]!.AsObject().Select(kv => uint.Parse(kv.Key, CultureInfo.InvariantCulture)).ToList();
            Assert.Equal(keys.Order(), keys);
            Assert.All(root[section]!.AsObject(), kv => Assert.False(string.IsNullOrWhiteSpace((string?)kv.Value!["note"]), $"{section} {kv.Key} has no note"));
        }
    }
}
