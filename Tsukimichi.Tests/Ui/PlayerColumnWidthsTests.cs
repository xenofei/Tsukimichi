using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// Resizable Journal columns (feature plan v6 U9): the plan with the widths the player dragged columns to, the floors a
/// drag stops at, the drag's own rules (a traded pair keeps its sum, the name gives up at most its slack), the icons a
/// narrowed cell shows whole, and "Reset column widths" giving the automatic plan back.
/// </summary>
public class PlayerColumnWidthsTests
{
    private const int N = TableGeometry.QuestColumnCount;

    /// <summary>Content widths in the shape TablePane measures (scale 1): every column, EXP and Unlocks on, 16 px icons.</summary>
    private static readonly QuestTableWidths Widths = new(
        Glyph: 32f, Level: 40f, JobIcon: 32f, Job: 90f, StateWord: 120f, Expansion: 40f, Rewards: 70f, CellOverhead: 9f,
        Scale: 1f, Exp: 110f, Opens: 46f, Icon: 16f);

    private static int Col(QuestColumn column) => (int)column;

    private static float[] Player(params (QuestColumn Column, float Width)[] sized)
    {
        var widths = new float[N];
        foreach (var (column, width) in sized)
        {
            widths[Col(column)] = width;
        }

        return widths;
    }

    private static (QuestTablePlan Plan, bool[] Visible, float[] Widths, ColumnSpec[] Specs) Plan(float width, float[]? player = null, bool[]? hidden = null, QuestTableWidths? measures = null)
    {
        var specs = new ColumnSpec[N];
        var visible = new bool[N];
        var widths = new float[N];
        var plan = TableGeometry.PlanQuestTable(width, measures ?? Widths, hidden ?? [], player ?? [], default, [], specs, visible, widths);
        return (plan, visible, widths, specs);
    }

    /// <summary>The columns in the order they hide as the table narrows from wide, one pixel at a time, without history.</summary>
    private static List<QuestColumn> HideOrder(float[]? player)
    {
        var order = new List<QuestColumn>();
        for (var width = 2000f; width > 0f; width -= 1f)
        {
            var (plan, visible, _, _) = Plan(width, player);
            if (plan.TwoLine)
            {
                break;
            }

            for (var i = Col(QuestColumn.Level); i < N; i++)
            {
                if (!visible[i] && !order.Contains((QuestColumn)i))
                {
                    order.Add((QuestColumn)i);
                }
            }
        }

        return order;
    }

    [Fact]
    public void A_column_the_player_sized_is_planned_at_that_width()
    {
        var (_, visible, widths, specs) = Plan(1400f, Player((QuestColumn.Level, 80f), (QuestColumn.Rewards, 30f)));
        Assert.True(visible[Col(QuestColumn.Level)]);
        Assert.Equal(80f + Widths.CellOverhead, widths[Col(QuestColumn.Level)]);
        Assert.Equal(30f + Widths.CellOverhead, widths[Col(QuestColumn.Rewards)]);
        Assert.Equal(specs[Col(QuestColumn.Level)].Min, specs[Col(QuestColumn.Level)].Ideal);

        // Its priority is its own: Level still outlasts Job, Rewards still hides before Expansion.
        Assert.Equal(2, specs[Col(QuestColumn.Level)].Priority);
        Assert.Equal(6, specs[Col(QuestColumn.Rewards)].Priority);

        // The rest is planned as before, the status still sharing the spare room with the name.
        var auto = Plan(1400f);
        Assert.Equal(auto.Widths[Col(QuestColumn.Job)], widths[Col(QuestColumn.Job)]);
        Assert.True(specs[Col(QuestColumn.Status)].Stretch);
        Assert.Equal(1400f, widths.Sum(), 2);
    }

    [Fact]
    public void A_status_the_player_sized_stops_stretching_and_the_name_takes_the_rest()
    {
        var (_, _, widths, specs) = Plan(1400f, Player((QuestColumn.Status, 300f)));
        Assert.False(specs[Col(QuestColumn.Status)].Stretch);
        Assert.Equal(0, specs[Col(QuestColumn.Status)].Priority);
        Assert.Equal(300f + Widths.CellOverhead, widths[Col(QuestColumn.Status)]);
        Assert.Equal(1400f, widths.Sum(), 2);
    }

    [Theory]
    [InlineData(QuestColumn.Level, 24f)]
    [InlineData(QuestColumn.Expansion, 24f)]
    [InlineData(QuestColumn.Exp, 24f)]
    [InlineData(QuestColumn.Job, 24f)]
    [InlineData(QuestColumn.Rewards, 24f)]
    [InlineData(QuestColumn.Opens, 24f)]
    [InlineData(QuestColumn.Status, 120f)]
    public void A_player_width_is_clamped_to_the_columns_floor(QuestColumn column, float floor)
    {
        Assert.Equal(floor, TableGeometry.PlayerColumnFloor(column, Widths));
        var (_, _, widths, _) = Plan(1400f, Player((column, 3f)));
        Assert.Equal(floor + Widths.CellOverhead, widths[Col(column)]);
    }

    [Fact]
    public void Floors_follow_the_scale_keep_an_icon_and_never_widen_a_column()
    {
        // Scale 2: the logical floor doubles; an icon wider than it is the floor of a column of icons.
        var big = Widths with { Scale = 2f, Icon = 60f, Rewards = 200f, Level = 100f };
        Assert.Equal(2f * LayoutBudgets.TableColumnMinLogical, TableGeometry.PlayerColumnFloor(QuestColumn.Level, big));
        Assert.Equal(60f, TableGeometry.PlayerColumnFloor(QuestColumn.Rewards, big));

        // A column whose automatic width is narrower than the floor has that width as its floor.
        var narrow = Widths with { Expansion = 18f };
        Assert.Equal(18f, TableGeometry.PlayerColumnFloor(QuestColumn.Expansion, narrow));

        // The glyph and the name are never sized by the player.
        Assert.Equal(0f, TableGeometry.PlayerColumnFloor(QuestColumn.Glyph, Widths));
        Assert.Equal(0f, TableGeometry.PlayerColumnFloor(QuestColumn.Name, Widths));
        Assert.False(TableGeometry.PlayerSized(Player((QuestColumn.Name, 300f)), QuestColumn.Name));
        var (_, _, widths, _) = Plan(1400f, Player((QuestColumn.Glyph, 300f)));
        Assert.Equal(Widths.Glyph + Widths.CellOverhead, widths[Col(QuestColumn.Glyph)]);
    }

    [Fact]
    public void Columns_hide_in_the_same_order_at_the_players_widths()
    {
        var auto = HideOrder(null);
        Assert.Equal([QuestColumn.Exp, QuestColumn.Rewards, QuestColumn.Expansion, QuestColumn.Opens, QuestColumn.Job, QuestColumn.Level], auto);

        // A wide Rewards and a narrow Level change where the steps fall, never their order.
        var sized = Player((QuestColumn.Rewards, 260f), (QuestColumn.Level, 24f), (QuestColumn.Exp, 30f));
        Assert.Equal(auto, HideOrder(sized));

        // Rewards, wider, hides at a wider table than it does automatically.
        static float LowestShowing(float[]? player)
        {
            var width = 2000f;
            while (Plan(width - 1f, player).Visible[Col(QuestColumn.Rewards)])
            {
                width -= 1f;
            }

            return width;
        }

        Assert.True(LowestShowing(sized) > LowestShowing(null));
    }

    [Fact]
    public void A_job_the_player_sized_hides_whole_rather_than_going_to_its_icon()
    {
        var player = Player((QuestColumn.Job, 120f));
        for (var width = 1400f; width > 300f; width -= 1f)
        {
            var (plan, visible, widths, _) = Plan(width, player);
            Assert.False(plan.JobIconOnly);
            if (visible[Col(QuestColumn.Job)])
            {
                Assert.Equal(120f + Widths.CellOverhead, widths[Col(QuestColumn.Job)]);
            }
        }
    }

    [Fact]
    public void A_column_that_takes_no_room_or_the_player_hid_stays_out_whatever_its_width()
    {
        var off = Widths with { Exp = 0f };
        var (_, visible, widths, _) = Plan(1400f, Player((QuestColumn.Exp, 200f)), measures: off);
        Assert.Equal(0f, Plan(1400f, measures: off).Widths[Col(QuestColumn.Exp)]);
        Assert.Equal(0f, widths[Col(QuestColumn.Exp)]);

        var hidden = new bool[N];
        hidden[Col(QuestColumn.Level)] = true;
        (_, visible, widths, _) = Plan(1400f, Player((QuestColumn.Level, 200f)), hidden);
        Assert.False(visible[Col(QuestColumn.Level)]);
        Assert.Equal(0f, widths[Col(QuestColumn.Level)]);
    }

    [Fact]
    public void Reset_column_widths_gives_the_automatic_plan_back()
    {
        foreach (var width in new[] { 1400f, 700f, 520f, 420f, 330f })
        {
            var auto = Plan(width);
            var reset = Plan(width, new float[N]);
            var unreadable = Plan(width, Player((QuestColumn.Level, float.NaN), (QuestColumn.Job, -5f), (QuestColumn.Status, float.PositiveInfinity)));
            Assert.Equal(auto.Plan, reset.Plan);
            Assert.Equal(auto.Visible, reset.Visible);
            Assert.Equal(auto.Widths, reset.Widths);
            Assert.Equal(auto.Widths, unreadable.Widths);
        }
    }

    [Fact]
    public void A_dragged_edge_trades_between_its_two_columns_and_nothing_else_moves()
    {
        float[] before = [0f, 0f, 40f, 90f, 160f, 40f, 70f];
        float[] after = [0f, 0f, 40f, 120f, 130f, 40f, 70f];
        var floors = new float[] { 0f, 0f, 24f, 24f, 120f, 24f, 24f };
        var changed = new bool[7];
        Assert.True(TableGeometry.ApplyColumnDrag(before, after, floors, slack: 0f, changed));
        Assert.Equal([0f, 0f, 40f, 120f, 130f, 40f, 70f], after);
        Assert.Equal([false, false, false, true, true, false, false], changed);
        Assert.Equal(before.Sum(), after.Sum(), 3);
    }

    [Fact]
    public void A_trade_stops_at_the_shrinking_columns_floor_and_keeps_the_pair()
    {
        float[] before = [0f, 0f, 40f, 90f, 160f];
        float[] after = [0f, 0f, 40f, 246f, 4f];
        var floors = new float[] { 0f, 0f, 24f, 24f, 120f };
        var changed = new bool[5];
        Assert.True(TableGeometry.ApplyColumnDrag(before, after, floors, slack: 0f, changed));
        Assert.Equal(120f, after[4]);
        Assert.Equal(130f, after[3]);
        Assert.Equal(before.Sum(), after.Sum(), 3);
    }

    [Fact]
    public void A_column_beside_the_name_grows_only_by_the_names_slack_and_shrinks_to_its_floor()
    {
        // Beside the stretching name only the column changes: it may take what the name has over its minimum.
        float[] before = [0f, 0f, 40f, 90f];
        float[] after = [0f, 0f, 400f, 90f];
        var floors = new float[] { 0f, 0f, 24f, 24f };
        var changed = new bool[4];
        Assert.True(TableGeometry.ApplyColumnDrag(before, after, floors, slack: 60f, changed));
        Assert.Equal(100f, after[2]);

        after = [0f, 0f, 2f, 90f];
        Assert.True(TableGeometry.ApplyColumnDrag(before, after, floors, slack: 60f, changed));
        Assert.Equal(24f, after[2]);

        // No slack at all: the column cannot grow.
        after = [0f, 0f, 70f, 90f];
        Assert.True(TableGeometry.ApplyColumnDrag(before, after, floors, slack: -5f, changed));
        Assert.Equal(40f, after[2]);
    }

    [Fact]
    public void A_change_under_half_a_pixel_or_unreadable_is_no_change()
    {
        float[] before = [0f, 0f, 40f, 90f];
        float[] after = [0f, 0f, 40.4f, float.NaN];
        var changed = new bool[4];
        Assert.False(TableGeometry.ApplyColumnDrag(before, after, new float[4], 100f, changed));
        Assert.Equal(before, after);
        Assert.All(changed, static c => Assert.False(c));
        Assert.Throws<ArgumentException>(() => TableGeometry.ApplyColumnDrag(before, new float[2], new float[4], 0f, new bool[4]));
    }

    [Fact]
    public void A_narrowed_cell_shows_whole_icons_then_the_count_of_the_rest()
    {
        // Four 16 px icons with 2 px gaps need 70 px.
        Assert.Equal(4, TableGeometry.IconsThatFit(70f, 16f, 2f, 4, 14f));
        Assert.Equal(4, TableGeometry.IconsThatFit(69.6f, 16f, 2f, 4, 14f));

        // One pixel short: as many icons as leave room for "+N".
        Assert.Equal(3, TableGeometry.IconsThatFit(69f, 16f, 2f, 4, 14f));
        Assert.Equal(1, TableGeometry.IconsThatFit(32f, 16f, 2f, 4, 14f));

        // Not even one icon beside the count: the count alone, never part of an icon.
        Assert.Equal(0, TableGeometry.IconsThatFit(24f, 16f, 2f, 4, 14f));
        Assert.Equal(1, TableGeometry.IconsThatFit(16f, 16f, 2f, 1, 14f));
        Assert.Equal(0, TableGeometry.IconsThatFit(100f, 16f, 2f, 0, 14f));
        Assert.Equal(0, TableGeometry.IconsThatFit(float.NaN, 16f, 2f, 3, 14f));
    }

    [Fact]
    public void A_saved_width_is_kept_only_when_readable()
    {
        Assert.Equal(80f, TableGeometry.SanitizePlayerWidth(80f));
        Assert.Equal(0f, TableGeometry.SanitizePlayerWidth(0f));
        Assert.Equal(0f, TableGeometry.SanitizePlayerWidth(-3f));
        Assert.Equal(0f, TableGeometry.SanitizePlayerWidth(float.NaN));
        Assert.Equal(TableGeometry.MaxPlayerWidthLogical, TableGeometry.SanitizePlayerWidth(1e9f));
    }
}
