namespace Tsukimichi.Core.Storage;

/// <summary>A start city's first quest ("Coming to Gridania") and the city's name, from <c>curated/path_choices.json</c>.</summary>
/// <param name="Root">Quest row id of the city's first quest.</param>
/// <param name="Label">The city as the detail line names it ("Gridania").</param>
public sealed record CityPin(uint Root, string Label, string Note);

/// <summary>
/// One A Realm Reborn starting class, from <c>curated/path_choices.json</c>: its "Close to Home" row (the start city
/// has one per class it offers, the rows differing only in the guild actor) and its starter "Way of" quest (the one
/// with no previous quest, which only a character that started as the class can take).
/// </summary>
/// <param name="ClassJob">ClassJob row id of the class (1 Gladiator … 7 Thaumaturge, 26 Arcanist).</param>
/// <param name="Label">The class as the detail line names it ("Gladiator").</param>
/// <param name="CloseToHome">Quest row id of the class's "Close to Home".</param>
/// <param name="Starter">Quest row id of the class's starter "Way of" quest.</param>
public sealed record ClassPin(byte ClassJob, string Label, uint CloseToHome, uint Starter, string Note);

/// <summary>A quest's Grand Company where the sheet's column leaves it 0 (The Company You Keep, Call of the Wild).</summary>
public sealed record GrandCompanyTag(byte GrandCompany, string Note);

/// <summary>
/// The labels and guards the choice groups need beyond the sheets (<c>Evaluation.PathIndex</c>), from
/// <c>curated/path_choices.json</c>: the three start cities (a pin the rule that finds them must agree with), the
/// eight starting classes, and the Grand Company of the quests whose sheet row does not say it.
/// </summary>
public sealed record PathChoices(
    IReadOnlyList<CityPin> Cities,
    IReadOnlyList<ClassPin> Classes,
    IReadOnlyDictionary<uint, GrandCompanyTag> GrandCompanies)
{
    public static readonly PathChoices Empty = new([], [], new Dictionary<uint, GrandCompanyTag>());

    public bool IsEmpty => Cities.Count == 0 && Classes.Count == 0 && GrandCompanies.Count == 0;
}
