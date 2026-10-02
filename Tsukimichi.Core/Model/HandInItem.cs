using System.Text.Json.Serialization;

namespace Tsukimichi.Core.Model;

/// <summary>
/// An item a quest asks the player to hand over (deliver, present, show), read from the quest at catalog build
/// (<c>Tsukimichi.GameData.QuestHandIns</c>): the allied society and role quests' <c>QuestClassJobSupply</c> rows, which
/// carry the amount and whether it must be high quality, and the script's <c>RITEMn</c> constants, which carry neither.
/// Items the script gives the player rather than asks for (gear to wear, materials to craft from) are left out.
/// Display only: nothing in the state engine reads it.
/// </summary>
public sealed record HandInItem
{
    /// <summary>Item sheet row.</summary>
    public uint ItemId { get; init; }

    /// <summary>The item's name in the catalog's language.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Item icon id; zero when none.</summary>
    public uint Icon { get; init; }

    /// <summary>
    /// How many the quest asks for, from <c>QuestClassJobSupply.AmountRequired</c>; zero when the game data does not
    /// say (every <c>RITEM</c> item: the script names the item, its amount lives in script code the sheets do not hold).
    /// </summary>
    public byte Amount { get; init; }

    /// <summary>The supply row asks for a high-quality item (<c>QuestClassJobSupply.ItemHQ</c>); false when unknown.</summary>
    public bool IsHq { get; init; }

    /// <summary>
    /// ClassJobCategory rows the supply row is for (a gatherer quest asks a miner for ore and a botanist for logs); empty
    /// when any job hands the item in.
    /// </summary>
    public uint[] ClassJobCategories { get; init; } = [];

    /// <summary>The recipes that make the item, by crafter (CraftType) then recipe id; empty when none.</summary>
    public HandInRecipe[] Recipes { get; init; } = [];

    /// <summary>Whether a gathering node, fishing hole or spearfishing spot yields the item.</summary>
    public GatherKind Gather { get; init; }

    /// <summary>Whether <see cref="Amount"/> is known.</summary>
    [JsonIgnore]
    public bool AmountKnown => Amount != 0;

    /// <summary>How many to have before the turn-in: <see cref="Amount"/>, or one when the data does not say.</summary>
    [JsonIgnore]
    public int Needed => Math.Max((int)Amount, 1);
}

/// <summary>A recipe that makes a hand-in item.</summary>
/// <param name="RecipeId">Recipe sheet row (what Artisan's <c>CraftItem</c> takes).</param>
/// <param name="CraftType">CraftType row: 0 Carpenter, 1 Blacksmith, … 7 Culinarian (ClassJob row 8 + this).</param>
/// <param name="Yield">
/// How many items one craft makes (<c>Recipe.AmountResult</c>): Artisan's amount counts crafts, so the hand-off asks
/// for the items missing divided by this, rounded up. One when the data does not say.
/// </param>
public sealed record HandInRecipe(uint RecipeId, byte CraftType, byte Yield = 1);

/// <summary>How a hand-in item is gathered, when it is.</summary>
public enum GatherKind
{
    /// <summary>No gathering source in the sheets.</summary>
    None,

    /// <summary>A mining or botany node (<c>GatheringItem</c>).</summary>
    Gathered,

    /// <summary>A fishing hole or spearfishing spot (<c>FishParameter</c>, <c>SpearfishingItem</c>).</summary>
    Fish,
}
