using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// Characters (spec §7, F-50..F-61, F-73). <see cref="DrawLeft"/> lists every stored snapshot (the live one marked ●)
/// and switches the viewed character; <see cref="DrawMain"/> is a dashboard for the viewed character, top to bottom:
/// header, completion per journal section (filling moons), the Moonlit summary, pinned quests, recent activity, jobs
/// grouped by role with the game's job icons, Grand Company and allied societies, Export JSON and Forget (with a
/// confirm popup), and the Account view: the state of <see cref="UiState.SelectedRowId"/> on every character,
/// evaluated offline from their snapshots.
/// <para>
/// Every label, count and icon id is built once per <see cref="SessionState.Version"/> or viewed character (and once
/// a minute for the ages); nothing allocates per frame in the table bodies. Snapshots of other characters are loaded
/// through <c>loadSnapshot</c> only when their capture time changed, and their evaluations are memoized per version.
/// </para>
/// </summary>
public sealed class CharactersPane
{
    private const string ExportsFolder = "exports";
    private const int MaxRecentRows = 10;
    private const int MaxPinnedRows = 50;
    private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(8);

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

    // Snapshot is null when the file could not be read at that capture time; the failure is cached too so an
    // unreadable file is not re-read on every session version bump.
    private readonly Dictionary<ulong, (DateTime Taken, CharacterSnapshot? Snapshot)> snapshotCache = [];
    private readonly Dictionary<ulong, QuestEvaluation?> accountCache = [];
    private uint accountRowId;
    private int accountVersion = -1;

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
        ImGui.TextUnformatted(d.CountsLine);
        if (d.MsqLine.Length > 0)
        {
            // Independent of the journal's hide state: the first main scenario quest not yet completed.
            ImGui.TextUnformatted(d.MsqLine);
            if (d.MsqQuest is { } msqQuest && ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.MsqClickHint);
                if (ImGui.IsItemClicked())
                {
                    ui.Reveal(msqQuest.RowId, msqQuest.IsUnlisted ? QuestScope.VirtualUnlisted : QuestScope.Genre(msqQuest.Journal.GenreId), msqQuest.IsUnlisted);
                }
            }
        }

        Gap();
        DrawSections(d);
        Gap();
        DrawMoonlitSummary(d);
        Gap();
        DrawPinned(ui, d);
        Gap();
        DrawRecent(d);
        Gap();
        DrawJobs(d);
        ImGui.Spacing();
        DrawGrandCompanyAndTribes(d);
        Gap();
        DrawActions(snapshot);
        DrawForgetPopup(ui);
        DrawToast();
        Gap();
        DrawAccountView(ui);
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

        foreach (var row in d.Sections)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            MoonGlyph.DrawFillingInline(row.Fraction, UiMetrics.InlineGlyphSize(line));
            ImGui.TableNextColumn();
            if (row.Overall)
            {
                using (Theme.PushText(Theme.Moon))
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

        foreach (var row in d.Moonlit)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            if (row.AllUnknown)
            {
                MoonGlyph.DrawInline(QuestState.Unknown, UiMetrics.InlineGlyphSize(line));
                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(Strings.MoonlitObtainedUnknown);
                }
            }
            else
            {
                MoonGlyph.DrawFillingInline(row.Fraction, UiMetrics.InlineGlyphSize(line));
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
        ImGui.TableSetupColumn(Strings.CharactersColumnNextStep, ImGuiTableColumnFlags.WidthStretch);

        for (var i = 0; i < d.Pinned.Length; i++)
        {
            var row = d.Pinned[i];
            using var rowId = ImRaii.PushId(i);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            MoonGlyph.DrawInline(row.State, UiMetrics.InlineGlyphSize(line));
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.MoonlitStateName(row.State));
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

    /// <summary>(f) Job levels grouped by role, each with the game's job icon; base classes hidden once their job is unlocked.</summary>
    private void DrawJobs(Dashboard d)
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
            ImGui.TableNextColumn();
            DrawJobIcon(row.IconId, iconSize);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Name);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(row.Abbreviation);
            }

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Level);
        }
    }

    private void DrawJobIcon(uint iconId, Vector2 size)
    {
        if (textures is null || iconId == 0)
        {
            ImGui.Dummy(size);
            return;
        }

        var wrap = textures.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrEmpty();
        ImGui.Image(wrap.Handle, size);
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

        using (Theme.PushText(Theme.Moon))
        {
            ImGui.TextWrapped(toast);
        }
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

        ImGui.TextUnformatted(quest.Name);
        RefreshItems();

        using var table = ImRaii.Table("##account", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        var line = ImGui.GetTextLineHeight();
        ImGui.TableSetupColumn(Strings.CharactersColumnCharacter, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(200f));
        ImGui.TableSetupColumn(Strings.CharactersColumnState, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(170f));
        ImGui.TableSetupColumn(Strings.CharactersColumnNextStep, ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();

        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var evaluation = EvaluateFor(item, quest, bundle);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(item.Name);

            ImGui.TableNextColumn();
            if (evaluation is null)
            {
                MoonGlyph.DrawInline(QuestState.Unknown, UiMetrics.InlineGlyphSize(line));
                ImGui.SameLine();
                ImGui.TextDisabled(Strings.CharactersSnapshotUnreadable);
            }
            else
            {
                MoonGlyph.DrawInline(evaluation.State, UiMetrics.InlineGlyphSize(line));
                ImGui.SameLine();
                using (Theme.PushText(Theme.StateColor(evaluation.State)))
                {
                    ImGui.TextUnformatted(Strings.MoonlitStateName(evaluation.State));
                }
            }

            ImGui.TableNextColumn();
            if (evaluation?.NextStep is { } next)
            {
                ImGui.TextUnformatted(next.Detail);
            }
        }
    }

    private static void Reveal(UiState ui, QuestRecord quest) =>
        ui.Reveal(quest.RowId, quest.IsUnlisted ? QuestScope.VirtualUnlisted : QuestScope.Genre(quest.Journal.GenreId), quest.IsUnlisted);

    private QuestEvaluation? EvaluateFor(CharacterItem item, QuestRecord quest, CatalogBundle bundle)
    {
        if (item.ContentId == session.ViewedContentId)
        {
            return session.States.TryGetValue(quest.RowId, out var viewed) ? viewed : null;
        }

        if (accountCache.TryGetValue(item.ContentId, out var cached))
        {
            return cached;
        }

        QuestEvaluation? evaluation = null;
        var snapshot = SnapshotFor(item);
        if (snapshot is not null)
        {
            try
            {
                evaluation = StateResolver.Resolve(quest, snapshot, bundle.Catalog, session.Context);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Could not evaluate quest {RowId} for character {ContentId}", quest.RowId, item.ContentId);
            }
        }

        accountCache[item.ContentId] = evaluation;
        return evaluation;
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
        var key = new DashboardKey(session.Version, snapshot.ContentId, session.IsLive, minute, MoonlitCounts is not null);
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
            BuildMoonlit(),
            BuildPinned(snapshot, bundle),
            BuildRecent(bundle),
            BuildJobs(snapshot, bundle),
            gcLine,
            tribeRows,
            allowances);
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
        if (next.Issuer is not { } issuer)
        {
            return (string.Format(CultureInfo.CurrentCulture, Strings.CharactersMsqNoGiverFormat, expansion, next.Name), next);
        }

        var zone = ZoneName(issuer.MapId);
        var giver = zone.Length > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.MsqGiverFormat, issuer.Name, zone) : issuer.Name;
        return (string.Format(CultureInfo.CurrentCulture, Strings.CharactersMsqFormat, expansion, next.Name, giver), next);
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

    /// <summary>The viewed character's pins from <c>user/pins.json</c>, in the file's order, with the current state and next step.</summary>
    private PinnedRow[] BuildPinned(CharacterSnapshot snapshot, CatalogBundle? bundle)
    {
        var warnings = new List<string>();
        Dictionary<ulong, List<uint>> pins;
        try
        {
            pins = PinsFile.Load(paths.PinsFile, warnings);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Pins could not be read for the dashboard");
            return [];
        }

        foreach (var warning in warnings)
        {
            log.Warning("Pins: {Warning}", warning);
        }

        if (!pins.TryGetValue(snapshot.ContentId, out var list) || list.Count == 0)
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
                quest?.Name ?? Strings.MoonlitQuestPrefix + rowId.ToString(CultureInfo.InvariantCulture),
                evaluation?.State ?? QuestState.Unknown,
                evaluation?.NextStep?.Detail ?? string.Empty));
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
                quest?.Name ?? Strings.MoonlitQuestPrefix + ev.RowId.ToString(CultureInfo.InvariantCulture));
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
        QuestEventKind.Completed => Theme.Moon,
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

    private readonly record struct DashboardKey(int Version, ulong ContentId, bool Live, long Minute, bool HasMoonlit);

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

    private sealed record JobRow(JobGroup Group, uint IconId, string Name, string Abbreviation, string Level, short LevelValue);

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
        MoonlitRow[] Moonlit,
        PinnedRow[] Pinned,
        RecentRow[] Recent,
        JobRow[] Jobs,
        string GrandCompanyLine,
        (string Tribe, string Rank, string Value)[] Tribes,
        string AllowancesLine);
}
