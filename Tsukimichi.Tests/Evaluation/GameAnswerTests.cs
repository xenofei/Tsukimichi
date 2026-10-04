using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Evaluation;

/// <summary>
/// The game's answer over Tsukimichi's (feature plan v7 C1; spec-1.19 "C1. The game confirms it"): a Not checked quest
/// the game offered reads Ready, and the player's per-character "Go with the game" turns a Blocked one Ready until it is
/// taken back, with Tsukimichi's own answer kept so the disagreement is still reported.
/// </summary>
public class GameAnswerTests
{
    private const ulong Main = 1;
    private const ulong Alt = 2;
    private static readonly DateTime Now = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Blocked until A is done.</summary>
    private static readonly QuestRecord Blocked = Quest(Target, "Sleepless in Ishgard") with { PreviousQuests = new Prereq([A], JoinKind.All) };

    /// <summary>Not checked: a house nobody read.</summary>
    private static readonly QuestRecord NotChecked = Quest(B, "Knocking") with { HouseRequired = true };

    private static QuestCatalog Quests() => Catalog(Quest(A, "The Narwhal Beckons"), Blocked, NotChecked, Quest(C));

    private static EvalContext Answers(Func<ulong, uint, bool>? offered = null, Func<ulong, uint, bool>? withGame = null) => new()
    {
        GameOffered = offered ?? ((_, _) => false),
        GoWithGame = withGame ?? ((_, _) => false),
    };

    private static QuestEvaluation Resolve(QuestRecord quest, EvalContext ctx, ulong contentId = Main, CharacterSnapshot? snapshot = null) =>
        StateResolver.Resolve(quest, (snapshot ?? Snapshot()) with { ContentId = contentId }, Quests(), ctx);

    [Fact]
    public void A_not_checked_quest_the_game_offered_reads_ready_by_the_game()
    {
        var offered = Answers(offered: (id, row) => id == Main && row == B);

        var ready = Resolve(NotChecked, offered);
        Assert.Equal((QuestState.Ready, GameAnswer.Offered), (ready.State, ready.ByGame));
        Assert.Equal(QuestState.Unknown, ready.Own!.State);
        Assert.Null(ready.NextStep);

        // Per character: the alt the game never showed it to keeps Not checked.
        var alt = Resolve(NotChecked, offered, Alt);
        Assert.Equal((QuestState.Unknown, GameAnswer.None, null), (alt.State, alt.ByGame, alt.Own));
    }

    [Fact]
    public void An_offer_alone_never_overrules_a_blocked_quest()
    {
        var blocked = Resolve(Blocked, Answers(offered: (_, _) => true));
        Assert.Equal((QuestState.Blocked, GameAnswer.None), (blocked.State, blocked.ByGame));
    }

    [Fact]
    public void Go_with_the_game_turns_a_blocked_quest_ready_on_that_character_only_and_keeps_tsukimichi_s_answer()
    {
        var chosen = Answers(withGame: (id, row) => id == Main && row == Target);

        var ready = Resolve(Blocked, chosen);
        Assert.Equal((QuestState.Ready, GameAnswer.Override), (ready.State, ready.ByGame));
        Assert.Equal(QuestState.Blocked, ready.Own!.State);
        Assert.Equal(RequirementKind.PreviousQuests, ready.Own.NextStep!.Req.Kind);

        Assert.Equal(QuestState.Blocked, Resolve(Blocked, chosen, Alt).State);

        // Taken back ("Use Tsukimichi's answer", or Undo): Tsukimichi's own answer again.
        Assert.Equal((QuestState.Blocked, GameAnswer.None), (Resolve(Blocked, Answers()).State, Resolve(Blocked, Answers()).ByGame));
    }

    [Fact]
    public void Go_with_the_game_never_touches_a_quest_done_in_the_journal_or_repeatable()
    {
        var always = Answers(offered: (_, _) => true, withGame: (_, _) => true);

        Assert.Equal(QuestState.Completed, Resolve(Blocked, always, snapshot: Snapshot(Target)).State);
        Assert.Equal(QuestState.Accepted, Resolve(Blocked, always, snapshot: Snapshot() with { Accepted = [Accepted(Target)] }).State);
        var repeatable = Blocked with { IsRepeatable = true, RepeatInterval = 1 };
        Assert.Equal(QuestState.Blocked, StateResolver.Resolve(repeatable, Snapshot(), Catalog(Quest(A), repeatable), always).State);
    }

    [Fact]
    public void Resolving_everything_honours_the_game_s_answers()
    {
        var ctx = Answers(offered: (_, row) => row == B, withGame: (_, row) => row == Target);
        var all = StateResolver.ResolveAll(Quests(), Snapshot(), ctx);

        Assert.Equal(GameAnswer.Override, all[Target].ByGame);
        Assert.Equal(GameAnswer.Offered, all[B].ByGame);
        Assert.Equal(GameAnswer.None, all[C].ByGame);
    }

    [Fact]
    public void Under_the_game_s_answer_the_checks_still_tell_the_disagreement_however_old()
    {
        var old = new OfferSighting(QuestRecord.ToQuestId(Target), Now.AddDays(-3), Now.AddDays(-2), OfferSource.Marker);
        var overridden = Resolve(Blocked, Answers(withGame: (_, _) => true));

        var check = GameOfferChecks.Judge(Blocked, overridden, old, 0, Now);
        Assert.Equal(GameOfferVerdict.Disagrees, check.Verdict);
        Assert.EndsWith("disagrees: tsukimichi Blocked; the player went with the game", GameOfferChecks.DiagnosticText(check, overridden), StringComparison.Ordinal);

        var confirmed = Resolve(NotChecked, Answers(offered: (_, _) => true));
        Assert.Equal(GameOfferVerdict.Confirms, GameOfferChecks.Judge(NotChecked, confirmed, old with { QuestId = QuestRecord.ToQuestId(B) }, 0, Now).Verdict);
    }

    [Fact]
    public void The_disagreement_names_the_quest_tsukimichi_expects_first()
    {
        var catalog = Quests();
        var names = new BlockerNames { Catalog = catalog };
        var blocked = StateResolver.Resolve(Blocked, Snapshot(), catalog, EvalContext.Default);

        var (first, reason) = GameOfferChecks.Expectation(blocked, Blocked, names, null);
        Assert.Equal(A, first);
        Assert.Equal("after MSQ: The Narwhal Beckons", reason);

        // Under "Go with the game" it is still Tsukimichi's own answer that is told.
        var overridden = StateResolver.Resolve(Blocked, Snapshot(), catalog, Answers(withGame: (_, _) => true));
        Assert.Equal((A, reason), GameOfferChecks.Expectation(overridden, Blocked, names, null));

        // A blocker that isn't a quest is told in words alone.
        var level = Quest(D) with { Level = 90 };
        var low = StateResolver.Resolve(level, Snapshot(), Catalog(level), EvalContext.Default);
        var (none, words) = GameOfferChecks.Expectation(low, level, names, null);
        Assert.Null(none);
        Assert.NotEmpty(words);
    }
}
