using System;
using System.Collections.Generic;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Ui;

/// <summary>Which left-hand navigation tab is active in the main window.</summary>
public enum NavTab
{
    Journal,
    Moonlit,
    Characters,
    Flight,
}

/// <summary>
/// Per-window UI state shared by the main window's panes. Owned by MainWindow, passed to each pane every frame.
/// Persisted parts (filters, sort) are copied to/from Configuration by MainWindow; the rest is session-only.
/// </summary>
public sealed class UiState
{
    public NavTab Tab { get; set; } = NavTab.Journal;

    private QuestScope scope = QuestScope.None;

    /// <summary>
    /// Journal tree selection; QuestScope.None means "everything". A change by any other means than
    /// <see cref="Reveal(uint, QuestScope, bool)"/> (a tree click, a command, the tutorial) drops a reveal still pending
    /// for the previous scope, so the tree never opens and scrolls to the wrong node later.
    /// </summary>
    public QuestScope Scope
    {
        get => scope;
        set
        {
            if (value != scope)
            {
                RevealPending = false;
            }

            scope = value;
        }
    }

    /// <summary>Row id of the quest shown in the detail pane, or null.</summary>
    public uint? SelectedRowId { get; set; }

    /// <summary>Raw search text as typed; MainWindow debounces before querying.</summary>
    public string SearchText { get; set; } = string.Empty;

    public bool FilterPanelOpen { get; set; }

    public FilterSet Filters { get; set; } = new();

    public SortSpec Sort { get; set; } = SortSpec.Default;

    /// <summary>Moonlit pane: selected reward kind filter (null = all kinds).</summary>
    public Tsukimichi.Core.Model.RewardKind? MoonlitKind { get; set; }

    public bool MoonlitHideObtained { get; set; }

    /// <summary>Flight pane: TerritoryType id of the selected flying zone; null until the pane picks the current zone on its first draw.</summary>
    public uint? FlightTerritoryId { get; set; }

    /// <summary>Set by "Show path"; the detail pane clears it when drawn, then scrolls to and briefly highlights its Path section.</summary>
    public bool ScrollToPath { get; set; }

    /// <summary>
    /// Set by <see cref="Reveal(uint, QuestScope, bool)"/>: the Journal tree opens the ancestors of <see cref="Scope"/>
    /// and scrolls to it on its next draw, then clears this.
    /// </summary>
    public bool RevealPending { get; set; }

    /// <summary>
    /// Bumped by every <see cref="Reveal(uint, QuestScope, bool)"/>: the table compares it with the last one it saw and
    /// plays the reveal pulse on <see cref="RevealedRowId"/>'s row when that row is next drawn.
    /// </summary>
    public int RevealSerial { get; private set; }

    /// <summary>The row the last <see cref="Reveal(uint, QuestScope, bool)"/> landed on.</summary>
    public uint RevealedRowId { get; private set; }

    /// <summary>
    /// Screen rectangles of named UI regions recorded during the last frame (toolbar, search, filters, chips,
    /// tabs, tree, table, detail, path, moonlit, characters, flight, settings, help). Consumers such as the interactive
    /// tutorial read them to draw highlights; panes call <see cref="RecordRect"/> right after drawing a region.
    /// </summary>
    public Dictionary<string, (System.Numerics.Vector2 Min, System.Numerics.Vector2 Max)> Rects { get; } = new();

    /// <summary>Records the last drawn ImGui item's rectangle under a stable key.</summary>
    public void RecordRect(string key, System.Numerics.Vector2 min, System.Numerics.Vector2 max) => Rects[key] = (min, max);

    /// <summary>Bumped by any pane that changed something affecting the query; MainWindow re-runs the query when it differs from the last run.</summary>
    public int QueryVersion { get; private set; }

    public void MarkQueryDirty() => QueryVersion++;

    /// <summary>Raised when <see cref="Filters"/> was changed by something other than the filter panel (e.g. <see cref="Reveal"/>), so the window can persist it.</summary>
    public event Action? FiltersChanged;

    /// <summary>Select a quest and ask the detail pane to scroll to its Path section.</summary>
    public void ShowPath(uint rowId)
    {
        SelectedRowId = rowId;
        ScrollToPath = true;
    }

    /// <summary>
    /// Select a quest and switch to the Journal tab scoped to its genre, used by cross-pane links. The state-based
    /// narrowing filters and the active preset are cleared so the revealed row cannot be hidden by them; an unlisted
    /// quest also turns Include Unlisted on so its virtual scope is reachable.
    /// </summary>
    public void Reveal(uint rowId, QuestScope scope, bool isUnlisted = false)
    {
        Tab = NavTab.Journal;
        Scope = scope;
        SelectedRowId = rowId;
        RevealPending = true;
        RevealedRowId = rowId;
        RevealSerial++;

        if (ClearNarrowingFilters(isUnlisted))
        {
            FiltersChanged?.Invoke();
        }

        MarkQueryDirty();
    }

    /// <summary>
    /// Switch to the Journal tab scoped to the quests one NPC hands out (<see cref="QuestScope.Issuer"/>), from the
    /// NPC context menu. The narrowing filters are cleared as <see cref="Reveal(uint, QuestScope, bool)"/> clears
    /// them, so a Blocked quest is listed with its blocker rather than hidden; the selection is dropped, since the
    /// quest shown in the detail pane may not be one of the NPC's. No tree node carries the scope, so no reveal is
    /// pending; the scope chip names the NPC and clears the scope.
    /// </summary>
    public void ShowIssuer(uint npcId)
    {
        Tab = NavTab.Journal;
        Scope = QuestScope.Issuer(npcId);
        SelectedRowId = null;
        RevealPending = false;

        if (ClearNarrowingFilters(includeUnlisted: false))
        {
            FiltersChanged?.Invoke();
        }

        MarkQueryDirty();
    }

    /// <summary>
    /// Switch to the Journal tab on the whole journal filtered to the viewed character's abandoned quests (the
    /// Characters dashboard's "Show in Journal"): the other narrowing filters are cleared as a reveal clears them,
    /// then the Abandoned filter is turned on; its chip clears it.
    /// </summary>
    public void ShowAbandoned()
    {
        Tab = NavTab.Journal;
        Scope = QuestScope.None;
        SelectedRowId = null;
        RevealPending = false;
        ClearNarrowingFilters(includeUnlisted: false);
        Filters.AbandonedOnly = true;
        FiltersChanged?.Invoke();
        MarkQueryDirty();
    }

    /// <summary>
    /// Turns off the state-based narrowing filters and the active preset (and turns Include removed on when asked);
    /// true when anything changed and the window should persist the filters.
    /// </summary>
    private bool ClearNarrowingFilters(bool includeUnlisted)
    {
        var f = Filters;
        var changed = false;
        if (f.HideCompletedEngaged())
        {
            f.HideCompleted = false;
            f.PerCategoryHideCompleted.Clear();
            changed = true;
        }

        if (f.AvailableOnlyEngaged())
        {
            f.AvailableOnly = false;
            f.PerCategoryAvailableOnly.Clear();
            changed = true;
        }

        if (f.PinnedOnly)
        {
            f.PinnedOnly = false;
            changed = true;
        }

        if (f.AbandonedOnly)
        {
            f.AbandonedOnly = false;
            changed = true;
        }

        if (f.StateMask != QuestStateMask.All)
        {
            f.StateMask = QuestStateMask.All;
            changed = true;
        }

        if (f.Preset != Preset.None)
        {
            f.Preset = Preset.None;
            changed = true;
        }

        if (includeUnlisted && !f.IncludeUnlisted)
        {
            f.IncludeUnlisted = true;
            changed = true;
        }

        return changed;
    }

    /// <summary>Reveals a catalog quest: its genre's scope, or the "Removed from the game" scope for a removed quest.</summary>
    public void Reveal(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        Reveal(quest.RowId, quest.IsRemoved ? QuestScope.VirtualUnlisted : QuestScope.Genre(quest.Journal.GenreId), quest.IsRemoved);
    }

    /// <summary>Raised by <see cref="OpenRoute"/>; the plugin opens the route window on the target.</summary>
    public event Action<Tsukimichi.Core.Route.RouteTarget>? RouteRequested;

    /// <summary>Opens the unlock route (P6) to a job, duty, system, reward or quest for the viewed character. Safe from inside a menu.</summary>
    public void OpenRoute(Tsukimichi.Core.Route.RouteTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        RouteRequested?.Invoke(target);
    }
}
