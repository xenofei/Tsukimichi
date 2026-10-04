using System.Globalization;
using Tsukimichi.Core.Localization;

namespace Tsukimichi.Core.Sources;

/// <summary>How a quest reward can be had again.</summary>
public enum BuyBackKind : byte
{
    /// <summary>A shop sells it to anyone (no quest or achievement first): the reward is not missable at all.</summary>
    Sold,

    /// <summary>
    /// A shop sells it once the quest is done (a Calamity salvager's or recompense officer's reclaim menu, whose row
    /// names the quest): a reward thrown away can be bought back.
    /// </summary>
    BuyBack,
}

/// <summary>The shop a reward can be had again from, and how.</summary>
public sealed record BuyBack(BuyBackKind Kind, ShopOffer Offer)
{
    /// <summary>The NPC the line names; never null (<see cref="BuyBacks.For"/> keeps only shops an NPC opens).</summary>
    public Vendor Vendor => Offer.FirstVendor!;
}

/// <summary>
/// "Quest rewards you can buy back" (feature plan v7, 1.19.0, C6): whether a reward the quest hands out can be had
/// again after the quest, from the shops the game data lists (<see cref="ItemSources.Shops"/>), so a player can tell what
/// is truly missable from what a vendor sells back. It never claims a re-buy the data does not show: only shops an NPC
/// opens count, a reclaim row counts only for the quest it names (or for any quest when none is given), and a row that
/// wants an achievement instead never counts for a quest. Pure.
/// </summary>
public static class BuyBacks
{
    /// <summary>
    /// The best way to have the item again, or null when no shop sells it back. A gil or seal shop before any exchange;
    /// then a shop that sells it to anyone (the reward is not missable) before a reclaim row for
    /// <paramref name="questRowId"/>; then gil before seals, a known price before none, cheapest first, a vendor the
    /// data places before one it does not.
    /// </summary>
    /// <param name="questRowId">The quest that rewards the item; 0 when unknown, which lets any quest's reclaim row count.</param>
    public static BuyBack? For(ItemSources? sources, uint questRowId = 0)
    {
        if (sources is null || sources.Shops.Count == 0)
        {
            return null;
        }

        BuyBack? best = null;
        foreach (var offer in sources.Shops)
        {
            if (offer.FirstVendor is null || KindOf(offer, questRowId) is not { } kind)
            {
                continue;
            }

            var candidate = new BuyBack(kind, offer);
            if (best is null || Better(candidate, best))
            {
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>The mark a reward tile or a Moonlit row wears: "Re-buyable".</summary>
    public static string Mark => CoreText.T("Core.BuyBack.Mark", "Re-buyable");

    /// <summary>
    /// The line under a reward: "Can be bought back from Calamity salvager for 100 gil" or "Also sold by Material
    /// supplier, Limsa Lominsa Lower Decks (9.4, 11.2) for 20 gil"; the price is left out when the data does not give it.
    /// </summary>
    public static string Line(BuyBack buyBack)
    {
        ArgumentNullException.ThrowIfNull(buyBack);
        var vendor = SourceText.VendorWithPlace(buyBack.Vendor);
        var cost = SourceText.Cost(buyBack.Offer.Costs);
        return (buyBack.Kind, cost.Length > 0) switch
        {
            (BuyBackKind.BuyBack, true) => string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.BuyBack.BuyBackFor", "Can be bought back from {0} for {1}"), vendor, cost),
            (BuyBackKind.BuyBack, false) => string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.BuyBack.BuyBack", "Can be bought back from {0}"), vendor),
            (_, true) => string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.BuyBack.SoldFor", "Also sold by {0} for {1}"), vendor, cost),
            _ => string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.BuyBack.Sold", "Also sold by {0}"), vendor),
        };
    }

    /// <summary>The mark's tooltip: <see cref="Line"/> plus what it means for the reward.</summary>
    public static string Tooltip(BuyBack buyBack)
    {
        ArgumentNullException.ThrowIfNull(buyBack);
        var note = buyBack.Kind == BuyBackKind.BuyBack
            ? CoreText.T("Core.BuyBack.BuyBackNote", "Once the quest is done, a reward you sold or discarded can be had again.")
            : CoreText.T("Core.BuyBack.SoldNote", "Anyone can buy it, so the quest is not the only way to get it.");
        return Line(buyBack) + "\n" + note;
    }

    /// <summary>What a shop row means for a reward of <paramref name="questRowId"/>; null when it does not count.</summary>
    private static BuyBackKind? KindOf(ShopOffer offer, uint questRowId)
    {
        if (!offer.Gated)
        {
            return BuyBackKind.Sold;
        }

        if (offer.RequiredQuest == 0)
        {
            // An achievement's reclaim row: the quest alone never opens it.
            return null;
        }

        return questRowId == 0 || offer.RequiredQuest == questRowId ? BuyBackKind.BuyBack : null;
    }

    private static bool Better(BuyBack a, BuyBack b)
    {
        // A gil or seal shop before any exchange: an exchange may want the very gear it hands out in another cut (the
        // Calamity salvager's gender-specific swaps), so a reclaim row for gil says more.
        var exchangeA = a.Offer.Kind == ShopKind.Exchange;
        if (exchangeA != (b.Offer.Kind == ShopKind.Exchange))
        {
            return !exchangeA;
        }

        if (a.Kind != b.Kind)
        {
            return a.Kind == BuyBackKind.Sold;
        }

        if (a.Offer.Kind != b.Offer.Kind)
        {
            return KindRank(a.Offer.Kind) < KindRank(b.Offer.Kind);
        }

        var costA = a.Offer.Costs.Count > 0;
        var costB = b.Offer.Costs.Count > 0;
        if (costA != costB)
        {
            return costA;
        }

        var priceA = PriceOf(a.Offer);
        var priceB = PriceOf(b.Offer);
        if (priceA != priceB)
        {
            return priceA < priceB;
        }

        return a.Vendor.Spot is not null && b.Vendor.Spot is null;
    }

    private static int KindRank(ShopKind kind) => kind switch
    {
        ShopKind.Gil => 0,
        ShopKind.GrandCompany => 1,
        _ => 2,
    };

    private static ulong PriceOf(ShopOffer offer)
    {
        ulong total = 0;
        foreach (var cost in offer.Costs)
        {
            total += cost.Amount;
        }

        return total;
    }
}
