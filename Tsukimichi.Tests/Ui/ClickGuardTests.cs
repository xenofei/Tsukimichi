using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The armed items of the safety table (feature plan v6 S1): Mark as unique, Not unique, Restore and Don't track act
/// only on a click made while Ctrl or Shift is held, or on two clicks when the user chose that for hand strain.
/// </summary>
public class ClickGuardTests
{
    [Fact]
    public void A_click_with_the_key_held_acts_at_once()
    {
        var guard = new ClickGuard();

        Assert.True(guard.Click(modifierHeld: true, twoClick: false, now: 1.0));
        Assert.True(guard.Click(modifierHeld: true, twoClick: false, now: 1.1), "every armed click acts");
    }

    [Fact]
    public void A_plain_click_does_nothing_and_shows_the_hint_for_a_moment()
    {
        var guard = new ClickGuard();

        Assert.False(guard.Click(modifierHeld: false, twoClick: false, now: 5.0));
        Assert.True(guard.ShowRefusedHint(5.5));
        Assert.False(guard.ShowRefusedHint(5.0 + SafetyRules.RefusedHintSeconds + 0.1));
        Assert.False(guard.AwaitingSecond(5.5), "without two-click mode a plain click arms nothing");
    }

    [Fact]
    public void Enter_on_the_focused_button_does_not_count_without_the_key()
    {
        // ImGui reports Enter (or the gamepad's accept) on a focused button as a click: it is a plain click here.
        var guard = new ClickGuard();
        for (var i = 0; i < 5; i++)
        {
            Assert.False(guard.Click(modifierHeld: false, twoClick: false, now: 1.0 + i));
        }
    }

    [Fact]
    public void Plain_clicks_never_add_up_to_an_action()
    {
        var guard = new ClickGuard();

        Assert.False(guard.Click(modifierHeld: false, twoClick: false, now: 1.0));
        Assert.False(guard.Click(modifierHeld: false, twoClick: false, now: 1.5), "two plain clicks are not two-click mode");
    }

    [Fact]
    public void A_key_held_before_the_pointer_arrived_still_counts()
    {
        // Shift pressed first, then the pointer moved over the item and clicked: the click sees the key down.
        var guard = new ClickGuard();
        Assert.True(guard.Click(modifierHeld: true, twoClick: false, now: 3.0));
    }

    [Fact]
    public void Two_click_mode_acts_on_the_second_click()
    {
        var guard = new ClickGuard();

        Assert.False(guard.Click(modifierHeld: false, twoClick: true, now: 10.0));
        Assert.True(guard.AwaitingSecond(10.5));
        Assert.True(guard.Click(modifierHeld: false, twoClick: true, now: 11.0));
        Assert.False(guard.AwaitingSecond(11.1), "the guard starts over after acting");
        Assert.False(guard.Click(modifierHeld: false, twoClick: true, now: 11.5), "a third click is a new first click");
    }

    [Fact]
    public void A_double_click_is_not_two_clicks()
    {
        var guard = new ClickGuard();

        Assert.False(guard.Click(modifierHeld: false, twoClick: true, now: 10.0));
        Assert.False(guard.Click(modifierHeld: false, twoClick: true, now: 10.0 + (SafetyRules.SecondClickMinGapSeconds / 2)));
        Assert.True(guard.AwaitingSecond(10.2), "still waiting for a deliberate second click");
        Assert.True(guard.Click(modifierHeld: false, twoClick: true, now: 10.0 + SafetyRules.SecondClickMinGapSeconds + 0.05));
    }

    [Fact]
    public void A_late_second_click_starts_over()
    {
        var guard = new ClickGuard();

        Assert.False(guard.Click(modifierHeld: false, twoClick: true, now: 10.0));
        var late = 10.0 + SafetyRules.SecondClickWindowSeconds + 0.5;
        Assert.False(guard.AwaitingSecond(late));
        Assert.False(guard.Click(modifierHeld: false, twoClick: true, now: late), "too late: this is a first click");
        Assert.True(guard.Click(modifierHeld: false, twoClick: true, now: late + 1.0));
    }

    [Fact]
    public void The_key_still_acts_at_once_in_two_click_mode()
    {
        var guard = new ClickGuard();
        Assert.True(guard.Click(modifierHeld: true, twoClick: true, now: 1.0));
    }

    [Fact]
    public void A_second_click_on_another_row_is_a_first_click_there()
    {
        // One guard serves every Restore button in Settings' verdict list.
        var guard = new ClickGuard();

        Assert.False(guard.Click(modifierHeld: false, twoClick: true, now: 1.0, target: 7));
        Assert.True(guard.AwaitingSecond(1.5, target: 7));
        Assert.False(guard.AwaitingSecond(1.5, target: 8));
        Assert.False(guard.Click(modifierHeld: false, twoClick: true, now: 2.0, target: 8), "row 8 was never clicked");
        Assert.True(guard.Click(modifierHeld: false, twoClick: true, now: 3.0, target: 8));
    }

    [Fact]
    public void The_hint_belongs_to_the_row_that_was_clicked()
    {
        var guard = new ClickGuard();

        guard.Click(modifierHeld: false, twoClick: false, now: 1.0, target: 3);
        Assert.True(guard.ShowRefusedHint(1.2, target: 3));
        Assert.False(guard.ShowRefusedHint(1.2, target: 4));
    }

    [Fact]
    public void Cancel_forgets_a_first_click_and_the_hint()
    {
        var guard = new ClickGuard();
        guard.Click(modifierHeld: false, twoClick: true, now: 1.0);
        guard.Click(modifierHeld: false, twoClick: false, now: 1.0);

        guard.Cancel();

        Assert.False(guard.AwaitingSecond(1.5));
        Assert.False(guard.ShowRefusedHint(1.5));
        Assert.False(guard.Click(modifierHeld: false, twoClick: true, now: 2.0), "after Cancel the next click is a first click");
    }

    // ---- 1.11.0 review: a menu's first click ends with the menu ----

    [Fact]
    public void A_menu_first_click_lasts_while_the_menu_is_drawn()
    {
        var guard = new ClickGuard();
        Assert.False(guard.Click(modifierHeld: false, twoClick: true, now: 1.0, target: 5, inMenu: true));
        Assert.True(guard.MenuShown(frame: 100, target: 5));

        for (var frame = 101L; frame < 110; frame++)
        {
            Assert.True(guard.KeepMenuArm(frame));
            Assert.True(guard.MenuShown(frame, target: 5));
        }

        Assert.True(guard.AwaitingSecond(1.5, target: 5, inMenu: true));
        Assert.True(guard.Click(modifierHeld: false, twoClick: true, now: 2.0, target: 5, inMenu: true));
    }

    [Fact]
    public void Closing_the_menu_forgets_its_first_click()
    {
        var guard = new ClickGuard();
        guard.Click(modifierHeld: false, twoClick: true, now: 1.0, target: 5, inMenu: true);
        guard.MenuShown(frame: 100, target: 5);
        Assert.True(guard.KeepMenuArm(101));

        // Frame 101 drew no menu (it closed): frame 102 starts without the first click.
        Assert.False(guard.KeepMenuArm(102));
        Assert.False(guard.AwaitingSecond(1.5, target: 5, inMenu: true));

        // Reopened in time, the menu shows the plain label and the next click is a first click again.
        Assert.False(guard.MenuShown(frame: 103, target: 5));
        Assert.False(guard.Click(modifierHeld: false, twoClick: true, now: 2.0, target: 5, inMenu: true));
    }

    [Fact]
    public void Another_rows_menu_does_not_keep_a_first_click_alive()
    {
        var guard = new ClickGuard();
        guard.Click(modifierHeld: false, twoClick: true, now: 1.0, target: 5, inMenu: true);
        guard.MenuShown(frame: 100, target: 5);

        // Row 5's menu closed and row 6's opened at once: row 6's item never renews row 5's click.
        Assert.False(guard.MenuShown(frame: 101, target: 6));
        Assert.False(guard.KeepMenuArm(102));
        Assert.False(guard.AwaitingSecond(1.5, target: 5, inMenu: true));
    }

    [Fact]
    public void A_menu_click_and_a_button_click_never_make_two_clicks()
    {
        // One guard serves "Not unique" in a row's menu and "Mark as unique" in the detail pane, for the same row.
        var guard = new ClickGuard();

        Assert.False(guard.Click(modifierHeld: false, twoClick: true, now: 1.0, target: 5, inMenu: true));
        Assert.False(guard.AwaitingSecond(1.5, target: 5), "the button does not show Click again");
        Assert.False(guard.Click(modifierHeld: false, twoClick: true, now: 2.0, target: 5), "a button click is a first click there");
        Assert.True(guard.AwaitingSecond(2.5, target: 5));
        Assert.False(guard.AwaitingSecond(2.5, target: 5, inMenu: true));
    }

    [Fact]
    public void A_button_first_click_needs_no_menu()
    {
        var guard = new ClickGuard();
        guard.Click(modifierHeld: false, twoClick: true, now: 1.0, target: 5);

        Assert.False(guard.MenuShown(frame: 100, target: 5));
        Assert.False(guard.KeepMenuArm(200));
        Assert.True(guard.AwaitingSecond(1.5, target: 5), "the sweep leaves a button's first click alone");
    }

    [Fact]
    public void A_clock_that_went_backwards_does_not_act()
    {
        var guard = new ClickGuard();
        guard.Click(modifierHeld: false, twoClick: true, now: 100.0);
        Assert.False(guard.Click(modifierHeld: false, twoClick: true, now: 50.0));
    }
}
