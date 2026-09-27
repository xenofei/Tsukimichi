using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// Characters (spec §7, F-50..F-61). <see cref="DrawLeft"/> lists every stored snapshot (the live one marked ●) and
/// switches the viewed character; <see cref="DrawMain"/> shows the viewed snapshot (taken time, completion, job levels,
/// Grand Company, allied societies), Export JSON, Forget with a confirm popup, and the Account view: the state of
/// <see cref="UiState.SelectedRowId"/> on every character, evaluated offline from their snapshots.
/// <para>
/// Labels are rebuilt once per <see cref="SessionState.Version"/> (and once a minute for the ages); snapshots of other
/// characters are loaded through <c>loadSnapshot</c> only when their capture time changed, and their evaluations are
/// memoized per version.
/// </para>
/// </summary>
public sealed class CharactersPane
{
    private const string ExportsFolder = "exports";
    private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(8);

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

    private Dictionary<uint, string>? worldNames;

    private CharacterItem[] items = [];
    private int itemsVersion = -1;
    private long itemsMinute = -1;

    private Detail? detail;

    private readonly Dictionary<ulong, (DateTime Taken, CharacterSnapshot Snapshot)> snapshotCache = [];
    private readonly Dictionary<ulong, QuestEvaluation?> accountCache = [];
    private uint accountRowId;
    private int accountVersion = -1;

    private string? toast;
    private DateTime toastUntilUtc;
    private string forgetQuestion = string.Empty;
    private ulong forgetTarget;

    /// <param name="loadSnapshot">Loads a stored character by content id (e.g. <c>SnapshotService.Load</c>); null when unreadable.</param>
    /// <param name="data">Optional; resolves world names from the World sheet. Without it the world id is shown.</param>
    public CharactersPane(SessionState session, PluginPaths paths, IPluginLog log, Func<ulong, CharacterSnapshot?> loadSnapshot, IDataManager? data = null)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.loadSnapshot = loadSnapshot ?? throw new ArgumentNullException(nameof(loadSnapshot));
        this.data = data;
    }

    /// <summary>Left column: stored characters, newest capture first; selecting one views it.</summary>
    public void DrawLeft(UiState ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        using var id = ImRaii.PushId("charactersLeft");
        RefreshItems();

        if (items.Length == 0)
        {
            ImGui.TextWrapped(Strings.CharactersNoneStored);
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
    }

    /// <summary>Center column: the viewed character, its actions and the account view for the selected quest.</summary>
    public void DrawMain(UiState ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        using var id = ImRaii.PushId("charactersMain");

        var snapshot = session.ViewedSnapshot;
        if (snapshot is null)
        {
            ImGui.TextWrapped(Strings.CharactersNoneViewed);
            return;
        }

        var d = RefreshDetail(snapshot);

        ImGui.TextUnformatted(d.Name);
        ImGui.SameLine();
        ImGui.TextDisabled(d.World);
        ImGui.TextDisabled(d.TakenLine);
        ImGui.TextUnformatted(d.CountsLine);
        ImGui.Spacing();

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
            ImGui.SetTooltip(Strings.CharactersForgetLiveHint);
        }

        DrawForgetPopup(ui);
        DrawToast();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawJobs(d);
        ImGui.Spacing();
        DrawGrandCompanyAndTribes(d);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawAccountView(ui);
    }

    private void DrawForgetPopup(UiState ui)
    {
        using var modal = ImRaii.PopupModal(Strings.CharactersForgetPopup, ImGuiWindowFlags.AlwaysAutoResize);
        if (!modal)
        {
            return;
        }

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

    private static void DrawJobs(Detail d)
    {
        ImGui.TextDisabled(Strings.CharactersJobs);
        if (d.Jobs.Length == 0)
        {
            ImGui.TextDisabled(Strings.CharactersNoJobs);
            return;
        }

        using var table = ImRaii.Table("##jobs", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        ImGui.TableSetupColumn(Strings.CharactersColumnJob, ImGuiTableColumnFlags.WidthFixed, 220f * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn(Strings.CharactersColumnLevel, ImGuiTableColumnFlags.WidthFixed, 60f * ImGuiHelpers.GlobalScale);
        ImGui.TableHeadersRow();
        foreach (var (job, level) in d.Jobs)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(job);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(level);
        }
    }

    private static void DrawGrandCompanyAndTribes(Detail d)
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

        var scale = ImGuiHelpers.GlobalScale;
        ImGui.TableSetupColumn(Strings.CharactersColumnTribe, ImGuiTableColumnFlags.WidthFixed, 220f * scale);
        ImGui.TableSetupColumn(Strings.CharactersColumnRank, ImGuiTableColumnFlags.WidthFixed, 120f * scale);
        ImGui.TableSetupColumn(Strings.CharactersColumnReputation, ImGuiTableColumnFlags.WidthFixed, 90f * scale);
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
        ImGui.TableSetupColumn(Strings.CharactersColumnCharacter, ImGuiTableColumnFlags.WidthFixed, 200f * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn(Strings.CharactersColumnState, ImGuiTableColumnFlags.WidthFixed, 170f * ImGuiHelpers.GlobalScale);
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
                MoonGlyph.DrawInline(QuestState.Unknown, line);
                ImGui.SameLine();
                ImGui.TextDisabled(Strings.CharactersSnapshotUnreadable);
            }
            else
            {
                MoonGlyph.DrawInline(evaluation.State, line);
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

    /// <summary>The stored snapshot of a character, reloaded only when its capture time changed.</summary>
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

        if (loaded is null)
        {
            snapshotCache.Remove(item.ContentId);
            return null;
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

    /// <summary>Detail strings for the viewed snapshot, rebuilt when the snapshot instance or the catalog bundle changes.</summary>
    private Detail RefreshDetail(CharacterSnapshot snapshot)
    {
        var bundle = session.Bundle;
        if (detail is { } current && ReferenceEquals(current.Snapshot, snapshot) && ReferenceEquals(current.Bundle, bundle) && current.Live == session.IsLive)
        {
            return current;
        }

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

        var jobs = new List<(byte Id, short Level)>(snapshot.JobLevels.Count);
        foreach (var (job, level) in snapshot.JobLevels)
        {
            jobs.Add((job, level));
        }

        jobs.Sort((a, b) => b.Level != a.Level ? b.Level.CompareTo(a.Level) : a.Id.CompareTo(b.Id));
        var jobRows = new (string Job, string Level)[jobs.Count];
        for (var i = 0; i < jobRows.Length; i++)
        {
            jobRows[i] = (JobName(names, jobs[i].Id), jobs[i].Level.ToString(CultureInfo.InvariantCulture));
        }

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

        detail = new Detail(snapshot, bundle, session.IsLive, snapshot.Name, WorldName(snapshot.World), takenLine, countsLine, jobRows, gcLine, tribeRows, allowances);
        return detail;
    }

    private static string JobName(GameNames? names, byte job)
    {
        if (job == 0)
        {
            return Strings.CharactersJobPrefix + "0";
        }

        var name = names?.ClassJob(job);
        return string.IsNullOrEmpty(name) ? Strings.CharactersJobPrefix + job.ToString(CultureInfo.InvariantCulture) : name;
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

    private sealed record CharacterItem(ulong ContentId, string Name, DateTime TakenUtc, string Label, string Detail);

    private sealed record Detail(
        CharacterSnapshot Snapshot,
        CatalogBundle? Bundle,
        bool Live,
        string Name,
        string World,
        string TakenLine,
        string CountsLine,
        (string Job, string Level)[] Jobs,
        string GrandCompanyLine,
        (string Tribe, string Rank, string Value)[] Tribes,
        string AllowancesLine);
}
