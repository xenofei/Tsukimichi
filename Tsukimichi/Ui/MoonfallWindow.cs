using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Art;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Moonfall's window (feature plan v9, 1.23.0): the game as a whole, from its title through Adventure's map, level
/// select, the companions, Quick Play, the challenges, the duel and the options to the board, its pause menu and its
/// tally. Which screen shows is <see cref="MoonfallScreenFlow"/>'s (Core, tested); what each screen offers comes from
/// <see cref="MoonfallModes"/>, and a finished level is recorded there. The screens are drawn from the game's own UI art
/// and fonts at the approved 1280 × 800 design, and at its 640 × 480 one in a small window (spec-rich2.md §4).
/// <para>
/// The board runs on the engine's fixed 100 Hz clock, fed the frame's time (<see cref="MoonfallGame.Advance"/>), so the
/// frame rate never changes the game. It pauses itself in combat, in duties, in cutscenes, when the window loses focus,
/// when it is collapsed and when it is opened (<see cref="MoonfallPauseState"/>); the pause menu then shows over it, and
/// neither a click that resumes nor one that starts a level ever shoots.
/// </para>
/// <para>
/// Mouse first. The menus also move their focus with the keyboard and, through Dalamud's gamepad navigation, the
/// gamepad, with a visible focus; Esc (or the gamepad's back) goes back, and in play pauses. Every key Moonfall answers
/// is claimed from the game (<see cref="GameKeyClaim"/>), so it never also reaches the game.
/// </para>
/// </summary>
public sealed partial class MoonfallWindow : Window
{
    private const string Id = "###TsukimichiMoonfall";

    /// <summary>The window's least size: the 640 × 480 design (spec-rich2.md §1, the text floors hold there).</summary>
    private const float MinWidthLogical = 640f;

    private const float MinHeightLogical = 480f;

    private readonly MoonfallCampaigns campaigns;
    private readonly Func<MoonfallPauseReason> causes;
    private readonly string progressPath;
    private readonly IPluginLog? log;
    private readonly MoonfallPauseState pause = new();
    private readonly MoonfallProgress progress;
    private readonly MoonfallModes modes;
    private readonly MoonfallScreenFlow flow = new();

    /// <summary>The board's level: its campaign and index (its code "3-3" and its place in Adventure).</summary>
    private MoonfallCampaignKind campaign = MoonfallCampaignKind.Base;

    private int levelIndex;
    private MoonfallGame? game;
    private volatile bool unsaved;
    private int saving;
    private MoonfallProgress? mergedFromDisk;
    private double aim;
    private Theme.StyleScope chrome;
    private int titleFor = -1;
    private MoonfallDrawWatch drawWatch;

    /// <summary>Moonfall's Decoration this frame (<see cref="IMoonfallOptions.Decoration"/>; Full without options).</summary>
    private Flair decoration = Flair.Full;

    /// <param name="campaigns">The shipped levels.</param>
    /// <param name="progress">The account's progress as loaded; the window moves it forward as levels are won.</param>
    /// <param name="progressPath">Where progress is saved (<see cref="MoonfallProgress.PathFor"/>).</param>
    /// <param name="causes">What the game is doing now that pauses the board (combat, duty, cutscene).</param>
    /// <param name="log">Where a failed save is logged; null logs nothing.</param>
    /// <param name="textures">Dalamud's textures, for the board's art (<see cref="MoonfallArtTextures"/>); null draws the stage 1 primitives.</param>
    /// <param name="pluginDirectory">The plugin's folder, holding <c>assets/moonfall/</c>; null draws the primitives.</param>
    /// <param name="data">Dalamud's game data: the game's own UI art, cards and paintings are read from it at runtime (<see cref="MoonfallGameArtTextures"/>); null keeps the interim art.</param>
    /// <param name="fontAtlas">The plugin's font atlas, for the game's own fonts (<see cref="MoonfallFonts"/>); null sets the chrome in the window's font.</param>
    /// <param name="options">Moonfall's options (Peg marks and its hint, Decoration, Reduce motion).</param>
    /// <param name="story">Who the player's story has introduced (the spoiler shield, for the companions); null: everyone.</param>
    /// <param name="shield">The spoiler shield for places (the Far Shore's stages, the scenes); null hides nothing.</param>
    public MoonfallWindow(MoonfallCampaigns campaigns, MoonfallProgress progress, string progressPath, Func<MoonfallPauseReason> causes, IPluginLog? log = null, ITextureProvider? textures = null,
        string? pluginDirectory = null, IDataManager? data = null, IFontAtlas? fontAtlas = null, IMoonfallOptions? options = null, MoonfallStory? story = null, MoonfallShield? shield = null)
        : this(campaigns, progress, progressPath, causes, log,
            textures is not null && pluginDirectory is not null ? new MoonfallArtTextures(textures, log) : null,
            pluginDirectory is not null ? MoonfallArtFiles.Folder(pluginDirectory) : null,
            textures is not null && data is not null && pluginDirectory is not null ? new MoonfallGameArtTextures(textures, data, pluginDirectory, log) : null,
            fontAtlas is not null ? new MoonfallFonts(fontAtlas, log) : null,
            options,
            story,
            shield: shield)
    {
    }

    /// <summary>The window over hosts of its own (the offline renderer brings stand-ins for Dalamud's textures and fonts).</summary>
    internal MoonfallWindow(MoonfallCampaigns campaigns, MoonfallProgress progress, string progressPath, Func<MoonfallPauseReason> causes, IPluginLog? log,
        IMoonfallArtHost<IDalamudTextureWrap>? artHost, string? artFolder, IMoonfallGameArtHost<IDalamudTextureWrap>? gameHost, IMoonfallFonts? fontSource, IMoonfallOptions? options,
        MoonfallStory? story = null, IReadOnlyList<MoonfallChallenge>? challenges = null, Func<string?, long?>? aces = null, MoonfallShield? shield = null)
        : base(Strings.MoonfallTitle + Id, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.campaigns = campaigns ?? throw new ArgumentNullException(nameof(campaigns));
        this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
        this.progressPath = progressPath ?? throw new ArgumentNullException(nameof(progressPath));
        this.causes = causes ?? throw new ArgumentNullException(nameof(causes));
        this.log = log;
        modes = new MoonfallModes(campaigns, progress, story ?? MoonfallStory.Everyone, challenges ?? LoadChallenges(log))
        {
            AceOf = aces ?? MoonfallAces.For,
            Shield = shield ?? MoonfallShield.Open,
        };
        Size = new Vector2(1280f, 840f);
        SizeCondition = ImGuiCond.FirstUseEver;
        RespectCloseHotkey = true;
        InitArt(artHost, artFolder);
        InitRich(gameHost, fontSource, options);
        if (gameArt is not null)
        {
            // A scene set past the player's story (or on a veiled stage) is never drawn: the night sky stands in.
            gameArt.HidesScene = VeilsScene;
        }
    }

    private bool VeilsScene(MoonfallLevel level, MoonfallSceneRecipe recipe) => modes.SceneVeiled(level, recipe.Name);

    /// <summary>
    /// The session whose spoiler shield the placeholders answer to (its hover, and its right-click "Reveal this name");
    /// set by the plugin. Null (the offline renderer) draws the placeholders without the menu.
    /// </summary>
    internal SessionState? ShieldSession { get; set; }

    /// <summary>The shield version the menus' words and the scene veil were made for.</summary>
    private int shieldSeen = int.MinValue;

    /// <summary>Follows the spoiler shield: a reveal or a story step remakes the menus' words and lifts or lays the scene veil.</summary>
    private void FollowShield()
    {
        var version = modes.Shield.Version;
        if (version == shieldSeen)
        {
            return;
        }

        shieldSeen = version;
        gameArt?.VeilChanged();

        // The menus' views and words are made per progress epoch: a shield change remakes them as a win does.
        progressEpoch++;
    }

    private static IReadOnlyList<MoonfallChallenge> LoadChallenges(IPluginLog? log)
    {
        var load = MoonfallChallenges.LoadBuiltIn();
        foreach (var error in load.Errors)
        {
            log?.Warning("Moonfall challenges not loaded: {Error}", error);
        }

        return load.Challenges;
    }

    /// <summary>The keys Moonfall answers, claimed from the game (set by the plugin; null claims nothing).</summary>
    internal GameKeyClaim? Keys { get; set; }

    public override void OnOpen()
    {
        pause.Pause(MoonfallPauseReason.Reopened);
        SoundOpen();
    }

    public override void OnClose()
    {
        SaveInBackground();
        SoundClose();
    }

    public override void PreDraw()
    {
        if (titleFor != Localization.Loc.Version)
        {
            titleFor = Localization.Loc.Version;
            WindowName = Strings.MoonfallTitle + Id;
        }

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(MinWidthLogical, MinHeightLogical) * UiMetrics.UiScale,
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        chrome = Theme.PushNightWindow();
    }

    public override void PostDraw()
    {
        chrome.Dispose();
        chrome = default;
    }

    public override void Draw()
    {
        UiMetrics.ApplyFontScale();
        try
        {
            DrawWindow();
        }
        finally
        {
            ImGui.SetWindowFontScale(1f);
        }
    }

    private void DrawWindow()
    {
        TakeMerged();
        ContentSizeForRender = ImGui.GetContentRegionAvail();
        drawWatch.Drew(ImGui.GetFrameCount());
        decoration = options?.Decoration ?? Flair.Full;
        motion = MoonfallMotion.For(decoration, UiMetrics.ReduceMotion);
        var dt = ImGui.GetIO().DeltaTime;
        menuClock += Math.Clamp(dt, 0f, 0.25f);
        FollowShield();

        var inPlay = flow.Current == MoonfallScreen.Play && game is not null;
        if (inPlay)
        {
            var focused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
            pause.Update(causes() | (focused ? MoonfallPauseReason.None : MoonfallPauseReason.Unfocused));
        }

        SoundFrame(inPlay && pause.Paused);
        HandleKeys();
        // Esc closes the window from the title alone (Dalamud's close key); everywhere else it goes back, or pauses.
        RespectCloseHotkey = flow.Current == MoonfallScreen.Title;
        if (flow.Version != screenVersion)
        {
            screenVersion = flow.Version;
            EnteredScreen();
        }

        switch (flow.Current)
        {
            case MoonfallScreen.Play when game is { } g:
                DrawPlay(g, dt);
                break;

            case MoonfallScreen.Play:
                // The board went (the plugin reloaded its levels, say): back to the title.
                flow.Home();
                break;

            default:
                DrawMenu(flow.Current);
                break;
        }

        // The spoiler shield's right-click menu ("Reveal this name") of the placeholders the screens drew, at the window's root.
        if (ShieldSession is { } session)
        {
            ShieldText.DrawMenu(nameof(MoonfallWindow), session);
        }
    }

    /// <summary>The board this frame: its clock, its events, its end, the board itself, the pause menu and the tally.</summary>
    private void DrawPlay(MoonfallGame g, float dt)
    {
        // Over the tally there is no pause menu: a pause that came while it showed (the window lost focus) lifts once
        // its cause has, so the count-up goes on.
        if (LevelOver(g) && pause.Paused)
        {
            pause.TryResume();
        }

        // Every resume re-arms the board as its start does, so the second press of a double click on Resume (or on
        // the crest) never shoots; and a pause just begun ignores a press outside its panel for as long.
        if (pause.Paused != wasPaused)
        {
            wasPaused = pause.Paused;
            boardArmedAt = ImGui.GetTime() + BoardArmSeconds;
            outsidePress = false;
        }

        // The board's clock runs while it is not paused; after the level ends it runs on for the tally's count-up.
        if (!pause.Paused)
        {
            if (!LevelOver(g))
            {
                FeedFlippers(g);
            }

            if (duel is { } d)
            {
                d.Advance(dt);
            }
            else
            {
                g.Advance(dt);
            }

            boardClock += Math.Clamp(dt, 0f, 0.25f);
        }

        ReadEvents(g);
        FinishIfOver(g);
        if (!richHud)
        {
            // The plain board (Decoration Off, or while the game's art loads) has its numbers in one row above it.
            DrawPlainBar(g);
        }

        DrawBoard(g);
        if (pause.Paused && !LevelOver(g))
        {
            DrawPauseMenu(g);
        }
    }

    /// <summary>Whether the board's level (or duel) has ended: the tally shows.</summary>
    private bool LevelOver(MoonfallGame g) =>
        duel is { } d ? d.Outcome != MoonfallDuelOutcome.Undecided : g.Phase is MoonfallPhase.Won or MoonfallPhase.Lost;

    // ---- The offline renderer's hooks (tools/Tsukimichi.MoonfallRender: the board drawn by this code, without the game) ----

    /// <summary>The seed every new game takes, so a render comes out the same each time; null: the clock's (always, in the plugin).</summary>
    internal ulong? SeedForRender { get; set; }

    /// <summary>The game on the board.</summary>
    internal MoonfallGame? GameForRender => game;

    /// <summary>The duel on the board, or null.</summary>
    internal MoonfallDuel? DuelForRender => duel;

    /// <summary>The screens' flow.</summary>
    internal MoonfallScreenFlow FlowForRender => flow;

    /// <summary>The modes the menus read.</summary>
    internal MoonfallModes ModesForRender => modes;

    /// <summary>The pause.</summary>
    internal MoonfallPauseState PauseForRender => pause;

    /// <summary>The aim, set by the renderer where the mouse would set it.</summary>
    internal double AimForRender
    {
        get => aim;
        set => aim = value;
    }

    /// <summary>The board's clock.</summary>
    internal double ClockForRender => boardClock;

    /// <summary>Whether the art has settled: the chrome, the level's scene at its tier (or failed), and the menus' backdrops.</summary>
    internal bool ArtSettledForRender => gameArt is null
        || (gameArt.ChromeTexture is not null && art?.Atlas is not null && (flow.Current != MoonfallScreen.Play || gameArt.SceneSettled) && MenuArtSettled);

    /// <summary>A style shot's ribbon, as the event would place it.</summary>
    internal void RibbonForRender(string title, string value)
    {
        if (game is { } g)
        {
            AddRibbon(g, title, value);
        }
    }

    // ---- Events ----

    private void ReadEvents(MoonfallGame g)
    {
        while (g.TryReadEvent(out var e))
        {
            ArtEvent(e);
            NoteMomentEvent(e);
            NotePowerEvent(e);
            SoundEvent(e);
            switch (e.Kind)
            {
                case MoonfallEventKind.PegHit:
                    AddPopup(e.Peg, e.Value, e.X, e.Y);
                    break;

                case MoonfallEventKind.PegCleared or MoonfallEventKind.StuckClear:
                    AddRing(e.X, e.Y);
                    break;

                case MoonfallEventKind.FreeBall:
                    freeBallUntil = boardClock + FreeBallSeconds;
                    break;
            }
        }

        SoundFlush(g);
    }

    /// <summary>
    /// Saves unsaved progress on a worker, one save at a time. A failed save keeps it unsaved, so the next level ended,
    /// closing the window or unloading the plugin (<see cref="SaveNow"/>) tries again. Another client's further progress,
    /// merged in by the save, shows on the next frame.
    /// </summary>
    private void SaveInBackground()
    {
        if (!unsaved || Interlocked.Exchange(ref saving, 1) == 1)
        {
            return;
        }

        unsaved = false;
        var snapshot = progress.Copy();
        _ = Task.Run(() =>
        {
            try
            {
                Save(snapshot);
            }
            finally
            {
                Volatile.Write(ref saving, 0);
            }
        });
    }

    /// <summary>Saves unsaved progress now, on this thread (the window closing, the plugin unloading). Never throws.</summary>
    public void SaveNow()
    {
        if (!unsaved || Interlocked.Exchange(ref saving, 1) == 1)
        {
            return;
        }

        unsaved = false;
        try
        {
            Save(progress.Copy());
        }
        finally
        {
            Volatile.Write(ref saving, 0);
        }
    }

    private void Save(MoonfallProgress snapshot)
    {
        try
        {
            var warnings = new List<string>();
            var merged = MoonfallProgress.Record(progressPath, snapshot, warnings);
            foreach (var warning in warnings)
            {
                log?.Warning("Moonfall progress: {Warning}", warning);
            }

            Volatile.Write(ref mergedFromDisk, merged);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            unsaved = true;
            log?.Warning(ex, "Moonfall progress could not be saved; it is tried again with the next level ended and when the window closes");
        }
    }

    /// <summary>The progress a save merged with the file (another client may be further on), taken in on the draw thread.</summary>
    private void TakeMerged()
    {
        if (Interlocked.Exchange(ref mergedFromDisk, null) is { } merged && progress.Absorb(merged))
        {
            progressEpoch++;
        }
    }

    // ---- The plain board's bar (Decoration Off, or while the game's art loads): one row, never more ----

    private string ballsText = string.Empty;
    private int ballsFor = -1;
    private string orangesText = string.Empty;
    private int orangesFor = -1;
    private string multiplierText = string.Empty;
    private int multiplierFor = -1;
    private string scoreText = "0";
    private long scoreFor;
    private int languageFor = -1;

    private void RefreshBarText(MoonfallGame g)
    {
        if (languageFor != Localization.Loc.Version)
        {
            languageFor = Localization.Loc.Version;
            ballsFor = orangesFor = multiplierFor = -1;
        }

        // In a duel the balls are the shooter's (the board's count is both sides' together).
        if (ballsFor != TubeBalls(g))
        {
            ballsFor = TubeBalls(g);
            ballsText = string.Format(CultureInfo.CurrentCulture, Strings.MoonfallBallsFormat, ballsFor);
        }

        if (orangesFor != g.OrangesLeft)
        {
            orangesFor = g.OrangesLeft;
            orangesText = string.Format(CultureInfo.CurrentCulture, Strings.MoonfallOrangesFormat, orangesFor);
        }

        if (multiplierFor != g.Multiplier)
        {
            multiplierFor = g.Multiplier;
            multiplierText = string.Format(CultureInfo.CurrentCulture, Strings.MoonfallMultiplierFormat, multiplierFor);
        }

        // Over the tally the Ace bonus is in the total, so the score plate counts to the same number.
        var shown = duel?.ShownScore(MoonfallDuel.PlayerSide) ?? (g.ShownScore + (finished ? aceBonus : 0));
        if (scoreFor != shown)
        {
            scoreFor = shown;
            scoreText = scoreFor.ToString("N0", CultureInfo.CurrentCulture);
        }
    }

    /// <summary>
    /// The plain board's one-row bar (the rich chrome shows all of this on the board itself): the level, the balls, the
    /// oranges left, the multiplier and the score, drawn as text over a strip above the board so it never wraps.
    /// </summary>
    private float DrawPlainBar(MoonfallGame g)
    {
        RefreshBarText(g);
        RefreshHudText(g);
        var dl = ImGui.GetWindowDrawList();
        var at = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = ImGui.GetFrameHeight();
        var gap = UiMetrics.Px(14f);
        var y = at.Y + ((height - ImGui.GetTextLineHeight()) * 0.5f);
        var x = at.X;
        var right = at.X + width;
        // Pause at the right (the mouse's way in; Esc too), then the score.
        // Pause, or Resume while paused; none over the tally.
        if (!LevelOver(g))
        {
            var pauseWidth = MathF.Max(ImGui.CalcTextSize(Strings.MoonfallPause).X, ImGui.CalcTextSize(Strings.MoonfallResume).X) + (2f * ImGui.GetStyle().FramePadding.X);
            ImGui.SetCursorScreenPos(new Vector2(right - pauseWidth, at.Y));
            if (ImGui.Button(PauseButtonLabel(pause.Paused), new Vector2(pauseWidth, height)))
            {
                TogglePause(g);
            }

            right -= pauseWidth + gap;
        }

        // A duel shows both sides' scores (the opponent's, then yours) and, first on the left, whose shot it is.
        var turn = string.Empty;
        if (duel is { } d)
        {
            RefreshDuelNames(d);
            RefreshPlainDuel(d);
            var youWidth = ImGui.CalcTextSize(plainDuelYou).X;
            dl.AddText(new Vector2(right - youWidth, y), Theme.U32(Theme.Gold), plainDuelYou);
            right -= youWidth + gap;
            var foeWidth = ImGui.CalcTextSize(plainDuelFoe).X;
            dl.AddText(new Vector2(right - foeWidth, y), Theme.U32(Theme.Surface.Text), plainDuelFoe);
            right -= foeWidth + gap;
            turn = DuelTurnText(d);
        }
        else
        {
            var scoreWidth = ImGui.CalcTextSize(scoreText).X;
            dl.AddText(new Vector2(right - scoreWidth, y), Theme.U32(Theme.Gold), scoreText);
            right -= scoreWidth + gap;
        }

        dl.PushClipRect(at, new Vector2(right, at.Y + height), true);
        if (turn.Length > 0)
        {
            dl.AddText(new Vector2(x, y), Theme.U32(Theme.Gold), turn);
            x += ImGui.CalcTextSize(turn).X + gap;
        }

        foreach (var part in (ReadOnlySpan<string>)[stageText, playLevel is null ? string.Empty : PlayLevelName(playLevel), ballsText, orangesText, multiplierText])
        {
            if (part.Length == 0)
            {
                continue;
            }

            dl.AddText(new Vector2(x, y), Theme.U32(Theme.Surface.Text), part);
            x += ImGui.CalcTextSize(part).X + gap;
        }

        dl.PopClipRect();
        ImGui.SetCursorScreenPos(at);
        ImGui.Dummy(new Vector2(width, height));
        return height;
    }

    private string plainDuelYou = string.Empty;
    private string plainDuelFoe = string.Empty;
    private (long You, long Foe, MoonfallDuel? Duel, int Language) plainDuelFor;

    /// <summary>The plain bar's two duel scores, "LOUISOIX 4,200" and "YOU 3,900", made when a score changes.</summary>
    private void RefreshPlainDuel(MoonfallDuel d)
    {
        var key = (d.ShownScore(MoonfallDuel.PlayerSide), d.ShownScore(MoonfallDuel.OpponentSide), d, Localization.Loc.Version);
        if (key == plainDuelFor)
        {
            return;
        }

        plainDuelFor = key;
        var c = CultureInfo.CurrentCulture;
        plainDuelYou = duelYou + " " + key.Item1.ToString("N0", c);
        plainDuelFoe = duelFoe + " " + key.Item2.ToString("N0", c);
    }

    private string pauseButtonLabel = string.Empty;
    private string resumeButtonLabel = string.Empty;
    private int pauseButtonFor = -1;

    /// <summary>The plain bar's Pause (Resume while paused), with its id, made once per language.</summary>
    private string PauseButtonLabel(bool paused)
    {
        if (pauseButtonFor != Localization.Loc.Version)
        {
            pauseButtonFor = Localization.Loc.Version;
            pauseButtonLabel = Strings.MoonfallPause + "##moonfallPause";
            resumeButtonLabel = Strings.MoonfallResume + "##moonfallPause";
        }

        return paused ? resumeButtonLabel : pauseButtonLabel;
    }
}
