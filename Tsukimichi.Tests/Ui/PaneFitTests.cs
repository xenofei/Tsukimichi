using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The narrow-width rules of My blues, Flight, Abandoned, Moonlit and Settings (feature plan v4 L6, UI audit §4.3,
/// design v4 §8.2): the row tiers, the one-line split that never cuts a state word, the column plans that hide the
/// right columns first, and the clamps that keep a control inside the room there is.
/// </summary>
public class PaneFitTests
{
    private const float H = LayoutBudgets.HysteresisLogical;

    [Theory]
    [InlineData(900f, PlanRowTier.OneLine)]
    [InlineData(560f, PlanRowTier.OneLine)]
    [InlineData(559.9f, PlanRowTier.TwoLine)]
    [InlineData(420f, PlanRowTier.TwoLine)]
    [InlineData(419.9f, PlanRowTier.TwoLineMenu)]
    [InlineData(320f, PlanRowTier.TwoLineMenu)]
    [InlineData(0f, PlanRowTier.TwoLineMenu)]
    [InlineData(float.NaN, PlanRowTier.TwoLineMenu)]
    public void My_blues_goes_two_line_under_560_and_folds_its_buttons_under_420(float width, PlanRowTier tier)
    {
        Assert.Equal(tier, PaneFit.PlanTier(width));
    }

    [Fact]
    public void My_blues_shows_only_the_primary_kind_once_two_line_and_under_520()
    {
        Assert.True(PaneFit.PlanAllKindsLogical < LayoutBudgets.PlanOneLineLogical);
        Assert.True(PaneFit.PlanAllKindsLogical > LayoutBudgets.PlanMenuLogical);
        Assert.True(PaneFit.PlanAllKinds(520f));
        Assert.False(PaneFit.PlanAllKinds(519f));
        Assert.False(PaneFit.PlanAllKinds(float.NaN));
    }

    [Fact]
    public void Flight_and_abandoned_fold_their_buttons_where_my_blues_does()
    {
        Assert.Equal(LayoutBudgets.PlanMenuLogical, PaneFit.FoldActionsLogical);
        Assert.False(PaneFit.FoldActions(420f));
        Assert.True(PaneFit.FoldActions(419f));
        Assert.True(PaneFit.FoldActions(float.NaN));
    }

    [Theory]
    [InlineData(400f, 90f)]
    [InlineData(300f, 90f)]
    [InlineData(200f, 150f)]
    [InlineData(1000f, 90f)]
    public void The_one_line_split_fills_the_middle_and_never_cuts_the_state_word(float middle, float statusMin)
    {
        var (name, pills, status) = PaneFit.PlanOneLine(middle, statusMin);
        Assert.Equal(middle, name + pills + status, 3);
        Assert.True(status >= statusMin);
        Assert.True(status >= middle * PaneFit.PlanStatusShare - 0.001f);
        Assert.True(name >= pills);
    }

    [Fact]
    public void A_status_wider_than_the_middle_takes_it_all()
    {
        var (name, pills, status) = PaneFit.PlanOneLine(60f, 90f);
        Assert.Equal(60f, status);
        Assert.Equal(0f, name);
        Assert.Equal(0f, pills);

        var broken = PaneFit.PlanOneLine(float.NaN, float.NaN);
        Assert.Equal((0f, 0f, 0f), broken);
    }

    [Theory]
    [InlineData(220f, 400f, 220f)]
    [InlineData(220f, 150f, 150f)]
    [InlineData(220f, 0f, 1f)]
    [InlineData(220f, -20f, 1f)]
    [InlineData(220f, float.NaN, 220f)]
    public void A_fixed_control_shrinks_to_the_room_left(float ideal, float available, float expected)
    {
        Assert.Equal(expected, PaneFit.ControlWidth(ideal, available));
    }

    [Theory]
    [InlineData(1000f, 120f, 360f, 16f, 360f)]
    [InlineData(300f, 120f, 360f, 16f, 284f)]
    [InlineData(130f, 120f, 360f, 16f, 120f)]
    [InlineData(100f, 120f, 360f, 16f, 100f)]
    [InlineData(40f, 80f, 360f, 16f, 40f)]
    public void An_empty_state_column_keeps_its_floor_only_while_there_is_room(float available, float floor, float max, float margin, float expected)
    {
        Assert.Equal(expected, PaneFit.Column(available, floor, max, margin));
    }

    [Fact]
    public void Flight_hides_the_state_moon_first_and_never_the_actions_or_the_status()
    {
        Span<ColumnSpec> columns = stackalloc ColumnSpec[5];
        PaneFit.FlightColumns(glyph: 30f, nameMin: 48f, statusMin: 100f, actions: 120f, columns);
        var all = 30f + 48f + 30f + 100f + 120f;

        var (visible, widths) = Plan(columns, all + 200f);
        Assert.All(visible, static v => Assert.True(v));
        // The name and the status share what the minimums leave evenly.
        Assert.Equal(widths[1] - 48f, widths[3] - 100f, 3);

        (visible, widths) = Plan(columns, all - 1f);
        Assert.False(visible[2]);
        Assert.True(visible[0] && visible[1] && visible[3] && visible[4]);
        Assert.Equal(120f, widths[4]);
        Assert.True(widths[3] >= 100f);

        // Narrower than every minimum: nothing more hides, and the actions keep their width.
        (visible, widths) = Plan(columns, 100f);
        Assert.Equal(120f, widths[4]);
        Assert.True(visible[3]);
    }

    [Fact]
    public void Moonlit_hides_confidence_then_kind_then_availability_and_never_state()
    {
        Span<ColumnSpec> columns = stackalloc ColumnSpec[7];
        PaneFit.MoonlitColumns(glyph: 44f, rewardMin: 110f, kind: 110f, questMin: 80f, confidence: 80f, availability: 100f, columns);
        var all = 44f + 110f + 110f + 80f + 44f + 80f + 100f;

        var (visible, _) = Plan(columns, all);
        Assert.All(visible, static v => Assert.True(v));

        (visible, _) = Plan(columns, all - 1f);
        Assert.False(visible[5]);
        Assert.True(visible[2]);
        Assert.True(visible[4]);
        Assert.True(visible[6]);

        (visible, _) = Plan(columns, all - 80f - 1f);
        Assert.False(visible[5]);
        Assert.False(visible[2]);
        Assert.True(visible[4]);
        Assert.True(visible[6]);

        (visible, _) = Plan(columns, all - 80f - 110f - 1f);
        Assert.False(visible[6]);
        Assert.True(visible[4]);

        (visible, _) = Plan(columns, 10f);
        Assert.True(visible[0] && visible[1] && visible[3] && visible[4]);
    }

    [Fact]
    public void The_verdicts_table_hides_the_note_then_the_date_and_keeps_restore()
    {
        Span<ColumnSpec> columns = stackalloc ColumnSpec[5];
        PaneFit.VerdictColumns(nameMin: 60f, verdict: 90f, date: 110f, restore: 70f, columns);
        var all = 60f + 90f + 60f + 110f + 70f;

        var (visible, widths) = Plan(columns, all - 1f);
        Assert.False(visible[2]);
        Assert.True(visible[3]);

        (visible, widths) = Plan(columns, all - 60f - 1f);
        Assert.False(visible[2]);
        Assert.False(visible[3]);
        Assert.True(visible[4]);
        Assert.Equal(70f, widths[4]);
    }

    private static (bool[] Visible, float[] Widths) Plan(ReadOnlySpan<ColumnSpec> columns, float width)
    {
        var visible = new bool[columns.Length];
        var widths = new float[columns.Length];
        TableGeometry.PlanColumns(width, columns, [], visible, widths, H);
        return (visible, widths);
    }
}
