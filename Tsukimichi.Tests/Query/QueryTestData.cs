using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Tests.Query;

/// <summary>Small factories for query tests. Journal ids: section 1 or 2, category 10..13, genre 100..104.</summary>
internal static class QueryTestData
{
    public static QuestRecord Quest(
        uint rowId,
        string name,
        uint section = 1,
        uint category = 10,
        uint genre = 100,
        int sortKey = 0,
        byte level = 1,
        byte expansion = 0,
        uint classJobCategory = 0,
        bool repeatable = false,
        ushort festival = 0,
        string? internalId = null,
        params RewardRef[] rewards) => new()
    {
        RowId = rowId,
        QuestId = QuestRecord.ToQuestId(rowId),
        InternalId = internalId ?? $"Test_{rowId}",
        Name = name,
        Journal = new JournalRef(
            section, $"Section {section}",
            category, $"Category {category}",
            genre, $"Genre {genre}",
            sortKey == 0 ? (int)rowId : sortKey),
        Level = level,
        Expansion = expansion,
        ClassJobCategory = classJobCategory,
        IsRepeatable = repeatable,
        Festival = festival,
        Rewards = rewards,
    };

    public static RewardRef Reward(RewardKind kind, string name, uint id = 1) =>
        new(kind, id, kind == RewardKind.Item ? id : 0, 1, name, 0);

    public static Dictionary<uint, QuestState> States(QuestCatalog catalog, QuestState state)
    {
        var result = new Dictionary<uint, QuestState>(catalog.Count);
        foreach (var quest in catalog.All)
        {
            result[quest.RowId] = state;
        }

        return result;
    }

    public static Dictionary<uint, QuestState> States(params (uint RowId, QuestState State)[] entries)
    {
        var result = new Dictionary<uint, QuestState>(entries.Length);
        foreach (var (rowId, state) in entries)
        {
            result[rowId] = state;
        }

        return result;
    }

    /// <summary>One evaluation per state entry; a next-step detail, when given, rides on an unmet Level requirement.</summary>
    public static Dictionary<uint, QuestEvaluation> Evaluations(IReadOnlyDictionary<uint, QuestState> states, IReadOnlyDictionary<uint, string>? nextSteps = null)
    {
        var result = new Dictionary<uint, QuestEvaluation>(states.Count);
        foreach (var (rowId, state) in states)
        {
            RequirementResult? next = null;
            if (nextSteps is not null && nextSteps.TryGetValue(rowId, out var detail))
            {
                next = new RequirementResult(new LevelRequirement(50, 1), false, detail);
            }

            result[rowId] = new QuestEvaluation(state, next is null ? [] : [next], next, null, null);
        }

        return result;
    }

    public static QueryResult Run(
        QuestCatalog catalog,
        IReadOnlyDictionary<uint, QuestEvaluation> evaluations,
        FilterSet? filters = null,
        QuestScope? scope = null,
        SortSpec? sort = null,
        string search = "",
        QueryContext? ctx = null) =>
        QuestQuery.Apply(catalog, evaluations, filters ?? new FilterSet(), scope ?? QuestScope.None, sort ?? SortSpec.Default, search, ctx ?? QueryContext.Empty);

    public static QueryResult Run(
        QuestCatalog catalog,
        IReadOnlyDictionary<uint, QuestState> states,
        FilterSet? filters = null,
        QuestScope? scope = null,
        SortSpec? sort = null,
        string search = "",
        QueryContext? ctx = null) =>
        QuestQuery.Apply(catalog, states, filters ?? new FilterSet(), scope ?? QuestScope.None, sort ?? SortSpec.Default, search, ctx ?? QueryContext.Empty);

    public static uint[] RowIds(QueryResult result) => result.Rows.Select(r => r.Quest.RowId).ToArray();

    public static string[] Names(QueryResult result) => result.Rows.Select(r => r.Quest.Name).ToArray();
}
