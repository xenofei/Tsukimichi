using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Discovery;

/// <summary>
/// Pure lookups behind the discovery commands: what can be started in a zone, what an NPC hands out.
/// </summary>
public static class QuestDiscovery
{
    /// <summary>
    /// Quests whose giver stands in <paramref name="territoryId"/> and whose state is Ready or ReadyOnOtherJob,
    /// sorted by level then name. Empty for territory 0 or without evaluations.
    /// </summary>
    public static List<QuestRecord> StartableInZone(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> states, uint territoryId) =>
        StartableInZone(catalog, states, territoryId, includeOtherJob: true);

    /// <summary>
    /// Quests whose giver stands in <paramref name="territoryId"/> and whose state is Ready, plus ReadyOnOtherJob when
    /// <paramref name="includeOtherJob"/> is set, sorted by level then name. Empty for territory 0 or without evaluations.
    /// </summary>
    public static List<QuestRecord> StartableInZone(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> states, uint territoryId, bool includeOtherJob) =>
        InZoneWithState(catalog, states, territoryId, includeOtherJob ? ZoneFilter.Startable : ZoneFilter.ReadyOnly);

    /// <summary>
    /// Accepted quests whose giver stands in <paramref name="territoryId"/>, sorted by level then name: the "also
    /// accepted here" list. Empty for territory 0 or without evaluations.
    /// </summary>
    public static List<QuestRecord> AcceptedInZone(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> states, uint territoryId) =>
        InZoneWithState(catalog, states, territoryId, ZoneFilter.Accepted);

    private enum ZoneFilter
    {
        ReadyOnly,
        Startable,
        Accepted,
    }

    private static bool Matches(ZoneFilter filter, QuestState state) => filter switch
    {
        ZoneFilter.ReadyOnly => state == QuestState.Ready,
        ZoneFilter.Startable => state is QuestState.Ready or QuestState.ReadyOnOtherJob,
        _ => state == QuestState.Accepted,
    };

    private static List<QuestRecord> InZoneWithState(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> states, uint territoryId, ZoneFilter filter)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(states);

        var matches = new List<QuestRecord>();
        if (territoryId == 0 || states.Count == 0)
        {
            return matches;
        }

        foreach (var quest in catalog.All)
        {
            if (quest.IsRetired || quest.Issuer is not { } issuer || issuer.TerritoryId != territoryId)
            {
                continue;
            }

            // A spare alternative (the choice's other options) is not startable news; the journal never holds one.
            if (!states.TryGetValue(quest.RowId, out var evaluation) || !Matches(filter, evaluation.State) || evaluation.IsSpareAlternative)
            {
                continue;
            }

            matches.Add(quest);
        }

        matches.Sort(static (a, b) =>
        {
            var byLevel = a.DisplayLevel.CompareTo(b.DisplayLevel);
            return byLevel != 0 ? byLevel : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
        });
        return matches;
    }

    /// <summary>
    /// Quests whose issuer is the NPC with this id (ENpcResident row id, the game object's base id), in journal order.
    /// Retired quests (<see cref="QuestRecord.IsRetired"/>) are left out: the NPC no longer hands them out. A live
    /// quest with no journal genre (<see cref="QuestRecord.IsUnlisted"/>; under <see cref="JournalFiling.Legacy"/> all
    /// 180 genre-0 rows) stays, since the NPC still gives it; <see cref="Query.QuestQuery"/> decides whether the
    /// <see cref="Query.QuestScope.Issuer"/> scope shows it. Backs <c>/tsuki which</c>, the NPC context menu's count
    /// and that scope.
    /// </summary>
    public static List<QuestRecord> IssuedBy(QuestCatalog catalog, uint npcId)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var matches = new List<QuestRecord>();
        if (npcId == 0)
        {
            return matches;
        }

        foreach (var quest in catalog.All)
        {
            if (!quest.IsRetired && quest.Issuer is { } issuer && issuer.NpcId == npcId)
            {
                matches.Add(quest);
            }
        }

        return matches;
    }

    /// <summary>The name the catalog records for the NPC with this id, from its first quest; null when it issues none.</summary>
    public static string? IssuerName(QuestCatalog catalog, uint npcId)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (npcId == 0)
        {
            return null;
        }

        foreach (var quest in catalog.All)
        {
            if (quest.Issuer is { } issuer && issuer.NpcId == npcId && issuer.Name.Length > 0)
            {
                return issuer.Name;
            }
        }

        return null;
    }
}
