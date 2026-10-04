namespace Tsukimichi.Core.Ui;

/// <summary>
/// A one-shot request for something drawn later ("scroll the Duties board into view"): asked for on one frame and
/// taken by the draw that reaches it within <see cref="MaxAge"/> frames, else it lapses. So a request nothing reached
/// (the board hidden, another tab) never fires much later, out of the blue. Kept free of ImGui so the timing is tested.
/// </summary>
public struct FrameRequest
{
    /// <summary>How many frames after the request it may still be taken (asked after the draw that takes it, it is taken next frame).</summary>
    public const int MaxAge = 2;

    private bool pending;
    private int frame;

    /// <summary>Asks on <paramref name="now"/> (the frame count).</summary>
    public void Request(int now)
    {
        pending = true;
        frame = now;
    }

    /// <summary>True once for a request made at most <see cref="MaxAge"/> frames before <paramref name="now"/>; cleared either way.</summary>
    public bool Take(int now)
    {
        var taken = pending && now - frame is >= 0 and <= MaxAge;
        pending = false;
        return taken;
    }
}
