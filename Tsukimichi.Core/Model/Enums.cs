namespace Tsukimichi.Core.Model;

/// <summary>Resolved state of a quest for one character. Each state maps to a moon phase in the UI.</summary>
public enum QuestState
{
    Ready,
    ReadyOnOtherJob,
    Accepted,
    Blocked,
    DoneThisCycle,
    Completed,
    Foreclosed,
    Unknown,
}

/// <summary>How a list of prerequisite ids combines: every entry, or at least one.</summary>
public enum JoinKind
{
    All,
    Any,
}

/// <summary>One gate that can keep a quest from being accepted. Order matches display and "next step" priority.</summary>
public enum RequirementKind
{
    /// <summary>The game removed the quest (<see cref="QuestRecord.IsRetired"/>); nothing can be done about it.</summary>
    Retired,
    Foreclosure,
    ExpansionCap,
    LevelCap,
    ClassJob,
    Level,
    PreviousQuests,
    GrandCompany,
    GrandCompanyRank,
    TribeRank,
    TribeReputation,
    TribeAllowance,
    TribeDailyOffer,
    DutyCompletion,
    Seasonal,
    AcceptCondition,
    Mount,
    House,
    Achievement,

    /// <summary>Custom delivery satisfaction rank with one client (<see cref="QuestRecord.SatisfactionNpc"/>).</summary>
    CustomDeliveryRank,

    /// <summary>Delivery Moogle carrier level (<see cref="QuestRecord.CarrierLevel"/>).</summary>
    CarrierLevel,

    /// <summary>
    /// The quest lies on a path the character did not take: another city's start, another starting class, another
    /// Grand Company or another choice of a set only one of which can be done (<c>Evaluation.PathIndex</c>). Listed
    /// right after <see cref="Retired"/>; unmet, it makes the quest Locked out.
    /// </summary>
    OtherPath,

    /// <summary>
    /// A gate the game checks that Tsukimichi cannot read, from <c>curated/game_gates.json</c> (a relic weapon at some
    /// stage equipped, Eureka or Doman Enclave progress). Never judged: once every other requirement is met it makes
    /// the quest Not checked rather than Ready (<c>QuestCatalog.GameGateOf</c>).
    /// </summary>
    GameGate,
}

/// <summary>What a quest hands out. The first block mirrors the Quest sheet reward slots; the rest are resolved links.</summary>
public enum RewardKind
{
    Item,
    OptionalItem,
    Emote,
    Action,
    GeneralAction,
    Instance,
    ClassJob,
    Other,
    ArtifactGear,
    Mount,
    Minion,
    Orchestrion,
    TripleTriadCard,
    Ornament,
    Barding,
    Hairstyle,
    AetherCurrent,
    BlueMageSpell,
    Trait,
    Achievement,
    Title,
    DutyUnlock,
    SystemUnlock,
}

/// <summary>How sure the unique-reward data is about an entry, from least to most authoritative.</summary>
public enum Confidence
{
    Static,
    Community,
    Curated,
    UserOverride,
}
