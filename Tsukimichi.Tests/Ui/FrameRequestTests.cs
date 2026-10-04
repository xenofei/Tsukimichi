using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The Before Evercold card's "Duties board" asks the Characters dashboard to scroll the board into view (spec-1.20 N7).
/// The request is taken by the next draw that reaches the board, and never fires much later, after the player went
/// elsewhere.
/// </summary>
public sealed class FrameRequestTests
{
    [Fact]
    public void A_request_is_taken_once_by_the_draw_that_reaches_it()
    {
        var request = default(FrameRequest);
        Assert.False(request.Take(10));

        request.Request(10);
        Assert.True(request.Take(10));
        Assert.False(request.Take(10));

        // Asked from the Tonight card (drawn after the dashboard), taken the next frame.
        request.Request(20);
        Assert.True(request.Take(21));
    }

    [Fact]
    public void A_request_nothing_reached_expires_instead_of_firing_later()
    {
        var request = default(FrameRequest);
        request.Request(10);

        // The board was not drawn for a while (another tab, the board hidden): the request is gone, not waiting.
        Assert.False(request.Take(10 + FrameRequest.MaxAge + 1));
        Assert.False(request.Take(500));
    }
}
