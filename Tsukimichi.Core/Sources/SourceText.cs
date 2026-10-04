using System.Globalization;
using System.Text;
using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Core.Sources;

/// <summary>
/// The phrases the buy-back mark and the "Where" lines share: a vendor with its place, a price, a spot. Facts from the
/// data only; nothing here guesses a place or a price the data does not hold.
/// </summary>
public static class SourceText
{
    /// <summary>"Calamity salvager", the NPC's name with its first letter raised (the game writes generic NPCs lower case).</summary>
    public static string VendorName(Vendor vendor)
    {
        ArgumentNullException.ThrowIfNull(vendor);
        return Capitalized(vendor.Name);
    }

    /// <summary>
    /// The vendor as a sentence names it, in the game's own letter case: "a Calamity salvager", "an independent
    /// merchant" for a role (<see cref="Vendor.Generic"/>), the name alone for a person ("Kurogai"); a person the story
    /// has not introduced by the placeholder of <paramref name="spoilers"/> ("Someone ahead (Lv 61)", 1.20.0 N6).
    /// </summary>
    public static string VendorInSentence(Vendor vendor, SpoilerMask? spoilers = null)
    {
        ArgumentNullException.ThrowIfNull(vendor);
        var name = vendor.Name;
        if (!vendor.Generic)
        {
            return spoilers?.Name(SpoilerKind.Npc, name) ?? name;
        }

        return string.Format(
            CultureInfo.CurrentCulture,
            vendor.StartsWithVowel ? CoreText.T("Core.Sources.AnVendor", "an {0}") : CoreText.T("Core.Sources.AVendor", "a {0}"),
            name);
    }

    /// <summary>
    /// "a Calamity salvager, Ul'dah - Steps of Thal (11.2, 9.8)" (<see cref="VendorInSentence"/> and the place), or the
    /// vendor alone when the data does not place it.
    /// </summary>
    public static string VendorWithPlace(Vendor vendor, SpoilerMask? spoilers = null)
    {
        ArgumentNullException.ThrowIfNull(vendor);
        var name = VendorInSentence(vendor, spoilers);
        return vendor.Spot is { } spot ? string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Sources.VendorAt", "{0}, {1}"), name, Spot(spot, spoilers)) : name;
    }

    /// <summary>
    /// "Ul'dah - Steps of Thal (11.2, 9.8)": the zone and the map coordinates the game prints; a zone the story has not
    /// reached by the placeholder of <paramref name="spoilers"/>, without coordinates (1.20.0 N6).
    /// </summary>
    public static string Spot(WorldSpot spot, SpoilerMask? spoilers = null)
    {
        ArgumentNullException.ThrowIfNull(spot);
        if (spoilers is not null && spoilers.IsNameMasked(SpoilerKind.Area, spot.Zone))
        {
            return spoilers.Name(SpoilerKind.Area, spot.Zone);
        }

        return string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Sources.Spot", "{0} ({1:0.0}, {2:0.0})"), spot.Zone, spot.MapX, spot.MapY);
    }

    /// <summary>Whether <paramref name="spoilers"/> hides the vendor's name or the zone it stands in (1.20.0 N6).</summary>
    public static bool Shielded(Vendor vendor, SpoilerMask? spoilers)
    {
        ArgumentNullException.ThrowIfNull(vendor);
        return spoilers is { MasksNames: true }
            && ((!vendor.Generic && spoilers.IsNameMasked(SpoilerKind.Npc, vendor.Name)) || (vendor.Spot is { } spot && spoilers.IsNameMasked(SpoilerKind.Area, spot.Zone)));
    }

    /// <summary>"1,500 gil", "200 Storm Seals", "3 Bicolor Gemstones + 100 gil"; empty when the costs are unknown.</summary>
    public static string Cost(IReadOnlyList<ShopCost> costs)
    {
        ArgumentNullException.ThrowIfNull(costs);
        if (costs.Count == 0)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        foreach (var cost in costs)
        {
            if (text.Length > 0)
            {
                text.Append(CoreText.T("Core.Sources.CostJoin", " + "));
            }

            text.Append(cost.IsGil
                ? string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Sources.Gil", "{0:N0} gil"), cost.Amount)
                : string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Sources.Currency", "{0:N0} {1}"), cost.Amount, cost.Name));
        }

        return text.ToString();
    }

    /// <summary>The text with its first letter raised in the current culture; unchanged when empty or already raised.</summary>
    public static string Capitalized(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0 || !char.IsLower(text[0]))
        {
            return text;
        }

        return char.ToUpper(text[0], CultureInfo.CurrentCulture) + text[1..];
    }
}
