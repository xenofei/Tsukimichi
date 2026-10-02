using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ipc;

/// <summary>
/// The words <c>Tsukimichi.GetFirstBlocker</c> answers with (docs/ipc.md, "Blocker kinds"). A public vocabulary of its
/// own, mapped from the evaluator's requirement kinds, so an internal rename never reaches a caller: a word, once
/// shipped, keeps its meaning and is never removed. A later release may add a word; a caller treats one it does not
/// know as <see cref="Other"/>.
/// </summary>
public static class IpcBlockerKinds
{
    /// <summary>No answer: not ready, or an id the catalog does not hold.</summary>
    public const string NoAnswer = "";

    /// <summary>Nothing blocks: Ready, in the journal, done, or done for this cycle.</summary>
    public const string None = "none";

    /// <summary>The job's level is too low. refId: the ClassJob the level was read on (0 for the current job); need/have: levels.</summary>
    public const string Level = "level";

    /// <summary>A previous quest is not done. refId: the quest to do next (row id); need/have: quests needed and done.</summary>
    public const string Quest = "quest";

    /// <summary>Needs another class or job. refId: the ClassJob the quest is open on (Ready on another job), else the one it requires, else 0.</summary>
    public const string Job = "job";

    /// <summary>Needs one of a ClassJobCategory's jobs. refId: the ClassJobCategory row id.</summary>
    public const string JobCategory = "jobCategory";

    /// <summary>Needs a Grand Company. refId and need: the GrandCompany row id; have: the character's (0 for none).</summary>
    public const string GrandCompany = "grandCompany";

    /// <summary>Needs a Grand Company rank. refId: the GrandCompany; need/have: GrandCompanyRank row ids.</summary>
    public const string GrandCompanyRank = "grandCompanyRank";

    /// <summary>Needs an allied society rank. refId: the BeastTribe row id; need/have: ranks.</summary>
    public const string AlliedSocietyRank = "alliedSocietyRank";

    /// <summary>Needs allied society reputation. refId: the BeastTribe; need/have: reputation points.</summary>
    public const string AlliedSocietyReputation = "alliedSocietyReputation";

    /// <summary>No allied society allowances left today. need: 1; have: allowances left.</summary>
    public const string AlliedSocietyAllowance = "alliedSocietyAllowance";

    /// <summary>An allied society daily the quest giver does not offer today. refId: the quest's row id.</summary>
    public const string NotOfferedToday = "notOfferedToday";

    /// <summary>A duty must be cleared. refId: the first InstanceContent row id the quest lists; need/have: duties needed and cleared.</summary>
    public const string Duty = "duty";

    /// <summary>A seasonal event must be running (or its chapter open). refId: the Festival row id; need: 1; have: 0.</summary>
    public const string Seasonal = "seasonal";

    /// <summary>The account does not own the expansion. refId and need: the ExVersion row id; have: the newest one owned.</summary>
    public const string Expansion = "expansion";

    /// <summary>The level is above the account's level cap. need: the quest's level; have: the cap.</summary>
    public const string LevelCap = "levelCap";

    /// <summary>On a path the character did not take (another city, class, Grand Company or choice). refId: a quest that decided it, or 0. Locked out for good.</summary>
    public const string OtherPath = "otherPath";

    /// <summary>A quest that forecloses this one is done. refId: that quest's row id. Locked out for good.</summary>
    public const string LockedOut = "lockedOut";

    /// <summary>The game removed the quest. Locked out for good.</summary>
    public const string Removed = "removed";

    /// <summary>Needs an achievement. refId: the Achievement row id; have: -1 while the achievement list is not loaded.</summary>
    public const string Achievement = "achievement";

    /// <summary>Needs a mount (have: -1 when the plugin could not tell).</summary>
    public const string Mount = "mount";

    /// <summary>Needs a house (have: -1 when the plugin could not tell).</summary>
    public const string House = "house";

    /// <summary>Needs a custom delivery satisfaction rank. refId: the SatisfactionNpc row id; need/have: ranks (have -1 when unread).</summary>
    public const string CustomDeliveryRank = "customDeliveryRank";

    /// <summary>Needs a Delivery Moogle carrier level. need/have: levels (have -1 when unread).</summary>
    public const string CarrierLevel = "carrierLevel";

    /// <summary>An accept condition the game does not expose. refId: the first condition id. The quest reads Not checked.</summary>
    public const string Unchecked = "unchecked";

    /// <summary>Blocked by something this vocabulary has no word for yet.</summary>
    public const string Other = "other";

    /// <summary>Every word, <see cref="NoAnswer"/> excepted, in docs/ipc.md's order.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        None, Level, Quest, Job, JobCategory, GrandCompany, GrandCompanyRank, AlliedSocietyRank, AlliedSocietyReputation,
        AlliedSocietyAllowance, NotOfferedToday, Duty, Seasonal, Expansion, LevelCap, OtherPath, LockedOut, Removed,
        Achievement, Mount, House, CustomDeliveryRank, CarrierLevel, Unchecked, Other,
    ];
}

/// <summary>The answer of <c>Tsukimichi.GetFirstBlocker</c>; <see cref="Have"/> is -1 when the plugin could not read the value.</summary>
public readonly record struct IpcBlocker(string Kind, uint RefId, int Need, int Have)
{
    /// <summary>No answer (not ready, unknown id).</summary>
    public static readonly IpcBlocker NoAnswer = new(IpcBlockerKinds.NoAnswer, 0, 0, 0);

    /// <summary>Nothing blocks.</summary>
    public static readonly IpcBlocker None = new(IpcBlockerKinds.None, 0, 0, 0);

    /// <summary>The tuple the gate returns.</summary>
    public (string Kind, uint RefId, int Need, int Have) ToTuple() => (Kind, RefId, Need, Have);

    /// <summary>
    /// The first blocker of an evaluated quest: <see cref="None"/> when nothing blocks it now (Ready, in the journal,
    /// done), the job it is open on for Ready on another job, else its first unmet requirement
    /// (<see cref="QuestEvaluation.NextStep"/>) in <see cref="IpcBlockerKinds"/>' words.
    /// </summary>
    public static IpcBlocker Of(QuestEvaluation evaluation, QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation>? states)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(catalog);
        switch (evaluation.State)
        {
            case QuestState.Ready or QuestState.Accepted or QuestState.Completed or QuestState.DoneThisCycle:
                return None;
            case QuestState.ReadyOnOtherJob:
                return new IpcBlocker(IpcBlockerKinds.Job, evaluation.ReadyOnJob is { } job ? job : 0u, 0, 0);
        }

        return evaluation.NextStep is { } step ? Map(step.Req, catalog, states) : new IpcBlocker(IpcBlockerKinds.Other, 0, 0, 0);
    }

    private static IpcBlocker Map(Requirement requirement, QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation>? states) => requirement switch
    {
        LevelRequirement r => new(IpcBlockerKinds.Level, r.MeasuredOn, r.Level, r.NoJob ? -1 : r.ActualLevel),
        PreviousQuestsRequirement r => new(
            IpcBlockerKinds.Quest,
            BlockerText.NearestPrerequisite(r, catalog, states) ?? FirstOr0(r.QuestIds),
            r.Join == JoinKind.Any ? Math.Min(1, r.QuestIds.Length) : r.QuestIds.Length,
            r.DoneCount),
        ClassJobRequirement { RequiredJob: not 0 } r => new(IpcBlockerKinds.Job, r.RequiredJob, 0, 0),
        ClassJobRequirement r => new(IpcBlockerKinds.JobCategory, r.CategoryId, 0, 0),
        GrandCompanyRequirement r => new(IpcBlockerKinds.GrandCompany, r.GrandCompany, r.GrandCompany, r.ActualGrandCompany),
        GrandCompanyRankRequirement r => new(IpcBlockerKinds.GrandCompanyRank, r.GrandCompany, r.RequiredRank, r.ActualRank),
        TribeRankRequirement r => new(IpcBlockerKinds.AlliedSocietyRank, r.Tribe, r.RequiredRank, r.ActualRank),
        TribeReputationRequirement r => new(IpcBlockerKinds.AlliedSocietyReputation, r.Tribe, r.RequiredValue, r.ActualValue),
        TribeAllowanceRequirement r => new(IpcBlockerKinds.AlliedSocietyAllowance, 0, 1, r.Allowance),
        TribeDailyOfferRequirement r => new(IpcBlockerKinds.NotOfferedToday, IpcView.FirstRowId | r.QuestId, 1, 0),
        DutyCompletionRequirement r => new(
            IpcBlockerKinds.Duty,
            FirstOr0(r.InstanceIds),
            r.Join == JoinKind.Any ? Math.Min(1, r.InstanceIds.Length) : r.InstanceIds.Length,
            r.DoneCount),
        SeasonalRequirement r => new(IpcBlockerKinds.Seasonal, r.FestivalId, 1, 0),
        ExpansionCapRequirement r => new(IpcBlockerKinds.Expansion, r.Expansion, r.Expansion, r.MaxExpansion),
        LevelCapRequirement r => new(IpcBlockerKinds.LevelCap, 0, r.Level, r.LevelCap),
        OtherPathRequirement r => new(IpcBlockerKinds.OtherPath, FirstOr0(r.Evidence), 0, 0),
        ForeclosureRequirement r => new(IpcBlockerKinds.LockedOut, FirstOr0(r.CompletedLockIds.Length > 0 ? r.CompletedLockIds : r.LockIds), 0, 0),
        RetiredRequirement => new(IpcBlockerKinds.Removed, 0, 0, 0),
        AchievementRequirement r => new(IpcBlockerKinds.Achievement, r.RowId, 1, r.Loaded ? 0 : -1),
        MountRequirement r => new(IpcBlockerKinds.Mount, 0, 1, r.HasMount is { } has ? (has ? 1 : 0) : -1),
        HouseRequirement r => new(IpcBlockerKinds.House, 0, 1, r.HasHouse is { } has ? (has ? 1 : 0) : -1),
        CustomDeliveryRankRequirement r => new(IpcBlockerKinds.CustomDeliveryRank, r.Npc, r.RequiredRank, r.ActualRank ?? -1),
        CarrierLevelRequirement r => new(IpcBlockerKinds.CarrierLevel, 0, r.RequiredLevel, r.ActualLevel ?? -1),
        AcceptConditionRequirement r => new(IpcBlockerKinds.Unchecked, FirstOr0(r.ConditionIds), 0, 0),
        GameGateRequirement => new(IpcBlockerKinds.Unchecked, 0, 0, 0),
        _ => new(IpcBlockerKinds.Other, 0, 0, 0),
    };

    private static uint FirstOr0(uint[] ids) => ids.Length > 0 ? ids[0] : 0;
}
