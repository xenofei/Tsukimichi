using System.Buffers.Binary;
using Lumina;
using Tsukimichi.Core.Discovery;
using Tsukimichi.Tests.Data;
using LuminaGameData = Lumina.GameData;

namespace Tsukimichi.Tests.Discovery;

/// <summary>
/// The one server info bar entry (plan v8 M1; spec-1.22 M1, decisions 13, 14 and 20): its words at each count, when it
/// shows by default, the 1.x migration, the moon's glyph and its fallback to the game's own icon (owner decision 8), the
/// font check that decides between them, the UIColor pick and the tooltip's lines.
/// </summary>
public sealed class ServerInfoBarTests
{
    private static readonly DtrWords Words = new("{0} Ready", "{0} here", "Nothing Ready", "Nothing here");

    [Fact]
    public void The_entry_counts_Ready_quests_or_this_zone()
    {
        Assert.Equal("12 Ready", ServerInfoBar.Text(DtrCounts.Ready, ready: 12, here: 3, showAtZero: false, Words));
        Assert.Equal("3 here", ServerInfoBar.Text(DtrCounts.Zone, ready: 12, here: 3, showAtZero: false, Words));
    }

    [Fact]
    public void At_zero_the_entry_hides_unless_Show_at_zero()
    {
        Assert.Null(ServerInfoBar.Text(DtrCounts.Ready, 0, 3, showAtZero: false, Words));
        Assert.Equal("Nothing Ready", ServerInfoBar.Text(DtrCounts.Ready, 0, 3, showAtZero: true, Words));
        Assert.Null(ServerInfoBar.Text(DtrCounts.Zone, 12, 0, showAtZero: false, Words));
        Assert.Equal("Nothing here", ServerInfoBar.Text(DtrCounts.Zone, 12, 0, showAtZero: true, Words));
    }

    [Fact]
    public void Until_the_player_chooses_it_shows_with_Umbra_and_without_the_add_on()
    {
        Assert.True(ServerInfoBar.Shown(chosen: false, show: false, umbraInstalled: true, addonInstalled: false));
        Assert.False(ServerInfoBar.Shown(chosen: false, show: true, umbraInstalled: true, addonInstalled: true));
        Assert.False(ServerInfoBar.Shown(chosen: false, show: true, umbraInstalled: false, addonInstalled: false));

        // Once chosen it is the player's, whatever Umbra does.
        Assert.False(ServerInfoBar.Shown(chosen: true, show: false, umbraInstalled: true, addonInstalled: false));
        Assert.True(ServerInfoBar.Shown(chosen: true, show: true, umbraInstalled: false, addonInstalled: true));
    }

    [Fact]
    public void Players_who_had_the_Nearby_entry_keep_it_counting_this_zone()
    {
        var hadIt = new DiscoverySettings { ShowDtrEntry = true };
        Assert.True(ServerInfoBar.Migrate(hadIt, fileExisted: true));
        Assert.True(hadIt.DtrEntryChosen);
        Assert.True(hadIt.ShowDtrEntry);
        Assert.Equal(DtrCounts.Zone, hadIt.DtrCounts);
        Assert.Equal(DiscoverySettings.CurrentServerInfoBarSchema, hadIt.ServerInfoBarSchema);

        // Turned off in 1.x: it stays off, as their choice, and counts Ready if they turn it on.
        var turnedOff = new DiscoverySettings { ShowDtrEntry = false };
        Assert.True(ServerInfoBar.Migrate(turnedOff, fileExisted: true));
        Assert.True(turnedOff.DtrEntryChosen);
        Assert.False(turnedOff.ShowDtrEntry);
        Assert.Equal(DtrCounts.Ready, turnedOff.DtrCounts);

        // A fresh install chooses nothing: Ready, and the default rule.
        var fresh = new DiscoverySettings();
        Assert.True(ServerInfoBar.Migrate(fresh, fileExisted: false));
        Assert.False(fresh.DtrEntryChosen);
        Assert.Equal(DtrCounts.Ready, fresh.DtrCounts);

        // Once migrated, never again: a later choice of Ready stays.
        hadIt.DtrCounts = DtrCounts.Ready;
        Assert.False(ServerInfoBar.Migrate(hadIt, fileExisted: true));
        Assert.Equal(DtrCounts.Ready, hadIt.DtrCounts);
    }

    [Fact]
    public void The_moon_is_the_glyph_lit_on_the_left_or_the_games_icon()
    {
        Assert.Equal("◐", ServerInfoBar.Moon);
        Assert.Equal(0x25D0, char.ConvertToUtf32(ServerInfoBar.Moon, 0));
        Assert.Equal(DtrMoonKind.Glyph, ServerInfoBar.MoonKind(fontHasMoon: true));
        Assert.Equal(DtrMoonKind.Icon, ServerInfoBar.MoonKind(fontHasMoon: false));

        // BitmapFontIcon.Meteor, 126 in gfdata.gfd: a gold ring with a halo, the nearest the game has to a moon.
        Assert.Equal(126u, ServerInfoBar.FallbackIcon);
    }

    [Fact]
    public void The_font_check_finds_a_glyph_in_a_font_table()
    {
        var fdt = Fdt('A', '●', 0x25D0);
        Assert.True(ServerInfoBar.FontHas(fdt, 'A'));
        Assert.True(ServerInfoBar.FontHas(fdt, 0x25CF));
        Assert.True(ServerInfoBar.FontHas(fdt, 0x25D0));
        Assert.False(ServerInfoBar.FontHas(fdt, 0x263E));

        var without = Fdt('A', '●');
        Assert.False(ServerInfoBar.FontHas(without, 0x25D0));

        // Anything that is not a font table reads as no glyph, never a throw.
        Assert.False(ServerInfoBar.FontHas([], 0x25D0));
        Assert.False(ServerInfoBar.FontHas(new byte[64], 0x25D0));
        var badTable = Fdt('A');
        BinaryPrimitives.WriteUInt32LittleEndian(badTable.AsSpan(8), 9999);
        Assert.False(ServerInfoBar.FontHas(badTable, 'A'));
    }

    [Fact]
    public void The_font_key_is_the_characters_UTF8_bytes()
    {
        Assert.Equal(0x41u, ServerInfoBar.Utf8Key('A'));
        Assert.Equal(0xE29790u, ServerInfoBar.Utf8Key(0x25D0));
        Assert.Equal(0xE298BEu, ServerInfoBar.Utf8Key(0x263E));
    }

    [GameDataFact]
    public void The_games_UI_font_lacks_both_moons_so_the_entry_uses_the_games_icon()
    {
        // Checked in the game's own files (game 2026.09): AXIS has neither "◐" nor 1.x's "☾", but has "●".
        using var game = new LuminaGameData(Environment.GetEnvironmentVariable(GameDataFactAttribute.EnvVar)!, new LuminaOptions { PanicOnSheetChecksumMismatch = false });
        var fdt = game.GetFile("common/font/AXIS_12.fdt")!.Data;
        Assert.False(ServerInfoBar.FontHas(fdt, ServerInfoBar.MoonCodePoint));
        Assert.False(ServerInfoBar.FontHas(fdt, 0x263E));
        Assert.True(ServerInfoBar.FontHas(fdt, 0x25CF));
        Assert.Equal(DtrMoonKind.Icon, ServerInfoBar.MoonKind(ServerInfoBar.FontHas(fdt, ServerInfoBar.MoonCodePoint)));

        // The fallback icon exists in the game's icon table and is drawn (a non-empty cell).
        var gfd = game.GetFile("common/font/gfdata.gfd")!.Data;
        var count = BinaryPrimitives.ReadInt32LittleEndian(gfd.AsSpan(8));
        var found = false;
        for (var i = 0; i < count; i++)
        {
            var entry = gfd.AsSpan(16 + (i * 16), 16);
            if (BinaryPrimitives.ReadUInt16LittleEndian(entry) == ServerInfoBar.FallbackIcon)
            {
                found = BinaryPrimitives.ReadUInt16LittleEndian(entry[6..]) > 0 && BinaryPrimitives.ReadUInt16LittleEndian(entry[8..]) > 0;
            }
        }

        Assert.True(found);
    }

    [Fact]
    public void The_moons_colour_is_the_nearest_UIColor_row()
    {
        (ushort, uint)[] rows =
        [
            (1, 0xFFFFFFFFu), // white
            (2, 0xEEC86EFFu), // gold
            (3, 0x6F8FD0FFu), // tide
            (4, 0xEEC86E00u), // a transparent gold row: never picked
        ];
        Assert.Equal((ushort)2, ServerInfoBar.NearestUiColor(0xE6C878, rows));
        Assert.Equal((ushort)3, ServerInfoBar.NearestUiColor(0x7090D0, rows));
        Assert.Equal((ushort)1, ServerInfoBar.NearestUiColor(0xF0F0F0, rows));
        Assert.Null(ServerInfoBar.NearestUiColor(0xFFFFFF, []));
    }

    [Fact]
    public void The_tooltip_is_the_quick_cards_lines_without_empty_ones()
    {
        var lines = ServerInfoBar.TooltipLines(
            "Tsukimichi",
            "Up next: The Long Road to Xak Tural",
            "Step 3: Speak with Erenville. · Shaaloani",
            "12 quests are Ready on WHM · 3 can start here",
            "Journal 27/30 · 3 slots left",
            ["The Rising ends in 2 days", " "],
            "Click: open Tsukimichi · Right-click: Tonight");
        Assert.Equal(
            [
                "Tsukimichi",
                "Up next: The Long Road to Xak Tural",
                "Step 3: Speak with Erenville. · Shaaloani",
                "12 quests are Ready on WHM · 3 can start here",
                "Journal 27/30 · 3 slots left",
                "The Rising ends in 2 days",
                "Click: open Tsukimichi · Right-click: Tonight",
            ],
            lines);

        // No Up next, no journal, nothing ending: the title, the counts and the clicks.
        Assert.Equal(["Tsukimichi", "Nothing is Ready on WHM", "Click"], ServerInfoBar.TooltipLines("Tsukimichi", null, "step without a quest", "Nothing is Ready on WHM", null, [], "Click"));
    }

    /// <summary>A minimal game font table holding <paramref name="codePoints"/>, sorted as the game sorts them.</summary>
    private static byte[] Fdt(params int[] codePoints)
    {
        var keys = codePoints.Select(ServerInfoBar.Utf8Key).Order().ToArray();
        const int table = 0x20;
        var data = new byte[table + 0x20 + (keys.Length * 16)];
        "fcsv0100"u8.CopyTo(data);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), table);
        "fthd"u8.CopyTo(data.AsSpan(table));
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(table + 4), (uint)keys.Length);
        for (var i = 0; i < keys.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(table + 0x20 + (i * 16)), keys[i]);
        }

        return data;
    }
}
