using System;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Moonfall;

namespace Tsukimichi.Ui;

/// <summary>
/// Moonfall's way through its screens (<see cref="MoonfallScreenFlow"/>): what Back does, the keys it claims from the
/// game, how each mode starts the board, what is recorded when a level ends (<see cref="MoonfallModes.FinishLevel"/>,
/// <see cref="MoonfallModes.FinishChallenge"/>, <see cref="MoonfallModes.FinishDuel"/>) and the tally's ways on.
/// </summary>
public sealed partial class MoonfallWindow
{
    /// <summary>After the board appears, a press that begins sooner than this is not taken: a double click on Play never shoots.</summary>
    private const double BoardArmSeconds = 0.3;

    private int screenVersion = -1;
    private double menuClock;

    /// <summary>Moves on whenever the progress changes (a level recorded, another client's merged in): the menus rebuild their views.</summary>
    private int progressEpoch;

    private MoonfallPlayKind playKind;
    private MoonfallStart? playStart;
    private MoonfallChallengeRun? challengeRun;
    private MoonfallChallengeStatus runStatus;
    private MoonfallDuel? duel;
    private (string LevelId, MoonfallCompanion Companion, MoonfallCompanion Opponent, MoonfallAiDifficulty Difficulty) duelSetup;
    private MoonfallLevel? playLevel;
    private MoonfallLevelResult? lastResult;
    private bool finished;
    private double boardArmedAt;

    /// <summary>The companion picked for a "Your Pick" stage, kept for its next level.</summary>
    private MoonfallCompanion adventurePick = MoonfallCompanion.None;

    /// <summary>Esc and the gamepad's back and Start: back a screen, or pause and resume the board. Claims the keys it answers.</summary>
    private void HandleKeys()
    {
        var hasKeys = Keyboard.WindowHasKeys();
        if (hasKeys && flow.Current != MoonfallScreen.Title)
        {
            Keys?.ClaimBack();
        }

        // The menus (and the pause menu) move their focus with the arrows, Tab, Enter and Space: the game must not see them.
        var menu = flow.Current != MoonfallScreen.Play || (game is { } g && pause.Paused && !LevelOver(g));
        if (hasKeys && menu)
        {
            Keys?.ClaimNavigation();
        }

        if (ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel))
        {
            return;
        }

        if (flow.Current == MoonfallScreen.Play && game is { } board && Keyboard.StartPressed())
        {
            Keys?.ClaimBack();
            TogglePause(board);
            return;
        }

        if (flow.Current == MoonfallScreen.Title || !Keyboard.BackPressed())
        {
            return;
        }

        Back();
    }

    /// <summary>Back, from a key or a Back button.</summary>
    private void Back()
    {
        var over = game is { } g && LevelOver(g);
        switch (flow.Back(pause.Paused, over))
        {
            case MoonfallBack.Pause:
                pause.Pause(MoonfallPauseReason.Player);
                SoundClick();
                break;

            case MoonfallBack.Resume:
                pause.TryResume();
                SoundClick();
                break;

            case MoonfallBack.Leave:
                EndBoard();
                SoundClick();
                break;

            case MoonfallBack.Screen:
                SoundClick();
                break;

            case MoonfallBack.Close:
                IsOpen = false;
                break;
        }
    }

    private void TogglePause(MoonfallGame g)
    {
        if (LevelOver(g))
        {
            return;
        }

        if (pause.Paused)
        {
            pause.TryResume();
        }
        else
        {
            pause.Pause(MoonfallPauseReason.Player);
        }

        SoundClick();
    }

    /// <summary>Opens a screen from a menu (a click or an activation); a locked one says why instead.</summary>
    private void Open(MoonfallScreen screen)
    {
        if (flow.Open(screen, modes.ChallengesOpen))
        {
            SoundClick();
        }
    }

    // ---- The offline renderer's way in ----

    /// <summary>The content area the last frame drew in (the renderer sizes the window so this is the design's).</summary>
    internal Vector2 ContentSizeForRender { get; private set; }

    /// <summary>Shows a screen as a player would reach it, for the offline renderer; false for a name it does not know.</summary>
    internal bool ShowForRender(string name)
    {
        flow.Home();
        switch (name)
        {
            case "title":
                return true;
            case "map":
                mapCampaign = MoonfallCampaignKind.Base;
                mapStage = -1;
                Open(MoonfallScreen.Map);
                return true;
            case "levels":
                mapCampaign = MoonfallCampaignKind.Base;
                mapStage = -1;
                Open(MoonfallScreen.Map);
                RefreshMenuViews();
                EnsureMapSelection();
                OpenLevels(mapStage, -1);
                return true;
            case "characters":
                Open(MoonfallScreen.Characters);
                return true;
            case "quickplay":
                Open(MoonfallScreen.QuickPlay);
                return true;
            case "challenges":
                Open(MoonfallScreen.Challenges);
                return true;
            case "duel":
                Open(MoonfallScreen.Duel);
                return true;
            case "options":
                Open(MoonfallScreen.Options);
                return true;
            case "duelhud":
                RefreshMenuViews();
                return continuePlace is { } at && PlayDuel(MoonfallStages.LevelId(at.Campaign, at.Index), MoonfallCompanion.Cid, MoonfallCompanion.Louisoix, MoonfallAiDifficulty.Adept);
            case "play":
                RefreshMenuViews();
                Continue();
                return flow.Current == MoonfallScreen.Play;
            default:
                return false;
        }
    }

    // ---- Starting the board ----

    private ulong NewSeed() => SeedForRender ?? (ulong)Stopwatch.GetTimestamp();

    /// <summary>Adventure's level <paramref name="index"/> of <paramref name="kind"/> (<paramref name="picked"/> on a "Your Pick" stage).</summary>
    private bool PlayAdventure(MoonfallCampaignKind kind, int index, MoonfallCompanion picked = MoonfallCompanion.None)
    {
        if (modes.Adventure(kind, index, picked) is not { } start)
        {
            return false;
        }

        if (MoonfallStages.StageOf(kind, index) is { PlayerPicks: true })
        {
            adventurePick = start.Companion;
        }

        StartBoard(MoonfallPlayKind.Adventure, start, start.Create(NewSeed()), null, null);
        return true;
    }

    private bool PlayQuick(string levelId, MoonfallCompanion companion)
    {
        if (modes.QuickPlay(levelId, companion) is not { } start)
        {
            return false;
        }

        StartBoard(MoonfallPlayKind.QuickPlay, start, start.Create(NewSeed()), null, null);
        return true;
    }

    private bool PlayChallenge(string id, MoonfallCompanion companion)
    {
        if (modes.StartChallenge(id, companion, NewSeed()) is not { } run)
        {
            return false;
        }

        StartRunLevel(run);
        return true;
    }

    private void StartRunLevel(MoonfallChallengeRun run)
    {
        if (run.Challenge.Kind == MoonfallChallengeKind.Duel)
        {
            var d = run.StartDuel();
            StartBoard(MoonfallPlayKind.Challenge, null, d.Game, run, d);
        }
        else
        {
            StartBoard(MoonfallPlayKind.Challenge, null, run.StartLevel(), run, null);
        }
    }

    private bool PlayDuel(string levelId, MoonfallCompanion companion, MoonfallCompanion opponent, MoonfallAiDifficulty difficulty)
    {
        if (modes.StartDuel(levelId, companion, opponent, difficulty, NewSeed()) is not { } d)
        {
            return false;
        }

        duelSetup = (levelId, companion, opponent, difficulty);
        StartBoard(MoonfallPlayKind.Duel, null, d.Game, null, d);
        return true;
    }

    /// <summary>The board starts: its game, its mode, a clean slate of effects, the pause lifted (unless a cause holds it).</summary>
    private void StartBoard(MoonfallPlayKind kind, MoonfallStart? start, MoonfallGame board, MoonfallChallengeRun? run, MoonfallDuel? newDuel)
    {
        playKind = kind;
        playStart = start;
        challengeRun = run;
        runStatus = MoonfallChallengeStatus.Playing;
        duel = newDuel;
        game = board;
        playLevel = board.Level;
        if (MoonfallStages.TryPlace(board.Level.Id, out var place))
        {
            campaign = place.Campaign;
            levelIndex = place.Index;
        }
        else
        {
            campaign = MoonfallCampaignKind.Base;
            levelIndex = 0;
        }

        finished = false;
        lastResult = null;
        wonAced = false;
        wonNewBest = false;
        wonPreviousBest = 0;
        aceBonus = 0;
        ClearEffects();
        ClearPowerEffects();
        ClearMoments();
        SoundNewLevel();
        pause.TryResume();
        boardArmedAt = ImGui.GetTime() + BoardArmSeconds;
        pauseHolds = default;
        flow.Play(kind);
    }

    /// <summary>Whether the board takes presses yet (not in the first moments after it appears).</summary>
    private bool BoardArmed => ImGui.GetTime() >= boardArmedAt;

    /// <summary>Whether the player aims now: always, but in a duel only on the player's own turn (the opponent aims its own).</summary>
    private bool PlayerAims => duel is not { } d || d.PlayersTurn;

    /// <summary>The board's shooter: the duel, which keeps its sides and turns, or the level's game.</summary>
    private IMoonfallShooter Shooter(MoonfallGame g) => duel is { } d ? d : g;

    /// <summary>The balls the tube shows: the player's own in a duel, else the level's.</summary>
    private int TubeBalls(MoonfallGame g) => duel?.BallsLeft(MoonfallDuel.PlayerSide) ?? g.BallsLeft;

    /// <summary>The board's level again, from the start (Restart held, Replay, Try again).</summary>
    private void Restart()
    {
        switch (playKind)
        {
            case MoonfallPlayKind.Challenge when challengeRun is { } run:
                if (run.Status == MoonfallChallengeStatus.Playing)
                {
                    StartRunLevel(run);
                }
                else
                {
                    PlayChallenge(run.Challenge.Id, run.Companion);
                }

                break;

            case MoonfallPlayKind.Duel:
                PlayDuel(duelSetup.LevelId, duelSetup.Companion, duelSetup.Opponent, duelSetup.Difficulty);
                break;

            default:
                if (playStart is { } start)
                {
                    StartBoard(playKind, start, start.Create(NewSeed()), null, null);
                }

                break;
        }
    }

    /// <summary>The board is left for its mode's screen (Leave held, the tally's Map, Back over the tally).</summary>
    private void LeaveBoard()
    {
        flow.Leave();
        EndBoard();
    }

    private void EndBoard()
    {
        SoundNewLevel();
        pause.TryResume();
        game = null;
        duel = null;
        challengeRun = null;
        playStart = null;
    }

    // ---- The end of a level ----

    /// <summary>Records the level's end once: Adventure and Quick Play through FinishLevel, a challenge's run, a duel.</summary>
    private void FinishIfOver(MoonfallGame g)
    {
        if (finished || !LevelOver(g))
        {
            return;
        }

        finished = true;
        if (duel is { } d)
        {
            if (challengeRun is { } duelRun)
            {
                runStatus = duelRun.Finish(d);
                if (runStatus != MoonfallChallengeStatus.Playing)
                {
                    modes.FinishChallenge(duelRun);
                }
            }
            else
            {
                modes.FinishDuel(d);
            }
        }
        else if (challengeRun is { } run)
        {
            runStatus = run.Finish(g);
            if (runStatus != MoonfallChallengeStatus.Playing)
            {
                modes.FinishChallenge(run);
            }
        }
        else if (playStart is { } start)
        {
            var before = progress.Best(start.LevelId);
            var result = modes.FinishLevel(start, g);
            lastResult = result;
            aceBonus = result.AceBonus;
            wonAced = result.Won && result.AceBonus > 0;
            wonNewBest = result.NewBest && before > 0;
            wonPreviousBest = before;
        }

        progressEpoch++;
        unsaved = true;
        SaveInBackground();
    }

    /// <summary>The next level the tally offers, or null: Adventure's next reached level, Quick Play's next reached one, a challenge run's next level.</summary>
    private bool HasNext(MoonfallGame g)
    {
        switch (playKind)
        {
            case MoonfallPlayKind.Adventure:
                return g.Phase == MoonfallPhase.Won && levelIndex + 1 < MoonfallStages.LevelCount(campaign) && modes.Slot(campaign, levelIndex + 1).Reached;
            case MoonfallPlayKind.QuickPlay:
                return NextQuickLevel() is not null;
            case MoonfallPlayKind.Challenge:
                return challengeRun is { Status: MoonfallChallengeStatus.Playing };
            default:
                return false;
        }
    }

    private string? NextQuickLevel()
    {
        if (playLevel is null)
        {
            return null;
        }

        var levels = QuickLevels();
        for (var i = 0; i < levels.Count - 1; i++)
        {
            if (string.Equals(levels[i].Id, playLevel.Id, StringComparison.Ordinal))
            {
                return levels[i + 1].Id;
            }
        }

        return null;
    }

    /// <summary>The tally's Next.</summary>
    private void Next()
    {
        switch (playKind)
        {
            case MoonfallPlayKind.Adventure:
                var pick = MoonfallStages.StageOf(campaign, levelIndex + 1) is { PlayerPicks: true }
                    ? (adventurePick != MoonfallCompanion.None ? adventurePick : MoonfallCompanions.Carrying(game?.Power ?? MoonfallPower.None))
                    : MoonfallCompanion.None;
                if (!PlayAdventure(campaign, levelIndex + 1, pick))
                {
                    LeaveBoard();
                }

                break;

            case MoonfallPlayKind.QuickPlay when NextQuickLevel() is { } id && playStart is { } start:
                PlayQuick(id, start.Companion);
                break;

            case MoonfallPlayKind.Challenge when challengeRun is { Status: MoonfallChallengeStatus.Playing } run:
                StartRunLevel(run);
                break;

            default:
                LeaveBoard();
                break;
        }
    }

    /// <summary>The level the tally's Next would start, built ahead so its board opens with its scene (null: none).</summary>
    private MoonfallLevel? NextLevelToWarm(MoonfallGame g)
    {
        if (!HasNext(g))
        {
            return null;
        }

        return playKind switch
        {
            MoonfallPlayKind.Adventure => modes.Slot(campaign, levelIndex + 1).Level,
            MoonfallPlayKind.QuickPlay => NextQuickLevel() is { } id ? campaigns.Find(id) : null,
            MoonfallPlayKind.Challenge => challengeRun?.Current,
            _ => null,
        };
    }
}
