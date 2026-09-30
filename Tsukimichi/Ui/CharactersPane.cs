using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Diff;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Unique;
using Tsukimichi.Game;
using Tsukimichi.GameData;

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

    // Motion keys of the dashboard's halos (T17): one table per block in the high bits of the id, the row index in the
    // low; a halo's fill eases only when its fraction changes (another character viewed, a quest completed).
    private const uint DashboardGaugeTag = 0x4441_5347; // "DASG"
    private const uint SectionsGauges = 0x1_0000;
    private const uint JobGauges = 0x2_0000;
    private const uint ChainGauges = 0x3_0000;
    private const uint NotStartedGauges = 0x4_0000;
    private const uint MoonlitGauges = 0x5_0000;

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

    private Dictionary<uint, string>? worldNames;
    private readonly Dictionary<uint, string> zoneNames = [];

    private CharacterItem[] items = [];
    private int itemsVersion = -1;
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

    // Compare with (V2-12): the chosen other character (null follows the most recent capture), the other characters'
    // offline evaluations memoized per capture time, bundle and the server festivals they were resolved with (Live
    // says those were the live character's flags, which change under a stored character), and the view model with its key.
    private ulong? compareTarget;
    private Compare? compare;
    private CompareKey compareKey;
    private readonly Dictionary<ulong, (DateTime Taken, CatalogBundle Bundle, ServerFestivals? Live, IReadOnlyDictionary<uint, QuestEvaluation>? States)> compareStates = [];
    private UniqueRewardCatalog? fallbackRewards;

    private string? toast;
    private DateTime toastUntilUtc;
    private string forgetQuestion = string.Empty;
    private ulong forgetTarget;

    /// <param name="loadSnapshot">Loads a stored character by content id (e.g. <c>SnapshotService.Load</c>); null when unreadable.</param>
    /// <param name="data">Optional; resolves world names from the World sheet. Without it the world id is shown.</param>
    /// <param name="textures">Optional; draws the game's job icons next to job levels. Without it the rows are text only.</param>
    public CharactersPane(
        SessionState session,
        PluginPaths paths,
        IPluginLog log,
        Func<ulong, CharacterSnapshot?> loadSnapshot,
        IDataManager? data = null,
        ITextureProvider? textures = null)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.loadSnapshot = loadSnapshot ?? throw new ArgumentNullException(nameof(loadSnapshot));
        this.data = data;
        this.textures = textures;
    }

    /// <summary>
    /// Obtained/total/unknown per reward kind for the viewed character, normally <c>MoonlitPane.CountsFor</c>, so the
    /// summary agrees with the Moonlit pane. Null hides the Moonlit section.
    /// </summary>
    public Func<RewardKind, UniqueRewardCounts>? MoonlitCounts { get; set; }

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

    /// <summary>Left column: stored characters, newest capture first; selecting one views it.</summary>
    public void DrawLeft(UiState ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        using var id = ImRaii.PushId("charactersLeft");
        RefreshItems();

        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        if (items.Length == 0)
        {
            ImGui.TextWrapped(Strings.CharactersNoneStored);
            ui.RecordSpan(UiRects.CharactersList, start, width);
            return;
        }

        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            using var itemId = ImRaii.PushId(i);
            var selected = session.ViewedContentId == item.ContentId;
            if (ImGui.Selectable(item.Label, selected))
            {
                if (session.ViewCharacter(item.ContentId))
                {
                    ui.MarkQueryDirty();
                }
                else
                {
                    log.Warning("Character {ContentId} could not be viewed; its snapshot is unreadable", item.ContentId);
                }
            }

            using (ImRaii.PushIndent())
            {
                ImGui.TextDisabled(item.Detail);
            }
        }

        ui.RecordSpan(UiRects.CharactersList, start, width);
    }

    /// <summary>Center column: the viewed character's dashboard, its actions and the account view for the selected quest.</summary>
    public void DrawMain(UiState ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        using var id = ImRaii.PushId("charactersMain");

        // The dashboard fills the centre column; the column is its own child window.
        ui.RecordWindow(UiRects.CharactersDashboard);
        var snapshot = session.ViewedSnapshot;
        if (snapshot is null)
        {
            ImGui.TextWrapped(Strings.CharactersNoneViewed);
            return;
        }

        var d = RefreshDashboard(snapshot);

        // (a) Header.
        ImGui.TextUnformatted(d.Name);
        ImGui.SameLine();
        ImGui.TextDisabled(d.World);
        ImGui.TextDisabled(d.TakenLine);
        DrawWelcomeBackButton(snapshot);
        ImGui.TextUnformatted(d.CountsLine);
        DrawMsqLine(ui, d);

        DrawPayoffGates(ui);
        Gap();
        DrawSections(d);
        Gap();
        DrawJobQuests(ui, d);
        Gap();
        DrawChains(ui, d);
        Gap();
        DrawCompare(ui, d);
        Gap();
        DrawMoonlitSummary(d);
        Gap();
        DrawPinned(ui, d);
        Gap();
        DrawAbandoned(ui);
        Gap();
        DrawSeasonal(ui);
        Gap();
        DrawRecent(d);
        Gap();
        DrawJobs(ui, d);
        ImGui.Spacing();
        DrawGrandCompanyAndTribes(d);
        Gap();
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

        ImGui.TextUnformatted(d.MsqLine);
        if (d.MsqQuest is { } msqQuest && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.MsqClickHint);
            if (ImGui.IsItemClicked())
            {
                ui.Reveal(msqQuest);
            }
        }
    }

    private static void Gap()
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    /// <summary>(b) One row per journal section: filling moon, done/total and percent; All quests on top.</summary>
    private static void DrawSections(Dashboard d)
    {
        ImGui.TextDisabled(Strings.CharactersSectionCompletion);
        if (d.Sections.Length == 0)
        {
            ImGui.TextDisabled(Strings.CharactersNoSections);
            return;
        }

        using var table = ImRaii.Table("##sections", 4, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        var line = ImGui.GetTextLineHeight();
        ImGui.TableSetupColumn("##moon", ImGuiTableColumnFlags.WidthFixed, line * 1.4f);
        ImGui.TableSetupColumn(Strings.CharactersColumnSection, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(300f));
        ImGui.TableSetupColumn(Strings.CharactersColumnDone, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(90f));
        ImGui.TableSetupColumn(Strings.CharactersColumnPercent, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(50f));
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
            if (row.Overall)
            {
                // A heading, not a call to action: Silver, not gold (game UX panel finding 2).
                using (Theme.PushText(Theme.Silver))
                {
                    ImGui.TextUnformatted(row.Name);
                }
            }
            else
            {
                ImGui.TextUnformatted(row.Name);
            }

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Count);
            ImGui.TableNextColumn();
            ImGui.TextDisabled(row.Percent);
        }
    }

    /// <summary>
    /// Job quests (V2-11): one row per leveled job with its icon, level, a filling moon over its ladder and the next
    /// quest ("Lv N" in Moon when it can be taken now, its decisive blocker in Dusk otherwise), then one row per role the
    /// character has a job in. The next quest's name reveals it in the Journal.
    /// </summary>
    private void DrawJobQuests(UiState ui, Dashboard d)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(Strings.JobsSection);
        ImGui.SameLine();
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
        ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, line * 1.4f);
        ImGui.TableSetupColumn(Strings.JobsColumnJob, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(200f));
        ImGui.TableSetupColumn(Strings.JobsColumnLevel, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(50f));
        ImGui.TableSetupColumn("##moon", ImGuiTableColumnFlags.WidthFixed, line * 1.4f);
        ImGui.TableSetupColumn(Strings.JobsColumnDone, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(60f));
        ImGui.TableSetupColumn(Strings.JobsColumnNext, ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();

        for (var i = 0; i < d.JobQuests.Length; i++)
        {
            var row = d.JobQuests[i];
            using var rowId = ImRaii.PushId(i);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            DrawJobIcon(row.IconId, iconSize, row.Name, row.Level);
            ImGui.TableNextColumn();
            if (row.IsRole)
            {
                using (Theme.PushText(Theme.Dusk))
                {
                    ImGui.TextUnformatted(row.Name);
                }
            }
            else
            {
                ImGui.TextUnformatted(row.Name);
            }

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Level);
            ImGui.TableNextColumn();
            MoonGlyph.DrawHaloInline(Motion.Key(DashboardGaugeTag, JobGauges | (uint)i), row.Fraction, UiMetrics.InlineGlyphSize(line));
            if (ImGui.IsItemHovered())
            {
                FillingMoonTooltip(row.Count);
            }

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Count);
            ImGui.TableNextColumn();
            DrawNextQuest(ui, row.Next, row.NextText, row.Ready);
        }
    }

    /// <summary>
    /// Story chains (V2-09 on the dashboard): every curated chain with a filling moon, N of M and the next quest;
    /// chains with nothing done yet fold under "Not started (N)" so the list stays short.
    /// </summary>
    private void DrawChains(UiState ui, Dashboard d)
    {
        ImGui.TextDisabled(Strings.JobsChainsSection);
        if (d.Chains.Length == 0 && d.ChainsNotStarted.Length == 0)
        {
            ImGui.TextDisabled(Strings.JobsChainsNone);
            return;
        }

        DrawChainTable(ui, "##chains", d.Chains, ChainGauges);
        if (d.ChainsNotStarted.Length == 0)
        {
            return;
        }

        using var node = ImRaii.TreeNode(d.ChainsNotStartedLabel);
        if (node)
        {
            DrawChainTable(ui, "##chainsNotStarted", d.ChainsNotStarted, NotStartedGauges);
        }
    }

    private static void DrawChainTable(UiState ui, string id, ChainRow[] rows, uint gaugeKeys)
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

        var line = ImGui.GetTextLineHeight();
        ImGui.TableSetupColumn("##moon", ImGuiTableColumnFlags.WidthFixed, line * 1.4f);
        ImGui.TableSetupColumn(Strings.JobsColumnChain, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(220f));
        ImGui.TableSetupColumn(Strings.JobsColumnDone, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(80f));
        ImGui.TableSetupColumn(Strings.JobsColumnNext, ImGuiTableColumnFlags.WidthStretch);

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
            ImGui.TextUnformatted(row.Name);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Count);
            ImGui.TableNextColumn();
            DrawNextQuest(ui, row.Next, row.NextText, ready: true);
        }
    }

    /// <summary>A clickable "next" cell: the text in Moon when the quest is open now, Dusk otherwise; null quest means finished.</summary>
    private static void DrawNextQuest(UiState ui, QuestRecord? next, string text, bool ready)
    {
        if (next is null)
        {
            ImGui.TextDisabled(text);
            return;
        }

        using (Theme.PushText(ready ? Theme.Moon : Theme.Dusk))
        {
            if (ImGui.Selectable(text))
            {
                Reveal(ui, next);
            }
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.MoonlitShowInJournal);
        }
    }

    /// <summary>(c) Obtained/total for the collectible reward kinds, with the Moonlit pane's own counts.</summary>
    private void DrawMoonlitSummary(Dashboard d)
    {
        ImGui.TextDisabled(Strings.CharactersMoonlitSummary);
        if (d.Moonlit.Length == 0)
        {
            ImGui.TextDisabled(Strings.CharactersMoonlitUnavailable);
            return;
        }

        if (!session.IsLive)
        {
            using (Theme.PushText(Theme.Dusk))
            {
                ImGui.TextUnformatted(Strings.MoonlitOfflineHint);
            }
        }

        using var table = ImRaii.Table("##moonlitSummary", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        var line = ImGui.GetTextLineHeight();
        ImGui.TableSetupColumn("##moon", ImGuiTableColumnFlags.WidthFixed, line * 1.4f);
        ImGui.TableSetupColumn(Strings.CharactersColumnKind, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(300f));
        ImGui.TableSetupColumn(Strings.CharactersColumnObtained, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(90f));

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
            ImGui.TextUnformatted(row.Name);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Count);
        }
    }

    /// <summary>(d) The viewed character's pins with glyph, name and next step; clicking reveals the quest in the Journal.</summary>
    private static void DrawPinned(UiState ui, Dashboard d)
    {
        ImGui.TextDisabled(Strings.CharactersPinned);
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
        ImGui.TableSetupColumn("##state", ImGuiTableColumnFlags.WidthFixed, line * 1.4f);
        ImGui.TableSetupColumn(Strings.CharactersColumnQuest, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(300f));
        ImGui.TableSetupColumn(Strings.CharactersColumnStatus, ImGuiTableColumnFlags.WidthStretch);

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
                if (ImGui.Selectable(row.Name))
                {
                    Reveal(ui, quest);
                }

                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(Strings.MoonlitShowInJournal);
                }
            }
            else
            {
                ImGui.TextDisabled(row.Name);
            }

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.NextStep);
        }
    }

    /// <summary>(e) The last few quest events of the live character: kind, quest and local time.</summary>
    private void DrawRecent(Dashboard d)
    {
        ImGui.TextDisabled(Strings.CharactersRecent);
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

        ImGui.TableSetupColumn(Strings.CharactersColumnTime, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(50f));
        ImGui.TableSetupColumn(Strings.CharactersColumnEvent, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(110f));
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
            ImGui.TextUnformatted(row.Quest);
        }
    }

    /// <summary>
    /// (f) Job levels grouped by role, each with the game's job icon; base classes hidden once their job is unlocked.
    /// A right-click on a class offers the unlock route to the jobs it grows into (P6).
    /// </summary>
    private void DrawJobs(UiState ui, Dashboard d)
    {
        ImGui.TextDisabled(Strings.CharactersJobs);
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
        ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, line * 1.4f);
        ImGui.TableSetupColumn(Strings.CharactersColumnJob, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(220f));
        ImGui.TableSetupColumn(Strings.CharactersColumnLevel, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(60f));
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
                ImGui.TableNextColumn();
                using (Theme.PushText(Theme.Dusk))
                {
                    ImGui.TextUnformatted(Strings.CharactersJobGroupName(group));
                }
            }

            ImGui.TableNextRow();
            using var rowId = ImRaii.PushId((int)row.JobId);
            ImGui.TableNextColumn();
            DrawJobIcon(row.IconId, iconSize, row.Name, row.Level);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Name);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(row.Abbreviation);
            }

            DrawJobRowRouteMenu(ui, d, row.JobId);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Level);
        }
    }

    /// <summary>The job's icon (a blank of the same size without one), naming the job and its level on hover.</summary>
    private void DrawJobIcon(uint iconId, Vector2 size, string name, string level)
    {
        if (textures is null || iconId == 0)
        {
            ImGui.Dummy(size);
        }
        else
        {
            var wrap = textures.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrEmpty();
            ImGui.Image(wrap.Handle, size);
        }

        if (ImGui.IsItemHovered())
        {
            JobTooltip(name, level);
        }
    }

    /// <summary>Job icon tooltip: the job's name, then "Level N" under it; a role row has no level and shows the name alone.</summary>
    private static void JobTooltip(string name, string level)
    {
        using var tooltipStyle = Theme.PushTooltip();
        using var tooltip = ImRaii.Tooltip();
        UiMetrics.ApplyFontScale();
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
        using var tooltipStyle = Theme.PushTooltip();
        using var tooltip = ImRaii.Tooltip();
        UiMetrics.ApplyFontScale();
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

    private static void DrawGrandCompanyAndTribes(Dashboard d)
    {
        ImGui.TextDisabled(Strings.CharactersGrandCompany);
        ImGui.TextUnformatted(d.GrandCompanyLine);
        ImGui.Spacing();

        ImGui.TextDisabled(Strings.CharactersTribes);
        ImGui.TextUnformatted(d.AllowancesLine);
        if (d.Tribes.Length == 0)
        {
            ImGui.TextDisabled(Strings.CharactersNoTribes);
            return;
        }

        using var table = ImRaii.Table("##tribes", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        ImGui.TableSetupColumn(Strings.CharactersColumnTribe, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(220f));
        ImGui.TableSetupColumn(Strings.CharactersColumnRank, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(120f));
        ImGui.TableSetupColumn(Strings.CharactersColumnReputation, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(90f));
        ImGui.TableHeadersRow();
        foreach (var (tribe, rank, value) in d.Tribes)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(tribe);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(rank);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(value);
        }
    }

    /// <summary>(g) Export and Forget.</summary>
    private void DrawActions(CharacterSnapshot snapshot)
    {
        if (ImGui.Button(Strings.CharactersExport))
        {
            Export(snapshot);
        }

        ImGui.SameLine();
        var live = session.IsLive;
        using (ImRaii.Disabled(live))
        using (Theme.PushDestructiveButton())
        {
            if (ImGui.Button(Strings.CharactersForget))
            {
                forgetTarget = snapshot.ContentId;
                forgetQuestion = Strings.CharactersForgetQuestionPrefix + snapshot.Name + Strings.CharactersForgetQuestionSuffix;
                ImGui.OpenPopup(Strings.CharactersForgetPopup);
            }
        }

        if (live && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.CharactersForgetLiveHint);
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
            if (ImGui.Button(Strings.CharactersForgetConfirm))
            {
                session.ForgetCharacter(forgetTarget);
                snapshotCache.Remove(forgetTarget);
                compareStates.Remove(forgetTarget);
                if (compareTarget == forgetTarget)
                {
                    compareTarget = null;
                }

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

        using (Theme.PushText(Theme.Silver))
        {
            ImGui.TextWrapped(toast);
        }
    }

    /// <summary>
    /// Compare with (V2-12): a combo of the other stored characters (the most recently captured one at first), the lead
    /// line and per-section counts, then the quests done on the viewed character and not on the other and the reverse,
    /// each ranked by unlock value, capped at <see cref="MaxDiffRows"/> rows and copyable as text. Hidden behind a hint
    /// until a second character is stored.
    /// </summary>
    private void DrawCompare(UiState ui, Dashboard d)
    {
        ImGui.TextDisabled(Strings.DiffSection);
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

        ImGui.TextUnformatted(c.Summary);
        ImGui.TextDisabled(c.CountsLine);
        DrawCompareSections(c);
        ImGui.Spacing();
        DrawDiffList(ui, "onlyViewed", c.OnlyViewed);
        ImGui.Spacing();
        DrawDiffList(ui, "onlyOther", c.OnlyOther);
    }

    private void DrawCompareCombo(Compare c)
    {
        ImGui.SetNextItemWidth(UiMetrics.CharacterComboWidth);
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
            if (ImGui.Selectable(candidate.Label, candidate.ContentId == c.OtherContentId))
            {
                compareTarget = candidate.ContentId;
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

        ImGui.TableSetupColumn(Strings.DiffColumnSection, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(300f));
        ImGui.TableSetupColumn(c.ViewedHeader, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(130f));
        ImGui.TableSetupColumn(c.OtherHeader, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(130f));
        ImGui.TableHeadersRow();

        foreach (var row in c.Sections)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Name);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.OnlyViewed);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.OnlyOther);
        }
    }

    /// <summary>One side of the diff: header with count and Copy list, the capped table, then "and N more".</summary>
    private void DrawDiffList(UiState ui, string id, DiffList list)
    {
        using var listId = ImRaii.PushId(id);
        ImGui.TextUnformatted(list.Header);
        if (list.Rows.Length == 0)
        {
            ImGui.TextDisabled(Strings.DiffNone);
            return;
        }

        ImGui.SameLine();
        if (ImGui.SmallButton(Strings.DiffCopyList))
        {
            ImGui.SetClipboardText(list.Clipboard);
            ShowToast(list.CopiedToast);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.DiffCopyListTooltip);
        }

        DrawDiffTable(ui, list.Rows);
        if (list.More.Length > 0)
        {
            ImGui.TextDisabled(list.More);
        }
    }

    /// <summary>Rows: the lacking character's state moon, the quest (click reveals it), the value badge and the reason in Dusk.</summary>
    private static void DrawDiffTable(UiState ui, DiffRow[] rows)
    {
        using var table = ImRaii.Table("##diff", 4, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        var line = ImGui.GetTextLineHeight();
        ImGui.TableSetupColumn("##state", ImGuiTableColumnFlags.WidthFixed, line * 1.4f);
        ImGui.TableSetupColumn(Strings.DiffColumnQuest, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(300f));
        ImGui.TableSetupColumn(Strings.DiffColumnValue, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(60f));
        ImGui.TableSetupColumn(Strings.DiffColumnWhy, ImGuiTableColumnFlags.WidthStretch);
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
            if (ImGui.Selectable(row.Name))
            {
                Reveal(ui, row.Quest);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.MoonlitShowInJournal);
            }

            ImGui.TableNextColumn();
            DrawValueBadge(row.Value);
            ImGui.TableNextColumn();
            using (Theme.PushText(Theme.Dusk))
            {
                ImGui.TextUnformatted(row.Reason);
            }
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
        using (Theme.PushText(Theme.Silver))
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
    /// capture time, or the bundle changes. The other character defaults to the most recently captured one that is not
    /// the viewed one; a chosen character that was forgotten falls back to that default.
    /// </summary>
    private Compare? RefreshCompare(Dashboard d)
    {
        var viewedId = d.Snapshot.ContentId;
        CharacterItem? other = null;
        CharacterItem? newest = null;
        foreach (var item in items)
        {
            if (item.ContentId == viewedId)
            {
                continue;
            }

            newest ??= item;
            if (item.ContentId == compareTarget)
            {
                other = item;
                break;
            }
        }

        other ??= newest;
        if (other is null)
        {
            return null;
        }

        var key = new CompareKey(session.Version, viewedId, other.ContentId, other.TakenUtc);
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
            var name = s.SectionName.Length == 0 ? Strings.CharactersSectionPrefix + s.SectionId.ToString(CultureInfo.InvariantCulture) : s.SectionName;
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

        // Festivals run server-wide: while someone is logged in the live flags decide for every stored character, so a
        // change of them (a login, a logout, an event starting) resolves the other character again.
        var live = session.LiveSnapshot is { } liveSnapshot ? ServerFestivals.Of(liveSnapshot) : null;
        if (compareStates.TryGetValue(item.ContentId, out var cached) && cached.Taken == item.TakenUtc && ReferenceEquals(cached.Bundle, bundle)
            && (live is null ? cached.Live is null : live.SameAs(cached.Live)))
        {
            return cached.States;
        }

        IReadOnlyDictionary<uint, QuestEvaluation>? states = null;
        if (SnapshotFor(item) is { } snapshot)
        {
            try
            {
                states = StateResolver.ResolveAll(bundle.Catalog, snapshot, ContextFor(snapshot));
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Character {ContentId} could not be evaluated for the comparison", item.ContentId);
            }
        }

        compareStates[item.ContentId] = (item.TakenUtc, bundle, live, states);
        return states;
    }

    /// <summary>
    /// The context another stored character is resolved with: the session's, with the festivals running on the server
    /// for that character (<see cref="ServerFestivals.For"/>: the live flags while someone is logged in, else its own
    /// less the stale ones) instead of the viewed character's.
    /// </summary>
    private EvalContext ContextFor(CharacterSnapshot snapshot) =>
        session.Context with { ServerFestivals = ServerFestivals.For(snapshot, session.LiveSnapshot, session.Curated.Festivals, DateTime.UtcNow) };

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
        ImGui.TextDisabled(Strings.CharactersAccountView);
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

        if (accountRowId != rowId || accountVersion != session.Version)
        {
            accountCache.Clear();
            accountRowId = rowId;
            accountVersion = session.Version;
        }

        ImGui.TextUnformatted(session.Spoilers.DisplayName(quest));
        RefreshItems();

        using var table = ImRaii.Table("##account", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        var line = ImGui.GetTextLineHeight();
        ImGui.TableSetupColumn(Strings.CharactersColumnCharacter, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(200f));
        ImGui.TableSetupColumn(Strings.CharactersColumnState, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(170f));
        ImGui.TableSetupColumn(Strings.CharactersColumnStatus, ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();

        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var (evaluation, reason) = EvaluateFor(item, quest, bundle);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(item.Name);

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
                using (Theme.PushText(Theme.StateColor(evaluation.State)))
                {
                    ImGui.TextUnformatted(Strings.StateName(evaluation.State, quest));
                }
            }

            ImGui.TableNextColumn();
            if (reason.Length > 0)
            {
                // The state column beside it already names the state; this is the reason alone (blocker or step).
                ImGui.TextUnformatted(reason);
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
            var text = File.Exists(source) ? File.ReadAllText(source) : JsonSerializer.Serialize(snapshot, ExportJson);
            File.WriteAllText(file, text);

            log.Information("Exported {Name} to {Path}", snapshot.Name, file);
            ShowToast(Strings.CharactersExportedPrefix + file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            log.Error(ex, "Export of {Name} failed", snapshot.Name);
            ShowToast(Strings.CharactersExportFailedPrefix + ex.Message);
        }
    }

    private void ShowToast(string text)
    {
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

    /// <summary>Left-column labels, once per session version and once a minute (the ages tick).</summary>
    private void RefreshItems()
    {
        var minute = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute;
        if (itemsVersion == session.Version && itemsMinute == minute)
        {
            return;
        }

        itemsVersion = session.Version;
        itemsMinute = minute;

        var characters = session.Characters;
        var built = new CharacterItem[characters.Count];
        for (var i = 0; i < built.Length; i++)
        {
            var c = characters[i];
            var live = c.ContentId == session.LiveContentId;
            var label = (live ? Strings.CharactersLiveMarker : string.Empty) + c.Name;
            var detailText = WorldName(c.World) + " · " + (live ? Strings.CharactersLive : Age(c.TakenUtc)) + " · "
                             + c.CompletedCount.ToString(CultureInfo.InvariantCulture) + Strings.CharactersCompletedSuffix;
            built[i] = new CharacterItem(c.ContentId, c.Name, c.TakenUtc, label, detailText);
        }

        items = built;
    }

    /// <summary>
    /// The dashboard view model, rebuilt only when the session version, the viewed character, the catalog bundle or
    /// the live flag changes (and once a minute so the snapshot age ticks).
    /// </summary>
    private Dashboard RefreshDashboard(CharacterSnapshot snapshot)
    {
        var bundle = session.Bundle;
        var minute = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute;
        var key = new DashboardKey(session.Version, snapshot.ContentId, session.IsLive, minute, MoonlitCounts is not null, Pins?.PinsVersion ?? -1);
        if (dashboard is { } current && key == dashboardKey && ReferenceEquals(current.Snapshot, snapshot) && ReferenceEquals(current.Bundle, bundle))
        {
            return current;
        }

        dashboardKey = key;
        dashboard = BuildDashboard(snapshot, bundle);
        return dashboard;
    }

    private Dashboard BuildDashboard(CharacterSnapshot snapshot, CatalogBundle? bundle)
    {
        var names = bundle?.Names;
        var taken = snapshot.TakenUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var takenLine = session.IsLive
            ? Strings.CharactersLive + " · " + taken
            : Strings.CharactersSnapshotPrefix + taken + " (" + Age(snapshot.TakenUtc) + ")";

        var completed = 0;
        foreach (var b in snapshot.CompletedBits)
        {
            completed += System.Numerics.BitOperations.PopCount(b);
        }

        var currentJob = JobName(names, snapshot.CurrentJob);
        var countsLine = completed.ToString(CultureInfo.InvariantCulture) + Strings.CharactersCompletedSuffix + " · "
                         + snapshot.Accepted.Count.ToString(CultureInfo.InvariantCulture) + Strings.CharactersAcceptedSuffix + " · " + currentJob;

        string gcLine;
        if (snapshot.GrandCompany == 0)
        {
            gcLine = Strings.CharactersNoGrandCompany;
        }
        else
        {
            var gcName = names?.GrandCompany(snapshot.GrandCompany) is { Length: > 0 } n ? n : GrandCompanies.Name(snapshot.GrandCompany);
            var rank = snapshot.GrandCompany < snapshot.GcRanks.Length ? snapshot.GcRanks[snapshot.GrandCompany] : (byte)0;
            gcLine = gcName + " · " + Strings.CharactersRankPrefix + rank.ToString(CultureInfo.InvariantCulture);
        }

        var tribes = new List<(byte Id, TribeStanding Standing)>(snapshot.Tribes.Count);
        foreach (var (tribe, standing) in snapshot.Tribes)
        {
            tribes.Add((tribe, standing));
        }

        tribes.Sort((a, b) => a.Id.CompareTo(b.Id));
        var tribeRows = new (string Tribe, string Rank, string Value)[tribes.Count];
        for (var i = 0; i < tribeRows.Length; i++)
        {
            var (id, standing) = tribes[i];
            var tribeName = names?.Tribe(id) is { Length: > 0 } t ? t : Strings.CharactersTribePrefix + id.ToString(CultureInfo.InvariantCulture);
            var rankName = names?.TribeRank(standing.Rank) is { Length: > 0 } r ? r : TribeRanks.Name(standing.Rank);
            tribeRows[i] = (tribeName, rankName, standing.Value.ToString(CultureInfo.InvariantCulture));
        }

        var allowances = Strings.CharactersAllowancesPrefix + snapshot.TribeAllowance.ToString(CultureInfo.InvariantCulture) + Strings.CharactersTribeAllowanceSuffix
                         + snapshot.LeveAllowance.ToString(CultureInfo.InvariantCulture) + Strings.CharactersLeveAllowanceSuffix;

        var (msqLine, msqQuest) = BuildMsq(bundle);
        RefreshDerived(bundle);
        var jobs = BuildJobs(snapshot, bundle);
        var (chainRows, notStarted) = BuildChains(bundle);
        var notStartedLabel = notStarted.Length == 0
            ? string.Empty
            : string.Format(CultureInfo.CurrentCulture, Strings.JobsChainsNotStartedFormat, notStarted.Length);

        return new Dashboard(
            snapshot,
            bundle,
            snapshot.Name,
            WorldName(snapshot.World),
            takenLine,
            countsLine,
            msqLine,
            msqQuest,
            BuildSections(bundle),
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
            allowances);
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

            rows.Add(LadderRowFor(job.IconId, job.Name, job.Level, isRole: false, ladder.Progress(entry, states, job.LevelValue)));
        }

        foreach (var (role, level) in roleLevels)
        {
            var quests = ladder.RoleLadder(role);
            if (quests.Count == 0)
            {
                continue;
            }

            var name = string.Format(CultureInfo.CurrentCulture, Strings.JobsRoleRowFormat, Strings.JobsRoleName(role));
            rows.Add(LadderRowFor(0, name, string.Empty, isRole: true, ladder.Progress(quests, states, level)));
        }

        return rows.ToArray();
    }

    private LadderRow LadderRowFor(uint iconId, string name, string level, bool isRole, LadderProgress progress)
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

        return new LadderRow(iconId, name, level, isRole, progress.Fraction, count, next, text, progress.IsReadyNow);
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
            var next = progress.NextRowId is { } nextRowId ? bundle.Catalog.GetByRowId(nextRowId) : null;
            var row = new ChainRow(
                chain.Name,
                progress.Fraction,
                string.Format(CultureInfo.CurrentCulture, Strings.JobsChainCountFormat, progress.Done, progress.Total),
                next,
                next is null ? Strings.JobsChainComplete : Strings.JobsChainNextPrefix + session.Spoilers.DisplayName(next));
            (progress.Done > 0 ? started : notStarted).Add(row);
        }

        return (started.ToArray(), notStarted.ToArray());
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

    /// <summary>All quests first, then every journal section in journal order, from the viewed character's evaluations.</summary>
    private SectionRow[] BuildSections(CatalogBundle? bundle)
    {
        if (bundle is null || session.States.Count == 0)
        {
            return [];
        }

        TreeCounts counts;
        try
        {
            counts = TreeCounts.Compute(bundle.Catalog, session.States, includeUnlisted: false);
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
            sections.Add((id, name.Length == 0 ? Strings.CharactersSectionPrefix + id.ToString(CultureInfo.InvariantCulture) : name));
        }

        sections.Sort((a, b) => a.Id.CompareTo(b.Id));
        var rows = new SectionRow[sections.Count + 1];
        rows[0] = SectionRow.From(Strings.CharactersAllQuests, counts.Overall, overall: true);
        for (var i = 0; i < sections.Count; i++)
        {
            rows[i + 1] = SectionRow.From(sections[i].Name, counts.Section(sections[i].Id), overall: false);
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
                quest is null ? Strings.MoonlitQuestPrefix + rowId.ToString(CultureInfo.InvariantCulture) : session.Spoilers.DisplayName(quest),
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
                quest is null ? Strings.MoonlitQuestPrefix + ev.RowId.ToString(CultureInfo.InvariantCulture) : session.Spoilers.DisplayName(quest));
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
        QuestEventKind.Completed => Theme.MoonDim,
        QuestEventKind.Accepted => Theme.Silver,
        QuestEventKind.NewlyAvailable => Theme.Moon,
        QuestEventKind.Abandoned => Theme.Eclipse,
        _ => Theme.Dusk,
    };

    private static string JobName(GameNames? names, byte job)
    {
        if (job == 0)
        {
            return Strings.CharactersJobPrefix + "0";
        }

        var name = names?.ClassJob(job);
        return string.IsNullOrEmpty(name) ? Strings.CharactersJobPrefix + job.ToString(CultureInfo.InvariantCulture) : DisplayName(name);
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

        return worldNames.TryGetValue(world, out var known) ? known : Strings.CharactersWorldPrefix + world.ToString(CultureInfo.InvariantCulture);
    }

    private static string Age(DateTime takenUtc)
    {
        var age = DateTime.UtcNow - takenUtc;
        if (age < TimeSpan.FromMinutes(1))
        {
            return Strings.CharactersAgeJustNow;
        }

        if (age < TimeSpan.FromHours(1))
        {
            return ((int)age.TotalMinutes).ToString(CultureInfo.InvariantCulture) + Strings.CharactersAgeMinutesSuffix;
        }

        if (age < TimeSpan.FromDays(2))
        {
            return ((int)age.TotalHours).ToString(CultureInfo.InvariantCulture) + Strings.CharactersAgeHoursSuffix;
        }

        return ((int)age.TotalDays).ToString(CultureInfo.InvariantCulture) + Strings.CharactersAgeDaysSuffix;
    }

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

    private sealed record CharacterItem(ulong ContentId, string Name, DateTime TakenUtc, string Label, string Detail);

    private readonly record struct DashboardKey(int Version, ulong ContentId, bool Live, long Minute, bool HasMoonlit, int PinsVersion);

    private sealed record SectionRow(string Name, string Count, string Percent, float Fraction, bool Overall)
    {
        public static SectionRow From(string name, NodeCount count, bool overall)
        {
            var percent = count.Total == 0 ? 0 : (int)MathF.Round(100f * count.Done / count.Total);
            return new SectionRow(
                name,
                count.Done.ToString(CultureInfo.InvariantCulture) + "/" + count.Total.ToString(CultureInfo.InvariantCulture),
                percent.ToString(CultureInfo.InvariantCulture) + "%",
                count.Fraction,
                overall);
        }
    }

    private sealed record MoonlitRow(string Name, string Count, float Fraction, bool AllUnknown);

    private sealed record PinnedRow(QuestRecord? Quest, string Name, QuestState State, string NextStep);

    private sealed record RecentRow(string Time, string Kind, Vector4 Color, string Quest);

    private sealed record JobRow(JobGroup Group, uint JobId, uint IconId, string Name, string Abbreviation, string Level, short LevelValue);

    /// <summary>A job's (or a role's) ladder: icon and level are empty for role rows; <paramref name="Next"/> is null once finished.</summary>
    private sealed record LadderRow(uint IconId, string Name, string Level, bool IsRole, float Fraction, string Count, QuestRecord? Next, string NextText, bool Ready);

    private sealed record ChainRow(string Name, float Fraction, string Count, QuestRecord? Next, string NextText);

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
        DiffList OnlyOther);

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
        (string Tribe, string Rank, string Value)[] Tribes,
        string AllowancesLine);
}
