namespace Tsukimichi.Core.Ui;

/// <summary>
/// The listed entries of a table grouped by expansion (Moonlit's "Group by expansion"): row indices, each expansion's
/// rows opened by a heading entry. A heading for expansion <c>e</c> is stored as <c>~e</c>, so it is always negative
/// and never a row index; anything that reads rows from the list (the drawing loop, "Copy view as TSV") skips it.
/// </summary>
public static class GroupedRows
{
    /// <summary>True when an entry is a group heading, not a row index.</summary>
    public static bool IsHeading(int entry) => entry < 0;

    /// <summary>The expansion a heading entry opens.</summary>
    public static byte HeadingExpansion(int entry) => (byte)~entry;

    /// <summary>The heading entry of an expansion.</summary>
    public static int Heading(byte expansion) => ~(int)expansion;

    /// <summary>
    /// Writes <paramref name="picked"/> (already in group order) into <paramref name="into"/>, each run of the same
    /// expansion opened by its heading entry when <paramref name="headings"/> is set; <paramref name="heading"/> is told
    /// each group's expansion and row count. Returns the number of entries written. <paramref name="into"/> needs room
    /// for the rows plus one heading per group.
    /// </summary>
    public static int Fill(IReadOnlyList<int> picked, Func<int, byte> expansionOf, bool headings, int[] into, Action<byte, int>? heading = null)
    {
        ArgumentNullException.ThrowIfNull(picked);
        ArgumentNullException.ThrowIfNull(expansionOf);
        ArgumentNullException.ThrowIfNull(into);
        var count = 0;
        for (var i = 0; i < picked.Count; i++)
        {
            var expansion = expansionOf(picked[i]);
            if (headings && (i == 0 || expansionOf(picked[i - 1]) != expansion))
            {
                var size = 1;
                while (i + size < picked.Count && expansionOf(picked[i + size]) == expansion)
                {
                    size++;
                }

                heading?.Invoke(expansion, size);
                into[count++] = Heading(expansion);
            }

            into[count++] = picked[i];
        }

        return count;
    }

    /// <summary>The row indices among the first <paramref name="count"/> entries, in order, headings skipped.</summary>
    public static List<int> RowIndices(int[] entries, int count)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var rows = new List<int>(count);
        for (var i = 0; i < count && i < entries.Length; i++)
        {
            if (!IsHeading(entries[i]))
            {
                rows.Add(entries[i]);
            }
        }

        return rows;
    }
}
