namespace Tsukimichi.Core.Model;

/// <summary>A reward attached to a quest.</summary>
/// <param name="Kind">Reward category.</param>
/// <param name="Id">Row id in the sheet the kind refers to (Item, Emote, Action, ...). Zero when not applicable.</param>
/// <param name="ItemId">Item row id when the reward is delivered as an item, else zero.</param>
/// <param name="Count">Quantity for item rewards; one otherwise.</param>
/// <param name="Name">Display name, resolved at catalog build time.</param>
/// <param name="Icon">Icon id, zero when none.</param>
public sealed record RewardRef(
    RewardKind Kind,
    uint Id,
    uint ItemId,
    uint Count,
    string Name,
    uint Icon);
