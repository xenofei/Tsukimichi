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

    /// <summary>The quests one NPC hands out (<see cref="QuestScope.Id"/> is the ENpcResident row id); reached from the NPC context menu, not the tree.</summary>
    VirtualIssuer,
}

/// <summary>One-click table presets from the filter panel; at most one is active at a time.</summary>
public enum Preset
{
    None,

    /// <summary>Feature (unlock) quests only, from <see cref="QueryContext.FeatureQuestIds"/>; the caller sorts available ones first.</summary>
    FeatureQuests,

    /// <summary>Quests within <see cref="QuestQuery.LevelBandRadius"/> levels of <see cref="QueryContext.CurrentLevel"/>.</summary>
    LevelBand,

    /// <summary>Accepted quests whose journal entry is <see cref="QueryContext.StalledDays"/> days old or older.</summary>
    Stalled,
}
