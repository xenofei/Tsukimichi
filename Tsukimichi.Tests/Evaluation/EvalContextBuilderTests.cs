using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Evaluation;

/// <summary>
/// Bug-hunt C3/G1: 0.5.0 built the base context with only ClassJobs, so curated festival ends never reached rule 3
/// and rule 6 (Unknown) could never fire. The builder is what SessionState.SetCatalog now uses.
/// </summary>
public class EvalContextBuilderTests
{
    private static readonly DateTime Now = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    private const ushort Starlight = 9;
    private const ushort Valentione = 10;

    private static Dictionary<ushort, FestivalInfo> Festivals(params (ushort Id, DateTime? End)[] entries)
    {
        var result = new Dictionary<ushort, FestivalInfo>();
        foreach (var (id, end) in entries)
        {
            result[id] = new FestivalInfo($"Festival {id}", end?.AddDays(-14), end, MogStation: false);
        }

        return result;
    }

    private static QuestEvaluation Resolve(QuestRecord quest, EvalContext ctx, CharacterSnapshot? snapshot = null) =>
        StateResolver.Resolve(quest, snapshot ?? Snapshot(), Catalog(quest), ctx);

    [Fact]
    public void Festival_with_a_curated_end_in_the_past_resolves_Foreclosed()
    {
        var quest = Quest(Target) with { Festival = Starlight };
        var ctx = EvalContextBuilder.Build(Festivals((Starlight, Now.AddDays(-30))), classJobs: null, () => Now);

        var result = Resolve(quest, ctx);

        Assert.Equal(QuestState.Foreclosed, result.State);
        Assert.Equal(RequirementKind.Seasonal, result.NextStep!.Req.Kind);
    }

    [Fact]
    public void Festival_with_a_curated_end_still_ahead_resolves_Blocked_as_seasonal()
    {
        var quest = Quest(Target) with { Festival = Starlight };
        var ctx = EvalContextBuilder.Build(Festivals((Starlight, Now.AddDays(1))), classJobs: null, () => Now);

        var result = Resolve(quest, ctx);

        Assert.Equal(QuestState.Blocked, result.State);
        Assert.Equal(RequirementKind.Seasonal, result.NextStep!.Req.Kind);
        Assert.Equal("seasonal event not active", result.NextStep.Detail);
    }

    [Fact]
    public void Festival_without_a_known_end_and_inactive_resolves_Blocked_as_seasonal()
    {
        var quest = Quest(Target) with { Festival = Valentione };

        // The shipped festivals.json is empty today.
        var empty = EvalContextBuilder.Build(CuratedData.Empty.Festivals, classJobs: null, () => Now);
        var blocked = Resolve(quest, empty);
        Assert.Equal(QuestState.Blocked, blocked.State);
        Assert.Equal(RequirementKind.Seasonal, blocked.NextStep!.Req.Kind);
        Assert.Equal("seasonal event not active", blocked.NextStep.Detail);

        // An entry with a null end is "unknown", never "already over".
        var nullEnd = EvalContextBuilder.Build(Festivals((Valentione, null)), classJobs: null, () => Now);
        Assert.Equal(QuestState.Blocked, Resolve(quest, nullEnd).State);

        // Another festival's end says nothing about this one.
        var other = EvalContextBuilder.Build(Festivals((Starlight, Now.AddDays(-30))), classJobs: null, () => Now);
        Assert.Equal(QuestState.Blocked, Resolve(quest, other).State);
    }

    [Fact]
    public void Active_festival_is_not_foreclosed_by_a_stale_curated_end()
    {
        // Rule 3 only runs for an inactive festival: the live flag wins over an out-of-date end date.
        var quest = Quest(Target) with { Festival = Starlight };
        var ctx = EvalContextBuilder.Build(Festivals((Starlight, Now.AddDays(-30))), classJobs: null, () => Now);

        Assert.Equal(QuestState.Ready, Resolve(quest, ctx, Snapshot() with { ActiveFestivals = [Starlight] }).State);
    }

    [Fact]
    public void Clock_is_read_on_every_check_so_a_long_lived_context_stays_current()
    {
        var quest = Quest(Target) with { Festival = Starlight };
        var now = Now;
        var ctx = EvalContextBuilder.Build(Festivals((Starlight, Now.AddDays(1))), classJobs: null, () => now);

        Assert.Equal(QuestState.Blocked, Resolve(quest, ctx).State);

        now = Now.AddDays(2);
        Assert.Equal(QuestState.Foreclosed, Resolve(quest, ctx).State);
    }

    [Fact]
    public void Achievement_gated_quest_resolves_Unknown_when_achievements_are_not_readable()
    {
        var quest = Quest(Target);
        var ctx = EvalContextBuilder.Build(CuratedData.Empty.Festivals, classJobs: null, () => Now, achievementGatedRowIds: new HashSet<uint> { Target });

        var notLoaded = Resolve(quest, ctx, Snapshot() with { AchievementsLoaded = false });
        Assert.Equal(QuestState.Unknown, notLoaded.State);
        Assert.Equal(RequirementKind.Achievement, notLoaded.NextStep!.Req.Kind);

        Assert.Equal(QuestState.Ready, Resolve(quest, ctx).State);
        Assert.Equal(QuestState.Ready, Resolve(Quest(A), ctx, Snapshot() with { AchievementsLoaded = false }).State);
    }

    [Fact]
    public void Without_an_achievement_source_nothing_is_gated()
    {
        var ctx = EvalContextBuilder.Build(CuratedData.Empty.Festivals, classJobs: null, () => Now);

        Assert.Equal(QuestState.Ready, Resolve(Quest(Target), ctx, Snapshot() with { AchievementsLoaded = false }).State);
        Assert.False(ctx.IsAchievementGated(Target));
    }

    [Fact]
    public void Class_job_lookup_and_unknown_daily_offer_are_carried_into_the_context()
    {
        var jobs = new Jobs((5, [Gladiator, Conjurer]));
        var ctx = EvalContextBuilder.Build(CuratedData.Empty.Festivals, jobs, () => Now);

        Assert.Same(jobs, ctx.ClassJobs);
        Assert.Null(ctx.TodaysDailyOffer);
    }

    [Fact]
    public void FestivalEnds_keeps_only_entries_with_an_end()
    {
        var ends = EvalContextBuilder.FestivalEnds(Festivals((Starlight, Now), (Valentione, null)));

        Assert.Equal(new Dictionary<ushort, DateTime?> { [Starlight] = Now }, ends);
    }
}
