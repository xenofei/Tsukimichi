using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.HandIn;

/// <summary>
/// How many of a hand-in item the logged-in character holds, as far as the reads can tell (1.6.0): the game's own count
/// splits normal from high quality, Allagan Tools' does not. A hand-in that asks for high quality therefore counts only
/// what the game says is HQ; Allagan Tools' numbers (the retainers, or the character itself while the game read is
/// paused) are shown, labelled "NQ+HQ", but never make an HQ item enough. Any hand-in that takes a normal item takes a
/// high-quality one too, so for those everything counts.
/// </summary>
/// <param name="Held">
/// What the character itself holds, NQ and HQ together: bags, armoury, equipped and saddlebag from the game, or Allagan
/// Tools' count of the character while the game read is paused; null when neither can tell.
/// </param>
/// <param name="HeldHq">The high-quality part of <paramref name="Held"/>, from the game only; null when the game was not read.</param>
/// <param name="Retainers">What the retainers hold, NQ and HQ together (Allagan Tools); null without it.</param>
public readonly record struct HandInCount(int? Held, int? HeldHq, int? Retainers)
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
}
