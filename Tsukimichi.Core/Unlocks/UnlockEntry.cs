using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Unlocks;

/// <summary>
/// One thing a quest opens (feature plan v6 K1). Immutable.
/// </summary>
/// <param name="Target">What the row is.</param>
/// <param name="TargetId">
/// The row id in the target's own sheet: TerritoryType for a zone, PlaceName for a world-map region, Aetheryte,
/// ContentFinderCondition for a duty, ClassJob, Emote, Action, … and the Quest row id for a next quest. 0 when the
/// target has no row of its own (a curated feature, flying in a zone).
/// </param>
/// <param name="Name">What the row prints ("Kugane", "The Sirensong Sea", "Retainers"); a next quest's real name, which a caller prints through the spoiler shield.</param>
/// <param name="Icon">The game icon id; 0 when none is known (the pane draws its stand-in).</param>
/// <param name="Source">How sure the row is; a <see cref="UnlockSource.Derived"/> row reads "Likely: you first reach it here".</param>
/// <param name="Expansion">The expansion (ExVersion) the target belongs to, which Sprout mode compares with the character's reach.</param>
/// <param name="SortKey">The order inside the target: a duty's level, a quest's level, a zone's sheet order.</param>
/// <param name="PlaceId">The territory a zone or aetheryte lies in (for Teleport and the map); 0 when none.</param>
/// <param name="Detail">The one fact after the kind word in a row's caption: a zone's region, a duty's level, an aetheryte's zone; empty for none.</param>
/// <param name="Note">The curated note or the evidence, for the tooltip; null for none.</param>
public sealed record UnlockEntry(
    UnlockTarget Target,
    uint TargetId,
    string Name,
    uint Icon,
    UnlockSource Source,
    byte Expansion,
    uint SortKey = 0,
    uint PlaceId = 0,
    string Detail = "",
    string? Note = null)
{
    /// <summary>The section the row is drawn under.</summary>
    public UnlockGroup Group => UnlockTargets.GroupOf(Target);

    /// <summary>Inferred from the first visit rather than stated: the tooltip says "Likely: you first reach it here".</summary>
    public bool IsLikely => Source == UnlockSource.Derived;

    /// <summary>
    /// The reward kind the row came from (a duty unlock, an emote, a mount), so the plugin can ask the game whether the
    /// character has it; null for a row no reward stands behind (an area, an aetheryte, a next quest).
    /// </summary>
    public RewardKind? Reward { get; init; }

    /// <summary>The reward's item id when it is delivered as an item (a mount's whistle); 0 otherwise.</summary>
    public uint ItemId { get; init; }

    /// <summary>
    /// The quest's own reward list carries the same thing, so the detail pane's Rewards tiles already show it and its
    /// Unlocks section leaves it out of Actions &amp; emotes and Items.
    /// </summary>
    public bool InRewards { get; init; }

    /// <summary>The caption after the name: "Area · Hingashi", "Dungeon · Lv 61", "Emote".</summary>
    public string Caption => Detail.Length > 0 ? UnlockTargets.Name(Target) + Evaluation.BlockerText.Separator + Detail : UnlockTargets.Name(Target);
}
