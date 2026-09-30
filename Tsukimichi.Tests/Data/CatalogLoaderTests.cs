using System.Diagnostics;
using Lumina;
using LuminaGameData = Lumina.GameData;
using Lumina.Data;
using Tsukimichi.Core.Model;
using Tsukimichi.GameData;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>Runs only when TSUKIMICHI_GAME_PATH points at the game's sqpack folder; skipped otherwise.</summary>
public sealed class GameDataFactAttribute : FactAttribute
{
    public const string EnvVar = "TSUKIMICHI_GAME_PATH";

    public GameDataFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvVar)))
        {
            Skip = $"{EnvVar} is not set";
        }
    }
}

/// <summary>
/// Opens the game data and maps the catalog once per test class, refiled with the shipped curated overlay as the plugin
/// does. Lazy so skipped runs never touch the disk.
/// </summary>
public sealed class GameDataFixture : IDisposable
{
    private readonly Lazy<(LuminaGameData Game, CatalogBundle Bundle, TimeSpan Elapsed)> built;

    public GameDataFixture()
    {
        built = new Lazy<(LuminaGameData, CatalogBundle, TimeSpan)>(Build, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public CatalogBundle Bundle => built.Value.Bundle;

    public LuminaGameData Game => built.Value.Game;

    public TimeSpan MapElapsed => built.Value.Elapsed;

    public void Dispose()
    {
        if (built.IsValueCreated)
        {
            built.Value.Game.Dispose();
        }
    }

    private static (LuminaGameData, CatalogBundle, TimeSpan) Build()
    {
        var path = Environment.GetEnvironmentVariable(GameDataFactAttribute.EnvVar)
                   ?? throw new InvalidOperationException($"{GameDataFactAttribute.EnvVar} is not set");
        var game = new LuminaGameData(path, new LuminaOptions
        {
            DefaultExcelLanguage = Language.English,
            PanicOnSheetChecksumMismatch = false,
        });

        var stopwatch = Stopwatch.StartNew();
        var bundle = CatalogMapper.Map(game.Excel, Language.English, curated: Tsukimichi.Core.Storage.CuratedData.Load(FixtureCatalog.CuratedDir()));
        return (game, bundle, stopwatch.Elapsed);
    }
}

public class CatalogLoaderTests(GameDataFixture fixture, ITestOutputHelper output) : IClassFixture<GameDataFixture>
{
    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    [Fact]
    public void Join_byte_maps_two_to_any_and_everything_else_to_all()
    {
        Assert.Equal(JoinKind.All, CatalogMapper.ToJoin(0));
        Assert.Equal(JoinKind.All, CatalogMapper.ToJoin(1));
        Assert.Equal(JoinKind.Any, CatalogMapper.ToJoin(2));
    }

    [Fact]
    public void Lookup_from_membership_answers_admits_and_jobs_in()
    {
        var lookup = ClassJobCategoryLookup.FromMembership(
        [
            new KeyValuePair<uint, IEnumerable<byte>>(1, [1, 19, 43]),
            new KeyValuePair<uint, IEnumerable<byte>>(2, []),
        ]);

        Assert.True(lookup.Admits(1, 1));
        Assert.True(lookup.Admits(1, 43));
        Assert.False(lookup.Admits(1, 2));
        Assert.False(lookup.Admits(2, 1));
        Assert.False(lookup.Admits(99, 1));
        Assert.Equal([1, 19, 43], lookup.JobsIn(1).ToArray());
        Assert.Empty(lookup.JobsIn(99));
    }

    [GameDataFact]
    public void Maps_every_named_quest()
    {
        output.WriteLine($"CatalogMapper.Map: {Catalog.Count} quests in {fixture.MapElapsed.TotalMilliseconds:F0} ms (excludes GameData construction)");
        Assert.Equal(5373, Catalog.Count);
        Assert.Equal("English", fixture.Bundle.Language);
        Assert.All(Catalog.All, q => Assert.False(string.IsNullOrWhiteSpace(q.Name)));
        Assert.True(fixture.MapElapsed < TimeSpan.FromSeconds(10), $"mapping took {fixture.MapElapsed.TotalMilliseconds:F0} ms");
    }

    [GameDataFact]
    public void Close_to_home_requires_coming_to_gridania()
    {
        var quest = Catalog.GetByRowId(65621u);
        Assert.NotNull(quest);
        Assert.Equal("Close to Home", quest.Name);
        Assert.Equal("ManFst002_00085", quest.InternalId);
        Assert.Equal([65575u], quest.PreviousQuests.QuestIds);

        // The sheet stores PreviousQuestJoin = 2 for this quest (single prerequisite in slot 1), which is the "any" join.
        Assert.Equal(JoinKind.Any, quest.PreviousQuests.Join);

        Assert.Equal("Seventh Umbral Era Main Scenario Quests", quest.Journal.CategoryName);
        Assert.Equal("Seventh Umbral Era", quest.Journal.GenreName);
        Assert.Equal(0u, quest.Journal.SectionId);
        Assert.Equal("Main Scenario (A Realm Reborn through Endwalker)", quest.Journal.SectionName);
        Assert.False(quest.IsUnlisted);
        Assert.Equal(1, quest.Level);
        Assert.Equal(130u, quest.ClassJobCategory);
        Assert.Contains(quest.Rewards, r => r.Kind == RewardKind.ClassJob && r.Id == 4);
    }

    [GameDataFact]
    public void Ultimate_weapon_rewards_item_6008()
    {
        var quest = Catalog.GetByRowId(70058u);
        Assert.NotNull(quest);
        Assert.Equal("The Ultimate Weapon", quest.Name);
        var reward = Assert.Single(quest.Rewards, r => r.Kind == RewardKind.Item && r.ItemId == 6008);
        Assert.Equal(6008u, reward.Id);
        Assert.Equal(1u, reward.Count);
        Assert.NotEqual(0u, reward.Icon);
        Assert.False(string.IsNullOrEmpty(reward.Name));
        Assert.Equal(15000u, quest.Gil);
    }

    [GameDataFact]
    public void Immortal_flames_hunt_has_grand_company_gate_and_two_locks()
    {
        var quest = Catalog.GetByRowId(67101u);
        Assert.NotNull(quest);
        Assert.Equal(3, quest.GrandCompany);
        Assert.Equal(9, quest.GrandCompanyRank);
        Assert.Equal([67099u, 67100u], quest.QuestLocks);
        Assert.Equal("Immortal Flames", fixture.Bundle.Names.GrandCompany(quest.GrandCompany));
    }

    [GameDataFact]
    public void Golden_rain_is_festival_48()
    {
        var quest = Catalog.GetByRowId(67960u);
        Assert.NotNull(quest);
        Assert.Equal(67960u, quest.RowId);
        Assert.Equal(48, quest.Festival);
        Assert.Equal(JoinKind.Any, quest.PreviousQuests.Join);
        Assert.Equal(3, quest.PreviousQuests.QuestIds.Length);
        Assert.Equal(80116u, quest.IconSpecial);
        Assert.Equal(0u, Catalog.GetByRowId(65621u)!.IconSpecial);
    }

    [GameDataFact]
    public void Event_icon_type_is_three_for_a_main_scenario_quest_and_eight_for_a_feature_quest()
    {
        // "Close to Home" (65621) carries the main scenario icon (1 is the ordinary side quest); "Hallo Halatali" (66233) the blue "+" feature icon.
        Assert.Equal(3, Catalog.GetByRowId(65621u)!.EventIconType);
        Assert.Equal(8, Catalog.GetByRowId(66233u)!.EventIconType);
    }

    [GameDataFact]
    public void Issuer_carries_npc_name_and_raw_level_coordinates()
    {
        var issuer = Catalog.GetByRowId(65621u)!.Issuer;
        Assert.NotNull(issuer);
        Assert.Equal(1001140u, issuer.NpcId);
        Assert.False(string.IsNullOrEmpty(issuer.Name));
        Assert.Equal(183u, issuer.TerritoryId);
        Assert.Equal(2u, issuer.MapId);
        Assert.Equal(23.79f, issuer.X, 0.01f);
        Assert.Equal(-8f, issuer.Y, 0.01f);
        Assert.Equal(115.86f, issuer.Z, 0.01f);
    }

    [GameDataFact]
    public void Journal_order_groups_sections_then_categories_then_genres()
    {
        var all = Catalog.All;
        Assert.Equal(0u, all[0].Journal.SectionId);

        // Within a genre, quests follow the sheet's SortKey (low 16 bits of the composite key).
        var msq = Catalog.ByGenre[1];
        Assert.Equal("Close to Home", msq.First(q => q.RowId == 65621).Name);
        Assert.True(msq.Zip(msq.Skip(1)).All(p => p.First.Journal.SortKey <= p.Second.Journal.SortKey));

        // Listed quests never sort after unlisted ones; after refiling only the retired rows are left without a genre.
        var lastListed = all.Select((q, i) => (q, i)).Last(p => !p.q.IsUnlisted).i;
        var firstUnlisted = all.Select((q, i) => (q, i)).First(p => p.q.IsUnlisted).i;
        Assert.True(lastListed < firstUnlisted);
        Assert.Equal(ExpectedCounts.RetiredByRule1, all.Count(q => q.IsUnlisted));
    }

    [GameDataFact]
    public void Class_job_categories_come_from_the_raw_sheet_columns()
    {
        var jobs = fixture.Bundle.Jobs;
        Assert.True(jobs.Admits(142, 19), "142 (any DoW/DoM) admits PLD");
        Assert.False(jobs.Admits(142, 8), "142 does not admit CRP");
        Assert.True(jobs.Admits(1, 1), "1 (all) admits GLA");
        Assert.False(jobs.Admits(0, 1), "0 admits nothing");
        Assert.Contains((byte)19, jobs.JobsIn(142));
        Assert.DoesNotContain((byte)8, jobs.JobsIn(142));
        Assert.True(jobs.JobColumns >= 44, "sheet has a column per class/job including BST (43)");

        // Cross-check the raw column mapping against the generated struct's named properties.
        var typed = fixture.Game.Excel.GetSheet<Lumina.Excel.Sheets.ClassJobCategory>().GetRow(142);
        Assert.Equal(typed.PLD, jobs.Admits(142, 19));
        Assert.Equal(typed.CRP, jobs.Admits(142, 8));
        Assert.Equal(typed.BLU, jobs.Admits(142, 36));
    }

    [GameDataFact]
    public void Game_names_resolve_from_their_sheets()
    {
        var names = fixture.Bundle.Names;
        Assert.Equal("A Realm Reborn", names.Expansion(0));
        Assert.Equal("Dawntrail", names.Expansion(5));
        Assert.Equal("Maelstrom", names.GrandCompany(1));
        Assert.Equal("Amalj'aa", names.Tribe(1));
        Assert.Equal("PLD", names.ClassJobAbbreviation(19));
        Assert.Equal("Paladin", names.ClassJob(19));
        Assert.Equal("Black Mage", names.ClassJob(25));
        Assert.Equal(string.Empty, names.Tribe(0));
        Assert.NotEmpty(names.TribeRanks);
    }

    [GameDataFact]
    public void Accept_conditions_drop_empty_slots()
    {
        var quest = Catalog.GetByRowId(65961u);
        Assert.NotNull(quest);
        Assert.Equal([66031u], quest.AcceptConditions);
        Assert.Empty(Catalog.GetByRowId(65621u)!.AcceptConditions);
        Assert.All(Catalog.All, q => Assert.DoesNotContain(0u, q.AcceptConditions));
    }

    [GameDataFact]
    public void Removed_quests_never_share_a_journal_node_with_listed_ones()
    {
        var unlisted = Catalog.All.Where(q => q.IsUnlisted).ToArray();
        Assert.NotEmpty(unlisted);
        Assert.All(unlisted, q => Assert.Equal(0u, q.Journal.GenreId));
        Assert.All(unlisted, q => Assert.Equal(string.Empty, q.Journal.CategoryName));

        // Whatever section or category ids the sheet gives them, the tree never counts them under a journal node.
        var states = Catalog.All.ToDictionary(q => q.RowId, _ => QuestState.Ready);
        var counts = Tsukimichi.Core.Query.TreeCounts.Compute(Catalog, states, includeUnlisted: true);
        var listedInSection0 = Catalog.All.Count(q => !q.IsRemoved && q.Journal.SectionId == 0);
        Assert.Equal(listedInSection0, counts.Section(0).Total);
        Assert.Equal(Catalog.Removed.Count, counts.Unlisted.Total);
        Assert.Equal(ExpectedCounts.Retired, Catalog.Removed.Count);
    }

    [GameDataFact]
    public void Currency_rewards_map_to_Other_not_Item()
    {
        // A currency reward is the only Other-kind reward that carries an item row, so its presence proves the
        // mapping; Item-kind rewards must never carry the low row ids the Item sheet reserves for currencies.
        var currencies = Catalog.All.SelectMany(q => q.Rewards).Where(r => r.Kind == RewardKind.Other && r.ItemId != 0).ToArray();
        Assert.NotEmpty(currencies);
        Assert.All(currencies, r => Assert.Equal(r.Id, r.ItemId));
        Assert.All(currencies, r => Assert.False(string.IsNullOrEmpty(r.Name)));
        Assert.DoesNotContain(Catalog.All.SelectMany(q => q.Rewards), r => r.Kind == RewardKind.Item && r.ItemId is > 0 and < 20);
    }

    [GameDataFact]
    public void Sheet_names_title_case_at_load()
    {
        // Mount 6 is "magitek armor" in the sheet; the same helper the unique-reward loader applies gives the display name.
        var mount = fixture.Game.Excel.GetSheet<Lumina.Excel.Sheets.Mount>().GetRow(6);
        Assert.Equal("magitek armor", mount.Singular.ExtractText());
        Assert.Equal("Magitek Armor", NameCase.Title(mount.Singular.ExtractText()));

        var conjurer = Catalog.GetByRowId(65558u);
        Assert.NotNull(conjurer);
        Assert.Equal("Conjurer", Assert.Single(conjurer.Rewards, r => r.Kind == RewardKind.ClassJob).Name);
    }

    [GameDataFact]
    public void Cancelled_token_stops_the_build()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => CatalogMapper.Map(fixture.Game.Excel, Language.English, cts.Token));
    }
    [GameDataFact]
    public void Class_job_infos_carry_parent_unlock_quest_roles_and_disciplines()
    {
        var names = fixture.Bundle.Names;
        var jobs = fixture.Bundle.Jobs;
        var paladin = names.ClassJobInfo(19);
        Assert.NotNull(paladin);
        Assert.Equal("Paladin", paladin.Name);
        Assert.Equal("PLD", paladin.Abbreviation);
        Assert.Equal(1u, paladin.ParentRowId);
        Assert.Equal(66591u, paladin.UnlockQuestRowId);
        Assert.Equal(1, paladin.Role);
        Assert.False(paladin.IsCrafter);
        Assert.False(paladin.IsGatherer);

        // A job shares its class's level slot, which is why hiding the class once the job is unlocked loses nothing.
        var gladiator = names.ClassJobInfo(1);
        Assert.NotNull(gladiator);
        Assert.Equal(1u, gladiator.ParentRowId);
        Assert.True(gladiator.ExpArrayIndex >= 0);
        Assert.Equal(gladiator.ExpArrayIndex, paladin.ExpArrayIndex);

        var pictomancer = names.ClassJobInfo(42);
        Assert.NotNull(pictomancer);
        Assert.False(string.IsNullOrEmpty(pictomancer.Name));
        Assert.Equal("PCT", pictomancer.Abbreviation);
        Assert.Equal(42u, pictomancer.ParentRowId);
        Assert.Equal(3, pictomancer.Role);
        Assert.True(jobs.Admits(31, 42), "31 (Disciples of Magic) admits PCT");

        var bard = names.ClassJobInfo(23);
        Assert.NotNull(bard);
        Assert.Equal(3, bard.Role);
        Assert.False(jobs.Admits(31, 23), "31 (Disciples of Magic) does not admit BRD");

        var carpenter = names.ClassJobInfo(8);
        Assert.NotNull(carpenter);
        Assert.True(carpenter.IsCrafter);
        Assert.False(carpenter.IsGatherer);
        Assert.Equal(0, carpenter.Role);

        var miner = names.ClassJobInfo(16);
        Assert.NotNull(miner);
        Assert.True(miner.IsGatherer);
        Assert.False(miner.IsCrafter);

        // Rows 44 and 45 are empty placeholders whose exp slot is 0 (Gladiator's); they must not become jobs.
        Assert.All(names.ClassJobInfos, i => Assert.False(string.IsNullOrEmpty(i.Name), $"row {i.RowId} has no name"));
        Assert.DoesNotContain(names.ClassJobInfos, i => i.RowId is 44 or 45);
        Assert.Null(names.ClassJobInfo(44));
        Assert.Equal(string.Empty, names.ClassJob(44));
        Assert.Equal(62119u, paladin.IconId);
    }
}
