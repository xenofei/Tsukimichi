using System;
using System.Collections.Generic;
using System.Numerics;
using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Sources;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// Where items come from (feature plan v7, 1.19.0, C6 and N5): the item source index the plugin warms at load
/// (<see cref="ItemSourceIndex"/>), the buy-back verdict the reward tiles, the reward tooltip and Moonlit share, and the
/// Flag and Teleport a "Where" line offers for its place. Read-only UI calls; Teleport goes through Lifestream and shows
/// only while the automation level shows Teleport.
/// </summary>
public sealed partial class GameLinks
{
    private readonly Dictionary<(uint Item, uint Quest), BuyBackText?> buyBacks = [];
    private ItemSourceIndex? buyBackIndex;
    private int buyBackTextVersion = -1;

    /// <summary>The warmed item source index, attached by the plugin; null (or a null result) until it lands.</summary>
    public Func<ItemSourceIndex?>? ItemSourceIndex { get; set; }

    /// <summary>The index once it landed; null while it is read.</summary>
    public ItemSourceIndex? ItemSources => ItemSourceIndex?.Invoke();

    /// <summary>A buy-back verdict with its words, composed once.</summary>
    /// <param name="Line">"Can be bought back from Calamity salvager for 100 gil".</param>
    /// <param name="Tooltip">The line and what it means for the reward.</param>
    public sealed record BuyBackText(BuyBack BuyBack, string Line, string Tooltip);

    /// <summary>
    /// How a reward can be had again (<see cref="BuyBacks.For"/>) with its words, cached per item and quest; null when no
    /// shop sells it back, or while the index is read. <paramref name="questRowId"/> 0 lets any quest's reclaim row count.
    /// </summary>
    public BuyBackText? BuyBackOf(uint itemId, uint questRowId = 0)
    {
        if (itemId == 0 || ItemSources is not { } index)
        {
            return null;
        }

        if (!ReferenceEquals(index, buyBackIndex) || buyBackTextVersion != CoreText.Version)
        {
            buyBacks.Clear();
            buyBackIndex = index;
            buyBackTextVersion = CoreText.Version;
        }

        if (!buyBacks.TryGetValue((itemId, questRowId), out var text))
        {
            text = BuyBacks.For(index.For(itemId), questRowId) is { } found ? new BuyBackText(found, BuyBacks.Line(found), BuyBacks.Tooltip(found)) : null;
            buyBacks[(itemId, questRowId)] = text;
        }

        return text;
    }

    /// <summary>Opens the game's map with a flag on <paramref name="spot"/>; false when it could not.</summary>
    public bool FlagWorldSpot(WorldSpot spot)
    {
        ArgumentNullException.ThrowIfNull(spot);
        return FlagSpot(spot.TerritoryId, new Vector3(spot.X, 0f, spot.Z));
    }

    /// <summary>Whether <see cref="FlagWorldSpot"/> can open the map there now.</summary>
    public bool CanFlagWorldSpot(WorldSpot spot)
    {
        ArgumentNullException.ThrowIfNull(spot);
        return CanFlagSpot(spot.TerritoryId);
    }

    /// <summary>The aetheryte nearest <paramref name="spot"/> in its zone (or the zone's own); null when none is known.</summary>
    public AetheryteInfo? AetheryteNear(WorldSpot spot)
    {
        ArgumentNullException.ThrowIfNull(spot);
        return Aetherytes.Nearest(spot.TerritoryId, spot.X, spot.Z);
    }
}
