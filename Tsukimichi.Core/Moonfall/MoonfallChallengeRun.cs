namespace Tsukimichi.Core.Moonfall;

/// <summary>Where a challenge run stands.</summary>
public enum MoonfallChallengeStatus : byte
{
    /// <summary>A level is to be played (<see cref="MoonfallChallengeRun.Current"/>).</summary>
    Playing,

    /// <summary>The challenge was met.</summary>
    Met,

    /// <summary>The run ended without meeting it.</summary>
    Failed,
}

/// <summary>
/// One run of a challenge (plan v9 G7): its levels in order, each started from here (<see cref="StartLevel"/> or, for
/// a duel, <see cref="StartDuel"/>) and reported back when it ends (<see cref="Finish(MoonfallGame)"/>,
/// <see cref="Finish(MoonfallDuel)"/>). A score challenge is met as soon as a level ends with the run's total at the
/// target; a lost level ends any run. The other kinds are met when the last level ends as they ask: won, cleared of
/// every peg, or the duel won (a draw is not a win). Each level is dealt from the run's seed and its place in the run.
/// </summary>
public sealed class MoonfallChallengeRun
{
    private readonly MoonfallCampaigns campaigns;
    private readonly ulong seed;

    /// <param name="challenge">The challenge.</param>
    /// <param name="campaigns">The shipped levels; every level the challenge names must be one (<see cref="MoonfallChallenges.Playable"/>).</param>
    /// <param name="companion">
    /// The player's companion: the challenge's own when it fixes one (this is then ignored), else the player's pick
    /// (<see cref="MoonfallCompanion.None"/> for no power).
    /// </param>
    /// <param name="seed">Deals every level of the run.</param>
    public MoonfallChallengeRun(MoonfallChallenge challenge, MoonfallCampaigns campaigns, MoonfallCompanion companion, ulong seed)
    {
        Challenge = challenge ?? throw new ArgumentNullException(nameof(challenge));
        this.campaigns = campaigns ?? throw new ArgumentNullException(nameof(campaigns));
        if (!MoonfallChallenges.Playable(challenge, campaigns))
        {
            throw new ArgumentException($"challenge {challenge.Id} names a level that is not shipped", nameof(challenge));
        }

        Companion = challenge.Companion != MoonfallCompanion.None ? challenge.Companion : companion;
        this.seed = seed;
    }

    public MoonfallChallenge Challenge { get; }

    /// <summary>The player's companion for the run.</summary>
    public MoonfallCompanion Companion { get; }

    /// <summary>The level being played (its index in the run, from 0).</summary>
    public int LevelIndex { get; private set; }

    /// <summary>The level to play now.</summary>
    public MoonfallLevel Current => campaigns.Find(Challenge.LevelIds[Math.Min(LevelIndex, Challenge.LevelIds.Count - 1)])!;

    /// <summary>The run's total so far (the player's own scores in a duel).</summary>
    public long Total { get; private set; }

    public MoonfallChallengeStatus Status { get; private set; }

    /// <summary>The seed of the run's level <paramref name="index"/>.</summary>
    public ulong SeedOf(int index) => seed + ((ulong)index * 0x9E3779B97F4A7C15UL);

    /// <summary>The current level's game, as the challenge sets it: its balls, oranges and the run's companion.</summary>
    public MoonfallGame StartLevel()
    {
        if (Status != MoonfallChallengeStatus.Playing || Challenge.Kind == MoonfallChallengeKind.Duel)
        {
            throw new InvalidOperationException("no level of this run is waiting, or it is a duel (StartDuel)");
        }

        var id = Challenge.LevelIds[LevelIndex];
        return new MoonfallGame(
            Current,
            MoonfallChallenges.LevelNumber(id),
            SeedOf(LevelIndex),
            Challenge.Balls,
            MoonfallCompanions.TryGet(Companion, out var info) ? info.Power : MoonfallPower.None,
            oranges: Challenge.Oranges);
    }

    /// <summary>The current level's duel against the challenge's opponent.</summary>
    public MoonfallDuel StartDuel()
    {
        if (Status != MoonfallChallengeStatus.Playing || Challenge.Kind != MoonfallChallengeKind.Duel)
        {
            throw new InvalidOperationException("no duel of this run is waiting");
        }

        var id = Challenge.LevelIds[LevelIndex];
        return new MoonfallDuel(Current, MoonfallChallenges.LevelNumber(id), SeedOf(LevelIndex), Companion, Challenge.Opponent, Challenge.Difficulty, Challenge.Balls);
    }

    /// <summary>The current level ended (won or lost): the run moves on, is met, or fails.</summary>
    public MoonfallChallengeStatus Finish(MoonfallGame game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (Status != MoonfallChallengeStatus.Playing || Challenge.Kind == MoonfallChallengeKind.Duel || game.Phase is not (MoonfallPhase.Won or MoonfallPhase.Lost))
        {
            throw new InvalidOperationException("the level has not ended, or the run is not waiting for it");
        }

        Total += game.Score;
        var won = game.Phase == MoonfallPhase.Won;
        var ok = Challenge.Kind switch
        {
            MoonfallChallengeKind.Score => true,
            MoonfallChallengeKind.ClearAll => won && game.Perfect,
            _ => won,
        };
        if (Challenge.Kind == MoonfallChallengeKind.Score && Total >= Challenge.Target)
        {
            return Status = MoonfallChallengeStatus.Met;
        }

        return Next(ok && won);
    }

    /// <summary>The current level's duel ended: the run moves on, is met, or fails.</summary>
    public MoonfallChallengeStatus Finish(MoonfallDuel duel)
    {
        ArgumentNullException.ThrowIfNull(duel);
        if (Status != MoonfallChallengeStatus.Playing || Challenge.Kind != MoonfallChallengeKind.Duel || duel.Outcome == MoonfallDuelOutcome.Undecided)
        {
            throw new InvalidOperationException("the duel has not ended, or the run is not waiting for it");
        }

        Total += duel.Score(MoonfallDuel.PlayerSide);
        return Next(duel.Outcome == MoonfallDuelOutcome.Won);
    }

    private MoonfallChallengeStatus Next(bool carryOn)
    {
        if (!carryOn)
        {
            return Status = MoonfallChallengeStatus.Failed;
        }

        if (++LevelIndex < Challenge.LevelIds.Count)
        {
            return Status;
        }

        LevelIndex = Challenge.LevelIds.Count - 1;
        return Status = Challenge.Kind == MoonfallChallengeKind.Score ? MoonfallChallengeStatus.Failed : MoonfallChallengeStatus.Met;
    }
}
