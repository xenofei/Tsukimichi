using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Unlocks;

namespace Tsukimichi.GameData;

/// <summary>
/// Reads what only the sheets know about the unlocks of every quest (<see cref="UnlockLinks"/>, feature plan v6 K1),
/// from the client's own sheets at runtime, so a game patch needs no data run:
/// <list type="bullet">
/// <item>the zones a row can name: every town and field zone (<c>TerritoryIntendedUse</c> 0 or 1), and every
/// destination of a quest-gated warp that is an area (a housing district, the Gold Saucer, the Firmament);</item>
/// <item>quest-gated warps: <c>Warp.WarpCondition.RequiredQuest1..4</c>, to an area (the city ferries and airships,
/// Kugane, Ishgard, the Gold Saucer, the housing districts; a room inside a story building is no area);</item>
/// <item>world-map regions a quest reveals: <c>Map.MapCondition</c> and <c>PlaceName.MapCondition</c>;</item>
/// <item>aethernet gates a quest opens: <c>Aetheryte.RequiredQuest</c>;</item>
/// <item>where each quest's objectives lead: its <c>IssuerLocation</c> and every <c>TodoParams.ToDoLocation</c>
/// <c>Level</c> row in a town or field zone, with the coordinates;</item>
/// <item>every duty's icon (its content type's), level and expansion;</item>
/// <item>the aetherytes (from <see cref="AetheryteIndex"/>, placed by their map markers) and the gates' names.</item>
/// </list>
/// Standalone (takes an <see cref="ExcelModule"/>) so tests read it against game data without Dalamud.
/// </summary>
public static class UnlockLinkReader
{
    /// <summary><c>TerritoryIntendedUse</c> of a town.</summary>
    public const uint TownUse = 0;

    /// <summary><c>TerritoryIntendedUse</c> of a field zone.</summary>
    public const uint FieldUse = 1;

    /// <summary>
    /// The intended uses a warp may open as an area: towns, fields, housing districts (13), the Firmament (21) and the
    /// Gold Saucer (23). A room inside a story building (15), an inn (2) or an event instance is no area.
    /// </summary>
    public static readonly IReadOnlySet<uint> AreaUses = new HashSet<uint> { TownUse, FieldUse, 13, 21, 23 };

    /// <summary><c>MainCommand</c> row of the Map menu, whose icon an area row wears.</summary>
    public const uint MapMainCommand = 16;

    /// <summary>Reads the links; a sheet that fails to read leaves its part empty rather than failing the rest.</summary>
    public static UnlockLinks Read(ExcelModule excel, Language language = Language.None, AetheryteIndex? aetherytes = null)
    {
        ArgumentNullException.ThrowIfNull(excel);
        var territories = excel.GetSheet<TerritoryType>(language);
        var levels = excel.GetSheet<Level>(language);

        // Zones: town and field zones with a name, then the warps' destinations.
        var zones = new Dictionary<uint, UnlockZone>();
        UnlockZone? ZoneOf(uint territoryId)
        {
            if (zones.TryGetValue(territoryId, out var known))
            {
                return known;
            }

            if (territories.GetRowOrDefault(territoryId) is not { } row)
            {
                return null;
            }

            var name = row.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var zone = new UnlockZone(
                row.RowId,
                name,
                row.PlaceNameRegion.ValueNullable?.Name.ExtractText() ?? string.Empty,
                row.Map.RowId,
                (byte)row.ExVersion.RowId,
                row.Aetheryte.RowId,
                row.RowId);
            zones[territoryId] = zone;
            return zone;
        }

        var areaTerritories = new HashSet<uint>();
        foreach (var row in territories)
        {
            if (row.TerritoryIntendedUse.RowId is TownUse or FieldUse && row.Map.RowId != 0 && ZoneOf(row.RowId) is not null)
            {
                areaTerritories.Add(row.RowId);
            }
        }

        // Quest-gated warps to an area.
        var warps = new List<UnlockWarp>();
        foreach (var warp in excel.GetSheet<Warp>(language))
        {
            if (warp.WarpCondition.ValueNullable is not { } condition || warp.TerritoryType.ValueNullable is not { } destination)
            {
                continue;
            }

            if (!AreaUses.Contains(destination.TerritoryIntendedUse.RowId) || ZoneOf(destination.RowId) is null)
            {
                continue;
            }

            foreach (var quest in new[] { condition.RequiredQuest1.RowId, condition.RequiredQuest2.RowId, condition.RequiredQuest3.RowId, condition.RequiredQuest4.RowId })
            {
                if (quest != 0)
                {
                    warps.Add(new UnlockWarp(quest, destination.RowId));
                }
            }
        }

        // World-map regions.
        var regions = new List<UnlockMapRegion>();
        var seenRegions = new HashSet<(uint Quest, uint Place)>();
        void AddRegion(uint quest, uint placeNameId, string name, byte expansion)
        {
            if (quest != 0 && placeNameId != 0 && !string.IsNullOrWhiteSpace(name) && seenRegions.Add((quest, placeNameId)))
            {
                regions.Add(new UnlockMapRegion(quest, placeNameId, name, expansion));
            }
        }

        foreach (var map in excel.GetSheet<Map>(language))
        {
            if (map.MapCondition.ValueNullable is { } condition && condition.Quest.RowId != 0)
            {
                var expansion = (byte)(map.TerritoryType.ValueNullable?.ExVersion.RowId ?? 0);
                AddRegion(condition.Quest.RowId, map.PlaceName.RowId, map.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty, expansion);
            }
        }

        foreach (var place in excel.GetSheet<PlaceName>(language))
        {
            if (place.MapCondition.ValueNullable is { } condition && condition.Quest.RowId != 0)
            {
                AddRegion(condition.Quest.RowId, place.RowId, place.Name.ExtractText(), 0);
            }
        }

        // Aetherytes: every teleportable one and shard the index placed, then the gates' rows.
        var index = aetherytes ?? AetheryteIndex.Build(excel, language);
        var aetheryteRows = new Dictionary<uint, UnlockAetheryte>();
        foreach (var info in index.All)
        {
            aetheryteRows.TryAdd(info.RowId, new UnlockAetheryte(info.RowId, info.TerritoryId, info.Name, info.X, info.Z, IsAetheryte: true));
        }

        foreach (var info in index.Shards)
        {
            aetheryteRows.TryAdd(info.RowId, new UnlockAetheryte(info.RowId, info.TerritoryId, info.Name, info.X, info.Z, IsAetheryte: false));
        }

        var gates = new List<UnlockGatedAethernet>();
        foreach (var row in excel.GetSheet<Aetheryte>(language))
        {
            if (row.RequiredQuest.RowId == 0)
            {
                continue;
            }

            if (!aetheryteRows.ContainsKey(row.RowId))
            {
                var name = row.AethernetName.ValueNullable?.Name.ExtractText() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = row.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                aetheryteRows[row.RowId] = new UnlockAetheryte(row.RowId, row.Territory.RowId, name, 0f, 0f, row.IsAetheryte);
                ZoneOf(row.Territory.RowId);
            }

            gates.Add(new UnlockGatedAethernet(row.RequiredQuest.RowId, row.RowId));
        }

        // Where each quest's objectives lead, in town and field zones.
        var touches = new List<UnlockTouch>();
        var seenTouches = new HashSet<(uint Territory, float X, float Z)>();
        foreach (var quest in excel.GetSheet<Quest>(language))
        {
            if (quest.Name.IsEmpty)
            {
                continue;
            }

            seenTouches.Clear();
            void Touch(uint levelId)
            {
                if (levelId == 0 || levels.GetRowOrDefault(levelId) is not { } level)
                {
                    return;
                }

                var territory = level.Territory.RowId;
                if (areaTerritories.Contains(territory) && seenTouches.Add((territory, level.X, level.Z)))
                {
                    touches.Add(new UnlockTouch(quest.RowId, territory, level.X, level.Z));
                }
            }

            Touch(quest.IssuerLocation.RowId);
            foreach (var todo in quest.TodoParams)
            {
                foreach (var location in todo.ToDoLocation)
                {
                    Touch(location.RowId);
                }
            }
        }

        // Duties: their content type's icon, their level and expansion.
        var duties = new List<UnlockDuty>();
        foreach (var row in excel.GetSheet<ContentFinderCondition>(language))
        {
            if (row.Name.IsEmpty)
            {
                continue;
            }

            var icon = row.ContentType.ValueNullable?.Icon ?? 0u;
            duties.Add(new UnlockDuty(row.RowId, icon, row.ClassJobLevelRequired, (byte)(row.TerritoryType.ValueNullable?.ExVersion.RowId ?? 0)));
        }

        var areaIcon = 0u;
        if (excel.GetSheet<MainCommand>(language).GetRowOrDefault(MapMainCommand) is { Icon: > 0 } command)
        {
            areaIcon = (uint)command.Icon;
        }

        return new UnlockLinks
        {
            Zones = [.. zones.Values.OrderBy(static z => z.TerritoryId)],
            Aetherytes = [.. aetheryteRows.Values],
            Warps = warps,
            MapRegions = regions,
            GatedAethernet = gates,
            Touches = touches,
            Duties = duties,
            AreaIcon = areaIcon,
        };
    }
}
