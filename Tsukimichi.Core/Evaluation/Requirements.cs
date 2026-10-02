using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Evaluation;

// One concrete record per RequirementKind. Each carries the ids and numbers a renderer needs; the
// human-readable clause lives in RequirementResult.Detail so the records stay language-neutral.

/// <summary>The game removed the quest (<see cref="QuestRecord.IsRetired"/>); never met, so an uncompleted one is locked out.</summary>
public sealed record RetiredRequirement : Requirement
{
    public RetiredRequirement()
        : base(RequirementKind.Retired)
    {
    }
}

/// <summary>
/// The quest lies on a path the character did not take (<see cref="PathIndex"/>): another city's start, another
/// starting class, another Grand Company, or another option of a set only one of which can be done.
/// <paramref name="Path"/> is the decisive kind (the first excluding facet in <see cref="PathKind"/> order);
/// <paramref name="Facets"/> lists every choice group the quest belongs to, excluding or not, which the detail line
/// reads ("Only for Ul'dah Gladiator starters · you started in Gridania as a Lancer"); <paramref name="Evidence"/> holds
/// the completed quests (row ids) that decided the character's choice.
/// </summary>
public sealed record OtherPathRequirement(PathKind Path, IReadOnlyList<PathFacet> Facets, uint[] Evidence) : Requirement(RequirementKind.OtherPath)
{
    /// <summary>The facet that names the reason: the first one that excludes the quest, in <see cref="PathKind"/> order.</summary>
    public PathFacet? Decisive
    {
        get
        {
            PathFacet? best = null;
            foreach (var facet in Facets)
            {
                if (facet.Excludes && (best is null || facet.Kind < best.Kind))
                {
                    best = facet;
                }
            }

            return best;
        }
    }
}

/// <summary>Quests that foreclose this one once completed, and which of them the character has completed.</summary>
public sealed record ForeclosureRequirement(uint[] LockIds, uint[] CompletedLockIds) : Requirement(RequirementKind.Foreclosure);

/// <summary>The quest belongs to an expansion above the character's entitlement.</summary>
public sealed record ExpansionCapRequirement(byte Expansion, byte MaxExpansion) : Requirement(RequirementKind.ExpansionCap);

/// <summary>The quest level is above the character's level cap.</summary>
public sealed record LevelCapRequirement(byte Level, byte LevelCap) : Requirement(RequirementKind.LevelCap);

/// <summary>Class or job admission. <paramref name="Job"/> is the job the check was made for.</summary>
/// <param name="CategoryId">ClassJobCategory row id; zero when the quest has none.</param>
/// <param name="RequiredJob">ClassJob row id when the quest pins one job; zero otherwise.</param>
public sealed record ClassJobRequirement(uint CategoryId, uint RequiredJob, byte Job) : Requirement(RequirementKind.ClassJob);

/// <summary>Unsynced level of the job under consideration against the quest level.</summary>
public sealed record LevelRequirement(byte Level, byte ActualLevel) : Requirement(RequirementKind.Level)
{
    /// <summary>
    /// The job <see cref="ActualLevel"/> was read from when it is not the job the quest was evaluated on: the job the
    /// quest is ready on, its pinned job, or the character's best job its category admits
    /// (<see cref="NotYetText.OnAdmittedJob"/>). Zero for the evaluated job, as the evaluator writes it.
    /// </summary>
    public byte MeasuredOn { get; init; }

    /// <summary>
    /// No job the character has can take the quest (the pinned job not unlocked, or no job of its category levelled):
    /// there is no level to compare, so <see cref="ActualLevel"/> is zero and nothing reads it
    /// (<see cref="NotYetText.OnAdmittedJob"/>).
    /// </summary>
    public bool NoJob { get; init; }
}

/// <summary>Previous quests with their join, how many are completed and which (<paramref name="DoneIds"/>, null when the caller did not say).</summary>
public sealed record PreviousQuestsRequirement(uint[] QuestIds, JoinKind Join, int DoneCount, uint[]? DoneIds = null) : Requirement(RequirementKind.PreviousQuests);

/// <summary>Membership in a specific Grand Company.</summary>
public sealed record GrandCompanyRequirement(byte GrandCompany, byte ActualGrandCompany) : Requirement(RequirementKind.GrandCompany);

/// <summary>Rank within a Grand Company (the quest's own, or the character's when the quest names none).</summary>
public sealed record GrandCompanyRankRequirement(byte GrandCompany, byte RequiredRank, byte ActualRank) : Requirement(RequirementKind.GrandCompanyRank);

/// <summary>Allied society rank; names via <see cref="TribeRanks"/>.</summary>
public sealed record TribeRankRequirement(byte Tribe, byte RequiredRank, byte ActualRank) : Requirement(RequirementKind.TribeRank);

/// <summary>Allied society reputation points within the current rank.</summary>
public sealed record TribeReputationRequirement(byte Tribe, ushort RequiredValue, ushort ActualValue) : Requirement(RequirementKind.TribeReputation);

/// <summary>Daily allied society allowances left.</summary>
public sealed record TribeAllowanceRequirement(byte Allowance) : Requirement(RequirementKind.TribeAllowance);

/// <summary>Whether this repeatable tribe quest is among today's offered quests.</summary>
public sealed record TribeDailyOfferRequirement(ushort QuestId, bool Offered) : Requirement(RequirementKind.TribeDailyOffer);

/// <summary>Instance content that must be cleared, with join and count.</summary>
public sealed record DutyCompletionRequirement(uint[] InstanceIds, JoinKind Join, int DoneCount) : Requirement(RequirementKind.DutyCompletion);

/// <summary>
/// Seasonal event that must be running, and, for a quest with a phase window (<see cref="Begin"/> / <see cref="End"/>,
/// both 0 when the quest is offered for the whole run), the event's current phase must lie inside it.
/// <paramref name="Phase"/> is the phase the client reports for the running event, null when the event is not
/// running or the client did not say; an unknown phase never blocks.
/// </summary>
public sealed record SeasonalRequirement(ushort FestivalId, bool Active, byte Begin = 0, byte End = 0, ushort? Phase = null) : Requirement(RequirementKind.Seasonal)
{
    /// <summary>Whether the quest has a phase window at all.</summary>
    public bool HasWindow => Begin != 0 || End != 0;

    /// <summary>The event is running and its phase is known to lie before <see cref="Begin"/>: the chapter has not opened yet.</summary>
    public bool ChapterNotOpen => Active && Phase is { } phase && Begin != 0 && phase < Begin;

    /// <summary>The event is running and its phase is known to lie after <see cref="End"/>: the chapter is over.</summary>
    public bool ChapterOver => Active && Phase is { } phase && End != 0 && phase > End;
}

/// <summary>
/// Custom delivery satisfaction rank with one client. <paramref name="ActualRank"/> is null when the plugin did not
/// read the rank (a snapshot written before it was captured); such a requirement is listed, not judged.
/// </summary>
public sealed record CustomDeliveryRankRequirement(byte Npc, byte RequiredRank, byte? ActualRank) : Requirement(RequirementKind.CustomDeliveryRank);

/// <summary>
/// Delivery Moogle carrier level. <paramref name="ActualLevel"/> is null when the plugin did not read the level (a
/// snapshot written before it was captured); such a requirement is listed, not judged.
/// </summary>
public sealed record CarrierLevelRequirement(byte RequiredLevel, byte? ActualLevel) : Requirement(RequirementKind.CarrierLevel);

/// <summary>
/// Accept conditions from the sheet that name no quest (QuestCatalog.UncheckedAcceptConditions). Core cannot evaluate
/// these; they are listed, not checked. The ones that name a quest are judged with the previous quests.
/// </summary>
public sealed record AcceptConditionRequirement(uint[] ConditionIds) : Requirement(RequirementKind.AcceptCondition);

/// <summary>A mount is required. Null means the plugin did not say.</summary>
public sealed record MountRequirement(bool? HasMount) : Requirement(RequirementKind.Mount);

/// <summary>A house is required. Null means the plugin did not say.</summary>
public sealed record HouseRequirement(bool? HasHouse) : Requirement(RequirementKind.House);

/// <summary>The quest is gated by an achievement per curated data; it can only be judged once achievements are loaded.</summary>
public sealed record AchievementRequirement(uint RowId, bool Loaded) : Requirement(RequirementKind.Achievement);

/// <summary>Allied society rank names; index is the rank as held by the client.</summary>
public static class TribeRanks
{
    private static readonly string[] Names =
        ["None", "Neutral", "Recognized", "Friendly", "Trusted", "Respected", "Honored", "Sworn", "Allied"];

    public static string Name(byte rank) => rank < Names.Length ? Names[rank] : $"rank {rank}";
}

/// <summary>Grand Company names by GrandCompany row id.</summary>
public static class GrandCompanies
{
    private static readonly string[] Names = ["Grand Company", "Maelstrom", "Order of the Twin Adder", "Immortal Flames"];

    public static string Name(byte grandCompany) =>
        grandCompany != 0 && grandCompany < Names.Length ? Names[grandCompany] : $"Grand Company {grandCompany}";
}

/// <summary>
/// Grand Company rank titles by GrandCompanyRank row id, without the company's own prefix (Storm, Serpent, Flame):
/// the rank a quest asks for is the same tier in every company.
/// </summary>
public static class GrandCompanyRanks
{
    private static readonly string[] Names =
    [
        "no rank",
        "Private Third Class",
        "Private Second Class",
        "Private First Class",
        "Corporal",
        "Sergeant Third Class",
        "Sergeant Second Class",
        "Sergeant First Class",
        "Chief Sergeant",
        "Second Lieutenant",
        "First Lieutenant",
        "Captain",
    ];

    public static string Name(byte rank) => rank < Names.Length ? Names[rank] : $"rank {rank}";
}

/// <summary>
/// The expansions by ExVersion row id (8.0 readiness, R10 proposal 6). The plugin names an expansion from the game's
/// ExVersion sheet wherever a catalog is at hand (<c>GameNames.Expansions</c>: the blocker text, the requirement
/// details, the panes), in the client's language. What the sheet cannot give is the short code the expansion pills
/// and the Journal tree's short names use ("ShB"), so that is the one fact written here per expansion; the English
/// name beside it is only the fallback for code without a catalog (tests, tools) and what the English journal names
/// are matched against. <c>ExpansionTableTests</c> fails when the game data has an ExVersion row this table lacks, so
/// a new expansion is added here (and given its banner art, <see cref="Ui.BannerArts.Msq"/>) rather than reading
/// "expansion 6".
/// </summary>
public static class Expansions
{
    private static readonly (string Short, string English)[] Table =
    [
        ("ARR", "A Realm Reborn"),
        ("HW", "Heavensward"),
        ("StB", "Stormblood"),
        ("ShB", "Shadowbringers"),
        ("EW", "Endwalker"),
        ("DT", "Dawntrail"),
    ];

    /// <summary>How many expansions the table knows: ids 0 to <c>Count - 1</c>.</summary>
    public static int Count => Table.Length;

    /// <summary>The English name, the fallback where no catalog names it; "expansion 6" for an id the table lacks.</summary>
    public static string Name(byte expansion) => expansion < Table.Length ? Table[expansion].English : $"expansion {expansion}";

    /// <summary>The short code of the pills and short names ("ARR", "ShB"); null for an id the table lacks.</summary>
    public static string? ShortCode(byte expansion) => expansion < Table.Length ? Table[expansion].Short : null;

    /// <summary>The id whose English name is <paramref name="englishName"/> exactly (ordinal, trimmed); null for none.</summary>
    public static byte? FromEnglishName(string? englishName)
    {
        var name = englishName?.Trim();
        for (var i = 0; i < Table.Length; i++)
        {
            if (string.Equals(Table[i].English, name, StringComparison.Ordinal))
            {
                return (byte)i;
            }
        }

        return null;
    }

    /// <summary>Whether <paramref name="text"/> is one of the short codes, exactly.</summary>
    public static bool IsShortCode(string? text)
    {
        foreach (var (code, _) in Table)
        {
            if (string.Equals(code, text, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
