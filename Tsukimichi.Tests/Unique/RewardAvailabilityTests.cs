using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Unique;

/// <summary>"Can I still get it?" (feature plan v5, decision 4): the availability label of a Moonlit reward.</summary>
public class RewardAvailabilityTests
{
    private const ushort Running = 10;
    private const ushort PastDated = 11;
    private const ushort Collab = 12;
    private const ushort NotYet = 13;
    private const ushort OldEdition = 14;
    private const ushort ThisYear = 15;
    private const ushort Unknown = 16;

    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Dictionary<ushort, FestivalInfo> Festivals = new()
    {
        [Running] = new FestivalInfo("Moonfire Faire (2026)", new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 5, 14, 59, 0, DateTimeKind.Utc), false, "https://na.finalfantasyxiv.com/lodestone/special/2026/Moonfire_Faire/"),
        [PastDated] = new FestivalInfo("All Saints' Wake (2013)", new DateTime(2013, 10, 18, 0, 0, 0, DateTimeKind.Utc), new DateTime(2013, 11, 1, 23, 59, 59, DateTimeKind.Utc), false, "https://example.org"),
        [Collab] = new FestivalInfo("Yo-kai Watch: Gather One, Gather All!", null, null, false, "https://example.org"),
        [NotYet] = new FestivalInfo("Starlight Celebration (2026)", new DateTime(2026, 12, 15, 0, 0, 0, DateTimeKind.Utc), null, false),
        [ThisYear] = new FestivalInfo("All Saints' Wake (2026)", null, null, false),
    };

    private static readonly Dictionary<ushort, int> Years = new()
    {
        [OldEdition] = 2019,
        [ThisYear] = 2026,
    };

    private static readonly AvailabilityContext Context = new(Festivals, Years, id => id == Running, Now);

    private static UniqueRewardEntry Entry(uint quest = Fixture.Target) => new(quest, RewardKind.Minion, 30, 0, "Wind-up Moogle", Confidence.Static, "test");

    private static UniqueRewardEntry Sold() => Entry().WithOtherSource(OtherSource.OnlineStore);

    private static QuestRecord Seasonal(ushort festival) => Fixture.Quest(Fixture.Target) with { Festival = festival };

    private static RewardAvailability Kind(UniqueRewardEntry entry, QuestRecord? quest, QuestEvaluation? evaluation = null) =>
        RewardAvailabilities.Classify(entry, quest, evaluation, Context).Kind;

    [Fact]
    public void A_quest_that_stays_in_the_game_reads_get_now()
    {
        Assert.Equal(RewardAvailability.GetNow, Kind(Entry(), Fixture.Quest(Fixture.Target)));
        Assert.Equal(RewardAvailability.GetNow, Kind(Entry(), null));

        // Blocked by an ordinary gate is still "get now": nothing time-limited about it.
        var blocked = new QuestEvaluation(QuestState.Blocked, [], null, null, null);
        Assert.Equal(RewardAvailability.GetNow, Kind(Entry(), Fixture.Quest(Fixture.Target), blocked));
    }

    [Fact]
    public void A_running_event_reads_event_running_with_its_announced_end()
    {
        var info = RewardAvailabilities.Classify(Entry(), Seasonal(Running), null, Context);
        Assert.Equal(RewardAvailability.EventRunning, info.Kind);
        Assert.Equal(Festivals[Running].End, info.EndsUtc);
        Assert.False(info.IsGone);
    }

    [Fact]
    public void A_collaboration_may_return_and_is_never_gone()
    {
        Assert.Equal(RewardAvailability.CollabMayReturn, Kind(Entry(), Seasonal(Collab)));

        // Even when the character's own evaluation locks the quest out.
        var lockedOut = new QuestEvaluation(QuestState.Foreclosed, [], null, null, null);
        Assert.Equal(RewardAvailability.CollabMayReturn, Kind(Entry(), Seasonal(Collab), lockedOut));
    }

    [Fact]
    public void A_past_event_is_gone_for_good_unless_the_online_store_sells_the_reward()
    {
        Assert.Equal(RewardAvailability.GoneForGood, Kind(Entry(), Seasonal(PastDated)));
        Assert.Equal(RewardAvailability.PastEventOnStore, Kind(Sold(), Seasonal(PastDated)));
        Assert.Equal(RewardAvailability.GoneForGood, Kind(Entry(), Seasonal(OldEdition)));
        Assert.Equal(RewardAvailability.GoneForGood, Kind(Entry(), Seasonal(Unknown)));
        Assert.True(RewardAvailabilities.Classify(Entry(), Seasonal(PastDated), null, Context).IsGone);
    }

    [Fact]
    public void An_edition_that_has_not_run_yet_is_upcoming_not_gone()
    {
        // A curated start still ahead, and an undated edition of this year nobody has done yet.
        Assert.Equal(RewardAvailability.UpcomingEvent, Kind(Entry(), Seasonal(NotYet)));
        Assert.Equal(RewardAvailability.UpcomingEvent, Kind(Entry(), Seasonal(ThisYear)));

        // The resolver's own verdict (a completed run of it) makes this year's edition past.
        var lockedOut = new QuestEvaluation(QuestState.Foreclosed, [], null, null, null);
        Assert.Equal(RewardAvailability.GoneForGood, Kind(Entry(), Seasonal(ThisYear), lockedOut));
    }

    [Fact]
    public void A_quest_the_game_removed_is_gone_for_good_or_on_the_store()
    {
        var retired = Fixture.Quest(Fixture.Target) with { IsRetired = true };
        Assert.Equal(RewardAvailability.GoneForGood, Kind(Entry(), retired));
        Assert.Equal(RewardAvailability.PastEventOnStore, Kind(Sold(), retired));
    }

    [Fact]
    public void Export_names_are_camel_case_and_ranks_follow_the_enum()
    {
        Assert.Equal("getNow", RewardAvailabilities.ExportName(RewardAvailability.GetNow));
        Assert.Equal("collabMayReturn", RewardAvailabilities.ExportName(RewardAvailability.CollabMayReturn));
        Assert.Equal("goneForGood", RewardAvailabilities.ExportName(RewardAvailability.GoneForGood));
        Assert.True(RewardAvailabilities.Rank(RewardAvailability.GetNow) < RewardAvailabilities.Rank(RewardAvailability.GoneForGood));
        Assert.All(Enum.GetValues<RewardAvailability>(), a => Assert.DoesNotContain(" ", RewardAvailabilities.ExportName(a), StringComparison.Ordinal));
    }
}
