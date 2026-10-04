using Lumina.Data;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Tsukimichi.GameData;

/// <summary>Where a field aether current stands: its territory and world position (map X and Z, height Y).</summary>
/// <param name="AetherCurrentId">AetherCurrent sheet row id.</param>
/// <param name="TerritoryId">The TerritoryType row whose layout places it.</param>
public sealed record AetherCurrentPlace(uint AetherCurrentId, uint TerritoryId, float X, float Y, float Z);

/// <summary>
/// Where a flying zone's field currents stand (plan v7, 1.19.0 K3, Route to unlock flying): each current is an event
/// object (<c>EObj</c>) whose <c>Data</c> is the AetherCurrent row, placed in the territory's <c>planevent.lgb</c>
/// layout. Read from the game's own files, never guessed: a current no layout places is left out (the Route window
/// then counts it without a place). Standalone (an <see cref="ExcelModule"/> and a layout reader) so tests can read
/// the game data without Dalamud.
/// </summary>
public static class AetherCurrentPlaces
{
    /// <summary>
    /// The places of <paramref name="zone"/>'s field currents, in the zone's sheet order, from the layouts of its
    /// territory and of every territory sharing its set. A layout that cannot be read places nothing.
    /// </summary>
    public static IReadOnlyList<AetherCurrentPlace> Read(ExcelModule excel, Func<string, LgbFile?> readLayout, FlightZone zone, Language language = Language.None)
    {
        ArgumentNullException.ThrowIfNull(excel);
        ArgumentNullException.ThrowIfNull(readLayout);
        ArgumentNullException.ThrowIfNull(zone);
        if (zone.FieldCurrentIds.Count == 0)
        {
            return [];
        }

        var wanted = new HashSet<uint>(zone.FieldCurrentIds);
        var found = new Dictionary<uint, AetherCurrentPlace>();
        var territories = new List<uint> { zone.TerritoryId };
        if (zone.OtherTerritoryIds is { } others)
        {
            territories.AddRange(others);
        }

        var objects = excel.GetSheet<EObj>(language);
        foreach (var territory in territories)
        {
            if (LayoutPath(excel, language, territory) is not { } path || Read(readLayout, path) is not { } layout)
            {
                continue;
            }

            foreach (var layer in layout.Layers)
            {
                foreach (var instance in layer.InstanceObjects)
                {
                    if (instance.Object is LayerCommon.EventInstanceObject obj
                        && objects.GetRowOrDefault(obj.ParentData.BaseId) is { } row
                        && wanted.Contains(row.Data.RowId)
                        && !found.ContainsKey(row.Data.RowId))
                    {
                        var at = instance.Transform.Translation;
                        found[row.Data.RowId] = new AetherCurrentPlace(row.Data.RowId, territory, at.X, at.Y, at.Z);
                    }
                }
            }
        }

        var places = new List<AetherCurrentPlace>(found.Count);
        foreach (var id in zone.FieldCurrentIds)
        {
            if (found.TryGetValue(id, out var place))
            {
                places.Add(place);
            }
        }

        return places;
    }

    /// <summary><c>bg/…/level/planevent.lgb</c> beside the territory's level file; null when the row names none.</summary>
    private static string? LayoutPath(ExcelModule excel, Language language, uint territory)
    {
        if (excel.GetSheet<TerritoryType>(language).GetRowOrDefault(territory) is not { } row)
        {
            return null;
        }

        var bg = row.Bg.ExtractText();
        var slash = bg.LastIndexOf('/');
        return slash <= 0 ? null : $"bg/{bg[..slash]}/planevent.lgb";
    }

    private static LgbFile? Read(Func<string, LgbFile?> readLayout, string path)
    {
        try
        {
            return readLayout(path);
        }
        catch (Exception)
        {
            // A layout that does not parse (a newer format) places nothing; the Route window counts its currents instead.
            return null;
        }
    }
}
