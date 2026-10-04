using System.Numerics;
using Tsukimichi.Core.Releases;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;
using Tsukimichi.Core.Updates;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The rules behind the 1.22.0 UI review's fixes (the wiring is linted in <see cref="Release122UiFixesLintTests"/>):
/// What's new is placed after measuring at every motion setting and kept on screen, Reset counts Follow Umbra, the
/// outgoing release picture is kept for the cross-fade inside the budget, the quick card measures only on open, the Hide
/// toast is instant at Plain, the update note never runs into the version, and a failed draw retries.
/// </summary>
public sealed class Release122UiFixesTests
{
    private static readonly Vector2 WorkPos = Vector2.Zero;
    private static readonly Vector2 FullHd = new(1920f, 1080f);

    [Fact]
    public void Whats_new_is_placed_on_the_first_frame_after_measuring_with_motion_off()
    {
        var target = new Vector2(680f, 300f);
        var size = new Vector2(560f, 480f);
        var placement = default(WhatsNewPlacement);

        // Reduce motion or Plain: placed at once at the target, then left where it is.
        Assert.Equal(target, placement.Next(target, size, WorkPos, FullHd, motion: false, sinceOpen: 0.0, scale: 1f));
        Assert.Null(placement.Next(target, size, WorkPos, FullHd, motion: false, sinceOpen: 0.02, scale: 1f));

        // Motion that stops for a frame mid-rise (a wheel scroll) still lands it at the target.
        placement.Reset();
        Assert.Equal(target + new Vector2(0f, 4f), placement.Next(target, size, WorkPos, FullHd, motion: true, sinceOpen: 0.0, scale: 1f));
        Assert.Equal(target, placement.Next(target, size, WorkPos, FullHd, motion: false, sinceOpen: 0.05, scale: 1f));
        Assert.Null(placement.Next(target, size, WorkPos, FullHd, motion: true, sinceOpen: 0.06, scale: 1f));
    }

    [Fact]
    public void Whats_new_rises_4_px_and_ends_exactly_on_its_target()
    {
        var target = new Vector2(680f, 300f);
        var size = new Vector2(560f, 480f);
        var placement = default(WhatsNewPlacement);
        var last = float.PositiveInfinity;
        for (var t = 0.0; t < MotionTokens.Rise; t += 0.02)
        {
            var at = placement.Next(target, size, WorkPos, FullHd, motion: true, sinceOpen: t, scale: 2f);
            Assert.NotNull(at);
            Assert.InRange(at.Value.Y - target.Y, 0f, 8f);
            Assert.True(at.Value.Y <= last);
            last = at.Value.Y;
        }

        Assert.Equal(target, placement.Next(target, size, WorkPos, FullHd, motion: true, sinceOpen: MotionTokens.Rise, scale: 2f));
        Assert.Null(placement.Next(target, size, WorkPos, FullHd, motion: true, sinceOpen: 1.0, scale: 2f));
    }

    [Fact]
    public void Whats_new_stays_on_screen_at_ui_scale_1_6_on_1080p()
    {
        // 560 × 1.6 wide and as tall as the cap allows; centred on a main window low on the screen.
        var size = new Vector2(896f, 1080f * WhatsNewLayout.ScreenShare);
        var centre = new Vector2(960f, 900f);
        var target = WhatsNewLayout.Clamp(centre - (size * 0.5f), size, WorkPos, FullHd);
        Assert.True(target.Y + size.Y <= FullHd.Y, "the footer must be on screen");
        Assert.True(target.Y >= 0f);

        // The rise never pushes the footer off the bottom either.
        var placement = default(WhatsNewPlacement);
        var first = placement.Next(target, size, WorkPos, FullHd, motion: true, sinceOpen: 0.0, scale: 1.6f);
        Assert.NotNull(first);
        Assert.True(first.Value.Y + size.Y <= FullHd.Y);

        // Taller than the screen: the header stays on screen.
        Assert.Equal(new Vector2(0f, 0f), WhatsNewLayout.Clamp(new Vector2(-50f, -80f), new Vector2(2000f, 1200f), WorkPos, FullHd));
    }

    [Fact]
    public void Reset_is_not_already_default_while_following_umbra()
    {
        var config = new AppearanceConfig();
        AppearanceEdits.Reset(config);
        var resolved = AppearanceResolver.Resolve(config);
        Assert.True(AppearanceEdits.IsDefault(config, resolved, followUmbra: false));
        Assert.False(AppearanceEdits.IsDefault(config, resolved, followUmbra: true));
    }

    [Fact]
    public void A_page_change_keeps_the_outgoing_picture_until_the_new_one_lands()
    {
        var slots = new ReleaseArtSlots<string>();
        Assert.Equal((null, null), slots.Land("1.22"));
        Assert.Equal("1.22", slots.Current);

        // Paging on: 1.22 fades out while 1.21 loads; nothing is let go yet.
        Assert.Null(slots.Replace());
        Assert.Null(slots.Current);
        Assert.Equal("1.22", slots.Outgoing);

        // 1.21 lands: the outgoing picture is let go then.
        Assert.Equal((null, "1.22"), slots.Land("1.21"));
        Assert.Equal("1.21", slots.Current);
        Assert.Null(slots.Outgoing);

        // Paging twice quickly: the older outgoing picture is let go, never more than two are held.
        Assert.Null(slots.Replace());
        Assert.Null(slots.Replace());
        Assert.Equal("1.21", slots.Outgoing);
        Assert.Equal((null, "1.21"), slots.Land("1.20"));
        Assert.Null(slots.Replace());
        Assert.Equal((null, "1.20"), slots.Land("1.19"));

        // A page with no picture: the outgoing one is let go once the cross-fade has run.
        Assert.Null(slots.Replace());
        Assert.Equal("1.19", slots.Retire());
        Assert.Null(slots.Outgoing);

        // Closing lets both go.
        slots.Land("1.18");
        slots.Replace();
        slots.Land("1.17");
        slots.Replace();
        Assert.Equal((null, "1.17"), slots.Clear());
    }

    [Fact]
    public void Both_picture_slots_fit_the_12_MB_budget()
    {
        Assert.Equal(2, ReleaseArt.HeldSlots);
        Assert.Equal(2L * 1120 * 440 * 4, ReleaseArt.HeldBytes);
        Assert.True(ReleaseArt.HeldBytes <= ReleaseArt.BudgetBytes);
        Assert.Equal(12L * 1024 * 1024, ReleaseArt.BudgetBytes);
    }

    [Fact]
    public void The_quick_card_measures_only_on_open_and_resizes_visibly_after()
    {
        Assert.Equal(CardSettle.Measure, MoonIconRules.Settle(0f, 180f));
        Assert.Equal(CardSettle.Keep, MoonIconRules.Settle(180f, 180.3f));

        // A line arriving or leaving while it is open: taken at once, never hidden.
        Assert.Equal(CardSettle.Resize, MoonIconRules.Settle(180f, 204f));
        Assert.Equal(CardSettle.Resize, MoonIconRules.Settle(204f, 180f));
    }

    [Theory]
    [InlineData(Flair.Plain, false, 1f)]
    [InlineData(Flair.Full, true, 1f)]
    [InlineData(Flair.Quiet, true, 1f)]
    [InlineData(Flair.Full, false, 0.5f)]
    [InlineData(Flair.Quiet, false, 0.5f)]
    public void The_hide_toast_fades_in_only_where_motion_plays(Flair flair, bool reduce, float expected)
    {
        Assert.Equal(expected, MoonIconRules.ToastFade(0.09, 0.18f, flair, reduce), 3);
        Assert.Equal(1f, MoonIconRules.ToastFade(1.0, 0.18f, flair, reduce));
    }

    [Fact]
    public void The_update_note_never_runs_into_the_version()
    {
        // Room for all of it.
        Assert.Equal(180f, UpdateNoteFit.TextRoom(400f, 120f, 180f, 1f));

        // A narrower window: the words end in an ellipsis within the room.
        var room = UpdateNoteFit.TextRoom(250f, 120f, 180f, 1f);
        Assert.Equal(130f, room);
        Assert.True(120f + room <= 250f);

        // Narrower still: the note is left out rather than overlap.
        Assert.True(UpdateNoteFit.TextRoom(150f, 120f, 180f, 1f) < 0f);
        Assert.True(UpdateNoteFit.TextRoom(-40f, 120f, 180f, 1f) < 0f);

        // The floor scales with the UI.
        Assert.True(UpdateNoteFit.TextRoom(120f + 60f, 120f, 180f, 1.5f) < 0f);
        Assert.Equal(60f, UpdateNoteFit.TextRoom(120f + 60f, 120f, 180f, 1f));
    }

    [Fact]
    public void A_failed_draw_rests_then_retries_and_logs_at_most_once_a_minute()
    {
        var retry = new DrawRetry();
        Assert.True(retry.Ready(0.0));

        Assert.True(retry.Failed(10.0));
        Assert.False(retry.Ready(10.5));
        Assert.True(retry.Ready(11.0));

        // Failing again: the rest doubles, and nothing new is logged within the minute.
        Assert.False(retry.Failed(11.0));
        Assert.Equal(2.0, retry.Backoff);
        Assert.False(retry.Ready(12.9));
        Assert.True(retry.Ready(13.0));

        var now = 13.0;
        for (var i = 0; i < 10; i++)
        {
            retry.Failed(now);
            now += retry.Backoff;
        }

        Assert.Equal(DrawRetry.MaxSeconds, retry.Backoff);
        Assert.True(retry.Failed(now + 60.0));

        // A frame that draws: the next failure rests a second again.
        retry.Succeeded();
        retry.Failed(500.0);
        Assert.Equal(DrawRetry.FirstSeconds, retry.Backoff);
        Assert.True(retry.Ready(501.0));
    }
}
