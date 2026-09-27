namespace Tsukimichi.Core.Query;

/// <summary>Three-way reward-kind filter: quests carrying the kind are removed, left alone, or required.</summary>
public enum TriState
{
    Hidden,
    Show,
    Only,
}

/// <summary>Table column a query sorts on. Journal is the catalog order (SortKey, then row id).</summary>
public enum SortColumn
{
    Journal,
    Name,
    Level,
    State,
    Expansion,
}

/// <summary>Which tree node scopes the table.</summary>
public enum ScopeKind
{
    None,
    Section,
    Category,
    Genre,
    VirtualFeature,
    VirtualUnlisted,
}
