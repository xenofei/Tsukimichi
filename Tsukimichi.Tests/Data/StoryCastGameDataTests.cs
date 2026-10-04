using Lumina.Data;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Portraits;
using Tsukimichi.GameData;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// Who's in it (feature plan v7 N10) over the installed game: the recurring story characters read from the quest
/// scripts' actors and listeners (<see cref="QuestCastReader"/>), the curated aliases and blocks, and the "met" rule.
/// </summary>
public class StoryCastGameDataTests(GameDataFixture fixture, ITestOutputHelper output) : IClassFixture<GameDataFixture>
{
    private const uint InTheMiddleOfNowhere = 68791;
    private const uint TreasuredBonds = 70348;

    private StoryCast Cast => fixture.Bundle.Cast;

    [GameDataFact]
    public void The_scripts_give_about_a_hundred_recurring_characters_and_no_generic_names()
    {
        var cast = Cast;
        output.WriteLine($"{cast.Members.Count} recurring characters in {cast.QuestCount} quests");
        foreach (var member in cast.Members.Take(30))
        {
            output.WriteLine($"  {member.StoryQuests.Count,4}  {member.Name}");
        }

        Assert.InRange(cast.Members.Count, 80, 200);
        Assert.All(cast.Members, m => Assert.False(PortraitNames.IsGeneric(m.Key), $"{m.Key} is generic"));
        Assert.All(cast.Members, m => Assert.True(m.StoryQuests.Count >= StoryCast.MinStoryQuests));
        foreach (var name in new[] { "Alphinaud", "Alisaie", "Thancred", "Urianger", "Y'shtola", "Tataru", "Wuk Lamat" })
        {
            Assert.NotNull(cast.Find(name));
        }

        // An era name is a character of its own: the Crystal Exarch is never joined to the name the story reveals later.
        Assert.NotNull(cast.Find("Crystal Exarch"));
        Assert.NotNull(cast.Find("G'raha Tia"));
        Assert.NotSame(cast.Find("Crystal Exarch"), cast.Find("G'raha Tia"));
    }

    [GameDataFact]
    public void Side_quests_name_their_story_characters()
    {
        var cast = Cast;
        Assert.Contains("Thancred", cast.Of(InTheMiddleOfNowhere).Select(m => m.Key));
        Assert.Contains("Urianger", cast.Of(InTheMiddleOfNowhere).Select(m => m.Key));
        Assert.Contains("Tataru", cast.Of(TreasuredBonds).Select(m => m.Key));

        var sideQuests = fixture.Bundle.Catalog.All.Count(q => cast.HasCast(q.RowId) && !Core.Query.FeaturePresets.IsMainScenario(q));
        output.WriteLine($"{sideQuests} side quests feature a recurring story character");
        Assert.True(sideQuests >= 147, $"expected the spec's 147 side quests at least, found {sideQuests}");
    }

    [GameDataFact]
    public void A_character_is_met_only_after_a_completed_story_quest_that_features_them()
    {
        var cast = Cast;
        var thancred = cast.Find("Thancred")!;
        Assert.False(StoryCast.HasMet(thancred, _ => false));
        Assert.True(StoryCast.HasMet(thancred, id => id == thancred.StoryQuests[0]));

        var line = cast.Line(InTheMiddleOfNowhere, id => id == thancred.StoryQuests[0]);
        Assert.Contains(thancred, line.Met);
        Assert.Equal(cast.Of(InTheMiddleOfNowhere).Count - line.Met.Count, line.Familiar);
    }

    [GameDataFact]
    [Trait("Category", "Curated")]
    public void Curated_aliases_and_blocks_name_people_the_scripts_have()
    {
        var names = QuestCastReader.Read(fixture.Game.Excel, Language.English)
            .SelectMany(kv => kv.Value)
            .Select(n => n.Key)
            .ToHashSet(StringComparer.Ordinal);
        var curation = Core.Storage.CuratedData.Load(FixtureCatalog.CuratedDir()).StoryCast;
        foreach (var (alias, target) in curation.Aliases)
        {
            Assert.Contains(alias, names);
            Assert.Contains(target, names);
            Assert.NotEqual("Crystal Exarch", alias);
        }

        Assert.All(curation.Blocks, block => Assert.Contains(block, names));
    }
}
