using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Tests.Ipc;

/// <summary>
/// <see cref="QuestionableWiderCheck"/>, the wider Questionable cross-check (feature plan v5, 1.6.0): its unobtainable
/// answer against Tsukimichi's Locked out, its active-event quests against the festivals the game reports running, and
/// the "questionable more:" line of Report this quest.
/// </summary>
public class QuestionableWiderCheckTests
{
    private static readonly RequirementResult ForeclosureUnmet = new(new ForeclosureRequirement([65600], [65600]), false, string.Empty);
    private static readonly RequirementResult ExpansionUnmet = new(new ExpansionCapRequirement(5, 4), false, string.Empty);
    private static readonly RequirementResult PrevUnmet = new(new PreviousQuestsRequirement([65600], JoinKind.All, 0), false, string.Empty);
    private static readonly RequirementResult SeasonUnmet = new(new SeasonalRequirement(7, false), false, string.Empty);

    private static QuestEvaluation Eval(QuestState state, params RequirementResult[] requirements) =>
        new(state, requirements, requirements.FirstOrDefault(r => !r.Met), null, null);

    private static readonly QuestRecord EventQuest = new() { RowId = 0x10000 + 5000, Festival = 7 };
    private static readonly QuestRecord PlainQuest = new() { RowId = 0x10000 + 428 };

    [Fact]
    public void Locked_out_and_unobtainable_agree()
    {
        Assert.Equal(UnobtainableOutcome.Agrees, QuestionableWiderCheck.CompareUnobtainable(Eval(QuestState.Foreclosed, ForeclosureUnmet), true));
    }

    [Fact]
    public void Locked_out_while_Questionable_says_obtainable_disagrees()
    {
        Assert.Equal(UnobtainableOutcome.QuestionableObtainable, QuestionableWiderCheck.CompareUnobtainable(Eval(QuestState.Foreclosed, ForeclosureUnmet), false));
    }

    [Fact]
    public void Open_and_obtainable_agree()
    {
        Assert.Equal(UnobtainableOutcome.Agrees, QuestionableWiderCheck.CompareUnobtainable(Eval(QuestState.Ready), false));
        Assert.Equal(UnobtainableOutcome.Agrees, QuestionableWiderCheck.CompareUnobtainable(Eval(QuestState.Blocked, PrevUnmet), false));
    }

    [Fact]
    public void Open_while_Questionable_says_unobtainable_disagrees()
    {
        Assert.Equal(UnobtainableOutcome.QuestionableUnobtainable, QuestionableWiderCheck.CompareUnobtainable(Eval(QuestState.Ready), true));
        Assert.Equal(UnobtainableOutcome.QuestionableUnobtainable, QuestionableWiderCheck.CompareUnobtainable(Eval(QuestState.Blocked, PrevUnmet), true));
    }

    [Fact]
    public void An_expansion_the_account_lacks_agrees_with_unobtainable()
    {
        Assert.Equal(UnobtainableOutcome.Agrees, QuestionableWiderCheck.CompareUnobtainable(Eval(QuestState.Blocked, ExpansionUnmet), true));
    }

    [Theory]
    [InlineData(QuestState.Completed)]
    [InlineData(QuestState.DoneThisCycle)]
    [InlineData(QuestState.Accepted)]
    [InlineData(QuestState.Unknown)]
    public void Done_in_journal_or_unchecked_quests_are_not_compared(QuestState state)
    {
        Assert.Equal(UnobtainableOutcome.NotCompared, QuestionableWiderCheck.CompareUnobtainable(Eval(state), true));
    }

    [Fact]
    public void Out_of_season_no_answer_and_stored_characters_are_not_compared()
    {
        Assert.Equal(UnobtainableOutcome.NotCompared, QuestionableWiderCheck.CompareUnobtainable(Eval(QuestState.Blocked, SeasonUnmet), true));
        Assert.Equal(UnobtainableOutcome.NotCompared, QuestionableWiderCheck.CompareUnobtainable(Eval(QuestState.Ready), null));
        Assert.Equal(UnobtainableOutcome.NotCompared, QuestionableWiderCheck.CompareUnobtainable(Eval(QuestState.Ready), true, live: false));
        Assert.Equal(UnobtainableOutcome.NotCompared, QuestionableWiderCheck.CompareUnobtainable(null, true));
    }

    [Fact]
    public void An_event_quest_listed_while_its_festival_runs_agrees()
    {
        Assert.Equal(EventOutcome.Agrees, QuestionableWiderCheck.CompareEvent(EventQuest, festivalRunning: true, listed: true));
    }

    [Fact]
    public void An_event_quest_listed_while_its_festival_is_not_running_disagrees()
    {
        Assert.Equal(EventOutcome.QuestionableListsInactive, QuestionableWiderCheck.CompareEvent(EventQuest, festivalRunning: false, listed: true));
    }

    [Fact]
    public void A_running_event_quest_Questionable_does_not_list_is_noted_not_a_disagreement()
    {
        var outcome = QuestionableWiderCheck.CompareEvent(EventQuest, festivalRunning: true, listed: false);

        Assert.Equal(EventOutcome.NotListed, outcome);
        Assert.False(new QuestionableWider(null, null, false, null, UnobtainableOutcome.NotCompared, outcome).Disagrees);
    }

    [Fact]
    public void A_quest_of_no_festival_or_no_answer_is_not_compared()
    {
        Assert.Equal(EventOutcome.NotCompared, QuestionableWiderCheck.CompareEvent(PlainQuest, festivalRunning: false, listed: false));
        Assert.Equal(EventOutcome.NotCompared, QuestionableWiderCheck.CompareEvent(PlainQuest, festivalRunning: false, listed: true));
        Assert.Equal(EventOutcome.NotCompared, QuestionableWiderCheck.CompareEvent(EventQuest, festivalRunning: true, listed: null));
    }

    [Fact]
    public void Event_row_ids_keep_the_quests_and_drop_the_other_kinds()
    {
        var rows = QuestionableWiderCheck.EventRowIds(["5000", "A12", " 428 ", "U5"]);

        Assert.Equal(new HashSet<uint> { 0x10000 + 5000, 0x10000 + 428 }, rows);
        Assert.Empty(QuestionableWiderCheck.EventRowIds(null));
    }

    [Fact]
    public void The_diagnostic_text_lists_what_is_known()
    {
        var wider = new QuestionableWider(true, 3, true, false, UnobtainableOutcome.Agrees, EventOutcome.Agrees);

        Assert.Equal("path yes; list #3; unobtainable no, agrees; event listed, agrees", QuestionableWiderCheck.DiagnosticText(wider, QuestState.Ready));
    }

    [Fact]
    public void The_diagnostic_text_names_a_disagreement_with_Tsukimichis_state()
    {
        var wider = new QuestionableWider(false, null, true, true, UnobtainableOutcome.QuestionableUnobtainable, EventOutcome.QuestionableListsInactive);

        Assert.True(wider.Disagrees);
        Assert.Equal(
            "path no; not on list; unobtainable yes, disagrees (tsukimichi Ready); event listed, disagrees (festival not running)",
            QuestionableWiderCheck.DiagnosticText(wider, QuestState.Ready));
    }

    [Fact]
    public void The_fork_with_nothing_known_prints_nothing()
    {
        var wider = new QuestionableWider(null, null, false, null, UnobtainableOutcome.NotCompared, EventOutcome.NotCompared);

        Assert.Equal(string.Empty, QuestionableWiderCheck.DiagnosticText(wider, QuestState.Ready));
    }

    [Fact]
    public void Report_this_quest_prints_the_questionable_more_line_only_when_something_is_known()
    {
        var quest = new QuestRecord { RowId = 0x10000 + 428, Name = "Test" };
        var known = QuestDiagnostic.Compose(new DiagnosticInputs
        {
            Quest = quest,
            Evaluation = Eval(QuestState.Ready),
            QuestionableMore = new QuestionableWider(true, 2, true, null, UnobtainableOutcome.NotCompared, EventOutcome.NotCompared),
        });
        var unknown = QuestDiagnostic.Compose(new DiagnosticInputs
        {
            Quest = quest,
            QuestionableMore = new QuestionableWider(null, null, false, null, UnobtainableOutcome.NotCompared, EventOutcome.NotCompared),
        });

        Assert.Contains("questionable more: path yes; list #2\n", known, StringComparison.Ordinal);
        Assert.DoesNotContain("questionable more:", unknown, StringComparison.Ordinal);
    }
}
