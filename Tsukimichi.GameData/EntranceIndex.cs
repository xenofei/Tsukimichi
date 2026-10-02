using Lumina.Data;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Travel;

namespace Tsukimichi.GameData;

/// <summary>
/// The way into interiors: territories a quest giver can stand in that no teleport or aethernet hop reaches (no
/// aetheryte, no shard, not one of <see cref="TravelSpecials"/>' zones), such as the Waking Sands, the Rising Stones,
/// Fortemps Manor or a story area like Zero's Domain. For each, <see cref="For"/> answers which outside territory its
/// door stands in and, when the data says, where: Go to giver then teleports to the aetheryte nearest that door, not
/// to the one nearest the giver's position inside (a position in another territory's coordinates), and walks to the
/// door.
/// <para>Read from the game's event layout (each territory's <c>planevent.lgb</c>): the NPCs and objects there whose
/// event is a <c>Warp</c> row (directly, through a <c>CustomTalk</c> script or a <c>PreHandler</c>) lead somewhere.
/// Inside the interior, a warp to another territory names the outside (an exit warp's <c>PopRange</c> level, when set,
/// is the spot outside the door); outside, the NPC or door whose warp leads in is the entrance. An interior whose exit
/// leads into another interior (the Copied Factory into the Excavation Tunnels) takes that one's door. An interior
/// with no exit in the data (a story area entered through the quest itself) takes the territory of the aetheryte its
/// TerritoryType row names, unplaced unless a warp in there leads in.</para>
/// Resolved lazily per territory and cached; a layout file that cannot be read counts as empty, and a territory whose
/// resolution throws counts as having no way in (logged once through <see cref="OnError"/>). <see cref="Warm"/> resolves
/// a set of territories ahead, on a worker thread, so the draw thread never resolves many at once; meanwhile
/// <see cref="Peek"/> answers only what is known without resolving, and <see cref="Revision"/> moves when a warm-up
/// ends. Thread-safe. Standalone (takes an <see cref="ExcelModule"/> and a layout reader) so tests can build it against
/// game data without Dalamud.
/// </summary>
public sealed class EntranceIndex
{
    /// <summary>How many interiors deep an exit is followed (an interior inside an interior).</summary>
    public const int MaxDepth = 3;

    /// <summary>
    /// Handler kinds of an NPC's or object's event id (its high 16 bits), as FFXIVClientStructs'
    /// <c>EventHandlerContent</c> names them: Warp, CustomTalk, Array (an ArrayEventHandler row listing handlers, what
    /// most doors carry) and PreHandler (a confirmation in front of another handler).
    /// </summary>
    private const uint WarpHandler = 0x2;
    private const uint CustomTalkHandler = 0xB;
    private const uint ArrayHandler = 0xD;
    private const uint PreHandlerHandler = 0x36;

    /// <summary>TerritoryIntendedUse of an open field zone, the only kind left over a zone line.</summary>
    private const uint FieldUse = 1;

    public static readonly EntranceIndex Empty = new(null, null, Language.None, AetheryteIndex.Empty);

    /// <summary>One way out of a territory: a warp (an NPC or door) or a zone line, where it leads and where it stands.</summary>
    private readonly record struct Way(uint Destination, float X, float Y, float Z, uint PopLevel, bool ZoneLine);

    private readonly ExcelModule? excel;
    private readonly Func<string, LgbFile?>? readLayout;
    private readonly Language language;
    private readonly AetheryteIndex aetherytes;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<uint, InteriorEntrance?> cache = new();
    private readonly Dictionary<uint, IReadOnlyList<Way>> warpsByTerritory = [];
    private readonly Lock gate = new();
    private int revision;
    private int warming;
    private int errorLogged;

    private EntranceIndex(ExcelModule? excel, Func<string, LgbFile?>? readLayout, Language language, AetheryteIndex aetherytes)
    {
        this.excel = excel;
        this.readLayout = readLayout;
        this.language = language;
        this.aetherytes = aetherytes;
    }

    /// <summary>
    /// An index over the game data. <paramref name="readLayout"/> reads a layout file by its game path (the plugin
    /// passes Dalamud's data manager, tests Lumina) and may return null or throw for a file that is missing or does not
    /// parse.
    /// </summary>
    public static EntranceIndex Create(ExcelModule excel, Func<string, LgbFile?> readLayout, AetheryteIndex aetherytes, Language language = Language.None)
    {
        ArgumentNullException.ThrowIfNull(excel);
        ArgumentNullException.ThrowIfNull(readLayout);
        ArgumentNullException.ThrowIfNull(aetherytes);
        return new EntranceIndex(excel, readLayout, language, aetherytes);
    }

    /// <summary>
    /// True for a territory travel cannot land in: no aetheryte and no aethernet shard stands in it, and it is not one
    /// of the zones <see cref="TravelSpecials"/> reaches its own way. False for 0.
    /// </summary>
    public bool IsInterior(uint territoryId) =>
        territoryId != 0 && !aetherytes.Reachable(territoryId) && TravelSpecials.Classify(territoryId) == TravelSpecial.None;

    /// <summary>
    /// Called once, with the territory, for the first territory whose resolution threw (the plugin logs it); every
    /// such territory reads as having no way in. Null logs nothing.
    /// </summary>
    public Action<Exception, uint>? OnError { get; set; }

    /// <summary>Moves each time a <see cref="Warm"/> ends: answers <see cref="Peek"/> could not give before may be known now.</summary>
    public int Revision => Volatile.Read(ref revision);

    /// <summary>True while a <see cref="Warm"/> runs.</summary>
    public bool Warming => Volatile.Read(ref warming) > 0;

    /// <summary>
    /// The way into <paramref name="territoryId"/>; null when it is no interior, the data names no outside for it, or
    /// its resolution threw. Resolves it now when no answer is cached (may wait for a warm-up's current territory).
    /// </summary>
    public InteriorEntrance? For(uint territoryId)
    {
        if (excel is null || !IsInterior(territoryId))
        {
            return null;
        }

        if (cache.TryGetValue(territoryId, out var known))
        {
            return known;
        }

        lock (gate)
        {
            if (cache.TryGetValue(territoryId, out var entrance))
            {
                return entrance;
            }

            try
            {
                entrance = Resolve(territoryId, [territoryId]);
            }
            catch (Exception ex)
            {
                // A sheet or layout this code does not expect: no way in for this one, the zone's aetheryte instead.
                entrance = null;
                if (Interlocked.Exchange(ref errorLogged, 1) == 0)
                {
                    OnError?.Invoke(ex, territoryId);
                }
            }

            cache[territoryId] = entrance;
            return entrance;
        }
    }

    /// <summary>
    /// The answer <see cref="For"/> would give, when it is known without resolving anything (no interior, or resolved
    /// already): true with <paramref name="entrance"/> set; false while the territory still waits to be resolved.
    /// Never blocks.
    /// </summary>
    public bool Peek(uint territoryId, out InteriorEntrance? entrance)
    {
        if (excel is null || !IsInterior(territoryId))
        {
            entrance = null;
            return true;
        }

        return cache.TryGetValue(territoryId, out entrance);
    }

    /// <summary>
    /// Resolves each of <paramref name="territories"/> (the givers' territories) that is an interior, one at a time, so
    /// a later <see cref="For"/> or <see cref="Peek"/> finds it cached; meant for a worker thread. Stops early when
    /// <paramref name="token"/> is cancelled. <see cref="Revision"/> moves when it ends either way.
    /// </summary>
    public void Warm(IEnumerable<uint> territories, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(territories);
        Interlocked.Increment(ref warming);
        try
        {
            foreach (var territory in territories)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                For(territory);
            }
        }
        finally
        {
            Interlocked.Decrement(ref warming);
            Interlocked.Increment(ref revision);
        }
    }

    private InteriorEntrance? Resolve(uint interior, List<uint> visited)
    {
        var territories = excel!.GetSheet<TerritoryType>(language);
        if (territories.GetRowOrDefault(interior) is not { } row)
        {
            return null;
        }

        var preferred = row.Aetheryte.RowId != 0 ? aetherytes.Find(row.Aetheryte.RowId) : null;

        // Only an open field (the Dravanian Hinterlands) is left on foot over a zone line; the zone lines a building or
        // story area's map carries are leftovers that lead nowhere the player goes. The exits are tried best first; one
        // whose way out ends where the zone's own aetheryte stands wins over a better-looking one that ends elsewhere
        // (a story area's passage a later patch added toward another region).
        var preferredTerritory = preferred?.TerritoryId ?? 0;
        InteriorEntrance? fallback = null;
        foreach (var exit in RankExits(interior, visited, row.Aetheryte.RowId, preferredTerritory, row.TerritoryIntendedUse.RowId == FieldUse))
        {
            var entrance = Through(interior, exit, visited, preferred);
            if (entrance is not { } found)
            {
                continue;
            }

            if (preferredTerritory == 0 || found.TerritoryId == preferredTerritory)
            {
                return found;
            }

            fallback ??= found;
        }

        if (fallback is not null)
        {
            return fallback;
        }

        // No exit in the data (a story area the quest itself takes you into): the aetheryte the zone names.
        if (preferred is { } aetheryte && aetheryte.TerritoryId != interior)
        {
            return EntryIn(aetheryte.TerritoryId, interior, preferred);
        }

        return null;
    }

    /// <summary>The way in through one exit: the exit's own pop spot or the entrance outside, or the next interior's way in.</summary>
    private InteriorEntrance? Through(uint interior, Way exit, List<uint> visited, AetheryteInfo? preferred)
    {
        if (IsInterior(exit.Destination))
        {
            // A room inside another interior: its way in is that interior's.
            return visited.Count <= MaxDepth && Resolve(exit.Destination, [.. visited, exit.Destination]) is { } outer
                ? outer with { Interior = interior }
                : null;
        }

        var levels = excel!.GetSheet<Level>(language);
        if (exit.PopLevel != 0 && levels.GetRowOrDefault(exit.PopLevel) is { } pop && pop.Territory.RowId == exit.Destination)
        {
            return new InteriorEntrance(interior, exit.Destination, pop.X, pop.Y, pop.Z, true);
        }

        return EntryIn(exit.Destination, interior, preferred);
    }

    /// <summary>
    /// The ways out of an interior, best first: into a territory travel reaches, then into the territory of the zone's
    /// own aetheryte, then into a zone that names the same aetheryte (a story area's neighbour), then layout order.
    /// One per destination; none back into a territory already on the way.
    /// </summary>
    private List<Way> RankExits(uint interior, List<uint> visited, uint preferredAetheryte, uint preferredTerritory, bool zoneLines)
    {
        var territories = excel!.GetSheet<TerritoryType>(language);
        var ranked = new List<(Way Way, int Score, int Order)>();
        foreach (var warp in WarpsIn(interior))
        {
            if (warp.Destination == 0 || visited.Contains(warp.Destination) || (warp.ZoneLine && !zoneLines))
            {
                continue;
            }

            // One per destination, the one that names its pop spot outside when any does.
            var known = ranked.FindIndex(r => r.Way.Destination == warp.Destination);
            if (known >= 0)
            {
                if (ranked[known].Way.PopLevel == 0 && warp.PopLevel != 0)
                {
                    ranked[known] = ranked[known] with { Way = warp };
                }

                continue;
            }

            var sameHome = preferredAetheryte != 0 && territories.GetRowOrDefault(warp.Destination)?.Aetheryte.RowId == preferredAetheryte;
            var score = (IsInterior(warp.Destination) ? 0 : 4) + (warp.Destination == preferredTerritory ? 2 : 0) + (sameHome ? 1 : 0);
            ranked.Add((warp, score, ranked.Count));
        }

        ranked.Sort(static (a, b) => a.Score != b.Score ? b.Score.CompareTo(a.Score) : a.Order.CompareTo(b.Order));
        return ranked.ConvertAll(static r => r.Way);
    }

    /// <summary>
    /// Where in <paramref name="outside"/> the way into <paramref name="interior"/> stands: the NPC or door whose warp
    /// leads in (the one nearest the zone's own aetheryte when there are several); unplaced when none does.
    /// </summary>
    private InteriorEntrance EntryIn(uint outside, uint interior, AetheryteInfo? preferred)
    {
        (float X, float Y, float Z)? best = null;
        var bestDistance = float.MaxValue;
        foreach (var warp in WarpsIn(outside))
        {
            if (warp.Destination != interior)
            {
                continue;
            }

            var distance = preferred is { } p && p.TerritoryId == outside ? TravelPlanner.Distance(warp.X, warp.Z, p.X, p.Z) : 0f;
            if (best is null || distance < bestDistance)
            {
                best = (warp.X, warp.Y, warp.Z);
                bestDistance = distance;
            }
        }

        return best is { } at
            ? new InteriorEntrance(interior, outside, at.X, at.Y, at.Z, true)
            : new InteriorEntrance(interior, outside, 0f, 0f, 0f, false);
    }

    /// <summary>
    /// The ways out of a territory: the warps placed in its event layout and the zone lines of its map layout, each
    /// with where it leads, where it stands and (a warp's) pop level.
    /// </summary>
    private IReadOnlyList<Way> WarpsIn(uint territory)
    {
        if (warpsByTerritory.TryGetValue(territory, out var known))
        {
            return known;
        }

        var found = new List<Way>();

        // Zone lines (exit ranges in the map layout) lead on foot into the next territory.
        if (LayoutPath(territory, "planmap.lgb") is { } mapPath && Read(mapPath) is { } map)
        {
            foreach (var layer in map.Layers)
            {
                foreach (var instance in layer.InstanceObjects)
                {
                    if (instance.Object is LayerCommon.ExitRangeInstanceObject exit && exit.TerritoryType != 0)
                    {
                        var at = instance.Transform.Translation;
                        found.Add(new Way(exit.TerritoryType, at.X, at.Y, at.Z, 0u, true));
                    }
                }
            }
        }

        if (LayoutPath(territory, "planevent.lgb") is { } path && Read(path) is { } layout)
        {
            var npcs = excel!.GetSheet<ENpcBase>(language);
            var objects = excel.GetSheet<EObj>(language);
            var warps = excel.GetSheet<Warp>(language);
            var handlers = new List<uint>(4);
            foreach (var layer in layout.Layers)
            {
                foreach (var instance in layer.InstanceObjects)
                {
                    handlers.Clear();
                    switch (instance.Object)
                    {
                        case LayerCommon.ENPCInstanceObject npc when npcs.GetRowOrDefault(npc.ParentData.ParentData.BaseId) is { } npcRow:
                            foreach (var data in npcRow.ENpcData)
                            {
                                if (data.RowId != 0)
                                {
                                    Collect(data.RowId, handlers, 0);
                                }
                            }

                            break;
                        case LayerCommon.EventInstanceObject obj when objects.GetRowOrDefault(obj.ParentData.BaseId) is { } objRow && objRow.Data.RowId != 0:
                            Collect(objRow.Data.RowId, handlers, 0);
                            break;
                        default:
                            continue;
                    }

                    var at = instance.Transform.Translation;
                    foreach (var warpId in handlers)
                    {
                        if (warps.GetRowOrDefault(warpId) is { } warp && warp.TerritoryType.RowId != 0)
                        {
                            found.Add(new Way(warp.TerritoryType.RowId, at.X, at.Y, at.Z, warp.PopRange.RowId, false));
                        }
                    }
                }
            }
        }

        warpsByTerritory[territory] = found;
        return found;
    }

    /// <summary>The warp rows an event id leads to: itself for a warp, a custom talk's script arguments, a pre-handler's target.</summary>
    private void Collect(uint handler, List<uint> into, int depth)
    {
        switch (handler >> 16)
        {
            case WarpHandler:
                into.Add(handler);
                break;
            case CustomTalkHandler when excel!.GetSheet<CustomTalk>(language).GetRowOrDefault(handler) is { } talk:
                foreach (var script in talk.Script)
                {
                    if (script.ScriptArg >> 16 == WarpHandler)
                    {
                        into.Add(script.ScriptArg);
                    }
                }

                break;
            case PreHandlerHandler when depth < 3 && excel!.GetSheet<PreHandler>(language).GetRowOrDefault(handler) is { } pre && pre.Target.RowId != 0:
                Collect(pre.Target.RowId, into, depth + 1);
                break;
            case ArrayHandler when depth < 3 && excel!.GetSheet<ArrayEventHandler>(language).GetRowOrDefault(handler) is { } array:
                foreach (var item in array.Data)
                {
                    if (item.RowId != 0)
                    {
                        Collect(item.RowId, into, depth + 1);
                    }
                }

                break;
        }
    }

    /// <summary><c>bg/…/level/<paramref name="file"/></c> beside the territory's level file; null when the row names none.</summary>
    private string? LayoutPath(uint territory, string file)
    {
        if (excel!.GetSheet<TerritoryType>(language).GetRowOrDefault(territory) is not { } row)
        {
            return null;
        }

        var bg = row.Bg.ExtractText();
        var slash = bg.LastIndexOf('/');
        return slash <= 0 ? null : $"bg/{bg[..slash]}/{file}";
    }

    private LgbFile? Read(string path)
    {
        try
        {
            return readLayout!(path);
        }
        catch (Exception)
        {
            // A layout that does not parse (a newer format) counts as empty: no entrance, the old fallback.
            return null;
        }
    }
}
