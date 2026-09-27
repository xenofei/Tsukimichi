using Tsukimichi.Core.Model;

namespace Tsukimichi.GameData;

/// <summary>
/// Everything one catalog build produces: the quest catalog plus the small sheet-derived lookups the evaluator and UI need.
/// Immutable once built.
/// </summary>
/// <param name="Catalog">Every named quest, indexed.</param>
/// <param name="Names">Display names for tribes, Grand Companies, expansions, classes and tribe ranks.</param>
/// <param name="Jobs">ClassJobCategory membership.</param>
/// <param name="Language">Lumina language name the strings were read in, e.g. "English".</param>
public sealed record CatalogBundle(QuestCatalog Catalog, GameNames Names, ClassJobCategoryLookup Jobs, string Language);

/// <summary>
/// Small id-to-name lookups read from the BeastTribe, GrandCompany, ExVersion, ClassJob and BeastReputationRank sheets.
/// Keys are row ids; the byte-sized ids on <see cref="QuestRecord"/> widen implicitly.
/// </summary>
public sealed record GameNames(
    IReadOnlyDictionary<uint, string> Tribes,
    IReadOnlyDictionary<uint, string> GrandCompanies,
    IReadOnlyDictionary<uint, string> Expansions,
    IReadOnlyDictionary<uint, string> ClassJobs,
    IReadOnlyDictionary<uint, string> ClassJobAbbreviations,
    IReadOnlyDictionary<uint, string> TribeRanks)
{
    public static readonly GameNames Empty = new(
        new Dictionary<uint, string>(),
        new Dictionary<uint, string>(),
        new Dictionary<uint, string>(),
        new Dictionary<uint, string>(),
        new Dictionary<uint, string>(),
        new Dictionary<uint, string>());

    public string Tribe(uint id) => Tribes.GetValueOrDefault(id, string.Empty);

    public string GrandCompany(uint id) => GrandCompanies.GetValueOrDefault(id, string.Empty);

    public string Expansion(uint id) => Expansions.GetValueOrDefault(id, string.Empty);

    public string ClassJob(uint id) => ClassJobs.GetValueOrDefault(id, string.Empty);

    public string ClassJobAbbreviation(uint id) => ClassJobAbbreviations.GetValueOrDefault(id, string.Empty);

    public string TribeRank(uint id) => TribeRanks.GetValueOrDefault(id, string.Empty);
}
