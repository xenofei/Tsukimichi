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
    public static List<QuestRecord> StartableInZone(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> states, uint territoryId)
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
            if (quest.Issuer is not { } issuer || issuer.TerritoryId != territoryId)
            {
                continue;
            }

            if (!states.TryGetValue(quest.RowId, out var evaluation) || evaluation.State is not (QuestState.Ready or QuestState.ReadyOnOtherJob))
            {
                continue;
            }

            matches.Add(quest);
        }

        matches.Sort(static (a, b) =>
        {
            var byLevel = a.Level.CompareTo(b.Level);
            return byLevel != 0 ? byLevel : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
        });
        return matches;
    }

    /// <summary>Quests whose issuer is the NPC with this id (ENpcResident row id, the game object's base id), in journal order.</summary>
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
            if (quest.Issuer is { } issuer && issuer.NpcId == npcId)
            {
                matches.Add(quest);
            }
        }

        return matches;
    }
}
