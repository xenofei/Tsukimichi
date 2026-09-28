using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
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
    public const float MinWidth = 800f;
    public const float MinHeight = 500f;
    public const float LeftColumnWidth = 240f;
    public const float RightColumnWidth = 360f;
    public const int MaxChatMatches = 5;
    public const float MinChipStripWidth = 40f;

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

    private Task? retryTask;
    private bool initialized;
    private DateTime? settingsDirtyAtUtc;
    private SortSpec persistedSort = SortSpec.Default;
    private string searchBuffer = string.Empty;
    private NavTab drawnTab = NavTab.Journal;

    // Toolbar strings, rebuilt when the session version changes.
    private int toolbarVersion = -1;
    private string characterPreview = Strings.NoCharacter;
    private string staleBanner = string.Empty;
    private string syncTooltip = Strings.NoCharacter;
    private readonly List<(ulong Id, string Label)> characterLabels = [];

    // Status bar string, rebuilt when its inputs change.
    private (int Catalog, int Rows, int Total, bool Live, long SnapshotMinute) statusKey = (-1, -1, -1, false, -1);
    private string status = string.Empty;

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
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(MinWidth, MinHeight) };

        filterPanel = new FilterPanel(ui, OnFiltersChanged);
        ui.FiltersChanged += OnFiltersChanged;
        treePane = new TreePane(ui);
        tablePane = new TablePane(ui, runner, links, textures, pluginInterface, log, filterPanel.ResetAll);
        detailPane = new DetailPane(ui, runner, links, textures);

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

    /// <summary>Gives the detail pane the user's unique-reward verdicts so it can show and change them.</summary>
    public void AttachOverrides(IUniqueOverrides overrides)
    {
        detailPane.Overrides = overrides ?? throw new ArgumentNullException(nameof(overrides));
    }

    public override void Draw()
    {
        if (plugin.Session is not { } session)
        {
            return;
        }

        EnsureInitialized();
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
        // Chat results mirror the table: unlisted quests only when the Include Unlisted filter is on.
        var showUnlisted = ui.Filters.IncludeUnlisted;
        var count = 0;
        foreach (var quest in bundle.Catalog.All)
        {
            if ((quest.IsUnlisted && !showUnlisted) || !index.Matches(quest.RowId, normalized))
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

    public void Dispose()
    {
        FlushSettings(DateTime.UtcNow, force: true);
        ui.FiltersChanged -= OnFiltersChanged;
        tablePane.Dispose();
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
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
        var scale = ImGuiHelpers.GlobalScale;

        if (!string.Equals(searchBuffer, ui.SearchText, StringComparison.Ordinal))
        {
            searchBuffer = ui.SearchText;
        }

        ImGui.SetNextItemWidth(280f * scale);
        if (ImGui.InputTextWithHint("##search", Strings.SearchHint, ref searchBuffer, 200))
        {
            ui.SearchText = searchBuffer;
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Strings.SearchTooltip);
        }

        ImGui.SameLine();
        using (ImRaii.Disabled(searchBuffer.Length == 0))
        {
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Times))
            {
                searchBuffer = string.Empty;
                ui.SearchText = string.Empty;
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(Strings.ClearSearch);
        }

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive), ui.FilterPanelOpen))
        {
            if (ImGui.Button(Strings.Filters))
            {
                ui.FilterPanelOpen = !ui.FilterPanelOpen;
            }
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Strings.FiltersTooltip);
        }

        ImGui.SameLine();
        DrawCharacterCombo(session, scale);

        // Active-filter chips live on the toolbar row itself, in a fixed-height strip clipped horizontally, so toggling
        // a filter never moves the layout below. The strip stays empty when nothing is engaged.
        ImGui.SameLine();
        var glyphSize = ImGui.GetFrameHeight();
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var chipsWidth = ImGui.GetContentRegionAvail().X - glyphSize - spacing;
        if (chipsWidth > MinChipStripWidth * scale)
        {
            using (var strip = ImRaii.Child("##chips", new Vector2(chipsWidth, glyphSize), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                if (strip)
                {
                    filterPanel.DrawChips();
                }
            }

            ImGui.SameLine();
        }

        // Sync glyph, right-aligned.
        var avail = ImGui.GetContentRegionAvail().X;
        if (avail > glyphSize)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - glyphSize);
        }

        MoonGlyph.DrawInline(session.IsLive && session.PollerHealthy ? QuestState.Completed : QuestState.Unknown, glyphSize);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(syncTooltip);
        }
    }

    private void DrawCharacterCombo(SessionState session, float scale)
    {
        ImGui.SetNextItemWidth(240f * scale);
        using var combo = ImRaii.Combo("##character", characterPreview);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Strings.CharacterComboTooltip);
        }

        if (!combo)
        {
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
            characterLabels.Add((summary.ContentId, label));
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
        var scale = ImGuiHelpers.GlobalScale;
        var style = ImGui.GetStyle();
        var statusHeight = ImGui.GetTextLineHeightWithSpacing() + style.ItemSpacing.Y * 2f;
        var bodyHeight = MathF.Max(120f * scale, ImGui.GetContentRegionAvail().Y - statusHeight);
        var cellHeight = bodyHeight - style.CellPadding.Y * 2f;

        using var layout = ImRaii.Table("##layout", 3, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.NoPadOuterX, new Vector2(0f, bodyHeight));
        if (!layout)
        {
            return;
        }

        ImGui.TableSetupColumn("##left", ImGuiTableColumnFlags.WidthFixed, LeftColumnWidth * scale);
        ImGui.TableSetupColumn("##center", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##right", ImGuiTableColumnFlags.WidthFixed, RightColumnWidth * scale);
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
                    default:
                        tablePane.Draw(session.ViewedSnapshot is not null);
                        break;
                }
            }
        }

        ImGui.TableNextColumn();
        detailPane.Draw(session, bundle, new Vector2(0f, cellHeight));
    }

    private void DrawNavigation(SessionState session, CatalogBundle bundle, float height)
    {
        using var left = ImRaii.Child("##left", new Vector2(0f, height));
        if (!left)
        {
            return;
        }

        using var bar = ImRaii.TabBar("##navTabs");
        if (!bar)
        {
            return;
        }

        var force = ui.Tab != drawnTab;
        DrawTab(NavTab.Journal, Strings.TabJournal, force, session, bundle);
        DrawTab(NavTab.Moonlit, Strings.TabMoonlit, force, session, bundle);
        DrawTab(NavTab.Characters, Strings.TabCharacters, force, session, bundle);
    }

    private void DrawTab(NavTab tab, string label, bool force, SessionState session, CatalogBundle bundle)
    {
        var flags = force && ui.Tab == tab ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        using var item = ImRaii.TabItem(label, flags);
        if (!item)
        {
            return;
        }

        ui.Tab = tab;
        drawnTab = tab;
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
                    filterPanel.Draw(bundle, session.ViewedSnapshot);
                }

                treePane.Draw(bundle, runner, plugin.Settings.ShowUnlisted);
                break;

            case NavTab.Moonlit when moonlitPane is not null:
                moonlitPane.DrawLeft(ui);
                break;

            case NavTab.Characters when charactersPane is not null:
                charactersPane.DrawLeft(ui);
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

        ImGui.Separator();
        ImGui.TextDisabled(status);
    }
}
