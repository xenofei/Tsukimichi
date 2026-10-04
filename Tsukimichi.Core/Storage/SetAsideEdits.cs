namespace Tsukimichi.Core.Storage;

/// <summary>
/// The edits behind Set aside (feature plan v7 P4) and their Undo: "Set aside for later" and "Not for me" move a quest
/// into one list and out of the other, Bring back takes it out of both, and a whole group set aside for later leaves a
/// quest already set aside (either way) as it is. Undo puts every quest back exactly as it was before
/// (<see cref="Restore"/>), never just out of both lists. Pure over the book's answers.
/// </summary>
public static class SetAsideEdits
{
    /// <summary>Where one quest stood before an edit: in the "Set aside for later" list, the "Not for me" list, or neither.</summary>
    public readonly record struct Before(uint RowId, bool SetAside, bool NotForMe);

    /// <summary>Where each quest stands now, for <see cref="Restore"/>.</summary>
    public static Before[] Capture(CharacterSettingsBook book, ulong contentId, IReadOnlyList<uint> rowIds)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(rowIds);
        var before = new Before[rowIds.Count];
        var entry = book.Get(contentId);
        for (var i = 0; i < rowIds.Count; i++)
        {
            var rowId = rowIds[i];
            before[i] = new Before(rowId, entry?.SetAside.Contains(rowId) == true, entry?.NotForMe.Contains(rowId) == true);
        }

        return before;
    }

    /// <summary>
    /// The edits that set <paramref name="rowIds"/> aside (<paramref name="aside"/>: for later, or "Not for me") or bring
    /// them back (out of both lists). With <paramref name="keepSetAside"/> (a whole group), a quest already set aside
    /// either way is left as it is.
    /// </summary>
    public static List<CharacterSettingChange> Changes(IReadOnlyList<Before> current, ulong contentId, bool aside, bool notForMe, bool keepSetAside = false)
    {
        ArgumentNullException.ThrowIfNull(current);
        var changes = new List<CharacterSettingChange>(current.Count * 2);
        foreach (var quest in current)
        {
            if (keepSetAside && (quest.SetAside || quest.NotForMe))
            {
                continue;
            }

            changes.Add(CharacterSettingChange.SetAside(contentId, quest.RowId, aside && !notForMe));
            changes.Add(CharacterSettingChange.NotForMe(contentId, quest.RowId, aside && notForMe));
        }

        return changes;
    }

    /// <summary>The edits that put every quest back where <see cref="Capture"/> found it: Undo.</summary>
    public static List<CharacterSettingChange> Restore(IReadOnlyList<Before> before, ulong contentId)
    {
        ArgumentNullException.ThrowIfNull(before);
        var changes = new List<CharacterSettingChange>(before.Count * 2);
        foreach (var quest in before)
        {
            changes.Add(CharacterSettingChange.SetAside(contentId, quest.RowId, quest.SetAside));
            changes.Add(CharacterSettingChange.NotForMe(contentId, quest.RowId, quest.NotForMe));
        }

        return changes;
    }
}
