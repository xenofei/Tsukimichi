namespace Tsukimichi.Core.Model;

/// <summary>Where a quest sits in the in-game journal: Section, then Category, then Genre.</summary>
/// <param name="SortKey">Stable ordering key across the journal (section, category, genre order, then row order).</param>
public sealed record JournalRef(
    uint SectionId,
    string SectionName,
    uint CategoryId,
    string CategoryName,
    uint GenreId,
    string GenreName,
    int SortKey)
{
    public static readonly JournalRef None = new(0, string.Empty, 0, string.Empty, 0, string.Empty, 0);
}
