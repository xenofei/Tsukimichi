using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Storage;

/// <summary>
/// Set aside (feature plan v7 P4): "Set aside for later" and "Not for me" are per character, saved in
/// <c>user/characters.json</c>, merged across game clients like the other choices, and Undo (Bring back) takes them out.
/// </summary>
public sealed class SetAsideSettingsTests : IDisposable
{
    private const ulong Main = 1;
    private const ulong Alt = 2;

    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private string Path => tmp.File("characters.json");

    [Fact]
    public void Setting_aside_is_per_character_saved_and_listed()
    {
        var book = new CharacterSettingsBook(Path);
        book.Edit([CharacterSettingChange.SetAside(Main, 66233, true), CharacterSettingChange.NotForMe(Main, 66591, true), CharacterSettingChange.SetAside(Main, 66024, true)]);

        Assert.True(book.IsSetAside(Main, 66233));
        Assert.True(book.IsSetAside(Main, 66591));
        Assert.True(book.IsNotForMe(Main, 66591));
        Assert.False(book.IsNotForMe(Main, 66233));
        Assert.False(book.IsSetAside(Alt, 66233));
        Assert.Equal([66024u, 66233u, 66591u], book.SetAsideByCharacter()[Main]);
        Assert.False(book.SetAsideByCharacter().ContainsKey(Alt));

        var saved = CharacterSettingsFile.Load(Path)[Main];
        Assert.Equal([66024u, 66233u], saved.SetAside);
        Assert.Equal([66591u], saved.NotForMe);
    }

    [Fact]
    public void Undo_brings_a_quest_back_and_an_empty_character_leaves_the_file()
    {
        var book = new CharacterSettingsBook(Path);
        book.Edit(CharacterSettingChange.SetAside(Main, 66233, true));
        Assert.True(CharacterSettingsFile.Load(Path).ContainsKey(Main));

        book.Edit([CharacterSettingChange.SetAside(Main, 66233, false), CharacterSettingChange.NotForMe(Main, 66233, false)]);

        Assert.False(book.IsSetAside(Main, 66233));
        Assert.Empty(book.SetAsideByCharacter());
        Assert.False(CharacterSettingsFile.Load(Path).ContainsKey(Main));
    }

    [Fact]
    public void A_change_to_a_character_with_other_settings_is_seen_and_raised()
    {
        // CharacterSettingsBook.Same must compare the lists, or an edit to a character that already has an entry
        // (a spoiler override) would read as no change and never be saved.
        var book = new CharacterSettingsBook(Path);
        book.Edit(CharacterSettingChange.Spoiler(Main, true));
        var raised = 0;
        book.Changed += _ => raised++;

        book.Edit(CharacterSettingChange.SetAside(Main, 66233, true));
        book.Edit(CharacterSettingChange.NotForMe(Main, 66591, true));

        Assert.Equal(2, raised);
        var saved = CharacterSettingsFile.Load(Path)[Main];
        Assert.Equal([66233u], saved.SetAside);
        Assert.Equal([66591u], saved.NotForMe);
        Assert.True(saved.SpoilerShield);
    }

    [Fact]
    public void Two_clients_setting_aside_keep_both_and_forget_drops_them()
    {
        var here = new CharacterSettingsBook(Path);
        var there = new CharacterSettingsBook(Path);
        here.Edit(CharacterSettingChange.SetAside(Main, 66233, true));
        there.Edit(CharacterSettingChange.NotForMe(Main, 66591, true));

        var saved = CharacterSettingsFile.Load(Path)[Main];
        Assert.Equal([66233u], saved.SetAside);
        Assert.Equal([66591u], saved.NotForMe);
        here.ReloadFromDisk();
        Assert.True(here.IsSetAside(Main, 66591));

        here.Edit(CharacterSettingChange.Forget(Main));
        Assert.False(here.IsSetAside(Main, 66233));
        Assert.Empty(here.SetAsideByCharacter());
    }

    [Fact]
    public void A_copy_is_deep()
    {
        var settings = new CharacterSettings { SetAside = [1, 2], NotForMe = [3] };
        var copy = settings.Copy();
        copy.SetAside.Add(4);
        copy.NotForMe.Clear();

        Assert.Equal([1u, 2u], settings.SetAside);
        Assert.Equal([3u], settings.NotForMe);
        Assert.False(settings.IsEmpty);
        Assert.True(new CharacterSettings().IsEmpty);
    }
}
