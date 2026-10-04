using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Text;
using Tsukimichi.Core.Todo;
using Tsukimichi.Tests.Data;

namespace Tsukimichi.Tests.Todo;

/// <summary>
/// Up next's pick (plan v7, 1.21.0 P1; spec-1.21 P1 and decision 1, settled 3 October 2026): route, goal, main
/// scenario, first Ready pin, closest Ready stop, then the level gate; each rule falls through to the next when it has
/// nothing the character can act on. <c>/tsuki next</c> reads the same picker (<see cref="GuidancePick"/> delegates to
/// it), so the chat and Tonight agree.
/// </summary>
public sealed class UpNextPickerTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private static QuestEvaluation State(QuestState state) => new(state, [], null, null, null);

    // 1 route stop, 2 goal quest, 3 main scenario, 4 pin, 5 Next stop, 6 level gate; 9 blocked.
    private static Dictionary<uint, QuestEvaluation> Everything() => new()
    {
        [1] = State(QuestState.Ready),
        [2] = State(QuestState.Accepted),
        [3] = State(QuestState.Ready),
        [4] = State(QuestState.Ready),
        [5] = State(QuestState.Ready),
        [6] = State(QuestState.Blocked),
        [9] = State(QuestState.Blocked),
    };

    [Fact]
    public void The_order_is_route_goal_story_pin_stop_then_level()
    {
        Assert.Equal(
            [UpNextRule.Route, UpNextRule.Goal, UpNextRule.MainScenario, UpNextRule.Pinned, UpNextRule.ClosestStop, UpNextRule.LevelGate],
            UpNextPicker.Order);
    }

    [Fact]
    public void Each_rule_wins_over_the_ones_after_it()
    {
        var states = Everything();
        Assert.Equal(new UpNextPick(1, UpNextRule.Route), UpNextPicker.Pick(states, 1, [2], 3, [4], [5], 6));
        Assert.Equal(new UpNextPick(2, UpNextRule.Goal), UpNextPicker.Pick(states, null, [2], 3, [4], [5], 6));
        Assert.Equal(new UpNextPick(3, UpNextRule.MainScenario), UpNextPicker.Pick(states, null, [], 3, [4], [5], 6));
        Assert.Equal(new UpNextPick(4, UpNextRule.Pinned), UpNextPicker.Pick(states, null, [], null, [4], [5], 6));
        Assert.Equal(new UpNextPick(5, UpNextRule.ClosestStop), UpNextPicker.Pick(states, null, [], null, [], [5], 6));
        Assert.Equal(new UpNextPick(6, UpNextRule.LevelGate), UpNextPicker.Pick(states, null, [], null, [], [], 6));
        Assert.Null(UpNextPicker.Pick(states, null, [], null, [], [], null));
    }

    [Fact]
    public void A_rule_with_nothing_to_act_on_falls_through()
    {
        var states = Everything();

        // A blocked route stop, a goal whose quests all wait, a blocked story quest, a pin and a stop not Ready.
        Assert.Equal(new UpNextPick(4, UpNextRule.Pinned), UpNextPicker.Pick(states, 9, [9], 9, [9, 4], [5], 6));
        Assert.Equal(new UpNextPick(5, UpNextRule.ClosestStop), UpNextPicker.Pick(states, 9, [9], 9, [9, 2], [9, 5], 6));
        Assert.Equal(new UpNextPick(6, UpNextRule.LevelGate), UpNextPicker.Pick(states, 9, [9], 9, [9], [9], 6));

        // A pin in the journal is no Ready pin; a goal quest in the journal counts, after a waiting one.
        Assert.Equal(new UpNextPick(2, UpNextRule.Goal), UpNextPicker.Pick(states, null, [9, 2], null, [2], [], null));
        Assert.Null(UpNextPicker.Pick(states, null, [], null, [2], [2], null));
    }

    [Fact]
    public void A_route_you_chose_comes_before_the_goal()
    {
        // Settled 3 October 2026: the goal comes second, after a route you chose.
        var states = Everything();
        Assert.Equal(UpNextRule.Route, UpNextPicker.Pick(states, 1, [2], null, [], [], null)!.Value.Rule);
        Assert.Equal(UpNextRule.Goal, UpNextPicker.Pick(states, 9, [2], 3, [], [], null)!.Value.Rule);
    }

    [Fact]
    public void Tsuki_next_follows_the_same_picker()
    {
        var states = Everything();
        Assert.Equal((1u, GuidanceReason.Route), GuidancePick.Pick(states, 1, 3, [4], [5]));
        Assert.Equal((3u, GuidanceReason.MainScenario), GuidancePick.Pick(states, 9, 3, [4], [5]));
        Assert.Equal((4u, GuidanceReason.Pinned), GuidancePick.Pick(states, null, 9, [4], [5]));
        Assert.Equal((5u, GuidanceReason.ClosestStop), GuidancePick.Pick(states, null, null, [9], [5]));

        // Every combination without a goal or a gate picks what Up next picks.
        uint?[] routes = [null, 1, 9];
        uint?[] stories = [null, 3, 9];
        foreach (var route in routes)
        {
            foreach (var story in stories)
            {
                var up = UpNextPicker.Pick(states, route, [], story, [9, 4], [5], null);
                var next = GuidancePick.Pick(states, route, story, [9, 4], [5]);
                Assert.Equal(up?.RowId, next?.RowId);
            }
        }
    }

    [Fact]
    public void The_level_gate_line_is_plain_speech()
    {
        var line = GuidanceText.NextLevelGate("The Darkness Below", 70, 70, "DRK", 69);
        Assert.Equal("Next: The Darkness Below needs level 70. You are DRK 69.", line.Text);
        Assert.Equal(70u, line.QuestRowId);
        Assert.True(GuidanceText.IsSpeakable(line.Text));
        Assert.Equal("Next: X needs level 5. You are level 4.", GuidanceText.NextLevelGate("X", 1, 5, null, 4).Text);
    }

    [Fact]
    public void A_masked_story_quest_ahead_is_never_picked_by_name()
    {
        // Up next only picks what the character can act on now: over the frozen catalog, at Dawntrail's first quest the
        // pick is that quest, which the shield never masks, while the quest after it (past the story point) is masked.
        var catalog = fixture.Bundle.Catalog;
        var story = MsqGraph.For(catalog).Story;
        var at = story.ToList().FindIndex(q => q.Expansion == 5);
        var states = new Dictionary<uint, QuestEvaluation>();
        foreach (var quest in catalog.All)
        {
            states[quest.RowId] = State(QuestState.Blocked);
        }

        for (var i = 0; i < story.Count; i++)
        {
            states[story[i].RowId] = State(i < at ? QuestState.Completed : i == at ? QuestState.Ready : QuestState.Blocked);
        }

        var mask = SpoilerMask.Build(catalog, states, SpoilerOptions.Default);
        var ahead = story[at + SpoilerOptions.DefaultAhead + 1];
        var pick = UpNextPicker.Pick(states, ahead.RowId, [ahead.RowId], MsqProgress.Compute(catalog, states)?.Next?.RowId, [ahead.RowId], [], null);

        Assert.Equal(new UpNextPick(story[at].RowId, UpNextRule.MainScenario), pick);
        Assert.False(mask.IsMasked(story[at]));
        Assert.True(mask.IsMasked(ahead));
        Assert.NotEqual(ahead.Name, mask.DisplayName(ahead));
    }
}
