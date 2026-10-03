namespace Tsukimichi.Core.Rewards;

/// <summary>
/// The game icons beside a quest's EXP and gil (owner point 9, UI-5a): the ones the game's own reward lists show.
/// Checked against the installed game in the game-data tests (gil is <c>Item</c> 1's icon; the EXP laurel exists).
/// </summary>
public static class RewardIcons
{
    /// <summary>The EXP laurel in the 065000 currency set.</summary>
    public const uint Exp = 65001;

    /// <summary>The gil coin: <c>Item.Icon</c> of <see cref="GilItem"/>.</summary>
    public const uint Gil = 65002;

    /// <summary>The Item row of gil.</summary>
    public const uint GilItem = 1;
}
