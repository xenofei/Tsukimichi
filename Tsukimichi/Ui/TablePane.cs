using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
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
    private bool selectionFromTable;

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
        var scale = ImGuiHelpers.GlobalScale;
        var style = ImGui.GetStyle();
        var lineHeight = ImGui.GetTextLineHeight();
        var rowHeight = lineHeight + style.CellPadding.Y * 2f;

        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.Sortable | ImGuiTableFlags.ScrollY
            | ImGuiTableFlags.Resizable | ImGuiTableFlags.Reorderable | ImGuiTableFlags.Hideable | ImGuiTableFlags.SizingFixedFit;

        using var table = ImRaii.Table("##quests", 7, flags, ImGui.GetContentRegionAvail());
        if (!table)
        {
            return;
        }

        ImGui.TableSetupColumn(Strings.ColumnGlyph, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize | ImGuiTableColumnFlags.NoHide | ImGuiTableColumnFlags.NoHeaderLabel, 26f * scale);
        ImGui.TableSetupColumn(Strings.ColumnName, ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.NoHide, 3f);
        ImGui.TableSetupColumn(Strings.ColumnLevel, ImGuiTableColumnFlags.WidthFixed, 34f * scale);
        ImGui.TableSetupColumn(Strings.ColumnJob, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort, 64f * scale);
        ImGui.TableSetupColumn(Strings.ColumnNextStep, ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.NoSort, 2f);
        ImGui.TableSetupColumn(Strings.ColumnExpansion, ImGuiTableColumnFlags.WidthFixed, 40f * scale);
        ImGui.TableSetupColumn(Strings.ColumnRewards, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort, 120f * scale);
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableHeadersRow();

        ApplySortSpecs();
        ScrollToExternalSelection(rows, rowHeight);

        if (!clipperCreated)
        {
            clipper = ImGui.ImGuiListClipper();
            clipperCreated = true;
        }

        clipper.Begin(rows.Length, rowHeight);
        while (clipper.Step())
        {
            for (var i = clipper.DisplayStart; i < clipper.DisplayEnd && i < rows.Length; i++)
            {
                DrawRow(in rows[i], hasSnapshot, lineHeight, scale);
            }
        }

        clipper.End();
    }

    public void Dispose()
    {
        if (clipperCreated)
        {
            clipper.Destroy();
            clipperCreated = false;
        }
    }

    private void DrawRow(in QuestRow row, bool hasSnapshot, float lineHeight, float scale)
    {
        var quest = row.Quest;
        using var id = ImRaii.PushId((int)quest.RowId);
        ImGui.TableNextRow();

        // Glyph column: pinned dot at the left edge, state moon centred in the rest.
        ImGui.TableNextColumn();
        var cell = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(24f * scale, lineHeight));
        var dl = ImGui.GetWindowDrawList();
        if (runner.IsPinned(quest.RowId))
        {
            dl.AddCircleFilled(cell + new Vector2(3f * scale, lineHeight * 0.5f), 2.5f * scale, Theme.MoonU32);
        }

        MoonGlyph.Draw(dl, cell + new Vector2(15f * scale, lineHeight * 0.5f), lineHeight * 0.42f, hasSnapshot ? row.State : QuestState.Unknown);

        // Name column carries the row-wide selectable and the context menu.
        ImGui.TableNextColumn();
        var selected = ui.SelectedRowId == quest.RowId;
        if (ImGui.Selectable(quest.Name, selected, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowDoubleClick | ImGuiSelectableFlags.AllowItemOverlap))
        {
            SelectFromTable(quest.RowId);
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
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

                DrawContextMenu(quest);
            }
        }

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(runner.LevelText(quest.Level));

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(runner.JobShort(quest));

        ImGui.TableNextColumn();
        if (hasSnapshot)
        {
            ImGui.TextUnformatted(row.NextStep);
        }
        else
        {
            ImGui.TextDisabled(row.NextStep);
        }

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(runner.ExpansionShort(quest.Expansion));

        ImGui.TableNextColumn();
        DrawRewardIcons(quest, lineHeight, scale);
    }

    private void DrawRewardIcons(QuestRecord quest, float lineHeight, float scale)
    {
        var drawn = 0;
        var rewards = quest.Rewards;
        for (var i = 0; i < rewards.Count && drawn < MaxRewardIcons; i++)
        {
            var reward = rewards[i];
            if (reward.Icon == 0)
            {
                continue;
            }

            if (drawn > 0)
            {
                ImGui.SameLine(0f, 2f * scale);
            }

            var wrap = textures.GetFromGameIcon(new GameIconLookup(reward.Icon)).GetWrapOrEmpty();
            ImGui.Image(wrap.Handle, new Vector2(lineHeight, lineHeight));
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(RewardTooltip(reward));
            }

            drawn++;
        }
    }

    private void DrawContextMenu(QuestRecord quest)
    {
        if (ImGui.MenuItem(runner.IsPinned(quest.RowId) ? Strings.Unpin : Strings.Pin))
        {
            runner.TogglePin(quest.RowId);
        }

        if (ImGui.MenuItem(Strings.FlagOnMap, enabled: links.CanFlagMap(quest)))
        {
            links.FlagMap(quest);
        }

        if (ImGui.MenuItem(Strings.OpenJournal))
        {
            links.OpenJournal(quest);
        }

        if (ImGui.MenuItem(Strings.CopyName))
        {
            ImGui.SetClipboardText(quest.Name);
        }

        if (ImGui.MenuItem(Strings.ShowPath))
        {
            ui.ShowPath(quest.RowId);
        }

        if (ImGui.MenuItem(Strings.LinkInChat))
        {
            links.PrintQuestLink(quest);
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
        ImGui.Spacing();
        using (Theme.PushText(Theme.Dusk))
        {
            ImGui.TextUnformatted(empty.ScopeIsEmpty ? Strings.ScopeEmpty : Strings.NothingMatches);
        }

        if (empty.ScopeIsEmpty)
        {
            return;
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

    private void ApplySortSpecs()
    {
        var specs = ImGui.TableGetSortSpecs();
        if (specs.IsNull || !specs.SpecsDirty)
        {
            return;
        }

        var sort = SortSpec.Default;
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
            sort = new SortSpec(column, spec.SortDirection == ImGuiSortDirection.Descending);
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
        if (selectionFromTable)
        {
            selectionFromTable = false;
            return;
        }

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

        ui.SelectedRowId = rowId;
        lastSelection = rowId;
        selectionFromTable = true;
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

    private static string RewardTooltip(RewardRef reward) =>
        reward.Count > 1
            ? string.Format(CultureInfo.CurrentCulture, Strings.RewardCountFormat, reward.Name, reward.Count)
            : reward.Name;
}
