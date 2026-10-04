using Lumina.Data;
using Lumina.Data.Files;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
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
/// and how many wait on a quest (the spec's 142 and 106), a known opponent's gate, place and cards, an any join and an
/// all join, and the spoiler shield over an opponent past the story point.
/// </summary>
public sealed class TriadGameDataTests(TriadFixture fixture, ITestOutputHelper output) : IClassFixture<TriadFixture>
{
    private const uint Elaisse = 2293806;
    private const uint Swift = 2293788;
    private const uint Laniaitte = 2293808;
    private const uint Celia = 2293879;

    private TriadOpponent Opponent(uint residentId) => Assert.Single(fixture.Opponents.All, o => o.ResidentId == residentId);

    private string QuestName(uint rowId) => fixture.Catalog.GetByRowId(rowId)?.Name.Trim() ?? rowId.ToString(System.Globalization.CultureInfo.InvariantCulture);

    [GameDataFact]
    public void The_sheets_place_142_opponents_and_106_wait_on_a_quest()
    {
        var index = fixture.Opponents;
        output.WriteLine($"{index.Count} opponents, {index.QuestGatedCount} behind a quest, {index.WatchedCards.Count} cards, {index.All.Count(o => o.Spot is null)} unplaced");
        Assert.Equal(142, index.Count);
        Assert.Equal(106, index.QuestGatedCount);

        // Only the eight tournament copies of the Battlehall and Gold Saucer regulars stand nowhere; none hands out a card.
        var unplaced = index.All.Where(o => o.Spot is null).ToArray();
        Assert.Equal(8, unplaced.Length);
        Assert.All(unplaced, o => Assert.Empty(o.Cards));

        // Every gate quest is a quest of the catalog.
        Assert.All(index.All.SelectMany(o => o.Gate.QuestIds), id => Assert.NotNull(fixture.Catalog.GetByRowId(id)));
    }

    [GameDataFact]
    public void Elaisse_plays_after_Caught_in_the_Act_in_The_Pillars()
    {
        var elaisse = Opponent(Elaisse);
        Assert.Equal("Elaisse", elaisse.Name);
        Assert.Equal("The Pillars", elaisse.Zone);
        Assert.Equal(["Caught in the Act"], elaisse.Gate.QuestIds.Select(QuestName));
        Assert.Equal([102u, 142u], elaisse.Cards);
        Assert.Contains(elaisse, fixture.Opponents.OpenedBy(elaisse.Gate.QuestIds[0]));

        var celia = Opponent(Celia);
        Assert.Equal("Old Sharlayan", celia.Zone);
        Assert.Equal(["Endwalker"], celia.Gate.QuestIds.Select(QuestName));
    }

    [GameDataFact]
    public void Swift_plays_after_any_Grand_Company_quest_and_Laniaitte_after_two_quests()
    {
        var swift = Opponent(Swift);
        Assert.Equal(JoinKind.Any, swift.Gate.Join);
        Assert.Equal(3, swift.Gate.QuestIds.Length);

        var laniaitte = Opponent(Laniaitte);
        Assert.Equal(JoinKind.All, laniaitte.Gate.Join);
        Assert.Equal(["An Engineering Enterprise", "Honoring the Past"], laniaitte.Gate.QuestIds.Select(QuestName));
    }

    [GameDataFact]
    public void An_opponent_past_the_story_point_is_masked_on_the_card()
    {
        var catalog = fixture.Catalog;
        var story = MsqGraph.For(catalog).Story;
        var firstEndwalker = story.Select((q, i) => (q, i)).First(p => p.q.Expansion == 4).i;
        var states = new Dictionary<uint, QuestState>();
        foreach (var quest in catalog.All)
        {
            states[quest.RowId] = QuestState.Blocked;
        }

        for (var i = 0; i < story.Count; i++)
        {
            states[story[i].RowId] = i < firstEndwalker ? QuestState.Completed : i == firstEndwalker ? QuestState.Ready : QuestState.Blocked;
        }

        var spoilers = SpoilerMask.Build(catalog, states, SpoilerOptions.Default, names: fixture.Unlocks.Unlocks.Names);
        var evaluations = states.ToDictionary(kv => kv.Key, kv => new QuestEvaluation(kv.Value, [], null, null, null));
        var board = TriadBoard.Build(fixture.Opponents, catalog, evaluations, null, spoilers.IsMasked, zone => spoilers.IsNameMasked(SpoilerKind.Area, zone));
        output.WriteLine($"plays you {board.PlaysYou.Count}, locked {board.Locked.Count} ({board.Locked.Count(r => r.Masked)} masked)");

        Assert.Contains(board.Locked, r => r.Opponent.ResidentId == Celia && r.Masked);
        Assert.Contains(board.PlaysYou.Concat(board.Locked), r => r.Opponent.ResidentId == Elaisse && !r.Masked);

        // Masked rows come last.
        Assert.Equal(board.Locked.OrderBy(r => r.Masked).Select(r => r.Opponent.ResidentId), board.Locked.Select(r => r.Opponent.ResidentId));
    }
}
