using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;
using Tsukimichi.GameData;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The per-job reward table (QuestClassJobReward) through <see cref="ClassJobRewardItems"/>: only equipment is artifact
/// gear in the catalog, and the crystals, Cordials and society currencies it also pays are ordinary item rewards.
/// </summary>
public class ClassJobRewardTests(GameDataFixture fixture) : IClassFixture<GameDataFixture>
{
    /// <summary>A Namazu crafter daily (Dhoro Iloh), paid through the per-job table.</summary>
    private const uint TheImportanceOfBeingYesYes = 68639;

    [GameDataFact]
    public void Every_artifact_gear_reward_is_equipment()
    {
        var items = fixture.Game.Excel.GetSheet<Item>();
        var gear = fixture.Bundle.Catalog.All.SelectMany(q => q.Rewards).Where(r => r.Kind == RewardKind.ArtifactGear).ToList();

        Assert.NotEmpty(gear);
        Assert.All(gear, r => Assert.True(
            items.GetRowOrDefault(r.ItemId) is { } item && ClassJobRewardItems.IsArtifactGear(in item),
            $"{r.Name} ({r.ItemId}) is filed as artifact gear but has no equip slot"));
    }

    [GameDataFact]
    public void A_Namazu_crafter_quest_pays_crystals_as_items_not_artifact_gear()
    {
        var quest = fixture.Bundle.Catalog.GetByRowId(TheImportanceOfBeingYesYes);
        Assert.NotNull(quest);
        Assert.Equal(11, quest.BeastTribe);

        Assert.DoesNotContain(quest.Rewards, r => r.Kind == RewardKind.ArtifactGear);
        var crystals = quest.Rewards.Where(r => r.Name.EndsWith(" Crystal", StringComparison.Ordinal)).ToList();
        Assert.Equal(6, crystals.Count);
        Assert.All(crystals, r => Assert.Equal((RewardKind.Item, r.ItemId, 20u), (r.Kind, r.Id, r.Count)));
        Assert.Contains(quest.Rewards, r => r.Kind == RewardKind.Item && r.Name == "Cordial");
        Assert.Contains(quest.Rewards, r => r.Kind == RewardKind.Item && r.Name == "Namazu Koban");
    }
}
