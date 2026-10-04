using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Storage;

/// <summary>
/// Set aside's edits and Undo (feature plan v7 P4, 1.21.0 review): a group set aside for later leaves a quest already
/// marked "Not for me" as it is, and Undo puts every quest back exactly as it was rather than out of both lists.
/// </summary>
public sealed class SetAsideEditsTests : IDisposable
{
    private const ulong Main = 1;
    private const uint Mine = 100;
    private const uint NotMine = 101;
    private const uint Later = 102;

    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private CharacterSettingsBook Book()
    {
        var book = new CharacterSettingsBook(tmp.File("characters.json"));
        book.Edit([CharacterSettingChange.NotForMe(Main, NotMine, true), CharacterSettingChange.SetAside(Main, Later, true)]);
        return book;
    }

    [Fact]
    public void A_group_set_aside_keeps_not_for_me_and_its_undo_restores_each_quest()
    {
        var book = Book();
        uint[] group = [Mine, NotMine, Later];

        var before = SetAsideEdits.Capture(book, Main, group);
        book.Edit(SetAsideEdits.Changes(before, Main, aside: true, notForMe: false, keepSetAside: true));

        Assert.True(book.IsSetAside(Main, Mine));
        Assert.True(book.IsNotForMe(Main, NotMine));
        Assert.Contains(Later, book.Get(Main)!.SetAside);

        book.Edit(SetAsideEdits.Restore(before, Main));

        Assert.False(book.IsSetAside(Main, Mine));
        Assert.True(book.IsNotForMe(Main, NotMine));
        Assert.DoesNotContain(NotMine, book.Get(Main)!.SetAside);
        Assert.Contains(Later, book.Get(Main)!.SetAside);
    }

    [Fact]
    public void Undo_of_one_quest_set_aside_puts_it_back_in_its_list()
    {
        var book = Book();

        // "Not for me" on a quest set aside for later, then Undo: set aside for later again, not brought back.
        var before = SetAsideEdits.Capture(book, Main, [Later]);
        book.Edit(SetAsideEdits.Changes(before, Main, aside: true, notForMe: true));
        Assert.True(book.IsNotForMe(Main, Later));

        book.Edit(SetAsideEdits.Restore(before, Main));
        Assert.False(book.IsNotForMe(Main, Later));
        Assert.Contains(Later, book.Get(Main)!.SetAside);

        // Bring back, then Undo: "Not for me" again.
        before = SetAsideEdits.Capture(book, Main, [NotMine]);
        book.Edit(SetAsideEdits.Changes(before, Main, aside: false, notForMe: false));
        Assert.False(book.IsSetAside(Main, NotMine));
        book.Edit(SetAsideEdits.Restore(before, Main));
        Assert.True(book.IsNotForMe(Main, NotMine));
    }
}
