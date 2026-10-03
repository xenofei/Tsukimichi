namespace Tsukimichi.Core.Unlocks;

/// <summary>A zone an unlock row can name: a town or field zone, or the destination of a quest-gated warp.</summary>
/// <param name="TerritoryId">TerritoryType row id.</param>
/// <param name="Name">The zone's place name ("Kugane").</param>
/// <param name="Region">Its region's place name ("Hingashi"); empty when none.</param>
/// <param name="MapId">The zone's Map row id, for opening the map; 0 when none.</param>
/// <param name="Expansion">The zone's ExVersion.</param>
/// <param name="AetheryteId">The aetheryte the zone's TerritoryType row names (a city sub-zone names its city's); 0 when none.</param>
/// <param name="SortKey">The zone's place in the sheet, which follows the game's own order of regions.</param>
public sealed record UnlockZone(uint TerritoryId, string Name, string Region, uint MapId, byte Expansion, uint AetheryteId, uint SortKey = 0);

/// <summary>An aetheryte or aethernet shard an unlock row can name, placed by its map marker.</summary>
/// <param name="AetheryteId">Aetheryte row id.</param>
/// <param name="TerritoryId">The territory it stands in.</param>
/// <param name="Name">Its place name ("Onokoro").</param>
/// <param name="X">Raw world X, comparable with a Level row's.</param>
/// <param name="Z">Raw world Z.</param>
/// <param name="IsAetheryte">A teleportable aetheryte (the first-visit rule's targets); false for a shard or an invisible gate.</param>
public sealed record UnlockAetheryte(uint AetheryteId, uint TerritoryId, string Name, float X, float Z, bool IsAetheryte);

/// <summary>A warp (a ferry, a gate, an NPC's passage) the game opens only after <paramref name="QuestRowId"/>: <c>Warp.WarpCondition.RequiredQuest1..4</c>.</summary>
public readonly record struct UnlockWarp(uint QuestRowId, uint TerritoryId);

/// <summary>A world-map region the quest reveals: <c>Map.MapCondition</c> or <c>PlaceName.MapCondition</c> naming the quest.</summary>
/// <param name="PlaceNameId">The region's PlaceName row id.</param>
/// <param name="Expansion">The expansion the region belongs to (that of the quest when the sheet does not say).</param>
public sealed record UnlockMapRegion(uint QuestRowId, uint PlaceNameId, string Name, byte Expansion);

/// <summary>An aethernet shard or invisible gate the quest opens: <c>Aetheryte.RequiredQuest</c>.</summary>
public readonly record struct UnlockGatedAethernet(uint QuestRowId, uint AetheryteId);

/// <summary>
/// One place a quest's objectives reach: its issuer's <c>Level</c> or one of its <c>TodoParams.ToDoLocation</c> rows,
/// in a town or field zone.
/// </summary>
public readonly record struct UnlockTouch(uint QuestRowId, uint TerritoryId, float X, float Z);

/// <summary>A duty's icon, level and expansion, for the duty rows (ContentFinderCondition row id).</summary>
public sealed record UnlockDuty(uint ContentFinderConditionId, uint Icon, byte Level, byte Expansion);

/// <summary>
/// What only the game's sheets know about unlocks (feature plan v6 K1): the plugin fills it from the client's own sheets
/// (<c>Tsukimichi.GameData.UnlockLinkReader</c>), so a patch needs no data run; tests build it by hand. Plain arrays.
/// Immutable once built.
/// </summary>
public sealed record UnlockLinks
{
    public static readonly UnlockLinks Empty = new();

    /// <summary>Every town and field zone, and every warp destination.</summary>
    public IReadOnlyList<UnlockZone> Zones { get; init; } = [];

    /// <summary>Every aetheryte, shard and gate a row can name.</summary>
    public IReadOnlyList<UnlockAetheryte> Aetherytes { get; init; } = [];

    public IReadOnlyList<UnlockWarp> Warps { get; init; } = [];

    public IReadOnlyList<UnlockMapRegion> MapRegions { get; init; } = [];

    public IReadOnlyList<UnlockGatedAethernet> GatedAethernet { get; init; } = [];

    /// <summary>The places each quest's objectives reach, town and field zones only.</summary>
    public IReadOnlyList<UnlockTouch> Touches { get; init; } = [];

    public IReadOnlyList<UnlockDuty> Duties { get; init; } = [];

    /// <summary>The icon a zone or world-map row wears (the game's Map menu icon); 0 for the stand-in.</summary>
    public uint AreaIcon { get; init; }

    /// <summary>The icon a teleportable aetheryte row wears (the map's aetheryte marker).</summary>
    public uint AetheryteIcon { get; init; } = DefaultAetheryteIcon;

    /// <summary>The icon a shard or gate row wears (the map's aethernet marker).</summary>
    public uint AethernetIcon { get; init; } = DefaultAethernetIcon;

    /// <summary><c>MapMarker</c> icon of an aetheryte (DataType 3).</summary>
    public const uint DefaultAetheryteIcon = 60453;

    /// <summary><c>MapMarker</c> icon of an aethernet shard (DataType 4).</summary>
    public const uint DefaultAethernetIcon = 60430;
}
