using Tsukimichi.Core.Moonfall.Art;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>
/// Frames a <see cref="MoonfallGameArt{T}"/> until a condition holds. Its reads and builds run on the thread pool and take
/// as long as the machine is busy (in a full test run beside other work, a scene build of a second or two has taken more
/// than thirty), so the wait follows the art's own signal and never a set time: it frames on while
/// <see cref="MoonfallGameArt{T}.Running"/> is above zero. With nothing running, everything started has finished, and a
/// few frames land it; a condition still false after <see cref="LandingFrames"/> such frames never will hold, and the
/// wait fails at once rather than at a deadline.
/// </summary>
internal static class MoonfallArtWait
{
    /// <summary>Frames with nothing running that land what finished: a build's result, then its upload (the tests' hosts upload at once).</summary>
    public const int LandingFrames = 4;

    /// <summary>Calls <paramref name="frame"/> until <paramref name="done"/>; fails when nothing is running and it is still not done.</summary>
    public static void Until<T>(MoonfallGameArt<T> art, Func<bool> done, Action frame)
        where T : class
    {
        var idle = 0;
        while (!done())
        {
            // Read before the frame: with nothing running now, all that was started has finished, and this frame lands it.
            idle = art.Running == 0 ? idle + 1 : 0;
            Assert.True(idle <= LandingFrames, "never settled: nothing is reading or building, and the art is still not there");
            frame();
            if (idle == 0)
            {
                Thread.Sleep(2);
            }
        }
    }
}
