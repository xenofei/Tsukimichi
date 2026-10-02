using System.Text.Json.Nodes;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Storage;

/// <summary>
/// <c>user/characters.json</c> (1.8.0, R7 D): per-character settings every game client shares, saved field by field
/// under the cross-client lock, and the one-time move from the Dalamud settings.
/// </summary>
public sealed class CharacterSettingsFileTests : IDisposable
{
    private const ulong Main = 0x0040_0000_0000_0001UL;
    private const ulong Alt = 2;

    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private string Path => tmp.File(System.IO.Path.Combine("user", "characters.json"));

    private static LegacyCharacterSettings Legacy(
        Dictionary<ulong, bool>? shield = null,
        Dictionary<ulong, HashSet<string>>? noticed = null,
        Dictionary<ulong, HashSet<string>>? why = null) =>
        new(shield ?? [], noticed ?? [], why ?? []);

    [Fact]
    public void Settings_round_trip_and_unset_fields_are_not_written()
    {
        var map = new Dictionary<ulong, CharacterSettings>();
        CharacterSettingsFile.Apply(map,
        [
            CharacterSettingChange.Spoiler(Main, false),
            CharacterSettingChange.Hide(Alt, true),
            CharacterSettingChange.Compare(Main, Alt),
            CharacterSettingChange.Noticed(Main, "eden"),
        ]);
        CharacterSettingsFile.Save(Path, map);

        var json = JsonNode.Parse(File.ReadAllText(Path))!.AsObject();
        var main = json[Main.ToString(System.Globalization.CultureInfo.InvariantCulture)]!.AsObject();
        Assert.False((bool)main["spoilerShield"]!);
        Assert.Equal(2, (int)main["compareWith"]!);
        Assert.False(main.ContainsKey("hidden"));
        Assert.False(main.ContainsKey("payoffWhyOpen"));

        var loaded = CharacterSettingsFile.Load(Path);
        Assert.False(loaded[Main].SpoilerShield);
        Assert.Equal(["eden"], loaded[Main].PayoffGatesNoticed);
        Assert.True(loaded[Alt].Hidden);
        Assert.True(loaded[Alt].SpoilerShield is null);
    }

    [Fact]
    public void A_character_left_with_nothing_set_leaves_the_file()
    {
        var map = new Dictionary<ulong, CharacterSettings>();
        CharacterSettingsFile.Apply(map, [CharacterSettingChange.Hide(Alt, true), CharacterSettingChange.Hide(Alt, false)]);
        Assert.Empty(map);

        CharacterSettingsFile.Apply(map, [CharacterSettingChange.Why(Main, "eden", true), CharacterSettingChange.Why(Main, "eden", false)]);
        Assert.Empty(map);
    }

    [Fact]
    public void Fields_a_newer_build_wrote_survive_a_save_here()
    {
        File.WriteAllText(tmp.File("characters.json"), """{ "2": { "hidden": true, "streamerMask": "A." } }""");
        var path = tmp.File("characters.json");

        CharacterSettingsFile.SaveChanges(path, [CharacterSettingChange.Spoiler(Alt, true)], new Dictionary<ulong, CharacterSettings>());

        var alt = JsonNode.Parse(File.ReadAllText(path))!["2"]!.AsObject();
        Assert.Equal("A.", (string)alt["streamerMask"]!);
        Assert.True((bool)alt["spoilerShield"]!);
        Assert.True((bool)alt["hidden"]!);
    }

    [Fact]
    public void Two_clients_editing_different_fields_of_one_character_keep_both()
    {
        // Client A hides the alt; client B, which loaded before A saved, sets the alt's spoiler override.
        var a = CharacterSettingsFile.Load(Path);
        var b = CharacterSettingsFile.Load(Path);
        CharacterSettingsFile.Apply(a, [CharacterSettingChange.Hide(Alt, true)]);
        CharacterSettingsFile.SaveChanges(Path, [CharacterSettingChange.Hide(Alt, true)], a);
        CharacterSettingsFile.Apply(b, [CharacterSettingChange.Spoiler(Alt, false)]);
        var merged = CharacterSettingsFile.SaveChanges(Path, [CharacterSettingChange.Spoiler(Alt, false)], b);

        Assert.True(merged[Alt].Hidden);
        Assert.False(merged[Alt].SpoilerShield);
        Assert.True(CharacterSettingsFile.Load(Path)[Alt].Hidden);
    }

    [Fact]
    public void Gates_noticed_in_two_clients_are_both_kept()
    {
        CharacterSettingsFile.SaveChanges(Path, [CharacterSettingChange.Noticed(Main, "eden")], new Dictionary<ulong, CharacterSettings>());
        var merged = CharacterSettingsFile.SaveChanges(Path, [CharacterSettingChange.Noticed(Main, "bozja")], new Dictionary<ulong, CharacterSettings>());

        Assert.Equal(["eden", "bozja"], merged[Main].PayoffGatesNoticed);
    }

    [Fact]
    public void A_file_that_does_not_parse_is_quarantined_and_this_clients_map_stands_in()
    {
        File.WriteAllText(tmp.File("characters.json"), "{ not json");
        var path = tmp.File("characters.json");
        var mine = new Dictionary<ulong, CharacterSettings>();
        CharacterSettingsFile.Apply(mine, [CharacterSettingChange.Hide(Main, true), CharacterSettingChange.Spoiler(Alt, true)]);
        var warnings = new List<string>();

        var merged = CharacterSettingsFile.SaveChanges(path, [CharacterSettingChange.Spoiler(Alt, true)], mine, warnings);

        Assert.True(merged[Main].Hidden);
        Assert.True(merged[Alt].SpoilerShield);
        Assert.Single(warnings);
        Assert.Single(Directory.GetFiles(tmp.Path, "characters.json.corrupt*"));
    }

    [Fact]
    public void Forget_drops_the_character_and_keep_only_keeps_the_chosen()
    {
        CharacterSettingsFile.SaveChanges(Path, [CharacterSettingChange.Hide(Main, true), CharacterSettingChange.Hide(Alt, true)], new Dictionary<ulong, CharacterSettings>());
        var forgotten = CharacterSettingsFile.SaveChanges(Path, [CharacterSettingChange.Forget(Main)], new Dictionary<ulong, CharacterSettings>());
        Assert.Equal([Alt], forgotten.Keys);

        var kept = CharacterSettingsFile.KeepOnly(Path, static id => id == Main);
        Assert.Empty(kept);
        Assert.Empty(CharacterSettingsFile.Load(Path));
    }

    [Fact]
    public void Migration_moves_the_dalamud_settings_into_the_file()
    {
        var legacy = Legacy(
            shield: new() { [Main] = true },
            noticed: new() { [Main] = ["eden", "bozja"] },
            why: new() { [Alt] = ["eden"] });

        var merged = CharacterSettingsFile.MergeLegacy(Path, legacy);

        Assert.True(merged[Main].SpoilerShield);
        Assert.Equal(["bozja", "eden"], merged[Main].PayoffGatesNoticed);
        Assert.Equal(["eden"], merged[Alt].PayoffWhyOpen);
        Assert.Equal(merged.Keys.Order(), CharacterSettingsFile.Load(Path).Keys.Order());
    }

    [Fact]
    public void Migration_never_overrides_what_the_file_already_says()
    {
        // Another client moved the settings first and the player changed the override since; a stale copy of the
        // Dalamud settings (saved again by an older client) must not bring the old value back.
        CharacterSettingsFile.SaveChanges(Path, [CharacterSettingChange.Spoiler(Main, false), CharacterSettingChange.Noticed(Main, "eden")], new Dictionary<ulong, CharacterSettings>());

        var merged = CharacterSettingsFile.MergeLegacy(Path, Legacy(shield: new() { [Main] = true }, noticed: new() { [Main] = ["bozja"] }));

        Assert.False(merged[Main].SpoilerShield);
        Assert.Equal(["eden", "bozja"], merged[Main].PayoffGatesNoticed);
    }

    [Fact]
    public void Migration_run_twice_changes_nothing_more()
    {
        var legacy = Legacy(shield: new() { [Alt] = false }, noticed: new() { [Alt] = ["eden"] });
        CharacterSettingsFile.MergeLegacy(Path, legacy);
        var first = File.ReadAllText(Path);

        CharacterSettingsFile.MergeLegacy(Path, legacy);

        Assert.Equal(first, File.ReadAllText(Path));
    }

    [Fact]
    public void A_hand_edited_null_entry_or_list_reads_as_empty()
    {
        File.WriteAllText(tmp.File("characters.json"), """{ "1": null, "2": { "payoffGatesNoticed": null, "hidden": true } }""");

        var loaded = CharacterSettingsFile.Load(tmp.File("characters.json"));

        Assert.Equal([Alt], loaded.Keys);
        Assert.Empty(loaded[Alt].PayoffGatesNoticed);
    }
}
