using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Query;

/// <summary>
/// Produces the flat, filtered, sorted row array the table renders. Pure: no caching beyond the shared
/// <see cref="SearchIndex"/>; the caller runs it only when a dirty flag says so.
/// <para>
/// Removed quests (<see cref="QuestRecord.IsRemoved"/>: retired rows and quests with no journal genre) are shown only
/// under <see cref="QuestScope.None"/> and <see cref="QuestScope.VirtualFeature"/> when
/// <see cref="FilterSet.IncludeUnlisted"/> is on, and always under <see cref="QuestScope.VirtualUnlisted"/>. A
/// section, category or genre node never shows them, whatever ids the sheet gave them, because section 0 is a real
/// journal section and a retired listed row still carries its old genre. <see cref="QuestScope.Issuer"/> (the NPC
/// context menu's scope) lists what the NPC hands out today: its candidates carry no retired quest, and it always
/// shows the unlisted live ones among them (every genre-0 quest under <see cref="JournalFiling.Legacy"/>), so the
/// scope matches the menu's count and <c>/tsuki which</c> under either filing.
/// </para>
/// <para>
/// Quests on a path the character did not take (<see cref="QuestEvaluation.IsOtherPath"/>) are listed under
/// <see cref="QuestScope.VirtualOtherPaths"/>, grouped by the kind of path (city, class, Grand Company, other
/// choices) in journal order within each, and elsewhere only when <see cref="FilterSet.IncludeOtherPaths"/> is on.
/// </para>
/// </summary>
public static class QuestQuery
{
    /// <summary>Half-width of the Around-my-level preset: quest level within the current level ± this.</summary>
    public const int LevelBandRadius = 5;

    private static readonly QuestRow[] NoRows = [];

    /// <summary>Filters, ordered as the panel shows them; the order also fixes <see cref="EmptyReason.Filters"/>.</summary>
    private enum Filter
    {
        None,
        Preset,
        HideCompleted,
        AvailableOnly,
        State,
        Expansion,
        AddedIn,
        LevelRange,
        JobCategory,
        RewardKinds,
        Repeatable,
        SeasonalActive,
        IncludeUnlisted,
        IncludeOtherPaths,
        Pinned,
        Abandoned,
        Search,
    }

    /// <summary>The preset's name depends on which one is active, so its entry here is resolved at diagnosis time.</summary>
    private static readonly (Filter Filter, string Name)[] Diagnosable =
    [
        (Filter.Preset, string.Empty),
        (Filter.HideCompleted, FilterNames.HideCompleted),
        (Filter.AvailableOnly, FilterNames.AvailableOnly),
        (Filter.State, FilterNames.State),
        (Filter.Expansion, FilterNames.Expansion),
        (Filter.AddedIn, FilterNames.AddedIn),
        (Filter.LevelRange, FilterNames.LevelRange),
        (Filter.JobCategory, FilterNames.JobCategory),
        (Filter.RewardKinds, FilterNames.RewardKinds),
        (Filter.Repeatable, FilterNames.Repeatable),
        (Filter.SeasonalActive, FilterNames.SeasonalActive),
        (Filter.IncludeUnlisted, FilterNames.IncludeUnlisted),
        (Filter.IncludeOtherPaths, FilterNames.IncludeOtherPaths),
        (Filter.Pinned, FilterNames.Pinned),
        (Filter.Abandoned, FilterNames.Abandoned),
        (Filter.Search, FilterNames.Search),
    ];

    /// <summary>
    /// Runs the query over evaluator output; each row's state comes from its <see cref="QuestEvaluation"/> and its
    /// Status text from <see cref="BlockerText.StatusText"/> with <see cref="QueryContext.Names"/>.
    /// </summary>
    /// <param name="evaluations">Resolved evaluation per quest row id; missing rows read as <see cref="QuestState.Unknown"/> with no status text.</param>
    /// <param name="search">Raw search text; normalized here.</param>
    public static QueryResult Apply(
        QuestCatalog catalog,
        IReadOnlyDictionary<uint, QuestEvaluation> evaluations,
        FilterSet filters,
        QuestScope scope,
        SortSpec sort,
        string? search,
        QueryContext ctx)
    {
        ArgumentNullException.ThrowIfNull(evaluations);
        ArgumentNullException.ThrowIfNull(ctx);
        return Apply(catalog, new EvaluationSource(evaluations, ctx.Names ?? BlockerNames.Default), filters, scope, sort, search, ctx);
    }

    /// <summary>Runs the query over a plain state map; status text comes from <see cref="QueryContext.NextStepText"/> when set.</summary>
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
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(ctx);
#pragma warning disable CS0618 // the legacy next-step map is honoured for callers that still fill it
        var source = new StateMapSource(states, ctx.NextStepText);
#pragma warning restore CS0618
        return Apply(catalog, source, filters, scope, sort, search, ctx);
    }

    private static QueryResult Apply<TSource>(
        QuestCatalog catalog,
        TSource source,
        FilterSet filters,
        QuestScope scope,
        SortSpec sort,
        string? search,
        QueryContext ctx)
        where TSource : struct, IStateSource
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(ctx);

        var candidates = scope.Kind == ScopeKind.VirtualOtherPaths ? OtherPathCandidates(catalog, source) : Candidates(catalog, scope, ctx);
        if (candidates.Count == 0)
        {
            return new QueryResult(NoRows, EmptyReason.Scope, 0);
        }

        var query = SearchIndex.Normalize(search);
        var index = query.Length == 0 ? null : ctx.SearchIndex ?? SearchIndex.For(catalog);
        var plan = new Plan(filters, ctx, index, query, scope);

        var rows = new List<QuestRow>(candidates.Count);
        var totalInScope = 0;
        var otherPathsHidden = false;
        foreach (var quest in candidates)
        {
            if (quest.IsRemoved && !plan.IncludeUnlisted)
            {
                continue;
            }

            if (!plan.IncludeOtherPaths && source.OtherPathKind(quest.RowId) is not null)
            {
                otherPathsHidden = true;
                continue;
            }

            totalInScope++;
            var state = source.StateOf(quest.RowId);
            if (plan.Passes(quest, state, Filter.None))
            {
                rows.Add(new QuestRow(quest, state, source.StatusOf(quest)));
            }
        }

        if (rows.Count == 0)
        {
            // A journal node holding only removed quests has nothing a filter could bring back; one holding quests on
            // another path has "Include other paths".
            var reason = totalInScope == 0 && !otherPathsHidden && !plan.UnlistedToggleable
                ? EmptyReason.Scope
                : Diagnose(candidates, source, plan);
            return new QueryResult(NoRows, reason, totalInScope);
        }

        var sorted = filters.Preset == Preset.StorySidequests && sort.Column == SortColumn.Journal && ctx.Stories is { } stories
            ? StoryOrder(rows, stories, sort.Descending)
            : Sort(rows, sort, ctx.Spoilers);
        if (sort.AvailableFirst)
        {
            sorted = Partition(sorted, static row => IsAvailable(row.State));
        }

        if (sort.PinnedFirst && ctx.Pinned.Count > 0)
        {
            var pinned = ctx.Pinned;
            sorted = Partition(sorted, row => pinned.Contains(row.Quest.RowId));
        }

        var newCount = 0;
        if (sort.NewThisPatchFirst)
        {
            // The Unlocks quick view's first group (P8): its quests from the newest patch series in the data.
            var patches = PatchIndex.For(catalog);
            sorted = Partition(sorted, row => patches.IsNew(row.Quest));
            while (newCount < sorted.Length && patches.IsNew(sorted[newCount].Quest))
            {
                newCount++;
            }
        }

        return new QueryResult(sorted, null, totalInScope, newCount);
    }

    /// <summary>
    /// For a reveal (a jump from the detail pane, a "Show in Journal"): turns off each filter that keeps
    /// <paramref name="quest"/> out of the table whatever its state, the ones a reveal does not clear already (the
    /// expansions, the patch, the level range, the job category, the reward kinds, Repeatable only and Seasonal
    /// active only); a filter the quest passes stays as it is. Returns whether any changed.
    /// </summary>
    /// <param name="activeFestivals">The festivals running (<see cref="QueryContext.ActiveFestivals"/>); null reads as none.</param>
    /// <param name="newSinceData">The quests newer than the data (<see cref="QueryContext.NewSinceData"/>), for "New since data"; null reads as none.</param>
    public static bool ClearFiltersHiding(QuestRecord quest, FilterSet filters, IReadOnlySet<ushort>? activeFestivals, IReadOnlySet<uint>? newSinceData = null)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(filters);
        var changed = false;
        if (filters.Expansions.Count > 0 && !filters.Expansions.Contains(quest.Expansion))
        {
            filters.Expansions.Clear();
            changed = true;
        }

        if (filters.AddedInEngaged() && !PassesAddedIn(quest, filters.AddedInNewSinceData() ? FilterSet.NewSinceData : PatchVersion.Normalize(filters.AddedIn), newSinceData))
        {
            filters.AddedIn = string.Empty;
            changed = true;
        }

        if (filters.LevelRangeEngaged() && (quest.DisplayLevel < filters.LevelMin || quest.DisplayLevel > filters.LevelMax))
        {
            filters.LevelMin = FilterSet.NoLevelMin;
            filters.LevelMax = FilterSet.NoLevelMax;
            changed = true;
        }

        if (filters.ClassJobCategoryId is { } job && quest.ClassJobCategory != job && quest.ClassJobCategory1 != job)
        {
            filters.ClassJobCategoryId = null;
            changed = true;
        }

        if (filters.RewardKindsEngaged() && !new Plan(filters, QueryContext.Empty, null, string.Empty, QuestScope.None).PassesRewardKinds(quest))
        {
            filters.RewardKinds.Clear();
            changed = true;
        }

        if (filters.RepeatableOnly && !quest.IsRepeatable)
        {
            filters.RepeatableOnly = false;
            changed = true;
        }

        if (filters.SeasonalActiveOnly && (quest.Festival == 0 || activeFestivals?.Contains(quest.Festival) != true))
        {
            filters.SeasonalActiveOnly = false;
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Whether <paramref name="search"/> keeps <paramref name="quest"/> out of the table: the quest's name, as the
    /// spoiler shield prints it, and its other searchable words do not match, nor does its journal text (<see
    /// cref="QueryContext.JournalHits"/>, when that search is on). False for an empty search; true for any other when
    /// <paramref name="ctx"/> has no search index to ask.
    /// </summary>
    public static bool SearchHides(QuestRecord quest, string? search, QueryContext? ctx)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var query = SearchIndex.Normalize(search);
        if (query.Length == 0)
        {
            return false;
        }

        return ctx?.SearchIndex is not { } index
            || (!index.Matches(quest.RowId, query, ctx.Spoilers) && ctx.JournalHits?.Contains(quest.RowId) != true);
    }

    /// <summary>
    /// The Added in filter for an engaged value: <see cref="FilterSet.NewSinceData"/> keeps the quests in
    /// <paramref name="newSinceData"/>; a series keeps the quests added in it.
    /// </summary>
    private static bool PassesAddedIn(QuestRecord quest, string addedIn, IReadOnlySet<uint>? newSinceData) =>
        string.Equals(addedIn, FilterSet.NewSinceData, StringComparison.Ordinal)
            ? newSinceData?.Contains(quest.RowId) == true
            : PatchVersion.InSeries(quest.AddedIn, addedIn);

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
                return catalog.Removed;
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
            case ScopeKind.VirtualIssuer:
                // Journal order, retired quests left out: an NPC that lost a quest in a patch never lists it.
                return Discovery.QuestDiscovery.IssuedBy(catalog, scope.Id);
            default:
                throw new ArgumentOutOfRangeException(nameof(scope), scope.Kind, "Unknown scope kind.");
        }
    }

    /// <summary>
    /// The Other paths node's quests: every listed quest on a path the character did not take, grouped by the kind of
    /// path in <see cref="PathKind"/> order, journal order within each kind.
    /// </summary>
    private static List<QuestRecord> OtherPathCandidates<TSource>(QuestCatalog catalog, TSource source)
        where TSource : struct, IStateSource
    {
        var picked = new List<(PathKind Kind, int Order, QuestRecord Quest)>();
        for (var i = 0; i < catalog.All.Count; i++)
        {
            var quest = catalog.All[i];
            if (!quest.IsRemoved && source.OtherPathKind(quest.RowId) is { } kind)
            {
                picked.Add((kind, i, quest));
            }
        }

        picked.Sort(static (a, b) => a.Kind != b.Kind ? a.Kind.CompareTo(b.Kind) : a.Order.CompareTo(b.Order));
        return picked.ConvertAll(static p => p.Quest);
    }

    /// <summary>
    /// Names each engaged filter whose removal alone would restore at least one row. Runs only on empty results. Quests
    /// on another path count only for <see cref="FilterNames.IncludeOtherPaths"/>: when they are the only hits for the
    /// search or the node, the reason says so and the UI offers to include them.
    /// </summary>
    private static EmptyReason Diagnose<TSource>(IReadOnlyList<QuestRecord> candidates, TSource source, Plan plan)
        where TSource : struct, IStateSource
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
                if ((quest.IsRemoved && !plan.IncludeUnlisted && filter != Filter.IncludeUnlisted)
                    || (!plan.IncludeOtherPaths && filter != Filter.IncludeOtherPaths && source.OtherPathKind(quest.RowId) is not null))
                {
                    continue;
                }

                if (plan.Passes(quest, source.StateOf(quest.RowId), filter))
                {
                    (blamed ??= []).Add(filter == Filter.Preset ? FilterNames.PresetName(plan.Preset) : name);
                    break;
                }
            }
        }

        return blamed is null ? EmptyReason.Combination : new EmptyReason(blamed, false);
    }

    private static QuestRow[] Sort(List<QuestRow> rows, SortSpec sort, SpoilerMask? spoilers)
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

        Array.Sort(order, new RowComparer(result, sort, spoilers ?? SpoilerMask.None));

        var sorted = new QuestRow[result.Length];
        for (var i = 0; i < order.Length; i++)
        {
            sorted[i] = result[order[i]];
        }

        return sorted;
    }

    /// <summary>
    /// The Story sidequests preset under the journal sort: reading order (<see cref="StorySidequests.OrderOf"/>), so a
    /// side story reads top to bottom in play order where the journal would list its two lines apart.
    /// </summary>
    private static QuestRow[] StoryOrder(List<QuestRow> rows, StorySidequests stories, bool descending)
    {
        var result = rows.ToArray();
        var keys = new int[result.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            keys[i] = stories.OrderOf(result[i].Quest.RowId);
        }

        // Every row passed the preset, so every key is distinct: an unstable sort is still deterministic.
        Array.Sort(keys, result);
        if (descending)
        {
            Array.Reverse(result);
        }

        return result;
    }

    private static bool IsAvailable(QuestState state) => state is QuestState.Ready or QuestState.ReadyOnOtherJob or QuestState.Accepted;

    /// <summary>Stable partition: rows matching <paramref name="first"/> lead, then the rest, each group in the order <paramref name="rows"/> had. Returns the input when nothing moves.</summary>
    private static QuestRow[] Partition(QuestRow[] rows, Func<QuestRow, bool> first)
    {
        var leadCount = 0;
        foreach (ref readonly var row in rows.AsSpan())
        {
            if (first(row))
            {
                leadCount++;
            }
        }

        if (leadCount == 0 || leadCount == rows.Length)
        {
            return rows;
        }

        var result = new QuestRow[rows.Length];
        var nextLead = 0;
        var nextOther = leadCount;
        foreach (ref readonly var row in rows.AsSpan())
        {
            if (first(row))
            {
                result[nextLead++] = row;
            }
            else
            {
                result[nextOther++] = row;
            }
        }

        return result;
    }

    /// <summary>Orders rows by the sort column; names compare as printed, so a masked quest sorts by its placeholder (by level among placeholders).</summary>
    private sealed class RowComparer(QuestRow[] rows, SortSpec sort, SpoilerMask spoilers) : IComparer<int>
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
                SortColumn.Name => spoilers.CompareDisplayNames(a.Quest, b.Quest),
                SortColumn.Level => a.Quest.DisplayLevel.CompareTo(b.Quest.DisplayLevel),
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
        private readonly string addedIn;
        private readonly int bandMin;
        private readonly int bandMax;
        private readonly DateTime stalledBeforeUtc;
        private readonly byte reachExpansion;
        public Plan(FilterSet filters, QueryContext ctx, SearchIndex? index, string query, QuestScope scope)
        {
            this.filters = filters;
            this.ctx = ctx;
            this.index = index;
            this.query = query;
            Preset = filters.Preset;
            bandMin = ctx.CurrentLevel - LevelBandRadius;
            bandMax = ctx.CurrentLevel + LevelBandRadius;
            var days = Math.Max(0, ctx.StalledDays);
            stalledBeforeUtc = ctx.NowUtc > DateTime.MinValue + TimeSpan.FromDays(days) ? ctx.NowUtc - TimeSpan.FromDays(days) : DateTime.MinValue;
            reachExpansion = ctx.Spoilers?.ReachExpansion ?? byte.MaxValue;
            UnlistedToggleable = scope.Kind is ScopeKind.None or ScopeKind.VirtualFeature;
            IncludeOtherPaths = scope.Kind == ScopeKind.VirtualOtherPaths || filters.IncludeOtherPaths;
            IncludeUnlisted = scope.Kind switch
            {
                // The issuer's candidates are never retired, so "removed" there can only mean an unlisted live quest
                // the NPC still hands out.
                ScopeKind.VirtualUnlisted or ScopeKind.VirtualIssuer => true,
                ScopeKind.Section or ScopeKind.Category or ScopeKind.Genre => false,
                _ => filters.IncludeUnlisted,
            };
            hideCompletedEngaged = filters.HideCompletedEngaged();
            availableOnlyEngaged = filters.AvailableOnlyEngaged();
            levelRangeEngaged = filters.LevelRangeEngaged();
            // Normalized once, so a hand-edited "7.50" still reads as the 7.5 series.
            addedIn = !filters.AddedInEngaged() ? string.Empty
                : filters.AddedInNewSinceData() ? FilterSet.NewSinceData
                : PatchVersion.Normalize(filters.AddedIn);

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

        /// <summary>Whether removed quests pass under this scope.</summary>
        public bool IncludeUnlisted { get; }

        /// <summary>Whether quests on another path pass under this scope: always under the Other paths node, elsewhere when <see cref="FilterSet.IncludeOtherPaths"/> is on.</summary>
        public bool IncludeOtherPaths { get; }

        /// <summary>Whether <see cref="FilterSet.IncludeUnlisted"/> has any say under this scope (it never does under a journal node).</summary>
        public bool UnlistedToggleable { get; }

        public Preset Preset { get; }

        public bool IsEngaged(Filter filter) => filter switch
        {
            Filter.Preset => Preset != Preset.None,
            Filter.HideCompleted => hideCompletedEngaged,
            Filter.AvailableOnly => availableOnlyEngaged,
            Filter.State => filters.StateMask != QuestStateMask.All,
            Filter.Expansion => filters.Expansions.Count > 0,
            Filter.AddedIn => addedIn.Length > 0,
            Filter.LevelRange => levelRangeEngaged,
            Filter.JobCategory => filters.ClassJobCategoryId is not null,
            Filter.RewardKinds => (hiddenRewardMask | onlyRewardMask) != 0,
            Filter.Repeatable => filters.RepeatableOnly,
            Filter.SeasonalActive => filters.SeasonalActiveOnly,
            Filter.IncludeUnlisted => UnlistedToggleable && !IncludeUnlisted,
            Filter.IncludeOtherPaths => !IncludeOtherPaths,
            Filter.Pinned => filters.PinnedOnly,
            Filter.Abandoned => filters.AbandonedOnly,
            Filter.Search => query.Length > 0,
            _ => false,
        };

        /// <summary>Every filter except <paramref name="skip"/>; the Unlisted rule is applied by the caller.</summary>
        public bool Passes(QuestRecord quest, QuestState state, Filter skip)
        {
            var categoryId = quest.Journal.CategoryId;

            if (skip != Filter.Preset && !PassesPreset(quest, state))
            {
                return false;
            }

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

            if (skip != Filter.AddedIn && addedIn.Length > 0 && !PassesAddedIn(quest, addedIn, ctx.NewSinceData))
            {
                return false;
            }

            if (skip != Filter.LevelRange && levelRangeEngaged
                && (quest.DisplayLevel < filters.LevelMin || quest.DisplayLevel > filters.LevelMax))
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

            if (skip != Filter.Abandoned && filters.AbandonedOnly
                && (ctx.Abandoned is not { } abandoned || !abandoned.Contains(quest.QuestId)))
            {
                return false;
            }

            if (skip != Filter.Search && index is not null && !index.Matches(quest.RowId, query, ctx.Spoilers)
                && ctx.JournalHits?.Contains(quest.RowId) != true)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Feature quests: membership in the derived set. Around my level: quest level within
        /// <see cref="LevelBandRadius"/> of the current level (nothing when the level is unknown). Stalled: in the
        /// journal, with a known accepted time at least <see cref="QueryContext.StalledDays"/> days before now. Sprout
        /// mode: the quest's expansion at or below the one the character's main scenario has reached. Story sidequests:
        /// membership in <see cref="QueryContext.Stories"/>.
        /// </summary>
        private bool PassesPreset(QuestRecord quest, QuestState state) => Preset switch
        {
            Preset.None => true,
            Preset.FeatureQuests => ctx.FeatureQuestIds.Contains(quest.RowId),
            Preset.LevelBand => ctx.CurrentLevel > 0 && quest.DisplayLevel >= bandMin && quest.DisplayLevel <= bandMax,
            Preset.Stalled => state == QuestState.Accepted
                && ctx.AcceptedSince is { } since
                && since.TryGetValue(quest.QuestId, out var acceptedUtc)
                && acceptedUtc <= stalledBeforeUtc,
            Preset.Sprout => quest.Expansion <= reachExpansion,
            Preset.StorySidequests => ctx.Stories is { } stories && stories.Contains(quest.RowId),
            _ => true,
        };

        public bool PassesRewardKinds(QuestRecord quest)
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
