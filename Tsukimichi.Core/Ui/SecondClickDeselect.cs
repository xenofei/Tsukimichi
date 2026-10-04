namespace Tsukimichi.Core.Ui;

/// <summary>
/// "A second click on the selected row goes back to Tonight" (plan v7, 1.21.0 P1) without a double-click flashing
/// Tonight: a single click on the selected row only asks (<see cref="Click"/>), and the row is deselected once the
/// double-click time has passed with no second click (<see cref="Due"/>). A double-click (<see cref="DoubleClick"/>),
/// a click on another row, or the selection moving elsewhere drops the ask. Pure; fed ImGui's time.
/// </summary>
public sealed class SecondClickDeselect
{
    private uint row;
    private double at;

    /// <summary>The row waiting to be deselected; 0 for none.</summary>
    public uint Pending => row;

    /// <summary>A single click on the selected row <paramref name="rowId"/> at <paramref name="now"/>.</summary>
    public void Click(uint rowId, double now)
    {
        row = rowId;
        at = now;
    }

    /// <summary>A double-click (or a click on another row): nothing is deselected.</summary>
    public void DoubleClick() => row = 0;

    /// <summary>
    /// Once a frame: the row to deselect now, when the double-click time passed since its click with no second click
    /// and it is still <paramref name="selected"/>; else 0. The ask ends either way once it is answered.
    /// </summary>
    public uint Due(double now, double doubleClickTime, uint? selected)
    {
        if (row == 0)
        {
            return 0;
        }

        if (selected != row)
        {
            row = 0;
            return 0;
        }

        if (now - at <= doubleClickTime)
        {
            return 0;
        }

        var due = row;
        row = 0;
        return due;
    }
}
