using System.Numerics;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Tests.Ui;

/// <summary>The moon icon's rules (spec-1.22 H1): place per screen size and clamping, lock, the first-run hint, Hide with Undo, hiding, the dot, the card.</summary>
public class MoonIconTests
{
    private static readonly Vector2 FullHd = new(1920f, 1080f);
    private const float Diameter = 40f;
    private const float Margin = MoonIconRules.EdgeMarginLogical;

    [Fact]
    public void Sizes_are_32_40_and_48_with_40_for_anything_else()
    {
        Assert.Equal(32f, MoonIconRules.SizeLogical(MoonIconSize.Small));
        Assert.Equal(40f, MoonIconRules.SizeLogical(MoonIconSize.Medium));
        Assert.Equal(48f, MoonIconRules.SizeLogical(MoonIconSize.Large));
        Assert.Equal(40f, MoonIconRules.SizeLogical((MoonIconSize)9));
    }

    [Fact]
    public void A_place_is_saved_per_screen_size_and_others_keep_theirs()
    {
        var places = new Dictionary<string, MoonIconPlace>();
        var qhd = new Vector2(2560f, 1440f);
        MoonIconRules.Store(places, FullHd, new Vector2(300f, 400f), Diameter, 1f);
        MoonIconRules.Store(places, qhd, new Vector2(2000f, 1000f), Diameter, 1f);

        Assert.Equal(2, places.Count);
        Assert.Equal(new Vector2(300f, 400f), MoonIconRules.Resolve(places, FullHd, Diameter, 1f, ToolbarClearance.None));
        Assert.Equal(new Vector2(2000f, 1000f), MoonIconRules.Resolve(places, qhd, Diameter, 1f, ToolbarClearance.None));
        Assert.True(places.ContainsKey("1920x1080"));
        Assert.True(places.ContainsKey("2560x1440"));
    }

    [Fact]
    public void A_screen_size_never_placed_on_gets_the_default_on_screen()
    {
        var places = new Dictionary<string, MoonIconPlace> { ["2560x1440"] = new(2500f, 1400f) };
        var at = MoonIconRules.Resolve(places, FullHd, Diameter, 1f, ToolbarClearance.None);
        Assert.Equal(MoonIconRules.DefaultCentre(FullHd, Diameter, 1f), at);
        Assert.InRange(at.X, Margin + (Diameter / 2f), FullHd.X - Margin - (Diameter / 2f));
        Assert.InRange(at.Y, Margin + (Diameter / 2f), FullHd.Y - Margin - (Diameter / 2f));
        Assert.Equal(MoonIconRules.DefaultCentre(FullHd, Diameter, 1f), MoonIconRules.Resolve(null, FullHd, Diameter, 1f, ToolbarClearance.None));
    }

    [Theory]
    [InlineData(-500f, -500f, 28f, 28f)]
    [InlineData(5000f, 5000f, 1892f, 1052f)]
    [InlineData(10f, 540f, 28f, 540f)]
    [InlineData(960f, 1079f, 960f, 1052f)]
    public void The_icon_stays_8_px_inside_the_screen(float x, float y, float expectedX, float expectedY)
    {
        var at = MoonIconRules.Clamp(new Vector2(x, y), Diameter, FullHd, Margin, ToolbarClearance.None);
        Assert.Equal(new Vector2(expectedX, expectedY), at);
    }

    [Fact]
    public void A_saved_place_off_a_smaller_screen_is_brought_back_on()
    {
        // The same size key saved on a bigger window, or a corrupt file: never off screen.
        var places = new Dictionary<string, MoonIconPlace> { ["1920x1080"] = new(4000f, -300f) };
        var at = MoonIconRules.Resolve(places, FullHd, Diameter, 1f, ToolbarClearance.None);
        Assert.Equal(new Vector2(1892f, 28f), at);

        var broken = new Dictionary<string, MoonIconPlace> { ["1920x1080"] = new(float.NaN, float.PositiveInfinity) };
        var fixedUp = MoonIconRules.Resolve(broken, FullHd, Diameter, 1f, ToolbarClearance.None);
        Assert.True(float.IsFinite(fixedUp.X) && float.IsFinite(fixedUp.Y));
    }

    [Fact]
    public void The_margin_and_the_place_follow_the_ui_scale()
    {
        var at = MoonIconRules.Resolve(null, FullHd, 80f, 2f, ToolbarClearance.None);
        Assert.Equal((MoonIconRules.DefaultYLogical * 2f) + 40f, at.Y);
        var corner = MoonIconRules.Clamp(Vector2.Zero, 80f, FullHd, Margin * 2f, ToolbarClearance.None);
        Assert.Equal(new Vector2(56f, 56f), corner);
    }

    [Fact]
    public void Umbra_top_bar_pushes_the_icon_below_it_and_the_saved_place_is_kept()
    {
        var places = new Dictionary<string, MoonIconPlace>();
        MoonIconRules.Store(places, FullHd, new Vector2(500f, 30f), Diameter, 1f);
        var top = new ToolbarClearance(ToolbarEdge.Top, 32f);
        var under = MoonIconRules.Resolve(places, FullHd, Diameter, 1f, top);

        // The top edge at bar height + 8.
        Assert.Equal(32f + Margin + (Diameter / 2f), under.Y);
        Assert.Equal(500f, under.X);

        // Never written back: without Umbra the player's place returns.
        Assert.Equal(30f, places["1920x1080"].Y, 3);
        Assert.Equal(new Vector2(500f, 30f), MoonIconRules.Resolve(places, FullHd, Diameter, 1f, ToolbarClearance.None));
    }

    [Fact]
    public void Umbra_bottom_bar_keeps_the_bottom_edge_above_it_and_none_or_floating_changes_nothing()
    {
        var bottom = new ToolbarClearance(ToolbarEdge.Bottom, 40f);
        var at = MoonIconRules.Clamp(new Vector2(500f, 1070f), Diameter, FullHd, Margin, bottom);
        Assert.Equal(FullHd.Y - 40f - Margin - (Diameter / 2f), at.Y);

        var floating = new ToolbarClearance(ToolbarEdge.None, 40f);
        Assert.Equal(new Vector2(500f, 30f), MoonIconRules.Clamp(new Vector2(500f, 30f), Diameter, FullHd, Margin, floating));
        Assert.Equal(0f, new ToolbarClearance(ToolbarEdge.Top, float.NaN).Room);
    }

    [Fact]
    public void A_drop_is_saved_on_screen_but_without_the_toolbar_clearance()
    {
        var places = new Dictionary<string, MoonIconPlace>();
        var saved = MoonIconRules.Store(places, FullHd, new Vector2(-40f, 12f), Diameter, 1f);
        Assert.Equal(new MoonIconPlace(28f, 28f), saved);
    }

    [Fact]
    public void A_press_is_a_drag_only_past_the_4_px_dead_zone()
    {
        Assert.False(MoonIconRules.IsDrag(new Vector2(3f, 2f), MoonIconRules.DeadZoneLogical));
        Assert.False(MoonIconRules.IsDrag(new Vector2(4f, 0f), MoonIconRules.DeadZoneLogical));
        Assert.True(MoonIconRules.IsDrag(new Vector2(3f, 3f), MoonIconRules.DeadZoneLogical));
        Assert.True(MoonIconRules.IsDrag(new Vector2(0f, -10f), MoonIconRules.DeadZoneLogical));
        Assert.False(MoonIconRules.IsDrag(new Vector2(float.NaN, 0f), MoonIconRules.DeadZoneLogical));

        // At UI scale 2 the dead zone is 8 px.
        Assert.False(MoonIconRules.IsDrag(new Vector2(6f, 0f), MoonIconRules.DeadZoneLogical * 2f));
    }

    [Theory]
    // Shown, hidden by the player, logged out.
    [InlineData(true, false, false, false, true, true)]
    [InlineData(false, false, false, false, true, false)]
    [InlineData(true, false, false, false, false, false)]
    // The defaults: hidden in cutscenes and Group Pose, shown in duties.
    [InlineData(true, true, false, false, true, false)]
    [InlineData(true, false, true, false, true, false)]
    [InlineData(true, false, false, true, true, true)]
    public void It_shows_by_the_default_hiding_rules(bool enabled, bool cutscene, bool groupPose, bool duty, bool loggedIn, bool shows)
    {
        var context = new MoonIconContext(loggedIn, cutscene, groupPose, duty);
        Assert.Equal(shows, MoonIconRules.Shows(enabled, MoonIconHideRules.Default, context));
    }

    [Fact]
    public void Each_hiding_option_works_on_its_own()
    {
        var cutscene = new MoonIconContext(true, true, false, false);
        var groupPose = new MoonIconContext(true, false, true, false);
        var duty = new MoonIconContext(true, false, false, true);
        var none = new MoonIconHideRules(false, false, false);
        Assert.True(MoonIconRules.Shows(true, none, cutscene));
        Assert.True(MoonIconRules.Shows(true, none, groupPose));
        Assert.True(MoonIconRules.Shows(true, none, duty));

        var duties = new MoonIconHideRules(false, false, true);
        Assert.False(MoonIconRules.Shows(true, duties, duty));
        Assert.True(MoonIconRules.Shows(true, duties, cutscene));
    }

    [Fact]
    public void Needs_you_wins_the_dot_over_an_update()
    {
        Assert.Equal(MoonIconDot.None, MoonIconRules.Dot(false, null));
        Assert.Equal(MoonIconDot.None, MoonIconRules.Dot(false, string.Empty));
        Assert.Equal(MoonIconDot.UpdateReady, MoonIconRules.Dot(false, "1.23.0"));
        Assert.Equal(MoonIconDot.NeedsYou, MoonIconRules.Dot(true, null));
        Assert.Equal(MoonIconDot.NeedsYou, MoonIconRules.Dot(true, "1.23.0"));
    }

    [Fact]
    public void The_first_run_hint_shows_once_for_8_seconds()
    {
        var hint = new MoonIconHint(seen: false);
        Assert.False(hint.Update(0.0, iconShown: false, clicked: false, out var seen));
        Assert.False(seen);

        Assert.True(hint.Update(1.0, iconShown: true, clicked: false, out seen));
        Assert.True(seen);
        Assert.True(hint.Seen);
        Assert.True(hint.Update(8.9, iconShown: true, clicked: false, out seen));
        Assert.False(seen);
        Assert.False(hint.Update(9.0, iconShown: true, clicked: false, out _));
        Assert.False(hint.Update(20.0, iconShown: true, clicked: false, out _));

        // Saved as seen: never again, after a reload too.
        var again = new MoonIconHint(hint.Seen);
        Assert.False(again.Update(30.0, iconShown: true, clicked: false, out seen));
        Assert.False(seen);
    }

    [Fact]
    public void A_click_ends_the_first_run_hint()
    {
        var hint = new MoonIconHint(seen: false);
        Assert.True(hint.Update(0.0, true, false, out _));
        Assert.False(hint.Update(0.5, true, clicked: true, out _));
        Assert.False(hint.Update(1.0, true, false, out _));
        Assert.True(hint.Seen);
    }

    [Fact]
    public void Hide_offers_Undo_for_8_seconds_and_hover_stops_the_clock()
    {
        var undo = new MoonIconHideUndo();
        Assert.False(undo.Showing);
        Assert.False(undo.TryUndo());

        undo.Hidden(10.0);
        Assert.True(undo.Tick(15.0, paused: false, iconEnabled: false));
        Assert.True(undo.Tick(20.0, paused: true, iconEnabled: false));
        Assert.True(undo.Tick(22.9, paused: false, iconEnabled: false));
        Assert.True(undo.TryUndo());
        Assert.False(undo.Showing);
        Assert.False(undo.TryUndo());
    }

    [Fact]
    public void Undo_is_gone_after_8_seconds()
    {
        var undo = new MoonIconHideUndo();
        undo.Hidden(0.0);
        Assert.False(undo.Tick(8.01, paused: false, iconEnabled: false));
        Assert.False(undo.TryUndo());
    }

    [Fact]
    public void Showing_the_icon_another_way_takes_the_Undo_down()
    {
        var undo = new MoonIconHideUndo();
        undo.Hidden(0.0);
        Assert.False(undo.Tick(1.0, paused: false, iconEnabled: true));
        Assert.False(undo.TryUndo());
    }

    [Fact]
    public void The_card_opens_on_the_side_with_room_and_never_covers_the_icon()
    {
        var screen = new ScreenRect(Vector2.Zero, FullHd);
        var card = new Vector2(300f, 200f);

        var leftIcon = new ScreenRect(new Vector2(100f, 100f), new Vector2(140f, 140f));
        Assert.Equal(new Vector2(148f, 100f), MoonIconRules.CardPlace(leftIcon, card, screen, 8f));

        var rightIcon = new ScreenRect(new Vector2(1800f, 100f), new Vector2(1840f, 140f));
        Assert.Equal(new Vector2(1492f, 100f), MoonIconRules.CardPlace(rightIcon, card, screen, 8f));

        // Low on the screen: still beside the icon, lifted to stay on screen.
        var lowIcon = new ScreenRect(new Vector2(100f, 1020f), new Vector2(140f, 1060f));
        var low = MoonIconRules.CardPlace(lowIcon, card, screen, 8f);
        Assert.Equal(880f, low.Y);

        foreach (var icon in (ScreenRect[])[leftIcon, rightIcon, lowIcon])
        {
            var at = MoonIconRules.CardPlace(icon, card, screen, 8f);
            Assert.True(ScreenRect.Intersect(icon, ScreenRect.FromSize(at, card)).IsEmpty);
        }

        // A screen too narrow for either side: below the icon.
        var narrow = new ScreenRect(Vector2.Zero, new Vector2(400f, 1080f));
        var middle = new ScreenRect(new Vector2(180f, 100f), new Vector2(220f, 140f));
        var below = MoonIconRules.CardPlace(middle, card, narrow, 8f);
        Assert.Equal(148f, below.Y);
        Assert.True(ScreenRect.Intersect(middle, ScreenRect.FromSize(below, card)).IsEmpty);
    }

    [Fact]
    public void Hover_rises_2_px_at_Full_1_at_Quiet_and_never_under_Reduce_motion_or_Plain()
    {
        Assert.Equal(2f, MoonIconHover.RiseLogical(Flair.Full, false));
        Assert.Equal(1f, MoonIconHover.RiseLogical(Flair.Quiet, false));
        Assert.Equal(0f, MoonIconHover.RiseLogical(Flair.Plain, false));
        Assert.Equal(0f, MoonIconHover.RiseLogical(Flair.Full, true));
        Assert.Equal(1f, MoonIconHover.GlowStrength(Flair.Full));
        Assert.Equal(0.7f, MoonIconHover.GlowStrength(Flair.Quiet));
        Assert.Equal(0f, MoonIconHover.GlowStrength(Flair.Plain));
        Assert.False(MoonIconHover.Shadow(Flair.Plain));
        Assert.Equal((2f, 2f, 0.5f), MoonIconHover.ShadowAt(0f));
        var lifted = MoonIconHover.ShadowAt(1f);
        Assert.Equal(4f, lifted.Offset);
        Assert.Equal(3.6f, lifted.Blur, 3);
        Assert.Equal(0.42f, lifted.Alpha, 3);
    }

    [Fact]
    public void Under_Reduce_motion_the_glow_is_there_at_once()
    {
        Assert.Equal(1f, MoonIconHover.Step(0f, true, 0.001f, Flair.Full, reduceMotion: true));
        Assert.Equal(0f, MoonIconHover.Step(1f, false, 0.001f, Flair.Full, reduceMotion: true));
        var eased = MoonIconHover.Step(0f, true, 0.016f, Flair.Full, reduceMotion: false);
        Assert.InRange(eased, 0.01f, 0.99f);
    }

    [Fact]
    public void The_rim_is_the_kits_resting_metal_and_the_glow_is_cool_moonlight()
    {
        var actNow = ColorMath.FromHex(GlyphTokens.MoonHex);
        var specular = ColorMath.FromHex(GlyphTokens.Medallion.GiltSpecularHex);
        foreach (var kit in Enum.GetValues<FrameKitId>())
        {
            var rim = MoonIconInks.Rim(kit);
            Assert.Equal(4, rim.Length);
            Assert.All(rim, stop => Assert.NotEqual(actNow, stop.Color));
            Assert.All(rim, stop => Assert.NotEqual(specular, stop.Color));

            // Lit from the upper left: the ramp darkens toward the lower right.
            Assert.True(Luma(rim[0].Color) > Luma(rim[^1].Color), $"{kit} rim is not lit from the upper left");
        }

        Assert.Equal(FrameKitMetals.Silver.Ornament.High, MoonIconInks.Rim(FrameKitId.Silver)[0].Color);
        Assert.Equal(FrameKitMetals.Came.Ornament.Deep, MoonIconInks.Rim(FrameKitId.Came)[^1].Color);
        Assert.Equal(ColorMath.FromHex(0xE2E8F4), MoonIconInks.Glow);
        Assert.True(MoonIconInks.Glow.Z > MoonIconInks.Glow.X, "the glow is cool");

        foreach (var theme in Enum.GetValues<ThemeId>())
        {
            var face = MoonIconInks.Face(theme);
            Assert.True(Luma(face.Moon[0].Color) > Luma(face.Moon[^1].Color), $"{theme}'s crescent is not lit on its upper-left limb");
            Assert.True(Luma(face.Well[^1].Color) < 0.25f, $"{theme}'s well is not dark");
        }

        static float Luma(Vector4 c) => (0.2126f * c.X) + (0.7152f * c.Y) + (0.0722f * c.Z);
    }

    [Fact]
    public void Screen_keys_are_whole_pixels()
    {
        Assert.Equal("1920x1080", MoonIconRules.ScreenKey(new Vector2(1919.6f, 1080.2f)));
        Assert.Equal("0x0", MoonIconRules.ScreenKey(new Vector2(float.NaN, -5f)));
    }
}
