using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Characters;
using Tsukimichi.Core.Diff;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Unique;
using Tsukimichi.Config;
using Tsukimichi.Game;
using Tsukimichi.GameData;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Characters (spec §7, F-50..F-61, F-73). <see cref="DrawLeft"/> lists every stored snapshot (the live one marked ●)
/// and switches the viewed character; <see cref="DrawMain"/> is a dashboard for the viewed character, top to bottom:
/// header, completion per journal section (filling moons), job and role quest ladders, curated story chains, a
/// comparison with another stored character (the alt diff, V2-12), the Moonlit summary, pinned quests, abandoned quests
/// (P10, <c>CharactersPane.Abandoned.cs</c>), seasonal events and history (P11, <c>CharactersPane.Seasonal.cs</c>), recent activity, jobs
/// grouped by role with the game's job icons, Grand Company and allied societies, Export JSON and Forget (with a
/// confirm popup), and the Account view: the state of <see cref="UiState.SelectedRowId"/> on every character,
/// evaluated offline from their snapshots.
/// <para>
/// The Moon Road look (feature plan v4 V5, proposal §7.6), at Flair Full and Quiet: a framed header (the current job's
/// icon in a frame with corner marks at Full, the name in the Title role, the world in the secondary tone and the overall
/// orbit gauge on the right, under the name below 360 px), a grid of section rings above the completion table (one orbit
/// per journal section with its official icon, its count in the Numeral role and its name, ⌊width / 120⌋ columns), and
/// open-section headings (sigil, eyebrow, fading rule) in place of the separators. Every table stays. Plain keeps the
/// look before 1.4.
/// </para>
/// <para>
/// Every label, count and icon id is built once per <see cref="SessionState.Version"/> or viewed character (and once
/// a minute for the ages); nothing allocates per frame in the table bodies. Snapshots of other characters are loaded
/// through <c>loadSnapshot</c> only when their capture time changed, and their evaluations are memoized per version.
/// </para>
/// </summary>
public sealed partial class CharactersPane
{
    private const string ExportsFolder = "exports";
    private const int MaxRecentRows = 10;
    private const int MaxPinnedRows = 50;
    private const int MaxDiffRows = 25;
    private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(8);

    /// <summary>The account view's "live in another client" badge (multibox, D11), built once per language.</summary>
    private static readonly LocText MultiboxBadge = new(static () => Strings.MultiboxMarker.TrimEnd());

    // The fixed columns' widths, measured from their headers and widest cells when the rows, the font or the language
    // change (feature plan v4 L6); a width guessed in pixels clipped "3123/5402" and "Completed" mid-glyph.
    private static readonly FixedWidth SectionsDoneWidth = new();
    private static readonly FixedWidth SectionsPercentWidth = new();
    private static readonly FixedWidth JobQuestsLevelWidth = new();
    private static readonly FixedWidth JobQuestsDoneWidth = new();
    private static readonly FixedWidth ChainsDoneWidth = new();
    private static readonly FixedWidth NotStartedDoneWidth = new();
    private static readonly FixedWidth MoonlitObtainedWidth = new();
    private static readonly FixedWidth RecentTimeWidth = new();
    private static readonly FixedWidth RecentEventWidth = new();
    private static readonly FixedWidth JobsLevelWidth = new();
    private static readonly FixedWidth TribesReputationWidth = new();
    private static readonly FixedWidth DiffCountWidth = new();
    private static readonly FixedWidth DiffViewedValueWidth = new();
    private static readonly FixedWidth DiffOtherValueWidth = new();
    private readonly FixedWidth accountStateWidth = new();

    /// <summary>A moon column: the inline glyph with a little air, never narrower than the glyph.</summary>
    private static float GlyphColumn(float line) => MathF.Max(line * 1.4f, UiMetrics.InlineGlyphSize(line));

    // Motion keys of the dashboard's halos (T17): one table per block in the high bits of the id, the row index in the
    // low; a halo's fill eases only when its fraction changes (another character viewed, a quest completed).
    private const uint DashboardGaugeTag = 0x4441_5347; // "DASG"
    private const uint SectionsGauges = 0x1_0000;
    private const uint JobGauges = 0x2_0000;
    private const uint ChainGauges = 0x3_0000;
    private const uint NotStartedGauges = 0x4_0000;
    private const uint MoonlitGauges = 0x5_0000;
    private const uint SectionGridGauges = 0x6_0000;
    private const uint HeaderGauge = 0x7_0000;

    /// <summary>The least room a heading keeps beside a button on its line before the button moves under it.</summary>
    private const float HeadingLeastLogical = 80f;

    /// <summary>The gap between an icon leading a line and its text (<see cref="DrawLeadIcon"/>), logical px.</summary>
    private const float LeadIconGapLogical = 6f;

    /// <summary>The section rings show this many rows until "Show all sections" (the table under them lists every one).</summary>
    private const int SectionRingRows = 2;

    // Whether the section rings show every row rather than the first SectionRingRows.
    private bool sectionRingsAll;

    // The node icons the dashboard was built with (SessionState.NodeIcons, resolved off the draw thread once per
    // catalog): a new map rebuilds the section rings.
    private NodeIconMap? dashboardIcons;

    /// <summary>Reward kinds the dashboard summarizes, in display order.</summary>
    private static readonly RewardKind[] SummaryKinds =
    [
        RewardKind.Emote, RewardKind.Mount, RewardKind.Minion, RewardKind.Orchestrion, RewardKind.TripleTriadCard, RewardKind.DutyUnlock,
    ];

    /// <summary>Same shape as the store writes (camelCase, enums as names), for exporting a character that has no file yet.</summary>
    private static readonly JsonSerializerOptions ExportJson = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly SessionState session;
    private readonly PluginPaths paths;
    private readonly IPluginLog log;
    private readonly Func<ulong, CharacterSnapshot?> loadSnapshot;
    private readonly IDataManager? data;
    private readonly ITextureProvider? textures;
    private readonly CharacterRoster roster;
    private readonly Configuration settings;
    private readonly System.Action saveSettings;

    private Dictionary<uint, string>? worldNames;
    private readonly Dictionary<uint, string> zoneNames = [];

    // The characters every dashboard list reads (Compare with, the account view, the grid): hidden ones left out
    // (1.8.0, R7 E), in the roster's stable order (R7 B), with their CharacterEntry beside them for the Core rules.
    private CharacterItem[] items = [];
    private List<CharacterEntry> itemEntries = [];
    private int itemsVersion = -1;
    private int itemsRoster = -1;
    private long itemsMinute = -1;

    private Dashboard? dashboard;
    private DashboardKey dashboardKey;

    // Per-job ladders, built once per catalog bundle (the named chains are the session's).
    private JobLadder ladder = JobLadder.Empty;
    private CatalogBundle? derivedBundle;

    // Snapshot is null when the file could not be read at that capture time; the failure is cached too so an
    // unreadable file is not re-read on every session version bump.
    private readonly Dictionary<ulong, (DateTime Taken, CharacterSnapshot? Snapshot)> snapshotCache = [];
    private readonly Dictionary<ulong, (QuestEvaluation? Evaluation, string Reason)> accountCache = [];
    private uint accountRowId;
    private int accountVersion = -1;

    // Compare with (V2-12): the other character is the one remembered for the viewed one in user/characters.json, else
    // the first other one in the list (1.8.0, R7 B: a fixed choice, never "the newest save"); the other characters'
    // offline evaluations memoized per capture time, bundle and the server festivals they were resolved with (Live
    // says those were the live character's flags, which change under a stored character), and the view model with its key.
    private Compare? compare;
    private CompareKey compareKey;
    private readonly Dictionary<ulong, (DateTime Taken, CatalogBundle Bundle, ServerFestivals? Live, IReadOnlyDictionary<uint, QuestEvaluation>? States)> compareStates = [];

    // Re-evaluations of a compared character on a worker (a newer copy another client saved, or other festivals),
    // keyed like compareStates; the previous states are shown until one lands (TakeCompareResolves).
    private readonly Dictionary<ulong, (DateTime Taken, CatalogBundle Bundle, ServerFestivals? Live, Task<IReadOnlyDictionary<uint, QuestEvaluation>> Task)> compareResolving = [];
    private UniqueRewardCatalog? fallbackRewards;

    // The reset cycle compareStates was resolved in (GameResets.Cycle); a reset passing clears it.
    private (DateTime Daily, DateTime Weekly) compareCycle;

    private string? toast;
    private DateTime toastUntilUtc;
    private string forgetQuestion = string.Empty;
    private string forgetName = string.Empty;

    // The safety table (feature plan v6 S2): Don't track is armed, Forget is press and hold.
    private readonly ClickGuard trackGuard = new();
    private readonly ConfirmGate forgetGate = new();

    private static string ForgetConfirmLabel => forgetConfirmLabelText.Value;

    private static readonly LocText forgetConfirmLabelText = new(static () => Strings.CharactersForgetConfirm + Chrome.HoldIdSuffix);
    private ulong forgetTarget;

    /// <param name="loadSnapshot">Loads a stored character by content id (e.g. <c>SnapshotService.Load</c>); null when unreadable.</param>
    /// <param name="roster">Every character in the alt lists' stable order, with hidden, not tracked and the Compare target (1.8.0).</param>
    /// <param name="settings">The list's data center grouping and "show hidden" (Settings › Data › Characters).</param>
    /// <param name="saveSettings">Saves <paramref name="settings"/> after the list's "Show hidden" toggle.</param>
    /// <param name="data">Optional; resolves world names from the World sheet. Without it the world id is shown.</param>
    /// <param name="textures">Optional; draws the game's job icons next to job levels. Without it the rows are text only.</param>
    public CharactersPane(
        SessionState session,
        PluginPaths paths,
        IPluginLog log,
        Func<ulong, CharacterSnapshot?> loadSnapshot,
        CharacterRoster roster,
        Configuration settings,
        System.Action saveSettings,
        IDataManager? data = null,
        ITextureProvider? textures = null)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.loadSnapshot = loadSnapshot ?? throw new ArgumentNullException(nameof(loadSnapshot));
        this.roster = roster ?? throw new ArgumentNullException(nameof(roster));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.saveSettings = saveSettings ?? throw new ArgumentNullException(nameof(saveSettings));
        this.data = data;
        this.textures = textures;
    }

    /// <summary>
    /// Obtained/total/unknown per reward kind for the viewed character, normally <c>MoonlitPane.CountsFor</c>, so the
    /// summary agrees with the Moonlit pane. Null hides the Moonlit section.
    /// </summary>
    public Func<RewardKind, UniqueRewardCounts>? MoonlitCounts { get; set; }

    /// <summary>The role, society, Grand Company and achievement icons (UI-5d), once read off the frame; set by the plugin. Null or unread: the rows keep blank slots.</summary>
    public Func<IPaneIconSheets?>? IconSheetsSource { get; set; }

    /// <summary>Moonlit's reward and kind icons, for the collection rows; set by the plugin. Null: names only.</summary>
    public MoonlitIconResolver? MoonlitIcons { get; set; }

    private IPaneIconSheets IconSheets => IconSheetsSource?.Invoke() ?? PaneIconSheets.Empty;

    /// <summary>
    /// The merged unique-reward catalog (normally <c>MoonlitPane.Catalog</c>), which gives the comparison its unlock
    /// values. Null falls back to the shipped data and curated files without the user's overrides.
    /// </summary>
    public Func<UniqueRewardCatalog>? UniqueRewards { get; set; }

    /// <summary>
    /// The query runner that owns the live pinned set: the dashboard's Pinned section reads its pins in order and
    /// rebuilds when its pins version moves, instead of re-reading <c>user/pins.json</c>. Null hides the section.
    /// </summary>
    public QueryRunner? Pins { get; set; }

    /// <summary>The shared Questionable hand-offs (1.6.0): "Send to Questionable" on the ladder and chain rows; null hides it.</summary>
    public QuestionableActions? Questionable { get; set; }

    /// <summary>
    /// Left column (1.8.0, R7 B, E, H): every character in one stable order (live here, live in another client, then by
    /// name and world), grouped by data center when they span more than one, with a search box once the list is long;
    /// selecting one views it, right-click hides it or stops tracking it. Hidden characters show only with "Show hidden".
    /// </summary>
    public void DrawLeft(UiState ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        using var id = ImRaii.PushId("charactersLeft");
        RefreshList();

        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        if (listTotal == 0)
        {
            ImGui.TextWrapped(Strings.CharactersNoneStored);
            ui.RecordSpan(UiRects.CharactersList, start, width);
            return;
        }

        if (listTotal > ListSearchFrom || listSearch.Length > 0)
        {
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.InputTextWithHint("##characterSearch", Strings.AltsSearchHint, ref listSearch, 64))
            {
                listKey = default;
                RefreshList();
            }

            ImGui.Spacing();
        }

        if (listGroups.Count == 0)
        {
            // Nothing listed with no search means every stored character is hidden; "Show hidden" follows below.
            ImGui.TextDisabled(string.IsNullOrWhiteSpace(listSearch) ? Strings.AltsAllHidden : Strings.AltsNoMatch);
        }

        var row = 0;
        foreach (var group in listGroups)
        {
            if (group.Heading.Length > 0)
            {
                ImGui.Spacing();
                Chrome.FitText(group.Heading, ImGui.GetColorU32(ImGuiCol.TextDisabled));
            }

            foreach (var item in group.Items)
            {
                DrawListItem(ui, item, row++);
            }
        }

        if (listHiddenCount > 0)
        {
            ImGui.Spacing();
            var show = settings.ShowHiddenCharacters;
            if (ImGui.Checkbox(listShowHiddenLabel, ref show))
            {
                settings.ShowHiddenCharacters = show;
                saveSettings();
                listKey = default;
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.AltsHideTooltip);
            }
        }

        ui.RecordSpan(UiRects.CharactersList, start, width);
    }

    /// <summary>One row of the list: the name (dimmed when hidden), its detail line, its tooltip and its menu.</summary>
    private void DrawListItem(UiState ui, CharacterItem item, int index)
    {
        using var itemId = ImRaii.PushId(index);
        var selected = session.ViewedContentId == item.ContentId;
        bool clicked;
        bool cut;
        using (Theme.PushText(Theme.Surface.TextDisabled, item.Hidden))
        {
            clicked = Chrome.EllipsisSelectable(item.Label, selected, 0f, out cut);
        }

        if (clicked)
        {
            View(ui, item.ContentId);
        }

        if (ImGui.IsItemHovered())
        {
            if (item.NotUpdating is { } problem)
            {
                UiMetrics.Tooltip(cut ? item.Label : Strings.AltsNotUpdatingBadge, NotUpdatingTooltip(problem));
            }
            else if (item.Elsewhere)
            {
                UiMetrics.Tooltip(cut ? item.Label : Strings.MultiboxLiveElsewhere, Strings.MultiboxLiveElsewhereTooltip);
            }
            else if (cut)
            {
                UiMetrics.Tooltip(item.Label);
            }
        }

        if (ImGui.BeginPopupContextItem("##characterMenu"))
        {
            // The menu opens from the left column (own font scale 1), so it scales itself.
            UiMetrics.ApplyFontScale();
            DrawCharacterMenu(ui, item);
            ImGui.EndPopup();
        }

        using (ImRaii.PushIndent())
        {
            Chrome.FitText(item.Detail, ImGui.GetColorU32(ImGuiCol.TextDisabled));
        }
    }

    /// <summary>View, hide or show, track or don't track one character (1.8.0, R7 E).</summary>
    private void DrawCharacterMenu(UiState ui, CharacterItem item)
    {
        if (ImGui.MenuItem(Strings.AltsMenuView, string.Empty, false, session.ViewedContentId != item.ContentId))
        {
            View(ui, item.ContentId);
        }

        var book = roster.Settings;
        if (ImGui.MenuItem(item.Hidden ? Strings.AltsMenuShow : Strings.AltsMenuHide))
        {
            SetHidden(book, item.ContentId, item.Name, !item.Hidden);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.AltsHideTooltip);
        }

        // Don't track is armed (feature plan v6 S2): a whole session's progress could silently go unsaved. Tracking
        // again is one plain click.
        if (!item.Tracked)
        {
            if (ImGui.MenuItem(Strings.AltsMenuTrack))
            {
                SetTracked(book, item.ContentId, item.Name, tracked: true);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.AltsDontTrackTooltip);
            }
        }
        else if (Chrome.ArmedMenuItem(Strings.AltsMenuDontTrack, trackGuard, GuardedAction.DontTrackCharacter, Strings.AltsDontTrackTooltip, item.ContentId))
        {
            SetTracked(book, item.ContentId, item.Name, tracked: false);
        }
    }

    /// <summary>Hides or shows a character in the lists, with the floating Undo.</summary>
    private static void SetHidden(CharacterSettingsBook book, ulong contentId, string name, bool hidden)
    {
        book.Edit(CharacterSettingChange.Hide(contentId, hidden));
        UndoToast.Show(
            string.Format(CultureInfo.CurrentCulture, hidden ? Strings.UndoToastHiddenFormat : Strings.UndoToastShownFormat, name),
            () => book.Edit(CharacterSettingChange.Hide(contentId, !hidden)));
    }

    /// <summary>Tracks or stops tracking a character, with the floating Undo.</summary>
    private static void SetTracked(CharacterSettingsBook book, ulong contentId, string name, bool tracked)
    {
        book.Edit(CharacterSettingChange.Track(contentId, tracked));
        UndoToast.Show(
            string.Format(CultureInfo.CurrentCulture, tracked ? Strings.UndoToastTrackedFormat : Strings.UndoToastNotTrackedFormat, name),
            () => book.Edit(CharacterSettingChange.Track(contentId, !tracked)));
    }

    private void View(UiState ui, ulong contentId)
    {
        if (session.ViewCharacter(contentId))
        {
            ui.MarkQueryDirty();
        }
        else
        {
            log.Warning("Character {ContentId} could not be viewed; its snapshot is unreadable", contentId);
        }
    }

    /// <summary>The tooltip of a character whose file another client saved and this one cannot read (1.8.0, R7 G).</summary>
    private static string NotUpdatingTooltip(SharedLoad problem) =>
        problem == SharedLoad.Newer ? Strings.AltsNotUpdatingNewerTooltip : Strings.AltsNotUpdatingInvalidTooltip;

    /// <summary>Center column: the viewed character's dashboard, its actions and the account view for the selected quest.</summary>
    public void DrawMain(UiState ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        using var id = ImRaii.PushId("charactersMain");

        // The dashboard fills the centre column; the column is its own child window.
        ui.RecordWindow(UiRects.CharactersDashboard);
        if (DrawViewSwitch())
        {
            DrawCollection(ui);
            DrawToast();
            return;
        }

        var snapshot = session.ViewedSnapshot;
        if (snapshot is null)
        {
            ImGui.TextWrapped(Strings.CharactersNoneViewed);
            // Forgetting the viewed character while logged out lands here: its toast still shows.
            DrawToast();
            return;
        }

        var d = RefreshDashboard(snapshot);

        // (a) Header. The lines wrap between words and the world and the button move to the next line rather than
        // run past the edge (feature plan v4 L6). At Full and Quiet flair the name and world sit in a framed block.
        if (Theme.Sectioned)
        {
            DrawFramedHeader(d);
        }
        else
        {
            Chrome.FitText(d.Name, ImGui.GetColorU32(ImGuiCol.Text));
            Chrome.SameLineOrWrap(ImGui.CalcTextSize(d.World).X);
            Chrome.FitText(d.World, ImGui.GetColorU32(ImGuiCol.TextDisabled));
        }

        TextFlow.Wrapped(d.TakenLine, 0f, ImGui.GetColorU32(ImGuiCol.TextDisabled));
        if (!session.IsLive && session.IsLiveElsewhere(snapshot.ContentId) && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.MultiboxLiveElsewhereTooltip);
        }

        DrawWelcomeBackButton(snapshot);
        DrawStatusNotices(snapshot.ContentId);
        TextFlow.Wrapped(d.CountsLine);
        DrawMsqLine(ui, d);

        DrawPayoffGates(ui);
        Gap();
        DrawSections(d);
        Gap();
        DrawJobQuests(ui, d);
        Gap();
        DrawChains(ui, d);
        DrawAchievementLadders(ui);
        Gap();
        DrawCompare(ui, d);
        Gap();
        DrawMoonlitSummary(d);
        Gap();
        DrawPinned(ui, d);
        Gap();
        DrawJournalRoom(ui);
        Gap();
        DrawAbandoned(ui);
        Gap();
        DrawSeasonal(ui);
        Gap();
        DrawRecent(d);
        Gap();
        DrawJobs(ui, d);
        DrawPlanning(ui, d);
        DrawDutyBoard(ui);
        ImGui.Spacing();
        DrawGrandCompanyAndTribes(d);
        Gap(ruled: true);
        DrawActions(snapshot);
        DrawForgetPopup(ui);
        DrawToast();
        Gap();
        DrawAccountView(ui);
    }

    /// <summary>
    /// The main scenario line under the header: "MSQ: Dawntrail · next: …", or every route inside a branch region
    /// ("MSQ: Evercold · route A 3 of 9 · route B not started"). Independent of the journal's hide state; a click
    /// selects the next quest (the first route's).
    /// </summary>
    private static void DrawMsqLine(UiState ui, Dashboard d)
    {
        if (d.MsqLine.Length == 0)
        {
            return;
        }

        TextFlow.Wrapped(d.MsqLine);
        if (d.MsqQuest is { } msqQuest && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.MsqClickHint);
            if (ImGui.IsItemClicked())
            {
                ui.Reveal(msqQuest);
            }
        }
    }

    /// <summary>
    /// Between two blocks: a separator, or at Full and Quiet flair only air (the next heading's rule is the line). With
    /// <paramref name="ruled"/> the separator stays at every level, before a block with no heading of its own.
    /// </summary>
    private static void Gap(bool ruled = false)
    {
        ImGui.Spacing();
        if (Theme.Sectioned && !ruled)
        {
            ImGui.Spacing();
        }
        else
        {
            ImGui.Separator();
        }

        ImGui.Spacing();
    }

    /// <summary>
    /// The framed header (proposal §7.6, Flair Full and Quiet): the current job's icon at 40 px on the sunken ground in
    /// a frame (corner marks at Full: the one framed object of this pane), the name in the Title role and the world
    /// under it, the overall orbit gauge at 48 px on the right, or on its own line under the name in a pane under
    /// 360 px. One block item; the name's item carries its tooltip when cut.
    /// </summary>
    private void DrawFramedHeader(Dashboard d)
    {
        var start = ImGui.GetCursorScreenPos();
        var room = ImGui.GetContentRegionAvail().X;
        var dl = ImGui.GetWindowDrawList();
        var s = Theme.Surface;
        var highContrast = Theme.Glyphs.HighContrast;
        var frame = MathF.Round(UiMetrics.Icon(40f));
        var gauge = MathF.Round(UiMetrics.Icon(48f));
        var gap = UiMetrics.Px(10f);
        var mark = MathF.Round(UiMetrics.Px(8f));
        var outset = MathF.Round(UiMetrics.Px(3f));
        var wide = room / UiMetrics.Scale >= 360f && d.Sections.Length > 0;

        // The job icon in its frame.
        var frameMin = start + new Vector2(outset);
        var frameMax = frameMin + new Vector2(frame);
        var rounding = UiMetrics.Px(4f);
        dl.AddRectFilled(frameMin, frameMax, Theme.U32(s.Sunken), rounding);
        if (textures is not null && d.JobIconId != 0)
        {
            var inset = new Vector2(MathF.Round(UiMetrics.Px(3f)));
            if (GameIcon.TryGetWrap(textures, d.JobIconId, frame, out var wrap))
            {
                dl.AddImage(wrap.Handle, frameMin + inset, frameMax - inset);
            }
        }

        dl.AddRect(frameMin, frameMax, highContrast ? Theme.U32(Theme.Surface.StrongLine) : Theme.U32(s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        if (Theme.ShowCornerMarks)
        {
            OrnamentAtlas.Corners(dl, frameMin - new Vector2(outset), frameMax + new Vector2(outset), mark, 0f, Theme.OrnamentU32);
        }

        // Name (Title role) and world.
        var textX = frameMax.X + outset + gap;
        var textRoom = MathF.Max(0f, start.X + room - textX - (wide ? gauge + gap : 0f));
        float titleLine;
        using (Typography.Title(d.Name))
        {
            titleLine = ImGui.GetTextLineHeight();
        }

        var line = ImGui.GetTextLineHeight();
        var block = MathF.Max(frame + (2f * outset), titleLine + line);
        var textY = start.Y + MathF.Max(0f, (block - titleLine - line) * 0.5f);
        ImGui.SetCursorScreenPos(new Vector2(textX, textY));
        if (SectionHeading.Title(d.Name, textRoom) && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(d.Name);
        }

        ImGui.SetCursorScreenPos(new Vector2(textX, textY + titleLine));
        if (Chrome.EllipsisText(d.World, textRoom, Theme.U32(s.TextSecondary)) && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(d.World);
        }

        // The overall gauge: right of the block, or under it in a narrow pane.
        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(room, block));
        if (d.Sections.Length == 0)
        {
            return;
        }

        Vector2 gaugeMin;
        if (wide)
        {
            gaugeMin = new Vector2(start.X + room - gauge, start.Y + MathF.Max(0f, (block - gauge) * 0.5f));
        }
        else
        {
            gaugeMin = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(gauge, gauge));
        }

        var overall = d.Sections[0];
        var fraction = Motion.Fill(Motion.Key(DashboardGaugeTag, HeaderGauge), overall.Fraction);
        if (textures is not null)
        {
            Orbit.Draw(dl, textures, gaugeMin, gauge, overall.Icon, fraction, highContrast: highContrast);
        }
        else
        {
            MoonGlyph.DrawHalo(dl, gaugeMin + new Vector2(gauge * 0.5f), gauge * 0.5f, fraction);
        }

        if (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(gaugeMin, gaugeMin + new Vector2(gauge)))
        {
            FillingMoonTooltip(overall.Count, overall.Percent);
        }
    }

    /// <summary>(b) One row per journal section: filling moon, done/total and percent; All quests on top.</summary>
    private void DrawSections(Dashboard d)
    {
        SectionHeading.Draw(Strings.CharactersSectionCompletion);
        if (d.Sections.Length == 0)
        {
            ImGui.TextDisabled(Strings.CharactersNoSections);
            return;
        }

        if (Theme.Sectioned)
        {
            DrawSectionGrid(d);
            ImGui.Spacing();
        }

        using var table = ImRaii.Table("##sections", 4, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        var line = ImGui.GetTextLineHeight();
        if (SectionsDoneWidth.Stale(d.Sections))
        {
            var done = FixedWidth.Fit(0f, Strings.CharactersColumnDone);
            var percent = FixedWidth.Fit(0f, Strings.CharactersColumnPercent);
            foreach (var row in d.Sections)
            {
                done = FixedWidth.Fit(done, row.Count);
                percent = FixedWidth.Fit(percent, row.Percent);
            }

            SectionsDoneWidth.Store(d.Sections, done);
            SectionsPercentWidth.Store(d.Sections, percent);
        }

        // Names stretch and end in an ellipsis; the numbers keep the width of their widest (feature plan v4 L6).
        ImGui.TableSetupColumn("##moon", ImGuiTableColumnFlags.WidthFixed, GlyphColumn(line));
        ImGui.TableSetupColumn(Strings.CharactersColumnSection, ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn(Strings.CharactersColumnDone, ImGuiTableColumnFlags.WidthFixed, SectionsDoneWidth.Value);
        ImGui.TableSetupColumn(Strings.CharactersColumnPercent, ImGuiTableColumnFlags.WidthFixed, SectionsPercentWidth.Value);
        ImGui.TableHeadersRow();

        for (var i = 0; i < d.Sections.Length; i++)
        {
            var row = d.Sections[i];
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            MoonGlyph.DrawHaloInline(Motion.Key(DashboardGaugeTag, SectionsGauges | (uint)i), row.Fraction, UiMetrics.InlineGlyphSize(line));
            if (ImGui.IsItemHovered())
            {
                FillingMoonTooltip(row.Count, row.Percent);
            }

            ImGui.TableNextColumn();
            // The overall row is a heading, not a call to action: Silver, not gold (game UX panel finding 2).
            Chrome.FitText(row.Name, row.Overall ? Theme.U32(Theme.Surface.Text) : ImGui.GetColorU32(ImGuiCol.Text));

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Count);
            ImGui.TableNextColumn();
            ImGui.TextDisabled(row.Percent);
        }
    }

    /// <summary>
    /// The section rings (proposal §7.6, Flair Full and Quiet): one orbit per journal section, All quests first, with the
    /// section's official icon inside, its count in the Numeral role and its name in the caption role, in ⌊width / 120⌋
    /// columns. The fill eases when it changes, as the table's moons do. Drawn on the draw list over one block item;
    /// hovering a ring names its section with the count and percent.
    /// </summary>
    private void DrawSectionGrid(Dashboard d)
    {
        var origin = ImGui.GetCursorScreenPos();
        var room = ImGui.GetContentRegionAvail().X;
        var columns = PaneGrid.SectionColumns(room / UiMetrics.Scale);
        var cell = MathF.Floor(room / columns);
        var pad = UiMetrics.Px(4f);
        var orbit = MathF.Round(MathF.Max(UiMetrics.Px(20f), MathF.Min(UiMetrics.Icon(PaneGrid.SectionOrbitLogical), cell - (2f * pad))));
        float numeralLine;
        using (Typography.Numeral(default))
        {
            numeralLine = ImGui.GetTextLineHeight();
        }

        float captionLine;
        using (Typography.Caption())
        {
            captionLine = ImGui.GetTextLineHeight();
        }

        var height = MathF.Ceiling(orbit + pad + numeralLine + captionLine + (2f * pad));
        var capped = PaneGrid.Capped(d.Sections.Length, columns, SectionRingRows);
        var count = sectionRingsAll ? d.Sections.Length : capped;
        var rows = PaneGrid.Rows(count, columns);
        ImGui.Dummy(new Vector2(room, rows * height));
        var visible = ImGui.IsItemVisible();
        var hoverable = ImGui.IsItemHovered();
        if (capped < d.Sections.Length)
        {
            // Two rows keep the dashboard short in a narrow pane; the table under the rings lists every section.
            if (ImGui.SmallButton(sectionRingsAll ? Strings.CharactersSectionRingsFewer : Strings.CharactersSectionRingsAll))
            {
                sectionRingsAll = !sectionRingsAll;
            }
        }

        if (!visible)
        {
            return;
        }

        var dl = ImGui.GetWindowDrawList();
        var s = Theme.Surface;
        var highContrast = Theme.Glyphs.HighContrast;
        for (var i = 0; i < count; i++)
        {
            var row = d.Sections[i];
            var min = origin + new Vector2((i % columns) * cell, (i / columns) * height);
            var centerX = min.X + (cell * 0.5f);
            var orbitMin = new Vector2(MathF.Floor(centerX - (orbit * 0.5f)), min.Y + pad);
            var fraction = Motion.Fill(Motion.Key(DashboardGaugeTag, SectionGridGauges | (uint)i), row.Fraction);
            if (textures is not null)
            {
                Orbit.Draw(dl, textures, orbitMin, orbit, row.Icon, fraction, highContrast: highContrast);
            }
            else
            {
                MoonGlyph.DrawHalo(dl, orbitMin + new Vector2(orbit * 0.5f), orbit * 0.5f, fraction);
            }

            var y = orbitMin.Y + orbit + pad;
            using (Typography.Numeral(row.Count))
            {
                var width = ImGui.CalcTextSize(row.Count).X;
                dl.AddText(new Vector2(MathF.Floor(centerX - (width * 0.5f)), y), Theme.U32(row.Overall ? s.Text : s.TextSecondary), row.Count);
            }

            y += numeralLine;
            using (Typography.Caption())
            {
                var textRoom = MathF.Max(1f, cell - (2f * pad));
                var width = ImGui.CalcTextSize(row.Name).X;
                var x = MathF.Floor(centerX - (MathF.Min(width, textRoom) * 0.5f));
                Chrome.EllipsisTextAt(dl, new Vector2(x, y), textRoom, row.Name, Theme.U32(row.Overall ? s.Text : s.TextSecondary), width);
            }

            if (hoverable && ImGui.IsMouseHoveringRect(min, min + new Vector2(cell, height)))
            {
                SectionTooltip(row);
            }
        }
    }

    /// <summary>A section ring's tooltip: the section's name, then the filling moon's count and percent.</summary>
    private static void SectionTooltip(SectionRow row)
    {
        using var tooltip = Theme.Tooltip();
        UiMetrics.ApplyFontScale();
        using var wrap = UiMetrics.TooltipWrap();
        ImGui.TextUnformatted(row.Name);
        ImGui.TextDisabled(row.Count);
        ImGui.SameLine();
        ImGui.TextDisabled(Strings.StateReasonSeparator);
        ImGui.SameLine();
        ImGui.TextDisabled(row.Percent);
    }

    /// <summary>
    /// Job quests (V2-11): one row per leveled job with its icon, level, a filling moon over its ladder and the next
    /// quest ("Lv N" in Moon when it can be taken now, its decisive blocker in Dusk otherwise), then one row per role the
    /// character has a job in. The next quest's name reveals it in the Journal.
    /// </summary>
    private void DrawJobQuests(UiState ui, Dashboard d)
    {
        var routeButton = TravelControls.RowButtonWidth(ActionGlyphs.Route, Strings.RouteToUnlockMenu);
        var reserve = routeButton + ImGui.GetStyle().ItemSpacing.X;
        if (Theme.Sectioned && ImGui.GetContentRegionAvail().X - reserve >= UiMetrics.Px(HeadingLeastLogical))
        {
            // The open-section heading leaves room for the button at the end of its line.
            SectionHeading.Draw(Strings.JobsSection, reserve: reserve);
            ImGui.SameLine();
        }
        else if (Theme.Sectioned)
        {
            // Too narrow to share the line (the heading would be a bare "…"): the heading whole, the button under it.
            SectionHeading.Draw(Strings.JobsSection);
            Chrome.SameLineOrWrap(routeButton);
        }
        else
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled(Strings.JobsSection);
            Chrome.SameLineOrWrap(routeButton);
        }

        DrawRouteToUnlockButton(ui, d);
        if (d.JobQuests.Length == 0)
        {
            ImGui.TextDisabled(Strings.JobsNone);
            return;
        }

        using var table = ImRaii.Table("##jobQuests", 6, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        var line = ImGui.GetTextLineHeight();
        var iconSize = UiMetrics.Square(UiMetrics.JobIconSize);
        if (JobQuestsLevelWidth.Stale(d.JobQuests))
        {
            var level = FixedWidth.Fit(0f, Strings.JobsColumnLevel);
            var done = FixedWidth.Fit(0f, Strings.JobsColumnLeft);
            foreach (var row in d.JobQuests)
            {
                level = FixedWidth.Fit(level, row.Level);
                done = FixedWidth.Fit(done, row.Left);
            }

            JobQuestsLevelWidth.Store(d.JobQuests, level);
            JobQuestsDoneWidth.Store(d.JobQuests, done);
        }

        ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, MathF.Max(line * 1.4f, iconSize.X));
        ImGui.TableSetupColumn(Strings.JobsColumnJob, ImGuiTableColumnFlags.WidthStretch, 2f);
        ImGui.TableSetupColumn(Strings.JobsColumnLevel, ImGuiTableColumnFlags.WidthFixed, JobQuestsLevelWidth.Value);
        ImGui.TableSetupColumn("##moon", ImGuiTableColumnFlags.WidthFixed, GlyphColumn(line));
        ImGui.TableSetupColumn(Strings.JobsColumnLeft, ImGuiTableColumnFlags.WidthFixed, JobQuestsDoneWidth.Value);
        ImGui.TableSetupColumn(Strings.JobsColumnNext, ImGuiTableColumnFlags.WidthStretch, 3f);
        ImGui.TableHeadersRow();

        for (var i = 0; i < d.JobQuests.Length; i++)
        {
            var row = d.JobQuests[i];
            using var rowId = ImRaii.PushId(i);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            DrawJobIcon(row.IconId, iconSize, row.Name, row.Level);
            ImGui.TableNextColumn();
            Chrome.FitText(row.Name, row.IsRole ? Theme.U32(Theme.Surface.TextTertiary) : ImGui.GetColorU32(ImGuiCol.Text));
            DrawRowMenu(ui, row.RowIds);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Level);
            ImGui.TableNextColumn();
            MoonGlyph.DrawHaloInline(Motion.Key(DashboardGaugeTag, JobGauges | (uint)i), row.Fraction, UiMetrics.InlineGlyphSize(line));
            if (ImGui.IsItemHovered())
            {
                FillingMoonTooltip(row.Count);
            }

            ImGui.TableNextColumn();
            LeftCell(row.Left, row.Count);
            ImGui.TableNextColumn();
            DrawNextQuest(ui, row.Next, row.NextText, row.Ready);
        }
    }

    /// <summary>
    /// A ladder's or chain's "Left" cell (feature plan v6 U5): how many quests are still to do, empty once none is; the
    /// tally ("4/7") on hover, as on the moon beside it.
    /// </summary>
    private static void LeftCell(string left, string tally)
    {
        ImGui.TextUnformatted(left);
        if (left.Length > 0 && ImGui.IsItemHovered())
        {
            FillingMoonTooltip(tally);
        }
    }

    /// <summary>
    /// Story chains (V2-09 on the dashboard): every curated chain with a filling moon, how many are left and the next quest;
    /// chains with nothing done yet fold under "Not started (N)" so the list stays short.
    /// </summary>
    private void DrawChains(UiState ui, Dashboard d)
    {
        SectionHeading.Draw(Strings.JobsChainsSection);
        if (d.Chains.Length == 0 && d.ChainsNotStarted.Length == 0)
        {
            ImGui.TextDisabled(Strings.JobsChainsNone);
            return;
        }

        DrawChainTable(ui, "##chains", d.Chains, ChainGauges, ChainsDoneWidth);
        if (d.ChainsNotStarted.Length == 0)
        {
            return;
        }

        using var node = ImRaii.TreeNode(d.ChainsNotStartedLabel);
        if (node)
        {
            DrawChainTable(ui, "##chainsNotStarted", d.ChainsNotStarted, NotStartedGauges, NotStartedDoneWidth);
        }
    }

    private void DrawChainTable(UiState ui, string id, ChainRow[] rows, uint gaugeKeys, FixedWidth doneWidth)
    {
        if (rows.Length == 0)
        {
            return;
        }

        using var table = ImRaii.Table(id, 4, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        if (doneWidth.Stale(rows))
        {
            var done = FixedWidth.Fit(0f, Strings.JobsColumnLeft);
            foreach (var row in rows)
            {
                done = FixedWidth.Fit(done, row.Left);
            }

            doneWidth.Store(rows, done);
        }

        var line = ImGui.GetTextLineHeight();
        ImGui.TableSetupColumn("##moon", ImGuiTableColumnFlags.WidthFixed, GlyphColumn(line));
        ImGui.TableSetupColumn(Strings.JobsColumnChain, ImGuiTableColumnFlags.WidthStretch, 2f);
        ImGui.TableSetupColumn(Strings.JobsColumnLeft, ImGuiTableColumnFlags.WidthFixed, doneWidth.Value);
        ImGui.TableSetupColumn(Strings.JobsColumnNext, ImGuiTableColumnFlags.WidthStretch, 3f);

        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            using var rowId = ImRaii.PushId(i);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            MoonGlyph.DrawHaloInline(Motion.Key(DashboardGaugeTag, gaugeKeys | (uint)i), row.Fraction, UiMetrics.InlineGlyphSize(line));
            if (ImGui.IsItemHovered())
            {
                FillingMoonTooltip(row.Count);
            }

            ImGui.TableNextColumn();
            Chrome.FitText(row.Name, ImGui.GetColorU32(ImGuiCol.Text));
            DrawRowMenu(ui, row.RowIds, row.RecapQuest);
            ImGui.TableNextColumn();
            LeftCell(row.Left, row.Count);
            ImGui.TableNextColumn();
            DrawNextQuest(ui, row.Next, row.NextText, ready: true);
        }
    }

    /// <summary>
    /// The right-click menu on a ladder's or a chain's name (feature plan v5, 1.6.0): "Send to Questionable" with the
    /// row's quests in their order (done ones left out when sent), and for a story chain the character has started,
    /// "Read the story so far" (the story recap, 1.9.0).
    /// </summary>
    /// <param name="recapQuest">A quest naming the chain for the recap (<see cref="RecapRequest.ChainQuestRowId"/>); 0 offers none.</param>
    private void DrawRowMenu(UiState ui, IReadOnlyList<uint> rowIds, uint recapQuest = 0)
    {
        var questionable = rowIds.Count > 0 ? AutomationGate.Questionable(Questionable) : null;
        if (questionable is null && recapQuest == 0)
        {
            return;
        }

        using var menu = ImRaii.ContextPopupItem("##questionableRow");
        if (!menu)
        {
            return;
        }

        UiMetrics.ApplyFontScale();
        questionable?.DrawSubmenu(MainWindow.QuestionableHost, Strings.QuestionableSendButton, rowIds, static rows => rows);
        if (recapQuest != 0)
        {
            if (ImGui.MenuItem(Strings.RecapReadChain))
            {
                ui.OpenRecap(new RecapRequest(recapQuest));
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.RecapReadChainTooltip);
            }
        }
    }

    /// <summary>A clickable "next" cell: the text in Moon when the quest is open now, Dusk otherwise; null quest means finished.</summary>
    private static void DrawNextQuest(UiState ui, QuestRecord? next, string text, bool ready)
    {
        if (next is null)
        {
            Chrome.FitText(text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
            return;
        }

        bool cut;
        using (Theme.PushText(ready ? Theme.Accent : Theme.Surface.TextTertiary))
        {
            if (Chrome.EllipsisSelectable(text, false, 0f, out cut))
            {
                Reveal(ui, next);
            }
        }

        ShowInJournalTooltip(text, cut);
    }

    /// <summary>(c) Obtained/total for the collectible reward kinds, with the Moonlit pane's own counts.</summary>
    private void DrawMoonlitSummary(Dashboard d)
    {
        SectionHeading.Draw(Strings.CharactersMoonlitSummary);
        if (d.Moonlit.Length == 0)
        {
            ImGui.TextDisabled(Strings.CharactersMoonlitUnavailable);
            return;
        }

        if (!session.IsLive)
        {
            // The counts come from the character's last capture (decision 9); a file from before 1.5 saved none.
            using (Theme.PushText(Theme.Surface.TextTertiary))
            {
                ImGui.TextUnformatted(Strings.MoonlitOwnedNote(d.Snapshot.Collectibles.Count > 0 ? d.Snapshot.TakenUtc : null));
            }
        }

        using var table = ImRaii.Table("##moonlitSummary", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        if (MoonlitObtainedWidth.Stale(d.Moonlit))
        {
            var obtained = FixedWidth.Fit(0f, Strings.CharactersColumnObtained);
            foreach (var row in d.Moonlit)
            {
                obtained = FixedWidth.Fit(obtained, row.Count);
            }

            MoonlitObtainedWidth.Store(d.Moonlit, obtained);
        }

        var line = ImGui.GetTextLineHeight();
        ImGui.TableSetupColumn("##moon", ImGuiTableColumnFlags.WidthFixed, GlyphColumn(line));
        ImGui.TableSetupColumn(Strings.CharactersColumnKind, ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn(Strings.CharactersColumnObtained, ImGuiTableColumnFlags.WidthFixed, MoonlitObtainedWidth.Value);

        for (var i = 0; i < d.Moonlit.Length; i++)
        {
            var row = d.Moonlit[i];
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            if (row.AllUnknown)
            {
                Marks.DrawInline(Mark.Unknown, UiMetrics.InlineGlyphSize(line));
                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(Strings.MoonlitObtainedUnknown);
                }
            }
            else
            {
                MoonGlyph.DrawHaloInline(Motion.Key(DashboardGaugeTag, MoonlitGauges | (uint)i), row.Fraction, UiMetrics.InlineGlyphSize(line));
                if (ImGui.IsItemHovered())
                {
                    FillingMoonTooltip(row.Count);
                }
            }

            ImGui.TableNextColumn();
            if (MoonlitIcons is { } icons)
            {
                // The kind's own menu icon (Mount Guide, Emotes, the Duty Finder), as Moonlit's kinds list wears (UI-5d).
                DrawLeadIcon(icons.KindIcon(row.Kind), MathF.Round(UiMetrics.JobIconSize));
            }

            Chrome.FitText(row.Name, ImGui.GetColorU32(ImGuiCol.Text));
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Count);
        }
    }

    /// <summary>(d) The viewed character's pins with glyph, name and next step; clicking reveals the quest in the Journal.</summary>
    private static void DrawPinned(UiState ui, Dashboard d)
    {
        SectionHeading.Draw(Strings.CharactersPinned);
        if (d.Pinned.Length == 0)
        {
            ImGui.TextDisabled(Strings.CharactersNoPins);
            return;
        }

        using var table = ImRaii.Table("##pinned", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        var line = ImGui.GetTextLineHeight();
        ImGui.TableSetupColumn("##state", ImGuiTableColumnFlags.WidthFixed, GlyphColumn(line));
        ImGui.TableSetupColumn(Strings.CharactersColumnQuest, ImGuiTableColumnFlags.WidthStretch, 3f);
        ImGui.TableSetupColumn(Strings.CharactersColumnStatus, ImGuiTableColumnFlags.WidthStretch, 2f);

        for (var i = 0; i < d.Pinned.Length; i++)
        {
            var row = d.Pinned[i];
            using var rowId = ImRaii.PushId(i);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            MoonGlyph.DrawInline(row.State, UiMetrics.InlineGlyphSize(line));
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.StateTooltip(row.State, row.Quest));
            }

            ImGui.TableNextColumn();
            if (row.Quest is { } quest)
            {
                if (Chrome.EllipsisSelectable(row.Name, false, 0f, out var cut))
                {
                    Reveal(ui, quest);
                }

                ShowInJournalTooltip(row.Name, cut);
            }
            else
            {
                Chrome.FitText(row.Name, ImGui.GetColorU32(ImGuiCol.TextDisabled));
            }

            ImGui.TableNextColumn();
            StatusCell(row.NextStep);
        }
    }

    /// <summary>A status cell: the state word never cut, the reason ellipsised with the whole line on hover.</summary>
    private static void StatusCell(string status)
    {
        if (status.Length == 0)
        {
            return;
        }

        var s = Theme.Surface;
        Chrome.StatusText(status, ImGui.GetContentRegionAvail().X, s.Text, s.TextSecondary);
    }

    /// <summary>The hover text of a quest name that reveals it in the Journal: the whole name first when it was cut.</summary>
    private static void ShowInJournalTooltip(string name, bool cut)
    {
        if (!ImGui.IsItemHovered())
        {
            return;
        }

        if (cut)
        {
            UiMetrics.Tooltip(name, Strings.MoonlitShowInJournal);
        }
        else
        {
            UiMetrics.Tooltip(Strings.MoonlitShowInJournal);
        }
    }

    /// <summary>(e) The last few quest events of the live character: kind, quest and local time.</summary>
    private void DrawRecent(Dashboard d)
    {
        SectionHeading.Draw(Strings.CharactersRecent);
        if (!session.IsLive)
        {
            ImGui.TextDisabled(Strings.CharactersRecentNeedsLive);
            return;
        }

        if (d.Recent.Length == 0)
        {
            ImGui.TextDisabled(Strings.CharactersNoRecent);
            return;
        }

        using var table = ImRaii.Table("##recent", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        if (RecentTimeWidth.Stale(d.Recent))
        {
            var time = FixedWidth.Fit(0f, Strings.CharactersColumnTime);
            var kind = FixedWidth.Fit(0f, Strings.CharactersColumnEvent);
            foreach (var row in d.Recent)
            {
                time = FixedWidth.Fit(time, row.Time);
                kind = FixedWidth.Fit(kind, row.Kind);
            }

            RecentTimeWidth.Store(d.Recent, time);
            RecentEventWidth.Store(d.Recent, kind);
        }

        ImGui.TableSetupColumn(Strings.CharactersColumnTime, ImGuiTableColumnFlags.WidthFixed, RecentTimeWidth.Value);
        ImGui.TableSetupColumn(Strings.CharactersColumnEvent, ImGuiTableColumnFlags.WidthFixed, RecentEventWidth.Value);
        ImGui.TableSetupColumn(Strings.CharactersColumnQuest, ImGuiTableColumnFlags.WidthStretch);

        foreach (var row in d.Recent)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextDisabled(row.Time);
            ImGui.TableNextColumn();
            using (Theme.PushText(row.Color))
            {
                ImGui.TextUnformatted(row.Kind);
            }

            ImGui.TableNextColumn();
            Chrome.FitText(row.Quest, ImGui.GetColorU32(ImGuiCol.Text));
        }
    }

    /// <summary>
    /// (f) Job levels grouped by role, each with the game's job icon; base classes hidden once their job is unlocked.
    /// A right-click on a class offers the unlock route to the jobs it grows into (P6).
    /// </summary>
    private void DrawJobs(UiState ui, Dashboard d)
    {
        SectionHeading.Draw(Strings.CharactersJobs);
        if (d.Jobs.Length == 0)
        {
            ImGui.TextDisabled(Strings.CharactersNoJobs);
            return;
        }

        using var table = ImRaii.Table("##jobs", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        var line = ImGui.GetTextLineHeight();
        var iconSize = UiMetrics.Square(UiMetrics.JobIconSize);
        if (JobsLevelWidth.Stale(d.Jobs))
        {
            var level = FixedWidth.Fit(0f, Strings.CharactersColumnLevel);
            foreach (var row in d.Jobs)
            {
                level = FixedWidth.Fit(level, row.Level);
            }

            JobsLevelWidth.Store(d.Jobs, level);
        }

        ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, MathF.Max(line * 1.4f, iconSize.X));
        ImGui.TableSetupColumn(Strings.CharactersColumnJob, ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn(Strings.CharactersColumnLevel, ImGuiTableColumnFlags.WidthFixed, JobsLevelWidth.Value);
        ImGui.TableHeadersRow();

        var group = JobGroup.Other;
        var first = true;
        foreach (var row in d.Jobs)
        {
            if (first || row.Group != group)
            {
                first = false;
                group = row.Group;
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                // The group's role icon (the Disciples of the Hand's or the Land's tile) in the icon column, so the icons
                // read down one column and the group name stays a quiet heading (UI-5d).
                var groupName = Strings.CharactersJobGroupName(group);
                DrawJobIcon(PaneIcons.Family(FamilyOf(group), IconSheets), iconSize, groupName, string.Empty);

                ImGui.TableNextColumn();
                Chrome.FitText(groupName, Theme.U32(Theme.Surface.TextTertiary));
            }

            ImGui.TableNextRow();
            using var rowId = ImRaii.PushId((int)row.JobId);
            ImGui.TableNextColumn();
            DrawJobIcon(row.IconId, iconSize, row.Name, row.Level);
            ImGui.TableNextColumn();
            if (!Chrome.FitText(row.Name, ImGui.GetColorU32(ImGuiCol.Text)) && ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(row.Abbreviation);
            }

            DrawJobRowRouteMenu(ui, d, row.JobId);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Level);
        }
    }

    /// <summary>
    /// The job's icon (a blank of the same size without one), naming the job and its level on hover; a role, a job group
    /// or an allied society has no level, and a society's <paramref name="note"/> (its rank) goes under its name.
    /// </summary>
    private void DrawJobIcon(uint iconId, Vector2 size, string name, string level, string? note = null)
    {
        if (textures is null || iconId == 0)
        {
            ImGui.Dummy(size);
        }
        else
        {
            GameIcon.Draw(textures, iconId, size.X);
        }

        if (!ImGui.IsItemHovered())
        {
            return;
        }

        if (note is { Length: > 0 })
        {
            UiMetrics.Tooltip(name, note);
        }
        else
        {
            JobTooltip(name, level);
        }
    }

    /// <summary>The icon family of a Jobs table group: its role, or the Disciples of the Hand or the Land.</summary>
    private static JobFamily FamilyOf(JobGroup group) => group switch
    {
        JobGroup.Tank => JobFamily.Tank,
        JobGroup.Healer => JobFamily.Healer,
        JobGroup.Melee => JobFamily.Melee,
        JobGroup.Ranged => JobFamily.PhysicalRanged,
        JobGroup.Caster => JobFamily.MagicalRanged,
        JobGroup.Crafter => JobFamily.Hand,
        JobGroup.Gatherer => JobFamily.Land,
        _ => JobFamily.None,
    };

    /// <summary>Job icon tooltip: the job's name, then "Level N" under it; a role row has no level and shows the name alone.</summary>
    private static void JobTooltip(string name, string level)
    {
        using var tooltip = Theme.Tooltip();
        UiMetrics.ApplyFontScale();
        using var wrap = UiMetrics.TooltipWrap();
        ImGui.TextUnformatted(name);
        if (level.Length > 0)
        {
            ImGui.TextDisabled(Strings.CharactersColumnLevel);
            ImGui.SameLine();
            ImGui.TextDisabled(level);
        }
    }

    /// <summary>Filling moon tooltip: what the moon shows, then the row's done/total (and percent where the row has one).</summary>
    private static void FillingMoonTooltip(string count, string? percent = null)
    {
        using var tooltip = Theme.Tooltip();
        UiMetrics.ApplyFontScale();
        using var wrap = UiMetrics.TooltipWrap();
        ImGui.TextUnformatted(Strings.FillingMoonTooltip);
        ImGui.TextDisabled(count);
        if (percent is { Length: > 0 })
        {
            ImGui.SameLine();
            ImGui.TextDisabled(Strings.StateReasonSeparator);
            ImGui.SameLine();
            ImGui.TextDisabled(percent);
        }
    }

    /// <summary>
    /// The Grand Company line, led by the rank's insignia in that company (UI-5d), then the allied societies, each led by
    /// its emblem; an icon's hover names the company and rank, or the society and its rank.
    /// </summary>
    private void DrawGrandCompanyAndTribes(Dashboard d)
    {
        SectionHeading.Draw(Strings.CharactersGrandCompany);
        var company = d.Snapshot.GrandCompany;
        var rank = company < d.Snapshot.GcRanks.Length ? d.Snapshot.GcRanks[company] : (byte)0;
        var insignia = PaneIcons.GrandCompanyRank(company, rank, IconSheets);
        if (insignia != 0)
        {
            // The insignia is a wide badge in a square icon: half again the job icons' size keeps it legible.
            DrawLeadIcon(NodeIcon.Game(insignia), MathF.Round(UiMetrics.JobIconSize * 1.5f), d.GrandCompanyLine);
        }

        TextFlow.Wrapped(d.GrandCompanyLine, Chrome.RoomX());
        ImGui.Spacing();

        SectionHeading.Draw(Strings.CharactersTribes);
        TextFlow.Wrapped(d.AllowancesLine);
        if (d.Tribes.Length == 0)
        {
            ImGui.TextDisabled(Strings.CharactersNoTribes);
            return;
        }

        using var table = ImRaii.Table("##tribes", 4, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        if (TribesReputationWidth.Stale(d.Tribes))
        {
            var reputation = FixedWidth.Fit(0f, Strings.CharactersColumnReputation);
            foreach (var (_, _, _, value) in d.Tribes)
            {
                reputation = FixedWidth.Fit(reputation, value);
            }

            TribesReputationWidth.Store(d.Tribes, reputation);
        }

        var iconSize = UiMetrics.Square(UiMetrics.JobIconSize);
        ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, MathF.Max(ImGui.GetTextLineHeight() * 1.4f, iconSize.X));
        ImGui.TableSetupColumn(Strings.CharactersColumnTribe, ImGuiTableColumnFlags.WidthStretch, 3f);
        ImGui.TableSetupColumn(Strings.CharactersColumnRank, ImGuiTableColumnFlags.WidthStretch, 2f);
        ImGui.TableSetupColumn(Strings.CharactersColumnReputation, ImGuiTableColumnFlags.WidthFixed, TribesReputationWidth.Value);
        ImGui.TableHeadersRow();
        var sheets = IconSheets;
        foreach (var (id, tribe, rankName, value) in d.Tribes)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            DrawJobIcon(PaneIcons.Tribe(id, sheets), iconSize, tribe, string.Empty, rankName);
            ImGui.TableNextColumn();
            Chrome.FitText(tribe, ImGui.GetColorU32(ImGuiCol.Text));
            ImGui.TableNextColumn();
            Chrome.FitText(rankName, ImGui.GetColorU32(ImGuiCol.Text));
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(value);
        }
    }

    /// <summary>
    /// A game icon <paramref name="size"/> across leading the text drawn after it on the same line, centred on that
    /// text's first line (the line moves down to meet the icon's centre); its hover says <paramref name="tooltip"/>.
    /// Leaves the cursor where the text goes.
    /// </summary>
    private void DrawLeadIcon(NodeIcon icon, float size, string? tooltip = null)
    {
        var line = ImGui.GetTextLineHeight();
        var start = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(size, size));
        if (textures is not null && ImGui.IsItemVisible())
        {
            Orbit.DrawIcon(ImGui.GetWindowDrawList(), textures, icon, start, start + new Vector2(size, size));
        }

        if (tooltip is { Length: > 0 } && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(tooltip);
        }

        ImGui.SameLine(0f, MathF.Round(UiMetrics.Px(LeadIconGapLogical)));
        ImGui.SetCursorScreenPos(new Vector2(ImGui.GetCursorScreenPos().X, start.Y + MathF.Round(MathF.Max(0f, size - line) * 0.5f)));
    }

    /// <summary>(g) Export and Forget.</summary>
    private void DrawActions(CharacterSnapshot snapshot)
    {
        if (ImGui.Button(Strings.CharactersExport))
        {
            Export(snapshot);
        }

        Chrome.SameLineOrWrap(ImGui.CalcTextSize(Strings.CharactersForget).X + (ImGui.GetStyle().FramePadding.X * 2f));
        var live = session.IsLive;
        // Multibox (D11): a character live in another game client belongs to that client, which would save it again.
        var elsewhere = !live && session.IsLiveElsewhere(snapshot.ContentId);
        using (ImRaii.Disabled(live || elsewhere))
        using (Theme.PushDestructiveButton())
        {
            if (ImGui.Button(Strings.CharactersForget))
            {
                forgetTarget = snapshot.ContentId;
                forgetName = snapshot.Name;
                forgetQuestion = string.Format(CultureInfo.CurrentCulture, Strings.CharactersForgetQuestionFormat, snapshot.Name);
                forgetGate.Cancel();
                ImGui.OpenPopup(Strings.CharactersForgetPopup);
            }
        }

        if ((live || elsewhere) && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(live ? Strings.CharactersForgetLiveHint : Strings.MultiboxForgetHint);
        }

        // Hide and don't track (1.8.0, R7 E): saved in user/characters.json, shared by every game client.
        var book = roster.Settings;
        var hidden = book.IsHidden(snapshot.ContentId);
        if (ImGui.Checkbox(Strings.AltsMenuHide, ref hidden))
        {
            SetHidden(book, snapshot.ContentId, snapshot.Name, hidden);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.AltsHideTooltip);
        }

        // Turning "Don't track" on is armed (Ctrl or Shift and click); turning it off is one plain click.
        Chrome.SameLineOrWrap(ImGui.CalcTextSize(Strings.AltsMenuDontTrack).X + ImGui.GetFrameHeight() + (ImGui.GetStyle().ItemInnerSpacing.X * 2f));
        var untracked = !book.IsTracked(snapshot.ContentId);
        if (Chrome.ArmedCheckbox(Strings.AltsMenuDontTrack, ref untracked, guardedValue: true, trackGuard, GuardedAction.DontTrackCharacter, Strings.AltsDontTrackTooltip, snapshot.ContentId))
        {
            SetTracked(book, snapshot.ContentId, snapshot.Name, tracked: !untracked);
        }
    }

    private void DrawForgetPopup(UiState ui)
    {
        using var modal = ImRaii.PopupModal(Strings.CharactersForgetPopup, ImGuiWindowFlags.AlwaysAutoResize);
        if (!modal)
        {
            return;
        }

        // Opened from the centre column, whose own font scale is 1 (it inherits the window's), so the modal scales itself.
        UiMetrics.ApplyFontScale();

        ImGui.TextWrapped(forgetQuestion);
        ImGui.Spacing();
        using (Theme.PushDestructiveButton())
        {
            // Forgetting cannot be undone: press and hold (feature plan v6 S2).
            var confirmed = Chrome.HoldButton(ForgetConfirmLabel, forgetGate);
            if (ImGui.IsItemHovered())
            {
                Safety.Tooltip(Strings.CharactersForgetConfirmTooltip, GuardedAction.ForgetCharacter);
            }

            if (confirmed)
            {
                if (session.IsLiveElsewhere(forgetTarget))
                {
                    // Logged in on another client while the question was open (1.8.0, R7 G): that client owns it now.
                    ShowToast(string.Format(CultureInfo.CurrentCulture, Strings.AltsForgetRefusedFormat, forgetName));
                    ImGui.CloseCurrentPopup();
                    return;
                }

                // A listener that failed may still hold this character's pins or overrides and save them again: say so.
                if (session.ForgetCharacter(forgetTarget) > 0)
                {
                    ShowToast(string.Format(CultureInfo.CurrentCulture, Strings.CharactersForgetPartialFormat, forgetName));
                }

                snapshotCache.Remove(forgetTarget);
                compareStates.Remove(forgetTarget);
                compareResolving.Remove(forgetTarget);
                accountVersion = -1;
                ui.MarkQueryDirty();
                log.Information("Forgot character {ContentId}", forgetTarget);
                ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();
        if (ImGui.Button(Strings.CharactersCancel))
        {
            ImGui.CloseCurrentPopup();
        }
    }

    private void DrawToast()
    {
        if (toast is null)
        {
            return;
        }

        if (DateTime.UtcNow >= toastUntilUtc)
        {
            toast = null;
            return;
        }

        using (Theme.PushText(Theme.Surface.Text))
        {
            ImGui.TextWrapped(toast);
        }
    }

    /// <summary>
    /// Compare with (V2-12): a combo of the other characters (the one remembered for the viewed character, else the first
    /// other one in the list; hidden ones left out, 1.8.0), the lead
    /// line and per-section counts, then the quests done on the viewed character and not on the other and the reverse,
    /// each ranked by unlock value, capped at <see cref="MaxDiffRows"/> rows and copyable as text. Hidden behind a hint
    /// until a second character is stored.
    /// </summary>
    private void DrawCompare(UiState ui, Dashboard d)
    {
        SectionHeading.Draw(Strings.DiffSection);
        RefreshItems();
        if (items.Length < 2)
        {
            ImGui.TextDisabled(Strings.DiffNeedsTwo);
            return;
        }

        if (d.Bundle is null)
        {
            ImGui.TextDisabled(Strings.DiffNeedsCatalog);
            return;
        }

        if (RefreshCompare(d) is not { } c)
        {
            ImGui.TextDisabled(Strings.DiffNeedsTwo);
            return;
        }

        DrawCompareCombo(c);
        if (c.OtherUnreadable)
        {
            ImGui.TextDisabled(Strings.DiffOtherUnreadable);
            return;
        }

        if (c.Comparing)
        {
            ImGui.TextDisabled(Strings.DiffComparing);
            return;
        }

        ImGui.TextUnformatted(c.Summary);
        ImGui.TextDisabled(c.CountsLine);
        DrawCompareSections(c);
        ImGui.Spacing();
        DrawDiffList(ui, "onlyViewed", c.OnlyViewed, DiffViewedValueWidth);
        ImGui.Spacing();
        DrawDiffList(ui, "onlyOther", c.OnlyOther, DiffOtherValueWidth);
    }

    private void DrawCompareCombo(Compare c)
    {
        ImGui.SetNextItemWidth(Chrome.FitWidth(UiMetrics.CharacterComboWidth));
        using var combo = ImRaii.Combo("##compareWith", c.OtherLabel);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.DiffComboTooltip);
        }

        if (!combo)
        {
            return;
        }

        // The popup opens from the centre column (own font scale 1), so it scales itself.
        UiMetrics.ApplyFontScale();
        for (var i = 0; i < c.Candidates.Length; i++)
        {
            var candidate = c.Candidates[i];
            using var itemId = ImRaii.PushId(i);
            if (ImGui.Selectable(candidate.Label, candidate.ContentId == c.OtherContentId) && session.ViewedContentId is { } viewed)
            {
                // Remembered per viewed character in user/characters.json (1.8.0, R7 B).
                roster.Settings.Edit(CharacterSettingChange.Compare(viewed, candidate.ContentId));
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(candidate.Detail);
            }
        }
    }

    /// <summary>Sections where the two characters differ: how many quests each has that the other lacks.</summary>
    private static void DrawCompareSections(Compare c)
    {
        if (c.Sections.Length == 0)
        {
            return;
        }

        using var table = ImRaii.Table("##diffSections", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        if (DiffCountWidth.Stale(c.Sections))
        {
            // Room for a short name in the header, never less than the widest count.
            var count = UiMetrics.Px(80f);
            foreach (var row in c.Sections)
            {
                count = FixedWidth.Fit(FixedWidth.Fit(count, row.OnlyViewed), row.OnlyOther);
            }

            DiffCountWidth.Store(c.Sections, count);
        }

        // The count columns' headers are character names: they end in an ellipsis instead of widening the columns.
        ImGui.TableSetupColumn(Strings.DiffColumnSection, ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn(c.ViewedHeader, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoHeaderWidth, DiffCountWidth.Value);
        ImGui.TableSetupColumn(c.OtherHeader, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoHeaderWidth, DiffCountWidth.Value);
        ImGui.TableHeadersRow();

        foreach (var row in c.Sections)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            Chrome.FitText(row.Name, ImGui.GetColorU32(ImGuiCol.Text));
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.OnlyViewed);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.OnlyOther);
        }
    }

    // Copy for Discord per diff side (1.8.0), keyed by the list's id.
    private readonly Dictionary<string, DiscordCopy> diffDiscord = [];

    /// <summary>One side of the diff: header with count, Copy list and Copy for Discord, the capped table, then "and N more".</summary>
    private void DrawDiffList(UiState ui, string id, DiffList list, FixedWidth valueWidth)
    {
        using var listId = ImRaii.PushId(id);
        Chrome.FitText(list.Header, ImGui.GetColorU32(ImGuiCol.Text));
        if (list.Rows.Length == 0)
        {
            ImGui.TextDisabled(Strings.DiffNone);
            return;
        }

        Chrome.SameLineOrWrap(ImGui.CalcTextSize(Strings.DiffCopyList).X + (ImGui.GetStyle().FramePadding.X * 2f));
        if (ImGui.SmallButton(Strings.DiffCopyList))
        {
            ImGui.SetClipboardText(list.Clipboard);
            ShowToast(list.CopiedToast);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.DiffCopyListTooltip);
        }

        // Copy for Discord (1.8.0): the header in bold, one bullet per line, in parts of 2,000 characters.
        Chrome.SameLineOrWrap(ImGui.CalcTextSize(Strings.LinksCopyDiscord).X + (ImGui.GetStyle().FramePadding.X * 2f));
        if (!diffDiscord.TryGetValue(id, out var discord))
        {
            discord = new DiscordCopy();
            diffDiscord[id] = discord;
        }

        discord.Draw(id, list, list, static (l, _) => Core.Text.DiscordText.List(l.Header, l.Clipboard.Split('\n')), small: true);

        DrawDiffTable(ui, list.Rows, valueWidth);
        if (list.More.Length > 0)
        {
            ImGui.TextDisabled(list.More);
        }
    }

    /// <summary>Rows: the lacking character's state moon, the quest (click reveals it), the value badge and the reason in Dusk.</summary>
    private static void DrawDiffTable(UiState ui, DiffRow[] rows, FixedWidth valueWidth)
    {
        using var table = ImRaii.Table("##diff", 4, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        if (valueWidth.Stale(rows))
        {
            // The value badge: its text and the pill's padding either side (DrawValueBadge).
            var value = 0f;
            foreach (var row in rows)
            {
                value = FixedWidth.Fit(value, row.Value);
            }

            valueWidth.Store(rows, MathF.Max(value + (UiMetrics.Px(5f) * 2f), ImGui.CalcTextSize(Strings.DiffColumnValue).X));
        }

        var line = ImGui.GetTextLineHeight();
        ImGui.TableSetupColumn("##state", ImGuiTableColumnFlags.WidthFixed, GlyphColumn(line));
        ImGui.TableSetupColumn(Strings.DiffColumnQuest, ImGuiTableColumnFlags.WidthStretch, 3f);
        ImGui.TableSetupColumn(Strings.DiffColumnValue, ImGuiTableColumnFlags.WidthFixed, valueWidth.Value);
        ImGui.TableSetupColumn(Strings.DiffColumnWhy, ImGuiTableColumnFlags.WidthStretch, 2f);
        ImGui.TableHeadersRow();

        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            using var rowId = ImRaii.PushId(i);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            MoonGlyph.DrawInline(row.State, UiMetrics.InlineGlyphSize(line));
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.StateTooltip(row.State, row.Quest), row.StateTooltip);
            }

            ImGui.TableNextColumn();
            if (Chrome.EllipsisSelectable(row.Name, false, 0f, out var cut))
            {
                Reveal(ui, row.Quest);
            }

            ShowInJournalTooltip(row.Name, cut);
            ImGui.TableNextColumn();
            DrawValueBadge(row.Value);
            ImGui.TableNextColumn();
            Chrome.FitText(row.Reason, Theme.U32(Theme.Surface.TextTertiary));
        }
    }

    /// <summary>The unlock value as a small pill: Silver text on a raised rounded rectangle (information, not a call to action, so not gold).</summary>
    private static void DrawValueBadge(string value)
    {
        var pad = UiMetrics.Px(5f);
        var height = ImGui.GetTextLineHeight();
        var min = ImGui.GetCursorScreenPos();
        var max = new Vector2(min.X + ImGui.CalcTextSize(value).X + pad * 2f, min.Y + height);
        ImGui.GetWindowDrawList().AddRectFilled(min, max, Theme.U32(Theme.Surface.Raised), height * 0.35f);
        ImGui.SetCursorScreenPos(new Vector2(min.X + pad, min.Y));
        using (Theme.PushText(Theme.Surface.Text))
        {
            ImGui.TextUnformatted(value);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.DiffValueTooltip);
        }
    }

    /// <summary>
    /// The comparison view model, rebuilt when the session version, the viewed character, the other character or its
    /// capture time, or the bundle changes. The other character is the one remembered for the viewed one, else the first
    /// other one in the list (<see cref="CharacterList.CompareTarget"/>); one forgotten or hidden falls back to that.
    /// </summary>
    private Compare? RefreshCompare(Dashboard d)
    {
        if (TakeCompareResolves())
        {
            // A re-evaluation landed: the comparison is built again with it.
            compare = null;
        }

        var viewedId = d.Snapshot.ContentId;
        var target = CharacterList.CompareTarget(itemEntries, viewedId, roster.Settings.CompareWith(viewedId));
        CharacterItem? other = null;
        foreach (var item in items)
        {
            if (item.ContentId == target)
            {
                other = item;
                break;
            }
        }

        if (other is null)
        {
            return null;
        }

        var key = new CompareKey(session.RosterVersion, viewedId, other.ContentId, other.TakenUtc);
        if (compare is { } current && key == compareKey && ReferenceEquals(current.Bundle, d.Bundle) && ReferenceEquals(current.Rewards, RewardsCatalog()))
        {
            return current;
        }

        compareKey = key;
        compare = BuildCompare(d, other);
        return compare;
    }

    private Compare BuildCompare(Dashboard d, CharacterItem other)
    {
        var bundle = d.Bundle!;
        var viewedId = d.Snapshot.ContentId;
        var candidates = new List<CompareCandidate>(items.Length - 1);
        foreach (var item in items)
        {
            if (item.ContentId != viewedId)
            {
                candidates.Add(new CompareCandidate(item.ContentId, item.Label, item.Detail));
            }
        }

        var viewedHeader = string.Format(CultureInfo.CurrentCulture, Strings.DiffOnlyColumnFormat, d.Name);
        var otherHeader = string.Format(CultureInfo.CurrentCulture, Strings.DiffOnlyColumnFormat, other.Name);
        var otherStates = StatesFor(other, bundle);
        if (otherStates is null)
        {
            return new Compare(bundle, RewardsCatalog(), other.ContentId, other.Label, candidates.ToArray(), true, string.Empty, string.Empty, viewedHeader, otherHeader, [], DiffList.Empty, DiffList.Empty);
        }

        if (ReferenceEquals(otherStates, ComparingStates))
        {
            return new Compare(bundle, RewardsCatalog(), other.ContentId, other.Label, candidates.ToArray(), false, string.Empty, string.Empty, viewedHeader, otherHeader, [], DiffList.Empty, DiffList.Empty, Comparing: true);
        }

        var rewards = RewardsCatalog();
        var diff = DiffResult.Empty;
        try
        {
            var ctx = DiffContext.For(bundle.Catalog, session.FeatureQuestIds, rowId => rewards.ForQuest(rowId).Count);
            diff = CharacterDiff.Compute(bundle.Catalog, session.States, otherStates, ctx);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Characters {Viewed} and {Other} could not be compared", viewedId, other.ContentId);
        }

        var lead = diff.Lead;
        var summary = lead switch
        {
            > 0 => string.Format(CultureInfo.CurrentCulture, Strings.DiffAheadFormat, d.Name, Strings.DiffQuestCount(lead), other.Name),
            < 0 => string.Format(CultureInfo.CurrentCulture, Strings.DiffBehindFormat, d.Name, Strings.DiffQuestCount(-lead), other.Name),
            _ => string.Format(CultureInfo.CurrentCulture, Strings.DiffLevelFormat, d.Name, other.Name),
        };
        var counts = string.Format(CultureInfo.CurrentCulture, Strings.DiffCountsFormat, diff.SharedDone, diff.NeitherDone);

        var sections = new DiffSectionRow[diff.Sections.Count];
        for (var i = 0; i < sections.Length; i++)
        {
            var s = diff.Sections[i];
            var name = s.SectionName.Length == 0 ? string.Format(CultureInfo.InvariantCulture, Strings.CharactersSectionFormat, s.SectionId) : s.SectionName;
            sections[i] = new DiffSectionRow(name, s.OnlyA.ToString(CultureInfo.InvariantCulture), s.OnlyB.ToString(CultureInfo.InvariantCulture));
        }

        return new Compare(
            bundle,
            rewards,
            other.ContentId,
            other.Label,
            candidates.ToArray(),
            false,
            summary,
            counts,
            viewedHeader,
            otherHeader,
            sections,
            BuildDiffList(diff.OnlyA, otherStates, d.Name, other.Name, bundle, session.Spoilers),
            BuildDiffList(diff.OnlyB, session.States, other.Name, d.Name, bundle, session.Spoilers));
    }

    /// <summary>
    /// One side's list: the first <see cref="MaxDiffRows"/> as rows (moon from the lacking side), every entry as
    /// clipboard text. Names go through the viewed character's spoiler shield, the clipboard's too.
    /// </summary>
    private static DiffList BuildDiffList(
        IReadOnlyList<DiffEntry> entries,
        IReadOnlyDictionary<uint, QuestEvaluation> lackingStates,
        string has,
        string lacks,
        CatalogBundle bundle,
        SpoilerMask spoilers)
    {
        var names = bundle.BlockerNames() with { QuestName = spoilers.DisplayName };
        var header = string.Format(CultureInfo.CurrentCulture, Strings.DiffOnlyFormat, has, lacks) + " · " + Strings.DiffQuestCount(entries.Count);
        var rows = new List<DiffRow>(Math.Min(entries.Count, MaxDiffRows));
        var clipboard = new StringBuilder();
        foreach (var entry in entries)
        {
            if (bundle.Catalog.GetByRowId(entry.RowId) is not { } quest)
            {
                continue;
            }

            if (clipboard.Length > 0)
            {
                clipboard.Append('\n');
            }

            clipboard.AppendFormat(CultureInfo.CurrentCulture, Strings.DiffClipboardLineFormat, spoilers.DisplayName(quest), entry.Value);
            if (rows.Count >= MaxDiffRows)
            {
                continue;
            }

            var state = lackingStates.TryGetValue(entry.RowId, out var evaluation) ? evaluation.State : QuestState.Unknown;
            rows.Add(new DiffRow(
                quest,
                spoilers.DisplayName(quest),
                state,
                lacks + ": " + BlockerText.StatusText(evaluation, quest, names, lackingStates),
                entry.Value.ToString(CultureInfo.InvariantCulture),
                entry.Reason));
        }

        var more = entries.Count > MaxDiffRows
            ? string.Format(CultureInfo.CurrentCulture, Strings.DiffMoreFormat, entries.Count - MaxDiffRows)
            : string.Empty;
        var copied = string.Format(CultureInfo.CurrentCulture, Strings.DiffCopiedFormat, Strings.DiffQuestCount(entries.Count));
        return new DiffList(header, rows.ToArray(), more, clipboard.ToString(), copied);
    }

    /// <summary>
    /// The other character's evaluations: the poller's for the live character, otherwise resolved offline from the
    /// stored snapshot once per capture time and bundle. Null when the snapshot cannot be read.
    /// </summary>
    private IReadOnlyDictionary<uint, QuestEvaluation>? StatesFor(CharacterItem item, CatalogBundle bundle)
    {
        if (item.ContentId == session.LiveContentId && session.LiveStates.Count > 0)
        {
            return session.LiveStates;
        }

        // A daily or weekly reset since the comparisons were resolved opens what the stored characters did before it.
        var cycle = GameResets.Cycle(DateTime.UtcNow);
        if (cycle != compareCycle)
        {
            compareCycle = cycle;
            compareStates.Clear();
            compareResolving.Clear();
        }

        // Festivals run server-wide: while someone is logged in the live flags decide for every stored character, so a
        // change of them (a login, a logout, an event starting) resolves the other character again.
        var live = session.LiveSnapshot is { } liveSnapshot ? ServerFestivals.Of(liveSnapshot) : null;
        var hasCached = compareStates.TryGetValue(item.ContentId, out var cached);
        if (hasCached && cached.Taken == item.TakenUtc && ReferenceEquals(cached.Bundle, bundle)
            && (live is null ? cached.Live is null : live.SameAs(cached.Live)))
        {
            return cached.States;
        }

        if (SnapshotFor(item) is not { } snapshot)
        {
            compareStates[item.ContentId] = (item.TakenUtc, bundle, live, null);
            return null;
        }

        if (hasCached && cached.States is { } previous && ReferenceEquals(cached.Bundle, bundle))
        {
            // A newer copy (another game client saved it, D11) or other festivals: resolving the whole catalog takes
            // tens of milliseconds, so it runs on a worker and the comparison keeps the previous result until it lands.
            if (!compareResolving.TryGetValue(item.ContentId, out var running) || running.Taken != item.TakenUtc
                || !(live is null ? running.Live is null : live.SameAs(running.Live)))
            {
                var context = ContextFor(snapshot);
                compareResolving[item.ContentId] = (item.TakenUtc, bundle, live, Task.Run(() => (IReadOnlyDictionary<uint, QuestEvaluation>)StateResolver.ResolveAll(bundle.Catalog, snapshot, context)));
            }

            return previous;
        }

        // The first comparison with this character (or a new catalog): nothing to show meanwhile, but the resolve still
        // runs on a worker (15-40 ms was a hitch on the click) and the comparison reads "Comparing…" until it lands.
        if (!compareResolving.TryGetValue(item.ContentId, out var first) || first.Taken != item.TakenUtc || !ReferenceEquals(first.Bundle, bundle)
            || !(live is null ? first.Live is null : live.SameAs(first.Live)))
        {
            var context = ContextFor(snapshot);
            compareResolving[item.ContentId] = (item.TakenUtc, bundle, live, Task.Run(() => (IReadOnlyDictionary<uint, QuestEvaluation>)StateResolver.ResolveAll(bundle.Catalog, snapshot, context)));
        }

        return ComparingStates;
    }

    /// <summary>What <see cref="StatesFor"/> returns while a character's first comparison resolves on a worker.</summary>
    private static readonly IReadOnlyDictionary<uint, QuestEvaluation> ComparingStates = new Dictionary<uint, QuestEvaluation>();

    /// <summary>
    /// Takes the comparison evaluations that finished on a worker (<see cref="StatesFor"/>); true when one replaced what
    /// the comparison shows. One for a character forgotten meanwhile is dropped.
    /// </summary>
    private bool TakeCompareResolves()
    {
        if (compareResolving.Count == 0)
        {
            return false;
        }

        List<ulong>? done = null;
        foreach (var (id, running) in compareResolving)
        {
            if (running.Task.IsCompleted)
            {
                (done ??= []).Add(id);
            }
        }

        if (done is null)
        {
            return false;
        }

        var taken = false;
        foreach (var id in done)
        {
            // A character forgotten meanwhile took its resolve with it (the Forget button removes both).
            var running = compareResolving[id];
            compareResolving.Remove(id);

            IReadOnlyDictionary<uint, QuestEvaluation>? states = null;
            if (running.Task.IsCompletedSuccessfully)
            {
                states = running.Task.Result;
            }
            else
            {
                log.Warning(running.Task.Exception?.GetBaseException(), "Character {ContentId} could not be evaluated for the comparison", id);
            }

            compareStates[id] = (running.Taken, running.Bundle, running.Live, states);
            taken = true;
        }

        return taken;
    }

    /// <summary>
    /// The context another stored character is resolved with: the session's, with the festivals running on the server
    /// for that character (<see cref="ServerFestivals.For"/>: the live flags while someone is logged in, else its own
    /// less the stale ones) instead of the viewed character's. The daily offer (the live character's) is left out, and
    /// the stored character's dailies and weeklies from before the last reset read as cleared (<see cref="EvalContext.CycleClock"/>).
    /// </summary>
    private EvalContext ContextFor(CharacterSnapshot snapshot) =>
        session.Context.WithDailyOffer(null) with
        {
            ServerFestivals = ServerFestivals.For(snapshot, session.LiveSnapshot, session.Curated.Festivals, DateTime.UtcNow),
            CycleClock = SessionState.StoredCycleClock,
        };

    /// <summary>The Moonlit pane's merged catalog when attached; otherwise the shipped data and curated files, built once.</summary>
    private UniqueRewardCatalog RewardsCatalog()
    {
        if (UniqueRewards is { } provider)
        {
            try
            {
                return provider();
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Unique rewards could not be read for the comparison; using the shipped data");
            }
        }

        return fallbackRewards ??= UniqueRewardCatalog.Build(session.UniqueRewards, new Dictionary<uint, UniqueOverride>(), session.Curated);
    }

    /// <summary>Every character × the selected quest's state, evaluated offline for characters other than the viewed one.</summary>
    private void DrawAccountView(UiState ui)
    {
        SectionHeading.Draw(Strings.CharactersAccountView);
        if (ui.SelectedRowId is not { } rowId)
        {
            ImGui.TextWrapped(Strings.CharactersAccountNoQuest);
            return;
        }

        if (session.Bundle is not { } bundle || bundle.Catalog.GetByRowId(rowId) is not { } quest)
        {
            ImGui.TextWrapped(Strings.CharactersAccountUnknownQuest);
            return;
        }

        if (accountRowId != rowId || accountVersion != session.RosterVersion)
        {
            accountCache.Clear();
            accountRowId = rowId;
            accountVersion = session.RosterVersion;
        }

        Chrome.FitText(session.Spoilers.DisplayName(quest), ImGui.GetColorU32(ImGuiCol.Text));
        RefreshItems();

        using var table = ImRaii.Table("##account", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        var line = ImGui.GetTextLineHeight();
        var stamp = HashCode.Combine(accountVersion, accountRowId);
        if (accountStateWidth.Stale(items, stamp))
        {
            // The moon, the gap SameLine leaves and the widest state word ("Completed"); an unreadable file's words.
            var word = 0f;
            foreach (var item in items)
            {
                var (evaluation, _) = EvaluateFor(item, quest, bundle);
                word = FixedWidth.Fit(word, evaluation is null ? Strings.CharactersSnapshotUnreadable : Strings.StateName(evaluation.State, quest));
            }

            var cell = UiMetrics.InlineGlyphSize(line) + ImGui.GetStyle().ItemSpacing.X + word;
            accountStateWidth.Store(items, FixedWidth.Fit(cell, Strings.CharactersColumnState), stamp);
        }

        // The character's name stretches and ends in an ellipsis before its multibox badge; the state keeps its
        // moon and word, and the reason takes what is left (feature plan v4 L6).
        ImGui.TableSetupColumn(Strings.CharactersColumnCharacter, ImGuiTableColumnFlags.WidthStretch, 2f);
        ImGui.TableSetupColumn(Strings.CharactersColumnState, ImGuiTableColumnFlags.WidthFixed, accountStateWidth.Value);
        ImGui.TableSetupColumn(Strings.CharactersColumnStatus, ImGuiTableColumnFlags.WidthStretch, 3f);
        ImGui.TableHeadersRow();

        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var (evaluation, reason) = EvaluateFor(item, quest, bundle);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            if (item.Elsewhere)
            {
                // Multibox (D11): its state comes from the other client's latest save. The badge keeps its place at
                // the name's end; the name gives way first.
                var badgeWidth = ImGui.CalcTextSize(MultiboxBadge.Value).X + ImGui.GetStyle().ItemSpacing.X;
                Chrome.EllipsisText(item.Name, MathF.Max(0f, Chrome.RoomX() - badgeWidth), ImGui.GetColorU32(ImGuiCol.Text));
                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(item.Name);
                }

                ImGui.SameLine();
                ImGui.TextDisabled(MultiboxBadge.Value);
                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(Strings.MultiboxLiveElsewhere, Strings.MultiboxLiveElsewhereTooltip);
                }
            }
            else
            {
                Chrome.FitText(item.Name, ImGui.GetColorU32(ImGuiCol.Text));
            }

            ImGui.TableNextColumn();
            if (evaluation is null)
            {
                MoonGlyph.DrawInline(QuestState.Unknown, UiMetrics.InlineGlyphSize(line));
                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(Strings.StateTooltip(QuestState.Unknown), Strings.CharactersSnapshotUnreadable);
                }

                ImGui.SameLine();
                ImGui.TextDisabled(Strings.CharactersSnapshotUnreadable);
            }
            else
            {
                MoonGlyph.DrawInline(evaluation.State, UiMetrics.InlineGlyphSize(line));
                if (ImGui.IsItemHovered())
                {
                    // The viewed character has the session's state map (the same line Flight and Moonlit show); another
                    // character's evaluation has none, so its blocker line reads the done ids it carries.
                    UiMetrics.StateTooltip(evaluation.State, evaluation, quest, session.Names, item.ContentId == session.ViewedContentId ? session.States : null);
                }

                ImGui.SameLine();
                using (Theme.PushText(Theme.StateText(evaluation.State)))
                {
                    ImGui.TextUnformatted(Strings.StateName(evaluation.State, quest));
                }
            }

            ImGui.TableNextColumn();
            if (reason.Length > 0)
            {
                // The state column beside it already names the state; this is the reason alone (blocker or step).
                Chrome.FitText(reason, ImGui.GetColorU32(ImGuiCol.Text));
            }
        }
    }

    private static void Reveal(UiState ui, QuestRecord quest) => ui.Reveal(quest);

    /// <summary>
    /// One character's evaluation of the quest and its reason line, memoized in <see cref="accountCache"/> (cleared
    /// when the quest or the session version changes): the viewed character's comes from the session, the others are
    /// resolved offline from their stored snapshot. The reason is composed here, not per frame.
    /// </summary>
    private (QuestEvaluation? Evaluation, string Reason) EvaluateFor(CharacterItem item, QuestRecord quest, CatalogBundle bundle)
    {
        if (accountCache.TryGetValue(item.ContentId, out var cached))
        {
            return cached;
        }

        QuestEvaluation? evaluation = null;
        IReadOnlyDictionary<uint, QuestEvaluation>? states = null;
        if (item.ContentId == session.ViewedContentId)
        {
            states = session.States;
            states.TryGetValue(quest.RowId, out evaluation);
        }
        else if (SnapshotFor(item) is { } snapshot)
        {
            try
            {
                evaluation = StateResolver.Resolve(quest, snapshot, bundle.Catalog, ContextFor(snapshot));
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Could not evaluate quest {RowId} for character {ContentId}", quest.RowId, item.ContentId);
            }
        }

        var reason = evaluation is null ? string.Empty : BlockerText.Reason(evaluation, quest, session.Names, states);
        var entry = (evaluation, reason);
        accountCache[item.ContentId] = entry;
        return entry;
    }

    /// <summary>The stored snapshot of a character (or null when unreadable), reloaded only when its capture time changed.</summary>
    private CharacterSnapshot? SnapshotFor(CharacterItem item)
    {
        if (snapshotCache.TryGetValue(item.ContentId, out var cached) && cached.Taken == item.TakenUtc)
        {
            return cached.Snapshot;
        }

        CharacterSnapshot? loaded = null;
        try
        {
            loaded = loadSnapshot(item.ContentId);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not load the snapshot of character {ContentId}", item.ContentId);
        }

        snapshotCache[item.ContentId] = (item.TakenUtc, loaded);
        return loaded;
    }

    private void Export(CharacterSnapshot snapshot)
    {
        try
        {
            var dir = Path.Combine(paths.ConfigDir, ExportsFolder);
            Directory.CreateDirectory(dir);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
            var file = Path.Combine(dir, SafeFileName(snapshot.Name) + "-" + stamp + ".json");

            var source = paths.SnapshotFile(snapshot.ContentId);
            // Read and written the multibox-safe way (D11): another game client may save the snapshot meanwhile.
            var text = AtomicFile.Read(source) ?? JsonSerializer.Serialize(snapshot, ExportJson);
            AtomicFile.Write(file, text);

            log.Information("Exported {Name} to {Path}", snapshot.Name, file);
            ShowToast(string.Format(CultureInfo.CurrentCulture, Strings.CharactersExportedFormat, file));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            log.Error(ex, "Export of {Name} failed", snapshot.Name);
            ShowToast(string.Format(CultureInfo.CurrentCulture, Strings.CharactersExportFailedFormat, ex.Message));
        }
    }

    /// <summary>
    /// Where a finished action's note goes (Exported, Forgot, Copied): the main window's status bar (feature plan v6,
    /// U4), so it never takes a line of the dashboard. Without it the note shows inline, as before 1.12.
    /// </summary>
    public Action<string>? ShowNote { get; set; }

    private void ShowToast(string text)
    {
        if (ShowNote is { } note)
        {
            note(text);
            return;
        }

        toast = text;
        toastUntilUtc = DateTime.UtcNow + ToastDuration;
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] == ' ' || Array.IndexOf(invalid, chars[i]) >= 0)
            {
                chars[i] = '_';
            }
        }

        var result = new string(chars).Trim('_');
        return result.Length == 0 ? "character" : result;
    }

    /// <summary>
    /// The characters the dashboard's lists read (hidden ones left out but the one on view), once per session version,
    /// roster version and minute (the ages tick).
    /// </summary>
    private void RefreshItems()
    {
        var minute = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute;
        var rosterVersion = roster.Version;
        if (itemsVersion == session.RosterVersion && itemsRoster == rosterVersion && itemsMinute == minute)
        {
            return;
        }

        itemsVersion = session.RosterVersion;
        itemsRoster = rosterVersion;
        itemsMinute = minute;
        // Hidden characters are left out, except the one on view (its own row in the account view).
        var viewed = session.ViewedContentId;
        itemEntries = roster.All.Where(e => !e.Hidden || e.LiveHere || e.ContentId == viewed).ToList();
        var built = new CharacterItem[itemEntries.Count];
        for (var i = 0; i < built.Length; i++)
        {
            built[i] = ItemFor(itemEntries[i]);
        }

        items = built;
    }

    /// <summary>One character's labels: the marker and name, then world, state (live, elsewhere or age), count and badges.</summary>
    private CharacterItem ItemFor(CharacterEntry c)
    {
        var notUpdating = session.NotUpdating.TryGetValue(c.ContentId, out var problem) ? problem : (SharedLoad?)null;
        var label = (c.LiveHere ? Strings.CharactersLiveMarker : c.LiveElsewhere ? Strings.MultiboxMarker : string.Empty) + c.Name;
        var state = c.LiveHere ? Strings.CharactersLive : c.LiveElsewhere ? Strings.MultiboxLiveElsewhere : Age(c.TakenUtc);
        var detail = new StringBuilder(c.WorldName).Append(" · ").Append(state).Append(" · ").Append(Strings.CharactersCompleted(c.CompletedCount));
        if (notUpdating is not null)
        {
            detail.Append(" · ").Append(Strings.AltsNotUpdatingBadge);
        }

        if (!c.Tracked)
        {
            detail.Append(" · ").Append(Strings.AltsUntrackedBadge);
        }

        if (c.Hidden)
        {
            detail.Append(" · ").Append(Strings.AltsHiddenBadge);
        }

        return new CharacterItem(c.ContentId, c.Name, c.TakenUtc, label, detail.ToString(), c.LiveElsewhere, c.Hidden, c.Tracked, notUpdating, c.WorldName);
    }

    /// <summary>
    /// The left column's runs (1.8.0, R7 H): the roster with hidden characters only when shown, matched against the
    /// search box, grouped by data center when the setting is on. Rebuilt when any of those or the ages change.
    /// </summary>
    private void RefreshList()
    {
        var key = new ListKey(session.RosterVersion, roster.Version, DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute, settings.ShowHiddenCharacters, settings.CharacterListByDataCenter, listSearch);
        if (key == listKey)
        {
            return;
        }

        listKey = key;
        var all = roster.All;
        listTotal = all.Count;
        listHiddenCount = 0;
        foreach (var entry in all)
        {
            listHiddenCount += entry.Hidden && !entry.LiveHere ? 1 : 0;
        }

        listShowHiddenLabel = string.Format(CultureInfo.CurrentCulture, Strings.AltsShowHiddenFormat, listHiddenCount);
        var shown = CharacterList.Visible(all, settings.ShowHiddenCharacters).Where(e => CharacterList.Matches(e, listSearch)).ToList();
        var groups = CharacterList.Group(shown, settings.CharacterListByDataCenter);
        listGroups = groups.Select(g => new ListGroup(g.UnknownDataCenter ? Strings.AltsUnknownDataCenter : g.DataCenter, g.Entries.Select(ItemFor).ToArray())).ToList();
    }

    /// <summary>
    /// The dashboard view model, rebuilt only when the session version, the viewed character, the catalog bundle, the
    /// session's node icons or the live flag changes (and once a minute so the snapshot age ticks).
    /// </summary>
    private Dashboard RefreshDashboard(CharacterSnapshot snapshot)
    {
        var bundle = session.Bundle;
        var minute = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute;
        var key = new DashboardKey(session.Version, snapshot.ContentId, session.IsLive, minute, MoonlitCounts is not null, Pins?.PinsVersion ?? -1, settings.FreeTrialView);
        var icons = session.NodeIcons;
        if (dashboard is { } current && key == dashboardKey && ReferenceEquals(current.Snapshot, snapshot) && ReferenceEquals(current.Bundle, bundle)
            && ReferenceEquals(dashboardIcons, icons))
        {
            return current;
        }

        dashboardKey = key;
        dashboardIcons = icons;
        dashboard = BuildDashboard(snapshot, bundle, icons);
        return dashboard;
    }

    private Dashboard BuildDashboard(CharacterSnapshot snapshot, CatalogBundle? bundle, NodeIconMap icons)
    {
        var names = bundle?.Names;
        var taken = snapshot.TakenUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var takenLine = session.IsLive
            ? Strings.CharactersLive + " · " + taken
            : session.IsLiveElsewhere(snapshot.ContentId)
                ? Strings.MultiboxMarker + Strings.MultiboxLiveElsewhere + " · " + taken
                : string.Format(CultureInfo.CurrentCulture, Strings.CharactersSnapshotFormat, taken, Age(snapshot.TakenUtc));

        var completed = 0;
        foreach (var b in snapshot.CompletedBits)
        {
            completed += System.Numerics.BitOperations.PopCount(b);
        }

        var currentJob = JobName(names, snapshot.CurrentJob);
        var countsLine = Strings.CharactersCompleted(completed) + " · "
                         + string.Format(CultureInfo.CurrentCulture, Strings.CharactersAcceptedFormat, snapshot.Accepted.Count) + " · " + currentJob;

        string gcLine;
        if (snapshot.GrandCompany == 0)
        {
            gcLine = Strings.CharactersNoGrandCompany;
        }
        else
        {
            var gcName = names?.GrandCompany(snapshot.GrandCompany) is { Length: > 0 } n ? n : GrandCompanies.Name(snapshot.GrandCompany);
            var rank = snapshot.GrandCompany < snapshot.GcRanks.Length ? snapshot.GcRanks[snapshot.GrandCompany] : (byte)0;
            gcLine = string.Format(CultureInfo.CurrentCulture, Strings.CharactersGcRankFormat, gcName, rank);
        }

        var tribes = new List<(byte Id, TribeStanding Standing)>(snapshot.Tribes.Count);
        foreach (var (tribe, standing) in snapshot.Tribes)
        {
            tribes.Add((tribe, standing));
        }

        tribes.Sort((a, b) => a.Id.CompareTo(b.Id));
        var tribeRows = new (byte Id, string Tribe, string Rank, string Value)[tribes.Count];
        for (var i = 0; i < tribeRows.Length; i++)
        {
            var (id, standing) = tribes[i];
            var tribeName = names?.Tribe(id) is { Length: > 0 } t ? t : string.Format(CultureInfo.InvariantCulture, Strings.CharactersTribeFormat, id);
            var rankName = names?.TribeRank(standing.Rank) is { Length: > 0 } r ? r : TribeRanks.Name(standing.Rank);
            tribeRows[i] = (id, tribeName, rankName, standing.Value.ToString(CultureInfo.InvariantCulture));
        }

        // A stored character's allied society allowances are full again once the daily reset passed since it was saved.
        var tribeAllowance = !session.IsLive && bundle is not null
            ? GameResets.AsOf(snapshot, bundle.Catalog, DateTime.UtcNow).TribeAllowance
            : snapshot.TribeAllowance;
        var allowances = string.Format(CultureInfo.CurrentCulture, Strings.CharactersAllowancesFormat, tribeAllowance, snapshot.LeveAllowance);

        var (msqLine, msqQuest) = BuildMsq(bundle);
        RefreshDerived(bundle);
        var jobs = BuildJobs(snapshot, bundle);
        var (chainRows, notStarted) = BuildChains(bundle);
        var notStartedLabel = notStarted.Length == 0
            ? string.Empty
            : string.Format(CultureInfo.CurrentCulture, Strings.JobsChainsNotStartedFormat, notStarted.Length);

        var jobIcon = snapshot.CurrentJob == 0 ? 0u : MoonlitIconResolver.ClassJobIconBase + snapshot.CurrentJob;
        foreach (var job in jobs)
        {
            if (job.JobId == snapshot.CurrentJob && job.IconId != 0)
            {
                jobIcon = job.IconId;
                break;
            }
        }

        return new Dashboard(
            snapshot,
            bundle,
            snapshot.Name,
            WorldName(snapshot.World),
            takenLine,
            countsLine,
            msqLine,
            msqQuest,
            BuildSections(bundle, icons),
            BuildJobQuests(jobs),
            chainRows,
            notStarted,
            notStartedLabel,
            BuildMoonlit(),
            BuildPinned(snapshot, bundle),
            BuildRecent(bundle),
            jobs,
            gcLine,
            tribeRows,
            allowances,
            jobIcon);
    }

    /// <summary>
    /// Ladders follow the bundle. Chains are the session's (<see cref="SessionState.Chains"/>, built and logged once per
    /// bundle there), so the dashboard and the detail pane read the same catalog.
    /// </summary>
    private void RefreshDerived(CatalogBundle? bundle)
    {
        if (ReferenceEquals(derivedBundle, bundle))
        {
            return;
        }

        derivedBundle = bundle;
        ladder = JobLadder.Empty;
        if (bundle is null)
        {
            return;
        }

        try
        {
            ladder = bundle.BuildJobLadder();
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Job ladders could not be built for the dashboard");
        }
    }

    /// <summary>
    /// One row per job of the job table (same order and the same class/job collapsing), then one per role those jobs
    /// cover, in role order. Jobs without quests (none in the sheet) are skipped.
    /// </summary>
    private LadderRow[] BuildJobQuests(JobRow[] jobs)
    {
        var states = session.States;
        if (states.Count == 0 || ladder.Jobs.Count == 0)
        {
            return [];
        }

        var rows = new List<LadderRow>(jobs.Length + 5);
        var roleLevels = new SortedDictionary<JobRole, short>();
        foreach (var job in jobs)
        {
            if (ladder.RoleOf(job.JobId) is { } role)
            {
                roleLevels[role] = Math.Max(roleLevels.GetValueOrDefault(role), job.LevelValue);
            }

            if (ladder.ForJob(job.JobId) is not { } entry)
            {
                continue;
            }

            rows.Add(LadderRowFor(job.IconId, job.Name, job.Level, isRole: false, ladder.Progress(entry, states, job.LevelValue), entry.QuestRowIds));
        }

        foreach (var (role, level) in roleLevels)
        {
            var quests = ladder.RoleLadder(role);
            if (quests.Count == 0)
            {
                continue;
            }

            var name = string.Format(CultureInfo.CurrentCulture, Strings.JobsRoleRowFormat, Strings.JobsRoleName(role));
            rows.Add(LadderRowFor(PaneIcons.Role(role), name, string.Empty, isRole: true, ladder.Progress(quests, states, level), quests));
        }

        return rows.ToArray();
    }

    private LadderRow LadderRowFor(uint iconId, string name, string level, bool isRole, LadderProgress progress, IReadOnlyList<uint> rowIds)
    {
        var count = string.Format(CultureInfo.InvariantCulture, Strings.JobsCountFormat, progress.Done, progress.Total);
        var next = progress.NextRowId is { } nextRowId ? derivedBundle?.Catalog.GetByRowId(nextRowId) : null;
        string text;
        if (next is null)
        {
            text = Strings.JobsAllDone;
        }
        else if (progress.IsReadyNow)
        {
            text = string.Format(CultureInfo.CurrentCulture, Strings.JobsNextReadyFormat, session.Spoilers.DisplayName(next), next.DisplayLevel);
        }
        else
        {
            // The decisive blocker ("after MSQ: The Vault", "Lv 80 on DRG") rather than only the level, so a job quest
            // gated by the main scenario says so; the level is the fallback when nothing else is known.
            var blocker = session.States.TryGetValue(next.RowId, out var evaluation) ? BlockerText.For(evaluation, next, session.Names, session.States) : string.Empty;
            text = blocker.Length > 0
                ? string.Format(CultureInfo.CurrentCulture, Strings.JobsNextBlockedFormat, session.Spoilers.DisplayName(next), blocker)
                : string.Format(CultureInfo.CurrentCulture, Strings.JobsNextLaterFormat, session.Spoilers.DisplayName(next), next.DisplayLevel);
        }

        return new LadderRow(iconId, name, level, isRole, progress.Fraction, count, LeftText.Left(progress.Done, progress.Total), next, text, progress.IsReadyNow, rowIds);
    }

    /// <summary>Curated chains in file order, split into those with at least one quest done and those not started.</summary>
    private (ChainRow[] Started, ChainRow[] NotStarted) BuildChains(CatalogBundle? bundle)
    {
        var states = session.States;
        var chains = session.Chains;
        if (bundle is null || states.Count == 0 || chains.Chains.Count == 0)
        {
            return ([], []);
        }

        var curatedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in session.Curated.Chains)
        {
            curatedNames.Add(entry.Name);
        }

        var started = new List<ChainRow>();
        var notStarted = new List<ChainRow>();
        foreach (var chain in chains.Chains)
        {
            if (!curatedNames.Contains(chain.Name))
            {
                continue;
            }

            var progress = ChainCatalog.Progress(chain, states);
            if (progress.IsEmpty)
            {
                // Nothing in it counts for this character: not a chain to finish, and never "complete".
                continue;
            }

            var next = progress.NextRowId is { } nextRowId ? bundle.Catalog.GetByRowId(nextRowId) : null;
            var row = new ChainRow(
                chain.Name,
                progress.Fraction,
                string.Format(CultureInfo.CurrentCulture, Strings.JobsChainCountFormat, progress.Done, progress.Total),
                LeftText.Left(progress.Done, progress.Total),
                next,
                next is null ? Strings.JobsChainComplete : string.Format(CultureInfo.CurrentCulture, Strings.JobsChainNextFormat, session.Spoilers.DisplayName(next)),
                chain.RowIds)
            {
                RecapQuest = RecapQuestOf(chains, chain, states),
            };
            (progress.Done > 0 ? started : notStarted).Add(row);
        }

        return (started.ToArray(), notStarted.ToArray());
    }

    /// <summary>
    /// A quest that names <paramref name="chain"/> for the story recap (one <see cref="ChainCatalog.ForQuest"/> maps
    /// back to it), once the character has completed some quest of it; 0 otherwise, and the row offers no recap.
    /// </summary>
    private static uint RecapQuestOf(ChainCatalog chains, Chain chain, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        if (!StoryRecap.HasStarted(chain, rowId => states.TryGetValue(rowId, out var evaluation) && evaluation.State == QuestState.Completed))
        {
            return 0;
        }

        foreach (var rowId in chain.RowIds)
        {
            if (ReferenceEquals(chains.ForQuest(rowId), chain))
            {
                return rowId;
            }
        }

        return 0;
    }

    /// <summary>"MSQ: &lt;expansion&gt; · next: &lt;quest&gt; (&lt;NPC&gt;, &lt;zone&gt;)" for the viewed character; empty without evaluations.</summary>
    private (string Line, QuestRecord? Quest) BuildMsq(CatalogBundle? bundle)
    {
        if (bundle is null || session.States.Count == 0)
        {
            return (string.Empty, null);
        }

        MsqPosition? position;
        try
        {
            position = MsqProgress.Compute(bundle.Catalog, session.States);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Main scenario position could not be computed");
            return (string.Empty, null);
        }

        if (position is null)
        {
            return (string.Empty, null);
        }

        if (position.Next is not { } next)
        {
            return (Strings.CharactersMsqComplete, null);
        }

        var expansion = bundle.Names.Expansion(next.Expansion) is { Length: > 0 } named ? named : Expansions.Name(next.Expansion);
        if (position.IsBranched)
        {
            // Inside a branch region: "MSQ: Evercold · route A 3 of 9 · route B not started · route C done"; a click
            // selects the first route's next quest.
            return (string.Format(CultureInfo.CurrentCulture, Strings.CharactersMsqRoutesFormat, expansion, MsqText.Spelled(position, session.Spoilers.DisplayName)), next);
        }

        if (next.Issuer is not { } issuer)
        {
            return (string.Format(CultureInfo.CurrentCulture, Strings.CharactersMsqNoGiverFormat, expansion, session.Spoilers.DisplayName(next)), next);
        }

        var zone = ZoneName(issuer.MapId);
        var giver = zone.Length > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.MsqGiverFormat, issuer.Name, zone) : issuer.Name;
        return (string.Format(CultureInfo.CurrentCulture, Strings.CharactersMsqFormat, expansion, session.Spoilers.DisplayName(next), giver), next);
    }

    /// <summary>Place name of a map from the Map sheet; empty without game data or for an unknown id.</summary>
    private string ZoneName(uint mapId)
    {
        if (mapId == 0 || data is null)
        {
            return string.Empty;
        }

        if (zoneNames.TryGetValue(mapId, out var known))
        {
            return known;
        }

        var name = string.Empty;
        try
        {
            name = data.GetExcelSheet<Map>()?.GetRowOrDefault(mapId)?.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Map {MapId} could not be read for the dashboard", mapId);
        }

        zoneNames[mapId] = name;
        return name;
    }

    /// <summary>
    /// All quests first, then every journal section in journal order, from the viewed character's evaluations; each
    /// section's ring carries its icon from <paramref name="icons"/> (the session's map for the catalog).
    /// </summary>
    private SectionRow[] BuildSections(CatalogBundle? bundle, NodeIconMap icons)
    {
        if (bundle is null || session.States.Count == 0)
        {
            return [];
        }

        TreeCounts counts;
        try
        {
            // The free-trial view (1.9.0) counts as the Journal tree does: what the trial does not include is not left to do.
            counts = TreeCounts.Compute(bundle.Catalog, session.States, includeUnlisted: false, settings.FreeTrialView ? FreeTrial.IsBeyond : null);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Section counts could not be computed");
            return [];
        }

        var sections = new List<(uint Id, string Name)>(bundle.Catalog.BySection.Count);
        foreach (var (id, quests) in bundle.Catalog.BySection)
        {
            if (quests.Count == 0)
            {
                continue;
            }

            var name = quests[0].Journal.SectionName;
            sections.Add((id, name.Length == 0 ? string.Format(CultureInfo.InvariantCulture, Strings.CharactersSectionFormat, id) : name));
        }

        sections.Sort((a, b) => a.Id.CompareTo(b.Id));
        var rows = new SectionRow[sections.Count + 1];
        rows[0] = SectionRow.From(Strings.CharactersAllQuests, counts.Overall, overall: true, NodeIcon.Of(OrnamentGlyph.AllQuests));
        for (var i = 0; i < sections.Count; i++)
        {
            var scope = QuestScope.Section(sections[i].Id);
            rows[i + 1] = SectionRow.From(sections[i].Name, counts.Section(sections[i].Id), overall: false, icons.For(scope));
        }

        return rows;
    }

    private MoonlitRow[] BuildMoonlit()
    {
        if (MoonlitCounts is not { } countsFor)
        {
            return [];
        }

        var rows = new List<MoonlitRow>(SummaryKinds.Length);
        foreach (var kind in SummaryKinds)
        {
            UniqueRewardCounts counts;
            try
            {
                counts = countsFor(kind);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Moonlit counts for {Kind} could not be read", kind);
                continue;
            }

            if (counts.Total == 0)
            {
                continue;
            }

            rows.Add(new MoonlitRow(
                kind,
                Strings.MoonlitKindName(kind),
                counts.Obtained.ToString(CultureInfo.InvariantCulture) + "/" + counts.Total.ToString(CultureInfo.InvariantCulture),
                (float)counts.Obtained / counts.Total,
                counts.Unknown == counts.Total));
        }

        return rows.ToArray();
    }

    /// <summary>
    /// The viewed character's pins from the runner (the order they were pinned), with the current state and next
    /// step. The runner's pins follow the viewed character, so a snapshot other than the viewed one has none.
    /// </summary>
    private PinnedRow[] BuildPinned(CharacterSnapshot snapshot, CatalogBundle? bundle)
    {
        if (Pins is not { } runner || session.ViewedContentId != snapshot.ContentId)
        {
            return [];
        }

        var list = runner.PinnedInOrder;
        if (list.Count == 0)
        {
            return [];
        }

        var rows = new List<PinnedRow>(Math.Min(list.Count, MaxPinnedRows));
        foreach (var rowId in list)
        {
            if (rows.Count >= MaxPinnedRows)
            {
                break;
            }

            var quest = bundle?.Catalog.GetByRowId(rowId);
            var evaluation = session.States.TryGetValue(rowId, out var e) ? e : null;
            rows.Add(new PinnedRow(
                quest,
                quest is null ? string.Format(CultureInfo.InvariantCulture, Strings.MoonlitQuestFormat, rowId) : session.Spoilers.DisplayName(quest),
                evaluation?.State ?? QuestState.Unknown,
                quest is null ? string.Empty : BlockerText.StatusText(evaluation, quest, session.Names, session.States)));
        }

        return rows.ToArray();
    }

    private RecentRow[] BuildRecent(CatalogBundle? bundle)
    {
        if (!session.IsLive)
        {
            return [];
        }

        var events = session.RecentEvents;
        var count = Math.Min(events.Count, MaxRecentRows);
        var rows = new RecentRow[count];
        for (var i = 0; i < count; i++)
        {
            var ev = events[i];
            var quest = bundle?.Catalog.GetByRowId(ev.RowId);
            rows[i] = new RecentRow(
                ev.TimeUtc.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture),
                Strings.CharactersEventName(ev.Kind),
                EventColor(ev.Kind),
                quest is null ? string.Format(CultureInfo.InvariantCulture, Strings.MoonlitQuestFormat, ev.RowId) : session.Spoilers.DisplayName(quest));
        }

        return rows;
    }

    /// <summary>
    /// Jobs with a level, grouped by role and sorted by level within a group. A class and its job share one level
    /// slot, so the pair is collapsed: the job shows once its unlock quest is complete, the class until then.
    /// Nameless placeholder rows and zero levels are skipped.
    /// </summary>
    private static JobRow[] BuildJobs(CharacterSnapshot snapshot, CatalogBundle? bundle)
    {
        var names = bundle?.Names;
        var infos = names?.ClassJobInfos ?? [];
        var rows = new List<JobRow>(snapshot.JobLevels.Count);
        foreach (var (jobId, level) in snapshot.JobLevels)
        {
            if (level <= 0)
            {
                continue;
            }

            var info = names?.ClassJobInfo(jobId);
            if (info is null || info.Name.Length == 0)
            {
                continue;
            }

            if (info.ParentRowId != info.RowId && info.ParentRowId != 0)
            {
                // A job grown from a class: only once unlocked; the class stands in until then.
                if (!IsUnlocked(info, snapshot))
                {
                    continue;
                }
            }
            else if (HasUnlockedJob(info, infos, snapshot))
            {
                continue;
            }

            rows.Add(new JobRow(
                GroupOf(info, bundle),
                info.RowId,
                info.IconId,
                DisplayName(info.Name),
                info.Abbreviation,
                level.ToString(CultureInfo.InvariantCulture),
                level));
        }

        rows.Sort(static (a, b) =>
        {
            var byGroup = a.Group.CompareTo(b.Group);
            if (byGroup != 0)
            {
                return byGroup;
            }

            var byLevel = b.LevelValue.CompareTo(a.LevelValue);
            return byLevel != 0 ? byLevel : string.CompareOrdinal(a.Name, b.Name);
        });
        return rows.ToArray();
    }

    private static bool IsUnlocked(ClassJobInfo job, CharacterSnapshot snapshot) =>
        job.UnlockQuestRowId == 0 || snapshot.IsCompleted(QuestRecord.ToQuestId(job.UnlockQuestRowId));

    private static bool HasUnlockedJob(ClassJobInfo @class, IReadOnlyList<ClassJobInfo> all, CharacterSnapshot snapshot)
    {
        foreach (var other in all)
        {
            if (other.RowId != @class.RowId && other.ParentRowId == @class.RowId && IsUnlocked(other, snapshot))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>ClassJobCategory 31 is "Disciples of Magic": the sheet's role 3 covers both ranged and casters, this tells them apart.</summary>
    private const uint DisciplesOfMagicCategory = 31;

    private static JobGroup GroupOf(ClassJobInfo info, CatalogBundle? bundle)
    {
        if (info.IsCrafter)
        {
            return JobGroup.Crafter;
        }

        if (info.IsGatherer)
        {
            return JobGroup.Gatherer;
        }

        return info.Role switch
        {
            1 => JobGroup.Tank,
            4 => JobGroup.Healer,
            2 => JobGroup.Melee,
            3 => info.RowId <= byte.MaxValue && bundle?.Jobs.Admits(DisciplesOfMagicCategory, (byte)info.RowId) == true ? JobGroup.Caster : JobGroup.Ranged,
            _ => JobGroup.Other,
        };
    }

    /// <summary>The sheet names jobs in lower case ("black mage"); the dashboard shows them as titles.</summary>
    private static string DisplayName(string name) =>
        name.Length == 0 ? name : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name);

    private static Vector4 EventColor(QuestEventKind kind) => kind switch
    {
        QuestEventKind.Completed => Theme.AccentDim,
        QuestEventKind.Accepted => Theme.Surface.Text,
        QuestEventKind.NewlyAvailable => Theme.Accent,
        QuestEventKind.Abandoned => Theme.Danger,
        _ => Theme.Surface.TextTertiary,
    };

    private static string JobName(GameNames? names, byte job)
    {
        if (job == 0)
        {
            return string.Format(CultureInfo.InvariantCulture, Strings.CharactersJobFormat, 0);
        }

        var name = names?.ClassJob(job);
        return string.IsNullOrEmpty(name) ? string.Format(CultureInfo.InvariantCulture, Strings.CharactersJobFormat, job) : DisplayName(name);
    }

    private string WorldName(uint world)
    {
        if (worldNames is null)
        {
            worldNames = [];
            if (data is not null)
            {
                try
                {
                    foreach (var row in data.GetExcelSheet<World>())
                    {
                        var name = row.Name.ExtractText();
                        if (name.Length != 0)
                        {
                            worldNames[row.RowId] = name;
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "World sheet could not be read; world ids are shown instead of names");
                }
            }
        }

        return worldNames.TryGetValue(world, out var known) ? known : string.Format(CultureInfo.InvariantCulture, Strings.CharactersWorldFormat, world);
    }

    /// <summary>How old a capture is, by the one rule every surface follows (<see cref="UiFormat.Age"/>).</summary>
    private static string Age(DateTime takenUtc) => UiFormat.Age(takenUtc);

    /// <summary>Role groups in display order.</summary>
    public enum JobGroup
    {
        Tank,
        Healer,
        Melee,
        Ranged,
        Caster,
        Crafter,
        Gatherer,
        Other,
    }

    /// <param name="Elsewhere">Logged in on another game client (multibox, D11): badged, and never forgotten from here.</param>
    /// <param name="Hidden">Hidden from the lists (1.8.0); listed only with "Show hidden", dimmed.</param>
    /// <param name="Tracked">False after "Don't track this character".</param>
    /// <param name="NotUpdating">Its file was saved by another client and cannot be read here (a newer plugin's, or broken).</param>
    private sealed record CharacterItem(
        ulong ContentId,
        string Name,
        DateTime TakenUtc,
        string Label,
        string Detail,
        bool Elsewhere = false,
        bool Hidden = false,
        bool Tracked = true,
        SharedLoad? NotUpdating = null,
        string WorldName = "");

    /// <summary>One run of the left column under its data center heading (empty: no heading).</summary>
    private sealed record ListGroup(string Heading, CharacterItem[] Items);

    private readonly record struct ListKey(int Version, int Roster, long Minute, bool ShowHidden, bool ByDataCenter, string Search);

    private readonly record struct DashboardKey(int Version, ulong ContentId, bool Live, long Minute, bool HasMoonlit, int PinsVersion, bool FreeTrial);

    /// <param name="Icon">The section's official icon (or glyph) inside its ring; the crest glyph for All quests.</param>
    private sealed record SectionRow(string Name, string Count, string Percent, float Fraction, bool Overall, NodeIcon Icon)
    {
        public static SectionRow From(string name, NodeCount count, bool overall, NodeIcon icon)
        {
            var percent = count.Total == 0 ? 0 : (int)MathF.Round(100f * count.Done / count.Total);
            return new SectionRow(
                name,
                count.Done.ToString(CultureInfo.InvariantCulture) + "/" + count.Total.ToString(CultureInfo.InvariantCulture),
                percent.ToString(CultureInfo.InvariantCulture) + "%",
                count.Fraction,
                overall,
                icon);
        }
    }

    private sealed record MoonlitRow(RewardKind Kind, string Name, string Count, float Fraction, bool AllUnknown);

    private sealed record PinnedRow(QuestRecord? Quest, string Name, QuestState State, string NextStep);

    private sealed record RecentRow(string Time, string Kind, Vector4 Color, string Quest);

    private sealed record JobRow(JobGroup Group, uint JobId, uint IconId, string Name, string Abbreviation, string Level, short LevelValue);

    /// <summary>A job's (or a role's) ladder: a role row wears its role's icon and has no level; <paramref name="Next"/> is null once finished.</summary>
    /// <param name="Count">The tally ("4/7"), shown on hover only.</param>
    /// <param name="Left">How many are still to do ("3 left"), empty once none is.</param>
    private sealed record LadderRow(uint IconId, string Name, string Level, bool IsRole, float Fraction, string Count, string Left, QuestRecord? Next, string NextText, bool Ready, IReadOnlyList<uint> RowIds);

    /// <param name="Count">The tally ("4 of 7"), shown on hover only.</param>
    /// <param name="Left">How many are still to do ("3 left"), empty once none is.</param>
    private sealed record ChainRow(string Name, float Fraction, string Count, string Left, QuestRecord? Next, string NextText, IReadOnlyList<uint> RowIds)
    {
        /// <summary>A quest naming the chain for "Read the story so far" in the row's menu; 0 while nothing of it is done.</summary>
        public uint RecapQuest { get; init; }
    }

    // ---- Compare with (V2-12) ----

    private readonly record struct CompareKey(int Version, ulong Viewed, ulong Other, DateTime OtherTaken);

    private sealed record CompareCandidate(ulong ContentId, string Label, string Detail);

    private sealed record DiffSectionRow(string Name, string OnlyViewed, string OnlyOther);

    /// <summary>One quest of a diff list; <paramref name="State"/> and its tooltip belong to the character that lacks the quest.</summary>
    private sealed record DiffRow(QuestRecord Quest, string Name, QuestState State, string StateTooltip, string Value, string Reason);

    /// <summary>One side of the diff: the capped rows, the "and N more" line (empty when none) and the full list as clipboard text.</summary>
    private sealed record DiffList(string Header, DiffRow[] Rows, string More, string Clipboard, string CopiedToast)
    {
        public static readonly DiffList Empty = new(string.Empty, [], string.Empty, string.Empty, string.Empty);
    }

    private sealed record Compare(
        CatalogBundle Bundle,
        UniqueRewardCatalog Rewards,
        ulong OtherContentId,
        string OtherLabel,
        CompareCandidate[] Candidates,
        bool OtherUnreadable,
        string Summary,
        string CountsLine,
        string ViewedHeader,
        string OtherHeader,
        DiffSectionRow[] Sections,
        DiffList OnlyViewed,
        DiffList OnlyOther,
        bool Comparing = false);

    private sealed record Dashboard(
        CharacterSnapshot Snapshot,
        CatalogBundle? Bundle,
        string Name,
        string World,
        string TakenLine,
        string CountsLine,
        string MsqLine,
        QuestRecord? MsqQuest,
        SectionRow[] Sections,
        LadderRow[] JobQuests,
        ChainRow[] Chains,
        ChainRow[] ChainsNotStarted,
        string ChainsNotStartedLabel,
        MoonlitRow[] Moonlit,
        PinnedRow[] Pinned,
        RecentRow[] Recent,
        JobRow[] Jobs,
        string GrandCompanyLine,
        (byte Id, string Tribe, string Rank, string Value)[] Tribes,
        string AllowancesLine,
        uint JobIconId);
}
