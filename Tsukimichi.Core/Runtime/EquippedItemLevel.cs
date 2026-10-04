namespace Tsukimichi.Core.Runtime;

/// <summary>
/// The average item level of the equipped gear, as the character window reckons it (feature plan v7 C7): the item
/// levels of the twelve gear slots summed and divided by twelve, the main hand counted twice when nothing is in the
/// off hand (a two-handed weapon), the soul crystal and the retired waist slot left out. Pure.
/// </summary>
public static class EquippedItemLevel
{
    /// <summary>The equipped container's slots, in the game's order.</summary>
    public const int MainHand = 0;
    public const int OffHand = 1;
    public const int Waist = 5;
    public const int SoulCrystal = 13;

    /// <summary>The slots the average divides by.</summary>
    public const int Slots = 12;

    /// <summary>
    /// The average over <paramref name="slotLevels"/>, indexed like the equipped container (0 the main hand, 1 the off
    /// hand, 13 the soul crystal); an empty slot reads 0. Rounds down, as the game does. 0 with no main hand: nothing
    /// to judge a wall by.
    /// </summary>
    public static ushort Average(ReadOnlySpan<ushort> slotLevels)
    {
        if (slotLevels.Length <= MainHand || slotLevels[MainHand] == 0)
        {
            return 0;
        }

        var sum = 0;
        for (var slot = 0; slot < slotLevels.Length && slot < SoulCrystal; slot++)
        {
            if (slot == Waist)
            {
                continue;
            }

            sum += slotLevels[slot];
        }

        if (slotLevels.Length <= OffHand || slotLevels[OffHand] == 0)
        {
            sum += slotLevels[MainHand];
        }

        return (ushort)Math.Min(sum / Slots, ushort.MaxValue);
    }
}
