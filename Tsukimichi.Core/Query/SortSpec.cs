namespace Tsukimichi.Core.Query;

/// <summary>
/// Column and direction for the quest table. Sorts are stable: ties keep journal order. <see cref="PinnedFirst"/>
/// moves the pinned rows to the top after sorting, each group keeping its sorted order.
/// </summary>
public readonly record struct SortSpec(SortColumn Column, bool Descending, bool PinnedFirst = true)
{
    public static readonly SortSpec Default = new(SortColumn.Journal, false);
}
