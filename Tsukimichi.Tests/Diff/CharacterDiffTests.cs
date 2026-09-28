using Tsukimichi.Core.Diff;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Diff;

public class CharacterDiffTests
{
    // Section 0: main scenario (1, 2, the two Grand Company choices 3 and 4). Section 2: side quests 10..14, with 12 a
    // feature quest and 13 a feature quest with two unique rewards. 99 is unlisted.
    private static readonly QuestCatalog Catalog = QuestCatalog.Build(
    [
        Quest(1, "Coming to Gridania", section: 0, category: 1, genre: 1, sortKey: 10),
        Quest(2, "Close to Home", section: 0, category: 1, genre: 1, sortKey: 20),
        Quest(3, "The Company You Keep (Maelstrom)", section: 0, category: 1, genre: 1, sortKey: 30),
        Quest(4, "The Company You Keep (Twin Adder)", section: 0, category: 1, genre: 1, sortKey: 31),
        Quest(10, "Side A", section: 2, category: 10, genre: 100, sortKey: 100),
        Quest(11, "Side B", section: 2, category: 10, genre: 100, sortKey: 110),
        Quest(12, "Unlock the thing", section: 2, category: 10, genre: 100, sortKey: 120),
        Quest(13, "Unlock the mount", section: 2, category: 10, genre: 100, sortKey: 130),
        Quest(14, "Side C", section: 2, category: 10, genre: 100, sortKey: 140),
        Quest(99, "Removed", section: 0, category: 0, genre: 0, sortKey: 1),
    ]);

    private static readonly DiffContext Ctx = DiffContext.For(
        Catalog,
        new HashSet<uint> { 12, 13 },
        rowId => rowId == 13 ? 2 : 0);

    private static Dictionary<uint, QuestEvaluation> Done(params uint[] rowIds)
    {
        var states = States(Catalog, QuestState.Ready);
        foreach (var rowId in rowIds)
        {
            states[rowId] = QuestState.Completed;
        }

        return Evaluations(states);
    }

    [Fact]
    public void Splits_quests_into_only_a_only_b_shared_and_neither()
    {
        var a = Done(1, 2, 10, 12);
        var b = Done(1, 11);

        var diff = CharacterDiff.Compute(Catalog, a, b, Ctx);

        Assert.Equal([12u, 2u, 10u], diff.OnlyA.Select(e => e.RowId));
        Assert.Equal([11u], diff.OnlyB.Select(e => e.RowId));
        Assert.Equal(1, diff.SharedDone);
        Assert.Equal(4, diff.NeitherDone); // 3, 4, 13, 14
        Assert.Equal(2, diff.Lead);
    }

    [Fact]
    public void Neither_done_skips_a_quest_out_of_season_for_both()
    {
        var a = Done(1);
        var b = Done(1);
        var seasonal = new RequirementResult(new SeasonalRequirement(7, false), false, "seasonal event not active");
        a[14] = new QuestEvaluation(QuestState.Blocked, [seasonal], seasonal, null, null);
        b[14] = new QuestEvaluation(QuestState.Blocked, [seasonal], seasonal, null, null);
        // Out of season for one side only stays pending: the other could still do it.
        a[11] = new QuestEvaluation(QuestState.Blocked, [seasonal], seasonal, null, null);

        var diff = CharacterDiff.Compute(Catalog, a, b, Ctx);

        Assert.Equal(7, diff.NeitherDone); // 2, 3, 4, 10, 11, 12, 13; not 14
    }

    [Fact]
    public void Value_adds_base_main_scenario_feature_and_two_per_unique_reward()
    {
        Assert.Equal(1, CharacterDiff.ValueOf(10, Ctx));
        Assert.Equal(4, CharacterDiff.ValueOf(1, Ctx));
        Assert.Equal(6, CharacterDiff.ValueOf(12, Ctx));
        Assert.Equal(10, CharacterDiff.ValueOf(13, Ctx));
    }

    [Fact]
    public void Reason_names_the_parts_of_the_value()
    {
        Assert.Equal("Side quest", CharacterDiff.ReasonOf(10, Ctx));
        Assert.Equal("Main scenario", CharacterDiff.ReasonOf(1, Ctx));
        Assert.Equal("Unlock quest", CharacterDiff.ReasonOf(12, Ctx));
        Assert.Equal("Unlock quest · 2 unique rewards", CharacterDiff.ReasonOf(13, Ctx));

        var oneReward = new DiffContext(new HashSet<uint>(), _ => 1, _ => true);
        Assert.Equal("Main scenario · 1 unique reward", CharacterDiff.ReasonOf(10, oneReward));
    }

    [Fact]
    public void Entries_sort_by_value_descending_then_journal_order()
    {
        var a = Done(14, 13, 11, 10, 2, 1, 12);
        var b = Done();

        var diff = CharacterDiff.Compute(Catalog, a, b, Ctx);

        Assert.Equal([13u, 12u, 1u, 2u, 10u, 11u, 14u], diff.OnlyA.Select(e => e.RowId));
        Assert.Equal([10, 6, 4, 4, 1, 1, 1], diff.OnlyA.Select(e => e.Value));
        Assert.Empty(diff.OnlyB);
    }

    [Fact]
    public void A_quest_foreclosed_on_the_other_side_is_not_missing()
    {
        // A chose the Maelstrom, B the Twin Adder: each has the other's choice foreclosed.
        var a = Done(1, 2, 3);
        a[4] = new QuestEvaluation(QuestState.Foreclosed, [], null, null, null);
        var b = Done(1, 2, 4);
        b[3] = new QuestEvaluation(QuestState.Foreclosed, [], null, null, null);

        var diff = CharacterDiff.Compute(Catalog, a, b, Ctx);

        Assert.Empty(diff.OnlyA);
        Assert.Empty(diff.OnlyB);
        Assert.Equal(2, diff.SharedDone);
        Assert.Empty(diff.Sections);
    }

    [Fact]
    public void A_quest_foreclosed_on_both_sides_counts_as_neither_done_only_when_still_open_to_one()
    {
        var a = Done();
        var b = Done();
        a[3] = new QuestEvaluation(QuestState.Foreclosed, [], null, null, null);
        b[3] = new QuestEvaluation(QuestState.Foreclosed, [], null, null, null);
        a[4] = new QuestEvaluation(QuestState.Foreclosed, [], null, null, null);

        var diff = CharacterDiff.Compute(Catalog, a, b, Ctx);

        // Nine listed quests; 3 is closed to both and drops out, 4 is still open to B.
        Assert.Equal(8, diff.NeitherDone);
    }

    [Fact]
    public void Unlisted_quests_never_enter_the_diff()
    {
        var a = Done(99);
        var b = Done();

        var diff = CharacterDiff.Compute(Catalog, a, b, Ctx);

        Assert.Empty(diff.OnlyA);
        Assert.Equal(9, diff.NeitherDone);
    }

    [Fact]
    public void Missing_evaluations_read_as_not_done()
    {
        var a = Done(10);
        var b = new Dictionary<uint, QuestEvaluation>();

        var diff = CharacterDiff.Compute(Catalog, a, b, Ctx);

        Assert.Equal([10u], diff.OnlyA.Select(e => e.RowId));
        Assert.Equal(0, diff.SharedDone);
    }

    [Fact]
    public void Section_counts_cover_only_sections_with_a_difference_in_section_order()
    {
        var a = Done(1, 2, 10);
        var b = Done(1, 11, 12);

        var diff = CharacterDiff.Compute(Catalog, a, b, Ctx);

        Assert.Equal(2, diff.Sections.Count);
        Assert.Equal(new DiffSectionCount(0, "Section 0", 1, 0), diff.Sections[0]);
        Assert.Equal(new DiffSectionCount(2, "Section 2", 1, 2), diff.Sections[1]);
    }

    [Fact]
    public void Identical_states_yield_no_entries()
    {
        var a = Done(1, 2, 10, 13);
        var diff = CharacterDiff.Compute(Catalog, a, Done(1, 2, 10, 13), Ctx);

        Assert.Empty(diff.OnlyA);
        Assert.Empty(diff.OnlyB);
        Assert.Equal(4, diff.SharedDone);
        Assert.Equal(0, diff.Lead);
    }

    [Fact]
    public void Empty_catalog_yields_the_empty_result()
    {
        var diff = CharacterDiff.Compute(QuestCatalog.Empty, new Dictionary<uint, QuestEvaluation>(), new Dictionary<uint, QuestEvaluation>(), Ctx);
        Assert.Same(DiffResult.Empty, diff);
    }

    [Fact]
    public void Context_for_a_catalog_treats_sections_zero_and_one_as_main_scenario_but_not_unlisted_rows()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(1, "ARR", section: 0, category: 1, genre: 1),
            Quest(2, "DT", section: 1, category: 3, genre: 3),
            Quest(3, "Side", section: 2),
            Quest(4, "Removed", section: 0, category: 0, genre: 0),
        ]);
        var ctx = DiffContext.For(catalog, new HashSet<uint>(), _ => 0);

        Assert.True(ctx.IsMsq(1));
        Assert.True(ctx.IsMsq(2));
        Assert.False(ctx.IsMsq(3));
        Assert.False(ctx.IsMsq(4));
        Assert.False(ctx.IsMsq(500));
    }
}
