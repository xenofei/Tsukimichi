using System.Text;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Tests.Ipc;

/// <summary>
/// <see cref="QuestionableList"/>, behind "Send to Questionable" (feature plan v5, 1.6.0): which quests are sent and in
/// what order, the <c>qst:priority:</c> format Questionable's <c>ImportQuestPriority</c> reads, decoding its
/// <c>ExportQuestPriority</c>, and the "Sent 14 of 17 (3 have no Questionable path)" count read back from it.
/// </summary>
public class QuestionableListTests
{
    private const uint Row428 = 0x10000 + 428;
    private const uint Row1021 = 0x10000 + 1021;
    private const uint Row2000 = 0x10000 + 2000;
    private const uint Row3000 = 0x10000 + 3000;
    private const uint Row4000 = 0x10000 + 4000;
    private const uint Row5000 = 0x10000 + 5000;

    private static QuestEvaluation Eval(QuestState state) => new(state, [], null, null, null);

    private static IReadOnlyDictionary<uint, QuestEvaluation> States(params (uint RowId, QuestState State)[] states) =>
        states.ToDictionary(static s => s.RowId, static s => Eval(s.State));

    [Fact]
    public void A_plan_sends_open_quests_as_low_16_bit_ids_in_the_given_order()
    {
        var states = States((Row1021, QuestState.Ready), (Row428, QuestState.Blocked), (Row2000, QuestState.ReadyOnOtherJob));

        var plan = QuestionableList.Plan([Row1021, Row428, Row2000], states);

        Assert.Equal(["1021", "428", "2000"], plan.Ids);
        Assert.Equal([Row1021, Row428, Row2000], plan.RowIds);
        Assert.Equal(3, plan.Count);
        Assert.Equal(0, plan.Skipped);
    }

    [Fact]
    public void A_plan_leaves_out_done_in_journal_and_locked_out_quests_and_counts_each()
    {
        var states = States(
            (Row428, QuestState.Completed),
            (Row1021, QuestState.DoneThisCycle),
            (Row2000, QuestState.Accepted),
            (Row3000, QuestState.Foreclosed),
            (Row4000, QuestState.Ready));

        var plan = QuestionableList.Plan([Row428, Row1021, Row2000, Row3000, Row4000, Row5000], states);

        // Row5000 has no evaluation (not checked yet): it is sent.
        Assert.Equal(["4000", "5000"], plan.Ids);
        Assert.Equal(2, plan.Done);
        Assert.Equal(1, plan.InJournal);
        Assert.Equal(1, plan.LockedOut);
        Assert.Equal(0, plan.NoId);
        Assert.Equal(4, plan.Skipped);
    }

    [Fact]
    public void A_plan_keeps_the_first_of_a_repeated_quest_and_skips_rows_without_a_Questionable_id()
    {
        var states = States((Row428, QuestState.Ready), (Row1021, QuestState.Ready));

        // 42 is not a Quest row; 0x10000 + 4081 is one Tsukimichi never asks Questionable about.
        var plan = QuestionableList.Plan([Row1021, 42, Row428, Row1021, 0x10000 + 4081], states);

        Assert.Equal(["1021", "428"], plan.Ids);
        Assert.Equal(2, plan.NoId);
    }

    [Fact]
    public void Encode_writes_the_prefix_and_the_base64_of_the_ids_joined_by_semicolons()
    {
        var encoded = QuestionableList.Encode(["428", "1021"]);

        Assert.Equal("qst:priority:" + Convert.ToBase64String(Encoding.UTF8.GetBytes("428;1021")), encoded);
        Assert.Equal("qst:priority:NDI4OzEwMjE=", encoded);
    }

    [Fact]
    public void Decode_reads_back_what_Encode_wrote_and_Questionables_other_kinds()
    {
        Assert.Equal(["428", "1021"], QuestionableList.Decode(QuestionableList.Encode(["428", "1021"])));
        var mixed = "qst:priority:" + Convert.ToBase64String(Encoding.UTF8.GetBytes("428;A12;U5;1021"));
        Assert.Equal(["428", "A12", "U5", "1021"], QuestionableList.Decode(mixed));
        var legacy = "qst:v1:" + Convert.ToBase64String(Encoding.UTF8.GetBytes("7"));
        Assert.Equal(["7"], QuestionableList.Decode(legacy));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("qst:priority:")]
    [InlineData("qst:priority:@@not base64@@")]
    [InlineData("428;1021")]
    public void Decode_reads_empty_or_malformed_text_as_an_empty_list(string? text)
    {
        Assert.Empty(QuestionableList.Decode(text));
    }

    [Theory]
    [InlineData("428", Row428)]
    [InlineData("1", 0x10001u)]
    [InlineData("65535", 0x1FFFFu)]
    public void RowIdOf_reads_a_bare_number_as_a_quest(string id, uint rowId)
    {
        Assert.Equal(rowId, QuestionableList.RowIdOf(id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("A12")]
    [InlineData("S3")]
    [InlineData("U5")]
    [InlineData("N1")]
    [InlineData("C7")]
    [InlineData("-4")]
    [InlineData("+4")]
    [InlineData(" 4")]
    public void RowIdOf_ignores_everything_but_a_quest(string? id)
    {
        Assert.Null(QuestionableList.RowIdOf(id));
    }

    [Fact]
    public void Positions_number_every_entry_and_map_only_the_quests()
    {
        var positions = QuestionableList.Positions(["A12", "428", "U5", "1021"]);

        Assert.Equal(2, positions.Count);
        Assert.Equal(2, positions[Row428]);
        Assert.Equal(4, positions[Row1021]);
    }

    [Fact]
    public void Verify_counts_the_quests_on_the_list_afterwards_and_those_already_there()
    {
        var plan = QuestionableList.Plan([Row428, Row1021, Row2000, Row3000], new Dictionary<uint, QuestEvaluation>());

        // 2000 has no path (dropped); 428 was on the list before.
        var result = QuestionableList.Verify(plan, ["428"], ["428", "1021", "3000"]);

        Assert.True(result.Verified);
        Assert.Equal(4, result.Sent);
        Assert.Equal(3, result.OnList);
        Assert.Equal(1, result.NoPath);
        Assert.Equal(1, result.AlreadyThere);
        Assert.Equal(3, result.Positions[Row3000]);
    }

    [Fact]
    public void Verify_without_a_read_back_counts_every_quest_as_sent()
    {
        var plan = QuestionableList.Plan([Row428, Row1021], new Dictionary<uint, QuestEvaluation>());

        var result = QuestionableList.Verify(plan, null, null);

        Assert.False(result.Verified);
        Assert.Equal(2, result.OnList);
        Assert.Equal(0, result.NoPath);
        Assert.Empty(result.Positions);
    }

    [Fact]
    public void Verify_after_a_replace_counts_nothing_as_already_there()
    {
        var plan = QuestionableList.Plan([Row428], new Dictionary<uint, QuestEvaluation>());

        var result = QuestionableList.Verify(plan, null, ["428"]);

        Assert.Equal(1, result.OnList);
        Assert.Equal(0, result.AlreadyThere);
    }
}
