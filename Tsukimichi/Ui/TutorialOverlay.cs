using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The interactive tour (<see cref="ITutorial"/>, T14, accessibility C1, game UX panel finding 6), in three chapters:
/// <b>Find</b> (search, quick views, filters and chips, the tab rail, the tree, the table), <b>Read</b> (the detail
/// pane, the Status column, a legend of all eight states with their glyphs drawn in the card, the path and giver) and
/// <b>Beyond</b> (Moonlit, Characters, Flight, the Todo overlay and Nearby, the companion plugins, help and settings). Each step points at a
/// region the main window recorded in <see cref="UiState.Rects"/> from the real items; its body is at most 35 words.
/// Drawn on the foreground draw list at the end of the main window's Draw: the window area is dimmed with Night at
/// 70 % except for a rounded cutout around the target and around the card; the target gets a Moon border with a soft
/// glow; the card is a small ImGui window beside the target, flipping sides near the screen edge. The card carries a
/// chapter strip (click to jump), the step within its chapter, title, body and Back / Next / Close.
/// Keys while the tour runs and the card or the main window has focus (not while typing): Enter or → next, ← or
/// Backspace back, Esc closes (on the first-run offer, Esc means Later). While the card itself has focus,
/// <see cref="ConsumeKeys"/> keeps Enter, Esc and Backspace from the game, so Enter does not also open the chat box; the
/// arrows always reach the game too, so the camera still turns during the tour.
/// On first run the welcome card offers the tour with "Take the tour", "Later" (offered again next session, up to
/// <see cref="LaterLimit"/> times) and "Don't offer again". The Read chapter selects a real quest (<see cref="SampleQuest"/>,
/// 1.7.0) so its steps point at real requirements. The tab, the filter panel, the selection and the other window state
/// the tour changes are put back when it ends. Sizes follow <see cref="UiMetrics.Scale"/> and the card's text follows the
/// UI scale, so a player at UiScale 1.6 gets a 1.6 card (accessibility B6).
/// </summary>
public sealed class TutorialOverlay : ITutorial
{
    /// <summary>"Later" answers after which the first-run offer stops coming back.</summary>
    public const int LaterLimit = 3;

    private const float CutoutPad = 6f;
    private const float BorderRounding = 8f;
    private const float BorderThickness = 2f;
    private const float GlowStep = 3f;
    private const int GlowLayers = 3;
    private const float Gap = 14f;
    private const float CardWidth = 400f;
    private const float CardHeightGuess = 220f;
    private const float LegendGlyphRadius = 9f;

    /// <summary>
    /// Frames a step is drawn before the card's measured size is trusted: the auto-resized card takes its size from
    /// the previous frame's content, so the frame a step changes (and the measurement taken on it) still reflect the
    /// previous step.
    /// </summary>
    private const int CardSettleFrames = 2;
    private const float DimAlpha = 0.7f;

    /// <summary>Two holes (target and card) split the area into at most 4² pieces.</summary>
    private const int PieceCapacity = 16;

    private static readonly uint DimU32 = Theme.WithAlpha(Theme.Night, DimAlpha);
    private static readonly Vector4 CardBorder = Theme.WithAlphaVector(Theme.Moon, 0.8f);
    private static readonly Vector4 CardButton = Vector4.Lerp(Theme.Night, Theme.Veil, 0.45f) with { W = 1f };
    private static readonly Vector4 CardButtonHovered = Theme.Veil;
    private static readonly Vector4 CardButtonActive = Theme.Dusk;
    private static readonly Vector4 ChapterActive = Theme.WithAlphaVector(Theme.Moon, 0.22f);

    private const ImGuiWindowFlags CardFlags =
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize |
        ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoDocking |
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse;

    private enum Chapter
    {
        Find,
        Read,
        Beyond,
    }

    private enum StepKind
    {
        Normal,
        Welcome,
        Legend,
        Finish,
    }

    private static readonly string[] NoKeys = [];

    private static string[] ChapterNames => chapterNamesText.Value;

    private static readonly Localization.LocArray chapterNamesText = new(static () =>
        [Strings.Tutorial.ChapterFind, Strings.Tutorial.ChapterRead, Strings.Tutorial.ChapterBeyond]);

    private static readonly string[] ChapterIds = ["##chapterFind", "##chapterRead", "##chapterBeyond"];

    /// <summary>The legend's order: what you can do, what you are doing, what stops you, what is behind you.</summary>
    private static readonly QuestState[] LegendStates =
    [
        QuestState.Ready,
        QuestState.ReadyOnOtherJob,
        QuestState.Accepted,
        QuestState.Blocked,
        QuestState.DoneThisCycle,
        QuestState.Completed,
        QuestState.Foreclosed,
        QuestState.Unknown,
    ];

    /// <summary>
    /// The Journal with the filter drawer shut: only the Filters step opens it, and it would otherwise stay over the tree
    /// for the steps after it (the Tree step's highlight needs the tree drawn). <see cref="End"/> puts the drawer back as
    /// the player had it.
    /// </summary>
    private static readonly Action<UiState> ShowJournal = static ui =>
    {
        ui.Tab = NavTab.Journal;
        ui.FilterPanelOpen = false;
    };

    /// <summary>
    /// One step: its chapter, what the card shows, the rect keys whose union is highlighted (empty for a card with no
    /// target), keys tried when none of the first set is recorded, and what to change when the step is shown.
    /// </summary>
    private readonly record struct Step(Chapter Chapter, StepKind Kind, string Title, string Body, string[] Keys, string[] FallbackKeys, Action<UiState>? OnShow);

    private static Step[] Steps => stepsCache.Value;

    private static readonly Localization.LocCache<Step[]> stepsCache = new(static () =>
        [
        // Find
        new(Chapter.Find, StepKind.Welcome, Strings.Tutorial.WelcomeTitle, Strings.Tutorial.WelcomeBody, NoKeys, NoKeys, ShowJournal),
        new(Chapter.Find, StepKind.Normal, Strings.Tutorial.SearchTitle, Strings.Tutorial.SearchBody, [UiRects.Search], [UiRects.Toolbar], ShowJournal),
        new(Chapter.Find, StepKind.Normal, Strings.Tutorial.QuickViewsTitle, Strings.Tutorial.QuickViewsBody, [UiRects.QuickViews], [UiRects.Toolbar], ShowJournal),
        new(Chapter.Find, StepKind.Normal, Strings.Tutorial.FiltersTitle, Strings.Tutorial.FiltersBody, [UiRects.FilterPanel, UiRects.FiltersButton], [UiRects.FiltersButton], static ui =>
        {
            ui.Tab = NavTab.Journal;
            ui.FilterPanelOpen = true;
        }),
        new(Chapter.Find, StepKind.Normal, Strings.Tutorial.TabsTitle, Strings.Tutorial.TabsBody, [UiRects.Tabs], NoKeys, ShowJournal),
        new(Chapter.Find, StepKind.Normal, Strings.Tutorial.TreeTitle, Strings.Tutorial.TreeBody, [UiRects.Tree], NoKeys, ShowJournal),
        new(Chapter.Find, StepKind.Normal, Strings.Tutorial.TableTitle, Strings.Tutorial.TableBody, [UiRects.Table], NoKeys, ShowJournal),

        // Read
        new(Chapter.Read, StepKind.Normal, Strings.Tutorial.DetailTitle, Strings.Tutorial.DetailBody, [UiRects.DetailRequirements], [UiRects.Detail], ShowJournal),
        new(Chapter.Read, StepKind.Normal, Strings.Tutorial.StatusTitle, Strings.Tutorial.StatusBody, [UiRects.Table], NoKeys, ShowJournal),
        new(Chapter.Read, StepKind.Legend, Strings.Tutorial.LegendTitle, Strings.Tutorial.LegendBody, [UiRects.Table], NoKeys, ShowJournal),
        new(Chapter.Read, StepKind.Normal, Strings.Tutorial.PathTitle, Strings.Tutorial.PathBody, [UiRects.DetailPath, UiRects.DetailGiver], [UiRects.Detail], ShowJournal),

        // Beyond
        new(Chapter.Beyond, StepKind.Normal, Strings.Tutorial.MoonlitTitle, Strings.Tutorial.MoonlitBody, [UiRects.MoonlitKinds, UiRects.MoonlitTable], [UiRects.Tabs], static ui => ui.Tab = NavTab.Moonlit),
        new(Chapter.Beyond, StepKind.Normal, Strings.Tutorial.CharactersTitle, Strings.Tutorial.CharactersBody, [UiRects.CharactersDashboard], [UiRects.CharactersList], static ui => ui.Tab = NavTab.Characters),
        new(Chapter.Beyond, StepKind.Normal, Strings.Tutorial.FlightTitle, Strings.Tutorial.FlightBody, [UiRects.FlightTable], [UiRects.FlightZones], static ui => ui.Tab = NavTab.Flight),
        new(Chapter.Beyond, StepKind.Normal, Strings.Tutorial.PlanTitle, Strings.Tutorial.PlanBody, [UiRects.PlanCards], [UiRects.Tabs], static ui => ui.Tab = NavTab.Plan),
        new(Chapter.Beyond, StepKind.Normal, Strings.Tutorial.PlayTitle, Strings.Tutorial.PlayBody, [UiRects.OverlayButton, UiRects.NearbyButton], [UiRects.SettingsButton], null),
        new(Chapter.Beyond, StepKind.Normal, Strings.Tutorial.CompanionsTitle, Strings.Tutorial.CompanionsBody, [UiRects.SettingsButton], [UiRects.Toolbar], null),
        new(Chapter.Beyond, StepKind.Normal, Strings.Tutorial.HelpTitle, Strings.Tutorial.HelpBody, [UiRects.HelpButton, UiRects.SettingsButton], [UiRects.Toolbar], null),
        new(Chapter.Beyond, StepKind.Finish, Strings.Tutorial.FinishTitle, Strings.Tutorial.FinishBody, NoKeys, NoKeys, null),
    ]);

    /// <summary>First step of each chapter, by <see cref="Chapter"/>.</summary>
    private static readonly int[] ChapterStart = BuildChapterStarts();

    private readonly Configuration settings;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly UiState ui;

    /// <summary>"Find · step 3 of 7" per step, built once per UI language.</summary>
    private string[] Progress => progressCache.Value;

    private static readonly Localization.LocArray progressCache = new(BuildProgress);

    private int index = -1;
    private bool offering;
    private bool offeredThisSession;
    private bool mainWasOpen;
    private bool focusCard;
    private Vector2 cardSize;

    /// <summary>Frames drawn on the current step; the card is placed from an estimate until <see cref="CardSettleFrames"/>.</summary>
    private int stepFrames;

    /// <summary>Set when a button changed the step this frame, so a key press that also activated it is not applied twice.</summary>
    private bool stepChanged;

    // Window state the tour changes, put back when it ends.
    private bool stateSaved;
    private NavTab savedTab;
    private bool savedFilterPanelOpen;

    // The quest the Read chapter selected (1.7.0) and the selection it replaced, put back when the tour ends.
    private bool sampleApplied;
    private uint? savedSelection;

    public TutorialOverlay(Configuration settings, IDalamudPluginInterface pluginInterface, UiState ui)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));

    }

    private static string[] BuildProgress()
    {
        var progress = new string[Steps.Length];
        for (var i = 0; i < Steps.Length; i++)
        {
            var chapter = (int)Steps[i].Chapter;
            var start = ChapterStart[chapter];
            var end = chapter + 1 < ChapterStart.Length ? ChapterStart[chapter + 1] : Steps.Length;
            progress[i] = string.Format(CultureInfo.CurrentCulture, Strings.Tutorial.ProgressFormat, ChapterNames[chapter], i - start + 1, end - start);
        }

        return progress;
    }

    /// <summary>The main window, watched by <see cref="CheckFirstRun"/> for its first opening.</summary>
    public Window? WatchedWindow { get; set; }

    /// <summary>Opens the help window; the finish card's "Open help" button. Null hides the button.</summary>
    public Action? OpenHelp { get; set; }

    /// <summary>
    /// The quest the Read chapter selects so the detail pane shows real requirements, path and giver
    /// (<see cref="Core.Ui.TourSample"/>; feature plan v5, 1.7.0). Null, or a null answer, leaves the selection alone.
    /// </summary>
    public Func<uint?>? SampleQuest { get; set; }

    /// <inheritdoc/>
    public bool Active => index >= 0;

    /// <summary>Zero-based step shown, or -1.</summary>
    public int StepIndex => index;

    public int StepCount => Steps.Length;

    /// <summary>True while the first-run welcome card is showing "Take the tour" / "Later" / "Don't offer again".</summary>
    public bool Offering => offering;

    /// <inheritdoc/>
    public void Start()
    {
        Begin(0);
    }

    /// <inheritdoc/>
    public void Stop()
    {
        End(seen: false);
    }

    /// <summary>
    /// <c>UiBuilder.Draw</c> handler: the first time <see cref="WatchedWindow"/> is seen open while the tour was
    /// neither finished nor declined (and "Later" was not answered <see cref="LaterLimit"/> times), shows the welcome
    /// card with the offer. Once per session.
    /// </summary>
    public void CheckFirstRun()
    {
        if (WatchedWindow is not { } window)
        {
            return;
        }

        var open = window.IsOpen;
        if (open && !mainWasOpen && !offeredThisSession && !settings.TutorialCompleted && settings.TutorialLaterCount < LaterLimit && !Active)
        {
            offeredThisSession = true;
            offering = true;
            index = 0;
            focusCard = true;
            stepFrames = 0;
        }

        mainWasOpen = open;
    }

    /// <inheritdoc/>
    public void Draw(UiState ui)
    {
        if (!Active)
        {
            popupDepthAtEnd = 0;
            return;
        }

        stepChanged = false;
        var scale = UiMetrics.Scale;
        ref readonly var step = ref Steps[index];

        var viewport = ImGuiHelpers.MainViewport;
        var screen = ScreenRect.FromSize(viewport.WorkPos, viewport.WorkSize);
        var area = UnionOfRects(ui);
        if (area.IsEmpty)
        {
            area = screen;
        }

        var hasTarget = TryTarget(ui, in step, out var target);
        if (hasTarget)
        {
            target = target.Expand(CutoutPad * scale);
        }

        // Right after a step change the card is placed from a conservative estimate and gets no hole in the dim;
        // once it has settled, its measured size is used.
        var cardMeasured = stepFrames >= CardSettleFrames;
        var size = cardMeasured ? cardSize : new Vector2(CardWidthPx(screen), CardHeightGuess * scale);
        var cardPos = hasTarget
            ? OverlayGeometry.PlaceCard(in target, size, in screen, Gap * scale, out _)
            : OverlayGeometry.CenterIn(in area, size);
        var card = ScreenRect.FromSize(cardPos, size);

        DrawDim(in area, hasTarget, in target, in card, cardMeasured);
        if (hasTarget)
        {
            DrawHighlight(in target, scale);
        }

        // Called from the end of the main window's Draw, so this is the main window's focus.
        var mainFocused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        DrawCard(cardPos, in step, screen, mainFocused);

        // A click in the main window gives it focus; its NoBringToFrontOnFocus flag (set by MainWindow while the tour
        // runs) already keeps the card in front. As a second guard the card asks for focus again on the frame after a
        // release over a plugin window, but not while a widget is active or a popup is open (focusing would deactivate
        // the widget or close the popup, and the window underneath must stay usable), and not after a click on the
        // game, which takes the keyboard back.
        if (Active && AnyMouseReleased() && ImGui.GetIO().WantCaptureMouse && !ImGui.IsAnyItemActive() && !AnyPopupOpen())
        {
            focusCard = true;
        }

        popupDepthAtEnd = ImGui.GetCurrentContext().OpenPopupStack.Size;
    }

    /// <summary>
    /// How many popups were open when the last frame's Draw ended. ImGui's keyboard navigation can close a popup on
    /// Esc in NewFrame, before this frame's <see cref="HandleKeys"/> runs, so <see cref="AnyPopupOpen"/> alone would let
    /// the same press close the tour too; a popup open last frame means this frame's keys were the popup's.
    /// </summary>
    private int popupDepthAtEnd;

    private static bool AnyMouseReleased() =>
        ImGui.IsMouseReleased(ImGuiMouseButton.Left) || ImGui.IsMouseReleased(ImGuiMouseButton.Right) || ImGui.IsMouseReleased(ImGuiMouseButton.Middle);

    private static bool AnyPopupOpen() => ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel);

    // ------------------------------------------------------------------ steps

    /// <summary>Starts the tour at <paramref name="step"/>, remembering the window state it will change.</summary>
    private void Begin(int step)
    {
        offering = false;
        if (!stateSaved)
        {
            stateSaved = true;
            savedTab = ui.Tab;
            savedFilterPanelOpen = ui.FilterPanelOpen;
        }

        GoTo(step);
    }

    private void GoTo(int i)
    {
        index = Math.Clamp(i, 0, Steps.Length - 1);
        focusCard = true;
        stepFrames = 0;
        stepChanged = true;
        Steps[index].OnShow?.Invoke(ui);
        if (Steps[index].Chapter == Chapter.Read)
        {
            SelectSample();
        }
    }

    /// <summary>
    /// The first time the tour reaches the Read chapter: selects <see cref="SampleQuest"/>, remembering the selection it
    /// replaces. A quest the player selects during the tour is not overridden when they step back and forth.
    /// </summary>
    private void SelectSample()
    {
        if (sampleApplied || !stateSaved || SampleQuest?.Invoke() is not { } rowId)
        {
            return;
        }

        sampleApplied = true;
        savedSelection = ui.SelectedRowId;
        ui.SelectedRowId = rowId;
    }

    /// <summary>
    /// Ends the tour or the offer and puts the window back as it was. <paramref name="seen"/> records that the tour
    /// need not be offered again (finished, closed after taking it, or "Don't offer again").
    /// </summary>
    private void End(bool seen)
    {
        if (stateSaved)
        {
            stateSaved = false;
            ui.Tab = savedTab;
            ui.FilterPanelOpen = savedFilterPanelOpen;
            if (sampleApplied)
            {
                sampleApplied = false;
                ui.SelectedRowId = savedSelection;
                savedSelection = null;
            }
        }

        index = -1;
        offering = false;
        stepChanged = true;
        keysOwned = false;
        cardHasKeys = false;
        if (seen && !settings.TutorialCompleted)
        {
            settings.TutorialCompleted = true;
            settings.Save(pluginInterface);
        }
    }

    /// <summary>"Later": no tour now, the offer comes back next session (up to <see cref="LaterLimit"/> times).</summary>
    private void Later()
    {
        settings.TutorialLaterCount = Math.Min(LaterLimit, settings.TutorialLaterCount + 1);
        settings.Save(pluginInterface);
        End(seen: false);
    }

    private void Next()
    {
        if (offering)
        {
            Begin(1);
        }
        else if (index >= Steps.Length - 1)
        {
            End(seen: true);
        }
        else
        {
            GoTo(index + 1);
        }
    }

    private void Back()
    {
        if (!offering && index > 0)
        {
            GoTo(index - 1);
        }
    }

    private void Close()
    {
        if (offering)
        {
            Later();
        }
        else
        {
            End(seen: true);
        }
    }

    /// <summary>
    /// <c>Framework.Update</c> handler: while the card itself has the keyboard (<see cref="cardHasKeys"/>, decided on the
    /// last draw), clears Enter, Esc and Backspace from the game's key state before the game reads them, so Enter does
    /// not also open the chat box and Esc does not also open the system menu. The arrows are left alone (the camera
    /// turns with them), and nothing is cleared while only the main window has focus. Dalamud passes keys to the game unless a text input is
    /// active; ImGui still receives them through its own window messages.
    /// </summary>
    public void ConsumeKeys(IFramework framework)
    {
        // Only right after a draw that owned them: a tour paused by closing the main window gives the keys back.
        if (!cardHasKeys || Environment.TickCount64 - keysOwnedAt > KeysOwnedGraceMs || KeyState is not { } keys)
        {
            return;
        }

        foreach (var key in TourKeys)
        {
            if (keys[key])
            {
                keys[key] = false;
            }
        }
    }

    /// <summary>The game's key state, for <see cref="ConsumeKeys"/>; null leaves the game's keys alone.</summary>
    public IKeyState? KeyState { get; set; }

    private static readonly VirtualKey[] TourKeys = [VirtualKey.RETURN, VirtualKey.ESCAPE, VirtualKey.BACK];

    /// <summary>Whether the tour answered keys on the last draw: the card or the main window had focus and nothing was being typed.</summary>
    private bool keysOwned;

    /// <summary>Whether the card window itself had focus (not only the main window) when <see cref="keysOwned"/> was decided.</summary>
    private bool cardHasKeys;

    /// <summary>When <see cref="keysOwned"/> was last decided (<see cref="Environment.TickCount64"/>).</summary>
    private long keysOwnedAt;

    /// <summary>How long a draw's <see cref="keysOwned"/> stays good for <see cref="ConsumeKeys"/>.</summary>
    private const long KeysOwnedGraceMs = 250;

    /// <summary>
    /// The tour's keys, applied after the card's buttons so a key that also activated a focused button counts once.
    /// Only while the card or the main window has focus (a click on the game gives the keys back to it) and nothing is
    /// being typed or edited.
    /// </summary>
    private void HandleKeys(bool mainFocused)
    {
        var cardFocused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        keysOwned = Active && !ImGui.GetIO().WantTextInput && (mainFocused || cardFocused);
        cardHasKeys = keysOwned && cardFocused;
        keysOwnedAt = Environment.TickCount64;
        if (!keysOwned || stepChanged || ImGui.IsAnyItemActive() || AnyPopupOpen() || popupDepthAtEnd > 0)
        {
            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Escape, false))
        {
            Close();
        }
        else if (ImGui.IsKeyPressed(ImGuiKey.Enter, false) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter, false) || ImGui.IsKeyPressed(ImGuiKey.RightArrow, false))
        {
            Next();
        }
        else if (ImGui.IsKeyPressed(ImGuiKey.LeftArrow, false) || ImGui.IsKeyPressed(ImGuiKey.Backspace, false))
        {
            Back();
        }
    }

    // ------------------------------------------------------------------ geometry

    private static float CardWidthPx(in ScreenRect screen) =>
        MathF.Min(CardWidth * UiMetrics.Scale, MathF.Max(1f, screen.Max.X - screen.Min.X - 2f * Gap * UiMetrics.Scale));

    private static ScreenRect UnionOfRects(UiState ui)
    {
        var area = default(ScreenRect);
        foreach (var entry in ui.Rects)
        {
            var rect = new ScreenRect(entry.Value.Min, entry.Value.Max);
            area = ScreenRect.Union(in area, in rect);
        }

        return area;
    }

    /// <summary>Union of the step's recorded keys; the fallback keys when none of the first set is present.</summary>
    private static bool TryTarget(UiState ui, in Step step, out ScreenRect target)
    {
        return TryUnion(ui, step.Keys, out target) || TryUnion(ui, step.FallbackKeys, out target);
    }

    private static bool TryUnion(UiState ui, string[] keys, out ScreenRect target)
    {
        target = default;
        var found = false;
        foreach (var key in keys)
        {
            if (!ui.Rects.TryGetValue(key, out var r))
            {
                continue;
            }

            var rect = new ScreenRect(r.Min, r.Max);
            if (rect.IsEmpty)
            {
                continue;
            }

            target = found ? ScreenRect.Union(in target, in rect) : rect;
            found = true;
        }

        return found;
    }

    private static int[] BuildChapterStarts()
    {
        var starts = new int[ChapterNames.Length];
        for (var c = 0; c < starts.Length; c++)
        {
            starts[c] = Array.FindIndex(Steps, s => (int)s.Chapter == c);
        }

        return starts;
    }

    // ------------------------------------------------------------------ drawing

    /// <summary>Night at 70 % over the area, as bands around the target and the card (ImGui has no cutouts).</summary>
    private static void DrawDim(in ScreenRect area, bool hasTarget, in ScreenRect target, in ScreenRect card, bool cardMeasured)
    {
        Span<ScreenRect> holes = stackalloc ScreenRect[2];
        var holeCount = 0;
        if (hasTarget)
        {
            holes[holeCount++] = target;
        }

        // The card's hole is cut only from its measured rectangle; an estimate would leave a visible seam.
        if (cardMeasured)
        {
            holes[holeCount++] = card;
        }

        Span<ScreenRect> pieces = stackalloc ScreenRect[PieceCapacity];
        Span<ScreenRect> scratch = stackalloc ScreenRect[PieceCapacity];
        var count = OverlayGeometry.Cutout(in area, holes[..holeCount], pieces, scratch);

        var dl = ImGui.GetForegroundDrawList();
        for (var i = 0; i < count; i++)
        {
            dl.AddRectFilled(pieces[i].Min, pieces[i].Max, DimU32);
        }
    }

    /// <summary>A Moon border with three fading rings outside it.</summary>
    private static void DrawHighlight(in ScreenRect target, float scale)
    {
        var dl = ImGui.GetForegroundDrawList();
        var rounding = BorderRounding * scale;
        for (var k = GlowLayers; k >= 1; k--)
        {
            var spread = k * GlowStep * scale;
            var alpha = 0.28f - 0.08f * k;
            dl.AddRect(target.Min - new Vector2(spread), target.Max + new Vector2(spread), Theme.WithAlpha(Theme.Moon, alpha), rounding + spread, ImDrawFlags.RoundCornersAll, GlowStep * scale);
        }

        dl.AddRect(target.Min, target.Max, Theme.MoonU32, rounding, ImDrawFlags.RoundCornersAll, BorderThickness * scale);
    }

    private void DrawCard(Vector2 pos, in Step step, in ScreenRect screen, bool mainFocused)
    {
        var scale = UiMetrics.Scale;
        var width = CardWidthPx(screen);
        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
        ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0f), new Vector2(width, float.MaxValue));
        if (focusCard)
        {
            ImGui.SetNextWindowFocus();
            focusCard = false;
        }

        using var colors = ImRaii.PushColor(ImGuiCol.WindowBg, Theme.Night)
                                 .Push(ImGuiCol.Border, CardBorder)
                                 .Push(ImGuiCol.Text, Theme.Silver)
                                 .Push(ImGuiCol.TextDisabled, Theme.Mist)
                                 .Push(ImGuiCol.Separator, Theme.Veil)
                                 .Push(ImGuiCol.Button, CardButton)
                                 .Push(ImGuiCol.ButtonHovered, CardButtonHovered)
                                 .Push(ImGuiCol.ButtonActive, CardButtonActive);
        using var styles = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, BorderRounding * scale)
                                 .Push(ImGuiStyleVar.WindowBorderSize, 1f)
                                 .Push(ImGuiStyleVar.WindowPadding, new Vector2(16f, 14f) * scale)
                                 .Push(ImGuiStyleVar.FrameRounding, 4f * scale)
                                 .Push(ImGuiStyleVar.FramePadding, new Vector2(10f, 4f) * scale)
                                 .Push(ImGuiStyleVar.ItemSpacing, new Vector2(8f, 6f) * scale);

        var visible = ImGui.Begin(Strings.Tutorial.CardId, CardFlags);
        try
        {
            if (visible)
            {
                // The card is a top-level window, so it takes the UI scale itself (accessibility B6).
                UiMetrics.ApplyFontScale();

                // Measured before the content, whose buttons may move to another step and restart the count.
                cardSize = ImGui.GetWindowSize();
                if (stepFrames < CardSettleFrames)
                {
                    stepFrames++;
                }

                DrawCardContent(in step);
                HandleKeys(mainFocused);
            }
        }
        finally
        {
            ImGui.End();
        }
    }

    private void DrawCardContent(in Step step)
    {
        var gap = UiMetrics.Px(8f);
        DrawChapterStrip(step.Chapter);
        if (!offering)
        {
            ImGui.TextDisabled(Progress[index]);
        }

        using (Typography.Display())
        using (Theme.PushText(Theme.Moon))
        {
            ImGui.TextUnformatted(step.Title);
        }

        ImGui.TextWrapped(step.Body);
        if (step.Kind == StepKind.Legend)
        {
            ImGui.Spacing();
            DrawLegend();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var buttonHeight = UiMetrics.MinTarget;
        if (offering)
        {
            if (Button(Strings.Tutorial.TakeTour, buttonHeight))
            {
                Begin(1);
            }

            ImGui.SetItemDefaultFocus();
            ImGui.SameLine(0f, gap);
            if (Button(Strings.Tutorial.Later, buttonHeight))
            {
                Later();
            }

            Tip(Strings.Tutorial.LaterTooltip);
            ImGui.SameLine(0f, gap);
            if (Button(Strings.Tutorial.DontOffer, buttonHeight))
            {
                End(seen: true);
            }

            Tip(Strings.Tutorial.DontOfferTooltip);
            ImGui.TextDisabled(Strings.Tutorial.OfferKeysHint);
            return;
        }

        if (index > 0)
        {
            if (Button(Strings.Tutorial.Back, buttonHeight))
            {
                Back();
            }

            ImGui.SameLine(0f, gap);
        }

        if (step.Kind == StepKind.Finish)
        {
            if (Button(Strings.Tutorial.Done, buttonHeight))
            {
                End(seen: true);
            }

            ImGui.SetItemDefaultFocus();
            if (OpenHelp is { } openHelp)
            {
                ImGui.SameLine(0f, gap);
                if (Button(Strings.Tutorial.OpenHelp, buttonHeight))
                {
                    End(seen: true);
                    openHelp();
                }
            }
        }
        else
        {
            if (Button(Strings.Tutorial.Next, buttonHeight))
            {
                Next();
            }

            ImGui.SetItemDefaultFocus();
            var closeWidth = ImGui.CalcTextSize(Strings.Tutorial.Close).X + ImGui.GetStyle().FramePadding.X * 2f;
            ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - closeWidth);
            if (Button(Strings.Tutorial.Close, buttonHeight))
            {
                Close();
            }
        }

        ImGui.TextDisabled(Strings.Tutorial.KeysHint);
    }

    /// <summary>
    /// Find · Read · Beyond as buttons: the current chapter is washed in Moon; a click jumps to the chapter's first step
    /// (on the first-run offer, it takes the tour from there).
    /// </summary>
    private void DrawChapterStrip(Chapter current)
    {
        var height = UiMetrics.MinTarget;
        for (var c = 0; c < ChapterNames.Length; c++)
        {
            if (c > 0)
            {
                ImGui.SameLine(0f, UiMetrics.Px(4f));
            }

            var active = !offering && (int)current == c;
            using (ImRaii.PushId(ChapterIds[c]))
            using (ImRaii.PushColor(ImGuiCol.Button, ChapterActive, active).Push(ImGuiCol.Text, Theme.Moon, active))
            {
                if (ImGui.Button(ChapterNames[c], new Vector2(0f, height)))
                {
                    if (offering)
                    {
                        Begin(Math.Max(1, ChapterStart[c]));
                    }
                    else if (ChapterStart[c] != index)
                    {
                        GoTo(ChapterStart[c]);
                    }
                }
            }

            Tip(Strings.Tutorial.ChapterTooltip);
        }
    }

    /// <summary>
    /// All eight states in two columns: each moon drawn at a readable size with its name in the state's colour; hover
    /// a row for the glyph's shape ("new moon, silver ring"). Items, so hover works; nothing allocates.
    /// </summary>
    private static void DrawLegend()
    {
        var dl = ImGui.GetWindowDrawList();
        var radius = UiMetrics.Px(LegendGlyphRadius);
        var line = ImGui.GetTextLineHeight();
        var rowHeight = MathF.Max(line, 2f * radius) + UiMetrics.Px(6f);
        var start = ImGui.GetCursorScreenPos();
        var columnWidth = ImGui.GetContentRegionAvail().X * 0.5f;
        var perColumn = (LegendStates.Length + 1) / 2;
        for (var i = 0; i < LegendStates.Length; i++)
        {
            var state = LegendStates[i];
            var column = i / perColumn;
            var row = i % perColumn;
            var min = start + new Vector2(column * columnWidth, row * rowHeight);
            ImGui.SetCursorScreenPos(min);
            using (ImRaii.PushId(i))
            {
                ImGui.Dummy(new Vector2(columnWidth - UiMetrics.Px(4f), rowHeight));
            }

            var center = new Vector2(min.X + radius + UiMetrics.Px(2f), min.Y + rowHeight * 0.5f);
            MoonGlyph.Draw(dl, center, radius, state);
            var textPos = new Vector2(center.X + radius + UiMetrics.Px(8f), min.Y + (rowHeight - line) * 0.5f);
            dl.AddText(textPos, Theme.StateColorU32(state), StateNames.Name(state));
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(StateNames.Tooltip(state));
            }
        }

        ImGui.SetCursorScreenPos(start + new Vector2(0f, perColumn * rowHeight));
        ImGui.Dummy(new Vector2(2f * columnWidth - UiMetrics.Px(4f), 0f));
    }

    private static bool Button(string label, float height) => ImGui.Button(label, new Vector2(0f, height));

    private static void Tip(string text)
    {
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(text);
        }
    }
}
