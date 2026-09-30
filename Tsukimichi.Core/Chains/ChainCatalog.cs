using System.Collections.Frozen;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Chains;

/// <summary>A named, ordered run of quests: a side story, a relic line, a raid tier's story.</summary>
/// <param name="Name">Display name: the curated name, the journal genre's name for a derived chain, or "Story: &lt;first quest&gt;" for a side story.</param>
/// <param name="RowIds">Quest sheet row ids in play order.</param>
public sealed record Chain(string Name, IReadOnlyList<uint> RowIds)
{
    /// <summary>
    /// A side story derived by <see cref="StorySidequests"/>. Its name holds its first quest's sheet name; wherever a
    /// name prints, use <see cref="ChainCatalog.Title"/> or <see cref="ChainCatalog.DisplayName"/> so the name goes
    /// through the caller's spoiler shield.
    /// </summary>
    public bool IsStory { get; init; }
}

/// <summary>Where a character stands in a chain.</summary>
/// <param name="Done">Completed quests.</param>
/// <param name="Total">Quests in the chain.</param>
/// <param name="NextRowId">The first quest in chain order that is not completed; null once the chain is finished.</param>
public readonly record struct ChainProgress(int Done, int Total, uint? NextRowId)
{
    public bool IsComplete => Done >= Total;

    public float Fraction => Total == 0 ? 0f : (float)Done / Total;
}

/// <summary>
/// Every chain a catalog holds, built once per catalog. Three sources: curated chains from <c>curated/chains.json</c>
/// (named lists of journal genres concatenated in order), derived chains, one per journal genre whose quests form a
/// single previous-quest line (each quest after the first requires exactly the quest before it), and, when given,
/// the side stories of <see cref="StorySidequests"/>. A quest belongs to at most one chain; curated chains win, then
/// the first derived chain that lists it, then its side story.
/// </summary>
public sealed class ChainCatalog
{
    /// <summary>A derived chain needs at least this many quests; a lone quest is not a story.</summary>
    public const int MinChainLength = 2;

    public static readonly ChainCatalog Empty = new([], FrozenDictionary<uint, Chain>.Empty, []);

    private readonly FrozenDictionary<uint, Chain> byRowId;

    private ChainCatalog(IReadOnlyList<Chain> chains, FrozenDictionary<uint, Chain> byRowId, IReadOnlyList<string> warnings)
    {
        Chains = chains;
        this.byRowId = byRowId;
        Warnings = warnings;
    }

    /// <summary>Curated chains first in file order, then derived chains in journal order.</summary>
    public IReadOnlyList<Chain> Chains { get; }

    /// <summary>One line per curated entry that named a genre the catalog does not have, for the caller to log once.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>The chain a quest belongs to, or null.</summary>
    public Chain? ForQuest(uint rowId) => byRowId.GetValueOrDefault(rowId);

    /// <param name="stories">The side stories to add after the genre chains; null (or <see cref="StorySidequests.Empty"/>) adds none.</param>
    public static ChainCatalog Build(QuestCatalog catalog, CuratedData curated, StorySidequests? stories = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(curated);

        var chains = new List<Chain>();
        var byRowId = new Dictionary<uint, Chain>();
        var warnings = new List<string>();
        var claimedGenres = new HashSet<uint>();

        foreach (var entry in curated.Chains)
        {
            var rowIds = new List<uint>();
            foreach (var genreId in entry.GenreIds)
            {
                if (genreId == 0 || !catalog.ByGenre.TryGetValue(genreId, out var quests))
                {
                    warnings.Add($"chain \"{entry.Name}\": genre {genreId} is not in the catalog; skipped.");
                    continue;
                }

                // A curated genre need not be linear (branching side stories are still one story); its quests keep
                // journal order either way, which is the play order for every linear genre. A retired row the genre
                // still carries (two Crystal Tower quests the 6.3 rewrite removed) is no longer a step of the story.
                claimedGenres.Add(genreId);
                foreach (var quest in quests)
                {
                    if (!quest.IsRetired)
                    {
                        rowIds.Add(quest.RowId);
                    }
                }
            }

            if (rowIds.Count == 0)
            {
                continue;
            }

            Add(chains, byRowId, new Chain(entry.Name, rowIds.ToArray()));
        }

        var derived = new List<IReadOnlyList<QuestRecord>>();
        var nameCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (genreId, all) in catalog.ByGenre.OrderBy(kv => kv.Value[0].Journal.SortKey))
        {
            if (genreId == 0 || claimedGenres.Contains(genreId))
            {
                continue;
            }

            var quests = Live(all);
            if (!IsLinear(quests))
            {
                continue;
            }

            derived.Add(quests);
            var genreName = quests[0].Journal.GenreName;
            nameCounts[genreName] = nameCounts.GetValueOrDefault(genreName) + 1;
        }

        foreach (var quests in derived)
        {
            var rowIds = new uint[quests.Count];
            for (var i = 0; i < rowIds.Length; i++)
            {
                rowIds[i] = quests[i].RowId;
            }

            Add(chains, byRowId, new Chain(DerivedName(quests[0].Journal, nameCounts), rowIds));
        }

        if (stories is not null)
        {
            // A side story wholly inside a curated or genre chain (a stretch of Hildibrand) adds nothing.
            foreach (var story in stories.Chains)
            {
                if (story.RowIds.Any(id => !byRowId.ContainsKey(id)))
                {
                    Add(chains, byRowId, story);
                }
            }
        }

        return new ChainCatalog(chains, byRowId.ToFrozenDictionary(), warnings);
    }

    /// <summary>The genre's quests without its retired rows; the same list when it has none.</summary>
    private static IReadOnlyList<QuestRecord> Live(IReadOnlyList<QuestRecord> quests)
    {
        List<QuestRecord>? live = null;
        for (var i = 0; i < quests.Count; i++)
        {
            if (quests[i].IsRetired)
            {
                live ??= [.. quests.Take(i)];
            }
            else
            {
                live?.Add(quests[i]);
            }
        }

        return live ?? quests;
    }

    /// <summary>
    /// True when <paramref name="quests"/>, in the order given, form one line: at least <see cref="MinChainLength"/>
    /// quests, and every quest after the first names exactly one previous quest, the one just before it. A single-id
    /// Any join counts (the sheet stores lone prerequisites with either join).
    /// </summary>
    public static bool IsLinear(IReadOnlyList<QuestRecord> quests)
    {
        ArgumentNullException.ThrowIfNull(quests);
        if (quests.Count < MinChainLength)
        {
            return false;
        }

        for (var i = 1; i < quests.Count; i++)
        {
            var previous = quests[i].PreviousQuests.QuestIds;
            if (previous.Length != 1 || previous[0] != quests[i - 1].RowId)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Counts completed quests and finds the first one still to do; a quest without an evaluation is not done.</summary>
    public static ChainProgress Progress(Chain chain, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(states);

        var done = 0;
        uint? next = null;
        foreach (var rowId in chain.RowIds)
        {
            if (states.TryGetValue(rowId, out var evaluation) && evaluation.State == QuestState.Completed)
            {
                done++;
            }
            else
            {
                next ??= rowId;
            }
        }

        return new ChainProgress(done, chain.RowIds.Count, next);
    }

    /// <summary>
    /// What a chain is called in a sentence: a side story by its first quest's name as <paramref name="questName"/>
    /// prints it (the spoiler shield's name), any other chain by its name. "Part of a side story: {title}".
    /// </summary>
    public static string Title(Chain chain, Func<uint, string> questName)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(questName);
        return chain.IsStory && chain.RowIds.Count > 0 ? questName(chain.RowIds[0]) : chain.Name;
    }

    /// <summary>A chain's label: "Story: " and the shielded first quest name for a side story, the name for any other chain.</summary>
    public static string DisplayName(Chain chain, Func<uint, string> questName)
    {
        ArgumentNullException.ThrowIfNull(chain);
        return chain.IsStory ? StorySidequests.StoryName(Title(chain, questName)) : chain.Name;
    }

    /// <summary>Zero-based place of a quest in a chain's play order; -1 when the chain does not list it.</summary>
    public static int IndexOf(Chain chain, uint rowId)
    {
        ArgumentNullException.ThrowIfNull(chain);
        for (var i = 0; i < chain.RowIds.Count; i++)
        {
            if (chain.RowIds[i] == rowId)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// The genre name, qualified by the category when several derived chains share it (every allied society has a
    /// "Main Quests" genre), so the widget never says just "Main Quests".
    /// </summary>
    private static string DerivedName(JournalRef journal, Dictionary<string, int> nameCounts) =>
        nameCounts.GetValueOrDefault(journal.GenreName) > 1 && journal.CategoryName.Length > 0
            ? $"{journal.GenreName} ({journal.CategoryName})"
            : journal.GenreName;

    private static void Add(List<Chain> chains, Dictionary<uint, Chain> byRowId, Chain chain)
    {
        chains.Add(chain);
        foreach (var rowId in chain.RowIds)
        {
            byRowId.TryAdd(rowId, chain);
        }
    }
}
