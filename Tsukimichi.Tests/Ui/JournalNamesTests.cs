using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Ui;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The Journal tree's short names (feature plan v4 L3, UI audit §2): <see cref="JournalNames.Short"/> checked against
/// the audit's before/after table for every English node (<c>docs/data/v4/journal-short-names.tsv</c>), the one
/// documented departure from it, the expansion suffix the tree draws as a pill, and the full names every other
/// language keeps. In the CoreText collection because "Story" and "General" are Core phrases read through the ambient
/// provider, which another test may swap.
/// </summary>
[Collection(CoreTextCollection.Name)]
public class JournalNamesTests
{
    private const string English = "English";

    /// <summary>
    /// Where the plugin departs from the table, by node id, with the reason. The table's "Sidequests" for the Lakeland
    /// genre under the Lakeland category is the parent's prefix stripped to the kind word; its own flag says to keep
    /// the region's name and let the tooltip tell the zone from the region, as it does for Thavnair.
    /// </summary>
    private static readonly Dictionary<string, string> Departures = new(StringComparer.Ordinal)
    {
        ["c71/g134"] = "Lakeland",
    };

    private sealed record Row(string Kind, string Id, string Parent, string Before, string After);

    private static readonly Lazy<List<Row>> Table = new(LoadTable);

    public static IEnumerable<object[]> Rows() => Table.Value.Select(static r => new object[] { r.Id, r.Before });

    [Fact]
    public void The_table_covers_every_node_the_audit_measured()
    {
        Assert.Equal(307, Table.Value.Count);
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void Every_English_node_gets_the_short_name_in_the_table(string id, string before)
    {
        var row = Table.Value.Single(r => r.Id == id && r.Before == before);
        var (parent, section) = Context(row);
        var expected = Departures.TryGetValue(row.Id, out var departure) ? departure : row.After;
        Assert.Equal(expected, JournalNames.Short(row.Before, parent, section, English));
    }

    [Fact]
    public void The_audits_examples_come_out_as_written()
    {
        Assert.Equal("Main Scenario · ARR–EW", JournalNames.Short("Main Scenario (A Realm Reborn through Endwalker)", null, "Main Scenario (A Realm Reborn through Endwalker)", English));
        Assert.Equal("Seventh Umbral Era", JournalNames.Short("Seventh Umbral Era Main Scenario Quests", "Main Scenario (A Realm Reborn through Endwalker)", "Main Scenario (A Realm Reborn through Endwalker)", English));
        Assert.Equal("Eden", JournalNames.Short("Chronicles of a New Era - Eden", "Chronicles of a New Era", "Chronicles of a New Era", English));
        Assert.Equal("Hildibrand", JournalNames.Short("Hildibrand Sidequests", "Sidequests", "Sidequests", English));
        Assert.Equal("Allied Societies · ARR–EW", JournalNames.Short("Allied Society Quests (A Realm Reborn through Endwalker)", null, null, English));
        Assert.Equal("Class & Job", JournalNames.Short("Class & Job Quests", null, "Class & Job Quests", English));
    }

    [Fact]
    public void A_child_equal_to_its_parent_is_Story_in_the_chronicles_and_General_elsewhere()
    {
        Assert.Equal(JournalNames.Story, JournalNames.Short("Omega Quests", "Chronicles of a New Era - Omega", "Chronicles of a New Era", English));
        Assert.Equal(JournalNames.General, JournalNames.Short("Studium Quests", "Studium Quests", "Class & Job Quests", English));
        Assert.Equal("Story", JournalNames.Story);
        Assert.Equal("General", JournalNames.General);
    }

    [Theory]
    [InlineData("Japanese")]
    [InlineData("German")]
    [InlineData("French")]
    [InlineData("ja")]
    public void Other_languages_keep_the_full_name(string language)
    {
        Assert.Equal("Hildibrand Sidequests", JournalNames.Short("Hildibrand Sidequests", "Sidequests", "Sidequests", language));
        Assert.Equal("Chronicles of a New Era - Eden", JournalNames.Short("Chronicles of a New Era - Eden", "Chronicles of a New Era", "Chronicles of a New Era", language));
    }

    [Theory]
    [InlineData("English")]
    [InlineData("english")]
    [InlineData("en")]
    public void English_is_recognised_by_name_or_code(string language)
    {
        Assert.True(JournalNames.IsEnglish(language));
    }

    [Fact]
    public void An_empty_name_stays_empty()
    {
        Assert.Equal(string.Empty, JournalNames.Short(string.Empty, "Sidequests", "Sidequests", English));
    }

    [Theory]
    [InlineData("Main Scenario · ARR–EW", "Main Scenario", "ARR–EW")]
    [InlineData("Allied Societies · DT", "Allied Societies", "DT")]
    [InlineData("Tank · ShB", "Tank", "ShB")]
    [InlineData("Seventh Umbral Era", "Seventh Umbral Era", "")]
    [InlineData("Valentione's · Day", "Valentione's · Day", "")]
    public void The_expansion_suffix_splits_off_for_the_pill(string shortName, string head, string suffix)
    {
        Assert.Equal((head, suffix), JournalNames.SplitExpansion(shortName));
    }

    [Fact]
    public void Siblings_stay_apart_after_shortening()
    {
        // Two children of one parent never read the same, or the tree would show two identical rows side by side.
        foreach (var group in Table.Value.Where(static r => r.Parent.Length > 0).GroupBy(static r => r.Parent))
        {
            var names = group.Select(r =>
            {
                var (parent, section) = Context(r);
                return JournalNames.Short(r.Before, parent, section, English);
            }).ToList();
            var duplicates = names.GroupBy(static n => n).Where(static g => g.Count() > 1).Select(static g => g.Key).ToList();
            Assert.True(duplicates.Count == 0, $"under {group.Key}: {string.Join(", ", duplicates)}");
        }
    }

    [Fact]
    public void Shortening_makes_the_tree_about_half_as_wide()
    {
        // The audit measured 62,040 px of labels before and 32,443 after; in characters the saving is alike.
        var before = Table.Value.Sum(static r => r.Before.Length);
        var after = Table.Value.Sum(r =>
        {
            var (parent, section) = Context(r);
            return JournalNames.Short(r.Before, parent, section, English).Length;
        });
        Assert.InRange((float)after / before, 0.4f, 0.6f);
    }

    /// <summary>The parent's and the section's full names for a row, as the tree passes them.</summary>
    private static (string? Parent, string? Section) Context(Row row)
    {
        switch (row.Kind)
        {
            case "section":
                return (null, row.Before);
            case "category":
            case "folded-category-leaf":
                return (row.Parent, row.Parent);
            default:
                // A genre: its category is the row whose id ends in the genre's category id; that row's parent is the section.
                var categoryId = row.Id[..row.Id.IndexOf('/', StringComparison.Ordinal)];
                var category = Table.Value.Single(r => r.Kind == "category" && r.Id.EndsWith("/" + categoryId, StringComparison.Ordinal));
                return (row.Parent, category.Parent);
        }
    }

    private static List<Row> LoadTable()
    {
        var path = Path.Combine(ResxFiles.RepositoryRoot(), "docs", "data", "v4", "journal-short-names.tsv");
        var rows = new List<Row>();
        foreach (var line in File.ReadAllLines(path).Skip(1))
        {
            if (line.Length == 0)
            {
                continue;
            }

            var cells = line.Split('\t');
            rows.Add(new Row(cells[0], cells[1], cells[2], cells[3], cells[4]));
        }

        return rows;
    }
}
