using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Todo;

namespace Tsukimichi.Tests.Plan;

/// <summary>The todo overlay's "Clear my blues" section: the pinned expansion's Ready unlock quests.</summary>
public class PlanTodoTests(PlanFixture fixture) : IClassFixture<PlanFixture>
{
    private TodoInputs Inputs(UnlockPlan? plan, int expansion, bool show = true)
    {
        var snapshot = PlanFixture.Fresh();
        return new TodoInputs(
            fixture.Bundle.Catalog,
            fixture.States(snapshot),
            [],
            fixture.Features,
            0,
            snapshot.CurrentJob,
            snapshot.JobLevels,
            JobLadder.Empty,
            new Dictionary<uint, string>(),
            ShowPins: false,
            ShowNearbyFeature: false,
            ShowMsq: false,
            ShowJobQuests: false,
            Names: fixture.Names,
            ShowSeasonal: false,
            Plan: plan,
            PlanExpansion: expansion,
            ShowPlan: show);
    }

    [Fact]
    public void A_pinned_expansion_lists_its_ready_unlock_quests_in_plan_order()
    {
        var plan = fixture.Plan(PlanFixture.Fresh());
        var arr = plan.Expansion(0)!;
        var expected = arr.Entries.Where(e => e.IsReady).Take(TodoList.MaxPlan).Select(e => e.Quest.RowId).ToArray();
        Assert.NotEmpty(expected);

        var model = TodoList.Build(Inputs(plan, 0));
        var section = Assert.Single(model.Sections);
        Assert.Equal(TodoSection.Plan, section.Section);
        Assert.Equal(1, model.EnabledSections);
        Assert.Equal(expected, section.Rows.Select(r => r.RowId));
        Assert.All(section.Rows, r =>
        {
            Assert.Equal(TodoRowKind.Plan, r.Kind);
            Assert.True(r.State is QuestState.Ready or QuestState.ReadyOnOtherJob);
        });
        Assert.Equal("A Realm Reborn · " + arr.Count + " left", Assert.Single(section.Notes));

        var first = arr.Entries.First(e => e.State == QuestState.Ready);
        Assert.Equal(TodoList.PlanHint(first), section.Rows.First(r => r.RowId == first.Quest.RowId).Hint);
        Assert.StartsWith("Lv " + first.Quest.DisplayLevel + " · ", TodoList.PlanHint(first), StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_pinned_or_the_toggle_off_leaves_the_section_out()
    {
        var plan = fixture.Plan(PlanFixture.Fresh());
        Assert.Same(TodoModel.Empty, TodoList.Build(Inputs(plan, -1)));
        Assert.Same(TodoModel.Empty, TodoList.Build(Inputs(plan, 0, show: false)));
        Assert.Same(TodoModel.Empty, TodoList.Build(Inputs(null, 0)));

        // An expansion with nothing ready is enabled but empty.
        var none = TodoList.Build(Inputs(plan.Filter(new PlanFilter(MaxExpansion: 0)), 3));
        Assert.True(none.IsEmpty);
        Assert.Equal(1, none.EnabledSections);
    }

    [Fact]
    public void The_tier_word_follows_the_level_and_a_set_aside_quest_leaves_the_section()
    {
        // 1.21.0, P4: "Lv 50 · Story needs it · …" in the overlay; a quest set aside is not in the plan's entries.
        var plan = fixture.Plan(PlanFixture.Fresh());
        var first = plan.Expansion(0)!.Entries.First(e => e.State == QuestState.Ready);
        var inputs = Inputs(plan, 0) with { TierOf = static _ => UnlockTier.StoryNeedsIt };
        var row = TodoList.Build(inputs).Sections[0].Rows.First(r => r.RowId == first.Quest.RowId);
        var level = "Lv " + first.Quest.DisplayLevel;
        Assert.Equal(level + " · Story needs it" + TodoList.PlanHint(first)[level.Length..], row.Hint);
        Assert.Equal("Lv 12 · giver", TodoList.WithTier(inputs with { TierOf = null }, first.Quest, "Lv 12 · giver"));
        Assert.Equal("Blocked", TodoList.WithTier(inputs, first.Quest, "Blocked"));

        var aside = UnlockPlan.Build(fixture.Tags, fixture.States(PlanFixture.Fresh()), fixture.Names, setAside: new HashSet<uint> { first.Quest.RowId });
        var rows = TodoList.Build(Inputs(aside, 0)).Sections[0].Rows;
        Assert.DoesNotContain(rows, r => r.RowId == first.Quest.RowId);
    }
}
