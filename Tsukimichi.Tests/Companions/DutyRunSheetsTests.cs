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

    /// <summary>C7: the clear badges and the walls read from the sheets for duties players know.</summary>
    [GameDataFact]
    public void How_you_will_clear_it_reads_from_the_sheets()
    {
        // Sastasha: Duty Support or the Duty Finder, level 15, no item level.
        var sastasha = fixture.Index.ByCondition(4)!;
        Assert.Equal(DutyClearWays.DutySupport | DutyClearWays.DutyFinder, DutyClear.Ways(sastasha));
        Assert.Equal(15, sastasha.LevelRequired);
        Assert.Equal(0, sastasha.ItemLevelRequired);
        Assert.Equal(4, sastasha.Players);
        Assert.True(sastasha.Roulettes.HasFlag(DutyRoulettes.Leveling));

        // Holminster Switch: both NPC windows.
        Assert.Equal(DutyClearWays.DutySupport | DutyClearWays.Trust | DutyClearWays.DutyFinder, DutyClear.Ways(fixture.Index.ByCondition(676)!));

        // The Bowl of Embers (Hard): players only, through the Duty Finder.
        var ifritHard = fixture.Index.ByCondition(59)!;
        Assert.Equal(DutyClearWays.DutyFinder, DutyClear.Ways(ifritHard));
        Assert.False(DutyClear.WithoutOthers(ifritHard));

        // The Ultimate raids are not matched by the Duty Finder: a full party enters together.
        var ucob = fixture.Index.ByCondition(280)!;
        output.WriteLine($"{ucob.Name}: {DutyClear.Ways(ucob)}, {ucob.Players} players");
        Assert.Equal(DutyClearWays.PartyOnly, DutyClear.Ways(ucob));
        Assert.Equal(8, ucob.Players);

        // Dawntrail's level-cap dungeons ask an item level.
        var cap = fixture.Index.All.Where(d => d.Roulettes.HasFlag(DutyRoulettes.LevelCap)).ToArray();
        Assert.NotEmpty(cap);
        Assert.All(cap, d => Assert.True(d.ItemLevelRequired > 600, $"{d.Name} asks i{d.ItemLevelRequired}"));
    }

    /// <summary>N4: the roulettes, mapped from their rows, with the open rule the board reads.</summary>
    [GameDataFact]
    public void The_roulettes_map_to_their_columns()
    {
        var roulettes = fixture.Index.Roulettes;
        foreach (var r in roulettes)
        {
            output.WriteLine($"{r.Id} {r.Name} ({r.ShortName}) every={r.RequiresEveryDuty} lvl={r.RequiredLevel} ex={r.RequiredExpansion} duties={fixture.Index.All.Count(d => (d.Roulettes & r.Flag) != 0)}");
        }

        Assert.Equal(DutyRunSheets.RouletteRows.Count, roulettes.Count);
        string Short(DutyRoulettes flag) => Assert.Single(roulettes, r => r.Flag == flag).ShortName;
        Assert.Equal("Leveling", Short(DutyRoulettes.Leveling));
        Assert.Equal("High-level Dungeons", Short(DutyRoulettes.HighLevel));
        Assert.Equal("Main Scenario", Short(DutyRoulettes.MainScenario));
        Assert.Equal("Guildhests", Short(DutyRoulettes.Guildhests));
        Assert.Equal("Expert", Short(DutyRoulettes.Expert));
        Assert.Equal("Trials", Short(DutyRoulettes.Trials));
        Assert.Equal("Level Cap Dungeons", Short(DutyRoulettes.LevelCap));
        Assert.Equal("Mentor", Short(DutyRoulettes.Mentor));
        Assert.Equal("Alliance Raids", Short(DutyRoulettes.AllianceRaids));
        Assert.Equal("Normal Raids", Short(DutyRoulettes.NormalRaids));

        // The open rule: Expert and Level Cap ask for every duty (the "why is my roulette locked" case), Leveling does not.
        Assert.True(roulettes.Single(r => r.Flag == DutyRoulettes.LevelCap).RequiresEveryDuty);
        Assert.True(roulettes.Single(r => r.Flag == DutyRoulettes.Expert).RequiresEveryDuty);
        Assert.False(roulettes.Single(r => r.Flag == DutyRoulettes.Leveling).RequiresEveryDuty);
        Assert.Equal(16, roulettes.Single(r => r.Flag == DutyRoulettes.Leveling).RequiredLevel);

        // Every roulette draws from duties, and the Main Scenario roulette from its three.
        Assert.All(roulettes, r => Assert.Contains(fixture.Index.All, d => (d.Roulettes & r.Flag) != 0));
        Assert.Equal(3, fixture.Index.All.Count(d => d.Roulettes.HasFlag(DutyRoulettes.MainScenario)));

        // The board reads every roulette duty's records.
        var watched = DutyBoard.Watched(fixture.Index);
        Assert.Contains(fixture.Index.ByCondition(4)!.InstanceContentId, watched);
        Assert.True(watched.Length > 300, $"only {watched.Length} duties watched");
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
