using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Tests.Evaluation;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Diagnostics;

/// <summary>
/// "The game confirms it" (feature plan v7, C1): what the game's own offers say against Tsukimichi's states, in the
/// detail pane, <c>/tsuki why</c>, the Report block and the owner's report. Nothing here changes a state.
/// </summary>
public class GameOfferChecksTests
{
    private static readonly DateTime Now = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

    private static OfferSighting Seen(uint rowId, TimeSpan ago, OfferSource source = OfferSource.Marker) =>
        new(QuestRecord.ToQuestId(rowId), Now - ago - TimeSpan.FromHours(2), Now - ago, source);

    private static QuestEvaluation State(QuestState state, RequirementResult? next = null) => new(state, next is null ? [] : [next], next, null, null);

    [Fact]
    public void Ready_agrees_and_Not_checked_is_confirmed()
    {
        var quest = Quest(A);
        var sighting = Seen(A, TimeSpan.FromDays(3));

        Assert.Equal(GameOfferVerdict.Agrees, GameOfferChecks.Judge(quest, State(QuestState.Ready), sighting, 0, Now).Verdict);
        Assert.Equal(GameOfferVerdict.Agrees, GameOfferChecks.Judge(quest, State(QuestState.ReadyOnOtherJob), sighting, 0, Now).Verdict);
        var confirms = GameOfferChecks.Judge(quest, State(QuestState.Unknown), sighting, 0, Now);
        Assert.Equal(GameOfferVerdict.Confirms, confirms.Verdict);
        Assert.Same(sighting, confirms.Sighting);
        Assert.Equal(GameOfferCheck.Nothing, GameOfferChecks.Judge(quest, null, sighting, 0, Now));
    }

    [Fact]
    public void Blocked_disagrees_only_while_the_sighting_is_fresh_and_never_for_a_seasonal_quest()
    {
        var quest = Quest(A);
        var blocked = State(QuestState.Blocked);

        Assert.Equal(GameOfferVerdict.Disagrees, GameOfferChecks.Judge(quest, blocked, Seen(A, TimeSpan.FromMinutes(5)), 0, Now).Verdict);
        Assert.Equal(GameOfferVerdict.Disagrees, GameOfferChecks.Judge(quest, State(QuestState.Foreclosed), Seen(A, TimeSpan.Zero), 0, Now).Verdict);

        // An older sighting may predate a real change (another Grand Company): printed, not called a disagreement.
        var stale = GameOfferChecks.Judge(quest, blocked, Seen(A, GameOfferChecks.Fresh + TimeSpan.FromMinutes(1)), 0, Now);
        Assert.Equal(GameOfferVerdict.None, stale.Verdict);
        Assert.NotNull(stale.Sighting);

        var seasonal = quest with { Festival = 4 };
        Assert.Equal(GameOfferVerdict.None, GameOfferChecks.Judge(seasonal, blocked, Seen(A, TimeSpan.Zero), 0, Now).Verdict);
    }

    [Fact]
    public void A_ready_quest_without_its_marker_is_unseen_after_three_polls_and_done_quests_say_nothing()
    {
        var quest = Quest(A);
        Assert.Equal(GameOfferVerdict.None, GameOfferChecks.Judge(quest, State(QuestState.Ready), null, GameOfferChecks.UnseenPolls - 1, Now).Verdict);
        Assert.Equal(GameOfferVerdict.Unseen, GameOfferChecks.Judge(quest, State(QuestState.Ready), null, GameOfferChecks.UnseenPolls, Now).Verdict);
        Assert.Equal(GameOfferVerdict.None, GameOfferChecks.Judge(quest, State(QuestState.Blocked), null, 9, Now).Verdict);
        Assert.Equal(GameOfferVerdict.None, GameOfferChecks.Judge(quest, State(QuestState.Completed), Seen(A, TimeSpan.Zero), 0, Now).Verdict);
    }

    [Fact]
    public void The_report_block_line_says_where_when_and_the_verdict()
    {
        var sighting = new OfferSighting(QuestRecord.ToQuestId(A), Now.AddHours(-1), Now, OfferSource.Marker | OfferSource.Offer);
        var blocked = State(QuestState.Blocked);
        var check = GameOfferChecks.Judge(Quest(A), blocked, sighting, 0, Now);

        Assert.Equal(
            "offered on the map and in the offer window 2026-10-03T11:00:00Z, last 2026-10-03T12:00:00Z; disagrees: tsukimichi Blocked",
            GameOfferChecks.DiagnosticText(check, blocked));
        Assert.Equal(
            "offered on the map 2026-10-03T12:00:00Z; agrees",
            GameOfferChecks.DiagnosticText(new GameOfferCheck(GameOfferVerdict.Agrees, new OfferSighting(1, Now, Now, OfferSource.Marker), 0), State(QuestState.Ready)));
        Assert.Equal(
            "no marker in the giver's zone over 4 polls; tsukimichi Ready",
            GameOfferChecks.DiagnosticText(new GameOfferCheck(GameOfferVerdict.Unseen, null, 4), State(QuestState.Ready)));
        Assert.Null(GameOfferChecks.DiagnosticText(GameOfferCheck.Nothing, null));
    }

    [Fact]
    public void The_detail_pane_speaks_only_for_a_confirmation_or_a_disagreement()
    {
        var sighting = Seen(A, TimeSpan.FromMinutes(5));
        Assert.Equal(
            "The game offered this quest 5 min ago, but Tsukimichi reads it as not available. Report this quest so it can be fixed.",
            GameOfferChecks.DetailLine(new GameOfferCheck(GameOfferVerdict.Disagrees, sighting, 0), Now));
        Assert.Equal(
            "The game offers this quest (seen 5 min ago), so the gate Tsukimichi cannot read is met.",
            GameOfferChecks.DetailLine(new GameOfferCheck(GameOfferVerdict.Confirms, sighting, 0), Now));
        Assert.Null(GameOfferChecks.DetailLine(new GameOfferCheck(GameOfferVerdict.Agrees, sighting, 0), Now));
        Assert.Null(GameOfferChecks.DetailLine(new GameOfferCheck(GameOfferVerdict.Unseen, null, 5), Now));

        Assert.Equal("Game: shown on the map 5 min ago.", GameOfferChecks.WhyLine(new GameOfferCheck(GameOfferVerdict.Agrees, sighting, 0), Now));
        Assert.Equal(
            "Game: offered to you 5 min ago. Tsukimichi disagrees: please report this quest.",
            GameOfferChecks.WhyLine(new GameOfferCheck(GameOfferVerdict.Disagrees, Seen(A, TimeSpan.FromMinutes(5), OfferSource.Offer), 0), Now));
        Assert.Null(GameOfferChecks.WhyLine(GameOfferCheck.Nothing, Now));
    }

    [Fact]
    public void The_owner_report_lists_disagreements_then_confirmations_then_unseen_quests()
    {
        var catalog = Catalog(Quest(A, "Alpha"), Quest(B, "Beta"), Quest(C, "Gamma"), Quest(D, "Delta"));
        var states = new Dictionary<uint, QuestEvaluation>
        {
            [A] = State(QuestState.Ready),
            [B] = State(QuestState.Unknown),
            [C] = State(QuestState.Blocked),
            [D] = State(QuestState.Ready),
        };
        var sightings = new Dictionary<ushort, OfferSighting>
        {
            [QuestRecord.ToQuestId(A)] = Seen(A, TimeSpan.Zero),
            [QuestRecord.ToQuestId(B)] = Seen(B, TimeSpan.Zero),
            [QuestRecord.ToQuestId(C)] = Seen(C, TimeSpan.Zero),
        };
        var unseen = new Dictionary<ushort, int> { [QuestRecord.ToQuestId(D)] = 3, [QuestRecord.ToQuestId(A)] = 9 };

        var rows = GameOfferChecks.Disagreements(catalog, states, sightings, unseen, Now);

        Assert.Equal([C, B, D], rows.Select(r => r.Quest.RowId));
        Assert.Equal([GameOfferVerdict.Disagrees, GameOfferVerdict.Confirms, GameOfferVerdict.Unseen], rows.Select(r => r.Check.Verdict));

        var report = GameOfferChecks.Report(rows, BlockerNames.Default, states);
        var lines = report.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("65602 \"Gamma\": Blocked | game offered on the map ", lines[0], StringComparison.Ordinal);
        Assert.EndsWith("; disagrees: tsukimichi Blocked", lines[0], StringComparison.Ordinal);
        Assert.EndsWith("| game no marker in the giver's zone over 3 polls; tsukimichi Ready", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void The_diagnostic_block_carries_the_in_game_line_only_when_there_is_one()
    {
        var quest = Quest(A, "Alpha");
        var evaluation = State(QuestState.Blocked);
        var inputs = new DiagnosticInputs { Quest = quest, Evaluation = evaluation };
        Assert.DoesNotContain("in game:", QuestDiagnostic.Compose(inputs), StringComparison.Ordinal);

        var check = GameOfferChecks.Judge(quest, evaluation, Seen(A, TimeSpan.Zero), 0, Now);
        var block = QuestDiagnostic.Compose(inputs with { GameOffer = check });
        Assert.Contains("\nin game: offered on the map ", block, StringComparison.Ordinal);
        Assert.Contains("; disagrees: tsukimichi Blocked\n", block, StringComparison.Ordinal);
    }
}
