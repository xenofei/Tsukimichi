using Lumina;
using Lumina.Data;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Unique;
using Tsukimichi.GameData;
using Xunit.Abstractions;
using LuminaGameData = Lumina.GameData;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The catalog follows the client's language (V2-19): the plugin maps it with <c>IDataManager.Language</c>, so a
/// Japanese, German or French client reads every quest, place, NPC, job and duty name from its own sheets. Mapped here
/// over the real game data in each language: the same quests with the same filing, names in the language, and the
/// language-independent logic (role quests, job ladders) finding the same quests.
/// </summary>
public class CatalogLanguageTests(ITestOutputHelper output)
{
    private const uint ComingToGridania = 65575;
    private const byte Heavensward = 1;

    private static readonly Lazy<(LuminaGameData Game, Dictionary<Language, CatalogBundle> Bundles)> Mapped = new(Map, LazyThreadSafetyMode.ExecutionAndPublication);

    [GameDataFact]
    public void Every_language_maps_the_same_quests_into_the_same_journal()
    {
        var english = Mapped.Value.Bundles[Language.English].Catalog;
        foreach (var (language, bundle) in Mapped.Value.Bundles)
        {
            var catalog = bundle.Catalog;
            output.WriteLine($"{language}: {catalog.Count} quests, \"{catalog.GetByRowId(ComingToGridania)?.Name}\", {bundle.Names.Expansion(Heavensward)}");
            Assert.Equal(language.ToString(), bundle.Language);
            Assert.Equal(english.Count, catalog.Count);
            foreach (var quest in english.All)
            {
                var other = catalog.GetByRowId(quest.RowId);
                Assert.NotNull(other);
                Assert.Equal(quest.Journal.GenreId, other.Journal.GenreId);
                Assert.Equal(quest.Level, other.Level);
            }
        }
    }

    [GameDataFact]
    public void Names_come_in_the_client_language()
    {
        var bundles = Mapped.Value.Bundles;
        var english = bundles[Language.English];
        Assert.Equal("Coming to Gridania", english.Catalog.GetByRowId(ComingToGridania)!.Name);
        foreach (var language in new[] { Language.Japanese, Language.German, Language.French })
        {
            var bundle = bundles[language];
            Assert.NotEqual(english.Catalog.GetByRowId(ComingToGridania)!.Name, bundle.Catalog.GetByRowId(ComingToGridania)!.Name);
        }

        // Expansion names are proper names in German and French ("Heavensward" either way); Japanese has its own.
        Assert.NotEqual(english.Names.Expansion(Heavensward), bundles[Language.Japanese].Names.Expansion(Heavensward));

        Assert.Contains("グリダニア", bundles[Language.Japanese].Catalog.GetByRowId(ComingToGridania)!.Name, StringComparison.Ordinal);
    }

    [GameDataFact]
    public void Role_quests_are_found_in_every_language()
    {
        // JobLadder.IsRoleQuest read the English genre name before V2-19; the journal category is language-free.
        var expected = Mapped.Value.Bundles[Language.English].Catalog.All.Count(JobLadder.IsRoleQuest);
        Assert.True(expected > 50, $"expected the role quests, found {expected}");
        foreach (var (language, bundle) in Mapped.Value.Bundles)
        {
            Assert.Equal(expected, bundle.Catalog.All.Count(JobLadder.IsRoleQuest));
            Assert.All(bundle.Catalog.All.Where(JobLadder.IsRoleQuest), static q => Assert.Equal(JobLadder.RoleQuestsCategory, q.Journal.CategoryId));
        }
    }

    [GameDataFact]
    public void Moonlit_reward_names_follow_the_catalog_language()
    {
        var rewards = Tsukimichi.Core.Storage.UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        var japanese = Mapped.Value.Bundles[Language.Japanese];
        var english = Mapped.Value.Bundles[Language.English];
        var localized = 0;
        var total = 0;
        foreach (var entry in rewards.Entries.Where(static e => e.ItemId != 0).Take(200))
        {
            total++;
            var name = RewardNames.Display(entry, japanese.Catalog.GetByRowId(entry.QuestRowId), japanese.Language);
            Assert.Equal(entry.RewardName, RewardNames.Display(entry, english.Catalog.GetByRowId(entry.QuestRowId), english.Language));
            if (name != entry.RewardName)
            {
                localized++;
            }
        }

        output.WriteLine($"{localized} of {total} item rewards named from the Japanese sheets");
        Assert.True(localized > total / 2, $"only {localized} of {total} item rewards took the Japanese name");
    }

    private static (LuminaGameData, Dictionary<Language, CatalogBundle>) Map()
    {
        var path = Environment.GetEnvironmentVariable(GameDataFactAttribute.EnvVar)
                   ?? throw new InvalidOperationException($"{GameDataFactAttribute.EnvVar} is not set");
        var game = new LuminaGameData(path, new LuminaOptions { DefaultExcelLanguage = Language.English, PanicOnSheetChecksumMismatch = false });
        var curated = Tsukimichi.Core.Storage.CuratedData.Load(FixtureCatalog.CuratedDir());
        var bundles = new Dictionary<Language, CatalogBundle>();
        foreach (var language in new[] { Language.English, Language.Japanese, Language.German, Language.French })
        {
            bundles[language] = CatalogMapper.Map(game.Excel, language, curated: curated);
        }

        return (game, bundles);
    }
}
