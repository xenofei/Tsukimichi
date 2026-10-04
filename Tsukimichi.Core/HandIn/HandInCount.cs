using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.HandIn;

/// <summary>Where on the character an item sits that the game does not count for a hand-in (1.19, N5).</summary>
public enum HandInPlace : byte
{
    /// <summary>Nowhere else, or the reads cannot tell.</summary>
    None,

    /// <summary>The saddlebag.</summary>
    Saddlebag,

    /// <summary>The armoury chest.</summary>
    Armoury,
}

/// <summary>
/// How many of a hand-in item the logged-in character holds, as far as the reads can tell (1.6.0): the game's own count
/// splits normal from high quality, Allagan Tools' does not. A hand-in that asks for high quality therefore counts only
/// what the game says is HQ; Allagan Tools' numbers (the retainers, or the character itself while the game read is
/// paused) are shown, labelled "NQ+HQ", but never make an HQ item enough. Any hand-in that takes a normal item takes a
/// high-quality one too, so for those everything counts.
/// <para>
/// Since 1.19 (N5, spec-1.19 "N5") <see cref="Held"/> is the inventory only, what the game counts when the item is
/// handed over; what the saddlebag and the armoury chest hold is kept apart (<see cref="Saddlebag"/>,
/// <see cref="Armoury"/>) so the pane can say "1 in your saddlebag" and "take it out of the saddlebag".
/// </para>
/// </summary>
/// <param name="Held">
/// What the character's inventory holds, NQ and HQ together, from the game; or Allagan Tools' count of the character
/// while the game read is paused (which cannot tell the containers apart); null when neither can tell.
/// </param>
/// <param name="HeldHq">The high-quality part of <paramref name="Held"/>, from the game only; null when the game was not read.</param>
/// <param name="Retainers">What the retainers hold, NQ and HQ together (Allagan Tools); null without it.</param>
/// <param name="Saddlebag">What the saddlebag holds (the game, once the saddlebag was opened this session); null when not read.</param>
/// <param name="Armoury">What the armoury chest holds, NQ and HQ (the game); null when not read.</param>
/// <param name="Equipped">What the character wears (the game); null when not read.</param>
public readonly record struct HandInCount(int? Held, int? HeldHq, int? Retainers, int? Saddlebag = null, int? Armoury = null, int? Equipped = null)
{
    /// <summary>Everything counted, NQ and HQ, character and retainers; null when nothing is known.</summary>
    public int? Total => Held is null && Retainers is null ? null : (Held ?? 0) + (Retainers ?? 0);

    /// <summary>True when some number is known at all.</summary>
    public bool Known => Held is not null || Retainers is not null;

    /// <summary>
    /// True when a shown number mixes NQ and HQ: the retainers' count, or the character's while only Allagan Tools
    /// counted it. An HQ row labels those "(NQ+HQ)".
    /// </summary>
    public bool HasMixed => Retainers is not null || (Held is not null && HeldHq is null);

    /// <summary>
    /// What counts toward the item: for one that must be high quality, only what the game says is HQ (null when the
    /// game was not read); otherwise <see cref="Total"/>.
    /// </summary>
    public int? Usable(bool hq) => hq ? HeldHq : Total;

    /// <summary>What counts toward <paramref name="item"/> (<see cref="Usable"/> for its quality).</summary>
    public int? UsableFor(HandInItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Usable(item.IsHq);
    }

    /// <summary>True when what counts toward <paramref name="item"/> covers what the quest asks for.</summary>
    public bool IsEnough(HandInItem item) => UsableFor(item) is { } usable && usable >= item.Needed;

    /// <summary>
    /// Where on the character more of the item sits than the inventory holds, while the inventory alone is not enough
    /// (spec-1.19 "N5": the game counts the inventory only): the saddlebag first, then the armoury chest; none otherwise.
    /// An HQ item never names a place, since those reads cannot tell HQ apart.
    /// </summary>
    public HandInPlace MisplacedIn(HandInItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.IsHq || HeldHq is null || (Held ?? 0) >= item.Needed)
        {
            return HandInPlace.None;
        }

        return Saddlebag is > 0 ? HandInPlace.Saddlebag : Armoury is > 0 ? HandInPlace.Armoury : HandInPlace.None;
    }

    /// <summary>How many sit in <paramref name="place"/>; zero for <see cref="HandInPlace.None"/> or when not read.</summary>
    public int CountIn(HandInPlace place) => place switch
    {
        HandInPlace.Saddlebag => Saddlebag ?? 0,
        HandInPlace.Armoury => Armoury ?? 0,
        _ => 0,
    };

    /// <summary>
    /// Whether the character carries the item (inventory or worn), as far as the game read tells; null when the game
    /// was not read (1.19, C6: a reward "on you" needs no buy-back line).
    /// </summary>
    public bool? OnYou => HeldHq is null ? null : (Held ?? 0) + (Equipped ?? 0) > 0;
}
