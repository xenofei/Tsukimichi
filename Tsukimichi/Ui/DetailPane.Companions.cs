using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Model;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The detail pane's companion plugin pieces (feature plan v5, decision 1).
/// <para>
/// <b>Duties</b>, a section after the Path for a quest that requires or unlocks a duty (<see cref="QuestDuties"/>): each
/// duty's name and how it relates to the quest, "AutoDuty has a path" when AutoDuty says so (read only), and "Run with
/// AutoDuty", which hands the duty to AutoDuty for one clear in Duty Support, else Trust, else (with Settings ›
/// Integrations › "Allow AutoDuty to queue in the regular Duty Finder") the Duty Finder. The button is always shown;
/// disabled, its tooltip says why (<see cref="AutoDutyPlan.Choose"/>: AutoDuty or what it needs is missing, a stored
/// character, no path, not unlocked, no Duty Support or Trust). While AutoDuty runs the section says so and offers Stop.
/// </para>
/// <para>
/// <b>Open in Quest Map</b>, a small button at the end of the Path section, disabled with the reason while Quest Map is
/// not loaded.
/// </para>
/// The duty list, AutoDuty's path answers and the unlock checks are refreshed when the selection, the session version or
/// Dalamud's plugin list changes, never per frame; AutoDuty's running state is its own cached read.
/// </summary>
public sealed partial class DetailPane
{
    private const double CompanionNoteSeconds = 6.0;

    private static readonly string DutiesIcon = FontAwesomeIcon.Dungeon.ToIconString();

    private static readonly Localization.LocText QuestMapLabel = new(static () => Strings.QuestMapOpen + "##questMap");

    /// <summary>One duty row as of the last refresh.</summary>
    private sealed record DutyRow(QuestDuty Duty, string Caption, bool? HasPath, bool? Unlocked, string RunId);

    private readonly List<DutyRow> dutyRows = [];
    private uint dutyRowId = uint.MaxValue;
    private int dutyVersion = -1;
    private int dutyGeneration = -1;
    private DutyRunIndex? dutyIndex;

    // "AutoDuty is running …" / "Quest Map does not chart this quest" for a few seconds after a press.
    private string? companionNote;
    private double companionNoteUntil;
    private uint companionNoteRowId;

    /// <summary>The companion plugin registry; null until the plugin attaches it, which hides the Duties section and Quest Map.</summary>
    public CompanionPlugins? Companions { get; set; }

    /// <summary>AutoDuty's IPC; null hides the Duties section.</summary>
    public AutoDutyIpc? AutoDuty { get; set; }

    /// <summary>Quest Map's IPC; null hides "Open in Quest Map".</summary>
    public QuestMapIpc? QuestMap { get; set; }

    /// <summary>The duty index (built once from the sheets); null hides the Duties section.</summary>
    public Func<DutyRunIndex?>? DutyRuns { get; set; }

    /// <summary>A quest's reward entries in the merged reward catalog (its duty unlocks among them); null reads none.</summary>
    public Func<uint, IReadOnlyList<UniqueRewardEntry>>? RewardEntries { get; set; }

    /// <summary>Whether the logged-in character has an InstanceContent row unlocked; null when it cannot be read.</summary>
    public Func<uint, bool?>? IsDutyUnlocked { get; set; }

    /// <summary>Reads Settings › Integrations › "Allow AutoDuty to queue in the regular Duty Finder"; null reads as off.</summary>
    public Func<bool>? AutoDutyAllowDutyFinder { get; set; }

    // ------------------------------------------------------------------ Duties

    private void RefreshDuties(SessionState session, QuestRecord quest)
    {
        var index = DutyRuns?.Invoke();
        var generation = Companions?.Generation ?? 0;
        if (dutyRowId == quest.RowId && dutyVersion == session.Version && dutyGeneration == generation && ReferenceEquals(dutyIndex, index))
        {
            return;
        }

        dutyRowId = quest.RowId;
        dutyVersion = session.Version;
        dutyGeneration = generation;
        dutyIndex = index;
        dutyRows.Clear();
        if (index is null || AutoDuty is not { } autoDuty)
        {
            return;
        }

        var duties = QuestDuties.For(quest, index, session.Curated, RewardEntries?.Invoke(quest.RowId));
        foreach (var duty in duties)
        {
            var info = duty.Duty;
            var hasPath = autoDuty.HasPath(info.TerritoryTypeId);
            bool? unlocked = session.IsLive && info.InstanceContentId != 0 ? IsDutyUnlocked?.Invoke(info.InstanceContentId) : null;
            var relation = duty.Relation == QuestDutyRelation.Required ? Strings.AutoDutyRelationRequired : Strings.AutoDutyRelationUnlocks;
            var caption = hasPath switch
            {
                true => relation + Strings.AutoDutyCaptionSeparator + Strings.AutoDutyHasPath,
                false => relation + Strings.AutoDutyCaptionSeparator + Strings.AutoDutyNoPath,
                _ => relation,
            };
            dutyRows.Add(new DutyRow(duty, caption, hasPath, unlocked, "##autoDuty" + info.ContentFinderConditionId.ToString(CultureInfo.InvariantCulture)));
        }
    }

    /// <summary>The Duties section; nothing for a quest that requires and unlocks no duty AutoDuty could know.</summary>
    private void DrawDuties(SessionState session, QuestRecord quest)
    {
        if (Companions is not { } companions || AutoDuty is not { } autoDuty)
        {
            return;
        }

        RefreshDuties(session, quest);
        if (dutyRows.Count == 0)
        {
            return;
        }

        Gap();
        BeginSection("##duties", Strings.DutiesSection, DutiesIcon);
        var running = autoDuty.Available && !autoDuty.IsStopped;
        if (running)
        {
            using (Theme.PushText(Theme.Surface.Text))
            {
                TextFlow.Wrapped(Strings.AutoDutyRunning, RoomTo(cardRight));
            }

            if (Chrome.ActionPill("##autoDutyStop", StopIcon, Strings.AutoDutyStop, PillTone.Danger, true, Strings.AutoDutyStopTooltip) && !autoDuty.Stop())
            {
                ShowCompanionNote(quest.RowId, Strings.AutoDutyUnreachable);
            }
        }

        var inputsBase = AutoDutyInputsFor(companions, session, running);
        var anyPath = false;
        for (var i = 0; i < dutyRows.Count; i++)
        {
            var row = dutyRows[i];
            anyPath |= row.HasPath == true;
            if (Chrome.EllipsisText(row.Duty.Duty.Name, RoomTo(cardRight), Theme.U32(Theme.Surface.Text)) && ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(row.Duty.Duty.Name);
            }

            TextFlow.Wrapped(row.Caption, RoomTo(cardRight), Theme.U32(Theme.Surface.TextSecondary));
            if (row.HasPath == true && ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.AutoDutyHasPathTooltip);
            }

            var choice = AutoDutyPlan.Choose(row.Duty.Duty, inputsBase with { HasPath = row.HasPath, Unlocked = row.Unlocked });

            // The same pill as the action bar's Run with AutoDuty (1.10); the icon alone when the card is narrower than its label.
            var label = Chrome.ActionPillWidth(AutoDutyIcon, Strings.AutoDutyRun) <= RoomTo(cardRight) ? Strings.AutoDutyRun : null;
            var pressed = Chrome.ActionPill(row.RunId, AutoDutyIcon, label, PillTone.Normal, choice.CanRun);
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                var text = choice.CanRun
                    ? string.Format(CultureInfo.CurrentCulture, Strings.AutoDutyRunTooltipFormat, Strings.AutoDutyModeName(choice.Mode))
                    : AutoDutyBlockerText(choice.Blocker, companions);
                if (label is null)
                {
                    UiMetrics.Tooltip(Strings.AutoDutyRun, text);
                }
                else
                {
                    UiMetrics.Tooltip(text);
                }
            }

            if (pressed && choice.CanRun)
            {
                StartAutoDuty(autoDuty, quest, row, choice);
            }
        }

        // Boss Mod's own autorotation serves too, so a missing rotation plugin is a note, not a blocker.
        if (anyPath && companions.IsLoaded(CompanionPlugin.AutoDuty) && !companions.IsLoaded(CompanionPlugin.RotationPlugin))
        {
            TextFlow.Wrapped(Strings.AutoDutyRotationNote, RoomTo(cardRight), Theme.U32(Theme.Surface.TextDisabled));
        }

        DrawCompanionNote(quest.RowId);
        EndSection();
    }

    /// <summary>AutoDuty's inputs for a run as of now, before the duty's own path and unlock answers.</summary>
    private AutoDutyInputs AutoDutyInputsFor(CompanionPlugins companions, SessionState session, bool running) => new(
        companions.Status(CompanionPlugin.AutoDuty).State,
        companions.Status(CompanionPlugin.Vnavmesh).State,
        companions.Status(CompanionPlugin.BossMod).State,
        session.IsLive,
        running,
        null,
        null,
        AutoDutyAllowDutyFinder?.Invoke() == true);

    /// <summary>Hands the duty to AutoDuty in the chosen mode and says how it went under the Duties section.</summary>
    private void StartAutoDuty(AutoDutyIpc autoDuty, QuestRecord quest, DutyRow row, AutoDutyChoice choice)
    {
        var result = autoDuty.Run(row.Duty.Duty.TerritoryTypeId, choice.Mode);
        ShowCompanionNote(quest.RowId, result switch
        {
            AutoDutyStart.Started => string.Format(CultureInfo.CurrentCulture, Strings.AutoDutyStartedFormat, row.Duty.Duty.Name),
            AutoDutyStart.ModeRefused => Strings.AutoDutyModeRefused,
            AutoDutyStart.NotStarted => Strings.AutoDutyNotStarted,
            _ => Strings.AutoDutyUnreachable,
        });
    }

    /// <summary>The disabled Run button's reason, naming the plugin that is missing.</summary>
    private static string AutoDutyBlockerText(AutoDutyBlocker blocker, CompanionPlugins companions) => blocker switch
    {
        AutoDutyBlocker.AutoDutyUnavailable => companions.ReasonFor(CompanionPlugin.AutoDuty) ?? Strings.AutoDutyUnreachable,
        AutoDutyBlocker.NeedsVnavmesh => string.Format(CultureInfo.CurrentCulture, Strings.AutoDutyNeedsFormat, CompanionPlugins.NeedName(companions.Status(CompanionPlugin.Vnavmesh))),
        AutoDutyBlocker.NeedsBossMod => string.Format(CultureInfo.CurrentCulture, Strings.AutoDutyNeedsFormat, CompanionPlugins.NeedName(companions.Status(CompanionPlugin.BossMod))),
        AutoDutyBlocker.NotLive => Strings.AutoDutyNotLive,
        AutoDutyBlocker.Busy => Strings.AutoDutyBusy,
        AutoDutyBlocker.NoPath => Strings.AutoDutyNoPath,
        AutoDutyBlocker.Locked => Strings.AutoDutyLocked,
        AutoDutyBlocker.NeedsDutyFinder => Strings.AutoDutyNeedsDutyFinder,
        _ => Strings.AutoDutyNoQueue,
    };

    // ------------------------------------------------------------------ Quest Map

    /// <summary>"Open in Quest Map" at the end of the Path section; disabled, naming Quest Map, while it is not loaded.</summary>
    private void DrawQuestMapButton(uint rowId)
    {
        if (QuestMap is not { } questMap)
        {
            return;
        }

        var available = questMap.Available;
        ImGui.BeginDisabled(!available);
        var pressed = ImGui.SmallButton(QuestMapLabel.Value);
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(available ? Strings.QuestMapOpenTooltip : questMap.DisabledReason ?? Strings.QuestMapOpenTooltip);
        }

        if (pressed && available && !questMap.ShowGraph(rowId))
        {
            ShowCompanionNote(rowId, Strings.QuestMapNotCharted);
        }

        // The note belongs to the Duties section when it has one; a quest without duties shows it here.
        if (dutyRows.Count == 0 || dutyRowId != rowId)
        {
            DrawCompanionNote(rowId);
        }
    }

    private void ShowCompanionNote(uint rowId, string note)
    {
        companionNote = note;
        companionNoteRowId = rowId;
        companionNoteUntil = ImGui.GetTime() + CompanionNoteSeconds;
    }

    private void DrawCompanionNote(uint rowId)
    {
        if (companionNote is { } note && companionNoteRowId == rowId && ImGui.GetTime() < companionNoteUntil)
        {
            TextFlow.Wrapped(note, RoomTo(cardRight), Theme.U32(Theme.Surface.TextSecondary));
        }
    }
}
