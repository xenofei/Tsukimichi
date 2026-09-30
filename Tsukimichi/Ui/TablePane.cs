using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The quest table: glyph, name, level, job, status, expansion, reward icons; sortable, reorderable, hideable,
/// clipped with <see cref="ImGuiListClipperPtr"/>. Row click selects, double-click opens the journal, right-click
/// opens the context menu. The body allocates nothing: every string it shows is pre-materialized by
/// <see cref="QueryRunner"/> or the query rows, and style pushes inside a row are raw ImGui pushes.
///
/// Row chrome (feature plan v3 T15, ui-revamp §2.4): a 3 px state stripe on the row's left edge whose pattern, not
/// only its colour, names the state (<see cref="StripePattern"/>, accessibility A3); a hover fill and two-line
/// <see cref="Chrome.Lift"/> on the row hovered the previous frame (the binding has no <c>TableGetHoveredRow</c>);
/// a neutral selection wash with a 1 px ring (never gold); level and expansion pills; the job's game icon. Fills go
/// through <c>TableSetBgColor</c> with the selectable's own Header colours pushed transparent so it does not paint
/// over them; lines go on the table's background channel so they span every column under the text.
/// </summary>
public sealed class TablePane : IDisposable
{
    private const string QuestMapPluginName = "QuestMap";
    private const string QuestMapIpcName = "QuestMap.ShowGraphByQuestId";
    private const int MaxRewardIcons = 4;

    /// <summary>Logical space left of the state moon for the pinned dot; also the state stripe's hover zone.</summary>
    private const float GlyphColumnLead = 8f;

    /// <summary>Initial logical width of the Name column; the player can drag it, and long names clip.</summary>
    private const float NameColumnWidth = 240f;

    /// <summary>Logical width under which the Status column sheds Rewards, then Expansion: room for "Ready on another job".</summary>
    private const float StatusMinWidth = LayoutBudgets.StatusMinLogical;

    // The status column's minimum for the current language (V2-19): the widest state name must show whole.
    private int statusMinLanguage = -1;
    private float statusMinFont = -1f;
    private float statusMin;

    /// <summary>
    /// <see cref="StatusMinWidth"/>, or wider for a language whose longest state name (with " · …" after the states that
    /// carry a reason) would not fit it (<see cref="LayoutBudgets.StatusMin"/>); measured again on a language or font change.
    /// </summary>
    private float StatusMin()
    {
        var font = ImGui.GetFontSize();
        if (statusMinLanguage != Localization.Loc.Version || statusMinFont != font)
        {
            statusMinLanguage = Localization.Loc.Version;
            statusMinFont = font;
            var unit = MathF.Max(UiMetrics.Px(1f), 0.01f);
            var ellipsis = ImGui.CalcTextSize(Strings.StateReasonSeparator + "…").X;
            var widest = 0f;
            foreach (var state in Enum.GetValues<QuestState>())
            {
                var reason = state is QuestState.Blocked or QuestState.Foreclosed or QuestState.Unknown or QuestState.Accepted ? ellipsis : 0f;
                widest = MathF.Max(widest, ImGui.CalcTextSize(Strings.StateName(state)).X + reason);
            }

            widest = MathF.Max(widest, ImGui.CalcTextSize(Strings.StateName(QuestState.DoneThisCycle, null)).X);
            statusMin = UiMetrics.Px(LayoutBudgets.StatusMin(widest / unit));
        }

        return statusMin;
    }

    /// <summary>Logical gap between a story sidequest's name and its book badge.</summary>
    private const float StoryBadgeGap = 6f;

    /// <summary>Level pill (ui-revamp §2.4): at least 28 × 16 logical, text padded 6 each side.</summary>
    private const float LevelPillMinWidth = 28f;
    private const float PillHeight = 16f;
    private const float PillPadX = 6f;
    private const float ExpansionPillPadX = 5f;

    /// <summary>Job icon side and the gap before the label, logical.</summary>
    private const float JobIconSide = 16f;
    private const float JobIconGap = 5f;

    /// <summary>Initial logical width of the Job column: icon, gap and "DoH/DoL".</summary>
    private const float JobColumnWidth = 76f;

    /// <summary>Selection: the wash's alpha of the text colour, the ring's alpha and its logical rounding.</summary>
    private const float SelectionWashAlpha = 0.10f;
    private const float SelectionRingAlpha = 0.45f;
    private const float SelectionRounding = 4f;

    /// <summary>Row separators in Comfortable (zebra off): the line colour at this alpha.</summary>
    private const float SeparatorAlpha = 0.5f;

    /// <summary>High bits of the <see cref="Motion"/> key of a row's hover fade, so row ids never meet ImGui ids.</summary>
    private const ulong HoverKeyTag = 0x7461_6200_0000_0000UL;

    /// <summary>High half of the <see cref="Motion"/> key of a row's reveal pulse.</summary>
    private const uint RevealTag = 0x5441_5250; // "TARP"

    /// <summary>How long a reveal waits for its row to be drawn (the query and the scroll land a frame or two later).</summary>
    private const double RevealWaitSeconds = 2.0;

    private static readonly string StoryBadgeIcon = FontAwesomeIcon.BookOpen.ToIconString();

    /// <summary>The row's context menu, opened by a right-click, the Menu key or Shift+F10, or the "…" button.</summary>
    private const string RowMenuId = "##ctx";

    /// <summary>The widest level a pill is sized for when the Level column is first laid out.</summary>
    private const string WidestLevel = "100";

    private enum Column
    {
        Glyph,
        Name,
        Level,
        Job,
        Status,
        Expansion,
        Rewards,
    }

    /// <summary>Header label per <see cref="Column"/>; the glyph column keeps its name for the hide/show menu but shows none.</summary>
    private static string[] HeaderLabels => headerLabelsText.Value;

    private static readonly Localization.LocArray headerLabelsText = new(static () =>
        [
        string.Empty,
        Strings.ColumnName,
        Strings.ColumnLevel,
        Strings.ColumnJob,
        Strings.ColumnStatus,
        Strings.ColumnExpansion,
        Strings.ColumnRewards,
    ]);

    /// <summary>Header tooltip per <see cref="Column"/>, in column order.</summary>
    private static string[] HeaderTooltips => headerTooltipsText.Value;

    private static readonly Localization.LocArray headerTooltipsText = new(static () =>
        [
        Strings.ColumnGlyphTooltip,
        Strings.ColumnNameTooltip,
        Strings.ColumnLevelTooltip,
        Strings.ColumnJobTooltip,
        Strings.ColumnStatusTooltip,
        Strings.ColumnExpansionTooltip,
        Strings.ColumnRewardsTooltip,
    ]);

    private readonly UiState ui;
    private readonly QueryRunner runner;
    private readonly GameLinks links;
    private readonly ITextureProvider textures;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;
    private readonly Action resetFilters;
    private readonly Action filtersChanged;

    private ImGuiListClipperPtr clipper;

    /// <summary>Row id of the last "New in 7.5x" row under the Unlocks quick view (a rule is drawn under it); null when there is no group, or nothing after it.</summary>
    private uint? newGroupEnd;
    private bool clipperCreated;

    private ICallGateSubscriber<uint, object>? questMap;
    private bool questMapAvailable;

    private uint? lastSelection;
    private bool tableInitialized;

    // The Job column's minimum, re-asserted once per table init: measured on the first frame, applied on the next.
    private bool measureJobWidth;
    private float jobWidthFix;

    // The Level column's width raised to a longer translated header on the frame after it was measured (V2-19).
    private float levelWidthFix;

    // The UI language the header widths were last checked in.
    private int headerLanguage = -1;

    // Hover lift (dalamud-developer panel §4: no TableGetHoveredRow in the binding): the quest hovered on the previous
    // frame gets the fill and the lift this frame; hoveredNext collects this frame's for the next.
    private uint? hoveredRow;
    private uint? hoveredNext;

    // The row array hoveredRow was recorded against: a re-sort, filter or search hands out a new one, and the quest
    // under the mouse last frame is somewhere else now, so it gets neither the lift nor a fade at its new place.
    private QuestRow[]? hoverRows;

    // The row whose "…" button had keyboard focus last frame (it stays drawn while focused), and this frame's.
    private uint? moreFocusedRow;
    private uint? moreFocusedNext;

    // Reveal pulse (T17): the last UiState.RevealSerial seen, and the row still waiting to be drawn to start its pulse.
    private int revealSeen;
    private uint? revealRow;
    private double revealUntil;

    // FitStatusColumn state (see the method): the columns it hid, whether a re-show by hand suspended it, each
    // hideable column's enabled flag last frame (null before the first) and whether a re-show is the fit's own; the
    // Status width each hide gained, measured on the frame the hide took effect (widthBeforeHide is the width the
    // frame before); and a sort to write back into the column state once the sorted column is enabled again.
    private bool rewardsAutoHidden;
    private bool expansionAutoHidden;
    private bool fitSuspended;
    private bool? lastRewardsEnabled;
    private bool? lastExpansionEnabled;
    private bool fitReshowing;
    private Column? measureGainFor;
    private float widthBeforeHide;
    private float rewardsGain;
    private float expansionGain;
    private bool restoreSort;

    public TablePane(UiState ui, QueryRunner runner, GameLinks links, ITextureProvider textures, IDalamudPluginInterface pluginInterface, IPluginLog log, Action resetFilters, Action filtersChanged)
    {
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.textures = textures ?? throw new ArgumentNullException(nameof(textures));
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.resetFilters = resetFilters ?? throw new ArgumentNullException(nameof(resetFilters));
        this.filtersChanged = filtersChanged ?? throw new ArgumentNullException(nameof(filtersChanged));

        try
        {
            questMap = pluginInterface.GetIpcSubscriber<uint, object>(QuestMapIpcName);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Quest Map IPC subscriber unavailable");
            questMap = null;
        }
    }

    /// <summary><paramref name="hasSnapshot"/> false greys the runtime columns (browse mode).</summary>
    public void Draw(bool hasSnapshot)
    {
        if (ui.RevealSerial != revealSeen)
        {
            revealSeen = ui.RevealSerial;
            revealRow = ui.RevealedRowId;
            revealUntil = ImGui.GetTime() + RevealWaitSeconds;
        }

        if (runner.Empty is { } empty)
        {
            hoveredRow = null;
            DrawEmpty(empty);
            return;
        }

        var rows = runner.Rows;
        if (!ReferenceEquals(rows, hoverRows))
        {
            hoverRows = rows;
            if (hoveredRow is { } moved)
            {
                // Snap its fill out instead of fading it where the row landed.
                Motion.Lerp(HoverKeyTag | moved, 0f, float.MaxValue);
                hoveredRow = null;
            }
        }

        if (runner.SproutCaption is { } caption)
        {
            // Sprout mode (T19): how much of the game is in reach, instead of the whole catalog.
            ImGui.TextDisabled(caption);
        }

        if (runner.NewThisPatchCaption is { } newCaption)
        {
            // The Unlocks quick view's first group (P8): named here, closed by a rule under its last row.
            ImGui.TextDisabled(newCaption);
        }

        newGroupEnd = runner.NewThisPatch > 0 && runner.NewThisPatch < rows.Length ? rows[runner.NewThisPatch - 1].Quest.RowId : null;

        // SortTristate lets the header cycle back to "no sort" (journal order) and stops ImGui from picking the first
        // sortable column (the glyph) as an implicit default on the first frame.
        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.Sortable | ImGuiTableFlags.SortTristate | ImGuiTableFlags.ScrollY
            | ImGuiTableFlags.Resizable | ImGuiTableFlags.Reorderable | ImGuiTableFlags.Hideable | ImGuiTableFlags.SizingFixedFit;

        // The header's fill is painted when its row ends, which is when the clipper begins, so the push spans the table.
        ImGui.PushStyleColor(ImGuiCol.TableHeaderBg, Theme.Surface.Raised);
        using var headerBg = new Theme.StyleScope(1, 0);
        using var table = ImRaii.Table("##quests", 7, flags, ImGui.GetContentRegionAvail());
        if (!table)
        {
            hoveredRow = null;
            return;
        }

        // ScrollY gives the table its own inner window, so this is the table's rectangle; that window sits inside the
        // centre column (own font scale 1), so it scales itself before anything is measured.
        ui.RecordWindow(UiRects.Table);
        UiMetrics.ApplyFontScale();
        var style = ImGui.GetStyle();
        var lineHeight = ImGui.GetTextLineHeight();
        var padY = style.CellPadding.Y;
        var rowContent = UiMetrics.TableRowContentHeight(lineHeight, padY);
        var rowHeight = rowContent + padY * 2f;
        // The moon, and with it the Glyph column, follows the row height (r 9 Comfortable, r 6.4 Dense at scale 1).
        var glyphRadius = TableGeometry.GlyphRadius(rowContent, UiMetrics.RowGlyphRadius);
        var glyphBox = glyphRadius * TableGeometry.GlyphBoxPerRadius;
        var glyphColumn = UiMetrics.Px(GlyphColumnLead) + glyphBox + UiMetrics.Px(2f);
        // A translated header is never cut (V2-19, LayoutBudgets): each fixed column is at least its header label wide,
        // with, where the column sorts, the arrow. These are content widths: ImGui adds the cell padding either side of
        // a TableSetupColumn / TableSetColumnWidth width itself, so it is not added here.
        var sortArrow = UiMetrics.Px(LayoutBudgets.SortArrowLogical);
        float HeaderFloor(string label, bool sortable) => ImGui.CalcTextSize(label).X + (sortable ? sortArrow : 0f);
        var rewardsColumn = MathF.Max(
            UiMetrics.RowIconSize * MaxRewardIcons + UiMetrics.Px(2f) * (MaxRewardIcons - 1) + UiMetrics.Px(8f),
            HeaderFloor(Strings.ColumnRewards, sortable: false));
        var levelColumn = MathF.Max(
            MathF.Max(UiMetrics.Px(LayoutBudgets.LevelColumnLogical), PillWidth(WidestLevel, UiMetrics.Px(LevelPillMinWidth), UiMetrics.Px(PillPadX))),
            HeaderFloor(Strings.ColumnLevel, sortable: true));

        ImGui.TableSetupColumn(Strings.ColumnGlyph, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize | ImGuiTableColumnFlags.NoHide | ImGuiTableColumnFlags.NoHeaderLabel, glyphColumn);
        // Status is the one stretch column (feature plan v3 P1): it holds the answer to "why not", so it takes the
        // width the others leave, and FitStatusColumn hides Rewards, then Expansion, before it drops under its minimum.
        var expansionColumn = MathF.Max(UiMetrics.Px(LayoutBudgets.ExpansionColumnLogical), HeaderFloor(Strings.ColumnExpansion, sortable: true));
        ImGui.TableSetupColumn(Strings.ColumnName, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoHide, UiMetrics.Px(NameColumnWidth));
        ImGui.TableSetupColumn(Strings.ColumnLevel, ImGuiTableColumnFlags.WidthFixed, levelColumn);
        ImGui.TableSetupColumn(Strings.ColumnJob, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort, UiMetrics.Px(JobColumnWidth));
        ImGui.TableSetupColumn(Strings.ColumnStatus, ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.NoSort, 1f);
        // Expansion is a four-letter tag: NoResize keeps its width the setup width, so hiding it frees exactly that.
        ImGui.TableSetupColumn(Strings.ColumnExpansion, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, expansionColumn);
        ImGui.TableSetupColumn(Strings.ColumnRewards, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort, rewardsColumn);
        ImGui.TableSetupScrollFreeze(0, 1);

        // The persisted sort is written straight into the column state on the table's first frame: ImGui's own saved
        // settings (imgui.ini) would otherwise win over DefaultSort and hand their sort back through SpecsDirty. It is
        // written again on the frame the sorted column comes back from a FitStatusColumn hide (ImGui dropped it).
        if (headerLanguage != Localization.Loc.Version)
        {
            // A new language: the saved Level and Job widths are checked against the new headers (below).
            headerLanguage = Localization.Loc.Version;
            measureJobWidth = true;
        }

        if (!tableInitialized || restoreSort)
        {
            // Once per table init the Job column's saved width is checked against its widest label (below).
            measureJobWidth |= !tableInitialized;
            tableInitialized = true;
            restoreSort = false;
            ApplyInitialSort(ui.Sort);
        }

        // Players with saved widths from before T15 keep a 64 px Job column, which clips "DoH/DoL" beside the icon:
        // the width measured on the table's first frame is raised to the minimum on the next (before the layout locks).
        if (jobWidthFix > 0f)
        {
            ImGuiP.TableSetColumnWidth((int)Column.Job, jobWidthFix);
            jobWidthFix = 0f;
        }

        if (levelWidthFix > 0f)
        {
            ImGuiP.TableSetColumnWidth((int)Column.Level, levelWidthFix);
            levelWidthFix = 0f;
        }

        // The two icon-derived widths are re-asserted every frame (imgui.ini restores font-tracked widths, not IconScale).
        ImGuiP.TableSetColumnWidth((int)Column.Glyph, glyphColumn);
        ImGuiP.TableSetColumnWidth((int)Column.Rewards, rewardsColumn);
        var statusWidth = DrawHeaders(SortedColumn(ui.Sort), out var jobWidth, out var levelWidth);
        if (measureJobWidth)
        {
            measureJobWidth = false;
            var jobMin = MathF.Max(
                MathF.Min(UiMetrics.Icon(JobIconSide), rowContent) + UiMetrics.Px(JobIconGap) + ImGui.CalcTextSize(Strings.JobDohDol).X,
                HeaderFloor(Strings.ColumnJob, sortable: false));
            if (jobWidth > 0f && jobWidth + 0.5f < jobMin)
            {
                jobWidthFix = MathF.Max(jobMin, UiMetrics.Px(JobColumnWidth));
            }

            var levelMin = levelColumn;
            if (levelWidth > 0f && levelWidth + 0.5f < levelMin)
            {
                levelWidthFix = levelColumn;
            }
        }

        FitStatusColumn(statusWidth, expansionColumn, rewardsColumn);

        ApplySortSpecs();
        ScrollToExternalSelection(rows, rowHeight);

        if (!clipperCreated)
        {
            clipper = ImGui.ImGuiListClipper();
            clipperCreated = true;
        }

        // Rows can be taller than the text when icons are scaled up; the selectable fills the row and centres its label.
        // Dense keeps the zebra so long lists stay easy to track across columns; Comfortable trades it for a faint
        // separator under each row (ui-revamp §2.4), which with the hover fill keeps the eye on the row. Raw pushes in a
        // struct scope: nothing allocates per frame.
        var dense = UiMetrics.Density == RowDensity.Dense;
        ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0f, 0.5f));
        ImGui.PushStyleColor(ImGuiCol.TableRowBgAlt, dense ? Theme.ZebraRow : Vector4.Zero);
        using var rowStyle = new Theme.StyleScope(1, 1);
        var layout = new RowLayout(lineHeight, rowContent, rowHeight, padY, glyphBox, glyphRadius, dense);
        var liftRow = hoveredRow;
        hoveredNext = null;
        moreFocusedNext = null;
        clipper.Begin(rows.Length, rowHeight);
        while (clipper.Step())
        {
            for (var i = clipper.DisplayStart; i < clipper.DisplayEnd && i < rows.Length; i++)
            {
                DrawRow(in rows[i], hasSnapshot, in layout, liftRow);
            }
        }

        clipper.End();
        hoveredRow = hoveredNext;
        moreFocusedRow = moreFocusedNext;
    }

    /// <summary>Per-frame row measurements, computed once per draw.</summary>
    private readonly record struct RowLayout(float LineHeight, float RowContent, float RowHeight, float PadY, float GlyphBox, float GlyphRadius, bool Dense)
    {
        /// <summary>Offset that centres a text line in the row.</summary>
        public float TextOffset => MathF.Max(0f, (RowContent - LineHeight) * 0.5f);
    }

    /// <summary>Moves the cursor down so a text line sits in the vertical middle of a row taller than the text.</summary>
    private static void CenterText(in RowLayout layout)
    {
        if (layout.TextOffset > 0.5f)
        {
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + layout.TextOffset);
        }
    }

    public void Dispose()
    {
        if (clipperCreated)
        {
            clipper.Destroy();
            clipperCreated = false;
        }
    }

    /// <summary>
    /// What TableHeadersRow does, one header at a time, so each can carry a tooltip. Labels are in the secondary tone
    /// on the raised fill; the sorted column's label and its arrow are in the primary text colour (a sort is not a call
    /// to action, so never gold). Returns the Status column's laid-out width (0 when hidden).
    /// </summary>
    private static float DrawHeaders(int sortedColumn, out float jobWidth, out float levelWidth)
    {
        var statusWidth = 0f;
        jobWidth = 0f;
        levelWidth = 0f;
        var s = Theme.Surface;

        // Header labels are captions (ui-revamp §4.2): 0.85× the body, never under 12 px, in the caption game font.
        using var caption = Typography.Caption();
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
        for (var i = 0; i < HeaderTooltips.Length; i++)
        {
            if (!ImGui.TableSetColumnIndex(i))
            {
                continue;
            }

            if (i == (int)Column.Status)
            {
                statusWidth = ImGui.GetContentRegionAvail().X;
            }
            else if (i == (int)Column.Job)
            {
                jobWidth = ImGui.GetContentRegionAvail().X;
            }
            else if (i == (int)Column.Level)
            {
                levelWidth = ImGui.GetContentRegionAvail().X;
            }

            ImGui.PushID(i);
            ImGui.PushStyleColor(ImGuiCol.Text, i == sortedColumn ? s.Text : s.TextSecondary);
            ImGui.TableHeader(HeaderLabels[i]);
            ImGui.PopStyleColor();
            ImGui.PopID();
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(HeaderTooltips[i]);
            }
        }

        return statusWidth;
    }

    /// <summary>The column index the persisted sort puts its arrow on, or -1 for journal order.</summary>
    private static int SortedColumn(SortSpec sort) => sort.Column switch
    {
        SortColumn.State => (int)Column.Glyph,
        SortColumn.Name => (int)Column.Name,
        SortColumn.Level => (int)Column.Level,
        SortColumn.Expansion => (int)Column.Expansion,
        _ => -1,
    };

    /// <summary>
    /// Keeps the Status column at least <see cref="StatusMinWidth"/> wide: when the laid-out width falls short, Rewards
    /// is hidden first, then Expansion; each comes back, Expansion first, once Status would still hold the minimum
    /// after giving back what the hide gained (measured on the frame the hide took effect, never less than the
    /// column's setup width, so the two cannot alternate). The column the table is sorted by is never hidden here,
    /// since ImGui drops the sort of a hidden column. A column the player hid or re-showed from the header menu is
    /// theirs: only the columns this hid are re-shown, and any column the player brings back (a disabled-to-enabled
    /// step this did not request) suspends the fit until Status is wide enough on its own.
    /// </summary>
    private void FitStatusColumn(float statusWidth, float expansionWidth, float rewardsWidth)
    {
        if (statusWidth <= 0f)
        {
            return;
        }

        var min = StatusMin();
        var padding = ImGui.GetStyle().CellPadding.X * 2f;
        var rewardsEnabled = IsColumnEnabled(Column.Rewards);
        var expansionEnabled = IsColumnEnabled(Column.Expansion);

        if (measureGainFor is { } hidden)
        {
            var measured = statusWidth - widthBeforeHide;
            if (hidden == Column.Rewards)
            {
                rewardsGain = MathF.Max(measured, rewardsWidth + padding);
            }
            else
            {
                expansionGain = MathF.Max(measured, expansionWidth + padding);
            }

            measureGainFor = null;
        }

        var rewardsBack = lastRewardsEnabled == false && rewardsEnabled;
        var expansionBack = lastExpansionEnabled == false && expansionEnabled;
        lastRewardsEnabled = rewardsEnabled;
        lastExpansionEnabled = expansionEnabled;
        if ((rewardsBack || expansionBack) && !fitReshowing)
        {
            if (expansionBack && expansionAutoHidden && ui.Sort.Column == SortColumn.Expansion)
            {
                restoreSort = true;
            }

            rewardsAutoHidden &= !rewardsBack;
            expansionAutoHidden &= !expansionBack;
            fitSuspended = true;
        }

        fitReshowing = false;

        if (statusWidth < min)
        {
            if (fitSuspended)
            {
                return;
            }

            if (rewardsEnabled)
            {
                Hide(Column.Rewards, statusWidth);
                rewardsAutoHidden = true;
            }
            else if (expansionEnabled && ui.Sort.Column != SortColumn.Expansion)
            {
                Hide(Column.Expansion, statusWidth);
                expansionAutoHidden = true;
            }

            return;
        }

        fitSuspended = false;
        if (expansionAutoHidden && statusWidth - expansionGain >= min)
        {
            ImGui.TableSetColumnEnabled((int)Column.Expansion, true);
            expansionAutoHidden = false;
            fitReshowing = true;
            restoreSort = ui.Sort.Column == SortColumn.Expansion;
        }
        else if (rewardsAutoHidden && !expansionAutoHidden && statusWidth - rewardsGain >= min)
        {
            ImGui.TableSetColumnEnabled((int)Column.Rewards, true);
            rewardsAutoHidden = false;
            fitReshowing = true;
        }
    }

    /// <summary>Hides a column for the next frame and arms the measurement of what Status gains from it.</summary>
    private void Hide(Column column, float statusWidth)
    {
        ImGui.TableSetColumnEnabled((int)column, false);
        measureGainFor = column;
        widthBeforeHide = statusWidth;
    }

    private static bool IsColumnEnabled(Column column) =>
        (ImGui.TableGetColumnFlags((int)column) & ImGuiTableColumnFlags.IsEnabled) != 0;

    private void DrawRow(in QuestRow row, bool hasSnapshot, in RowLayout layout, uint? liftRow)
    {
        var quest = row.Quest;
        ImGui.PushID((int)quest.RowId);
        ImGui.TableNextRow();

        var s = Theme.Surface;
        var state = hasSnapshot ? row.State : QuestState.Unknown;
        var selected = ui.SelectedRowId == quest.RowId;

        // Fills are the row's own background (painted when the row ends, behind every cell): the selection wash in
        // RowBg0 (it replaces the zebra), the hover fill over it in RowBg1, easing in and out through Motion.
        var hover = Motion.Lerp(HoverKeyTag | quest.RowId, liftRow == quest.RowId ? 1f : 0f);
        if (selected)
        {
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, Theme.WithAlpha(s.Text, SelectionWashAlpha));
        }

        if (hover > 0.004f)
        {
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, Theme.WithAlpha(s.Hover, hover * s.Hover.W));
        }

        // Glyph column: pinned dot at the left edge, state moon centred in the rest.
        ImGui.TableNextColumn();
        var cell = ImGui.GetCursorScreenPos();
        var lead = UiMetrics.Px(GlyphColumnLead);
        ImGui.Dummy(new Vector2(lead + layout.GlyphBox, layout.RowContent));
        var dl = ImGui.GetWindowDrawList();
        var centerY = cell.Y + layout.RowContent * 0.5f;

        if (runner.IsPinned(quest.RowId))
        {
            dl.AddCircleFilled(cell + new Vector2(UiMetrics.Px(3f), centerY - cell.Y), UiMetrics.Px(2.5f), Theme.MoonU32);
        }

        MoonGlyph.Draw(dl, new Vector2(cell.X + lead + layout.GlyphBox * 0.5f, centerY), layout.GlyphRadius, state);

        // Name column carries the row-wide selectable and the context menu. Its Header colours are transparent so it
        // paints neither hover nor selection over the fills above (keyboard focus still gets ImGui's nav frame).
        ImGui.TableNextColumn();
        var nameCellMin = ImGui.GetCursorScreenPos();
        var nameCellWidth = ImGui.GetContentRegionAvail().X;
        var name = runner.Spoilers.DisplayName(quest);
        ImGui.PushStyleColor(ImGuiCol.Header, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, Vector4.Zero);
        var clicked = ImGui.Selectable(name, selected, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowDoubleClick | ImGuiSelectableFlags.AllowItemOverlap, new Vector2(0f, layout.RowContent));
        ImGui.PopStyleColor(3);

        // The selectable spans the table's width: its rectangle gives the row's left and right edges.
        var rowHovered = ImGui.IsItemHovered();
        var rowFocused = ImGui.IsItemFocused();

        // The Menu key or Shift+F10 on the focused row opens its menu (accessibility A6).
        if (rowFocused && Keyboard.OpenMenuOnKey(RowMenuId))
        {
            SelectFromTable(quest.RowId);
        }

        var rowMin = new Vector2(ImGui.GetItemRectMin().X, nameCellMin.Y - layout.PadY);
        var rowMax = new Vector2(ImGui.GetItemRectMax().X, rowMin.Y + layout.RowHeight);
        if (rowHovered)
        {
            hoveredNext = quest.RowId;
        }

        // A reveal from another pane landed on this row: its pulse starts the first frame the row is drawn.
        if (revealRow == quest.RowId)
        {
            revealRow = null;
            if (ImGui.GetTime() <= revealUntil)
            {
                Motion.Trigger(Motion.Key(RevealTag, quest.RowId));
            }
        }

        DrawRowChrome(rowMin, rowMax, state, selected, liftRow == quest.RowId && hover > 0.5f, in layout, Motion.Key(RevealTag, quest.RowId));
        if (newGroupEnd == quest.RowId)
        {
            DrawGroupEnd(rowMin, rowMax);
        }

        if (clicked)
        {
            SelectFromTable(quest.RowId);
            // The game journal only knows accepted and completed quests; for the rest a double-click just selects.
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && GameLinks.CanOpenJournal(quest, row.State))
            {
                links.OpenJournal(quest);
            }
        }

        using (var popup = ImRaii.ContextPopupItem(RowMenuId))
        {
            if (popup)
            {
                if (ImGui.IsWindowAppearing())
                {
                    SelectFromTable(quest.RowId);
                    RefreshQuestMapAvailability();
                }

                DrawContextMenu(quest, row.State);
            }
        }

        // The "…" button at the name cell's right end, shown while the mouse is over the row or the row (or the button
        // itself) has keyboard focus: a left click, Enter or Space opens the same menu, so no action needs the right
        // button (accessibility A6).
        var mouseInRow = ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(rowMin, rowMax);
        if (mouseInRow)
        {
            // The "…" takes the hover from the selectable (AllowItemOverlap), so rowHovered alone would drop the
            // row's fill while the pointer is on the button.
            hoveredNext = quest.RowId;
        }

        if (mouseInRow || rowFocused || moreFocusedRow == quest.RowId)
        {
            // Sized to the row's content, not its padded height: a button reaching into the cell padding would push
            // the cell's CursorMaxPos down and make the hovered or focused row taller.
            var size = MathF.Min(layout.RowContent, UiMetrics.MinTarget);
            var moreMin = new Vector2(nameCellMin.X + nameCellWidth - size, nameCellMin.Y + ((layout.RowContent - size) * 0.5f));
            if (Keyboard.MoreButton("##more", RowMenuId, moreMin, size))
            {
                SelectFromTable(quest.RowId);
            }

            if (ImGui.IsItemFocused())
            {
                moreFocusedNext = quest.RowId;
            }
        }

        // The selectable spans every column; the banner tooltip belongs to the name cell only, the stripe's to the
        // row's left edge, so the reward and job icons keep their own tooltips.
        var badge = runner.Stories.Contains(quest.RowId)
            ? DrawStoryBadge(ImGui.GetWindowDrawList(), nameCellMin, nameCellWidth, layout.RowContent, name)
            : default;
        var mouseX = ImGui.GetMousePos().X;
        if (rowHovered)
        {
            if (mouseX >= rowMin.X && mouseX <= rowMin.X + MathF.Max(lead, StripeThickness()))
            {
                UiMetrics.Tooltip(StripePattern.Tooltip(state, quest.RepeatInterval));
            }
            else if (badge.Y > 0f && mouseX >= badge.X && mouseX <= badge.X + badge.Y)
            {
                UiMetrics.Tooltip(runner.StoryBadgeText(quest.RowId));
            }
            else if (mouseX >= nameCellMin.X && mouseX <= nameCellMin.X + nameCellWidth)
            {
                DrawNameTooltip(quest, row.State);
            }
        }

        ImGui.TableNextColumn();
        DrawPill(runner.LevelText(quest.DisplayLevel), UiMetrics.Px(LevelPillMinWidth), UiMetrics.Px(PillPadX), s.TextSecondary, in layout);

        ImGui.TableNextColumn();
        DrawJob(runner.Job(quest), rowHovered, mouseX, in layout);

        ImGui.TableNextColumn();
        CenterText(in layout);
        var statusCellMin = ImGui.GetCursorScreenPos();
        var statusCellWidth = ImGui.GetContentRegionAvail().X;
        var cut = DrawStatus(row.Status, hasSnapshot, statusCellWidth, layout.LineHeight);

        // The state word is never cut; when the reason after it was ellipsised, the whole line is the cell's tooltip.
        if (cut && rowHovered && mouseX >= statusCellMin.X && mouseX <= statusCellMin.X + statusCellWidth)
        {
            UiMetrics.Tooltip(row.Status);
        }

        ImGui.TableNextColumn();
        DrawPill(runner.ExpansionShort(quest.Expansion), 0f, UiMetrics.Px(ExpansionPillPadX), s.TextTertiary, in layout);

        ImGui.TableNextColumn();
        DrawRewardIcons(quest, in layout);
        ImGui.PopID();
    }

    /// <summary>
    /// The line under the last row of the Unlocks quick view's "New in 7.5x" group (P8): a moon-gold rule across
    /// every column on the background channel, so the group reads as one block above the rest.
    /// </summary>
    private static void DrawGroupEnd(Vector2 rowMin, Vector2 rowMax)
    {
        ImGuiP.TablePushBackgroundChannel();
        var thickness = MathF.Max(1f, UiMetrics.Hairline * 2f);
        var y = rowMax.Y - thickness * 0.5f;
        ImGui.GetWindowDrawList().AddLine(new Vector2(rowMin.X, y), new Vector2(rowMax.X, y), Theme.WithAlpha(Theme.Moon, 0.55f), thickness);
        ImGuiP.TablePopBackgroundChannel();
    }

    /// <summary>Width of the state stripe in pixels: the glyph palette's (3 logical, 4 in high contrast), never under 2.</summary>
    internal static float StripeThickness() => MathF.Max(2f, MathF.Round(UiMetrics.Px(Theme.Glyphs.StripeWidth)));

    /// <summary>
    /// The row's lines, on the table's background channel so they span every column under the text: the separator
    /// (Comfortable), the selection ring (1 px, the text colour at 0.45, rounded, inset 1 px: a selection, not gold),
    /// the hover lift, and the state stripe on top of them at the left edge.
    /// </summary>
    private static void DrawRowChrome(Vector2 rowMin, Vector2 rowMax, QuestState state, bool selected, bool lifted, in RowLayout layout, ulong revealKey)
    {
        var s = Theme.Surface;
        ImGuiP.TablePushBackgroundChannel();
        var dl = ImGui.GetWindowDrawList();
        var hairline = UiMetrics.Hairline;
        if (!layout.Dense)
        {
            var y = rowMax.Y - hairline * 0.5f;
            dl.AddLine(new Vector2(rowMin.X, y), new Vector2(rowMax.X, y), Theme.WithAlpha(s.Line, SeparatorAlpha * s.Line.W), hairline);
        }

        if (lifted)
        {
            Chrome.Lift(dl, rowMin, rowMax);
        }

        if (selected)
        {
            var inset = new Vector2(hairline * 0.5f + UiMetrics.Px(1f));
            dl.AddRect(rowMin + inset, rowMax - inset, Theme.WithAlpha(s.Text, SelectionRingAlpha), UiMetrics.Px(SelectionRounding), ImDrawFlags.None, hairline);
        }

        DrawStripe(dl, rowMin.X, rowMin.Y, rowMax.Y - rowMin.Y, state);
        if (selected)
        {
            Motion.DrawRevealPulse(dl, revealKey, rowMin, rowMax, UiMetrics.Px(SelectionRounding));
        }

        ImGuiP.TablePopBackgroundChannel();
    }

    /// <summary>The state stripe: <see cref="StripePattern"/>'s runs for the state, snapped to whole pixels, in the state's colour.</summary>
    private static void DrawStripe(ImDrawListPtr dl, float x, float top, float height, QuestState state)
    {
        var segments = StripePattern.Segments(state);
        if (segments.IsEmpty || height <= 0f)
        {
            return;
        }

        var color = StripeColor(state);
        var right = x + StripeThickness();
        foreach (var segment in segments)
        {
            var y0 = MathF.Round(top + segment.Start * height);
            var y1 = MathF.Max(y0 + 1f, MathF.Round(top + segment.End * height));
            dl.AddRectFilled(new Vector2(x, y0), new Vector2(right, y1), color);
        }
    }

    /// <summary>
    /// The stripe's colour, from the Theme tokens the moons use: gold for what can be acted on (Ready, In journal), the
    /// quieter gold for Completed, silver for Ready on another job and Done (the palette's text colour in a light
    /// Dalamud theme, where silver would vanish), Eclipse for Locked out and VeilText for Not checked. The pattern,
    /// not the colour, carries the state. The high-contrast glyph palette uses its own rungs instead
    /// (<see cref="GlyphPalette.Stripe"/>: the state's identity colour, 3 : 1 or better on the host window).
    /// </summary>
    internal static uint StripeColor(QuestState state) => Theme.Glyphs.HighContrast ? Theme.U32(Theme.Glyphs.Stripe(state)) : state switch
    {
        QuestState.Ready or QuestState.Accepted => Theme.MoonU32,
        QuestState.Completed => Theme.MoonDimU32,
        QuestState.ReadyOnOtherJob or QuestState.DoneThisCycle => Theme.Surface.Light ? Theme.U32(Theme.Surface.Text) : Theme.SilverU32,
        QuestState.Foreclosed => Theme.EclipseU32,
        _ => Theme.VeilTextU32,
    };

    /// <summary>Width of a pill holding <paramref name="text"/>: the text padded each side, at least <paramref name="minWidth"/>.</summary>
    private static float PillWidth(string text, float minWidth, float padX) => MathF.Max(minWidth, ImGui.CalcTextSize(text).X + padX * 2f);

    /// <summary>
    /// A level or expansion pill (ui-revamp §2.4): the palette's sunken fill with the text in <paramref name="ink"/>,
    /// vertically centred in the row and never wider than the cell. A dummy of the pill's width is the cell's item.
    /// </summary>
    private static void DrawPill(string text, float minWidth, float padX, Vector4 ink, in RowLayout layout)
    {
        if (text.Length == 0)
        {
            return;
        }

        var pos = ImGui.GetCursorScreenPos();
        var avail = ImGui.GetContentRegionAvail().X;
        using var caption = Typography.Caption();
        var textSize = ImGui.CalcTextSize(text);
        var height = MathF.Min(layout.RowContent, MathF.Max(UiMetrics.Px(PillHeight), textSize.Y + UiMetrics.Px(2f)));
        var width = MathF.Max(1f, MathF.Min(avail, MathF.Max(minWidth, textSize.X + padX * 2f)));
        ImGui.Dummy(new Vector2(width, layout.RowContent));
        var s = Theme.Surface;
        var min = new Vector2(pos.X, pos.Y + MathF.Round((layout.RowContent - height) * 0.5f));
        Chrome.PillAt(ImGui.GetWindowDrawList(), min, new Vector2(width, height), text, Theme.U32(s.Sunken), 0u, Theme.U32(ink));
    }

    /// <summary>
    /// The Job cell: a quest limited to one job shows the job's game icon (16 px at scale 1) before its abbreviation;
    /// groups and "Any" keep the icon's slot so the labels line up. Hovering it names the job, or the group.
    /// </summary>
    private void DrawJob(JobLabel job, bool rowHovered, float mouseX, in RowLayout layout)
    {
        var pos = ImGui.GetCursorScreenPos();
        var icon = MathF.Min(UiMetrics.Icon(JobIconSide), layout.RowContent);
        var gap = UiMetrics.Px(JobIconGap);
        var textSize = ImGui.CalcTextSize(job.Short);
        var width = icon + gap + textSize.X;
        ImGui.Dummy(new Vector2(width, layout.RowContent));

        var dl = ImGui.GetWindowDrawList();
        if (job.IconId != 0)
        {
            var iconMin = new Vector2(pos.X, pos.Y + MathF.Round((layout.RowContent - icon) * 0.5f));
            var wrap = textures.GetFromGameIcon(new GameIconLookup(job.IconId)).GetWrapOrEmpty();
            dl.AddImage(wrap.Handle, iconMin, iconMin + new Vector2(icon, icon));
        }

        dl.AddText(new Vector2(pos.X + icon + gap, pos.Y + layout.TextOffset), Theme.U32(Theme.Surface.TextSecondary), job.Short);

        if (rowHovered && job.Name.Length > 0 && mouseX >= pos.X && mouseX <= pos.X + width)
        {
            UiMetrics.Tooltip(job.Name);
        }
    }

    /// <summary>
    /// The book badge of a story sidequest in Dusk, just after the name, or at the cell's right edge when the name
    /// fills it. Draw list only, so the row's selectable stays the hovered item. Returns the badge's left x and width.
    /// </summary>
    private static Vector2 DrawStoryBadge(ImDrawListPtr dl, Vector2 cellMin, float cellWidth, float rowContent, string name)
    {
        var afterName = cellMin.X + ImGui.CalcTextSize(name).X + UiMetrics.Px(StoryBadgeGap);
        ImGui.PushFont(UiBuilder.IconFont);
        var size = ImGui.CalcTextSize(StoryBadgeIcon);
        var x = MathF.Max(cellMin.X, MathF.Min(afterName, cellMin.X + cellWidth - size.X));
        dl.AddText(new Vector2(x, cellMin.Y + (rowContent - size.Y) * 0.5f), Theme.U32(Theme.Surface.TextTertiary), StoryBadgeIcon);
        ImGui.PopFont();
        return new Vector2(x, size.X);
    }

    /// <summary>
    /// Status text (P1): the state word first in the primary text colour, then the reason after the separator in the
    /// secondary tone (Mist: Dusk fails AA on a hovered row), spans only, no new strings. The state word is never cut
    /// short; a reason too long for the cell is ellipsised in the room the state word leaves. In browse mode (no
    /// snapshot) the whole line is in the tertiary tone. Returns whether the reason was cut.
    /// </summary>
    private static bool DrawStatus(string text, bool hasSnapshot, float cellWidth, float lineHeight)
    {
        var s = Theme.Surface;
        var stateInk = hasSnapshot ? s.Text : s.TextTertiary;
        var reasonInk = hasSnapshot ? s.TextSecondary : s.TextTertiary;
        var split = TableGeometry.StateWordLength(text);
        var state = text.AsSpan(0, split);
        ImGui.PushStyleColor(ImGuiCol.Text, stateInk);
        ImGui.TextUnformatted(state);
        ImGui.PopStyleColor();
        if (split >= text.Length)
        {
            return false;
        }

        var reason = text.AsSpan(split);
        var room = TableGeometry.ReasonWidth(cellWidth, ImGui.CalcTextSize(state).X);
        var reasonSize = ImGui.CalcTextSize(reason);
        ImGui.SameLine(0f, 0f);
        ImGui.PushStyleColor(ImGuiCol.Text, reasonInk);
        if (!TableGeometry.ReasonNeedsEllipsis(reasonSize.X, room))
        {
            ImGui.TextUnformatted(reason);
            ImGui.PopStyleColor();
            return false;
        }

        var min = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(room, lineHeight));
        if (room > 1f)
        {
            var max = new Vector2(min.X + room, min.Y + lineHeight);
            Vector2? known = reasonSize;
            ImGuiP.RenderTextEllipsis(ImGui.GetWindowDrawList(), in min, in max, max.X, max.X, reason, in known);
        }

        ImGui.PopStyleColor();
        return true;
    }

    /// <summary>
    /// Hover card for a quest name: the journal banner (when the quest has one and it is loaded) about 240 px wide,
    /// the name, then genre, expansion and level. Textures come from the provider's per-frame cache, nothing is kept.
    /// The spoiler shield hides the banner of a quest not yet in the journal (a line says so) and masks the name.
    /// </summary>
    private void DrawNameTooltip(QuestRecord quest, QuestState state)
    {
        using var tooltipStyle = Theme.PushTooltip();
        using var tooltip = ImRaii.Tooltip();
        UiMetrics.ApplyFontScale();
        var width = UiMetrics.BannerTooltipWidth;
        var spoilers = runner.Spoilers;
        var showArtwork = spoilers.ShowArtwork(quest, state);
        if (quest.Icon != 0 && !showArtwork)
        {
            ArtworkPlaceholder.Draw(width);
        }
        else if (quest.Icon != 0 && textures.GetFromGameIcon(new GameIconLookup(quest.Icon)).TryGetWrap(out var wrap, out _) && wrap.Width > 0 && wrap.Height > 0)
        {
            ImGui.Image(wrap.Handle, new Vector2(width, width * wrap.Height / wrap.Width));
        }

        using var wrapPos = ImRaii.TextWrapPos(ImGui.GetCursorPosX() + width);
        ImGui.TextWrapped(spoilers.DisplayName(quest));
        ImGui.TextDisabled(quest.Journal.GenreName);
        ImGui.TextDisabled(runner.ExpansionShort(quest.Expansion));
        ImGui.SameLine();
        ImGui.TextDisabled(Strings.ColumnLevel);
        ImGui.SameLine(0f, UiMetrics.Px(3f));
        ImGui.TextDisabled(runner.LevelText(quest.DisplayLevel));
    }

    private void DrawRewardIcons(QuestRecord quest, in RowLayout layout)
    {
        var drawn = 0;
        var rewards = quest.Rewards;
        var iconSize = UiMetrics.RowIconSize;
        var iconOffset = MathF.Max(0f, (layout.RowContent - iconSize) * 0.5f);
        for (var i = 0; i < rewards.Count && drawn < MaxRewardIcons; i++)
        {
            var reward = rewards[i];
            if (reward.Icon == 0)
            {
                continue;
            }

            if (drawn > 0)
            {
                ImGui.SameLine(0f, UiMetrics.Px(2f));
            }
            else if (iconOffset > 0.5f)
            {
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + iconOffset);
            }

            var wrap = textures.GetFromGameIcon(new GameIconLookup(reward.Icon)).GetWrapOrEmpty();
            ImGui.Image(wrap.Handle, new Vector2(iconSize, iconSize));
            if (ImGui.IsItemHovered())
            {
                RewardTooltip.Draw(reward, links, textures);
            }

            drawn++;
        }
    }

    private void DrawContextMenu(QuestRecord quest, QuestState state)
    {
        if (ImGui.MenuItem(runner.IsPinned(quest.RowId) ? Strings.Unpin : Strings.Pin, enabled: runner.CanPin))
        {
            runner.TogglePin(quest.RowId);
        }

        if (ImGui.MenuItem(Strings.FlagOnMap, enabled: links.CanFlagMap(quest)))
        {
            links.FlagMap(quest);
        }

        var canOpen = GameLinks.CanOpenJournal(quest, state);
        if (ImGui.MenuItem(Strings.OpenJournal, enabled: canOpen))
        {
            links.OpenJournal(quest);
        }

        if (!canOpen && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.OpenJournalUnavailable);
        }

        if (ImGui.MenuItem(Strings.CopyName))
        {
            ImGui.SetClipboardText(runner.Spoilers.DisplayName(quest));
        }

        var canCopyCoordinates = links.MapCoordinates(quest) is not null;
        if (ImGui.MenuItem(Strings.CopyCoordinates, enabled: canCopyCoordinates) && links.CoordinateText(quest) is { } coordinates)
        {
            ImGui.SetClipboardText(coordinates);
        }

        if (ImGui.MenuItem(Strings.ShowPath))
        {
            ui.ShowPath(quest.RowId);
        }

        if (ImGui.MenuItem(Strings.LinkInChat))
        {
            links.PrintQuestLink(quest);
        }

        // Hidden without Lifestream; disabled, with the reason on hover, while it is busy or the giver's zone has no aetheryte.
        if (links.TeleportAvailable)
        {
            var aetheryte = links.NearestAetheryte(quest);
            var busy = links.TeleportBusy;
            if (ImGui.MenuItem(Strings.TeleportToGiver, enabled: aetheryte is not null && !busy))
            {
                links.TeleportToGiver(quest);
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                if (aetheryte is not { } target)
                {
                    UiMetrics.Tooltip(Strings.TeleportNoAetheryte);
                }
                else if (busy)
                {
                    UiMetrics.Tooltip(Strings.TeleportBusy);
                }
                else
                {
                    UiMetrics.Tooltip(string.Format(CultureInfo.CurrentCulture, Strings.TeleportTooltipFormat, target.Name));
                }
            }
        }

        if (questMapAvailable && ImGui.MenuItem(Strings.QuestMapGraph))
        {
            try
            {
                questMap?.InvokeAction(quest.RowId);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Quest Map IPC call failed");
                questMapAvailable = false;
            }
        }
    }

    /// <summary>
    /// The chips' labels in the UI language: the guard names filters by their English identities
    /// (<see cref="FilterNames"/>), which clearing one still switches on. Rebuilt when the list or the language changes.
    /// </summary>
    private System.Collections.Generic.IReadOnlyList<string> EmptyChipLabels(System.Collections.Generic.IReadOnlyList<string> filters)
    {
        if (!ReferenceEquals(filters, emptyChipSource) || emptyChipLanguage != Localization.Loc.Version)
        {
            emptyChipSource = filters;
            emptyChipLanguage = Localization.Loc.Version;
            var labels = new string[filters.Count];
            for (var i = 0; i < labels.Length; i++)
            {
                labels[i] = FilterNames.Display(filters[i]);
            }

            emptyChipLabels = labels;
        }

        return emptyChipLabels;
    }

    private System.Collections.Generic.IReadOnlyList<string>? emptyChipSource;
    private int emptyChipLanguage = -1;
    private System.Collections.Generic.IReadOnlyList<string> emptyChipLabels = [];

    private void DrawEmpty(EmptyReason empty)
    {
        ui.RecordWindow(UiRects.Table);
        if (empty.ScopeIsEmpty)
        {
            EmptyState.Draw(Strings.ScopeEmpty);
            return;
        }

        // Heading, one line, the offending filters as chips (each clears only itself) and Reset (T16, ui-revamp §2.8).
        var body = empty.Filters.Count > 0 ? Strings.EmptyFiltersHiding : Strings.NothingMatchesCombination;
        var clicked = EmptyState.DrawWithAction(Strings.EmptyNothingMatchesHeading, body, Strings.ResetFilters, EmptyChipLabels(empty.Filters), QuestState.Blocked);
        if (clicked == EmptyState.ActionClicked)
        {
            resetFilters();
        }
        else if (clicked >= 0 && EmptyState.ClearFilter(ui, empty.Filters[clicked]))
        {
            // Re-runs the query and saves the filters, as every other filter change does.
            filtersChanged();
        }
    }

    /// <summary>
    /// Sets the table's sort column and direction from the persisted sort; journal order clears every column's sort
    /// (allowed because the table is SortTristate). Called between the column setup and the header row.
    /// </summary>
    private static void ApplyInitialSort(SortSpec initial)
    {
        var column = initial.Column switch
        {
            SortColumn.State => Column.Glyph,
            SortColumn.Name => Column.Name,
            SortColumn.Level => Column.Level,
            SortColumn.Expansion => Column.Expansion,
            _ => (Column?)null,
        };
        if (column is not { } index)
        {
            ImGuiP.TableSetColumnSortDirection((int)Column.Glyph, ImGuiSortDirection.None, appendToSortSpecs: false);
            return;
        }

        ImGuiP.TableSetColumnSortDirection((int)index, initial.Descending ? ImGuiSortDirection.Descending : ImGuiSortDirection.Ascending, appendToSortSpecs: false);
    }

    private void ApplySortSpecs()
    {
        var specs = ImGui.TableGetSortSpecs();
        if (specs.IsNull || !specs.SpecsDirty)
        {
            return;
        }

        // ImGui clears the sort of a column that is disabled and reports it as no specs: while FitStatusColumn holds
        // the sorted column hidden that is its doing, not a header click, and the persisted sort stands.
        if (specs.SpecsCount == 0 && expansionAutoHidden && ui.Sort.Column == SortColumn.Expansion)
        {
            specs.SpecsDirty = false;
            return;
        }

        // No specs (the third header click, or a fresh table) means journal order; the pinned-first choice is not a header's to change.
        var sort = SortSpec.Default with { PinnedFirst = ui.Sort.PinnedFirst };
        if (specs.SpecsCount > 0)
        {
            var spec = specs.Specs;
            var column = spec.ColumnIndex switch
            {
                (short)Column.Glyph => SortColumn.State,
                (short)Column.Name => SortColumn.Name,
                (short)Column.Level => SortColumn.Level,
                (short)Column.Expansion => SortColumn.Expansion,
                _ => SortColumn.Journal,
            };
            sort = sort with { Column = column, Descending = spec.SortDirection == ImGuiSortDirection.Descending };
        }

        specs.SpecsDirty = false;
        if (sort != ui.Sort)
        {
            ui.Sort = sort;
            ui.MarkQueryDirty();
        }
    }

    /// <summary>When another pane changed the selection, scroll the table so the row is visible.</summary>
    private void ScrollToExternalSelection(QuestRow[] rows, float rowHeight)
    {
        if (ui.SelectedRowId == lastSelection)
        {
            return;
        }

        lastSelection = ui.SelectedRowId;
        if (lastSelection is not { } rowId)
        {
            return;
        }

        for (var i = 0; i < rows.Length; i++)
        {
            if (rows[i].Quest.RowId == rowId)
            {
                ImGui.SetScrollY(MathF.Max(0f, i * rowHeight - ImGui.GetContentRegionAvail().Y * 0.4f));
                return;
            }
        }
    }

    private void SelectFromTable(uint rowId)
    {
        if (ui.SelectedRowId == rowId)
        {
            return;
        }

        // Recording the selection here keeps ScrollToExternalSelection from scrolling to a row the user just clicked.
        ui.SelectedRowId = rowId;
        lastSelection = rowId;
    }

    private void RefreshQuestMapAvailability()
    {
        questMapAvailable = false;
        if (questMap is null)
        {
            return;
        }

        try
        {
            foreach (var plugin in pluginInterface.InstalledPlugins)
            {
                if (plugin.IsLoaded && string.Equals(plugin.InternalName, QuestMapPluginName, StringComparison.OrdinalIgnoreCase))
                {
                    questMapAvailable = true;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Installed plugin list unavailable");
        }
    }
}
