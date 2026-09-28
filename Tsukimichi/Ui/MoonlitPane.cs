using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Tsukimichi.Config;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// Moonlit treasures (spec §7): quests whose rewards exist nowhere else. <see cref="DrawLeft"/> lists reward kinds with
/// obtained/total and a filling moon; <see cref="DrawMain"/> is the toolbar plus the reward table. The pane owns the
/// user's unique/not-unique overrides (<c>user/overrides.json</c>) and the merged <see cref="UniqueRewardCatalog"/>,
/// which the detail pane and the settings window reach through <see cref="IUniqueOverrides"/>. "Not unique (hide)…"
/// in a row's context menu goes through the same <see cref="VerdictPrompt"/> as the detail pane's "Mark as unique…";
/// quests hidden that way stay in the row array as struck-through rows the Yours confidence filter lists, so their
/// context menu can restore them.
/// <para>
/// A row whose reward the FFXIV Online Store also sells (entry OtherSources carries OnlineStore) wears a small Dusk
/// "Store only" mark; the persisted "Hide store re-sells" toggle drops those rows and leaves them out of every count.
/// Row arrays and every label are built once per catalog build; obtained states and the filtered index refresh only
/// when <see cref="SessionState.Version"/>, the kind, the toggles or the filter text change. Nothing allocates per frame
/// in the table body except tooltips on hover. The list clipper lives as long as the pane; <see cref="Dispose"/>
/// destroys it and unsubscribes from the session.
/// </para>
/// </summary>
public sealed class MoonlitPane : IDisposable, IUniqueOverrides
{
    private const int FilterMaxLength = 128;

    /// <summary>Font size of the "Store only" mark relative to the row's text.</summary>
    private const float SmallTextScale = 0.85f;

    /// <summary>The combo next to "Hide obtained": which rows to keep by confidence, or only the unreadable ones.</summary>
    public enum ConfidenceFilter
    {
        Any,
        Static,
        Curated,
        Yours,
        UnknownObtained,
    }

    /// <summary>Combo labels in <see cref="ConfidenceFilter"/> order.</summary>
    private static readonly string[] ConfidenceFilterItems =
    [
        Strings.MoonlitConfidenceAny,
        Strings.MoonlitConfidenceStaticOnly,
        Strings.MoonlitConfidenceCuratedOnly,
        Strings.MoonlitConfidenceYoursOnly,
        Strings.MoonlitConfidenceUnknownObtained,
    ];

    private readonly SessionState session;
    private readonly ITextureProvider textures;
    private readonly GameLinks links;
    private readonly RewardUnlockReader unlocks;
    private readonly PluginPaths paths;
    private readonly IPluginLog log;
    private readonly Configuration settings;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly Dictionary<uint, UniqueOverride> overrides;
    private readonly VerdictPrompt verdict = new(Strings.MoonlitVerdictPopup);
    private int overridesVersion;

    /// <summary>Session-only: which confidence (or the unreadable rows) the table shows.</summary>
    private ConfidenceFilter confidenceFilter = ConfidenceFilter.Any;

    private ImGuiListClipperPtr clipper;
    private bool clipperCreated;

    private UniqueRewardCatalog catalog = UniqueRewardCatalog.Empty;
    private bool catalogDirty = true;
    private int catalogBuild;

    private Row[] rows = [];
    private int uniqueCount;
    private int storeCount;
    private int rowsBuild = -1;
    private CatalogBundle? rowsBundle;

    private int obtainedVersion = -1;
    private int obtainedBuild = -1;
    private bool countsHideStore;
    private readonly KindItem allItem = new(null, Strings.MoonlitAllKinds);
    private KindItem[] kindItems = [];
    private int kindsBuild = -1;

    // Per-kind counts from the last RefreshObtained, indexed by RewardKind; CountsFor reads them.
    private readonly int[] kindObtained = new int[KindCount];
    private readonly int[] kindTotal = new int[KindCount];
    private readonly int[] kindUnknown = new int[KindCount];

    private int[] visible = [];
    private int visibleCount;
    private VisibleKey visibleKey;
    private string visibleSummary = string.Empty;
    private string filterText = string.Empty;

    public MoonlitPane(SessionState session, ITextureProvider textures, RewardUnlockReader unlocks, PluginPaths paths, IPluginLog log, IDataManager data, Configuration settings, IDalamudPluginInterface pluginInterface, GameLinks links)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.textures = textures ?? throw new ArgumentNullException(nameof(textures));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        Icons = new MoonlitIconResolver(data ?? throw new ArgumentNullException(nameof(data)), log);

        var warnings = new List<string>();
        overrides = OverridesFile.Load(paths.OverridesFile, warnings);
        foreach (var warning in warnings)
        {
            log.Warning("Overrides: {Warning}", warning);
        }

        // "Delete all data" removes user/overrides.json; re-read it so hidden quests reappear.
        session.DataDeleted += ReloadOverrides;
    }

    public void Dispose()
    {
        session.DataDeleted -= ReloadOverrides;
        if (clipperCreated)
        {
            clipper.Destroy();
            clipperCreated = false;
        }
    }

    /// <summary>The merged unique-reward catalog, rebuilt lazily after an override change. Safe to call from any pane.</summary>
    public UniqueRewardCatalog Catalog
    {
        get
        {
            EnsureCatalog();
            return catalog;
        }
    }

    /// <summary>The user's overrides by quest row id.</summary>
    public IReadOnlyDictionary<uint, UniqueOverride> Overrides => overrides;

    IReadOnlyDictionary<uint, UniqueOverride> IUniqueOverrides.All => overrides;

    int IUniqueOverrides.Version => overridesVersion;

    /// <summary>The user's verdict for a quest, or null when the shipped data applies.</summary>
    public UniqueOverride? Get(uint rowId) => overrides.TryGetValue(rowId, out var stored) ? stored : null;

    void IUniqueOverrides.Clear(uint rowId) => ClearOverride(rowId);

    void IUniqueOverrides.ClearAll() => ClearAllOverrides();

    void IUniqueOverrides.Set(uint rowId, bool unique, string? note) => SetOverride(rowId, unique, note);

    /// <summary>Icon lookup for reward entries (quest reward list first, then per-kind sheet fallbacks); shared with Wotsit.</summary>
    public MoonlitIconResolver Icons { get; }

    /// <summary>
    /// Marks a quest unique (a note names the reward) or not unique (hidden from the Moonlit view), saves
    /// <c>user/overrides.json</c> and schedules a catalog rebuild. Intended for the detail pane's "Mark quest unique".
    /// </summary>
    public void SetOverride(uint rowId, bool unique, string? note)
    {
        overrides[rowId] = new UniqueOverride(unique, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), DateTime.UtcNow);
        SaveOverrides();
    }

    /// <summary>Removes every verdict (Settings › Data › Restore all) so the shipped data applies everywhere again.</summary>
    public void ClearAllOverrides()
    {
        if (overrides.Count == 0)
        {
            return;
        }

        overrides.Clear();
        SaveOverrides();
    }

    /// <summary>Removes the user's verdict for a quest so the shipped data applies again.</summary>
    public void ClearOverride(uint rowId)
    {
        if (overrides.Remove(rowId))
        {
            SaveOverrides();
        }
    }

    /// <summary>
    /// Obtained/total/unknown for one reward kind on the viewed character, with the same obtained logic the table
    /// uses. Memoized per <see cref="SessionState.Version"/>; the Characters dashboard reads it every frame.
    /// </summary>
    public UniqueRewardCounts CountsFor(RewardKind kind)
    {
        Refresh();
        var k = (int)kind;
        return (uint)k < KindCount
            ? new UniqueRewardCounts(kindObtained[k], kindTotal[k], kindUnknown[k])
            : default;
    }

    /// <summary>Re-reads <c>user/overrides.json</c>, e.g. after the file was deleted or replaced outside the pane.</summary>
    public void ReloadOverrides()
    {
        var warnings = new List<string>();
        var loaded = OverridesFile.Load(paths.OverridesFile, warnings);
        foreach (var warning in warnings)
        {
            log.Warning("Overrides: {Warning}", warning);
        }

        overrides.Clear();
        foreach (var (rowId, stored) in loaded)
        {
            overrides[rowId] = stored;
        }

        catalogDirty = true;
        overridesVersion++;
    }

    /// <summary>Left column: reward kinds with obtained/total and a filling moon; "All" on top.</summary>
    public void DrawLeft(UiState ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        using var id = ImRaii.PushId("moonlitLeft");
        Refresh();

        if (rows.Length == 0)
        {
            ImGui.TextWrapped(Strings.MoonlitNoData);
            return;
        }

        var line = ImGui.GetTextLineHeight();
        var countWidth = ImGui.CalcTextSize("9999/9999").X;
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        using (var table = ImRaii.Table("##moonlitKinds", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX))
        {
            if (!table)
            {
                return;
            }

            ImGui.TableSetupColumn("##moon", ImGuiTableColumnFlags.WidthFixed, line * 1.4f);
            ImGui.TableSetupColumn("##name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("##count", ImGuiTableColumnFlags.WidthFixed, countWidth);

            DrawKindRow(ui, allItem, -1);
            for (var i = 0; i < kindItems.Length; i++)
            {
                DrawKindRow(ui, kindItems[i], i);
            }
        }

        ui.RecordSpan(UiRects.MoonlitKinds, start, width);
    }

    /// <summary>Whether rows the Online Store also sells are dropped from the table and the counts (Configuration.MoonlitHideStoreResells).</summary>
    public bool HideStoreResells => settings.MoonlitHideStoreResells;

    /// <summary>Center column: toolbar (hide obtained, hide store re-sells, filter) and the reward table with a list clipper.</summary>
    public void DrawMain(UiState ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        using var id = ImRaii.PushId("moonlitMain");
        Refresh();

        using (Theme.PushText(Theme.Dusk))
        {
            ImGui.TextUnformatted(Strings.MoonlitSubtitle);
        }

        var hide = ui.MoonlitHideObtained;
        if (ImGui.Checkbox(Strings.MoonlitHideObtainedLabel, ref hide))
        {
            ui.MoonlitHideObtained = hide;
        }

        ImGui.SameLine();
        var hideStore = settings.MoonlitHideStoreResells;
        if (ImGui.Checkbox(Strings.MoonlitHideStoreResellsLabel, ref hideStore))
        {
            settings.MoonlitHideStoreResells = hideStore;
            settings.Save(pluginInterface);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.MoonlitHideStoreResellsTooltip);
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(UiMetrics.Px(150f));
        DrawConfidenceCombo();

        ImGui.SameLine();
        ImGui.SetNextItemWidth(UiMetrics.Px(220f));
        ImGui.InputTextWithHint("##moonlitFilter", Strings.MoonlitFilterHint, ref filterText, FilterMaxLength);

        RefreshVisible(ui);
        ImGui.SameLine();
        ImGui.TextDisabled(visibleSummary);
        if (!session.IsLive)
        {
            ImGui.SameLine();
            using (Theme.PushText(Theme.Dusk))
            {
                ImGui.TextUnformatted(Strings.MoonlitOfflineHint);
            }
        }

        if (verdict.UndoShowing)
        {
            ImGui.SameLine();
            verdict.DrawUndo(this);
        }

        // The verdict popup is begun here, in the centre column's scope, because the context menu that requests it
        // lives inside the table's inner window and closes before the popup could be shown from there.
        verdict.Draw(this, UiMetrics.Scale);

        if (rows.Length == 0)
        {
            ImGui.TextWrapped(Strings.MoonlitNoData);
            return;
        }

        if (visibleCount == 0)
        {
            ImGui.TextDisabled(Strings.MoonlitNothingMatches);
            return;
        }

        // Every column is reorderable (no NoReorder anywhere). The two glyph columns are fixed and not resizable, but
        // wide enough that their header can be grabbed away from the neighbouring resize border: ImGui claims the
        // 4 px either side of a border for resizing, and a header only as wide as the glyph left almost nothing
        // else to drag, so a reorder attempt on State became a resize of the stretch column before it.
        const ImGuiTableFlags Flags = ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH
                                      | ImGuiTableFlags.Resizable | ImGuiTableFlags.Reorderable | ImGuiTableFlags.Hideable
                                      | ImGuiTableFlags.SizingStretchProp;
        using var table = ImRaii.Table("##moonlitTable", 6, Flags, new Vector2(-1f, -1f));
        if (!table)
        {
            return;
        }

        // ScrollY gives the table its own inner window, so this is the table's rectangle; that window sits inside the
        // centre column (own font scale 1), so it scales itself before anything is measured.
        ui.RecordWindow(UiRects.MoonlitTable);
        UiMetrics.ApplyFontScale();
        var line = ImGui.GetTextLineHeight();
        var glyphColumn = MathF.Max(UiMetrics.InlineGlyphSize(line) * 2f, UiMetrics.Px(44f));
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn(Strings.MoonlitColumnObtained, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, glyphColumn);
        ImGui.TableSetupColumn(Strings.MoonlitColumnReward, ImGuiTableColumnFlags.WidthStretch, 3f);
        ImGui.TableSetupColumn(Strings.MoonlitColumnKind, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(110f));
        ImGui.TableSetupColumn(Strings.MoonlitColumnQuest, ImGuiTableColumnFlags.WidthStretch, 3f);
        ImGui.TableSetupColumn(Strings.MoonlitColumnState, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, glyphColumn);
        ImGui.TableSetupColumn(Strings.MoonlitColumnConfidence, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(80f));
        // The glyph columns follow IconScale, which imgui.ini's saved widths do not track; re-asserted every frame
        // (a no-op once they agree) so a changed IconScale never clips the moons.
        ImGuiP.TableSetColumnWidth(0, glyphColumn);
        ImGuiP.TableSetColumnWidth(4, glyphColumn);
        ImGui.TableHeadersRow();

        if (!clipperCreated)
        {
            clipper = ImGui.ImGuiListClipper();
            clipperCreated = true;
        }

        clipper.Begin(visibleCount);
        while (clipper.Step())
        {
            for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                DrawRow(ui, rows[visible[i]], line);
            }
        }

        clipper.End();
    }

    /// <summary>The confidence filter; its popup opens from the centre column (own font scale 1), so it scales itself.</summary>
    private void DrawConfidenceCombo()
    {
        using var combo = ImRaii.Combo("##moonlitConfidence", ConfidenceFilterItems[(int)confidenceFilter]);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.MoonlitConfidenceFilterTooltip);
        }

        if (!combo)
        {
            return;
        }

        UiMetrics.ApplyFontScale();
        for (var i = 0; i < ConfidenceFilterItems.Length; i++)
        {
            if (ImGui.Selectable(ConfidenceFilterItems[i], i == (int)confidenceFilter))
            {
                confidenceFilter = (ConfidenceFilter)i;
            }
        }
    }

    private void DrawKindRow(UiState ui, KindItem item, int index)
    {
        using var id = ImRaii.PushId(index);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        if (item.AllUnknown)
        {
            // Nothing readable for this kind on the viewed character (logged out, a stored snapshot, or a kind the
            // reader cannot answer): a veiled moon says so instead of a misleading empty one.
            MoonGlyph.DrawInline(QuestState.Unknown, UiMetrics.InlineGlyphSize(ImGui.GetTextLineHeight()));
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.MoonlitObtainedUnknown);
            }
        }
        else
        {
            MoonGlyph.DrawFillingInline(item.Fraction, UiMetrics.InlineGlyphSize(ImGui.GetTextLineHeight()));
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(item.TooltipText);
            }
        }

        ImGui.TableNextColumn();
        var selected = ui.MoonlitKind == item.Kind;
        if (ImGui.Selectable(item.Name, selected, ImGuiSelectableFlags.SpanAllColumns))
        {
            ui.MoonlitKind = item.Kind;
        }

        ImGui.TableNextColumn();
        ImGui.TextDisabled(item.CountText);
    }

    private void DrawRow(UiState ui, Row row, float line)
    {
        using var id = ImRaii.PushId(row.Index);
        ImGui.TableNextRow();

        // Obtained.
        ImGui.TableNextColumn();
        MoonGlyph.DrawInline(row.ObtainedGlyph, UiMetrics.InlineGlyphSize(line));
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(row.ObtainedText);
        }

        // Icon and reward name; the row's context menu hangs off the name. A row hidden by the user's verdict is
        // Dusk and struck through, and says so on hover.
        ImGui.TableNextColumn();
        DrawIcon(row, UiMetrics.RowIconSize);
        ImGui.SameLine();
        using (Theme.PushText(Theme.Dusk, row.Hidden))
        {
            // The highlight follows the global selection, as in the Flight pane, so a quest picked from the detail
            // pane's path, another pane or chat lights its Moonlit row too, and an override never wipes it.
            if (ImGui.Selectable(row.Name, ui.SelectedRowId == row.Entry.QuestRowId))
            {
                ui.SelectedRowId = row.Entry.QuestRowId;
            }
        }

        if (row.Hidden)
        {
            StrikeThrough(row.Name);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.MoonlitHiddenTooltip);
            }
        }

        using (var menu = ImRaii.ContextPopupItem("ctx"))
        {
            if (menu)
            {
                DrawContextMenu(ui, row);
            }
        }

        if (row.StoreResell)
        {
            ImGui.SameLine();
            SmallDuskText(Strings.MoonlitStoreOnly);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.MoonlitStoreOnlyTooltip);
            }
        }

        // Kind.
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(row.KindName);

        // Quest: click reveals it in the Journal.
        ImGui.TableNextColumn();
        if (row.Quest is { } quest)
        {
            using (Theme.PushText(Theme.Dusk, row.Hidden))
            {
                if (ImGui.Selectable(row.QuestLabel))
                {
                    Reveal(ui, quest);
                }
            }

            if (row.Hidden)
            {
                StrikeThrough(row.QuestName);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.MoonlitShowInJournal);
            }
        }
        else
        {
            ImGui.TextDisabled(row.QuestName);
        }

        // Quest state for the viewed character.
        ImGui.TableNextColumn();
        var state = session.States.TryGetValue(row.Entry.QuestRowId, out var evaluation) ? evaluation.State : QuestState.Unknown;
        MoonGlyph.DrawInline(state, UiMetrics.InlineGlyphSize(line));
        if (ImGui.IsItemHovered())
        {
            UiMetrics.StateTooltip(state, evaluation, row.Quest, session.Names, session.States);
        }

        // Confidence badge: what it means, with the source under it.
        ImGui.TableNextColumn();
        using (Theme.PushText(row.ConfidenceColor))
        {
            ImGui.TextUnformatted(row.ConfidenceLabel);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(row.ConfidenceTooltip, row.SourceText);
        }
    }

    /// <summary>
    /// The reward's icon with the blown-up reward tooltip on hover (the source line under it), or, for a kind without
    /// sheet art, a faded veiled moon that says so: the Obtained column already shows whether the reward is owned.
    /// </summary>
    private void DrawIcon(Row row, float size)
    {
        if (row.Reward is { } reward)
        {
            var wrap = textures.GetFromGameIcon(new GameIconLookup(row.Icon)).GetWrapOrEmpty();
            ImGui.Image(wrap.Handle, new Vector2(size, size));
            if (ImGui.IsItemHovered())
            {
                RewardTooltip.Draw(reward, links, textures, row.SourceText);
            }
        }
        else
        {
            MoonGlyph.DrawVeiledInline(size, NoIconAlpha);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.MoonlitNoIconTooltip);
            }
        }
    }

    /// <summary>Alpha of the veiled stand-in where an icon would go: present but clearly not a state.</summary>
    private const float NoIconAlpha = 0.6f;

    private void DrawContextMenu(UiState ui, Row row)
    {
        var rowId = row.Entry.QuestRowId;
        if (row.Quest is { } quest && ImGui.MenuItem(Strings.MoonlitShowInJournal))
        {
            Reveal(ui, quest);
        }

        ImGui.Separator();
        if (overrides.ContainsKey(rowId))
        {
            if (ImGui.MenuItem(Strings.MoonlitRestoreOverride))
            {
                ClearOverride(rowId);
            }
        }
        else if (ImGui.MenuItem(Strings.MoonlitMarkNotUnique))
        {
            // Only requested here; the popup itself is begun in DrawMain once this menu has closed.
            verdict.Open(rowId, false, row.QuestName);
        }
    }

    /// <summary>Small Dusk text vertically centred on the current line, occupying its own width; hoverable through the reserved item.</summary>
    private static void SmallDuskText(string text)
    {
        var size = ImGui.GetFontSize() * SmallTextScale;
        var extent = ImGui.CalcTextSize(text) * SmallTextScale;
        var lineHeight = ImGui.GetTextLineHeight();
        var pos = ImGui.GetCursorScreenPos();
        pos.Y += MathF.Round((lineHeight - extent.Y) * 0.5f);
        ImGui.GetWindowDrawList().AddText(ImGui.GetFont(), size, pos, Theme.DuskU32, text, 0f);
        ImGui.Dummy(new Vector2(extent.X, lineHeight));
    }

    /// <summary>A Dusk hairline through the middle of the last item's text (its own width, not the whole cell).</summary>
    private static void StrikeThrough(string text)
    {
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var y = MathF.Round((min.Y + max.Y) * 0.5f);
        var right = MathF.Min(max.X, min.X + ImGui.CalcTextSize(text, true, -1f).X);
        ImGui.GetWindowDrawList().AddLine(new Vector2(min.X, y), new Vector2(right, y), Theme.DuskU32, UiMetrics.Hairline);
    }

    /// <summary>Shows a quest in the Journal tab scoped to its genre (or the Unlisted bucket); also used by Wotsit picks.</summary>
    internal static void Reveal(UiState ui, QuestRecord quest) => ui.Reveal(quest);

    /// <summary>Catalog, rows and obtained states, each only when its inputs changed.</summary>
    private void Refresh()
    {
        EnsureCatalog();
        if (rowsBuild != catalogBuild || !ReferenceEquals(rowsBundle, session.Bundle))
        {
            BuildRows();
        }

        if (obtainedVersion != session.Version || obtainedBuild != rowsBuild || countsHideStore != settings.MoonlitHideStoreResells)
        {
            RefreshObtained();
        }
    }

    private void EnsureCatalog()
    {
        if (!catalogDirty)
        {
            return;
        }

        catalog = UniqueRewardCatalog.Build(session.UniqueRewards, overrides, session.Curated);
        catalogDirty = false;
        catalogBuild++;
    }

    private void BuildRows()
    {
        var bundle = session.Bundle;
        var all = catalog.All;
        var hidden = catalog.Hidden;
        var built = new Row[all.Count + hidden.Count];
        for (var i = 0; i < all.Count; i++)
        {
            var entry = all[i];
            var quest = bundle?.Catalog.GetByRowId(entry.QuestRowId);
            built[i] = new Row(i, entry, quest, Icons.Resolve(quest, entry), hidden: false);
        }

        // Rows hidden by a "not unique" verdict follow the view so the Yours filter can list them for Restore.
        for (var j = 0; j < hidden.Count; j++)
        {
            var i = all.Count + j;
            var entry = hidden[j];
            var quest = bundle?.Catalog.GetByRowId(entry.QuestRowId);
            built[i] = new Row(i, entry, quest, Icons.Resolve(quest, entry), hidden: true);
        }

        uniqueCount = all.Count;
        storeCount = 0;
        for (var i = 0; i < all.Count; i++)
        {
            if (built[i].StoreResell)
            {
                storeCount++;
            }
        }

        rows = built;
        rowsBuild = catalogBuild;
        rowsBundle = bundle;
        obtainedVersion = -1;
    }

    /// <summary>Obtained state per row and the per-kind counts, once per session version (and per store toggle: hidden re-sells leave the counts).</summary>
    private void RefreshObtained()
    {
        var obtained = kindObtained;
        var total = kindTotal;
        var unknown = kindUnknown;
        Array.Clear(obtained);
        Array.Clear(total);
        Array.Clear(unknown);
        var hideStore = settings.MoonlitHideStoreResells;

        foreach (var row in rows)
        {
            row.SetObtained(unlocks.IsObtained(row.Entry));
            if (row.Hidden || (hideStore && row.StoreResell))
            {
                continue;
            }

            var k = (int)row.Entry.Kind;
            if ((uint)k < KindCount)
            {
                total[k]++;
                switch (row.Obtained)
                {
                    case true:
                        obtained[k]++;
                        break;
                    case null:
                        unknown[k]++;
                        break;
                }
            }
        }

        var allObtained = 0;
        var allTotal = 0;
        var allUnknown = 0;
        var kinds = catalog.Kinds;
        if (kindsBuild != rowsBuild)
        {
            kindsBuild = rowsBuild;
            kindItems = new KindItem[kinds.Count];
            for (var i = 0; i < kindItems.Length; i++)
            {
                kindItems[i] = new KindItem(kinds[i].Kind, Strings.MoonlitKindName(kinds[i].Kind));
            }
        }

        for (var i = 0; i < kindItems.Length; i++)
        {
            var k = (int)kinds[i].Kind;
            var o = (uint)k < KindCount ? obtained[k] : 0;
            var t = (uint)k < KindCount ? total[k] : kinds[i].Count;
            var u = (uint)k < KindCount ? unknown[k] : 0;
            kindItems[i].SetCounts(o, t, u);
            allObtained += o;
            allTotal += t;
            allUnknown += u;
        }

        allItem.SetCounts(allObtained, allTotal, allUnknown);
        obtainedVersion = session.Version;
        obtainedBuild = rowsBuild;
        countsHideStore = hideStore;
        visibleKey = default;
    }

    /// <summary>The filtered index array, rebuilt when the kind, the toggle, the filter text or the obtained states change.</summary>
    private void RefreshVisible(UiState ui)
    {
        var hideStore = settings.MoonlitHideStoreResells;
        var key = new VisibleKey(rowsBuild, obtainedVersion, ui.MoonlitKind, ui.MoonlitHideObtained, hideStore, confidenceFilter, filterText);
        if (key == visibleKey)
        {
            return;
        }

        visibleKey = key;
        if (visible.Length < rows.Length)
        {
            visible = new int[rows.Length];
        }

        var filter = filterText.Trim();
        var count = 0;
        var listed = 0;
        foreach (var row in rows)
        {
            if (ui.MoonlitKind is { } kind && row.Entry.Kind != kind)
            {
                continue;
            }

            if (ui.MoonlitHideObtained && row.Obtained == true)
            {
                continue;
            }

            if (hideStore && row.StoreResell)
            {
                continue;
            }

            if (row.Hidden)
            {
                // Hidden by the user's verdict: only the Yours filter lists it (struck through) so it can be restored.
                if (confidenceFilter != ConfidenceFilter.Yours)
                {
                    continue;
                }
            }
            else if (!PassesConfidence(confidenceFilter, row.Entry.Confidence, row.Obtained))
            {
                continue;
            }

            if (filter.Length != 0 && !row.Matches(filter))
            {
                continue;
            }

            visible[count++] = row.Index;
            if (!row.Hidden)
            {
                listed++;
            }
        }

        visibleCount = count;
        var denominator = hideStore ? uniqueCount - storeCount : uniqueCount;
        visibleSummary = listed.ToString(CultureInfo.InvariantCulture) + " / " + denominator.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Whether a row passes the confidence combo: a confidence match, or (Obtained not checked) an unreadable obtained state.</summary>
    internal static bool PassesConfidence(ConfidenceFilter filter, Confidence confidence, bool? obtained) => filter switch
    {
        ConfidenceFilter.Any => true,
        ConfidenceFilter.Static => confidence == Confidence.Static,
        ConfidenceFilter.Curated => confidence == Confidence.Curated,
        ConfidenceFilter.Yours => confidence == Confidence.UserOverride,
        ConfidenceFilter.UnknownObtained => obtained is null,
        _ => true,
    };

    private void SaveOverrides()
    {
        try
        {
            OverridesFile.Save(paths.OverridesFile, overrides);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Error(ex, "Could not save {Path}", paths.OverridesFile);
        }

        catalogDirty = true;
        overridesVersion++;
    }

    private static readonly int KindCount = Enum.GetValues<RewardKind>().Length;

    private static string ConfidenceLabel(Confidence confidence) => confidence switch
    {
        Confidence.Static => Strings.MoonlitConfidenceStatic,
        Confidence.Community => Strings.MoonlitConfidenceCommunity,
        Confidence.Curated => Strings.MoonlitConfidenceCurated,
        Confidence.UserOverride => Strings.MoonlitConfidenceUser,
        _ => confidence.ToString(),
    };

    private static string ConfidenceTooltip(Confidence confidence) => confidence switch
    {
        Confidence.Static => Strings.MoonlitBadgeStatic,
        Confidence.Community => Strings.MoonlitBadgeCommunity,
        Confidence.Curated => Strings.MoonlitBadgeCurated,
        Confidence.UserOverride => Strings.MoonlitBadgeUser,
        _ => Strings.MoonlitSourceUnknown,
    };

    private static Vector4 ConfidenceColor(Confidence confidence) => confidence switch
    {
        Confidence.Static => Theme.Silver,
        Confidence.Community => Theme.Dusk,
        Confidence.Curated => Theme.Moon,
        Confidence.UserOverride => Theme.Eclipse,
        _ => Theme.Veil,
    };

    /// <summary>One left-column line: a kind (null for All), its name and the current counts.</summary>
    private sealed class KindItem(RewardKind? kind, string name)
    {
        public RewardKind? Kind { get; } = kind;
        public string Name { get; } = name;
        public string CountText { get; private set; } = "0/0";

        /// <summary>Obtained over every entry of the kind (unknown entries count as not obtained).</summary>
        public float Fraction { get; private set; }

        /// <summary>True when no entry of the kind is readable, so the row shows a veiled moon instead of a fraction.</summary>
        public bool AllUnknown { get; private set; }

        /// <summary>The filling moon's hover text: the count with what it counts.</summary>
        public string TooltipText { get; private set; } = string.Empty;

        public void SetCounts(int obtained, int total, int unknown)
        {
            CountText = obtained.ToString(CultureInfo.InvariantCulture) + "/" + total.ToString(CultureInfo.InvariantCulture);
            TooltipText = string.Format(CultureInfo.CurrentCulture, Strings.MoonlitKindCountTooltipFormat, CountText);
            Fraction = total > 0 ? (float)obtained / total : 0f;
            AllUnknown = total > 0 && unknown == total;
        }
    }

    /// <summary>
    /// One table row with every label pre-materialized; only the obtained state changes after construction. A hidden
    /// row (kept out of the unique view by the user's verdict) wears the "yours" badge whatever its entry's confidence.
    /// </summary>
    private sealed class Row
    {
        public Row(int index, UniqueRewardEntry entry, QuestRecord? quest, uint icon, bool hidden)
        {
            Index = index;
            Entry = entry;
            Quest = quest;
            Icon = icon;
            Hidden = hidden;
            KindName = Strings.MoonlitKindName(entry.Kind);
            Name = string.IsNullOrWhiteSpace(entry.RewardName)
                ? KindName + " #" + entry.RewardId.ToString(CultureInfo.InvariantCulture)
                : entry.RewardName;
            QuestName = quest?.Name ?? Strings.MoonlitQuestPrefix + entry.QuestRowId.ToString(CultureInfo.InvariantCulture);
            QuestLabel = QuestName + "##q";
            ConfidenceLabel = hidden ? Strings.MoonlitConfidenceUser : MoonlitPane.ConfidenceLabel(entry.Confidence);
            ConfidenceColor = hidden ? Theme.Eclipse : MoonlitPane.ConfidenceColor(entry.Confidence);
            ConfidenceTooltip = hidden ? Strings.MoonlitBadgeHidden : MoonlitPane.ConfidenceTooltip(entry.Confidence);
            SourceText = string.IsNullOrWhiteSpace(entry.Source) ? Strings.MoonlitSourceUnknown : entry.Source;
            StoreResell = entry.SoldOnOnlineStore;
            Reward = icon == 0 ? null : RewardFor(quest, entry, icon, Name);
        }

        /// <summary>
        /// The reward the icon tooltip describes: the quest's own reward entry (same item, else same kind and id; what
        /// <see cref="MoonlitIconResolver.FromQuestRewards"/> matched), or one made from the catalog entry when the
        /// icon came from the sheets instead.
        /// </summary>
        private static RewardRef RewardFor(QuestRecord? quest, UniqueRewardEntry entry, uint icon, string name)
        {
            if (quest is not null)
            {
                foreach (var reward in quest.Rewards)
                {
                    if (entry.ItemId != 0 && reward.ItemId == entry.ItemId && reward.Icon == icon)
                    {
                        return reward;
                    }
                }

                foreach (var reward in quest.Rewards)
                {
                    if (entry.RewardId != 0 && reward.Kind == entry.Kind && reward.Id == entry.RewardId && reward.Icon == icon)
                    {
                        return reward;
                    }
                }
            }

            return new RewardRef(entry.Kind, entry.RewardId, entry.ItemId, 1, name, icon);
        }

        public int Index { get; }
        public UniqueRewardEntry Entry { get; }
        public QuestRecord? Quest { get; }
        public uint Icon { get; }
        public bool Hidden { get; }
        public string Name { get; }
        public string KindName { get; }
        public string QuestName { get; }
        public string QuestLabel { get; }
        public string ConfidenceLabel { get; }
        public Vector4 ConfidenceColor { get; }
        public string ConfidenceTooltip { get; }
        public string SourceText { get; }

        /// <summary>The FFXIV Online Store also sells this reward (entry OtherSources carries OnlineStore).</summary>
        public bool StoreResell { get; }

        /// <summary>What the icon's tooltip describes; null when the row has no icon (the veiled stand-in is drawn instead).</summary>
        public RewardRef? Reward { get; }

        public bool? Obtained { get; private set; }
        public QuestState ObtainedGlyph { get; private set; } = QuestState.Unknown;
        public string ObtainedText { get; private set; } = Strings.MoonlitObtainedUnknown;

        public void SetObtained(bool? obtained)
        {
            Obtained = obtained;
            (ObtainedGlyph, ObtainedText) = obtained switch
            {
                true => (QuestState.Completed, Strings.MoonlitObtainedYes),
                false => (QuestState.Blocked, Strings.MoonlitObtainedNo),
                null => (QuestState.Unknown, Strings.MoonlitObtainedUnknown),
            };
        }

        public bool Matches(string filter) =>
            Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || QuestName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || KindName.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct VisibleKey(int Build, int Version, RewardKind? Kind, bool HideObtained, bool HideStore, ConfidenceFilter Confidence, string Filter);
}

/// <summary>
/// Icon for a unique-reward entry. The quest's own reward list is tried first (same item, else same kind and id);
/// otherwise the kind decides: duty unlocks and instances show their ContentFinderCondition's content-type icon,
/// jobs the 062100-series job icon, aether currents the attunement crystal (060033), traits, achievements and blue
/// mage spells their sheet icon. Titles and system unlocks have no sheet icon and keep the veiled moon (0).
/// Sheet lookups are memoized per (kind, id); a sheet failure logs once and reads as no icon.
/// </summary>
public sealed class MoonlitIconResolver(IDataManager data, IPluginLog log)
{
    /// <summary>The aether current attunement crystal in the 060000 icon set (verified against the shipped textures).</summary>
    public const uint AetherCurrentIcon = 60033;

    /// <summary>First job icon in the 062000 set: 062101 Gladiator … 062142 Pictomancer, offset by ClassJob row id.</summary>
    public const uint ClassJobIconBase = 62100;

    /// <summary><c>ContentFinderCondition.ContentLinkType</c> value whose <c>Content</c> is an InstanceContent row.</summary>
    private const byte InstanceContentLink = 1;

    private readonly Dictionary<(RewardKind Kind, uint Id), uint> memo = [];
    private Dictionary<uint, uint>? contentTypeIconByCondition;
    private Dictionary<uint, uint>? conditionByInstance;
    private bool warned;

    /// <summary>Icon id for the entry, or 0 when none is known.</summary>
    public uint Resolve(QuestRecord? quest, UniqueRewardEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var fromQuest = FromQuestRewards(quest, entry);
        if (fromQuest != 0)
        {
            return fromQuest;
        }

        if (entry.RewardId == 0)
        {
            return 0;
        }

        var key = (entry.Kind, entry.RewardId);
        if (memo.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var icon = 0u;
        try
        {
            icon = entry.Kind switch
            {
                RewardKind.DutyUnlock => ConditionIcon(entry.RewardId),
                RewardKind.Instance => ConditionForInstance(entry.RewardId) is { } condition ? ConditionIcon(condition) : 0u,
                RewardKind.ClassJob => data.GetExcelSheet<ClassJob>()?.GetRowOrDefault(entry.RewardId) is not null ? ClassJobIconBase + entry.RewardId : 0u,
                RewardKind.AetherCurrent => AetherCurrentIcon,
                RewardKind.Trait => Positive(data.GetExcelSheet<Trait>()?.GetRowOrDefault(entry.RewardId)?.Icon),
                RewardKind.Achievement => data.GetExcelSheet<Achievement>()?.GetRowOrDefault(entry.RewardId)?.Icon ?? 0u,
                RewardKind.BlueMageSpell => data.GetExcelSheet<AozAction>()?.GetRowOrDefault(entry.RewardId)?.Action.ValueNullable?.Icon ?? 0u,
                _ => 0u,
            };
        }
        catch (Exception ex)
        {
            WarnOnce(ex, entry.Kind);
        }

        memo[key] = icon;
        return icon;
    }

    /// <summary>The reward's icon from the quest's reward list: same item, else same kind and id. Zero when unknown.</summary>
    public static uint FromQuestRewards(QuestRecord? quest, UniqueRewardEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (quest is null)
        {
            return 0;
        }

        if (entry.ItemId != 0)
        {
            foreach (var reward in quest.Rewards)
            {
                if (reward.ItemId == entry.ItemId && reward.Icon != 0)
                {
                    return reward.Icon;
                }
            }
        }

        if (entry.RewardId != 0)
        {
            foreach (var reward in quest.Rewards)
            {
                if (reward.Kind == entry.Kind && reward.Id == entry.RewardId && reward.Icon != 0)
                {
                    return reward.Icon;
                }
            }
        }

        return 0;
    }

    /// <summary>Sheet icon columns typed as int: negative or missing reads as no icon.</summary>
    private static uint Positive(int? icon) => icon is > 0 ? (uint)icon.Value : 0u;

    private uint ConditionIcon(uint conditionId)
    {
        EnsureConditions();
        return contentTypeIconByCondition!.GetValueOrDefault(conditionId);
    }

    private uint? ConditionForInstance(uint instanceContentId)
    {
        EnsureConditions();
        return conditionByInstance!.TryGetValue(instanceContentId, out var condition) ? condition : null;
    }

    /// <summary>ContentFinderCondition read once: its ContentType icon per row, and the row per InstanceContent it links.</summary>
    private void EnsureConditions()
    {
        if (contentTypeIconByCondition is not null)
        {
            return;
        }

        var icons = new Dictionary<uint, uint>();
        var byInstance = new Dictionary<uint, uint>();
        try
        {
            var sheet = data.GetExcelSheet<ContentFinderCondition>();
            if (sheet is not null)
            {
                foreach (var row in sheet)
                {
                    var icon = row.ContentType.ValueNullable?.Icon ?? 0u;
                    if (icon != 0)
                    {
                        icons[row.RowId] = icon;
                    }

                    if (row.ContentLinkType == InstanceContentLink && row.Content.RowId != 0)
                    {
                        byInstance.TryAdd(row.Content.RowId, row.RowId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            WarnOnce(ex, RewardKind.DutyUnlock);
        }

        contentTypeIconByCondition = icons;
        conditionByInstance = byInstance;
    }

    private void WarnOnce(Exception ex, RewardKind kind)
    {
        if (warned)
        {
            log.Debug(ex, "Icon lookup for {Kind} failed", kind);
            return;
        }

        warned = true;
        log.Warning(ex, "Icon lookup for {Kind} failed; the veiled moon stands in", kind);
    }
}
