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
using Column = Tsukimichi.Core.Ui.QuestColumn;

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
///
/// Narrow widths (feature plan v4 L4, UI audit §4.3): <see cref="TableGeometry.PlanQuestTable"/> decides the columns
/// from the table's width. Name is the one ImGui stretch column and takes what the fixed columns leave; Status is sized
/// by the plan (its state word never cut) and hides only after Rewards, Expansion, Job (its icon alone first) and
/// Level. The plan hides a column with <see cref="ImGuiTableColumnFlags.Disabled"/>, which ImGui neither saves nor
/// lists in the header menu, so the player's own hidden columns (the menu's) stay theirs and stay hidden. Under
/// 360 px the rows go two-line: the name and the level pill, then the status under the name.
/// </summary>
public sealed class TablePane : IDisposable
{
    private const string QuestMapPluginName = "QuestMap";
    private const string QuestMapIpcName = "QuestMap.ShowGraphByQuestId";
    private const int MaxRewardIcons = 4;

    /// <summary>Logical space left of the state moon for the pinned dot; also the state stripe's hover zone.</summary>
    private const float GlyphColumnLead = 8f;

    /// <summary>
    /// The table's ImGui id. It changed with the column plan (feature plan v4 L4): the widths, weights and hidden flags
    /// imgui.ini kept under the old id include columns the old status fit hid on its own, which would otherwise read
    /// as hidden by the player forever.
    /// </summary>
    private const string TableId = "##questtable";

    private const int ColumnCount = TableGeometry.QuestColumnCount;

    // The widest state word in the current language and font (V2-19): the status column never cuts it.
    private int stateWordLanguage = -1;
    private float stateWordFont = -1f;
    private float stateWord;

    /// <summary>The widest state word (every state's name, and "Done this cycle"), measured again on a language or font change.</summary>
    private float StateWordWidth()
    {
        var font = ImGui.GetFontSize();
        if (stateWordLanguage != Localization.Loc.Version || stateWordFont != font)
        {
            stateWordLanguage = Localization.Loc.Version;
            stateWordFont = font;
            var widest = 0f;
            foreach (var state in Enum.GetValues<QuestState>())
            {
                widest = MathF.Max(widest, ImGui.CalcTextSize(Strings.StateName(state)).X);
            }

            stateWord = MathF.Max(widest, ImGui.CalcTextSize(Strings.StateName(QuestState.DoneThisCycle, null)).X);
        }

        return stateWord;
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

    // The column plan (PlanColumns): this frame's specs, visibility and widths; last frame's visibility (planned is
    // false before the first plan) and row mode; the columns the plan hides this frame (Disabled) and the ones the
    // player hid from the header menu, read from the table after each layout. Arrays, so nothing allocates per frame.
    private readonly ColumnSpec[] planSpecs = new ColumnSpec[ColumnCount];
    private readonly bool[] planVisible = new bool[ColumnCount];
    private readonly float[] planWidths = new float[ColumnCount];
    private readonly bool[] lastVisible = new bool[ColumnCount];
    private readonly bool[] autoHidden = new bool[ColumnCount];
    private readonly bool[] playerHidden = new bool[ColumnCount];
    private bool planned;
    private QuestTablePlan plan;

    // A sort to write back into the column state once the sorted column is shown again (ImGui drops a hidden column's sort).
    private bool restoreSort;

    // The Name header's note while the plan hides the sorted column, and what it was built for.
    private string hiddenSortNote = string.Empty;
    private SortColumn hiddenSortColumn;
    private int hiddenSortLanguage = -1;

    // Last frame's row height: rows that change height keep the first visible row in view.
    private float lastRowHeight;

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
        using var table = ImRaii.Table(TableId, ColumnCount, flags, ImGui.GetContentRegionAvail());
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
        // The moon, and with it the Glyph column, follows the one-line row height (r 9 Comfortable, r 6.4 Dense at scale 1).
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
        var expansionColumn = MathF.Max(UiMetrics.Px(LayoutBudgets.ExpansionColumnLogical), HeaderFloor(Strings.ColumnExpansion, sortable: true));
        // The job column: icon, gap and "DoH/DoL" (never under its 76 px budget); narrowed, its icon alone (and its header).
        var jobIcon = MathF.Min(UiMetrics.Icon(JobIconSide), rowContent);
        var jobHeader = HeaderFloor(Strings.ColumnJob, sortable: false);
        var jobColumn = MathF.Max(MathF.Max(UiMetrics.Px(LayoutBudgets.JobColumnLogical), jobIcon + UiMetrics.Px(JobIconGap) + ImGui.CalcTextSize(Strings.JobDohDol).X), jobHeader);
        var jobIconColumn = MathF.Max(jobIcon, jobHeader);

        // The column plan, from the table's width (its window less the scrollbar when the rows overflow). ImGui adds the
        // cell padding either side of a column's content width, and the border between columns.
        var overhead = style.CellPadding.X * 2f + 1f;
        var available = ImGui.GetWindowWidth() - (ImGui.GetScrollMaxY() > 0f ? style.ScrollbarSize : 0f);
        var widths = new QuestTableWidths(glyphColumn, levelColumn, jobIconColumn, jobColumn, StateWordWidth(), expansionColumn, rewardsColumn, overhead, UiMetrics.Px(1f));
        var sortedWasHidden = SortColumnAutoHidden();
        PlanColumns(available, in widths);

        // Name is the one ImGui stretch column: it takes what the fixed columns leave. Every other column is fixed and
        // NoResize, so ImGui lays it out at the width given here each frame (the plan's for Job and Status); a column the
        // plan hides is Disabled, which ImGui neither saves nor lists in the header menu.
        const ImGuiTableColumnFlags fixedFlags = ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize;
        ImGui.TableSetupColumn(Strings.ColumnGlyph, fixedFlags | ImGuiTableColumnFlags.NoHide | ImGuiTableColumnFlags.NoHeaderLabel, glyphColumn);
        ImGui.TableSetupColumn(Strings.ColumnName, ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.NoHide, LayoutBudgets.TableNameWeight);
        ImGui.TableSetupColumn(Strings.ColumnLevel, fixedFlags | Planned(Column.Level), levelColumn);
        ImGui.TableSetupColumn(Strings.ColumnJob, fixedFlags | ImGuiTableColumnFlags.NoSort | Planned(Column.Job), plan.JobIconOnly ? jobIconColumn : jobColumn);
        ImGui.TableSetupColumn(Strings.ColumnStatus, fixedFlags | ImGuiTableColumnFlags.NoSort | Planned(Column.Status), PlannedContent(Column.Status, TableGeometry.StatusColumnMin(widths) - overhead, overhead));
        ImGui.TableSetupColumn(Strings.ColumnExpansion, fixedFlags | Planned(Column.Expansion), expansionColumn);
        ImGui.TableSetupColumn(Strings.ColumnRewards, fixedFlags | ImGuiTableColumnFlags.NoSort | Planned(Column.Rewards), rewardsColumn);
        ImGui.TableSetupScrollFreeze(0, 1);

        // The persisted sort is written straight into the column state on the table's first frame: ImGui's own saved
        // settings (imgui.ini) would otherwise win over DefaultSort and hand their sort back through SpecsDirty. It is
        // written again on the frame the sorted column comes back from a plan hide (ImGui dropped its sort).
        if (sortedWasHidden && !SortColumnAutoHidden())
        {
            restoreSort = true;
        }

        if (!tableInitialized || restoreSort)
        {
            tableInitialized = true;
            restoreSort = false;
            ApplyInitialSort(ui.Sort);
        }

        // While the plan hides the sorted column (its header and arrow are gone), the Name header's tooltip names the sort.
        DrawHeaders(SortedColumn(ui.Sort), SortColumnAutoHidden() ? HiddenSortNote() : null);

        // The player's own hidden columns, read after the layout: a column the plan did not hide is shown unless the
        // player hid it from the header menu (the plan's hides never touch that flag).
        var hiddenChanged = false;
        for (var i = (int)Column.Level; i < ColumnCount; i++)
        {
            if (!autoHidden[i])
            {
                var hidden = !IsColumnEnabled((Column)i);
                hiddenChanged |= hidden != playerHidden[i];
                playerHidden[i] = hidden;
            }
        }

        // A column hidden or shown from the header menu reaches the layout only now (ImGui applies it in this frame's
        // layout), so the plan made before it is stale: plan again, so this frame's rows and the next frame's columns
        // follow the menu rather than lag a frame behind it.
        if (hiddenChanged)
        {
            var sortWasHidden = SortColumnAutoHidden();
            PlanColumns(available, in widths);
            restoreSort |= sortWasHidden && !SortColumnAutoHidden();
        }

        // Two-line rows (design v4 §8.2): the name and the level on the first line, the status under it in the caption
        // role, the moon centred on both.
        float statusLine;
        using (Typography.Caption())
        {
            statusLine = ImGui.GetTextLineHeight();
        }

        var lineGap = UiMetrics.Px(LayoutBudgets.TableTwoLineGapLogical);
        var content = plan.TwoLine ? MathF.Max(rowContent, lineHeight + lineGap + statusLine) : rowContent;
        var rowHeight = content + padY * 2f;

        // Rows that change height (one line to two and back, the density, the UI scale) keep the first visible row in
        // view rather than the pixel offset, which would land elsewhere in the list and could lose the selection.
        // Rows start under the frozen header, so row i is in view from i × rowHeight.
        if (lastRowHeight > 0f && rowHeight != lastRowHeight && ImGui.GetScrollY() is var scrollY and > 0f)
        {
            ImGui.SetScrollY(scrollY / lastRowHeight * rowHeight);
        }

        lastRowHeight = rowHeight;

        ApplySortSpecs();
        ScrollToExternalSelection(rows, rowHeight);

        if (!clipperCreated)
        {
            clipper = ImGui.ImGuiListClipper();
            clipperCreated = true;
        }

        // Rows can be taller than the text when icons are scaled up; the selectable fills the row, and the name and the
        // other cells centre themselves in it.
        // Dense keeps the zebra so long lists stay easy to track across columns; Comfortable trades it for a faint
        // separator under each row (ui-revamp §2.4), which with the hover fill keeps the eye on the row. Raw pushes in a
        // struct scope: nothing allocates per frame.
        var dense = UiMetrics.Density == RowDensity.Dense;
        ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0f, 0.5f));
        ImGui.PushStyleColor(ImGuiCol.TableRowBgAlt, dense ? Theme.ZebraRow : Vector4.Zero);
        using var rowStyle = new Theme.StyleScope(1, 1);
        var layout = new RowLayout(lineHeight, content, rowHeight, padY, glyphBox, glyphRadius, dense, plan.TwoLine, statusLine, lineGap);
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

    /// <summary>
    /// Per-frame row measurements, computed once per draw. In two-line rows <paramref name="StatusLine"/> is the second
    /// line's height (the caption role) and <paramref name="LineGap"/> the space between the lines.
    /// </summary>
    private readonly record struct RowLayout(float LineHeight, float RowContent, float RowHeight, float PadY, float GlyphBox, float GlyphRadius, bool Dense, bool TwoLine, float StatusLine, float LineGap)
    {
        /// <summary>Offset that centres a text line in the row.</summary>
        public float TextOffset => MathF.Max(0f, (RowContent - LineHeight) * 0.5f);

        /// <summary>Offset of a two-line row's first line: the two lines centred in the row together.</summary>
        public float FirstLineOffset => MathF.Max(0f, (RowContent - LineHeight - LineGap - StatusLine) * 0.5f);

        /// <summary>Offset of the name's line: the first of a two-line row, else the row's middle.</summary>
        public float NameOffset => TwoLine ? FirstLineOffset : TextOffset;

        /// <summary>Offset of a two-line row's second line.</summary>
        public float SecondLineOffset => FirstLineOffset + LineHeight + LineGap;
    }

    /// <summary>
    /// Plans the columns for <paramref name="available"/> pixels (<see cref="TableGeometry.PlanQuestTable"/>) with the
    /// player's hidden columns, keeping this plan's visibility for the next one's hysteresis, and marks the columns the
    /// plan hides (Disabled at the next setup).
    /// </summary>
    private void PlanColumns(float available, in QuestTableWidths widths)
    {
        plan = TableGeometry.PlanQuestTable(available, widths, playerHidden, plan, planned ? lastVisible : [], planSpecs, planVisible, planWidths);
        planned = true;
        Array.Copy(planVisible, lastVisible, ColumnCount);
        for (var i = 0; i < ColumnCount; i++)
        {
            autoHidden[i] = !planVisible[i] && !playerHidden[i];
        }
    }

    /// <summary>"Sorted by Level (…)" for the Name header's tooltip while the plan hides the sorted column; rebuilt only when the sort's column or the language changes.</summary>
    private string HiddenSortNote()
    {
        if (hiddenSortColumn != ui.Sort.Column || hiddenSortLanguage != Localization.Loc.Version || hiddenSortNote.Length == 0)
        {
            hiddenSortColumn = ui.Sort.Column;
            hiddenSortLanguage = Localization.Loc.Version;
            var column = ui.Sort.Column == SortColumn.Expansion ? Strings.ColumnExpansion : Strings.ColumnLevel;
            hiddenSortNote = string.Format(CultureInfo.CurrentCulture, Strings.TableSortHiddenFormat, column);
        }

        return hiddenSortNote;
    }

    /// <summary>The Disabled flag for a column the plan hides this frame.</summary>
    private ImGuiTableColumnFlags Planned(Column column) => autoHidden[(int)column] ? ImGuiTableColumnFlags.Disabled : ImGuiTableColumnFlags.None;

    /// <summary>A column's content width from the plan (its width less the cell overhead), or <paramref name="fallback"/> while it is hidden.</summary>
    private float PlannedContent(Column column, float fallback, float overhead) =>
        planVisible[(int)column] && planWidths[(int)column] > overhead ? planWidths[(int)column] - overhead : fallback;

    /// <summary>Whether the persisted sort is on a column the plan hides this frame (ImGui drops a hidden column's sort).</summary>
    private bool SortColumnAutoHidden() => ui.Sort.Column switch
    {
        SortColumn.Level => autoHidden[(int)Column.Level],
        SortColumn.Expansion => autoHidden[(int)Column.Expansion],
        _ => false,
    };

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
    /// to action, so never gold). <paramref name="nameNote"/>, when given, is a second line in the Name header's tooltip.
    /// </summary>
    private static void DrawHeaders(int sortedColumn, string? nameNote)
    {
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

            ImGui.PushID(i);
            ImGui.PushStyleColor(ImGuiCol.Text, i == sortedColumn ? s.Text : s.TextSecondary);
            ImGui.TableHeader(HeaderLabels[i]);
            ImGui.PopStyleColor();
            ImGui.PopID();
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(HeaderTooltips[i], i == (int)Column.Name ? nameNote : null);
            }
        }
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
        // paints neither hover nor selection over the fills above (keyboard focus still gets ImGui's nav frame). The
        // name itself is drawn below, ellipsised in the room the "…" button and the badges leave.
        ImGui.TableNextColumn();
        var nameCellMin = ImGui.GetCursorScreenPos();
        var nameCellWidth = ImGui.GetContentRegionAvail().X;
        var name = runner.Spoilers.DisplayName(quest);
        var nameInk = ImGui.GetColorU32(ImGuiCol.Text);
        ImGui.PushStyleColor(ImGuiCol.Header, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, Vector4.Zero);
        var clicked = ImGui.Selectable("##row", selected, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowDoubleClick | ImGuiSelectableFlags.AllowItemOverlap, new Vector2(0f, layout.RowContent));
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

        // Sized to the row's content, not its padded height: a button reaching into the cell padding would push the
        // cell's CursorMaxPos down and make the hovered or focused row taller.
        var moreShown = mouseInRow || rowFocused || moreFocusedRow == quest.RowId;
        var moreSize = MathF.Min(layout.RowContent, UiMetrics.MinTarget);
        if (moreShown)
        {
            var moreMin = new Vector2(nameCellMin.X + nameCellWidth - moreSize, nameCellMin.Y + ((layout.RowContent - moreSize) * 0.5f));
            if (Keyboard.MoreButton("##more", RowMenuId, moreMin, moreSize))
            {
                SelectFromTable(quest.RowId);
            }

            if (ImGui.IsItemFocused())
            {
                moreFocusedNext = quest.RowId;
            }
        }

        var nameLine = DrawNameLine(quest, name, nameInk, nameCellMin, nameCellWidth, moreShown, moreSize, in layout);

        // Two-line rows: the status on the second line, from the name's left edge to the cell's end (short of the "…"
        // while it shows, since the button sits on both lines).
        var statusCut = false;
        var secondLineY = nameCellMin.Y + layout.SecondLineOffset;
        if (layout.TwoLine)
        {
            var gap = UiMetrics.Px(StoryBadgeGap);
            ImGui.SetCursorScreenPos(new Vector2(nameCellMin.X, secondLineY));
            using var caption = Typography.Caption();
            statusCut = DrawStatus(row.Status, row.State, hasSnapshot, nameCellWidth - (moreShown ? moreSize + gap : 0f));
        }

        // The selectable spans every column; the banner tooltip belongs to the name cell only, the stripe's to the
        // row's left edge, so the reward and job icons keep their own tooltips.
        var mouse = ImGui.GetMousePos();
        var mouseX = mouse.X;
        if (rowHovered)
        {
            if (mouseX >= rowMin.X && mouseX <= rowMin.X + MathF.Max(lead, StripeThickness()))
            {
                UiMetrics.Tooltip(StripePattern.Tooltip(state, quest.RepeatInterval));
            }
            else if (nameLine.Y > 0f && mouseX >= nameLine.X && mouseX <= nameLine.X + nameLine.Y && (!layout.TwoLine || mouse.Y < secondLineY))
            {
                UiMetrics.Tooltip(runner.StoryBadgeText(quest.RowId));
            }
            else if (statusCut && mouse.Y >= secondLineY && mouseX >= nameCellMin.X && mouseX <= nameCellMin.X + nameCellWidth)
            {
                // The state word is never cut; when the reason after it was ellipsised, the whole line is its tooltip.
                UiMetrics.Tooltip(row.Status);
            }
            else if (mouseX >= nameCellMin.X && mouseX <= nameCellMin.X + nameCellWidth)
            {
                DrawNameTooltip(quest, row.State);
            }
        }

        // The other cells, each only when its column shows (the plan hides columns by disabling them).
        if (ImGui.TableNextColumn())
        {
            DrawPill(runner.LevelText(quest.DisplayLevel), UiMetrics.Px(LevelPillMinWidth), UiMetrics.Px(PillPadX), s.TextSecondary, in layout);
        }

        if (ImGui.TableNextColumn())
        {
            DrawJob(runner.Job(quest), plan.JobIconOnly, rowHovered, mouseX, in layout);
        }

        if (ImGui.TableNextColumn())
        {
            CenterText(in layout);
            var statusCellMin = ImGui.GetCursorScreenPos();
            var statusCellWidth = ImGui.GetContentRegionAvail().X;
            var cut = DrawStatus(row.Status, row.State, hasSnapshot, statusCellWidth);

            // The state word is never cut; when the reason after it was ellipsised, the whole line is the cell's tooltip.
            if (cut && rowHovered && mouseX >= statusCellMin.X && mouseX <= statusCellMin.X + statusCellWidth)
            {
                UiMetrics.Tooltip(row.Status);
            }
        }

        if (ImGui.TableNextColumn())
        {
            DrawPill(runner.ExpansionShort(quest.Expansion), 0f, UiMetrics.Px(ExpansionPillPadX), s.TextTertiary, in layout);
        }

        if (ImGui.TableNextColumn())
        {
            DrawRewardIcons(quest, in layout);
        }

        ImGui.PopID();
    }

    /// <summary>
    /// The name's line in the name cell, draw list only so the row's selectable stays the hovered item: the name,
    /// ellipsised in the room the rest of the line leaves (its full name is in the hover card), then a story
    /// sidequest's book badge. The rest of the line is the "…" button while it shows (one-line rows: the name gives
    /// way to it rather than run under it) or, in two-line rows, the level pill at the right end, whose place the
    /// "…" takes while it shows so the name never moves. Returns the badge's left x and width (0 without a badge).
    /// </summary>
    private Vector2 DrawNameLine(QuestRecord quest, string name, uint ink, Vector2 cellMin, float cellWidth, bool moreShown, float moreSize, in RowLayout layout)
    {
        var dl = ImGui.GetWindowDrawList();
        var gap = UiMetrics.Px(StoryBadgeGap);
        var lineY = cellMin.Y + layout.NameOffset;
        var reserve = moreShown ? moreSize + gap : 0f;
        if (layout.TwoLine && !playerHidden[(int)Column.Level])
        {
            var level = runner.LevelText(quest.DisplayLevel);
            if (level.Length > 0)
            {
                var pill = PillSize(level, UiMetrics.Px(LevelPillMinWidth), UiMetrics.Px(PillPadX), layout.LineHeight);
                reserve = MathF.Max(reserve, pill.X + gap);
                if (!moreShown)
                {
                    var pillMin = new Vector2(cellMin.X + cellWidth - pill.X, lineY + MathF.Round((layout.LineHeight - pill.Y) * 0.5f));
                    PillAt(dl, pillMin, pill, level, Theme.Surface.TextSecondary);
                }
            }
        }

        var story = runner.Stories.Contains(quest.RowId);
        var badgeSize = Vector2.Zero;
        if (story)
        {
            ImGui.PushFont(UiBuilder.IconFont);
            badgeSize = ImGui.CalcTextSize(StoryBadgeIcon);
            ImGui.PopFont();
            reserve += badgeSize.X + gap;
        }

        var room = MathF.Max(0f, cellWidth - reserve);
        var nameWidth = ImGui.CalcTextSize(name).X;
        EllipsisAt(dl, new Vector2(cellMin.X, lineY), room, name, ink, nameWidth);
        if (!story)
        {
            return default;
        }

        // The book badge in Dusk just after the name, or after the ellipsis when the name was cut.
        var x = cellMin.X + MathF.Min(nameWidth, room) + gap;
        ImGui.PushFont(UiBuilder.IconFont);
        dl.AddText(new Vector2(x, lineY + (layout.LineHeight - badgeSize.Y) * 0.5f), Theme.U32(Theme.Surface.TextTertiary), StoryBadgeIcon);
        ImGui.PopFont();
        return new Vector2(x, badgeSize.X);
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
        var size = PillSize(text, minWidth, padX, layout.RowContent);
        size.X = MathF.Max(1f, MathF.Min(avail, size.X));
        ImGui.Dummy(new Vector2(size.X, layout.RowContent));
        var min = new Vector2(pos.X, pos.Y + MathF.Round((layout.RowContent - size.Y) * 0.5f));
        PillAt(ImGui.GetWindowDrawList(), min, size, text, ink);
    }

    /// <summary>A pill's size for <paramref name="text"/> in the caption role: 16 px tall (never over <paramref name="maxHeight"/>), padded, at least <paramref name="minWidth"/> wide.</summary>
    private static Vector2 PillSize(string text, float minWidth, float padX, float maxHeight)
    {
        using var caption = Typography.Caption();
        var textSize = ImGui.CalcTextSize(text);
        var height = MathF.Min(maxHeight, MathF.Max(UiMetrics.Px(PillHeight), textSize.Y + UiMetrics.Px(2f)));
        return new Vector2(MathF.Max(minWidth, textSize.X + padX * 2f), height);
    }

    /// <summary>A pill at <paramref name="min"/>: the palette's sunken fill, the text in the caption role in <paramref name="ink"/>.</summary>
    private static void PillAt(ImDrawListPtr dl, Vector2 min, Vector2 size, string text, Vector4 ink)
    {
        using var caption = Typography.Caption();
        Chrome.PillAt(dl, min, size, text, Theme.U32(Theme.Surface.Sunken), 0u, Theme.U32(ink));
    }

    /// <summary>
    /// The Job cell: a quest limited to one job shows the job's game icon (16 px at scale 1) before its abbreviation;
    /// groups and "Any" keep the icon's slot so the labels line up. Hovering it names the job, or the group. When the
    /// column plan narrows the column to its icon (<paramref name="iconOnly"/>, <see cref="QuestTablePlan.JobIconOnly"/>)
    /// a job shows its icon alone and a group its label, ellipsised in the cell.
    /// </summary>
    private void DrawJob(JobLabel job, bool iconOnly, bool rowHovered, float mouseX, in RowLayout layout)
    {
        var pos = ImGui.GetCursorScreenPos();
        var cellWidth = ImGui.GetContentRegionAvail().X;
        var icon = MathF.Min(UiMetrics.Icon(JobIconSide), layout.RowContent);
        var gap = UiMetrics.Px(JobIconGap);
        var textSize = ImGui.CalcTextSize(job.Short);
        var width = iconOnly ? MathF.Max(1f, cellWidth) : icon + gap + textSize.X;
        ImGui.Dummy(new Vector2(width, layout.RowContent));

        var dl = ImGui.GetWindowDrawList();
        var ink = Theme.U32(Theme.Surface.TextSecondary);
        if (job.IconId != 0)
        {
            var iconMin = new Vector2(pos.X, pos.Y + MathF.Round((layout.RowContent - icon) * 0.5f));
            GameIcon.DrawAt(dl, textures, job.IconId, iconMin, iconMin + new Vector2(icon, icon));
            if (!iconOnly)
            {
                dl.AddText(new Vector2(pos.X + icon + gap, pos.Y + layout.TextOffset), ink, job.Short);
            }
        }
        else if (iconOnly)
        {
            EllipsisAt(dl, new Vector2(pos.X, pos.Y + layout.TextOffset), cellWidth, job.Short, ink, textSize.X);
        }
        else
        {
            dl.AddText(new Vector2(pos.X + icon + gap, pos.Y + layout.TextOffset), ink, job.Short);
        }

        if (rowHovered && job.Name.Length > 0 && mouseX >= pos.X && mouseX <= pos.X + width)
        {
            UiMetrics.Tooltip(job.Name);
        }
    }

    /// <summary>
    /// Status text (P1): the state word first in the primary text colour, then the reason after the separator in the
    /// secondary tone (Mist: Dusk fails AA on a hovered row), spans only, no new strings. The state word is never cut
    /// short; a reason too long for the cell is ellipsised in the room the state word leaves. In browse mode (no
    /// snapshot) the whole line is in the tertiary tone. Returns whether the reason was cut. The rule is
    /// <see cref="Chrome.StatusText"/>'s, drawn with raw colour pushes so a row allocates nothing.
    /// </summary>
    private static bool DrawStatus(string text, QuestState state, bool hasSnapshot, float cellWidth)
    {
        var s = Theme.Surface;
        var split = TableGeometry.StateWordLength(text);
        var stateWord = text.AsSpan(0, split);
        ImGui.PushStyleColor(ImGuiCol.Text, hasSnapshot ? s.Text : s.TextTertiary);
        ImGui.TextUnformatted(stateWord);
        ImGui.PopStyleColor();
        if (split >= text.Length)
        {
            return false;
        }

        var reason = text.AsSpan(split);
        var reasonInk = !hasSnapshot ? s.TextTertiary : state is QuestState.Blocked or QuestState.Foreclosed ? Theme.EclipseText : s.TextSecondary;
        var room = TableGeometry.ReasonWidth(cellWidth, ImGui.CalcTextSize(stateWord).X);
        var reasonWidth = ImGui.CalcTextSize(reason).X;
        ImGui.SameLine(0f, 0f);
        if (!TableGeometry.ReasonNeedsEllipsis(reasonWidth, room))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, reasonInk);
            ImGui.TextUnformatted(reason);
            ImGui.PopStyleColor();
            return false;
        }

        var pos = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(room, ImGui.GetTextLineHeight()));
        EllipsisAt(ImGui.GetWindowDrawList(), pos, room, reason, ImGui.GetColorU32(reasonInk), reasonWidth);
        return true;
    }

    /// <summary>
    /// <paramref name="text"/> on <paramref name="dl"/> at <paramref name="pos"/> within <paramref name="width"/>, ending
    /// in an ellipsis when it is longer (<see cref="Chrome.EllipsisTextAt(ImDrawListPtr, Vector2, float, ReadOnlySpan{char}, uint, float)"/>
    /// with a raw colour push, so a row allocates nothing). Returns whether the text was cut.
    /// </summary>
    private static bool EllipsisAt(ImDrawListPtr dl, Vector2 pos, float width, ReadOnlySpan<char> text, uint color, float textWidth)
    {
        if (text.IsEmpty || !(width > 0f))
        {
            return !text.IsEmpty;
        }

        if (textWidth <= width + 0.5f)
        {
            dl.AddText(pos, color, text);
            return false;
        }

        var max = new Vector2(pos.X + width, pos.Y + ImGui.GetTextLineHeight());
        Vector2? size = new Vector2(textWidth, max.Y - pos.Y);
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGuiP.RenderTextEllipsis(dl, in pos, in max, max.X, max.X, text, in size);
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
        else if (quest.Icon != 0 && textures.TryGetFromGameIcon(new GameIconLookup(quest.Icon), out var bannerTex) && bannerTex.TryGetWrap(out var wrap, out _) && wrap.Width > 0 && wrap.Height > 0)
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

            GameIcon.Draw(textures, reward.Icon, iconSize);
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

        // Teleport, Walk and Go to giver; disabled, with the reason on hover (naming Lifestream or vnavmesh when missing).
        TravelControls.MenuItems(links, quest, Strings.TeleportToGiver);

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

        // ImGui clears the sort of a column that is disabled and reports it as no specs: while the column plan holds
        // the sorted column hidden that is its doing, not a header click, and the persisted sort stands.
        if (specs.SpecsCount == 0 && SortColumnAutoHidden())
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
