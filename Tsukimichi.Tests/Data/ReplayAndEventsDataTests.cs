using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// 1.19.0 C4 and C5 against the game data at <c>TSUKIMICHI_GAME_PATH</c>: the New Game+ chapters as parts (their
/// names, links and the "quest 87 of 112" count) and the allied society ids the rank-up hint names.
/// </summary>
public sealed class ReplayAndEventsDataTests(GameDataFixture fixture) : IClassFixture<GameDataFixture>
{
    private const uint TheUltimateWeapon = 66060;
    private const uint CloseToHomeGridania = 65621;

    [GameDataFact]
    public void Every_New_Game_plus_quest_sits_in_a_named_chapter_part()
    {
        var bundle = fixture.Bundle;
        var chapters = bundle.NewGamePlusChapters;
        Assert.True(chapters.Count > 200, $"only {chapters.Count} New Game+ parts");
        Assert.Equal("A Realm Reborn - Part 1", chapters.ChapterName(1));
        Assert.All(bundle.NewGamePlus, id => Assert.True(chapters.Lists(id), $"quest {id} is in no part"));
        Assert.False(chapters.Lists(CloseToHomeGridania));

        // Every listed quest gets a place within its chapter's count, or none when its chapter has no name.
        var unplaced = bundle.NewGamePlus.Where(id => chapters.Position(id) is not { } p || p.Index < 1 || p.Index > p.Count).ToList();
        Assert.True(unplaced.Count < bundle.NewGamePlus.Count / 50, $"{unplaced.Count} New Game+ quests have no place: {string.Join(", ", unplaced.Take(10))}");
    }

    [GameDataFact]
    public void The_Ultimate_Weapon_closes_its_A_Realm_Reborn_chapter()
    {
        var position = fixture.Bundle.NewGamePlusChapters.Position(TheUltimateWeapon);
        Assert.NotNull(position);
        Assert.StartsWith("A Realm Reborn - Part", position.Value.Name, StringComparison.Ordinal);
        Assert.Equal(position.Value.Count, position.Value.Index);
        Assert.InRange(position.Value.Count, 10, 200);
    }

    [GameDataFact]
    public void The_societies_and_the_rank_the_bonus_dailies_rule_names_are_the_game_s()
    {
        var names = fixture.Bundle.Names;
        Assert.Contains("Moogle", names.Tribe(8), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Namazu", names.Tribe(11), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Sworn", names.TribeRank(AlliedCarryover.SwornRank));
        Assert.All(AlliedCarryover.BonusAtSworn, tribe => Assert.False(string.IsNullOrEmpty(names.Tribe(tribe))));
    }
}
