using Tsukimichi.Core.Plan;

namespace Tsukimichi.Tests.Plan;

/// <summary>
/// Set aside in the plan (feature plan v7 P4): a set-aside quest leaves the entries and every count, its own view lists
/// it, and a view that keeps a moved row in place puts it exactly where it stood, without counting it.
/// </summary>
public class PlanSetAsideTests(PlanFixture fixture) : IClassFixture<PlanFixture>
{
    private const uint HalloHalatali = 66233;
    private const uint IfritBleeds = 66584;

    private UnlockPlan Plan(params uint[] setAside) =>
        UnlockPlan.Build(fixture.Tags, fixture.States(PlanFixture.Fresh()), fixture.Names, setAside: new HashSet<uint>(setAside));

    [Fact]
    public void A_set_aside_quest_leaves_the_entries_and_every_count()
    {
        var all = Plan();
        var aside = Plan(HalloHalatali, IfritBleeds);

        Assert.Equal(all.Count - 2, aside.Count);
        Assert.Equal(2, aside.SetAsideCount);
        Assert.DoesNotContain(aside.Entries, e => e.Quest.RowId == HalloHalatali);
        Assert.Equal(all.Expansions[0].Count - 2, aside.Expansions[0].Count);
        var readyAside = all.Entries.Count(e => e.IsReady && e.Quest.RowId is HalloHalatali or IfritBleeds);
        Assert.Equal(all.ReadyCount - readyAside, aside.ReadyCount);
        Assert.Equal(0, all.SetAsideCount);
    }

    [Fact]
    public void The_set_aside_view_lists_them_grouped_like_the_plan()
    {
        var view = Plan(HalloHalatali, IfritBleeds).SetAside;

        Assert.Equal([HalloHalatali, IfritBleeds], view.Entries.Select(static e => e.Quest.RowId).Order());
        Assert.All(view.Entries, static e => Assert.True(e.IsSetAside));
        Assert.Equal(0, view.Count);
        Assert.Equal(2, view.Expansions.Sum(static b => b.SetAsideCount));
        Assert.True(Plan().SetAside.IsEmpty);
    }

    [Fact]
    public void A_row_set_aside_keeps_its_place_until_the_view_is_rebuilt()
    {
        var before = Plan().Filter(PlanFilter.None);
        var index = before.Entries.ToList().FindIndex(static e => e.Quest.RowId == HalloHalatali);
        Assert.True(index >= 0);

        // The plan rebuilt with the quest set aside, the view keeping it in place.
        var after = Plan(HalloHalatali);
        var kept = after.Filter(PlanFilter.None, new HashSet<uint> { HalloHalatali });

        Assert.Equal(before.Entries.Select(static e => e.Quest.RowId), kept.Entries.Select(static e => e.Quest.RowId));
        Assert.True(kept.Entries[index].IsSetAside);
        Assert.Equal(before.Count - 1, kept.Count);
        Assert.Equal(before.Expansions[0].Count - 1, kept.Expansions[0].Count);

        // Rebuilt (nothing kept in place): the row is gone.
        Assert.DoesNotContain(after.Filter(PlanFilter.None).Entries, static e => e.Quest.RowId == HalloHalatali);
    }

    [Fact]
    public void A_row_brought_back_keeps_its_place_in_the_set_aside_view()
    {
        var before = Plan(HalloHalatali, IfritBleeds).SetAside;
        var after = Plan(IfritBleeds).SetAside.Filter(PlanFilter.None, new HashSet<uint> { HalloHalatali });

        Assert.Equal(before.Entries.Select(static e => e.Quest.RowId), after.Entries.Select(static e => e.Quest.RowId));
        Assert.False(after.Entries.Single(static e => e.Quest.RowId == HalloHalatali).IsSetAside);
    }

    [Fact]
    public void Kind_filters_apply_to_rows_kept_in_place()
    {
        var after = Plan(HalloHalatali);
        var dungeons = after.Filter(new PlanFilter(UnlockKinds.Mask(UnlockKind.Dungeon)), new HashSet<uint> { HalloHalatali });
        var trials = after.Filter(new PlanFilter(UnlockKinds.Mask(UnlockKind.Trial)), new HashSet<uint> { HalloHalatali });

        Assert.Single(dungeons.Entries, static e => e.Quest.RowId == HalloHalatali);
        Assert.DoesNotContain(trials.Entries, static e => e.Quest.RowId == HalloHalatali);
    }
}
