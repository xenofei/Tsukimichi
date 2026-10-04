using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Tests.Plan;

/// <summary>
/// The pace line of Your story (feature plan v7 N9): evenings run 05:00 to 05:00 local, the pace is the median over
/// the last 20 evenings, it is hidden below 15 dated story quests, and only dates the plugin saw count.
/// </summary>
public class StoryPaceTests
{
    private static readonly TimeZoneInfo Tokyo = TimeZoneInfo.CreateCustomTimeZone("Test+9", TimeSpan.FromHours(9), "Test+9", "Test+9");

    /// <summary><paramref name="count"/> completions on the evening of <paramref name="day"/> (Tokyo), from 20:00 local.</summary>
    private static IEnumerable<DateTime> Evening(DateOnly day, int count) =>
        Enumerable.Range(0, count).Select(i => new DateTime(day, new TimeOnly(11, 0), DateTimeKind.Utc).AddMinutes(10 * i));

    [Fact]
    public void Below_fifteen_dated_quests_there_is_no_pace()
    {
        var dates = Evening(new DateOnly(2026, 9, 1), 14).ToList();
        var pace = StoryPace.Compute(dates, Tokyo);

        Assert.False(pace.IsShown);
        Assert.Equal(14, pace.Dated);
        Assert.Null(pace.EveningsFor(100));

        var enough = StoryPace.Compute(Evening(new DateOnly(2026, 9, 1), 15).ToList(), Tokyo);
        Assert.True(enough.IsShown);
        Assert.Equal(15, enough.PerEvening);
        Assert.Equal(7, enough.EveningsFor(100));
    }

    [Fact]
    public void An_evening_runs_from_five_to_five_local()
    {
        // 04:59 in Tokyo on the 2nd is still the evening of the 1st; 05:00 starts the 2nd.
        Assert.Equal(new DateOnly(2026, 9, 1), StoryPace.Evening(new DateTime(2026, 9, 1, 19, 59, 0, DateTimeKind.Utc), Tokyo));
        Assert.Equal(new DateOnly(2026, 9, 2), StoryPace.Evening(new DateTime(2026, 9, 1, 20, 0, 0, DateTimeKind.Utc), Tokyo));
        Assert.Equal(new DateOnly(2026, 9, 1), StoryPace.Evening(new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc), TimeZoneInfo.Utc));
        Assert.Equal(new DateOnly(2026, 8, 31), StoryPace.Evening(new DateTime(2026, 9, 1, 4, 0, 0, DateTimeKind.Utc), TimeZoneInfo.Utc));
    }

    [Fact]
    public void The_pace_is_the_median_of_the_last_twenty_evenings()
    {
        var dates = new List<DateTime>();
        var day = new DateOnly(2026, 6, 1);

        // Ten old evenings of 30 quests each, then 20 recent evenings: ten of 2 and ten of 4.
        for (var i = 0; i < 10; i++)
        {
            dates.AddRange(Evening(day.AddDays(i), 30));
        }

        for (var i = 0; i < 20; i++)
        {
            dates.AddRange(Evening(day.AddDays(30 + i), i % 2 == 0 ? 2 : 4));
        }

        var pace = StoryPace.Compute(dates, Tokyo);

        Assert.True(pace.IsShown);
        Assert.Equal(20, pace.Evenings);
        Assert.Equal(3.0, pace.PerEvening);
        Assert.Equal(31, pace.EveningsFor(91));
        Assert.Equal(0, pace.EveningsFor(0));
    }

    [Fact]
    public void Only_dates_the_plugin_saw_count()
    {
        // Three story quests: one seen completing, one found done at a login, one done before recording began.
        var seen = new QuestRecord { RowId = 65537, QuestId = 1 };
        var foundAtLogin = new QuestRecord { RowId = 65538, QuestId = 2 };
        var before = new QuestRecord { RowId = 65539, QuestId = 3 };
        var at = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var snapshot = new CharacterSnapshot
        {
            ContentId = 1,
            CompletedBits = Evaluation.Fixture.Bits(65537, 65538, 65539),
            CompletionDatesSinceUtc = at.AddDays(-10),
            CompletedBeforeBits = Evaluation.Fixture.Bits(65539),
            CompletedUtc = new Dictionary<ushort, DateTime> { [1] = at, [2] = at },
            CompletedAfterUtc = new Dictionary<ushort, DateTime> { [2] = at.AddDays(-3) },
        };

        Assert.Equal([at], StoryPace.DatedStoryQuests([seen, foundAtLogin, before], snapshot));
        Assert.Empty(StoryPace.DatedStoryQuests([seen], null));
    }
}
