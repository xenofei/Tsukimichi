using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Tests.Ipc;

/// <summary>
/// <see cref="QuestionableCrossCheck"/>, the comparison behind the detail pane's "Questionable agrees" line and the
/// diagnostic block's "questionable:" line (V2-17): agreement, disagreement both ways, Questionable not answering, and
/// the answers that are not compared.
/// </summary>
public class QuestionableCrossCheckTests
{
    private static readonly RequirementResult LevelMet = new(new LevelRequirement(10, 50), true, string.Empty);
    private static readonly RequirementResult LevelUnmet = new(new LevelRequirement(60, 50), false, string.Empty);
    private static readonly RequirementResult PrevUnmet = new(new PreviousQuestsRequirement([65600], JoinKind.All, 0), false, string.Empty);

    private static QuestEvaluation Eval(QuestState state, params RequirementResult[] requirements) =>
        new(state, requirements, requirements.FirstOrDefault(r => !r.Met), null, null);

    private static readonly QuestEvaluation Ready = Eval(QuestState.Ready, LevelMet);
    private static readonly QuestEvaluation BlockedByPrevious = Eval(QuestState.Blocked, LevelMet, PrevUnmet);
    private static readonly QuestEvaluation BlockedByLevel = Eval(QuestState.Blocked, LevelUnmet);

    private static readonly QuestionableAnswer Open = new(false, string.Empty);
    private static readonly QuestionableAnswer LockedPrev = new(true, "Prev quest (1),Aetheryte locked: Ul'dah");
    private static readonly QuestionableAnswer NoPath = new(true, string.Empty);

    [Fact]
    public void Ready_and_not_locked_agree()
    {
        var result = QuestionableCrossCheck.Compare(Ready, Open);

        Assert.Equal(CrossCheckOutcome.Agrees, result.Outcome);
        Assert.False(result.Disagrees);
        Assert.False(result.LevelAside);
        Assert.Equal("agrees; not locked", QuestionableCrossCheck.DiagnosticText(result));
    }

    [Fact]
    public void Blocked_by_a_previous_quest_and_locked_agree()
    {
        var result = QuestionableCrossCheck.Compare(BlockedByPrevious, LockedPrev);

        Assert.Equal(CrossCheckOutcome.Agrees, result.Outcome);
        Assert.Equal("agrees; locked: Prev quest (1), Aetheryte locked: Ul'dah", QuestionableCrossCheck.DiagnosticText(result));
    }

    [Fact]
    public void Ready_while_Questionable_says_locked_disagrees_with_its_reason()
    {
        var result = QuestionableCrossCheck.Compare(Ready, LockedPrev);

        Assert.Equal(CrossCheckOutcome.QuestionableLocked, result.Outcome);
        Assert.True(result.Disagrees);
        Assert.Equal("Prev quest (1), Aetheryte locked: Ul'dah", result.Answer!.ReasonText);
        Assert.Equal("disagrees; locked: Prev quest (1), Aetheryte locked: Ul'dah; tsukimichi Ready", QuestionableCrossCheck.DiagnosticText(result));
    }

    [Fact]
    public void Blocked_while_Questionable_says_not_locked_disagrees()
    {
        var result = QuestionableCrossCheck.Compare(BlockedByPrevious, Open);

        Assert.Equal(CrossCheckOutcome.QuestionableOpen, result.Outcome);
        Assert.True(result.Disagrees);
        Assert.Equal("disagrees; not locked; tsukimichi Blocked", QuestionableCrossCheck.DiagnosticText(result));
    }

    [Fact]
    public void Questionable_not_answering_is_unavailable()
    {
        var result = QuestionableCrossCheck.Compare(Ready, null);

        Assert.Equal(CrossCheckOutcome.Unavailable, result.Outcome);
        Assert.False(result.Disagrees);
        Assert.Null(result.Answer);
        Assert.Equal("loaded, no answer from its IPC", QuestionableCrossCheck.DiagnosticText(result));
    }

    [Fact]
    public void Locked_without_a_reason_is_no_answer_not_a_disagreement()
    {
        // IsQuestLockedReason answers (true, "") for a quest Questionable has no path for.
        var result = QuestionableCrossCheck.Compare(Ready, NoPath);

        Assert.Equal(CrossCheckOutcome.NoAnswer, result.Outcome);
        Assert.False(result.Disagrees);
        Assert.Equal("no answer; locked, no reason (no path for this quest)", QuestionableCrossCheck.DiagnosticText(result));
    }

    [Fact]
    public void The_reasonless_gate_compares_only_a_not_locked_answer()
    {
        // A Questionable with IsQuestLocked alone: locked may mean "no path", not locked is an answer.
        var locked = QuestionableCrossCheck.Compare(Ready, new QuestionableAnswer(true, null));
        var open = QuestionableCrossCheck.Compare(BlockedByPrevious, new QuestionableAnswer(false, null));

        Assert.Equal(CrossCheckOutcome.NoAnswer, locked.Outcome);
        Assert.Equal("no answer; locked (reason gate absent)", QuestionableCrossCheck.DiagnosticText(locked));
        Assert.Equal(CrossCheckOutcome.QuestionableOpen, open.Outcome);
    }

    [Fact]
    public void Blocked_by_level_alone_compares_as_open()
    {
        // Questionable's lock does not check the level of an ordinary quest.
        var result = QuestionableCrossCheck.Compare(BlockedByLevel, Open);

        Assert.Equal(CrossCheckOutcome.Agrees, result.Outcome);
        Assert.True(result.LevelAside);
        Assert.Equal("agrees (level and job aside); not locked", QuestionableCrossCheck.DiagnosticText(result));
    }

    [Fact]
    public void Questionables_low_level_reason_agrees_with_a_level_gate()
    {
        var result = QuestionableCrossCheck.Compare(BlockedByLevel, new QuestionableAnswer(true, "Low level (GLA)"));

        Assert.Equal(CrossCheckOutcome.Agrees, result.Outcome);
    }

    [Theory]
    [InlineData(QuestState.Accepted)]
    [InlineData(QuestState.Completed)]
    [InlineData(QuestState.DoneThisCycle)]
    [InlineData(QuestState.Foreclosed)]
    [InlineData(QuestState.Unknown)]
    public void States_Questionables_lock_does_not_speak_to_are_not_compared(QuestState state)
    {
        var result = QuestionableCrossCheck.Compare(Eval(state), LockedPrev);

        Assert.Equal(CrossCheckOutcome.NotCompared, result.Outcome);
        Assert.False(result.Disagrees);
    }

    [Fact]
    public void Out_of_season_and_missing_evaluations_are_not_compared()
    {
        var outOfSeason = Eval(QuestState.Blocked, new RequirementResult(new SeasonalRequirement(39, false), false, string.Empty));

        Assert.Equal(CrossCheckOutcome.NotCompared, QuestionableCrossCheck.Compare(outOfSeason, Open).Outcome);
        Assert.Equal(CrossCheckOutcome.NotCompared, QuestionableCrossCheck.Compare(null, Open).Outcome);
    }

    [Fact]
    public void A_stored_character_is_not_compared()
    {
        var result = QuestionableCrossCheck.Compare(Ready, LockedPrev, live: false);
        var unasked = QuestionableCrossCheck.Compare(Ready, null, live: false);

        Assert.Equal(CrossCheckOutcome.OtherCharacter, result.Outcome);
        Assert.False(result.Disagrees);
        Assert.Equal(CrossCheckOutcome.OtherCharacter, unasked.Outcome);
        Assert.Equal("not compared (viewing a stored character)", QuestionableCrossCheck.DiagnosticText(unasked));
    }

    [Theory]
    [InlineData(65536u, "0")]
    [InlineData(65964u, "428")]
    [InlineData(70000u, "4464")]
    public void Questionable_ids_are_the_row_ids_low_16_bits(uint rowId, string expected)
    {
        Assert.Equal(expected, QuestionableCrossCheck.QuestionableId(rowId));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(65535u)]
    [InlineData(131072u)]
    [InlineData(65536u + 4081u)]
    [InlineData(65536u + 2387u)]
    public void Rows_outside_the_quest_sheet_and_side_effect_quests_have_no_Questionable_id(uint rowId)
    {
        Assert.Null(QuestionableCrossCheck.QuestionableId(rowId));
    }
}
