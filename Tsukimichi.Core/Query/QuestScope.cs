namespace Tsukimichi.Core.Query;

/// <summary>The selected journal tree node. <see cref="Id"/> is meaningful for Section, Category, Genre and Issuer only.</summary>
public readonly record struct QuestScope(ScopeKind Kind, uint Id)
{
    public static readonly QuestScope None = new(ScopeKind.None, 0);

    /// <summary>Feature Unlocks virtual node; membership comes from <see cref="QueryContext.FeatureQuestIds"/>.</summary>
    public static readonly QuestScope VirtualFeature = new(ScopeKind.VirtualFeature, 0);

    /// <summary>The "Removed from the game" virtual node: retired quests and those with no journal genre (<see cref="Model.QuestRecord.IsRemoved"/>), shown regardless of <see cref="FilterSet.IncludeUnlisted"/>.</summary>
    public static readonly QuestScope VirtualUnlisted = new(ScopeKind.VirtualUnlisted, 0);

    /// <summary>The "Other paths" virtual node: quests on a path the character did not take, shown regardless of <see cref="FilterSet.IncludeOtherPaths"/>.</summary>
    public static readonly QuestScope VirtualOtherPaths = new(ScopeKind.VirtualOtherPaths, 0);

    public static QuestScope Section(uint id) => new(ScopeKind.Section, id);

    public static QuestScope Category(uint id) => new(ScopeKind.Category, id);

    public static QuestScope Genre(uint id) => new(ScopeKind.Genre, id);

    /// <summary>
    /// The quests the NPC with this ENpcResident row id hands out (<see cref="Model.Issuer.NpcId"/>), in journal order,
    /// removed quests left out: what the NPC context menu opens the Journal on. Not a tree node; the scope chip names it.
    /// </summary>
    public static QuestScope Issuer(uint npcId) => new(ScopeKind.VirtualIssuer, npcId);

    /// <summary>
    /// The quests the last completions opened (<see cref="QueryContext.JustOpened"/>), in journal order: what the
    /// "Opened:" chat line's Show link opens the Journal on. <paramref name="serial"/> is the line's batch, so a newer
    /// line's scope differs from an older one's. Not a tree node; the scope chip names it.
    /// </summary>
    public static QuestScope JustOpened(uint serial) => new(ScopeKind.VirtualJustOpened, serial);
}
