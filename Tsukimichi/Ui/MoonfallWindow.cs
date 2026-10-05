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
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Art;

namespace Tsukimichi.Ui;

/// <summary>
/// Moonfall's window (feature plan v9, 1.23.0, stage 1): the first playable board over the pure engine
/// (<see cref="MoonfallGame"/>). A bar on top (the level, balls, orange pegs left, the multiplier, the score, Pause and
/// Restart), the 800×600 board scaled to the window below it, letterboxed, and the end-of-level tally over the board.
/// Aim with the mouse, click the board to shoot; the aim guide shows the first stretch of the path.
/// <para>
/// The board runs on the engine's fixed 100 Hz clock, fed the frame's time (<see cref="MoonfallGame.Advance"/>), so the
/// frame rate never changes the game. It pauses itself in combat, in duties, in cutscenes, when the window loses focus
/// and when it is opened (<see cref="MoonfallPauseState"/>); a click on the board resumes it without shooting. Reduce
/// motion keeps the camera still (no Full Moon zoom) and drops the clearing rings; the glows are Full's only.
/// </para>
/// <para>
/// Everything is drawn as placeholder shapes in Tsukimichi's palette (circles for pegs, bars for bricks, a box for the
/// bucket); the art track replaces them (plan v9 G8). Powers, style shots and modes come in later stages and hook into
/// the engine's events.
/// </para>
/// </summary>
public sealed partial class MoonfallWindow : Window
{
    private const string Id = "###TsukimichiMoonfall";
    private const float MinWidthLogical = 440f;
    private const float MinHeightLogical = 400f;

    /// <summary>The score's size against the body text.</summary>
    private const float ScoreScale = 1.35f;

    private readonly MoonfallCampaigns campaigns;
    private readonly Func<MoonfallPauseReason> causes;
    private readonly string progressPath;
    private readonly IPluginLog? log;
    private readonly MoonfallPauseState pause = new();
    private readonly MoonfallProgress progress;
    private readonly MoonfallCampaignKind campaign = MoonfallCampaignKind.Base;

    private MoonfallGame? game;
    private volatile bool unsaved;
    private int saving;
    private MoonfallProgress? mergedFromDisk;
    private int levelIndex;
    private double aim;
    private Theme.StyleScope chrome;
    private int titleFor = -1;

    /// <summary>A question waiting over the board (Restart, or leaving for another level): <see cref="NoChoice"/> for none.</summary>
    private int pendingLevel = NoChoice;

    private const int NoChoice = -2;
    private const int RestartChoice = -1;

    /// <param name="campaigns">The shipped levels.</param>
    /// <param name="progress">The account's progress as loaded; the window moves it forward as levels are won.</param>
    /// <param name="progressPath">Where progress is saved (<see cref="MoonfallProgress.PathFor"/>).</param>
    /// <param name="causes">What the game is doing now that pauses the board (combat, duty, cutscene).</param>
    /// <param name="log">Where a failed save is logged; null logs nothing.</param>
    /// <param name="textures">Dalamud's textures, for the board's art (<see cref="MoonfallArtTextures"/>); null draws the stage 1 primitives.</param>
    /// <param name="pluginDirectory">The plugin's folder, holding <c>assets/moonfall/</c>; null draws the primitives.</param>
    /// <param name="data">Dalamud's game data: the game's own UI art, cards and paintings are read from it at runtime (<see cref="MoonfallGameArtTextures"/>); null keeps the interim art.</param>
    /// <param name="fontAtlas">The plugin's font atlas, for the game's own fonts (<see cref="MoonfallFonts"/>); null sets the chrome in the window's font.</param>
    /// <param name="options">Moonfall's options (Peg marks and its hint).</param>
    public MoonfallWindow(MoonfallCampaigns campaigns, MoonfallProgress progress, string progressPath, Func<MoonfallPauseReason> causes, IPluginLog? log = null, ITextureProvider? textures = null,
        string? pluginDirectory = null, IDataManager? data = null, IFontAtlas? fontAtlas = null, IMoonfallOptions? options = null)
        : this(campaigns, progress, progressPath, causes, log,
            textures is not null && pluginDirectory is not null ? new MoonfallArtTextures(textures, log) : null,
            pluginDirectory is not null ? MoonfallArtFiles.Folder(pluginDirectory) : null,
            textures is not null && data is not null && pluginDirectory is not null ? new MoonfallGameArtTextures(textures, data, pluginDirectory, log) : null,
            fontAtlas is not null ? new MoonfallFonts(fontAtlas, log) : null,
            options)
    {
    }

    /// <summary>The window over hosts of its own (the offline renderer brings stand-ins for Dalamud's textures and fonts).</summary>
    internal MoonfallWindow(MoonfallCampaigns campaigns, MoonfallProgress progress, string progressPath, Func<MoonfallPauseReason> causes, IPluginLog? log,
        IMoonfallArtHost<IDalamudTextureWrap>? artHost, string? artFolder, IMoonfallGameArtHost<IDalamudTextureWrap>? gameHost, IMoonfallFonts? fontSource, IMoonfallOptions? options)
        : base(Strings.MoonfallTitle + Id, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.campaigns = campaigns ?? throw new ArgumentNullException(nameof(campaigns));
        this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
        this.progressPath = progressPath ?? throw new ArgumentNullException(nameof(progressPath));
        this.causes = causes ?? throw new ArgumentNullException(nameof(causes));
        this.log = log;
        Size = new Vector2(820f, 720f);
        SizeCondition = ImGuiCond.FirstUseEver;
        RespectCloseHotkey = true;

        // The furthest level reached is the one waiting.
        levelIndex = Math.Max(0, campaigns.Playable(campaign, progress) - 1);
        InitArt(artHost, artFolder);
        InitRich(gameHost, fontSource, options);
    }

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
        var levels = campaigns[campaign].Levels;
        if (levels.Count == 0)
        {
            ImGui.TextWrapped(Strings.MoonfallNoLevels);
            return;
        }

        TakeMerged();
        game ??= NewGame(levelIndex);
        var focused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        pause.Update(causes() | (focused ? MoonfallPauseReason.None : MoonfallPauseReason.Unfocused));
        SoundFrame();

        DrawBar(game);
        if (pendingLevel != NoChoice)
        {
            DrawQuestion();
        }

        if (!pause.Paused)
        {
            FeedFlippers(game);
            var dt = ImGui.GetIO().DeltaTime;
            game.Advance(dt);
            boardClock += Math.Clamp(dt, 0f, 0.25f);
        }

        ReadEvents(game);
        DrawBoard(game);
    }

    private MoonfallGame NewGame(int index)
    {
        var levels = campaigns[campaign].Levels;
        levelIndex = Math.Clamp(index, 0, levels.Count - 1);
        ClearEffects();
        ClearPowerEffects();
        ClearMoments();
        SoundNewLevel();
        // A fresh board each time: the seed only has to differ between plays, the engine does the rest.
        return new MoonfallGame(levels[levelIndex], levelIndex + 1, SeedForRender ?? (ulong)Stopwatch.GetTimestamp(), power: PowerFor(levelIndex));
    }

    // ---- The offline renderer's hooks (tools/Tsukimichi.MoonfallRender: the board drawn by this code, without the game) ----

    /// <summary>The seed every new game takes, so a render comes out the same each time; null: the clock's (always, in the plugin).</summary>
    internal ulong? SeedForRender { get; set; }

    /// <summary>The game on the board.</summary>
    internal MoonfallGame? GameForRender => game;

    /// <summary>The aim, set by the renderer where the mouse would set it.</summary>
    internal double AimForRender
    {
        get => aim;
        set => aim = value;
    }

    /// <summary>The board's clock.</summary>
    internal double ClockForRender => boardClock;

    /// <summary>Whether the board's art has settled: the chrome, and the level's scene built or failed (nothing still loading).</summary>
    internal bool ArtSettledForRender => gameArt is null
        || (gameArt.ChromeTexture is not null && gameArt.SceneState is not Core.Moonfall.Art.MoonfallSceneState.Building && art?.Atlas is not null);

    /// <summary>A style shot's ribbon, as the event would place it.</summary>
    internal void RibbonForRender(string title, string value)
    {
        if (game is { } g)
        {
            AddRibbon(g, title, value);
        }
    }

    /// <summary>Whether leaving the level now would lose something: a shot taken or a ball in play, and the level not over.</summary>
    private static bool UnderWay(MoonfallGame g) =>
        g.Phase is not (MoonfallPhase.Won or MoonfallPhase.Lost) && (g.Phase != MoonfallPhase.Aiming || g.Score > 0 || g.BallsLeft != MoonfallRules.BallsPerLevel);

    /// <summary>Restarts, or moves to <paramref name="index"/>; asks first while a level is under way (<see cref="DrawQuestion"/>).</summary>
    private void Choose(int index)
    {
        if (game is { } g && UnderWay(g))
        {
            pendingLevel = index;
            return;
        }

        Go(index);
    }

    private void Go(int index)
    {
        pendingLevel = NoChoice;
        game = NewGame(index == RestartChoice ? levelIndex : index);
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

                case MoonfallEventKind.LevelWon:
                    NoteWin(g);
                    RecordWin();
                    break;
            }
        }

        SoundFlush(g);
    }

    /// <summary>The level just won moves the account's furthest level on: in memory now, on disk off the frame.</summary>
    private void RecordWin()
    {
        var cleared = levelIndex + 1;
        if (cleared > progress.Cleared(campaign))
        {
            if (campaign == MoonfallCampaignKind.Expansion)
            {
                progress.ExpansionCleared = cleared;
            }
            else
            {
                progress.BaseCleared = cleared;
            }

            unsaved = true;
        }

        // A save that failed before is tried again with this win.
        SaveInBackground();
    }

    /// <summary>
    /// Saves unsaved progress on a worker, one save at a time. A failed save keeps it unsaved, so the next win, closing
    /// the window or unloading the plugin (<see cref="SaveNow"/>) tries again. Another client's further progress, merged
    /// in by the save, shows on the next frame.
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
            log?.Warning(ex, "Moonfall progress could not be saved; it is tried again with the next level won and when the window closes");
        }
    }

    /// <summary>The progress a save merged with the file (another client may be further on), taken in on the draw thread.</summary>
    private void TakeMerged()
    {
        if (Interlocked.Exchange(ref mergedFromDisk, null) is { } merged)
        {
            progress.Absorb(merged);
        }
    }

    // ---- The bar ----

    private string levelText = string.Empty;
    private int levelTextFor = -1;
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
            levelTextFor = ballsFor = orangesFor = multiplierFor = -1;
        }

        if (levelTextFor != levelIndex)
        {
            levelTextFor = levelIndex;
            levelText = LevelLabel(levelIndex);
        }

        if (ballsFor != g.BallsLeft)
        {
            ballsFor = g.BallsLeft;
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

        if (scoreFor != g.ShownScore)
        {
            scoreFor = g.ShownScore;
            scoreText = scoreFor.ToString("N0", CultureInfo.CurrentCulture);
        }
    }

    private string LevelLabel(int index)
    {
        var levels = campaigns[campaign].Levels;
        return string.Format(CultureInfo.CurrentCulture, Strings.MoonfallLevelFormat, index + 1, levels[index].Name);
    }

    private void DrawBar(MoonfallGame g)
    {
        RefreshBarText(g);
        var gap = UiMetrics.Px(14f);

        // The level picker: every level won and the next one.
        ImGui.SetNextItemWidth(MathF.Min(UiMetrics.Px(220f), ImGui.GetContentRegionAvail().X * 0.45f));
        if (ImGui.BeginCombo("##moonfallLevel", levelText))
        {
            var playable = campaigns.Playable(campaign, progress);
            for (var i = 0; i < playable; i++)
            {
                if (ImGui.Selectable(LevelLabel(i), i == levelIndex) && i != levelIndex)
                {
                    SoundClick();
                    Choose(i);
                }
            }

            ImGui.EndCombo();
        }

        // The rich chrome shows the balls, the oranges, the multiplier, the score and the power's turns on the board itself.
        if (!richHud)
        {
            ImGui.SameLine(0f, gap);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(ballsText);
            ImGui.SameLine(0f, gap);
            Swatch(PegInk(PegColour.Orange));
            ImGui.SameLine(0f, UiMetrics.Px(5f));
            ImGui.TextUnformatted(orangesText);
            ImGui.SameLine(0f, gap);
            using (Theme.PushText(g.Multiplier > 1 ? Theme.Gold : Theme.Surface.TextSecondary))
            {
                ImGui.TextUnformatted(multiplierText);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.MoonfallMultiplierTooltip);
            }
        }

        DrawPowerBar(g, gap);
        var leftEnd = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X;

        // The score, then Pause and Restart, at the right.
        var style = ImGui.GetStyle();
        var pauseLabel = pause.Active.HasFlag(MoonfallPauseReason.Player) || pause.AwaitingResume ? Strings.MoonfallResume : Strings.MoonfallPause;
        var pauseWidth = MathF.Max(ImGui.CalcTextSize(Strings.MoonfallPause).X, ImGui.CalcTextSize(Strings.MoonfallResume).X) + (2f * style.FramePadding.X);
        var restartWidth = ImGui.CalcTextSize(Strings.MoonfallRestart).X + (2f * style.FramePadding.X);
        var optionsWidth = options is null ? 0f : ImGui.CalcTextSize(Strings.MoonfallOptions).X + (2f * style.FramePadding.X) + style.ItemSpacing.X;
        var scoreSize = ImGui.GetFontSize() * ScoreScale;
        var scoreWidth = richHud ? 0f : ImGui.CalcTextSize(scoreText).X * ScoreScale;
        var right = ImGui.GetWindowContentRegionMax().X;
        var scoreX = right - restartWidth - style.ItemSpacing.X - pauseWidth - optionsWidth - gap - scoreWidth;
        // On the same row while it fits; in a narrow window the score and the buttons take a row of their own.
        if (scoreX >= leftEnd + gap)
        {
            ImGui.SameLine(scoreX);
        }
        else
        {
            ImGui.SetCursorPosX(MathF.Max(style.WindowPadding.X, scoreX));
        }

        var at = ImGui.GetCursorScreenPos();
        var frame = ImGui.GetFrameHeight();
        if (!richHud)
        {
            ImGui.GetWindowDrawList().AddText(ImGui.GetFont(), scoreSize, new Vector2(at.X, at.Y + ((frame - scoreSize) * 0.5f)), Theme.U32(Theme.Gold), scoreText);
        }

        ImGui.Dummy(new Vector2(MathF.Max(1f, scoreWidth), frame));
        ImGui.SameLine(0f, gap);
        DrawOptions();
        // While combat, a duty or a cutscene holds the pause, Resume is off and says why.
        var held = (pause.Active & (MoonfallPauseReason.Combat | MoonfallPauseReason.Duty | MoonfallPauseReason.Cutscene)) != 0;
        using (ImRaii.Disabled(held))
        {
            if (ImGui.Button(pauseLabel + "##moonfallPause", new Vector2(pauseWidth, 0f)))
            {
                SoundClick();
                if (pause.Paused)
                {
                    pause.TryResume();
                }
                else
                {
                    pause.Pause(MoonfallPauseReason.Player);
                }
            }
        }

        if (held && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(PauseReasonText() ?? Strings.MoonfallPaused, Strings.MoonfallWaitsForIt);
        }

        ImGui.SameLine();
        if (ImGui.Button(Strings.MoonfallRestart + "##moonfallRestart", new Vector2(restartWidth, 0f)))
        {
            SoundClick();
            Choose(RestartChoice);
        }

        DrawPegMarksHint();
        ImGui.Spacing();
    }

    /// <summary>Moonfall's Options: a small menu with Peg marks, the colour-blind assist (decision 27: off by default).</summary>
    private void DrawOptions()
    {
        if (options is null)
        {
            return;
        }

        if (ImGui.Button(Strings.MoonfallOptions + "##moonfallOptions"))
        {
            ImGui.OpenPopup("##moonfallOptionsMenu");
        }

        if (ImGui.BeginPopup("##moonfallOptionsMenu"))
        {
            var marks = options.PegMarks;
            if (ImGui.Checkbox(Strings.MoonfallPegMarks + "##moonfallPegMarks", ref marks))
            {
                options.PegMarks = marks;
                options.PegMarksHintSeen = true;
                options.Save();
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.MoonfallPegMarksTooltip);
            }

            ImGui.EndPopup();
        }

        ImGui.SameLine();
    }

    /// <summary>The one-time hint about Peg marks (decision 27), under the bar until it is answered.</summary>
    private void DrawPegMarksHint()
    {
        if (options is null || options.PegMarksHintSeen)
        {
            return;
        }

        ImGui.AlignTextToFramePadding();
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.TextUnformatted(Strings.MoonfallPegMarksHint);
        }

        ImGui.SameLine(0f, UiMetrics.Px(10f));
        if (ImGui.SmallButton(Strings.MoonfallTurnOn + "##moonfallMarksOn"))
        {
            options.PegMarks = true;
            options.PegMarksHintSeen = true;
            options.Save();
        }

        ImGui.SameLine();
        if (ImGui.SmallButton(Strings.MoonfallNoThanks + "##moonfallMarksNo"))
        {
            options.PegMarksHintSeen = true;
            options.Save();
        }
    }

    /// <summary>"Start this level again?" or "Leave this level?", with the way on and Keep playing, until answered.</summary>
    private void DrawQuestion()
    {
        ImGui.AlignTextToFramePadding();
        using (Theme.PushText(Theme.Surface.Text))
        {
            ImGui.TextUnformatted(pendingLevel == RestartChoice ? Strings.MoonfallRestartQuestion : Strings.MoonfallLeaveQuestion);
        }

        ImGui.SameLine(0f, UiMetrics.Px(12f));
        if (ImGui.Button((pendingLevel == RestartChoice ? Strings.MoonfallRestart : Strings.MoonfallLeave) + "##moonfallYes"))
        {
            SoundClick();
            Go(pendingLevel);
        }

        ImGui.SameLine();
        if (ImGui.Button(Strings.MoonfallKeepPlaying + "##moonfallNo"))
        {
            SoundClick();
            pendingLevel = NoChoice;
        }

        ImGui.Spacing();
    }

    /// <summary>A small filled circle at the text's height, as a key for the counter beside it.</summary>
    private static void Swatch(uint color)
    {
        var size = ImGui.GetFrameHeight();
        var at = ImGui.GetCursorScreenPos();
        var radius = MathF.Round(ImGui.GetFontSize() * 0.3f);
        ImGui.GetWindowDrawList().AddCircleFilled(new Vector2(at.X + radius, at.Y + (size * 0.5f)), radius, color, 16);
        ImGui.Dummy(new Vector2(radius * 2f, size));
    }
}
