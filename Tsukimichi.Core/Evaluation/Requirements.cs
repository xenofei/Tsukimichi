using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Evaluation;

// One concrete record per RequirementKind. Each carries the ids and numbers a renderer needs; the
// human-readable clause lives in RequirementResult.Detail so the records stay language-neutral.

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
public sealed record LevelRequirement(byte Level, byte ActualLevel) : Requirement(RequirementKind.Level);

/// <summary>Previous quests with their join and how many are completed.</summary>
public sealed record PreviousQuestsRequirement(uint[] QuestIds, JoinKind Join, int DoneCount) : Requirement(RequirementKind.PreviousQuests);

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

/// <summary>Seasonal event that must be running.</summary>
public sealed record SeasonalRequirement(ushort FestivalId, bool Active) : Requirement(RequirementKind.Seasonal);

/// <summary>Extra accept conditions from the sheet. Core cannot evaluate these; they are listed, not checked.</summary>
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

/// <summary>Expansion names by ExVersion row id.</summary>
public static class Expansions
{
    private static readonly string[] Names = ["A Realm Reborn", "Heavensward", "Stormblood", "Shadowbringers", "Endwalker", "Dawntrail"];

    public static string Name(byte expansion) => expansion < Names.Length ? Names[expansion] : $"expansion {expansion}";
}
