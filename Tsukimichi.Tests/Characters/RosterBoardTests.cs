using Tsukimichi.Core.Characters;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Unique;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Characters;

/// <summary>
/// The All characters roster's rules (plan v7, 1.21.0 P3): where a row's character is, which rows are read only (every
/// one but the character logged in here), the caption's counts, the default order and the narrow-window columns.
/// </summary>
public sealed class RosterBoardTests
{
    private static RosterRow Row(ulong id, string name, RosterPlace place, bool live = false, int left = 10, bool starred = false, DateTime? taken = null) =>
        new(id, name, "World", place, live, taken ?? new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), 1, 50,
            new RosterStory("Dawntrail", left == 0 ? null : Quest((uint)id, "Next"), left, 0), 3, 12, 40, 100)
        {
            Starred = starred,
        };

    [Fact]
    public void Only_the_character_logged_in_here_is_not_read_only()
    {
        Assert.Equal(RosterPlace.Here, RosterBoard.PlaceOf(liveHere: true, liveElsewhere: false, otherFolder: false));
        Assert.Equal(RosterPlace.OtherClient, RosterBoard.PlaceOf(false, true, false));
        Assert.Equal(RosterPlace.Stored, RosterBoard.PlaceOf(false, false, false));
        Assert.Equal(RosterPlace.OtherFolder, RosterBoard.PlaceOf(false, true, true));

        var here = Row(1, "Here", RosterPlace.Here, live: true);
        Assert.False(here.ReadOnly);
        Assert.True(here.CanHandOff);
        foreach (var place in new[] { RosterPlace.OtherClient, RosterPlace.Stored, RosterPlace.OtherFolder })
        {
            var row = Row(2, "Other", place, live: place != RosterPlace.Stored);
            Assert.True(row.ReadOnly);
            Assert.False(row.CanHandOff);
        }
    }

    [Fact]
    public void The_caption_counts_rows_live_in_other_clients()
    {
        var rows = new[]
        {
            Row(1, "A", RosterPlace.Here, live: true),
            Row(2, "B", RosterPlace.OtherClient, live: true),
            Row(3, "C", RosterPlace.OtherFolder, live: true),
            Row(4, "D", RosterPlace.OtherFolder),
            Row(5, "E", RosterPlace.Stored),
        };

        Assert.Equal((5, 2), RosterBoard.Counts(rows));
    }

    [Fact]
    public void The_default_order_is_starred_then_furthest_along_then_name()
    {
        var rows = new[]
        {
            Row(1, "Ren", RosterPlace.Stored, left: 463),
            Row(2, "Michiru", RosterPlace.Here, left: 0),
            Row(3, "Aki", RosterPlace.Stored, left: 91),
            Row(4, "Nao", RosterPlace.Stored, left: 940, starred: true),
            Row(5, "Emi", RosterPlace.Stored, left: 0),
        };

        Assert.Equal(["Nao", "Emi", "Michiru", "Aki", "Ren"], RosterBoard.Sort(rows).Select(static r => r.Name));
        Assert.Equal(["Nao", "Ren", "Aki", "Emi", "Michiru"], RosterBoard.Sort(rows, RosterColumn.Story, descending: true).Select(static r => r.Name));
        Assert.Equal(["Aki", "Emi", "Michiru", "Nao", "Ren"], RosterBoard.Sort(rows, RosterColumn.Character).Select(static r => r.Name));
    }

    [Fact]
    public void Live_rows_are_seen_now()
    {
        var old = Row(1, "Old", RosterPlace.Stored, taken: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var recent = Row(2, "Recent", RosterPlace.Stored);
        var live = Row(3, "Live", RosterPlace.OtherFolder, live: true, taken: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(["Live", "Recent", "Old"], RosterBoard.Sort([old, recent, live], RosterColumn.LastSeen).Select(static r => r.Name));
    }

    [Fact]
    public void Narrow_windows_drop_moonlit_and_today_first()
    {
        foreach (var column in Enum.GetValues<RosterColumn>())
        {
            Assert.True(RosterBoard.Shows(column, 1200f));
        }

        Assert.False(RosterBoard.Shows(RosterColumn.Moonlit, 900f));
        Assert.False(RosterBoard.Shows(RosterColumn.Today, 900f));
        Assert.True(RosterBoard.Shows(RosterColumn.Goal, 900f));
        Assert.True(RosterBoard.Shows(RosterColumn.Moonlit, 900f, forcedOn: true));
    }

    [Fact]
    public void Ready_and_story_come_from_the_characters_own_states()
    {
        var catalog = QuestCatalog.Build([Quest(1, "A", section: 0), Quest(2, "B", section: 0), Quest(3, "Side", section: 2), Quest(4, "Gone", section: 2) with { IsRetired = true }]);
        var states = new Dictionary<uint, QuestEvaluation>
        {
            [1] = new(QuestState.Completed, [], null, null, null),
            [2] = new(QuestState.Ready, [], null, null, null),
            [3] = new(QuestState.Ready, [], null, null, null),
            [4] = new(QuestState.Ready, [], null, null, null),
        };

        Assert.Equal(2, RosterBoard.ReadyCount(catalog, states));
        var story = RosterBoard.StoryOf(catalog, states);
        Assert.NotNull(story);
        Assert.Equal(2u, story.Next!.RowId);
        Assert.Equal(1, story.LeftToLatest);

        states[2] = new(QuestState.Completed, [], null, null, null);
        Assert.True(RosterBoard.StoryOf(catalog, states)!.CaughtUp);
    }

    [Fact]
    public void Moonlit_left_counts_the_rewards_checked_and_missing()
    {
        var entries = new[]
        {
            new UniqueRewardEntry(1, RewardKind.Mount, 10, 0, "Mount", Confidence.Static, "test"),
            new UniqueRewardEntry(2, RewardKind.Mount, 11, 0, "Other mount", Confidence.Static, "test"),
            new UniqueRewardEntry(3, RewardKind.Mount, 11, 0, "Other mount again", Confidence.Static, "test"),
            new UniqueRewardEntry(4, RewardKind.Minion, 12, 0, "Unchecked", Confidence.Static, "test"),
        };
        var snapshot = new CharacterSnapshot
        {
            ContentId = 1,
            Collectibles = new Dictionary<string, CollectibleSet> { ["Mount"] = new CollectibleSet { Owned = [10], Missing = [11] } },
        };

        Assert.Equal(1, RosterBoard.MoonlitLeft(entries, CollectibleLookup.For(snapshot)));
        Assert.Null(RosterBoard.MoonlitLeft(entries, null));
    }
}
