using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Data;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The journal tree's node icons (Moon Road proposal §6.2): every node of the frozen catalog's tree resolves to an
/// official icon or a gap glyph, and siblings resolve distinctly wherever the proposal gives them a distinct source
/// (sections, allied societies, classes and jobs, Grand Companies, the class-and-job categories, relic lines).
/// The fixture half uses stand-in sheet values shaped like the real ones; the live half reads the sheets.
/// </summary>
public sealed class NodeIconsTests(FixtureCatalog fixture, ITestOutputHelper output) : IClassFixture<FixtureCatalog>
{
    /// <summary>Stand-in sheet columns with the real ones' shape: generic genre icons except classes, jobs, GCs, Studium, Wachumeqimeqi and roles.</summary>
    private sealed class FakeSheets : INodeIconSheets
    {
        public uint GenreIcon(uint genreId) => genreId switch
        {
            <= 15 => NodeIcons.GenericMsqIcon,
            >= 156 and <= 175 => 62300 + (genreId - 155),
            >= 176 and <= 198 => 62400 + (genreId - 175),
            >= 205 and <= 210 => 62521,
            >= 211 and <= 216 => 62523,
            >= 217 and <= 233 => 62522,
            >= 234 and <= 236 => 61400 + (genreId - 233),
            _ => NodeIcons.GenericSidequestIcon,
        };

        public uint ExpansionIcon(byte expansion) => expansion <= 5 ? 61875u + expansion : 0;

        public uint TribeReputationIcon(byte tribe) => tribe == 0 ? 0 : 61900u + tribe;

        public uint TribeIcon(byte tribe) => tribe == 0 ? 0 : 65000u + tribe;

        public uint ContentTypeIcon(uint contentType) => contentType == 0 ? 0 : 61800 + contentType;
    }

    private NodeIconMap Resolve() => NodeIcons.Resolve(fixture.Bundle.Catalog, new FakeSheets());

    [Fact]
    public void Every_tree_node_resolves_to_an_icon_or_a_glyph()
    {
        var map = Resolve();
        Assert.True(map.Nodes.Count > 250, $"only {map.Nodes.Count} nodes");
        foreach (var node in map.Nodes)
        {
            Assert.False(node.Icon.IsEmpty, node.Scope.ToString());
        }

        Assert.Equal(NodeIcon.Of(OrnamentGlyph.AllQuests), map.For(QuestScope.None));
        Assert.Equal(NodeIcon.Game(NodeIcons.FeatureMarker), map.For(QuestScope.VirtualFeature));
        Assert.Equal(NodeIcon.Of(OrnamentGlyph.Removed), map.For(QuestScope.VirtualUnlisted));

        // Every genre a listed quest sits in answers to its scope (a folded category through its genre).
        foreach (var quest in fixture.Bundle.Catalog.All.Where(q => !q.IsRemoved))
        {
            Assert.True(map.TryGet(QuestScope.Genre(quest.Journal.GenreId), out _), quest.Journal.GenreName);
        }
    }

    [Fact]
    public void Top_level_nodes_are_distinct_but_for_the_two_expansion_split_pairs()
    {
        var map = Resolve();
        var top = map.Nodes.Where(n => n.Facts.Level is NodeLevel.All or NodeLevel.Feature or NodeLevel.Removed or NodeLevel.Section).ToArray();
        Assert.Equal(NodeIcon.Game(NodeIcons.MsqMarker), map.For(QuestScope.Section(0)));
        Assert.Equal(NodeIcon.Game(NodeIcons.MsqMarker), map.For(QuestScope.Section(1)));
        Assert.Equal(NodeIcon.Game(NodeIcons.SidequestMarker), map.For(QuestScope.Section(3)));
        Assert.Equal(NodeIcon.Of(OrnamentGlyph.Chronicles), map.For(QuestScope.Section(2)));
        Assert.Equal(map.For(QuestScope.Section(4)), map.For(QuestScope.Section(5)));

        // MSQ (ARR–EW) / (DT) and Allied (ARR–EW) / (DT) share by design; every other top-level node is its own.
        Assert.Equal(top.Length - 2, top.Select(n => n.Icon).Distinct().Count());
    }

    [Fact]
    public void Siblings_with_their_own_sheet_icons_are_distinct()
    {
        var map = Resolve();
        var catalog = fixture.Bundle.Catalog;

        // Allied societies: each tribe its reputation emblem; under a tribe, main quests and dailies differ.
        foreach (var section in new uint[] { 4, 5 })
        {
            var categories = map.Nodes.Where(n => n.Facts is { Level: NodeLevel.Category } f && f.SectionId == section).ToArray();
            var tribes = categories.Where(n => n.Facts.Tribe != 0).ToArray();

            // Every society is a tribe node; only the one Intersocietal category per section names none.
            Assert.Equal(categories.Length - 1, tribes.Length);
            AssertDistinct(tribes, $"allied section {section}");
            foreach (var tribe in tribes)
            {
                Assert.Equal(NodeIcon.Game(61900u + tribe.Facts.Tribe), tribe.Icon);
                var genres = map.Nodes.Where(n => n.Facts is { Level: NodeLevel.Genre } f && f.CategoryId == tribe.Facts.CategoryId).ToArray();
                AssertDistinct(genres, $"tribe {tribe.Facts.Tribe}");
            }
        }

        // Classes and jobs, per category; and the Class & Job categories themselves.
        foreach (var category in new uint[] { 86, 87, 88, 89, 93, 94 })
        {
            AssertDistinct(map.Nodes.Where(n => n.Facts is { Level: NodeLevel.Genre } f && f.CategoryId == category).ToArray(), $"category {category}");
        }

        AssertDistinct(map.Nodes.Where(n => n.Facts is { Level: NodeLevel.Category, SectionId: 6 }).ToArray(), "class & job categories");

        // Grand Companies and the relic lines.
        AssertDistinct(map.Nodes.Where(n => n.Facts is { Level: NodeLevel.Genre, CategoryId: 96 }).ToArray(), "grand companies");
        AssertDistinct(map.Nodes.Where(n => n.Facts is { Level: NodeLevel.Genre, CategoryId: 56 }).ToArray(), "weapon enhancement");

        // Role genres keep the role icon in every expansion.
        Assert.Equal(NodeIcon.Game(62581), map.For(QuestScope.Genre(217)));
        Assert.Equal(NodeIcon.Game(62581), map.For(QuestScope.Genre(222)));
        Assert.Equal(NodeIcon.Game(62587), map.For(QuestScope.Genre(232)));

        // A facet of the Crystalline Mean shows its crafting class.
        Assert.Equal(map.For(QuestScope.Genre(166)), map.For(QuestScope.Genre(199)));

        Assert.True(catalog.Count > 0);
    }

    [Fact]
    public void Gap_nodes_take_their_glyphs()
    {
        var map = Resolve();
        Assert.Equal(NodeIcon.Of(OrnamentGlyph.Hildibrand), map.For(QuestScope.Category(55)));
        Assert.Equal(NodeIcon.Of(OrnamentGlyph.ChroniclesOfLight), map.For(QuestScope.Category(54)));
        Assert.Equal(NodeIcon.Of(OrnamentGlyph.Relic), map.For(QuestScope.Category(56)));
        Assert.Equal(NodeIcon.Of(OrnamentGlyph.Endeavors), map.For(QuestScope.Category(57)));
        Assert.Equal(NodeIcon.Of(OrnamentGlyph.SideStory), map.For(QuestScope.Category(58)));
        Assert.Equal(NodeIcon.Of(OrnamentGlyph.RegionCoerthas), map.For(QuestScope.Genre(115)));   // Coerthan: a folded category
        Assert.Equal(NodeIcon.Of(OrnamentGlyph.RegionMordhona), map.For(QuestScope.Genre(116)));
        Assert.Equal(NodeIcon.Of(OrnamentGlyph.Festival), map.For(QuestScope.Category(97)));
        Assert.Equal(NodeIcon.Of(OrnamentGlyph.Festival), map.For(QuestScope.Genre(246)));
        Assert.Equal(NodeIcon.Game(61800 + NodeIcons.DeepDungeonContent), map.For(QuestScope.Genre(103)));
        Assert.Equal(NodeIcon.Of(OrnamentGlyph.DeepDungeon), map.For(QuestScope.Genre(104)));

        // MSQ chapters: their expansion's ring, shared within an expansion.
        Assert.Equal(NodeIcon.Game(61875), map.For(QuestScope.Genre(1)));
        Assert.Equal(NodeIcon.Game(61875), map.For(QuestScope.Genre(2)));
        Assert.Equal(NodeIcon.Game(61880), map.For(QuestScope.Genre(13)));
    }

    [Fact]
    public void Distinct_choice_takes_the_next_candidate_and_shares_when_all_are_taken()
    {
        var a = NodeIcon.Game(1);
        var b = NodeIcon.Game(2);
        var g = NodeIcon.Of(OrnamentGlyph.Relic);
        var chosen = NodeIcons.ChooseDistinct([[a, b], [a, b], [a, g], [a]]);
        Assert.Equal([a, b, g, a], chosen);
    }

    [GameDataFact]
    public void Every_node_resolves_from_the_live_sheets_to_an_icon_file_or_a_glyph()
    {
        using var live = new GameDataFixture();
        var resolver = NodeIconResolver.Build(live.Game.Excel);
        var map = resolver.Resolve(live.Bundle.Catalog);
        var official = 0;
        foreach (var node in map.Nodes)
        {
            Assert.False(node.Icon.IsEmpty, node.Scope.ToString());
            if (node.Icon.IsOfficial)
            {
                official++;
                var id = node.Icon.IconId;
                var path = $"ui/icon/{id / 1000 * 1000:D6}/{id:D6}.tex";
                Assert.True(live.Game.FileExists(path), $"{node.Scope}: {path}");
            }
        }

        // The sheet columns behave as the audit found.
        Assert.Equal(61875u, resolver.ExpansionIcon(0));
        Assert.Equal(61901u, resolver.TribeReputationIcon(1));
        Assert.Equal(61814u, resolver.ContentTypeIcon(NodeIcons.SocietyContent));
        Assert.Equal(62301u, resolver.GenreIcon(156));

        output.WriteLine($"{map.Nodes.Count} nodes, {official} official icons, {map.Nodes.Count - official} glyphs");
        foreach (var node in map.Nodes)
        {
            output.WriteLine($"{node.Facts.Level,-8} {node.Scope.Kind,-15} {node.Scope.Id,4}  {node.Icon}");
        }
    }

    private static void AssertDistinct(IReadOnlyList<NodeIconEntry> siblings, string what)
    {
        Assert.NotEmpty(siblings);
        var dupes = siblings.GroupBy(n => n.Icon).Where(g => g.Count() > 1).Select(g => $"{g.Key}: {string.Join(", ", g.Select(n => n.Scope))}").ToArray();
        Assert.True(dupes.Length == 0, $"{what}: {string.Join("; ", dupes)}");
    }
}
