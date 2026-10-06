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

    /// <summary>
    /// Its stage is set past the player's story (<see cref="MoonfallShield"/>): no name, no scene, and closed until the
    /// story reaches it or the place is revealed, whatever Moonfall's progress.
    /// </summary>
    Veiled,
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

    /// <summary>Set past the player's story (<see cref="MoonfallShield"/>): the shield's mark, its name a placeholder, closed.</summary>
    Veiled,
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
/// <param name="Reached">
/// Moonfall's progress has come to it (the road's frontier, stepping over veiled stages, is at or past its first level).
/// A <see cref="MoonfallStageState.Veiled"/> stage not yet reached is closed twice over: past the story and not reached.
/// </param>
public sealed record MoonfallStageView(MoonfallStage Stage, MoonfallStageState State, MoonfallCompanionState Companion, bool Here, IReadOnlyList<MoonfallLevelSlot> Levels, bool Reached = true);

/// <summary>How a level's scene shows under the spoiler shield.</summary>
public enum MoonfallSceneHide : byte
{
    /// <summary>As its recipe has it.</summary>
    Shown,

    /// <summary>The scene's own place is past the story (its stage is not): the recipe over its declared story-safe fallback picture.</summary>
    Fallback,

    /// <summary>Its stage is past the story: no scene art at all, the night sky.</summary>
    Hidden,
}

/// <summary>Where Adventure goes next (the title's Continue): a level to play, or the first stage it steps over because it is set past the player's story.</summary>
/// <param name="Place">The level: the next to play, or the first of the veiled stage.</param>
/// <param name="Veiled">The place is on a stage past the player's story: nothing ahead can be played until it opens.</param>
public readonly record struct MoonfallNext(MoonfallLevelPlace Place, bool Veiled);

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

    /// <summary>The spoiler shield for places (<see cref="MoonfallShield"/>; the plugin passes the viewed character's).</summary>
    public MoonfallShield Shield { get; init; } = MoonfallShield.Open;

    // ---- The spoiler shield ----

    /// <summary>Whether <paramref name="stage"/> is set past the player's story (<see cref="MoonfallPlaces.OfStage"/>).</summary>
    public bool StageVeiled(MoonfallStage stage) => Shield.Hides(MoonfallPlaces.OfStage(stage));

    /// <summary>Whether the level <paramref name="id"/> belongs to a stage set past the player's story.</summary>
    public bool LevelVeiled(string? id) =>
        MoonfallStages.TryPlace(id, out var place) && MoonfallStages.StageOf(place.Campaign, place.Index) is { } stage && StageVeiled(stage);

    /// <summary>The stage's name as the menus print it: the shield's placeholder for its place while it is veiled.</summary>
    public string StageName(MoonfallStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        return StageVeiled(stage) ? Shield.Placeholder(MoonfallPlaces.OfStage(stage)) : stage.Name;
    }

    /// <summary>
    /// The place a veiled stage hides (the area the shield's reveal opens), or null when <paramref name="stage"/> is not
    /// veiled.
    /// </summary>
    public string? VeiledZone(MoonfallStage stage) => StageVeiled(stage) ? MoonfallPlaces.OfStage(stage).Zone : null;

    /// <summary>
    /// Whether <paramref name="level"/>'s scene, the recipe <paramref name="sceneName"/>, must not show: its stage is
    /// veiled, or the scene itself is set past the player's story (<see cref="MoonfallPlaces.OfScene"/>).
    /// </summary>
    public bool SceneVeiled(MoonfallLevel level, string? sceneName) => SceneHide(level, sceneName) != MoonfallSceneHide.Shown;

    /// <summary>
    /// How <paramref name="level"/>'s scene, the recipe <paramref name="sceneName"/>, shows: hidden outright on a veiled
    /// stage (no scene art), over the recipe's story-safe fallback when only the scene's own place is past the story.
    /// </summary>
    public MoonfallSceneHide SceneHide(MoonfallLevel level, string? sceneName)
    {
        ArgumentNullException.ThrowIfNull(level);
        return LevelVeiled(level.Id) ? MoonfallSceneHide.Hidden
            : MoonfallPlaces.OfScene(sceneName) is { } place && Shield.Hides(place) ? MoonfallSceneHide.Fallback
            : MoonfallSceneHide.Shown;
    }

    /// <summary>
    /// The road's frontier in <paramref name="campaign"/> (the owner's "step over it"): the first level neither won nor on a
    /// veiled stage. Adventure steps over a veiled stage, whose levels wait unwon until the story or a reveal opens it;
    /// "all won" and the campaign's count still need them. The level count when every level is won or veiled.
    /// </summary>
    public int Frontier(MoonfallCampaignKind campaign)
    {
        var count = MoonfallStages.LevelCount(campaign);
        for (var i = Progress.Cleared(campaign); i < count; i++)
        {
            if (Progress.IsCleared(MoonfallStages.LevelId(campaign, i)))
            {
                continue;
            }

            if (MoonfallStages.StageOf(campaign, i) is { } stage && StageVeiled(stage))
            {
                continue;
            }

            return i;
        }

        return count;
    }

    /// <summary>
    /// Winning a level opens the next, whatever the frontier says now: a level whose road-order predecessor is won stays
    /// open, so a reveal, or the story reaching a stepped-over stage (which pulls the frontier back to it), never closes
    /// a level the player had come to by winning the one before.
    /// </summary>
    private bool Opened(MoonfallCampaignKind campaign, int index) =>
        index > 0 && Progress.IsCleared(MoonfallStages.LevelId(campaign, index - 1));

    /// <summary>Whether <paramref name="stage"/> has a level that ships (a stage of levels not built yet is never "waiting past the story").</summary>
    public bool StageBuilt(MoonfallStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        foreach (var id in stage.LevelIds)
        {
            if (Campaigns.Find(id) is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether Moonfall's progress has come to <paramref name="stage"/>: its campaign is open and the frontier is at or past its first level.</summary>
    public bool StageReached(MoonfallStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        return CampaignOpen(stage.Campaign) && stage.FirstLevelIndex <= Frontier(stage.Campaign);
    }

    /// <summary>
    /// Adventure's level after <paramref name="index"/> (the tally's Next): the next reached level, stepping over veiled
    /// stages; null when there is none. <paramref name="steppedOver"/> is the first veiled stage passed on the way, if any.
    /// </summary>
    public int? NextLevel(MoonfallCampaignKind campaign, int index, out MoonfallStage? steppedOver)
    {
        steppedOver = null;
        for (var i = index + 1; i < MoonfallStages.LevelCount(campaign); i++)
        {
            var slot = Slot(campaign, i);
            if (slot.State == MoonfallLevelState.Veiled)
            {
                // Only a stage with levels built is one the road waits at.
                if (steppedOver is null && MoonfallStages.StageOf(campaign, i) is { } stage && StageBuilt(stage))
                {
                    steppedOver = stage;
                }

                continue;
            }

            if (slot.State == MoonfallLevelState.Missing)
            {
                // The road stops at a level not built yet: that, not the veil, is why there is no next.
                steppedOver = null;
                return null;
            }

            return slot.Reached ? i : null;
        }

        return null;
    }

    // ---- Adventure ----

    /// <summary>Whether the campaign can be played: The Moon Road always; The Far Shore once all 55 of The Moon Road are won.</summary>
    public bool CampaignOpen(MoonfallCampaignKind campaign) =>
        campaign == MoonfallCampaignKind.Base || Progress.BaseCleared >= MoonfallStages.BaseLevels;

    /// <summary>Level <paramref name="index"/> (from 0) of <paramref name="campaign"/> as the menus show it.</summary>
    public MoonfallLevelSlot Slot(MoonfallCampaignKind campaign, int index)
    {
        var id = MoonfallStages.LevelId(campaign, index);
        var level = Campaigns.Find(id);
        var state = MoonfallStages.StageOf(campaign, index) is { } stage && StageVeiled(stage) ? MoonfallLevelState.Veiled
            : level is null ? MoonfallLevelState.Missing
            : Progress.IsCleared(id) ? MoonfallLevelState.Cleared
            : CampaignOpen(campaign) && (index <= Frontier(campaign) || Opened(campaign, index) || index == Progress.Reach(campaign)) ? MoonfallLevelState.Open
            : MoonfallLevelState.Sealed;
        return new MoonfallLevelSlot(id, new MoonfallLevelPlace(campaign, index), level, state, Progress.Best(id), Progress.IsAced(id), AceOf(id));
    }

    /// <summary>The campaign's stages as the map shows them.</summary>
    public IReadOnlyList<MoonfallStageView> Stages(MoonfallCampaignKind campaign)
    {
        var next = Next()?.Place;
        var frontier = Frontier(campaign);
        var open = CampaignOpen(campaign);
        var views = new List<MoonfallStageView>();
        foreach (var stage in MoonfallStages.Of(campaign))
        {
            var slots = new MoonfallLevelSlot[stage.LevelIds.Count];
            for (var k = 0; k < slots.Length; k++)
            {
                slots[k] = Slot(campaign, stage.FirstLevelIndex + k);
            }

            var state = StageVeiled(stage) ? MoonfallStageState.Veiled
                : slots.All(static s => s.State == MoonfallLevelState.Cleared) ? MoonfallStageState.Done
                : slots[0].Reached ? MoonfallStageState.Open
                : MoonfallStageState.Sealed;
            var companion = stage.PlayerPicks ? MoonfallCompanionState.Available : CompanionState(stage.Companion);
            var here = next is { } n && n.Campaign == campaign && MoonfallCharacters.Stage(n.Index) == stage.Number;
            var reached = open && stage.FirstLevelIndex <= frontier;
            views.Add(new MoonfallStageView(stage, state, companion, here, slots, state != MoonfallStageState.Sealed && (state != MoonfallStageState.Veiled || reached)));
        }

        return views;
    }

    /// <summary>The next level Adventure plays (the title's Continue card): the first open one, The Moon Road first; null when every shipped level is won or the next is not shipped yet.</summary>
    public MoonfallLevelPlace? Continue() => Next() is { Veiled: false } next ? next.Place : null;

    /// <summary>
    /// Where Adventure goes next: the frontier's level when it can be played (stepping over veiled stages); else the
    /// level the road had come to (<see cref="MoonfallProgress.Reach"/>) when it is still open; otherwise, when a stage set past the player's story is what stands between the player and more of the road, that stage's
    /// first level, <see cref="MoonfallNext.Veiled"/>. Null when every shipped level is won, or the next is not built yet
    /// and nothing is veiled.
    /// </summary>
    public MoonfallNext? Next()
    {
        foreach (var campaign in (ReadOnlySpan<MoonfallCampaignKind>)[MoonfallCampaignKind.Base, MoonfallCampaignKind.Expansion])
        {
            if (!CampaignOpen(campaign))
            {
                continue;
            }

            var count = MoonfallStages.LevelCount(campaign);
            var index = Frontier(campaign);
            if (index < count && Slot(campaign, index).State == MoonfallLevelState.Open)
            {
                return new MoonfallNext(new MoonfallLevelPlace(campaign, index), false);
            }

            // The frontier fell back onto a level not built yet (a story step unveiled an unbuilt stage): the level the road
            // had come to (its high-water mark) is still open, so Adventure goes on there (GD n15).
            var reach = Progress.Reach(campaign);
            if (reach != index && reach < count && Slot(campaign, reach).State == MoonfallLevelState.Open)
            {
                return new MoonfallNext(new MoonfallLevelPlace(campaign, reach), false);
            }

            // Nothing to play ahead: the first stage the road stepped over (before the frontier) that has levels built is
            // where it waits. A stage of levels not built yet is not "past your story": its levels are on their way.
            for (var i = Progress.Cleared(campaign); i < Math.Min(index, count); i++)
            {
                if (!Progress.IsCleared(MoonfallStages.LevelId(campaign, i)) && MoonfallStages.StageOf(campaign, i) is { } stage && StageVeiled(stage) && StageBuilt(stage))
                {
                    return new MoonfallNext(new MoonfallLevelPlace(campaign, stage.FirstLevelIndex), true);
                }
            }

            if (index < count)
            {
                return null;
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
    public MoonfallCompanionState CompanionState(MoonfallCompanion companion)
    {
        if (!Story.HasMet(companion))
        {
            return MoonfallCompanionState.NotMet;
        }

        return CompanionReached(companion) ? MoonfallCompanionState.Available : MoonfallCompanionState.MetNotReached;
    }

    /// <summary>
    /// Whether Moonfall has reached the companion: its stage is open by the road's own rule (the frontier, stepping over
    /// veiled stages, or a level of it already won or opened), and the stage is not itself set past the player's story.
    /// So the moogle is reached once Storm Post opens, wherever the consecutive count stands.
    /// </summary>
    public bool CompanionReached(MoonfallCompanion companion)
    {
        if (!MoonfallCompanions.TryGet(companion, out var info) || !CampaignOpen(info.Campaign))
        {
            return false;
        }

        var stages = MoonfallStages.Of(info.Campaign);
        if (info.Stage < 1 || info.Stage > stages.Count)
        {
            return MoonfallCompanions.Reached(companion, Progress);
        }

        var stage = stages[info.Stage - 1];
        if (StageVeiled(stage))
        {
            return false;
        }

        // Its stage's first level is open by the road's rule (the frontier, the level before it won, or the road's
        // high-water mark), or a level of it is already won.
        if (Slot(info.Campaign, stage.FirstLevelIndex).Reached)
        {
            return true;
        }

        foreach (var id in stage.LevelIds)
        {
            if (Progress.IsCleared(id))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Notes where each open campaign's frontier stands as its high-water mark (<see cref="MoonfallProgress.Reach"/>):
    /// done after every level's end, so a level the road came to stays open when a reveal or the story moving on pulls
    /// the frontier back. True when a mark moved.
    /// </summary>
    public bool NoteReach()
    {
        var moved = false;
        foreach (var campaign in (ReadOnlySpan<MoonfallCampaignKind>)[MoonfallCampaignKind.Base, MoonfallCampaignKind.Expansion])
        {
            if (CampaignOpen(campaign))
            {
                moved |= Progress.RecordReach(campaign, Frontier(campaign));
            }
        }

        return moved;
    }

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
    public IReadOnlyList<MoonfallCompanion> QuickPlayCompanions() =>
        MoonfallCompanions.All.Where(c => CompanionState(c.Companion) == MoonfallCompanionState.Available).Select(static c => c.Companion).ToList();

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
        NoteReach();
        var unlocked = before != (Progress.BaseCleared, Progress.ExpansionCleared, ChallengesOpen);
        return new MoonfallLevelResult(won, score, bonus, bonus > 0 && !wasAced, newBest, unlocked);
    }

    // ---- Challenges ----

    /// <summary>Whether challenges are open (<see cref="MoonfallChallenges.Open"/>).</summary>
    public bool ChallengesOpen => MoonfallChallenges.Open(Progress);

    /// <summary>Each challenge with where it stands.</summary>
    public IReadOnlyList<(MoonfallChallenge Challenge, MoonfallChallengeState State)> ChallengeList() =>
        Challenges.Select(c => (c, ChallengeState(c))).ToList();

    /// <summary>Where a challenge stands; one that runs through a level of a veiled stage is <see cref="MoonfallChallengeState.Veiled"/> once open.</summary>
    private MoonfallChallengeState ChallengeState(MoonfallChallenge challenge)
    {
        var state = MoonfallChallenges.State(challenge, Campaigns, Progress);
        return state is MoonfallChallengeState.Open or MoonfallChallengeState.Done && challenge.LevelIds.Any(LevelVeiled) ? MoonfallChallengeState.Veiled : state;
    }

    /// <summary>
    /// A run of the challenge <paramref name="id"/>, with <paramref name="companion"/> when it lets the player pick (an
    /// available one, or none); null unless it is open (or done) and the pick is available.
    /// </summary>
    public MoonfallChallengeRun? StartChallenge(string id, MoonfallCompanion companion, ulong seed)
    {
        var challenge = Challenges.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal));
        if (challenge is null || ChallengeState(challenge) is not (MoonfallChallengeState.Open or MoonfallChallengeState.Done))
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
