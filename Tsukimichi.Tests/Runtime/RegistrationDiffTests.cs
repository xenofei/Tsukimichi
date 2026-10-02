using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Tests.Runtime;

public sealed class RegistrationDiffTests
{
    private static bool SameText(string a, string b) => string.Equals(a, b, StringComparison.Ordinal);

    [Fact]
    public void Lists_only_the_positions_whose_entry_changed()
    {
        // One MSQ completion unmasks one quest: its own entry and a reward entry naming it change, nothing else.
        string[] current = ["Main scenario quest (Lv 83)", "Hunt", "Reward · Main scenario quest (Lv 83)", "Other"];
        string[] next = ["The Gift of Mercy", "Hunt", "Reward · The Gift of Mercy", "Other"];

        var changed = RegistrationDiff.ChangedIndices(current, next, SameText);

        Assert.Equal([0, 2], changed);
    }

    [Fact]
    public void Identical_lists_change_nothing()
    {
        string[] entries = ["a", "b", "c"];

        var changed = RegistrationDiff.ChangedIndices(entries, [.. entries], SameText);

        Assert.NotNull(changed);
        Assert.Empty(changed);
    }

    [Fact]
    public void A_length_change_asks_for_a_full_rebuild()
    {
        Assert.Null(RegistrationDiff.ChangedIndices(["a", "b"], ["a", "b", "c"], SameText));
        Assert.Null(RegistrationDiff.ChangedIndices(["a", "b"], ["a"], SameText));
    }

    private static bool Unchanged(int position) => true;

    [Fact]
    public void An_order_the_registry_already_holds_is_kept_whole()
    {
        // Registered 0, 1, 2, 3 in that order.
        long[] sequence = [1, 2, 3, 4];

        Assert.Equal(4, RegistrationDiff.KeptPrefix([0, 1, 2, 3], sequence, Unchanged));
        Assert.Equal(0, RegistrationDiff.KeptPrefix([], sequence, Unchanged));
    }

    [Fact]
    public void An_entry_moved_back_costs_only_itself_and_what_now_follows_it()
    {
        // 1 moves to the end: 0, 2 and 3 already sit in that order, so only 1 is registered again (appended).
        long[] sequence = [1, 2, 3, 4];

        Assert.Equal(3, RegistrationDiff.KeptPrefix([0, 2, 3, 1], sequence, Unchanged));

        // 3 moves to the front: it stays, and 0, 1 and 2 are registered again behind it.
        Assert.Equal(1, RegistrationDiff.KeptPrefix([3, 0, 1, 2], sequence, Unchanged));
    }

    [Fact]
    public void An_unregistered_or_changed_entry_ends_the_kept_prefix()
    {
        // 2 was never registered (a batch gave up), or was unregistered and not yet registered again.
        Assert.Equal(2, RegistrationDiff.KeptPrefix([0, 1, 2, 3], [1, 2, 0, 4], Unchanged));

        // 1's text changed (the spoiler shield moved): it and everything after it are registered again.
        Assert.Equal(1, RegistrationDiff.KeptPrefix([0, 1, 2, 3], [1, 2, 3, 4], position => position != 1));

        // A registry that holds nothing keeps nothing.
        Assert.Equal(0, RegistrationDiff.KeptPrefix([0, 1], [], Unchanged));
    }

    [Fact]
    public void Re_registering_after_the_kept_prefix_leaves_the_registry_in_the_new_order()
    {
        // Wotsit's list for one plugin: registering appends, re-registering moves an entry to the end.
        int[][] orders = [[0, 1, 2, 3, 4, 5], [2, 0, 1, 3, 4, 5], [2, 0, 3, 4, 5, 1], [5, 4, 3, 2, 1, 0], [0, 1, 2, 3, 4, 5]];
        var sequence = new long[6];
        long next = 0;
        foreach (var order in orders)
        {
            var kept = RegistrationDiff.KeptPrefix(order, sequence, Unchanged);
            foreach (var position in order[kept..])
            {
                sequence[position] = ++next;
            }

            Assert.Equal(order, Enumerable.Range(0, sequence.Length).OrderBy(p => sequence[p]));
        }
    }
}
