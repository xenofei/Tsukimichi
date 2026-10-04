using System.Globalization;
using System.Text;
using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Sources;

/// <summary>What a "Where" line names, in the order the lines come.</summary>
public enum WhereKind : byte
{
    /// <summary>A gil shop sells it.</summary>
    Vendor,

    /// <summary>A recipe makes it.</summary>
    Crafted,

    /// <summary>A mining or botany node yields it.</summary>
    Gathered,

    /// <summary>A fishing hole or spearfishing spot yields it.</summary>
    Fished,

    /// <summary>A Grand Company quartermaster sells it for seals.</summary>
    GrandCompany,

    /// <summary>An exchange (tomestones, scrips, MGP, other items) hands it out.</summary>
    Exchange,

    /// <summary>A duty drops it (the curated other sources).</summary>
    Drops,

    /// <summary>Nothing above, but it can be bought on the market board.</summary>
    MarketOnly,
}

/// <summary>One "Where" line: what it says, the place a Flag marks (null when none), and how many more of its kind there are.</summary>
/// <param name="More">Other shops or spots of the same kind the line does not name; the tooltip lists them.</param>
/// <param name="Others">Those others, one phrase each, for the tooltip.</param>
public sealed record WhereLine(WhereKind Kind, string Text, WorldSpot? Spot, int More, IReadOnlyList<string> Others);

/// <summary>
/// "Where to get hand-in items" (feature plan v7, 1.19.0, N5): the "Where" lines under each item of the detail pane's
/// Hand in section, from <see cref="ItemSources"/>: the cheapest gil vendor with its place and price, the crafters and
/// their levels, the lowest gathering node or fishing hole with its zone, a Grand Company quartermaster, an exchange, the
/// duties it drops in, or "Market board only". One line per kind, best first; facts from the data only, no advice
/// (feature-ideas.md, item 10). Pure.
/// </summary>
public static class WhereToGet
{
    private static readonly string[] NoOthers = [];

    /// <summary>The lines for an item, in <see cref="WhereKind"/> order; empty when the data knows no source.</summary>
    public static IReadOnlyList<WhereLine> Lines(ItemSources? sources)
    {
        if (sources is null || !sources.Any)
        {
            return [];
        }

        var lines = new List<WhereLine>(4);
        AddShopLine(lines, sources, ShopKind.Gil, WhereKind.Vendor);
        if (sources.Crafts.Count > 0)
        {
            lines.Add(new WhereLine(WhereKind.Crafted, CraftedText(sources.Crafts), null, 0, NoOthers));
        }

        AddGatherLine(lines, sources, GatherKind.Gathered, WhereKind.Gathered);
        AddGatherLine(lines, sources, GatherKind.Fish, WhereKind.Fished);
        AddShopLine(lines, sources, ShopKind.GrandCompany, WhereKind.GrandCompany);
        AddShopLine(lines, sources, ShopKind.Exchange, WhereKind.Exchange);
        if (sources.DropWhere.Length > 0)
        {
            lines.Add(new WhereLine(
                WhereKind.Drops,
                string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Where.Drops", "Drops in {0}"), sources.DropWhere),
                null,
                0,
                NoOthers));
        }

        if (lines.Count == 0 && sources.Marketable)
        {
            lines.Add(new WhereLine(WhereKind.MarketOnly, CoreText.T("Core.Where.MarketOnly", "Market board only"), null, 0, NoOthers));
        }

        return lines;
    }

    /// <summary>"Crafted: Carpenter Lv. 15, Blacksmith Lv. 12": each crafter once, at its lowest recipe level.</summary>
    public static string CraftedText(IReadOnlyList<CraftOption> crafts)
    {
        ArgumentNullException.ThrowIfNull(crafts);
        var lowest = new List<CraftOption>(crafts.Count);
        foreach (var craft in crafts)
        {
            var at = lowest.FindIndex(c => c.CraftType == craft.CraftType);
            if (at < 0)
            {
                lowest.Add(craft);
            }
            else if (craft.Level < lowest[at].Level)
            {
                lowest[at] = craft;
            }
        }

        var jobs = new StringBuilder();
        foreach (var craft in lowest)
        {
            if (jobs.Length > 0)
            {
                jobs.Append(CoreText.T("Core.Where.ListJoin", ", "));
            }

            jobs.Append(craft.Stars > 0
                ? string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Where.JobLevelStars", "{0} Lv. {1} ({2}-star)"), SourceText.Capitalized(craft.JobName), craft.Level, craft.Stars)
                : string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Where.JobLevel", "{0} Lv. {1}"), SourceText.Capitalized(craft.JobName), craft.Level));
        }

        return string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Where.Crafted", "Crafted: {0}"), jobs);
    }

    /// <summary>"Sold by Material supplier, Limsa Lominsa Lower Decks (9.4, 11.2) · 20 gil" and its kin for the other shops.</summary>
    public static string ShopText(ShopOffer offer, WhereKind kind)
    {
        ArgumentNullException.ThrowIfNull(offer);
        var vendor = offer.FirstVendor is { } first ? SourceText.VendorWithPlace(first) : offer.ShopName;
        var cost = SourceText.Cost(offer.Costs);
        var head = kind switch
        {
            WhereKind.GrandCompany => string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Where.GrandCompany", "Grand Company: {0}"), vendor),
            WhereKind.Exchange => string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Where.Exchange", "Exchange: {0}"), vendor),
            _ => string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Where.SoldBy", "Sold by {0}"), vendor),
        };
        var text = cost.Length == 0 ? head : string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Where.WithCost", "{0} · {1}"), head, cost);
        return offer.Gated ? string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Where.Gated", "{0} (after a quest or achievement)"), text) : text;
    }

    /// <summary>"Gathered: Miner Lv. 25 · Black Brush, Central Thanalan (24.1, 18.0)", or "Fished: Lv. 20 · …"; "(timed)" for a timed node.</summary>
    public static string GatherText(GatherSpot spot)
    {
        ArgumentNullException.ThrowIfNull(spot);
        string text;
        if (spot.Spot is not { } at)
        {
            // A node the sheets do not place (an allied society quest's own): the job and level only.
            text = spot.Kind == GatherKind.Fish
                ? string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Where.FishedUnplaced", "Fished: Lv. {0}"), spot.Level)
                : string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Where.GatheredUnplaced", "Gathered: {0} Lv. {1}"), SourceText.Capitalized(spot.JobName), spot.Level);
        }
        else
        {
            var where = spot.Place.Length > 0 && !string.Equals(spot.Place, at.Zone, StringComparison.Ordinal)
                ? string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Where.PlaceInZone", "{0}, {1}"), spot.Place, SourceText.Spot(at))
                : SourceText.Spot(at);
            text = spot.Kind == GatherKind.Fish
                ? string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Where.Fished", "Fished: Lv. {0} · {1}"), spot.Level, where)
                : string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Where.Gathered", "Gathered: {0} Lv. {1} · {2}"), SourceText.Capitalized(spot.JobName), spot.Level, where);
        }

        return spot.Timed ? string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Where.Timed", "{0} (timed)"), text) : text;
    }

    /// <summary>"+2 more", for a line that names one of several.</summary>
    public static string MoreText(int more) =>
        more <= 0 ? string.Empty : string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Where.More", "+{0} more"), more);

    private static void AddShopLine(List<WhereLine> lines, ItemSources sources, ShopKind shopKind, WhereKind kind)
    {
        ShopOffer? best = null;
        var bestText = string.Empty;
        var others = new List<string>();
        foreach (var offer in sources.Shops)
        {
            if (offer.Kind != shopKind || offer.FirstVendor is null)
            {
                continue;
            }

            // Shops come cheapest and ungated first within a kind (ItemSourceIndex sorts them), so the first one leads.
            var phrase = ShopText(offer, kind);
            if (best is null)
            {
                best = offer;
                bestText = phrase;
            }
            else if (!others.Contains(phrase) && !string.Equals(phrase, bestText, StringComparison.Ordinal))
            {
                others.Add(phrase);
            }
        }

        if (best is not null)
        {
            lines.Add(new WhereLine(kind, bestText, best.FirstVendor!.Spot, others.Count, others.Count == 0 ? NoOthers : others));
        }
    }

    private static void AddGatherLine(List<WhereLine> lines, ItemSources sources, GatherKind gatherKind, WhereKind kind)
    {
        GatherSpot? best = null;
        var bestText = string.Empty;
        var others = new List<string>();
        foreach (var spot in sources.Gathering)
        {
            if (spot.Kind != gatherKind)
            {
                continue;
            }

            // Spots come placed, untimed and lowest first (ItemSourceIndex sorts them).
            var phrase = GatherText(spot);
            if (best is null)
            {
                best = spot;
                bestText = phrase;
            }
            else if (!others.Contains(phrase) && !string.Equals(phrase, bestText, StringComparison.Ordinal))
            {
                others.Add(phrase);
            }
        }

        if (best is not null)
        {
            lines.Add(new WhereLine(kind, bestText, best.Spot, others.Count, others.Count == 0 ? NoOthers : others));
        }
    }
}
