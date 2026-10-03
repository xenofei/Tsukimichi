using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// Moonlit's grouped list (1.11.0, S5): heading entries sit among the row indices, so "Copy view as TSV" crashed
/// indexing the rows with a heading. Whatever reads rows from the list goes through <see cref="GroupedRows.RowIndices"/>.
/// </summary>
public sealed class GroupedRowsTests
{
    // Row index -> expansion: rows 0 and 3 are A Realm Reborn (0), 1 and 4 Heavensward (1), 2 Endwalker (4).
    private static readonly byte[] Expansions = [0, 1, 4, 0, 1];

    private static readonly int[] Picked = [0, 3, 1, 4, 2];

    [Fact]
    public void Grouped_list_opens_each_expansion_with_a_heading()
    {
        var into = new int[Picked.Length + 3];
        var groups = new List<(byte, int)>();
        var count = GroupedRows.Fill(Picked, i => Expansions[i], headings: true, into, (e, size) => groups.Add((e, size)));

        Assert.Equal(8, count);
        Assert.Equal([GroupedRows.Heading(0), 0, 3, GroupedRows.Heading(1), 1, 4, GroupedRows.Heading(4), 2], into[..count]);
        Assert.Equal([((byte)0, 2), ((byte)1, 2), ((byte)4, 1)], groups);
    }

    [Fact]
    public void Headings_are_negative_even_for_expansion_zero()
    {
        Assert.True(GroupedRows.IsHeading(GroupedRows.Heading(0)));
        Assert.Equal((byte)0, GroupedRows.HeadingExpansion(GroupedRows.Heading(0)));
        Assert.Equal((byte)5, GroupedRows.HeadingExpansion(GroupedRows.Heading(5)));
        Assert.False(GroupedRows.IsHeading(0));
    }

    [Fact]
    public void Copy_view_reads_only_rows_when_grouped()
    {
        var rows = new string[Expansions.Length];
        var into = new int[Picked.Length + 3];
        var count = GroupedRows.Fill(Picked, i => Expansions[i], headings: true, into);

        // The 1.10 exporter indexed the rows with every entry, so the first heading threw.
        Assert.Throws<IndexOutOfRangeException>(() =>
        {
            for (var i = 0; i < count; i++)
            {
                _ = rows[into[i]];
            }
        });

        var listed = GroupedRows.RowIndices(into, count);
        Assert.Equal(Picked, listed);
        Assert.All(listed, i => Assert.InRange(i, 0, rows.Length - 1));
    }

    [Fact]
    public void Without_headings_the_list_is_the_rows()
    {
        var into = new int[Picked.Length];
        var count = GroupedRows.Fill(Picked, i => Expansions[i], headings: false, into);

        Assert.Equal(Picked, into[..count]);
        Assert.Equal(Picked, GroupedRows.RowIndices(into, count));
    }
}
