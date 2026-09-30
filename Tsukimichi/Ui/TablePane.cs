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

namespace Tsukimichi.Ui;

/// <summary>
/// The quest table: glyph, name, level, job, status, expansion, reward icons; sortable, reorderable, hideable,
/// clipped with <see cref="ImGuiListClipperPtr"/>. Row click selects, double-click opens the journal, right-click
/// opens the context menu. The body allocates nothing: every string it shows is pre-materialized by
/// <see cref="QueryRunner"/> or the query rows.
/// </summary>
public sealed class TablePane : IDisposable
{
    private const string QuestMapPluginName = "QuestMap";
    private const string QuestMapIpcName = "QuestMap.ShowGraphByQuestId";
    private const int MaxRewardIcons = 4;

    /// <summary>Logical space left of the state moon for the pinned dot.</summary>
    private const float GlyphColumnLead = 8f;

    /// <summary>Initial logical width of the Name column; the player can drag it, and long names clip.</summary>
    private const float NameColumnWidth = 240f;

    /// <summary>Logical width under which the Status column sheds Rewards, then Expansion: room for "Ready on another job".</summary>
    private const float StatusMinWidth = 170f;

    /// <summary>Logical gap between a story sidequest's name and its book badge.</summary>
    private const float StoryBadgeGap = 6f;

    private static readonly string StoryBadgeIcon = FontAwesomeIcon.BookOpen.ToIconString();

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
    private static readonly string[] HeaderLabels =
    [
        string.Empty,
        Strings.ColumnName,
        Strings.ColumnLevel,
        Strings.ColumnJob,
        Strings.ColumnStatus,
        Strings.ColumnExpansion,
        Strings.ColumnRewards,
    ];

    /// <summary>Header tooltip per <see cref="Column"/>, in column order.</summary>
    private static readonly string[] HeaderTooltips =
    [
        Strings.ColumnGlyphTooltip,
        Strings.ColumnNameTooltip,
        Strings.ColumnLevelTooltip,
        Strings.ColumnJobTooltip,
        Strings.ColumnStatusTooltip,
        Strings.ColumnExpansionTooltip,
        Strings.ColumnRewardsTooltip,
    ];

    private readonly UiState ui;
    private readonly QueryRunner runner;
    private readonly GameLinks links;
    private readonly ITextureProvider textures;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;
    private readonly Action resetFilters;

    private ImGuiListClipperPtr clipper;
    private bool clipperCreated;

    private ICallGateSubscriber<uint, object>? questMap;
    private bool questMapAvailable;

    private uint? lastSelection;
    private bool tableInitialized;

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

    public TablePane(UiState ui, QueryRunner runner, GameLinks links, ITextureProvider textures, IDalamudPluginInterface pluginInterface, IPluginLog log, Action resetFilters)
    {
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.textures = textures ?? throw new ArgumentNullException(nameof(textures));
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.resetFilters = resetFilters ?? throw new ArgumentNullException(nameof(resetFilters));

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
        if (runner.Empty is { } empty)
        {
            DrawEmpty(empty);
            return;
        }

        var rows = runner.Rows;
        if (runner.SproutCaption is { } caption)
        {
            // Sprout mode (T19): how much of the game is in reach, instead of the whole catalog.
            ImGui.TextDisabled(caption);
        }

        // SortTristate lets the header cycle back to "no sort" (journal order) and stops ImGui from picking the first
        // sortable column (the glyph) as an implicit default on the first frame.
        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.Sortable | ImGuiTableFlags.SortTristate | ImGuiTableFlags.ScrollY
            | ImGuiTableFlags.Resizable | ImGuiTableFlags.Reorderable | ImGuiTableFlags.Hideable | ImGuiTableFlags.SizingFixedFit;

        using var table = ImRaii.Table("##quests", 7, flags, ImGui.GetContentRegionAvail());
        if (!table)
        {
            return;
        }

        // ScrollY gives the table its own inner window, so this is the table's rectangle; that window sits inside the
        // centre column (own font scale 1), so it scales itself before anything is measured.
        ui.RecordWindow(UiRects.Table);
        UiMetrics.ApplyFontScale();
        var style = ImGui.GetStyle();
        var lineHeight = ImGui.GetTextLineHeight();
        var rowContent = UiMetrics.TableRowContentHeight(lineHeight, style.CellPadding.Y);
        var rowHeight = rowContent + style.CellPadding.Y * 2f;
        var glyphBox = UiMetrics.RowGlyphRadius * 2.4f;
        var glyphColumn = UiMetrics.Px(GlyphColumnLead) + glyphBox + UiMetrics.Px(2f);
        var rewardsColumn = UiMetrics.RowIconSize * MaxRewardIcons + UiMetrics.Px(2f) * (MaxRewardIcons - 1) + UiMetrics.Px(8f);

        ImGui.TableSetupColumn(Strings.ColumnGlyph, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize | ImGuiTableColumnFlags.NoHide | ImGuiTableColumnFlags.NoHeaderLabel, glyphColumn);
        // Status is the one stretch column (feature plan v3 P1): it holds the answer to "why not", so it takes the
        // width the others leave, and FitStatusColumn hides Rewards, then Expansion, before it drops under its minimum.
        var expansionColumn = UiMetrics.Px(40f);
        ImGui.TableSetupColumn(Strings.ColumnName, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoHide, UiMetrics.Px(NameColumnWidth));
        ImGui.TableSetupColumn(Strings.ColumnLevel, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(34f));
        ImGui.TableSetupColumn(Strings.ColumnJob, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort, UiMetrics.Px(64f));
        ImGui.TableSetupColumn(Strings.ColumnStatus, ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.NoSort, 1f);
        // Expansion is a four-letter tag: NoResize keeps its width the setup width, so hiding it frees exactly that.
        ImGui.TableSetupColumn(Strings.ColumnExpansion, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, expansionColumn);
        ImGui.TableSetupColumn(Strings.ColumnRewards, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort, rewardsColumn);
        ImGui.TableSetupScrollFreeze(0, 1);

        // The persisted sort is written straight into the column state on the table's first frame: ImGui's own saved
        // settings (imgui.ini) would otherwise win over DefaultSort and hand their sort back through SpecsDirty. It is
        // written again on the frame the sorted column comes back from a FitStatusColumn hide (ImGui dropped it).
        if (!tableInitialized || restoreSort)
        {
            tableInitialized = true;
            restoreSort = false;
            ApplyInitialSort(ui.Sort);
        }

        // The two icon-derived widths are re-asserted every frame (imgui.ini restores font-tracked widths, not IconScale).
        ImGuiP.TableSetColumnWidth((int)Column.Glyph, glyphColumn);
        ImGuiP.TableSetColumnWidth((int)Column.Rewards, rewardsColumn);
        var statusWidth = DrawHeaders();
        FitStatusColumn(statusWidth, expansionColumn, rewardsColumn);

        ApplySortSpecs();
        ScrollToExternalSelection(rows, rowHeight);

        if (!clipperCreated)
        {
            clipper = ImGui.ImGuiListClipper();
            clipperCreated = true;
        }

        // Rows can be taller than the text when icons are scaled up; the selectable fills the row and centres its label.
        using var textAlign = ImRaii.PushStyle(ImGuiStyleVar.SelectableTextAlign, new Vector2(0f, 0.5f));
        // Zebra rows: every other row tinted so long lists stay easy to track across columns.
        using var zebra = ImRaii.PushColor(ImGuiCol.TableRowBgAlt, Theme.ZebraRow);
        var layout = new RowLayout(lineHeight, rowContent, glyphBox);
        clipper.Begin(rows.Length, rowHeight);
        while (clipper.Step())
        {
            for (var i = clipper.DisplayStart; i < clipper.DisplayEnd && i < rows.Length; i++)
            {
                DrawRow(in rows[i], hasSnapshot, in layout);
            }
        }

        clipper.End();
    }

    /// <summary>Per-frame row measurements, computed once per draw.</summary>
    private readonly record struct RowLayout(float LineHeight, float RowContent, float GlyphBox)
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

    /// <summary>What TableHeadersRow does, one header at a time, so each can carry a tooltip. Returns the Status column's laid-out width (0 when hidden).</summary>
    private static float DrawHeaders()
    {
        var statusWidth = 0f;
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

            using var id = ImRaii.PushId(i);
            ImGui.TableHeader(HeaderLabels[i]);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(HeaderTooltips[i]);
            }
        }

        return statusWidth;
    }

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

        var min = UiMetrics.Px(StatusMinWidth);
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

    private void DrawRow(in QuestRow row, bool hasSnapshot, in RowLayout layout)
    {
        var quest = row.Quest;
        using var id = ImRaii.PushId((int)quest.RowId);
        ImGui.TableNextRow();

        // Glyph column: pinned dot at the left edge, state moon centred in the rest.
        ImGui.TableNextColumn();
        var cell = ImGui.GetCursorScreenPos();
        var lead = UiMetrics.Px(GlyphColumnLead);
        ImGui.Dummy(new Vector2(lead + layout.GlyphBox, layout.RowContent));
        var dl = ImGui.GetWindowDrawList();
        var centerY = cell.Y + layout.RowContent * 0.5f;
        var state = hasSnapshot ? row.State : QuestState.Unknown;

        // A thin stripe on the row's left edge: Moon for Ready, Silver for Accepted, so the actionable rows stand out.
        if (state is QuestState.Ready or QuestState.Accepted)
        {
            var padding = ImGui.GetStyle().CellPadding;
            var stripeMin = new Vector2(cell.X - padding.X, cell.Y - padding.Y);
            dl.AddRectFilled(stripeMin, new Vector2(stripeMin.X + UiMetrics.Stripe, cell.Y + layout.RowContent + padding.Y), state == QuestState.Ready ? Theme.MoonU32 : Theme.SilverU32);
        }

        if (runner.IsPinned(quest.RowId))
        {
            dl.AddCircleFilled(cell + new Vector2(UiMetrics.Px(3f), centerY - cell.Y), UiMetrics.Px(2.5f), Theme.MoonU32);
        }

        MoonGlyph.Draw(dl, new Vector2(cell.X + lead + layout.GlyphBox * 0.5f, centerY), UiMetrics.RowGlyphRadius, state);

        // Name column carries the row-wide selectable and the context menu.
        ImGui.TableNextColumn();
        var nameCellMin = ImGui.GetCursorScreenPos();
        var nameCellWidth = ImGui.GetContentRegionAvail().X;
        var selected = ui.SelectedRowId == quest.RowId;
        var name = runner.Spoilers.DisplayName(quest);
        if (ImGui.Selectable(name, selected, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowDoubleClick | ImGuiSelectableFlags.AllowItemOverlap, new Vector2(0f, layout.RowContent)))
        {
            SelectFromTable(quest.RowId);
            // The game journal only knows accepted and completed quests; for the rest a double-click just selects.
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && GameLinks.CanOpenJournal(quest, row.State))
            {
                links.OpenJournal(quest);
            }
        }

        using (var popup = ImRaii.ContextPopupItem("##ctx"))
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

        // The selectable spans every column; the banner tooltip belongs to the name cell only, so the reward icons
        // keep their own tooltips.
        var rowHovered = ImGui.IsItemHovered();
        var badge = runner.Stories.Contains(quest.RowId)
            ? DrawStoryBadge(ImGui.GetWindowDrawList(), nameCellMin, nameCellWidth, layout.RowContent, name)
            : default;
        if (rowHovered)
        {
            var mouseX = ImGui.GetMousePos().X;
            if (badge.Y > 0f && mouseX >= badge.X && mouseX <= badge.X + badge.Y)
            {
                UiMetrics.Tooltip(runner.StoryBadgeText(quest.RowId));
            }
            else if (mouseX >= nameCellMin.X && mouseX <= nameCellMin.X + nameCellWidth)
            {
                DrawNameTooltip(quest, row.State);
            }
        }

        ImGui.TableNextColumn();
        CenterText(in layout);
        ImGui.TextUnformatted(runner.LevelText(quest.DisplayLevel));

        ImGui.TableNextColumn();
        CenterText(in layout);
        ImGui.TextUnformatted(runner.JobShort(quest));

        ImGui.TableNextColumn();
        CenterText(in layout);
        var statusCellMin = ImGui.GetCursorScreenPos();
        var statusCellWidth = ImGui.GetContentRegionAvail().X;
        if (hasSnapshot)
        {
            DrawStatus(row.Status);
        }
        else
        {
            ImGui.TextDisabled(row.Status);
        }

        // The cell clips rather than ellipsises, so the state word (first) always survives; the full line is a tooltip
        // whenever the tail was cut.
        if (rowHovered && row.Status.Length > 0 && ImGui.CalcTextSize(row.Status).X > statusCellWidth)
        {
            var mouseX = ImGui.GetMousePos().X;
            if (mouseX >= statusCellMin.X && mouseX <= statusCellMin.X + statusCellWidth)
            {
                UiMetrics.Tooltip(row.Status);
            }
        }

        ImGui.TableNextColumn();
        CenterText(in layout);
        ImGui.TextUnformatted(runner.ExpansionShort(quest.Expansion));

        ImGui.TableNextColumn();
        DrawRewardIcons(quest, in layout);
    }

    /// <summary>
    /// The book badge of a story sidequest in Dusk, just after the name, or at the cell's right edge when the name
    /// fills it. Draw list only, so the row's selectable stays the hovered item. Returns the badge's left x and width.
    /// </summary>
    private static Vector2 DrawStoryBadge(ImDrawListPtr dl, Vector2 cellMin, float cellWidth, float rowContent, string name)
    {
        var afterName = cellMin.X + ImGui.CalcTextSize(name).X + UiMetrics.Px(StoryBadgeGap);
        using var font = ImRaii.PushFont(UiBuilder.IconFont);
        var size = ImGui.CalcTextSize(StoryBadgeIcon);
        var x = MathF.Max(cellMin.X, MathF.Min(afterName, cellMin.X + cellWidth - size.X));
        dl.AddText(new Vector2(x, cellMin.Y + (rowContent - size.Y) * 0.5f), Theme.DuskU32, StoryBadgeIcon);
        return new Vector2(x, size.X);
    }

    /// <summary>
    /// Status text: the state name (everything before the separator) in Silver, the reason after it in Dusk, so the
    /// state reads at a glance and the blocker sits beside it; spans only, no new strings.
    /// </summary>
    private static void DrawStatus(string text)
    {
        var split = text.IndexOf(Strings.StateReasonSeparator, StringComparison.Ordinal);
        if (split <= 0)
        {
            using var silver = Theme.PushText(Theme.Silver);
            ImGui.TextUnformatted(text);
            return;
        }

        using (Theme.PushText(Theme.Silver))
        {
            ImGui.TextUnformatted(text.AsSpan(0, split));
        }

        ImGui.SameLine(0f, 0f);
        using (Theme.PushText(Theme.Dusk))
        {
            ImGui.TextUnformatted(text.AsSpan(split));
        }
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
        var clicked = EmptyState.DrawWithAction(Strings.EmptyNothingMatchesHeading, body, Strings.ResetFilters, empty.Filters, QuestState.Blocked);
        if (clicked == EmptyState.ActionClicked)
        {
            resetFilters();
        }
        else if (clicked >= 0 && EmptyState.ClearFilter(ui, empty.Filters[clicked]))
        {
            ui.MarkQueryDirty();
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
