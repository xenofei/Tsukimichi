namespace Tsukimichi.Core.Query;

/// <summary>
/// Column and direction for the quest table. Sorts are stable: ties keep journal order. <see cref="AvailableFirst"/>
/// moves Ready, ReadyOnOtherJob and Accepted rows to the top after sorting; <see cref="PinnedFirst"/> then moves the
/// pinned rows above everything. Each partition keeps the order it was given, so pinned available quests lead.
/// </summary>
public readonly record struct SortSpec(SortColumn Column, bool Descending, bool PinnedFirst = true, bool AvailableFirst = false)
{
    public static readonly SortSpec Default = new(SortColumn.Journal, false);
}
