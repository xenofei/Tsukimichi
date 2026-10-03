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
    public void Pill_widths_follow_the_scale()
    {
        Assert.Equal(12f + 6f + 14f + 14f + 60f, ActionPillFit.LabelledWidth(14f, 60f, 1f), 3);
        Assert.Equal(((12f + 6f + 14f) * 2f) + 28f + 120f, ActionPillFit.LabelledWidth(28f, 120f, 2f), 3);
        Assert.Equal(38f, ActionPillFit.IconOnlyWidth(26f), 3);
        Assert.Equal(0f, ActionPillFit.IconOnlyWidth(float.NaN));
    }

    [Fact]
    public void A_text_only_pill_is_the_label_between_its_pads()
    {
        // Plain's buttons: no icon and no gap, so neither is counted (nor clamped away into an off-centre label).
        Assert.Equal(12f + 14f + 60f, ActionPillFit.TextOnlyWidth(60f, 1f), 3);
        Assert.Equal(((12f + 14f) * 2f) + 120f, ActionPillFit.TextOnlyWidth(120f, 2f), 3);
        Assert.Equal(12f + 14f, ActionPillFit.TextOnlyWidth(float.NaN, float.NaN), 3);
    }
}
