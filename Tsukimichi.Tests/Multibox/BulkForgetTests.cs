using Tsukimichi.Core.Characters;

namespace Tsukimichi.Tests.Multibox;

/// <summary>
/// "Forget characters not seen in N days" at confirm time (1.8.0, R7 H): the characters the question named are checked
/// again, so one played meanwhile is kept and counted.
/// </summary>
public sealed class BulkForgetTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static CharacterEntry Entry(ulong id, double daysAgo, bool here = false, bool elsewhere = false) =>
        new(id, "C" + id, 0, "Gilgamesh", "Aether", Now.AddDays(-daysAgo), 0, here, elsewhere);

    [Fact]
    public void Characters_that_still_qualify_are_forgotten_in_the_order_picked()
    {
        var picked = CharacterList.NotSeenFor([Entry(1, 200), Entry(2, 120), Entry(3, 10)], 90, Now);

        var plan = BulkForget.Recheck(picked, [Entry(1, 200), Entry(2, 120), Entry(3, 10)], 90, Now.AddMinutes(5));

        Assert.Equal([1UL, 2], plan.Forget);
        Assert.Equal(0, plan.Kept);
    }

    [Fact]
    public void A_character_played_elsewhere_and_logged_out_meanwhile_is_kept_and_counted()
    {
        var picked = new[] { Entry(1, 200), Entry(2, 120), Entry(3, 150) };

        // While the question was open: 2 was played on another client and logged out (a fresh save), 3 is logged in
        // there now.
        var now = new[] { Entry(1, 200), Entry(2, 0.01), Entry(3, 150, elsewhere: true) };
        var plan = BulkForget.Recheck(picked, now, 90, Now);

        Assert.Equal([1UL], plan.Forget);
        Assert.Equal(2, plan.Kept);
    }

    [Fact]
    public void A_character_logged_in_here_meanwhile_is_kept()
    {
        var plan = BulkForget.Recheck([Entry(1, 200)], [Entry(1, 200, here: true)], 90, Now);

        Assert.Empty(plan.Forget);
        Assert.Equal(1, plan.Kept);
    }

    [Fact]
    public void A_character_already_gone_is_neither_forgotten_nor_counted()
    {
        var plan = BulkForget.Recheck([Entry(1, 200), Entry(2, 300)], [Entry(2, 300)], 90, Now);

        Assert.Equal([2UL], plan.Forget);
        Assert.Equal(0, plan.Kept);
    }
}
