using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Tsukimichi.Config;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Ui;
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
/// A row whose reward can be had outside the quest wears one small Dusk mark per such source: "Store only" when the
/// FFXIV Online Store also sells it (entry OtherSources carries OnlineStore), "Also drops" when a duty also drops it
/// (DungeonDrop; the tooltip names the duties). The persisted "Hide rewards found elsewhere" toggle
/// (Configuration.MoonlitHideStoreResells) drops both kinds of row and leaves them out of every count.
/// Row arrays and every label are built once per catalog build; obtained states and the filtered index refresh only
/// when <see cref="SessionState.Version"/>, the kind, the toggles or the filter text change. Nothing allocates per frame
/// in the table body except tooltips on hover. The list clipper lives as long as the pane; <see cref="Dispose"/>
/// destroys it and unsubscribes from the session.
/// </para>
/// <para>
/// The Moon Road look (feature plan v4 V5, proposal §7.5): the kinds list draws each kind's official menu icon (the
/// MainCommand icon, else an atlas glyph) inside an orbit of obtained/total at Flair Full and Quiet, and the centre
/// column opens with the kind in the Title role over a brass rule. A toggle beside it switches to the gallery
/// (remembered in <c>Configuration.MoonlitGallery</c>): 64 px hi-res reward icons on the sunken ground in columns of
/// 96 px, the name under each in two lines at most and the quest in the secondary tone; an obtained reward has a gold
/// frame and a checked full-moon pip, any other the quest's state moon as its pip (a shape, never colour alone). Tiles
/// are real, focusable items with the same click, context menu and "…" menu as the table rows, so verdicts and their
/// undo work in either view. At Full each tile fades in once (never under Reduce motion).
/// </para>
/// </summary>
public sealed class MoonlitPane : IDisposable, IUniqueOverrides
{
    private const int FilterMaxLength = 128;

    /// <summary>Font size of the "Store only" and "Also drops" marks relative to the row's text.</summary>
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
    private static string[] ConfidenceFilterItems => confidenceFilterItemsText.Value;

    private static readonly Localization.LocArray confidenceFilterItemsText = new(static () =>
        [
        Strings.MoonlitConfidenceAny,
        Strings.MoonlitConfidenceStaticOnly,
        Strings.MoonlitConfidenceCuratedOnly,
        Strings.MoonlitConfidenceYoursOnly,
        Strings.MoonlitConfidenceUnknownObtained,
    ]);

    private readonly SessionState session;
    private readonly ITextureProvider textures;
    private readonly GameLinks links;
    private readonly RewardUnlockReader unlocks;
    private readonly PluginPaths paths;
    private readonly IPluginLog log;
    private readonly Configuration settings;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly Dictionary<uint, UniqueOverride> overrides;

    /// <summary>Multibox (D11): quests whose verdict changed here and is not queued for a save yet; a save merges only these into the file on disk.</summary>
    private readonly HashSet<uint> overridesTouched = [];
    private readonly VerdictPrompt verdict = new(Strings.MoonlitVerdictPopup);
    private int overridesVersion;

    /// <summary>The quests of the save on the background writer; null while none is. One save is in flight at a time.</summary>
    private HashSet<uint>? overridesInFlight;

    /// <summary>Bumped by "Delete all data": a save or reload queued before it is ignored when it lands.</summary>
    private int overridesGeneration;

    /// <summary>Session-only: which confidence (or the unreadable rows) the table shows.</summary>
    private ConfidenceFilter confidenceFilter = ConfidenceFilter.Any;

    private ImGuiListClipperPtr clipper;
    private bool clipperCreated;

    private UniqueRewardCatalog catalog = UniqueRewardCatalog.Empty;
    private bool catalogDirty = true;
    private int catalogBuild;

    private Row[] rows = [];
    private int uniqueCount;
    private int elsewhereCount;
    private int rowsBuild = -1;
    private CatalogBundle? rowsBundle;
    private int rowsSpoilers;

    private int obtainedVersion = -1;
    private int obtainedBuild = -1;
    private int obtainedAchievementState = -1;
    private bool countsHideStore;
    private KindItem allItem = new(null, Strings.MoonlitAllKinds, NodeIcon.Of(MoonlitKindIcons.Glyph(null)));

    /// <summary>Motion tag of the gallery tiles' fade-in ("GALL"); the row index is the low half.</summary>
    private const uint GalleryTag = 0x4741_4C4C;

    /// <summary>How long a gallery tile takes to fade in the first time it shows (Flair Full).</summary>
    private const float TileFadeSeconds = 0.3f;

    /// <summary>Per row: whether its gallery tile has been shown since the rows were built (it fades in once).</summary>
    private bool[] tileSeen = [];
    private KindItem[] kindItems = [];
    private int kindsBuild = -1;
    private int kindsLanguage = -1;

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

        // "Delete all data" empties user/overrides.json; the verdicts held here go with it so hidden quests reappear.
        session.DataDeleted += OnDataDeleted;
    }

    /// <summary>
    /// The background queue verdicts are saved and re-read on (D11), so the framework thread never waits on the disk or
    /// the cross-client lock. Without one, saves run at once.
    /// </summary>
    public SerialWriter? Writer { get; set; }

    public void Dispose()
    {
        session.DataDeleted -= OnDataDeleted;
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
        overridesTouched.Add(rowId);
        SaveOverrides();
    }

    /// <summary>Removes every verdict (Settings › Data › Restore all) so the shipped data applies everywhere again.</summary>
    public void ClearAllOverrides()
    {
        if (overrides.Count == 0)
        {
            return;
        }

        // Every verdict this client knows of goes; one another game client adds meanwhile survives the merge.
        overridesTouched.UnionWith(overrides.Keys);
        overrides.Clear();
        SaveOverrides();
    }

    /// <summary>Removes the user's verdict for a quest so the shipped data applies again.</summary>
    public void ClearOverride(uint rowId)
    {
        if (overrides.Remove(rowId))
        {
            overridesTouched.Add(rowId);
            SaveOverrides();
        }
    }

    /// <summary>
    /// Multibox (D11): <c>user/overrides.json</c> changed on disk (another game client's verdict, or "Delete all data"
    /// there). The file is merged into the verdicts held here, keeping any change here not saved yet; the catalog
    /// rebuilds only when something changed. Read on the background writer, after any save queued before it, and never
    /// quarantined: a file that cannot be read or parsed right now leaves the verdicts held here as they are.
    /// </summary>
    public void MergeOverridesFromDisk()
    {
        var generation = overridesGeneration;
        var path = paths.OverridesFile;
        SerialWriter.Submit(Writer, () => OverridesFile.LoadShared(path), (read, error) =>
        {
            if (generation != overridesGeneration)
            {
                return;
            }

            if (error is not null || read.Status is SharedLoad.Unreadable or SharedLoad.Invalid)
            {
                log.Debug(error, "Overrides not merged from disk: {Problem}", read.Problem ?? error?.Message ?? string.Empty);
                return;
            }

            // A missing file (nothing saved yet, or moved away) is no reason to forget the verdicts held here.
            if (!read.IsLoaded)
            {
                return;
            }

            // Verdicts changed here and not saved yet (queued or in flight) keep what this client holds.
            var keep = new HashSet<uint>(overridesTouched);
            if (overridesInFlight is { } inFlight)
            {
                keep.UnionWith(inFlight);
            }

            AdoptOverrides(KeyedMerge.Apply(read.Value!, overrides, keep));
        });
    }

    /// <summary>"Delete all data" emptied the file: the verdicts held here go, and saves or reloads queued before are ignored.</summary>
    private void OnDataDeleted()
    {
        overridesGeneration++;
        overridesInFlight = null;
        overridesTouched.Clear();
        overrides.Clear();
        catalogDirty = true;
        overridesVersion++;
    }

    /// <summary>Takes a merged verdict map as the one held here; marks the catalog for a rebuild when it differs.</summary>
    private void AdoptOverrides(Dictionary<uint, UniqueOverride> merged)
    {
        var same = merged.Count == overrides.Count;
        if (same)
        {
            foreach (var (rowId, stored) in merged)
            {
                if (!overrides.TryGetValue(rowId, out var held) || held != stored)
                {
                    same = false;
                    break;
                }
            }
        }

        if (same)
        {
            return;
        }

        overrides.Clear();
        foreach (var (rowId, stored) in merged)
        {
            overrides[rowId] = stored;
        }

        catalogDirty = true;
        overridesVersion++;
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
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;

        // The count column is as wide as the widest count; in a pane too narrow for it beside a few letters of the
        // name it gives way to the name's tooltip rather than be crushed (feature plan v4 L6). At Full and Quiet the
        // counts are in the Numeral role and each kind wears its icon in an orbit.
        var art = Theme.ShowRules;
        float countWidth;
        if (art)
        {
            using var numeral = Typography.Numeral(default);
            countWidth = WidestCount();
        }
        else
        {
            countWidth = WidestCount();
        }

        var orbit = OrbitBox(line);
        var moonWidth = MathF.Max(line * 1.4f, art ? orbit : UiMetrics.InlineGlyphSize(line));
        var padding = ImGui.GetStyle().CellPadding.X * 2f;
        var showCount = width - moonWidth - countWidth - (padding * 2f) >= UiMetrics.Px(LayoutBudgets.RowNameMinLogical);
        using (var table = ImRaii.Table(showCount ? "##moonlitKinds" : "##moonlitKindsNarrow", showCount ? 3 : 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX))
        {
            if (!table)
            {
                return;
            }

            ImGui.TableSetupColumn("##moon", ImGuiTableColumnFlags.WidthFixed, moonWidth);
            ImGui.TableSetupColumn("##name", ImGuiTableColumnFlags.WidthStretch);
            if (showCount)
            {
                ImGui.TableSetupColumn("##count", ImGuiTableColumnFlags.WidthFixed, countWidth);
            }

            DrawKindRow(ui, allItem, -1, showCount, art, orbit);
            for (var i = 0; i < kindItems.Length; i++)
            {
                DrawKindRow(ui, kindItems[i], i, showCount, art, orbit);
            }
        }

        ui.RecordSpan(UiRects.MoonlitKinds, start, width);
    }

    /// <summary>The widest count of the kinds list in the current font.</summary>
    private float WidestCount()
    {
        var widest = ImGui.CalcTextSize(allItem.CountText).X;
        for (var i = 0; i < kindItems.Length; i++)
        {
            widest = MathF.Max(widest, ImGui.CalcTextSize(kindItems[i].CountText).X);
        }

        return widest;
    }

    /// <summary>A kind's orbit in the list: a little taller than the line, as the tree's (proposal §6.3).</summary>
    private static float OrbitBox(float line) => MathF.Round(MathF.Max(UiMetrics.InlineGlyphSize(line), UiMetrics.Icon(22f)));

    /// <summary>
    /// Whether rows found elsewhere (sold on the Online Store, or dropping in a duty) are dropped from the table and the
    /// counts (Configuration.MoonlitHideStoreResells, named before dungeon drops joined it).
    /// </summary>
    public bool HideStoreResells => settings.MoonlitHideStoreResells;

    /// <summary>Center column: toolbar (hide obtained, hide rewards found elsewhere, filter) and the reward table with a list clipper.</summary>
    public void DrawMain(UiState ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        using var id = ImRaii.PushId("moonlitMain");
        Refresh();

        DrawTitle(ui);

        // The toolbar flows: under MoonlitTwoRowToolbarLogical the two checkboxes take the first row and the rest the
        // second, and any item that would run past the edge starts a new row (feature plan v4 L6).
        var twoRows = ImGui.GetContentRegionAvail().X / UiMetrics.Scale < LayoutBudgets.MoonlitTwoRowToolbarLogical;
        var hide = ui.MoonlitHideObtained;
        if (ImGui.Checkbox(Strings.MoonlitHideObtainedLabel, ref hide))
        {
            ui.MoonlitHideObtained = hide;
        }

        Chrome.SameLineOrWrap(CheckboxWidth(Strings.MoonlitHideStoreResellsLabel));
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

        var comboWidth = UiMetrics.Px(150f);
        if (!twoRows)
        {
            Chrome.SameLineOrWrap(comboWidth);
        }

        ImGui.SetNextItemWidth(Chrome.FitWidth(comboWidth));
        DrawConfidenceCombo();

        // The filter keeps at least a third of its width on the line, and shrinks to the room left.
        var filterWidth = UiMetrics.Px(220f);
        Chrome.SameLineOrWrap(filterWidth / 3f);
        ImGui.SetNextItemWidth(Chrome.FitWidth(filterWidth));
        ImGui.InputTextWithHint("##moonlitFilter", Strings.MoonlitFilterHint, ref filterText, FilterMaxLength);

        RefreshVisible(ui);
        Chrome.SameLineOrWrap(ImGui.CalcTextSize(visibleSummary).X);
        ImGui.TextDisabled(visibleSummary);
        if (!session.IsLive)
        {
            // A stored character's owned states are its last capture's ("Owned as of …"); older files have none.
            var ownedNote = Strings.MoonlitOwnedNote(unlocks.StoredAsOfUtc);
            Chrome.SameLineOrWrap(ImGui.CalcTextSize(ownedNote).X);
            using (Theme.PushText(Theme.Dusk))
            {
                TextFlow.Wrapped(ownedNote, Chrome.RoomX());
            }
        }

        if (verdict.UndoShowing)
        {
            Chrome.SameLineOrWrap(verdict.UndoWidth());
            verdict.DrawUndo(this);
        }

        // The verdict popup is begun here, in the centre column's scope, because the context menu that requests it
        // lives inside the table's inner window and closes before the popup could be shown from there.
        verdict.Draw(this);

        // Until the client has loaded the title or achievement list, those obtained marks are worked out from quests.
        if (session.IsLive && ui.MoonlitKind is { } shownKind && (shownKind is RewardKind.Title or RewardKind.Achievement) && !unlocks.ReadsExactly(shownKind))
        {
            using (Theme.PushText(Theme.Dusk))
            {
                ImGui.TextWrapped(Strings.MoonlitAchievementsFromQuests);
            }
        }

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

        if (settings.MoonlitGallery)
        {
            DrawGallery(ui);
            return;
        }

        // Every column is reorderable (no NoReorder anywhere). The two glyph columns are fixed and not resizable, but
        // wide enough that their header can be grabbed away from the neighbouring resize border: ImGui claims the
        // 4 px either side of a border for resizing, and a header only as wide as the glyph left almost nothing
        // else to drag, so a reorder attempt on State became a resize of the stretch column before it.
        const ImGuiTableFlags Flags = ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH
                                      | ImGuiTableFlags.Resizable | ImGuiTableFlags.Reorderable | ImGuiTableFlags.Hideable
                                      | ImGuiTableFlags.SizingStretchProp;
        var tableWidth = ImGui.GetContentRegionAvail().X;
        using var table = ImRaii.Table(TableId, 6, Flags, new Vector2(-1f, -1f));
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
        FitColumns(tableWidth, glyphColumn, line);
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn(Strings.MoonlitColumnObtained, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, glyphColumn);
        ImGui.TableSetupColumn(Strings.MoonlitColumnReward, ImGuiTableColumnFlags.WidthStretch, 3f);
        ImGui.TableSetupColumn(Strings.MoonlitColumnKind, ImGuiTableColumnFlags.WidthFixed | Planned(KindColumn), UiMetrics.Px(110f));
        ImGui.TableSetupColumn(Strings.MoonlitColumnQuest, ImGuiTableColumnFlags.WidthStretch, 3f);
        ImGui.TableSetupColumn(Strings.MoonlitColumnState, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, glyphColumn);
        ImGui.TableSetupColumn(Strings.MoonlitColumnConfidence, ImGuiTableColumnFlags.WidthFixed | Planned(ConfidenceColumn), UiMetrics.Px(80f));
        // The glyph columns follow IconScale, which imgui.ini's saved widths do not track; re-asserted every frame
        // (a no-op once they agree) so a changed IconScale never clips the moons.
        ImGuiP.TableSetColumnWidth(0, glyphColumn);
        ImGuiP.TableSetColumnWidth(4, glyphColumn);
        RecordPlayerHidden();
        ImGui.TableHeadersRow();

        if (!clipperCreated)
        {
            clipper = ImGui.ImGuiListClipper();
            clipperCreated = true;
        }

        moreFocusedNext = -1;
        clipper.Begin(visibleCount);
        while (clipper.Step())
        {
            for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                DrawRow(ui, rows[visible[i]], line);
            }
        }

        clipper.End();
        moreFocusedRow = moreFocusedNext;
    }

    /// <summary>
    /// The table's ImGui id. It changed when the narrow-width plan stopped hiding columns through the player's own
    /// hide state (1.3): imgui.ini kept Kind or Confidence hidden under the old id whenever the pane had been narrow,
    /// which would otherwise read as hidden by the player forever.
    /// </summary>
    private const string TableId = "##moonlitRewards";

    // The table's narrow-width plan (feature plan v4 L6): Confidence hides first, then Kind; State never. The plan hides
    // a column with ImGuiTableColumnFlags.Disabled, which ImGui neither saves nor lists in the header menu, so a column
    // the player hides from that menu stays theirs and stays hidden, and one the plan hid comes back when there is room.
    private const int KindColumn = 2;
    private const int ConfidenceColumn = 5;
    private readonly bool[] columnsShown = new bool[6];
    private readonly bool[] columnsWere = new bool[6];
    private readonly float[] columnWidths = new float[6];
    private readonly bool[] playerHidden = new bool[6];
    private readonly bool[] autoHidden = new bool[6];
    private bool columnsPlanned;

    /// <summary>Plans the table's columns for its width, before they are set up; a column the player hid takes no room.</summary>
    private void FitColumns(float tableWidth, float glyphColumn, float line)
    {
        var padding = ImGui.GetStyle().CellPadding.X * 2f;
        var nameMin = UiMetrics.Px(LayoutBudgets.RowNameMinLogical);
        var rewardMin = UiMetrics.RowIconSize + ImGui.GetStyle().ItemSpacing.X + nameMin + MoreSize(line);
        Span<ColumnSpec> specs = stackalloc ColumnSpec[6];
        PaneFit.MoonlitColumns(
            glyphColumn + padding,
            rewardMin + padding,
            UiMetrics.Px(110f) + padding,
            nameMin + padding,
            UiMetrics.Px(80f) + padding,
            specs);
        for (var i = 0; i < specs.Length; i++)
        {
            if (playerHidden[i])
            {
                specs[i] = specs[i] with { Min = 0f, Ideal = 0f };
            }
        }

        TableGeometry.PlanColumns(tableWidth, specs, columnsPlanned ? columnsWere : [], columnsShown, columnWidths, UiMetrics.Px(LayoutBudgets.HysteresisLogical));
        columnsPlanned = true;
        Array.Copy(columnsShown, columnsWere, columnsShown.Length);
        for (var i = 0; i < autoHidden.Length; i++)
        {
            autoHidden[i] = !columnsShown[i] && !playerHidden[i];
        }
    }

    /// <summary>The Disabled flag for a column the plan hides this frame.</summary>
    private ImGuiTableColumnFlags Planned(int column) => autoHidden[column] ? ImGuiTableColumnFlags.Disabled : ImGuiTableColumnFlags.None;

    /// <summary>After setup: which of the plan's columns the player hid from the header menu (the plan's hides never touch that).</summary>
    private void RecordPlayerHidden()
    {
        foreach (var column in PlannedColumns)
        {
            if (!autoHidden[column])
            {
                playerHidden[column] = (ImGui.TableGetColumnFlags(column) & ImGuiTableColumnFlags.IsEnabled) == 0;
            }
        }
    }

    private static readonly int[] PlannedColumns = [KindColumn, ConfidenceColumn];

    /// <summary>The "…" button's side in a row.</summary>
    private static float MoreSize(float line) => MathF.Min(UiMetrics.MinTarget, MathF.Max(line, UiMetrics.RowIconSize));

    /// <summary>A checkbox's width: the box, the inner spacing and the label.</summary>
    private static float CheckboxWidth(string label) =>
        ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X + ImGui.CalcTextSize(label).X;

    /// <summary>The row's context menu, opened by a right-click, the Menu key or Shift+F10, or the "…" button.</summary>
    private const string RowMenuId = "ctx";

    // The row whose "…" button had keyboard focus last frame (it stays drawn while focused), and this frame's; -1 none.
    private int moreFocusedRow = -1;
    private int moreFocusedNext = -1;

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

    private void DrawKindRow(UiState ui, KindItem item, int index, bool showCount, bool art, float orbit)
    {
        using var id = ImRaii.PushId(index);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        var line = ImGui.GetTextLineHeight();

        // At Full and Quiet the row is as tall as the orbit and its text is centred on it.
        var lift = art ? MathF.Floor(MathF.Max(0f, (orbit - line) * 0.5f)) : 0f;
        if (item.AllUnknown)
        {
            // Nothing readable for this kind on the viewed character (logged out, a stored snapshot, or a kind the
            // reader cannot answer): a dash says so instead of a misleading empty gauge.
            Marks.DrawInline(Mark.Unknown, art ? orbit : UiMetrics.InlineGlyphSize(line));
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.MoonlitObtainedUnknown);
            }
        }
        else if (art)
        {
            // The kind's own menu icon keeps its identity; the orbit round it is obtained/total (proposal §6.3).
            var min = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(orbit, orbit));
            Orbit.Draw(ImGui.GetWindowDrawList(), textures, min, orbit, item.Icon, item.Fraction, highContrast: Theme.Glyphs.HighContrast);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(item.TooltipText);
            }
        }
        else
        {
            MoonGlyph.DrawHaloInline(item.Fraction, UiMetrics.InlineGlyphSize(line));
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(item.TooltipText);
            }
        }

        // The selectable spans every column and the row's whole height, so a click on the orbit selects the kind too;
        // the name is centred on it.
        ImGui.TableNextColumn();
        var selected = ui.MoonlitKind == item.Kind;
        var rowHeight = art ? MathF.Max(line, orbit) : 0f;
        if (Chrome.EllipsisSelectable(item.Name, selected, 0f, out var cut, ImGuiSelectableFlags.SpanAllColumns, rowHeight))
        {
            ui.MoonlitKind = item.Kind;
        }

        if ((cut || !showCount) && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(item.Name, item.TooltipText);
        }

        if (showCount)
        {
            ImGui.TableNextColumn();
            if (art)
            {
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + lift);
                using var numeral = Typography.Numeral(item.CountText);
                ImGui.TextDisabled(item.CountText);
            }
            else
            {
                ImGui.TextDisabled(item.CountText);
            }
        }
    }

    /// <summary>
    /// The centre column's first line: at Full and Quiet the shown kind in the Title role (the pane's one title) with
    /// the subtitle beside it and a brass rule under both; under Plain the subtitle alone, as before 1.4. The view
    /// toggle (table or gallery) sits at the line's right end.
    /// </summary>
    private void DrawTitle(UiState ui)
    {
        var art = Theme.ShowRules;
        var start = ImGui.GetCursorScreenPos();
        var room = ImGui.GetContentRegionAvail().X;
        var toggle = (UiMetrics.MinTarget * 2f) + ImGui.GetStyle().ItemSpacing.X;
        if (art)
        {
            var title = ui.MoonlitKind is { } kind ? Strings.MoonlitKindName(kind) : Strings.TabMoonlit;
            float titleWidth;
            float titleLine;
            using (Typography.Title(title))
            {
                titleWidth = ImGui.CalcTextSize(title).X;
                titleLine = ImGui.GetTextLineHeight();
            }

            var targetLine = MathF.Max(titleLine, UiMetrics.MinTarget);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + MathF.Max(0f, (targetLine - titleLine) * 0.5f));
            if (SectionHeading.Title(title, MathF.Max(0f, MathF.Min(titleWidth, room - toggle - ImGui.GetStyle().ItemSpacing.X))) && ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(title);
            }

            var subtitle = ImGui.CalcTextSize(Strings.MoonlitSubtitle).X;
            if (ImGui.GetItemRectMax().X + ImGui.GetStyle().ItemSpacing.X + subtitle + toggle <= start.X + room)
            {
                ImGui.SameLine();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + MathF.Max(0f, titleLine - ImGui.GetTextLineHeight()) * 0.7f);
                using (Theme.PushText(Theme.Dusk))
                {
                    ImGui.TextUnformatted(Strings.MoonlitSubtitle);
                }
            }
        }
        else
        {
            using (Theme.PushText(Theme.Dusk))
            {
                ImGui.TextUnformatted(Strings.MoonlitSubtitle);
            }
        }

        // The toggle at the line's right end, or at the right of the next line when the line has no room left.
        var toggleX = MathF.Max(start.X, start.X + room - toggle);
        if (ImGui.GetItemRectMax().X + ImGui.GetStyle().ItemSpacing.X <= toggleX)
        {
            ImGui.SameLine();
            ImGui.SetCursorScreenPos(new Vector2(toggleX, start.Y));
        }
        else
        {
            ImGui.SetCursorScreenPos(new Vector2(toggleX, ImGui.GetCursorScreenPos().Y));
        }

        DrawViewToggle();
        if (art)
        {
            var y = ImGui.GetCursorScreenPos().Y;
            Ornament.Rule(ImGui.GetWindowDrawList(), new Vector2(start.X, y), room);
            ImGui.Dummy(new Vector2(room, UiMetrics.Px(4f)));
        }
    }

    /// <summary>Table or gallery: two round icon buttons, the current view's held; the choice is saved at once.</summary>
    private void DrawViewToggle()
    {
        var gallery = settings.MoonlitGallery;
        if (Chrome.IconButtonRound("##viewTable", Chrome.Icon(FontAwesomeIcon.List), Strings.MoonlitViewTable, active: !gallery) && gallery)
        {
            settings.MoonlitGallery = false;
            settings.Save(pluginInterface);
        }

        ImGui.SameLine();
        if (Chrome.IconButtonRound("##viewGallery", Chrome.Icon(FontAwesomeIcon.ThLarge), Strings.MoonlitViewGallery, active: gallery) && !gallery)
        {
            settings.MoonlitGallery = true;
            settings.Save(pluginInterface);
        }
    }

    /// <summary>
    /// The gallery (proposal §7.5): the visible rows as tiles, ⌊width / 96⌋ columns (at least two), in their own
    /// scrolling child with a list clipper over the tile rows, so only visible tiles are drawn and only their icons asked
    /// for. Allocation-free per frame.
    /// </summary>
    private void DrawGallery(UiState ui)
    {
        ImGui.BeginChild("##moonlitGallery", new Vector2(-1f, -1f));
        try
        {
            // The child sits inside the centre column (own font scale 1), so it scales itself before measuring.
            ui.RecordWindow(UiRects.MoonlitTable);
            UiMetrics.ApplyFontScale();
            var width = ImGui.GetContentRegionAvail().X;
            var columns = PaneGrid.GalleryColumns(width / UiMetrics.Scale);
            var cell = MathF.Floor(width / columns);
            var pad = UiMetrics.Px(6f);
            var icon = MathF.Round(MathF.Max(UiMetrics.Px(24f), MathF.Min(UiMetrics.Icon(PaneGrid.GalleryIconLogical), cell - (pad * 2f))));
            var line = ImGui.GetTextLineHeight();
            float captionLine;
            using (Typography.Caption())
            {
                captionLine = ImGui.GetTextLineHeight();
            }

            var tileHeight = MathF.Ceiling(pad + icon + UiMetrics.Px(6f) + (2f * line) + captionLine + pad);
            var spacing = ImGui.GetStyle().ItemSpacing.Y;
            if (tileSeen.Length < rows.Length)
            {
                tileSeen = new bool[rows.Length];
            }

            if (!clipperCreated)
            {
                clipper = ImGui.ImGuiListClipper();
                clipperCreated = true;
            }

            moreFocusedNext = -1;
            var origin = ImGui.GetCursorScreenPos();
            clipper.Begin(PaneGrid.Rows(visibleCount, columns), tileHeight + spacing);
            while (clipper.Step())
            {
                for (var r = clipper.DisplayStart; r < clipper.DisplayEnd; r++)
                {
                    var rowStart = new Vector2(origin.X, ImGui.GetCursorScreenPos().Y);
                    for (var c = 0; c < columns; c++)
                    {
                        var i = (r * columns) + c;
                        if (i >= visibleCount)
                        {
                            break;
                        }

                        DrawTile(ui, rows[visible[i]], new Vector2(rowStart.X + (c * cell), rowStart.Y), new Vector2(cell, tileHeight), icon, pad, line);
                    }

                    ImGui.SetCursorScreenPos(rowStart);
                    ImGui.Dummy(new Vector2(width, tileHeight));
                }
            }

            clipper.End();
            moreFocusedRow = moreFocusedNext;
        }
        finally
        {
            ImGui.EndChild();
        }
    }

    /// <summary>
    /// One gallery tile at <paramref name="min"/>: a selectable the size of the cell (a click selects the quest, as a
    /// table row's name does; right-click, the Menu key or the "…" button opens the row's menu), the reward's hi-res
    /// icon on the sunken ground in a gold frame when obtained, its pip, the name in two lines and the quest in the
    /// caption role. Fades in once at Full flair.
    /// </summary>
    private void DrawTile(UiState ui, Row row, Vector2 min, Vector2 size, float icon, float pad, float line)
    {
        ImGui.PushID(row.Index);
        try
        {
            var inset = MathF.Max(1f, UiMetrics.Px(2f));
            var tileMin = min + new Vector2(inset, 0f);
            var tileSize = new Vector2(MathF.Max(1f, size.X - (2f * inset)), size.Y);
            var tileMax = tileMin + tileSize;
            ImGui.SetCursorScreenPos(tileMin);
            var questId = row.Entry.QuestRowId;
            var dl = ImGui.GetWindowDrawList();
            var firstVertex = dl.VtxBuffer.Size;

            // No item spacing around the tile's selectable: its highlight would pad out by half of it and overlap the
            // neighbours' by a few pixels.
            bool clicked;
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
            {
                clicked = ImGui.Selectable("##tile", ui.SelectedRowId == questId, ImGuiSelectableFlags.AllowItemOverlap, tileSize);
            }

            if (clicked)
            {
                ui.SelectedRowId = questId;
            }

            var tileHovered = ImGui.IsItemHovered();
            var tileFocused = ImGui.IsItemFocused();
            var tileVisible = ImGui.IsItemVisible();
            Keyboard.OpenMenuOnKey(RowMenuId);
            if (ImGui.BeginPopupContextItem(RowMenuId))
            {
                DrawContextMenu(ui, row);
                ImGui.EndPopup();
            }

            if (!tileVisible)
            {
                return;
            }

            var s = Theme.Surface;
            var highContrast = Theme.Glyphs.HighContrast;

            // The icon on the sunken ground; a gold frame when the reward is yours.
            var iconMin = new Vector2(min.X + MathF.Floor((size.X - icon) * 0.5f), min.Y + pad);
            var iconMax = iconMin + new Vector2(icon, icon);
            var rounding = MathF.Round(icon * 0.125f);
            var obtained = row.Obtained == true;
            dl.AddRectFilled(iconMin, iconMax, Theme.U32(s.Sunken), rounding);
            var art = MathF.Round(icon * 6f / 64f);
            if (row.Reward is not null)
            {
                if (GameIcon.TryGetWrap(textures, row.Icon, icon - (2f * art), out var wrap))
                {
                    dl.AddImageRounded(wrap.Handle, iconMin + new Vector2(art), iconMax - new Vector2(art), Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding * 0.5f);
                }
            }
            else
            {
                MoonGlyph.DrawVeiled(dl, (iconMin + iconMax) * 0.5f, icon * 0.28f, NoIconAlpha);
            }

            if (obtained)
            {
                dl.AddRect(iconMin, iconMax, Theme.MoonU32, rounding, ImDrawFlags.None, MathF.Max(1.5f, UiMetrics.Px(highContrast ? 2.5f : 1.5f)));
            }
            else
            {
                dl.AddRect(iconMin, iconMax, highContrast ? Theme.VeilLineU32 : Theme.U32(s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
            }

            // The pip on the frame's corner: a checked full moon when obtained, the unknown dash in a ring when it cannot
            // be read (a stored snapshot), else the quest's state moon.
            var pipRadius = MathF.Round(MathF.Max(UiMetrics.Px(6f), icon * 0.11f));
            var pip = iconMax - new Vector2(pipRadius * 0.55f);
            var state = session.States.TryGetValue(questId, out var evaluation) ? evaluation.State : QuestState.Unknown;
            dl.AddCircleFilled(pip, pipRadius + MathF.Max(1.5f, UiMetrics.Px(1.5f)), Theme.U32(s.Window));
            if (obtained)
            {
                ObtainedPip(dl, pip, pipRadius);
            }
            else if (row.Obtained is null)
            {
                dl.AddCircle(pip, pipRadius, highContrast ? Theme.VeilLineU32 : Theme.DuskU32, 0, MathF.Max(1f, UiMetrics.Hairline));
                Marks.Draw(dl, pip, pipRadius * 2.5f, Mark.Unknown);
            }
            else
            {
                MoonGlyph.Draw(dl, pip, pipRadius, state);
            }

            // The name in two lines at most, centred; the quest under it in the caption role.
            var textX = tileMin.X + pad;
            var textWidth = MathF.Max(1f, tileSize.X - (2f * pad));
            var nameY = iconMax.Y + UiMetrics.Px(6f);
            var nameInk = row.Hidden ? Theme.DuskU32 : Theme.U32(s.Text);
            // Hidden by the user's verdict: struck through across the name's own width, as in the table (not colour alone).
            var nameCut = TextFlow.DrawClamped(dl, new Vector2(textX, nameY), row.Name, textWidth, 2, nameInk, center: true, strike: row.Hidden);

            using (Typography.Caption())
            {
                var questWidth = ImGui.CalcTextSize(row.QuestName).X;
                var questX = textX + MathF.Floor(MathF.Max(0f, textWidth - questWidth) * 0.5f);
                Chrome.EllipsisTextAt(dl, new Vector2(questX, nameY + (2f * line)), textWidth, row.QuestName, Theme.U32(s.TextSecondary), questWidth);
            }

            // Fades in once, the first time the tile shows (Full flair, motion on): everything the tile drew so far takes
            // the fade's alpha, so the pane's gradient shows through rather than a flat window-coloured veil.
            var key = Motion.Key(GalleryTag, (uint)row.Index);
            if (!tileSeen[row.Index])
            {
                tileSeen[row.Index] = true;
                if (Theme.FlairMotion)
                {
                    Motion.Trigger(key);
                }
            }

            if (Theme.FlairMotion && Motion.Pulse(key, TileFadeSeconds) is var fade and >= 0f)
            {
                Chrome.FadeVertices(dl, firstVertex, MotionMath.EaseOutCubic(fade));
            }

            // Hover: the pip says what it means; elsewhere the reward's tooltip (the name when cut, the verdict when hidden).
            if (tileHovered)
            {
                var pipHalf = new Vector2(pipRadius + UiMetrics.Px(2f));
                if (ImGui.IsMouseHoveringRect(pip - pipHalf, pip + pipHalf))
                {
                    if (row.Obtained is false)
                    {
                        // The obtained answer first, as the table's column says it; the state moon's meaning under it.
                        UiMetrics.Tooltip(row.ObtainedText, Strings.StateTooltip(state, row.Quest));
                    }
                    else
                    {
                        UiMetrics.Tooltip(row.ObtainedText);
                    }
                }
                else if (row.Hidden)
                {
                    UiMetrics.Tooltip(row.Name, Strings.MoonlitHiddenTooltip);
                }
                else if (row.Reward is { } reward)
                {
                    RewardTooltip.Draw(reward, links, textures, row.SourceText);
                }
                else
                {
                    UiMetrics.Tooltip(nameCut ? row.Name : row.ObtainedText, nameCut ? row.ObtainedText : null);
                }
            }

            // The "…" button in the tile's top-right corner while it is hovered or focused (accessibility A6).
            var more = MoreSize(line);
            if (tileFocused || moreFocusedRow == row.Index || (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(tileMin, tileMax)))
            {
                Keyboard.MoreButton("##more", RowMenuId, new Vector2(tileMax.X - more - inset, tileMin.Y + inset), more);
                if (ImGui.IsItemFocused())
                {
                    moreFocusedNext = row.Index;
                }
            }
        }
        finally
        {
            ImGui.PopID();
        }
    }

    /// <summary>
    /// The obtained pip: a full gold moon with a check cut into it in the window colour, a shape no state moon has (a
    /// completed quest's full moon carries no check), so obtained never rests on colour alone (proposal §10.3).
    /// </summary>
    private static void ObtainedPip(ImDrawListPtr dl, Vector2 center, float radius)
    {
        dl.AddCircleFilled(center, radius, Theme.MoonU32);
        var ink = Theme.U32(Theme.Surface.Window);
        var t = MathF.Max(1.2f, radius * 0.24f);
        var a = center + new Vector2(-radius * 0.45f, radius * 0.02f);
        var b = center + new Vector2(-radius * 0.1f, radius * 0.38f);
        var c = center + new Vector2(radius * 0.5f, -radius * 0.34f);
        dl.AddLine(a, b, ink, t);
        dl.AddLine(b, c, ink, t);
    }

    private void DrawRow(UiState ui, Row row, float line)
    {
        using var id = ImRaii.PushId(row.Index);
        ImGui.TableNextRow();

        // Obtained.
        ImGui.TableNextColumn();
        Marks.DrawInline(row.ObtainedGlyph, UiMetrics.InlineGlyphSize(line));
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(row.ObtainedText);
        }

        // Icon and reward name; the row's context menu hangs off the name. A row hidden by the user's verdict is
        // Dusk and struck through, and says so on hover.
        ImGui.TableNextColumn();
        var cellMin = ImGui.GetCursorScreenPos();
        var cellWidth = ImGui.GetContentRegionAvail().X;
        DrawIcon(row, UiMetrics.RowIconSize);
        ImGui.SameLine();
        using (Theme.PushText(Theme.Dusk, row.Hidden))
        {
            // The highlight follows the global selection, as in the Flight pane, so a quest picked from the detail
            // pane's path, another pane or chat lights its Moonlit row too, and an override never wipes it.
            // AllowItemOverlap lets the "…" button drawn over the cell's right end take the hover and the click.
            if (ImGui.Selectable(row.Name, ui.SelectedRowId == row.Entry.QuestRowId, ImGuiSelectableFlags.AllowItemOverlap))
            {
                ui.SelectedRowId = row.Entry.QuestRowId;
            }
        }

        // The Menu key or Shift+F10 on the focused name opens its menu (accessibility A6).
        var nameFocused = ImGui.IsItemFocused();
        Keyboard.OpenMenuOnKey(RowMenuId);

        if (row.Hidden)
        {
            StrikeThrough(row.Name);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.MoonlitHiddenTooltip);
            }
        }

        using (var menu = ImRaii.ContextPopupItem(RowMenuId))
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

        if (row.DropsInDuty)
        {
            ImGui.SameLine();
            SmallDuskText(Strings.MoonlitAlsoDrops);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(row.DropTooltip);
            }
        }

        // The "…" button at the reward cell's right end while the mouse is over the cell or the name (or the button)
        // has keyboard focus: a left click, Enter or Space opens the same menu (accessibility A6).
        var size = MoreSize(line);
        var cellMax = new Vector2(cellMin.X + cellWidth, cellMin.Y + size);
        if (nameFocused || moreFocusedRow == row.Index || (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(cellMin, cellMax)))
        {
            // No cursor restore afterwards: TableNextColumn follows at once, and a set position folded into the
            // cell's CursorMaxPos would make the row taller while the button shows. In a narrow cell the button
            // stays right of the reward's icon rather than cover it (feature plan v4 L6).
            var moreX = MathF.Max(cellMin.X + UiMetrics.RowIconSize + ImGui.GetStyle().ItemSpacing.X, cellMax.X - size);
            Keyboard.MoreButton("##more", RowMenuId, new Vector2(moreX, cellMin.Y), size);
            if (ImGui.IsItemFocused())
            {
                moreFocusedNext = row.Index;
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
            GameIcon.Draw(textures, row.Icon, size);
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

        if (ImGui.MenuItem(Strings.RouteToThisReward))
        {
            ui.OpenRoute(Core.Route.RouteTarget.ForReward(row.Entry, catalog.All, row.Name));
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
        // The quest names are baked into the rows, so a spoiler mask that hides other names rebuilds them (T19).
        if (rowsBuild != catalogBuild || !ReferenceEquals(rowsBundle, session.Bundle) || rowsSpoilers != session.Spoilers.Fingerprint || rowsLanguage != Localization.Loc.Version)
        {
            BuildRows();
        }

        // Titles and achievements switch to the game's own state once the client has loaded it (opening the Titles or
        // Achievements window); that bumps no session version, so the reader's own counter is watched too.
        var achievementState = unlocks.LiveStateVersion;
        if (obtainedVersion != session.Version || obtainedBuild != rowsBuild || countsHideStore != settings.MoonlitHideStoreResells
            || obtainedAchievementState != achievementState)
        {
            obtainedAchievementState = achievementState;
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

    /// <summary>The UI language the rows' labels were composed in.</summary>
    private int rowsLanguage = -1;

    private void BuildRows()
    {
        rowsLanguage = Localization.Loc.Version;
        var bundle = session.Bundle;
        var spoilers = session.Spoilers;
        var all = catalog.All;
        var hidden = catalog.Hidden;
        var built = new Row[all.Count + hidden.Count];
        for (var i = 0; i < all.Count; i++)
        {
            var entry = all[i];
            var quest = bundle?.Catalog.GetByRowId(entry.QuestRowId);
            built[i] = new Row(i, entry, quest, Icons.Resolve(quest, entry), hidden: false, spoilers, bundle?.Language);
        }

        // Rows hidden by a "not unique" verdict follow the view so the Yours filter can list them for Restore.
        for (var j = 0; j < hidden.Count; j++)
        {
            var i = all.Count + j;
            var entry = hidden[j];
            var quest = bundle?.Catalog.GetByRowId(entry.QuestRowId);
            built[i] = new Row(i, entry, quest, Icons.Resolve(quest, entry), hidden: true, spoilers, bundle?.Language);
        }

        uniqueCount = all.Count;
        elsewhereCount = 0;
        tileSeen = new bool[built.Length];
        for (var i = 0; i < all.Count; i++)
        {
            if (built[i].FoundElsewhere)
            {
                elsewhereCount++;
            }
        }

        rows = built;
        rowsBuild = catalogBuild;
        rowsBundle = bundle;
        rowsSpoilers = spoilers.Fingerprint;
        obtainedVersion = -1;
    }

    /// <summary>Obtained state per row and the per-kind counts, once per session version (and per "found elsewhere" toggle: hidden rows leave the counts).</summary>
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
            if (row.Hidden || (hideStore && row.FoundElsewhere))
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
        if (kindsBuild != rowsBuild || kindsLanguage != Localization.Loc.Version)
        {
            kindsBuild = rowsBuild;
            kindsLanguage = Localization.Loc.Version;
            allItem = new KindItem(null, Strings.MoonlitAllKinds, Icons.KindIcon(null));
            kindItems = new KindItem[kinds.Count];
            for (var i = 0; i < kindItems.Length; i++)
            {
                kindItems[i] = new KindItem(kinds[i].Kind, Strings.MoonlitKindName(kinds[i].Kind), Icons.KindIcon(kinds[i].Kind));
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

            if (hideStore && row.FoundElsewhere)
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
        var denominator = hideStore ? uniqueCount - elsewhereCount : uniqueCount;
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

    /// <summary>
    /// Queues the verdicts changed here on the background writer (the catalog rebuilds at once from the verdicts held
    /// here). Multibox (D11): only those quests are written; the others keep what is on disk, which another game client
    /// may have changed since this one read it. One save is in flight at a time; changes made meanwhile follow it.
    /// </summary>
    private void SaveOverrides()
    {
        catalogDirty = true;
        overridesVersion++;
        if (overridesTouched.Count == 0 || overridesInFlight is not null)
        {
            return;
        }

        var touched = new HashSet<uint>(overridesTouched);
        overridesTouched.Clear();
        overridesInFlight = touched;
        var local = new Dictionary<uint, UniqueOverride>(overrides);
        var generation = overridesGeneration;
        var path = paths.OverridesFile;
        var warnings = new List<string>();
        SerialWriter.Submit(Writer, () => OverridesFile.SaveMerged(path, local, touched, warnings), (merged, error) =>
        {
            if (generation != overridesGeneration)
            {
                return;
            }

            overridesInFlight = null;
            foreach (var warning in warnings)
            {
                log.Warning("Overrides: {Warning}", warning);
            }

            if (error is not null || merged is null)
            {
                // Still touched: the next verdict saves it too.
                overridesTouched.UnionWith(touched);
                log.Error(error, "Could not save {Path}", path);
                return;
            }

            // The saved file, with the verdicts changed here since it was queued.
            AdoptOverrides(KeyedMerge.Apply(merged, overrides, overridesTouched));
            if (overridesTouched.Count > 0)
            {
                SaveOverrides();
            }
        });
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
        Confidence.Static => Theme.Mist,
        Confidence.Community => Theme.VeilText,
        Confidence.Curated => Theme.Silver,
        Confidence.UserOverride => Theme.EclipseText,
        _ => Theme.Veil,
    };

    /// <summary>One left-column line: a kind (null for All), its name, its identity icon and the current counts.</summary>
    private sealed class KindItem(RewardKind? kind, string name, NodeIcon icon)
    {
        public RewardKind? Kind { get; } = kind;
        public string Name { get; } = name;

        /// <summary>The kind's menu icon (MainCommand) or atlas glyph, inside its orbit at Full and Quiet flair.</summary>
        public NodeIcon Icon { get; } = icon;
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
        /// <param name="spoilers">The viewed character's shield: a masked quest's name is its placeholder here too.</param>
        /// <param name="catalogLanguage">The catalog's language: a non-English client prints the sheet's reward name (<see cref="RewardNames"/>).</param>
        public Row(int index, UniqueRewardEntry entry, QuestRecord? quest, uint icon, bool hidden, SpoilerMask spoilers, string? catalogLanguage)
        {
            Index = index;
            Entry = entry;
            Quest = quest;
            Icon = icon;
            Hidden = hidden;
            KindName = Strings.MoonlitKindName(entry.Kind);
            var rewardName = RewardNames.Display(entry, quest, catalogLanguage);
            Name = string.IsNullOrWhiteSpace(rewardName)
                ? KindName + " #" + entry.RewardId.ToString(CultureInfo.InvariantCulture)
                : rewardName;
            QuestName = quest is null ? string.Format(CultureInfo.InvariantCulture, Strings.MoonlitQuestFormat, entry.QuestRowId) : spoilers.DisplayName(quest);
            QuestLabel = QuestName + "##q";
            ConfidenceLabel = hidden ? Strings.MoonlitConfidenceUser : MoonlitPane.ConfidenceLabel(entry.Confidence);
            ConfidenceColor = hidden ? Theme.EclipseText : MoonlitPane.ConfidenceColor(entry.Confidence);
            ConfidenceTooltip = hidden ? Strings.MoonlitBadgeHidden : MoonlitPane.ConfidenceTooltip(entry.Confidence);
            SourceText = string.IsNullOrWhiteSpace(entry.Source) ? Strings.MoonlitSourceUnknown : entry.Source;
            StoreResell = entry.SoldOnOnlineStore;
            DropsInDuty = entry.DropsInDuty;
            DropTooltip = DropsInDuty ? Strings.MoonlitAlsoDropsTooltip(entry.DropWhere) : string.Empty;
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

        /// <summary>A duty also drops this reward (entry OtherSources carries DungeonDrop).</summary>
        public bool DropsInDuty { get; }

        /// <summary>"Also drops in …; not exclusive to the quest" for the "Also drops" mark; empty when the reward does not drop.</summary>
        public string DropTooltip { get; }

        /// <summary>Found outside the quest (store or drop): what "Hide rewards found elsewhere" leaves out.</summary>
        public bool FoundElsewhere => StoreResell || DropsInDuty;

        /// <summary>What the icon's tooltip describes; null when the row has no icon (the veiled stand-in is drawn instead).</summary>
        public RewardRef? Reward { get; }

        public bool? Obtained { get; private set; }
        public Mark ObtainedGlyph { get; private set; } = Mark.Unknown;
        public string ObtainedText { get; private set; } = Strings.MoonlitObtainedUnknown;

        public void SetObtained(bool? obtained)
        {
            Obtained = obtained;
            (ObtainedGlyph, ObtainedText) = obtained switch
            {
                true => (Mark.Check, Strings.MoonlitObtainedYes),
                false => (Mark.Cross, Strings.MoonlitObtainedNo),
                null => (Mark.Unknown, Strings.MoonlitObtainedUnknown),
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
    private readonly Dictionary<RewardKind, NodeIcon> kindIcons = [];
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

    /// <summary>
    /// The identity icon of a reward kind in the kinds list (proposal §7.5): the game's own menu icon
    /// (<c>MainCommand.Icon</c> of <see cref="MoonlitKindIcons.MainCommandRow"/>), else the kind's atlas glyph; the
    /// Moonlit glyph for All (null). Memoized per kind.
    /// </summary>
    public NodeIcon KindIcon(RewardKind? kind)
    {
        if (kind is not { } k)
        {
            return NodeIcon.Of(MoonlitKindIcons.Glyph(null));
        }

        if (kindIcons.TryGetValue(k, out var known))
        {
            return known;
        }

        var icon = NodeIcon.Of(MoonlitKindIcons.Glyph(k));
        var row = MoonlitKindIcons.MainCommandRow(k);
        if (row != 0)
        {
            try
            {
                if (data.GetExcelSheet<MainCommand>()?.GetRowOrDefault(row) is { } command && command.Icon > 0)
                {
                    icon = NodeIcon.Game((uint)command.Icon);
                }
            }
            catch (Exception ex)
            {
                WarnOnce(ex, k);
            }
        }

        kindIcons[k] = icon;
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
