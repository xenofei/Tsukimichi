namespace Tsukimichi.Core.Query;

/// <summary>
/// Column and direction for the quest table. Sorts are stable: ties keep journal order. <see cref="AvailableFirst"/>
/// moves Ready, ReadyOnOtherJob and Accepted rows to the top after sorting; <see cref="PinnedFirst"/> then moves the
/// pinned rows above everything; <see cref="NewThisPatchFirst"/> last moves the quests added in the newest patch
/// (<see cref="PatchIndex.IsNew"/>) above those, as the Unlocks quick view's first group. Each partition keeps the
/// order it was given, so within the new group pinned available quests still lead.
/// </summary>
public readonly record struct SortSpec(SortColumn Column, bool Descending, bool PinnedFirst = true, bool AvailableFirst = false, bool NewThisPatchFirst = false)
{
    public static readonly SortSpec Default = new(SortColumn.Journal, false);
}
