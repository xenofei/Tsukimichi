using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Chains;

public sealed class ChainCatalogTests
{
    private const uint A = 65600;
    private const uint B = 65601;
    private const uint C = 65602;
    private const uint D = 65603;
    private const uint E = 65604;

    private static QuestRecord Quest(uint rowId, uint genre, int sortKey, uint[]? previous = null, JoinKind join = JoinKind.All, string? category = null) => new()
    {
        RowId = rowId,
        QuestId = QuestRecord.ToQuestId(rowId),
        InternalId = $"Test_{rowId}",
        Name = $"Quest {rowId}",
        Journal = new JournalRef(1, "Section", genre / 10, category ?? $"Category {genre / 10}", genre, $"Genre {genre}", (int)(genre << 16) | sortKey),
        PreviousQuests = previous is null ? Prereq.None : new Prereq(previous, join),
        Level = 1,
    };

    private static IReadOnlyDictionary<uint, QuestEvaluation> States(params (uint RowId, QuestState State)[] states) =>
        states.ToDictionary(s => s.RowId, s => new QuestEvaluation(s.State, [], null, null, null));

    [Fact]
    public void Linear_genre_is_a_chain()
    {
        var quests = new[] { Quest(A, 10, 1), Quest(B, 10, 2, [A]), Quest(C, 10, 3, [B]) };

        Assert.True(ChainCatalog.IsLinear(quests));
    }

    [Fact]
    public void Single_id_any_join_still_counts_as_the_preceding_quest()
    {
        // The sheet stores lone prerequisites with PreviousQuestJoin = 2 on many quests (Close to Home does).
        var quests = new[] { Quest(A, 10, 1), Quest(B, 10, 2, [A], JoinKind.Any), Quest(C, 10, 3, [B], JoinKind.Any) };

        Assert.True(ChainCatalog.IsLinear(quests));
    }

    [Fact]
    public void Branching_genre_is_not_a_chain()
    {
        // C requires A, not B: two quests hang off A.
        var fork = new[] { Quest(A, 10, 1), Quest(B, 10, 2, [A]), Quest(C, 10, 3, [A]) };
        Assert.False(ChainCatalog.IsLinear(fork));

        // C requires both A and B: a join, not a line.
        var join = new[] { Quest(A, 10, 1), Quest(B, 10, 2, [A]), Quest(C, 10, 3, [A, B]) };
        Assert.False(ChainCatalog.IsLinear(join));

        // Any join over two ids is not a single previous quest either.
        var any = new[] { Quest(A, 10, 1), Quest(B, 10, 2, [A]), Quest(C, 10, 3, [A, B], JoinKind.Any) };
        Assert.False(ChainCatalog.IsLinear(any));

        // B has no prerequisite at all: two starts.
        var gap = new[] { Quest(A, 10, 1), Quest(B, 10, 2), Quest(C, 10, 3, [B]) };
        Assert.False(ChainCatalog.IsLinear(gap));
    }

    [Fact]
    public void Prerequisite_outside_the_genre_breaks_the_line()
    {
        var quests = new[] { Quest(A, 10, 1), Quest(B, 10, 2, [D]), Quest(C, 10, 3, [B]) };

        Assert.False(ChainCatalog.IsLinear(quests));
    }

    [Fact]
    public void Empty_and_single_quest_genres_are_not_chains()
    {
        Assert.False(ChainCatalog.IsLinear([]));
        Assert.False(ChainCatalog.IsLinear([Quest(A, 10, 1)]));
    }

    [Fact]
    public void Build_derives_a_chain_per_linear_genre_in_journal_order()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(C, 10, 3, [B]),
            Quest(A, 10, 1),
            Quest(B, 10, 2, [A]),
            Quest(D, 20, 1),
            Quest(E, 20, 2, [A]),
        ]);

        var chains = ChainCatalog.Build(catalog, CuratedData.Empty);

        var chain = Assert.Single(chains.Chains);
        Assert.Equal("Genre 10", chain.Name);
        Assert.Equal([A, B, C], chain.RowIds);
        Assert.Same(chain, chains.ForQuest(B));
        Assert.Null(chains.ForQuest(D));
        Assert.Null(chains.ForQuest(E));
        Assert.Null(chains.ForQuest(99));
        Assert.Empty(chains.Warnings);
    }

    [Fact]
    public void Unlisted_quests_never_form_a_chain()
    {
        var catalog = QuestCatalog.Build([Quest(A, 0, 1), Quest(B, 0, 2, [A])]);

        Assert.Empty(ChainCatalog.Build(catalog, CuratedData.Empty).Chains);
    }

    [Fact]
    public void Derived_names_are_qualified_by_category_when_genre_names_repeat()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(A, 10, 1, category: "Amalj'aa Quests") with { Journal = new JournalRef(1, "S", 1, "Amalj'aa Quests", 10, "Main Quests", 10 << 16 | 1) },
            Quest(B, 10, 2, [A]) with { Journal = new JournalRef(1, "S", 1, "Amalj'aa Quests", 10, "Main Quests", 10 << 16 | 2) },
            Quest(C, 20, 1) with { Journal = new JournalRef(1, "S", 2, "Sylph Quests", 20, "Main Quests", 20 << 16 | 1) },
            Quest(D, 20, 2, [C]) with { Journal = new JournalRef(1, "S", 2, "Sylph Quests", 20, "Main Quests", 20 << 16 | 2) },
        ]);

        var chains = ChainCatalog.Build(catalog, CuratedData.Empty);

        Assert.Equal(["Main Quests (Amalj'aa Quests)", "Main Quests (Sylph Quests)"], chains.Chains.Select(c => c.Name));
    }

    [Fact]
    public void Curated_chain_concatenates_genres_in_the_listed_order_and_claims_them()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(A, 10, 1),
            Quest(B, 10, 2, [A]),
            Quest(C, 20, 1, [B]),
            Quest(D, 20, 2, [C]),
        ]);
        var curated = Curated(new CuratedChain("Story", [20, 10], "reversed on purpose"));

        var chains = ChainCatalog.Build(catalog, curated);

        var chain = Assert.Single(chains.Chains);
        Assert.Equal("Story", chain.Name);
        Assert.Equal([C, D, A, B], chain.RowIds);
        Assert.All(new[] { A, B, C, D }, id => Assert.Same(chain, chains.ForQuest(id)));
        Assert.Empty(chains.Warnings);
    }

    [Fact]
    public void Curated_chain_accepts_a_branching_genre_in_journal_order()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(C, 10, 3, [A]),
            Quest(A, 10, 1),
            Quest(B, 10, 2, [A]),
        ]);

        var chains = ChainCatalog.Build(catalog, Curated(new CuratedChain("Fork", [10], null)));

        var chain = Assert.Single(chains.Chains);
        Assert.Equal([A, B, C], chain.RowIds);
    }

    [Fact]
    public void Curated_chain_with_an_unknown_genre_warns_and_keeps_the_rest()
    {
        var catalog = QuestCatalog.Build([Quest(A, 10, 1), Quest(B, 10, 2, [A])]);

        var chains = ChainCatalog.Build(catalog, Curated(
            new CuratedChain("Known and unknown", [10, 999], null),
            new CuratedChain("Nothing", [998], null)));

        var chain = Assert.Single(chains.Chains);
        Assert.Equal("Known and unknown", chain.Name);
        Assert.Equal([A, B], chain.RowIds);
        Assert.Equal(2, chains.Warnings.Count);
        Assert.Contains(chains.Warnings, w => w.Contains("999"));
        Assert.Contains(chains.Warnings, w => w.Contains("Nothing") && w.Contains("998"));
    }

    [Fact]
    public void Curated_chains_win_over_derived_ones_for_a_shared_quest()
    {
        var catalog = QuestCatalog.Build([Quest(A, 10, 1), Quest(B, 10, 2, [A]), Quest(C, 20, 1), Quest(D, 20, 2, [C])]);

        var chains = ChainCatalog.Build(catalog, Curated(new CuratedChain("Named", [10], null)));

        Assert.Equal(["Named", "Genre 20"], chains.Chains.Select(c => c.Name));
        Assert.Equal("Named", chains.ForQuest(A)!.Name);
        Assert.Equal("Genre 20", chains.ForQuest(D)!.Name);
    }

    [Fact]
    public void Progress_counts_completed_and_points_at_the_first_open_quest()
    {
        var chain = new Chain("Story", [A, B, C, D]);

        var progress = ChainCatalog.Progress(chain, States((A, QuestState.Completed), (B, QuestState.Completed), (C, QuestState.Ready)));

        Assert.Equal(new ChainProgress(2, 4, C), progress);
        Assert.False(progress.IsComplete);
        Assert.Equal(0.5f, progress.Fraction);
    }

    [Fact]
    public void Progress_skips_a_completed_quest_after_a_gap()
    {
        var chain = new Chain("Story", [A, B, C]);

        var progress = ChainCatalog.Progress(chain, States((A, QuestState.Completed), (C, QuestState.Completed)));

        Assert.Equal(new ChainProgress(2, 3, B), progress);
    }

    [Fact]
    public void Progress_of_a_finished_chain_has_no_next()
    {
        var chain = new Chain("Story", [A, B]);

        var progress = ChainCatalog.Progress(chain, States((A, QuestState.Completed), (B, QuestState.Completed)));

        Assert.Equal(new ChainProgress(2, 2, null), progress);
        Assert.True(progress.IsComplete);
        Assert.Equal(1f, progress.Fraction);
    }

    [Fact]
    public void Progress_leaves_out_repeatables_and_locked_out_quests_and_never_points_at_them()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(A, 10, 1),
            Quest(B, 10, 2, [A]) with { IsRepeatable = true },
            Quest(C, 10, 3, [A]),
            Quest(D, 10, 4, [C]),
        ]);
        var chain = Assert.Single(ChainCatalog.Build(catalog, Curated(new CuratedChain("Chronicle", [10], null))).Chains);
        Assert.Equal([B], chain.Uncounted);

        var progress = ChainCatalog.Progress(chain, States((A, QuestState.Completed), (B, QuestState.Ready), (C, QuestState.Foreclosed), (D, QuestState.Completed)));

        Assert.Equal(new ChainProgress(2, 2, null), progress);
        Assert.True(progress.IsComplete);
    }

    [Fact]
    public void A_curated_genre_orders_its_steps_by_the_prerequisite_graph()
    {
        // B was refiled into the genre and keeps the sheet's SortKey 0, which sorts it before its prerequisite C.
        var catalog = QuestCatalog.Build(
        [
            Quest(A, 10, 1),
            Quest(B, 10, 0, [C]),
            Quest(C, 10, 3, [A]),
            Quest(D, 10, 4, [B]),
        ]);
        Assert.Equal([B, A, C, D], catalog.ByGenre[10].Select(q => q.RowId));

        var chain = Assert.Single(ChainCatalog.Build(catalog, Curated(new CuratedChain("Relic", [10], null))).Chains);

        Assert.Equal([A, C, B, D], chain.RowIds);
        Assert.Equal([A, C, D], ChainCatalog.InPrerequisiteOrder([A, C, D], catalog));
    }

    [Fact]
    public void A_chain_with_nothing_counted_is_empty_and_never_complete()
    {
        // Every step locked out or out of season for this character: nothing to do and nothing done.
        var progress = ChainCatalog.Progress(new Chain("Another city's festival", [A, B]), States((A, QuestState.Foreclosed), (B, QuestState.Foreclosed)));

        Assert.Equal(new ChainProgress(0, 0, null), progress);
        Assert.True(progress.IsEmpty);
        Assert.False(progress.IsComplete);
        Assert.Equal(0f, progress.Fraction);
        Assert.False(new ChainProgress(1, 2, B).IsEmpty);
    }

    [Fact]
    public void Progress_without_evaluations_is_zero_with_the_first_quest_next()
    {
        var progress = ChainCatalog.Progress(new Chain("Story", [A, B]), States());

        Assert.Equal(new ChainProgress(0, 2, A), progress);
        Assert.Equal(0f, progress.Fraction);
    }

    private static CuratedData Curated(params CuratedChain[] chains)
    {
        // CuratedData has no public constructor; round-trip through a file so the loader shape stays the single truth.
        using var tmp = new Storage.TempDir();
        var dir = tmp.File("curated");
        Directory.CreateDirectory(dir);
        var entries = chains.Select(c =>
            $"{{ \"name\": \"{c.Name}\", \"genreIds\": [{string.Join(", ", c.GenreIds)}]{(c.Note is null ? string.Empty : $", \"note\": \"{c.Note}\"")} }}");
        File.WriteAllText(Path.Combine(dir, CuratedData.ChainsFileName), $"{{ \"chains\": [ {string.Join(", ", entries)} ] }}");
        var data = CuratedData.Load(dir);
        Assert.Empty(data.Warnings);
        return data;
    }
}
