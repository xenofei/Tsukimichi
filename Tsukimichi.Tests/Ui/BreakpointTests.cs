using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The responsive breakpoints in <see cref="LayoutBudgets"/> (feature plan v4 L2, design v4 §8.2): each pane's tiers
/// run from wide to narrow and its narrowest tier starts at the floor <see cref="PaneLayout"/> enforces, so no pane
/// is ever drawn narrower than a layout was designed for.
/// </summary>
public class BreakpointTests
{
    [Fact]
    public void Tree_tiers_descend_to_the_tree_floor()
    {
        Assert.True(LayoutBudgets.TreeFullLogical > LayoutBudgets.TreeTrimLogical);
        Assert.True(LayoutBudgets.TreeTrimLogical > LayoutBudgets.TreeCompactLogical);
        Assert.True(LayoutBudgets.TreeCompactLogical > LayoutBudgets.TreeSlimLogical);
        Assert.Equal(PaneLayout.TreeFloorLogical, LayoutBudgets.TreeSlimLogical);
        Assert.Equal(300f, LayoutBudgets.TreeFullLogical);
        Assert.Equal(240f, LayoutBudgets.TreeTrimLogical);
        Assert.Equal(200f, LayoutBudgets.TreeCompactLogical);
    }

    [Fact]
    public void The_tree_default_shows_the_full_row()
    {
        Assert.True(PaneLayout.TreeDefaultLogical >= LayoutBudgets.TreeFullLogical);
    }

    [Fact]
    public void Table_tiers_descend_to_the_centre_floor()
    {
        float[] tiers =
        [
            LayoutBudgets.TableFullLogical, LayoutBudgets.TableNoRewardsLogical, LayoutBudgets.TableNoExpansionLogical,
            LayoutBudgets.TableNoJobLogical, LayoutBudgets.TableNoLevelLogical, LayoutBudgets.TableTwoLineLogical,
        ];
        for (var i = 1; i < tiers.Length; i++)
        {
            Assert.True(tiers[i - 1] > tiers[i], $"table tier {i} is not narrower than tier {i - 1}");
        }

        Assert.Equal(PaneLayout.CentreFloorLogical, LayoutBudgets.TableTwoLineLogical);
    }

    [Fact]
    public void Detail_tiers_descend_to_the_detail_floor()
    {
        Assert.True(LayoutBudgets.DetailFullLogical > LayoutBudgets.DetailMediumLogical);
        Assert.True(LayoutBudgets.DetailMediumLogical > LayoutBudgets.DetailNarrowLogical);
        Assert.Equal(PaneLayout.DetailFloorLogical, LayoutBudgets.DetailNarrowLogical);
        Assert.True(PaneLayout.DetailDefaultLogical >= LayoutBudgets.DetailFullLogical);
    }

    [Fact]
    public void My_blues_folds_its_buttons_only_after_going_two_line()
    {
        Assert.True(LayoutBudgets.PlanOneLineLogical > LayoutBudgets.PlanMenuLogical);
        Assert.True(LayoutBudgets.PlanMenuLogical > PaneLayout.CentreFloorLogical);
    }

    [Fact]
    public void A_row_name_keeps_room_for_a_few_characters()
    {
        // 48 logical px hold about five average Latin letters or three kanji at the body size.
        Assert.InRange(LayoutBudgets.RowNameMinLogical / LayoutBudgets.BodyFontPx, 2.5f, 4f);
        Assert.Equal(16f, LayoutBudgets.HysteresisLogical);
    }

    [Theory]
    [InlineData(400f, 80f, 8f, 16f, false)]
    [InlineData(248f, 80f, 8f, 16f, false)]
    [InlineData(247f, 80f, 8f, 16f, true)]
    [InlineData(200f, 120f, 8f, 16f, true)]
    [InlineData(600f, 120f, 8f, 20f, false)]
    [InlineData(300f, 120f, 8f, 20f, true)]
    public void A_label_stacks_over_its_value_when_the_value_would_get_under_ten_ems(float available, float label, float gap, float em, bool stacks)
    {
        Assert.Equal(stacks, LayoutBudgets.StackLabelValue(available, label, gap, em));
    }

    [Fact]
    public void Unreadable_measurements_give_the_default_budgets_never_nan()
    {
        Assert.Equal(LayoutBudgets.StatusMinLogical, LayoutBudgets.StatusMin(float.NaN));
        Assert.Equal(LayoutBudgets.MaxStatusMinLogical, LayoutBudgets.StatusMin(float.PositiveInfinity));
        Assert.False(LayoutBudgets.CompactRail(float.NaN, float.NaN, wasCompact: false, forced: false));
    }

    [Fact]
    public void Unreadable_label_value_widths_stack()
    {
        Assert.True(LayoutBudgets.StackLabelValue(float.NaN, 80f, 8f, 16f));
        Assert.True(LayoutBudgets.StackLabelValue(400f, float.PositiveInfinity, 8f, 16f));
        Assert.False(LayoutBudgets.StackLabelValue(400f, 80f, float.NaN, 0f));
    }
}
