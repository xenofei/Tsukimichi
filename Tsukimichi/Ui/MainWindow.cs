using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
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
    public const float DefaultWidth = 1100f;
    public const float DefaultHeight = 700f;
    public const int MaxChatMatches = 5;
    public const int ToolbarButtonCount = 3;

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

    private readonly FilterPanel filterPanel;
    private readonly TreePane treePane;
    private readonly TablePane tablePane;
    private readonly DetailPane detailPane;

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
    private NavTab drawnTab = NavTab.Journal;

    // While the tour runs the window must not climb over the tutorial card when clicked, and Esc belongs to the
    // card; both settings are restored from these copies when the tour ends.
    private bool tourWasActive;
    private ImGuiWindowFlags flagsBeforeTour;
    private bool closeHotkeyBeforeTour;

    // Toolbar strings, rebuilt when the session version changes.
    private int toolbarVersion = -1;
    private string characterPreview = Strings.NoCharacter;
    private string staleBanner = string.Empty;
    private string syncTooltip = Strings.NoCharacter;
    private readonly List<(ulong Id, string Label)> characterLabels = [];

    // Status bar string, rebuilt when its inputs change.
    private (int Catalog, int Rows, int Total, bool Live, long SnapshotMinute) statusKey = (-1, -1, -1, false, -1);
    private string status = string.Empty;

    /// <summary>The least width the status text keeps when the MSQ segment crowds it, in logical pixels.</summary>
    private const float StatusMinLogical = 120f;

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
        ArgumentNullException.ThrowIfNull(textures);

        Size = new Vector2(DefaultWidth, DefaultHeight);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = ScaleMetrics.MinWindowSize(ScaleMetrics.DefaultUiScale) };

        filterPanel = new FilterPanel(ui, OnFiltersChanged, OnDisplayChanged);
        ui.FiltersChanged += OnFiltersChanged;
        treePane = new TreePane(ui);
        tablePane = new TablePane(ui, runner, links, textures, pluginInterface, log, filterPanel.ResetAll);
        detailPane = new DetailPane(ui, runner, links, textures, log);

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

    public override void Draw()
    {
        SyncTourState();
        if (plugin.Session is not { } session)
        {
            return;
        }

        EnsureInitialized();
        UiMetrics.Update(plugin.Settings);

        // The fixed side columns grow with the UI scale, so the minimum size must too or the centre column collapses.
        SizeConstraints = new WindowSizeConstraints { MinimumSize = ScaleMetrics.MinWindowSize(UiMetrics.FontScale) };

        // Dalamud closes the window on Esc while it or one of its popups is focused; while a popup (verdict prompt,
        // context menu) is open Esc belongs to the popup. The tour manages the flag itself while it runs.
        if (!tourWasActive)
        {
            RespectCloseHotkey = !ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel);
        }

        // Regions are re-recorded by whichever panes draw this frame; clearing first keeps hidden panes' rects
        // from lingering (the tutorial unions them for its dimmed area).
        ui.Rects.Clear();

        // The window's own font scale; direct children inherit it (see UiMetrics). It is reset before this Draw ends
        // so the next Begin lays the title bar out at Dalamud's size.
        UiMetrics.ApplyFontScale();
        try
        {
            DrawContent(session);
        }
        finally
        {
            ImGui.SetWindowFontScale(1f);
        }
    }

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
        // Chat results mirror the table: removed quests only when the Include removed filter is on.
        var showUnlisted = ui.Filters.IncludeUnlisted;
        var count = 0;
        foreach (var quest in bundle.Catalog.All)
        {
            if ((quest.IsRemoved && !showUnlisted) || !index.Matches(quest.RowId, normalized))
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
            characterPreview = Strings.NoCharacter;
            staleBanner = string.Empty;
            syncTooltip = Strings.NoCharacter;
            return;
        }

        var name = string.Format(CultureInfo.CurrentCulture, Strings.CharacterNameFormat, snapshot.Name, links.WorldName(snapshot.World));
        characterPreview = session.IsLive ? Strings.LiveMarker + name : name;
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
    }

    private void DrawToolbar(SessionState session)
    {
        if (!string.Equals(searchBuffer, ui.SearchText, StringComparison.Ordinal))
        {
            searchBuffer = ui.SearchText;
        }

        var toolbarMin = ImGui.GetCursorScreenPos();
        ImGui.SetNextItemWidth(UiMetrics.SearchWidth);
        if (ImGui.InputTextWithHint("##search", Strings.SearchHint, ref searchBuffer, 200))
        {
            ui.SearchText = searchBuffer;
        }

        ui.RecordItem(UiRects.Search);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.SearchTooltip);
        }

        ImGui.SameLine();
        using (ImRaii.Disabled(searchBuffer.Length == 0))
        {
            if (ImGuiComponents.IconButton("##clearSearch", FontAwesomeIcon.Times, UiMetrics.Square(UiMetrics.MinTarget)))
            {
                searchBuffer = string.Empty;
                ui.SearchText = string.Empty;
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.ClearSearch);
        }

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive), ui.FilterPanelOpen))
        {
            if (ImGui.Button(Strings.Filters))
            {
                ui.FilterPanelOpen = !ui.FilterPanelOpen;
            }
        }

        ui.RecordItem(UiRects.FiltersButton);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.FiltersTooltip);
        }

        ImGui.SameLine();
        DrawCharacterCombo(session);

        // Active-filter chips live on the toolbar row itself, in a fixed-height strip clipped horizontally, so toggling
        // a filter never moves the layout below. The strip stays empty when nothing is engaged.
        ImGui.SameLine();
        // The icon buttons (and so the strip) never go under the minimum click target.
        var glyphSize = MathF.Max(UiMetrics.ChipHeight, UiMetrics.MinTarget);
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        // Right block: sync glyph plus three square icon buttons (help, tutorial, settings).
        var rightWidth = glyphSize * (1 + ToolbarButtonCount) + spacing * ToolbarButtonCount;
        var chipsWidth = ImGui.GetContentRegionAvail().X - rightWidth - spacing;
        if (chipsWidth > UiMetrics.MinChipStripWidth)
        {
            using (var strip = ImRaii.Child("##chips", new Vector2(chipsWidth, glyphSize), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                if (strip)
                {
                    ui.RecordWindow(UiRects.Chips);
                    filterPanel.DrawChips();
                }
            }

            ImGui.SameLine();
        }
        else
        {
            ui.Rects.Remove(UiRects.Chips);
        }

        // Sync glyph and the action buttons, right-aligned.
        var avail = ImGui.GetContentRegionAvail().X;
        if (avail > rightWidth)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - rightWidth);
        }

        MoonGlyph.DrawInline(session.IsLive && session.PollerHealthy ? QuestState.Completed : QuestState.Unknown, glyphSize);
        ui.RecordItem(UiRects.Sync);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(syncTooltip);
        }

        var buttonSize = new Vector2(glyphSize, glyphSize);
        ImGui.SameLine();
        ToolbarButton("##help", FontAwesomeIcon.QuestionCircle, Strings.HelpButtonTooltip, openHelp, buttonSize, UiRects.HelpButton);
        ImGui.SameLine();
        ToolbarButton("##tutorial", FontAwesomeIcon.GraduationCap, Strings.TutorialButtonTooltip, startTutorial, buttonSize, UiRects.TutorialButton);
        ImGui.SameLine();
        ToolbarButton("##settings", FontAwesomeIcon.Cog, Strings.SettingsButtonTooltip, openSettings, buttonSize, UiRects.SettingsButton);

        // The whole row, from the search box to the last button.
        var lastMax = ImGui.GetItemRectMax();
        ui.RecordRect(UiRects.Toolbar, toolbarMin, new Vector2(lastMax.X, MathF.Max(lastMax.Y, toolbarMin.Y + glyphSize)));
    }

    /// <summary>A square icon button; disabled (with a tooltip saying so) until its action is attached.</summary>
    private void ToolbarButton(string id, FontAwesomeIcon icon, string tooltip, Action? action, Vector2 size, string rectKey)
    {
        using (ImRaii.Disabled(action is null))
        {
            if (ImGuiComponents.IconButton(id, icon, size))
            {
                action?.Invoke();
            }
        }

        ui.RecordItem(rectKey);
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(action is null ? Strings.ActionUnavailable : tooltip);
        }
    }

    private void DrawCharacterCombo(SessionState session)
    {
        ImGui.SetNextItemWidth(UiMetrics.CharacterComboWidth);
        using var combo = ImRaii.Combo("##character", characterPreview);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.CharacterComboTooltip);
        }

        if (!combo)
        {
            // While the popup is open the last item belongs to it; the closed frame's rectangle stays recorded.
            ui.RecordItem(UiRects.Character);
            return;
        }

        if (ImGui.IsWindowAppearing())
        {
            RebuildCharacterLabels(session);
        }

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

    private void DrawBanners(SessionState session)
    {
        if (session.ViewedSnapshot is null)
        {
            using var dusk = Theme.PushText(Theme.Dusk);
            ImGui.TextWrapped(Strings.BrowseModeNotice);
        }
        else if (staleBanner.Length > 0)
        {
            using var dusk = Theme.PushText(Theme.Dusk);
            ImGui.TextUnformatted(staleBanner);
        }
    }

    private void DrawBody(SessionState session, CatalogBundle bundle)
    {
        var style = ImGui.GetStyle();
        var statusHeight = ImGui.GetTextLineHeightWithSpacing() + style.ItemSpacing.Y * 2f;
        var bodyHeight = MathF.Max(UiMetrics.MinBodyHeight, ImGui.GetContentRegionAvail().Y - statusHeight);
        var cellHeight = bodyHeight - style.CellPadding.Y * 2f;

        using var layout = ImRaii.Table("##layout", 3, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.NoPadOuterX, new Vector2(0f, bodyHeight));
        if (!layout)
        {
            return;
        }

        ImGui.TableSetupColumn("##left", ImGuiTableColumnFlags.WidthFixed, UiMetrics.LeftColumnWidth);
        ImGui.TableSetupColumn("##center", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##right", ImGuiTableColumnFlags.WidthFixed, UiMetrics.RightColumnWidth);
        ImGui.TableNextRow();

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

        detailPane.Draw(session, bundle, new Vector2(0f, detailHeight));
    }

    private void DrawNavigation(SessionState session, CatalogBundle bundle, float height)
    {
        using var left = ImRaii.Child("##left", new Vector2(0f, height));
        if (!left)
        {
            return;
        }

        var tabsMin = ImGui.GetCursorScreenPos();
        var tabsWidth = ImGui.GetContentRegionAvail().X;
        using var bar = ImRaii.TabBar("##navTabs");
        if (!bar)
        {
            return;
        }

        // BeginTabBar leaves the cursor under the tab row.
        ui.RecordRect(UiRects.Tabs, tabsMin, new Vector2(tabsMin.X + tabsWidth, ImGui.GetCursorScreenPos().Y));

        // A programmatic switch (ui.Tab set by the tutorial, help or a command) is requested once for the whole row:
        // ImGui applies SetSelected a frame late, so the old tab is still the visible one this frame and must not
        // write itself back into ui.Tab while the request is pending.
        var requested = ui.Tab;
        var force = requested != drawnTab;
        DrawTab(NavTab.Journal, Strings.TabJournal, requested, force, session, bundle);
        DrawTab(NavTab.Moonlit, Strings.TabMoonlit, requested, force, session, bundle);
        DrawTab(NavTab.Characters, Strings.TabCharacters, requested, force, session, bundle);
        DrawTab(NavTab.Flight, Strings.TabFlight, requested, force, session, bundle);
    }

    private void DrawTab(NavTab tab, string label, NavTab requested, bool force, SessionState session, CatalogBundle bundle)
    {
        var flags = force && requested == tab ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        using var item = ImRaii.TabItem(label, flags);
        if (!item)
        {
            return;
        }

        drawnTab = tab;
        if (!force)
        {
            // The user clicked a tab: the visible item is the source of truth.
            ui.Tab = tab;
        }

        DrawTabBody(tab, session, bundle);
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
            var mode = session.IsLive
                ? Strings.StatusLive
                : snapshot is null
                    ? Strings.StatusNoSnapshot
                    : string.Format(CultureInfo.CurrentCulture, Strings.StatusSnapshotFormat, UiFormat.Time(snapshot.TakenUtc));
            status = string.Format(CultureInfo.CurrentCulture, Strings.StatusFormat, bundle.Catalog.Count, runner.Rows.Length, runner.TotalInScope, mode, version);
        }

        RefreshMsq(session, bundle);

        var barMin = ImGui.GetCursorScreenPos();
        ImGui.Separator();

        // A tiny filling moon of overall completion leads the line.
        var lineHeight = ImGui.GetTextLineHeight();
        var moonBox = MathF.Max(lineHeight, UiMetrics.StatusMoonRadius * 2.4f);
        var moonPos = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(moonBox, lineHeight));
        MoonGlyph.DrawFilling(ImGui.GetWindowDrawList(), moonPos + new Vector2(moonBox * 0.5f, lineHeight * 0.5f), UiMetrics.StatusMoonRadius, runner.Counts?.Overall.Fraction ?? 0f);
        ImGui.SameLine();
        var avail = ImGui.GetContentRegionAvail().X;
        var statusWidth = ImGui.CalcTextSize(status).X;
        var msqTextWidth = msqStatus.Length > 0 ? ImGui.CalcTextSize(msqStatus).X : 0f;
        var msqRoom = msqTextWidth;
        if (statusWidth + msqTextWidth <= avail)
        {
            ImGui.TextDisabled(status);
        }
        else
        {
            // Too narrow for both: the status keeps at least its floor and the MSQ segment gets the rest; whichever
            // does not fit ends in an ellipsis instead of running past the window edge.
            var statusRoom = MathF.Max(MathF.Min(statusWidth, UiMetrics.Px(StatusMinLogical)), avail - msqTextWidth);
            statusRoom = MathF.Min(statusRoom, avail);
            EllipsisText(status, statusRoom, statusWidth);
            msqRoom = MathF.Max(0f, avail - statusRoom);
        }

        // The status text carries the data stamp on hover, so "which data is this" is one hover away from any tab.
        if (DataStamp is { } stamp && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(stamp);
        }

        if (msqStatus.Length > 0)
        {
            // The MSQ position follows the status text as its own item so it can carry a tooltip and a click.
            ImGui.SameLine(0f, 0f);
            if (msqTextWidth <= msqRoom)
            {
                ImGui.TextDisabled(msqStatus);
            }
            else
            {
                EllipsisText(msqStatus, msqRoom, msqTextWidth);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(msqTooltip);
                if (msq?.Next is { } next && ImGui.IsItemClicked())
                {
                    SelectMsq(next);
                }
            }
        }

        var windowX = ImGui.GetWindowPos().X;
        ui.RecordRect(UiRects.StatusBar, new Vector2(windowX + ImGui.GetWindowContentRegionMin().X, barMin.Y), new Vector2(windowX + ImGui.GetWindowContentRegionMax().X, ImGui.GetItemRectMax().Y));
    }

    /// <summary>
    /// Disabled-coloured text clipped to <paramref name="width"/> with an ellipsis, as one item (a Dummy) so hover and
    /// click tests still work on it. Nothing is allocated: ImGui renders the ellipsis itself.
    /// </summary>
    private static void EllipsisText(string text, float width, float textWidth)
    {
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(MathF.Max(0f, width), ImGui.GetTextLineHeight());
        ImGui.Dummy(max - min);
        if (width <= 0f)
        {
            return;
        }

        using var color = ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
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

        msqStatus = string.Format(CultureInfo.CurrentCulture, Strings.StatusMsqFormat, next.Name);
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
