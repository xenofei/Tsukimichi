using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Tests.Data;
using Xunit.Abstractions;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Evaluation;

/// <summary>
/// Feature plan v4 D1 over the frozen catalog and the shipped <c>curated/path_choices.json</c>: the choice groups
/// <see cref="PathIndex"/> finds (the three start cities, the "Close to Home" sets of 3/2/3, the eight class tracks,
/// the Grand Company sets) and what they do to a character's counts, position and shield. The numbers are the ones
/// docs/data/v4/other-paths.md measured: Seventh Umbral Era counts 160 / 160 / 161 for a Gridania, Limsa Lominsa and
/// Ul'dah start once the Grand Company is chosen, and 164 / 164 / 165 before it when only the paths not taken leave.
/// </summary>
public sealed class PathIndexFixtureTests(FixtureCatalog fixture, ITestOutputHelper output) : IClassFixture<FixtureCatalog>
{
    private const uint ComingToGridania = 65575;
    private const uint ComingToLimsaLominsa = 65643;
    private const uint ComingToUldah = 66130;
    private const uint CloseToHomeLancer = 65621;
    private const uint CloseToHomeArcher = 65659;
    private const uint CloseToHomeMarauder = 65644;
    private const uint CloseToHomeGladiator = 66104;
    private const uint CompanyYouKeepTwinAdder = 66216;
    private const uint CompanyYouKeepMaelstrom = 66217;
    private const uint CompanyYouKeepImmortalFlames = 66218;
    private const uint WoodsWillBeDone = 66219;
    private const uint TillSeaSwallowsAll = 66220;
    private const uint WayOfTheGladiator = 65789;
    private const uint MyLittleChocoboTwinAdder = 66236;
    private const uint MyLittleChocoboMaelstrom = 66237;
    private const uint ReturnFromTheVoid = 70134;
    private const uint SeventhUmbralEra = 1;

    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    private PathIndex Index => PathIndex.For(Catalog);

    private EvalContext Context => new() { ClassJobs = fixture.Bundle.Jobs };

    [Fact]
    public void The_rule_finds_the_three_start_cities_and_the_pin_names_them()
    {
        Assert.Equal(new[] { ComingToGridania, ComingToLimsaLominsa, ComingToUldah }.Order(), Index.RuleCityRoots.Order());

        var city = Assert.Single(Index.Groups, g => g.Kind == PathKind.StartCity);
        Assert.Equal(["Gridania", "Limsa Lominsa", "Ul'dah"], city.Options.Select(o => o.Label));
        Assert.Equal([ComingToGridania, ComingToLimsaLominsa, ComingToUldah], city.Options.Select(o => o.Anchors.Single()));
    }

    [Fact]
    public void Close_to_home_comes_in_sets_of_three_two_and_three()
    {
        Assert.Contains(Index.SiblingSets, s => s.SequenceEqual([CloseToHomeLancer, CloseToHomeArcher, 65660u]));
        Assert.Contains(Index.SiblingSets, s => s.SequenceEqual([CloseToHomeMarauder, 65645u]));
        Assert.Contains(Index.SiblingSets, s => s.SequenceEqual([CloseToHomeGladiator, 66105u, 66106u]));
        var closeToHome = Index.SiblingSets.Where(s => s.All(id => Catalog.ByRowId[id].Name == "Close to Home")).Select(s => s.Count).ToList();
        Assert.Equal([3, 2, 3], closeToHome);
    }

    [Fact]
    public void Each_of_the_eight_classes_has_a_starter_and_a_switcher_track()
    {
        var classes = fixture.Curated.PathChoices.Classes;
        Assert.Equal(8, classes.Count);
        Assert.Equal(8, Index.ClassTracks.Count);
        Assert.Equal(classes.Select(c => c.Starter).Order(), Index.ClassTracks.Select(t => t.Starter).Order());
        Assert.All(Index.ClassTracks, t => Assert.Empty(Catalog.ByRowId[t.Starter].PreviousQuests.QuestIds));
        Assert.All(Index.ClassTracks, t => Assert.NotEmpty(Catalog.ByRowId[t.Switcher].PreviousQuests.QuestIds));
        Assert.All(Index.ClassTracks, t => Assert.Equal(JoinKind.Any, Catalog.ByRowId[t.Join].PreviousQuests.Join));

        var cls = Assert.Single(Index.Groups, g => g.Kind == PathKind.StartClass);
        Assert.Equal(classes.Select(c => c.Label), cls.Options.Select(o => o.Label));
        Assert.All(cls.Options, o => Assert.Equal(2, o.Anchors.Count));
    }

    [Fact]
    public void The_grand_company_sets_are_the_company_you_keep_and_each_company_version_trio()
    {
        var companies = Index.Groups.Where(g => g.Kind == PathKind.GrandCompany).ToList();
        Assert.Contains(companies, g => g.Options.Select(o => o.Anchors[0]).Order().SequenceEqual([CompanyYouKeepTwinAdder, CompanyYouKeepMaelstrom, CompanyYouKeepImmortalFlames]));
        Assert.Contains(companies, g => g.Options.Select(o => o.Anchors[0]).Order().SequenceEqual([67001u, 67002u, 67003u]));
        Assert.Contains(companies, g => g.Options.Any(o => o.Anchors[0] == MyLittleChocoboTwinAdder));
        Assert.All(companies, g => Assert.Equal([1, 2, 3], g.Options.Select(o => (int)o.GrandCompany).Order()));
        // The Company You Keep, My Little Chocobo, Call of the Wild, A Pup No Longer, Like Civilized Men and Women,
        // Let the Hunt Begin, Squadron and Commander.
        Assert.Equal(7, companies.Count);
    }

    /// <summary>A character that took its start city's first quest and one of its "Close to Home" rows, and maybe a company.</summary>
    private Dictionary<uint, QuestEvaluation> Resolve(params uint[] completed) =>
        StateResolver.ResolveAll(Catalog, Snapshot(completed), Context);

    [Theory]
    [InlineData(ComingToGridania, CloseToHomeLancer, 160, 164)]
    [InlineData(ComingToLimsaLominsa, CloseToHomeMarauder, 160, 164)]
    [InlineData(ComingToUldah, CloseToHomeGladiator, 161, 165)]
    public void Seventh_umbral_era_counts_the_city_s_own_line_before_and_after_the_grand_company(uint root, uint closeToHome, int afterCompany, int beforeCompany)
    {
        var counted = Catalog.ByGenre[SeventhUmbralEra].Where(q => !q.IsRemoved && q.EntersCounts).ToList();

        // Before the company is chosen: the other cities' and classes' quests leave; the three companies' do not.
        var before = Resolve(root, closeToHome);
        Assert.Equal(beforeCompany, counted.Count(q => !before[q.RowId].IsOtherPath));
        // The choice counts once: two of the three Company You Keep and their follow-ups are spare alternatives.
        Assert.Equal(afterCompany, TreeCounts.Compute(Catalog, before, includeUnlisted: false).Genre(SeventhUmbralEra).Total);
        Assert.Equal(3, before[CompanyYouKeepTwinAdder].ChoiceOf);

        // After: whichever company, the other two companies' quests are on other paths.
        foreach (var company in new[] { CompanyYouKeepTwinAdder, CompanyYouKeepMaelstrom, CompanyYouKeepImmortalFlames })
        {
            var after = Resolve(root, closeToHome, company);
            Assert.Equal(afterCompany, counted.Count(q => !after[q.RowId].IsOtherPath));
            Assert.Equal(afterCompany, TreeCounts.Compute(Catalog, after, includeUnlisted: false).Genre(SeventhUmbralEra).Total);
        }

        output.WriteLine($"{Catalog.ByRowId[root].Name}: {afterCompany} after the company, {beforeCompany} before");
    }

    [Fact]
    public void A_gridania_lancer_in_the_twin_adder_reads_the_other_paths_by_name_and_reason()
    {
        var states = Resolve(ComingToGridania, CloseToHomeLancer, 65559, CompanyYouKeepTwinAdder);
        var names = fixture.Bundle.BlockerNames();

        string Status(uint rowId) => BlockerText.StatusText(states[rowId], Catalog.ByRowId[rowId], names, states);
        string Detail(uint rowId) => PathText.Detail(states[rowId].OtherPath!, names.GrandCompany);

        Assert.Equal("Locked out · Another city's start (Ul'dah)", Status(CloseToHomeGladiator));
        Assert.Equal("Only for Ul'dah Gladiator starters · you started in Gridania as a Lancer", Detail(CloseToHomeGladiator));
        Assert.Equal("Locked out · Another city's start (Limsa Lominsa)", Status(ComingToLimsaLominsa));
        Assert.Equal("Locked out · Another starting class (Archer)", Status(CloseToHomeArcher));
        Assert.Equal("Only for Gridania Archer starters · you started in Gridania as a Lancer", Detail(CloseToHomeArcher));
        Assert.Equal("Locked out · Another starting class (Gladiator)", Status(WayOfTheGladiator));
        Assert.Equal("Locked out · Another Grand Company (Maelstrom)", Status(CompanyYouKeepMaelstrom));
        Assert.Equal("Locked out · Another Grand Company (Maelstrom)", Status(TillSeaSwallowsAll));
        Assert.Equal("For the Maelstrom · you chose the Order of the Twin Adder", Detail(TillSeaSwallowsAll));
        Assert.NotEqual(QuestState.Foreclosed, states[WoodsWillBeDone].State);

        // The company versions of a quest are a choice of their own until one is done; the company chosen in the main
        // scenario presumes its own version.
        Assert.False(states[MyLittleChocoboMaelstrom].IsOtherPath);
        Assert.True(states[MyLittleChocoboMaelstrom].IsSpareAlternative);
        var done = Resolve(ComingToGridania, CloseToHomeLancer, 65559, CompanyYouKeepTwinAdder, MyLittleChocoboTwinAdder);
        Assert.Equal("Locked out · Another Grand Company (Maelstrom)", BlockerText.StatusText(done[MyLittleChocoboMaelstrom], Catalog.ByRowId[MyLittleChocoboMaelstrom], names, done));
    }

    [Fact]
    public void Other_paths_leave_the_main_scenario_position_and_are_never_masked()
    {
        // Every Seventh Umbral Era quest of the Gridania line done, and nothing past it: the position moves on to the
        // Seventh Astral Era instead of stopping at another city's "Close to Home".
        var paths = Resolve(ComingToGridania, CloseToHomeLancer, 65559, CompanyYouKeepTwinAdder);
        var done = Catalog.ByGenre[SeventhUmbralEra].Where(q => !q.IsRemoved && !paths[q.RowId].LeavesTotals).Select(q => q.RowId)
            .Concat(new uint[] { ComingToGridania, 65559 }).ToArray();
        var states = Resolve(done);
        var position = MsqProgress.Compute(Catalog, states)!;
        Assert.NotNull(position.Next);
        Assert.NotEqual(SeventhUmbralEra, position.Next!.Journal.GenreId);
        Assert.False(states[position.Next.RowId].IsOtherPath);

        var mask = SpoilerMask.Build(Catalog, states, SpoilerOptions.Default);
        Assert.False(mask.IsMasked(CloseToHomeGladiator));
        Assert.False(mask.IsMasked(TillSeaSwallowsAll));
        Assert.Equal("Close to Home", mask.DisplayName(Catalog.ByRowId[CloseToHomeGladiator]));
        output.WriteLine($"position {position.Next.RowId} {position.Next.Name}, {position.Done}/{position.Total}, masked {mask.MaskedCount}");
    }

    [Fact]
    public void The_tree_tallies_the_other_paths_and_the_other_paths_node_lists_them_grouped()
    {
        var states = Resolve(ComingToGridania, CloseToHomeLancer, 65559, CompanyYouKeepTwinAdder);
        var counts = TreeCounts.Compute(Catalog, states, includeUnlisted: false);

        var era = counts.Genre(SeventhUmbralEra);
        var tally = counts.OtherPathsIn(QuestScope.Genre(SeventhUmbralEra));
        Assert.Equal(tally.Total, era.OtherPaths);
        // The other two cities' lines (23 + 26), the Archer's and Conjurer's "Close to Home", two companies' two quests.
        Assert.Equal(new PathTally(49, 2, 4, 0), tally);
        Assert.StartsWith($"{tally.Total} on other paths: another city's start {tally.StartCity}, another starting class {tally.StartClass}", PathText.Tally(tally));

        var rows = QuestQuery.Apply(Catalog, states, new FilterSet(), QuestScope.VirtualOtherPaths, SortSpec.Default, null, QueryContext.Empty);
        Assert.Equal(counts.OtherPathsNode.Total, rows.Rows.Length);
        var kinds = rows.Rows.Select(r => states[r.Quest.RowId].OtherPath!.Path).ToList();
        Assert.Equal(kinds.Order(), kinds);
        Assert.All(rows.Rows, r => Assert.StartsWith("Locked out · Another ", r.Status));

        var genre = QuestQuery.Apply(Catalog, states, new FilterSet(), QuestScope.Genre(SeventhUmbralEra), SortSpec.Default, null, QueryContext.Empty);
        Assert.DoesNotContain(genre.Rows, r => states[r.Quest.RowId].IsOtherPath);
        var included = QuestQuery.Apply(Catalog, states, new FilterSet { IncludeOtherPaths = true }, QuestScope.Genre(SeventhUmbralEra), SortSpec.Default, null, QueryContext.Empty);
        Assert.Equal(genre.Rows.Length + tally.Total, included.Rows.Length);
    }

    [Fact]
    public void Ipc_reads_an_other_path_as_foreclosed_with_its_path_line()
    {
        var states = Resolve(ComingToGridania, CloseToHomeLancer, 65559, CompanyYouKeepTwinAdder);
        var view = new IpcView(Catalog, states, fixture.Bundle.BlockerNames());

        Assert.Equal("Foreclosed", view.State(CloseToHomeArcher));
        Assert.False(view.IsQuestAvailable(CloseToHomeArcher));
        var lines = view.Blockers(CloseToHomeArcher);
        Assert.Equal("Locked out · Another starting class (Archer)", lines[0]);
        Assert.Equal("OtherPath: unmet (StartCity Gridania, chosen Gridania; StartClass Archer, chosen Lancer; by 65621 Close to Home, 65559 Way of the Lancer)", lines[1]);
        Assert.NotEqual(CloseToHomeArcher, view.MsqNext());
    }

    [Fact]
    public void Undecided_groups_count_one_option_and_tag_every_open_one()
    {
        // A new Gridania character: the city is held in the journal, nothing else is decided.
        var snapshot = Snapshot() with { Accepted = [Accepted(ComingToGridania)], CurrentJob = 5, JobLevels = Levels((5, 1)) };
        var states = StateResolver.ResolveAll(Catalog, snapshot, Context);

        Assert.True(states[ComingToLimsaLominsa].IsOtherPath);
        Assert.False(states[CloseToHomeLancer].IsOtherPath);
        Assert.Equal(3, states[CloseToHomeLancer].ChoiceOf);
        Assert.Equal(3, states[CloseToHomeArcher].ChoiceOf);
        // The Archer the character is presumes the Archer's row; the other two classes' rows are spare.
        Assert.False(states[CloseToHomeArcher].IsSpareAlternative);
        Assert.True(states[CloseToHomeLancer].IsSpareAlternative);
        Assert.EndsWith("Choose one of 3", BlockerText.StatusText(states[CloseToHomeArcher], Catalog.ByRowId[CloseToHomeArcher], fixture.Bundle.BlockerNames(), states));
    }
}
