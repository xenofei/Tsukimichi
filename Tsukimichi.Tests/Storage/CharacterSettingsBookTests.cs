using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Storage;

/// <summary>
/// The per-character settings one game client holds (1.8.0): edits show at once and are saved field by field, another
/// client's save is merged in under the edits not saved yet, and the Dalamud settings move in once.
/// </summary>
public sealed class CharacterSettingsBookTests : IDisposable
{
    private const ulong Main = 1;
    private const ulong Alt = 2;

    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private string Path => tmp.File("characters.json");

    [Fact]
    public void An_edit_shows_at_once_and_is_saved()
    {
        var book = new CharacterSettingsBook(Path);
        var raised = new List<bool>();
        book.Changed += raised.Add;

        book.Edit(CharacterSettingChange.Hide(Alt, true));
        book.Edit(CharacterSettingChange.Spoiler(Main, true));

        Assert.True(book.IsHidden(Alt));
        Assert.True(book.SpoilerShield(Main));
        Assert.Equal([false, true], raised);
        Assert.Equal(0, book.PendingCount);
        Assert.True(CharacterSettingsFile.Load(Path)[Alt].Hidden);
    }

    [Fact]
    public void An_edit_that_changes_nothing_is_not_saved_or_raised()
    {
        var book = new CharacterSettingsBook(Path);
        book.Edit(CharacterSettingChange.Track(Main, true));

        Assert.Equal(0, book.Version);
        Assert.False(File.Exists(Path));
    }

    [Fact]
    public void Another_clients_save_is_merged_in_and_this_clients_edits_kept()
    {
        var book = new CharacterSettingsBook(Path);
        book.Edit(CharacterSettingChange.Compare(Main, Alt));

        // The other client hides the alt and announces a gate for the main.
        CharacterSettingsFile.SaveChanges(Path, [CharacterSettingChange.Hide(Alt, true), CharacterSettingChange.Noticed(Main, "eden")], new Dictionary<ulong, CharacterSettings>());
        book.ReloadFromDisk();

        Assert.True(book.IsHidden(Alt));
        Assert.Equal(["eden"], book.Noticed(Main));
        Assert.Equal(Alt, book.CompareWith(Main));
    }

    [Fact]
    public void A_missing_or_broken_file_on_reload_leaves_what_is_held()
    {
        var book = new CharacterSettingsBook(Path);
        book.Edit(CharacterSettingChange.Hide(Alt, true));
        File.WriteAllText(Path, "{ broken");

        book.ReloadFromDisk();

        Assert.True(book.IsHidden(Alt));
        Assert.True(File.Exists(Path));
    }

    [Fact]
    public void Untracked_and_why_open_answers()
    {
        var book = new CharacterSettingsBook(Path);
        Assert.True(book.IsTracked(Main));

        book.Edit([CharacterSettingChange.Track(Main, false), CharacterSettingChange.Why(Main, "eden", true)]);

        Assert.False(book.IsTracked(Main));
        Assert.True(book.IsWhyOpen(Main, "eden"));
        Assert.False(book.IsWhyOpen(Alt, "eden"));
    }

    [Fact]
    public void Migration_shows_at_once_and_reports_when_saved()
    {
        var book = new CharacterSettingsBook(Path);
        bool? saved = null;

        book.MigrateLegacy(
            new LegacyCharacterSettings(new Dictionary<ulong, bool> { [Main] = false }, new Dictionary<ulong, HashSet<string>> { [Alt] = ["eden"] }, new Dictionary<ulong, HashSet<string>>()),
            ok => saved = ok);

        Assert.True(saved);
        Assert.False(book.SpoilerShield(Main));
        Assert.Equal(["eden"], book.Noticed(Alt));
        Assert.False(CharacterSettingsFile.Load(Path)[Main].SpoilerShield);
    }

    [Fact]
    public void Reset_keeps_only_the_chosen_characters()
    {
        var book = new CharacterSettingsBook(Path);
        book.Edit([CharacterSettingChange.Spoiler(Main, true), CharacterSettingChange.Spoiler(Alt, true)]);

        book.Reset(static id => id == Alt);

        Assert.Null(book.SpoilerShield(Main));
        Assert.True(book.SpoilerShield(Alt));
        Assert.Equal([Alt], CharacterSettingsFile.Load(Path).Keys);
    }

    [Fact]
    public void Forget_leaves_an_untracked_character_untracked()
    {
        var book = new CharacterSettingsBook(Path);
        book.Edit([CharacterSettingChange.Track(Main, false), CharacterSettingChange.Spoiler(Main, true), CharacterSettingChange.Hide(Alt, true)]);

        // What Forget character and "Forget characters not seen in N days" send, one per character.
        book.Edit([CharacterSettingChange.Forget(Main), CharacterSettingChange.Forget(Alt)]);

        Assert.False(book.IsTracked(Main));
        Assert.Null(book.SpoilerShield(Main));
        Assert.True(book.IsHidden(Alt));
        var reloaded = new CharacterSettingsBook(Path);
        reloaded.Load();
        Assert.False(reloaded.IsTracked(Main));
        Assert.True(reloaded.IsHidden(Alt));
    }

    [Fact]
    public void Delete_all_keeps_untracked_and_hidden_characters_so()
    {
        var book = new CharacterSettingsBook(Path);
        book.Edit([CharacterSettingChange.Track(Main, false), CharacterSettingChange.Spoiler(Main, true), CharacterSettingChange.Hide(Alt, true), CharacterSettingChange.Compare(Alt, Main)]);

        // Nobody live elsewhere: the untracked character logged in here must not be written at once.
        book.Reset(static _ => false);

        Assert.False(book.IsTracked(Main));
        Assert.Null(book.SpoilerShield(Main));
        Assert.True(book.IsHidden(Alt));
        Assert.Null(book.CompareWith(Alt));
        var onDisk = CharacterSettingsFile.Load(Path);
        Assert.True(onDisk[Main].DontTrack);
        Assert.True(onDisk[Alt].Hidden);
    }

    [Fact]
    public void Load_reads_the_file()
    {
        CharacterSettingsFile.SaveChanges(Path, [CharacterSettingChange.Track(Alt, false)], new Dictionary<ulong, CharacterSettings>());
        var book = new CharacterSettingsBook(Path);

        book.Load();

        Assert.False(book.IsTracked(Alt));
        Assert.Equal(1, book.Version);
    }
}
