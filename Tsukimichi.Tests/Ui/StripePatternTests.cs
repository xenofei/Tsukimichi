using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

public class StripePatternTests
{
    public static TheoryData<QuestState> AllStates()
    {
        var data = new TheoryData<QuestState>();
        foreach (var state in Enum.GetValues<QuestState>())
        {
            data.Add(state);
        }

        return data;
    }

    [Fact]
    public void All_eight_patterns_differ()
    {
        var states = Enum.GetValues<QuestState>();
        Assert.Equal(8, states.Length);
        for (var i = 0; i < states.Length; i++)
        {
            for (var j = i + 1; j < states.Length; j++)
            {
                Assert.False(
                    StripePattern.Segments(states[i]).SequenceEqual(StripePattern.Segments(states[j])),
                    $"{states[i]} and {states[j]} share a stripe pattern");
            }
        }
    }

    [Fact]
    public void The_named_states_have_the_planned_shapes()
    {
        // Feature plan v3 T15: solid (Completed), two segments (In journal), 60 % centred (Ready), none (Blocked).
        Assert.Equal([new StripeSegment(0f, 1f)], StripePattern.Segments(QuestState.Completed).ToArray());
        Assert.Equal(2, StripePattern.Segments(QuestState.Accepted).Length);
        Assert.Equal(0f, StripePattern.Segments(QuestState.Accepted)[0].Start);
        Assert.Equal(1f, StripePattern.Segments(QuestState.Accepted)[1].End);
        var ready = Assert.Single(StripePattern.Segments(QuestState.Ready).ToArray());
        Assert.Equal(0.6f, ready.End - ready.Start, 4);
        Assert.Equal(0.5f, (ready.Start + ready.End) * 0.5f, 4);
        Assert.True(StripePattern.Segments(QuestState.Blocked).IsEmpty);
    }

    [Fact]
    public void Dashes_are_longer_and_fewer_than_dots()
    {
        var dashes = StripePattern.Segments(QuestState.Foreclosed);
        var dots = StripePattern.Segments(QuestState.Unknown);
        Assert.True(dashes.Length >= 3);
        Assert.True(dots.Length > dashes.Length);
        Assert.True(dashes[0].End - dashes[0].Start >= 2f * (dots[0].End - dots[0].Start));
    }

    [Theory]
    [MemberData(nameof(AllStates))]
    public void Segments_are_inside_the_row_ascending_and_apart(QuestState state)
    {
        var previousEnd = 0f;
        var first = true;
        foreach (var segment in StripePattern.Segments(state))
        {
            Assert.InRange(segment.Start, 0f, 1f);
            Assert.InRange(segment.End, 0f, 1f);
            Assert.True(segment.End > segment.Start, $"{state}: empty segment");
            if (!first)
            {
                Assert.True(segment.Start > previousEnd, $"{state}: segments touch or overlap");
            }

            previousEnd = segment.End;
            first = false;
        }
    }

    [Fact]
    public void Pattern_names_are_distinct_and_the_tooltip_leads_with_the_state_name()
    {
        var names = Enum.GetValues<QuestState>().Select(StripePattern.Name).ToArray();
        Assert.All(names, name => Assert.False(string.IsNullOrEmpty(name)));
        Assert.Equal(names.Length, names.Distinct().Count());

        Assert.Equal("Ready · short bar", StripePattern.Tooltip(QuestState.Ready));
        Assert.Equal("In journal · two bars", StripePattern.Tooltip(QuestState.Accepted));
        Assert.Equal("Done today · lower half", StripePattern.Tooltip(QuestState.DoneThisCycle, StateNames.DailyInterval));
        Assert.Equal("Done this week · lower half", StripePattern.Tooltip(QuestState.DoneThisCycle, StateNames.WeeklyInterval));
        Assert.Equal("Done this cycle · lower half", StripePattern.Tooltip(QuestState.DoneThisCycle));
        foreach (var state in Enum.GetValues<QuestState>())
        {
            Assert.StartsWith(StateNames.Name(state) + StripePattern.Separator, StripePattern.Tooltip(state), StringComparison.Ordinal);
            Assert.Same(StripePattern.Tooltip(state), StripePattern.Tooltip(state));
        }
    }
}
