using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Seasonal;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Data;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Seasonal;

/// <summary>
/// <see cref="ServerFestivals"/>: festivals run server-wide, so a stored character is evaluated and listed with the
/// live character's flags while someone is logged in, and without flags a passed curated end shows to be stale when no
/// one is. Over the frozen catalog and the shipped curated festivals (Moonfire Faire 2026 is Festival 174, curated end
/// Aug 28, 2026).
/// </summary>
public sealed class ServerFestivalsTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private const ushort Moonfire2026 = 174;
    private const uint WaterWings = 70993;
    private const uint FloatsYourRaft = 70994;

    private static readonly DateTime DuringMoonfire = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime AfterMoonfire = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    private IReadOnlyDictionary<ushort, FestivalInfo> Curated => fixture.Curated.Festivals;

    private static CharacterSnapshot Character(DateTime taken, params ushort[] running) =>
        Fixture.Snapshot() with { JobLevels = Fixture.Levels((Fixture.Gladiator, 100)), ActiveFestivals = running, TakenUtc = taken };

    /// <summary>The viewed character resolved and listed as the plugin does for a stored one.</summary>
    private (IReadOnlyDictionary<uint, QuestEvaluation> States, IReadOnlyList<RunningFestival> Running) View(CharacterSnapshot viewed, CharacterSnapshot? live, DateTime now)
    {
        var server = ServerFestivals.For(viewed, live, Curated, now);
        var context = EvalContextBuilder.Build(Curated, fixture.Bundle.Jobs, () => now) with { ServerFestivals = server };
        var states = StateResolver.ResolveAll(Catalog, viewed, context);
        return (states, SeasonalNow.Running(Catalog, server, states, Curated, now));
    }

    [Fact]
    public void An_alt_saved_during_Moonfire_viewed_after_its_end_with_no_one_logged_in_shows_it_over_and_Locked_out()
    {
        var alt = Character(DuringMoonfire, Moonfire2026);

        // Its own flags still say running: that is the bug this guards against.
        Assert.NotEmpty(SeasonalNow.Running(Catalog, alt, StateResolver.ResolveAll(Catalog, alt, EvalContextBuilder.Build(Curated, fixture.Bundle.Jobs, () => AfterMoonfire)), Curated, AfterMoonfire));

        Assert.Empty(ServerFestivals.For(alt, live: null, Curated, AfterMoonfire).Ids);
        var (states, running) = View(alt, live: null, AfterMoonfire);
        Assert.Empty(running);
        Assert.Equal(QuestState.Foreclosed, states[WaterWings].State);
        Assert.Equal(QuestState.Foreclosed, states[FloatsYourRaft].State);
    }

    [Fact]
    public void An_alt_saved_before_Moonfire_viewed_while_a_main_is_logged_in_during_it_shows_it_running()
    {
        var alt = Character(DuringMoonfire.AddDays(-30));
        var main = Character(DuringMoonfire, Moonfire2026) with { ContentId = 2 };

        var (states, running) = View(alt, main, DuringMoonfire);
        var moonfire = Assert.Single(running);
        Assert.Equal(Moonfire2026, moonfire.FestivalId);
        Assert.Equal(QuestState.Ready, states[WaterWings].State);
        Assert.False(states[FloatsYourRaft].IsOutOfSeason);

        // The live character's flags win the other way too: the main logged in after Moonfire ended, the alt saved during it.
        var during = Character(DuringMoonfire, Moonfire2026);
        var (after, none) = View(during, Character(AfterMoonfire) with { ContentId = 2 }, AfterMoonfire);
        Assert.Empty(none);
        Assert.Equal(QuestState.Foreclosed, after[WaterWings].State);
    }

    [Fact]
    public void Without_a_live_character_a_flag_is_kept_unless_a_passed_curated_end_shows_it_stale()
    {
        // 84 is an undated rerun, 999 has no entry: both kept. 174 taken after its end (the game ran it late): kept.
        var late = Character(new DateTime(2026, 8, 29, 6, 0, 0, DateTimeKind.Utc), 84, Moonfire2026, 999) with { ActiveFestivalPhases = [1, 2, 3] };
        var kept = ServerFestivals.For(late, live: null, Curated, AfterMoonfire);
        Assert.Equal([84, Moonfire2026, 999], kept.Ids);
        Assert.Equal((ushort)2, kept.Phase(Moonfire2026));

        // Taken before the end: 174 is dropped and the phases stay parallel to the ids that remain.
        var stale = late with { TakenUtc = DuringMoonfire };
        var filtered = ServerFestivals.For(stale, live: null, Curated, AfterMoonfire);
        Assert.Equal([84, 999], filtered.Ids);
        Assert.Equal([1, 3], filtered.Phases);
        Assert.Equal((ushort)3, filtered.Phase(999));
        Assert.False(filtered.Contains(Moonfire2026));

        // Before the end has passed nothing is dropped; no snapshot at all is nothing running.
        Assert.Equal([84, Moonfire2026, 999], ServerFestivals.For(stale, live: null, Curated, DuringMoonfire).Ids);
        Assert.Same(ServerFestivals.None, ServerFestivals.For(null, null, Curated, AfterMoonfire));
        Assert.True(filtered.SameAs(new ServerFestivals([84, 999], [1, 3])));
        Assert.False(filtered.SameAs(kept));
    }

    [Fact]
    public void The_seasonal_requirement_reads_the_context_server_festivals_over_the_snapshot_flags()
    {
        var quest = Catalog.ByRowId[WaterWings];
        var alt = Character(DuringMoonfire, Moonfire2026);
        var context = EvalContext.Default with { ServerFestivals = ServerFestivals.None };

        var seasonal = RequirementEvaluator.Evaluate(quest, alt, Catalog, context).Single(r => r.Req.Kind == RequirementKind.Seasonal);
        Assert.False(seasonal.Met);

        var running = Character(DuringMoonfire);
        var on = RequirementEvaluator.Evaluate(quest, running, Catalog, EvalContext.Default with { ServerFestivals = new([Moonfire2026], []) })
            .Single(r => r.Req.Kind == RequirementKind.Seasonal);
        Assert.True(on.Met);
    }
}
