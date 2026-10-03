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

    /// <summary>"Clear my blues" (P3): the unlock quests left, by expansion and zone.</summary>
    Plan,
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
    public void Reveal(uint rowId, QuestScope scope, bool isUnlisted = false) => Reveal(rowId, scope, isUnlisted, filtersChanged: false);

    private void Reveal(uint rowId, QuestScope scope, bool isUnlisted, bool filtersChanged)
    {
        Tab = NavTab.Journal;
        Scope = scope;
        SelectedRowId = rowId;
        RevealPending = true;
        RevealedRowId = rowId;
        RevealSerial++;

        if (ClearNarrowingFilters(isUnlisted) | filtersChanged)
        {
            FiltersChanged?.Invoke();
        }

        MarkQueryDirty();
    }

    /// <summary>
    /// The query context of the table's last run (its running festivals, search index and spoiler shield); set by the
    /// query runner. <see cref="Reveal(QuestRecord)"/> asks it which of the other filters and the search hide a quest.
    /// </summary>
    public Func<QueryContext?>? RevealContext { get; set; }

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
    /// Switch to the Journal tab on the whole journal filtered to the viewed character's pins (the Todo overlay's
    /// "+N more" line): the other narrowing filters and the search are cleared, so no pin is hidden, then the Pinned
    /// filter is turned on; its chip clears it.
    /// </summary>
    public void ShowPinned()
    {
        Tab = NavTab.Journal;
        Scope = QuestScope.None;
        SelectedRowId = null;
        RevealPending = false;
        SearchText = string.Empty;
        ClearNarrowingFilters(includeUnlisted: false);
        Filters.PinnedOnly = true;
        FiltersChanged?.Invoke();
        MarkQueryDirty();
    }

    /// <summary>
    /// What the last "Opened:" chat line counted (1.7.0, "Opened by that"): the quest row ids the Just opened scope
    /// lists. Session-only; replaced by the next <see cref="ShowJustOpened"/>.
    /// </summary>
    public IReadOnlySet<uint> JustOpened { get; private set; } = new HashSet<uint>();

    /// <summary>
    /// Switch to the Journal tab scoped to the quests one "Opened:" line counted (<see cref="QuestScope.JustOpened"/>),
    /// from its Show link. The narrowing filters and the search are cleared as <see cref="ShowPinned"/> clears them, so
    /// every quest the line counted is listed; the scope chip clears the scope.
    /// </summary>
    public void ShowJustOpened(uint serial, IReadOnlyCollection<uint> rowIds)
    {
        ArgumentNullException.ThrowIfNull(rowIds);
        JustOpened = new HashSet<uint>(rowIds);
        Tab = NavTab.Journal;
        Scope = QuestScope.JustOpened(serial);
        SelectedRowId = null;
        RevealPending = false;
        SearchText = string.Empty;
        if (ClearNarrowingFilters(includeUnlisted: false))
        {
            FiltersChanged?.Invoke();
        }

        MarkQueryDirty();
    }

    /// <summary>
    /// What the Journal badge counted as newly ready when it was clicked (plan v7): the quest row ids the Newly ready
    /// scope lists. Session-only; replaced by the next <see cref="ShowNewlyReady"/>.
    /// </summary>
    public IReadOnlySet<uint> NewlyReady { get; private set; } = new HashSet<uint>();

    /// <summary>
    /// Switch to the Journal tab scoped to the quests the Journal badge counts as newly ready
    /// (<see cref="QuestScope.NewlyReady"/>), from a click on the badge. The narrowing filters and the search are
    /// cleared as <see cref="ShowJustOpened"/> clears them, so every one of them is listed; the scope chip clears the
    /// scope, and closing it marks them seen.
    /// </summary>
    public void ShowNewlyReady(uint serial, IReadOnlyCollection<uint> rowIds)
    {
        ArgumentNullException.ThrowIfNull(rowIds);
        NewlyReady = new HashSet<uint>(rowIds);
        Tab = NavTab.Journal;
        Scope = QuestScope.NewlyReady(serial);
        SelectedRowId = null;
        RevealPending = false;
        SearchText = string.Empty;
        if (ClearNarrowingFilters(includeUnlisted: false))
        {
            FiltersChanged?.Invoke();
        }

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

        if (f.OnceOnlyStory)
        {
            f.OnceOnlyStory = false;
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

    /// <summary>
    /// Whether a quest lies on a path the viewed character did not take (its rows are listed under Other paths only,
    /// unless Include other paths is on); set by the query runner. Null reads as no.
    /// </summary>
    public Func<uint, bool>? IsOtherPath { get; set; }

    /// <summary>
    /// Reveals a catalog quest: its genre's scope, the "Removed from the game" scope for a removed quest, or the
    /// "Other paths" scope for a quest on a path the character did not take while those are listed there only. Beside
    /// the narrowing filters every reveal clears, the other filters and the search are cleared only where they would
    /// hide this quest (<see cref="QuestQuery.ClearFiltersHiding"/>), so the row it selects is always in the table.
    /// </summary>
    public void Reveal(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var scope = quest.IsRemoved ? QuestScope.VirtualUnlisted
            : !Filters.IncludeOtherPaths && IsOtherPath?.Invoke(quest.RowId) == true ? QuestScope.VirtualOtherPaths
            : QuestScope.Genre(quest.Journal.GenreId);
        var context = RevealContext?.Invoke();
        var changed = QuestQuery.ClearFiltersHiding(quest, Filters, context?.ActiveFestivals, context?.NewSinceData);
        if (QuestQuery.SearchHides(quest, SearchText, context))
        {
            SearchText = string.Empty;
        }

        Reveal(quest.RowId, scope, quest.IsRemoved, changed);
    }

    /// <summary>Raised by <see cref="OpenRoute"/>; the plugin opens the route window on the target.</summary>
    public event Action<Tsukimichi.Core.Route.RouteTarget>? RouteRequested;

    /// <summary>Opens the unlock route (P6) to a job, duty, system, reward or quest for the viewed character. Safe from inside a menu.</summary>
    public void OpenRoute(Tsukimichi.Core.Route.RouteTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        RouteRequested?.Invoke(target);
    }

    /// <summary>Raised by <see cref="OpenRecap"/>; the plugin opens the story recap window on the request.</summary>
    public event Action<RecapRequest>? RecapRequested;

    /// <summary>
    /// Opens the story recap ("Previously…", feature plan v5 collector extras) for the viewed character, or the one the
    /// request names. Safe from inside a menu.
    /// </summary>
    public void OpenRecap(RecapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RecapRequested?.Invoke(request);
    }
}

/// <summary>
/// What the story recap reads: the last few main scenario quests the character completed
/// (<see cref="MainScenario"/>), or every completed quest of one chain, named by one of its quests.
/// </summary>
/// <param name="ChainQuestRowId">A quest of the chain to recap; 0 for the main scenario.</param>
/// <param name="ContentId">
/// The character whose story it is: the viewed or the logged-in one (the Since you were away card can speak for the
/// logged-in character while a stored one is viewed); null reads the viewed character.
/// </param>
public sealed record RecapRequest(uint ChainQuestRowId, ulong? ContentId = null)
{
    /// <summary>The main scenario's recap, as long as Settings › Display › Free trial and story recap says.</summary>
    public static readonly RecapRequest MainScenario = new(0u);

    /// <summary>Whether this is the main scenario's recap rather than a chain's.</summary>
    public bool IsMainScenario => ChainQuestRowId == 0;
}
