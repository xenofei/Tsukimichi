using System.Text.Json;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Tests.Ui.Themes;

/// <summary>
/// Share codes (feature plan v7 T12; docs/design/v7/ui/spec-1.17.md §C): the encoder and decoder agree with the approved
/// Python reference (docs/design/v7/ui/1.17/sharecode.py) on every vector <c>tools/themes/sharecode_vectors.py</c> wrote to
/// <c>Fixtures/sharecode-vectors.json</c>, character for character; the spec's examples; tolerant reading; the checksum
/// catching every single-character typo; ids from a newer build named and left out; what applying a code saves and
/// changes, which a code that does not read never does; and the receiver's high contrast, which a code never changes.
/// </summary>
public sealed class ShareCodeTests
{
    private static readonly Lazy<JsonElement> Vectors = new(static () =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sharecode-vectors.json"))).RootElement);

    private static readonly string[] Examples = ["TM1-8003-0", "TM1-2G45-0", "TM1-202C-000C-02C", "TM1-AD24-0000-028"];

    // ------------------------------------------------------------------ the reference's vectors

    [Fact]
    public void The_vectors_use_the_state_order_of_the_code()
    {
        var states = Vectors.Value.GetProperty("states").EnumerateArray().Select(static s => s.GetString()).ToArray();
        Assert.Equal(AppearanceStates.All.Select(AppearanceStates.Key), states);
    }

    [Fact]
    public void Every_reference_code_is_written_character_for_character()
    {
        var encode = Vectors.Value.GetProperty("encode");
        Assert.True(encode.GetArrayLength() > 700, "expected the whole encode table");
        foreach (var vector in encode.EnumerateArray())
        {
            var look = new ShareLook(vector.GetProperty("theme").GetInt32(), vector.GetProperty("palette").GetInt32(), vector.GetProperty("frames").GetInt32(), vector.GetProperty("hc").GetBoolean(), 0);
            if (vector.GetProperty("picks") is { ValueKind: JsonValueKind.Array } picks)
            {
                var i = 0;
                foreach (var pick in picks.EnumerateArray())
                {
                    look = look.WithPick(AppearanceStates.All[i++], pick.GetInt32());
                }
            }

            Assert.Equal(vector.GetProperty("code").GetString(), ShareCode.Encode(look));
        }
    }

    [Fact]
    public void Every_reference_text_reads_as_the_reference_reads_it()
    {
        var decode = Vectors.Value.GetProperty("decode");
        Assert.True(decode.GetArrayLength() > 3000, "expected the whole decode table");
        var offenders = new List<string>();
        foreach (var vector in decode.EnumerateArray())
        {
            var text = vector.GetProperty("text").GetString()!;
            var read = ShareCode.Decode(text);
            var expected = Describe(vector);
            var actual = Describe(read);
            if (expected != actual)
            {
                offenders.Add($"\"{text}\": reference {expected}, ShareCode {actual}");
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders.Take(20)));
    }

    /// <summary>A reference result in one comparable line.</summary>
    private static string Describe(JsonElement vector)
    {
        if (vector.TryGetProperty("error", out var error))
        {
            var version = vector.TryGetProperty("version", out var v) ? v.GetInt32() : -1;
            return error.GetString() == "newer" ? $"newer v{version}" : error.GetString()!;
        }

        var picks = string.Join(',', vector.GetProperty("picks").EnumerateArray().Select(static p => p.GetInt32()));
        var unknown = string.Join(',', vector.GetProperty("unknown").EnumerateArray().Select(static u => $"{u[0].GetString()}={u[1].GetInt32()}"));
        return $"ok t{vector.GetProperty("theme").GetInt32()} p{vector.GetProperty("palette").GetInt32()} f{vector.GetProperty("frames").GetInt32()} hc{vector.GetProperty("hc").GetBoolean()} [{picks}] [{unknown}]";
    }

    /// <summary>The decoder's result in the reference's terms (an empty text is the reference's "unreadable").</summary>
    private static string Describe(ShareCodeRead read)
    {
        switch (read.Status)
        {
            case ShareCodeStatus.Empty:
            case ShareCodeStatus.Unreadable:
                return "unreadable";
            case ShareCodeStatus.Checksum:
                return "checksum";
            case ShareCodeStatus.Newer:
                return $"newer v{read.Version}";
        }

        var look = read.Look;
        var picks = string.Join(',', Enumerable.Range(0, AppearanceStates.Count).Select(look.Pick));
        var unknown = string.Join(',', read.Unknown.Select(static u => $"{FieldName(u)}={u.Id}"));
        return $"ok t{look.Theme} p{look.Palette} f{look.Frames} hc{look.HighContrast} [{picks}] [{unknown}]";
    }

    private static string FieldName(ShareCodeOmission omission) => omission.Field switch
    {
        ShareCodeField.Theme => "theme",
        ShareCodeField.Palette => "palette",
        ShareCodeField.Frames => "frames",
        _ => AppearanceStates.Key(omission.State),
    };

    // ------------------------------------------------------------------ the spec's examples

    [Fact]
    public void The_spec_examples_are_written_as_the_spec_shows_them()
    {
        Assert.Equal("TM1-8003-0", ShareCode.Encode(new ShareLook((int)ThemeId.IshgardGlass, 0, 0, false, 0)));
        Assert.Equal("TM1-2G45-0", ShareCode.Encode(new ShareLook((int)ThemeId.Medallion, ShareCode.PaletteWire(PaletteId.KuganeLacquer), 0, true, 0)));
        Assert.Equal("TM1-202C-000C-02C", ShareCode.Encode(new ShareLook((int)ThemeId.Medallion, 0, 0, false, 0)
            .WithPick(QuestState.Ready, (int)GlyphSetId.AetherCrystal)
            .WithPick(QuestState.Completed, (int)GlyphSetId.AetherCrystal)));
        Assert.Equal("TM1-AD24-0000-028", ShareCode.Encode(new ShareLook((int)ThemeId.Orrery, ShareCode.PaletteWire(PaletteId.Dawn), (int)FrameKitId.Astrolabe, false, 0)
            .WithPick(QuestState.Ready, (int)GlyphSetId.Medallion)));
    }

    [Fact]
    public void A_theme_alone_is_6_characters_and_a_mix_12()
    {
        Assert.All(Examples, static code => Assert.StartsWith("TM1-", code, StringComparison.Ordinal));
        Assert.Equal(ShareCode.PresetLength, ShareCode.Decode("TM1-8003-0").Length);
        Assert.Equal(ShareCode.MixLength, ShareCode.Decode("TM1-202C-000C-02C").Length);
    }

    [Fact]
    public void The_codes_written_use_Crockford_letters_only()
    {
        foreach (var code in Examples)
        {
            Assert.DoesNotContain(code[2..], static ch => ch is 'I' or 'L' or 'O' or 'U');
        }
    }

    // ------------------------------------------------------------------ reading

    [Theory]
    [InlineData("TM1-202C-000C-02C")]
    [InlineData("tm1-202c-000c-02c")]
    [InlineData("TM1202C000C02C")]
    [InlineData("  TM1 202C 000C 02C  ")]
    [InlineData("1-202C-000C-02C")]
    [InlineData("TMl-2O2C-OOOC-O2C")]
    [InlineData("TMI-202C-000C-02C")]
    [InlineData("TM1-202C-\t000C-\n02C")]
    public void Reading_ignores_case_spaces_dashes_and_a_missing_TM_and_reads_O_as_0_and_I_or_L_as_1(string text)
    {
        var read = ShareCode.Decode(text);
        Assert.True(read.Ok);
        Assert.Equal((int)ThemeId.Medallion, read.Look.Theme);
        Assert.Equal((int)GlyphSetId.AetherCrystal, read.Look.Pick(QuestState.Ready));
        Assert.Equal((int)GlyphSetId.AetherCrystal, read.Look.Pick(QuestState.Completed));
        Assert.Equal(0, read.Look.Pick(QuestState.Blocked));
        Assert.Empty(read.Unknown);
    }

    [Fact]
    public void Every_single_character_typo_of_the_examples_is_caught()
    {
        foreach (var code in Examples)
        {
            var flat = code.Replace("-", string.Empty, StringComparison.Ordinal);
            for (var i = 2; i < flat.Length; i++)
            {
                foreach (var ch in ShareCode.Alphabet)
                {
                    if (ch != flat[i])
                    {
                        var typo = string.Concat(flat.AsSpan(0, i), ch.ToString(), flat.AsSpan(i + 1));
                        Assert.False(ShareCode.Decode(typo).Ok, typo);
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(null, ShareCodeStatus.Empty)]
    [InlineData("", ShareCodeStatus.Empty)]
    [InlineData(" - TM - ", ShareCodeStatus.Empty)]
    [InlineData("TMU-8003-0", ShareCodeStatus.Unreadable)]
    [InlineData("TM1-80!3-0", ShareCodeStatus.Unreadable)]
    [InlineData("TM0-8003-0", ShareCodeStatus.Unreadable)]
    [InlineData("TM1", ShareCodeStatus.Checksum)]
    [InlineData("TM1-80", ShareCodeStatus.Checksum)]
    [InlineData("TM1-8003", ShareCodeStatus.Checksum)]
    [InlineData("TM1-8003-1", ShareCodeStatus.Checksum)]
    [InlineData("TM1-202C-000C-02", ShareCodeStatus.Checksum)]
    [InlineData("TM2-8003-0", ShareCodeStatus.Newer)]
    public void A_text_that_does_not_read_says_why_and_carries_no_look(string? text, ShareCodeStatus status)
    {
        var read = ShareCode.Decode(text);
        Assert.Equal(status, read.Status);
        Assert.False(read.Ok);
        Assert.Equal(default, read.Look);
        Assert.Empty(read.Unknown);
    }

    [Fact]
    public void Zero_padding_after_the_checksum_still_reads()
    {
        Assert.True(ShareCode.Decode("TM1-8003-00").Ok);
        Assert.False(ShareCode.Decode("TM1-8003-01").Ok);
    }

    [Fact]
    public void A_newer_format_names_its_version()
    {
        var read = ShareCode.Decode("TM3-8003-0");
        Assert.Equal(ShareCodeStatus.Newer, read.Status);
        Assert.Equal(3, read.Version);
        Assert.True(read.Prefixed);
    }

    [Fact]
    public void Ids_from_a_newer_build_are_named_and_the_rest_still_reads()
    {
        // The mock's newer-build code: Ready from set 9, Completed from Aether Crystal.
        var read = ShareCode.Decode("TM1-2034-000C-0DJ");
        Assert.True(read.Ok);
        Assert.Equal([new ShareCodeOmission(ShareCodeField.State, QuestState.Ready, 9, Registered: false)], read.Unknown);
        Assert.Equal(0, read.Look.Pick(QuestState.Ready));
        Assert.Equal((int)GlyphSetId.AetherCrystal, read.Look.Pick(QuestState.Completed));

        var everything = ShareCode.Decode(ShareCode.Encode(new ShareLook(12, 9, 10, true, 0).WithPick(QuestState.Blocked, 15)));
        Assert.True(everything.Ok);
        Assert.Equal(
            [ShareCodeField.Theme, ShareCodeField.Palette, ShareCodeField.Frames, ShareCodeField.State],
            everything.Unknown.Select(static u => u.Field));
        Assert.Equal(new ShareLook((int)ThemeId.Medallion, 0, 0, true, 0), everything.Look);
    }

    [Fact]
    public void Reading_never_throws()
    {
        var random = new Random(1170);
        const string pool = "0123456789ABCDEFGHJKMNPQRSTVWXYZabcdefIiLlOoUu -_!?\t\nTM";
        for (var n = 0; n < 5000; n++)
        {
            var chars = new char[random.Next(0, 30)];
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = pool[random.Next(pool.Length)];
            }

            var read = ShareCode.Decode(new string(chars));
            Assert.True(Enum.IsDefined(read.Status));
            if (read.Ok)
            {
                Assert.InRange(read.Look.Theme, 1, 15);
            }
        }
    }

    // ------------------------------------------------------------------ ids and the saved appearance

    [Fact]
    public void The_codes_palette_numbers_are_pinned_and_cover_every_palette()
    {
        Assert.Equal(
            [(PaletteId.Night, 1), (PaletteId.IshgardSnow, 2), (PaletteId.Dawn, 3), (PaletteId.KuganeLacquer, 4), (PaletteId.FollowDalamud, 5)],
            new[] { PaletteId.Night, PaletteId.IshgardSnow, PaletteId.Dawn, PaletteId.KuganeLacquer, PaletteId.FollowDalamud }.Select(static p => (p, ShareCode.PaletteWire(p))));
        foreach (var palette in Enum.GetValues<PaletteId>())
        {
            var wire = ShareCode.PaletteWire(palette);
            Assert.InRange(wire, 1, 15);
            Assert.True(ShareCode.TryPaletteFromWire(wire, out var back));
            Assert.Equal(palette, back);
        }

        Assert.False(ShareCode.TryPaletteFromWire(0, out _));
        Assert.False(ShareCode.TryPaletteFromWire(6, out _));
    }

    [Fact]
    public void A_code_holds_ids_0_to_15_only()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ShareCode.Encode(new ShareLook(16, 0, 0, false, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShareCode.Encode(new ShareLook(1, -1, 0, false, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShareLook(1, 0, 0, false, 0).WithPick(QuestState.Ready, 16));
    }

    [Fact]
    public void A_saved_look_is_written_as_it_is_saved()
    {
        Assert.Equal(new ShareLook((int)ThemeId.Medallion, 0, 0, false, 0), ShareCode.LookOf(new AppearanceConfig()));
        Assert.Equal(ShareCode.Encode(new ShareLook((int)ThemeId.Medallion, 0, 0, false, 0)), ShareCode.Encode(new AppearanceConfig()));

        var config = new AppearanceConfig
        {
            Theme = "ishgard-glass",
            Palette = "night",
            Frames = "silver",
            HighContrast = true,
            Glyphs = new Dictionary<string, string> { ["ready"] = "aether-crystal", ["locked-out"] = "medallion" },
        };
        var look = ShareCode.LookOf(config);
        Assert.Equal(new ShareLook((int)ThemeId.IshgardGlass, 1, (int)FrameKitId.Silver, true, 0)
            .WithPick(QuestState.Ready, (int)GlyphSetId.AetherCrystal)
            .WithPick(QuestState.Foreclosed, (int)GlyphSetId.Medallion), look);
    }

    [Fact]
    public void Keys_a_newer_build_saved_are_written_as_from_the_theme()
    {
        var config = new AppearanceConfig
        {
            Theme = "moonfall",
            Palette = "ultraviolet",
            Frames = "obsidian",
            Glyphs = new Dictionary<string, string> { ["ready"] = "future-set", ["someday"] = "medallion" },
        };
        Assert.Equal(new ShareLook((int)ThemeId.Medallion, 0, 0, false, 0), ShareCode.LookOf(config));
    }

    public static TheoryData<string, string?, string?, bool, string?> OfferedLooks()
    {
        var data = new TheoryData<string, string?, string?, bool, string?>();
        foreach (var theme in ThemePresets.All.Where(static t => t.Offered))
        {
            foreach (var palette in PaletteChoices.All.Where(static p => p.Offered))
            {
                foreach (var kit in FrameKits.All.Where(static k => k.Offered))
                {
                    var palettePick = palette.Id == theme.Palette ? null : palette.Key;
                    var framesPick = kit.Id == theme.Frames ? null : kit.Key;
                    data.Add(theme.Key, palettePick, framesPick, false, null);
                    data.Add(theme.Key, palettePick, framesPick, true, theme.Legacy ? null : "aether-crystal");
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(OfferedLooks))]
    public void Every_offered_look_comes_back_from_its_code(string theme, string? palette, string? frames, bool highContrast, string? readyFrom)
    {
        var original = new AppearanceConfig
        {
            Theme = theme,
            Palette = palette,
            Frames = frames,
            HighContrast = highContrast,
            Glyphs = readyFrom is null ? null : new Dictionary<string, string> { ["ready"] = readyFrom, ["completed"] = "ishgard-glass" },
        };

        var read = ShareCode.Decode(ShareCode.Encode(original));
        Assert.True(read.Ok);
        var leftOut = new List<ShareCodeOmission>();
        var applied = ShareCode.Apply(new AppearanceConfig { HighContrast = highContrast }, read.Look, leftOut);
        Assert.Empty(leftOut);
        Assert.True(original.SameAs(applied), $"{AppearanceJson.Write(original)} came back as {AppearanceJson.Write(applied)}");
        Assert.Empty(ShareCode.Changes(original, applied));
    }

    // ------------------------------------------------------------------ applying

    [Fact]
    public void Applying_keeps_the_saved_version_and_a_newer_builds_fields()
    {
        var saved = AppearanceJson.Read("""{"Version":1,"Theme":"medallion","Sparkle":{"x":1}}""")!;
        var applied = ShareCode.Apply(saved, ShareCode.Decode("TM1-8003-0").Look, new List<ShareCodeOmission>());
        Assert.Equal("ishgard-glass", applied.Theme);
        Assert.Equal(1, applied.Version);
        Assert.NotNull(applied.Unknown);
        Assert.True(applied.Unknown!.ContainsKey("Sparkle"));
        Assert.Equal("medallion", saved.Theme);
    }

    [Fact]
    public void A_palette_or_frames_equal_to_the_themes_own_is_saved_as_from_the_theme()
    {
        var look = new ShareLook((int)ThemeId.IshgardGlass, ShareCode.PaletteWire(PaletteId.IshgardSnow), (int)FrameKitId.Came, false, 0);
        var applied = ShareCode.Apply(new AppearanceConfig(), look, new List<ShareCodeOmission>());
        Assert.Null(applied.Palette);
        Assert.Null(applied.Frames);
        Assert.False(AppearanceEdits.IsCustom(applied));
    }

    [Fact]
    public void Only_offered_choices_apply_and_the_rest_are_named()
    {
        foreach (var theme in ThemePresets.All)
        {
            var leftOut = new List<ShareCodeOmission>();
            var applied = ShareCode.Apply(new AppearanceConfig(), new ShareLook((int)theme.Id, 0, 0, false, 0), leftOut);
            if (theme.Offered)
            {
                Assert.Equal(theme.Key, applied.Theme);
                Assert.Empty(leftOut);
            }
            else
            {
                Assert.Equal(ThemePresets.Default.Key, applied.Theme);
                Assert.Equal([new ShareCodeOmission(ShareCodeField.Theme, QuestState.Ready, (int)theme.Id, Registered: true)], leftOut);
            }
        }

        foreach (var palette in PaletteChoices.All)
        {
            var leftOut = new List<ShareCodeOmission>();
            var applied = ShareCode.Apply(new AppearanceConfig(), new ShareLook((int)ThemeId.Medallion, ShareCode.PaletteWire(palette.Id), 0, false, 0), leftOut);
            Assert.Equal(palette.Offered, leftOut.Count == 0);
            Assert.Equal(palette.Offered && palette.Id != PaletteId.Night ? palette.Key : null, applied.Palette);
        }

        foreach (var kit in FrameKits.All)
        {
            var leftOut = new List<ShareCodeOmission>();
            var applied = ShareCode.Apply(new AppearanceConfig(), new ShareLook((int)ThemeId.Medallion, 0, (int)kit.Id, false, 0), leftOut);
            Assert.Equal(kit.Offered, leftOut.Count == 0);
            Assert.Equal(kit.Offered && kit.Id != FrameKitId.Brass ? kit.Key : null, applied.Frames);
        }

        foreach (var set in GlyphSets.All)
        {
            var leftOut = new List<ShareCodeOmission>();
            var applied = ShareCode.Apply(new AppearanceConfig(), new ShareLook((int)ThemeId.IshgardGlass, 0, 0, false, 0).WithPick(QuestState.Blocked, (int)set.Id), leftOut);
            var mixes = set.Offered && set.Mixable;
            Assert.Equal(mixes ? set.Key : null, applied.Glyphs?.GetValueOrDefault("blocked"));
            Assert.Equal(mixes ? [] : [new ShareCodeOmission(ShareCodeField.State, QuestState.Blocked, (int)set.Id, Registered: true)], leftOut);
        }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void A_code_never_changes_the_receivers_high_contrast(bool mine, bool theirs)
    {
        // The bit is still written and read, for the format's sake.
        var sender = new AppearanceConfig { Theme = "aether-crystal", HighContrast = theirs };
        var read = ShareCode.Decode(ShareCode.Encode(sender));
        Assert.Equal(theirs, read.Look.HighContrast);

        var saved = new AppearanceConfig { HighContrast = mine };
        var preview = SharePreview.Of(ShareCode.Encode(sender), saved);
        Assert.Equal(mine, preview.Result!.HighContrast);
        Assert.Equal(
            [
                new ShareChange(ShareChangeKind.Theme, QuestState.Ready, (int)ThemeId.Medallion, (int)ThemeId.AetherCrystal),
                new ShareChange(ShareChangeKind.Frames, QuestState.Ready, (int)FrameKitId.Brass, (int)FrameKitId.Silver),
            ],
            preview.Changes);

        // A code that differs only in high contrast has nothing to change.
        var same = SharePreview.Of(ShareCode.Encode(new AppearanceConfig { HighContrast = theirs }), saved);
        Assert.Equal(ShareVerdict.NothingToChange, same.Verdict(editing: false));
    }

    [Fact]
    public void Classic_is_never_a_pick()
    {
        var leftOut = new List<ShareCodeOmission>();
        var applied = ShareCode.Apply(new AppearanceConfig(), new ShareLook(1, 0, 0, false, 0).WithPick(QuestState.Ready, (int)GlyphSetId.Classic), leftOut);
        Assert.Null(applied.Glyphs);
        Assert.Single(leftOut);
        Assert.True(leftOut[0].Registered);
    }

    // ------------------------------------------------------------------ the preview

    [Fact]
    public void The_preview_lists_every_change_and_only_those()
    {
        var saved = new AppearanceConfig { Glyphs = new Dictionary<string, string> { ["completed"] = "aether-crystal" } };
        var code = ShareCode.Encode(new ShareLook((int)ThemeId.IshgardGlass, 0, 0, true, 0).WithPick(QuestState.Ready, (int)GlyphSetId.Medallion));
        var preview = SharePreview.Of(code, saved);

        Assert.Equal(ShareVerdict.Preview, preview.Verdict(editing: false));
        Assert.Equal(
            [
                new ShareChange(ShareChangeKind.Theme, QuestState.Ready, (int)ThemeId.Medallion, (int)ThemeId.IshgardGlass),
                new ShareChange(ShareChangeKind.Palette, QuestState.Ready, (int)PaletteId.Night, (int)PaletteId.IshgardSnow),
                new ShareChange(ShareChangeKind.Frames, QuestState.Ready, (int)FrameKitId.Brass, (int)FrameKitId.Came),
                new ShareChange(ShareChangeKind.State, QuestState.Ready, 0, (int)GlyphSetId.Medallion),
                new ShareChange(ShareChangeKind.State, QuestState.Completed, (int)GlyphSetId.AetherCrystal, 0),
            ],
            preview.Changes);
        Assert.Empty(preview.LeftOut);
        Assert.False(preview.Result!.HighContrast);

        // Building the preview saved nothing.
        Assert.Equal("medallion", saved.Theme);
        Assert.False(saved.HighContrast);
    }

    [Fact]
    public void The_look_in_use_has_nothing_to_change()
    {
        var saved = new AppearanceConfig { Theme = "aether-crystal", Palette = "ishgard-snow" };
        var preview = SharePreview.Of(ShareCode.Encode(saved), saved);
        Assert.True(preview.Read.Ok);
        Assert.Empty(preview.Changes);
        Assert.Equal(ShareVerdict.NothingToChange, preview.Verdict(editing: false));
    }

    [Fact]
    public void A_code_from_a_newer_build_names_what_is_left_out_and_applies_the_rest()
    {
        var preview = SharePreview.Of("TM1-2034-000C-0DJ", new AppearanceConfig());
        Assert.Equal(ShareVerdict.Preview, preview.Verdict(editing: false));
        Assert.Equal([new ShareCodeOmission(ShareCodeField.State, QuestState.Ready, 9, Registered: false)], preview.LeftOut);
        Assert.Equal([new ShareChange(ShareChangeKind.State, QuestState.Completed, 0, (int)GlyphSetId.AetherCrystal)], preview.Changes);
        Assert.Equal(new Dictionary<string, string> { ["completed"] = "aether-crystal" }, preview.Result!.Glyphs);
    }

    [Theory]
    [InlineData("", true, ShareVerdict.None)]
    [InlineData("", false, ShareVerdict.None)]
    [InlineData("TM1-202C", true, ShareVerdict.None)]
    [InlineData("TM1-202C", false, ShareVerdict.Mistyped)]
    [InlineData("TM1-202C-000C-02X", true, ShareVerdict.Mistyped)]
    [InlineData("TM1-202C-000C-02X", false, ShareVerdict.Mistyped)]
    [InlineData("TM1-U", true, ShareVerdict.Mistyped)]
    [InlineData("TM0-8003-0", true, ShareVerdict.Mistyped)]
    [InlineData("TM2-8003-0", true, ShareVerdict.Newer)]
    [InlineData("hello", true, ShareVerdict.None)]
    [InlineData("hello", false, ShareVerdict.Mistyped)]
    [InlineData("TM1-202C-000C-02C", true, ShareVerdict.Preview)]
    public void The_field_says_nothing_while_a_code_is_typed_and_explains_one_that_does_not_read(string text, bool editing, ShareVerdict verdict)
    {
        Assert.Equal(verdict, SharePreview.Of(text, new AppearanceConfig()).Verdict(editing));
    }

    [Fact]
    public void A_code_that_does_not_read_has_nothing_to_apply()
    {
        var preview = SharePreview.Of("TM1-202C-000C-02X", new AppearanceConfig());
        Assert.Null(preview.Result);
        Assert.Empty(preview.Changes);
        Assert.Empty(preview.LeftOut);
    }
}
