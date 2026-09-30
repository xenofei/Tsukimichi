using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>The quest table's column plan (feature plan v4 L2; adopted by the table in L4).</summary>
public class ColumnPlanTests
{
    private const float H = LayoutBudgets.HysteresisLogical;

    // The audit's table (UI audit §4), display order: Glyph, Name, Level, Job, Status, Expansion, Rewards.
    private static readonly ColumnSpec[] Table =
    [
        new(Priority: 0, Min: 28f, Ideal: 28f),
        new(Priority: 0, Min: 140f, Ideal: 140f, Weight: 3f),
        new(Priority: 2, Min: 40f, Ideal: 48f),
        new(Priority: 3, Min: 76f, Ideal: 76f),
        new(Priority: 1, Min: 120f, Ideal: 120f, Weight: 2f),
        new(Priority: 4, Min: 40f, Ideal: 40f),
        new(Priority: 5, Min: 80f, Ideal: 80f),
    ];

    private const int Level = 2;
    private const int Job = 3;
    private const int Status = 4;
    private const int Expansion = 5;
    private const int Rewards = 6;

    /// <summary>Every minimum together: 524.</summary>
    private static readonly float AllMins = Table.Sum(static c => c.Min);

    private static (bool[] Visible, float[] Widths, int Shown) Plan(float width, bool[]? was = null)
    {
        var visible = new bool[Table.Length];
        var widths = new float[Table.Length];
        var shown = TableGeometry.PlanColumns(width, Table, was ?? [], visible, widths, H);
        return (visible, widths, shown);
    }

    [Fact]
    public void A_wide_table_shows_every_column_and_the_stretch_columns_share_the_rest_three_to_two()
    {
        var (visible, widths, shown) = Plan(1000f);
        Assert.Equal(Table.Length, shown);
        Assert.All(visible, static v => Assert.True(v));
        Assert.Equal(1000f, widths.Sum(), 2);
        Assert.Equal(48f, widths[Level]);
        var extraName = widths[1] - 140f;
        var extraStatus = widths[Status] - 120f;
        Assert.Equal(1.5f, extraName / extraStatus, 3);
    }

    [Fact]
    public void Columns_hide_in_the_order_rewards_expansion_job_level()
    {
        Assert.False(Plan(AllMins - 1f).Visible[Rewards]);
        Assert.True(Plan(AllMins - 1f).Visible[Expansion]);

        var noExpansion = Plan(AllMins - 80f - 1f).Visible;
        Assert.False(noExpansion[Expansion]);
        Assert.True(noExpansion[Job]);

        var noJob = Plan(AllMins - 120f - 1f).Visible;
        Assert.False(noJob[Job]);
        Assert.True(noJob[Level]);

        var noLevel = Plan(AllMins - 196f - 1f).Visible;
        Assert.False(noLevel[Level]);
        Assert.True(noLevel[Status]);
        Assert.True(noLevel[1]);
    }

    [Fact]
    public void Priority_zero_never_hides_and_status_hides_last()
    {
        var (visible, widths, shown) = Plan(100f);
        Assert.Equal(2, shown);
        Assert.True(visible[0]);
        Assert.True(visible[1]);
        Assert.False(visible[Status]);
        Assert.Equal(28f, widths[0]);
        Assert.Equal(140f, widths[1]);
    }

    [Fact]
    public void Visible_columns_always_fit_down_to_the_never_hidden_ones()
    {
        for (var width = 168f; width <= 1200f; width += 3f)
        {
            var (visible, widths, _) = Plan(width);
            var mins = 0f;
            for (var i = 0; i < Table.Length; i++)
            {
                mins += visible[i] ? Table[i].Min : 0f;
                Assert.True(visible[i] || widths[i] == 0f);
            }

            Assert.True(mins <= width + 0.01f, $"minimums {mins} over {width}");
            Assert.True(widths.Sum() <= width + 0.01f, $"widths {widths.Sum()} over {width}");
        }
    }

    [Fact]
    public void Hidden_columns_are_a_suffix_of_the_priority_order()
    {
        var order = new[] { Status, Level, Job, Expansion, Rewards };
        for (var width = 0f; width <= 700f; width += 5f)
        {
            var visible = Plan(width).Visible;
            var seenHidden = false;
            foreach (var column in order)
            {
                Assert.False(visible[column] && seenHidden, $"column {column} shows after a more important one hid at {width}");
                seenHidden |= !visible[column];
            }
        }
    }

    [Fact]
    public void A_hidden_column_returns_only_with_the_hysteresis_to_spare()
    {
        var hidden = Plan(AllMins - 1f).Visible;
        Assert.False(hidden[Rewards]);

        // Just wide enough again: without history it shows, with it the column waits for 16 more pixels.
        Assert.True(Plan(AllMins).Visible[Rewards]);
        Assert.False(Plan(AllMins, hidden).Visible[Rewards]);
        Assert.False(Plan(AllMins + H - 1f, hidden).Visible[Rewards]);
        Assert.True(Plan(AllMins + H, hidden).Visible[Rewards]);

        // A shown column hides as soon as it does not fit: no hysteresis on the way down.
        var shown = Plan(AllMins + H).Visible;
        Assert.False(Plan(AllMins - 1f, shown).Visible[Rewards]);
    }

    [Fact]
    public void A_width_wobbling_on_a_threshold_does_not_flicker()
    {
        var was = Plan(AllMins - 1f).Visible;
        foreach (var width in new[] { AllMins + 2f, AllMins - 2f, AllMins + 5f, AllMins + 10f, AllMins - 3f })
        {
            was = Plan(width, was).Visible;
            Assert.False(was[Rewards], $"rewards came back at {width}");
        }
    }

    [Fact]
    public void The_plan_is_the_same_for_the_same_width_and_history()
    {
        var a = Plan(431f);
        var b = Plan(431f);
        Assert.Equal(a.Visible, b.Visible);
        Assert.Equal(a.Widths, b.Widths);
    }

    [Fact]
    public void Fixed_columns_grow_to_their_ideal_before_the_stretch_columns_share()
    {
        var (_, widths, _) = Plan(AllMins + 4f);
        Assert.Equal(44f, widths[Level]);
        Assert.Equal(140f, widths[1]);
        Assert.Equal(120f, widths[Status]);
    }

    [Fact]
    public void Ties_hide_the_later_column_first()
    {
        ColumnSpec[] columns = [new(0, 50f, 50f), new(1, 50f, 50f), new(1, 50f, 50f)];
        var visible = new bool[3];
        var widths = new float[3];
        TableGeometry.PlanColumns(120f, columns, [], visible, widths, H);
        Assert.Equal(new[] { true, true, false }, visible);
    }

    [Fact]
    public void Unreadable_inputs_are_safe_and_short_spans_are_refused()
    {
        var visible = new bool[Table.Length];
        var widths = new float[Table.Length];
        Assert.Equal(2, TableGeometry.PlanColumns(float.NaN, Table, [], visible, widths, float.NaN));
        Assert.Throws<ArgumentException>(() => TableGeometry.PlanColumns(100f, Table, [], new bool[1], new float[Table.Length], H));
    }
}
