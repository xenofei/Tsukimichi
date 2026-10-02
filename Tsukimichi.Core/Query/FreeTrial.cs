using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Query;

/// <summary>
/// The free-trial view (feature plan v5, collector extras; R9 F3): with Settings › Display › "I'm on the free trial"
/// on, content the trial does not include is folded into a "Beyond your trial" group instead of reading Blocked. The
/// limits are the official ones as of 2026-04-28 (freetrial.finalfantasyxiv.com, and the announcement adding
/// Shadowbringers "up to and including Patch 5.5"): A Realm Reborn through Shadowbringers, every patch of them, and
/// level 80. Other trial restrictions (no Free Company, retainers, PvP or market board) are not modelled; the game
/// still answers for those. The view never turns itself on: whether an account is a trial account is the player's to say.
/// </summary>
public static class FreeTrial
{
    /// <summary>The last expansion the trial includes: Shadowbringers (ExVersion 3), through its last patch.</summary>
    public const byte LastExpansion = 3;

    /// <summary>The trial's level cap.</summary>
    public const byte LevelCap = 80;

    /// <summary>
    /// Whether the quest lies beyond the trial: an expansion after <see cref="LastExpansion"/>, or a quest that needs a
    /// level above <see cref="LevelCap"/> to accept.
    /// </summary>
    public static bool IsBeyond(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return quest.Expansion > LastExpansion || quest.Level > LevelCap;
    }
}
