using System.Collections.Frozen;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Tsukimichi.GameData;

/// <summary>An aether current that a quest attunes: the AetherCurrent row and the quest that grants it.</summary>
/// <param name="AetherCurrentId">AetherCurrent sheet row id, the id <c>PlayerState.IsAetherCurrentUnlocked</c> takes.</param>
/// <param name="QuestRowId">Quest sheet row id of the quest whose completion attunes the current.</param>
public sealed record FlightCurrent(uint AetherCurrentId, uint QuestRowId);

/// <summary>
/// One flying zone: an AetherCurrentCompFlgSet row. The set holds every current the zone needs for flight; the ones
/// with a quest are listed in <see cref="QuestCurrents"/>, the rest are field currents found with the Aether Compass.
/// </summary>
/// <param name="TerritoryId">TerritoryType row id, comparable with <c>IClientState.TerritoryType</c>.</param>
/// <param name="Name">Zone place name.</param>
/// <param name="Expansion">ExVersion row id (0 = A Realm Reborn, 1 = Heavensward, …).</param>
/// <param name="QuestCurrents">Currents attuned by completing a quest, in sheet order.</param>
/// <param name="FieldCurrentIds">AetherCurrent row ids of the field currents (no quest), in sheet order.</param>
public sealed record FlightZone(uint TerritoryId, string Name, byte Expansion, IReadOnlyList<FlightCurrent> QuestCurrents, IReadOnlyList<uint> FieldCurrentIds)
{
    /// <summary>How many currents in the zone are found in the field rather than granted by a quest.</summary>
    public int FieldCurrentCount => FieldCurrentIds.Count;

    /// <summary>Every current the zone needs for flight.</summary>
    public int TotalCurrents => QuestCurrents.Count + FieldCurrentIds.Count;
}

/// <summary>
/// Flying zones with their aether currents, read once from the AetherCurrentCompFlgSet, AetherCurrent and
/// TerritoryType sheets. Zones are ordered by expansion, then by name. Standalone (takes an <see cref="ExcelModule"/>)
/// so tests can build it against game data without Dalamud.
/// </summary>
public sealed class FlightIndex
{
    private static readonly FlightZone[] None = [];

    public static readonly FlightIndex Empty = new(None, FrozenDictionary<uint, FlightZone>.Empty, 0);

    /// <summary>
    /// Action sheet row of "the Aether Compass", the action (listed under General in the game's Actions window) that
    /// points at field currents; its icon decorates the field-currents line. Verified against the sheets in tests.
    /// </summary>
    public const uint AetherCompassAction = 26988;

    private readonly FrozenDictionary<uint, FlightZone> byTerritory;

    private FlightIndex(FlightZone[] zones, FrozenDictionary<uint, FlightZone> byTerritory, uint aetherCompassIcon)
    {
        Zones = zones;
        this.byTerritory = byTerritory;
        AetherCompassIcon = aetherCompassIcon;
    }

    /// <summary>Every flying zone, ordered by expansion then name.</summary>
    public IReadOnlyList<FlightZone> Zones { get; }

    /// <summary>Icon id of the Aether Compass action, or 0 when the sheet row is missing.</summary>
    public uint AetherCompassIcon { get; }

    /// <summary>The flying zone for a territory, or null when the territory has no aether currents (cities, dungeons, ARR zones).</summary>
    public FlightZone? ZoneFor(uint territoryId) => byTerritory.GetValueOrDefault(territoryId);

    /// <summary>Builds an index from already-read zones; for tests and callers that read the sheets themselves. Sorts them.</summary>
    public static FlightIndex From(IEnumerable<FlightZone> zones, uint aetherCompassIcon = 0)
    {
        ArgumentNullException.ThrowIfNull(zones);

        var list = new List<FlightZone>();
        var byTerritory = new Dictionary<uint, FlightZone>();
        foreach (var zone in zones)
        {
            if (byTerritory.TryAdd(zone.TerritoryId, zone))
            {
                list.Add(zone);
            }
        }

        if (list.Count == 0)
        {
            return Empty;
        }

        list.Sort(static (a, b) =>
        {
            var byExpansion = a.Expansion.CompareTo(b.Expansion);
            return byExpansion != 0 ? byExpansion : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
        });

        return new FlightIndex(list.ToArray(), byTerritory.ToFrozenDictionary(), aetherCompassIcon);
    }

    /// <summary>
    /// Reads the sheets: one zone per AetherCurrentCompFlgSet row with a territory, its currents split by whether
    /// <c>AetherCurrent.Quest</c> is set. Rows without a territory or without a single current are skipped. The set is
    /// what the game's own Aether Currents window and completion flag use: since patch 6.0 the Heavensward to
    /// Endwalker zones list five quest currents and four field currents (the other field rows stay in the AetherCurrent
    /// sheet unreferenced), Dawntrail zones five and ten, and Mor Dhona carries the single current (The Ultimate
    /// Weapon) that opens flight in A Realm Reborn.
    /// </summary>
    public static FlightIndex Build(ExcelModule excel, Language language = Language.None)
    {
        ArgumentNullException.ThrowIfNull(excel);

        var currents = excel.GetSheet<AetherCurrent>(language);
        var zones = new List<FlightZone>();
        foreach (var set in excel.GetSheet<AetherCurrentCompFlgSet>(language))
        {
            if (set.Territory.ValueNullable is not { } territory)
            {
                continue;
            }

            var questCurrents = new List<FlightCurrent>();
            var fieldCurrents = new List<uint>();
            foreach (var slot in set.AetherCurrents)
            {
                if (slot.RowId == 0 || currents.GetRowOrDefault(slot.RowId) is not { } current)
                {
                    continue;
                }

                if (current.Quest.RowId != 0)
                {
                    questCurrents.Add(new FlightCurrent(current.RowId, current.Quest.RowId));
                }
                else
                {
                    fieldCurrents.Add(current.RowId);
                }
            }

            if (questCurrents.Count == 0 && fieldCurrents.Count == 0)
            {
                continue;
            }

            var name = territory.PlaceName.ValueNullable?.Name.ExtractText().Trim() ?? string.Empty;
            if (name.Length == 0)
            {
                name = "Territory " + territory.RowId;
            }

            var expansion = territory.ExVersion.RowId;
            zones.Add(new FlightZone(
                territory.RowId,
                name,
                expansion <= byte.MaxValue ? (byte)expansion : byte.MaxValue,
                questCurrents.ToArray(),
                fieldCurrents.ToArray()));
        }

        var compass = excel.GetSheet<Lumina.Excel.Sheets.Action>(language).GetRowOrDefault(AetherCompassAction);
        return From(zones, compass?.Icon ?? 0u);
    }
}
