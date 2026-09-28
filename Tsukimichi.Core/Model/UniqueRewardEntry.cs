using System.Text.Json.Serialization;

namespace Tsukimichi.Core.Model;

/// <summary>Names used in <see cref="UniqueRewardEntry.OtherSources"/>: other places the same reward can come from.</summary>
public static class OtherSource
{
    /// <summary>Sold on the FFXIV Online Store (Mog Station); from <c>curated/online_store.json</c>.</summary>
    public const string OnlineStore = "OnlineStore";

    /// <summary>The item is not flagged untradable.</summary>
    public const string Tradable = "Tradable";

    /// <summary>The item has a market board search category.</summary>
    public const string Marketable = "Marketable";

    /// <summary>A gil shop lists the item (including quest-reward reacquisition menus).</summary>
    public const string GilShopItem = "GilShopItem";

    /// <summary>A special shop (tomestones, scrips, seals, ...) hands the item out.</summary>
    public const string SpecialShop = "SpecialShop";

    /// <summary>A recipe produces the item.</summary>
    public const string Recipe = "Recipe";

    /// <summary>A gathering node yields the item.</summary>
    public const string GatheringItem = "GatheringItem";

    /// <summary>An achievement rewards the item.</summary>
    public const string Achievement = "Achievement";
}

/// <summary>One reward obtainable only through a quest, as shipped in unique_quests.json or overridden by the user.</summary>
/// <param name="Source">Where the claim comes from: sheet and field, curated file name, or "user".</param>
public sealed record UniqueRewardEntry(
    uint QuestRowId,
    RewardKind Kind,
    uint RewardId,
    uint ItemId,
    string RewardName,
    Confidence Confidence,
    string Source)
{
    private static readonly string[] NoSources = [];

    private readonly IReadOnlyList<string> otherSources = NoSources;

    /// <summary>
    /// Other places the same reward can be had (<see cref="OtherSource"/> names), in generator order; empty when the
    /// reward is quest-exclusive as far as the data knows. Files written before this field existed load as empty.
    /// </summary>
    public IReadOnlyList<string> OtherSources
    {
        get => otherSources;
        init => otherSources = value ?? NoSources;
    }

    /// <summary>Whether the reward is also sold on the FFXIV Online Store (<see cref="OtherSource.OnlineStore"/>).</summary>
    [JsonIgnore]
    public bool SoldOnOnlineStore => HasOtherSource(OtherSource.OnlineStore);

    /// <summary>Whether <see cref="OtherSources"/> names <paramref name="name"/> (ordinal).</summary>
    public bool HasOtherSource(string name)
    {
        for (var i = 0; i < otherSources.Count; i++)
        {
            if (string.Equals(otherSources[i], name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A copy with <paramref name="name"/> appended to <see cref="OtherSources"/>; the same entry when already listed.</summary>
    public UniqueRewardEntry WithOtherSource(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (HasOtherSource(name))
        {
            return this;
        }

        var extended = new string[otherSources.Count + 1];
        for (var i = 0; i < otherSources.Count; i++)
        {
            extended[i] = otherSources[i];
        }

        extended[^1] = name;
        return this with { OtherSources = extended };
    }

    /// <summary>Value equality including the <see cref="OtherSources"/> sequence (a list property would otherwise compare by reference).</summary>
    public bool Equals(UniqueRewardEntry? other) =>
        other is not null
        && QuestRowId == other.QuestRowId
        && Kind == other.Kind
        && RewardId == other.RewardId
        && ItemId == other.ItemId
        && string.Equals(RewardName, other.RewardName, StringComparison.Ordinal)
        && Confidence == other.Confidence
        && string.Equals(Source, other.Source, StringComparison.Ordinal)
        && otherSources.SequenceEqual(other.otherSources, StringComparer.Ordinal);

    public override int GetHashCode() => HashCode.Combine(QuestRowId, Kind, RewardId, ItemId, RewardName, Confidence, Source, otherSources.Count);
}
