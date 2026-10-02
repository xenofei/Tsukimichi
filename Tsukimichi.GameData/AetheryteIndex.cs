using System.Collections.Frozen;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Travel;

namespace Tsukimichi.GameData;

/// <summary>
/// One teleport destination (an Aetheryte sheet row with <c>IsAetheryte</c> set) or aethernet shard (a row without
/// it, in a city's network), placed by its map marker.
/// </summary>
/// <param name="RowId">Aetheryte sheet row id, the id Lifestream's Teleport and AethernetTeleportById take.</param>
/// <param name="TerritoryId">TerritoryType the aetheryte stands in.</param>
/// <param name="Name">Place name, or the aethernet name when the place name is empty.</param>
/// <param name="X">Raw world X, comparable with <see cref="Core.Model.Issuer.X"/>.</param>
/// <param name="Z">Raw world Z, comparable with <see cref="Core.Model.Issuer.Z"/>.</param>
/// <param name="Group">The sheet's <c>AethernetGroup</c>: the city network it belongs to; 0 for a field aetheryte.</param>
public sealed record AetheryteInfo(uint RowId, uint TerritoryId, string Name, float X, float Z, uint Group = 0)
{
    /// <summary>The same place for <see cref="TravelPlanner"/>.</summary>
    public TravelNode Node => new(RowId, TerritoryId, X, Z, Group);
}

/// <summary>
/// Teleportable aetherytes grouped by territory, and the cities' aethernet shards grouped by network, read once from
/// the Aetheryte, MapMarker, Map and TerritoryType sheets. <see cref="Nearest"/> answers "which aetheryte is closest
/// to this spot", which is what a teleport-to-giver needs; <see cref="ShardsInGroup"/> feeds the aethernet hop.
/// Standalone (takes an <see cref="ExcelModule"/>) so tests can build it against game data without Dalamud.
/// </summary>
public sealed class AetheryteIndex
{
    private static readonly AetheryteInfo[] None = [];
    private static readonly TravelNode[] NoNodes = [];

    public static readonly AetheryteIndex Empty = new(
        None,
        FrozenDictionary<uint, IReadOnlyList<AetheryteInfo>>.Empty,
        FrozenDictionary<uint, AetheryteInfo>.Empty,
        None,
        FrozenDictionary<uint, IReadOnlyList<AetheryteInfo>>.Empty,
        FrozenDictionary<uint, AetheryteInfo>.Empty);

    private readonly FrozenDictionary<uint, IReadOnlyList<AetheryteInfo>> byTerritory;
    private readonly FrozenDictionary<uint, AetheryteInfo> territoryDefault;
    private readonly FrozenDictionary<uint, IReadOnlyList<AetheryteInfo>> shardsByGroup;
    private readonly FrozenDictionary<uint, AetheryteInfo> byId;
    private readonly FrozenDictionary<uint, AetheryteInfo> mainByGroup;
    private readonly FrozenDictionary<uint, TravelNode[]> nodesByTerritory;
    private readonly FrozenDictionary<uint, TravelNode[]> shardNodesByGroup;
    private readonly FrozenSet<uint> shardTerritories;

    private AetheryteIndex(
        AetheryteInfo[] all,
        FrozenDictionary<uint, IReadOnlyList<AetheryteInfo>> byTerritory,
        FrozenDictionary<uint, AetheryteInfo> territoryDefault,
        AetheryteInfo[] shards,
        FrozenDictionary<uint, IReadOnlyList<AetheryteInfo>> shardsByGroup,
        FrozenDictionary<uint, AetheryteInfo> byId)
    {
        All = all;
        Shards = shards;
        this.byTerritory = byTerritory;
        this.territoryDefault = territoryDefault;
        this.shardsByGroup = shardsByGroup;
        this.byId = byId;
        var mains = new Dictionary<uint, AetheryteInfo>();
        foreach (var aetheryte in all)
        {
            if (aetheryte.Group != 0)
            {
                mains.TryAdd(aetheryte.Group, aetheryte);
            }
        }

        mainByGroup = mains.ToFrozenDictionary();
        shardTerritories = shards.Select(s => s.TerritoryId).ToFrozenSet();
        nodesByTerritory = byTerritory.ToFrozenDictionary(kv => kv.Key, kv => ToNodes(kv.Value));
        shardNodesByGroup = shardsByGroup.ToFrozenDictionary(kv => kv.Key, kv => ToNodes(kv.Value));
    }

    private static TravelNode[] ToNodes(IReadOnlyList<AetheryteInfo> infos)
    {
        var nodes = new TravelNode[infos.Count];
        for (var i = 0; i < nodes.Length; i++)
        {
            nodes[i] = infos[i].Node;
        }

        return nodes;
    }

    /// <summary>Every teleportable aetheryte with a known position, in sheet order.</summary>
    public IReadOnlyList<AetheryteInfo> All { get; }

    /// <summary>Every aethernet shard with a known position, in sheet order.</summary>
    public IReadOnlyList<AetheryteInfo> Shards { get; }

    /// <summary>Aetherytes standing in a territory; empty when the territory has none (or the id is unknown).</summary>
    public IReadOnlyList<AetheryteInfo> InTerritory(uint territoryId) => byTerritory.GetValueOrDefault(territoryId) ?? None;

    /// <summary>The aethernet shards of one city network (<see cref="AetheryteInfo.Group"/>); empty for group 0 or an unknown one.</summary>
    public IReadOnlyList<AetheryteInfo> ShardsInGroup(uint group) => group == 0 ? None : shardsByGroup.GetValueOrDefault(group) ?? None;

    /// <summary><see cref="InTerritory"/> as <see cref="TravelPlanner"/> nodes, built once; empty when the territory has none.</summary>
    public IReadOnlyList<TravelNode> NodesInTerritory(uint territoryId) => nodesByTerritory.GetValueOrDefault(territoryId) ?? NoNodes;

    /// <summary><see cref="ShardsInGroup"/> as <see cref="TravelPlanner"/> nodes, built once; empty for group 0 or an unknown one.</summary>
    public IReadOnlyList<TravelNode> ShardNodesInGroup(uint group) => group == 0 ? NoNodes : shardNodesByGroup.GetValueOrDefault(group) ?? NoNodes;

    /// <summary>True when an aethernet shard stands in the territory (a city or one of its sub-zones).</summary>
    public bool HasShardIn(uint territoryId) => shardTerritories.Contains(territoryId);

    /// <summary>True when a teleport or a hop lands in the territory: an aetheryte or an aethernet shard stands in it.</summary>
    public bool Reachable(uint territoryId) => byTerritory.ContainsKey(territoryId) || shardTerritories.Contains(territoryId);

    /// <summary>The teleportable aetheryte at the heart of a city network (New Gridania's for group 2); null for group 0 or an unknown one.</summary>
    public AetheryteInfo? GroupAetheryte(uint group) => group == 0 ? null : mainByGroup.GetValueOrDefault(group);

    /// <summary>An aetheryte or shard by its Aetheryte sheet row id; null when the index does not hold it.</summary>
    public AetheryteInfo? Find(uint rowId) => byId.GetValueOrDefault(rowId);

    /// <summary>
    /// The aetheryte the zone's TerritoryType row names (a city sub-zone without its own, like Old Gridania, names its
    /// city's); null when the row names none.
    /// </summary>
    public AetheryteInfo? TerritoryDefault(uint territoryId) => territoryDefault.GetValueOrDefault(territoryId);

    /// <summary>
    /// The aetheryte nearest to a raw (x, z) position in a territory. A territory without an aetheryte of its own
    /// (a city sub-zone, an instance) falls back to the aetheryte its TerritoryType row names; null when neither exists.
    /// </summary>
    public AetheryteInfo? Nearest(uint territoryId, float x, float z)
    {
        AetheryteInfo? best = null;
        var bestDistance = float.MaxValue;
        foreach (var candidate in InTerritory(territoryId))
        {
            var dx = candidate.X - x;
            var dz = candidate.Z - z;
            var distance = dx * dx + dz * dz;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best ?? territoryDefault.GetValueOrDefault(territoryId);
    }

    /// <summary>
    /// The aetheryte nearest a quest's giver (<see cref="Nearest"/> at the issuer's place), attuned or not: what the
    /// stop lists group quests by, so the grouping never moves with attunement. Teleport picks its own target (the
    /// nearest attuned one). Null without a giver place or an aetheryte for the giver's zone.
    /// </summary>
    public AetheryteInfo? NearestToGiver(Core.Model.QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return quest.Issuer is { TerritoryId: > 0 } issuer ? Nearest(issuer.TerritoryId, issuer.X, issuer.Z) : null;
    }

    /// <summary>Builds an index from already-read rows; for tests and for callers that read the sheets themselves.</summary>
    /// <param name="aetherytes">Teleportable aetherytes with positions.</param>
    /// <param name="territoryDefaults">TerritoryType row id to the aetheryte row id that zone belongs to.</param>
    /// <param name="shards">Aethernet shards with positions and their network (<see cref="AetheryteInfo.Group"/>).</param>
    public static AetheryteIndex From(IEnumerable<AetheryteInfo> aetherytes, IEnumerable<KeyValuePair<uint, uint>>? territoryDefaults = null, IEnumerable<AetheryteInfo>? shards = null)
    {
        ArgumentNullException.ThrowIfNull(aetherytes);

        var all = new List<AetheryteInfo>();
        var byId = new Dictionary<uint, AetheryteInfo>();
        var byTerritory = new Dictionary<uint, List<AetheryteInfo>>();
        foreach (var info in aetherytes)
        {
            all.Add(info);
            byId[info.RowId] = info;
            if (!byTerritory.TryGetValue(info.TerritoryId, out var list))
            {
                list = [];
                byTerritory[info.TerritoryId] = list;
            }

            list.Add(info);
        }

        var defaults = new Dictionary<uint, AetheryteInfo>();
        foreach (var (territoryId, aetheryteId) in territoryDefaults ?? [])
        {
            if (byId.TryGetValue(aetheryteId, out var info))
            {
                defaults[territoryId] = info;
            }
        }

        if (all.Count == 0)
        {
            return Empty;
        }

        var shardList = new List<AetheryteInfo>();
        var byGroup = new Dictionary<uint, List<AetheryteInfo>>();
        foreach (var shard in shards ?? [])
        {
            if (shard.Group == 0 || byId.ContainsKey(shard.RowId))
            {
                continue;
            }

            shardList.Add(shard);
            byId[shard.RowId] = shard;
            if (!byGroup.TryGetValue(shard.Group, out var members))
            {
                members = [];
                byGroup[shard.Group] = members;
            }

            members.Add(shard);
        }

        return new AetheryteIndex(
            all.ToArray(),
            byTerritory.ToFrozenDictionary(kv => kv.Key, kv => (IReadOnlyList<AetheryteInfo>)kv.Value.ToArray()),
            defaults.ToFrozenDictionary(),
            shardList.ToArray(),
            byGroup.ToFrozenDictionary(kv => kv.Key, kv => (IReadOnlyList<AetheryteInfo>)kv.Value.ToArray()),
            byId.ToFrozenDictionary());
    }

    /// <summary>
    /// Reads the sheets. An aetheryte's position comes from its map marker (MapMarker rows of type 3 carry the
    /// aetheryte id and a pixel position on the 2048-px map image), converted back to raw world units with the Map
    /// row's scale and offset; the Level rows the Aetheryte sheet names are mostly absent from the shipped Level sheet.
    /// A city aetheryte is drawn on several maps (its own, the region map, the world map), each page with its own
    /// scale and offset, so the marker on the page the aetheryte's own Map row names is used; any other page only
    /// serves rows that name no map. Rows without <c>IsAetheryte</c>, without a territory or without a marker are skipped.
    /// An aethernet shard (a row without <c>IsAetheryte</c>, with an <c>AethernetGroup</c>) has no type-3 marker: it is
    /// drawn by a type-4 (aethernet) marker keyed by its <c>AethernetName</c> place name, taken from the page of a map
    /// of the shard's own territory (several shards share a name, "Airship Landing", in different cities).
    /// </summary>
    public static AetheryteIndex Build(ExcelModule excel, Language language = Language.None)
    {
        ArgumentNullException.ThrowIfNull(excel);

        // Marker pixel positions per aetheryte id, one per MapMarker page (a page id is a Map.MapMarkerRange); the
        // aethernet markers likewise per place name.
        var markers = new Dictionary<uint, List<(uint Page, short X, short Y)>>();
        var aethernetMarkers = new Dictionary<uint, List<(uint Page, short X, short Y)>>();
        foreach (var page in excel.GetSubrowSheet<MapMarker>(language))
        {
            foreach (var marker in page)
            {
                var target = marker.DataType switch
                {
                    AetheryteMarkerType => markers,
                    AethernetMarkerType => aethernetMarkers,
                    _ => null,
                };
                if (target is null || marker.DataKey.RowId == 0)
                {
                    continue;
                }

                if (!target.TryGetValue(marker.DataKey.RowId, out var pages))
                {
                    pages = [];
                    target[marker.DataKey.RowId] = pages;
                }

                pages.Add((page.RowId, marker.X, marker.Y));
            }
        }

        var maps = excel.GetSheet<Map>(language);
        var mapsByRange = new Dictionary<uint, Map>();
        foreach (var candidate in maps)
        {
            mapsByRange.TryAdd(candidate.MapMarkerRange, candidate);
        }

        var aetherytes = new List<AetheryteInfo>();
        var shards = new List<AetheryteInfo>();
        foreach (var row in excel.GetSheet<Aetheryte>(language))
        {
            if (!row.IsAetheryte)
            {
                if (ReadShard(row, aethernetMarkers, mapsByRange) is { } shard)
                {
                    shards.Add(shard);
                }

                continue;
            }

            if (row.Territory.RowId == 0 || !markers.TryGetValue(row.RowId, out var pages))
            {
                continue;
            }

            var own = row.Map.RowId != 0 ? maps.GetRowOrDefault(row.Map.RowId) : null;
            var marker = PreferPage(pages, own?.MapMarkerRange ?? 0);

            // Convert with the map the chosen marker belongs to; the aetheryte's own map when its page had no marker.
            Map? map = own is { } o && o.MapMarkerRange == marker.Page ? o
                : mapsByRange.TryGetValue(marker.Page, out var byRange) ? byRange
                : own;
            if (map is not { } m)
            {
                continue;
            }

            var name = row.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = row.AethernetName.ValueNullable?.Name.ExtractText() ?? string.Empty;
            }

            aetherytes.Add(new AetheryteInfo(
                row.RowId,
                row.Territory.RowId,
                name,
                ToRaw(marker.X, m.OffsetX, m.SizeFactor),
                ToRaw(marker.Y, m.OffsetY, m.SizeFactor),
                row.AethernetGroup));
        }

        // TerritoryType.Aetheryte names the aetheryte a zone belongs to, including sub-zones that hold none themselves.
        var defaults = new List<KeyValuePair<uint, uint>>();
        foreach (var territory in excel.GetSheet<TerritoryType>(language))
        {
            if (territory.Aetheryte.RowId != 0)
            {
                defaults.Add(new KeyValuePair<uint, uint>(territory.RowId, territory.Aetheryte.RowId));
            }
        }

        return From(aetherytes, defaults, shards);
    }

    /// <summary>
    /// One aethernet shard: a row in a city network with a name and a territory, placed by the aethernet marker of its
    /// name on a page whose map shows its territory; null when any of that is missing.
    /// </summary>
    private static AetheryteInfo? ReadShard(in Aetheryte row, Dictionary<uint, List<(uint Page, short X, short Y)>> aethernetMarkers, Dictionary<uint, Map> mapsByRange)
    {
        var nameId = row.AethernetName.RowId;
        if (row.AethernetGroup == 0 || row.Territory.RowId == 0 || nameId == 0 || !aethernetMarkers.TryGetValue(nameId, out var pages))
        {
            return null;
        }

        foreach (var (page, x, y) in pages)
        {
            if (mapsByRange.TryGetValue(page, out var map) && map.TerritoryType.RowId == row.Territory.RowId)
            {
                var name = row.AethernetName.ValueNullable?.Name.ExtractText() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                {
                    return null;
                }

                return new AetheryteInfo(row.RowId, row.Territory.RowId, name, ToRaw(x, map.OffsetX, map.SizeFactor), ToRaw(y, map.OffsetY, map.SizeFactor), row.AethernetGroup);
            }
        }

        return null;
    }

    /// <summary><c>MapMarker.DataType</c> of an aetheryte marker; <c>DataKey</c> is then the Aetheryte row id.</summary>
    public const byte AetheryteMarkerType = 3;

    /// <summary><c>MapMarker.DataType</c> of an aethernet shard marker; <c>DataKey</c> is then the shard's PlaceName row id.</summary>
    public const byte AethernetMarkerType = 4;

    /// <summary>
    /// The marker on <paramref name="preferredPage"/> (the aetheryte's own <c>Map.MapMarkerRange</c>) when one exists,
    /// otherwise the first marker; page order in the sheet never decides. Pure; exposed for tests.
    /// </summary>
    public static (uint Page, short X, short Y) PreferPage(IReadOnlyList<(uint Page, short X, short Y)> markers, uint preferredPage)
    {
        ArgumentNullException.ThrowIfNull(markers);
        if (markers.Count == 0)
        {
            throw new ArgumentException("At least one marker is required.", nameof(markers));
        }

        foreach (var marker in markers)
        {
            if (marker.Page == preferredPage)
            {
                return marker;
            }
        }

        return markers[0];
    }

    /// <summary>
    /// Map-image pixel (0..2048) back to the raw world coordinate, the inverse of the game's
    /// <c>(raw + offset) * scale + 1024</c>; <paramref name="sizeFactor"/> is the Map row's percentage scale.
    /// </summary>
    public static float ToRaw(float pixel, short offset, ushort sizeFactor)
    {
        var scale = sizeFactor / 100f;
        if (scale <= 0f)
        {
            scale = 1f;
        }

        return (pixel - 1024f) / scale - offset;
    }
}
