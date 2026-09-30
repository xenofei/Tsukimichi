using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
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

namespace Tsukimichi.Ui;

/// <summary>
/// The main window (spec §2.4): toolbar, three resizable columns (navigation, quest table, detail) and a status bar.
/// Owns the per-window <see cref="UiState"/> contract for its panes and persists the filters into the configuration.
/// Reads <see cref="Plugin.Session"/>, <see cref="Plugin.Settings"/> and <see cref="Plugin.Paths"/> lazily because the
/// window is constructed before the game-state block initializes them.
/// </summary>
public sealed class MainWindow : Window, IDisposable
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

    // Attached after the game state exists (they need the session); null until then.
    private MoonlitPane? moonlitPane;
    private CharactersPane? charactersPane;
    private FlightPane? flightPane;

    // Toolbar actions and the tutorial overlay, attached by the plugin once those windows exist.
    private Action? openSettings;
    private Action? openHelp;
    private Action? startTutorial;
    private ITutorial? tutorial;
    private WhatsNewCard? whatsNew;

    private Task? retryTask;
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
    private string staleBanner = string.Empty;
    private string syncTooltip = Strings.NoCharacter;
    private readonly List<(ulong Id, string Label)> characterLabels = [];

    // Status bar string, rebuilt when its inputs change.
    private (int Catalog, int Rows, int Total, bool Live, long SnapshotMinute) statusKey = (-1, -1, -1, false, -1);
    private string status = string.Empty;
    private string statusMode = string.Empty;
    private string versionText = string.Empty;

    // The overall halo's percentage and its tooltip, rebuilt when the overall count changes.
    private NodeCount statusOverall = new(-1, -1, -1);
    private string statusPercent = string.Empty;
    private string statusProgress = string.Empty;

    /// <summary>The least width the status text keeps when the MSQ segment crowds it, in logical pixels.</summary>
    private const float StatusMinLogical = 120f;

    /// <summary>Motion key of the status bar's overall halo: its fill eases only when the overall count changes.</summary>
    private static readonly ulong StatusGaugeKey = Motion.Key(0x5354_4147, 0); // "STAG"

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
        SizeConstraints = new WindowSizeConstraints { MinimumSize = ScaleMetrics.MinWindowSize(ScaleMetrics.DefaultUiScale) };

        filterPanel = new FilterPanel(ui, OnFiltersChanged, OnDisplayChanged);
        ui.FiltersChanged += OnFiltersChanged;
        tabStrip = new TabStrip(ui);
        treePane = new TreePane(ui);
        tablePane = new TablePane(ui, runner, links, textures, pluginInterface, log, filterPanel.ResetAll, OnFiltersChanged);
        detailPane = new DetailPane(ui, runner, links, textures, log);
        tonightCard = new TonightCard(ui, runner, OnFiltersChanged);

        version = typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0";
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

    /// <summary>Gives the detail pane the user's unique-reward verdicts so it can show and change them.</summary>
    public void AttachOverrides(IUniqueOverrides overrides)
    {
        detailPane.Overrides = overrides ?? throw new ArgumentNullException(nameof(overrides));
    }

    /// <summary>The detail pane's Report button and the status bar's data stamp tooltip.</summary>
    public void AttachDiagnostics(DiagnosticBuilder diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        detailPane.Diagnostics = diagnostics;
        DataStamp = diagnostics.DataStampLine;
    }

    /// <summary>
    /// Wires the toolbar's Settings, Help and Tutorial buttons. Until this is called the buttons are drawn disabled,
    /// so the toolbar layout never changes.
    /// </summary>
    public void AttachActions(Action openSettings, Action openHelp, Action startTutorial)
    {
        this.openSettings = openSettings ?? throw new ArgumentNullException(nameof(openSettings));
        this.openHelp = openHelp ?? throw new ArgumentNullException(nameof(openHelp));
        this.startTutorial = startTutorial ?? throw new ArgumentNullException(nameof(startTutorial));
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

        // The rail and the fixed side columns grow with the UI scale, so the minimum size must too or the centre column
        // collapses; it never exceeds the viewport, so the window can always be placed whole.
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = ScaleMetrics.MinWindowSize(UiMetrics.FontScale, ImGuiHelpers.GlobalScale, ImGuiHelpers.MainViewport.WorkSize),
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
    /// The opt-in shortcuts of Settings › Keyboard (all off by default; accessibility A7): Ctrl+1..4 switch tabs, F
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
            runner.TogglePin(rowId);
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

    /// <summary>Ctrl+1..4 in <see cref="NavTab"/> order.</summary>
    private static readonly ImGuiKey[] TabKeys = [ImGuiKey.Key1, ImGuiKey.Key2, ImGuiKey.Key3, ImGuiKey.Key4];

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
        RefreshToolbarStrings(session);
        DrawToolbar(session);
        DrawChipRow(session);
        DrawBanners(session);
        DrawBody(session, bundle);
        DrawStatusBar(session, bundle);

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
    /// matching quest links to chat.
    /// </summary>
    public void SearchAndPrint(string text)
    {
        EnsureInitialized();
        ui.SearchText = text;
        searchBuffer = text;
        IsOpen = true;
        runner.FlushSearch();

        if (plugin.Session?.Bundle is not { } bundle)
        {
            links.PrintText(Strings.CatalogNotReady);
            return;
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

    private static void DrawLoading()
    {
        ImGui.TextUnformatted(Strings.LoadingCatalog);
        ImGui.SameLine(0f, 0f);
        ImGui.TextUnformatted(LoadingDots[(int)(ImGui.GetTime() * 2.0) % LoadingDots.Length]);
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
            staleBanner = string.Empty;
            syncTooltip = Strings.NoCharacter;
            characterTooltip = Strings.CharacterChipTooltip;
            return;
        }

        characterName = snapshot.Name;
        characterWorld = links.WorldName(snapshot.World);
        characterJobIcon = snapshot.CurrentJob == 0 ? 0u : MoonlitIconResolver.ClassJobIconBase + snapshot.CurrentJob;
        if (session.IsLive)
        {
            staleBanner = string.Empty;
            syncTooltip = session.PollerHealthy ? Strings.SyncLive : Strings.SyncPollerPaused;
        }
        else
        {
            var time = UiFormat.Time(snapshot.TakenUtc);
            staleBanner = string.Format(CultureInfo.CurrentCulture, Strings.StaleBannerFormat, snapshot.Name, links.WorldName(snapshot.World), time);
            syncTooltip = string.Format(CultureInfo.CurrentCulture, Strings.SyncSnapshotFormat, time);
        }

        characterTooltip = syncTooltip + "\n" + Strings.CharacterChipTooltip;
    }

    // ------------------------------------------------------------------ toolbar (T14, ui-revamp §2.1)

    /// <summary>Logical height of one toolbar row.</summary>
    private const float ToolbarRowLogical = 36f;

    /// <summary>Below this much available width (pixels) the toolbar always takes two rows (accessibility B6).</summary>
    private const float ToolbarReflowPx = 1000f;

    private const float ToolbarGapLogical = 8f;
    private const float SearchLogical = 280f;
    private const float SearchMinLogical = 160f;
    private const float CharacterMinLogical = 120f;
    private const string CharacterPopupId = "##characterMenu";

    private static readonly string SearchIcon = FontAwesomeIcon.Search.ToIconString();
    private static readonly string FiltersIcon = FontAwesomeIcon.SlidersH.ToIconString();
    private static readonly string ChevronIcon = FontAwesomeIcon.ChevronDown.ToIconString();
    private static readonly string HelpIcon = FontAwesomeIcon.QuestionCircle.ToIconString();
    private static readonly string TutorialIcon = FontAwesomeIcon.GraduationCap.ToIconString();
    private static readonly string SettingsIcon = FontAwesomeIcon.Cog.ToIconString();

    /// <summary>
    /// The toolbar (T14): a NightRaised strip, 36 px a row, flush with the title bar and edge to edge, with a hairline
    /// under it. Left to right: the search pill, the Quick views segmented control, the Filters button with its badge,
    /// the character chip, and the round Help, Tutorial and Settings buttons right-aligned. Below 1000 px of available
    /// width, or whenever one row cannot hold everything, it reflows to two rows (search and quick views / filters,
    /// character and buttons) instead of hiding anything; a row that still cannot fit shrinks the search pill and the
    /// character chip to their floors, and the quick views take a row of their own as the last resort.
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
        var rowHeight = MathF.Max(UiMetrics.Px(ToolbarRowLogical), UiMetrics.MinTarget + UiMetrics.Px(6f));
        var control = UiMetrics.MinTarget;
        var gap = UiMetrics.Px(ToolbarGapLogical);
        var clusterGap = UiMetrics.Px(4f);

        var searchWidth = UiMetrics.Px(SearchLogical);
        var quickWidth = FilterPanel.QuickViewsWidth();
        var filtersWidth = FiltersButtonWidth();
        var characterWidth = CharacterChipWidth();
        var clusterWidth = 3f * control + 2f * clusterGap;
        var oneRow = searchWidth + quickWidth + filtersWidth + characterWidth + clusterWidth + 4f * gap;

        // Row layout: row 0 holds the search (and the quick views when they fit beside it); the last row holds the
        // filters, the character chip and the buttons.
        var twoRows = avail < ToolbarReflowPx || oneRow > avail;
        var quickOwnRow = false;
        if (twoRows)
        {
            var searchMin = UiMetrics.Px(SearchMinLogical);
            searchWidth = MathF.Min(searchWidth, avail - gap - quickWidth);
            if (searchWidth < searchMin)
            {
                quickOwnRow = true;
                searchWidth = MathF.Min(UiMetrics.Px(SearchLogical), avail);
            }

            characterWidth = MathF.Max(MathF.Min(characterWidth, avail - filtersWidth - clusterWidth - 2f * gap), UiMetrics.Px(CharacterMinLogical));
        }

        var rows = twoRows ? (quickOwnRow ? 3 : 2) : 1;

        // The strip starts at the top of the content (the window padding above the cursor belongs to it).
        var top = origin.Y - style.WindowPadding.Y;
        var stripHeight = rows * rowHeight;
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

        // Last row (or the same row): filters, character, then the buttons right-aligned.
        var lastRow = rows - 1;
        if (twoRows)
        {
            x = origin.X;
        }

        DrawFiltersButton(new Vector2(x, RowY(lastRow)), filtersWidth, control);
        x += filtersWidth + gap;
        DrawCharacterChip(session, new Vector2(x, RowY(lastRow)), characterWidth, control);
        x += characterWidth + gap;

        var clusterX = MathF.Max(x, origin.X + avail - clusterWidth);
        var y = RowY(lastRow);
        ImGui.SetCursorScreenPos(new Vector2(clusterX, y));
        ToolbarButton("##help", HelpIcon, Strings.HelpButtonTooltip, openHelp, UiRects.HelpButton);
        ImGui.SetCursorScreenPos(new Vector2(clusterX + control + clusterGap, y));
        ToolbarButton("##tutorial", TutorialIcon, Strings.TutorialButtonTooltip, startTutorial, UiRects.TutorialButton);
        ImGui.SetCursorScreenPos(new Vector2(clusterX + 2f * (control + clusterGap), y));
        ToolbarButton("##settings", SettingsIcon, Strings.SettingsButtonTooltip, openSettings, UiRects.SettingsButton);

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
        dl.AddLine(new Vector2(min.X, max.Y - line * 0.5f), new Vector2(max.X, max.Y - line * 0.5f), Theme.U32(s.Line), line);
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
            Marks.Draw(dl, pipCenter, pipBox, session.IsLive && session.PollerHealthy ? Mark.LivePip : Mark.SnapshotPip);
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
        if (characterJobIcon != 0 && textures.GetFromGameIcon(new GameIconLookup(characterJobIcon)).TryGetWrap(out var wrap, out _))
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
        }
    }

    /// <summary>A round icon button on the toolbar; disabled (with a tooltip saying so) until its action is attached.</summary>
    private void ToolbarButton(string id, string icon, string tooltip, Action? action, string rectKey)
    {
        if (Chrome.IconButtonRound(id, icon, action is null ? Strings.ActionUnavailable : tooltip, enabled: action is not null))
        {
            action?.Invoke();
        }

        ui.RecordItem(rectKey);
    }

    private void RebuildCharacterLabels(SessionState session)
    {
        characterLabels.Clear();
        var now = DateTime.UtcNow;
        foreach (var summary in session.Characters)
        {
            var name = string.Format(CultureInfo.CurrentCulture, Strings.CharacterNameFormat, summary.Name, links.WorldName(summary.World));
            var label = summary.ContentId == session.LiveContentId
                ? Strings.LiveMarker + name
                : string.Format(CultureInfo.CurrentCulture, Strings.CharacterEntryFormat, summary.Name, links.WorldName(summary.World), UiFormat.Age(summary.TakenUtc, now));
            // The content id keeps the ImGui id unique when two snapshots share a name and world.
            characterLabels.Add((summary.ContentId, label + "##" + summary.ContentId.ToString(CultureInfo.InvariantCulture)));
        }
    }

    /// <summary>
    /// The chip row under the toolbar (ui-revamp §2.1), drawn only while the scope or a filter narrows the table:
    /// the scope first, then one chip per engaged filter (<see cref="FilterPanel.DrawChips"/>).
    /// </summary>
    private void DrawChipRow(SessionState session)
    {
        if (!filterPanel.HasChips())
        {
            ui.Rects.Remove(UiRects.Chips);
            return;
        }

        filterPanel.DrawChips(session.Bundle);
    }

    private void DrawBanners(SessionState session)
    {
        if (session.ViewedSnapshot is null)
        {
            using var dusk = Theme.PushText(Theme.Surface.TextTertiary);
            ImGui.TextWrapped(Strings.BrowseModeNotice);
        }
        else if (staleBanner.Length > 0)
        {
            using var dusk = Theme.PushText(Theme.Surface.TextTertiary);
            ImGui.TextUnformatted(staleBanner);
        }
    }

    private void DrawBody(SessionState session, CatalogBundle bundle)
    {
        var style = ImGui.GetStyle();
        var statusHeight = ImGui.GetTextLineHeightWithSpacing() + style.ItemSpacing.Y * 2f;
        var bodyHeight = MathF.Max(UiMetrics.MinBodyHeight, ImGui.GetContentRegionAvail().Y - statusHeight);
        var cellHeight = bodyHeight - style.CellPadding.Y * 2f;

        // A new id for the four-column layout (T14): the three-column table's saved widths must not land on the rail.
        using var layout = ImRaii.Table("##body", 4, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.NoPadOuterX, new Vector2(0f, bodyHeight));
        if (!layout)
        {
            return;
        }

        // The rail is its own fixed column (not resizable, so it follows the UI scale every frame); the navigation
        // column beside it keeps its full width for the tree.
        ImGui.TableSetupColumn("##rail", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, UiMetrics.Px(ScaleMetrics.RailLogical));
        ImGui.TableSetupColumn("##left", ImGuiTableColumnFlags.WidthFixed, UiMetrics.LeftColumnWidth);
        ImGui.TableSetupColumn("##center", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##right", ImGuiTableColumnFlags.WidthFixed, UiMetrics.RightColumnWidth);
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        using (var rail = ImRaii.Child("##rail", new Vector2(0f, cellHeight)))
        {
            if (rail)
            {
                ImGui.Dummy(new Vector2(0f, UiMetrics.Px(2f)));
                var counts = runner.Counts;
                tabStrip.Draw(counts?.Overall.Fraction ?? 0f, counts?.OverallReady ?? 0);
            }
        }

        ImGui.TableNextColumn();
        DrawNavigation(session, bundle, cellHeight);

        ImGui.TableNextColumn();
        using (var center = ImRaii.Child("##center", new Vector2(0f, cellHeight)))
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
                    default:
                        tablePane.Draw(session.ViewedSnapshot is not null);
                        break;
                }
            }
        }

        ImGui.TableNextColumn();
        var detailHeight = cellHeight;
        if (whatsNew is { Visible: true } card)
        {
            detailHeight -= card.Draw(cellHeight);
        }

        // Nothing selected: the Tonight card answers "what now" in the detail column (game UX panel finding 1).
        if (ui.SelectedRowId is null)
        {
            tonightCard.Draw(session, bundle, new Vector2(0f, detailHeight));
        }
        else
        {
            detailPane.Draw(session, bundle, new Vector2(0f, detailHeight));
        }
    }

    /// <summary>
    /// The navigation column: the body of the tab the rail selected (<see cref="UiState.Tab"/> is the single source
    /// of truth, so a programmatic switch shows on the next frame with nothing to reconcile).
    /// </summary>
    private void DrawNavigation(SessionState session, CatalogBundle bundle, float height)
    {
        using var left = ImRaii.Child("##left", new Vector2(0f, height));
        if (!left)
        {
            return;
        }

        DrawTabBody(ui.Tab, session, bundle);
    }

    /// <summary>Left-column body of the active navigation tab.</summary>
    private void DrawTabBody(NavTab tab, SessionState session, CatalogBundle bundle)
    {
        switch (tab)
        {
            case NavTab.Journal:
                if (ui.FilterPanelOpen)
                {
                    filterPanel.Draw(bundle, session.ViewedSnapshot, plugin.Settings);
                }
                else
                {
                    ui.Rects.Remove(UiRects.FilterPanel);
                }

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

            default:
                ImGui.TextDisabled(Strings.Placeholder);
                break;
        }
    }

    /// <summary>
    /// The status bar (T12, ui-revamp §2.6), left to right: the overall halo with its percentage beside it, the catalog
    /// counts, a static pip with "live" or the snapshot time, the MSQ pill (click selects the next quest), and the
    /// version right-aligned in Dusk. Segments are separated by a Veil "·". When the line is too narrow the counts and
    /// then the MSQ pill end in an ellipsis; the halo, the pip and the version always show. Strings are rebuilt only
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

        var overall = runner.Counts?.Overall ?? default;
        if (overall != statusOverall)
        {
            statusOverall = overall;
            var percent = overall.Total <= 0 ? 0 : (int)MathF.Floor(100f * overall.Done / overall.Total);
            statusPercent = string.Format(CultureInfo.CurrentCulture, Strings.StatusPercentFormat, percent);
            statusProgress = UiFormat.Progress(overall.Done, overall.Total);
        }

        RefreshMsq(session, bundle);

        // The whole bar is in the caption role (ui-revamp §4.2): 0.85× the body, never under 12 px.
        using var caption = Typography.Caption();
        var barMin = ImGui.GetCursorScreenPos();
        ImGui.Separator();

        var dl = ImGui.GetWindowDrawList();
        var line = ImGui.GetTextLineHeight();
        var haloRadius = UiMetrics.StatusHaloRadius;
        var rowHeight = MathF.Max(line, 2f * haloRadius);
        var origin = ImGui.GetCursorScreenPos();
        var right = origin.X + ImGui.GetContentRegionAvail().X;
        var textY = origin.Y + (rowHeight - line) * 0.5f;
        var midY = origin.Y + rowHeight * 0.5f;
        var gap = UiMetrics.Px(6f);
        var separatorWidth = ImGui.CalcTextSize(StatusSeparator).X + 2f * gap;
        var x = origin.X;

        // Overall halo (track and arc; the number beside it, never a gauge under 16 px).
        if (GaugeGeometry.ModeFor(haloRadius) != HaloMode.NumberOnly)
        {
            ImGui.SetCursorScreenPos(new Vector2(x, origin.Y));
            ImGui.Dummy(new Vector2(2f * haloRadius, rowHeight));
            MoonGlyph.DrawHalo(dl, new Vector2(x + haloRadius, midY), haloRadius, Motion.Gauge(StatusGaugeKey, overall.Fraction));
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.FillingMoonTooltip, statusProgress);
            }

            x += 2f * haloRadius + UiMetrics.Px(4f);
        }

        x = StatusText(x, textY, statusPercent, Theme.U32(Theme.Surface.Text));
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.FillingMoonTooltip, statusProgress);
        }

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
        if (separatorWidth + statusWidth + fixedWidth + msqWidth > room)
        {
            // Too narrow for everything: the counts keep at least their floor, the MSQ pill gets the rest, and
            // whichever does not fit ends in an ellipsis instead of running into the version.
            statusRoom = MathF.Max(0f, MathF.Min(MathF.Max(MathF.Min(statusWidth, UiMetrics.Px(StatusMinLogical)), room - separatorWidth - fixedWidth - msqWidth), statusWidth));
            msqRoom = MathF.Max(0f, room - separatorWidth - statusRoom - fixedWidth);
        }

        if (statusRoom > 0f)
        {
            x = StatusSeparatorAt(dl, x, textY, gap);
            ImGui.SetCursorScreenPos(new Vector2(x, textY));
            EllipsisText(status, statusRoom, statusWidth, ImGui.GetColorU32(ImGuiCol.TextDisabled));
            if (DataStamp is { } stampAgain && ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(stampAgain);
            }

            x += statusRoom;
        }

        if (x + separatorWidth + modeWidth <= versionX)
        {
            // Static pip (accessibility B5: nothing here moves) and the live / snapshot words.
            x = StatusSeparatorAt(dl, x, textY, gap);
            ImGui.SetCursorScreenPos(new Vector2(x, textY));
            Marks.DrawInline(session.IsLive && session.PollerHealthy ? Mark.LivePip : Mark.SnapshotPip, pipSize);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(syncTooltip);
            }

            x = StatusText(x + pipSize + UiMetrics.Px(2f), textY, statusMode, ImGui.GetColorU32(ImGuiCol.TextDisabled));
        }

        if (msqWidth > 0f && msqRoom > 2f * pillPad && x + separatorWidth + msqRoom <= versionX + 0.5f)
        {
            x = StatusSeparatorAt(dl, x, textY, gap);
            var pillMin = new Vector2(x, textY - UiMetrics.Px(1f));
            var pillMax = new Vector2(x + msqRoom, textY + line + UiMetrics.Px(1f));
            dl.AddRectFilled(pillMin, pillMax, MsqPillFill, (pillMax.Y - pillMin.Y) * 0.5f);
            ImGui.SetCursorScreenPos(new Vector2(x + pillPad, textY));
            EllipsisText(msqStatus, msqRoom - 2f * pillPad, msqTextWidth, Theme.AccentU32);
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

        // One item spanning the bar so the layout advances past it.
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(MathF.Max(0f, right - origin.X), rowHeight));

        var windowX = ImGui.GetWindowPos().X;
        ui.RecordRect(UiRects.StatusBar, new Vector2(windowX + ImGui.GetWindowContentRegionMin().X, barMin.Y), new Vector2(windowX + ImGui.GetWindowContentRegionMax().X, origin.Y + rowHeight));
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
    /// Text in <paramref name="textColor"/> clipped to <paramref name="width"/> with an ellipsis, as one item (a Dummy) so hover and
    /// click tests still work on it. Nothing is allocated: ImGui renders the ellipsis itself.
    /// </summary>
    private static void EllipsisText(string text, float width, float textWidth, uint textColor)
    {
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(MathF.Max(0f, width), ImGui.GetTextLineHeight());
        ImGui.Dummy(max - min);
        if (width <= 0f)
        {
            return;
        }

        using var color = ImRaii.PushColor(ImGuiCol.Text, textColor);
        Vector2? size = new Vector2(textWidth, max.Y - min.Y);
        ImGuiP.RenderTextEllipsis(ImGui.GetWindowDrawList(), in min, in max, max.X, max.X, text, in size);
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

        msqStatus = string.Format(CultureInfo.CurrentCulture, Strings.StatusMsqFormat, session.Spoilers.DisplayName(next));
        var expansion = bundle.Names.Expansion(next.Expansion) is { Length: > 0 } named ? named : Expansions.Name(next.Expansion);
        var tooltip = string.Format(CultureInfo.CurrentCulture, Strings.MsqProgressFormat, expansion, position.Done, position.Total);
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
