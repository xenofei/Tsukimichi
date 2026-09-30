using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Unique;

/// <summary>
/// The name a Moonlit reward prints in the player's client language (V2-19). <c>unique_quests.json</c> ships its
/// reward names in English (DataGen reads the English sheets); the quest catalog is read in the client's language, and
/// its quest carries the same reward with the sheet's name. A non-English client therefore takes the catalog's name
/// where the quest lists the reward (same item, else same kind and id), and the shipped name otherwise (curated system
/// unlocks, which the sheets do not name).
/// </summary>
public static class RewardNames
{
    /// <summary>The catalog language the shipped names are written in (<see cref="Model.QuestCatalog"/> bundles name it).</summary>
    public const string ShippedLanguage = "English";

    /// <summary>
    /// The reward's name from <paramref name="quest"/>'s own rewards, or null when the quest does not list it (or has
    /// no name for it).
    /// </summary>
    public static string? FromQuest(UniqueRewardEntry entry, QuestRecord? quest)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (quest is null)
        {
            return null;
        }

        if (entry.ItemId != 0)
        {
            foreach (var reward in quest.Rewards)
            {
                if (reward.ItemId == entry.ItemId && reward.Name.Length > 0)
                {
                    return reward.Name;
                }
            }
        }

        if (entry.RewardId != 0)
        {
            foreach (var reward in quest.Rewards)
            {
                if (reward.Kind == entry.Kind && reward.Id == entry.RewardId && reward.Name.Length > 0)
                {
                    return reward.Name;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The name to print: the shipped one for an English catalog (unchanged from before localization), else the
    /// catalog's (<see cref="FromQuest"/>) where it has one.
    /// </summary>
    /// <param name="catalogLanguage">The language the quest catalog was read in (<c>CatalogBundle.Language</c>).</param>
    public static string Display(UniqueRewardEntry entry, QuestRecord? quest, string? catalogLanguage)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (catalogLanguage is null || string.Equals(catalogLanguage, ShippedLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return entry.RewardName;
        }

        return FromQuest(entry, quest) ?? entry.RewardName;
    }
}
