using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Query;

/// <summary>
/// Produces the flat, filtered, sorted row array the table renders. Pure: no caching beyond the shared
/// <see cref="SearchIndex"/>; the caller runs it only when a dirty flag says so.
/// </summary>
public static class QuestQuery
{
    private static readonly QuestRow[] NoRows = [];

    /// <summary>Filters, ordered as the panel shows them; the order also fixes <see cref="EmptyReason.Filters"/>.</summary>
    private enum Filter
    {
        None,
        HideCompleted,
        AvailableOnly,
        State,
        Expansion,
        LevelRange,
        JobCategory,
        RewardKinds,
        Repeatable,
        SeasonalActive,
        IncludeUnlisted,
        Pinned,
        Search,
    }

    private static readonly (Filter Filter, string Name)[] Diagnosable =
    [
        (Filter.HideCompleted, FilterNames.HideCompleted),
        (Filter.AvailableOnly, FilterNames.AvailableOnly),
        (Filter.State, FilterNames.State),
        (Filter.Expansion, FilterNames.Expansion),
        (Filter.LevelRange, FilterNames.LevelRange),
        (Filter.JobCategory, FilterNames.JobCategory),
        (Filter.RewardKinds, FilterNames.RewardKinds),
        (Filter.Repeatable, FilterNames.Repeatable),
        (Filter.SeasonalActive, FilterNames.SeasonalActive),
        (Filter.IncludeUnlisted, FilterNames.IncludeUnlisted),
        (Filter.Pinned, FilterNames.Pinned),
        (Filter.Search, FilterNames.Search),
    ];

    /// <param name="states">Resolved state per quest row id; missing rows read as <see cref="QuestState.Unknown"/>.</param>
    /// <param name="search">Raw search text; normalized here.</param>
    public static QueryResult Apply(
        QuestCatalog catalog,
        IReadOnlyDictionary<uint, QuestState> states,
        FilterSet filters,
        QuestScope scope,
        SortSpec sort,
        string? search,
        QueryContext ctx)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(ctx);

        var candidates = Candidates(catalog, scope, ctx);
        if (candidates.Count == 0)
        {
            return new QueryResult(NoRows, EmptyReason.Scope, 0);
        }

        var query = SearchIndex.Normalize(search);
        var index = query.Length == 0 ? null : ctx.SearchIndex ?? SearchIndex.For(catalog);
        var plan = new Plan(filters, ctx, index, query, filters.IncludeUnlisted || scope.Kind == ScopeKind.VirtualUnlisted);

        var rows = new List<QuestRow>(candidates.Count);
        var totalInScope = 0;
        foreach (var quest in candidates)
        {
            if (quest.IsUnlisted && !plan.IncludeUnlisted)
            {
                continue;
            }

            totalInScope++;
            var state = states.GetValueOrDefault(quest.RowId, QuestState.Unknown);
            if (plan.Passes(quest, state, Filter.None))
            {
                var nextStep = ctx.NextStepText?.GetValueOrDefault(quest.RowId) ?? string.Empty;
                rows.Add(new QuestRow(quest, state, nextStep));
            }
        }

        if (rows.Count == 0)
        {
            return new QueryResult(NoRows, Diagnose(candidates, states, plan), totalInScope);
        }

        return new QueryResult(Sort(rows, sort), null, totalInScope);
    }

    private static IReadOnlyList<QuestRecord> Candidates(QuestCatalog catalog, QuestScope scope, QueryContext ctx)
    {
        switch (scope.Kind)
        {
            case ScopeKind.None:
                return catalog.All;
            case ScopeKind.Section:
                return catalog.BySection.GetValueOrDefault(scope.Id) ?? [];
            case ScopeKind.Category:
                return catalog.ByCategory.GetValueOrDefault(scope.Id) ?? [];
            case ScopeKind.Genre:
                return catalog.ByGenre.GetValueOrDefault(scope.Id) ?? [];
            case ScopeKind.VirtualUnlisted:
                return catalog.ByGenre.GetValueOrDefault(0u) ?? [];
            case ScopeKind.VirtualFeature:
            {
                if (ctx.FeatureQuestIds.Count == 0)
                {
                    return [];
                }

                // Walk the catalog rather than the id set so the result keeps journal order.
                var picked = new List<QuestRecord>(ctx.FeatureQuestIds.Count);
                foreach (var quest in catalog.All)
                {
                    if (ctx.FeatureQuestIds.Contains(quest.RowId))
                    {
                        picked.Add(quest);
                    }
                }

                return picked;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(scope), scope.Kind, "Unknown scope kind.");
        }
    }

    /// <summary>Names each engaged filter whose removal alone would restore at least one row. Runs only on empty results.</summary>
    private static EmptyReason Diagnose(IReadOnlyList<QuestRecord> candidates, IReadOnlyDictionary<uint, QuestState> states, Plan plan)
    {
        List<string>? blamed = null;
        foreach (var (filter, name) in Diagnosable)
        {
            if (!plan.IsEngaged(filter))
            {
                continue;
            }

            foreach (var quest in candidates)
            {
                if (quest.IsUnlisted && !plan.IncludeUnlisted && filter != Filter.IncludeUnlisted)
                {
                    continue;
                }

                var state = states.GetValueOrDefault(quest.RowId, QuestState.Unknown);
                if (plan.Passes(quest, state, filter))
                {
                    (blamed ??= []).Add(name);
                    break;
                }
            }
        }

        return blamed is null ? EmptyReason.Combination : new EmptyReason(blamed, false);
    }

    private static QuestRow[] Sort(List<QuestRow> rows, SortSpec sort)
    {
        var result = rows.ToArray();
        if (sort.Column == SortColumn.Journal)
        {
            // Candidates arrive in journal order, so nothing to sort; descending is a plain reversal.
            if (sort.Descending)
            {
                Array.Reverse(result);
            }

            return result;
        }

        // Sort an index array so ties fall back to the original position: a stable sort on top of Array.Sort.
        var order = new int[result.Length];
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }

        Array.Sort(order, new RowComparer(result, sort));

        var sorted = new QuestRow[result.Length];
        for (var i = 0; i < order.Length; i++)
        {
            sorted[i] = result[order[i]];
        }

        return sorted;
    }

    private sealed class RowComparer(QuestRow[] rows, SortSpec sort) : IComparer<int>
    {
        public int Compare(int x, int y)
        {
            if (x == y)
            {
                return 0;
            }

            ref readonly var a = ref rows[x];
            ref readonly var b = ref rows[y];
            var c = sort.Column switch
            {
                SortColumn.Name => string.Compare(a.Quest.Name, b.Quest.Name, StringComparison.OrdinalIgnoreCase),
                SortColumn.Level => a.Quest.Level.CompareTo(b.Quest.Level),
                SortColumn.State => ((int)a.State).CompareTo((int)b.State),
                SortColumn.Expansion => a.Quest.Expansion.CompareTo(b.Quest.Expansion),
                _ => 0,
            };

            if (c == 0)
            {
                return x.CompareTo(y);
            }

            return sort.Descending ? -c : c;
        }
    }

    /// <summary>Filter inputs resolved once per query so the per-row check touches no dictionaries it can avoid.</summary>
    private readonly struct Plan
    {
        private readonly FilterSet filters;
        private readonly QueryContext ctx;
        private readonly SearchIndex? index;
        private readonly string query;
        private readonly uint hiddenRewardMask;
        private readonly uint onlyRewardMask;
        private readonly bool hideCompletedEngaged;
        private readonly bool availableOnlyEngaged;
        private readonly bool levelRangeEngaged;

        public Plan(FilterSet filters, QueryContext ctx, SearchIndex? index, string query, bool includeUnlisted)
        {
            this.filters = filters;
            this.ctx = ctx;
            this.index = index;
            this.query = query;
            IncludeUnlisted = includeUnlisted;
            hideCompletedEngaged = filters.HideCompletedEngaged;
            availableOnlyEngaged = filters.AvailableOnlyEngaged;
            levelRangeEngaged = filters.LevelRangeEngaged;

            foreach (var (kind, state) in filters.RewardKinds)
            {
                var bit = 1u << (int)kind;
                switch (state)
                {
                    case TriState.Hidden:
                        hiddenRewardMask |= bit;
                        break;
                    case TriState.Only:
                        onlyRewardMask |= bit;
                        break;
                }
            }
        }

        public bool IncludeUnlisted { get; }

        public bool IsEngaged(Filter filter) => filter switch
        {
            Filter.HideCompleted => hideCompletedEngaged,
            Filter.AvailableOnly => availableOnlyEngaged,
            Filter.State => filters.StateMask != QuestStateMask.All,
            Filter.Expansion => filters.Expansions.Count > 0,
            Filter.LevelRange => levelRangeEngaged,
            Filter.JobCategory => filters.ClassJobCategoryId is not null,
            Filter.RewardKinds => (hiddenRewardMask | onlyRewardMask) != 0,
            Filter.Repeatable => filters.RepeatableOnly,
            Filter.SeasonalActive => filters.SeasonalActiveOnly,
            Filter.IncludeUnlisted => !IncludeUnlisted,
            Filter.Pinned => filters.PinnedOnly,
            Filter.Search => query.Length > 0,
            _ => false,
        };

        /// <summary>Every filter except <paramref name="skip"/>; the Unlisted rule is applied by the caller.</summary>
        public bool Passes(QuestRecord quest, QuestState state, Filter skip)
        {
            var categoryId = quest.Journal.CategoryId;

            if (skip != Filter.HideCompleted && hideCompletedEngaged
                && state is QuestState.Completed or QuestState.Foreclosed
                && filters.HideCompletedFor(categoryId))
            {
                return false;
            }

            if (skip != Filter.AvailableOnly && availableOnlyEngaged
                && state is not (QuestState.Ready or QuestState.ReadyOnOtherJob or QuestState.Accepted)
                && filters.AvailableOnlyFor(categoryId))
            {
                return false;
            }

            if (skip != Filter.State && !filters.StateMask.Contains(state))
            {
                return false;
            }

            if (skip != Filter.Expansion && filters.Expansions.Count > 0 && !filters.Expansions.Contains(quest.Expansion))
            {
                return false;
            }

            if (skip != Filter.LevelRange && levelRangeEngaged
                && (quest.Level < filters.LevelMin || quest.Level > filters.LevelMax))
            {
                return false;
            }

            if (skip != Filter.JobCategory && filters.ClassJobCategoryId is { } job
                && quest.ClassJobCategory != job && quest.ClassJobCategory1 != job)
            {
                return false;
            }

            if (skip != Filter.RewardKinds && !PassesRewardKinds(quest))
            {
                return false;
            }

            if (skip != Filter.Repeatable && filters.RepeatableOnly && !quest.IsRepeatable)
            {
                return false;
            }

            if (skip != Filter.SeasonalActive && filters.SeasonalActiveOnly
                && (quest.Festival == 0 || !ctx.ActiveFestivals.Contains(quest.Festival)))
            {
                return false;
            }

            if (skip != Filter.Pinned && filters.PinnedOnly && !ctx.Pinned.Contains(quest.RowId))
            {
                return false;
            }

            if (skip != Filter.Search && index is not null && !index.Matches(quest.RowId, query))
            {
                return false;
            }

            return true;
        }

        private bool PassesRewardKinds(QuestRecord quest)
        {
            if ((hiddenRewardMask | onlyRewardMask) == 0)
            {
                return true;
            }

            var hasOnly = false;
            var rewards = quest.Rewards;
            for (var i = 0; i < rewards.Count; i++)
            {
                var bit = 1u << (int)rewards[i].Kind;
                if ((hiddenRewardMask & bit) != 0)
                {
                    return false;
                }

                if ((onlyRewardMask & bit) != 0)
                {
                    hasOnly = true;
                }
            }

            return onlyRewardMask == 0 || hasOnly;
        }
    }
}
