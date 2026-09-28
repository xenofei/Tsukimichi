using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The tooltip every state moon shows on hover (feature plan v3 T8): the display name first, the glyph's shape hint
/// after the separator, composed once per state so hovering allocates nothing.
/// </summary>
public class StateTooltipTests
{
    public static IEnumerable<object[]> States() =>
        Enum.GetValues<QuestState>().Select(static s => new object[] { s });

    [Theory]
    [MemberData(nameof(States))]
    public void Tooltip_starts_with_the_name_and_carries_the_shape_hint(QuestState state)
    {
        var tooltip = StateNames.Tooltip(state);
        Assert.False(string.IsNullOrWhiteSpace(tooltip), state.ToString());
        Assert.StartsWith(StateNames.Name(state), tooltip, StringComparison.Ordinal);
        Assert.Contains(StateNames.GlyphSubtitle(state), tooltip, StringComparison.Ordinal);
        Assert.Equal(StateNames.Name(state) + StateNames.TooltipSeparator + StateNames.GlyphSubtitle(state), tooltip);
        Assert.Equal(tooltip, StateNames.Tooltip(state, quest: null));
    }

    [Theory]
    [MemberData(nameof(States))]
    public void Tooltip_is_the_same_instance_every_time(QuestState state)
    {
        Assert.Same(StateNames.Tooltip(state), StateNames.Tooltip(state));
        Assert.Same(StateNames.Tooltip(state, StateNames.DailyInterval), StateNames.Tooltip(state, StateNames.DailyInterval));
    }

    [Theory]
    [InlineData(QuestState.Blocked, "Blocked · new moon, silver ring")]
    [InlineData(QuestState.Ready, "Ready · first quarter, glow")]
    [InlineData(QuestState.Completed, "Completed · full moon")]
    [InlineData(QuestState.Foreclosed, "Locked out · eclipsed")]
    [InlineData(QuestState.Unknown, "Not checked · veiled")]
    public void Tooltip_reads_as_the_plan_example(QuestState state, string expected)
    {
        Assert.Equal(expected, StateNames.Tooltip(state));
    }

    [Theory]
    [InlineData(StateNames.DailyInterval, "Done today")]
    [InlineData(StateNames.WeeklyInterval, "Done this week")]
    [InlineData((byte)0, "Done this cycle")]
    public void Done_repeatable_tooltip_is_named_by_its_reset(byte repeatInterval, string expectedName)
    {
        var tooltip = StateNames.Tooltip(QuestState.DoneThisCycle, repeatInterval);
        Assert.StartsWith(expectedName + StateNames.TooltipSeparator, tooltip, StringComparison.Ordinal);
        Assert.EndsWith(StateNames.GlyphSubtitle(QuestState.DoneThisCycle), tooltip, StringComparison.Ordinal);
        Assert.Equal(tooltip, StateNames.Tooltip(QuestState.DoneThisCycle, new QuestRecord { RepeatInterval = repeatInterval }));
    }

    [Theory]
    [InlineData(QuestState.Ready)]
    [InlineData(QuestState.Blocked)]
    [InlineData(QuestState.Completed)]
    public void Repeat_interval_changes_only_the_done_tooltip(QuestState state)
    {
        Assert.Same(StateNames.Tooltip(state), StateNames.Tooltip(state, StateNames.DailyInterval));
        Assert.Same(StateNames.Tooltip(state), StateNames.Tooltip(state, StateNames.WeeklyInterval));
    }

    [Fact]
    public void Compose_leaves_the_name_alone_without_a_subtitle()
    {
        Assert.Equal("Ready", StateNames.ComposeTooltip("Ready", string.Empty));
        Assert.Equal("Ready · x", StateNames.ComposeTooltip("Ready", "x"));
    }
}
