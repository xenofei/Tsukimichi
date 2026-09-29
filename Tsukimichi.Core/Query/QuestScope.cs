namespace Tsukimichi.Core.Query;

/// <summary>The selected journal tree node. <see cref="Id"/> is meaningful for Section, Category and Genre only.</summary>
public readonly record struct QuestScope(ScopeKind Kind, uint Id)
{
    public static readonly QuestScope None = new(ScopeKind.None, 0);

    /// <summary>Feature Unlocks virtual node; membership comes from <see cref="QueryContext.FeatureQuestIds"/>.</summary>
    public static readonly QuestScope VirtualFeature = new(ScopeKind.VirtualFeature, 0);

    /// <summary>The "Removed from the game" virtual node: retired quests and those with no journal genre (<see cref="Model.QuestRecord.IsRemoved"/>), shown regardless of <see cref="FilterSet.IncludeUnlisted"/>.</summary>
    public static readonly QuestScope VirtualUnlisted = new(ScopeKind.VirtualUnlisted, 0);

    public static QuestScope Section(uint id) => new(ScopeKind.Section, id);

    public static QuestScope Category(uint id) => new(ScopeKind.Category, id);

    public static QuestScope Genre(uint id) => new(ScopeKind.Genre, id);
}
