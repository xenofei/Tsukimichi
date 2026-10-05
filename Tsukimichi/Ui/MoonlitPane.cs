using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
/// which the detail pane and the settings window reach through <see cref="IUniqueOverrides"/>. "Not unique (hide)"
/// in a row's context menu goes through the same <see cref="VerdictPrompt"/> as the detail pane's "Mark as unique";
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
/// <para>
/// Honest totals (feature plan v5, decision 4; <see cref="MoonlitGroups"/> and <see cref="MoonlitTally"/> in Core): a
/// row is a counted reward, so a reward several quests give (Guildhests from each city, a class from three quests, an
/// achievement nine quests award) is one row showing the quest that matters most to the viewed character, with the
/// others under "Also from" in the quest's tooltip and the row's menu; a relic or special weapon quest is one row
/// ("Honorbound (1 of 18)") with its repeatable "another job" twin. Rows whose every quest lies on another path are not
/// listed. Each row has an availability label (Get now, Event running, Upcoming event, Collab — may return, Past event —
/// on the Online Store, Gone for good); gone-for-good rewards the character lacks leave the totals unless "Count rewards
/// that are gone for good" (Configuration.MoonlitCountGone) is on, and the status strip says how many there are.
/// Expansion and State filters, "Group by expansion" (Configuration.MoonlitGroupByExpansion; headings in the table)
/// sit in the Filters popover and "Copy missing" (Markdown for Discord, in 2,000-character parts) in the toolbar.
/// </para>
/// <para>
/// A steady layout (feature plan v6, U4): the toolbar is one line whatever is set (search, Filters with a count badge,
/// Copy missing, the view toggle), and under it one reserved status strip holds the listed count, "Owned as of …",
/// the missed rewards and "Copied", so setting a filter or copying never moves the table.
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
    private int rowsBuild = -1;
    private CatalogBundle? rowsBundle;

    /// <summary>The art index revision (<see cref="MoonlitIconResolver.Revision"/>) the rows were built with.</summary>
    private int rowsArt = -1;
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

    // Counted rewards (feature plan v5, decision 4): the catalog folded per reward, built with the rows; each group's
    // state for the viewed character and the totals from the last RefreshObtained, which CountsFor reads.
    private MoonlitGroups groups = MoonlitGroups.Empty;
    private MoonlitGroupState[] groupStates = [];
    private MoonlitTotals totals = new();
    private bool countsGone;

    /// <summary>Rows the summary counts: in the unique view, on the character's path, and not left out as found elsewhere.</summary>
    private int listableCount;

    /// <summary>Edition year per festival of the rows' catalog (<see cref="Core.Seasonal.SeasonalNow.EditionYears"/>), for the availability labels.</summary>
    private IReadOnlyDictionary<ushort, int> editionYears = new Dictionary<ushort, int>();

    /// <summary>Last year's dates for the undated editions (<see cref="Core.Seasonal.SeasonalNow.EditionWindows"/>), for the availability labels.</summary>
    private IReadOnlyDictionary<ushort, Core.Seasonal.EditionWindow> editionWindows = new Dictionary<ushort, Core.Seasonal.EditionWindow>();

    /// <summary>The expansions the rows' quests belong to, in order, for the Expansion filter.</summary>
    private byte[] expansions = [];

    // Session-only filters beside the confidence combo (feature plan v5 R5 F5).
    private byte? expansionFilter;
    private MoonlitStateFilter stateFilter = MoonlitStateFilter.Any;

    /// <summary>
    /// The Added in filter (1.9.0, R9 F8): a patch series ("7.5" keeps 7.5, 7.51 and 7.55) the row's quest was added in,
    /// as the quest table's filter reads it; empty keeps every row. <see cref="patchSeries"/> holds the series the rows'
    /// quests carry, newest first, with their labels.
    /// </summary>
    private string addedInFilter = string.Empty;
    private (string Series, string Label)[] patchSeries = [];

    private static string[] StateFilterItems => stateFilterItemsText.Value;

    private static readonly Localization.LocArray stateFilterItemsText = new(static () =>
        [
        Strings.MoonlitStateAny,
        Strings.MoonlitStateReadyNow,
        Strings.MoonlitStateInJournal,
        Strings.MoonlitStateBlocked,
        Strings.MoonlitStateDone,
    ]);

    // "Copy missing": the Markdown of the listed rows not obtained, split into Discord-sized parts when the visible
    // rows change; the button copies the next part. copyLines are the lines the parts were written from, so a refresh
    // that lists the same lines (a poll, another session version) keeps the parts and the next part to copy.
    // copiedAt is ImGui time of the last copy (the "Copied" note).
    private IReadOnlyList<string> copyParts = [];
    private List<MoonlitMissingLine> copyLines = [];
    private int copyNext;
    private double copiedAt = double.NegativeInfinity;

    private int[] visible = [];
    private int visibleCount;
    private VisibleKey visibleKey;
    private string visibleSummary = string.Empty;
    private string filterText = string.Empty;

    /// <summary>Per expansion (by id), the "Endwalker (12)" heading of its group of rows when the table groups by expansion.</summary>
    private readonly Dictionary<byte, string> groupHeadings = [];

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

    void IUniqueOverrides.PutBack(IReadOnlyCollection<KeyValuePair<uint, UniqueOverride>> verdicts) => PutBackOverrides(verdicts);

    /// <summary>Icon lookup for reward entries (quest reward list first, then per-kind sheet fallbacks); shared with Wotsit.</summary>
    public MoonlitIconResolver Icons { get; }

    /// <summary>
    /// What every quest opens (feature plan v6 K4), for the quest tooltip's "Also opens: …" line; null until the plugin
    /// attaches it, which leaves the line out.
    /// </summary>
    public Core.Unlocks.QuestUnlocksSource? Unlocks { get; set; }

    /// <summary>
    /// Sprout mode's reach (<see cref="QueryRunner.UnlockReach"/>): rows past it stay out of "Also opens"; null names
    /// every row.
    /// </summary>
    public Func<byte>? UnlockReach { get; set; }

    // "Also opens: …" per quest, composed once per index revision, reach and Moonlit catalog.
    private readonly Dictionary<uint, string> alsoOpens = [];
    private int alsoOpensRevision = -1;
    private int alsoOpensShield;
    private byte alsoOpensReach = byte.MaxValue;
    private UniqueRewardCatalog? alsoOpensCatalog;

    /// <summary>
    /// "Also opens: Kugane · The Sirensong Sea" for a quest the shield does not mask; empty otherwise. What the quest's
    /// own Moonlit rows already show (its duty unlock, its aether current, its job: <see cref="Core.Unlocks.UnlockRewards"/>)
    /// is left out, as is what belongs to its Rewards (the index never draws a reward-class row: <see cref="Core.Unlocks.RewardSplit"/>).
    /// </summary>
    private string AlsoOpensText(QuestRecord quest)
    {
        if (Unlocks is not { } unlocks || session.Spoilers.IsMasked(quest))
        {
            return string.Empty;
        }

        var reach = UnlockReach?.Invoke() ?? byte.MaxValue;
        var moonlit = Catalog;
        var shield = session.Spoilers.Fingerprint;
        if (alsoOpensRevision != unlocks.Revision || alsoOpensReach != reach || !ReferenceEquals(alsoOpensCatalog, moonlit) || alsoOpensShield != shield)
        {
            alsoOpensShield = shield;
            alsoOpensRevision = unlocks.Revision;
            alsoOpensReach = reach;
            alsoOpensCatalog = moonlit;
            alsoOpens.Clear();
        }

        if (!alsoOpens.TryGetValue(quest.RowId, out var text))
        {
            var uniques = moonlit.ForQuest(quest.RowId);
            var entries = new List<Core.Unlocks.UnlockEntry>();
            // Matched against the Moonlit rows as they are; printed as the shield shows them (1.20.0 N6). Both lists
            // keep the same rows in the same order.
            var rows = unlocks.For(quest.RowId);
            var plain = Core.Unlocks.UnlockView.Visible(rows, masked: false, reach);
            var shown = Core.Unlocks.UnlockView.Visible(rows, masked: false, reach, session.Spoilers);
            for (var i = 0; i < plain.Count; i++)
            {
                var own = plain[i];
                if (!uniques.Any(unique => Core.Unlocks.UnlockRewards.Same(unique, own)))
                {
                    entries.Add(shown[i]);
                }
            }

            var places = Core.Unlocks.UnlockText.Places(entries);
            text = places.Length > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.MoonlitAlsoOpensFormat, places) : string.Empty;
            alsoOpens[quest.RowId] = text;
        }

        return text;
    }

    private static string AlsoFromAndOpens(string alsoFrom, string opens) => alsoFrom + "\n" + opens;

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

    /// <summary>Puts verdicts back as they were, dates included (the Undo of a restore or a note edit), in one save.</summary>
    public void PutBackOverrides(IReadOnlyCollection<KeyValuePair<uint, UniqueOverride>> verdicts)
    {
        ArgumentNullException.ThrowIfNull(verdicts);
        if (verdicts.Count == 0)
        {
            return;
        }

        foreach (var (rowId, stored) in verdicts)
        {
            overrides[rowId] = stored;
            overridesTouched.Add(rowId);
        }

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
    /// Obtained/total/unknown for one reward kind on the viewed character, with the same obtained logic and the same
    /// totals the kinds list shows (<see cref="MoonlitTally"/>: each reward once, other paths hidden, gone-for-good
    /// rewards left out unless counted). Memoized per <see cref="SessionState.Version"/>; the Characters dashboard reads
    /// it every frame.
    /// </summary>
    public UniqueRewardCounts CountsFor(RewardKind kind)
    {
        Refresh();
        return totals.For(kind);
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
        var art = Theme.Sectioned;
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

    /// <summary>
    /// Center column: the title, the one-line toolbar, the one-line status strip, and the reward table with a list
    /// clipper (or the gallery). The toolbar and the strip keep their height whatever is set or said, so the table never
    /// moves under the player (feature plan v6, U4).
    /// </summary>
    public void DrawMain(UiState ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        using var id = ImRaii.PushId("moonlitMain");
        Refresh();

        DrawTitle(ui);
        DrawToolbar(ui);

        // The note popup ("Add note" on the floating Undo) is begun here, in the centre column's scope, because the
        // context menu a verdict is given from lives inside the table's inner window and closes before a popup could be
        // shown from there.
        verdict.Draw(this);

        DrawStatusStrip(ui);

        if (rows.Length == 0)
        {
            ImGui.TextWrapped(Strings.MoonlitNoData);
            return;
        }

        if (visibleCount == 0)
        {
            // The shared empty state with one reset (1.7.0, onboarding proposal 9), as the quest table has.
            if (EmptyState.DrawWithAction(Strings.MoonlitNothingMatches, Strings.MoonlitEmptyBody, Strings.ResetFilters, moon: QuestState.Blocked) == EmptyState.ActionClicked)
            {
                ResetFilters(ui);
            }

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
                                      | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Sortable | ImGuiTableFlags.SortTristate;
        var tableWidth = ImGui.GetContentRegionAvail().X;
        using var table = ImRaii.Table(TableId, ColumnCount, Flags, new Vector2(-1f, -1f));
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
        var sortedWasHidden = SortColumnAutoHidden();
        FitColumns(tableWidth, glyphColumn, line);
        // ImGui drops the sort of a column the plan hides; it is written back on the frame the column returns.
        restoreSort |= sortedWasHidden && !SortColumnAutoHidden();
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn(Strings.MoonlitColumnObtained, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, glyphColumn);
        ImGui.TableSetupColumn(Strings.MoonlitColumnReward, ImGuiTableColumnFlags.WidthStretch, 3f);
        ImGui.TableSetupColumn(Strings.MoonlitColumnKind, ImGuiTableColumnFlags.WidthFixed | Planned(KindColumn), KindWidth());
        ImGui.TableSetupColumn(Strings.MoonlitColumnQuest, ImGuiTableColumnFlags.WidthStretch, 3f);
        ImGui.TableSetupColumn(Strings.MoonlitColumnState, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, glyphColumn);
        ImGui.TableSetupColumn(Strings.MoonlitColumnConfidence, ImGuiTableColumnFlags.WidthFixed | Planned(ConfidenceColumn), ConfidenceWidth());
        ImGui.TableSetupColumn(Strings.MoonlitColumnAvailability, ImGuiTableColumnFlags.WidthFixed | Planned(AvailabilityColumn), AvailabilityWidth());
        // The glyph columns follow IconScale, which imgui.ini's saved widths do not track; re-asserted every frame
        // (a no-op once they agree) so a changed IconScale never clips the moons.
        ImGuiP.TableSetColumnWidth(0, glyphColumn);
        ImGuiP.TableSetColumnWidth(4, glyphColumn);
        RecordPlayerHidden();

        // The persisted sort is written into the column state on the table's first frame (imgui.ini's own would win
        // otherwise), and again when the sorted column comes back from a plan hide; as the quest table does.
        if (!sortInitialized || restoreSort)
        {
            sortInitialized = true;
            restoreSort = false;
            ApplyInitialSort(settings.MoonlitSort());
        }

        ImGui.TableHeadersRow();
        if (ApplySortSpecs())
        {
            // A header click this frame: the rows below follow it now rather than a frame later.
            RefreshVisible(ui);
        }

        if (!clipperCreated)
        {
            clipper = ImGui.ImGuiListClipper();
            clipperCreated = true;
        }

        // Group headings are rows of the same height as a reward's, so the clipper's even spacing holds.
        var rowHeight = MathF.Max(line, RewardIconSize) + (ImGui.GetStyle().CellPadding.Y * 2f);
        moreFocusedNext = -1;
        clipper.Begin(visibleCount);
        while (clipper.Step())
        {
            for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                var index = visible[i];
                if (GroupedRows.IsHeading(index))
                {
                    DrawGroupHeading(GroupedRows.HeadingExpansion(index), rowHeight);
                }
                else
                {
                    DrawRow(ui, rows[index], line);
                }
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

    // The table's narrow-width plan (feature plan v4 L6): Confidence hides first, then Kind, then Availability; State
    // never. The plan hides a column with ImGuiTableColumnFlags.Disabled, which ImGui neither saves nor lists in the
    // header menu, so a column the player hides from that menu stays theirs and stays hidden, and one the plan hid comes
    // back when there is room. Availability (1.5) is the last column, so the saved order of the others holds.
    private const int ColumnCount = 7;
    private const int KindColumn = 2;
    private const int ConfidenceColumn = 5;
    private const int AvailabilityColumn = 6;
    private readonly bool[] columnsShown = new bool[ColumnCount];
    private readonly bool[] columnsWere = new bool[ColumnCount];
    private readonly float[] columnWidths = new float[ColumnCount];
    private readonly bool[] playerHidden = new bool[ColumnCount];
    private readonly bool[] autoHidden = new bool[ColumnCount];
    private bool columnsPlanned;

    private float availabilityWidth;
    private float kindHeaderWidth;
    private float confidenceHeaderWidth;
    private float availabilityWidthFont = -1f;
    private int availabilityWidthLanguage = -1;

    /// <summary>The Availability column's width: its widest label in the current font. Measured again only when the font size or the language changes.</summary>
    private float AvailabilityWidth()
    {
        MeasureColumns();
        return availabilityWidth;
    }

    /// <summary>The Kind column: its 110 px budget, or its header with the sort arrow when that is wider (a sorted header is never cut).</summary>
    private float KindWidth()
    {
        MeasureColumns();
        return MathF.Max(UiMetrics.Px(110f), kindHeaderWidth);
    }

    /// <summary>The Confidence column: its 80 px budget, or its header with the sort arrow when that is wider.</summary>
    private float ConfidenceWidth()
    {
        MeasureColumns();
        return MathF.Max(UiMetrics.Px(80f), confidenceHeaderWidth);
    }

    /// <summary>The measured column widths, again only when the font size or the language changes.</summary>
    private void MeasureColumns()
    {
        var font = ImGui.GetFontSize();
        if (font != availabilityWidthFont || availabilityWidthLanguage != Localization.Loc.Version)
        {
            availabilityWidthFont = font;
            availabilityWidthLanguage = Localization.Loc.Version;
            availabilityWidth = MeasureAvailability();
            kindHeaderWidth = SortedHeaderWidth(Strings.MoonlitColumnKind);
            confidenceHeaderWidth = SortedHeaderWidth(Strings.MoonlitColumnConfidence);
        }
    }

    /// <summary>A header label with room for the sort arrow beside it, so sorting never cuts the label (LayoutBudgets.SortArrowLogical).</summary>
    private static float SortedHeaderWidth(string label) => MathF.Ceiling(ImGui.CalcTextSize(label).X + UiMetrics.Px(LayoutBudgets.SortArrowLogical));

    private static float MeasureAvailability()
    {
        var widest = SortedHeaderWidth(Strings.MoonlitColumnAvailability);
        foreach (var kind in Enum.GetValues<RewardAvailability>())
        {
            widest = MathF.Max(widest, ImGui.CalcTextSize(Strings.MoonlitAvailability(new RewardAvailabilityInfo(kind), DateTime.UnixEpoch)).X);
        }

        // A running event with an announced end, at a wide date in the same year.
        var ends = new RewardAvailabilityInfo(RewardAvailability.EventRunning, new DateTime(1970, 12, 30, 0, 0, 0, DateTimeKind.Utc));
        widest = MathF.Max(widest, ImGui.CalcTextSize(Strings.MoonlitAvailability(ends, DateTime.UnixEpoch)).X);
        return MathF.Ceiling(widest);
    }

    /// <summary>Plans the table's columns for its width, before they are set up; a column the player hid takes no room.</summary>
    private void FitColumns(float tableWidth, float glyphColumn, float line)
    {
        var padding = ImGui.GetStyle().CellPadding.X * 2f;
        var nameMin = UiMetrics.Px(LayoutBudgets.RowNameMinLogical);
        var rewardMin = RewardIconSize + ImGui.GetStyle().ItemSpacing.X + nameMin + MoreSize(line);
        Span<ColumnSpec> specs = stackalloc ColumnSpec[ColumnCount];
        PaneFit.MoonlitColumns(
            glyphColumn + padding,
            rewardMin + padding,
            KindWidth() + padding,
            nameMin + padding,
            ConfidenceWidth() + padding,
            AvailabilityWidth() + padding,
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

    private static readonly int[] PlannedColumns = [KindColumn, ConfidenceColumn, AvailabilityColumn];

    // The column sort (owner request after 1.22.0): written into ImGui's column state on the table's first frame, and
    // again once a sorted column the plan hid is shown again (ImGui drops a hidden column's sort).
    private bool sortInitialized;
    private bool restoreSort;

    /// <summary>Whether the narrow-width plan hides the column the persisted sort is on (its header, and with it its arrow, are gone).</summary>
    private bool SortColumnAutoHidden() => MoonlitSort.TableColumnOf(settings.MoonlitSortColumn) is var column and >= 0 && autoHidden[column];

    /// <summary>
    /// Sets the table's sort column and direction from the persisted sort; the usual order clears every column's sort
    /// (allowed because the table is SortTristate). Called between the column setup and the header row.
    /// </summary>
    private static void ApplyInitialSort(MoonlitSortSpec initial)
    {
        var column = MoonlitSort.TableColumnOf(initial.Column);
        if (column < 0)
        {
            ImGuiP.TableSetColumnSortDirection(0, ImGuiSortDirection.None, appendToSortSpecs: false);
            return;
        }

        ImGuiP.TableSetColumnSortDirection(column, initial.Descending ? ImGuiSortDirection.Descending : ImGuiSortDirection.Ascending, appendToSortSpecs: false);
    }

    /// <summary>
    /// Reads the headers' sort (ImGui.TableGetSortSpecs) when it changed and saves it; true when the sort the rows
    /// follow changed. The rows are sorted in <see cref="RefreshVisible"/>, once per change, never per frame.
    /// </summary>
    private bool ApplySortSpecs()
    {
        var specs = ImGui.TableGetSortSpecs();
        if (specs.IsNull || !specs.SpecsDirty)
        {
            return false;
        }

        // ImGui clears the sort of a column the plan disables and reports no specs: that is the plan's doing, not a
        // header click, and the persisted sort stands (the rows keep following it).
        if (specs.SpecsCount == 0 && SortColumnAutoHidden())
        {
            specs.SpecsDirty = false;
            return false;
        }

        // No specs (the third header click) is the usual order.
        var sort = MoonlitSortSpec.Default;
        if (specs.SpecsCount > 0)
        {
            var spec = specs.Specs;
            sort = MoonlitSort.FromHeader(specs.SpecsCount, spec.ColumnIndex, spec.SortDirection == ImGuiSortDirection.Descending);
        }

        specs.SpecsDirty = false;
        if (sort == settings.MoonlitSort())
        {
            return false;
        }

        settings.MoonlitSortColumn = sort.Column;
        settings.MoonlitSortDescending = sort.Descending;
        settings.Save(pluginInterface);
        return true;
    }

    /// <summary>
    /// A reward's icon in the table, in logical pixels (feature plan v6 G6): readable at a glance, where a quest row's
    /// icon is 14. Drawn from the game's high-resolution texture; the row's height follows it.
    /// </summary>
    private const float RewardIconLogical = 24f;

    /// <summary>A reward's icon side in the table, in pixels.</summary>
    private static float RewardIconSize => UiMetrics.Icon(RewardIconLogical);

    /// <summary>How far a line of text sits down a row, so it is centred on the reward's icon.</summary>
    private static float TextDrop(float line) => MathF.Max(0f, MathF.Floor((RewardIconSize - line) * 0.5f));

    /// <summary>Moves the cursor down by <paramref name="drop"/> at the start of a cell.</summary>
    private static void DropInCell(float drop)
    {
        if (drop > 0f)
        {
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + drop);
        }
    }

    /// <summary>The "…" button's side in a row.</summary>
    private static float MoreSize(float line) => MathF.Min(UiMetrics.MinTarget, MathF.Max(line, RewardIconSize));

    /// <summary>The row's context menu, opened by a right-click, the Menu key or Shift+F10, or the "…" button.</summary>
    private const string RowMenuId = "ctx";

    // The row whose "…" button had keyboard focus last frame (it stays drawn while focused), and this frame's; -1 none.
    private int moreFocusedRow = -1;
    private int moreFocusedNext = -1;

    private const string FiltersPopupId = "##moonlitFilters";

    /// <summary>Under this much room for the search, Copy missing leaves the toolbar for the Filters popover, in logical pixels.</summary>
    private const float SearchMinLogical = 80f;

    private readonly TextFade copiedFade = new();
    private int filtersBadgeCount = -1;
    private string? filtersBadgeText;

    /// <summary>
    /// The toolbar (feature plan v6, U4), one line whatever is set: the search, the Filters button with a count badge
    /// (the filters themselves in its popover), Copy missing, and the table/gallery toggle at the right end. In a pane too
    /// narrow for all of it Copy missing moves into the popover; nothing ever wraps onto a second line.
    /// </summary>
    private void DrawToolbar(UiState ui)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var frame = ImGui.GetFrameHeight();
        var height = MathF.Max(frame, UiMetrics.MinTarget);
        var origin = ImGui.GetCursorScreenPos();
        var room = ImGui.GetContentRegionAvail().X;
        var toggleWidth = (UiMetrics.MinTarget * 2f) + spacing;
        var filtersWidth = Chrome.FiltersPillWidth();
        var copyWidth = CopyButtonWidth();
        var searchWidth = room - toggleWidth - filtersWidth - copyWidth - (3f * spacing);
        var copyInline = searchWidth >= UiMetrics.Px(SearchMinLogical);
        if (!copyInline)
        {
            searchWidth += copyWidth + spacing;
        }

        searchWidth = MathF.Max(UiMetrics.Px(40f), searchWidth);
        var frameY = origin.Y + ((height - frame) * 0.5f);
        ImGui.SetCursorScreenPos(new Vector2(origin.X, frameY));
        ImGui.SetNextItemWidth(searchWidth);
        ImGui.InputTextWithHint("##moonlitFilter", Strings.MoonlitFilterHint, ref filterText, FilterMaxLength);

        var x = origin.X + searchWidth + spacing;
        var count = NarrowingCount(ui);
        if (Chrome.FiltersPill("##moonlitFiltersButton", new Vector2(x, frameY), filtersWidth, frame, count, ImGui.IsPopupOpen(FiltersPopupId)))
        {
            ImGui.OpenPopup(FiltersPopupId);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.MoonlitFiltersTooltip, FiltersBadgeText(count));
        }

        DrawFiltersPopover(ui, count, copyInline ? 0f : copyWidth);
        RefreshVisible(ui);
        if (copyInline)
        {
            ImGui.SetCursorScreenPos(new Vector2(x + filtersWidth + spacing, frameY));
            DrawCopyMissing(copyWidth);
        }

        ImGui.SetCursorScreenPos(new Vector2(MathF.Max(x, origin.X + room - toggleWidth), origin.Y + ((height - UiMetrics.MinTarget) * 0.5f)));
        DrawViewToggle();

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(room, height));
    }

    /// <summary>How many narrowing filters are set (the badge): Hide obtained, confidence, expansion, state and Added in. The search shows itself.</summary>
    private int NarrowingCount(UiState ui) =>
        (ui.MoonlitHideObtained ? 1 : 0)
        + (confidenceFilter != ConfidenceFilter.Any ? 1 : 0)
        + (expansionFilter is null ? 0 : 1)
        + (stateFilter != MoonlitStateFilter.Any ? 1 : 0)
        + (addedInFilter.Length > 0 ? 1 : 0);

    /// <summary>The badge's meaning under the Filters tooltip, rebuilt only when the count changes; null with no badge.</summary>
    private string? FiltersBadgeText(int count)
    {
        if (count != filtersBadgeCount)
        {
            filtersBadgeCount = count;
            filtersBadgeText = count switch
            {
                0 => null,
                1 => Strings.MoonlitFiltersBadgeOne,
                _ => string.Format(CultureInfo.CurrentCulture, Strings.MoonlitFiltersBadgeFormat, count),
            };
        }

        return filtersBadgeText;
    }

    /// <summary>
    /// The Filters popover, roomy: what to list (Hide obtained, then confidence, expansion, state and Added in, each
    /// labelled), how to count and group (Group by expansion, Hide rewards found elsewhere, Count rewards that are gone
    /// for good), Copy missing when the toolbar had no room for it (<paramref name="copyWidth"/> over 0), and Reset
    /// filters for the narrowing ones. It opens from the centre column (own font scale 1), so it scales itself.
    /// </summary>
    private void DrawFiltersPopover(UiState ui, int count, float copyWidth)
    {
        if (!ImGui.IsPopupOpen(FiltersPopupId))
        {
            return;
        }

        using var style = Theme.PushPopup();
        using var roomy = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(UiMetrics.Px(14f), UiMetrics.Px(12f)))
            .Push(ImGuiStyleVar.ItemSpacing, new Vector2(UiMetrics.Px(10f), UiMetrics.Px(8f)));
        using var popup = ImRaii.Popup(FiltersPopupId);
        if (!popup)
        {
            return;
        }

        UiMetrics.ApplyFontScale();
        var hide = ui.MoonlitHideObtained;
        if (ImGui.Checkbox(Strings.MoonlitHideObtainedLabel, ref hide))
        {
            ui.MoonlitHideObtained = hide;
        }

        var labels = MathF.Max(
            MathF.Max(ImGui.CalcTextSize(Strings.MoonlitColumnConfidence).X, ImGui.CalcTextSize(Strings.ColumnExpansion).X),
            MathF.Max(ImGui.CalcTextSize(Strings.MoonlitColumnState).X, ImGui.CalcTextSize(Strings.AddedIn).X));
        var column = ImGui.GetStyle().WindowPadding.X + labels + UiMetrics.Px(16f);
        var comboWidth = UiMetrics.Px(190f);

        PopoverLabel(Strings.MoonlitColumnConfidence, column);
        ImGui.SetNextItemWidth(comboWidth);
        DrawConfidenceCombo();

        PopoverLabel(Strings.ColumnExpansion, column);
        ImGui.SetNextItemWidth(comboWidth);
        DrawExpansionCombo();

        PopoverLabel(Strings.MoonlitColumnState, column);
        ImGui.SetNextItemWidth(comboWidth);
        DrawStateCombo();

        PopoverLabel(Strings.AddedIn, column);
        ImGui.SetNextItemWidth(comboWidth);
        DrawAddedInCombo();

        ImGui.Separator();
        var groupBy = settings.MoonlitGroupByExpansion;
        if (ImGui.Checkbox(Strings.MoonlitGroupByExpansionLabel, ref groupBy))
        {
            settings.MoonlitGroupByExpansion = groupBy;
            settings.Save(pluginInterface);
        }

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

        var countGone = settings.MoonlitCountGone;
        if (ImGui.Checkbox(Strings.MoonlitCountGoneLabel, ref countGone))
        {
            settings.MoonlitCountGone = countGone;
            settings.Save(pluginInterface);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.MoonlitCountGoneTooltip);
        }

        ImGui.Separator();
        if (copyWidth > 0f)
        {
            DrawCopyMissing(copyWidth);
            ImGui.SameLine();
        }

        using (ImRaii.Disabled(count == 0))
        {
            if (ImGui.Button(Strings.ResetFilters))
            {
                ResetNarrowing(ui);
            }
        }
    }

    /// <summary>A popover row's label, centred on the control that follows it at <paramref name="column"/>.</summary>
    private static void PopoverLabel(string label, float column)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        ImGui.SameLine(column);
    }

    /// <summary>The popover's Reset filters: the narrowing choices the badge counts, back to their defaults. The kind, the search and the settings stay.</summary>
    private void ResetNarrowing(UiState ui)
    {
        ui.MoonlitHideObtained = false;
        confidenceFilter = ConfidenceFilter.Any;
        expansionFilter = null;
        stateFilter = MoonlitStateFilter.Any;
        addedInFilter = string.Empty;
    }

    /// <summary>
    /// The status strip under the toolbar (feature plan v6, U4): one line, always reserved, so nothing said here moves the
    /// table. Left to right: how many rewards are listed, "Owned as of …" for a stored character, the rewards missed for
    /// good, and the note while titles or achievements are worked out from quests, in the Dusk tone and cut to the room
    /// left with the whole text on hover; "Copied" at the right end for a moment after a copy, fading in.
    /// </summary>
    private void DrawStatusStrip(UiState ui)
    {
        var line = ImGui.GetTextLineHeight();
        var origin = ImGui.GetCursorScreenPos();
        var room = ImGui.GetContentRegionAvail().X;
        var gap = ImGui.GetStyle().ItemSpacing.X;
        var right = origin.X + room;

        var copied = ImGui.GetTime() - copiedAt < CopiedNoteSeconds ? Strings.MoonlitCopied : null;
        var alpha = copiedFade.Alpha(copied);
        if (copied is not null)
        {
            var mist = Theme.Surface.TextSecondary;
            right -= ImGui.CalcTextSize(copied).X;
            ImGui.GetWindowDrawList().AddText(new Vector2(right, origin.Y), Theme.WithAlpha(mist, mist.W * alpha), copied);
            right -= gap * 2f;
        }

        var x = Chrome.StripSegment(origin.X, origin.X, right, origin.Y, visibleSummary, ImGui.GetColorU32(ImGuiCol.TextDisabled));
        var dusk = Theme.U32(Theme.Surface.TextTertiary);
        if (!session.IsLive)
        {
            // A stored character's owned states are its last capture's ("Owned as of …"); older files have none.
            x = Chrome.StripSegment(x, origin.X, right, origin.Y, Strings.MoonlitOwnedNote(unlocks.StoredAsOfUtc), dusk);
        }

        // Rewards on the character's path that can no longer be had and are not theirs.
        if (totals.Missed > 0)
        {
            x = Chrome.StripSegment(x, origin.X, right, origin.Y, missedText, dusk, Strings.MoonlitMissedTooltip);
        }

        // Until the client has loaded the title or achievement list, those obtained marks are worked out from quests.
        if (session.IsLive && ui.MoonlitKind is { } shownKind && (shownKind is RewardKind.Title or RewardKind.Achievement) && !unlocks.ReadsExactly(shownKind))
        {
            Chrome.StripSegment(x, origin.X, right, origin.Y, Strings.MoonlitAchievementsFromQuests, dusk);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(room, line));
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

    /// <summary>The Expansion filter: All, or one of the expansions the rows' quests belong to.</summary>
    private void DrawExpansionCombo()
    {
        var names = session.Names;
        var label = expansionFilter is { } shown ? names.Expansion(shown) : Strings.MoonlitExpansionAll;
        using var combo = ImRaii.Combo("##moonlitExpansion", label);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.MoonlitExpansionFilterTooltip);
        }

        if (!combo)
        {
            return;
        }

        UiMetrics.ApplyFontScale();
        if (ImGui.Selectable(Strings.MoonlitExpansionAll, expansionFilter is null))
        {
            expansionFilter = null;
        }

        foreach (var expansion in expansions)
        {
            if (ImGui.Selectable(names.Expansion(expansion) + "##x" + expansion.ToString(CultureInfo.InvariantCulture), expansionFilter == expansion))
            {
                expansionFilter = expansion;
            }
        }
    }

    /// <summary>The Added in filter (1.9.0): any patch, or one patch series the rows' quests were added in.</summary>
    private void DrawAddedInCombo()
    {
        var label = addedInFilter.Length == 0 ? Strings.AddedInAny : string.Format(CultureInfo.CurrentCulture, Strings.AddedInChipFormat, addedInFilter);
        using (ImRaii.Disabled(patchSeries.Length == 0 && addedInFilter.Length == 0))
        using (var combo = ImRaii.Combo("##moonlitAddedIn", label))
        {
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(Strings.MoonlitAddedInTooltip);
            }

            if (!combo)
            {
                return;
            }

            UiMetrics.ApplyFontScale();
            if (ImGui.Selectable(Strings.AddedInAny, addedInFilter.Length == 0))
            {
                addedInFilter = string.Empty;
            }

            foreach (var (series, seriesLabel) in patchSeries)
            {
                if (ImGui.Selectable(seriesLabel, string.Equals(addedInFilter, series, StringComparison.Ordinal)))
                {
                    addedInFilter = series;
                }
            }
        }
    }

    /// <summary>The State filter: Any, Ready now, In journal, Blocked or Done, by the row's quest.</summary>
    private void DrawStateCombo()
    {
        using var combo = ImRaii.Combo("##moonlitState", StateFilterItems[(int)stateFilter]);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.MoonlitStateFilterTooltip);
        }

        if (!combo)
        {
            return;
        }

        UiMetrics.ApplyFontScale();
        for (var i = 0; i < StateFilterItems.Length; i++)
        {
            if (ImGui.Selectable(StateFilterItems[i], i == (int)stateFilter))
            {
                stateFilter = (MoonlitStateFilter)i;
            }
        }
    }

    /// <summary>How long the "Copied" note shows after a copy, in seconds.</summary>
    private const double CopiedNoteSeconds = 2.5;

    /// <summary>
    /// "Copy missing": copies the listed rows the character does not have as Markdown (<see cref="MoonlitMarkdown"/>);
    /// longer than a Discord message, one part per click, the button naming the part it copies next ("Copy part 2/3").
    /// "Copied" shows at the status strip's right end, never beside the button.
    /// </summary>
    private void DrawCopyMissing(float width)
    {
        using (ImRaii.Disabled(copyParts.Count == 0))
        {
            if (ImGui.Button(copyLabel, new Vector2(width, 0f)) && copyParts.Count > 0)
            {
                ImGui.SetClipboardText(copyParts[copyNext]);
                copyNext = (copyNext + 1) % copyParts.Count;
                copiedAt = ImGui.GetTime();
                UpdateCopyLabel();
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(copyParts.Count == 0 ? Strings.MoonlitCopyNothing : Strings.MoonlitCopyMissingTooltip);
        }
    }

    /// <summary>The Copy missing button's width: its label now, and never narrower than "Copy missing".</summary>
    private float CopyButtonWidth()
    {
        if (copyLabel.Length == 0 || copyLabelLanguage != Localization.Loc.Version)
        {
            UpdateCopyLabel();
        }

        var label = MathF.Max(ImGui.CalcTextSize(copyLabel, true, -1f).X, ImGui.CalcTextSize(Strings.MoonlitCopyMissing).X);
        return label + (ImGui.GetStyle().FramePadding.X * 2f);
    }

    /// <summary>
    /// Copy view as TSV (1.8.0): the rewards listed now (the kind, filters and search applied), under the export's
    /// Moonlit columns plus a link: the FFXIV Collect page (or its name search), else the item, else the quest on
    /// Garland Tools.
    /// </summary>
    private void CopyViewTsv()
    {
        var ids = links.ExternalIds;
        // Grouped by expansion, the list also holds heading entries (negative); only the rows are exported.
        var listed = GroupedRows.RowIndices(visible, visibleCount);
        var exported = new List<Core.Export.MoonlitExportRow>(listed.Count);
        foreach (var index in listed)
        {
            var row = rows[index];
            exported.Add(Core.Export.ExportWriter.Row(row.Entry, row.Obtained, row.Availability.Kind, ids));
        }

        ImGui.SetClipboardText(Core.Export.TableTsv.Moonlit(exported, r =>
            Core.Links.ExternalLinks.Reward(r.Kind, r.RewardId, r.ItemId, r.QuestRowId, r.RewardName, ids)));
    }

    private string copyLabel = string.Empty;
    private int copyLabelLanguage = -1;

    /// <summary>"Copy missing", or "Copy part 2/3" while a long list is being copied in parts; with its ImGui id.</summary>
    private void UpdateCopyLabel()
    {
        copyLabelLanguage = Localization.Loc.Version;
        var text = copyParts.Count > 1
            ? string.Format(CultureInfo.CurrentCulture, Strings.MoonlitCopyPartFormat, copyNext + 1, copyParts.Count)
            : Strings.MoonlitCopyMissing;
        copyLabel = text + "##copyMissing";
    }

    /// <summary>A group heading row of the table grouped by expansion: the expansion's name and its row count, as tall as a reward's row.</summary>
    private void DrawGroupHeading(byte expansion, float rowHeight)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(1);
        ImGui.Dummy(new Vector2(1f, MathF.Max(1f, rowHeight - (ImGui.GetStyle().CellPadding.Y * 2f))));
        ImGui.SameLine();
        using (Theme.PushText(Theme.Surface.Text))
        {
            ImGui.TextUnformatted(groupHeadings.GetValueOrDefault(expansion) ?? string.Empty);
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
    /// toggle (table or gallery) is the toolbar's last item (1.12.0).
    /// </summary>
    /// <summary>
    /// The empty state's Reset filters: every narrowing choice of this pane back to its default (all kinds, obtained
    /// shown, any confidence, expansion and state, no search). The store and gone-for-good switches are settings and stay.
    /// </summary>
    private void ResetFilters(UiState ui)
    {
        ui.MoonlitKind = null;
        ui.MoonlitHideObtained = false;
        confidenceFilter = ConfidenceFilter.Any;
        expansionFilter = null;
        stateFilter = MoonlitStateFilter.Any;
        addedInFilter = string.Empty;
        filterText = string.Empty;
    }

    private void DrawTitle(UiState ui)
    {
        var art = Theme.Sectioned;
        var start = ImGui.GetCursorScreenPos();
        var room = ImGui.GetContentRegionAvail().X;
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
            if (SectionHeading.Title(title, MathF.Max(0f, MathF.Min(titleWidth, room))) && ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(title);
            }

            var subtitle = ImGui.CalcTextSize(Strings.MoonlitSubtitle).X;
            if (ImGui.GetItemRectMax().X + ImGui.GetStyle().ItemSpacing.X + subtitle <= start.X + room)
            {
                ImGui.SameLine();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + MathF.Max(0f, titleLine - ImGui.GetTextLineHeight()) * 0.7f);
                using (Theme.PushText(Theme.Surface.TextTertiary))
                {
                    ImGui.TextUnformatted(Strings.MoonlitSubtitle);
                }
            }
        }
        else
        {
            using (Theme.PushText(Theme.Surface.TextTertiary))
            {
                ImGui.TextUnformatted(Strings.MoonlitSubtitle);
            }
        }

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
                // A mount's or minion's guide picture where the game has one, else the reward's own icon; the sunken
                // ground holds the place while either loads.
                var inner = icon - (2f * art);
                if ((row.Picture != 0 && GameIcon.TryGetWrap(textures, row.Picture, inner, out var wrap))
                    || GameIcon.TryGetWrap(textures, row.Icon, inner, out wrap))
                {
                    // A duty's emblem is no square: whole and centred (GameIcon.Fit).
                    var (artMin, artMax) = GameIcon.Fit(wrap, iconMin + new Vector2(art), iconMax - new Vector2(art));
                    dl.AddImageRounded(wrap.Handle, artMin, artMax, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding * 0.5f);
                }
            }
            else if (row.Shielded)
            {
                // A reward the shield hides: the 22 px moon-disc tile (spec-1.20 N6), centred.
                HiddenTile(dl, iconMin, iconMax);
            }
            else
            {
                // No art of its own: the kind's icon, faded, at half the tile.
                var kindInset = new Vector2(MathF.Round(icon * 0.25f));
                Orbit.DrawIcon(dl, textures, row.KindIcon, iconMin + kindInset, iconMax - kindInset, NoIconAlpha, rounding * 0.5f);
            }

            if (obtained)
            {
                dl.AddRect(iconMin, iconMax, Theme.GoldU32, rounding, ImDrawFlags.None, MathF.Max(1.5f, UiMetrics.Px(highContrast ? 2.5f : 1.5f)));
            }
            else
            {
                dl.AddRect(iconMin, iconMax, highContrast ? Theme.U32(Theme.Surface.StrongLine) : Theme.U32(s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
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
                dl.AddCircle(pip, pipRadius, highContrast ? Theme.U32(Theme.Surface.StrongLine) : Theme.U32(Theme.Surface.TextTertiary), 0, MathF.Max(1f, UiMetrics.Hairline));
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
            // A placeholder keeps the name's slot and turns Secondary (spec-1.20 N6).
            var nameInk = row.Hidden ? Theme.U32(Theme.Surface.TextTertiary) : row.Shielded ? Theme.U32(s.TextSecondary) : Theme.U32(s.Text);
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
                else if (row.Shielded)
                {
                    // A placeholder's three lines (spec-1.20 N6); its reveal is on the tile's right-click menu.
                    ShieldText.Hover(nameCut ? row.Name : null);
                }
                else if (row.Reward is { } reward)
                {
                    RewardTooltip.Draw(reward, links, textures, row.SourceText, row.BuyBack is null ? row.Entry.QuestRowId : row.BuyBackQuest, spoilers: session.Spoilers);
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
        dl.AddCircleFilled(center, radius, Theme.GoldU32);
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

        // The reward's icon sets the row's height; every cell's text and glyph is centred on it.
        var drop = TextDrop(line);

        // Obtained.
        ImGui.TableNextColumn();
        DropInCell(drop);
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
        DrawIcon(row, RewardIconSize);
        ImGui.SameLine();
        DropInCell(drop);
        using (Theme.PushText(row.Hidden ? Theme.Surface.TextTertiary : Theme.Surface.TextSecondary, row.Hidden || row.Shielded))
        {
            // A placeholder turns Secondary (spec-1.20 N6).
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
        else if (row.Shielded && ImGui.IsItemHovered())
        {
            // A placeholder's three lines (spec-1.20 N6); its reveal is on the row's right-click menu.
            ShieldText.Hover();
        }
        else if (row.ChoiceTooltip.Length > 0 && ImGui.IsItemHovered())
        {
            // A relic or special weapon quest: the items it offers, one per job, of which the character gets one.
            UiMetrics.Tooltip(row.Name, row.ChoiceTooltip);
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

        if (!row.Hidden && BuyBackOf(row) is { } buyBack)
        {
            // A shop sells it back once the quest is done, or to anyone (1.19, C6): what is truly missable reads apart.
            ImGui.SameLine();
            SmallDuskText(Core.Sources.BuyBacks.Mark);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(buyBack.Tooltip);
            }
        }

        // The "…" button at the reward cell's right end while the mouse is over the cell or the name (or the button)
        // has keyboard focus: a left click, Enter or Space opens the same menu (accessibility A6).
        var size = MoreSize(line);
        var cellMax = new Vector2(cellMin.X + cellWidth, cellMin.Y + MathF.Max(size, RewardIconSize));
        if (nameFocused || moreFocusedRow == row.Index || (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(cellMin, cellMax)))
        {
            // No cursor restore afterwards: TableNextColumn follows at once, and a set position folded into the
            // cell's CursorMaxPos would make the row taller while the button shows. In a narrow cell the button
            // stays right of the reward's icon rather than cover it (feature plan v4 L6). Centred on the icon.
            var moreX = MathF.Max(cellMin.X + RewardIconSize + ImGui.GetStyle().ItemSpacing.X, cellMax.X - size);
            var moreY = cellMin.Y + MathF.Max(0f, MathF.Floor((RewardIconSize - size) * 0.5f));
            Keyboard.MoreButton("##more", RowMenuId, new Vector2(moreX, moreY), size);
            if (ImGui.IsItemFocused())
            {
                moreFocusedNext = row.Index;
            }
        }

        // Kind.
        ImGui.TableNextColumn();
        DropInCell(drop);
        ImGui.TextUnformatted(row.KindName);

        // Quest: click reveals it in the Journal.
        ImGui.TableNextColumn();
        DropInCell(drop);
        if (row.Quest is { } quest)
        {
            using (Theme.PushText(Theme.Surface.TextTertiary, row.Hidden))
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
                // Other quests that give the same reward ("Also from …"), then what else the quest opens (feature plan
                // v6 K4), under the action.
                var opens = AlsoOpensText(quest);
                var detail = row.AlsoFromText.Length > 0
                    ? (opens.Length > 0 ? AlsoFromAndOpens(row.AlsoFromText, opens) : row.AlsoFromText)
                    : opens;
                UiMetrics.Tooltip(Strings.MoonlitShowInJournal, detail.Length > 0 ? detail : null);
            }
        }
        else
        {
            ImGui.TextDisabled(row.QuestName);
        }

        // Quest state for the viewed character.
        ImGui.TableNextColumn();
        DropInCell(drop);
        var state = session.States.TryGetValue(row.Entry.QuestRowId, out var evaluation) ? evaluation.State : QuestState.Unknown;
        MoonGlyph.DrawInline(state, UiMetrics.InlineGlyphSize(line));
        if (ImGui.IsItemHovered())
        {
            UiMetrics.StateTooltip(state, evaluation, row.Quest, session.Names, session.States);
        }

        // Confidence badge: what it means, with the source under it.
        ImGui.TableNextColumn();
        DropInCell(drop);
        using (Theme.PushText(row.ConfidenceColor))
        {
            ImGui.TextUnformatted(row.ConfidenceLabel);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(row.ConfidenceTooltip, row.SourceText);
        }

        // Availability: can the reward still be had, and how (feature plan v5, decision 4). Past and gone in Dusk.
        ImGui.TableNextColumn();
        DropInCell(drop);
        using (Theme.PushText(Theme.Surface.TextTertiary, row.Availability.Kind is RewardAvailability.GoneForGood or RewardAvailability.PastEventOnStore))
        {
            ImGui.TextUnformatted(row.AvailabilityText);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(row.AvailabilityText, row.AvailabilityTooltip);
        }
    }

    /// <summary>
    /// The reward's own game art (the high-resolution texture, a sunken square while it loads) with the blown-up reward
    /// tooltip on hover (the source line under it), or, for a reward without art of its own, its kind's icon, faded so
    /// it reads as a stand-in: the Obtained column already shows whether the reward is owned.
    /// </summary>
    private void DrawIcon(Row row, float size)
    {
        if (row.Reward is { } reward)
        {
            GameIcon.Draw(textures, row.Icon, size, hiRes: true);
            if (ImGui.IsItemHovered())
            {
                RewardTooltip.Draw(reward, links, textures, row.SourceText, row.BuyBack is null ? row.Entry.QuestRowId : row.BuyBackQuest, spoilers: session.Spoilers);
            }
        }
        else
        {
            var min = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(size, size));
            DrawKindStandIn(ImGui.GetWindowDrawList(), row, min, min + new Vector2(size, size));
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.MoonlitNoIconTooltip);
            }
        }
    }

    /// <summary>A reward without art of its own: its kind's icon (the game's menu icon, else the kind's glyph), faded, on the sunken ground.</summary>
    private void DrawKindStandIn(ImDrawListPtr dl, Row row, Vector2 min, Vector2 max)
    {
        if (row.Shielded)
        {
            HiddenTile(dl, min, max);
            return;
        }

        var side = max.X - min.X;
        var rounding = MathF.Round(side * 0.125f);
        dl.AddRectFilled(min, max, Theme.U32(Theme.Surface.Sunken), rounding);
        var inset = new Vector2(MathF.Round(side * 0.14f));
        Orbit.DrawIcon(dl, textures, row.KindIcon, min + inset, max - inset, NoIconAlpha, rounding * 0.5f);
    }

    /// <summary>The hidden-reward tile (spec-1.20 N6) centred in <paramref name="min"/>–<paramref name="max"/>: 22 px, or the room when smaller.</summary>
    private static void HiddenTile(ImDrawListPtr dl, Vector2 min, Vector2 max)
    {
        var side = MathF.Min(max.X - min.X, MathF.Round(UiMetrics.Px(22f)));
        Chrome.HiddenRewardTile(dl, new Vector2(MathF.Round((min.X + max.X - side) * 0.5f), MathF.Round((min.Y + max.Y - side) * 0.5f)), side);
    }

    /// <summary>Alpha of the kind's icon standing in where a reward's own art would go: present but clearly not the reward.</summary>
    private const float NoIconAlpha = 0.6f;

    private void DrawContextMenu(UiState ui, Row row)
    {
        var rowId = row.Entry.QuestRowId;
        if (row.Shielded)
        {
            // A placeholder's menu (spec-1.20 N6): Reveal this name, Reveal names in this quest.
            ShieldText.RevealItems(session, links, row.HiddenKind, row.HiddenName, row.Quest);
            ImGui.Separator();
        }

        if (row.Quest is { } quest && ImGui.MenuItem(Strings.MoonlitShowInJournal))
        {
            Reveal(ui, quest);
        }

        if (ImGui.MenuItem(Strings.RouteToThisReward))
        {
            ui.OpenRoute(Core.Route.RouteTarget.ForReward(row.Entry, catalog.All, row.Name) with { Icon = row.Icon });
        }

        // "Open on FFXIV Collect" and the item on Garland Tools (1.8.0); a masked quest's reward asks first.
        // A reward the wider shield hides asks too, naming it by its placeholder (1.20.0 N6).
        links.DrawRewardLinks(row.Entry, row.Shielded || (row.Quest is { } giver && session.Spoilers.IsMasked(giver)), row.Name);
        if (ImGui.MenuItem(Strings.LinksCopyViewTsv))
        {
            CopyViewTsv();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.LinksCopyViewTsvTooltip);
        }

        // The other quests on the character's path that give the same reward: each opens in the Journal.
        if (row.AlsoFrom.Count > 0)
        {
            ImGui.Separator();
            ImGui.TextDisabled(Strings.MoonlitAlsoFromMenu);
            foreach (var i in row.AlsoFrom)
            {
                if (row.QuestAt(i) is { } other && ImGui.MenuItem(row.QuestLabelAt(i)))
                {
                    Reveal(ui, other);
                }
            }
        }

        // Armed items (feature plan v6 S1): they act only while Ctrl or Shift is held, then the floating Undo follows.
        ImGui.Separator();
        if (overrides.ContainsKey(rowId))
        {
            verdict.DrawRestoreMenuItem(this, rowId);
        }
        else
        {
            verdict.DrawNotUniqueMenuItem(this, rowId, row.QuestName);
        }
    }

    /// <summary>Small Dusk text vertically centred on the current line, occupying its own width; hoverable through the reserved item.</summary>
    /// <summary>
    /// The row's buy-back (1.19, C6): the first of its quests whose reward a shop sells back, read once per item source
    /// index; null while the index is read or when no shop sells it back.
    /// </summary>
    private GameLinks.BuyBackText? BuyBackOf(Row row)
    {
        var sources = links.ItemSources;
        if (sources is null)
        {
            return null;
        }

        if (!ReferenceEquals(row.BuyBackSources, sources))
        {
            row.BuyBackSources = sources;
            row.BuyBack = null;
            row.BuyBackQuest = row.Entry.QuestRowId;
            for (var i = 0; i < row.QuestCount && row.BuyBack is null; i++)
            {
                var entry = row.EntryAt(i);
                if (links.BuyBackOf(entry.ItemId, entry.QuestRowId) is { } found)
                {
                    row.BuyBack = found;
                    row.BuyBackQuest = entry.QuestRowId;
                }
            }
        }

        return row.BuyBack;
    }

    private static void SmallDuskText(string text)
    {
        var size = ImGui.GetFontSize() * SmallTextScale;
        var extent = ImGui.CalcTextSize(text) * SmallTextScale;
        var lineHeight = ImGui.GetTextLineHeight();
        var pos = ImGui.GetCursorScreenPos();
        pos.Y += MathF.Round((lineHeight - extent.Y) * 0.5f);
        ImGui.GetWindowDrawList().AddText(ImGui.GetFont(), size, pos, Theme.U32(Theme.Surface.TextTertiary), text, 0f);
        ImGui.Dummy(new Vector2(extent.X, lineHeight));
    }

    /// <summary>A Dusk hairline through the middle of the last item's text (its own width, not the whole cell).</summary>
    private static void StrikeThrough(string text)
    {
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var y = MathF.Round((min.Y + max.Y) * 0.5f);
        var right = MathF.Min(max.X, min.X + ImGui.CalcTextSize(text, true, -1f).X);
        ImGui.GetWindowDrawList().AddLine(new Vector2(min.X, y), new Vector2(right, y), Theme.U32(Theme.Surface.TextTertiary), UiMetrics.Hairline);
    }

    /// <summary>Shows a quest in the Journal tab scoped to its genre (or the Unlisted bucket); also used by Wotsit picks.</summary>
    internal static void Reveal(UiState ui, QuestRecord quest) => ui.Reveal(quest);

    /// <summary>Catalog, rows and obtained states, each only when its inputs changed.</summary>
    private void Refresh()
    {
        EnsureCatalog();
        // The quest names are baked into the rows, so a spoiler mask that hides other names rebuilds them (T19).
        // The rewards' game art lands a moment after load (G6): the rows are built again with it then.
        if (rowsBuild != catalogBuild || !ReferenceEquals(rowsBundle, session.Bundle) || rowsSpoilers != session.Spoilers.Fingerprint || rowsLanguage != Localization.Loc.Version
            || rowsArt != Icons.Revision)
        {
            BuildRows();
        }

        // Titles and achievements switch to the game's own state once the client has loaded it (opening the Titles or
        // Achievements window); that bumps no session version, so the reader's own counter is watched too.
        var achievementState = unlocks.LiveStateVersion;
        if (obtainedVersion != session.Version || obtainedBuild != rowsBuild || countsHideStore != settings.MoonlitHideStoreResells
            || countsGone != settings.MoonlitCountGone || obtainedAchievementState != achievementState)
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

        // The catalog warmed at load serves when no verdict changed since it started (A11): nothing builds on the frame.
        if (warmCatalog is { IsCompletedSuccessfully: true } warmed && warmCatalogVersion == overridesVersion)
        {
            catalog = warmed.Result;
        }
        else
        {
            catalog = UniqueRewardCatalog.Build(session.UniqueRewards, overrides, session.Curated);
        }

        warmCatalog = null;
        catalogDirty = false;
        catalogBuild++;
    }

    // The merged catalog built on a worker at load (WarmCatalog), and the verdicts' version it was built from.
    private System.Threading.Tasks.Task<UniqueRewardCatalog>? warmCatalog;
    private int warmCatalogVersion = -1;

    /// <summary>
    /// Builds the merged catalog on a worker now (feature plan v6 A11), from a copy of the verdicts, so the first reader
    /// (the Duty Finder hint's index, the Rewards column, Moonlit's first open) takes it ready instead of building it on
    /// the frame. A verdict changed meanwhile makes the reader build afresh as before.
    /// </summary>
    public void WarmCatalog()
    {
        if (!catalogDirty || warmCatalog is not null)
        {
            return;
        }

        var unique = session.UniqueRewards;
        var curated = session.Curated;
        var verdicts = new Dictionary<uint, UniqueOverride>(overrides);
        warmCatalogVersion = overridesVersion;
        warmCatalog = System.Threading.Tasks.Task.Run(() => UniqueRewardCatalog.Build(unique, verdicts, curated));
    }

    /// <summary>The UI language the rows' labels were composed in.</summary>
    private int rowsLanguage = -1;

    /// <summary>
    /// One row per counted reward (<see cref="MoonlitGroups"/>: a reward several quests give, or a relic quest's items,
    /// is one row), then one per entry a "not unique" verdict hides. Labels are composed here once per catalog build.
    /// </summary>
    private void BuildRows()
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        // Only the art landed: the tiles already shown keep their place and do not fade in again.
        var artOnly = rowsBuild == catalogBuild && ReferenceEquals(rowsBundle, session.Bundle) && rowsLanguage == Localization.Loc.Version && rowsArt != Icons.Revision;
        rowsLanguage = Localization.Loc.Version;
        rowsArt = Icons.Revision;
        var bundle = session.Bundle;
        var spoilers = session.Spoilers;
        var hidden = catalog.Hidden;
        groups = MoonlitGroups.Build(catalog.All, id => bundle?.Catalog.GetByRowId(id) is { IsRepeatable: true });
        var grouped = groups.All;
        var built = new Row[grouped.Count + hidden.Count];
        QuestRecord? QuestOf(uint rowId) => bundle?.Catalog.GetByRowId(rowId);
        Row Make(int i, MoonlitGroup? group, UniqueRewardEntry entry, bool hiddenRow)
        {
            var art = new RowArt(Icons.Resolve(QuestOf(entry.QuestRowId), entry), Icons.Picture(entry), Icons.KindIcon(entry.Kind));
            return new Row(i, group, entry, QuestOf, art, hiddenRow, spoilers, bundle?.Language);
        }

        for (var i = 0; i < grouped.Count; i++)
        {
            built[i] = Make(i, grouped[i], grouped[i].Primary, hiddenRow: false);
        }

        // Rows hidden by a "not unique" verdict follow the view so the Yours filter can list them for Restore.
        for (var j = 0; j < hidden.Count; j++)
        {
            built[grouped.Count + j] = Make(grouped.Count + j, null, hidden[j], hiddenRow: true);
        }

        // The expansions the rows' quests belong to, for the Expansion filter.
        var present = new SortedSet<byte>();
        foreach (var row in built)
        {
            for (var q = 0; q < row.QuestCount; q++)
            {
                if (row.QuestAt(q) is { } quest)
                {
                    present.Add(quest.Expansion);
                }
            }
        }

        expansions = [.. present];
        if (expansionFilter is { } shown && !present.Contains(shown))
        {
            expansionFilter = null;
        }

        // The patch series the rows' quests were added in, newest first, for the Added in filter (1.9.0).
        var seriesCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in built)
        {
            if (!row.Hidden && row.Quest is { } quest && Core.Model.PatchVersion.IsPatch(quest.AddedIn))
            {
                var series = Core.Model.PatchVersion.Series(quest.AddedIn);
                seriesCounts[series] = seriesCounts.GetValueOrDefault(series) + 1;
            }
        }

        patchSeries = seriesCounts
            .OrderBy(static kv => kv.Key, Core.Model.PatchVersion.NewestFirst)
            .Select(static kv => (kv.Key, string.Format(CultureInfo.CurrentCulture, Strings.MoonlitAddedInOptionFormat, kv.Key, kv.Value)))
            .ToArray();
        if (addedInFilter.Length > 0 && !seriesCounts.ContainsKey(addedInFilter))
        {
            addedInFilter = string.Empty;
        }

        editionYears = bundle is null
            ? new Dictionary<ushort, int>()
            : Core.Seasonal.SeasonalNow.EditionYears(bundle.Catalog, session.Curated.Festivals);
        editionWindows = bundle is null
            ? new Dictionary<ushort, Core.Seasonal.EditionWindow>()
            : Core.Seasonal.SeasonalNow.EditionWindows(bundle.Catalog, session.Curated.Festivals, editionYears);
        groupStates = new MoonlitGroupState[grouped.Count];
        if (!artOnly || tileSeen.Length != built.Length)
        {
            tileSeen = new bool[built.Length];
        }

        rows = built;
        rowsBuild = catalogBuild;
        rowsBundle = bundle;
        rowsSpoilers = spoilers.Fingerprint;
        obtainedVersion = -1;
        log.Debug("Moonlit rows: {Count} built in {Ms:F1} ms ({Art})", built.Length, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds, rowsArt == 0 ? "art still loading" : "with game art");
    }

    /// <summary>A row's art: its icon (0 for none), its gallery picture (0 for none) and its kind's icon, worn when it has no icon.</summary>
    private readonly record struct RowArt(uint Icon, uint Picture, NodeIcon Fallback);

    /// <summary>
    /// Per row: the obtained state, the quest it shows, whether it is on the character's path and its availability
    /// (<see cref="MoonlitTally"/>); then the totals the kinds list and <see cref="CountsFor"/> read. Once per session
    /// version, and when the "found elsewhere" or "gone for good" toggle changes.
    /// </summary>
    private void RefreshObtained()
    {
        var hideStore = settings.MoonlitHideStoreResells;
        var countGone = settings.MoonlitCountGone;
        var bundle = session.Bundle;
        var now = DateTime.UtcNow;
        var context = bundle is null
            ? AvailabilityContext.None
            : new AvailabilityContext(session.Curated.Festivals, editionYears, session.ServerFestivals.Contains, now, editionWindows);
        var states = session.States;
        QuestEvaluation? EvaluationOf(uint rowId) => states.TryGetValue(rowId, out var evaluation) ? evaluation : null;
        RewardAvailabilityInfo AvailabilityOf(UniqueRewardEntry entry, QuestEvaluation? evaluation) =>
            RewardAvailabilities.Classify(entry, bundle?.Catalog.GetByRowId(entry.QuestRowId), evaluation, context);

        listableCount = 0;
        foreach (var row in rows)
        {
            MoonlitGroupState state;
            if (row.Group is { } group)
            {
                state = MoonlitTally.Evaluate(group, unlocks.IsObtained, EvaluationOf, AvailabilityOf);
                groupStates[group.Index] = state;
            }
            else
            {
                // Hidden by the user's verdict: its own entry, never counted.
                var evaluation = EvaluationOf(row.Entry.QuestRowId);
                state = new MoonlitGroupState(true, 0, unlocks.IsObtained(row.Entry), AvailabilityOf(row.Entry, evaluation), evaluation?.State == QuestState.Completed);
            }

            row.SetState(state, EvaluationOf, now);
            if (!row.Hidden && row.OnPath && !(hideStore && row.FoundElsewhere))
            {
                listableCount++;
            }
        }

        totals = MoonlitTally.Totals(groups.All, groupStates, new MoonlitCountOptions(countGone, hideStore));
        missedText = totals.Missed > 0 ? Strings.MoonlitMissed(totals.Missed) : string.Empty;

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
            var (o, t, u) = totals.For(kinds[i].Kind);
            kindItems[i].SetCounts(o, t, u);
        }

        var all = totals.All;
        allItem.SetCounts(all.Obtained, all.Total, all.Unknown);
        obtainedVersion = session.Version;
        obtainedBuild = rowsBuild;
        countsHideStore = hideStore;
        countsGone = countGone;
        visibleKey = default;
    }

    /// <summary>"3 time-limited rewards missed", composed when the totals change.</summary>
    private string missedText = string.Empty;

    /// <summary>
    /// The filtered index array, rebuilt when the kind, a toggle, a filter, the grouping or the obtained states change.
    /// Grouped by expansion, the rows are in expansion order (stable) and, in the table, each expansion opens with a
    /// heading entry (an expansion id <c>e</c> stored as <c>~e</c>). The "Copy missing" text follows the listed rows.
    /// </summary>
    private void RefreshVisible(UiState ui)
    {
        var hideStore = settings.MoonlitHideStoreResells;
        var groupBy = settings.MoonlitGroupByExpansion;
        var headings = groupBy && !settings.MoonlitGallery;
        var key = new VisibleKey(
            rowsBuild, obtainedVersion, ui.MoonlitKind, ui.MoonlitHideObtained, hideStore, confidenceFilter, filterText,
            expansionFilter, stateFilter, groupBy, headings, settings.MoonlitCountGone, Localization.Loc.Version, addedInFilter, settings.MoonlitSort());
        if (key == visibleKey)
        {
            return;
        }

        visibleKey = key;
        var filter = filterText.Trim();
        var picked = new List<int>(rows.Length);
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
            else if (!row.OnPath)
            {
                // Every quest that gives it lies on a path the character did not take (feature plan v5, decision 4).
                continue;
            }
            else if (!PassesConfidence(confidenceFilter, row.Entry.Confidence, row.Obtained))
            {
                continue;
            }

            if (expansionFilter is { } expansion && row.Expansion != expansion)
            {
                continue;
            }

            if (!MoonlitStateFilters.Passes(stateFilter, StateOf(row)))
            {
                continue;
            }

            if (addedInFilter.Length > 0 && (row.Quest is not { } rowQuest || !Core.Model.PatchVersion.InSeries(rowQuest.AddedIn, addedInFilter)))
            {
                continue;
            }

            if (filter.Length != 0 && !row.Matches(filter))
            {
                continue;
            }

            picked.Add(row.Index);
            if (!row.Hidden)
            {
                listed++;
            }
        }

        // The column sort the headers ask for (stable: ties keep the catalog's order); grouped, it holds within each expansion.
        MoonlitSort.Apply(picked, SortKeyOf, key.Sort);

        if (groupBy)
        {
            // Stable: within an expansion the rows keep the catalog's (or the column sort's) order.
            var order = new Dictionary<int, int>(picked.Count);
            for (var i = 0; i < picked.Count; i++)
            {
                order[picked[i]] = i;
            }

            picked.Sort((a, b) =>
            {
                var byExpansion = rows[a].Expansion.CompareTo(rows[b].Expansion);
                return byExpansion != 0 ? byExpansion : order[a].CompareTo(order[b]);
            });
        }

        groupHeadings.Clear();
        var capacity = picked.Count + (headings ? expansions.Length + 1 : 0);
        if (visible.Length < capacity)
        {
            visible = new int[capacity];
        }

        var names = session.Names;
        visibleCount = GroupedRows.Fill(picked, index => rows[index].Expansion, headings, visible, (expansion, size) =>
            groupHeadings[expansion] = names.Expansion(expansion) + " (" + size.ToString(CultureInfo.InvariantCulture) + ")");
        visibleSummary = listed.ToString(CultureInfo.InvariantCulture) + " / " + listableCount.ToString(CultureInfo.InvariantCulture);
        BuildCopyMissing(picked, groupBy);
    }

    /// <summary>
    /// What a row sorts by, as it shows: its printed names (a masked name is its placeholder), the quest's state as its
    /// moon reads it, and the confidence its badge shows.
    /// </summary>
    private MoonlitSortKey SortKeyOf(int index)
    {
        var row = rows[index];
        var maskedLevel = row.Quest is { } quest && session.Spoilers.IsMasked(quest) ? quest.DisplayLevel : -1;
        return new MoonlitSortKey(
            row.Obtained,
            row.Name,
            row.Entry.Kind,
            row.QuestName,
            maskedLevel,
            StateOf(row) ?? QuestState.Unknown,
            row.Hidden ? Confidence.UserOverride : row.Entry.Confidence,
            row.Availability);
    }

    /// <summary>The state of the quest a row shows for the viewed character; null when it has no evaluation.</summary>
    private QuestState? StateOf(Row row) => session.States.TryGetValue(row.Entry.QuestRowId, out var evaluation) ? evaluation.State : null;

    /// <summary>
    /// "Copy missing" over the listed rows: those the character does not have (unknown included), not hidden by a
    /// verdict, and not gone for good unless such rewards count; under their kind, or their expansion when grouped.
    /// Split into Discord-sized parts (<see cref="Core.Text.MessageSplitter"/>); the button starts at part 1 again only
    /// when the parts changed, so a session update in the middle of copying a long list keeps its place.
    /// </summary>
    private void BuildCopyMissing(List<int> picked, bool byExpansion)
    {
        var countGone = settings.MoonlitCountGone;
        var names = session.Names;
        var lines = new List<MoonlitMissingLine>();
        foreach (var index in picked)
        {
            var row = rows[index];
            if (row.Hidden || row.Obtained == true || (!countGone && row.Availability.IsGone))
            {
                continue;
            }

            var section = byExpansion ? names.Expansion(row.Expansion) : row.KindName;
            var note = row.Availability.Kind == RewardAvailability.GetNow ? string.Empty : row.AvailabilityText;
            lines.Add(new MoonlitMissingLine(section, row.Name, row.QuestName, note));
        }

        if (lines.SequenceEqual(copyLines))
        {
            return;
        }

        copyLines = lines;
        IReadOnlyList<string> parts = lines.Count == 0 ? [] : Core.Text.MessageSplitter.Split(MoonlitMarkdown.Write(lines));
        if (!parts.SequenceEqual(copyParts, StringComparer.Ordinal))
        {
            copyParts = parts;
            copyNext = 0;
        }

        UpdateCopyLabel();
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
        Confidence.Static => Theme.Surface.TextSecondary,
        Confidence.Community => Theme.UnknownText,
        Confidence.Curated => Theme.Surface.Text,
        Confidence.UserOverride => Theme.DangerText,
        _ => Theme.Surface.TextDisabled,
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
    /// One table row, a counted reward (<see cref="MoonlitGroup"/>), with every label pre-materialized: the reward's
    /// name, kind and badges once, and the name of each quest that gives it. What changes with the viewed character is
    /// set by <see cref="SetState"/>: the obtained state, which quest the row shows (the representative), whether it is
    /// on the character's path, its availability and the other quests ("Also from …"). A hidden row (kept out of the
    /// unique view by the user's verdict) is one entry of its own and wears the "yours" badge whatever its confidence.
    /// </summary>
    private sealed class Row
    {
        private static readonly int[] NoQuests = [];

        private readonly QuestRecord?[] quests;
        private readonly string[] questNames;
        private readonly string[] questLabels;
        private readonly UniqueRewardEntry[] questEntries;
        private readonly string searchText;
        private int representative;

        /// <param name="group">The counted reward; null for a row a verdict hides (<paramref name="entry"/> alone).</param>
        /// <param name="entry">The group's primary entry, or the hidden entry.</param>
        /// <param name="questOf">A quest of the catalog by row id; null when the catalog lacks it.</param>
        /// <param name="spoilers">The viewed character's shield: a masked quest's name is its placeholder here too.</param>
        /// <param name="catalogLanguage">The catalog's language: a non-English client prints the sheet's reward name (<see cref="RewardNames"/>).</param>
        /// <param name="art">The reward's icon, gallery picture and kind icon (<see cref="MoonlitIconResolver"/>).</param>
        public Row(int index, MoonlitGroup? group, UniqueRewardEntry entry, Func<uint, QuestRecord?> questOf, RowArt art, bool hidden, SpoilerMask spoilers, string? catalogLanguage)
        {
            Index = index;
            Group = group;

            // The wider shield (1.20.0 N6): a reward the story has not introduced is its placeholder, with the kind's
            // icon only (no art of its own, no gallery picture, no item tooltip).
            var primaryQuest = questOf(entry.QuestRowId);
            var rewardName = RewardNames.Display(entry, primaryQuest, catalogLanguage);
            // A duty or flying in a zone is placed as one, not as a reward (SpoilerMask.RewardName).
            var shielded = spoilers.IsRewardMasked(entry, rewardName);
            (HiddenKind, HiddenName) = spoilers.RewardName(entry, rewardName);
            var icon = shielded ? 0u : art.Icon;
            Shielded = shielded;
            Icon = icon;
            Picture = shielded ? 0u : art.Picture;
            KindIcon = art.Fallback;
            Hidden = hidden;
            IReadOnlyList<uint> ids = group is null ? [entry.QuestRowId] : group.Quests;
            quests = new QuestRecord?[ids.Count];
            questNames = new string[ids.Count];
            questLabels = new string[ids.Count];
            questEntries = new UniqueRewardEntry[ids.Count];
            for (var i = 0; i < ids.Count; i++)
            {
                var quest = questOf(ids[i]);
                quests[i] = quest;
                questNames[i] = quest is null ? string.Format(CultureInfo.InvariantCulture, Strings.MoonlitQuestFormat, ids[i]) : spoilers.DisplayName(quest);
                questLabels[i] = questNames[i] + "##q" + i.ToString(CultureInfo.InvariantCulture);
                questEntries[i] = group?.FirstOf(ids[i]) ?? entry;
            }

            KindName = Strings.MoonlitKindName(entry.Kind);
            var baseName = string.IsNullOrWhiteSpace(rewardName)
                ? KindName + " #" + entry.RewardId.ToString(CultureInfo.InvariantCulture)
                : shielded ? spoilers.RewardDisplay(entry, rewardName) : rewardName;
            var search = new List<string> { baseName };
            if (group is { IsChoice: true })
            {
                // A relic or special weapon quest: "Honorbound (1 of 18)", the items listed in the name's tooltip.
                Name = string.Format(CultureInfo.CurrentCulture, Strings.MoonlitChoiceFormat, baseName, group.Choices);
                var items = new List<string>();
                var seen = new HashSet<RewardKey>();
                foreach (var item in group.Entries)
                {
                    if (seen.Add(RewardKey.Of(item)))
                    {
                        var itemName = spoilers.RewardDisplay(item, RewardNames.Display(item, questOf(item.QuestRowId), catalogLanguage));
                        items.Add(itemName);
                        search.Add(itemName);
                    }
                }

                ChoiceTooltip = string.Format(CultureInfo.CurrentCulture, Strings.MoonlitChoiceTooltipFormat, group.Choices) + "\n" + string.Join("\n", items);
            }
            else
            {
                Name = baseName;
                ChoiceTooltip = string.Empty;
            }

            search.AddRange(questNames);
            search.Add(KindName);
            searchText = string.Join("\n", search);
            ConfidenceLabel = hidden ? Strings.MoonlitConfidenceUser : MoonlitPane.ConfidenceLabel(entry.Confidence);
            ConfidenceColor = hidden ? Theme.DangerText : MoonlitPane.ConfidenceColor(entry.Confidence);
            ConfidenceTooltip = hidden ? Strings.MoonlitBadgeHidden : MoonlitPane.ConfidenceTooltip(entry.Confidence);
            SourceText = string.IsNullOrWhiteSpace(entry.Source) ? Strings.MoonlitSourceUnknown : entry.Source;

            // The marks follow every entry of the reward: one sold on the store or dropping in a duty marks the row.
            var all = group?.Entries ?? [entry];
            var drop = (UniqueRewardEntry?)null;
            foreach (var e in all)
            {
                StoreResell |= e.SoldOnOnlineStore;
                if (drop is null && e.DropsInDuty)
                {
                    drop = e;
                }
            }

            DropsInDuty = drop is not null;
            DropTooltip = drop is null ? string.Empty : Strings.MoonlitAlsoDropsTooltip(spoilers.Name(SpoilerKind.Duty, drop.DropWhere));
            Reward = icon == 0 ? null : RewardFor(primaryQuest, entry, icon, baseName);
        }

        /// <summary>
        /// The reward the icon tooltip describes: the quest's own reward entry (same item, else same kind and id; what
        /// <see cref="MoonlitIconResolver.FromQuestRewards"/> matches), wearing the row's art when the reward has art of
        /// its own (a mount rather than its whistle), or one made from the catalog entry when the quest does not list it.
        /// </summary>
        private static RewardRef RewardFor(QuestRecord? quest, UniqueRewardEntry entry, uint icon, string name)
        {
            if (quest is not null)
            {
                foreach (var reward in quest.Rewards)
                {
                    if (entry.ItemId != 0 && reward.ItemId == entry.ItemId)
                    {
                        return reward.Icon == icon ? reward : reward with { Icon = icon };
                    }
                }

                foreach (var reward in quest.Rewards)
                {
                    if (entry.RewardId != 0 && reward.Kind == entry.Kind && reward.Id == entry.RewardId)
                    {
                        return reward.Icon == icon ? reward : reward with { Icon = icon };
                    }
                }
            }

            return new RewardRef(entry.Kind, entry.RewardId, entry.ItemId, 1, name, icon);
        }

        public int Index { get; }

        /// <summary>The counted reward; null for a row a verdict hides.</summary>
        public MoonlitGroup? Group { get; }

        /// <summary>The entry of the quest the row shows (the representative's).</summary>
        public UniqueRewardEntry Entry => questEntries[representative];

        /// <summary>The quest the row shows; null when the catalog lacks it.</summary>
        public QuestRecord? Quest => quests[representative];

        public string QuestName => questNames[representative];

        public string QuestLabel => questLabels[representative];

        /// <summary>How many quests give the reward.</summary>
        public int QuestCount => quests.Length;

        public QuestRecord? QuestAt(int index) => quests[index];

        /// <summary>The quest's name with an ImGui id unique within the row, for a menu item.</summary>
        public string QuestLabelAt(int index) => questLabels[index];

        /// <summary>The expansion of the quest the row shows (0 when unknown): what the Expansion filter and the grouping read.</summary>
        public byte Expansion => Quest?.Expansion ?? 0;

        /// <summary>The reward's own icon; 0 when it has none (<see cref="KindIcon"/> is worn instead).</summary>
        public uint Icon { get; }

        /// <summary>The wider spoiler shield hides the reward (1.20.0 N6): its placeholder, the moon-disc tile, no art.</summary>
        public bool Shielded { get; }

        /// <summary>The kind the shield places the reward's name under (<see cref="SpoilerMask.RewardName"/>): what "Reveal this name" reveals.</summary>
        public SpoilerKind HiddenKind { get; }

        /// <summary>The reward's name as the shield places it; never shown while <see cref="Shielded"/>.</summary>
        public string HiddenName { get; }

        /// <summary>The reward's large picture for a gallery tile (a mount's or minion's guide art); 0 when it has none.</summary>
        public uint Picture { get; }

        /// <summary>The reward kind's icon, worn by a reward without art of its own.</summary>
        public NodeIcon KindIcon { get; }

        public bool Hidden { get; }
        public string Name { get; }
        public string KindName { get; }

        /// <summary>For a relic or special weapon quest, the items it offers under a line that says one is received; empty otherwise.</summary>
        public string ChoiceTooltip { get; }
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

        /// <summary>
        /// The item sources <see cref="BuyBack"/> was read from (1.19, C6); it is read again when they land or change.
        /// A buy-back never hides the row: the quest still has to be done first, or the shop is one more way to it.
        /// </summary>
        public ItemSourceIndex? BuyBackSources { get; set; }

        /// <summary>How the reward can be had again from a shop; null when none sells it back.</summary>
        public GameLinks.BuyBackText? BuyBack { get; set; }

        /// <summary>The quest whose reclaim row <see cref="BuyBack"/> found (the representative's when none did).</summary>
        public uint BuyBackQuest { get; set; }

        /// <summary>The entry the row holds for its quest at <paramref name="index"/>.</summary>
        public UniqueRewardEntry EntryAt(int index) => questEntries[index];

        /// <summary>What the icon's tooltip describes; null when the row has no icon (its kind's icon stands in, faded).</summary>
        public RewardRef? Reward { get; }

        public bool? Obtained { get; private set; }
        public Mark ObtainedGlyph { get; private set; } = Mark.Unknown;
        public string ObtainedText { get; private set; } = Strings.MoonlitObtainedUnknown;

        /// <summary>False when every quest that gives the reward lies on a path the character did not take (and the reward is not theirs): the row is not listed.</summary>
        public bool OnPath { get; private set; } = true;

        public RewardAvailabilityInfo Availability { get; private set; }
        public string AvailabilityText { get; private set; } = string.Empty;
        public string AvailabilityTooltip { get; private set; } = string.Empty;

        /// <summary>Gone for good and missed (<see cref="MoonlitGroupState.Missed"/>): left out of the totals unless they count gone rewards.</summary>
        public bool Missed { get; private set; }

        /// <summary>Indexes (into the row's quests) of the other quests on the character's path that give the reward.</summary>
        public IReadOnlyList<int> AlsoFrom { get; private set; } = NoQuests;

        /// <summary>"Also from Quest B, Quest C" for the quest's tooltip; empty when no other quest gives it.</summary>
        public string AlsoFromText { get; private set; } = string.Empty;

        /// <summary>Applies the viewed character's state of the reward; <paramref name="evaluationOf"/> tells which other quests are on its path.</summary>
        public void SetState(MoonlitGroupState state, Func<uint, QuestEvaluation?> evaluationOf, DateTime nowUtc)
        {
            Obtained = state.Obtained;
            (ObtainedGlyph, ObtainedText) = state.Obtained switch
            {
                // Only Allagan Tools answers yes for a relic or special weapon (1.6.0), so the line says where it came from.
                true => (Mark.Check, Game.RewardUnlockReader.OwnedPerAllagan(Entry) ? Strings.HandInOwnedPerAllagan : Strings.MoonlitObtainedYes),
                false => (Mark.Cross, Strings.MoonlitObtainedNo),
                null => (Mark.Unknown, Strings.MoonlitObtainedUnknown),
            };

            representative = Math.Clamp(state.Representative, 0, quests.Length - 1);
            OnPath = state.OnPath;
            Missed = state.Missed;
            if (Availability != state.Availability || AvailabilityText.Length == 0)
            {
                Availability = state.Availability;
                AvailabilityText = Strings.MoonlitAvailability(state.Availability, nowUtc);
                AvailabilityTooltip = Strings.MoonlitAvailabilityTooltip(state.Availability.Kind);
            }

            if (quests.Length < 2)
            {
                return;
            }

            var others = new List<int>(quests.Length - 1);
            for (var i = 0; i < quests.Length; i++)
            {
                if (i != representative && (quests[i] is null || !MoonlitTally.IsOffPath(evaluationOf(quests[i]!.RowId))))
                {
                    others.Add(i);
                }
            }

            AlsoFrom = others;
            if (others.Count == 0)
            {
                AlsoFromText = string.Empty;
                return;
            }

            var names = new string[others.Count];
            for (var i = 0; i < names.Length; i++)
            {
                names[i] = questNames[others[i]];
            }

            AlsoFromText = string.Format(CultureInfo.CurrentCulture, Strings.MoonlitAlsoFromFormat, string.Join(", ", names));
        }

        public bool Matches(string filter) => searchText.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct VisibleKey(
        int Build,
        int Version,
        RewardKind? Kind,
        bool HideObtained,
        bool HideStore,
        ConfidenceFilter Confidence,
        string Filter,
        byte? Expansion,
        MoonlitStateFilter State,
        bool GroupByExpansion,
        bool Headings,
        bool CountGone,
        int Language,
        string AddedIn,
        MoonlitSortSpec Sort);
}

/// <summary>
/// Game art for a unique-reward entry (feature plan v6 G6), from the <see cref="RewardArtIndex"/> read off the frame at
/// load (<see cref="Art"/>). A collectible's own art comes first, so a mount, minion, emote, orchestrion roll, Triple
/// Triad card, barding, fashion accessory or hairstyle shows itself rather than the generic icon of the item that
/// unlocks it. Then the quest's own reward list (same item, else same kind and id); then the kind's sheet icon: duty
/// unlocks and instances their content type's, jobs the 062100-series job icon, aether currents the attunement crystal,
/// traits, achievements, actions, blue mage spells and other rewards their own, items theirs, titles the icon of the
/// achievement that awards them, and system unlocks the icon of the game menu they belong to. 0 when none is known;
/// the row then wears its kind's icon (<see cref="KindIcon"/>). Until the index lands only the quest's reward list
/// answers; <see cref="Revision"/> moves when it does, so the rows are built again. Reads no sheet itself apart from
/// the menu icons of <see cref="KindIcon"/> (memoized per kind).
/// </summary>
public sealed class MoonlitIconResolver(IDataManager data, IPluginLog log)
{
    /// <summary>The aether current attunement crystal in the 060000 icon set (verified against the shipped textures).</summary>
    public const uint AetherCurrentIcon = RewardArtIndex.AetherCurrentIcon;

    /// <summary>First job icon in the 062000 set: 062101 Gladiator … 062142 Pictomancer, offset by ClassJob row id.</summary>
    public const uint ClassJobIconBase = RewardArtIndex.ClassJobIconBase;

    private readonly Dictionary<RewardKind, NodeIcon> kindIcons = [];
    private RewardArtIndex? kindIconsIndex;
    private bool warned;

    /// <summary>The art index once it has landed; null while it is read (and when unset). Set by the plugin.</summary>
    public Func<RewardArtIndex?>? Art { get; set; }

    /// <summary>0 until the art index has landed, then 1: what the rows were built with.</summary>
    public int Revision => Art?.Invoke() is null ? 0 : 1;

    /// <summary>Whether <paramref name="kind"/> has art of its own that beats the icon of the item unlocking it.</summary>
    public static bool HasOwnArt(RewardKind kind) => kind is RewardKind.Mount or RewardKind.Minion or RewardKind.Emote
        or RewardKind.Orchestrion or RewardKind.TripleTriadCard or RewardKind.Barding or RewardKind.Ornament or RewardKind.Hairstyle;

    /// <summary>Icon id for the entry, or 0 when none is known.</summary>
    public uint Resolve(QuestRecord? quest, UniqueRewardEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var index = Art?.Invoke();
        if (index is not null && HasOwnArt(entry.Kind) && index.Icon(entry.Kind, entry.RewardId) is not 0 and var own)
        {
            return own;
        }

        var fromQuest = FromQuestRewards(quest, entry);
        if (fromQuest != 0 || index is null)
        {
            return fromQuest;
        }

        if (entry.Kind == RewardKind.SystemUnlock)
        {
            return index.FeatureIcon(entry.RewardName);
        }

        var (kind, id) = RewardArtIndex.KeyOf(entry);
        return id == 0 ? 0u : index.Icon(kind, id);
    }

    /// <summary>
    /// The reward's large picture for the gallery (a mount's or minion's 384 px guide art), or 0 when it has none or
    /// the index has not landed.
    /// </summary>
    public uint Picture(UniqueRewardEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return Art?.Invoke() is { } index ? index.Art(entry.Kind, entry.RewardId) : 0u;
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

        // The art index carries the menu icons: once it lands they are read from it, not from the sheet.
        var index = Art?.Invoke();
        if (!ReferenceEquals(kindIconsIndex, index))
        {
            kindIconsIndex = index;
            kindIcons.Clear();
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
                var menu = index is not null ? index.MenuIcon(row)
                    : data.GetExcelSheet<MainCommand>()?.GetRowOrDefault(row) is { Icon: > 0 } command ? (uint)command.Icon : 0u;
                if (menu != 0)
                {
                    icon = NodeIcon.Game(menu);
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

    private void WarnOnce(Exception ex, RewardKind kind)
    {
        if (warned)
        {
            log.Debug(ex, "Icon lookup for {Kind} failed", kind);
            return;
        }

        warned = true;
        log.Warning(ex, "Icon lookup for {Kind} failed; the kind's glyph stands in", kind);
    }
}
