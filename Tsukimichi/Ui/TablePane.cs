using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Ui;

/// <summary>
/// The quest table: glyph, name, level, job, next step, expansion, reward icons; sortable, reorderable, hideable,
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

    private enum Column
    {
        Glyph,
        Name,
        Level,
        Job,
        NextStep,
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
        Strings.ColumnNextStep,
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
        Strings.ColumnNextStepTooltip,
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
        var rowContent = UiMetrics.RowContentHeight(lineHeight);
        var rowHeight = rowContent + style.CellPadding.Y * 2f;
        var glyphBox = UiMetrics.RowGlyphRadius * 2.4f;
        var glyphColumn = UiMetrics.Px(GlyphColumnLead) + glyphBox + UiMetrics.Px(2f);
        var rewardsColumn = UiMetrics.RowIconSize * MaxRewardIcons + UiMetrics.Px(2f) * (MaxRewardIcons - 1) + UiMetrics.Px(8f);

        // The persisted sort is handed to ImGui only while the table initializes (ImGui ignores DefaultSort afterwards
        // and whenever its own saved settings already carry a sort), so no column is default-sorted otherwise.
        var initialSort = tableInitialized ? SortSpec.Default : ui.Sort;
        tableInitialized = true;

        ImGui.TableSetupColumn(Strings.ColumnGlyph, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize | ImGuiTableColumnFlags.NoHide | ImGuiTableColumnFlags.NoHeaderLabel | InitialSortFlags(initialSort, SortColumn.State), glyphColumn);
        ImGui.TableSetupColumn(Strings.ColumnName, ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.NoHide | InitialSortFlags(initialSort, SortColumn.Name), 3f);
        ImGui.TableSetupColumn(Strings.ColumnLevel, ImGuiTableColumnFlags.WidthFixed | InitialSortFlags(initialSort, SortColumn.Level), UiMetrics.Px(34f));
        ImGui.TableSetupColumn(Strings.ColumnJob, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort, UiMetrics.Px(64f));
        ImGui.TableSetupColumn(Strings.ColumnNextStep, ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.NoSort, 2f);
        ImGui.TableSetupColumn(Strings.ColumnExpansion, ImGuiTableColumnFlags.WidthFixed | InitialSortFlags(initialSort, SortColumn.Expansion), UiMetrics.Px(40f));
        ImGui.TableSetupColumn(Strings.ColumnRewards, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort, rewardsColumn);
        ImGui.TableSetupScrollFreeze(0, 1);
        DrawHeaders();

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

    /// <summary>What TableHeadersRow does, one header at a time, so each can carry a tooltip.</summary>
    private static void DrawHeaders()
    {
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
        for (var i = 0; i < HeaderTooltips.Length; i++)
        {
            if (!ImGui.TableSetColumnIndex(i))
            {
                continue;
            }

            using var id = ImRaii.PushId(i);
            ImGui.TableHeader(HeaderLabels[i]);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(HeaderTooltips[i]);
            }
        }
    }

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
        if (ImGui.Selectable(quest.Name, selected, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowDoubleClick | ImGuiSelectableFlags.AllowItemOverlap, new Vector2(0f, layout.RowContent)))
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
        if (ImGui.IsItemHovered())
        {
            var mouseX = ImGui.GetMousePos().X;
            if (mouseX >= nameCellMin.X && mouseX <= nameCellMin.X + nameCellWidth)
            {
                DrawNameTooltip(quest);
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
        if (hasSnapshot)
        {
            DrawNextStep(row.NextStep);
        }
        else
        {
            ImGui.TextDisabled(row.NextStep);
        }

        ImGui.TableNextColumn();
        CenterText(in layout);
        ImGui.TextUnformatted(runner.ExpansionShort(quest.Expansion));

        ImGui.TableNextColumn();
        DrawRewardIcons(quest, in layout);
    }

    /// <summary>Next step in Dusk with its first word in Silver, so the gate's kind reads at a glance; spans only, no new strings.</summary>
    private static void DrawNextStep(string text)
    {
        var split = text.IndexOf(' ');
        if (split <= 0)
        {
            using var dusk = Theme.PushText(Theme.Dusk);
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
    /// </summary>
    private void DrawNameTooltip(QuestRecord quest)
    {
        using var tooltip = ImRaii.Tooltip();
        UiMetrics.ApplyFontScale();
        var width = UiMetrics.BannerTooltipWidth;
        if (quest.Icon != 0 && textures.GetFromGameIcon(new GameIconLookup(quest.Icon)).TryGetWrap(out var wrap, out _) && wrap.Width > 0 && wrap.Height > 0)
        {
            ImGui.Image(wrap.Handle, new Vector2(width, width * wrap.Height / wrap.Width));
        }

        using var wrapPos = ImRaii.TextWrapPos(ImGui.GetCursorPosX() + width);
        ImGui.TextWrapped(quest.Name);
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
            ImGui.SetClipboardText(quest.Name);
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

        ImGui.Spacing();
        using (Theme.PushText(Theme.Dusk))
        {
            ImGui.TextUnformatted(Strings.NothingMatches);
        }

        if (empty.Filters.Count > 0)
        {
            ImGui.TextUnformatted(Strings.NothingMatchesHint);
            using var indent = ImRaii.PushIndent(12f);
            foreach (var name in empty.Filters)
            {
                ImGui.Bullet();
                ImGui.TextUnformatted(name);
            }
        }
        else
        {
            ImGui.TextWrapped(Strings.NothingMatchesCombination);
        }

        ImGui.Spacing();
        if (ImGui.Button(Strings.ResetFilters))
        {
            resetFilters();
        }
    }

    /// <summary>DefaultSort (plus the direction) for the column the persisted sort names; none for every other column.</summary>
    private static ImGuiTableColumnFlags InitialSortFlags(SortSpec initial, SortColumn column)
    {
        if (initial.Column != column)
        {
            return ImGuiTableColumnFlags.None;
        }

        return ImGuiTableColumnFlags.DefaultSort | (initial.Descending ? ImGuiTableColumnFlags.PreferSortDescending : ImGuiTableColumnFlags.PreferSortAscending);
    }

    private void ApplySortSpecs()
    {
        var specs = ImGui.TableGetSortSpecs();
        if (specs.IsNull || !specs.SpecsDirty)
        {
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
