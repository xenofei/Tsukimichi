using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The orbit fill fills from empty once per node (Moon Road proposal §9): a row whose tween state is pruned out of view
/// starts again from the fraction it last showed (<see cref="FillMemory"/>), and scrolling of any kind pauses motion
/// (<see cref="ScrollWatch"/>).
/// </summary>
public class FillMemoryTests
{
    private const float Seconds = MotionMath.OrbitFillSeconds;

    /// <summary>What the tree does per visible row: tween from the remembered start, then remember the fraction.</summary>
    private static float Fill(MotionStore store, FillMemory memory, ulong key, float fraction, double now)
    {
        var shown = store.Tween(key, fraction, Seconds, now, animate: true, from: memory.StartFor(key));
        memory.Remember(key, fraction);
        return shown;
    }

    [Fact]
    public void A_node_fills_from_empty_the_first_time_only()
    {
        var store = new MotionStore();
        var memory = new FillMemory();
        const ulong key = 7;

        Assert.Equal(0f, Fill(store, memory, key, 0.6f, 0.0));
        Assert.Equal(0.6f, Fill(store, memory, key, 0.6f, 1.0));
        Assert.True(memory.Knows(key));

        // Out of view for more than two seconds: the tween state is pruned, the memory is not.
        Assert.Equal(1, store.Prune(3.5));
        Assert.False(store.Contains(key));

        // Back in view (a scrollbar drag, a reveal jump, a return to the tab): no refill from 0.
        Assert.Equal(0.6f, Fill(store, memory, key, 0.6f, 3.6));
        Assert.Equal(0.6f, Fill(store, memory, key, 0.6f, 3.7));
    }

    [Fact]
    public void A_count_that_changed_out_of_view_runs_from_the_old_fraction()
    {
        var store = new MotionStore();
        var memory = new FillMemory();
        const ulong key = 8;
        Fill(store, memory, key, 0.5f, 0.0);
        Fill(store, memory, key, 0.5f, 1.0);
        store.Prune(4.0);

        var start = Fill(store, memory, key, 0.8f, 4.1);
        Assert.Equal(0.5f, start);
        var mid = Fill(store, memory, key, 0.8f, 4.1 + (Seconds * 0.5));
        Assert.InRange(mid, 0.5f, 0.8f);
        Assert.Equal(0.8f, Fill(store, memory, key, 0.8f, 4.1 + Seconds + 0.01));
    }

    [Fact]
    public void Clearing_the_memory_lets_nodes_fill_again_and_remembers_clamped_fractions()
    {
        var memory = new FillMemory();
        memory.Remember(1, 1.5f);
        memory.Remember(2, float.NaN);
        Assert.Equal(1f, memory.StartFor(1));
        Assert.Equal(0f, memory.StartFor(2));
        Assert.Equal(0f, memory.StartFor(3));
        Assert.Equal(2, memory.Count);

        memory.Clear();
        Assert.False(memory.Knows(1));
        Assert.Equal(0f, memory.StartFor(1));
    }

    [Fact]
    public void A_watched_window_moves_when_its_scroll_changes_and_not_on_first_sight()
    {
        var watch = new ScrollWatch();
        Assert.False(watch.Moved(10, 120f));   // first sight
        Assert.False(watch.Moved(10, 120f));
        Assert.True(watch.Moved(10, 180f));    // a scrollbar drag, a keyboard scroll, a reveal jump
        Assert.False(watch.Moved(10, 180f));
        Assert.False(watch.Moved(11, 0f));     // another window, watched separately
        Assert.True(watch.Moved(11, 4f));
        Assert.False(watch.Moved(10, float.NaN));
        Assert.Equal(2, watch.Count);
        Assert.Equal(10u, watch.IdAt(0));
        Assert.Equal(11u, watch.IdAt(1));
    }

    [Fact]
    public void The_watch_holds_a_fixed_number_of_windows()
    {
        var watch = new ScrollWatch();
        for (var id = 0u; id < ScrollWatch.Capacity + 3; id++)
        {
            watch.Moved(id, 0f);
        }

        Assert.Equal(ScrollWatch.Capacity, watch.Count);
        Assert.False(watch.Moved(ScrollWatch.Capacity + 1, 50f));   // never watched, never a move
        watch.Clear();
        Assert.Equal(0, watch.Count);
    }
}
