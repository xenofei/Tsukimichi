using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;
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
/// <item>the icons of the actions, traits, general actions and blue magic a quest can teach;</item>
/// <item>the icons of the feature rows (<see cref="FeatureIconReader"/>);</item>
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

    /// <summary>
    /// Reads the links; a sheet that fails to read leaves its part empty rather than failing the rest (each part is read
    /// on its own, and a failure is told to <paramref name="log"/>).
    /// </summary>
    /// <param name="excel">Excel module to read from.</param>
    /// <param name="language">Language for every name.</param>
    /// <param name="aetherytes">The aetheryte index, when the caller has one; read here otherwise.</param>
    /// <param name="log">Told one line about a part that could not be read.</param>
    public static UnlockLinks Read(ExcelModule excel, Language language = Language.None, AetheryteIndex? aetherytes = null, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(excel);
        var territories = Part<ExcelSheet<TerritoryType>?>("TerritoryType sheet", () => excel.GetSheet<TerritoryType>(language), null, log);

        // Zones: town and field zones with a name, then the warps' destinations.
        var zones = new Dictionary<uint, UnlockZone>();
        UnlockZone? ZoneOf(uint territoryId)
        {
            if (zones.TryGetValue(territoryId, out var known))
            {
                return known;
            }

            if (territories?.GetRowOrDefault(territoryId) is not { } row)
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

        var areaTerritories = Part("town and field zones", () =>
        {
            var areas = new HashSet<uint>();
            if (territories is not null)
            {
                foreach (var row in territories)
                {
                    if (row.TerritoryIntendedUse.RowId is TownUse or FieldUse && row.Map.RowId != 0 && ZoneOf(row.RowId) is not null)
                    {
                        areas.Add(row.RowId);
                    }
                }
            }

            return areas;
        }, [], log);

        // Quest-gated warps to an area.
        var warps = Part("quest-gated warps", () => ReadWarps(excel, language, ZoneOf), [], log);

        // World-map regions.
        var regions = Part("world-map regions", () => ReadRegions(excel, language), [], log);

        // Aetherytes: every teleportable one and shard the index placed, then the gates' rows.
        var aetheryteRows = Part("aetherytes", () => ReadAetherytes(excel, language, aetherytes), [], log);
        var (gateRows, gates) = Part("aethernet gates", () => ReadGates(excel, language, aetheryteRows, ZoneOf), ([], []), log);
        foreach (var (id, row) in gateRows)
        {
            aetheryteRows[id] = row;
        }

        // Where each quest's objectives lead, in town and field zones.
        var touches = Part("quest objectives' places", () => ReadTouches(excel, language, areaTerritories), [], log);

        // Duties: their content type's icon, their level and expansion.
        var duties = Part("duties", () => ReadDuties(excel, language), [], log);

        // Actions, traits, general actions and blue magic: their icons, for the action rows the reward data names.
        var actionIcons = Part("action icons", () => ReadActionIcons(excel, language), [], log);

        var areaIcon = Part("Map menu icon", () => excel.GetSheet<MainCommand>(language).GetRowOrDefault(MapMainCommand) is { Icon: > 0 } command ? (uint)command.Icon : 0u, 0u, log);

        // The feature rows' icons: the Duty Finder tile, menu or item that stands for each (FeatureArt).
        var featureIcons = FeatureIconReader.Read(excel, language, log);

        return new UnlockLinks
        {
            Zones = [.. zones.Values.OrderBy(static z => z.TerritoryId)],
            Aetherytes = [.. aetheryteRows.Values],
            Warps = warps,
            MapRegions = regions,
            GatedAethernet = gates,
            Touches = touches,
            Duties = duties,
            ActionIcons = actionIcons,
            AreaIcon = areaIcon,
            FeatureIcons = featureIcons,
        };
    }

    /// <summary>Reads one part; a failure is logged and gives <paramref name="fallback"/>, so the other parts still read.</summary>
    private static T Part<T>(string what, Func<T> read, T fallback, Action<string>? log)
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log?.Invoke($"The {what} could not be read for what quests open ({ex.GetType().Name}: {ex.Message}); that part is left out");
            return fallback;
        }
    }

    private static List<UnlockWarp> ReadWarps(ExcelModule excel, Language language, Func<uint, UnlockZone?> zoneOf)
    {
        var warps = new List<UnlockWarp>();
        foreach (var warp in excel.GetSheet<Warp>(language))
        {
            if (warp.WarpCondition.ValueNullable is not { } condition || warp.TerritoryType.ValueNullable is not { } destination)
            {
                continue;
            }

            if (!AreaUses.Contains(destination.TerritoryIntendedUse.RowId) || zoneOf(destination.RowId) is null)
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

        return warps;
    }

    private static List<UnlockMapRegion> ReadRegions(ExcelModule excel, Language language)
    {
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

        return regions;
    }

    private static Dictionary<uint, UnlockAetheryte> ReadAetherytes(ExcelModule excel, Language language, AetheryteIndex? aetherytes)
    {
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

        return aetheryteRows;
    }

    /// <summary>The gates a quest opens, and a row for each gate the aetheryte index has none for.</summary>
    private static (Dictionary<uint, UnlockAetheryte> Rows, List<UnlockGatedAethernet> Gates) ReadGates(ExcelModule excel, Language language, Dictionary<uint, UnlockAetheryte> aetheryteRows, Func<uint, UnlockZone?> zoneOf)
    {
        var gateRows = new Dictionary<uint, UnlockAetheryte>();
        var gates = new List<UnlockGatedAethernet>();
        foreach (var row in excel.GetSheet<Aetheryte>(language))
        {
            if (row.RequiredQuest.RowId == 0)
            {
                continue;
            }

            if (!aetheryteRows.ContainsKey(row.RowId) && !gateRows.ContainsKey(row.RowId))
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

                gateRows[row.RowId] = new UnlockAetheryte(row.RowId, row.Territory.RowId, name, 0f, 0f, row.IsAetheryte);
                zoneOf(row.Territory.RowId);
            }

            gates.Add(new UnlockGatedAethernet(row.RequiredQuest.RowId, row.RowId));
        }

        return (gateRows, gates);
    }

    private static List<UnlockTouch> ReadTouches(ExcelModule excel, Language language, HashSet<uint> areaTerritories)
    {
        var levels = excel.GetSheet<Level>(language);
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

        return touches;
    }

    private static List<UnlockDuty> ReadDuties(ExcelModule excel, Language language)
    {
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

        return duties;
    }

    /// <summary>
    /// The icon of every action a quest can teach: player actions and those of a class or job (Action), traits,
    /// general actions and blue magic (AozAction, which wears its action's icon). Rows without a name or an icon are left out.
    /// </summary>
    private static Dictionary<(RewardKind Kind, uint Id), uint> ReadActionIcons(ExcelModule excel, Language language)
    {
        var icons = new Dictionary<(RewardKind Kind, uint Id), uint>();
        foreach (var row in excel.GetSheet<Lumina.Excel.Sheets.Action>(language))
        {
            if (row.Icon != 0 && !row.Name.IsEmpty && (row.IsPlayerAction || row.ClassJob.RowId != 0 || row.ClassJobLevel != 0))
            {
                icons[(RewardKind.Action, row.RowId)] = row.Icon;
            }
        }

        foreach (var row in excel.GetSheet<Trait>(language))
        {
            if (row.Icon > 0 && !row.Name.IsEmpty)
            {
                icons[(RewardKind.Trait, row.RowId)] = (uint)row.Icon;
            }
        }

        foreach (var row in excel.GetSheet<GeneralAction>(language))
        {
            if (row.Icon > 0 && !row.Name.IsEmpty)
            {
                icons[(RewardKind.GeneralAction, row.RowId)] = (uint)row.Icon;
            }
        }

        foreach (var row in excel.GetSheet<AozAction>(language))
        {
            if (row.Action.ValueNullable is { Icon: > 0 } action)
            {
                icons[(RewardKind.BlueMageSpell, row.RowId)] = action.Icon;
            }
        }

        return icons;
    }
}
