using Tsukimichi.Core.Characters;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Storage;

/// <summary>
/// Alt goals, stars, roles and nicknames in <c>user\characters.json</c> (plan v7, 1.21.0 N11 and P3): saved per
/// character, merged across clients field by field like every other setting, undone by setting the one before, and a
/// newer build's goal never costs the file.
/// </summary>
public sealed class CharacterGoalSettingsTests : IDisposable
{
    private const ulong Kiri = 1;
    private const ulong Michiru = 2;

    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private string Path => tmp.File("characters.json");

    [Fact]
    public void A_goal_is_saved_per_character_and_reads_back()
    {
        var book = new CharacterSettingsBook(Path);
        book.Edit(CharacterSettingChange.SetGoal(Kiri, AltGoal.Match(Michiru)));
        book.Edit(CharacterSettingChange.SetGoal(Michiru, AltGoal.Story("7.0")));

        Assert.Equal(AltGoal.Match(Michiru), book.Goal(Kiri));
        var again = new CharacterSettingsBook(Path);
        again.Load();
        Assert.Equal(AltGoal.Match(Michiru), again.Goal(Kiri));
        Assert.Equal(AltGoal.Story("7.0"), again.Goal(Michiru));
        Assert.Contains("\"matchCharacter\"", File.ReadAllText(Path), StringComparison.Ordinal);
    }

    [Fact]
    public void Setting_and_clearing_a_goal_undo_by_putting_back_the_one_before()
    {
        var book = new CharacterSettingsBook(Path);
        var first = AltGoal.Roulettes();
        book.Edit(CharacterSettingChange.SetGoal(Kiri, first));

        // Set a new goal, then Undo: the one before comes back.
        var before = book.Goal(Kiri);
        book.Edit(CharacterSettingChange.SetGoal(Kiri, AltGoal.Flying(4)));
        Assert.Equal(AltGoal.Flying(4), book.Goal(Kiri));
        book.Edit(CharacterSettingChange.SetGoal(Kiri, before));
        Assert.Equal(first, book.Goal(Kiri));

        // Clear it, then Undo.
        var version = book.Version;
        book.Edit(CharacterSettingChange.SetGoal(Kiri, null));
        Assert.Null(book.Goal(Kiri));
        Assert.True(book.Version > version);
        Assert.False(CharacterSettingsFile.Load(Path).ContainsKey(Kiri));
        book.Edit(CharacterSettingChange.SetGoal(Kiri, first));
        Assert.Equal(first, CharacterSettingsFile.Load(Path)[Kiri].Goal);
    }

    [Fact]
    public void Another_clients_goal_is_merged_in_beside_this_clients_star()
    {
        var book = new CharacterSettingsBook(Path);
        book.Edit(CharacterSettingChange.Star(Kiri, true));
        CharacterSettingsFile.SaveChanges(Path, [CharacterSettingChange.SetGoal(Kiri, AltGoal.Story("6.0"))], new Dictionary<ulong, CharacterSettings>());
        book.ReloadFromDisk();

        Assert.True(book.IsStarred(Kiri));
        Assert.Equal(AltGoal.Story("6.0"), book.Goal(Kiri));
    }

    [Fact]
    public void Role_and_nickname_are_trimmed_and_blank_clears()
    {
        var book = new CharacterSettingsBook(Path);
        book.Edit(CharacterSettingChange.SetRole(Kiri, "  healer  "));
        book.Edit(CharacterSettingChange.SetNickname(Kiri, new string('k', 40)));
        Assert.Equal("healer", book.Role(Kiri));
        Assert.Equal(CharacterSettings.MaxLabelLength, book.Nickname(Kiri)!.Length);

        book.Edit(CharacterSettingChange.SetRole(Kiri, "   "));
        book.Edit(CharacterSettingChange.SetNickname(Kiri, null));
        Assert.Null(book.Role(Kiri));
        Assert.Null(book.Nickname(Kiri));
        Assert.Empty(book.All);
    }

    [Fact]
    public void A_long_label_is_cut_on_a_character_boundary()
    {
        // 1.21 Core review: the cut at 24 UTF-16 units split a surrogate pair; it counts characters (text elements).
        var moons = "a" + string.Concat(Enumerable.Repeat("\U0001F319", 30));
        var cut = CharacterSettings.Trimmed(moons)!;
        Assert.Equal(1 + ((CharacterSettings.MaxLabelLength - 1) * 2), cut.Length);
        Assert.False(char.IsHighSurrogate(cut[^1]));
        Assert.Equal("\U0001F319", cut[^2..]);

        var japanese = new string('月', 30);
        Assert.Equal(CharacterSettings.MaxLabelLength, CharacterSettings.Trimmed(japanese)!.Length);

        // The text field's buffer counts bytes: 24 Japanese characters take 72 of them.
        Assert.True(CharacterSettings.MaxLabelBytes >= System.Text.Encoding.UTF8.GetByteCount(japanese[..CharacterSettings.MaxLabelLength]));
    }

    [Fact]
    public void Copy_same_and_empty_know_the_new_fields()
    {
        var entry = new CharacterSettings { Goal = AltGoal.Flying(3), Starred = true, Role = "main", Nickname = "K" };
        Assert.False(entry.IsEmpty);
        var copy = entry.Copy();
        Assert.Equal(entry.Goal, copy.Goal);
        Assert.True(copy.Starred);
        Assert.Equal("main", copy.Role);
        Assert.Equal("K", copy.Nickname);

        // A change of the goal alone is a change the book raises.
        var book = new CharacterSettingsBook(Path);
        book.Edit(CharacterSettingChange.SetGoal(Kiri, AltGoal.Flying(3)));
        var version = book.Version;
        book.Edit(CharacterSettingChange.SetGoal(Kiri, AltGoal.Flying(3)));
        Assert.Equal(version, book.Version);
        book.Edit(CharacterSettingChange.SetGoal(Kiri, AltGoal.Flying(4)));
        Assert.Equal(version + 1, book.Version);
    }

    [Fact]
    public void A_goal_this_build_cannot_read_is_dropped_and_the_rest_kept()
    {
        File.WriteAllText(Path, """
        {
          "1": { "hidden": true, "goal": { "kind": "raidReady", "patch": "8.0" }, "starred": true },
          "2": { "goal": { "kind": "story" } },
          "3": { "goal": { "kind": 2, "other": 1 }, "role": "  " }
        }
        """);

        var map = CharacterSettingsFile.Load(Path);
        Assert.True(map[Kiri].Hidden);
        Assert.True(map[Kiri].Starred);
        Assert.Null(map[Kiri].Goal);
        Assert.False(map.ContainsKey(Michiru) && map[Michiru].Goal is not null);
        Assert.False(map.ContainsKey(3) && map[3].Goal is not null);
        Assert.True(!map.ContainsKey(3) || map[3].Role is null);
    }
}
