using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
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
/// duty's icon (<see cref="Core.Unlocks.DutyArt"/>'s chain) on a small well, its name and how it relates to the quest, "AutoDuty has a path" when AutoDuty says so (read only), and "Run with
/// AutoDuty", which hands the duty to AutoDuty for one clear in Duty Support, else Trust, else (with Settings ›
/// Integrations › "Allow AutoDuty to queue in the regular Duty Finder") the Duty Finder. The button is always shown;
/// disabled, its tooltip says why (<see cref="AutoDutyPlan.Choose"/>: AutoDuty or what it needs is missing, a stored
/// character, no path, not unlocked, no Duty Support or Trust). While AutoDuty runs, the status bar says so and offers
/// Stop, as the action row's pill does; the section itself never grows a line for it (feature plan v6, U4).
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
    private static readonly string DutiesIcon = FontAwesomeIcon.Dungeon.ToIconString();

    private static readonly Localization.LocText QuestMapLabel = new(static () => Strings.QuestMapOpen + "##questMap");

    /// <summary>One duty row as of the last refresh.</summary>
    private sealed record DutyRow(QuestDuty Duty, string Caption, bool? HasPath, bool? Unlocked, string RunId);

    private readonly List<DutyRow> dutyRows = [];
    private uint dutyRowId = uint.MaxValue;
    private int dutyVersion = -1;
    private int dutyGeneration = -1;
    private DutyRunIndex? dutyIndex;

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
        var inputsBase = AutoDutyInputsFor(companions, session, running);
        var anyPath = false;
        for (var i = 0; i < dutyRows.Count; i++)
        {
            var row = dutyRows[i];
            anyPath |= row.HasPath == true;
            DrawDutyIdentity(row);
            var choice = AutoDutyPlan.Choose(row.Duty.Duty, inputsBase with { HasPath = row.HasPath, Unlocked = row.Unlocked });

            // The same pill as the action bar's Run with AutoDuty (1.10); the icon alone when the card is narrower than its label.
            var runIcon = DutyPillIcon(row);
            var label = Chrome.ActionPillWidth(runIcon, Strings.AutoDutyRun) <= RoomTo(cardRight) ? Strings.AutoDutyRun : null;
            var pressed = Chrome.ActionPill(row.RunId, runIcon, label, PillTone.Normal, choice.CanRun);
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                var text = choice.CanRun
                    ? string.Format(CultureInfo.CurrentCulture, Strings.AutoDutyRunTooltipFormat, Strings.AutoDutyModeName(choice.Mode))
                    : AutoDutyBlockerText(choice.Blocker, companions);
                // A recommended AutoDuty setting set otherwise (companion setup) is a note, not a blocker.
                var note = choice.CanRun ? CompanionPlugins.SetupNote(CompanionPlugin.AutoDuty) : null;
                if (label is null)
                {
                    UiMetrics.Tooltip(Strings.AutoDutyRun, note is null ? text : text + "\n" + note);
                }
                else
                {
                    UiMetrics.Tooltip(text, note);
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

        EndSection();
    }

    /// <summary>
    /// The duty's icon on a well two lines high (as an Unlocks row wears it: the game icon, or the veiled moon while none
    /// is known), its name over the caption beside it; the cursor ends under both, at the row's left edge.
    /// </summary>
    private void DrawDutyIdentity(DutyRow row)
    {
        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var well = MathF.Round(2f * ImGui.GetTextLineHeight());
        var wellMax = start + new Vector2(well);
        var rounding = UiMetrics.Px(4f);
        dl.AddRectFilled(start, wellMax, Theme.U32(Theme.Surface.Sunken), rounding);
        dl.AddRect(start, wellMax, Theme.U32(Theme.Surface.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        var inset = new Vector2(UiMetrics.Px(3f));
        var icon = row.Duty.Duty.Icon;
        if (icon == 0 || !GameIcon.DrawAt(dl, textures, icon, start + inset, wellMax - inset, UiMetrics.Px(3f)))
        {
            MoonGlyph.DrawVeiled(dl, (start + wellMax) * 0.5f, well * 0.32f, 0.6f);
        }

        var textX = wellMax.X + UiMetrics.Px(8f);
        ImGui.SetCursorScreenPos(new Vector2(textX, start.Y));
        if (Chrome.EllipsisText(row.Duty.Duty.Name, RoomTo(cardRight), Theme.U32(Theme.Surface.Text)) && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(row.Duty.Duty.Name);
        }

        ImGui.SetCursorScreenPos(new Vector2(textX, ImGui.GetCursorScreenPos().Y));
        TextFlow.Wrapped(row.Caption, RoomTo(cardRight), Theme.U32(Theme.Surface.TextSecondary));
        if (row.HasPath == true && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.AutoDutyHasPathTooltip);
        }

        // Under the taller of the well and the text, at the row's left edge.
        var bottom = MathF.Max(ImGui.GetItemRectMax().Y, wellMax.Y);
        ImGui.SetCursorScreenPos(new Vector2(start.X, bottom + ImGui.GetStyle().ItemSpacing.Y));
    }

    /// <summary>
    /// AutoDuty's inputs for a run as of now, before the duty's own path and unlock answers. A trip of Tsukimichi's
    /// under way blocks the run: both would drive vnavmesh.
    /// </summary>
    private AutoDutyInputs AutoDutyInputsFor(CompanionPlugins companions, SessionState session, bool running) => new(
        companions.Status(CompanionPlugin.AutoDuty).State,
        companions.Status(CompanionPlugin.Vnavmesh).State,
        companions.Status(CompanionPlugin.BossMod).State,
        session.IsLive,
        running,
        null,
        null,
        AutoDutyAllowDutyFinder?.Invoke() == true)
    {
        Traveling = links.IsTraveling,
    };

    /// <summary>Hands the duty to AutoDuty in the chosen mode and says how it went in the status bar.</summary>
    private void StartAutoDuty(AutoDutyIpc autoDuty, QuestRecord quest, DutyRow row, AutoDutyChoice choice)
    {
        var result = autoDuty.Run(row.Duty.Duty.TerritoryTypeId, choice.Mode);
        ShowCompanionNote(result switch
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
        AutoDutyBlocker.Traveling => Strings.TravelBusyJourney,
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
            ShowCompanionNote(Strings.QuestMapNotCharted);
        }
    }
}
