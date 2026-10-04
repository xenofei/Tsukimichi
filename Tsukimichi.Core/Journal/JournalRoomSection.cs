namespace Tsukimichi.Core.Journal;

/// <summary>
/// The Characters pane's journal section (feature plan v7, C9): it opens by itself when the journal crosses into nearly
/// full, once, so the player can still close it. ImGui's <c>DefaultOpen</c> applies only on a header's first appearance,
/// so a journal that fills while the pane is open would otherwise stay closed. Pure.
/// </summary>
public static class JournalRoomSection
{
    /// <summary>
    /// Whether the section opens now that the journal reads <paramref name="now"/>: it is nearly full or full, and it
    /// had room before (<paramref name="before"/>), or nothing was known before (the first look, a newly viewed
    /// character). Nearly full turning full, or full turning nearly full, leaves it as the player left it.
    /// </summary>
    public static bool OpensOnChange(JournalRoom? before, JournalRoom now) =>
        now != JournalRoom.Room && before is null or JournalRoom.Room;
}
