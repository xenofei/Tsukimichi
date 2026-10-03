using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>The detail pane's travel and automation pills fitted to the pane (1.10): labels shorten, then go, then pills overflow.</summary>
public class ActionPillFitTests
{
    // Go to giver, Teleport, Walk, Start Questionable, Run with AutoDuty: full, short and icon-only widths.
    private static readonly PillWidths[] Row =
    [
        new(100f, 70f, 38f),
        new(90f, 90f, 38f),
        new(110f, 60f, 38f),
        new(140f, 110f, 38f),
        new(150f, 90f, 38f),
    ];

    private const float Gap = 6f;

    private static (PillRowFit Fit, PillForm[] Forms) FitRow(float available, PillWidths[]? pills = null)
    {
        pills ??= Row;
        var forms = new PillForm[pills.Length];
        var fit = ActionPillFit.Fit(available, Gap, pills, forms);
        return (fit, forms);
    }

    [Fact]
    public void A_wide_pane_shows_every_full_label()
    {
        // 590 of pills and 4 gaps of 6.
        var (fit, forms) = FitRow(614f);
        Assert.Equal(5, fit.Visible);
        Assert.Equal(614f, fit.Width, 3);
        Assert.All(forms, static f => Assert.Equal(PillForm.Full, f));
    }

    [Fact]
    public void Labels_shorten_from_the_least_important_pill_first()
    {
        // One pixel short: Run with AutoDuty takes its short label (150 to 90) and the rest keep theirs.
        var (fit, forms) = FitRow(613f);
        Assert.Equal(5, fit.Visible);
        Assert.Equal(new[] { PillForm.Full, PillForm.Full, PillForm.Full, PillForm.Full, PillForm.Short }, forms);
        Assert.Equal(554f, fit.Width, 3);

        // Then Start Questionable, then Walk; Teleport has no shorter label, so Go to giver is next.
        (fit, forms) = FitRow(500f);
        Assert.Equal(new[] { PillForm.Full, PillForm.Full, PillForm.Short, PillForm.Short, PillForm.Short }, forms);
        Assert.Equal(474f, fit.Width, 3);
    }

    [Fact]
    public void Every_label_goes_short_before_any_pill_loses_its_label()
    {
        // All short: 70 + 90 + 60 + 110 + 90 + 24 = 444.
        var (fit, forms) = FitRow(444f);
        Assert.All(forms, static f => Assert.Equal(PillForm.Short, f));
        Assert.Equal(444f, fit.Width, 3);

        // Just under: the last pill drops its label, the first keeps its own.
        (fit, forms) = FitRow(443f);
        Assert.Equal(new[] { PillForm.Short, PillForm.Short, PillForm.Short, PillForm.Short, PillForm.Icon }, forms);
        Assert.Equal(392f, fit.Width, 3);
    }

    [Fact]
    public void Icon_only_pills_come_before_any_overflow()
    {
        // Five icons and four gaps: 214.
        var (fit, forms) = FitRow(214f);
        Assert.Equal(5, fit.Visible);
        Assert.All(forms, static f => Assert.Equal(PillForm.Icon, f));

        // The first pill keeps its short label while the others are icons, if that is all the room allows.
        (fit, forms) = FitRow(250f);
        Assert.Equal(new[] { PillForm.Short, PillForm.Icon, PillForm.Icon, PillForm.Icon, PillForm.Icon }, forms);
        Assert.Equal(246f, fit.Width, 3);
    }

    [Fact]
    public void Pills_overflow_from_the_least_important_and_the_first_always_stays()
    {
        var (fit, forms) = FitRow(200f);
        Assert.Equal(4, fit.Visible);
        Assert.Equal(new[] { PillForm.Icon, PillForm.Icon, PillForm.Icon, PillForm.Icon, PillForm.Overflow }, forms);
        Assert.Equal(170f, fit.Width, 3);

        (fit, forms) = FitRow(10f);
        Assert.Equal(1, fit.Visible);
        Assert.Equal(new[] { PillForm.Icon, PillForm.Overflow, PillForm.Overflow, PillForm.Overflow, PillForm.Overflow }, forms);
        Assert.Equal(38f, fit.Width, 3);
    }

    [Fact]
    public void Unreadable_widths_read_as_no_room()
    {
        var (fit, forms) = FitRow(float.NaN);
        Assert.Equal(1, fit.Visible);
        Assert.Equal(PillForm.Icon, forms[0]);

        (fit, _) = FitRow(float.PositiveInfinity);
        Assert.Equal(1, fit.Visible);
    }

    [Fact]
    public void An_empty_row_takes_no_room()
    {
        var (fit, forms) = FitRow(300f, []);
        Assert.Equal(0, fit.Visible);
        Assert.Equal(0f, fit.Width);
        Assert.Empty(forms);
    }

    [Fact]
    public void The_forms_span_must_hold_every_pill()
    {
        Assert.Throws<ArgumentException>(() => ActionPillFit.Fit(300f, Gap, Row, new PillForm[2]));
    }

    [Fact]
    public void Pill_widths_follow_the_level_and_the_scale()
    {
        // Full: 11 + 18 + 6 + label + 13.
        Assert.Equal(11f + 18f + 6f + 60f + 13f, ActionPillFit.LabelledWidth(PillMetrics.Full, 18f, 60f, 1f), 3);
        Assert.Equal(((11f + 6f + 13f) * 2f) + 36f + 120f, ActionPillFit.LabelledWidth(PillMetrics.Full, 36f, 120f, 2f), 3);

        // Plain has its icon too (spec-1.15 decision 3), on tighter pads.
        Assert.Equal(7f + 14f + 4f + 60f + 8f, ActionPillFit.LabelledWidth(PillMetrics.Plain, 14f, 60f, 1f), 3);
        Assert.Equal(7f + 4f + 8f, ActionPillFit.LabelledWidth(PillMetrics.Plain, float.NaN, float.NaN, float.NaN), 3);

        Assert.Equal(38f, ActionPillFit.IconOnlyWidth(26f), 3);
        Assert.Equal(0f, ActionPillFit.IconOnlyWidth(float.NaN));
    }

    [Theory]
    [InlineData(Flair.Full, 30f, 18f)]
    [InlineData(Flair.Quiet, 28f, 16f)]
    [InlineData(Flair.Plain, 22f, 14f)]
    public void Each_level_has_its_height_and_icon(Flair flair, float height, float icon)
    {
        var metrics = PillMetrics.For(flair);
        Assert.Equal(height, metrics.Height);
        Assert.Equal(icon, metrics.Icon);
        Assert.Equal(icon, ActionPillFit.IconPx(metrics, height, 1f));

        // Icon-only is 1.45 x the height: 44 / 41 / 32.
        Assert.Equal(MathF.Round(height * 1.45f), ActionPillFit.IconOnlyWidth(height));
    }

    [Fact]
    public void The_icon_keeps_a_pixel_clear_of_the_pills_edges()
    {
        // A list row's button is the text line tall: 14 px icons fit a 17 px line, a 12 px line takes a 10 px icon.
        Assert.Equal(14f, ActionPillFit.IconPx(PillMetrics.Row, 17f, 1f));
        Assert.Equal(10f, ActionPillFit.IconPx(PillMetrics.Row, 12f, 1f));
        Assert.Equal(21f, ActionPillFit.IconPx(PillMetrics.Row, 30f, 1.5f));
        Assert.Equal(0f, ActionPillFit.IconPx(PillMetrics.Row, float.NaN, 1f));

        // A toolbar's button is the frame tall: 16 px icons in a 25 px frame, 14 in a 16 px one.
        Assert.Equal(16f, ActionPillFit.IconPx(PillMetrics.Frame, 25f, 1f));
        Assert.Equal(14f, ActionPillFit.IconPx(PillMetrics.Frame, 16f, 1f));

        // The panels beside game windows: 26 px pills with 16 px icons at every level.
        Assert.Equal(16f, ActionPillFit.IconPx(PillMetrics.Panel, PillMetrics.Panel.Height, 1f));
    }
}
