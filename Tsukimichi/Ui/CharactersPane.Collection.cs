using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Unique;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// Characters › Collection by character (1.8.0, R7 C, R5 F2): "who has it". Rows are the Moonlit collectibles, each led
/// by its own art or its kind's menu icon (UI-5d) (or, in the Unlock quests mode, the unlock quests), columns the characters the lists show (hidden ones left out), cells
/// a check, a cross or the unknown mark from what each character's last save holds (decision 9), or the quest's state
/// moon. Filters: the reward kind, "missing on any" ("not done on any"), and a name filter. The table scrolls both
/// ways with the name column frozen, and only the rows in view are drawn (a clipper), so hundreds of rows cost the same
/// as twenty. The rows are rebuilt when an input changes, never per frame; another character's quest states are
/// resolved on a worker and its cells read "being read" until they land.
/// </summary>
public sealed partial class CharactersPane
{
    /// <summary>A character column is at least this wide (logical px); its header is the name, cut to fit.</summary>
    private const float GridColumnLogical = 64f;

    private const float GridNameLogical = 220f;

    // Mode -1 collectibles, 0 unlock quests; the kind filter's index into gridKinds (-1 all kinds).
    private int gridMode = -1;
    private int gridKind = -1;
    private bool gridMissingOnAny;
    private bool gridNotDoneOnAny;
    private string gridSearch = string.Empty;

    private GridKey gridKey;
    private List<CollectionGridRow> rewardRows = [];
    private List<QuestGridRow> questRows = [];
    private CharacterItem[] gridColumns = [];
    private string[] gridColumnTips = [];
    private string[] gridHeaders = [];
    private string[] gridCountTexts = [];

    // Per reward row: its own icon (Moonlit's, UI-5d), else its kind's menu icon; empty without Moonlit's resolver.
    private NodeIcon[] gridIcons = [];

    // Per character column in the Unlock quests mode: true when its states could not be read (no stored save, or the
    // worker failed), so its cells say "unreadable" instead of "being read" forever.
    private bool[] gridColumnFailed = [];
    private List<RewardKind> gridKinds = [];
    private string gridKindLabel = string.Empty;
    private string gridSummary = string.Empty;
    private ImGuiListClipperPtr gridClipper;
    private bool gridClipperCreated;

    // Each character's saved answers, per snapshot instance; and its unlock quest states resolved on a worker, per
    // capture time and catalog (null states: the save could not be read). gridResolved counts the resolves taken in, so
    // the rows rebuild when one lands.
    private readonly Dictionary<ulong, (CharacterSnapshot Snapshot, CollectibleLookup? Lookup)> gridLookups = [];
    private readonly Dictionary<ulong, (DateTime Taken, CatalogBundle Bundle, Task<IReadOnlyDictionary<uint, QuestState>> Task)> gridResolving = [];
    private readonly Dictionary<ulong, (DateTime Taken, CatalogBundle Bundle, IReadOnlyDictionary<uint, QuestState>? States)> gridStates = [];
    private int gridResolved;

    private void DrawCollection(UiState ui)
    {
        RefreshItems();
        if (session.Bundle is not { } bundle)
        {
            ImGui.TextDisabled(Strings.AltsGridNeedsCatalog);
            return;
        }

        if (items.Length == 0)
        {
            ImGui.TextWrapped(Strings.AltsGridNoCharacters);
            return;
        }

        TakeGridResolves();
        DrawGridFilters();
        RefreshGrid(bundle);
        ImGui.TextDisabled(gridSummary);
        if (gridMode == -1 && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.AltsGridRewardsHint);
        }

        var rowCount = gridMode == -1 ? rewardRows.Count : questRows.Count;
        if (rowCount == 0)
        {
            ImGui.TextDisabled(Strings.AltsGridEmpty);
            return;
        }

        DrawGridTable(ui, rowCount);
    }

    private void DrawGridFilters()
    {
        if (Chrome.SegmentedControl("##gridMode", ref gridMode, Strings.AltsGridModeRewards, [Strings.AltsGridModeQuests]))
        {
            gridKey = default;
        }

        if (gridMode == -1)
        {
            Chrome.SameLineOrWrap(Chrome.FitWidth(UiMetrics.Px(160f)));
            ImGui.SetNextItemWidth(Chrome.FitWidth(UiMetrics.Px(160f)));
            using (var combo = ImRaii.Combo("##gridKind", gridKindLabel))
            {
                if (combo)
                {
                    // The popup opens from the centre column (own font scale 1), so it scales itself.
                    UiMetrics.ApplyFontScale();
                    if (ImGui.Selectable(Strings.AltsGridKindAll, gridKind == -1))
                    {
                        gridKind = -1;
                    }

                    for (var i = 0; i < gridKinds.Count; i++)
                    {
                        // Each kind led by its menu icon, as Moonlit's kinds list (UI-5d).
                        if (MoonlitIcons is { } icons)
                        {
                            DrawGridIcon(icons.KindIcon(gridKinds[i]), MathF.Round(ImGui.GetTextLineHeight()));
                        }

                        if (ImGui.Selectable(Strings.MoonlitKindName(gridKinds[i]), gridKind == i))
                        {
                            gridKind = i;
                        }
                    }
                }
            }

            Chrome.SameLineOrWrap(ImGui.CalcTextSize(Strings.AltsGridMissingOnAny).X + ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X);
            ImGui.Checkbox(Strings.AltsGridMissingOnAny, ref gridMissingOnAny);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.AltsGridMissingOnAnyTooltip);
            }
        }
        else
        {
            Chrome.SameLineOrWrap(ImGui.CalcTextSize(Strings.AltsGridNotDoneOnAny).X + ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X);
            ImGui.Checkbox(Strings.AltsGridNotDoneOnAny, ref gridNotDoneOnAny);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.AltsGridNotDoneOnAnyTooltip);
            }
        }

        ImGui.SetNextItemWidth(Chrome.FitWidth(UiMetrics.Px(220f)));
        ImGui.InputTextWithHint("##gridSearch", Strings.AltsGridSearchHint, ref gridSearch, 64);
    }

    /// <summary>The rows, the columns and their texts, rebuilt only when an input changed.</summary>
    private void RefreshGrid(CatalogBundle bundle)
    {
        var rewards = RewardsCatalog();
        var key = new GridKey(session.RosterVersion, itemsRoster, gridMode, gridKind, gridMissingOnAny, gridNotDoneOnAny, gridSearch, bundle, rewards, gridResolved, itemsMinute, MoonlitIcons?.Revision ?? -1);
        if (key == gridKey)
        {
            return;
        }

        gridKey = key;
        gridColumns = items;
        gridColumnTips = new string[items.Length];
        gridHeaders = new string[items.Length];
        for (var c = 0; c < items.Length; c++)
        {
            var item = items[c];
            // The content id keeps the ImGui id unique when two characters share a name.
            gridHeaders[c] = item.Name + "##" + item.ContentId.ToString(CultureInfo.InvariantCulture);
            var state = item.ContentId == session.LiveContentId ? Strings.CharactersLive : item.Elsewhere ? Strings.MultiboxLiveElsewhere : Age(item.TakenUtc);
            gridColumnTips[c] = string.Format(CultureInfo.CurrentCulture, Strings.AltsGridColumnTooltipFormat, item.Name, item.WorldName, state);
        }

        gridKinds = CollectionGrid.Kinds(rewards.All);
        if (gridKind >= gridKinds.Count)
        {
            gridKind = -1;
        }

        gridKindLabel = gridKind < 0 ? Strings.AltsGridKindAll : Strings.MoonlitKindName(gridKinds[gridKind]);
        int rowCount;
        if (gridMode == -1)
        {
            var lookups = new CollectibleLookup?[items.Length];
            for (var c = 0; c < items.Length; c++)
            {
                lookups[c] = LookupFor(items[c]);
            }

            // A reward the story has not introduced and nobody owns: its placeholder, no art (1.20.0 N6).
            rewardRows = CollectionGrid.Rewards(rewards.All, lookups, gridKind < 0 ? null : gridKinds[gridKind], gridMissingOnAny, gridSearch, session.Spoilers);
            gridCountTexts = rewardRows.Select(r => UiFormat.Count(r.OwnedCount, items.Length)).ToArray();
            gridIcons = MoonlitIcons is { } icons ? rewardRows.Select(r => r.Entry is { } e && !string.Equals(r.Name, e.RewardName, StringComparison.Ordinal) ? icons.KindIcon(r.Kind) : RowIcon(icons, bundle, r)).ToArray() : [];
            rowCount = rewardRows.Count;
        }
        else
        {
            var quests = new List<QuestRecord>();
            foreach (var rowId in session.FeatureQuestIds)
            {
                if (bundle.Catalog.GetByRowId(rowId) is { } quest)
                {
                    quests.Add(quest);
                }
            }

            quests.Sort(static (a, b) => a.Expansion != b.Expansion ? a.Expansion.CompareTo(b.Expansion)
                : a.Level != b.Level ? a.Level.CompareTo(b.Level)
                : a.RowId.CompareTo(b.RowId));
            var columns = new IReadOnlyDictionary<uint, QuestState>?[items.Length];
            gridColumnFailed = new bool[items.Length];
            for (var c = 0; c < items.Length; c++)
            {
                columns[c] = QuestStatesFor(items[c], bundle, quests, out gridColumnFailed[c]);
            }

            var spoilers = session.Spoilers;
            questRows = CollectionGrid.Quests(quests, columns, quest => spoilers.DisplayName(quest), gridNotDoneOnAny, gridSearch);
            gridCountTexts = questRows.Select(r => UiFormat.Count(r.DoneCount, items.Length)).ToArray();
            rowCount = questRows.Count;
        }

        gridSummary = string.Format(CultureInfo.CurrentCulture, Strings.AltsGridSummaryFormat, rowCount, items.Length);
    }

    /// <summary>A character's saved owned answers: the viewed and the live character's snapshot in memory, else its stored file (cached per capture).</summary>
    private CollectibleLookup? LookupFor(CharacterItem item)
    {
        var snapshot = item.ContentId == session.ViewedContentId ? session.ViewedSnapshot
            : item.ContentId == session.LiveContentId ? session.LiveSnapshot
            : SnapshotFor(item);
        if (snapshot is null)
        {
            return null;
        }

        if (gridLookups.TryGetValue(item.ContentId, out var cached) && ReferenceEquals(cached.Snapshot, snapshot))
        {
            return cached.Lookup;
        }

        var lookup = CollectibleLookup.For(snapshot);
        gridLookups[item.ContentId] = (snapshot, lookup);
        return lookup;
    }

    /// <summary>
    /// A character's unlock quest states: the session's for the viewed and the live character; for another one, resolved
    /// on a worker once per capture time and catalog (null until it lands, the cells then read "being read"). Null with
    /// <paramref name="failed"/> set when its save could not be read or resolved: the cells then read "unreadable".
    /// </summary>
    private IReadOnlyDictionary<uint, QuestState>? QuestStatesFor(CharacterItem item, CatalogBundle bundle, IReadOnlyList<QuestRecord> quests, out bool failed)
    {
        failed = false;
        IReadOnlyDictionary<uint, QuestEvaluation>? known = item.ContentId == session.ViewedContentId ? session.States
            : item.ContentId == session.LiveContentId && session.LiveStates.Count > 0 ? session.LiveStates
            : null;
        if (known is not null)
        {
            var states = new Dictionary<uint, QuestState>(quests.Count);
            foreach (var quest in quests)
            {
                if (known.TryGetValue(quest.RowId, out var evaluation))
                {
                    states[quest.RowId] = evaluation.State;
                }
            }

            return states;
        }

        if (gridStates.TryGetValue(item.ContentId, out var cached) && cached.Taken == item.TakenUtc && ReferenceEquals(cached.Bundle, bundle))
        {
            failed = cached.States is null;
            return cached.States;
        }

        if (gridResolving.TryGetValue(item.ContentId, out var running) && running.Taken == item.TakenUtc && ReferenceEquals(running.Bundle, bundle))
        {
            return null;
        }

        if (SnapshotFor(item) is not { } snapshot)
        {
            gridStates[item.ContentId] = (item.TakenUtc, bundle, null);
            failed = true;
            return null;
        }

        var context = ContextFor(snapshot);
        var rowIds = quests.Select(static q => q.RowId).ToArray();
        gridResolving[item.ContentId] = (item.TakenUtc, bundle, Task.Run(() =>
        {
            var result = new Dictionary<uint, QuestState>(rowIds.Length);
            foreach (var rowId in rowIds)
            {
                if (bundle.Catalog.GetByRowId(rowId) is { } quest)
                {
                    result[rowId] = StateResolver.Resolve(quest, snapshot, bundle.Catalog, context).State;
                }
            }

            return (IReadOnlyDictionary<uint, QuestState>)result;
        }));
        return null;
    }

    /// <summary>Takes the quest states that finished on a worker; the rows rebuild with them.</summary>
    private void TakeGridResolves()
    {
        if (gridResolving.Count == 0)
        {
            return;
        }

        List<ulong>? done = null;
        foreach (var (id, running) in gridResolving)
        {
            if (running.Task.IsCompleted)
            {
                (done ??= []).Add(id);
            }
        }

        if (done is null)
        {
            return;
        }

        foreach (var id in done)
        {
            var running = gridResolving[id];
            gridResolving.Remove(id);
            IReadOnlyDictionary<uint, QuestState>? states = null;
            if (running.Task.IsCompletedSuccessfully)
            {
                states = running.Task.Result;
            }
            else
            {
                log.Warning(running.Task.Exception?.GetBaseException(), "Character {ContentId} could not be evaluated for the collection grid", id);
            }

            gridStates[id] = (running.Taken, running.Bundle, states);
        }

        gridResolved++;
    }

    private void DrawGridTable(UiState ui, int rowCount)
    {
        var columns = gridColumns;
        // Two fixed columns (the name, then how many have it) and one per character; ImGui allows 512 in all.
        var characterColumns = Math.Min(columns.Length, 500);
        const ImGuiTableFlags Flags = ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg
                                      | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingFixedFit;
        using var table = ImRaii.Table("##collectionGrid", 2 + characterColumns, Flags, new Vector2(-1f, -1f));
        if (!table)
        {
            return;
        }

        // ScrollY gives the table its own inner window inside the centre column (own font scale 1): it scales itself.
        UiMetrics.ApplyFontScale();
        var line = ImGui.GetTextLineHeight();
        var glyph = UiMetrics.InlineGlyphSize(line);
        var cellWidth = MathF.Max(UiMetrics.Px(GridColumnLogical), glyph * 2f);
        ImGui.TableSetupScrollFreeze(2, 1);
        ImGui.TableSetupColumn(gridMode == -1 ? Strings.AltsGridColumnReward : Strings.AltsGridColumnQuest, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoHide, UiMetrics.Px(GridNameLogical));
        ImGui.TableSetupColumn(Strings.AltsGridColumnCount, ImGuiTableColumnFlags.WidthFixed, MathF.Max(UiMetrics.Px(48f), ImGui.CalcTextSize("000/000").X));
        for (var c = 0; c < characterColumns; c++)
        {
            ImGui.TableSetupColumn(gridHeaders[c], ImGuiTableColumnFlags.WidthFixed, cellWidth);
        }

        // The header row by hand, so each character's header says who it is and how old its save is.
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
        ImGui.TableSetColumnIndex(0);
        ImGui.TableHeader(gridMode == -1 ? Strings.AltsGridColumnReward : Strings.AltsGridColumnQuest);
        ImGui.TableSetColumnIndex(1);
        ImGui.TableHeader(Strings.AltsGridColumnCount);
        for (var c = 0; c < characterColumns; c++)
        {
            ImGui.TableSetColumnIndex(2 + c);
            ImGui.TableHeader(gridHeaders[c]);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(gridColumnTips[c]);
            }
        }

        if (!gridClipperCreated)
        {
            gridClipper = ImGui.ImGuiListClipper();
            gridClipperCreated = true;
        }

        var rowHeight = MathF.Max(line, glyph) + (ImGui.GetStyle().CellPadding.Y * 2f);
        gridClipper.Begin(rowCount, rowHeight);
        while (gridClipper.Step())
        {
            for (var r = gridClipper.DisplayStart; r < gridClipper.DisplayEnd; r++)
            {
                using var rowId = ImRaii.PushId(r);
                ImGui.TableNextRow(ImGuiTableRowFlags.None, rowHeight);
                if (gridMode == -1)
                {
                    DrawRewardRow(ui, rewardRows[r], r < gridIcons.Length ? gridIcons[r] : null, gridCountTexts[r], characterColumns, glyph);
                }
                else
                {
                    DrawQuestRow(ui, questRows[r], gridCountTexts[r], characterColumns, glyph);
                }
            }
        }

        gridClipper.End();
    }

    private void DrawRewardRow(UiState ui, CollectionGridRow row, NodeIcon? icon, string count, int characterColumns, float glyph)
    {
        ImGui.TableNextColumn();
        if (icon is { } shown)
        {
            // The reward's own art at the line's height, so the row keeps its height and the name its baseline (UI-5d).
            DrawGridIcon(shown, MathF.Round(ImGui.GetTextLineHeight()));
        }

        GridName(ui, row.Name, row.QuestRowId);
        ImGui.TableNextColumn();
        ImGui.TextDisabled(count);
        for (var c = 0; c < characterColumns; c++)
        {
            ImGui.TableNextColumn();
            var cell = row.Cells[c];
            Marks.DrawInline(cell switch { OwnedCell.Owned => Mark.Check, OwnedCell.Missing => Mark.Cross, _ => Mark.Unknown }, glyph);
            if (ImGui.IsItemHovered())
            {
                var format = cell switch
                {
                    OwnedCell.Owned => Strings.AltsGridCellOwnedFormat,
                    OwnedCell.Missing => Strings.AltsGridCellMissingFormat,
                    _ => Strings.AltsGridCellUnknownFormat,
                };
                UiMetrics.Tooltip(row.Name, string.Format(CultureInfo.CurrentCulture, format, gridColumns[c].Name));
            }
        }
    }

    private void DrawQuestRow(UiState ui, QuestGridRow row, string count, int characterColumns, float glyph)
    {
        ImGui.TableNextColumn();
        GridName(ui, row.Name, row.Quest.RowId);
        ImGui.TableNextColumn();
        ImGui.TextDisabled(count);
        for (var c = 0; c < characterColumns; c++)
        {
            ImGui.TableNextColumn();
            if (row.Cells[c] is { } state)
            {
                MoonGlyph.DrawInline(state, glyph);
                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(row.Name, string.Format(CultureInfo.CurrentCulture, Strings.AltsGridCellStateFormat, gridColumns[c].Name, Strings.StateName(state, row.Quest)));
                }
            }
            else
            {
                Marks.DrawInline(Mark.Unknown, glyph);
                if (ImGui.IsItemHovered())
                {
                    var format = c < gridColumnFailed.Length && gridColumnFailed[c] ? Strings.AltsGridCellUnreadableFormat : Strings.AltsGridCellPendingFormat;
                    UiMetrics.Tooltip(row.Name, string.Format(CultureInfo.CurrentCulture, format, gridColumns[c].Name));
                }
            }
        }
    }

    /// <summary>A reward row's icon: the reward's own art (a mount's, a minion's), else its kind's menu icon.</summary>
    private static NodeIcon RowIcon(MoonlitIconResolver icons, CatalogBundle bundle, CollectionGridRow row)
    {
        var own = row.Entry is { } entry ? icons.Resolve(bundle.Catalog.GetByRowId(row.QuestRowId), entry) : 0u;
        return own != 0 ? NodeIcon.Game(own) : icons.KindIcon(row.Kind);
    }

    /// <summary>An icon <paramref name="size"/> across leading the item after it on the line (a reward's name, a kind in the filter).</summary>
    private void DrawGridIcon(NodeIcon icon, float size)
    {
        var min = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(size, size));
        if (textures is not null && ImGui.IsItemVisible())
        {
            Orbit.DrawIcon(ImGui.GetWindowDrawList(), textures, icon, min, min + new Vector2(size, size));
        }

        ImGui.SameLine(0f, MathF.Round(UiMetrics.Px(LeadIconGapLogical)));
    }

    /// <summary>The row's name: a click selects its quest, so the detail pane shows where the reward comes from.</summary>
    private static void GridName(UiState ui, string name, uint questRowId)
    {
        if (Chrome.EllipsisSelectable(name, ui.SelectedRowId == questRowId, 0f, out var cut) && questRowId != 0)
        {
            ui.SelectedRowId = questRowId;
        }

        if (cut && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(name);
        }
    }

    private readonly record struct GridKey(
        int Version,
        int Roster,
        int Mode,
        int Kind,
        bool MissingOnAny,
        bool NotDoneOnAny,
        string Search,
        CatalogBundle? Bundle,
        UniqueRewardCatalog? Rewards,
        int Resolved,
        long Minute,
        int Art);
}
