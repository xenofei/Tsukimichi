using Tsukimichi.Core.Moonfall.Art;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>
/// Frames a <see cref="MoonfallGameArt{T}"/> until a condition holds. Its reads and builds run on the thread pool and take
/// as long as the machine is busy (in a full test run beside other work, a scene build of a second or two has taken more
/// than thirty), so the wait follows the art's own signal and never a set time: it frames on while
/// <see cref="MoonfallGameArt{T}.Running"/> is above zero. With nothing running, everything started has finished, and a
/// few frames land it; a condition still false after <see cref="LandingFrames"/> such frames never will hold, and the
/// wait fails at once rather than at a deadline. Only a hang (work that never finishes) meets <see cref="HangCap"/>.
/// </summary>
internal static class MoonfallArtWait
{
    /// <summary>Frames with nothing running that land what finished: a build's result, then its upload (the tests' hosts upload at once).</summary>
    public const int LandingFrames = 4;

    /// <summary>
    /// The safety cap for a hang: far beyond any build on a loaded machine (the slowest seen took under a minute), so it
    /// never judges a slow build, only stops a deadlock from holding the test run forever.
    /// </summary>
    public static readonly TimeSpan HangCap = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Calls <paramref name="frame"/> until <paramref name="done"/>; fails when nothing is running and it is still not
    /// done, or on a hang (<paramref name="hangCap"/>, <see cref="HangCap"/> by default; a test of the cap itself sets less).
    /// </summary>
    public static void Until<T>(MoonfallGameArt<T> art, Func<bool> done, Action frame, TimeSpan? hangCap = null)
        where T : class
    {
        var cap = hangCap ?? HangCap;
        var idle = 0;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!done())
        {
            // Read before the frame: with nothing running now, all that was started has finished, and this frame lands it.
            idle = art.Running == 0 ? idle + 1 : 0;
            Assert.True(idle <= LandingFrames, "never settled: nothing is reading or building, and the art is still not there");
            if (clock.Elapsed > cap)
            {
                Assert.Fail($"hung: after {cap.TotalSeconds:0} s still running: {string.Join(", ", art.RunningWork())}");
            }

            frame();
            if (idle == 0)
            {
                Thread.Sleep(2);
            }
        }
    }
}
