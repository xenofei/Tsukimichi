using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The spoiler shield's one right-click menu (spec-1.20 N6; <see cref="PlaceholderMenu{T}"/>), from the 1.20.0 UI
/// review: the menu belongs to the window that drew it, not to the placeholder that opened it, so it stays while that
/// placeholder is clipped out or skipped; and one right-click on a second placeholder moves it there.
/// </summary>
public sealed class PlaceholderMenuTests
{
    private const string Main = "MainWindow";
    private const string Route = "RouteWindow";

    [Fact]
    public void A_request_opens_once_in_the_first_host_that_draws_and_stays_with_it()
    {
        var menu = new PlaceholderMenu<string>();
        menu.Request("Dawntrail area 3");

        Assert.True(menu.TakeOpen(Main));
        Assert.False(menu.TakeOpen(Main));
        Assert.False(menu.TakeOpen(Route));
        Assert.True(menu.Owns(Main));
        Assert.False(menu.Owns(Route));
    }

    [Fact]
    public void The_menu_stays_while_the_placeholder_that_opened_it_is_not_drawn()
    {
        var menu = new PlaceholderMenu<string>();
        menu.Request("Dungeon (Lv 97)");
        menu.TakeOpen(Route);

        // Frames go by with the opening line clipped out of the Route window: no placeholder asks again.
        for (var frame = 0; frame < 10; frame++)
        {
            Assert.True(menu.Owns(Route));
            Assert.Equal("Dungeon (Lv 97)", menu.Target);
        }

        // Another host's frame never closes it.
        menu.Closed(Main);
        Assert.True(menu.Owns(Route));

        menu.Closed(Route);
        Assert.False(menu.Owns(Route));
        Assert.Null(menu.Target);
    }

    [Fact]
    public void One_right_click_on_a_second_placeholder_moves_the_menu_there()
    {
        // The release opens it whether or not a menu is open: the press already closed the first.
        Assert.True(PlaceholderMenu<string>.Opens(hovered: true, rightReleased: true));
        Assert.False(PlaceholderMenu<string>.Opens(hovered: false, rightReleased: true));
        Assert.False(PlaceholderMenu<string>.Opens(hovered: true, rightReleased: false));

        var menu = new PlaceholderMenu<string>();
        menu.Request("Dawntrail area 3");
        menu.TakeOpen(Main);

        menu.Request("Dawntrail character");
        // The first menu closes under the press; the new request survives it and opens on the same frame's draw.
        menu.Closed(Main);
        Assert.Equal("Dawntrail character", menu.Target);
        Assert.True(menu.TakeOpen(Main));
        Assert.True(menu.Owns(Main));
    }
}
