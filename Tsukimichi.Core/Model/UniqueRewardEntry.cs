namespace Tsukimichi.Core.Model;

/// <summary>One reward obtainable only through a quest, as shipped in unique_quests.json or overridden by the user.</summary>
/// <param name="Source">Where the claim comes from: sheet and field, curated file name, or "user".</param>
public sealed record UniqueRewardEntry(
    uint QuestRowId,
    RewardKind Kind,
    uint RewardId,
    uint ItemId,
    string RewardName,
    Confidence Confidence,
    string Source);
