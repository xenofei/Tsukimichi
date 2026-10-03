using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;
using Tsukimichi.GameData;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// The main window (spec §2.4): toolbar, the tab rail and three resizable panes (navigation, quest table, detail; see
/// <see cref="PaneSplit"/>) and a status bar.
/// Owns the per-window <see cref="UiState"/> contract for its panes and persists the filters into the configuration.
/// Reads <see cref="Plugin.Session"/>, <see cref="Plugin.Settings"/> and <see cref="Plugin.Paths"/> lazily because the
/// window is constructed before the game-state block initializes them.
/// </summary>
public sealed partial class MainWindow : Window, IDisposable
{
    public const int MaxChatMatches = 5;

    private static readonly TimeSpan SettingsSaveDebounce = TimeSpan.FromSeconds(1);
    private static readonly string[] LoadingDots = ["", ".", "..", "..."];

    private readonly Plugin plugin;
    private readonly UiState ui;
    private readonly QueryRunner runner;
    private readonly GameLinks links;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;
    private readonly Func<Task> retryCatalog;
    private readonly string version;

    private readonly ITextureProvider textures;
    private readonly FilterPanel filterPanel;
    private readonly TabStrip tabStrip;
    private readonly TreePane treePane;
    private readonly TablePane tablePane;
    private readonly DetailPane detailPane;
    private readonly TonightCard tonightCard;
    private readonly PaneSplit paneSplit = new();

    // Attached after the game state exists (they need the session); null until then.
    private MoonlitPane? moonlitPane;
    private CharactersPane? charactersPane;
    private FlightPane? flightPane;
    private PlanPane? planPane;

    // The rail foot's actions and the tutorial overlay, attached by the plugin once those windows exist.
    private Action? openSettings;
    private Action? openHelp;
    private ITutorial? tutorial;
    private WhatsNewCard? whatsNew;
    private WelcomeBackCard? welcomeBack;
    private SetupCard? setupCard;

    // The Todo overlay's switch (1.7.0): the rail's Overlay button and the first-pin prompt; null until attached.
    private Action? toggleOverlay;
    private Func<bool>? overlayOn;

    // Frames drawn with a catalog, up to SetupDelayFrames: the setup card waits until the tour offer had its frame.
    private int contentFrames;
    private const int SetupDelayFrames = 3;

    // "Show it on screen?" after the first pin while the overlay is off (1.7.0): asked once, ever.
    private bool pinPromptVisible;

    // The context bar (1.7.0): decided on the first draw with a catalog; dismissed for the session with its ×.
    private bool contextChecked;
    private bool contextVisible;
    private string contextLine = string.Empty;
    private (string Name, int Filters, int Language) contextKey = (string.Empty, -1, -1);

    private Task? retryTask;

    // The "Game updated" strip (1.5.0): its source, attached with the diagnostics, and its line per count and language.
    private DataFreshnessSource? freshness;
    private string freshnessLine = string.Empty;
    private int freshnessLineCount = -1;
    private int freshnessLineLanguage = -1;

    // The "rebuild failed" banner line, rebuilt when the error or the UI language changes.
    private string? rebuildFailure;
    private string? rebuildFailureError;
    private int rebuildFailureLanguage = -1;

    private bool initialized;
    private DateTime? settingsDirtyAtUtc;
    private SortSpec persistedSort = SortSpec.Default;
    private string searchBuffer = string.Empty;
    private bool sizeChosen;

    // While the tour runs the window must not climb over the tutorial card when clicked, and Esc belongs to the
    // card; both settings are restored from these copies when the tour ends.
    private bool tourWasActive;
    private ImGuiWindowFlags flagsBeforeTour;
    private bool closeHotkeyBeforeTour;

    // Toolbar strings, rebuilt when the session version changes.
    private int toolbarVersion = -1;
    private string characterName = Strings.NoCharacter;
    private string characterWorld = string.Empty;
    private uint characterJobIcon;
    private string characterTooltip = Strings.CharacterChipTooltip;
    private string syncTooltip = Strings.NoCharacter;
    private readonly List<(ulong Id, string Label)> characterLabels = [];

    // Status bar string, rebuilt when its inputs change.
    private (int Catalog, int Rows, int Total, bool Live, long SnapshotMinute) statusKey = (-1, -1, -1, false, -1);
    private string status = string.Empty;
    private string statusMode = string.Empty;
    private string versionText = string.Empty;

    /// <summary>The least width the status text keeps when the MSQ segment crowds it, in logical pixels.</summary>
    private const float StatusMinLogical = 120f;

    /// <summary>The MSQ pill's fill: Moon at 10 % (ui-revamp §2.6).</summary>
    private static readonly uint MsqPillFill = Theme.WithAlpha(Theme.Moon, 0.10f);

    /// <summary>The data version stamp the status text shows on hover (the same line as Settings › About); null shows no tooltip.</summary>
    public string? DataStamp { get; set; }

    // Main scenario position, memoized per session version and catalog; empty strings hide it.
    private int msqVersion = -1;
    private CatalogBundle? msqBundle;
    private MsqPosition? msq;
    private string msqStatus = string.Empty;
    private string msqTooltip = string.Empty;

    public MainWindow(
        Plugin plugin,
        UiState ui,
        QueryRunner runner,
        GameLinks links,
        ITextureProvider textures,
        IDalamudPluginInterface pluginInterface,
        IPluginLog log,
        Func<Task> retryCatalog)
        : base(Strings.MainWindowTitle)
    {
        this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.retryCatalog = retryCatalog ?? throw new ArgumentNullException(nameof(retryCatalog));
        this.textures = textures ?? throw new ArgumentNullException(nameof(textures));

        // The first-use size is chosen on the first PreDraw, when the viewport and the UI scale are known.
        Size = ScaleMetrics.DefaultWindowLogical;
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = ScaleMetrics.MinWindowSize(ScaleMetrics.DefaultUiScale, ScaleMetrics.RailCompactLogical) };

        filterPanel = new FilterPanel(ui, OnFiltersChanged, OnDisplayChanged);
        ui.FiltersChanged += OnFiltersChanged;
        tabStrip = new TabStrip(ui);
        treePane = new TreePane(ui, textures, () => plugin.Session.NodeIcons);
        tablePane = new TablePane(ui, runner, links, textures, pluginInterface, log, filterPanel.ResetAll, OnFiltersChanged)
        {
            Catalog = () => plugin.Session?.Bundle?.Catalog,
            ShowExp = () => plugin.Settings?.JournalShowExpColumn == true,
        };
        detailPane = new DetailPane(ui, runner, links, textures, log);
        tablePane.Lane = filterPanel;
        tonightCard = new TonightCard(ui, runner, OnFiltersChanged);
        runner.QuestPinned += OnQuestPinned;

        version = typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0";
    }

    /// <summary>
    /// The rail's Todo overlay and Nearby buttons (1.7.0), and the first-pin prompt's "Turn on overlay". Until this is
    /// called the two buttons are drawn disabled and no prompt shows.
    /// </summary>
    public void AttachPlay(Action toggleOverlay, Func<bool> overlayOn, Action toggleNearby, Func<bool> nearbyOpen)
    {
        this.toggleOverlay = toggleOverlay ?? throw new ArgumentNullException(nameof(toggleOverlay));
        this.overlayOn = overlayOn ?? throw new ArgumentNullException(nameof(overlayOn));
        tabStrip.ToggleOverlay = toggleOverlay;
        tabStrip.OverlayOn = overlayOn;
        tabStrip.ToggleNearby = toggleNearby ?? throw new ArgumentNullException(nameof(toggleNearby));
        tabStrip.NearbyOpen = nearbyOpen ?? throw new ArgumentNullException(nameof(nearbyOpen));
    }

    /// <summary>"Set up your road" (1.7.0): drawn above the detail pane once it is due or Help opens it.</summary>
    public void AttachSetup(SetupCard card)
    {
        setupCard = card ?? throw new ArgumentNullException(nameof(card));
    }

    /// <summary>Opens the window in front with the "Set up your road" card (Help › Quick start).</summary>
    public void ShowSetup()
    {
        EnsureInitialized();
        setupCard?.Show();
        // The card lives in the no-selection slot of the detail column (feature plan v6 U2).
        ui.SelectedRowId = null;
        IsOpen = true;
        BringToFront();
    }

    /// <summary>
    /// The quest the tour's Read chapter selects (<see cref="Core.Ui.TourSample"/>): the next main scenario quest when it
    /// is Blocked, else the table's first Blocked row, else its first row. Null before the catalog is loaded.
    /// </summary>
    public uint? TourSampleQuest()
    {
        if (plugin.Session is not { Bundle: { } bundle } session)
        {
            return null;
        }

        RefreshMsq(session, bundle);
        return Core.Ui.TourSample.Choose(msq?.Next, msq?.Next is null ? null : msq.State, runner.Rows);
    }

    /// <summary>The first pin while the overlay is off offers to turn it on, once ever.</summary>
    private void OnQuestPinned(uint rowId)
    {
        if (overlayOn is null || overlayOn() || plugin.Settings is not { } settings || settings.PinOverlayPromptShown)
        {
            return;
        }

        pinPromptVisible = true;
        settings.PinOverlayPromptShown = true;
        settingsDirtyAtUtc ??= DateTime.UtcNow;
    }

    /// <summary>The shared per-window state other panes bind to.</summary>
    public UiState Ui => ui;

    /// <summary>Attaches the Moonlit and Characters panes once the session they depend on exists.</summary>
    public void AttachPanes(MoonlitPane moonlit, CharactersPane characters)
    {
        moonlitPane = moonlit ?? throw new ArgumentNullException(nameof(moonlit));
        charactersPane = characters ?? throw new ArgumentNullException(nameof(characters));
    }

    /// <summary>Attaches the Flight pane (aether current quests per zone); the Flight tab shows a placeholder until then.</summary>
    public void AttachFlight(FlightPane flight)
    {
        flightPane = flight ?? throw new ArgumentNullException(nameof(flight));
    }

    /// <summary>Attaches the My blues tab ("Clear my blues", P3); the tab shows a placeholder until then.</summary>
    public void AttachPlan(PlanPane plan)
    {
        planPane = plan ?? throw new ArgumentNullException(nameof(plan));
    }

    /// <summary>"Before you continue" (P5): the Tonight card draws the payoff gate lines under its main scenario row.</summary>
    public void AttachPayoffLines(PayoffGateLines lines)
    {
        tonightCard.PayoffLines = lines ?? throw new ArgumentNullException(nameof(lines));
    }

    /// <summary>The 1.9.0 planning lines: the Tonight card shows the level gate and the catch-up under its main scenario row.</summary>
    public void AttachPlanning(PlanningSource planning)
    {
        tonightCard.Planning = planning ?? throw new ArgumentNullException(nameof(planning));
    }

    /// <summary>"Next stops" (1.6.0): the Tonight card lists the first stops with Teleport.</summary>
    public void AttachNextStops(NextStopsSource stops)
    {
        tonightCard.Stops = stops ?? throw new ArgumentNullException(nameof(stops));
        tonightCard.Links = links;
    }

    /// <summary>Gives the detail pane the user's unique-reward verdicts so it can show and change them.</summary>
    public void AttachOverrides(IUniqueOverrides overrides)
    {
        detailPane.Overrides = overrides ?? throw new ArgumentNullException(nameof(overrides));
    }

    /// <summary>
    /// The detail pane's Questionable cross-check line and its opt-in "Add to Questionable priority" (V2-17);
    /// <paramref name="handoff"/> reads Settings › Integrations › "Show Questionable hand-off". <paramref name="actions"/>
    /// (1.6.0) adds the badges, the status bar's live status with Stop, the Journal row Questionable works on, and the
    /// confirmations of the Send to Questionable menus drawn in this window.
    /// </summary>
    public void AttachQuestionable(QuestionableIpc questionable, Func<bool> handoff, QuestionableActions actions)
    {
        detailPane.Questionable = questionable ?? throw new ArgumentNullException(nameof(questionable));
        detailPane.QuestionableHandoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
        questionableActions = actions ?? throw new ArgumentNullException(nameof(actions));
        detailPane.QuestionableActions = actions;
        tablePane.QuestionableRow = () => actions.RunningRowId;
    }

    /// <summary>
    /// The detail pane's Hand in section (1.6.0): live item counts, and the Artisan and GatherBuddy hand-offs. Until this
    /// is called the section lists the items without counts and its buttons name the plugins as missing.
    /// </summary>
    public void AttachHandIns(HandInStock stock, ArtisanIpc artisan, GatherBuddyCommands gatherBuddy)
    {
        detailPane.Stock = stock ?? throw new ArgumentNullException(nameof(stock));
        detailPane.Artisan = artisan ?? throw new ArgumentNullException(nameof(artisan));
        detailPane.GatherBuddy = gatherBuddy ?? throw new ArgumentNullException(nameof(gatherBuddy));
    }

    /// <summary>
    /// The detail pane's companion plugin pieces (feature plan v5, decision 1): the Duties section with "Run with
    /// AutoDuty" and "Open in Quest Map". <paramref name="duties"/> hands out the duty index, <paramref name="rewardEntries"/>
    /// a quest's reward entries, <paramref name="isDutyUnlocked"/> the logged-in character's unlock of an InstanceContent
    /// row and <paramref name="allowDutyFinder"/> reads Settings › Integrations. Until this is called neither shows.
    /// </summary>
    public void AttachCompanions(
        CompanionPlugins companions,
        AutoDutyIpc autoDuty,
        QuestMapIpc questMap,
        Func<Core.Companions.DutyRunIndex?> duties,
        Func<uint, IReadOnlyList<Core.Model.UniqueRewardEntry>> rewardEntries,
        Func<uint, bool?> isDutyUnlocked,
        Func<bool> allowDutyFinder)
    {
        detailPane.Companions = companions ?? throw new ArgumentNullException(nameof(companions));
        detailPane.AutoDuty = autoDuty ?? throw new ArgumentNullException(nameof(autoDuty));
        detailPane.QuestMap = questMap ?? throw new ArgumentNullException(nameof(questMap));
        detailPane.DutyRuns = duties ?? throw new ArgumentNullException(nameof(duties));
        detailPane.RewardEntries = rewardEntries ?? throw new ArgumentNullException(nameof(rewardEntries));
        detailPane.IsDutyUnlocked = isDutyUnlocked ?? throw new ArgumentNullException(nameof(isDutyUnlocked));
        detailPane.AutoDutyAllowDutyFinder = allowDutyFinder ?? throw new ArgumentNullException(nameof(allowDutyFinder));
    }

    // Questionable's hand-offs (1.6.0); null until attached.
    private QuestionableActions? questionableActions;

    /// <summary>The detail pane's hero banners (V4); without it a quest shows its own banner or its category art.</summary>
    public void AttachBanners(BannerIndexSource<Core.Unique.DutyUnlockIndex> banners)
    {
        detailPane.Banners = banners ?? throw new ArgumentNullException(nameof(banners));
    }

    /// <summary>The detail pane's Journal card (P9); without it the card is hidden.</summary>
    public void AttachQuestText(QuestTextService questText)
    {
        detailPane.QuestText = questText ?? throw new ArgumentNullException(nameof(questText));
    }

    /// <summary>The live travel status ("Mounting…", "Flying to …") for the line under the detail pane's pills while a trip runs.</summary>
    public void AttachTravelStatus(Func<string?> status)
    {
        detailPane.TravelStatusText = status ?? throw new ArgumentNullException(nameof(status));
    }

    /// <summary>The game's achievement flags for the detail pane's achievement lines (1.9.0 collector extras); without them the quests decide.</summary>
    public void AttachAchievementFlags(Func<uint, bool?> earned)
    {
        detailPane.AchievementEarned = earned ?? throw new ArgumentNullException(nameof(earned));
    }

    /// <summary>The detail pane's Report button and the status bar's data stamp tooltip.</summary>
    public void AttachDiagnostics(DiagnosticBuilder diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        detailPane.Diagnostics = diagnostics;
        DataStamp = diagnostics.DataStampLine;
    }

    /// <summary>
    /// The "Game updated" strip and the Added in filter's "New since data" value (feature plan v5, 1.5.0). Until this
    /// is called neither shows.
    /// </summary>
    public void AttachFreshness(DataFreshnessSource source)
    {
        freshness = source ?? throw new ArgumentNullException(nameof(source));
        filterPanel.NewSinceDataCount = () => source.Current.NewQuests;
    }

    /// <summary>
    /// Wires the rail foot's Help and Settings buttons (feature plan v4 L7). Until this is called the buttons are drawn
    /// disabled, so the rail's layout never changes. The tour has no button of its own any more: Help lists it
    /// (design v4 §7.10), so <paramref name="startTutorial"/> is only checked.
    /// </summary>
    public void AttachActions(Action openSettings, Action openHelp, Action startTutorial)
    {
        this.openSettings = openSettings ?? throw new ArgumentNullException(nameof(openSettings));
        this.openHelp = openHelp ?? throw new ArgumentNullException(nameof(openHelp));
        ArgumentNullException.ThrowIfNull(startTutorial);
    }

    /// <summary>
    /// Attaches the interactive tutorial; it is drawn at the very end of <see cref="Draw"/>, after every pane has
    /// recorded its region rectangles in <see cref="UiState.Rects"/>.
    /// </summary>
    public void AttachTutorial(ITutorial tutorial)
    {
        this.tutorial = tutorial ?? throw new ArgumentNullException(nameof(tutorial));
    }

    /// <summary>Attaches the "What's new" card; it decides on the window's first draw and sits above the detail pane while visible.</summary>
    public void AttachWhatsNew(WhatsNewCard card)
    {
        whatsNew = card ?? throw new ArgumentNullException(nameof(card));
    }

    /// <summary>
    /// Attaches "Since you were away" (P7): drawn above the detail pane while <paramref name="source"/> has something to
    /// show, after the What's-new card when both are due.
    /// </summary>
    public void AttachWelcomeBack(WelcomeBackSource source, SessionState session)
    {
        welcomeBack = new WelcomeBackCard(source, session, ui, OnFiltersChanged);
    }

    /// <summary>
    /// Night chrome for the whole window, title bar included (T13): pushed before Begin, popped after End, so popups,
    /// combos and tooltips begun in Draw read as Night too (<see cref="Theme.PushNightWindow"/> says why that is
    /// deliberate). <see cref="Window.BgAlpha"/> is left alone so the user's Dalamud opacity still applies. Nothing is
    /// pushed while following Dalamud's colours.
    /// </summary>
    public override void PreDraw()
    {
        // First use: 1100 × 700 at the default UI scale, scaled with it and fitted to the viewport (accessibility B6).
        // Dalamud multiplies Size by its global scale; ImGui applies it only when no saved size exists.
        if (!sizeChosen)
        {
            sizeChosen = true;
            var uiScale = plugin.Settings?.UiScale ?? ScaleMetrics.DefaultUiScale;
            Size = ScaleMetrics.DefaultWindowSize(uiScale, ImGuiHelpers.GlobalScale, ImGuiHelpers.MainViewport.WorkSize);
        }

        nightChrome = Theme.PushNightWindow();
    }

    public override void PostDraw()
    {
        nightChrome.Dispose();
        nightChrome = default;
    }

    private Theme.StyleScope nightChrome;

    public override void Draw()
    {
        SyncTourState();
        if (plugin.Session is not { } session)
        {
            return;
        }

        EnsureInitialized();
        UiMetrics.Update(plugin.Settings);

        // The rail and the panes' floors grow with the UI scale, so the minimum size must too or a pane goes under its
        // floor (ScaleMetrics.MinWindowSize, PaneLayout); it never exceeds the viewport, so the window can always be
        // placed whole. It is the compact rail's minimum whatever the rail is: the labelled rail shows only on a window
        // wider than the compact threshold (LayoutBudgets.CompactRail), which is above the labelled rail's own minimum at
        // every scale, so a minimum that followed the rail would never bind and would only grow a window that was saved
        // between the two minimums back on every load, before the rail had turned compact.
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = ScaleMetrics.MinWindowSize(UiMetrics.FontScale, ImGuiHelpers.GlobalScale, ImGuiHelpers.MainViewport.WorkSize, ScaleMetrics.RailCompactLogical),
        };

        // Dalamud closes the window on Esc while it or one of its popups is focused. Esc closes the topmost thing first
        // (T17): while a popup (verdict prompt, context menu) or the filter panel is open, Esc belongs to it and the
        // window stays; the next Esc closes the window. The tour manages the flag and its keys itself while it runs.
        if (!tourWasActive)
        {
            var panelOpen = ui.FilterPanelOpen && ui.Tab == NavTab.Journal;
            var popupOpen = popupDepthAtEnd > 0 || ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel);
            RespectCloseHotkey = !popupOpen && !panelOpen;
            if ((popupOpen || panelOpen) && Keyboard.WindowHasKeys())
            {
                escOwnedAt = Environment.TickCount64;
            }

            HandleEscape(panelOpen);
            HandleShortcuts(session);
        }

        selectionAtStart = ui.SelectedRowId;

        // Regions are re-recorded by whichever panes draw this frame; clearing first keeps hidden panes' rects
        // from lingering (the tutorial unions them for its dimmed area).
        ui.Rects.Clear();

        // The window's own font scale; direct children inherit it (see UiMetrics). It is reset before this Draw ends
        // so the next Begin lays the title bar out at Dalamud's size.
        UiMetrics.ApplyFontScale();
        try
        {
            DrawContent(session);
            if (!tourWasActive)
            {
                HandleRevealShortcut(session);
            }
        }
        finally
        {
            ImGui.SetWindowFontScale(1f);
            popupDepthAtEnd = ImGui.GetCurrentContext().OpenPopupStack.Size;
        }
    }

    // ------------------------------------------------------------------ keyboard (T17)

    /// <summary>How many popups were open when the last frame's Draw ended (ImGui's own Esc may close one before the next Draw).</summary>
    private int popupDepthAtEnd;

    /// <summary>
    /// When a draw last found the Esc ladder owning Esc (a popup or the filter panel open, this window with the keys;
    /// <see cref="Environment.TickCount64"/>), for <see cref="ConsumeEscape"/>. Long ago means never.
    /// </summary>
    private long escOwnedAt = long.MinValue / 2;

    /// <summary>How long a draw's claim on Esc stays good for <see cref="ConsumeEscape"/>.</summary>
    private const long EscOwnedGraceMs = 250;

    /// <summary>The game's key state, for <see cref="ConsumeEscape"/>; null leaves the game's keys alone.</summary>
    public IKeyState? KeyState { get; set; }

    /// <summary>
    /// <c>Framework.Update</c> handler: while the Esc ladder owns Esc (decided on a recent draw), clears Esc from the
    /// game's key state before the game reads it. With <see cref="Window.RespectCloseHotkey"/> off Dalamud no longer
    /// redirects Esc to the window, so without this the press that closes the filter panel or a popup would also
    /// close the game's top window, clear the target or open the system menu. Same mechanism as
    /// <see cref="TutorialOverlay.ConsumeKeys"/>.
    /// </summary>
    public void ConsumeEscape(IFramework framework)
    {
        if (Environment.TickCount64 - escOwnedAt > EscOwnedGraceMs || KeyState is not { } keys)
        {
            return;
        }

        if (keys[VirtualKey.ESCAPE])
        {
            keys[VirtualKey.ESCAPE] = false;
        }
    }

    /// <summary>
    /// Esc while this window has the keys and no text field is active: closes the topmost popup (a modal handles its
    /// own Esc), else the filter panel; with neither open, Dalamud's close hotkey closes the window. When ImGui's
    /// navigation already closed a popup on this press, nothing else happens, so one press closes one thing.
    /// </summary>
    private void HandleEscape(bool panelOpen)
    {
        if (!ImGui.IsKeyPressed(ImGuiKey.Escape, false) || !Keyboard.WindowHasKeys())
        {
            return;
        }

        var depth = ImGui.GetCurrentContext().OpenPopupStack.Size;
        if (popupDepthAtEnd > 0)
        {
            if (depth > 0 && depth >= popupDepthAtEnd && ImGuiP.GetTopMostPopupModal().IsNull)
            {
                ImGuiP.ClosePopupToLevel(depth - 1, true);
            }

            return;
        }

        if (panelOpen)
        {
            ui.FilterPanelOpen = false;
        }
    }

    /// <summary>
    /// The opt-in shortcuts of Settings › Keyboard (all off by default; accessibility A7): Ctrl+1..5 switch tabs, F
    /// flags the selected quest's giver, Enter shows the selected quest in the Journal, P pins or unpins it. Only while
    /// this window has the keys and no text field is active; the game still sees the key.
    /// </summary>
    private void HandleShortcuts(SessionState session)
    {
        var settings = plugin.Settings;
        if (!(settings.ShortcutTabs || settings.ShortcutFlag || settings.ShortcutPin) || !Keyboard.WindowHasKeys())
        {
            return;
        }

        if (settings.ShortcutTabs)
        {
            for (var i = 0; i < TabKeys.Length; i++)
            {
                if (Keyboard.CtrlPressed(TabKeys[i]))
                {
                    ui.Tab = (NavTab)i;
                    return;
                }
            }
        }

        if (ui.SelectedRowId is not { } rowId || session.Bundle?.Catalog.GetByRowId(rowId) is not { } quest)
        {
            return;
        }

        if (settings.ShortcutFlag && Keyboard.LetterPressed(ImGuiKey.F) && links.CanFlagMap(quest))
        {
            links.FlagMap(quest);
        }
        else if (settings.ShortcutPin && Keyboard.LetterPressed(ImGuiKey.P) && runner.CanPin)
        {
            runner.TogglePinWithUndo(quest);
        }
    }

    /// <summary>The selection when this frame's Draw began, so the Enter shortcut can tell a row activated by Enter.</summary>
    private uint? selectionAtStart;

    /// <summary>
    /// The opt-in Enter shortcut, after the panes drew: on the Moonlit, Characters or Flight tab it shows the selected
    /// quest in the Journal (on the Journal it is already there). While keyboard navigation is visible Enter belongs to
    /// the focused item, so it reveals only when that item was a row whose activation selected a quest this frame.
    /// </summary>
    private void HandleRevealShortcut(SessionState session)
    {
        if (!plugin.Settings.ShortcutReveal || ui.Tab == NavTab.Journal || !Keyboard.WindowHasKeys()
            || !(Keyboard.LetterPressed(ImGuiKey.Enter) || Keyboard.LetterPressed(ImGuiKey.KeypadEnter)))
        {
            return;
        }

        if (ImGui.GetIO().NavVisible && ui.SelectedRowId == selectionAtStart)
        {
            return;
        }

        if (ui.SelectedRowId is { } rowId && session.Bundle?.Catalog.GetByRowId(rowId) is { } quest)
        {
            ui.Reveal(quest);
        }
    }

    /// <summary>Ctrl+1..5 in <see cref="NavTab"/> order.</summary>
    private static readonly ImGuiKey[] TabKeys = [ImGuiKey.Key1, ImGuiKey.Key2, ImGuiKey.Key3, ImGuiKey.Key4, ImGuiKey.Key5];

    private void DrawContent(SessionState session)
    {
        var now = DateTime.UtcNow;
        FlushSettings(now, force: false);

        if (session.CatalogLoading || retryTask is { IsCompleted: false })
        {
            DrawLoading();
            return;
        }

        if (session.Bundle is not { } bundle)
        {
            DrawCatalogError(session);
            return;
        }

        runner.Update(now);
        // "Set up your road" comes once the tour offer is answered and any tour taken has ended (decision 7). The offer
        // is decided after this window's first draw (TutorialOverlay.CheckFirstRun), so the card waits a few frames.
        if (contentFrames < SetupDelayFrames)
        {
            contentFrames++;
        }
        else
        {
            setupCard?.CheckDue(tutorial?.Active == true);
        }
        RefreshToolbarStrings(session);

        // The fixed frame (feature plan v6 U2): toolbar, body, status bar, and nothing between them, so the panes never
        // move under the player (ChromeBands). What used to push them down floats over the body instead.
        DrawToolbar(session);
        DrawBody(session, bundle);
        DrawStatusBar(session, bundle);
        DrawFloating(session, bundle);
        questionableActions?.DrawModals(QuestionableHost);

        // The table writes ui.Sort from ImGui's header state; persist it through the same debounce as the filters.
        if (ui.Sort != persistedSort)
        {
            persistedSort = ui.Sort;
            settingsDirtyAtUtc ??= now;
        }

        // Last, after every pane recorded its rectangles for this frame.
        tutorial?.Draw(ui);
    }

    /// <summary>
    /// <c>/tsukimichi &lt;text&gt;</c>: sets the search, opens the window and prints up to <see cref="MaxChatMatches"/>
    /// matching quest links to chat. Returns how many quests matched (-1 before the catalog is ready), so the command
    /// can suggest a subcommand after a search that found nothing.
    /// </summary>
    public int SearchAndPrint(string text)
    {
        EnsureInitialized();
        ui.SearchText = text;
        searchBuffer = text;
        IsOpen = true;
        runner.FlushSearch();

        if (plugin.Session?.Bundle is not { } bundle)
        {
            links.PrintText(Strings.CatalogNotReady);
            return -1;
        }

        var index = SearchIndex.For(bundle.Catalog);
        var normalized = SearchIndex.Normalize(text);
        // A name the spoiler shield hides never matches; its placeholder does.
        var spoilers = plugin.Session.Spoilers;
        // Chat results mirror the table: removed quests only when the Include removed filter is on.
        var showUnlisted = ui.Filters.IncludeUnlisted;
        var count = 0;
        foreach (var quest in bundle.Catalog.All)
        {
            if ((quest.IsRemoved && !showUnlisted) || !index.Matches(quest.RowId, normalized, spoilers))
            {
                continue;
            }

            if (count < MaxChatMatches)
            {
                links.PrintQuestLink(quest);
            }

            count++;
        }

        if (count == 0)
        {
            links.PrintText(Strings.NoMatches);
        }
        else if (count > MaxChatMatches)
        {
            links.PrintText(string.Format(CultureInfo.CurrentCulture, Strings.AndMoreFormat, count - MaxChatMatches));
        }

        return count;
    }

    /// <summary>
    /// Opens the window in front on the Journal filtered to the viewed character's pins (<see cref="UiState.ShowPinned"/>),
    /// for the Todo overlay's "+N more" line. The saved filters are loaded first, so a window opened for the first time
    /// keeps the Pinned filter instead of replacing it with them.
    /// </summary>
    public void ShowPinned()
    {
        EnsureInitialized();
        ui.ShowPinned();
        IsOpen = true;
        BringToFront();
    }

    /// <summary>Closing inside the save debounce must not lose the pending filters, sort or display settings.</summary>
    public override void OnClose()
    {
        FlushSettings(DateTime.UtcNow, force: true);
        SyncTourState();
    }

    public void Dispose()
    {
        FlushSettings(DateTime.UtcNow, force: true);
        ui.FiltersChanged -= OnFiltersChanged;
        runner.QuestPinned -= OnQuestPinned;
        tablePane.Dispose();
    }

    /// <summary>
    /// Applies or restores the tour-time window settings when the tutorial starts or stops: the card is a separate
    /// top-level window, so <see cref="ImGuiWindowFlags.NoBringToFrontOnFocus"/> keeps this window from climbing over
    /// it after a click, and the close hotkey is left to the card (Esc skips the tour instead of closing the window).
    /// Flags take effect at the next Begin.
    /// </summary>
    private void SyncTourState()
    {
        var active = tutorial?.Active == true;
        if (active == tourWasActive)
        {
            return;
        }

        tourWasActive = active;
        if (active)
        {
            flagsBeforeTour = Flags;
            closeHotkeyBeforeTour = RespectCloseHotkey;
            Flags |= ImGuiWindowFlags.NoBringToFrontOnFocus;
            RespectCloseHotkey = false;
        }
        else
        {
            Flags = flagsBeforeTour;
            RespectCloseHotkey = closeHotkeyBeforeTour;
        }
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        whatsNew?.CheckOnOpen();
        ui.Filters = plugin.Settings.Filters;
        ui.Sort = new SortSpec(plugin.Settings.SortColumn, plugin.Settings.SortDescending, plugin.Settings.PinnedFirst);
        persistedSort = ui.Sort;
        searchBuffer = ui.SearchText;
    }

    private void OnFiltersChanged()
    {
        ui.MarkQueryDirty();
        settingsDirtyAtUtc ??= DateTime.UtcNow;
    }

    /// <summary>A display slider moved: the settings object already holds the value; persist it after the debounce.</summary>
    private void OnDisplayChanged()
    {
        settingsDirtyAtUtc ??= DateTime.UtcNow;
    }

    private void FlushSettings(DateTime nowUtc, bool force)
    {
        if (settingsDirtyAtUtc is not { } dirtyAt || (!force && nowUtc - dirtyAt < SettingsSaveDebounce))
        {
            return;
        }

        settingsDirtyAtUtc = null;
        try
        {
            plugin.Settings.Filters = ui.Filters;
            plugin.Settings.SortColumn = ui.Sort.Column;
            plugin.Settings.SortDescending = ui.Sort.Descending;
            plugin.Settings.PinnedFirst = ui.Sort.PinnedFirst;
            plugin.Settings.Save(pluginInterface);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Settings could not be saved");
        }
    }

    /// <summary>
    /// "Loading the quest catalog" with a sign of life (R3 #12). At Flair Full and Quiet a moon before the text steps
    /// through its phases (<see cref="MotionMath.LoadingMoonFraction"/>), in a box of its own so nothing on the line
    /// moves; under Plain the dots, in room for all three. Under Reduce motion both stand still (the first quarter, or
    /// all three dots).
    /// </summary>
    private static void DrawLoading()
    {
        var reduce = UiMetrics.ReduceMotion;
        if (!Theme.ShowRules)
        {
            ImGui.TextUnformatted(Strings.LoadingCatalog);
            ImGui.SameLine(0f, 0f);
            var dots = reduce ? LoadingDots[^1] : LoadingDots[(int)(ImGui.GetTime() * 2.0) % LoadingDots.Length];
            var at = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(ImGui.CalcTextSize(LoadingDots[^1]).X, ImGui.GetTextLineHeight()));
            ImGui.GetWindowDrawList().AddText(at, ImGui.GetColorU32(ImGuiCol.Text), dots);
            return;
        }

        var line = ImGui.GetTextLineHeight();
        var size = UiMetrics.InlineGlyphSize(line);
        var min = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(size, MathF.Max(size, line)));
        var center = min + new Vector2(size * 0.5f, MathF.Max(size, line) * 0.5f);
        MoonGlyph.DrawFilling(ImGui.GetWindowDrawList(), center, size * MoonGlyph.InlineRadiusFraction, MotionMath.LoadingMoonFraction(ImGui.GetTime(), reduce));
        ImGui.SameLine();
        ImGui.SetCursorScreenPos(new Vector2(ImGui.GetCursorScreenPos().X, min.Y + MathF.Max(0f, (size - line) * 0.5f)));
        ImGui.TextUnformatted(Strings.LoadingCatalog);
    }

    private void DrawCatalogError(SessionState session)
    {
        using (Theme.PushText(Theme.Eclipse))
        {
            ImGui.TextUnformatted(Strings.CatalogUnavailable);
        }

        ImGui.TextWrapped(session.CatalogError ?? string.Empty);
        ImGui.Spacing();
        if (ImGui.Button(Strings.Retry))
        {
            retryTask = retryCatalog();
        }
    }

    private void RefreshToolbarStrings(SessionState session)
    {
        if (toolbarVersion == session.Version)
        {
            return;
        }

        toolbarVersion = session.Version;
        var snapshot = session.ViewedSnapshot;
        if (snapshot is null)
        {
            characterName = Strings.NoCharacter;
            characterWorld = string.Empty;
            characterJobIcon = 0;
            // Browse mode (feature plan v6 U2): the status bar says it, and its pip explains it on hover.
            syncTooltip = Strings.BrowseModeNotice;
            characterTooltip = Strings.CharacterChipTooltip;
            return;
        }

        characterName = snapshot.Name;
        characterWorld = links.WorldName(snapshot.World);
        characterJobIcon = snapshot.CurrentJob == 0 ? 0u : MoonlitIconResolver.ClassJobIconBase + snapshot.CurrentJob;
        if (session.IsLive)
        {
            syncTooltip = session.PollerHealthy ? Strings.SyncLive : Strings.SyncPollerPaused;
        }
        else if (session.IsLiveElsewhere(snapshot.ContentId))
        {
            // Multibox (D11): logged in on another game client; what shows is that client's latest save.
            // The line that was a banner over the panes until 1.12 (feature plan v6 U2) now opens the pip's tooltip.
            var time = UiFormat.Time(snapshot.TakenUtc);
            var banner = string.Format(CultureInfo.CurrentCulture, Strings.MultiboxBannerFormat, snapshot.Name, links.WorldName(snapshot.World), time);
            // 1.8.0 (R7 G): "updates each time that client saves" is not true of a file this client cannot read.
            var status = session.NotUpdating.TryGetValue(snapshot.ContentId, out var problem)
                ? CharactersPane.NotUpdatingText(problem)
                : Strings.MultiboxLiveElsewhereTooltip;
            syncTooltip = banner + "\n" + status + "\n" + string.Format(CultureInfo.CurrentCulture, Strings.SyncSnapshotFormat, time);
        }
        else
        {
            var time = UiFormat.Time(snapshot.TakenUtc);
            syncTooltip = string.Format(CultureInfo.CurrentCulture, Strings.StaleBannerFormat, snapshot.Name, links.WorldName(snapshot.World), time) + "\n" + string.Format(CultureInfo.CurrentCulture, Strings.SyncSnapshotFormat, time);
        }

        characterTooltip = syncTooltip + "\n" + Strings.CharacterChipTooltip;
    }

    // ------------------------------------------------------------------ toolbar (T14, ui-revamp §2.1)

    private const string CharacterPopupId = "##characterMenu";

    private static readonly string SearchIcon = FontAwesomeIcon.Search.ToIconString();
    private static readonly string FiltersIcon = FontAwesomeIcon.SlidersH.ToIconString();
    private static readonly string ChevronIcon = FontAwesomeIcon.ChevronDown.ToIconString();

    /// <summary>
    /// The window frame's bands this frame (<see cref="ChromeBands"/>), from the window's width and the sizes of type and
    /// controls only: nothing the player filters, selects or is told can change a band's height.
    /// </summary>
    private ChromeBandLayout Bands(float availableWidth) =>
        ChromeBands.Layout(new ChromeMetrics(
            availableWidth,
            UiMetrics.Scale,
            ImGui.GetTextLineHeight(),
            ImGui.GetStyle().ItemSpacing.Y,
            UiMetrics.MinTarget,
            FilterPanel.QuickViewsWidth(),
            FiltersButtonWidth()));

    /// <summary>
    /// The toolbar (T14): a NightRaised strip, 36 px a row, flush with the title bar and edge to edge, with a hairline
    /// under it. Left to right: the search pill, the Quick views segmented control, the Filters button with its badge
    /// and the character chip; Help and Settings are at the rail's foot (feature plan v4 L7). Below 1000 px of
    /// available width, or whenever one row cannot hold everything, it reflows to two rows (search and quick views /
    /// filters and character) instead of hiding anything, and the quick views take a row of their own as the last
    /// resort. The rows come from the width alone (<see cref="ChromeBands"/>): the character chip has a fixed slot in
    /// that decision, so logging in as another character never reflows the toolbar.
    /// </summary>
    private void DrawToolbar(SessionState session)
    {
        if (!string.Equals(searchBuffer, ui.SearchText, StringComparison.Ordinal))
        {
            searchBuffer = ui.SearchText;
        }

        var style = ImGui.GetStyle();
        var origin = ImGui.GetCursorScreenPos();
        var avail = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var bands = Bands(avail);
        var rowHeight = bands.ToolbarRow;
        var control = UiMetrics.MinTarget;
        var gap = UiMetrics.Px(ChromeBands.ToolbarGapLogical);
        var searchWidth = bands.SearchWidth;
        var quickWidth = FilterPanel.QuickViewsWidth();
        var filtersWidth = FiltersButtonWidth();
        var twoRows = bands.ToolbarRows > 1;
        var quickOwnRow = bands.QuickViewsOwnRow;

        // The strip starts at the top of the content (the window padding above the cursor belongs to it).
        var top = origin.Y - style.WindowPadding.Y;
        var stripHeight = bands.Toolbar;
        PaintToolbarStrip(top, stripHeight);

        float RowY(int row) => top + row * rowHeight + (rowHeight - control) * 0.5f;

        // Row 0: search, then the quick views beside it (or on row 1).
        var x = origin.X;
        DrawSearchPill(new Vector2(x, RowY(0)), searchWidth, control);
        x += searchWidth + gap;
        if (quickOwnRow)
        {
            x = origin.X;
        }

        ImGui.SetCursorScreenPos(new Vector2(x, RowY(quickOwnRow ? 1 : 0)));
        filterPanel.DrawQuickViews(session.ViewedSnapshot is not null);
        x += quickWidth + gap;

        // Last row (or the same row): filters, then the character chip in the room left (its name is clipped in it).
        var lastRow = bands.ToolbarRows - 1;
        if (twoRows)
        {
            x = origin.X;
        }

        DrawFiltersButton(new Vector2(x, RowY(lastRow)), filtersWidth, control);
        x += filtersWidth + gap;
        var characterRoom = origin.X + avail - x;
        var characterWidth = twoRows
            ? MathF.Max(MathF.Min(CharacterChipWidth(), characterRoom), UiMetrics.Px(ChromeBands.CharacterMinLogical))
            : MathF.Max(1f, MathF.Min(CharacterChipWidth(), characterRoom));
        DrawCharacterChip(session, new Vector2(x, RowY(lastRow)), characterWidth, control);

        // The whole strip, then one item spanning it so the layout continues underneath.
        ui.RecordRect(UiRects.Toolbar, new Vector2(origin.X, top), new Vector2(origin.X + avail, top + stripHeight));
        ImGui.SetCursorScreenPos(new Vector2(origin.X, top));
        ImGui.Dummy(new Vector2(avail, stripHeight));
    }

    /// <summary>The raised strip behind the toolbar rows, edge to edge, with a hairline under it.</summary>
    private static void PaintToolbarStrip(float top, float height)
    {
        var windowPos = ImGui.GetWindowPos();
        var windowMax = windowPos + ImGui.GetWindowSize();
        var dl = ImGui.GetWindowDrawList();
        var s = Theme.Surface;
        var min = new Vector2(windowPos.X, top);
        var max = new Vector2(windowMax.X, top + height);

        // The window clips its padding; the strip belongs to the whole width.
        dl.PushClipRect(windowPos, windowMax, false);
        dl.AddRectFilled(min, max, Theme.U32(s.Raised));
        var line = UiMetrics.Hairline;
        if (Theme.ShowRules)
        {
            // Moon Road (R3 #10): a brass rule fading out across the window (solid under high contrast).
            Ornament.Rule(dl, new Vector2(min.X, max.Y - line), max.X - min.X, thickness: line);
        }
        else
        {
            dl.AddLine(new Vector2(min.X, max.Y - line * 0.5f), new Vector2(max.X, max.Y - line * 0.5f), Theme.U32(s.Line), line);
        }

        dl.PopClipRect();
    }

    /// <summary>
    /// The search pill: a sunken rounded field with a magnifier on the left and, while it holds text, a × clear target
    /// on the right (at least the minimum target). Ctrl+F while the window has focus puts the caret in it. The input
    /// itself is transparent inside the pill, so ImGui's own text editing and keyboard focus work as before.
    /// </summary>
    private void DrawSearchPill(Vector2 min, float width, float height)
    {
        var io = ImGui.GetIO();
        if (io.KeyCtrl && !io.WantTextInput && ImGui.IsKeyPressed(ImGuiKey.F, false) && ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows))
        {
            focusSearch = true;
        }

        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var max = min + new Vector2(width, height);
        var rounding = height * 0.5f;
        dl.AddRectFilled(min, max, Theme.U32(s.Sunken), rounding);
        dl.AddRect(min, max, searchActive ? Theme.WithAlpha(s.Text, 0.6f) : Theme.U32(s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);

        // Magnifier.
        var padLeft = UiMetrics.Px(10f);
        ImGui.PushFont(UiBuilder.IconFont);
        var iconSize = ImGui.CalcTextSize(SearchIcon);
        dl.AddText(new Vector2(min.X + padLeft, min.Y + (height - iconSize.Y) * 0.5f), Theme.U32(s.TextTertiary), SearchIcon);
        ImGui.PopFont();

        // The input, transparent, between the magnifier and the clear target.
        var clearSize = height;
        var inputX = min.X + padLeft + iconSize.X + UiMetrics.Px(6f);
        var inputWidth = MathF.Max(1f, max.X - clearSize - inputX);
        ImGui.SetCursorScreenPos(new Vector2(inputX, min.Y));
        using (ImRaii.PushColor(ImGuiCol.FrameBg, Vector4.Zero)
                   .Push(ImGuiCol.FrameBgHovered, Vector4.Zero)
                   .Push(ImGuiCol.FrameBgActive, Vector4.Zero)
                   .Push(ImGuiCol.TextDisabled, s.TextTertiary))
        using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(0f, MathF.Max(0f, (height - ImGui.GetFontSize()) * 0.5f)))
                   .Push(ImGuiStyleVar.FrameBorderSize, 0f))
        {
            if (focusSearch)
            {
                ImGui.SetKeyboardFocusHere();
                focusSearch = false;
            }

            ImGui.SetNextItemWidth(inputWidth);
            if (ImGui.InputTextWithHint("##search", Strings.SearchHint, ref searchBuffer, 200))
            {
                ui.SearchText = searchBuffer;
            }
        }

        searchActive = ImGui.IsItemActive();
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.SearchTooltip);
        }

        ui.RecordRect(UiRects.Search, min, max);

        // Clear target, only while there is something to clear.
        if (searchBuffer.Length == 0)
        {
            return;
        }

        var clearMin = new Vector2(max.X - clearSize, min.Y);
        ImGui.SetCursorScreenPos(clearMin);
        if (ImGui.InvisibleButton("##clearSearch", new Vector2(clearSize, height)))
        {
            searchBuffer = string.Empty;
            ui.SearchText = string.Empty;
        }

        var hovered = ImGui.IsItemHovered();
        var center = clearMin + new Vector2(clearSize * 0.5f, height * 0.5f);
        var half = UiMetrics.Px(4f);
        var cross = Theme.U32(hovered ? s.Text : s.TextTertiary);
        var thickness = MathF.Max(1f, UiMetrics.Px(1.5f));
        dl.AddLine(center - new Vector2(half), center + new Vector2(half), cross, thickness);
        dl.AddLine(center + new Vector2(-half, half), center + new Vector2(half, -half), cross, thickness);
        Chrome.FocusRing(rounding);
        if (hovered)
        {
            UiMetrics.Tooltip(Strings.ClearSearch);
        }
    }

    private bool focusSearch;
    private bool searchActive;

    /// <summary>The Filters button's width: icon, label and room for the badge, so the layout does not move as it appears.</summary>
    private static float FiltersButtonWidth()
    {
        ImGui.PushFont(UiBuilder.IconFont);
        var icon = ImGui.CalcTextSize(FiltersIcon).X;
        ImGui.PopFont();
        return UiMetrics.Px(10f) + icon + UiMetrics.Px(6f) + ImGui.CalcTextSize(Strings.Filters).X + UiMetrics.Px(18f);
    }

    /// <summary>
    /// The Filters button: a pill with the sliders icon and "Filters", a neutral wash while the panel is open, and a
    /// badge at its top-right counting the engaged narrowing filters (<see cref="FilterBadge"/>; the chips under the
    /// toolbar list them). Opens and closes the filter panel beside the tree.
    /// </summary>
    private void DrawFiltersButton(Vector2 min, float width, float height)
    {
        var count = FilterBadge.Count(ui.Filters);
        var size = new Vector2(width, height);
        ImGui.SetCursorScreenPos(min);

        // The panel lives on the Journal tab: from another tab the click opens it there rather than closing it unseen.
        var open = ui.FilterPanelOpen && ui.Tab == NavTab.Journal;
        if (ImGui.InvisibleButton("##filters", size))
        {
            open = !open;
            ui.FilterPanelOpen = open;
            if (open)
            {
                ui.Tab = NavTab.Journal;
            }
        }

        var hovered = ImGui.IsItemHovered();
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var max = min + size;
        var rounding = height * 0.5f;
        if (open)
        {
            dl.AddRectFilled(min, max, Theme.WithAlpha(s.Text, 0.12f), rounding);
        }
        else if (hovered)
        {
            dl.AddRectFilled(min, max, Theme.U32(s.Hover), rounding);
        }

        var ink = Theme.U32(open || hovered ? s.Text : s.TextSecondary);
        var x = min.X + UiMetrics.Px(10f);
        ImGui.PushFont(UiBuilder.IconFont);
        var iconSize = ImGui.CalcTextSize(FiltersIcon);
        dl.AddText(new Vector2(x, min.Y + (height - iconSize.Y) * 0.5f), ink, FiltersIcon);
        ImGui.PopFont();
        x += iconSize.X + UiMetrics.Px(6f);
        var labelSize = ImGui.CalcTextSize(Strings.Filters);
        dl.AddText(new Vector2(x, min.Y + (height - labelSize.Y) * 0.5f), ink, Strings.Filters);

        if (count > 0)
        {
            var badge = UiMetrics.Px(14f);
            Chrome.Badge(dl, new Vector2(max.X - badge * 0.6f, min.Y + badge * 0.35f), count, actionable: false);
        }

        Chrome.FocusRing(rounding);
        ui.RecordRect(UiRects.FiltersButton, min, max);
        if (hovered)
        {
            UiMetrics.Tooltip(Strings.FiltersTooltip, FiltersBadgeText(count));
        }
    }

    /// <summary>The badge's meaning under the Filters tooltip, rebuilt only when the count changes; null with no badge.</summary>
    private string? FiltersBadgeText(int count)
    {
        if (count != filtersBadgeCount)
        {
            filtersBadgeCount = count;
            filtersBadgeText = count switch
            {
                0 => null,
                1 => Strings.FiltersBadgeOne,
                _ => string.Format(CultureInfo.CurrentCulture, Strings.FiltersBadgeFormat, count),
            };
        }

        return filtersBadgeText;
    }

    private int filtersBadgeCount = -1;
    private string? filtersBadgeText;

    /// <summary>The character chip's natural width: job icon, name, world, pip and chevron.</summary>
    private float CharacterChipWidth()
    {
        var width = UiMetrics.Px(4f) + UiMetrics.Px(18f) + UiMetrics.Px(7f) + ImGui.CalcTextSize(characterName).X;
        if (characterWorld.Length > 0)
        {
            width += UiMetrics.Px(6f) + ImGui.CalcTextSize(characterWorld).X;
        }

        ImGui.PushFont(UiBuilder.IconFont);
        var chevron = ImGui.CalcTextSize(ChevronIcon).X * 0.7f;
        ImGui.PopFont();
        return width + UiMetrics.Px(8f) + UiMetrics.Px(8f) + UiMetrics.Px(6f) + chevron + UiMetrics.Px(10f);
    }

    /// <summary>
    /// The character chip (ui-revamp §2.1): a sunken pill with the current job's icon in a circle, the name, the world
    /// in the secondary tone, a static pip (filled Moon for the live character, hollow for a snapshot; accessibility
    /// B5, nothing breathes) and a chevron. Click, Enter or Space opens the character list under it. When the chip is
    /// narrower than its content the name is clipped; the pip and the chevron always show.
    /// </summary>
    private void DrawCharacterChip(SessionState session, Vector2 min, float width, float height)
    {
        var size = new Vector2(width, height);
        var max = min + size;
        ImGui.SetCursorScreenPos(min);
        if (ImGui.InvisibleButton("##character", size))
        {
            RebuildCharacterLabels(session);
            ImGui.OpenPopup(CharacterPopupId);
        }

        var hovered = ImGui.IsItemHovered();
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var rounding = height * 0.5f;
        dl.AddRectFilled(min, max, Theme.U32(hovered ? s.Hover : s.Sunken), rounding);
        dl.AddRect(min, max, Theme.U32(s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        Chrome.FocusRing(rounding);
        ui.RecordRect(UiRects.Character, min, max);

        // Right end first: chevron, then the pip before it; the text gets what is left.
        ImGui.PushFont(UiBuilder.IconFont);
        var chevronSize = ImGui.CalcTextSize(ChevronIcon) * 0.7f;
        var chevronPos = new Vector2(max.X - UiMetrics.Px(10f) - chevronSize.X, min.Y + (height - chevronSize.Y) * 0.5f);
        dl.AddText(UiBuilder.IconFont, ImGui.GetFontSize() * 0.7f, chevronPos, Theme.U32(s.TextTertiary), ChevronIcon);
        ImGui.PopFont();

        var hasCharacter = session.ViewedSnapshot is not null;
        var pipBox = UiMetrics.Px(14f);
        var pipCenter = new Vector2(chevronPos.X - UiMetrics.Px(6f) - pipBox * 0.5f, min.Y + height * 0.5f);
        if (hasCharacter)
        {
            Marks.Draw(dl, pipCenter, pipBox, PipFor(session));
            ui.RecordRect(UiRects.Sync, pipCenter - new Vector2(pipBox * 0.5f), pipCenter + new Vector2(pipBox * 0.5f));
        }
        else
        {
            ui.Rects.Remove(UiRects.Sync);
        }

        // Job icon in a circle.
        var iconSize = UiMetrics.Px(18f);
        var iconMin = new Vector2(min.X + UiMetrics.Px(4f), min.Y + (height - iconSize) * 0.5f);
        var iconMax = iconMin + new Vector2(iconSize);
        if (GameIcon.TryGetWrap(textures, characterJobIcon, iconSize, out var wrap))
        {
            dl.AddImageRounded(wrap.Handle, iconMin, iconMax, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, iconSize * 0.5f);
        }
        else
        {
            dl.AddCircleFilled(iconMin + new Vector2(iconSize * 0.5f), iconSize * 0.5f, Theme.U32(s.StrongLine));
        }

        // Name and world, clipped short of the pip.
        var textX = iconMax.X + UiMetrics.Px(7f);
        var textRight = pipCenter.X - pipBox * 0.5f - UiMetrics.Px(4f);
        var nameSize = ImGui.CalcTextSize(characterName);
        var textY = min.Y + (height - nameSize.Y) * 0.5f;
        dl.PushClipRect(new Vector2(textX, min.Y), new Vector2(MathF.Max(textX, textRight), max.Y), true);
        dl.AddText(new Vector2(textX, textY), Theme.U32(s.Text), characterName);
        if (characterWorld.Length > 0)
        {
            dl.AddText(new Vector2(textX + nameSize.X + UiMetrics.Px(6f), textY), Theme.U32(s.TextSecondary), characterWorld);
        }

        dl.PopClipRect();

        if (hovered)
        {
            UiMetrics.Tooltip(characterTooltip);
        }

        DrawCharacterMenu(session, new Vector2(min.X, max.Y + UiMetrics.Px(4f)));
    }

    /// <summary>The character list opened from the chip, placed under it; the popup scales its own font.</summary>
    private void DrawCharacterMenu(SessionState session, Vector2 position)
    {
        // Next-window data must only be set when the popup will begin, or it would land on the next child window.
        if (!ImGui.IsPopupOpen(CharacterPopupId))
        {
            return;
        }

        ImGui.SetNextWindowPos(position, ImGuiCond.Appearing);
        using var popup = ImRaii.Popup(CharacterPopupId);
        if (!popup)
        {
            return;
        }

        UiMetrics.ApplyFontScale();
        if (characterLabels.Count == 0)
        {
            ImGui.TextDisabled(Strings.NoSnapshots);
            return;
        }

        foreach (var (id, label) in characterLabels)
        {
            if (ImGui.Selectable(label, session.ViewedContentId == id))
            {
                session.ViewCharacter(id);
            }

            if (!ImGui.IsItemHovered())
            {
                continue;
            }

            if (session.NotUpdating.TryGetValue(id, out var problem))
            {
                UiMetrics.Tooltip(CharactersPane.NotUpdatingText(problem));
            }
            else if (session.IsLiveElsewhere(id))
            {
                UiMetrics.Tooltip(Strings.MultiboxLiveElsewhereTooltip);
            }
        }
    }

    /// <summary>
    /// The chip's and status bar's pip: filled for the character live here, a ringed dot for one live in another game
    /// client (multibox, D11), hollow for a stored snapshot.
    /// </summary>
    private static Mark PipFor(SessionState session)
    {
        if (session.IsLive)
        {
            return session.PollerHealthy ? Mark.LivePip : Mark.SnapshotPip;
        }

        return session.ViewedContentId is { } viewed && session.IsLiveElsewhere(viewed) ? Mark.ElsewherePip : Mark.SnapshotPip;
    }

    /// <summary>
    /// The switcher's entries (1.8.0, R7 B, E): the alt lists' one stable order (live here, live elsewhere, then by name
    /// and world), hidden characters left out unless Settings › Data shows them, each badged as the Characters list does.
    /// </summary>
    private void RebuildCharacterLabels(SessionState session)
    {
        characterLabels.Clear();
        var now = DateTime.UtcNow;
        foreach (var entry in Core.Characters.CharacterList.Visible(plugin.Roster.All, plugin.Settings.ShowHiddenCharacters))
        {
            var name = string.Format(CultureInfo.CurrentCulture, Strings.CharacterNameFormat, entry.Name, entry.WorldName);
            var label = entry.LiveHere
                ? Strings.LiveMarker + name
                : entry.LiveElsewhere
                    ? Strings.MultiboxMarker + name + " · " + Strings.MultiboxLiveElsewhere
                    : string.Format(CultureInfo.CurrentCulture, Strings.CharacterEntryFormat, entry.Name, entry.WorldName, UiFormat.Age(entry.TakenUtc, now));
            if (session.NotUpdating.ContainsKey(entry.ContentId))
            {
                label += " · " + Strings.AltsNotUpdatingBadge;
            }

            if (!entry.Tracked)
            {
                label += " · " + Strings.AltsUntrackedBadge;
            }

            if (entry.Hidden)
            {
                label += " · " + Strings.AltsHiddenBadge;
            }

            // The content id keeps the ImGui id unique when two snapshots share a name and world.
            characterLabels.Add((entry.ContentId, label + "##" + entry.ContentId.ToString(CultureInfo.InvariantCulture)));
        }
    }

    /// <summary>
    /// The body (feature plan v4 L1): rail · tree · centre · detail side by side, their widths from
    /// <see cref="PaneSplit"/> (floors that hold, widths in logical units, a double-click to reset). Each pane is a child
    /// window of its width placed on one line; the detail column is a group so the What's new and Since you were away
    /// cards stack above the detail pane inside it.
    /// </summary>
    private void DrawBody(SessionState session, CatalogBundle bundle)
    {
        var style = ImGui.GetStyle();
        var statusHeight = ChromeBands.StatusBarHeight(ImGui.GetTextLineHeight(), style.ItemSpacing.Y);
        var height = MathF.Max(UiMetrics.MinBodyHeight, ImGui.GetContentRegionAvail().Y - statusHeight);
        var total = ImGui.GetContentRegionAvail().X;
        var settings = plugin.Settings;
        bodyMin = ImGui.GetCursorScreenPos();
        bodyHeight = height;

        // The strip is the Journal tree's alone for now: the other tabs' lists keep their width. The filter panel is a
        // drawer over the tree (feature plan v6 U2), so it no longer takes the column.
        var stripAllowed = ui.Tab == NavTab.Journal;

        // The rail keeps its own width (it follows the UI scale every frame): 64 logical px with labels, or the 44 px
        // compact rail on a narrow window or by setting (feature plan v4 L7).
        tabStrip.UpdateMode(ImGui.GetWindowSize().X / ImGuiHelpers.GlobalScale, UiMetrics.FontScale, settings.CompactRail);
        var widths = PaneSplit.Solve(settings, total, tabStrip.RailWidth, stripAllowed);

        // A rail taller than a short window scrolls with the wheel, without a scrollbar eating its width.
        using (var rail = ImRaii.Child("##rail", new Vector2(widths.Rail, height), false, ImGuiWindowFlags.NoScrollbar))
        {
            if (rail)
            {
                var counts = runner.Counts;
                tabStrip.Draw(counts?.Overall ?? default, counts?.OverallReady ?? 0, openHelp, openSettings);
            }
        }

        ImGui.SameLine(0f, 0f);
        PaneSplit.Divider("##railLine", in widths, height);

        ImGui.SameLine(0f, 0f);
        DrawNavigation(session, bundle, in widths, height);

        ImGui.SameLine(0f, 0f);
        var changed = paneSplit.Handle(PaneSide.Tree, settings, in widths, height, total, stripAllowed);

        ImGui.SameLine(0f, 0f);
        PaneGradientAtCursor(widths.Centre, height);
        // 0 would mean "fill the rest" to ImGui: a pane squeezed to nothing stays 1 px wide instead of covering the detail pane.
        using (var center = ImRaii.Child("##center", new Vector2(MathF.Max(1f, widths.Centre), height)))
        {
            if (center)
            {
                switch (ui.Tab)
                {
                    case NavTab.Moonlit when moonlitPane is not null:
                        moonlitPane.DrawMain(ui);
                        break;
                    case NavTab.Characters when charactersPane is not null:
                        charactersPane.DrawMain(ui);
                        break;
                    case NavTab.Flight when flightPane is not null:
                        flightPane.DrawMain(ui);
                        break;
                    case NavTab.Plan when planPane is not null:
                        planPane.DrawMain(ui);
                        break;
                    default:
                        tablePane.Draw(session.ViewedSnapshot is not null);
                        break;
                }
            }
        }

        ImGui.SameLine(0f, 0f);
        changed |= paneSplit.Handle(PaneSide.Detail, settings, in widths, height, total, stripAllowed);
        if (changed)
        {
            settingsDirtyAtUtc ??= DateTime.UtcNow;
        }

        // The detail column runs to the window's edge: its children take the content region's width. With the pane
        // gradient its Night backdrop is painted here, under the gradient, and the panels inside leave their own clear.
        ImGui.SameLine(0f, 0f);
        var gradient = Theme.ShowPaneGradient;
        if (gradient)
        {
            var min = ImGui.GetCursorScreenPos();
            var max = min + new Vector2(ImGui.GetContentRegionAvail().X, height);
            var dl = ImGui.GetWindowDrawList();
            dl.AddRectFilled(min, max, Theme.U32(Theme.Surface.Window), style.ChildRounding);
            Ornament.PaneGradient(dl, min, max);
        }

        using var backdrop = Theme.PushPaneBackdrop(gradient);
        using var detailColumn = ImRaii.Group();

        // A selected quest's details always start at the top of the column (feature plan v6 U2): the Setup, What's new
        // and Since you were away cards live in the no-selection slot, above the Tonight card, and while a quest is
        // selected the dock says one is waiting (DrawFloating).
        if (ui.SelectedRowId is not null)
        {
            detailPane.Draw(session, bundle, new Vector2(0f, height));
            return;
        }

        var detailHeight = height;
        switch (DueCard())
        {
            case NoticeKind.Setup:
                detailHeight -= setupCard!.Draw(height);
                break;
            case NoticeKind.WhatsNew:
                detailHeight -= whatsNew!.Draw(height);
                break;
            case NoticeKind.WelcomeBack:
                detailHeight -= welcomeBack!.Draw(height, bundle);
                break;
        }

        // Nothing selected: the Tonight card answers "what now" in the detail column (game UX panel finding 1).
        tonightCard.Draw(session, bundle, new Vector2(0f, detailHeight));
    }

    /// <summary>
    /// The detail-column card that is due, if any, in their order: "Set up your road" first (it shows by itself only on
    /// a fresh install, where What's new never does, and otherwise only when Help asked for it, and never during the
    /// tour), then What's new, then Since you were away.
    /// </summary>
    private NoticeKind? DueCard()
    {
        if (setupCard is { Visible: true } && tutorial?.Active != true)
        {
            return NoticeKind.Setup;
        }

        if (whatsNew is { Visible: true })
        {
            return NoticeKind.WhatsNew;
        }

        return welcomeBack is { Visible: true } ? NoticeKind.WelcomeBack : null;
    }

    /// <summary>
    /// The navigation column: the body of the tab the rail selected (<see cref="UiState.Tab"/> is the single source
    /// of truth, so a programmatic switch shows on the next frame with nothing to reconcile).
    /// </summary>
    private void DrawNavigation(SessionState session, CatalogBundle bundle, in PaneWidths widths, float height)
    {
        PaneGradientAtCursor(widths.Tree, height);
        leftMin = ImGui.GetCursorScreenPos();
        leftWidth = MathF.Max(1f, widths.Tree);
        using var left = ImRaii.Child("##left", new Vector2(MathF.Max(1f, widths.Tree), height));
        if (!left)
        {
            return;
        }

        if (widths.TreeStrip)
        {
            // Dragged shut: the Journal tree as a strip of icons (only ever on the Journal tab).
            treePane.DrawStrip(bundle, runner, plugin.Settings.ShowUnlisted);
            return;
        }

        DrawTabBody(ui.Tab, session, bundle);
    }

    /// <summary>
    /// The pane gradient (moon-road proposal §5, sky over water) behind a column about to be drawn at the cursor, on the
    /// window's draw list under the column's clear child background, at the user's window opacity. Full flair only.
    /// </summary>
    private static void PaneGradientAtCursor(float width, float height)
    {
        if (!Theme.ShowPaneGradient || !(width > 1f))
        {
            return;
        }

        var min = ImGui.GetCursorScreenPos();
        Ornament.PaneGradient(ImGui.GetWindowDrawList(), min, min + new Vector2(width, height), alpha: Theme.WindowAlpha);
    }

    /// <summary>Left-column body of the active navigation tab.</summary>
    private void DrawTabBody(NavTab tab, SessionState session, CatalogBundle bundle)
    {
        switch (tab)
        {
            case NavTab.Journal:
                // The filter panel is a drawer over the tree (DrawFloating), so the tree never moves.
                treePane.Draw(bundle, runner, plugin.Settings.ShowUnlisted);
                break;

            case NavTab.Moonlit when moonlitPane is not null:
                moonlitPane.DrawLeft(ui);
                break;

            case NavTab.Characters when charactersPane is not null:
                charactersPane.DrawLeft(ui);
                break;

            case NavTab.Flight when flightPane is not null:
                flightPane.DrawLeft(ui);
                break;

            case NavTab.Plan when planPane is not null:
                planPane.DrawLeft(ui);
                break;

            default:
                ImGui.TextDisabled(Strings.Placeholder);
                break;
        }
    }

    /// <summary>
    /// The status bar (T12, ui-revamp §2.6), left to right: the catalog counts, a static pip with "live" or the snapshot
    /// time, the MSQ pill (click selects the next quest), and the version right-aligned in Dusk. The overall gauge is at
    /// the rail's foot (feature plan v4 L7). Segments are separated by a Veil "·". When the line is too narrow the
    /// counts and then the MSQ pill end in an ellipsis; the pip and the version always show. Strings are rebuilt only
    /// when their inputs change.
    /// </summary>
    private void DrawStatusBar(SessionState session, CatalogBundle bundle)
    {
        var snapshot = session.ViewedSnapshot;
        var key = (
            bundle.Catalog.Count,
            runner.Rows.Length,
            runner.TotalInScope,
            session.IsLive,
            snapshot is null ? -1L : snapshot.TakenUtc.Ticks / TimeSpan.TicksPerMinute);
        if (key != statusKey)
        {
            statusKey = key;
            statusMode = session.IsLive
                ? Strings.StatusLive
                : snapshot is null
                    ? Strings.StatusNoSnapshot
                    : string.Format(CultureInfo.CurrentCulture, Strings.StatusSnapshotFormat, UiFormat.Time(snapshot.TakenUtc));
            status = string.Format(CultureInfo.CurrentCulture, Strings.StatusFormat, bundle.Catalog.Count, runner.Rows.Length, runner.TotalInScope);
        }

        if (versionText.Length == 0)
        {
            versionText = string.Format(CultureInfo.InvariantCulture, Strings.StatusVersionFormat, version);
        }

        RefreshMsq(session, bundle);

        // The whole bar is in the caption role (ui-revamp §4.2): 0.85× the body, never under 12 px.
        using var caption = Typography.Caption();
        var barMin = ImGui.GetCursorScreenPos();
        if (Theme.ShowRules)
        {
            // Moon Road (R3 #10): the separator over the bar is a brass rule, as wide as the separator was.
            var rule = new Vector2(ImGui.GetWindowPos().X, barMin.Y);
            var thickness = UiMetrics.Hairline;
            ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, thickness));
            Ornament.Rule(ImGui.GetWindowDrawList(), rule, ImGui.GetWindowSize().X, thickness: thickness);
        }
        else
        {
            ImGui.Separator();
        }

        var dl = ImGui.GetWindowDrawList();
        var line = ImGui.GetTextLineHeight();
        var rowHeight = line;
        var origin = ImGui.GetCursorScreenPos();
        var right = origin.X + ImGui.GetContentRegionAvail().X;
        var textY = origin.Y + (rowHeight - line) * 0.5f;
        var gap = UiMetrics.Px(6f);
        var separatorWidth = ImGui.CalcTextSize(StatusSeparator).X + 2f * gap;
        var x = origin.X;

        // Version, right-aligned in Dusk; it carries the data stamp on hover.
        var versionWidth = ImGui.CalcTextSize(versionText).X;
        var versionX = MathF.Max(x, right - versionWidth);
        StatusText(versionX, textY, versionText, Theme.U32(Theme.Surface.TextTertiary));
        if (DataStamp is { } stamp && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(stamp);
        }

        // The middle: counts, pip + mode, MSQ pill, fitted into what is left before the version.
        var pipSize = line;
        var modeWidth = pipSize + UiMetrics.Px(2f) + ImGui.CalcTextSize(statusMode).X;
        var pillPad = UiMetrics.Px(7f);
        var msqTextWidth = msqStatus.Length > 0 ? ImGui.CalcTextSize(msqStatus).X : 0f;
        var msqWidth = msqStatus.Length > 0 ? msqTextWidth + 2f * pillPad : 0f;
        var statusWidth = ImGui.CalcTextSize(status).X;
        var room = versionX - gap - x;

        var fixedWidth = separatorWidth + modeWidth + (msqWidth > 0f ? separatorWidth : 0f);
        var statusRoom = statusWidth;
        var msqRoom = msqWidth;
        if (statusWidth + fixedWidth + msqWidth > room)
        {
            // Too narrow for everything: the counts keep at least their floor, the MSQ pill gets the rest, and
            // whichever does not fit ends in an ellipsis instead of running into the version.
            statusRoom = MathF.Max(0f, MathF.Min(MathF.Max(MathF.Min(statusWidth, UiMetrics.Px(StatusMinLogical)), room - fixedWidth - msqWidth), statusWidth));
            msqRoom = MathF.Max(0f, room - statusRoom - fixedWidth);
        }

        // The counts open the bar, so no separator comes before them.
        if (statusRoom > 0f)
        {
            ImGui.SetCursorScreenPos(new Vector2(x, textY));
            Chrome.EllipsisText(status, statusRoom, ImGui.GetColorU32(ImGuiCol.TextDisabled), statusWidth);
            if (DataStamp is { } stampAgain && ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(stampAgain);
            }

            x += statusRoom;
        }

        if (x + separatorWidth + modeWidth <= versionX)
        {
            // Static pip (accessibility B5: nothing here moves) and the live / snapshot words.
            x = x > origin.X ? StatusSeparatorAt(dl, x, textY, gap) : x;
            ImGui.SetCursorScreenPos(new Vector2(x, textY));
            Marks.DrawInline(PipFor(session), pipSize);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(syncTooltip);
            }

            x = StatusText(x + pipSize + UiMetrics.Px(2f), textY, statusMode, ImGui.GetColorU32(ImGuiCol.TextDisabled));
            if (ImGui.IsItemHovered())
            {
                // Browse mode and a stored character's snapshot are said here, not in a banner over the panes (U2).
                UiMetrics.Tooltip(syncTooltip);
            }
        }

        if (msqWidth > 0f && msqRoom > 2f * pillPad && x + separatorWidth + msqRoom <= versionX + 0.5f)
        {
            x = x > origin.X ? StatusSeparatorAt(dl, x, textY, gap) : x;
            var pillMin = new Vector2(x, textY - UiMetrics.Px(1f));
            var pillMax = new Vector2(x + msqRoom, textY + line + UiMetrics.Px(1f));
            dl.AddRectFilled(pillMin, pillMax, MsqPillFill, (pillMax.Y - pillMin.Y) * 0.5f);
            ImGui.SetCursorScreenPos(new Vector2(x + pillPad, textY));
            Chrome.EllipsisText(msqStatus, msqRoom - 2f * pillPad, Theme.AccentU32, msqTextWidth);
            // Only while this window is the one under the mouse: another window (Settings, the Todo overlay, a popup)
            // covering the bar gets neither the tooltip nor the hand.
            if (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(pillMin, pillMax))
            {
                UiMetrics.Tooltip(msqTooltip);
                if (msq?.Next is { } next)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        SelectMsq(next);
                    }
                }
            }
        }

        DrawQuestionableStatus(dl, ref x, textY, gap, separatorWidth, versionX, origin.X);

        // One item spanning the bar so the layout advances past it.
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(MathF.Max(0f, right - origin.X), rowHeight));

        var windowX = ImGui.GetWindowPos().X;
        ui.RecordRect(UiRects.StatusBar, new Vector2(windowX + ImGui.GetWindowContentRegionMin().X, barMin.Y), new Vector2(windowX + ImGui.GetWindowContentRegionMax().X, origin.Y + rowHeight));

        // The floating layers keep above the bar (FloatingLayers, DrawFloating).
        statusTop = barMin.Y;
    }

    /// <summary>The host name of this window's Questionable confirmations.</summary>
    internal const string QuestionableHost = "main";

    /// <summary>
    /// Questionable's live status (feature plan v5, 1.6.0) after the MSQ pill, in the room the bar has left:
    /// "Questionable: running · &lt;quest&gt; · step 3 of 7" in gold, ending in an ellipsis, and a small Stop. Polled at
    /// most once a second, and only while this window draws; nothing shows while Questionable does not run.
    /// </summary>
    private void DrawQuestionableStatus(ImDrawListPtr dl, ref float x, float textY, float gap, float separatorWidth, float versionX, float left)
    {
        if (questionableActions?.PollStatusText() is not { } text)
        {
            return;
        }

        var stopWidth = ImGui.CalcTextSize(Strings.QuestionableStopShort).X + (ImGui.GetStyle().FramePadding.X * 2f);
        var textWidth = ImGui.CalcTextSize(text).X;
        var start = x > left ? x + separatorWidth : x;
        var room = MathF.Min(textWidth, versionX - gap - start - gap - stopWidth);
        if (room < UiMetrics.Px(48f))
        {
            return;
        }

        x = x > left ? StatusSeparatorAt(dl, x, textY, gap) : x;
        ImGui.SetCursorScreenPos(new Vector2(x, textY));
        Chrome.EllipsisText(text, room, Theme.AccentU32, textWidth);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(textWidth > room ? text : Strings.QuestionableStatusTooltip, textWidth > room ? Strings.QuestionableStatusTooltip : null);
        }

        x += room + gap;
        ImGui.SetCursorScreenPos(new Vector2(x, textY));
        using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(ImGui.GetStyle().FramePadding.X, 0f)))
        {
            questionableActions.DrawStopSmallButton(QuestionableHost, "##questionableStop");
        }

        x = ImGui.GetItemRectMax().X;
    }

    /// <summary>The status bar's segment separator, a Veil "·" with a gap either side.</summary>
    private const string StatusSeparator = "·";

    /// <summary>Draws a separator at <paramref name="x"/> and returns where the next segment starts.</summary>
    private static float StatusSeparatorAt(ImDrawListPtr dl, float x, float y, float gap)
    {
        dl.AddText(new Vector2(x + gap, y), Theme.U32(Theme.Surface.TextDisabled), StatusSeparator);
        return x + gap + ImGui.CalcTextSize(StatusSeparator).X + gap;
    }

    /// <summary>One status text as an item (so it can carry a tooltip) at a fixed position; returns its right edge.</summary>
    private static float StatusText(float x, float y, string text, uint color)
    {
        ImGui.SetCursorScreenPos(new Vector2(x, y));
        using (ImRaii.PushColor(ImGuiCol.Text, color))
        {
            ImGui.TextUnformatted(text);
        }

        return ImGui.GetItemRectMax().X;
    }

    /// <summary>
    /// Recomputes the main scenario position and its two strings when the session version or catalog changed. Hidden
    /// (empty strings) without a character, since every quest would read as unknown.
    /// </summary>
    private void RefreshMsq(SessionState session, CatalogBundle bundle)
    {
        if (msqVersion == session.Version && ReferenceEquals(msqBundle, bundle))
        {
            return;
        }

        msqVersion = session.Version;
        msqBundle = bundle;
        msq = session.ViewedSnapshot is null || session.States.Count == 0 ? null : MsqProgress.Compute(bundle.Catalog, session.States);
        if (msq is not { } position)
        {
            msqStatus = string.Empty;
            msqTooltip = string.Empty;
            return;
        }

        if (position.Next is not { } next)
        {
            msqStatus = Strings.StatusMsqComplete;
            msqTooltip = string.Format(CultureInfo.CurrentCulture, Strings.MsqCompleteFormat, position.Done, position.Total);
            return;
        }

        var spoilers = session.Spoilers;
        var expansion = bundle.Names.Expansion(next.Expansion) is { Length: > 0 } named ? named : Expansions.Name(next.Expansion);
        var tooltip = string.Format(CultureInfo.CurrentCulture, Strings.MsqProgressFormat, expansion, position.Done, position.Total);
        if (position.IsBranched)
        {
            // Inside a branch region: every route with its progress; a click selects the first route's next quest.
            msqStatus = string.Format(CultureInfo.CurrentCulture, Strings.StatusMsqRoutesFormat, MsqText.Compact(position, spoilers.DisplayName));
            msqTooltip = tooltip + "\n" + string.Join("\n", MsqText.Lines(position, spoilers.DisplayName)) + "\n"
                + MsqText.JoinLine(position, spoilers.DisplayName) + "\n" + Strings.MsqClickHint;
            return;
        }

        msqStatus = string.Format(CultureInfo.CurrentCulture, Strings.StatusMsqFormat, spoilers.DisplayName(next));
        if (next.Issuer is { } issuer)
        {
            var zone = links.Map(issuer.MapId)?.PlaceName ?? string.Empty;
            tooltip += "\n" + (zone.Length > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.MsqGiverFormat, issuer.Name, zone) : issuer.Name);
        }

        msqTooltip = tooltip + "\n" + Strings.StateName(position.State, next) + "\n" + Strings.MsqClickHint;
    }

    /// <summary>Selects the next main scenario quest in the Journal tab; an active preset would hide it, so it is cleared first.</summary>
    private void SelectMsq(QuestRecord quest)
    {
        if (ui.Filters.Preset != Preset.None)
        {
            ui.Filters.Preset = Preset.None;
            OnFiltersChanged();
        }

        ui.Reveal(quest);
    }
}
