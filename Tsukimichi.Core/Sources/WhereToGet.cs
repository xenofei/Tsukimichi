using System.Globalization;
using System.Text;
using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Sources;

/// <summary>What a source names, in the order sources are offered.</summary>
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
}

/// <summary>One source of an item, in both of the forms the "Where" line uses.</summary>
/// <param name="Lead">The source leading the line: "Sold by Kurogai · 1,053 gil", "Crafted · Culinarian Lv 55".</param>
/// <param name="Or">The source after "·": "or crafted, Culinarian Lv 54".</param>
/// <param name="Spot">Where its vendor stands, for Flag and Teleport; null for a recipe, a node (never named) or a drop.</param>
/// <param name="Icon">The game icon the line shows at 14 px: the shop, the Crafting, Gathering or Fishing Log; 0 for none.</param>
/// <param name="Others">The other shops of its kind, one phrase each, for the tooltip.</param>
public sealed record WhereLine(WhereKind Kind, string Lead, string Or, WorldSpot? Spot, uint Icon, IReadOnlyList<string> Others);

/// <summary>
/// The "Where" line of one hand-in item: its first two sources joined by "· or", the icon of the first, the place a
/// Flag marks and whether Open Gathering Log applies; <see cref="Sources"/> keeps every source for the tooltip.
/// </summary>
public sealed record WhereSummary(string Text, uint Icon, WorldSpot? Spot, bool GatheringLog, IReadOnlyList<WhereLine> Sources);

/// <summary>
/// "Where to get hand-in items" (feature plan v7, 1.19.0, N5; spec-1.19 "N5"): the line under each item of the detail
/// pane's Hand in section, from <see cref="ItemSources"/>. One source per kind, best first: the cheapest gil vendor
/// ("Sold by Kurogai · 1,053 gil"), the crafters and their levels, gathering (which names no node: the game's own
/// Gathering Log shows them, spec decision 7), fishing, a Grand Company quartermaster, an exchange, the duties it drops
/// in. At most two show, joined by "· or". No market board source (its prices need a network service). Facts from the
/// data only, no advice. Pure.
/// </summary>
public static class WhereToGet
{
    /// <summary>How many sources the line names.</summary>
    public const int Shown = 2;

    /// <summary>Game icons: a shop marker, the Crafting Log, the Gathering Log and the Fishing Log (MainCommand icons).</summary>
    public const uint ShopIcon = 60412;
    public const uint CraftingLogIcon = 22;
    public const uint GatheringLogIcon = 23;
    public const uint FishingLogIcon = 24;

    private static readonly string[] NoOthers = [];

    /// <summary>Every source of the item, one per kind, in <see cref="WhereKind"/> order; empty when the data knows none.</summary>
    public static IReadOnlyList<WhereLine> Lines(ItemSources? sources)
    {
        if (sources is null)
        {
            return [];
        }

        var lines = new List<WhereLine>(4);
        AddShopLine(lines, sources, ShopKind.Gil, WhereKind.Vendor);
        if (sources.Crafts.Count > 0)
        {
            var jobs = CraftedJobs(sources.Crafts);
            lines.Add(new WhereLine(
                WhereKind.Crafted,
                F("Core.Where.Crafted", "Crafted · {0}", jobs),
                F("Core.Where.OrCrafted", "or crafted, {0}", jobs),
                null,
                CraftingLogIcon,
                NoOthers));
        }

        AddGatherLine(lines, sources, GatherKind.Gathered);
        AddGatherLine(lines, sources, GatherKind.Fish);
        AddShopLine(lines, sources, ShopKind.GrandCompany, WhereKind.GrandCompany);
        AddShopLine(lines, sources, ShopKind.Exchange, WhereKind.Exchange);
        if (sources.DropWhere.Length > 0)
        {
            lines.Add(new WhereLine(
                WhereKind.Drops,
                F("Core.Where.Drops", "Drops in {0}", sources.DropWhere),
                F("Core.Where.OrDrops", "or drops in {0}", sources.DropWhere),
                null,
                0,
                NoOthers));
        }

        return lines;
    }

    /// <summary>
    /// The line for an item: the first source's lead and the second's "or" form ("Sold by Kurogai · 1,053 gil · or
    /// crafted, Culinarian Lv 54"); null when the data knows no source.
    /// </summary>
    public static WhereSummary? Summary(ItemSources? sources) => Summary(Lines(sources));

    /// <inheritdoc cref="Summary(ItemSources?)"/>
    public static WhereSummary? Summary(IReadOnlyList<WhereLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
        {
            return null;
        }

        var text = new StringBuilder(lines[0].Lead);
        WorldSpot? spot = lines[0].Spot;
        var gatheringLog = lines[0].Kind == WhereKind.Gathered;
        for (var i = 1; i < lines.Count && i < Shown; i++)
        {
            text.Append(CoreText.T("Core.Where.Join", " · ")).Append(lines[i].Or);
            spot ??= lines[i].Spot;
            gatheringLog |= lines[i].Kind == WhereKind.Gathered;
        }

        return new WhereSummary(text.ToString(), lines[0].Icon, spot, gatheringLog, lines);
    }

    /// <summary>"Culinarian Lv 55" or "Blacksmith Lv 1, Armorer Lv 1": each crafter once, at its lowest recipe level.</summary>
    public static string CraftedJobs(IReadOnlyList<CraftOption> crafts)
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
                ? F("Core.Where.JobLevelStars", "{0} Lv {1} ({2}-star)", SourceText.Capitalized(craft.JobName), craft.Level, craft.Stars)
                : F("Core.Where.JobLevel", "{0} Lv {1}", SourceText.Capitalized(craft.JobName), craft.Level));
        }

        return jobs.ToString();
    }

    /// <summary>"Sold by Kurogai · 1,053 gil" and its kin for the other shops (<paramref name="or"/>: the "or sold by …" form).</summary>
    public static string ShopText(ShopOffer offer, WhereKind kind, bool or = false)
    {
        ArgumentNullException.ThrowIfNull(offer);
        var vendor = offer.FirstVendor is { } first ? SourceText.VendorInSentence(first) : offer.ShopName;
        var cost = SourceText.Cost(offer.Costs);
        var head = (kind, or) switch
        {
            (WhereKind.Exchange, false) => F("Core.Where.Exchange", "Exchanged by {0}", vendor),
            (WhereKind.Exchange, true) => F("Core.Where.OrExchange", "or exchanged by {0}", vendor),
            (_, false) => F("Core.Where.SoldBy", "Sold by {0}", vendor),
            _ => F("Core.Where.OrSoldBy", "or sold by {0}", vendor),
        };
        var text = cost.Length == 0 ? head : F("Core.Where.WithCost", "{0} · {1}", head, cost);
        return offer.Gated ? F("Core.Where.Gated", "{0} (after a quest or achievement)", text) : text;
    }

    /// <summary>"Mined · the Gathering Log shows the nodes" (or "or mined, see the Gathering Log"): no node is named.</summary>
    public static string GatherText(GatherMethod method, bool or = false) => (method, or) switch
    {
        (GatherMethod.Mining, false) => CoreText.T("Core.Where.Mined", "Mined · the Gathering Log shows the nodes"),
        (GatherMethod.Quarrying, false) => CoreText.T("Core.Where.Quarried", "Quarried · the Gathering Log shows the nodes"),
        (GatherMethod.Logging, false) => CoreText.T("Core.Where.Logged", "Logged · the Gathering Log shows the nodes"),
        (GatherMethod.Harvesting, false) => CoreText.T("Core.Where.Harvested", "Harvested · the Gathering Log shows the nodes"),
        (GatherMethod.Spearfishing, false) => CoreText.T("Core.Where.Spearfished", "Spearfished · the Fishing Log shows the spots"),
        (GatherMethod.Fishing, false) => CoreText.T("Core.Where.Fished", "Fished · the Fishing Log shows the holes"),
        (GatherMethod.Mining, true) => CoreText.T("Core.Where.OrMined", "or mined, see the Gathering Log"),
        (GatherMethod.Quarrying, true) => CoreText.T("Core.Where.OrQuarried", "or quarried, see the Gathering Log"),
        (GatherMethod.Logging, true) => CoreText.T("Core.Where.OrLogged", "or logged, see the Gathering Log"),
        (GatherMethod.Harvesting, true) => CoreText.T("Core.Where.OrHarvested", "or harvested, see the Gathering Log"),
        (GatherMethod.Spearfishing, true) => CoreText.T("Core.Where.OrSpearfished", "or spearfished, see the Fishing Log"),
        _ => CoreText.T("Core.Where.OrFished", "or fished, see the Fishing Log"),
    };

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

            // Shops come cheapest and ungated first within a kind (ItemSourceIndex sorts them), so the first one leads;
            // the tooltip names the others with their places.
            var phrase = ShopText(offer, kind);
            if (best is null)
            {
                best = offer;
                bestText = phrase;
                continue;
            }

            var place = offer.FirstVendor.Spot is { } at ? F("Core.Where.AtPlace", "{0} ({1})", phrase, SourceText.Spot(at)) : phrase;
            if (!others.Contains(place))
            {
                others.Add(place);
            }
        }

        if (best is not null)
        {
            lines.Add(new WhereLine(kind, bestText, ShopText(best, kind, or: true), best.FirstVendor!.Spot, ShopIcon, others.Count == 0 ? NoOthers : others));
        }
    }

    private static void AddGatherLine(List<WhereLine> lines, ItemSources sources, GatherKind kind)
    {
        // The node is never named (spec decision 7): the line says how, and the game's own log shows where.
        foreach (var spot in sources.Gathering)
        {
            if (spot.Kind == kind)
            {
                lines.Add(new WhereLine(
                    kind == GatherKind.Fish ? WhereKind.Fished : WhereKind.Gathered,
                    GatherText(spot.Method),
                    GatherText(spot.Method, or: true),
                    null,
                    kind == GatherKind.Fish ? FishingLogIcon : GatheringLogIcon,
                    NoOthers));
                return;
            }
        }
    }

    private static string F(string key, string english, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, CoreText.T(key, english), args);
}
