using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The display-name table every surface reads (feature plan v3 T23, docs/glossary.md): one name per state, the
/// done-repeatable name picked by the quest's reset, and the moon-phase names kept out of the labels.
/// </summary>
public class StateNamesTests
{
    [Theory]
    [InlineData(QuestState.Ready, "Ready")]
    [InlineData(QuestState.ReadyOnOtherJob, "Ready on another job")]
    [InlineData(QuestState.Accepted, "In journal")]
    [InlineData(QuestState.Blocked, "Blocked")]
    [InlineData(QuestState.DoneThisCycle, "Done this cycle")]
    [InlineData(QuestState.Completed, "Completed")]
    [InlineData(QuestState.Foreclosed, "Locked out")]
    [InlineData(QuestState.Unknown, "Not checked")]
    public void Name_is_the_glossary_word(QuestState state, string expected)
    {
        Assert.Equal(expected, StateNames.Name(state));
        Assert.Equal(expected, StateNames.Name(state, quest: null));
    }

    [Theory]
    [InlineData(StateNames.DailyInterval, "Done today")]
    [InlineData(StateNames.WeeklyInterval, "Done this week")]
    [InlineData((byte)0, "Done this cycle")]
    [InlineData((byte)7, "Done this cycle")]
    public void Done_repeatable_is_named_by_its_reset(byte repeatInterval, string expected)
    {
        Assert.Equal(expected, StateNames.DoneName(repeatInterval));
        Assert.Equal(expected, StateNames.Name(QuestState.DoneThisCycle, repeatInterval));
        Assert.Equal(expected, StateNames.Name(QuestState.DoneThisCycle, new QuestRecord { RepeatInterval = repeatInterval }));
    }

    [Theory]
    [InlineData(QuestState.Ready)]
    [InlineData(QuestState.Blocked)]
    [InlineData(QuestState.Completed)]
    [InlineData(QuestState.Foreclosed)]
    public void Repeat_interval_changes_nothing_but_the_done_state(QuestState state)
    {
        Assert.Equal(StateNames.Name(state), StateNames.Name(state, StateNames.DailyInterval));
        Assert.Equal(StateNames.Name(state), StateNames.Name(state, StateNames.WeeklyInterval));
    }

    [Fact]
    public void Every_state_has_a_distinct_name_and_a_glyph_subtitle()
    {
        var states = Enum.GetValues<QuestState>();
        var names = states.Select(StateNames.Name).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        foreach (var state in states)
        {
            Assert.False(string.IsNullOrWhiteSpace(StateNames.GlyphSubtitle(state)), state.ToString());
        }
    }

    [Theory]
    [InlineData(QuestState.Foreclosed)]
    [InlineData(QuestState.Unknown)]
    [InlineData(QuestState.Accepted)]
    public void Retired_enum_spellings_are_not_display_names(QuestState state)
    {
        Assert.NotEqual(state.ToString(), StateNames.Name(state));
    }

    [Fact]
    public void Poetic_names_stay_in_the_subtitles()
    {
        Assert.Equal("veiled", StateNames.GlyphSubtitle(QuestState.Unknown));
        Assert.Equal("eclipsed", StateNames.GlyphSubtitle(QuestState.Foreclosed));
        Assert.Equal("full moon", StateNames.GlyphSubtitle(QuestState.Completed));
        Assert.StartsWith("first quarter", StateNames.GlyphSubtitle(QuestState.Ready), StringComparison.Ordinal);
        Assert.StartsWith("new moon", StateNames.GlyphSubtitle(QuestState.Blocked), StringComparison.Ordinal);
        foreach (var state in Enum.GetValues<QuestState>())
        {
            Assert.NotEqual(StateNames.GlyphSubtitle(state), StateNames.Name(state));
        }
    }

    [Fact]
    public void Quick_view_names_use_the_new_words()
    {
        Assert.Equal("Unlocks", FilterNames.PresetName(Preset.FeatureQuests));
        Assert.Equal("My level", FilterNames.PresetName(Preset.LevelBand));
        Assert.Equal("Stalled", FilterNames.PresetName(Preset.Stalled));
    }
}
