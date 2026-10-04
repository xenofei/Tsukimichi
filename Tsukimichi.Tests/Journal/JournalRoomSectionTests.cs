using Tsukimichi.Core.Journal;

namespace Tsukimichi.Tests.Journal;

/// <summary>
/// The Characters pane's journal section (C9; 1.19.0 review): it opens by itself when the journal crosses into nearly
/// full, once, not only on the header's first appearance, and never again while it stays there (the player may close it).
/// </summary>
public class JournalRoomSectionTests
{
    [Fact]
    public void It_opens_when_the_journal_crosses_into_nearly_full()
    {
        Assert.True(JournalRoomSection.OpensOnChange(JournalRoom.Room, JournalRoom.Near));
        Assert.True(JournalRoomSection.OpensOnChange(JournalRoom.Room, JournalRoom.Full));

        // The first look (or a newly viewed character) with a nearly full journal opens it too.
        Assert.True(JournalRoomSection.OpensOnChange(null, JournalRoom.Near));
        Assert.True(JournalRoomSection.OpensOnChange(null, JournalRoom.Full));
    }

    [Fact]
    public void It_is_left_as_the_player_left_it_otherwise()
    {
        Assert.False(JournalRoomSection.OpensOnChange(JournalRoom.Near, JournalRoom.Near));
        Assert.False(JournalRoomSection.OpensOnChange(JournalRoom.Near, JournalRoom.Full));
        Assert.False(JournalRoomSection.OpensOnChange(JournalRoom.Full, JournalRoom.Near));
        Assert.False(JournalRoomSection.OpensOnChange(JournalRoom.Full, JournalRoom.Room));
        Assert.False(JournalRoomSection.OpensOnChange(JournalRoom.Room, JournalRoom.Room));
        Assert.False(JournalRoomSection.OpensOnChange(null, JournalRoom.Room));
    }
}
