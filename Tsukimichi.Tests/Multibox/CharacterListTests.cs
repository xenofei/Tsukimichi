using Tsukimichi.Core.Characters;

namespace Tsukimichi.Tests.Multibox;

/// <summary>
/// The alt lists' rules (1.8.0, R7 B, E, H): a stable order that another client's save never changes, a fixed Compare
/// default, data center runs, the search box, hidden characters, and which characters "forget not seen in N days" picks.
/// </summary>
public sealed class CharacterListTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static CharacterEntry Entry(ulong id, string name, string world = "Gilgamesh", string dc = "Aether", int daysAgo = 0, bool here = false, bool elsewhere = false, bool hidden = false) =>
        new(id, name, 0, world, dc, Now.AddDays(-daysAgo), 0, here, elsewhere, hidden);

    [Fact]
    public void Live_here_comes_first_then_live_elsewhere_then_by_name_and_world()
    {
        var sorted = CharacterList.Sort(
        [
            Entry(1, "zed"),
            Entry(2, "Bea", world: "Siren"),
            Entry(3, "bea", world: "Adamantoise"),
            Entry(4, "Mia", elsewhere: true),
            Entry(5, "Yui", here: true),
            Entry(6, "Ann", elsewhere: true),
        ]);

        Assert.Equal([5UL, 6, 4, 3, 2, 1], sorted.Select(static e => e.ContentId));
    }

    [Fact]
    public void A_newer_save_does_not_move_a_character()
    {
        var before = CharacterList.Sort([Entry(1, "Ann", daysAgo: 3), Entry(2, "Bea", daysAgo: 1)]);
        var after = CharacterList.Sort([Entry(1, "Ann", daysAgo: 3), Entry(2, "Bea", daysAgo: 0) with { TakenUtc = Now.AddHours(1) }]);

        Assert.Equal(before.Select(static e => e.ContentId), after.Select(static e => e.ContentId));
    }

    [Fact]
    public void Same_name_and_world_falls_back_to_the_content_id()
    {
        var sorted = CharacterList.Sort([Entry(9, "Ann"), Entry(3, "Ann")]);

        Assert.Equal([3UL, 9], sorted.Select(static e => e.ContentId));
    }

    [Fact]
    public void Compare_takes_the_remembered_character_else_the_first_other_one()
    {
        var sorted = CharacterList.Sort([Entry(1, "Ann"), Entry(2, "Bea"), Entry(3, "Cid", here: true)]);

        Assert.Equal(2UL, CharacterList.CompareTarget(sorted, 1, remembered: 2));
        // Viewing an alt: the character live here is the default.
        Assert.Equal(3UL, CharacterList.CompareTarget(sorted, 1, remembered: null));
        // Viewing the live one: the first alt by name.
        Assert.Equal(1UL, CharacterList.CompareTarget(sorted, 3, remembered: null));
        // A remembered character no longer listed (forgotten, hidden) falls back.
        Assert.Equal(3UL, CharacterList.CompareTarget(sorted, 1, remembered: 42));
        // Never the viewed one itself.
        Assert.Equal(3UL, CharacterList.CompareTarget(sorted, 1, remembered: 1));
        Assert.Null(CharacterList.CompareTarget([Entry(1, "Ann")], 1, remembered: null));
    }

    [Fact]
    public void Hidden_characters_leave_the_list_unless_shown_but_the_live_one_stays()
    {
        var sorted = CharacterList.Sort([Entry(1, "Ann", hidden: true), Entry(2, "Bea"), Entry(3, "Cid", here: true, hidden: true)]);

        Assert.Equal([3UL, 2], CharacterList.Visible(sorted, includeHidden: false).Select(static e => e.ContentId));
        Assert.Equal([3UL, 1, 2], CharacterList.Visible(sorted, includeHidden: true).Select(static e => e.ContentId));
    }

    [Fact]
    public void Groups_by_data_center_with_live_characters_on_top()
    {
        var sorted = CharacterList.Sort(
        [
            Entry(1, "Ann", dc: "Primal"),
            Entry(2, "Bea", dc: "Aether"),
            Entry(3, "Cid", dc: "Primal", here: true),
            Entry(4, "Dee", dc: string.Empty),
            Entry(5, "Eve", dc: "aether"),
        ]);

        var groups = CharacterList.Group(sorted, byDataCenter: true);

        Assert.Equal([string.Empty, "Aether", "Primal", string.Empty], groups.Select(static g => g.DataCenter));
        Assert.Equal([3UL], groups[0].Entries.Select(static e => e.ContentId));
        Assert.Equal([2UL, 5], groups[1].Entries.Select(static e => e.ContentId));
        Assert.Equal([1UL], groups[2].Entries.Select(static e => e.ContentId));
        Assert.Equal([4UL], groups[3].Entries.Select(static e => e.ContentId));

        // Only the unknown data center's run is flagged, so it gets a heading of its own instead of sitting under Primal's.
        Assert.Equal([false, false, false, true], groups.Select(static g => g.UnknownDataCenter));
    }

    [Fact]
    public void One_data_center_or_grouping_off_is_a_single_run()
    {
        var sorted = CharacterList.Sort([Entry(1, "Ann"), Entry(2, "Bea", here: true)]);

        Assert.Single(CharacterList.Group(sorted, byDataCenter: true));
        Assert.False(Assert.Single(CharacterList.Group(CharacterList.Sort([Entry(1, "Ann", dc: "Primal"), Entry(2, "Bea", dc: string.Empty)]), byDataCenter: false)).UnknownDataCenter);
        Assert.Empty(CharacterList.Group([], byDataCenter: true));
    }

    [Fact]
    public void Search_matches_every_word_in_name_world_or_data_center()
    {
        var entry = Entry(1, "Yshtola Rhul", world: "Sargatanas", dc: "Aether");

        Assert.True(CharacterList.Matches(entry, null));
        Assert.True(CharacterList.Matches(entry, "  "));
        Assert.True(CharacterList.Matches(entry, "yshtola sarga"));
        Assert.True(CharacterList.Matches(entry, "AETHER"));
        Assert.False(CharacterList.Matches(entry, "yshtola primal"));
    }

    [Fact]
    public void Forget_not_seen_picks_old_captures_and_never_a_live_one()
    {
        var entries = new[]
        {
            Entry(1, "Ann", daysAgo: 200),
            Entry(2, "Bea", daysAgo: 30),
            Entry(3, "Cid", daysAgo: 90),
            Entry(4, "Dee", daysAgo: 400, elsewhere: true),
            Entry(5, "Eve", daysAgo: 365, here: true),
            Entry(6, "Fay", daysAgo: 120, hidden: true),
        };

        var picked = CharacterList.NotSeenFor(entries, 90, Now);

        Assert.Equal([1UL, 6, 3], picked.Select(static e => e.ContentId));
        Assert.Empty(CharacterList.NotSeenFor(entries, 0, Now));
        Assert.Empty(CharacterList.NotSeenFor(entries, 1000, Now));
    }
}
