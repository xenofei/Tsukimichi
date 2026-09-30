using System.Collections.Frozen;
using System.Globalization;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Tsukimichi.GameData;

/// <summary>An aether current that a quest attunes: the AetherCurrent row and the quest that grants it.</summary>
/// <param name="AetherCurrentId">AetherCurrent sheet row id, the id <c>PlayerState.IsAetherCurrentUnlocked</c> takes.</param>
/// <param name="QuestRowId">
/// Quest sheet row id of the quest whose completion attunes the current, as <see cref="AetherCurrentQuests"/> resolves it.
/// </param>
/// <param name="ListedQuestRowId">
/// The quest <c>AetherCurrent.Quest</c> names, kept for diagnostics; it differs from <paramref name="QuestRowId"/> for
/// the five currents the sheet lists wrongly. 0 means "the same as <paramref name="QuestRowId"/>".
/// </param>
public sealed record FlightCurrent(uint AetherCurrentId, uint QuestRowId, uint ListedQuestRowId = 0)
{
    /// <summary>True when the sheet's listed quest was replaced by the real awarding quest.</summary>
    public bool Corrected => ListedQuestRowId != 0 && ListedQuestRowId != QuestRowId;
}

/// <summary>
/// One flying zone: an AetherCurrentCompFlgSet row. The set holds every current the zone needs for flight; the ones
/// with a quest are listed in <see cref="QuestCurrents"/>, the rest are field currents found with the Aether Compass.
/// </summary>
/// <param name="TerritoryId">The set's own TerritoryType row id, comparable with <c>IClientState.TerritoryType</c>.</param>
/// <param name="Name">Zone place name; for a set shared by several territories, "&lt;expansion&gt; (all zones)".</param>
/// <param name="Expansion">ExVersion row id (0 = A Realm Reborn, 1 = Heavensward, …).</param>
/// <param name="QuestCurrents">Currents attuned by completing a quest, in sheet order.</param>
/// <param name="FieldCurrentIds">AetherCurrent row ids of the field currents (no quest), in sheet order.</param>
/// <param name="OtherTerritoryIds">
/// Further territories whose <c>TerritoryType.AetherCurrentCompFlgSet</c> is this set: the sixteen other A Realm Reborn
/// field zones share Mor Dhona's set. Null or empty for every later zone.
/// </param>
public sealed record FlightZone(
    uint TerritoryId,
    string Name,
    byte Expansion,
    IReadOnlyList<FlightCurrent> QuestCurrents,
    IReadOnlyList<uint> FieldCurrentIds,
    IReadOnlyList<uint>? OtherTerritoryIds = null)
{
    /// <summary>How many currents in the zone are found in the field rather than granted by a quest.</summary>
    public int FieldCurrentCount => FieldCurrentIds.Count;

    /// <summary>Every current the zone needs for flight.</summary>
    public int TotalCurrents => QuestCurrents.Count + FieldCurrentIds.Count;

    /// <summary>True when the set covers more than one territory (A Realm Reborn's covers all seventeen field zones).</summary>
    public bool CoversManyTerritories => OtherTerritoryIds is { Count: > 0 };
}

/// <summary>
/// Flying zones with their aether currents, read once from the AetherCurrentCompFlgSet, AetherCurrent, Quest and
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

    /// <summary>English label of an entry that covers every field zone of an expansion; {0} = the expansion's name.</summary>
    public const string AllZonesFormat = "{0} (all zones)";

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

    /// <summary>
    /// The flying zone for a territory, or null when the territory has no aether currents (cities, dungeons). Each of
    /// the seventeen A Realm Reborn field zones answers the one A Realm Reborn entry.
    /// </summary>
    public FlightZone? ZoneFor(uint territoryId) => byTerritory.GetValueOrDefault(territoryId);

    /// <summary>Builds an index from already-read zones; for tests and callers that read the sheets themselves. Sorts them.</summary>
    public static FlightIndex From(IEnumerable<FlightZone> zones, uint aetherCompassIcon = 0)
    {
        ArgumentNullException.ThrowIfNull(zones);

        var list = new List<FlightZone>();
        var byTerritory = new Dictionary<uint, FlightZone>();
        foreach (var zone in zones)
        {
            if (!byTerritory.TryAdd(zone.TerritoryId, zone))
            {
                continue;
            }

            list.Add(zone);
            if (zone.OtherTerritoryIds is { } others)
            {
                foreach (var other in others)
                {
                    byTerritory.TryAdd(other, zone);
                }
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
    /// sheet unreferenced), Dawntrail zones five and ten, and A Realm Reborn's set (its territory is Mor Dhona) carries
    /// the single current (The Ultimate Weapon) that opens flight in all seventeen of its field zones.
    /// <para>
    /// Each quest current's quest is the one that awards it (<see cref="AetherCurrentQuests"/>), which is not always
    /// the one the sheet lists. Territories are mapped through <c>TerritoryType.AetherCurrentCompFlgSet</c>, so a set
    /// shared by several territories becomes one entry named with <paramref name="allZonesFormat"/> ({0} = the
    /// expansion's name: "A Realm Reborn (all zones)"), and <see cref="ZoneFor"/> answers it in each of them.
    /// </para>
    /// </summary>
    public static FlightIndex Build(ExcelModule excel, Language language = Language.None, string allZonesFormat = AllZonesFormat)
    {
        ArgumentNullException.ThrowIfNull(excel);
        ArgumentNullException.ThrowIfNull(allZonesFormat);

        var currents = excel.GetSheet<AetherCurrent>(language);
        var quests = excel.GetSheet<Quest>(language);
        var expansions = excel.GetSheet<ExVersion>(language);
        var territoriesBySet = TerritoriesBySet(excel, language);

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

                if (AetherCurrentQuests.Resolve(current, quests) is { } awarding)
                {
                    questCurrents.Add(new FlightCurrent(current.RowId, awarding.QuestRowId, awarding.ListedQuestRowId));
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

            var expansion = territory.ExVersion.RowId;
            uint[] others = territoriesBySet.TryGetValue(set.RowId, out var mapped)
                ? mapped.Where(id => id != territory.RowId).ToArray()
                : [];

            var name = others.Length > 0 && expansions.GetRowOrDefault(expansion)?.Name.ExtractText().Trim() is { Length: > 0 } expansionName
                ? string.Format(CultureInfo.CurrentCulture, allZonesFormat, expansionName)
                : territory.PlaceName.ValueNullable?.Name.ExtractText().Trim() ?? string.Empty;
            if (name.Length == 0)
            {
                name = "Territory " + territory.RowId;
            }

            zones.Add(new FlightZone(
                territory.RowId,
                name,
                expansion <= byte.MaxValue ? (byte)expansion : byte.MaxValue,
                questCurrents.ToArray(),
                fieldCurrents.ToArray(),
                others));
        }

        var compass = excel.GetSheet<Lumina.Excel.Sheets.Action>(language).GetRowOrDefault(AetherCompassAction);
        return From(zones, compass?.Icon ?? 0u);
    }

    /// <summary>AetherCurrentCompFlgSet row id to the territories whose <c>TerritoryType.AetherCurrentCompFlgSet</c> names it, in sheet order.</summary>
    private static Dictionary<uint, List<uint>> TerritoriesBySet(ExcelModule excel, Language language)
    {
        var bySet = new Dictionary<uint, List<uint>>();
        foreach (var territoryType in excel.GetSheet<TerritoryType>(language))
        {
            var setId = territoryType.AetherCurrentCompFlgSet.RowId;
            if (setId == 0)
            {
                continue;
            }

            if (!bySet.TryGetValue(setId, out var list))
            {
                bySet[setId] = list = [];
            }

            list.Add(territoryType.RowId);
        }

        return bySet;
    }
}
