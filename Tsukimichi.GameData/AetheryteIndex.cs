using System.Collections.Frozen;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Tsukimichi.GameData;

/// <summary>One teleport destination: an Aetheryte sheet row with <c>IsAetheryte</c> set, placed by its map marker.</summary>
/// <param name="RowId">Aetheryte sheet row id, the id Lifestream's Teleport takes.</param>
/// <param name="TerritoryId">TerritoryType the aetheryte stands in.</param>
/// <param name="Name">Place name, or the aethernet name when the place name is empty.</param>
/// <param name="X">Raw world X, comparable with <see cref="Core.Model.Issuer.X"/>.</param>
/// <param name="Z">Raw world Z, comparable with <see cref="Core.Model.Issuer.Z"/>.</param>
public sealed record AetheryteInfo(uint RowId, uint TerritoryId, string Name, float X, float Z);

/// <summary>
/// Teleportable aetherytes grouped by territory, read once from the Aetheryte, MapMarker, Map and TerritoryType sheets.
/// <see cref="Nearest"/> answers "which aetheryte is closest to this spot", which is what a teleport-to-giver needs.
/// Standalone (takes an <see cref="ExcelModule"/>) so tests can build it against game data without Dalamud.
/// </summary>
public sealed class AetheryteIndex
{
    private static readonly AetheryteInfo[] None = [];

    public static readonly AetheryteIndex Empty = new(
        None,
        FrozenDictionary<uint, IReadOnlyList<AetheryteInfo>>.Empty,
        FrozenDictionary<uint, AetheryteInfo>.Empty);

    private readonly FrozenDictionary<uint, IReadOnlyList<AetheryteInfo>> byTerritory;
    private readonly FrozenDictionary<uint, AetheryteInfo> territoryDefault;

    private AetheryteIndex(
        AetheryteInfo[] all,
        FrozenDictionary<uint, IReadOnlyList<AetheryteInfo>> byTerritory,
        FrozenDictionary<uint, AetheryteInfo> territoryDefault)
    {
        All = all;
        this.byTerritory = byTerritory;
        this.territoryDefault = territoryDefault;
    }

    /// <summary>Every teleportable aetheryte with a known position, in sheet order.</summary>
    public IReadOnlyList<AetheryteInfo> All { get; }

    /// <summary>Aetherytes standing in a territory; empty when the territory has none (or the id is unknown).</summary>
    public IReadOnlyList<AetheryteInfo> InTerritory(uint territoryId) => byTerritory.GetValueOrDefault(territoryId) ?? None;

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

    /// <summary>Builds an index from already-read rows; for tests and for callers that read the sheets themselves.</summary>
    /// <param name="aetherytes">Teleportable aetherytes with positions.</param>
    /// <param name="territoryDefaults">TerritoryType row id to the aetheryte row id that zone belongs to.</param>
    public static AetheryteIndex From(IEnumerable<AetheryteInfo> aetherytes, IEnumerable<KeyValuePair<uint, uint>>? territoryDefaults = null)
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

        return new AetheryteIndex(
            all.ToArray(),
            byTerritory.ToFrozenDictionary(kv => kv.Key, kv => (IReadOnlyList<AetheryteInfo>)kv.Value.ToArray()),
            defaults.ToFrozenDictionary());
    }

    /// <summary>
    /// Reads the sheets. An aetheryte's position comes from its map marker (MapMarker rows of type 3 carry the
    /// aetheryte id and a pixel position on the 2048-px map image), converted back to raw world units with the Map
    /// row's scale and offset; the Level rows the Aetheryte sheet names are mostly absent from the shipped Level sheet.
    /// Rows without <c>IsAetheryte</c>, without a territory or without a marker are skipped.
    /// </summary>
    public static AetheryteIndex Build(ExcelModule excel, Language language = Language.None)
    {
        ArgumentNullException.ThrowIfNull(excel);

        // Marker pixel position per aetheryte id; the page that the aetheryte's own map names wins over any other.
        var markers = new Dictionary<uint, (uint Page, short X, short Y)>();
        foreach (var page in excel.GetSubrowSheet<MapMarker>(language))
        {
            foreach (var marker in page)
            {
                if (marker.DataType == AetheryteMarkerType && marker.DataKey.RowId != 0 && !markers.ContainsKey(marker.DataKey.RowId))
                {
                    markers[marker.DataKey.RowId] = (page.RowId, marker.X, marker.Y);
                }
            }
        }

        var maps = excel.GetSheet<Map>(language);
        var aetherytes = new List<AetheryteInfo>();
        foreach (var row in excel.GetSheet<Aetheryte>(language))
        {
            if (!row.IsAetheryte || row.Territory.RowId == 0 || !markers.TryGetValue(row.RowId, out var marker))
            {
                continue;
            }

            var map = row.Map.RowId != 0 ? maps.GetRowOrDefault(row.Map.RowId) : null;
            if (map is null && marker.Page != 0)
            {
                // The aetheryte row names no map: the marker page is a Map.MapMarkerRange, so find the map that way.
                foreach (var candidate in maps)
                {
                    if (candidate.MapMarkerRange == marker.Page)
                    {
                        map = candidate;
                        break;
                    }
                }
            }

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
                ToRaw(marker.Y, m.OffsetY, m.SizeFactor)));
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

        return From(aetherytes, defaults);
    }

    /// <summary><c>MapMarker.DataType</c> of an aetheryte marker; <c>DataKey</c> is then the Aetheryte row id.</summary>
    public const byte AetheryteMarkerType = 3;

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
