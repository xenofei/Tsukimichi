using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Evaluation;

/// <summary>
/// The context a stored character is resolved with, which the last live capture also takes once its character logs
/// out (SessionState.ClearLive): no daily offer, the stored cycle clock, the server's festivals, and everything else
/// the base context carries.
/// </summary>
public class StoredContextTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 16, 0, 0, DateTimeKind.Utc);

    private static readonly ServerFestivals Running = new([7], [1]);

    [Fact]
    public void A_stored_character_drops_the_live_offer_and_takes_the_stored_clock()
    {
        Func<uint, bool> gated = static id => id == Target;
        var live = new EvalContext
        {
            IsAchievementGated = gated,
            TodaysDailyOffer = new HashSet<ushort> { 1 },
            DailyOfferTribes = new HashSet<byte> { 2 },
            ServerFestivals = ServerFestivals.None,
        };
        Func<DateTime> clock = static () => Now;

        var stored = live.ForStoredCharacter(Running, clock);

        Assert.Null(stored.TodaysDailyOffer);
        Assert.Null(stored.DailyOfferTribes);
        Assert.Same(clock, stored.CycleClock);
        Assert.Same(Running, stored.ServerFestivals);
        Assert.Same(gated, stored.IsAchievementGated);

        // The live context itself is left as it was.
        Assert.NotNull(live.TodaysDailyOffer);
        Assert.Null(live.CycleClock);
    }

    [Fact]
    public void Yesterday_s_offer_no_longer_holds_a_daily_back_once_the_character_is_stored()
    {
        var quest = Quest(Target) with { BeastTribe = 2, BeastRank = 1, IsRepeatable = true, RepeatInterval = 1 };
        var snapshot = Snapshot() with
        {
            Tribes = new Dictionary<byte, TribeStanding> { [2] = new(3, 0) },
            TribeAllowance = 6,
            TakenUtc = Now,
        };
        var live = new EvalContext { TodaysDailyOffer = new HashSet<ushort> { 1 }, DailyOfferTribes = new HashSet<byte> { 2 } };

        var held = RequirementEvaluator.Evaluate(quest, snapshot, Catalog(quest), live);
        Assert.False(Only(held, RequirementKind.TribeDailyOffer).Met);

        var stored = RequirementEvaluator.Evaluate(quest, snapshot, Catalog(quest), live.ForStoredCharacter(ServerFestivals.None, static () => Now));
        Assert.DoesNotContain(stored, r => r.Req.Kind == RequirementKind.TribeDailyOffer);
    }
}
