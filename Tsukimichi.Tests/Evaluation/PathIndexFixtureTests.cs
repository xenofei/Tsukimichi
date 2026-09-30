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
    private const uint WeMustRebuild = 66131;
    private const uint NothingToSeeHere = 66207;
    private const uint UnderneathTheSultantree = 66086;
    private const uint CallOfTheWildTwinAdder = 67001;
    private const uint CallOfTheWildMaelstrom = 67002;
    private const uint CallOfTheWildImmortalFlames = 67003;
    private const byte Lancer = 4;

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

    private string LabelOf(PathKind kind, PathChoice choice, bool chosen)
    {
        var group = Index.Groups.Single(g => g.Kind == kind);
        var option = chosen ? choice.Chosen[group.Id] : choice.Presumed[group.Id];
        return option < 0 ? "-" : group.Options[option].Label;
    }

    [Fact]
    public void Quests_done_on_a_city_s_line_decide_the_city_without_its_first_quest()
    {
        // An old Ul'dah Gladiator whose "Coming to Ul'dah" bit is not set: its "Close to Home", two Ul'dah main
        // scenario quests and "Way of the Gladiator" are done. They agree on Ul'dah, so the city is decided.
        var snapshot = Snapshot(CloseToHomeGladiator, WeMustRebuild, NothingToSeeHere, WayOfTheGladiator) with { JobLevels = Levels((Gladiator, 5)) };
        var states = StateResolver.ResolveAll(Catalog, snapshot, Context);
        var choice = Index.Resolve(snapshot);

        Assert.Equal("Ul'dah", LabelOf(PathKind.StartCity, choice, chosen: true));
        Assert.Equal("Gladiator", LabelOf(PathKind.StartClass, choice, chosen: true));
        Assert.Equal(QuestState.Ready, states[UnderneathTheSultantree].State);
        Assert.False(states[UnderneathTheSultantree].LeavesTotals);
        Assert.False(states[ComingToUldah].IsSpareAlternative);
        Assert.NotEqual(QuestState.Ready, states[ComingToGridania].State);
        Assert.True(states[ComingToGridania].IsOtherPath);
        Assert.Equal(UnderneathTheSultantree, MsqProgress.Compute(Catalog, states)!.Next!.RowId);
    }

    [Fact]
    public void A_new_character_s_class_is_guessed_before_the_city_and_picks_the_city()
    {
        // A Gladiator with nothing in the journal: the class it plays presumes Gladiator, and Gladiator presumes Ul'dah.
        var snapshot = Snapshot() with { JobLevels = Levels((Gladiator, 1)) };
        var states = StateResolver.ResolveAll(Catalog, snapshot, Context);
        var choice = Index.Resolve(snapshot);

        Assert.Equal("Gladiator", LabelOf(PathKind.StartClass, choice, chosen: false));
        Assert.Equal("Ul'dah", LabelOf(PathKind.StartCity, choice, chosen: false));
        Assert.False(states[ComingToUldah].IsSpareAlternative);
        Assert.True(states[ComingToGridania].IsSpareAlternative);
        Assert.Equal(3, states[ComingToUldah].ChoiceOf);
        Assert.False(states[CloseToHomeGladiator].IsSpareAlternative);
        Assert.True(states[CloseToHomeLancer].IsSpareAlternative);
    }

    [Fact]
    public void Undecided_company_choices_presume_one_company()
    {
        var snapshot = Snapshot() with { Accepted = [Accepted(ComingToGridania)], CurrentJob = Lancer, JobLevels = Levels((Lancer, 1)) };
        var choice = Index.Resolve(snapshot);
        var companies = Index.Groups.Where(g => g.Kind == PathKind.GrandCompany).Select(g => g.Options[choice.Presumed[g.Id]].GrandCompany).Distinct().ToList();
        var company = Assert.Single(companies);
        Assert.NotEqual((byte)0, company);
    }

    [Fact]
    public void Resolving_only_the_dependents_of_a_tagged_quest_matches_resolving_everything()
    {
        // Any quest a group tags can decide it now (its options narrow the group), so its change resolves everything.
        var reverse = ReversePrereqIndex.Build(Catalog);
        var tagged = Catalog.All.Where(q => Index.TagsOf(q.RowId).Count > 0 && q.Journal.GenreId == SeventhUmbralEra).Select(q => q.RowId).Where((_, i) => i % 3 == 0).ToList();
        Assert.NotEmpty(tagged);
        var baseline = Snapshot() with { CurrentJob = Lancer, JobLevels = Levels((Lancer, 1)) };
        var before = StateResolver.ResolveAll(Catalog, baseline, Context);
        static string Sig(QuestEvaluation e) => $"{e.State}|{e.NextStep?.Req.Kind}|{e.IsSpareAlternative}|{e.ChoiceOf}|{e.RepeatableDoneBefore}";
        foreach (var rowId in tagged)
        {
            Assert.True(Index.IsAnchor(rowId), $"{rowId} is tagged but not an anchor");
            var changed = baseline with { CompletedBits = Bits(rowId) };
            var incremental = StateResolver.ResolveDependents(before, [rowId], reverse, Catalog, changed, Context);
            var full = StateResolver.ResolveAll(Catalog, changed, Context);
            var differing = full.Where(kv => Sig(kv.Value) != Sig(incremental[kv.Key])).Select(kv => kv.Key).ToList();
            Assert.True(differing.Count == 0, $"completing {rowId}: {differing.Count} rows differ, e.g. {string.Join(", ", differing.Take(3))}");
        }
    }

    [Fact]
    public void Another_company_s_call_of_the_wild_needs_that_company()
    {
        // The sheet leaves Call of the Wild's company at 0; the curated tag says whose officer offers it.
        var snapshot = Snapshot(ComingToGridania, CloseToHomeLancer, 65559, CompanyYouKeepTwinAdder) with { GrandCompany = 2 };
        var states = StateResolver.ResolveAll(Catalog, snapshot, Context);
        var names = fixture.Bundle.BlockerNames();
        var view = new IpcView(Catalog, states, names);

        foreach (var other in new[] { CallOfTheWildMaelstrom, CallOfTheWildImmortalFlames })
        {
            Assert.Equal(QuestState.Blocked, states[other].State);
            Assert.Equal(RequirementKind.GrandCompany, states[other].NextStep!.Req.Kind);
            Assert.StartsWith("Blocked · ", BlockerText.StatusText(states[other], Catalog.ByRowId[other], names, states), StringComparison.Ordinal);
            Assert.False(view.IsQuestAvailable(other));
        }

        Assert.True(Only(states[CallOfTheWildTwinAdder].Requirements, RequirementKind.GrandCompany).Met);

        // The main scenario's company choice is how a character joins one: never gated on a company.
        var newcomer = StateResolver.ResolveAll(Catalog, Snapshot(ComingToGridania), Context);
        Assert.DoesNotContain(newcomer[CompanyYouKeepMaelstrom].Requirements, r => r.Req.Kind == RequirementKind.GrandCompany);
    }
}
