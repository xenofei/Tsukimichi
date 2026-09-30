using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Seasonal;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Data;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Seasonal;

/// <summary>
/// <see cref="SeasonalNow"/> over the frozen catalog and the shipped curated festivals, with synthetic running events:
/// Moonfire Faire 2026 (Festival 174, two quests, the second after the first, a curated end of Aug 28) and A Nocturne
/// for Heroes (84, a collaboration rerun with no curated dates).
/// </summary>
public sealed class SeasonalNowTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private const ushort Moonfire2026 = 174;
    private const ushort Nocturne = 84;
    private const uint WaterWings = 70993;
    private const uint FloatsYourRaft = 70994;

    private static readonly DateTime DuringMoonfire = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime AfterMoonfire = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    private IReadOnlyDictionary<ushort, FestivalInfo> Curated => fixture.Curated.Festivals;

    private static CharacterSnapshot Character(params ushort[] running) =>
        Fixture.Snapshot() with { JobLevels = Fixture.Levels((Fixture.Gladiator, 100)), ActiveFestivals = running };

    private IReadOnlyDictionary<uint, QuestEvaluation> Resolve(CharacterSnapshot snapshot, DateTime now) =>
        StateResolver.ResolveAll(Catalog, snapshot, EvalContextBuilder.Build(Curated, fixture.Bundle.Jobs, () => now));

    [Fact]
    public void A_running_festival_lists_its_quests_ready_first_with_the_ready_count_and_the_announced_end()
    {
        var snapshot = Character(Moonfire2026);
        var running = SeasonalNow.Running(Catalog, snapshot, Resolve(snapshot, DuringMoonfire), Curated, DuringMoonfire);

        var moonfire = Assert.Single(running);
        Assert.Equal(Moonfire2026, moonfire.FestivalId);
        Assert.Equal("Moonfire Faire", moonfire.Name);
        Assert.Equal([WaterWings, FloatsYourRaft], moonfire.Quests.Select(q => q.Quest.RowId));
        Assert.Equal(QuestState.Ready, moonfire.Quests[0].State);
        Assert.Equal(QuestState.Blocked, moonfire.Quests[1].State);
        Assert.Equal(1, moonfire.ReadyCount);
        Assert.Equal(new DateTime(2026, 8, 28, 23, 59, 59, DateTimeKind.Utc), moonfire.AnnouncedEndUtc);
        Assert.StartsWith("https://", moonfire.EndEvidence, StringComparison.Ordinal);

        Assert.Equal("Ends Aug 28 (Lodestone)", SeasonalNow.EndsLine(moonfire, named: false, DuringMoonfire));
        Assert.Equal("Moonfire Faire: ends Aug 28 (Lodestone)", SeasonalNow.EndsLine(moonfire, named: true, DuringMoonfire));
        Assert.Equal("announced to end Aug 28 (Lodestone)", SeasonalNow.Status(moonfire, DuringMoonfire));
        Assert.Equal("Moonfire Faire is running: 1 quest ready (ends Aug 28)", SeasonalNow.NoticeText(moonfire, DuringMoonfire));
    }

    [Fact]
    public void An_end_that_has_passed_is_never_shown_even_while_the_game_still_runs_the_event()
    {
        // The flag says running; the curated end is behind us (a late close, or a rerun of the id): "running now" only.
        var snapshot = Character(Moonfire2026);
        var running = SeasonalNow.Running(Catalog, snapshot, Resolve(snapshot, AfterMoonfire), Curated, AfterMoonfire);

        var moonfire = Assert.Single(running);
        Assert.Null(moonfire.AnnouncedEndUtc);
        Assert.Null(moonfire.EndEvidence);
        Assert.Null(SeasonalNow.EndsLine(moonfire, named: false, AfterMoonfire));
        Assert.Equal(SeasonalNow.RunningNow, SeasonalNow.Status(moonfire, AfterMoonfire));
        Assert.Equal("Moonfire Faire is running: 1 quest ready", SeasonalNow.NoticeText(moonfire, AfterMoonfire));
        // The live flag wins over the past end: the quest is not Locked out while the event runs.
        Assert.Equal(QuestState.Ready, moonfire.Quests[0].State);
    }

    [Fact]
    public void A_rerun_collaboration_is_named_from_curated_data_and_has_no_date()
    {
        var snapshot = Character(Nocturne, Moonfire2026);
        var running = SeasonalNow.Running(Catalog, snapshot, Resolve(snapshot, AfterMoonfire), Curated, AfterMoonfire);

        Assert.Equal([Nocturne, Moonfire2026], running.Select(f => f.FestivalId));
        var nocturne = running[0];
        Assert.Equal("A Nocturne for Heroes", nocturne.Name);
        Assert.Null(nocturne.AnnouncedEndUtc);
        Assert.Equal(3, nocturne.Quests.Count);
        Assert.DoesNotContain(nocturne.Quests, q => q.State == QuestState.Foreclosed);
    }

    [Fact]
    public void Nothing_runs_without_an_active_festival_and_unknown_ids_are_ignored()
    {
        var states = new Dictionary<uint, QuestEvaluation>();
        Assert.Empty(SeasonalNow.Running(Catalog, Character(), states, Curated, AfterMoonfire));
        Assert.Empty(SeasonalNow.Running(Catalog, Character(9999, 0), states, Curated, AfterMoonfire));

        // Without evaluations (the first pass has not landed) the quests read Unknown and nothing counts as ready.
        var unevaluated = Assert.Single(SeasonalNow.Running(Catalog, Character(Moonfire2026, Moonfire2026), states, Curated, AfterMoonfire));
        Assert.All(unevaluated.Quests, q => Assert.Equal(QuestState.Unknown, q.State));
        Assert.Equal(0, unevaluated.ReadyCount);
    }

    [Fact]
    public void Removed_quests_are_left_out_and_an_uncurated_festival_is_named_after_its_genre()
    {
        var live = Fixture.Quest(Fixture.A, "Live event quest") with
        {
            Festival = 500,
            Journal = new JournalRef(3, "Other Quests", 30, "Seasonal Events", 77, "Moonfire Faire Events", 1),
        };
        var removed = Fixture.Quest(Fixture.B, "Removed event quest") with { Festival = 500, IsRetired = true, Journal = live.Journal };
        var catalog = Fixture.Catalog(live, removed);
        var snapshot = Fixture.Snapshot() with { ActiveFestivals = [500] };
        var states = StateResolver.ResolveAll(catalog, snapshot, EvalContext.Default);

        var festival = Assert.Single(SeasonalNow.Running(catalog, snapshot, states, new Dictionary<ushort, FestivalInfo>(), AfterMoonfire));
        Assert.Equal("Moonfire Faire", festival.Name);
        Assert.Equal([Fixture.A], festival.Quests.Select(q => q.Quest.RowId));
        Assert.Null(festival.AnnouncedEndUtc);
    }

    [Fact]
    public void An_end_without_https_evidence_is_not_shown()
    {
        var info = new FestivalInfo("Moonfire Faire (2026)", null, new DateTime(2026, 8, 28, 23, 59, 59, DateTimeKind.Utc), false);
        Assert.Null(SeasonalNow.AnnouncedEnd(info, DuringMoonfire));
        Assert.Null(SeasonalNow.AnnouncedEnd(info with { Evidence = "http://example.com/" }, DuringMoonfire));
        Assert.NotNull(SeasonalNow.AnnouncedEnd(info with { Evidence = "https://na.finalfantasyxiv.com/lodestone/" }, DuringMoonfire));
        Assert.Null(SeasonalNow.AnnouncedEnd(null, DuringMoonfire));
    }

    [Fact]
    public void Dates_name_the_year_only_when_it_is_not_this_one()
    {
        Assert.Equal("Aug 28", SeasonalNow.DateText(new DateTime(2026, 8, 28, 23, 59, 59, DateTimeKind.Utc), DuringMoonfire));
        Assert.Equal("Jan 14, 2027", SeasonalNow.DateText(new DateTime(2027, 1, 14, 23, 59, 59, DateTimeKind.Utc), DuringMoonfire));
    }

    [Fact]
    public void Edition_years_come_from_curated_names_or_are_counted_along_the_genre()
    {
        var years = SeasonalNow.EditionYears(Catalog, Curated);

        // Curated names.
        Assert.Equal(2014, years[11]);
        Assert.Equal(2021, years[118]);
        Assert.Equal(2026, years[174]);

        // Counted from the nearest curated edition of the same genre, one per year.
        Assert.Equal(2014, years[5]);    // Heavensturn, counted back from 2015
        Assert.Equal(2014, years[10]);   // Hatching-tide, counted back from 2015
        Assert.Equal(2023, years[142]);  // All Saints' Wake after 2022
        Assert.Equal(2026, years[176]);
        Assert.Equal(2024, years[134]);  // Heavensturn after 2023
        Assert.Equal(2025, years[149]);
        Assert.Equal(2026, years[157]);
        Assert.Equal(2025, years[159]);  // Moonfire Faire, between 2023 and 2026
        Assert.Equal(2025, years[165]);  // Starlight Celebration after 2022
        Assert.Equal(2026, years[258]);

        // Two directions disagree (the 2024 edition sits in the combined Little Ladies' & Hatching-tide genre) and
        // collaborations (rerun under one id): not known. The combined 2024 edition itself carries its curated year.
        Assert.False(years.ContainsKey(138));
        Assert.False(years.ContainsKey(155));
        Assert.False(years.ContainsKey(156));
        Assert.Equal(2024, years[145]);
        Assert.False(years.ContainsKey(84));
        Assert.False(years.ContainsKey(257));
    }

    [Fact]
    public void History_groups_completed_seasonal_quests_by_year_newest_first_with_unknown_years_last()
    {
        var moonfire2014 = Catalog.All.First(q => q.Festival == 11).RowId;
        var moonfire2026 = WaterWings;
        var nocturne = Catalog.All.First(q => q.Festival == Nocturne).RowId;
        var snapshot = Fixture.Snapshot(moonfire2014, moonfire2026, nocturne, Fixture.A);

        var history = SeasonalNow.History(Catalog, snapshot, Curated);

        Assert.Equal([2026, 2014, null], history.Select(y => y.Year));
        Assert.Equal("Moonfire Faire", Assert.Single(history[0].Festivals).Name);
        Assert.Equal(moonfire2026, Assert.Single(history[0].Festivals[0].Quests).RowId);
        Assert.Equal(11, Assert.Single(history[1].Festivals).FestivalId);
        Assert.Equal("A Nocturne for Heroes", Assert.Single(history[2].Festivals).Name);
        Assert.Equal(3, history.Sum(y => y.QuestCount));

        Assert.Empty(SeasonalNow.History(Catalog, Fixture.Snapshot(), Curated));
    }

    [Fact]
    public void Year_in_name_reads_the_edition_suffix_only()
    {
        Assert.Equal(2014, SeasonalNow.YearInName("Moonfire Faire (2014)"));
        Assert.Null(SeasonalNow.YearInName("A Nocturne for Heroes"));
        Assert.Null(SeasonalNow.YearInName("Final Fantasy XVI (2024) Collaboration: The Path Infernal"));
        Assert.Null(SeasonalNow.YearInName(null));
    }
}
