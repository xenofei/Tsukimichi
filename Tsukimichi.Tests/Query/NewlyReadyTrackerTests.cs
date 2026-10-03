using Tsukimichi.Core.Query;

namespace Tsukimichi.Tests.Query;

/// <summary>
/// The Journal badge's bookkeeping (<see cref="NewlyReadyTracker"/>): the seen set kept in memory for a character whose
/// set is not stored, no seeding from an empty or unsettled capture, nothing written for a character left or forgotten,
/// and a second click on the badge that does not empty the list.
/// </summary>
public class NewlyReadyTrackerTests
{
    private const ulong Alice = 1001;
    private const ulong Bob = 1002;

    /// <summary>A store like the per-character settings: <see cref="Tracked"/> false declines every write.</summary>
    private sealed class Store
    {
        public Dictionary<ulong, uint[]> Sets { get; } = [];

        public List<ulong> Writes { get; } = [];

        public bool Tracked { get; set; } = true;

        public NewlyReadyTracker Tracker() => new(id => Sets.GetValueOrDefault(id), Save);

        private bool Save(ulong id, uint[] seen)
        {
            if (!Tracked)
            {
                return false;
            }

            Writes.Add(id);
            Sets[id] = seen;
            return true;
        }
    }

    private static ReadyTally Available(params uint[] ids) => new(ids, ids.Length, 0);

    private static ReadyTally Range(uint from, uint count) =>
        Available([.. Enumerable.Range((int)from, (int)count).Select(static i => (uint)i)]);

    [Fact]
    public void An_untracked_character_keeps_its_seen_set_in_memory_instead_of_seeding_again()
    {
        var store = new Store { Tracked = false };
        var tracker = store.Tracker();
        tracker.SwitchTo(Alice);

        tracker.Recompute(Available(1, 2, 3), null, settled: true);
        Assert.Empty(tracker.New);

        // A quest made ready since is new: the set seeded above is kept, though nothing could be stored.
        tracker.Recompute(Available(1, 2, 3, 4), null, settled: true);
        Assert.Equal(new uint[] { 4 }, tracker.New);

        // A selection marks it seen, and the next evaluation remembers that.
        tracker.Select(4);
        Assert.Empty(tracker.New);
        tracker.Recompute(Available(1, 2, 3, 4, 5), null, settled: true);
        Assert.Equal(new uint[] { 5 }, tracker.New);
        Assert.Empty(store.Writes);
        Assert.Empty(store.Sets);
    }

    [Fact]
    public void Switching_character_starts_over()
    {
        var store = new Store { Tracked = false };
        var tracker = store.Tracker();
        tracker.SwitchTo(Alice);
        tracker.Recompute(Available(1, 2), null, settled: true);

        tracker.SwitchTo(Bob);
        Assert.Null(tracker.Seen);
        Assert.Empty(tracker.New);
        tracker.Recompute(Available(1, 2, 3), null, settled: true);
        Assert.Empty(tracker.New);
    }

    [Fact]
    public void An_empty_first_capture_seeds_nothing_so_the_real_one_is_not_all_new()
    {
        var store = new Store();
        var tracker = store.Tracker();
        tracker.SwitchTo(Alice);

        // A capture committed before the game sent the quest data (StatePoller's ReadyAfterTimeout): nothing available.
        tracker.Recompute(ReadyTally.Empty, null, settled: true);
        Assert.Null(tracker.Seen);
        Assert.Empty(store.Writes);

        tracker.Recompute(Range(1, 300), null, settled: true);
        Assert.Empty(tracker.New);
        Assert.Equal(300, store.Sets[Alice].Length);
    }

    [Fact]
    public void An_unsettled_capture_does_not_seed_but_a_stored_set_still_counts()
    {
        var store = new Store();
        var tracker = store.Tracker();
        tracker.SwitchTo(Alice);

        tracker.Recompute(Available(1, 2, 3), null, settled: false);
        Assert.Empty(tracker.New);
        Assert.Null(tracker.Seen);
        Assert.Empty(store.Writes);

        // A stored set is compared whatever the capture: only seeding waits for one with quest data.
        store.Sets[Bob] = [1, 2];
        tracker.SwitchTo(Bob);
        tracker.Recompute(Available(1, 2, 3), null, settled: false);
        Assert.Equal(new uint[] { 3 }, tracker.New);
    }

    [Fact]
    public void Leaving_a_character_with_its_list_open_writes_nothing_for_it()
    {
        var store = new Store();
        store.Sets[Alice] = [1];
        var tracker = store.Tracker();
        tracker.SwitchTo(Alice);
        tracker.Recompute(Available(1, 2, 3), null, settled: true);
        Assert.NotNull(tracker.Open());

        // Forget character: its entry goes, then the view moves to another character.
        store.Sets.Remove(Alice);
        tracker.SwitchTo(Bob);

        Assert.False(store.Sets.ContainsKey(Alice));
        Assert.DoesNotContain(Alice, store.Writes);
        Assert.Null(tracker.OpenIds);
    }

    [Fact]
    public void A_character_forgotten_while_viewed_is_not_written_again()
    {
        var store = new Store();
        var tracker = store.Tracker();
        tracker.SwitchTo(Alice);
        tracker.Recompute(Available(1, 2), null, settled: true);
        Assert.True(store.Sets.ContainsKey(Alice));
        store.Writes.Clear();

        // Forgotten (or "Delete all data") while still on view: the badge carries on in memory, writing nothing.
        store.Sets.Remove(Alice);
        tracker.Recompute(Available(1, 2, 3), null, settled: true);
        Assert.Equal(new uint[] { 3 }, tracker.New);
        tracker.Select(3);
        Assert.Empty(tracker.New);
        Assert.Empty(store.Writes);
        Assert.False(store.Sets.ContainsKey(Alice));
    }

    [Fact]
    public void Opening_the_list_again_lists_the_same_quests_rather_than_none()
    {
        var store = new Store();
        store.Sets[Alice] = [1];
        var tracker = store.Tracker();
        tracker.SwitchTo(Alice);
        tracker.Recompute(Available(1, 2, 3), null, settled: true);

        Assert.Equal(new uint[] { 2, 3 }, tracker.Open());

        // A second click replaces the list: the quests are taken before the old list marks them seen.
        Assert.Equal(new uint[] { 2, 3 }, tracker.Open());
        Assert.Equal(new uint[] { 2, 3 }, tracker.OpenIds);

        // Closing it marks them seen; with none new a click changes nothing.
        tracker.CloseList(markSeen: true);
        Assert.Empty(tracker.New);
        Assert.Null(tracker.Open());
        Assert.Null(tracker.OpenIds);
    }
}
