namespace Tsukimichi.Core.Runtime;

/// <summary>
/// Turns the client's custom delivery rank slots (<c>SatisfactionSupplyManager.SatisfactionRanks</c>, slot = SatisfactionNpc
/// row id − 1) into <see cref="Model.CharacterSnapshot.SatisfactionRanks"/>. Pure, so the rule is testable without the game.
/// </summary>
public static class SatisfactionRankSlots
{
    /// <summary>
    /// Every slot keyed by SatisfactionNpc row id (slot + 1), rank 0 included, so a client not yet unlocked reads rank 0
    /// beside ones that are. When every slot reads 0 the result is empty ("not checked"): the manager may be filled only
    /// once the server sends the custom delivery data, and twelve zeros then would block every rank quest for a poll
    /// interval after login. A character with no client unlocked loses nothing, since each rank quest is also held back
    /// by its unlock quest's prerequisites.
    /// </summary>
    public static Dictionary<byte, byte> ToRanks(ReadOnlySpan<byte> slots)
    {
        var count = Math.Min(slots.Length, byte.MaxValue);
        var result = new Dictionary<byte, byte>(count);
        if (!slots[..count].ContainsAnyExcept((byte)0))
        {
            return result;
        }

        for (var slot = 0; slot < count; slot++)
        {
            result[(byte)(slot + 1)] = slots[slot];
        }

        return result;
    }
}
