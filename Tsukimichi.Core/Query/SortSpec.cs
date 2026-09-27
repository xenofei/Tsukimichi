namespace Tsukimichi.Core.Query;

/// <summary>Column and direction for the quest table. Sorts are stable: ties keep journal order.</summary>
public readonly record struct SortSpec(SortColumn Column, bool Descending)
{
    public static readonly SortSpec Default = new(SortColumn.Journal, false);
}
