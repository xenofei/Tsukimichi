using System.Collections.Frozen;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// A journal tree node's identity icon (Moon Road proposal §6): an official game icon (<see cref="IconId"/>, drawn with
/// <c>GetFromGameIcon</c>) or, where the game has no legible one, an original gap glyph from the ornament atlas.
/// </summary>
public readonly record struct NodeIcon(uint IconId, OrnamentGlyph Glyph)
{
    public static NodeIcon Game(uint iconId) => new(iconId, OrnamentGlyph.None);

    public static NodeIcon Of(OrnamentGlyph glyph) => new(0, glyph);

    /// <summary>An official icon (else a glyph).</summary>
    public bool IsOfficial => IconId != 0;

    public bool IsEmpty => IconId == 0 && Glyph == OrnamentGlyph.None;

    public override string ToString() => IsOfficial ? IconId.ToString("D6", System.Globalization.CultureInfo.InvariantCulture) : Glyph.ToString();
}

/// <summary>
/// The sheet columns the node icons read (Lumina-backed in <c>Tsukimichi.GameData.NodeIconResolver</c>), so a patch that
/// adds a society or a class gets its emblem without a code change. Every method returns 0 for "none".
/// </summary>
public interface INodeIconSheets
{
    /// <summary><c>JournalGenre.Icon</c>.</summary>
    uint GenreIcon(uint genreId);

    /// <summary><c>ExVersion.Icon</c> (061875 ARR … 061880 DT).</summary>
    uint ExpansionIcon(byte expansion);

    /// <summary><c>BeastTribe.IconReputation</c> (061901…).</summary>
    uint TribeReputationIcon(byte tribe);

    /// <summary><c>BeastTribe.Icon</c>.</summary>
    uint TribeIcon(byte tribe);

    /// <summary><c>ContentType.Icon</c> (the Duty Finder category tiles).</summary>
    uint ContentTypeIcon(uint contentType);
}

/// <summary>Which level of the journal tree a node is.</summary>
public enum NodeLevel : byte
{
    All,
    Section,
    Category,
    Genre,
    Feature,
    Removed,
}

/// <summary>
/// What the candidate rules read about one tree node. <paramref name="Expansion"/> is the expansion at least 75 % of the
/// node's quests share (null when mixed); <paramref name="Tribe"/> the one allied society the node's quests name (quests
/// naming none are ignored; 0 when none or several);
/// <paramref name="FirstGenreId"/> the first genre under the node; <paramref name="AllRepeatable"/> whether every quest
/// is repeatable (an allied society's dailies).
/// </summary>
public sealed record NodeFacts(
    NodeLevel Level,
    uint SectionId,
    uint CategoryId,
    uint GenreId,
    byte? Expansion,
    byte Tribe,
    uint FirstGenreId,
    bool AllRepeatable);

/// <summary>
/// The icon of every journal tree node (Moon Road proposal §6.2, UI audit §1), resolved once per catalog: each node's
/// candidates in order, then the first candidate not already taken by an earlier sibling (a node whose candidates are
/// all taken shares its first; short names tell those apart). Only small, English-independent hand lists live here,
/// keyed by sheet ids: the three EventIconType markers, the ContentType tile per genre, the role and facet genres and
/// the category-to-glyph map. Everything else is read from the sheets through <see cref="INodeIconSheets"/>.
/// </summary>
public static class NodeIcons
{
    /// <summary>EventIconType markers (<c>NpcIconAvailable</c> + 1): main scenario, sidequest, feature (the blue plus).</summary>
    public const uint MsqMarker = 71201;
    public const uint SidequestMarker = 71221;
    public const uint FeatureMarker = 71341;

    /// <summary>The generic quest-kind icons <c>JournalGenre.Icon</c> falls back to; never distinct, so never a candidate.</summary>
    public const uint GenericSidequestIcon = 61411;
    public const uint GenericMsqIcon = 61412;

    /// <summary>Journal quest-kind icons for an allied society's main quests (feature) and dailies (repeatable).</summary>
    public const uint FeatureQuestIcon = 61419;
    public const uint RepeatableQuestIcon = 61413;

    /// <summary>The framed Class &amp; Job journal emblem.</summary>
    public const uint ClassJobEmblem = 62147;

    /// <summary>ContentType rows.</summary>
    public const uint GrandCompanyContent = 11;
    public const uint SocietyContent = 13;
    public const uint LandContent = 16;
    public const uint HandContent = 17;
    public const uint DeepDungeonContent = 21;

    /// <summary>JournalCategory ids with fixed treatment (see <see cref="BannerArts"/> for the section ids).</summary>
    public const uint DiscipleOfTheHandCategory = 88;
    public const uint DiscipleOfTheLandCategory = 89;
    public const uint CrystallineMeanCategory = 90;
    public const uint SpecialQuestsCategory = 98;

    /// <summary>The ContentType tile that backs a relic or endeavour genre (Eureka, Bozja, the Occult Crescent, Ishgardian Restoration, deep dungeons, Island Sanctuary, Variant dungeons).</summary>
    public static readonly FrozenDictionary<uint, uint> GenreContentType = new Dictionary<uint, uint>
    {
        [90] = 26, [91] = 29, [92] = 38,
        [100] = HandContent, [101] = HandContent, [102] = HandContent,
        [103] = DeepDungeonContent, [104] = DeepDungeonContent, [105] = DeepDungeonContent, [106] = DeepDungeonContent,
        [107] = 36, [108] = 30, [111] = 38,
    }.ToFrozenDictionary();

    /// <summary>Role quest genres to the role icons (062581 tank, 062582 healer, 062583 DPS, 062584 melee, 062586 physical ranged, 062587 magical ranged).</summary>
    public static readonly FrozenDictionary<uint, uint> RoleGenreIcon = new Dictionary<uint, uint>
    {
        [217] = 62581, [218] = 62582, [219] = 62583, [220] = 62587,
        [222] = 62581, [223] = 62582, [224] = 62584, [225] = 62586, [226] = 62587,
        [228] = 62581, [229] = 62582, [230] = 62584, [231] = 62586, [232] = 62587,
    }.ToFrozenDictionary();

    /// <summary>Crystalline Mean facet genres to the class genre whose icon stands for them (Forging → Blacksmith, Crafting → Carpenter, Nourishing → Culinarian, Gathering → Miner, Fishing → Fisher).</summary>
    public static readonly FrozenDictionary<uint, uint> FacetClassGenre = new Dictionary<uint, uint>
    {
        [199] = 166, [200] = 165, [201] = 172, [202] = 173, [203] = 175,
    }.ToFrozenDictionary();

    /// <summary>Sidequest categories drawn with a gap glyph.</summary>
    public static OrnamentGlyph SidequestCategoryGlyph(uint categoryId) => categoryId switch
    {
        BannerArts.ChroniclesOfLightCategory => OrnamentGlyph.ChroniclesOfLight,
        BannerArts.HildibrandCategory => OrnamentGlyph.Hildibrand,
        BannerArts.WeaponEnhancementCategory => OrnamentGlyph.Relic,
        BannerArts.UnusualEndeavorsCategory => OrnamentGlyph.Endeavors,
        BannerArts.SideStoryCategory => OrnamentGlyph.SideStory,
        62 => OrnamentGlyph.RegionCoerthas,
        63 => OrnamentGlyph.RegionMordhona,
        _ => OrnamentGlyph.None,
    };

    /// <summary>The node's icon candidates, best first; never empty (the last resort is the Other glyph).</summary>
    public static IReadOnlyList<NodeIcon> Candidates(NodeFacts node, INodeIconSheets sheets)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(sheets);
        var list = new List<NodeIcon>(4);
        switch (node.Level)
        {
            case NodeLevel.All:
                Add(list, OrnamentGlyph.AllQuests);
                break;
            case NodeLevel.Feature:
                Add(list, FeatureMarker);
                Add(list, OrnamentGlyph.PlanFallback);
                break;
            case NodeLevel.Removed:
                Add(list, OrnamentGlyph.Removed);
                break;
            case NodeLevel.Section:
                SectionCandidates(list, node, sheets);
                break;
            case NodeLevel.Category:
                CategoryCandidates(list, node, sheets);
                break;
            case NodeLevel.Genre:
                GenreCandidates(list, node, sheets);
                break;
        }

        if (list.Count == 0)
        {
            list.Add(NodeIcon.Of(OrnamentGlyph.Other));
        }

        return list;
    }

    private static void SectionCandidates(List<NodeIcon> list, NodeFacts node, INodeIconSheets sheets)
    {
        switch (node.SectionId)
        {
            case BannerArts.MainScenarioSection or BannerArts.MainScenarioDawntrailSection:
                Add(list, MsqMarker);
                break;
            case BannerArts.ChroniclesSection:
                Add(list, OrnamentGlyph.Chronicles);
                break;
            case BannerArts.SidequestSection:
                Add(list, SidequestMarker);
                break;
            case BannerArts.AlliedSection or BannerArts.AlliedDawntrailSection:
                Add(list, sheets.ContentTypeIcon(SocietyContent));
                break;
            case BannerArts.ClassJobSection:
                Add(list, ClassJobEmblem);
                Add(list, sheets.ContentTypeIcon(HandContent));
                break;
            case BannerArts.OtherQuestsSection:
                Add(list, OrnamentGlyph.Other);
                break;
            default:
                AddExpansion(list, node, sheets);
                Add(list, OrnamentGlyph.Other);
                break;
        }
    }

    private static void CategoryCandidates(List<NodeIcon> list, NodeFacts node, INodeIconSheets sheets)
    {
        switch (node.SectionId)
        {
            case BannerArts.MainScenarioSection or BannerArts.MainScenarioDawntrailSection:
                AddExpansion(list, node, sheets);
                break;
            case BannerArts.ChroniclesSection:
                // The series share their expansion's ring; the short names ("Eden", "Omega") tell them apart (§15).
                AddExpansion(list, node, sheets);
                break;
            case BannerArts.SidequestSection:
                var glyph = SidequestCategoryGlyph(node.CategoryId);
                if (glyph != OrnamentGlyph.None)
                {
                    Add(list, glyph);
                }

                AddExpansion(list, node, sheets);
                break;
            case BannerArts.AlliedSection or BannerArts.AlliedDawntrailSection:
                if (node.Tribe != 0)
                {
                    Add(list, sheets.TribeReputationIcon(node.Tribe));
                    Add(list, sheets.TribeIcon(node.Tribe));
                }
                else
                {
                    AddExpansion(list, node, sheets);
                    Add(list, sheets.ContentTypeIcon(SocietyContent));
                }

                break;
            case BannerArts.ClassJobSection:
                switch (node.CategoryId)
                {
                    case DiscipleOfTheHandCategory:
                        Add(list, sheets.ContentTypeIcon(HandContent));
                        break;
                    case DiscipleOfTheLandCategory:
                        Add(list, sheets.ContentTypeIcon(LandContent));
                        break;
                    case CrystallineMeanCategory:
                        Add(list, sheets.ContentTypeIcon(HandContent));
                        break;
                }

                AddGenreIcon(list, FacetClassGenre.GetValueOrDefault(node.FirstGenreId, node.FirstGenreId), sheets);
                break;
            case BannerArts.OtherQuestsSection:
                switch (node.CategoryId)
                {
                    case BannerArts.GrandCompanyCategory:
                        Add(list, sheets.ContentTypeIcon(GrandCompanyContent));
                        break;
                    case BannerArts.SeasonalEventsCategory:
                        Add(list, OrnamentGlyph.Festival);
                        break;
                    case SpecialQuestsCategory:
                        Add(list, OrnamentGlyph.Special);
                        break;
                }

                break;
            default:
                AddExpansion(list, node, sheets);
                break;
        }
    }

    // A node whose candidates run out shares its first candidate with an earlier sibling; the Other glyph is only the
    // last resort for a node with no candidate at all (see Candidates), never a fallback for a taken icon.
    private static void GenreCandidates(List<NodeIcon> list, NodeFacts node, INodeIconSheets sheets)
    {
        // Role genres keep their role icon even when an earlier expansion's genre has it ("Tank · EW" beside "Tank · ShB").
        if (RoleGenreIcon.TryGetValue(node.GenreId, out var role))
        {
            Add(list, role);
            return;
        }

        // Festivals share the lantern: their portraits are illegible at row size, and the short names tell them apart.
        if (node.SectionId == BannerArts.OtherQuestsSection && node.CategoryId == BannerArts.SeasonalEventsCategory)
        {
            Add(list, OrnamentGlyph.Festival);
            return;
        }

        if (FacetClassGenre.TryGetValue(node.GenreId, out var facetClass))
        {
            AddGenreIcon(list, facetClass, sheets);
        }

        if (GenreContentType.TryGetValue(node.GenreId, out var content))
        {
            Add(list, sheets.ContentTypeIcon(content));
            if (content == DeepDungeonContent)
            {
                Add(list, OrnamentGlyph.DeepDungeon);
            }
        }

        if (node.SectionId is BannerArts.AlliedSection or BannerArts.AlliedDawntrailSection && node.Tribe != 0)
        {
            Add(list, node.AllRepeatable ? RepeatableQuestIcon : FeatureQuestIcon);
            Add(list, sheets.TribeReputationIcon(node.Tribe));
        }

        // The sheet's own genre icon is the identity: siblings that share it (the Studium faculties) share it rather than
        // one of them drifting to an expansion ring.
        var before = list.Count;
        AddGenreIcon(list, node.GenreId, sheets);
        if (list.Count > before && before == 0)
        {
            return;
        }

        AddExpansion(list, node, sheets);
        if (node.SectionId == BannerArts.OtherQuestsSection && node.CategoryId == SpecialQuestsCategory)
        {
            Add(list, OrnamentGlyph.Special);
        }
    }

    /// <summary>
    /// The icons of one sibling group: each node takes its first candidate not taken by an earlier sibling, or its first
    /// candidate when every one is taken.
    /// </summary>
    public static NodeIcon[] ChooseDistinct(IReadOnlyList<IReadOnlyList<NodeIcon>> siblings)
    {
        ArgumentNullException.ThrowIfNull(siblings);
        var chosen = new NodeIcon[siblings.Count];
        var used = new HashSet<NodeIcon>();
        for (var i = 0; i < siblings.Count; i++)
        {
            var candidates = siblings[i];
            var pick = candidates.Count > 0 ? candidates[0] : NodeIcon.Of(OrnamentGlyph.Other);
            foreach (var candidate in candidates)
            {
                if (!used.Contains(candidate))
                {
                    pick = candidate;
                    break;
                }
            }

            used.Add(pick);
            chosen[i] = pick;
        }

        return chosen;
    }

    /// <summary>
    /// Every node of the journal tree as the tree pane builds it (All quests; Section → Category → Genre over the quests
    /// still in the journal, a category with one genre folded into a leaf, a section left with one leaf folded into it;
    /// then Feature Unlocks and Removed from the game), resolved sibling group by sibling group.
    /// </summary>
    public static NodeIconMap Resolve(QuestCatalog catalog, INodeIconSheets sheets)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(sheets);
        var tree = NodeTree.Build(catalog);
        var map = new Dictionary<QuestScope, NodeIcon>();
        var nodes = new List<(QuestScope Key, QuestScope? Alias, NodeFacts Facts)>();

        var top = new List<NodeTree.Node> { NodeTree.Node.Virtual(NodeLevel.All) };
        top.AddRange(tree.Sections);
        top.Add(NodeTree.Node.Virtual(NodeLevel.Feature));
        top.Add(NodeTree.Node.Virtual(NodeLevel.Removed));
        ResolveGroup(top, sheets, map, nodes);
        return new NodeIconMap(map.ToFrozenDictionary(), nodes.ConvertAll(n => new NodeIconEntry(n.Key, n.Facts, map[n.Key])));
    }

    private static void ResolveGroup(IReadOnlyList<NodeTree.Node> group, INodeIconSheets sheets, Dictionary<QuestScope, NodeIcon> map,
        List<(QuestScope, QuestScope?, NodeFacts)> nodes)
    {
        var candidates = new List<IReadOnlyList<NodeIcon>>(group.Count);
        foreach (var node in group)
        {
            candidates.Add(node.CandidatesFor(sheets));
        }

        var chosen = ChooseDistinct(candidates);
        for (var i = 0; i < group.Count; i++)
        {
            var node = group[i];
            map[node.Scope] = chosen[i];
            foreach (var alias in node.Aliases)
            {
                map.TryAdd(alias, chosen[i]);
            }

            nodes.Add((node.Scope, null, node.Facts));
            if (node.Children.Count > 0)
            {
                ResolveGroup(node.Children, sheets, map, nodes);
            }
        }
    }

    private static void Add(List<NodeIcon> list, uint iconId)
    {
        if (iconId != 0 && iconId != GenericSidequestIcon && iconId != GenericMsqIcon)
        {
            var icon = NodeIcon.Game(iconId);
            if (!list.Contains(icon))
            {
                list.Add(icon);
            }
        }
    }

    private static void Add(List<NodeIcon> list, OrnamentGlyph glyph)
    {
        var icon = NodeIcon.Of(glyph);
        if (glyph != OrnamentGlyph.None && !list.Contains(icon))
        {
            list.Add(icon);
        }
    }

    private static void AddExpansion(List<NodeIcon> list, NodeFacts node, INodeIconSheets sheets)
    {
        if (node.Expansion is { } expansion)
        {
            Add(list, sheets.ExpansionIcon(expansion));
        }
    }

    private static void AddGenreIcon(List<NodeIcon> list, uint genreId, INodeIconSheets sheets)
    {
        if (genreId != 0)
        {
            Add(list, sheets.GenreIcon(genreId));
        }
    }

    /// <summary>The journal tree's shape, as <c>TreePane.EnsureNodes</c> and <c>Fold</c> build it, with the facts each node's rules read.</summary>
    internal sealed class NodeTree
    {
        public List<Node> Sections { get; } = [];

        public static NodeTree Build(QuestCatalog catalog)
        {
            var tree = new NodeTree();
            var sections = new Dictionary<uint, Node>();
            var categories = new Dictionary<uint, Node>();
            var genres = new Dictionary<uint, Node>();
            var ordered = new List<QuestRecord>(catalog.All);
            ordered.Sort(static (a, b) => a.Journal.SortKey != b.Journal.SortKey ? a.Journal.SortKey.CompareTo(b.Journal.SortKey) : a.RowId.CompareTo(b.RowId));
            foreach (var quest in ordered)
            {
                if (quest.IsRemoved)
                {
                    continue;
                }

                var j = quest.Journal;
                if (!sections.TryGetValue(j.SectionId, out var section))
                {
                    section = new Node(NodeLevel.Section, QuestScope.Section(j.SectionId), j.SectionId, 0, 0);
                    sections[j.SectionId] = section;
                    tree.Sections.Add(section);
                }

                if (!categories.TryGetValue(j.CategoryId, out var category))
                {
                    category = new Node(NodeLevel.Category, QuestScope.Category(j.CategoryId), j.SectionId, j.CategoryId, 0);
                    categories[j.CategoryId] = category;
                    section.Children.Add(category);
                }

                if (!genres.TryGetValue(j.GenreId, out var genre))
                {
                    genre = new Node(NodeLevel.Genre, QuestScope.Genre(j.GenreId), j.SectionId, j.CategoryId, j.GenreId);
                    genres[j.GenreId] = genre;
                    category.Children.Add(genre);
                }

                section.Note(quest);
                category.Note(quest);
                genre.Note(quest);
            }

            for (var i = 0; i < tree.Sections.Count; i++)
            {
                tree.Sections[i] = Fold(tree.Sections[i]);
            }

            return tree;
        }

        private static Node Fold(Node section)
        {
            for (var i = 0; i < section.Children.Count; i++)
            {
                var category = section.Children[i];
                if (category.Children.Count == 1)
                {
                    section.Children[i] = category.FoldInto(category.Children[0]);
                }
            }

            if (section.Children.Count == 1 && section.Children[0].Children.Count == 0)
            {
                return section.FoldInto(section.Children[0]);
            }

            return section;
        }

        internal sealed class Node
        {
            private readonly Dictionary<byte, int> expansions = [];
            private readonly HashSet<byte> tribes = [];
            private int quests;
            private bool allRepeatable = true;
            private IReadOnlyList<NodeIcon>? foldedCandidates;

            public Node(NodeLevel level, QuestScope scope, uint sectionId, uint categoryId, uint genreId)
            {
                Level = level;
                Scope = scope;
                SectionId = sectionId;
                CategoryId = categoryId;
                GenreId = genreId;
            }

            public NodeLevel Level { get; }

            public QuestScope Scope { get; private set; }

            public uint SectionId { get; }

            public uint CategoryId { get; }

            public uint GenreId { get; }

            public List<Node> Children { get; } = [];

            /// <summary>Other scopes the same visible node answers to (a folded category's own category scope).</summary>
            public List<QuestScope> Aliases { get; } = [];

            public NodeFacts Facts => new(Level, SectionId, CategoryId, GenreId, DominantExpansion(), tribes.Count == 1 ? tribes.First() : (byte)0, FirstGenre(), quests > 0 && allRepeatable);

            public static Node Virtual(NodeLevel level) => new(level, level switch
            {
                NodeLevel.Feature => QuestScope.VirtualFeature,
                NodeLevel.Removed => QuestScope.VirtualUnlisted,
                _ => QuestScope.None,
            }, 0, 0, 0);

            public void Note(QuestRecord quest)
            {
                quests++;
                expansions[quest.Expansion] = expansions.GetValueOrDefault(quest.Expansion) + 1;

                // A society's own quests do not all carry its tribe (its opening quests have none), so zeros don't count.
                if (quest.BeastTribe != 0)
                {
                    tribes.Add(quest.BeastTribe);
                }
                allRepeatable &= quest.IsRepeatable;
            }

            /// <summary>
            /// This node shown as a leaf in place of <paramref name="leaf"/> (the tree's fold): it keeps its own level and
            /// place but takes the leaf's scope, and tries its own candidates before the leaf's.
            /// </summary>
            public Node FoldInto(Node leaf)
            {
                Aliases.Add(Scope);
                Aliases.AddRange(leaf.Aliases);
                Scope = leaf.Scope;
                Children.Clear();
                folded = leaf;
                return this;
            }

            private Node? folded;

            public IReadOnlyList<NodeIcon> CandidatesFor(INodeIconSheets sheets)
            {
                if (foldedCandidates is not null)
                {
                    return foldedCandidates;
                }

                var own = Candidates(Facts, sheets);
                if (folded is null)
                {
                    return own;
                }

                // The node's own specific candidates, then the leaf's, then the shared last resort.
                var merged = new List<NodeIcon>();
                foreach (var icon in own)
                {
                    if (icon.Glyph != OrnamentGlyph.Other)
                    {
                        merged.Add(icon);
                    }
                }

                foreach (var icon in folded.CandidatesFor(sheets))
                {
                    if (!merged.Contains(icon))
                    {
                        merged.Add(icon);
                    }
                }

                return foldedCandidates = merged;
            }

            private byte? DominantExpansion()
            {
                if (quests == 0)
                {
                    return null;
                }

                foreach (var (expansion, count) in expansions)
                {
                    if (count * 4 >= quests * 3)
                    {
                        return expansion;
                    }
                }

                return null;
            }

            private uint FirstGenre()
            {
                if (GenreId != 0)
                {
                    return GenreId;
                }

                var node = this;
                while (node.Children.Count > 0)
                {
                    node = node.Children[0];
                }

                return node.GenreId != 0 ? node.GenreId : folded?.FirstGenre() ?? 0;
            }
        }
    }
}

/// <summary>One resolved tree node, for diagnostics and tests.</summary>
public sealed record NodeIconEntry(QuestScope Scope, NodeFacts Facts, NodeIcon Icon);

/// <summary>The resolved icons, keyed by the scope the tree node selects (a folded category answers to its genre's scope and its own).</summary>
public sealed class NodeIconMap
{
    public static readonly NodeIconMap Empty = new(FrozenDictionary<QuestScope, NodeIcon>.Empty, []);

    private readonly FrozenDictionary<QuestScope, NodeIcon> byScope;

    internal NodeIconMap(FrozenDictionary<QuestScope, NodeIcon> byScope, IReadOnlyList<NodeIconEntry> nodes)
    {
        this.byScope = byScope;
        Nodes = nodes;
    }

    /// <summary>Every visible node in tree order (depth first), with the facts its rules read.</summary>
    public IReadOnlyList<NodeIconEntry> Nodes { get; }

    /// <summary>The node's icon; an unknown scope gets the Other glyph.</summary>
    public NodeIcon For(QuestScope scope) => byScope.TryGetValue(scope, out var icon) ? icon : NodeIcon.Of(OrnamentGlyph.Other);

    public bool TryGet(QuestScope scope, out NodeIcon icon) => byScope.TryGetValue(scope, out icon);
}
