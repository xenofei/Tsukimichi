using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Evaluation;

/// <summary>
/// The name lookups <see cref="BlockerText"/> prints with: quest names come from <see cref="Catalog"/>, the rest from
/// sheets Core cannot read, so the plugin supplies them (the catalog bundle builds one). Every lookup has a safe
/// default: an empty string drops the clause that needs it ("Rank: Trusted" without "with the Pelupelu"), the rank,
/// company and expansion tables fall back to the English names in <see cref="TribeRanks"/>,
/// <see cref="GrandCompanyRanks"/>, <see cref="GrandCompanies"/> and <see cref="Expansions"/>.
/// </summary>
public sealed record BlockerNames
{
    public static readonly BlockerNames Default = new();

    /// <summary>Names prerequisite, lock and main-scenario quests; <see cref="QuestCatalog.Empty"/> prints "quest N".</summary>
    public QuestCatalog Catalog { get; init; } = QuestCatalog.Empty;

    /// <summary>Allied society name by BeastTribe row id ("Pelupelu"); empty leaves the society out of the phrase.</summary>
    public Func<byte, string> Tribe { get; init; } = static _ => string.Empty;

    /// <summary>Allied society rank name by BeastReputationRank row id.</summary>
    public Func<byte, string> TribeRank { get; init; } = TribeRanks.Name;

    /// <summary>Grand Company name by row id.</summary>
    public Func<byte, string> GrandCompany { get; init; } = GrandCompanies.Name;

    /// <summary>Grand Company rank title by GrandCompanyRank row id.</summary>
    public Func<byte, string> GrandCompanyRank { get; init; } = GrandCompanyRanks.Name;

    /// <summary>Expansion name by ExVersion row id.</summary>
    public Func<byte, string> Expansion { get; init; } = Expansions.Name;

    /// <summary>Three-letter job abbreviation by ClassJob row id ("PLD"); empty drops the "on PLD" and "you are WHM" clauses.</summary>
    public Func<uint, string> JobAbbreviation { get; init; } = static _ => string.Empty;

    /// <summary>ClassJobCategory name by row id ("Disciples of the Hand"); empty prints "another job".</summary>
    public Func<uint, string> ClassJobCategory { get; init; } = static _ => string.Empty;

    /// <summary>Duty name by InstanceContent row id ("The Vault"); empty prints how many duties are left.</summary>
    public Func<uint, string> Duty { get; init; } = static _ => string.Empty;

    /// <summary>Custom delivery client name by SatisfactionNpc row id ("M'naago"); empty drops the "with M'naago" clause.</summary>
    public Func<byte, string> SatisfactionNpc { get; init; } = static _ => string.Empty;
}
