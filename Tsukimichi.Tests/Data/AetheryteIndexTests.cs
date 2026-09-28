using Tsukimichi.GameData;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

public class AetheryteIndexTests
{
    private const uint Gridania = 132;
    private const uint OldGridania = 133;
    private const uint Bentbranch = 148;

    private static AetheryteIndex Index() => AetheryteIndex.From(
        [
            new AetheryteInfo(2, Gridania, "New Gridania", 30f, 30f),
            new AetheryteInfo(3, Bentbranch, "Bentbranch Meadows", 0f, 0f),
            new AetheryteInfo(4, Bentbranch, "The Hawthorne Hut", 300f, -200f),
        ],
        [new KeyValuePair<uint, uint>(OldGridania, 2)]);

    [Fact]
    public void Nearest_picks_the_closest_aetheryte_in_the_territory()
    {
        var index = Index();

        Assert.Equal(4u, index.Nearest(Bentbranch, 250f, -150f)!.RowId);
        Assert.Equal(3u, index.Nearest(Bentbranch, 10f, 10f)!.RowId);
        Assert.Equal(2, index.InTerritory(Bentbranch).Count);
    }

    [Fact]
    public void Nearest_falls_back_to_the_territory_default_and_null_otherwise()
    {
        var index = Index();

        Assert.Equal(2u, index.Nearest(OldGridania, 0f, 0f)!.RowId);
        Assert.Null(index.Nearest(999, 0f, 0f));
        Assert.Empty(index.InTerritory(999));
        Assert.Null(AetheryteIndex.Empty.Nearest(Gridania, 0f, 0f));
    }

    [Fact]
    public void ToRaw_inverts_the_map_coordinate_scale()
    {
        // Pixel 1024 is the raw origin on an unscaled, unoffset map; a 200 % map halves the raw span.
        Assert.Equal(0f, AetheryteIndex.ToRaw(1024f, 0, 100));
        Assert.Equal(100f, AetheryteIndex.ToRaw(1124f, 0, 100));
        Assert.Equal(50f, AetheryteIndex.ToRaw(1124f, 0, 200));
        Assert.Equal(-30f, AetheryteIndex.ToRaw(1024f, 30, 100));
        Assert.Equal(100f, AetheryteIndex.ToRaw(1124f, 0, 0));
    }

    [Fact]
    public void From_with_nothing_is_empty()
    {
        Assert.Same(AetheryteIndex.Empty, AetheryteIndex.From([]));
        Assert.Empty(AetheryteIndex.Empty.All);
    }
}

public class AetheryteIndexGameDataTests(GameDataFixture fixture, ITestOutputHelper output) : IClassFixture<GameDataFixture>
{
    [GameDataFact]
    public void Builds_from_the_sheets_and_finds_the_giver_of_close_to_home()
    {
        var index = AetheryteIndex.Build(fixture.Game.Excel, Lumina.Data.Language.English);
        output.WriteLine($"{index.All.Count} teleportable aetherytes");
        Assert.InRange(index.All.Count, 90, 400);
        Assert.All(index.All, a => Assert.False(string.IsNullOrWhiteSpace(a.Name), $"aetheryte {a.RowId} has no name"));

        // Close to Home starts in New Gridania (territory 132) a stone's throw from the aetheryte plaza (row 2).
        var quest = fixture.Bundle.Catalog.GetByRowId(65621u);
        Assert.NotNull(quest?.Issuer);
        var nearest = index.Nearest(quest.Issuer.TerritoryId, quest.Issuer.X, quest.Issuer.Z);
        Assert.NotNull(nearest);
        output.WriteLine($"Close to Home: territory {quest.Issuer.TerritoryId} -> aetheryte {nearest.RowId} {nearest.Name}");
        Assert.Equal(2u, nearest.RowId);
        Assert.InRange(MathF.Sqrt((nearest.X - quest.Issuer.X) * (nearest.X - quest.Issuer.X) + (nearest.Z - quest.Issuer.Z) * (nearest.Z - quest.Issuer.Z)), 0f, 150f);

        // Old Gridania (133) holds no aetheryte of its own; the TerritoryType row points at Gridania's.
        Assert.Empty(index.InTerritory(133));
        Assert.Equal(2u, index.Nearest(133, 0f, 0f)!.RowId);
    }
}
