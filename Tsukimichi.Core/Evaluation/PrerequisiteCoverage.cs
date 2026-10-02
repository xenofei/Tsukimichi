using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Evaluation;

/// <summary>
/// Whether the catalog makes one quest wait for another: through <see cref="QuestCatalog.PrerequisitesOf"/> (previous
/// quests, accept conditions that name a quest and the curated extras), directly or through the prerequisites'
/// own. The cross-check against another tool's prerequisite links (<c>Tsukimichi.Verify questionable</c> and
/// <c>QuestionableLinksTests</c>) asks this for every link: a link the catalog already implies needs no entry.
/// </summary>
public static class PrerequisiteCoverage
{
    /// <summary>Why a link is not implied by the catalog.</summary>
    public enum Gap
    {
        /// <summary>The catalog requires it.</summary>
        None,

        /// <summary>The quest the link gates is no quest of the catalog.</summary>
        UnknownQuest,

        /// <summary>The quest the link asks for is no quest of the catalog.</summary>
        UnknownRequired,

        /// <summary>Both are catalog quests, and nothing makes the one wait for the other.</summary>
        NotRequired,
    }

    /// <summary>One link the catalog does not imply.</summary>
    public sealed record Uncovered(uint QuestRowId, uint RequiredRowId, Gap Gap);

    /// <summary>
    /// Whether <paramref name="rowId"/> cannot be taken before <paramref name="requiredRowId"/> is done: the id is one
    /// of its prerequisites, or of theirs. Under an Any join only an id every alternative needs counts (or one of
    /// <see cref="Prereq.Required"/>): an id one alternative needs can be gone round.
    /// </summary>
    public static bool Requires(QuestCatalog catalog, uint rowId, uint requiredRowId)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var memo = new Dictionary<uint, bool>();
        return Visit(catalog, rowId, requiredRowId, memo);
    }

    /// <summary>Every link of <paramref name="links"/> the catalog does not imply, in the order given.</summary>
    public static List<Uncovered> Check(QuestCatalog catalog, IEnumerable<(uint QuestRowId, uint RequiredRowId)> links)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(links);
        var gaps = new List<Uncovered>();
        foreach (var (quest, required) in links)
        {
            var gap = catalog.GetByRowId(quest) is null ? Gap.UnknownQuest
                : catalog.GetByRowId(required) is null ? Gap.UnknownRequired
                : Requires(catalog, quest, required) ? Gap.None
                : Gap.NotRequired;
            if (gap != Gap.None)
            {
                gaps.Add(new Uncovered(quest, required, gap));
            }
        }

        return gaps;
    }

    // Plain loops, no LINQ: the main scenario chain is hundreds of quests deep, and each level costs one frame.
    private static bool Visit(QuestCatalog catalog, uint rowId, uint requiredRowId, Dictionary<uint, bool> memo)
    {
        if (memo.TryGetValue(rowId, out var known))
        {
            return known;
        }

        // A cycle (none with today's data) reads as "not required" rather than looping.
        memo[rowId] = false;
        if (catalog.GetByRowId(rowId) is not { } quest)
        {
            return false;
        }

        var prerequisites = catalog.PrerequisitesOf(quest);
        var result = false;
        if (prerequisites.Join == JoinKind.Any)
        {
            var alternatives = 0;
            var allNeedIt = true;
            foreach (var id in prerequisites.QuestIds)
            {
                if (prerequisites.IsRequired(id))
                {
                    continue;
                }

                alternatives++;
                if (id != requiredRowId && !Visit(catalog, id, requiredRowId, memo))
                {
                    allNeedIt = false;
                    break;
                }
            }

            result = alternatives > 0 && allNeedIt;
            foreach (var id in prerequisites.Required)
            {
                if (result)
                {
                    break;
                }

                result = id == requiredRowId || Visit(catalog, id, requiredRowId, memo);
            }
        }
        else
        {
            foreach (var id in prerequisites.QuestIds)
            {
                if (id == requiredRowId || Visit(catalog, id, requiredRowId, memo))
                {
                    result = true;
                    break;
                }
            }
        }

        memo[rowId] = result;
        return result;
    }
}
