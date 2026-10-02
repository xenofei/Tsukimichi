using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Tests.Runtime;

/// <summary>
/// Review 1.10: containers that report loaded while already cleared (at logout) read as no relic weapon at all, and
/// the stored character then read Blocked on every relic weapon step until the next login.
/// </summary>
public class GateItemGuardTests
{
    private const uint Watch = 0x5EED;
    private const uint CurtanaNexus = 8649;
    private const uint HolyShieldNexus = 8658;

    private static readonly GateItemCapture Armed = new(Watch, [CurtanaNexus, HolyShieldNexus], [CurtanaNexus, HolyShieldNexus]);
    private static readonly GateItemCapture Carried = new(Watch, [], [CurtanaNexus]);
    private static readonly GateItemCapture Empty = new(Watch, [], []);

    [Fact]
    public void The_logout_capture_keeps_the_committed_read_whatever_it_finds()
    {
        Assert.Same(Armed, GateItemGuard.Settle(Armed, Empty, loggingOut: true));
        Assert.Same(Armed, GateItemGuard.Settle(Armed, Carried, loggingOut: true));
        Assert.Same(Armed, GateItemGuard.Settle(Armed, null, loggingOut: true));
        Assert.Null(GateItemGuard.Settle(null, Armed, loggingOut: true));
    }

    [Fact]
    public void A_sudden_empty_read_is_unread_until_it_persists_across_two_polls()
    {
        // First poll: weapons before, none now: taken as unread.
        Assert.Null(GateItemGuard.Settle(Armed, Empty, loggingOut: false));
        Assert.Null(GateItemGuard.Settle(Carried, Empty, loggingOut: false));

        // Second poll: the committed read is now null, and the same empty read is taken in.
        Assert.Same(Empty, GateItemGuard.Settle(null, Empty, loggingOut: false));

        // Back to normal: the weapons are read again at once.
        Assert.Same(Armed, GateItemGuard.Settle(null, Armed, loggingOut: false));
    }

    [Fact]
    public void Any_other_read_is_taken_as_read()
    {
        Assert.Same(Carried, GateItemGuard.Settle(Armed, Carried, loggingOut: false));
        Assert.Same(Empty, GateItemGuard.Settle(Empty, Empty, loggingOut: false));
        Assert.Null(GateItemGuard.Settle(Armed, null, loggingOut: false));

        // A read against another weapon list (a catalog rebuild) is not compared with the old one.
        var otherList = new GateItemCapture(Watch + 1, [], []);
        Assert.Same(otherList, GateItemGuard.Settle(Armed, otherList, loggingOut: false));
    }
}
