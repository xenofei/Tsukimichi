using Lumina.Data;
using Lumina.Data.Files;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Triad;
using Tsukimichi.GameData;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>Reads the Triple Triad opponents once per test class from the game, with the event layouts, as the plugin's warmer does.</summary>
public sealed class TriadFixture : IDisposable
{
    private readonly UnlockIndexFixture unlocks = new();
    private readonly Lazy<TriadOpponents> built;

    public TriadFixture()
    {
        built = new Lazy<TriadOpponents>(
            () => TriadReader.Build(unlocks.Game.Excel, Language.English, path => unlocks.Game.GetFile<LgbFile>(path)),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public TriadOpponents Opponents => built.Value;

    public UnlockIndexFixture Unlocks => unlocks;

    public QuestCatalog Catalog => unlocks.Catalog;

    public void Dispose() => unlocks.Dispose();
}

/// <summary>
/// The Triple Triad opponents (plan v7, 1.21.0 P6) against the game's own sheets: how many the sheets place on an NPC
/// and how many wait on a quest (the spec's 142 and 106), a known opponent's gate, place and cards.
/// </summary>
public sealed class TriadGameDataTests(TriadFixture fixture, ITestOutputHelper output) : IClassFixture<TriadFixture>
{
    [GameDataFact]
    public void Probe()
    {
        var index = fixture.Opponents;
        output.WriteLine($"{index.Count} opponents, {index.QuestGatedCount} gated, {index.WatchedCards.Count} cards, unplaced {index.All.Count(o => o.Spot is null)}");
        foreach (var o in index.All)
        {
            var gate = string.Join(o.Gate.Join == JoinKind.Any ? " | " : " & ", o.Gate.QuestIds.Select(id => fixture.Catalog.GetByRowId(id)?.Name ?? id.ToString()));
            output.WriteLine($"{o.ResidentId} npc {o.NpcId} {o.Name} @ {o.Zone} ({o.Spot?.MapX:0.0},{o.Spot?.MapY:0.0}) gate [{gate}] cards {string.Join(",", o.Cards)}");
        }
    }
}
