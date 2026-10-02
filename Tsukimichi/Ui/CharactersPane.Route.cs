using System.Collections.Generic;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Route;

namespace Tsukimichi.Ui;

/// <summary>
/// The Characters dashboard's ways into the unlock route (P6): "Route to unlock…" beside the Job quests heading lists
/// every job the viewed character has not unlocked (Blue Mage on a fresh alt, the expansion jobs, the jobs a class
/// grows into), and a right-click on a class in the Jobs table offers "Route to unlock Dragoon" for its jobs still
/// locked. Both open the route for the viewed character, a stored alt included. The locked jobs are worked out once
/// per dashboard.
/// </summary>
public sealed partial class CharactersPane
{
    private const string RouteToUnlockPopupId = "##routeToUnlock";
    private const string JobRouteMenuId = "##jobRoute";

    private object? lockedJobsFor;
    private LockedJob[] lockedJobs = [];

    /// <summary>A job whose unlock quest the character has not completed, with its menu label.</summary>
    private sealed record LockedJob(uint JobId, uint ParentId, string Name, uint UnlockQuestRowId, string MenuLabel);

    /// <summary>"Route to unlock…" and its popup of locked jobs, on the current line.</summary>
    private void DrawRouteToUnlockButton(UiState ui, Dashboard d)
    {
        if (ImGui.SmallButton(Strings.RouteToUnlockMenu))
        {
            ImGui.OpenPopup(RouteToUnlockPopupId);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.RouteToUnlockMenuTooltip);
        }

        using var popup = ImRaii.Popup(RouteToUnlockPopupId);
        if (!popup)
        {
            return;
        }

        UiMetrics.ApplyFontScale();
        var locked = LockedJobs(d);
        if (locked.Length == 0)
        {
            ImGui.TextDisabled(Strings.RouteToUnlockNone);
            return;
        }

        foreach (var job in locked)
        {
            if (ImGui.MenuItem(job.Name))
            {
                ui.OpenRoute(RouteTarget.ForJob(job.Name, job.UnlockQuestRowId));
            }
        }
    }

    /// <summary>
    /// The right-click menu on a Jobs table row: "Route to unlock &lt;job&gt;" for each locked job the class grows into,
    /// then (1.6.0) "Route: everything for &lt;job&gt;" for the jobs the row stands for (a class's jobs, else the row's
    /// own job): its unlock quest, its job quests and its role quests up to the character's level cap, in one route.
    /// A row with neither gets no menu at all.
    /// </summary>
    private void DrawJobRowRouteMenu(UiState ui, Dashboard d, uint jobId)
    {
        var locked = LockedJobs(d);
        var everything = EverythingJobs(d, jobId);
        if (!HasLockedJobFrom(locked, jobId) && everything.Count == 0)
        {
            return;
        }

        using var menu = ImRaii.ContextPopupItem(JobRouteMenuId);
        if (!menu)
        {
            return;
        }

        UiMetrics.ApplyFontScale();
        foreach (var job in locked)
        {
            if (job.ParentId != jobId || job.JobId == jobId)
            {
                continue;
            }

            if (ImGui.MenuItem(job.MenuLabel))
            {
                ui.OpenRoute(RouteTarget.ForJob(job.Name, job.UnlockQuestRowId));
            }
        }

        foreach (var job in everything)
        {
            if (ImGui.MenuItem(job.MenuLabel) && d.Bundle is { } bundle)
            {
                ui.OpenRoute(RouteTarget.ForJobQuests(ladder, bundle.Catalog, job.JobId, job.Name, job.UnlockQuestRowId, d.Snapshot.LevelCap));
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.RouteEverythingTooltip);
            }
        }
    }

    private object? everythingFor;
    private LockedJob[] everythingJobs = [];
    private readonly List<LockedJob> everythingRow = [];

    /// <summary>
    /// The jobs a row's "everything" entries route to: the jobs a class grows into, else the row's own job, each only
    /// when it has quests (a ladder or an unlock quest). Built once per dashboard, filtered per row without allocating.
    /// </summary>
    private List<LockedJob> EverythingJobs(Dashboard d, uint jobId)
    {
        if (!ReferenceEquals(everythingFor, d))
        {
            everythingFor = d;
            var list = new List<LockedJob>();
            if (d.Bundle is { } bundle)
            {
                foreach (var info in bundle.Names.ClassJobInfos)
                {
                    if (info.Name.Length == 0 || (info.UnlockQuestRowId == 0 && ladder.ForJob(info.RowId) is null))
                    {
                        continue;
                    }

                    var name = DisplayName(info.Name);
                    list.Add(new LockedJob(info.RowId, info.ParentRowId, name, info.UnlockQuestRowId, string.Format(CultureInfo.CurrentCulture, Strings.RouteEverythingJobFormat, name)));
                }
            }

            everythingJobs = list.ToArray();
        }

        everythingRow.Clear();
        foreach (var job in everythingJobs)
        {
            if (job.ParentId == jobId && job.JobId != jobId)
            {
                everythingRow.Add(job);
            }
        }

        if (everythingRow.Count == 0)
        {
            foreach (var job in everythingJobs)
            {
                if (job.JobId == jobId)
                {
                    everythingRow.Add(job);
                }
            }
        }

        return everythingRow;
    }

    private static bool HasLockedJobFrom(LockedJob[] locked, uint jobId)
    {
        foreach (var job in locked)
        {
            if (job.ParentId == jobId && job.JobId != jobId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Jobs with an unlock quest the viewed character has not completed, in sheet order; built once per dashboard.</summary>
    private LockedJob[] LockedJobs(Dashboard d)
    {
        if (ReferenceEquals(lockedJobsFor, d))
        {
            return lockedJobs;
        }

        lockedJobsFor = d;
        var list = new List<LockedJob>();
        if (d.Bundle is { } bundle)
        {
            foreach (var info in bundle.Names.ClassJobInfos)
            {
                if (info.UnlockQuestRowId == 0 || info.Name.Length == 0 || d.Snapshot.IsCompleted(QuestRecord.ToQuestId(info.UnlockQuestRowId)))
                {
                    continue;
                }

                var name = DisplayName(info.Name);
                list.Add(new LockedJob(info.RowId, info.ParentRowId, name, info.UnlockQuestRowId, string.Format(CultureInfo.CurrentCulture, Strings.RouteToUnlockJobFormat, name)));
            }
        }

        lockedJobs = list.ToArray();
        return lockedJobs;
    }
}
