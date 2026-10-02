using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.GameData;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The collector extras read from the game sheets (feature plan v5): the New Game+ chapters (<c>QuestRedo</c>,
/// <see cref="NewGamePlusQuests"/>) and the achievements that need several quests (<see cref="AchievementQuests.Ladders"/>).
/// Structure only, against the game data at <c>TSUKIMICHI_GAME_PATH</c>.
/// </summary>
public sealed class CollectorExtrasDataTests(GameDataFixture fixture) : IClassFixture<GameDataFixture>
{
    private const uint CloseToHomeGridania = 65621;
    private const uint HildibrandAgentOfEnquiry = 66038;
    private const uint TheUltimateWeapon = 66060;
    private const uint TalesOfWar = 314;

    [GameDataFact]
    public void Every_New_Game_plus_quest_is_a_catalog_quest()
    {
        var bundle = fixture.Bundle;
        Assert.True(bundle.NewGamePlus.Count > 2000, $"only {bundle.NewGamePlus.Count} quests in New Game+ chapters");
        Assert.All(bundle.NewGamePlus, id => Assert.NotNull(bundle.Catalog.GetByRowId(id)));
    }

    [GameDataFact]
    public void The_main_scenario_is_replayable_but_the_starting_classes_Close_to_Home()
    {
        var bundle = fixture.Bundle;
        var missing = MsqGraph.For(bundle.Catalog).Story.Where(q => !bundle.NewGamePlus.Contains(q.RowId)).ToList();

        // The eight "Close to Home" quests (one per starting class) are the only main scenario quests no chapter lists.
        Assert.Equal(8, missing.Count);
        Assert.Single(missing.Select(q => q.Name).Distinct());
        Assert.Contains(missing, q => q.RowId == CloseToHomeGridania);
    }

    [GameDataFact]
    public void The_badge_maps_replayable_once_only_and_repeatables()
    {
        var bundle = fixture.Bundle;
        var catalog = bundle.Catalog;
        Assert.Equal(ReplayKind.Replayable, NewGamePlus.Of(catalog.GetByRowId(TheUltimateWeapon)!, bundle.NewGamePlus));
        Assert.Equal(ReplayKind.Replayable, NewGamePlus.Of(catalog.GetByRowId(HildibrandAgentOfEnquiry)!, bundle.NewGamePlus));
        Assert.Equal(ReplayKind.OnceOnly, NewGamePlus.Of(catalog.GetByRowId(CloseToHomeGridania)!, bundle.NewGamePlus));

        // No New Game+ chapter replays a repeatable; it reads nothing rather than "once only".
        var daily = catalog.All.First(q => q.IsAlliedSocietyDaily && !q.IsRemoved);
        Assert.DoesNotContain(daily.RowId, bundle.NewGamePlus);
        Assert.Equal(ReplayKind.None, NewGamePlus.Of(daily, bundle.NewGamePlus));
    }

    [GameDataFact]
    public void The_achievements_that_need_several_quests_are_read_with_their_quests()
    {
        var bundle = fixture.Bundle;
        var ladders = bundle.AchievementLadders;
        Assert.InRange(ladders.Count, 1, 50);

        var tales = Assert.Single(ladders.All, l => l.AchievementId == TalesOfWar);
        Assert.Equal(5, tales.RowIds.Count);
        Assert.All(tales.RowIds, id => Assert.False(bundle.Catalog.GetByRowId(id)!.IsRemoved));
        Assert.All(tales.RowIds, id => Assert.Contains(tales, ladders.ForQuest(id)));

        // Every ladder names at least two live quests, none twice.
        Assert.All(ladders.All, l =>
        {
            Assert.True(l.RowIds.Count >= 2, l.Name);
            Assert.Equal(l.RowIds.Count, l.RowIds.Distinct().Count());
            Assert.All(l.RowIds, id => Assert.False(bundle.Catalog.GetByRowId(id)?.IsRemoved ?? true, $"{l.Name}: {id}"));
        });
    }
}
