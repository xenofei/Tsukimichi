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

    [Fact]
    public void A_clock_that_went_backwards_does_not_act()
    {
        var guard = new ClickGuard();
        guard.Click(modifierHeld: false, twoClick: true, now: 100.0);
        Assert.False(guard.Click(modifierHeld: false, twoClick: true, now: 50.0));
    }
}
