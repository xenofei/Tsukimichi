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
    private string statusMode = string.Empty;
    private string versionText = string.Empty;

    // The overall halo's percentage and its tooltip, rebuilt when the overall count changes.
    private NodeCount statusOverall = new(-1, -1, -1);
    private string statusPercent = string.Empty;
    private string statusProgress = string.Empty;

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
        ArgumentNullException.ThrowIfNull(textures);

        Size = new Vector2(DefaultWidth, DefaultHeight);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = ScaleMetrics.MinWindowSize(ScaleMetrics.DefaultUiScale) };

        filterPanel = new FilterPanel(ui, OnFiltersChanged, OnDisplayChanged);
        ui.FiltersChanged += OnFiltersChanged;
        treePane = new TreePane(ui);
        tablePane = new TablePane(ui, runner, links, textures, pluginInterface, log, filterPanel.ResetAll);
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
                    filterPanel.DrawChips(session.Bundle);
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

        Marks.DrawInline(session.IsLive && session.PollerHealthy ? Mark.LivePip : Mark.SnapshotPip, glyphSize);
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
        DrawTab(NavTab.Journal, JournalTabLabel(), requested, force, session, bundle);
        DrawTab(NavTab.Moonlit, Strings.TabMoonlit, requested, force, session, bundle);
        DrawTab(NavTab.Characters, Strings.TabCharacters, requested, force, session, bundle);
        DrawTab(NavTab.Flight, Strings.TabFlight, requested, force, session, bundle);
    }

    /// <summary>
    /// The Journal tab's label with its badge: the number of Ready quests for the viewed character (T11), rebuilt only
    /// when that number changes. The "###" id keeps the tab the same item as the count comes and goes.
    /// </summary>
    private string JournalTabLabel()
    {
        var ready = runner.Counts?.OverallReady ?? 0;
        if (ready != journalTabReady)
        {
            journalTabReady = ready;
            journalTabLabel = ready > 0
                ? string.Format(CultureInfo.CurrentCulture, Strings.TreeTabJournalReadyFormat, ready)
                : Strings.TreeTabJournal;
        }

        return journalTabLabel;
    }

    private int journalTabReady = -1;
    private string journalTabLabel = Strings.TreeTabJournal;

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
            MoonGlyph.DrawHalo(dl, new Vector2(x + haloRadius, midY), haloRadius, overall.Fraction);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.FillingMoonTooltip, statusProgress);
            }

            x += 2f * haloRadius + UiMetrics.Px(4f);
        }

        x = StatusText(x, textY, statusPercent, Theme.SilverU32);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.FillingMoonTooltip, statusProgress);
        }

        // Version, right-aligned in Dusk; it carries the data stamp on hover.
        var versionWidth = ImGui.CalcTextSize(versionText).X;
        var versionX = MathF.Max(x, right - versionWidth);
        StatusText(versionX, textY, versionText, Theme.DuskU32);
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
            EllipsisText(msqStatus, msqRoom - 2f * pillPad, msqTextWidth, Theme.MoonU32);
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
        dl.AddText(new Vector2(x + gap, y), Theme.VeilU32, StatusSeparator);
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
