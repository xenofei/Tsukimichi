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
    /// The right-click menu on a Jobs table row: "Route to unlock &lt;job&gt;" for each locked job the class grows into.
    /// A row with no such job (a job, or a class whose jobs are all unlocked) gets no menu at all.
    /// </summary>
    private void DrawJobRowRouteMenu(UiState ui, Dashboard d, uint jobId)
    {
        var locked = LockedJobs(d);
        if (!HasLockedJobFrom(locked, jobId))
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
