using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>Back and forward through the quests the player looked at (feature plan v7 N1).</summary>
public class QuestHistoryTests
{
    private static QuestHistory Walk(params uint[] visits)
    {
        var history = new QuestHistory();
        foreach (var id in visits)
        {
            history.Visit(id);
        }

        return history;
    }

    [Fact]
    public void An_empty_history_goes_nowhere()
    {
        var history = new QuestHistory();
        Assert.Null(history.Current);
        Assert.Equal(-1, history.Index);
        Assert.False(history.CanGoBack(null));
        Assert.False(history.CanGoForward(null));
        Assert.False(history.CanGoBack(7u));
        Assert.Null(history.Back(null));
        Assert.Null(history.Forward(null));
        Assert.Equal(-1, history.Index);
    }

    [Fact]
    public void One_quest_has_no_back_while_it_shows()
    {
        var history = Walk(1);
        Assert.False(history.CanGoBack(1u));
        Assert.False(history.CanGoForward(1u));
    }

    [Fact]
    public void Visits_append_and_back_and_forward_walk_them()
    {
        var history = Walk(1, 2, 3);
        Assert.Equal([1u, 2u, 3u], history.Entries);
        Assert.Equal(3u, history.Current);

        Assert.Equal(2u, history.Back(3u));
        Assert.Equal(1u, history.Back(2u));
        Assert.Null(history.Back(1u));
        Assert.Equal(1u, history.Current);

        Assert.Equal(2u, history.Forward(1u));
        Assert.Equal(3u, history.Forward(2u));
        Assert.Null(history.Forward(3u));
        Assert.Equal(3u, history.Current);
    }

    [Fact]
    public void Back_and_forward_add_no_entry()
    {
        var history = Walk(1, 2, 3);
        history.Back(3u);
        history.Back(2u);
        history.Forward(1u);
        Assert.Equal(3, history.Count);
        Assert.Equal([1u, 2u, 3u], history.Entries);
    }

    [Fact]
    public void Landing_after_back_and_reporting_it_as_a_visit_changes_nothing()
    {
        // The window may see the selection Back made as a change; the visit is the current entry and is skipped.
        var history = Walk(1, 2, 3);
        var landed = history.Back(3u)!.Value;
        history.Visit(landed);
        Assert.Equal([1u, 2u, 3u], history.Entries);
        Assert.True(history.CanGoForward(landed));
    }

    [Fact]
    public void A_new_jump_after_back_drops_the_forward_entries()
    {
        var history = Walk(1, 2, 3, 4);
        history.Back(4u);
        history.Back(3u);
        history.Visit(9);
        Assert.Equal([1u, 2u, 9u], history.Entries);
        Assert.Equal(9u, history.Current);
        Assert.False(history.CanGoForward(9u));
        Assert.Equal(2u, history.PeekBack(9u));
    }

    [Fact]
    public void Visiting_the_current_quest_again_keeps_the_forward_entries()
    {
        var history = Walk(1, 2, 3);
        history.Back(3u);
        history.Visit(2);
        Assert.Equal([1u, 2u, 3u], history.Entries);
        Assert.Equal(3u, history.PeekForward(2u));
    }

    [Fact]
    public void Consecutive_duplicates_are_skipped()
    {
        var history = Walk(1, 1, 2, 2, 2, 1);
        Assert.Equal([1u, 2u, 1u], history.Entries);
    }

    [Fact]
    public void The_oldest_entries_go_past_the_capacity()
    {
        var history = new QuestHistory();
        for (uint id = 1; id <= 60; id++)
        {
            history.Visit(id);
        }

        Assert.Equal(QuestHistory.DefaultCapacity, history.Count);
        Assert.Equal(11u, history.Entries[0]);
        Assert.Equal(60u, history.Current);
        Assert.Equal(QuestHistory.DefaultCapacity - 1, history.Index);

        // Walking all the way back ends at the oldest kept entry.
        uint? shown = 60u;
        var steps = 0;
        while (history.Back(shown) is { } previous)
        {
            shown = previous;
            steps++;
        }

        Assert.Equal(QuestHistory.DefaultCapacity - 1, steps);
        Assert.Equal(11u, shown);
    }

    [Fact]
    public void A_small_capacity_is_honoured_after_back()
    {
        var history = new QuestHistory(3);
        history.Visit(1);
        history.Visit(2);
        history.Visit(3);
        history.Back(3u);
        history.Visit(4);
        history.Visit(5);
        Assert.Equal([2u, 4u, 5u], history.Entries);
        Assert.Equal(5u, history.Current);
        Assert.Equal(2, history.Index);
    }

    [Fact]
    public void A_capacity_under_one_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuestHistory(0));
    }

    [Fact]
    public void With_nothing_selected_back_shows_the_current_entry_again()
    {
        var history = Walk(1, 2);
        Assert.True(history.CanGoBack(null));
        Assert.Equal(2u, history.PeekBack(null));
        Assert.Equal(2u, history.Back(null));
        Assert.Equal(1, history.Index);

        // Then on as usual.
        Assert.Equal(1u, history.Back(2u));
    }

    [Fact]
    public void A_single_entry_is_one_back_away_from_nothing_selected()
    {
        var history = Walk(5);
        Assert.Equal(5u, history.Back(null));
        Assert.False(history.CanGoBack(5u));
    }

    [Fact]
    public void An_unrecorded_quest_showing_goes_back_to_the_current_entry()
    {
        var history = Walk(1, 2);
        Assert.Equal(2u, history.Back(77u));
    }

    [Fact]
    public void Forward_from_nothing_selected_goes_to_the_next_entry()
    {
        var history = Walk(1, 2, 3);
        history.Back(3u);
        Assert.Equal(3u, history.Forward(null));
    }

    [Fact]
    public void Quests_no_longer_in_the_catalog_are_stepped_over()
    {
        var history = Walk(1, 2, 3, 4, 5);
        var gone = new HashSet<uint> { 2, 4 };
        bool Exists(uint id) => !gone.Contains(id);

        Assert.Equal(3u, history.Back(5u, Exists));
        Assert.Equal(1u, history.Back(3u, Exists));
        Assert.False(history.CanGoBack(1u, Exists));
        Assert.Equal(3u, history.Forward(1u, Exists));
        Assert.Equal(5u, history.Forward(3u, Exists));

        // Stepped over, not removed: a catalog that has them again finds them.
        Assert.Equal(5, history.Count);
        Assert.Equal(4u, history.PeekBack(5u));
    }

    [Fact]
    public void Back_with_every_earlier_quest_gone_stays_put()
    {
        var history = Walk(1, 2, 3);
        Assert.False(history.CanGoBack(3u, static id => id == 3));
        Assert.Null(history.Back(3u, static id => id == 3));
        Assert.Equal(3u, history.Current);
    }

    [Fact]
    public void An_entry_equal_to_the_quest_shown_is_stepped_over()
    {
        // 1, 2, 1 with 2 gone: Back from the last 1 would land on 1 again, which changes nothing, so there is no Back.
        var history = Walk(1, 2, 1);
        Assert.False(history.CanGoBack(1u, static id => id != 2));
        Assert.True(history.CanGoBack(1u));
    }

    [Fact]
    public void Peeking_does_not_move()
    {
        var history = Walk(1, 2, 3);
        Assert.Equal(2u, history.PeekBack(3u));
        Assert.Null(history.PeekForward(3u));
        Assert.Equal(2, history.Index);
    }

    [Fact]
    public void Clear_forgets_everything()
    {
        var history = Walk(1, 2, 3);
        history.Clear();
        Assert.Equal(0, history.Count);
        Assert.Null(history.Current);
        Assert.False(history.CanGoBack(null));
        history.Visit(4);
        Assert.Equal([4u], history.Entries);
    }
}
