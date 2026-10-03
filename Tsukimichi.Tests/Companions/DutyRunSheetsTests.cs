using Lumina;
using Lumina.Data;
using Tsukimichi.Core.Companions;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Data;
using Xunit.Abstractions;
using LuminaGameData = Lumina.GameData;

namespace Tsukimichi.Tests.Companions;

/// <summary>Opens the game data once per test class and builds the duty index; lazy so skipped runs never touch the disk.</summary>
public sealed class DutyRunFixture : IDisposable
{
    private readonly Lazy<(LuminaGameData Game, DutyRunIndex Index)> built = new(Build, LazyThreadSafetyMode.ExecutionAndPublication);

    public DutyRunIndex Index => built.Value.Index;

    public LuminaGameData Game => built.Value.Game;

    public void Dispose()
    {
        if (built.IsValueCreated)
        {
            built.Value.Game.Dispose();
        }
    }

    private static (LuminaGameData, DutyRunIndex) Build()
    {
        var path = Environment.GetEnvironmentVariable(GameDataFactAttribute.EnvVar)
                   ?? throw new InvalidOperationException($"{GameDataFactAttribute.EnvVar} is not set");
        var game = new LuminaGameData(path, new LuminaOptions
        {
            DefaultExcelLanguage = Language.English,
            PanicOnSheetChecksumMismatch = false,
        });
        return (game, DutyRunSheets.Build(game.Excel, Language.English));
    }
}

/// <summary>
/// <see cref="DutyRunSheets"/> against the game data: the ContentFinderCondition → TerritoryType mapping AutoDuty's
/// gates take, the InstanceContent link the quest data names, and the Duty Support and Trust flags "Run with AutoDuty"
/// picks its queue from.
/// </summary>
public sealed class DutyRunSheetsTests(DutyRunFixture fixture, ITestOutputHelper output) : IClassFixture<DutyRunFixture>
{
    [GameDataTheory]
    [InlineData(4u, 4u, 1036u, "Sastasha")]
    [InlineData(2u, 2u, 1037u, "the Tam-Tara Deepcroft")]
    [InlineData(16u, 86u, 1044u, "the Praetorium")]
    [InlineData(56u, 20001u, 1045u, "the Bowl of Embers")]
    [InlineData(676u, 72u, 837u, "Holminster Switch")]
    [InlineData(783u, 78u, 952u, "the Tower of Zot")]
    public void A_duty_maps_to_its_territory_and_instance(uint condition, uint instance, uint territory, string name)
    {
        var duty = fixture.Index.ByCondition(condition);
        Assert.NotNull(duty);
        output.WriteLine(duty.ToString());
        Assert.Equal(instance, duty.InstanceContentId);
        Assert.Equal(territory, duty.TerritoryTypeId);
        Assert.Equal(name, duty.Name);

        // The quest data names the instance (InstanceContentRequired); the index finds the same entry from it.
        Assert.Same(duty, fixture.Index.ByInstance(instance));
    }

    [GameDataFact]
    public void Duty_Support_and_Trust_follow_the_DawnContent_sheet()
    {
        // A Realm Reborn: Duty Support only.
        var sastasha = fixture.Index.ByCondition(4)!;
        Assert.True(sastasha.OffersDutySupport);
        Assert.False(sastasha.OffersTrust);
        Assert.Equal(AutoDutyMode.Support, AutoDutyPlan.ModeFor(sastasha, allowDutyFinder: false));

        // Shadowbringers on: both; Duty Support is preferred.
        var holminster = fixture.Index.ByCondition(676)!;
        Assert.True(holminster.OffersDutySupport);
        Assert.True(holminster.OffersTrust);
        Assert.Equal(AutoDutyMode.Support, AutoDutyPlan.ModeFor(holminster, allowDutyFinder: false));

        // A hard-mode dungeon has neither: the regular Duty Finder only, and only when allowed.
        var brayfloxHard = fixture.Index.ByCondition(20)!;
        Assert.False(brayfloxHard.OffersDutySupport);
        Assert.False(brayfloxHard.OffersTrust);
        Assert.Equal(AutoDutyMode.None, AutoDutyPlan.ModeFor(brayfloxHard, allowDutyFinder: false));
        Assert.Equal(AutoDutyMode.Regular, AutoDutyPlan.ModeFor(brayfloxHard, allowDutyFinder: true));
    }

    [GameDataFact]
    public void Every_dungeon_has_a_territory_and_the_index_is_large()
    {
        Assert.True(fixture.Index.Count > 500, $"only {fixture.Index.Count} duties");
        Assert.All(new uint[] { 1, 2, 4, 15, 16 }, cfc => Assert.Equal(DutyRunInfo.Dungeons, fixture.Index.ByCondition(cfc)!.ContentTypeId));
    }

    /// <summary>
    /// The 1.6 to 1.10 bug: the plugin called <c>Build(DataManager.Excel)</c>, the language defaulted to None, Lumina
    /// refused the ContentFinderCondition sheet and the Duties section stayed hidden in game, while these tests passed
    /// English. The language now has no default, so leaving it out does not compile.
    /// </summary>
    [Fact]
    public void The_language_cannot_be_left_out()
    {
        var language = typeof(DutyRunSheets).GetMethod(nameof(DutyRunSheets.Build))!.GetParameters().Single(static p => p.ParameterType == typeof(Language));
        Assert.False(language.HasDefaultValue, "Build's language must be passed: ContentFinderCondition exists only per language");
    }

    [GameDataFact]
    public void No_language_is_refused_before_any_sheet_is_read()
    {
        var refused = Assert.Throws<ArgumentException>(() => DutyRunSheets.Build(fixture.Game.Excel, Language.None));
        Assert.Equal("language", refused.ParamName);
    }
}
