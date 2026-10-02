using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;

namespace Tsukimichi.GameData;

/// <summary>
/// The one rule for an item of a QuestClassJobReward row (the per-job reward table, <c>ItemRewardType</c> 6), shared by
/// the catalog's rewards (<see cref="CatalogMapper"/>) and DataGen's unique-reward data. The table pays the relic,
/// Manderville, Skysteel and Splendorous weapons and tools, but also allied society crafter and gatherer quests in
/// crystals, Cordials and society currencies: only an item with an equip slot is artifact gear. Anything else is an
/// ordinary item reward in the catalog, and is no unique reward at all.
/// </summary>
public static class ClassJobRewardItems
{
    /// <summary>Whether the per-job reward item is gear (it has an equip slot), and so <see cref="RewardKind.ArtifactGear"/>.</summary>
    public static bool IsArtifactGear(in Item item) => item.EquipSlotCategory.RowId != 0;

    /// <summary><see cref="RewardKind.ArtifactGear"/> for gear, else <see cref="RewardKind.Item"/>.</summary>
    public static RewardKind KindOf(in Item item) => IsArtifactGear(in item) ? RewardKind.ArtifactGear : RewardKind.Item;
}
