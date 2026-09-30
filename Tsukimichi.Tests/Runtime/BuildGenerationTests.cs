using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Tests.Runtime;

/// <summary>
/// The supersession rule behind the catalog rebuild: a filing flip or a retry during a build makes the in-flight
/// build's result stale, and only the latest build started may hand its bundle to the session.
/// </summary>
public sealed class BuildGenerationTests
{
    [Fact]
    public void Nothing_is_current_before_the_first_build()
    {
        var generation = new BuildGeneration();

        Assert.Equal(0, generation.Current);
        Assert.False(generation.IsCurrent(1));
    }

    [Fact]
    public void The_first_build_is_current_until_another_starts()
    {
        var generation = new BuildGeneration();

        var first = generation.Start();
        Assert.Equal(1, first);
        Assert.True(generation.IsCurrent(first));

        var second = generation.Start();
        Assert.Equal(2, second);
        Assert.False(generation.IsCurrent(first));
        Assert.True(generation.IsCurrent(second));
    }

    [Fact]
    public void A_stale_build_finishing_after_the_newer_one_stays_stale()
    {
        // Click Legacy, then Refiled, while the Legacy build still maps sheets: the Legacy build (first) lands
        // second and must not replace the Refiled catalog.
        var generation = new BuildGeneration();
        var legacy = generation.Start();
        var refiled = generation.Start();

        // The Refiled build finishes first and publishes.
        Assert.True(generation.IsCurrent(refiled));
        // The Legacy build finishes afterwards and is dropped.
        Assert.False(generation.IsCurrent(legacy));
        // Nothing started since, so the published one stays current.
        Assert.True(generation.IsCurrent(refiled));
    }

    [Fact]
    public void Tickets_are_unique_and_monotonic_across_threads()
    {
        var generation = new BuildGeneration();
        const int Builds = 64;
        var tickets = new int[Builds];

        Parallel.For(0, Builds, i => tickets[i] = generation.Start());

        Assert.Equal(Builds, tickets.Distinct().Count());
        Assert.Equal(Builds, generation.Current);
        Assert.Single(tickets, t => generation.IsCurrent(t));
    }
}
