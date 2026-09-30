namespace Tsukimichi.Core.Ui;

/// <summary>
/// The detail pane's width tier (design v4 §8.2, feature plan v4 L5), from its own width in logical pixels. Each tier
/// keeps what the wider one shows and changes only how it is laid out.
/// </summary>
public enum DetailTier : byte
{
    /// <summary>D1, at least <see cref="LayoutBudgets.DetailFullLogical"/>: the moon beside the title, requirements as a grid.</summary>
    Full,

    /// <summary>D2, from <see cref="LayoutBudgets.DetailMediumLogical"/>: as Full; the meta line wraps by segment.</summary>
    Medium,

    /// <summary>D3, from the pane's floor (<see cref="LayoutBudgets.DetailNarrowLogical"/>): the moon above the title, requirements label over value.</summary>
    Narrow,

    /// <summary>Under the floor (a window too narrow for every floor): as Narrow, and the primary action is an icon button.</summary>
    Compact,
}

/// <summary>Picks the <see cref="DetailTier"/> for a width.</summary>
public static class DetailTiers
{
    /// <summary>The tier for a pane <paramref name="logicalWidth"/> logical pixels wide; an unreadable width is the narrowest.</summary>
    public static DetailTier For(float logicalWidth) =>
        !(logicalWidth >= LayoutBudgets.DetailNarrowLogical) ? DetailTier.Compact
        : logicalWidth < LayoutBudgets.DetailMediumLogical ? DetailTier.Narrow
        : logicalWidth < LayoutBudgets.DetailFullLogical ? DetailTier.Medium
        : DetailTier.Full;

    /// <summary>Whether the tier stacks: the moon above the title, and each requirement's label above its value.</summary>
    public static bool Stacks(DetailTier tier) => tier >= DetailTier.Narrow;
}
