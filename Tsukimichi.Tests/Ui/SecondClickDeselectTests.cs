using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// 1.21.0 review: a double-click on the selected Journal row flashed Tonight (the first click deselected it at once);
/// and the My blues rows kept in place outlived a change of character.
/// </summary>
public sealed class SecondClickDeselectTests
{
    private const double DoubleClickTime = 0.30;

    [Fact]
    public void A_single_click_on_the_selected_row_deselects_after_the_double_click_time()
    {
        var deselect = new SecondClickDeselect();
        deselect.Click(7, 1.00);

        Assert.Equal(0u, deselect.Due(1.10, DoubleClickTime, 7));
        Assert.Equal(0u, deselect.Due(1.29, DoubleClickTime, 7));
        Assert.Equal(7u, deselect.Due(1.32, DoubleClickTime, 7));
        Assert.Equal(0u, deselect.Due(1.40, DoubleClickTime, 7));
    }

    [Fact]
    public void A_double_click_or_a_selection_elsewhere_keeps_the_row()
    {
        var deselect = new SecondClickDeselect();
        deselect.Click(7, 1.00);
        deselect.DoubleClick();
        Assert.Equal(0u, deselect.Due(2.00, DoubleClickTime, 7));

        deselect.Click(7, 3.00);
        Assert.Equal(0u, deselect.Due(3.50, DoubleClickTime, 8));
        Assert.Equal(0u, deselect.Pending);
    }

    [Fact]
    public void Rows_kept_in_place_belong_to_one_character()
    {
        var kept = new KeptInPlace();
        Assert.False(kept.Follow(1));
        kept.Keep(100);
        kept.Keep(101);
        var version = kept.Version;

        Assert.False(kept.Follow(1));
        Assert.True(kept.Contains(100));

        // Another character on view: its list never shows the first one's moved rows or their Undo.
        Assert.True(kept.Follow(2));
        Assert.Equal(2ul, kept.Owner);
        Assert.False(kept.Contains(100));
        Assert.NotEqual(version, kept.Version);
    }
}
