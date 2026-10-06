using Tsukimichi.Core.Moonfall;
using static Tsukimichi.Tests.Moonfall.MoonfallTestKit;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>Challenges (plan v9 G7) [R §6 l.126, l.129–130]: the format, the starter set, the runs; and the Ace scores [R §3 l.93].</summary>
public sealed class MoonfallChallengeTests
{
    private static readonly double[] Angles = [-40, 10, 33, -12, 55, -70, 0, 21, -28, 44];

    private static string One(string body) => $$"""{ "format": "moonfall-challenges", "version": 1, "challenges": [ {{body}} ] }""";

    private static MoonfallChallenge Make(MoonfallChallengeKind kind, string[] levels, long target = 0, int balls = 10, int oranges = 25) =>
        new("t", "Test", string.Empty, kind, levels, target, balls, oranges, MoonfallCompanion.None, MoonfallCompanion.Raubahn, MoonfallAiDifficulty.Novice);

    [Fact]
    public void The_starter_set_is_twelve_challenges_of_every_kind_on_Adventures_levels()
    {
        var load = MoonfallChallenges.LoadBuiltIn();
        Assert.True(load.Ok, string.Join("; ", load.Errors));
        Assert.Equal(12, load.Challenges.Count);
        Assert.Equal(load.Challenges.Count, load.Challenges.Select(static c => c.Id).Distinct().Count());
        foreach (var kind in Enum.GetValues<MoonfallChallengeKind>())
        {
            Assert.Contains(load.Challenges, c => c.Kind == kind);
        }

        Assert.Contains(load.Challenges, static c => c.Balls < MoonfallRules.BallsPerLevel);
        Assert.Contains(load.Challenges, static c => c.Oranges == 35);
        Assert.Contains(load.Challenges, static c => c.Oranges == 45);
        Assert.Contains(load.Challenges, static c => c.LevelIds.Count > 1);
        Assert.All(load.Challenges.SelectMany(static c => c.LevelIds), static id => Assert.True(MoonfallStages.TryPlace(id, out _), id));
        Assert.All(load.Challenges.Where(static c => c.Kind == MoonfallChallengeKind.Duel), static c => Assert.NotEqual(MoonfallCompanion.None, c.Opponent));
    }

    [Theory]
    [InlineData("""{ "id": "a", "name": "A", "kind": "score", "levels": ["base-01"] }""", "target")]
    [InlineData("""{ "id": "a", "name": "A", "kind": "sprint", "levels": ["base-01"] }""", "kind")]
    [InlineData("""{ "id": "a", "name": "A", "kind": "win", "levels": [] }""", "levels")]
    [InlineData("""{ "id": "a", "name": "A", "kind": "win", "levels": ["a","b","c","d","e","f","g"] }""", "levels")]
    [InlineData("""{ "id": "a", "name": "A", "kind": "win", "levels": ["base-01"], "oranges": 50 }""", "oranges")]
    [InlineData("""{ "id": "a", "name": "A", "kind": "win", "levels": ["base-01"], "balls": 0 }""", "balls")]
    [InlineData("""{ "id": "a", "name": "A", "kind": "duel", "levels": ["base-01"] }""", "opponent")]
    [InlineData("""{ "id": "a", "name": "A", "kind": "duel", "levels": ["base-01"], "opponent": "gyobo" }""", "opponent")]
    [InlineData("""{ "id": "a", "name": "A", "kind": "duel", "levels": ["base-01"], "opponent": "cid", "difficulty": "godlike" }""", "difficulty")]
    [InlineData("""{ "id": "A B", "name": "A", "kind": "win", "levels": ["base-01"] }""", "id")]
    [InlineData("""{ "id": "a", "kind": "win", "levels": ["base-01"] }""", "name")]
    [InlineData("""{ "id": "a", "name": "A", "kind": "win", "levels": ["../x"] }""", "levels")]
    public void A_bad_challenge_is_refused_with_its_reason(string body, string word)
    {
        var load = MoonfallChallengeLoader.Parse(One(body));
        Assert.False(load.Ok);
        Assert.Empty(load.Challenges);
        Assert.Contains(load.Errors, e => e.Contains(word, StringComparison.Ordinal));
    }

    [Fact]
    public void The_file_is_checked_as_a_whole()
    {
        Assert.False(MoonfallChallengeLoader.Parse(null).Ok);
        Assert.False(MoonfallChallengeLoader.Parse("{").Ok);
        Assert.Contains("newer", MoonfallChallengeLoader.Parse("""{ "format": "moonfall-challenges", "version": 2, "challenges": [] }""").Errors[0], StringComparison.Ordinal);
        var twice = MoonfallChallengeLoader.Parse(One("""{ "id": "a", "name": "A", "kind": "win", "levels": ["base-01"] }, { "id": "a", "name": "B", "kind": "win", "levels": ["base-02"] }"""));
        Assert.Contains(twice.Errors, static e => e.Contains("twice", StringComparison.Ordinal));
        var defaults = MoonfallChallengeLoader.Parse(One("""{ "id": "d", "name": "D", "kind": "duel", "levels": ["base-01"], "opponent": "cid" }""")).Challenges[0];
        Assert.Equal((MoonfallRules.DuelBallsPerSide, MoonfallRules.OrangeCount, MoonfallAiDifficulty.Adept), (defaults.Balls, defaults.Oranges, defaults.Difficulty));
    }

    [Fact]
    public void Challenges_open_after_The_Moon_Road_and_need_their_levels_shipped()
    {
        var campaigns = MoonfallCampaigns.LoadBuiltIn();
        var shipped = Make(MoonfallChallengeKind.Win, ["base-01"]);
        var unshipped = Make(MoonfallChallengeKind.Win, ["base-30"]);
        var tooMany = Make(MoonfallChallengeKind.Win, ["base-01"], oranges: 45) with { Id = "u" };
        Assert.Equal(MoonfallChallengeState.Sealed, MoonfallChallenges.State(shipped, campaigns, new MoonfallProgress { BaseCleared = 54 }));
        var done = new MoonfallProgress { BaseCleared = 55 };
        Assert.Equal(MoonfallChallengeState.Open, MoonfallChallenges.State(shipped, campaigns, done));
        Assert.Equal(MoonfallChallengeState.Unavailable, MoonfallChallenges.State(unshipped, campaigns, done));
        Assert.Equal(campaigns.Find("base-01")!.OrangeCandidates >= 45, MoonfallChallenges.Playable(tooMany, campaigns));
        done.RecordChallenge("t", done: true, 1);
        Assert.Equal(MoonfallChallengeState.Done, MoonfallChallenges.State(shipped, campaigns, done));
    }

    [Fact]
    public void A_run_sets_the_balls_oranges_and_companion_and_a_score_challenge_is_met_at_its_target()
    {
        var campaigns = MoonfallCampaigns.LoadBuiltIn();
        var challenge = Make(MoonfallChallengeKind.Score, ["base-03"], target: 1, balls: 7, oranges: 27) with { Companion = MoonfallCompanion.Yshtola };
        var run = new MoonfallChallengeRun(challenge, campaigns, MoonfallCompanion.Cid, 11);
        Assert.Equal(MoonfallCompanion.Yshtola, run.Companion);
        var game = run.StartLevel();
        Assert.Equal(7, game.BallsLeft);
        Assert.Equal(27, game.OrangesLeft);
        Assert.Equal(MoonfallPower.Fireball, game.Power);
        Assert.Equal(3, game.LevelNumber);
        PlayOut(game, Angles);
        Assert.Equal(MoonfallChallengeStatus.Met, run.Finish(game));
        Assert.Equal(game.Score, run.Total);
        Assert.Throws<InvalidOperationException>(() => run.StartLevel());
    }

    [Fact]
    public void A_lost_level_ends_any_run_and_clear_all_asks_for_every_peg()
    {
        var campaigns = MoonfallCampaigns.LoadBuiltIn();
        var score = new MoonfallChallengeRun(Make(MoonfallChallengeKind.Score, ["base-01", "base-02"], target: long.MaxValue, balls: 1), campaigns, MoonfallCompanion.None, 3);
        var game = score.StartLevel();
        PlayOut(game, Angles);
        Assert.Equal(MoonfallPhase.Lost, game.Phase);
        Assert.Equal(MoonfallChallengeStatus.Failed, score.Finish(game));

        // Won but not perfect: a clear-all run fails; a win run goes on to its next level, and is met after the last.
        var clear = new MoonfallChallengeRun(Make(MoonfallChallengeKind.ClearAll, ["base-03"]), campaigns, MoonfallCompanion.None, 9);
        var win = new MoonfallChallengeRun(Make(MoonfallChallengeKind.Win, ["base-03", "base-03"]), campaigns, MoonfallCompanion.None, 9);
        var a = clear.StartLevel();
        var b = win.StartLevel();
        PlayOut(a, Angles);
        PlayOut(b, Angles);
        Assert.Equal(a.Fingerprint(), b.Fingerprint());
        if (a.Phase == MoonfallPhase.Won && !a.Perfect)
        {
            Assert.Equal(MoonfallChallengeStatus.Failed, clear.Finish(a));
            Assert.Equal(MoonfallChallengeStatus.Playing, win.Finish(b));
            Assert.Equal(1, win.LevelIndex);
            Assert.NotEqual(win.SeedOf(0), win.SeedOf(1));
        }
    }

    [Fact]
    public void A_duel_run_is_met_by_winning_every_duel()
    {
        var campaigns = MoonfallCampaigns.LoadBuiltIn();
        var run = new MoonfallChallengeRun(Make(MoonfallChallengeKind.Duel, ["base-01"], balls: 3), campaigns, MoonfallCompanion.None, 5);
        Assert.Throws<InvalidOperationException>(() => run.StartLevel());
        var duel = run.StartDuel();
        Assert.Equal(3, duel.BallsLeft(MoonfallDuel.PlayerSide));
        Assert.Equal(MoonfallCompanion.Raubahn, duel.Companion(MoonfallDuel.OpponentSide));
        var shot = 0;
        for (var t = 0; t < 400_000 && duel.Outcome == MoonfallDuelOutcome.Undecided; t++)
        {
            if (duel.PlayersTurn)
            {
                duel.Shoot(Angles[shot++ % Angles.Length]);
            }

            duel.Tick();
        }

        var status = run.Finish(duel);
        Assert.Equal(duel.Outcome == MoonfallDuelOutcome.Won ? MoonfallChallengeStatus.Met : MoonfallChallengeStatus.Failed, status);
        Assert.Equal(duel.Score(MoonfallDuel.PlayerSide), run.Total);
    }

    [Fact]
    public void The_modes_record_a_finished_challenge()
    {
        var campaigns = MoonfallCampaigns.LoadBuiltIn();
        var progress = new MoonfallProgress { BaseCleared = 55 };
        var modes = new MoonfallModes(campaigns, progress, MoonfallStory.Everyone, MoonfallChallenges.LoadBuiltIn().Challenges);
        var list = modes.ChallengeList();
        Assert.Equal(MoonfallChallengeState.Open, list.Single(static c => c.Challenge.Id == "ch-01").State);
        Assert.Equal(MoonfallChallengeState.Unavailable, list.Single(static c => c.Challenge.Id == "ch-12").State);
        Assert.Null(modes.StartChallenge("ch-12", MoonfallCompanion.None, 1));
        Assert.Null(modes.StartChallenge("nope", MoonfallCompanion.None, 1));

        var run = modes.StartChallenge("ch-01", MoonfallCompanion.Minfilia, 1)!;
        var game = run.StartLevel();
        PlayOut(game, Angles);
        run.Finish(game);
        modes.FinishChallenge(run);
        Assert.Equal(run.Status == MoonfallChallengeStatus.Met, progress.IsChallengeDone("ch-01"));
        Assert.Equal(run.Total, progress.Challenges["ch-01"].Best);
    }

    // ---- Ace scores ----

    [Fact]
    public void The_shipped_Aces_read_and_name_Adventures_levels()
    {
        Assert.Empty(MoonfallAces.Errors);
        Assert.NotEmpty(MoonfallAces.All);
        Assert.All(MoonfallAces.All, static a => Assert.True(MoonfallStages.TryPlace(a.Key, out _), a.Key));
        Assert.All(MoonfallAces.All, static a => Assert.True(a.Value > 0));
        Assert.Null(MoonfallAces.For("base-55-not"));
        var ace = MoonfallAces.For("base-01")!.Value;
        Assert.True(MoonfallAces.IsAce("base-01", ace));
        Assert.False(MoonfallAces.IsAce("base-01", ace - 1));
        Assert.Equal(MoonfallRules.AceBonus, MoonfallAces.Bonus("base-01", won: true, ace));
        Assert.Equal(0, MoonfallAces.Bonus("base-01", won: false, ace));
    }

    [Fact]
    public void An_aces_file_is_checked()
    {
        Assert.NotEmpty(MoonfallAces.Parse("""{ "format": "moonfall-aces", "version": 1, "aces": { "base-01": 0 } }""").Errors);
        Assert.NotEmpty(MoonfallAces.Parse("""{ "format": "moonfall-aces", "version": 1, "aces": { "Base 01": 5 } }""").Errors);
        Assert.NotEmpty(MoonfallAces.Parse("""{ "format": "moonfall-aces", "version": 2, "aces": { } }""").Errors);
        Assert.NotEmpty(MoonfallAces.Parse("nope").Errors);
        Assert.Equal(5, MoonfallAces.Parse("""{ "format": "moonfall-aces", "version": 1, "aces": { "base-09": 5 } }""").Aces["base-09"]);
    }

    [Fact]
    public void The_greedy_player_suggests_the_score_a_fifth_of_its_games_win_at_or_above()
    {
        // Ten games, six won: a fifth of them is two, so the Ace is the second-best win, rounded to the nearest 10,000.
        var wins = new[] { 100_000L, 210_000, 254_999, 300_001, 400_000, 180_000 }.Select(static s => new MoonfallGreedyGame(true, 10, 0, s, 0));
        var losses = Enumerable.Repeat(new MoonfallGreedyGame(false, 12, 2, 900_000, 0), 4);
        Assert.Equal(300_000, MoonfallAces.Suggest(new MoonfallPlayabilityReport("x", wins.Concat(losses).ToList())));

        // Fewer wins than a fifth: the lowest win.
        var few = new[] { new MoonfallGreedyGame(true, 10, 0, 254_999, 0) }.Concat(Enumerable.Repeat(new MoonfallGreedyGame(false, 12, 2, 5, 0), 9)).ToList();
        Assert.Equal(250_000, MoonfallAces.Suggest(new MoonfallPlayabilityReport("x", few)));
        Assert.Null(MoonfallAces.Suggest(new MoonfallPlayabilityReport("x", [new MoonfallGreedyGame(false, 9, 1, 5, 0)])));
        Assert.Equal(0.20, MoonfallAces.AceShare);
    }
}
