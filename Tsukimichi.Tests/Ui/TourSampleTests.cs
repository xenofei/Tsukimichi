using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Ui;

/// <summary>The quest the tour's Read chapter selects (1.7.0): a Blocked one, so its requirements have something to show.</summary>
public class TourSampleTests
{
    private static QuestRow Row(uint rowId, QuestState state) => new(Fixture.Quest(rowId), state, string.Empty);

    [Fact]
    public void A_blocked_next_main_scenario_quest_comes_first()
    {
        var msq = Fixture.Quest(Fixture.Target);
        var rows = new[] { Row(Fixture.A, QuestState.Blocked), Row(Fixture.B, QuestState.Ready) };
        Assert.Equal(Fixture.Target, TourSample.Choose(msq, QuestState.Blocked, rows));
    }

    [Theory]
    [InlineData(QuestState.Ready)]
    [InlineData(QuestState.Accepted)]
    [InlineData(null)]
    public void Otherwise_the_first_blocked_row(QuestState? msqState)
    {
        var msq = Fixture.Quest(Fixture.Target);
        var rows = new[] { Row(Fixture.A, QuestState.Completed), Row(Fixture.B, QuestState.Blocked), Row(Fixture.C, QuestState.Blocked) };
        Assert.Equal(Fixture.B, TourSample.Choose(msq, msqState, rows));
    }

    [Fact]
    public void Without_a_blocked_row_the_first_row()
    {
        var rows = new[] { Row(Fixture.A, QuestState.Ready), Row(Fixture.B, QuestState.Completed) };
        Assert.Equal(Fixture.A, TourSample.Choose(Fixture.Quest(Fixture.Target), QuestState.Ready, rows));
        Assert.Equal(Fixture.A, TourSample.Choose(null, null, rows));
    }

    [Fact]
    public void An_empty_table_falls_back_to_the_next_main_scenario_quest_or_nothing()
    {
        Assert.Equal(Fixture.Target, TourSample.Choose(Fixture.Quest(Fixture.Target), QuestState.Ready, []));
        Assert.Null(TourSample.Choose(null, null, []));
    }
}
