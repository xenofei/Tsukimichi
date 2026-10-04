using System.Globalization;
using Tsukimichi.Core.Localization;

namespace Tsukimichi.Core.Sources;

/// <summary>Where a reward of a finished quest is now, as far as the reads tell.</summary>
public enum RewardWhereabouts : byte
{
    /// <summary>
    /// The reads cannot tell (another character on view, the game read paused, or a place that may hold it not read:
    /// the saddlebag before it is opened this session, the retainers without Allagan Tools).
    /// </summary>
    Unknown,

    /// <summary>In the inventory or worn: nothing to say.</summary>
    OnYou,

    /// <summary>In the armoury chest.</summary>
    InArmoury,

    /// <summary>In the saddlebag.</summary>
    InSaddlebag,

    /// <summary>With a retainer (Allagan Tools).</summary>
    WithRetainers,

    /// <summary>Nowhere the reads look, and every place was read (the saddlebag loaded, the retainers counted).</summary>
    NotHeld,
}

/// <summary>A reward's state line: its words and whether it is the buy-back line (which wears the gold dot and offers Flag and Teleport).</summary>
public sealed record RewardStateLine(string Text, BuyBack? BuyBack)
{
    /// <summary>
    /// The line names a shop for an item the reads could not place (<see cref="RewardWhereabouts.Unknown"/>): the
    /// character may still have it, so the shop is a fact, not a call to act.
    /// </summary>
    public bool IfGone { get; init; }

    /// <summary>
    /// Whether the line says where to have it again for an item known to be missing: the gold dot, Flag and Teleport
    /// (spec-1.19 "C6"). Never for an <see cref="IfGone"/> line.
    /// </summary>
    public bool IsBuyBack => BuyBack is not null && !IfGone;
}

/// <summary>
/// The state line under a reward of a finished quest (feature plan v7, 1.19.0, C6; spec-1.19 "C6"): for a roll, minion,
/// card or emote book not learned, "Done, not learned · reclaim at a recompense officer, 100 gil"; for an item not on
/// the character, "Not on you · buy it back from a Calamity salvager, 100 gil"; where it is when it is elsewhere ("In
/// your armoury chest"); and, for a reward the quest alone gives, "Not offered by the Salvager" when no shop sells it
/// back. Nothing for a reward on the character or learned. It never says which optional reward was picked: the game
/// does not record it. Pure.
/// </summary>
public static class RewardStates
{
    /// <summary>The line, or null when there is nothing to say.</summary>
    /// <param name="collectible">A reward the game learns (an unlock flag: roll, minion, card, emote, mount…).</param>
    /// <param name="learned">The unlock flag; null when it cannot be read or the reward is not one.</param>
    /// <param name="where">Where the item is now.</param>
    /// <param name="buyBack">How a shop sells it back (<see cref="BuyBacks.For"/>); null when none does.</param>
    /// <param name="exclusive">The quest is the only way to it (a shipped unique reward): only then does "Not offered" say anything.</param>
    public static RewardStateLine? For(bool collectible, bool? learned, RewardWhereabouts where, BuyBack? buyBack, bool exclusive)
    {
        if (collectible && learned == true)
        {
            return null;
        }

        if (collectible && learned == false)
        {
            var head = CoreText.T("Core.RewardState.NotLearned", "Done, not learned");
            if (where == RewardWhereabouts.OnYou)
            {
                return new RewardStateLine(Join(head, CoreText.T("Core.RewardState.OnYou", "it is on you")), null);
            }

            if (Place(where) is { } place)
            {
                return new RewardStateLine(Join(head, place), null);
            }

            if (buyBack is not null)
            {
                return new RewardStateLine(Join(head, Reclaim(buyBack)), buyBack);
            }

            return exclusive ? new RewardStateLine(Join(head, NotOffered(lower: true)), null) : null;
        }

        switch (where)
        {
            case RewardWhereabouts.OnYou:
                return null;
            case RewardWhereabouts.InArmoury or RewardWhereabouts.InSaddlebag or RewardWhereabouts.WithRetainers:
                return new RewardStateLine(SourceText.Capitalized(Place(where)!), null);
        }

        if (buyBack is not null)
        {
            var again = BuyItBack(buyBack);
            if (where == RewardWhereabouts.NotHeld)
            {
                return new RewardStateLine(Join(CoreText.T("Core.RewardState.NotOnYou", "Not on you"), again), buyBack);
            }

            // The reads cannot place it: the shop is named, without "Not on you", the gold dot or the actions.
            var text = buyBack.Kind == BuyBackKind.BuyBack
                ? Join(CoreText.T("Core.RewardState.IfGone", "If you no longer have it"), again)
                : SourceText.Capitalized(again);
            return new RewardStateLine(text, buyBack) { IfGone = true };
        }

        return exclusive ? new RewardStateLine(NotOffered(lower: false), null) : null;
    }

    /// <summary>
    /// Where the item is from the logged-in character's count (<see cref="HandIn.HandInCount"/>): on the character, in
    /// the armoury chest, the saddlebag or with a retainer, or not held; unknown when the game was not read, or when a
    /// place that may hold it was not read (the saddlebag before it is opened this session, the retainers without
    /// Allagan Tools): "not read" is never "not held".
    /// </summary>
    public static RewardWhereabouts WhereaboutsOf(HandIn.HandInCount count)
    {
        // While the game read is paused, Allagan Tools' count of the character stands in (it cannot tell the containers).
        if (count.OnYou == true || (count.OnYou is null && count.Held is > 0))
        {
            return RewardWhereabouts.OnYou;
        }

        if (count.Armoury is > 0)
        {
            return RewardWhereabouts.InArmoury;
        }

        if (count.Saddlebag is > 0)
        {
            return RewardWhereabouts.InSaddlebag;
        }

        if (count.Retainers is > 0)
        {
            return RewardWhereabouts.WithRetainers;
        }

        return count.OnYou == false && count.Saddlebag is not null && count.Retainers is not null
            ? RewardWhereabouts.NotHeld
            : RewardWhereabouts.Unknown;
    }

    /// <summary>"reclaim at a recompense officer, 100 gil" (the price only when the shop row has one).</summary>
    public static string Reclaim(BuyBack buyBack)
    {
        ArgumentNullException.ThrowIfNull(buyBack);
        return WithCost(F("Core.RewardState.Reclaim", "reclaim at {0}", SourceText.VendorInSentence(buyBack.Vendor)), buyBack);
    }

    /// <summary>"buy it back from a Calamity salvager, 100 gil", or "also sold by …" for a shop anyone can use.</summary>
    public static string BuyItBack(BuyBack buyBack)
    {
        ArgumentNullException.ThrowIfNull(buyBack);
        var vendor = SourceText.VendorInSentence(buyBack.Vendor);
        return WithCost(
            buyBack.Kind == BuyBackKind.BuyBack
                ? F("Core.RewardState.BuyItBack", "buy it back from {0}", vendor)
                : F("Core.RewardState.AlsoSold", "also sold by {0}", vendor),
            buyBack);
    }

    private static string WithCost(string text, BuyBack buyBack)
    {
        var cost = SourceText.Cost(buyBack.Offer.Costs);
        return cost.Length == 0 ? text : F("Core.RewardState.WithCost", "{0}, {1}", text, cost);
    }

    private static string NotOffered(bool lower) => lower
        ? CoreText.T("Core.RewardState.NotOfferedLower", "not offered by the Salvager")
        : CoreText.T("Core.RewardState.NotOffered", "Not offered by the Salvager");

    private static string? Place(RewardWhereabouts where) => where switch
    {
        RewardWhereabouts.InArmoury => CoreText.T("Core.RewardState.InArmoury", "in your armoury chest"),
        RewardWhereabouts.InSaddlebag => CoreText.T("Core.RewardState.InSaddlebag", "in your saddlebag"),
        RewardWhereabouts.WithRetainers => CoreText.T("Core.RewardState.WithRetainers", "with your retainers"),
        _ => null,
    };

    private static string Join(string a, string b) => F("Core.RewardState.Join", "{0} · {1}", a, b);

    private static string F(string key, string english, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, CoreText.T(key, english), args);
}
