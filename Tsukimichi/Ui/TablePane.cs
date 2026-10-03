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
///
/// Column widths (feature plan v6 U9): every automatic width is measured from the current font, so nothing is cut at
/// any UI scale or text size. The player can drag any column edge but the glyph's: the two columns beside the edge
/// trade width (beside the name, the name gives or takes it), so nothing else on screen moves. A column the player
/// sized keeps that width (saved in the configuration, never under <see cref="TableGeometry.PlayerColumnFloor"/>) and
/// the plan hides it in the usual order; what no longer fits in it ends in an ellipsis or "+N" with the whole on
/// hover. "Reset column widths" in the header's menu, ImGui's "Size all columns to default", "Size column to fit" and a
/// double-click on an edge give columns back to the automatic width.
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

    /// <summary>The faint gold wash on the row of the quest Questionable works on (feature plan v5, 1.6.0).</summary>
    private const float QuestionableWashAlpha = 0.12f;

    // The quest Questionable works on this frame, read once before the rows.
    private uint? questionableRow;

    /// <summary>The quest Questionable works on while it runs (its live status), or null; its row gets a faint gold wash.</summary>
    public Func<uint?>? QuestionableRow { get; set; }

    /// <summary>The catalog the Moon Road title names the tree node from (R3 #6); null names only the virtual nodes.</summary>
    public Func<QuestCatalog?>? Catalog { get; set; }

    /// <summary>Settings › Display › Planning: whether the EXP column (1.9.0, R6 G) is offered; null or false keeps it off.</summary>
    public Func<bool>? ShowExp { get; set; }

    /// <summary>Settings › Display › Planning: whether the Opens column (feature plan v6 K4) is offered; null or false keeps it off.</summary>
    public Func<bool>? ShowOpens { get; set; }

    /// <summary>How many kind icons the Opens column shows per quest.</summary>
    private const int MaxOpensIcons = 3;

    /// <summary>The brass line under the Moon Road header (proposal §7.3: Gilt at 0.5; opaque under high contrast).</summary>
    private const float HeaderRuleAlpha = 0.5f;

    /// <summary>The road under a Ready row (proposal §7.3), brass fading out to the right; faint, so the gold moon keeps the signal.</summary>
    private const float ReadyRoadAlpha = 0.4f;

    /// <summary>Full's star field over the title band, shown between the title and the count: seeded once, so it never reshuffles.</summary>
    private static readonly SkyField TitleStars = new(97, 48);

    /// <summary>The Ready road's glint at Full: one run every 9 s, taking the last 18 % of the cycle.</summary>
    private const double GlintCycleSeconds = 9.0;

    private const float GlintShare = 0.18f;

    /// <summary>Logical gaps on the title line: before the count, and after the breadcrumb.</summary>
    private const float TitleCountGap = 12f;
    private const float TitleCrumbGap = 6f;

    // The title line (R3 #6): the node's name and its parent as a breadcrumb, named again when the scope, the catalog
    // or the language changes; the count, rebuilt when the numbers change. Nothing is built per frame otherwise.
    private QuestScope titleScope = QuestScope.None;
    private QuestCatalog? titleCatalog;
    private int titleLanguage = -1;
    private string titleName = string.Empty;
    private string titleCrumb = string.Empty;
    private string titlePath = string.Empty;
    private int countShown = -1;
    private int countTotal = -1;
    private int countLanguage = -1;
    private string countText = string.Empty;
    private const float SelectionRingAlpha = 0.45f;
    private const float SelectionRounding = 4f;

    /// <summary>Row separators in Comfortable (zebra off): the line colour at this alpha.</summary>
    private const float SeparatorAlpha = 0.5f;

    /// <summary>High bits of the <see cref="Motion"/> key of a row's hover fade, so row ids never meet ImGui ids.</summary>
    private const ulong HoverKeyTag = 0x7461_6200_0000_0000UL;

    /// <summary>High half of the <see cref="Motion"/> key of a row's reveal pulse.</summary>
    private const uint RevealTag = 0x5441_5250; // "TARP"

    /// <summary>High half of the <see cref="Motion"/> key of a row's selection settling (1.13, U8).</summary>
    private const uint SelectTag = 0x5441_5253; // "TARS"

    /// <summary>The row wash a completion or a quest becoming Ready plays at Quiet and Plain flair (M1), at its start.</summary>
    private const float MomentWashAlpha = 0.18f;

    /// <summary>How long a reveal waits for its row to be drawn (the query and the scroll land a frame or two later).</summary>
    private const double RevealWaitSeconds = 2.0;

    private static readonly string StoryBadgeIcon = FontAwesomeIcon.BookOpen.ToIconString();

    /// <summary>The row's context menu, opened by a right-click, the Menu key or Shift+F10, or the "…" button.</summary>
    private const string RowMenuId = "##ctx";

    /// <summary>The widest level a pill is sized for when the Level column is first laid out.</summary>
    private const string WidestLevel = "100";

    /// <summary>The widest EXP the column is sized for: a Quest Sync range at Dawntrail's levels, each end this wide.</summary>
    private const int WidestExp = 888_888;

    /// <summary>How many expansions have a short label (<see cref="Strings.ExpansionShort"/>), all measured for the Expansion column.</summary>
    private const int ExpansionCount = 6;

    /// <summary>The id of ImGui's own header menu under the table's id (imgui_tables.cpp, TableOpenContextMenu).</summary>
    private const string HeaderMenuId = "##ContextMenu";

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
        Strings.ColumnExp,
        Strings.ColumnOpens,
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
        Strings.ColumnExpTooltip,
        Strings.ColumnOpensTooltip,
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

    // The last row before the free-trial view's "Beyond your trial" group (1.9.0); null when there is none.
    private uint? trialGroupEnd;
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

    // Column widths the player dragged (feature plan v6 U9), in QuestColumn order (0: automatic): logical content pixels
    // as saved, and the pixels the plan takes. The content widths written into the table's columns last frame (a drag is
    // what ImGui changed since; zero before the first frame) and each column's automatic content width this frame. The
    // drag's scratch arrays. All arrays, so nothing allocates per frame.
    private readonly float[] playerLogical = new float[ColumnCount];
    private readonly float[] playerPixels = new float[ColumnCount];
    private readonly float[] written = new float[ColumnCount];
    private readonly float[] autoContent = new float[ColumnCount];
    private readonly float[] dragWidths = new float[ColumnCount];
    private readonly float[] dragFloors = new float[ColumnCount];
    private readonly bool[] dragChanged = new bool[ColumnCount];
    private bool hasWritten;

    // The configuration's dictionary the player widths were read from: another one (a reload) is read again.
    private System.Collections.Generic.Dictionary<string, float>? widthsSource;

    /// <summary>The saved key of each column's width (<see cref="Config.Configuration.JournalColumnWidths"/>), in <see cref="Column"/> order.</summary>
    private static readonly string[] ColumnKeys = Enum.GetNames<Column>();

    /// <summary>
    /// The configuration's saved column widths (feature plan v6 U9), which the table reads and writes in place; null
    /// leaves every column automatic and keeps a drag for this session only.
    /// </summary>
    public Func<System.Collections.Generic.Dictionary<string, float>?>? ColumnWidths { get; set; }

    /// <summary>Called after a drag or a reset changed the saved column widths, so the configuration is saved.</summary>
    public Action? ColumnWidthsChanged { get; set; }

    // The content widths measured from the current font (feature plan v6 U9): the widest job label, expansion pill and
    // EXP text, measured again when the font size, the language or the job labels change.
    private float measuredFont = -1f;
    private int measuredLanguage = -1;
    private System.Collections.Generic.IReadOnlyList<string>? measuredJobLabels;
    private float widestJobLabel;
    private float widestExpansionPill;
    private float widestExpText;

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

        SelectedRowRect = default;
        var rows = runner.Rows;
        var scopeChanged = false;
        if (!ReferenceEquals(rows, indexedRows))
        {
            // A new result set (filter, search, scope, sort or a live update): index it once, and note whether the tree
            // scope moved since the last one (the rows of a new scope arrive the frame after the click that set it).
            IndexRows(rows);
            scopeChanged = ui.Scope != rowsScope;
            rowsScope = ui.Scope;
        }

        var selectedIndex = ui.SelectedRowId is { } selectedId && rowIndex.TryGetValue(selectedId, out var listed) ? listed : -1;

        // The fixed band (feature plan v6 U2): the title and the chip lane, at every flair and even over an empty
        // result, so the list's top edge never moves.
        DrawJournalHeader(rows.Length, selectedIndex);

        if (runner.Empty is { } empty)
        {
            hoveredRow = null;
            DrawEmpty(empty);
            return;
        }

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

        // The header at the Decoration level (docs/design/flair-v13 §1, "Table header"): Full's Moon Road (tracked caps
        // over a brass rule), Quiet's body type over a hairline, Plain's raised band with column dividers. The quick
        // views' captions are in the chip lane now.
        var headerStyle = Theme.TableHeader;
        var moonRoad = headerStyle == TableHeaderStyle.MoonRoad;
        newGroupEnd = runner.NewThisPatch > 0 && runner.NewThisPatch < rows.Length ? rows[runner.NewThisPatch - 1].Quest.RowId : null;
        trialGroupEnd = runner.BeyondTrial > 0 && runner.BeyondTrial < rows.Length ? rows[rows.Length - runner.BeyondTrial - 1].Quest.RowId : null;

        // The selected row keeps its place on screen when the rows change (feature plan v6 U3). The scroll is written
        // into the table's window before it begins, so the frame the new rows show is already in place (ImGui's own
        // SetScrollY lands a frame late, which would flash the old offset); it is set again inside to stick.
        var jump = AnchorScroll(rows, selectedIndex, scopeChanged, PlayerScrolling());
        if (jump is { } jumpY && tableWindowId != 0 && ImGuiP.FindWindowByID(tableWindowId) is { IsNull: false } tableWindow)
        {
            tableWindow.Scroll.Y = jumpY;
        }

        // SortTristate lets the header cycle back to "no sort" (journal order) and stops ImGui from picking the first
        // sortable column (the glyph) as an implicit default on the first frame.
        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.Sortable | ImGuiTableFlags.SortTristate | ImGuiTableFlags.ScrollY
            | ImGuiTableFlags.Resizable | ImGuiTableFlags.Reorderable | ImGuiTableFlags.Hideable | ImGuiTableFlags.SizingFixedFit;

        // The header's fill is painted when its row ends, which is when the clipper begins, so the push spans the table.
        // The line under the header is the table's strong border, read when the table begins: in Moon Road it is brass.
        ImGui.PushStyleColor(ImGuiCol.TableHeaderBg, headerStyle == TableHeaderStyle.Raised ? Theme.Tones.HeaderBand : Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.TableBorderStrong, headerStyle switch
        {
            TableHeaderStyle.MoonRoad => Theme.Surface.Ornament with { W = Theme.OrnamentAlpha(HeaderRuleAlpha) },
            TableHeaderStyle.Raised => Theme.Glyphs.HighContrast ? Theme.Surface.StrongLine : Theme.Tones.HeaderLine,
            _ => Theme.RuleColor,
        });

        using var headerBg = new Theme.StyleScope(2, 0);
        using var table = ImRaii.Table(TableId, ColumnCount, flags, ImGui.GetContentRegionAvail());
        if (!table)
        {
            hoveredRow = null;
            return;
        }

        // ScrollY gives the table its own inner window, so this is the table's rectangle; that window sits inside the
        // centre column (own font scale 1), so it scales itself before anything is measured.
        ui.RecordWindow(UiRects.Table);
        tableWindowId = ImGuiP.GetCurrentWindow().ID;
        if (jump is { } stickY)
        {
            ImGui.SetScrollY(stickY);
        }

        UiMetrics.ApplyFontScale();
        var style = ImGui.GetStyle();
        var lineHeight = ImGui.GetTextLineHeight();
        var padY = style.CellPadding.Y;
        var rowContent = UiMetrics.TableRowContentHeight(lineHeight, padY);
        // The medal, and with it the Glyph column, follows the one-line row height and the level's row tier (18 px at
        // Full, 16 at Quiet, 12 at Plain at scale 1).
        var glyphRadius = TableGeometry.GlyphRadius(rowContent, UiMetrics.TableGlyphRadius);
        var glyphBox = glyphRadius * TableGeometry.GlyphBoxPerRadius;
        // Beside the row-size medal, its badge's content at text height (the row fallback, feature plan v6 G1).
        var glyphColumn = UiMetrics.Px(GlyphColumnLead) + glyphBox + RowBadgeSide(lineHeight, rowContent) + UiMetrics.Px(2f);
        // A translated header is never cut (V2-19, LayoutBudgets): each fixed column is at least its header label wide,
        // with, where the column sorts, the arrow. These are content widths: ImGui adds the cell padding either side of
        // a TableSetupColumn / TableSetColumnWidth width itself, so it is not added here.
        var sortArrow = UiMetrics.Px(LayoutBudgets.SortArrowLogical);
        float HeaderFloor(string label, bool sortable)
        {
            var width = ImGui.CalcTextSize(label).X;
            var text = HeaderText(label, moonRoad);
            using (var role = HeaderRole(text, moonRoad))
            {
                width = MathF.Max(width, Chrome.TrackedTextWidth(text, Typography.HeaderTracking(in role)));
            }

            return width + (sortable ? sortArrow : 0f);
        }

        // Every content width is measured from the current font (feature plan v6 U9: a larger UI scale or text size widens
        // the columns rather than cutting them) and rounded up to a whole pixel, since ImGui lays columns out in whole
        // pixels and would clip a fraction of the last icon or letter.
        MeasureContent();
        var rewardsColumn = MathF.Ceiling(MathF.Max(
            UiMetrics.RowIconSize * MaxRewardIcons + UiMetrics.Px(2f) * (MaxRewardIcons - 1) + UiMetrics.Px(8f),
            HeaderFloor(Strings.ColumnRewards, sortable: false)));
        // The level pill as drawn (the caption role, "100" padded), or its budget or header when wider.
        var levelColumn = MathF.Ceiling(MathF.Max(
            MathF.Max(UiMetrics.Px(LayoutBudgets.LevelColumnLogical), PillSize(WidestLevel, UiMetrics.Px(LevelPillMinWidth), UiMetrics.Px(PillPadX), rowContent).X),
            HeaderFloor(Strings.ColumnLevel, sortable: true)));
        var expansionColumn = MathF.Ceiling(MathF.Max(MathF.Max(UiMetrics.Px(LayoutBudgets.ExpansionColumnLogical), widestExpansionPill), HeaderFloor(Strings.ColumnExpansion, sortable: true)));
        // The job column: icon, gap and the widest label it can show (never under its 76 px budget); narrowed, its icon
        // alone (and its header).
        var jobIcon = MathF.Min(UiMetrics.Icon(JobIconSide), rowContent);
        var jobHeader = HeaderFloor(Strings.ColumnJob, sortable: false);
        var jobColumn = MathF.Ceiling(MathF.Max(MathF.Max(UiMetrics.Px(LayoutBudgets.JobColumnLogical), jobIcon + UiMetrics.Px(JobIconGap) + widestJobLabel), jobHeader));
        var jobIconColumn = MathF.Ceiling(MathF.Max(jobIcon, jobHeader));

        // The column plan, from the table's width (its window less the scrollbar when the rows overflow). ImGui adds the
        // cell padding either side of a column's content width, and the border between columns.
        var overhead = style.CellPadding.X * 2f + 1f;
        var available = ImGui.GetWindowWidth() - (ImGui.GetScrollMaxY() > 0f ? style.ScrollbarSize : 0f);
        // The EXP column (off by default): as wide as a Quest Sync range or its header; no room at all while Settings leaves it off.
        var showExp = ShowExp?.Invoke() == true;
        var expColumn = showExp ? MathF.Ceiling(MathF.Max(widestExpText, HeaderFloor(Strings.ColumnExp, sortable: false))) : 0f;
        // The Unlocks column (on by default since 1.12.1, feature plan v6 K4): three kind icons or its header.
        var showOpens = ShowOpens?.Invoke() == true && runner.Unlocks is not null;
        var opensColumn = showOpens
            ? MathF.Ceiling(MathF.Max(UiMetrics.RowIconSize * MaxOpensIcons + UiMetrics.Px(2f) * (MaxOpensIcons - 1), HeaderFloor(Strings.ColumnOpens, sortable: false)))
            : 0f;
        var widths = new QuestTableWidths(MathF.Ceiling(glyphColumn), levelColumn, jobIconColumn, jobColumn, MathF.Ceiling(StateWordWidth()), expansionColumn, rewardsColumn, overhead, UiMetrics.Px(1f), expColumn, opensColumn, MathF.Max(UiMetrics.RowIconSize, jobIcon));
        autoContent[(int)Column.Level] = levelColumn;
        autoContent[(int)Column.Job] = jobColumn;
        autoContent[(int)Column.Status] = TableGeometry.StatusColumnMin(widths) - overhead;
        autoContent[(int)Column.Expansion] = expansionColumn;
        autoContent[(int)Column.Rewards] = rewardsColumn;
        autoContent[(int)Column.Exp] = expColumn;
        autoContent[(int)Column.Opens] = opensColumn;

        // A drag on a column's edge last frame becomes the player's width before the plan (feature plan v6 U9).
        var tableState = ImGuiP.GetCurrentTable();
        ReadPlayerWidths(tableState, in widths);
        var sortedWasHidden = SortColumnAutoHidden();
        PlanColumns(available, in widths);

        // Name is the one ImGui stretch column: it takes what the fixed columns leave. Every other column is fixed, at the
        // width the plan gives it, written into the column each frame (WriteColumnWidths); the player can drag any edge
        // but the glyph's (ReadPlayerWidths keeps the drag). A column the plan hides is Disabled, which ImGui neither saves
        // nor lists in the header menu.
        const ImGuiTableColumnFlags fixedFlags = ImGuiTableColumnFlags.WidthFixed;
        ImGui.TableSetupColumn(Strings.ColumnGlyph, fixedFlags | ImGuiTableColumnFlags.NoResize | ImGuiTableColumnFlags.NoHide | ImGuiTableColumnFlags.NoHeaderLabel, widths.Glyph);
        ImGui.TableSetupColumn(Strings.ColumnName, ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.NoHide, LayoutBudgets.TableNameWeight);
        ImGui.TableSetupColumn(Strings.ColumnLevel, fixedFlags | Planned(Column.Level), PlannedContent(Column.Level, levelColumn, overhead));
        ImGui.TableSetupColumn(Strings.ColumnJob, fixedFlags | ImGuiTableColumnFlags.NoSort | Planned(Column.Job), PlannedContent(Column.Job, jobColumn, overhead));
        ImGui.TableSetupColumn(Strings.ColumnStatus, fixedFlags | ImGuiTableColumnFlags.NoSort | Planned(Column.Status), PlannedContent(Column.Status, autoContent[(int)Column.Status], overhead));
        ImGui.TableSetupColumn(Strings.ColumnExpansion, fixedFlags | Planned(Column.Expansion), PlannedContent(Column.Expansion, expansionColumn, overhead));
        ImGui.TableSetupColumn(Strings.ColumnRewards, fixedFlags | ImGuiTableColumnFlags.NoSort | Planned(Column.Rewards), PlannedContent(Column.Rewards, rewardsColumn, overhead));
        ImGui.TableSetupColumn(Strings.ColumnExp, fixedFlags | ImGuiTableColumnFlags.NoSort | (showExp ? Planned(Column.Exp) : ImGuiTableColumnFlags.Disabled), MathF.Max(1f, PlannedContent(Column.Exp, expColumn, overhead)));
        ImGui.TableSetupColumn(Strings.ColumnOpens, fixedFlags | ImGuiTableColumnFlags.NoSort | (showOpens ? Planned(Column.Opens) : ImGuiTableColumnFlags.Disabled), MathF.Max(1f, PlannedContent(Column.Opens, opensColumn, overhead)));
        ImGui.TableSetupScrollFreeze(0, 1);
        WriteColumnWidths(tableState, overhead);

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

        // ImGui draws its header menu while laying the table out (in DrawHeaders), from the frame after the right-click.
        var headerMenuDrawn = !tableState.IsNull && tableState.IsContextPopupOpen;

        // While the plan hides the sorted column (its header and arrow are gone), the Name header's tooltip names the sort.
        var headerBottom = DrawHeaders(SortedColumn(ui.Sort), SortColumnAutoHidden() ? HiddenSortNote() : null, headerStyle);
        if (headerMenuDrawn)
        {
            DrawHeaderMenuExtras(tableState);
        }

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
        ScrollToExternalSelection(rows.Length, rowHeight);

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
        // Plain's ledger is always zebra-striped, with a faint line under each row as well.
        var dense = FlairRules.Zebra(Theme.Flair, UiMetrics.Density);
        ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0f, 0.5f));
        ImGui.PushStyleColor(ImGuiCol.TableRowBgAlt, !dense ? Vector4.Zero : Theme.Flair == Flair.Plain ? Theme.Surface.Text with { W = 0.03f } : Theme.ZebraRow);
        using var rowStyle = new Theme.StyleScope(1, 1);
        var layout = new RowLayout(lineHeight, content, rowHeight, padY, glyphBox, glyphRadius, dense, plan.TwoLine, statusLine, lineGap, FlairRules.ReadyRoad(Theme.Flair));
        var liftRow = hoveredRow;
        questionableRow = QuestionableRow?.Invoke();
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

        // The anchor for the next change of rows: the selected row while it is on screen, else the first row on screen.
        // The rows' view starts under the frozen header, which the clip rectangle includes.
        var rowsView = ImGuiP.GetCurrentWindow().InnerClipRect;
        var rowsTop = MathF.Max(rowsView.Min.Y, headerBottom);
        var view = ScrollAnchor.RowsView(rowsView.Min.Y, rowsView.Max.Y, headerBottom, rowHeight);
        anchorRows = rows;
        anchorRowHeight = rowHeight;
        anchorView = view;
        hasAnchor = ScrollAnchor.TryCapture(rows, selectedIndex, ImGui.GetScrollY(), rowHeight, view, out anchor);
        if (!SelectedRowRect.IsEmpty)
        {
            // Only what is on screen counts for the floating layers (a row half scrolled under the header).
            var clip = new ScreenRect(new Vector2(rowsView.Min.X, rowsTop), rowsView.Max);
            SelectedRowRect = ScreenRect.Intersect(SelectedRowRect, clip) is { IsEmpty: false } seen ? seen : default;
        }
    }

    /// <summary>
    /// The selected row's rectangle on screen this frame (empty when it is not on screen, or another tab shows): the
    /// floating layers keep clear of it (<see cref="FloatingLayers"/>).
    /// </summary>
    public ScreenRect SelectedRowRect { get; private set; }

    /// <summary>The chip lane's owner (feature plan v6 U2); the band keeps its height without it.</summary>
    public FilterPanel? Lane { get; set; }

    // The row index of the current result set (row id → index), rebuilt only when the runner hands out new rows, so the
    // selection's place is a lookup rather than a scan every frame.
    private readonly System.Collections.Generic.Dictionary<uint, int> rowIndex = [];
    private QuestRow[]? indexedRows;
    private QuestScope rowsScope = QuestScope.None;

    // The scroll anchor (feature plan v6 U3): the rows it was taken from, the row and where it sat, the row height and
    // the rows' view height it was measured at, and the table's window, whose scroll a change of rows sets directly.
    private QuestRow[]? anchorRows;
    private RowAnchor anchor;
    private bool hasAnchor;
    private float anchorRowHeight;
    private float anchorView;
    private uint tableWindowId;

    // A selection made elsewhere that the rows did not list yet (a reveal whose query lands a frame later).
    private uint? pendingReveal;

    private void IndexRows(QuestRow[] rows)
    {
        indexedRows = rows;
        rowIndex.Clear();
        rowIndex.EnsureCapacity(rows.Length);
        for (var i = 0; i < rows.Length; i++)
        {
            rowIndex.TryAdd(rows[i].Quest.RowId, i);
        }
    }

    /// <summary>
    /// The scroll for a frame whose rows changed (feature plan v6 U3), or null to leave it: a selection made elsewhere
    /// that just became listed goes 40 % down the view; a new tree scope shows its selection there too, else its top;
    /// any other change keeps the anchored row where it was on screen (the selected row's neighbour when it was
    /// filtered out), and the selected row, moved in the list but kept in place, gets the reveal pulse.
    /// </summary>
    /// <summary>
    /// Whether the player is scrolling the list this frame: the wheel turns, the table's scrollbar is held, a wheel or
    /// keyboard scroll is about to land (<c>ScrollTarget</c>), or <see cref="Motion"/> still counts a recent scroll.
    /// </summary>
    private bool PlayerScrolling()
    {
        if (ImGui.GetIO().MouseWheel != 0f || Motion.Scrolling)
        {
            return true;
        }

        if (tableWindowId == 0 || ImGuiP.FindWindowByID(tableWindowId) is not { IsNull: false } window)
        {
            return false;
        }

        var active = ImGuiP.GetActiveID();
        return window.ScrollTarget.Y < float.MaxValue || (active != 0 && active == ImGuiP.GetWindowScrollbarID(window, ImGuiAxis.Y));
    }

    private float? AnchorScroll(QuestRow[] rows, int selectedIndex, bool scopeChanged, bool playerScrolling)
    {
        if (anchorRows is not { } old || ReferenceEquals(rows, old) || !(anchorRowHeight > 0f))
        {
            return null;
        }

        if (pendingReveal is { } pending && pending == ui.SelectedRowId && selectedIndex >= 0)
        {
            pendingReveal = null;
            lastSelection = pending;
            return ScrollAnchor.Reveal(selectedIndex, rows.Length, anchorRowHeight, anchorView);
        }

        if (scopeChanged)
        {
            return selectedIndex >= 0 ? ScrollAnchor.Reveal(selectedIndex, rows.Length, anchorRowHeight, anchorView) : 0f;
        }

        // The player is scrolling the list (the wheel, the scrollbar, a scroll landing): their scroll wins, and the rows
        // that changed under it are not pulled back to where the anchor sat. A reveal or a new scope above still jumps,
        // as both are the player's own asks.
        if (!hasAnchor || playerScrolling)
        {
            return null;
        }

        var y = ScrollAnchor.Restore(in anchor, old, rowIndex, rows.Length, anchorRowHeight, anchorView, out var landed);
        if (anchor.Selected && landed >= 0 && landed != anchor.Index)
        {
            Motion.Trigger(Motion.Key(RevealTag, anchor.RowId));
        }

        return y;
    }

    /// <summary>
    /// The Journal's fixed band over the list (feature plan v6 U2): the title line (the scope's name with an × to clear
    /// it, and the count) and the one-line chip lane, which names a selected quest the list does not show (U3).
    /// </summary>
    private void DrawJournalHeader(int shown, int selectedIndex)
    {
        DrawTitle(shown);
        var hidden = selectedIndex < 0 && ui.SelectedRowId is { } id ? Catalog?.Invoke()?.GetByRowId(id) : null;
        var caption = runner.SproutCaption ?? runner.NewThisPatchCaption ?? runner.BeyondTrialCaption;
        if (Lane is { } lane)
        {
            lane.DrawLane(Chrome.ChipHeightPx(), hidden, caption);
        }
        else
        {
            ImGui.Dummy(new Vector2(1f, Chrome.ChipHeightPx()));
        }
    }

    /// <summary>
    /// Per-frame row measurements, computed once per draw. In two-line rows <paramref name="StatusLine"/> is the second
    /// line's height (the caption role) and <paramref name="LineGap"/> the space between the lines.
    /// <paramref name="ReadyRoad"/> draws the road under Ready rows (Flair Full).
    /// </summary>
    /// <summary>
    /// The square beside a row's medal that holds its badge's content: text height, inside the row. None under the
    /// Classic moon style (<see cref="Theme.ClassicMoons"/>), whose 1.11 moons have no badges.
    /// </summary>
    private static float RowBadgeSide(float lineHeight, float rowContent) =>
        Theme.ClassicMoons || !MedalGlyph.RowBadges ? 0f : MathF.Round(MathF.Min(lineHeight, rowContent));

    private readonly record struct RowLayout(float LineHeight, float RowContent, float RowHeight, float PadY, float GlyphBox, float GlyphRadius, bool Dense, bool TwoLine, float StatusLine, float LineGap, bool ReadyRoad)
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
    /// player's hidden columns and column widths, keeping this plan's visibility for the next one's hysteresis, and marks the columns the
    /// plan hides (Disabled at the next setup).
    /// </summary>
    private void PlanColumns(float available, in QuestTableWidths widths)
    {
        plan = TableGeometry.PlanQuestTable(available, widths, playerHidden, playerPixels, plan, planned ? lastVisible : [], planSpecs, planVisible, planWidths);
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

    /// <summary>
    /// The widest job label, expansion pill and EXP text in the current font and language (feature plan v6 U9), measured
    /// again only when the font size, the language or the job labels change.
    /// </summary>
    private void MeasureContent()
    {
        var font = ImGui.GetFontSize();
        var labels = runner.JobLabels;
        if (measuredFont == font && measuredLanguage == Localization.Loc.Version && ReferenceEquals(measuredJobLabels, labels))
        {
            return;
        }

        measuredFont = font;
        measuredLanguage = Localization.Loc.Version;
        measuredJobLabels = labels;
        widestJobLabel = 0f;
        foreach (var label in labels)
        {
            widestJobLabel = MathF.Max(widestJobLabel, ImGui.CalcTextSize(label).X);
        }

        widestExpansionPill = 0f;
        for (byte expansion = 0; expansion < ExpansionCount; expansion++)
        {
            widestExpansionPill = MathF.Max(widestExpansionPill, PillSize(runner.ExpansionShort(expansion), 0f, UiMetrics.Px(ExpansionPillPadX), float.MaxValue).X);
        }

        var widest = WidestExp.ToString("N0", CultureInfo.CurrentCulture);
        widestExpText = ImGui.CalcTextSize(string.Format(CultureInfo.CurrentCulture, Strings.PlanningExpRangeShortFormat, widest, widest)).X;
    }

    /// <summary>
    /// The player's column widths (feature plan v6 U9), read from the configuration when it is another dictionary than
    /// last frame, then any drag on a column edge since last frame. ImGui applies a drag to the columns' requested widths
    /// before the table is set up; whatever moved since <see cref="WriteColumnWidths"/> wrote them is the player's:
    /// <list type="bullet">
    /// <item>a drag (the table's last resized column is set) keeps the widths it left, within
    /// <see cref="TableGeometry.ApplyColumnDrag"/>'s rules, so no other column moves and none is pushed out of the plan;</item>
    /// <item>ImGui's "Size column to fit" (or a double-click on an edge) and "Size all columns to default" give the
    /// columns they touched back to the automatic width.</item>
    /// </list>
    /// Then the pixels the plan takes, at this frame's UI scale. A change saves the configuration.
    /// </summary>
    private unsafe void ReadPlayerWidths(ImGuiTablePtr table, in QuestTableWidths widths)
    {
        var source = ColumnWidths?.Invoke();
        if (!ReferenceEquals(source, widthsSource))
        {
            widthsSource = source;
            for (var i = 0; i < ColumnCount; i++)
            {
                playerLogical[i] = source is not null && source.TryGetValue(ColumnKeys[i], out var saved) ? TableGeometry.SanitizePlayerWidth(saved) : 0f;
            }

            playerLogical[(int)Column.Glyph] = 0f;
            playerLogical[(int)Column.Name] = 0f;
        }

        var changed = false;
        if (hasWritten && !table.IsNull && table.ColumnsCount >= ColumnCount)
        {
            var dragged = table.LastResizedColumn >= 0;
            for (var i = 0; i < ColumnCount; i++)
            {
                dragWidths[i] = written[i];
                dragFloors[i] = TableGeometry.PlayerColumnFloor((Column)i, widths);
                if (i is (int)Column.Glyph or (int)Column.Name)
                {
                    continue;
                }

                var column = new ImGuiTableColumnPtr(table.Columns.Data + i);
                if (column.AutoFitQueue != 0)
                {
                    // "Size all columns to default": this column goes back to automatic, with no frame at ImGui's fit.
                    column.AutoFitQueue = 0;
                    if (column.IsEnabled && playerLogical[i] > 0f)
                    {
                        playerLogical[i] = 0f;
                        changed = true;
                    }

                    continue;
                }

                if (column.WidthRequest > 0f)
                {
                    dragWidths[i] = column.WidthRequest;
                }
            }

            if (dragged)
            {
                // The name gives up at most what it has over its minimum, so the drag never hides a column.
                var name = (int)Column.Name;
                var slack = MathF.Max(0f, planWidths[name] - planSpecs[name].Min - 1f);
                if (TableGeometry.ApplyColumnDrag(written, dragWidths, dragFloors, slack, dragChanged))
                {
                    var scale = UiMetrics.Px(1f);
                    for (var i = (int)Column.Level; i < ColumnCount; i++)
                    {
                        if (dragChanged[i])
                        {
                            playerLogical[i] = TableGeometry.SanitizePlayerWidth(MathF.Round(dragWidths[i]) / scale);
                            changed = true;
                        }
                    }
                }
            }
            else
            {
                for (var i = (int)Column.Level; i < ColumnCount; i++)
                {
                    if (MathF.Abs(dragWidths[i] - written[i]) > 0.5f && playerLogical[i] > 0f)
                    {
                        playerLogical[i] = 0f;
                        changed = true;
                    }
                }
            }
        }

        var px = UiMetrics.Px(1f);
        for (var i = 0; i < ColumnCount; i++)
        {
            playerPixels[i] = MathF.Round(playerLogical[i] * px);
        }

        if (changed)
        {
            SavePlayerWidths();
        }
    }

    /// <summary>
    /// Writes each column's planned content width (its saved or automatic width while the plan hides it) into the table,
    /// in whole pixels, after the columns are set up and before the layout. ImGui keeps a resizable column at the width
    /// written, so the plan, not ImGui's own fit, decides the layout; what a drag changes is read back next frame.
    /// </summary>
    private unsafe void WriteColumnWidths(ImGuiTablePtr table, float overhead)
    {
        if (table.IsNull || table.ColumnsCount < ColumnCount)
        {
            return;
        }

        for (var i = (int)Column.Level; i < ColumnCount; i++)
        {
            var fallback = playerPixels[i] > 0f ? playerPixels[i] : autoContent[i];
            var width = MathF.Max(1f, MathF.Round(PlannedContent((Column)i, fallback, overhead)));
            new ImGuiTableColumnPtr(table.Columns.Data + i).WidthRequest = width;
            written[i] = width;
        }

        hasWritten = true;
    }

    /// <summary>Copies the player's widths into the configuration's dictionary (a column at 0 is removed) and asks for a save.</summary>
    private void SavePlayerWidths()
    {
        if (widthsSource is { } target)
        {
            for (var i = (int)Column.Level; i < ColumnCount; i++)
            {
                if (playerLogical[i] > 0f)
                {
                    target[ColumnKeys[i]] = playerLogical[i];
                }
                else
                {
                    target.Remove(ColumnKeys[i]);
                }
            }
        }

        ColumnWidthsChanged?.Invoke();
    }

    /// <summary>Whether the player sized any column.</summary>
    private bool AnyPlayerWidth()
    {
        for (var i = (int)Column.Level; i < ColumnCount; i++)
        {
            if (playerLogical[i] > 0f)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>"Reset column widths": every column goes back to its automatic width; next frame's plan and widths follow.</summary>
    private void ResetColumnWidths()
    {
        Array.Clear(playerLogical);
        Array.Clear(playerPixels);
        SavePlayerWidths();
    }

    /// <summary>
    /// "Reset column widths" at the end of the table header's right-click menu (feature plan v6 U9), under ImGui's own
    /// sizing, order and visibility items: the menu is ImGui's popup, which ImGui draws while laying the table out, so it
    /// is opened again here (by the same id, under the table's) and this item appended to it. Called only on a frame
    /// ImGui already drew the menu, so the item never shows alone.
    /// </summary>
    private void DrawHeaderMenuExtras(ImGuiTablePtr table)
    {
        if (table.IsNull || !table.IsContextPopupOpen)
        {
            return;
        }

        const ImGuiWindowFlags popupFlags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings;
        if (!ImGuiP.BeginPopupEx(ImGui.GetID(HeaderMenuId), popupFlags))
        {
            return;
        }

        ImGui.Separator();
        if (ImGui.MenuItem(Strings.TableResetColumnWidths, enabled: AnyPlayerWidth()))
        {
            ResetColumnWidths();
        }

        ImGui.EndPopup();
    }

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
    /// What TableHeadersRow does, one header at a time, so each can carry a tooltip. Labels are in the secondary tone;
    /// the sorted column's label and its arrow are in the primary text colour, the lighter gilt at Full (a sort is not
    /// a call to action, so never Moon gold). Quiet and Plain draw them at the body size (Plain on the raised band);
    /// Moon Road (R3 #6, plan v7 §5) in the Header role, upper-cased in English and tracked, on a clear header. The row
    /// is at least <see cref="ScaleMetrics.TableHeaderMin"/> tall. <paramref name="nameNote"/>, when given, is a second
    /// line in the Name header's tooltip. Returns the header row's bottom on screen (where the rows' view starts; the
    /// header is frozen, so it stays there however the rows scroll), or <see cref="float.MinValue"/> with no header drawn.
    /// </summary>
    private static float DrawHeaders(int sortedColumn, string? nameNote, TableHeaderStyle style)
    {
        var s = Theme.Surface;
        var moonRoad = style == TableHeaderStyle.MoonRoad;
        var bottom = float.MinValue;

        // The row is at least the level's header height (plan v7 §5: 30 / 28 / 22), with the labels in its middle: the
        // header row's own cell padding takes up the difference, so ImGui's top-aligned header cells centre them.
        float line;
        var nameLabel = HeaderText(HeaderLabels[(int)Column.Name], moonRoad);
        using (HeaderRole(nameLabel, moonRoad))
        {
            line = ImGui.GetTextLineHeight();
        }

        var cellPadding = ImGui.GetStyle().CellPadding;
        var (rowHeight, rowPadY) = ScaleMetrics.TableHeaderRow(UiMetrics.Px(ScaleMetrics.TableHeaderMin(Theme.Flair)), line, cellPadding.Y);
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(cellPadding.X, rowPadY));
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers, rowHeight);
        ImGui.PopStyleVar();
        var arrow = MathF.Floor((ImGui.GetFontSize() * 0.65f) + ImGui.GetStyle().FramePadding.X);
        var lastShown = -1;
        for (var i = 0; i < HeaderTooltips.Length; i++)
        {
            if (IsColumnEnabled((Column)i))
            {
                lastShown = i;
            }
        }

        for (var i = 0; i < HeaderTooltips.Length; i++)
        {
            if (!ImGui.TableSetColumnIndex(i))
            {
                continue;
            }

            var label = HeaderText(HeaderLabels[i], moonRoad);
            ImGui.PushID(i);

            // The sorted column's label in the primary tone (at Full the lighter gilt); the rest in the secondary tone.
            var ink = i == sortedColumn ? moonRoad ? s.OrnamentLight : s.Text : s.TextSecondary;

            // ImGui's header (its hover, click to sort and sort arrow, in the label's ink) with no label of its own; the
            // label is drawn over it in the header's role, tracked at Full, ending in an ellipsis before the arrow.
            var labelPos = ImGui.GetCursorScreenPos();
            ImGui.PushStyleColor(ImGuiCol.Text, ink);
            ImGui.TableHeader("##header");
            ImGui.PopStyleColor();
            var labelRight = ImGui.GetItemRectMax().X - (i == sortedColumn ? arrow : cellPadding.X);
            float labelWidth;
            using (var role = HeaderRole(label, moonRoad))
            {
                var tracking = Typography.HeaderTracking(in role);
                labelWidth = Chrome.TrackedTextWidth(label, tracking);
                var y = labelPos.Y + MathF.Round((line - ImGui.GetTextLineHeight()) * 0.5f);
                Chrome.TrackedTextAt(ImGui.GetWindowDrawList(), new Vector2(labelPos.X, y), MathF.Max(0f, labelRight - labelPos.X), label, Theme.U32(ink), tracking);
            }

            ImGui.PopID();
            bottom = MathF.Max(bottom, ImGui.GetItemRectMax().Y);
            if (style == TableHeaderStyle.Raised && i != lastShown)
            {
                // Plain: a 1 px divider at the cell's right edge, in the header band only.
                var cellMax = ImGui.GetItemRectMax();
                var x = MathF.Floor(cellMax.X) - 1f;
                ImGui.GetWindowDrawList().AddRectFilled(new Vector2(x, ImGui.GetItemRectMin().Y), new Vector2(x + 1f, cellMax.Y), Theme.U32(Theme.Glyphs.HighContrast ? s.StrongLine : Theme.Tones.HeaderLine));
            }
            if (ImGui.IsItemHovered())
            {
                // A column the player made narrower than its label ends the label in an ellipsis (ImGui's header does);
                // its tooltip then starts with the whole label.
                var cut = i != (int)Column.Glyph && labelWidth > ImGui.GetItemRectSize().X - ImGui.GetStyle().CellPadding.X * 2f + 0.5f;
                if (cut)
                {
                    UiMetrics.Tooltip(HeaderLabels[i], HeaderTooltips[i]);
                }
                else
                {
                    UiMetrics.Tooltip(HeaderTooltips[i], i == (int)Column.Name ? nameNote : null);
                }
            }
        }

        return bottom;
    }

    /// <summary>A header label as drawn: cased like a section heading in Moon Road (<see cref="SectionHeading.Label"/>, cached), else as is.</summary>
    private static string HeaderText(string label, bool moonRoad) => moonRoad ? SectionHeading.Label(label) : label;

    /// <summary>
    /// The header labels' role (plan v7 §5): in Moon Road the Header role (TrumpGothic at 1.55× the body, tracked; the
    /// body size where the game face cannot draw the label), else the body size: never smaller than the rows' text.
    /// </summary>
    private static Typography.Scope HeaderRole(string text, bool moonRoad) =>
        moonRoad ? Typography.Header(text) : default;

    /// <summary>
    /// The title line over the table (R3 #6, proposal §7.3): the node's parent as a breadcrumb in the caption role and
    /// the tertiary tone, the node's name in the Title role, and the count in the Numeral role at the right end ("160",
    /// or "160 of 213" while filters or the search narrow the list). The breadcrumb goes first, then the count, before
    /// the name drops under its minimum; the name ends in an ellipsis, and whatever is left out is named on hover.
    /// </summary>
    private void DrawTitle(int shown)
    {
        RefreshTitle(Catalog?.Invoke(), shown, runner.TotalInScope);
        var start = ImGui.GetCursorScreenPos();
        var room = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var dl = ImGui.GetWindowDrawList();

        // The title in the Title role at Full and Quiet (Jupiter at Full; the display role in the body font at Quiet), in
        // the body font at Plain, where the chip lane already says the scope (docs/design/flair-v13 §1, "Table header").
        var plainTitle = Theme.Flair == Flair.Plain;
        float titleLine;
        float titleWidth;
        using (plainTitle ? default : Typography.Title(titleName))
        {
            titleLine = ImGui.GetTextLineHeight();
            titleWidth = ImGui.CalcTextSize(titleName).X;
        }

        // The caption line is measured whether or not a breadcrumb shows, so the band's height is the same for every scope.
        float crumbLine;
        var crumbWidth = 0f;
        using (Typography.Caption())
        {
            crumbLine = ImGui.GetTextLineHeight();
            crumbWidth = titleCrumb.Length > 0 ? ImGui.CalcTextSize(titleCrumb).X : 0f;
        }

        float countLine;
        float countWidth;
        using (Typography.Numeral(countText))
        {
            countLine = ImGui.GetTextLineHeight();
            countWidth = ImGui.CalcTextSize(countText).X;
        }

        // A scope other than All quests ends its name with an × that clears it (feature plan v6 U2: the scope is no chip).
        var height = MathF.Max(titleLine, MathF.Max(countLine, crumbLine));
        var scoped = ui.Scope != QuestScope.None;
        var clearSide = scoped ? height : 0f;
        var crumbGap = UiMetrics.Px(TitleCrumbGap);
        Span<float> parts = stackalloc float[2];
        Span<bool> partShown = stackalloc bool[2];
        parts[0] = countWidth + UiMetrics.Px(TitleCountGap);
        parts[1] = crumbWidth > 0f ? crumbWidth + crumbGap : 0f;
        var fit = RowFit.Fit(MathF.Max(1f, room - clearSide), titleWidth, UiMetrics.Px(LayoutBudgets.RowNameMinLogical), parts, partShown);
        var countVisible = partShown[0];
        var crumbVisible = partShown[1] && crumbWidth > 0f;

        var midY = start.Y + (height * 0.5f);
        var x = start.X;
        if (crumbVisible)
        {
            using var caption = Typography.Caption();
            dl.AddText(new Vector2(x, MathF.Round(midY - (crumbLine * 0.5f))), Theme.U32(Theme.Surface.TextTertiary), titleCrumb);
            x += crumbWidth + crumbGap;
        }

        bool cut;
        using (plainTitle ? default : Typography.Title(titleName))
        {
            cut = Chrome.EllipsisTextAt(dl, new Vector2(x, MathF.Round(midY - (titleLine * 0.5f))), fit.NameRoom, titleName, Theme.U32(Theme.Surface.Text), titleWidth);
        }

        // Full: the count in gilt; Quiet and Plain the tertiary tone.
        var countX = start.X + room - countWidth;
        if (countVisible)
        {
            using var numeral = Typography.Numeral(countText);
            var countInk = Theme.MoonRoadArt ? Theme.Surface.OrnamentHigh : Theme.Flair == Flair.Full ? Theme.Surface.TextSecondary : Theme.Surface.TextTertiary;
            dl.AddText(new Vector2(countX, MathF.Round(midY - (countLine * 0.5f))), Theme.U32(countInk), countText);
        }

        if (Theme.ShowStars)
        {
            // Full: a few faint stars in the title band's empty sky, between the title and the count, never on either.
            var skyLeft = x + MathF.Min(titleWidth, fit.NameRoom) + clearSide + UiMetrics.Px(16f);
            var skyRight = (countVisible ? countX : start.X + room) - UiMetrics.Px(16f);
            var canvasMax = new Vector2(start.X + room, start.Y + height - UiMetrics.Px(2f));
            var size = (canvasMax - start) / UiMetrics.Scale;
            NightSky.Field(dl, SkySite.Title, 0, TitleStars.For(size.X, size.Y), start, canvasMax, new Vector2(skyLeft, start.Y), new Vector2(skyRight, canvasMax.Y));
        }

        if (scoped)
        {
            DrawScopeClear(new Vector2(x + MathF.Min(titleWidth, fit.NameRoom), start.Y), clearSide);
        }

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(room, height));
        if (!ImGui.IsItemHovered() || (scoped && ImGui.IsMouseHoveringRect(clearMin, clearMin + new Vector2(clearSide))))
        {
            return;
        }

        if (countVisible && ImGui.GetMousePos().X >= countX)
        {
            UiMetrics.Tooltip(Strings.TableTitleCountTooltip);
        }
        else if (cut || !countVisible || (!crumbVisible && titleCrumb.Length > 0))
        {
            UiMetrics.Tooltip(titlePath, countVisible ? null : countText);
        }
    }

    // Where the title's scope × was drawn this frame, so the title's own tooltip leaves it alone.
    private Vector2 clearMin;

    /// <summary>
    /// The × after the scope's name in the title (feature plan v6 U2): a square of <paramref name="side"/> at
    /// <paramref name="min"/> that sets the scope back to All quests, as the scope chip of 1.10 did.
    /// </summary>
    private void DrawScopeClear(Vector2 min, float side)
    {
        clearMin = min + new Vector2(UiMetrics.Px(2f), 0f);
        ImGui.SetCursorScreenPos(clearMin);
        if (ImGui.InvisibleButton("##titleScopeClear", new Vector2(side)))
        {
            // The scope is not a filter and is not persisted; the query re-runs on the dirty mark alone.
            ui.Scope = QuestScope.None;
            ui.MarkQueryDirty();
        }

        var hovered = ImGui.IsItemHovered();
        var s = Theme.Surface;
        var center = clearMin + new Vector2(side * 0.5f);
        var half = UiMetrics.Px(4f);
        var dl = ImGui.GetWindowDrawList();
        if (hovered)
        {
            dl.AddCircleFilled(center, side * 0.42f, Theme.U32(s.Hover));
        }

        var cross = Theme.U32(hovered ? s.Text : s.TextTertiary);
        var thickness = MathF.Max(1f, UiMetrics.Px(1.5f));
        dl.AddLine(center - new Vector2(half), center + new Vector2(half), cross, thickness);
        dl.AddLine(center + new Vector2(-half, half), center + new Vector2(half, -half), cross, thickness);
        Chrome.FocusRing(side * 0.5f);
        if (hovered)
        {
            UiMetrics.Tooltip(ui.Scope.Kind == ScopeKind.VirtualIssuer ? Strings.ChipIssuerTooltip : Strings.ScopeChipTooltip);
        }
    }

    /// <summary>Names the title's node and builds its count, each only when its inputs changed.</summary>
    private void RefreshTitle(QuestCatalog? catalog, int shown, int total)
    {
        var language = Localization.Loc.Version;
        if (ui.Scope != titleScope || !ReferenceEquals(catalog, titleCatalog) || language != titleLanguage || titleName.Length == 0)
        {
            titleScope = ui.Scope;
            titleCatalog = catalog;
            titleLanguage = language;
            var (parent, name) = ui.Scope.Kind == ScopeKind.None ? (string.Empty, Strings.AllQuests) : FilterPanel.ScopeParts(ui.Scope, catalog);
            var crumb = parent.Length > 0 && !string.Equals(parent, name, StringComparison.Ordinal);
            titleName = name;
            titleCrumb = crumb ? parent + Strings.TitleCrumbSuffix : string.Empty;
            titlePath = crumb ? FilterPanel.ScopePath(parent, name) : name;
        }

        if (shown != countShown || total != countTotal || language != countLanguage)
        {
            countShown = shown;
            countTotal = total;
            countLanguage = language;
            countText = total > shown
                ? string.Format(CultureInfo.CurrentCulture, Strings.TableTitleCountFormat, shown, total)
                : shown.ToString("N0", CultureInfo.CurrentCulture);
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
        // RowBg0 (it replaces the zebra), the hover fill over it in RowBg1, easing in and out through Motion (1.13, U8:
        // hover in 120 ms and out 180 ms; a new selection settles in over 150 ms, wash and ring together).
        var hover = Motion.Hover(HoverKeyTag | quest.RowId, liftRow == quest.RowId);
        var settled = Motion.Select(Motion.Key(SelectTag, quest.RowId), selected);
        if (settled > 0.004f)
        {
            // Full: a warm wash (drawn as a gradient in the row's chrome); Quiet a neutral 0.06 wash; Plain 0.09.
            var wash = Theme.Flair switch
            {
                Flair.Full => Theme.WithAlpha(Theme.Moon, 0.035f * settled),
                Flair.Quiet => Theme.WithAlpha(s.Text, 0.06f * settled),
                _ => Theme.WithAlpha(s.Text, 0.09f * settled),
            };
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, wash);
        }
        else if (questionableRow == quest.RowId)
        {
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, Theme.WithAlpha(Theme.Accent, QuestionableWashAlpha));
        }

        // The Moon Road moments (M1): a completion (the moon waxes) and a quest that just became Ready. At Full flair
        // the stripe flashes and a soft halo swells round the moon; at Quiet and Plain the row takes one soft gold wash.
        var completion = Motion.Completion(quest.RowId);
        var moment = completion >= 0f ? completion : Motion.ReadyHalo(quest.RowId);
        var moonRoadMoment = Theme.FlairMotion;
        if (moment >= 0f && !moonRoadMoment)
        {
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, Theme.WithAlpha(Theme.MoonHigh, MomentWashAlpha * (1f - MotionMath.EaseOutCubic(moment))));
        }
        else if (hover > 0.004f)
        {
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, Theme.WithAlpha(s.Hover, hover * s.Hover.W));
        }

        // Glyph column: pinned dot at the left edge, state moon centred in the rest.
        ImGui.TableNextColumn();
        var cell = ImGui.GetCursorScreenPos();
        var lead = UiMetrics.Px(GlyphColumnLead);
        var badgeSide = RowBadgeSide(layout.LineHeight, layout.RowContent);
        ImGui.Dummy(new Vector2(lead + layout.GlyphBox + badgeSide, layout.RowContent));
        var dl = ImGui.GetWindowDrawList();
        var centerY = cell.Y + layout.RowContent * 0.5f;

        if (runner.IsPinned(quest.RowId))
        {
            dl.AddCircleFilled(cell + new Vector2(UiMetrics.Px(3f), centerY - cell.Y), UiMetrics.Px(2.5f), Theme.MoonU32);
        }

        var readyOn = state == QuestState.ReadyOnOtherJob ? runner.ReadyOnJob(quest.RowId) : (byte)0;
        var glyphCenter = new Vector2(cell.X + lead + layout.GlyphBox * 0.5f, centerY);
        if (Theme.MoonRoadArt && !Theme.ClassicMoons)
        {
            // Full: the medal sits on a 1.5 px shadow falling straight down, and a Ready medal glows (4 px, gold).
            if (state == QuestState.Ready && Theme.ShowGlow)
            {
                var reach = UiMetrics.Px(4f);
                dl.AddCircleFilled(glyphCenter, layout.GlyphRadius + reach, Theme.WithAlpha(Theme.Moon, 0.10f), 32);
                dl.AddCircleFilled(glyphCenter, layout.GlyphRadius + (reach * 0.5f), Theme.WithAlpha(Theme.Moon, 0.16f), 32);
            }

            dl.AddCircleFilled(glyphCenter + new Vector2(0f, UiMetrics.Px(1.5f)), layout.GlyphRadius, Theme.WithAlpha(Theme.Abyss, 0.55f), 24);
        }

        MoonWax.Draw(dl, glyphCenter, layout.GlyphRadius, state, quest.RowId, readyOn);
        if (moonRoadMoment && moment >= 0f)
        {
            MoonWax.DrawHalo(dl, glyphCenter, layout.GlyphRadius, moment);
        }
        MedalGlyph.DrawRowBadge(dl, new Vector2(cell.X + lead + layout.GlyphBox, centerY - badgeSide * 0.5f), badgeSide, state, readyOn);

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
        if (selected)
        {
            SelectedRowRect = new ScreenRect(rowMin, rowMax);
        }

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

        DrawRowChrome(rowMin, rowMax, nameCellMin.X, state, selected, settled, liftRow == quest.RowId && hover > 0.5f, in layout, Motion.Key(RevealTag, quest.RowId), moonRoadMoment && moment >= 0f ? MotionTokens.MomentAlpha(moment) : 0f);
        if (newGroupEnd == quest.RowId || trialGroupEnd == quest.RowId)
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

        if (ImGui.TableNextColumn())
        {
            // Ellipsised in a column the player made narrower than the number, which its hover then shows whole.
            CenterText(in layout);
            var exp = runner.ExpText(quest);
            var expPos = ImGui.GetCursorScreenPos();
            var expRoom = ImGui.GetContentRegionAvail().X;
            var expWidth = ImGui.CalcTextSize(exp).X;
            ImGui.Dummy(new Vector2(MathF.Max(1f, MathF.Min(expRoom, expWidth)), ImGui.GetTextLineHeight()));
            if (EllipsisAt(ImGui.GetWindowDrawList(), expPos, expRoom, exp, Theme.U32(s.TextSecondary), expWidth) && ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(exp);
            }
        }

        if (ImGui.TableNextColumn())
        {
            DrawOpensIcons(quest, in layout);
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

    /// <summary>
    /// Width of the state stripe in pixels: the glyph palette's at Full and under high contrast (3 logical, 4 in high
    /// contrast), 2 at Quiet and Plain (an accessibility carrier, A3, so never gone); never under 2.
    /// </summary>
    internal static float StripeThickness() =>
        MathF.Max(2f, MathF.Round(UiMetrics.Px(Theme.Flair == Flair.Full || Theme.Glyphs.HighContrast ? Theme.Glyphs.StripeWidth : 2f)));

    /// <summary>
    /// The road under a Ready row at Full (R3 #6, proposal §7.3): a gold line along its bottom from the name, fading out
    /// by 70 %, with a faint glow; and under the Moon Road's motion, a glint that runs along it once every 9 s.
    /// </summary>
    private static void DrawReadyRoad(ImDrawListPtr dl, float nameX, Vector2 rowMax, float hairline)
    {
        var y = rowMax.Y - hairline;
        var end = nameX + ((rowMax.X - nameX) * 0.7f);
        Ornament.Rule(dl, new Vector2(nameX, y), end - nameX, ReadyRoadAlpha, hairline, Theme.Moon);
        if (Theme.ShowGlow)
        {
            var glow = Theme.WithAlpha(Theme.Moon, 0.12f);
            var none = Theme.WithAlpha(Theme.Moon, 0f);
            dl.AddRectFilledMultiColor(new Vector2(nameX, y - UiMetrics.Px(2f)), new Vector2(end, y), none, none, glow, glow);
        }

        if (!Theme.FlairMotion)
        {
            return;
        }

        // The glint: a MoonHigh gleam a fifth of the road wide, crossing it in the last 18 % of each cycle.
        var phase = (float)(ImGui.GetTime() % GlintCycleSeconds / GlintCycleSeconds);
        if (phase < 1f - GlintShare)
        {
            return;
        }

        var t = (phase - (1f - GlintShare)) / GlintShare;
        var span = end - nameX;
        var width = span * 0.2f;
        var center = nameX - width + ((span + (2f * width)) * MotionMath.EaseOutCubic(t));
        var left = MathF.Max(nameX, center - (width * 0.5f));
        var right = MathF.Min(end, center + (width * 0.5f));
        if (right <= left)
        {
            return;
        }

        var bright = Theme.WithAlpha(Theme.MoonHigh, 0.9f * (1f - t));
        var clear = Theme.WithAlpha(Theme.MoonHigh, 0f);
        var mid = (left + right) * 0.5f;
        dl.AddRectFilledMultiColor(new Vector2(left, y), new Vector2(mid, y + hairline), clear, bright, bright, clear);
        dl.AddRectFilledMultiColor(new Vector2(mid, y), new Vector2(right, y + hairline), bright, clear, clear, bright);
    }

    /// <summary>
    /// The row's lines, on the table's background channel so they span every column under the text: the separator
    /// (Comfortable), the selection ring (1 px, the text colour at 0.45, rounded, inset 1 px: a selection, not gold),
    /// the hover lift, and the state stripe on top of them at the left edge. At Flair Full a Ready row also gets the
    /// road (R3 #6, proposal §7.3): a brass hairline along its bottom from the name to the right edge, fading out, faint
    /// enough that the gold moon stays the signal.
    /// </summary>
    private static void DrawRowChrome(Vector2 rowMin, Vector2 rowMax, float nameX, QuestState state, bool selected, float settled, bool lifted, in RowLayout layout, ulong revealKey, float flash)
    {
        var s = Theme.Surface;
        ImGuiP.TablePushBackgroundChannel();
        var dl = ImGui.GetWindowDrawList();
        var hairline = UiMetrics.Hairline;
        var flair = Theme.Flair;
        if (!layout.Dense || flair == Flair.Plain)
        {
            var y = rowMax.Y - hairline * 0.5f;
            var line = flair == Flair.Full ? s.Line with { W = 0.35f * s.Line.W } : flair == Flair.Quiet ? Theme.Tones.Rule with { W = 0.55f } : Theme.Tones.Rule with { W = 0.6f };
            dl.AddLine(new Vector2(rowMin.X, y), new Vector2(rowMax.X, y), Theme.U32(line), hairline);
        }

        if (layout.ReadyRoad && state == QuestState.Ready)
        {
            DrawReadyRoad(dl, nameX, rowMax, hairline);
        }

        if (lifted && flair == Flair.Full)
        {
            Chrome.Lift(dl, rowMin, rowMax);
        }

        if (settled > 0.004f)
        {
            switch (flair)
            {
                case Flair.Full when !Theme.Glyphs.HighContrast:
                {
                    // Full: a warm wash from the left (Moon 0.13 → 0.02) between brass hairlines top and bottom.
                    var warm = Theme.WithAlpha(Theme.Moon, 0.10f * settled);
                    var cool = Theme.WithAlpha(Theme.Moon, 0f);
                    dl.AddRectFilledMultiColor(rowMin, new Vector2(rowMin.X + ((rowMax.X - rowMin.X) * 0.6f), rowMax.Y), warm, cool, cool, warm);
                    var brass = Theme.WithAlpha(Theme.Surface.OrnamentHigh, 0.35f * settled);
                    dl.AddRectFilled(rowMin, new Vector2(rowMax.X, rowMin.Y + hairline), brass);
                    dl.AddRectFilled(new Vector2(rowMin.X, rowMax.Y - hairline), rowMax, brass);
                    break;
                }

                case Flair.Plain:
                    // Plain: the wash alone.
                    break;

                default:
                {
                    // Quiet (and high contrast): a 1 px outline settling in from 2 px inside the row.
                    var inset = new Vector2(hairline * 0.5f + UiMetrics.Px(2f - settled));
                    var ring = flair == Flair.Quiet && !Theme.Glyphs.HighContrast ? Theme.WithAlpha(Theme.Veil, settled) : Theme.WithAlpha(s.Text, SelectionRingAlpha * settled);
                    dl.AddRect(rowMin + inset, rowMax - inset, ring, flair == Flair.Quiet ? 0f : UiMetrics.Px(SelectionRounding), ImDrawFlags.None, hairline);
                    break;
                }
            }
        }

        DrawStripe(dl, rowMin.X, rowMin.Y, rowMax.Y - rowMin.Y, state);
        if (flash > 0f)
        {
            // The stripe's flash (M1, Full flair): MoonHigh over the stripe, fading out, never brighter than the moment's peak.
            dl.AddRectFilled(rowMin, new Vector2(rowMin.X + StripeThickness(), rowMax.Y), Theme.WithAlpha(Theme.MoonHigh, flash));
        }
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

    /// <summary>
    /// A level or expansion pill (ui-revamp §2.4): the palette's sunken fill with the text in <paramref name="ink"/>,
    /// vertically centred in the row and never wider than the cell. A dummy of the pill's width is the cell's item. In a
    /// cell the player made narrower than the pill (feature plan v6 U9) the pill fills the cell and its text ends in an
    /// ellipsis, never cut part-way, and hovering it shows the whole text.
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
        var fits = size.X <= avail + 0.5f;
        size.X = MathF.Max(1f, MathF.Min(avail, size.X));
        ImGui.Dummy(new Vector2(size.X, layout.RowContent));
        var min = new Vector2(pos.X, pos.Y + MathF.Round((layout.RowContent - size.Y) * 0.5f));
        var dl = ImGui.GetWindowDrawList();
        if (fits)
        {
            PillAt(dl, min, size, text, ink);
            return;
        }

        // The padding gives way first (the text centred in what the cell has), then the text takes the ellipsis.
        bool cut;
        using (Typography.Caption())
        {
            Chrome.PillAt(dl, min, size, string.Empty, Theme.U32(Theme.Surface.Sunken), 0u, 0u);
            var textWidth = ImGui.CalcTextSize(text).X;
            var pad = Math.Clamp((size.X - textWidth) * 0.5f, UiMetrics.Px(1f), padX);
            var textPos = new Vector2(min.X + pad, min.Y + MathF.Round((size.Y - ImGui.GetTextLineHeight()) * 0.5f));
            cut = EllipsisAt(dl, textPos, size.X - pad * 2f, text, Theme.U32(ink), textWidth);
        }

        if (cut && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(text);
        }
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
    /// a job shows its icon alone and a group its label, ellipsised in the cell. In a cell the player made narrower than
    /// the label (feature plan v6 U9) the label ends in an ellipsis after the icon, or the icon shows alone when not even
    /// that fits; a cut label's hover names the job (or the label itself when there is no other name).
    /// </summary>
    private void DrawJob(JobLabel job, bool iconOnly, bool rowHovered, float mouseX, in RowLayout layout)
    {
        var pos = ImGui.GetCursorScreenPos();
        var cellWidth = ImGui.GetContentRegionAvail().X;
        var icon = MathF.Min(UiMetrics.Icon(JobIconSide), layout.RowContent);
        var gap = UiMetrics.Px(JobIconGap);
        var textSize = ImGui.CalcTextSize(job.Short);
        var width = iconOnly ? MathF.Max(1f, cellWidth) : MathF.Max(1f, MathF.Min(cellWidth, icon + gap + textSize.X));
        ImGui.Dummy(new Vector2(width, layout.RowContent));

        var dl = ImGui.GetWindowDrawList();
        var ink = Theme.U32(Theme.Surface.TextSecondary);
        var labelPos = new Vector2(pos.X + icon + gap, pos.Y + layout.TextOffset);
        var labelRoom = cellWidth - icon - gap;
        var cut = false;
        if (job.IconId != 0)
        {
            var iconMin = new Vector2(pos.X, pos.Y + MathF.Round((layout.RowContent - icon) * 0.5f));
            GameIcon.DrawAt(dl, textures, job.IconId, iconMin, iconMin + new Vector2(icon, icon));
            if (!iconOnly)
            {
                // The icon alone when the room after it holds less than a letter and the ellipsis.
                cut = labelRoom < textSize.X - 0.5f;
                if (!cut || labelRoom >= EllipsisRoom())
                {
                    EllipsisAt(dl, labelPos, labelRoom, job.Short, ink, textSize.X);
                }
            }
        }
        else if (iconOnly || labelRoom < textSize.X - 0.5f)
        {
            // A group's label without an icon takes the icon's slot too once it would not fit after it.
            cut = EllipsisAt(dl, new Vector2(pos.X, pos.Y + layout.TextOffset), cellWidth, job.Short, ink, textSize.X);
        }
        else
        {
            dl.AddText(labelPos, ink, job.Short);
        }

        if (rowHovered && mouseX >= pos.X && mouseX <= pos.X + width && (job.Name.Length > 0 || cut))
        {
            UiMetrics.Tooltip(job.Name.Length > 0 ? job.Name : job.Short);
        }
    }

    /// <summary>The least room an ellipsised label is drawn in: a letter and the ellipsis, else it is left out.</summary>
    private static float EllipsisRoom() => ImGui.CalcTextSize("W…").X;

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
        using var tooltip = Theme.Tooltip();
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

        // What it opens (feature plan v6 K4), never for a quest the shield masks.
        if (runner.Unlocks is { } unlocks && !spoilers.IsMasked(quest) && unlocks.OpensLine(quest.RowId, runner.UnlockReach) is { Length: > 0 } opens)
        {
            ImGui.TextWrapped(opens);
        }
    }

    /// <summary>
    /// The Opens column: the icons of the first three things the quest opens, its own duty, job, action, flying and
    /// feature rewards among them (next quests left out, and anything that belongs to the Rewards column:
    /// <see cref="Core.Unlocks.RewardSplit"/>); nothing for a masked quest. In a column the player made narrower than
    /// the icons (feature plan v6 U9) only whole icons show, then "+N", whose hover names the rest.
    /// </summary>
    private void DrawOpensIcons(QuestRecord quest, in RowLayout layout)
    {
        if (runner.Unlocks is not { } unlocks || runner.Spoilers.IsMasked(quest))
        {
            return;
        }

        var entries = unlocks.For(quest.RowId);
        var reach = runner.UnlockReach;
        var count = 0;
        for (var i = 0; i < entries.Count && count < MaxOpensIcons; i++)
        {
            count += ShowsOpens(entries[i], reach) ? 1 : 0;
        }

        if (count == 0)
        {
            return;
        }

        var cell = ImGui.GetCursorScreenPos();
        var iconSize = UiMetrics.RowIconSize;
        var gap = UiMetrics.Px(2f);
        var fit = TableGeometry.IconsThatFit(ImGui.GetContentRegionAvail().X, iconSize, gap, count, ImGui.CalcTextSize(MoreLabel(count)).X);
        var iconOffset = MathF.Max(0f, (layout.RowContent - iconSize) * 0.5f);
        var drawn = 0;
        for (var i = 0; i < entries.Count && drawn < fit; i++)
        {
            var entry = entries[i];
            if (!ShowsOpens(entry, reach))
            {
                continue;
            }

            if (drawn > 0)
            {
                ImGui.SameLine(0f, gap);
            }
            else if (iconOffset > 0.5f)
            {
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + iconOffset);
            }

            GameIcon.Draw(textures, entry.Icon, iconSize);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(entry.Name, entry.Caption);
            }

            drawn++;
        }

        if (fit < count && DrawMoreCount(count - fit, cell, fit > 0 ? ImGui.GetItemRectMax().X + gap : cell.X, in layout))
        {
            using var tooltip = Theme.Tooltip();
            ImGui.PushFont(UiBuilder.DefaultFont);
            UiMetrics.ApplyFontScale();
            var skipped = 0;
            for (var i = 0; i < entries.Count && skipped < count; i++)
            {
                if (ShowsOpens(entries[i], reach) && skipped++ >= fit)
                {
                    ImGui.TextUnformatted(entries[i].Name);
                }
            }

            ImGui.PopFont();
        }
    }

    /// <summary>
    /// Whether the Opens column shows <paramref name="entry"/>: Sprout mode leaves out rows past the character's reach,
    /// and no row repeats a reward, as in the detail pane (UnlockView.Visible). A row without an icon counts too and
    /// draws the veiled moon, as its detail row does, so the two agree (UI-Q Q11).
    /// </summary>
    private static bool ShowsOpens(Core.Unlocks.UnlockEntry entry, byte reach) =>
        entry.Target != Core.Unlocks.UnlockTarget.NextQuest && Core.Unlocks.UnlockView.Shows(entry, reach);

    /// <summary>
    /// The Rewards column: up to four icons of what the quest hands over to keep. In a column the player made narrower
    /// than the icons (feature plan v6 U9) only whole icons show, then "+N", whose hover names the rest.
    /// </summary>
    private void DrawRewardIcons(QuestRecord quest, in RowLayout layout)
    {
        var rewards = quest.Rewards;
        var count = 0;
        for (var i = 0; i < rewards.Count && count < MaxRewardIcons; i++)
        {
            count += ShowsReward(rewards[i]) ? 1 : 0;
        }

        if (count == 0)
        {
            return;
        }

        var cell = ImGui.GetCursorScreenPos();
        var iconSize = UiMetrics.RowIconSize;
        var gap = UiMetrics.Px(2f);
        var fit = TableGeometry.IconsThatFit(ImGui.GetContentRegionAvail().X, iconSize, gap, count, ImGui.CalcTextSize(MoreLabel(count)).X);
        var iconOffset = MathF.Max(0f, (layout.RowContent - iconSize) * 0.5f);
        var drawn = 0;
        for (var i = 0; i < rewards.Count && drawn < fit; i++)
        {
            var reward = rewards[i];
            if (!ShowsReward(reward))
            {
                continue;
            }

            if (drawn > 0)
            {
                ImGui.SameLine(0f, gap);
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

        if (fit < count && DrawMoreCount(count - fit, cell, fit > 0 ? ImGui.GetItemRectMax().X + gap : cell.X, in layout))
        {
            using var tooltip = Theme.Tooltip();
            ImGui.PushFont(UiBuilder.DefaultFont);
            UiMetrics.ApplyFontScale();
            var skipped = 0;
            for (var i = 0; i < rewards.Count && skipped < count; i++)
            {
                if (ShowsReward(rewards[i]) && skipped++ >= fit)
                {
                    ImGui.TextUnformatted(rewards[i].Name);
                }
            }

            ImGui.PopFont();
        }
    }

    /// <summary>Whether the Rewards column shows <paramref name="reward"/>: only what the quest hands over to keep (a duty, a job, an action, flying or a feature is the Unlocks column's, RewardSplit).</summary>
    private static bool ShowsReward(RewardRef reward) => reward.Icon != 0 && Core.Unlocks.RewardSplit.IsReward(reward);

    /// <summary>"+1" … "+8", the count of icons a narrowed column leaves out.</summary>
    private static readonly string[] MoreLabels = ["+0", "+1", "+2", "+3", "+4", "+5", "+6", "+7", "+8"];

    private static string MoreLabel(int count) => MoreLabels[Math.Clamp(count, 0, MoreLabels.Length - 1)];

    /// <summary>
    /// "+N" in the tertiary tone at <paramref name="x"/> on the row's text line, for the icons a narrowed column leaves
    /// out; draw list only, so the row stays the hovered item. Returns whether the mouse is over it.
    /// </summary>
    private static bool DrawMoreCount(int count, Vector2 cell, float x, in RowLayout layout)
    {
        var label = MoreLabel(count);
        var size = ImGui.CalcTextSize(label);
        var pos = new Vector2(x, cell.Y + layout.TextOffset);
        ImGui.GetWindowDrawList().AddText(pos, Theme.U32(Theme.Surface.TextTertiary), label);
        return ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(new Vector2(x, cell.Y), new Vector2(x + size.X, cell.Y + layout.RowContent));
    }

    private void DrawContextMenu(QuestRecord quest, QuestState state)
    {
        if (ImGui.MenuItem(runner.IsPinned(quest.RowId) ? Strings.Unpin : Strings.Pin, enabled: runner.CanPin))
        {
            runner.TogglePinWithUndo(quest);
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

        // "Open on…" and Copy table as TSV (1.8.0).
        links.DrawOpenOnMenu(quest, runner.Spoilers.IsMasked(quest), runner.Spoilers.DisplayName(quest));
        if (ImGui.MenuItem(Strings.LinksCopyTableTsv))
        {
            ImGui.SetClipboardText(TableCopy.Quests(runner, links));
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.LinksCopyTableTsvTooltip);
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

    /// <summary>
    /// When another pane changed the selection, scroll the table so the row is 40 % down the view. A row the list does
    /// not hold yet (a reveal's query lands a frame later) is scrolled to when the new rows arrive (<see cref="AnchorScroll"/>).
    /// </summary>
    private void ScrollToExternalSelection(int count, float rowHeight)
    {
        if (ui.SelectedRowId == lastSelection)
        {
            return;
        }

        lastSelection = ui.SelectedRowId;
        pendingReveal = null;
        if (lastSelection is not { } rowId)
        {
            return;
        }

        if (rowIndex.TryGetValue(rowId, out var index))
        {
            ImGui.SetScrollY(ScrollAnchor.Reveal(index, count, rowHeight, ImGui.GetContentRegionAvail().Y));
        }
        else
        {
            pendingReveal = rowId;
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
