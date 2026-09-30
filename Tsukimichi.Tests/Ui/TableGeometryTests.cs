using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

public class TableGeometryTests
{
    [Theory]
    [InlineData(28f, 6f, 8.96f)]   // Comfortable at scale 1: r 9 (ui-revamp §2.4)
    [InlineData(20f, 6f, 6.4f)]    // Dense: r 6.4
    [InlineData(20f, 12f, 12f)]    // IconScale 2: the icon-scaled moon wins (the row has grown to hold it)
    [InlineData(float.NaN, 6f, 6f)]
    public void Row_moon_follows_the_row_height_but_never_shrinks_under_the_icon_scale(float content, float min, float expected)
    {
        Assert.Equal(expected, TableGeometry.GlyphRadius(content, min), 3);
    }

    [Theory]
    [InlineData(20f, 6f)]
    [InlineData(28f, 6f)]
    [InlineData(56f, 12f)]
    public void The_moon_box_fits_the_row(float content, float min)
    {
        var box = TableGeometry.GlyphRadius(content, min) * TableGeometry.GlyphBoxPerRadius;
        Assert.True(box <= MathF.Max(content, min * TableGeometry.GlyphBoxPerRadius) + 0.001f);
    }

    [Fact]
    public void The_state_word_is_everything_before_the_separator()
    {
        Assert.Equal("Ready".Length, TableGeometry.StateWordLength("Ready" + BlockerText.Separator + "Vorsaile Heuloix"));
        Assert.Equal(StateNames.ReadyOnOtherJob.Length, TableGeometry.StateWordLength(StateNames.ReadyOnOtherJob));
        Assert.Equal(0, TableGeometry.StateWordLength(null));
        Assert.Equal(0, TableGeometry.StateWordLength(string.Empty));
    }

    [Theory]
    [MemberData(nameof(StripePatternTests.AllStates), MemberType = typeof(StripePatternTests))]
    public void Every_state_name_is_a_whole_state_word(QuestState state)
    {
        // P1: the Status column's first token is the state name; the fit keeps it whole and cuts only what follows.
        var status = StateNames.Name(state) + BlockerText.Separator + "a reason long enough to need an ellipsis";
        Assert.Equal(StateNames.Name(state), status[..TableGeometry.StateWordLength(status)]);
    }

    [Theory]
    [InlineData(170f, 40f, 130f)]
    [InlineData(170f, 170f, 0f)]
    [InlineData(100f, 150f, 0f)]    // a state word wider than the cell keeps its width; the reason gets nothing
    [InlineData(float.NaN, 40f, 0f)]
    public void The_reason_gets_what_the_state_word_leaves(float cell, float state, float expected)
    {
        Assert.Equal(expected, TableGeometry.ReasonWidth(cell, state), 3);
    }

    [Fact]
    public void Only_a_reason_wider_than_its_room_is_ellipsised()
    {
        Assert.False(TableGeometry.ReasonNeedsEllipsis(100f, 100f));
        Assert.False(TableGeometry.ReasonNeedsEllipsis(100.4f, 100f));
        Assert.True(TableGeometry.ReasonNeedsEllipsis(101f, 100f));
    }
}
