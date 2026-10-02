using System.Globalization;
using System.Text;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.HandIn;

/// <summary>One item still to get: how many more the quests need than the character holds.</summary>
public sealed record MissingItem(uint ItemId, string Name, int Quantity);

/// <summary>
/// "Copy missing items" (feature plan v5, C4 #4): what the selected quest, or every quest of a list, still needs, and
/// the two clipboard forms of it. A Teamcraft import link
/// (<c>https://ffxivteamcraft.com/import/&lt;base64 of "itemId,null,qty;itemId,null,qty"&gt;</c>; Teamcraft's import page
/// URI-decodes the segment and then base64-decodes it, so the base64 is percent-encoded) and the plain text list Teamcraft
/// and Artisan both import ("3x Item Name", one per line). Only text is produced; nothing is sent anywhere.
/// </summary>
public static class MissingItems
{
    public const string TeamcraftImportBase = "https://ffxivteamcraft.com/import/";

    /// <summary>
    /// The items of <paramref name="quests"/> the character still needs: each item's <see cref="HandInItem.Needed"/>
    /// summed over the quests (one when the data has no amount), less what <paramref name="owned"/> says the character
    /// holds (null reads as none). Items held in full are left out; first-seen order.
    /// </summary>
    public static IReadOnlyList<MissingItem> For(IEnumerable<QuestRecord> quests, Func<uint, int?> owned)
    {
        ArgumentNullException.ThrowIfNull(quests);
        ArgumentNullException.ThrowIfNull(owned);
        var needed = new Dictionary<uint, (string Name, int Quantity)>();
        var order = new List<uint>();
        foreach (var quest in quests)
        {
            foreach (var item in quest.HandInItems)
            {
                if (needed.TryGetValue(item.ItemId, out var known))
                {
                    needed[item.ItemId] = (known.Name, known.Quantity + item.Needed);
                }
                else
                {
                    needed[item.ItemId] = (item.Name, item.Needed);
                    order.Add(item.ItemId);
                }
            }
        }

        var result = new List<MissingItem>(order.Count);
        foreach (var id in order)
        {
            var (name, quantity) = needed[id];
            var missing = quantity - Math.Max(0, owned(id) ?? 0);
            if (missing > 0)
            {
                result.Add(new MissingItem(id, name, missing));
            }
        }

        return result;
    }

    /// <summary>The Teamcraft import link for <paramref name="items"/>; null when there are none.</summary>
    public static string? TeamcraftLink(IEnumerable<MissingItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var rows = new StringBuilder();
        foreach (var item in items)
        {
            if (item.ItemId == 0 || item.Quantity <= 0)
            {
                continue;
            }

            if (rows.Length > 0)
            {
                rows.Append(';');
            }

            rows.Append(item.ItemId.ToString(CultureInfo.InvariantCulture))
                .Append(",null,")
                .Append(item.Quantity.ToString(CultureInfo.InvariantCulture));
        }

        if (rows.Length == 0)
        {
            return null;
        }

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(rows.ToString()));
        return TeamcraftImportBase + Uri.EscapeDataString(base64);
    }

    /// <summary>The text list ("3x Item Name" per line, no trailing newline); empty when there are none.</summary>
    public static string TextList(IEnumerable<MissingItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var text = new StringBuilder();
        foreach (var item in items)
        {
            if (item.Quantity <= 0 || item.Name.Length == 0)
            {
                continue;
            }

            if (text.Length > 0)
            {
                text.Append('\n');
            }

            text.Append(item.Quantity.ToString(CultureInfo.InvariantCulture)).Append("x ").Append(item.Name);
        }

        return text.ToString();
    }
}
