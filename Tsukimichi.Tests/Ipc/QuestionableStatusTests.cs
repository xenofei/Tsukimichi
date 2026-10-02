using Tsukimichi.Core.Ipc;

namespace Tsukimichi.Tests.Ipc;

/// <summary>
/// <see cref="QuestionableStatus.From"/>, the live status behind "Questionable: running · &lt;quest&gt; · step s"
/// (feature plan v5, 1.6.0): a numeric quest id becomes a row, the prefixed kinds (A, S, U, N, C) are ignored, and the
/// step data counts only for the same quest.
/// </summary>
public class QuestionableStatusTests
{
    private const uint Row428 = 0x10000 + 428;

    [Fact]
    public void A_running_quest_with_its_step_data()
    {
        var status = QuestionableStatus.From(true, "428", "428", 3, 1, 132);

        Assert.True(status.Running);
        Assert.Equal(Row428, status.RowId);
        Assert.Equal((byte)3, status.Sequence);
        Assert.Equal(1, status.Step);
        Assert.Equal(132u, status.TerritoryId);
        Assert.Equal("428", status.QuestIdText);
    }

    [Theory]
    [InlineData("A12")]
    [InlineData("S3")]
    [InlineData("U5")]
    [InlineData("N1")]
    [InlineData("C7")]
    [InlineData("not a quest")]
    public void A_non_numeric_id_is_running_without_a_quest(string id)
    {
        var status = QuestionableStatus.From(true, id, id, 1, 0, 132);

        Assert.True(status.Running);
        Assert.Null(status.RowId);
        Assert.Null(status.Sequence);
        Assert.Null(status.Step);
        Assert.Equal(0u, status.TerritoryId);
        Assert.Equal(string.Empty, status.QuestIdText);
    }

    [Fact]
    public void Step_data_for_another_quest_is_not_shown()
    {
        var status = QuestionableStatus.From(true, "428", "1021", 3, 1, 132);

        Assert.Equal(Row428, status.RowId);
        Assert.Null(status.Sequence);
        Assert.Null(status.Step);
    }

    [Fact]
    public void Without_a_quest_id_the_step_datas_quest_is_taken()
    {
        var status = QuestionableStatus.From(true, null, "428", 255, 2, 0);

        Assert.Equal(Row428, status.RowId);
        Assert.Equal((byte)255, status.Sequence);
    }

    [Fact]
    public void A_non_numeric_current_id_is_not_replaced_by_the_step_data()
    {
        var status = QuestionableStatus.From(true, "A12", "428", 1, 0, 0);

        Assert.Null(status.RowId);
    }

    [Fact]
    public void Idle_is_not_running()
    {
        Assert.False(QuestionableStatus.Idle.Running);
        Assert.Null(QuestionableStatus.Idle.RowId);
    }
}
