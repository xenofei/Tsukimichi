using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Evaluation;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// Record-only checks over the frozen catalog: the same totals, feature-quest counts and chains the
/// [GameDataFact] tests assert against the live sheets, run here on every machine and in CI.
/// </summary>
public class CatalogFixtureTests(FixtureCatalog fixture, ITestOutputHelper output) : IClassFixture<FixtureCatalog>
{
    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    [Fact]
    [Trait("Category", "Curated")]
    public void Fixture_loads_every_named_quest_with_its_lookups()
    {
        output.WriteLine($"{Path.GetFileName(fixture.Path)}: {Catalog.Count} quests, game {fixture.GameVersion}");
        Assert.Equal(ExpectedCounts.NamedQuests, Catalog.Count);
        Assert.Equal(ExpectedCounts.GameVersion, fixture.GameVersion);
        Assert.Equal("English", fixture.Bundle.Language);
        Assert.Matches(@"^\d{4}\.\d{2}\.\d{2}\.\d{4}\.\d{4}$", fixture.GameVersion);
        Assert.EndsWith(CatalogFixtureFile.FileName(fixture.GameVersion), fixture.Path);
        Assert.All(Catalog.All, q => Assert.False(string.IsNullOrWhiteSpace(q.Name)));

        // Landmarks the live tests pin, so a mis-serialised field shows up here first.
        var closeToHome = Catalog.GetByRowId(65621u);
        Assert.NotNull(closeToHome);
        Assert.Equal("Close to Home", closeToHome.Name);
        Assert.Equal([65575u], closeToHome.PreviousQuests.QuestIds);
        Assert.Equal(JoinKind.Any, closeToHome.PreviousQuests.Join);
        Assert.Equal("Seventh Umbral Era", closeToHome.Journal.GenreName);
        Assert.Equal(130u, closeToHome.ClassJobCategory);
        Assert.Equal(3, closeToHome.EventIconType);
        Assert.NotNull(closeToHome.Issuer);
        Assert.Equal(1001140u, closeToHome.Issuer.NpcId);
        Assert.Equal(23.79f, closeToHome.Issuer.X, 0.01f);
        Assert.Contains(closeToHome.Rewards, r => r.Kind == RewardKind.ClassJob && r.Id == 4);
        Assert.Equal([67099u, 67100u], Catalog.GetByRowId(67101u)!.QuestLocks);
        Assert.Equal(48, Catalog.GetByRowId(67960u)!.Festival);

        var names = fixture.Bundle.Names;
        Assert.Equal("A Realm Reborn", names.Expansion(0));
        Assert.Equal("Immortal Flames", names.GrandCompany(3));
        Assert.Equal("Paladin", names.ClassJob(19));
        Assert.Equal(66591u, names.ClassJobInfo(19)!.UnlockQuestRowId);

        var jobs = fixture.Bundle.Jobs;
        Assert.True(jobs.Admits(142, 19), "142 (any DoW/DoM) admits PLD");
        Assert.False(jobs.Admits(142, 8), "142 does not admit CRP");
        Assert.True(jobs.JobColumns >= 44, "sheet has a column per class/job including BST (43)");
    }

    [Fact]
    public void Tree_totals_count_listed_quests_per_section_and_bucket_the_removed()
    {
        var unlisted = Catalog.All.Where(q => q.IsUnlisted).ToArray();
        Assert.Equal(ExpectedCounts.RetiredByRule1, unlisted.Length);
        Assert.All(unlisted, q => Assert.Equal(0u, q.Journal.GenreId));
        Assert.All(unlisted, q => Assert.True(q.IsRetired));
        var removed = Catalog.Removed;
        Assert.Equal(ExpectedCounts.Retired, removed.Count);

        var states = Catalog.All.ToDictionary(q => q.RowId, _ => QuestState.Ready);
        var counts = TreeCounts.Compute(Catalog, states, includeUnlisted: true);

        // The class intros are listed under their genre but in no count (QuestRecord.CountsInTotals).
        var uncounted = Catalog.All.Count(q => !q.IsRemoved && !q.CountsInTotals);
        Assert.Equal(ExpectedCounts.RefiledByRule2, uncounted);

        Assert.Equal(removed.Count, counts.Unlisted.Total);
        Assert.Equal(Catalog.Count - uncounted, counts.Overall.Total);
        foreach (var (section, quests) in Catalog.BySection)
        {
            var listed = quests.Count(q => !q.IsRemoved && q.CountsInTotals);
            output.WriteLine($"section {section}: {listed} counted of {quests.Count}");
            Assert.Equal(listed, counts.Section(section).Total);
        }

        Assert.Equal(Catalog.Count - removed.Count - uncounted, counts.Sections.Values.Sum(c => c.Total));
        Assert.Equal(Catalog.Count - removed.Count - uncounted, TreeCounts.Compute(Catalog, states, includeUnlisted: false).Overall.Total);
    }

    [Fact]
    [Trait("Category", "Curated")]
    public void Derived_feature_set_covers_the_unlock_quests()
    {
        var dataDir = FixtureCatalog.ShippedDataDir();
        var curated = CuratedData.Load(Path.Combine(dataDir, "curated"));
        var unique = UniqueRewardsFile.Load(Path.Combine(dataDir, "unique_quests.json"));
        Assert.NotEmpty(unique.Entries);

        IReadOnlySet<uint> ids = FeaturePresets.Derive(Catalog, curated, unique.Entries);
        output.WriteLine($"Feature quests: {ids.Count} of {Catalog.Count}");

        Assert.Equal(ExpectedCounts.FeatureQuests, ids.Count);
        Assert.All(ids, id => Assert.False(FeaturePresets.IsMainScenario(Catalog.ByRowId[id])));
        Assert.All(ids, id => Assert.False(Catalog.ByRowId[id].IsRepeatable));
        Assert.All(ids, id => Assert.False(Catalog.ByRowId[id].IsRetired));

        // Every live, non-repeatable blue-icon quest (feature or quasi-quest) outside the main scenario is in.
        foreach (var quest in Catalog.All)
        {
            if (FeaturePresets.HasFeatureIcon(quest) && !quest.IsRetired && !quest.IsRepeatable && !FeaturePresets.IsMainScenario(quest))
            {
                Assert.Contains(quest.RowId, ids);
            }
        }

        Assert.DoesNotContain(Catalog.All.First(q => q.Name == "It's Probably Pirates" && !q.IsUnlisted).RowId, ids);
        Assert.DoesNotContain(65621u, ids);
        Assert.Contains(66233u, ids);
        Assert.Contains(66584u, ids);
        Assert.Contains(Catalog.All.First(q => q.Name == "Legacy of Allag" && !q.IsUnlisted).RowId, ids);
    }

    [Fact]
    public void Hildibrand_genres_are_linear()
    {
        var hildibrand = Catalog.ByGenre
            .Where(kv => kv.Key != 0 && kv.Value[0].Journal.GenreName.Contains("Hildibrand", StringComparison.Ordinal))
            .Select(kv => kv.Key)
            .OrderBy(id => id)
            .ToArray();

        Assert.Equal([82u, 83u, 84u, 85u, 87u], hildibrand);
        Assert.All(hildibrand, id => Assert.True(ChainCatalog.IsLinear(Catalog.ByGenre[id]), $"genre {id} is not linear"));
    }

    [Fact]
    [Trait("Category", "Curated")]
    public void Shipped_chains_resolve_every_genre_and_span_the_catalog()
    {
        var curated = CuratedData.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "curated"));
        Assert.DoesNotContain(curated.Warnings, w => w.StartsWith(CuratedData.ChainsFileName, StringComparison.Ordinal));
        Assert.True(curated.Chains.Count >= 20, $"expected the seeded chains, found {curated.Chains.Count}");

        var chains = ChainCatalog.Build(Catalog, curated);
        Assert.Empty(chains.Warnings);

        foreach (var entry in curated.Chains)
        {
            var chain = chains.Chains.First(c => c.Name == entry.Name);
            Assert.Equal(entry.GenreIds.Sum(id => Catalog.ByGenre[id].Count(q => !q.IsRetired)), chain.RowIds.Count);
            Assert.All(chain.RowIds, id => Assert.Same(chain, chains.ForQuest(id)));
        }

        var hildibrand = chains.Chains.First(c => c.Name == "Hildibrand");
        Assert.Equal(57, hildibrand.RowIds.Count);
        Assert.Equal(Catalog.ByGenre[82][0].RowId, hildibrand.RowIds[0]);
        Assert.Equal(Catalog.ByGenre[87][^1].RowId, hildibrand.RowIds[^1]);
        Assert.Equal(17, chains.Chains.First(c => c.Name == "Omega").RowIds.Count);
        Assert.Equal(17, chains.Chains.First(c => c.Name == "Pandæmonium").RowIds.Count);
    }

    [Fact]
    public void Derived_chains_cover_linear_genres_and_qualify_repeated_names()
    {
        var chains = ChainCatalog.Build(Catalog, CuratedData.Empty);
        output.WriteLine($"{chains.Chains.Count} derived chains");

        var bahamut = chains.ForQuest(Catalog.ByGenre[17][0].RowId);
        Assert.NotNull(bahamut);
        Assert.Equal("Bahamut Quests", bahamut.Name);
        Assert.Equal(6, bahamut.RowIds.Count);

        Assert.Null(chains.ForQuest(65621u));

        var amaljaa = chains.ForQuest(Catalog.ByGenre[40][0].RowId);
        Assert.NotNull(amaljaa);
        Assert.Equal("Main Quests (Amalj'aa Quests)", amaljaa.Name);

        Assert.All(chains.Chains, c => Assert.True(c.RowIds.Count >= ChainCatalog.MinChainLength));
        Assert.True(chains.Chains.Count >= 80, $"expected dozens of derived chains, found {chains.Chains.Count}");
    }

    [Fact]
    public void Quarrels_with_Squirrels_displays_the_journal_level_and_is_Ready_at_the_raw_level()
    {
        // Verification report 2, inaccuracy 1: the journal and the Lodestone print ClassJobLevel + QuestLevelOffset
        // (Lv 3), while the Lodestone requirement line still reads "Lv. 1"; the raw value stays the acceptance gate.
        var quest = Catalog.GetByRowId(65561u);
        Assert.NotNull(quest);
        Assert.Equal("Quarrels with Squirrels", quest.Name);
        Assert.Equal(1, quest.Level);
        Assert.Equal(2, quest.LevelOffset);
        Assert.Equal(3, quest.DisplayLevel);

        var ctx = EvalContext.Default with { ClassJobs = fixture.Bundle.Jobs };
        var levelOne = Fixture.Snapshot() with { JobLevels = Fixture.Levels((Fixture.Gladiator, 1)) };
        var result = StateResolver.Resolve(quest, levelOne, Catalog, ctx);

        Assert.Equal(QuestState.Ready, result.State);
        Assert.True(Fixture.Only(result.Requirements, RequirementKind.Level).Met, "level 1 meets the raw acceptance level");

        // The report's count, and the identity every display site relies on.
        Assert.Equal(215, Catalog.All.Count(q => q.LevelOffset > 0));
        Assert.All(Catalog.All, q => Assert.Equal(q.Level + q.LevelOffset, q.DisplayLevel));
    }
}

/// <summary>The one Lumina-backed check on the fixture: it must equal what the mapper reads from the installed game.</summary>
public class CatalogFixtureLiveTests(FixtureCatalog fixture, GameDataFixture live) : IClassFixture<FixtureCatalog>, IClassFixture<GameDataFixture>
{
    [GameDataFact]
    public void Fixture_matches_live_sheets()
    {
        var stale = $"{Path.GetFileName(fixture.Path)} is stale; {FixtureCatalog.RegenerateHint}";

        var sqpack = Environment.GetEnvironmentVariable(GameDataFactAttribute.EnvVar)!;
        var verFile = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sqpack).TrimEnd('\\', '/')) ?? sqpack, "ffxivgame.ver");
        if (File.Exists(verFile))
        {
            var installed = File.ReadLines(verFile).First().Trim();
            Assert.True(installed == fixture.GameVersion, $"installed game is {installed} but the fixture was dumped from {fixture.GameVersion}; {stale}");
        }

        var expected = live.Bundle.Catalog.All;
        var actual = fixture.Bundle.Catalog.All;
        Assert.True(expected.Count == actual.Count, $"live catalog has {expected.Count} quests, fixture {actual.Count}; {stale}");

        for (var i = 0; i < expected.Count; i++)
        {
            var e = expected[i];
            var a = actual[i];
            Assert.True(e.RowId == a.RowId, $"quest #{i}: live row {e.RowId}, fixture row {a.RowId}; {stale}");
            Assert.True(e.Name == a.Name, $"row {e.RowId}: live name \"{e.Name}\", fixture \"{a.Name}\"; {stale}");
            Assert.True(e.Journal.GenreId == a.Journal.GenreId, $"row {e.RowId}: live genre {e.Journal.GenreId}, fixture {a.Journal.GenreId}; {stale}");
            Assert.True(e.Level == a.Level, $"row {e.RowId}: live level {e.Level}, fixture {a.Level}; {stale}");
            Assert.True(CatalogFixtureFile.ToJson(e) == CatalogFixtureFile.ToJson(a), $"row {e.RowId} ({e.Name}) differs from the live record; {stale}");
        }

        Assert.True(live.Bundle.Jobs.Count == fixture.Bundle.Jobs.Count, $"live has {live.Bundle.Jobs.Count} class/job categories, fixture {fixture.Bundle.Jobs.Count}; {stale}");
        Assert.True(live.Bundle.Jobs.JobColumns == fixture.Bundle.Jobs.JobColumns, $"job columns differ; {stale}");
        Assert.True(live.Bundle.Names.ClassJobInfos.SequenceEqual(fixture.Bundle.Names.ClassJobInfos), $"ClassJob rows differ; {stale}");
        Assert.True(live.Bundle.Names.Expansions.OrderBy(kv => kv.Key).SequenceEqual(fixture.Bundle.Names.Expansions.OrderBy(kv => kv.Key)), $"expansion names differ; {stale}");
        Assert.True(live.Bundle.Names.Tribes.OrderBy(kv => kv.Key).SequenceEqual(fixture.Bundle.Names.Tribes.OrderBy(kv => kv.Key)), $"tribe names differ; {stale}");
    }
}
