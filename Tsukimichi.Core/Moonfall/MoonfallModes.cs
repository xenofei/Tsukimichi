namespace Tsukimichi.Core.Moonfall;

/// <summary>The four ways to play (plan v9 G7).</summary>
public enum MoonfallMode : byte
{
    Adventure,
    QuickPlay,
    Challenge,
    Duel,
}

/// <summary>Where a level stands for the map and level select.</summary>
public enum MoonfallLevelState : byte
{
    /// <summary>No level of this id ships yet: Adventure stops before it.</summary>
    Missing,

    /// <summary>Not reached: the card back, darkened, with a padlock.</summary>
    Sealed,

    /// <summary>Reached and not yet won: the open tile.</summary>
    Open,

    /// <summary>Won.</summary>
    Cleared,
}

/// <summary>Where a stage stands on the map.</summary>
public enum MoonfallStageState : byte
{
    /// <summary>Not reached: drained, the ring dimmed, a padlock.</summary>
    Sealed,

    /// <summary>Reached, with a level still to win: "here" when it holds the next level.</summary>
    Open,

    /// <summary>Every level won: a lit orange moon.</summary>
    Done,
}

/// <summary>One level as the menus show it.</summary>
/// <param name="Id">Its id.</param>
/// <param name="Place">Its campaign and index.</param>
/// <param name="Level">The level, or null when it is <see cref="MoonfallLevelState.Missing"/>.</param>
/// <param name="State">Where it stands.</param>
/// <param name="Best">The best score (0 when none).</param>
/// <param name="Aced">Its Ace has been beaten.</param>
/// <param name="Ace">Its Ace score, or null when it has none.</param>
public readonly record struct MoonfallLevelSlot(string Id, MoonfallLevelPlace Place, MoonfallLevel? Level, MoonfallLevelState State, long Best, bool Aced, long? Ace)
{
    /// <summary>Whether it may be played in Quick Play, a duel or from level select: shipped and reached.</summary>
    public bool Reached => State is MoonfallLevelState.Open or MoonfallLevelState.Cleared;
}

/// <summary>One stage as the map shows it.</summary>
/// <param name="Stage">The stage.</param>
/// <param name="State">Where it stands.</param>
/// <param name="Companion">How its companion shows (<see cref="MoonfallCompanionState.Available"/> on the stage where the player picks).</param>
/// <param name="Here">It holds the next level to play (the "here" glow).</param>
/// <param name="Levels">Its five levels.</param>
public sealed record MoonfallStageView(MoonfallStage Stage, MoonfallStageState State, MoonfallCompanionState Companion, bool Here, IReadOnlyList<MoonfallLevelSlot> Levels);

/// <summary>A level ready to start: what <see cref="MoonfallGame"/> is made with.</summary>
/// <param name="Mode">How it is played.</param>
/// <param name="LevelId">The level's id.</param>
/// <param name="Level">The level.</param>
/// <param name="LevelNumber">Its number (greens from 3).</param>
/// <param name="Companion">Who carries the power (<see cref="MoonfallCompanion.None"/>: none).</param>
/// <param name="Balls">Balls to start with.</param>
/// <param name="Oranges">Oranges to pick.</param>
public sealed record MoonfallStart(MoonfallMode Mode, string LevelId, MoonfallLevel Level, int LevelNumber, MoonfallCompanion Companion, int Balls, int Oranges)
{
    /// <summary>The power the companion carries.</summary>
    public MoonfallPower Power => MoonfallCompanions.TryGet(Companion, out var info) ? info.Power : MoonfallPower.None;

    /// <summary>The game, dealt from <paramref name="seed"/>.</summary>
    public MoonfallGame Create(ulong seed) => new(Level, LevelNumber, seed, Balls, Power, oranges: Oranges);
}

/// <summary>What a finished level earned, for the tally.</summary>
/// <param name="Won">It was won.</param>
/// <param name="Score">The level's score, with the Ace bonus.</param>
/// <param name="AceBonus">The Ace bonus (0 unless aced).</param>
/// <param name="Aced">The Ace was beaten this time.</param>
/// <param name="NewBest">The score beat the level's best.</param>
/// <param name="Unlocked">The win opened something: the next level, a stage, a companion, The Far Shore or the challenges.</param>
public readonly record struct MoonfallLevelResult(bool Won, long Score, long AceBonus, bool Aced, bool NewBest, bool Unlocked);

/// <summary>
/// The menus' one view of Moonfall's modes (plan v9 G7): the map, level select, the characters grid, Quick Play, the
/// challenges and the duels all ask here, and a finished level is recorded here (<see cref="FinishLevel"/>). It reads
/// the shipped levels, the account's progress (which it updates; save it after) and the story (the spoiler shield). Pure:
/// no ImGui, no clock, no file. See <c>docs/design/v9/moonfall-modes.md</c>.
/// </summary>
public sealed class MoonfallModes
{
    /// <summary>[J] The opponent the title offers first ("Duel against Louisoix" in the mocks): the sage of the cast.</summary>
    public const MoonfallCompanion DefaultOpponent = MoonfallCompanion.Louisoix;

    public MoonfallModes(MoonfallCampaigns campaigns, MoonfallProgress progress, MoonfallStory story, IReadOnlyList<MoonfallChallenge> challenges)
    {
        Campaigns = campaigns ?? throw new ArgumentNullException(nameof(campaigns));
        Progress = progress ?? throw new ArgumentNullException(nameof(progress));
        Story = story ?? throw new ArgumentNullException(nameof(story));
        Challenges = challenges ?? throw new ArgumentNullException(nameof(challenges));
    }

    public MoonfallCampaigns Campaigns { get; }

    public MoonfallProgress Progress { get; }

    public MoonfallStory Story { get; }

    public IReadOnlyList<MoonfallChallenge> Challenges { get; }

    /// <summary>Each level's Ace score (the shipped table, <see cref="MoonfallAces.For"/>; the offline renderer stages its own).</summary>
    public Func<string?, long?> AceOf { get; init; } = MoonfallAces.For;

    // ---- Adventure ----

    /// <summary>Whether the campaign can be played: The Moon Road always; The Far Shore once all 55 of The Moon Road are won.</summary>
    public bool CampaignOpen(MoonfallCampaignKind campaign) =>
        campaign == MoonfallCampaignKind.Base || Progress.BaseCleared >= MoonfallStages.BaseLevels;

    /// <summary>Level <paramref name="index"/> (from 0) of <paramref name="campaign"/> as the menus show it.</summary>
    public MoonfallLevelSlot Slot(MoonfallCampaignKind campaign, int index)
    {
        var id = MoonfallStages.LevelId(campaign, index);
        var level = Campaigns.Find(id);
        var state = level is null ? MoonfallLevelState.Missing
            : Progress.IsCleared(id) ? MoonfallLevelState.Cleared
            : CampaignOpen(campaign) && index <= Progress.Cleared(campaign) ? MoonfallLevelState.Open
            : MoonfallLevelState.Sealed;
        return new MoonfallLevelSlot(id, new MoonfallLevelPlace(campaign, index), level, state, Progress.Best(id), Progress.IsAced(id), AceOf(id));
    }

    /// <summary>The campaign's stages as the map shows them.</summary>
    public IReadOnlyList<MoonfallStageView> Stages(MoonfallCampaignKind campaign)
    {
        var next = Continue();
        var views = new List<MoonfallStageView>();
        foreach (var stage in MoonfallStages.Of(campaign))
        {
            var slots = new MoonfallLevelSlot[stage.LevelIds.Count];
            for (var k = 0; k < slots.Length; k++)
            {
                slots[k] = Slot(campaign, stage.FirstLevelIndex + k);
            }

            var state = slots.All(static s => s.State == MoonfallLevelState.Cleared) ? MoonfallStageState.Done
                : slots[0].Reached ? MoonfallStageState.Open
                : MoonfallStageState.Sealed;
            var companion = stage.PlayerPicks ? MoonfallCompanionState.Available : CompanionState(stage.Companion);
            var here = next is { } n && n.Campaign == campaign && MoonfallCharacters.Stage(n.Index) == stage.Number;
            views.Add(new MoonfallStageView(stage, state, companion, here, slots));
        }

        return views;
    }

    /// <summary>The next level Adventure plays (the title's Continue card): the first open one, The Moon Road first; null when every shipped level is won or the next is not shipped yet.</summary>
    public MoonfallLevelPlace? Continue()
    {
        foreach (var campaign in (ReadOnlySpan<MoonfallCampaignKind>)[MoonfallCampaignKind.Base, MoonfallCampaignKind.Expansion])
        {
            if (!CampaignOpen(campaign))
            {
                continue;
            }

            var index = Progress.Cleared(campaign);
            if (index < MoonfallStages.LevelCount(campaign))
            {
                return Slot(campaign, index).State == MoonfallLevelState.Open ? new MoonfallLevelPlace(campaign, index) : null;
            }
        }

        return null;
    }

    /// <summary>
    /// Adventure's level <paramref name="index"/> of <paramref name="campaign"/>, with its stage's companion; on the
    /// stage where the player picks, <paramref name="picked"/> (any <see cref="MoonfallCompanionState.Available"/>
    /// companion). Null when the level is sealed or missing, or the pick is not available.
    /// </summary>
    public MoonfallStart? Adventure(MoonfallCampaignKind campaign, int index, MoonfallCompanion picked = MoonfallCompanion.None)
    {
        if (index < 0 || index >= MoonfallStages.LevelCount(campaign))
        {
            return null;
        }

        var slot = Slot(campaign, index);
        if (!slot.Reached || slot.Level is null)
        {
            return null;
        }

        var companion = MoonfallStages.AdventureCompanion(campaign, index);
        if (companion == MoonfallCompanion.None)
        {
            if (picked != MoonfallCompanion.None && CompanionState(picked) != MoonfallCompanionState.Available)
            {
                return null;
            }

            companion = picked;
        }

        return new MoonfallStart(MoonfallMode.Adventure, slot.Id, slot.Level, slot.Place.Number, companion, MoonfallRules.BallsPerLevel, MoonfallRules.OrangeCount);
    }

    // ---- The companions ----

    /// <summary>How the companion shows (<see cref="MoonfallCompanions.State"/>).</summary>
    public MoonfallCompanionState CompanionState(MoonfallCompanion companion) => MoonfallCompanions.State(companion, Progress, Story);

    // ---- Quick Play ----

    /// <summary>Every level Quick Play offers: each shipped level reached in Adventure ("Quick Play replays unlocked levels" [R §6 l.126]), The Moon Road first.</summary>
    public IReadOnlyList<MoonfallLevelSlot> QuickPlayLevels()
    {
        var list = new List<MoonfallLevelSlot>();
        foreach (var campaign in (ReadOnlySpan<MoonfallCampaignKind>)[MoonfallCampaignKind.Base, MoonfallCampaignKind.Expansion])
        {
            for (var i = 0; i < MoonfallStages.LevelCount(campaign); i++)
            {
                var slot = Slot(campaign, i);
                if (slot.Reached)
                {
                    list.Add(slot);
                }
            }
        }

        return list;
    }

    /// <summary>The companions Quick Play offers: the available ones (<see cref="MoonfallCompanions.QuickPlay"/>).</summary>
    public IReadOnlyList<MoonfallCompanion> QuickPlayCompanions() => MoonfallCompanions.QuickPlay(Progress, Story);

    /// <summary>
    /// Quick Play on <paramref name="levelId"/> with <paramref name="companion"/> (<see cref="MoonfallCompanion.None"/>
    /// for no power): null unless the level is reached and the companion available.
    /// </summary>
    public MoonfallStart? QuickPlay(string levelId, MoonfallCompanion companion)
    {
        if (!MoonfallStages.TryPlace(levelId, out var place))
        {
            return null;
        }

        var slot = Slot(place.Campaign, place.Index);
        if (!slot.Reached || slot.Level is null || (companion != MoonfallCompanion.None && CompanionState(companion) != MoonfallCompanionState.Available))
        {
            return null;
        }

        return new MoonfallStart(MoonfallMode.QuickPlay, slot.Id, slot.Level, place.Number, companion, MoonfallRules.BallsPerLevel, MoonfallRules.OrangeCount);
    }

    /// <summary>
    /// A level of Adventure or Quick Play has ended: its best score (with the Ace bonus), cleared when won (which opens
    /// the next level in Adventure's order), and aced. Returns what it earned for the tally.
    /// </summary>
    public MoonfallLevelResult FinishLevel(MoonfallStart start, MoonfallGame game)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(game);
        if (game.Phase is not (MoonfallPhase.Won or MoonfallPhase.Lost))
        {
            throw new InvalidOperationException("the level has not ended");
        }

        var won = game.Phase == MoonfallPhase.Won;
        var bonus = won && AceOf(start.LevelId) is { } ace && game.Score >= ace ? MoonfallRules.AceBonus : 0;
        var score = game.Score + bonus;
        var newBest = score > Progress.Best(start.LevelId);
        var wasAced = Progress.IsAced(start.LevelId);
        var before = (Progress.BaseCleared, Progress.ExpansionCleared, ChallengesOpen);
        Progress.RecordLevel(start.LevelId, won, score, AceOf(start.LevelId));
        var unlocked = before != (Progress.BaseCleared, Progress.ExpansionCleared, ChallengesOpen);
        return new MoonfallLevelResult(won, score, bonus, bonus > 0 && !wasAced, newBest, unlocked);
    }

    // ---- Challenges ----

    /// <summary>Whether challenges are open (<see cref="MoonfallChallenges.Open"/>).</summary>
    public bool ChallengesOpen => MoonfallChallenges.Open(Progress);

    /// <summary>Each challenge with where it stands.</summary>
    public IReadOnlyList<(MoonfallChallenge Challenge, MoonfallChallengeState State)> ChallengeList() =>
        Challenges.Select(c => (c, MoonfallChallenges.State(c, Campaigns, Progress))).ToList();

    /// <summary>
    /// A run of the challenge <paramref name="id"/>, with <paramref name="companion"/> when it lets the player pick (an
    /// available one, or none); null unless it is open (or done) and the pick is available.
    /// </summary>
    public MoonfallChallengeRun? StartChallenge(string id, MoonfallCompanion companion, ulong seed)
    {
        var challenge = Challenges.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal));
        if (challenge is null || MoonfallChallenges.State(challenge, Campaigns, Progress) is not (MoonfallChallengeState.Open or MoonfallChallengeState.Done))
        {
            return null;
        }

        if (challenge.Companion == MoonfallCompanion.None && companion != MoonfallCompanion.None && CompanionState(companion) != MoonfallCompanionState.Available)
        {
            return null;
        }

        return new MoonfallChallengeRun(challenge, Campaigns, companion, seed);
    }

    /// <summary>A challenge run has ended: its result is recorded.</summary>
    public void FinishChallenge(MoonfallChallengeRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (run.Status == MoonfallChallengeStatus.Playing)
        {
            throw new InvalidOperationException("the run has not ended");
        }

        Progress.RecordChallenge(run.Challenge.Id, run.Status == MoonfallChallengeStatus.Met, run.Total);
    }

    // ---- Duels ----

    /// <summary>
    /// [J] The opponents a duel offers: every companion the story has introduced (their card is face up), reached in
    /// Moonfall or not, so the title can offer "Duel against Louisoix" from the start; never one not yet met.
    /// </summary>
    public IReadOnlyList<MoonfallCompanion> DuelOpponents() =>
        MoonfallCompanions.All.Where(c => CompanionState(c.Companion) != MoonfallCompanionState.NotMet).Select(static c => c.Companion).ToList();

    /// <summary>The opponent the title offers: <see cref="DefaultOpponent"/> when met, else the first met; None when none is.</summary>
    public MoonfallCompanion TitleOpponent()
    {
        var opponents = DuelOpponents();
        return opponents.Contains(DefaultOpponent) ? DefaultOpponent : opponents.FirstOrDefault();
    }

    /// <summary>
    /// A duel on <paramref name="levelId"/> (a level reached, as Quick Play) with the player's
    /// <paramref name="companion"/> (an available one, or none) against <paramref name="opponent"/> (one met); null
    /// otherwise.
    /// </summary>
    public MoonfallDuel? StartDuel(string levelId, MoonfallCompanion companion, MoonfallCompanion opponent, MoonfallAiDifficulty difficulty, ulong seed)
    {
        if (QuickPlay(levelId, companion) is not { } start || !DuelOpponents().Contains(opponent))
        {
            return null;
        }

        return new MoonfallDuel(start.Level, start.LevelNumber, seed, companion, opponent, difficulty);
    }

    /// <summary>A duel has ended: its result is recorded against its opponent and difficulty.</summary>
    public void FinishDuel(MoonfallDuel duel)
    {
        ArgumentNullException.ThrowIfNull(duel);
        if (duel.Outcome == MoonfallDuelOutcome.Undecided)
        {
            throw new InvalidOperationException("the duel has not ended");
        }

        Progress.RecordDuel(duel.Companion(MoonfallDuel.OpponentSide), duel.Opponent.Difficulty, duel.Outcome);
    }
}
