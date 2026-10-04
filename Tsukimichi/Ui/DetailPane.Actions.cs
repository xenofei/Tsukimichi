using System;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Travel;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The action bar's travel and automation row (1.10, the owner's "give the automation buttons the emphasis Teleport
/// has"; 1.15 the game's own icons, UI-5e: the flag marker, the quest's map marker, the aetheryte, Sprint, the duty's
/// tile): labelled pills of one weight, in this order: Flag on map (leading only while Lifestream is not loaded, as
/// the primary action did before), Go to giver, Teleport, Walk to giver, then, when they concern the quest, Start
/// Questionable and Run with AutoDuty. The first travel pill that can start now wears the accent
/// (<see cref="PillTone.Primary"/>). While a hand-off runs its pill turns into a labelled Stop (Eclipse); what it does
/// ("Going to giver · Teleporting…", Questionable's step) is said in the status bar (DetailPane.Activity.cs), never in
/// a line that would push the pane down. A pill that cannot start stays on the row, dimmed but labelled, its reason in
/// the tooltip.
/// <para>
/// The row is fitted to the pane by <see cref="ActionPillFit"/>: full labels, then short ones from the least
/// important pill, then icon-only pills (the label moves into the tooltip), then whole pills overflow into the "…"
/// menu, the first always staying. The pills' state is read once per frame in <see cref="PrepareActions"/>, before the
/// bar's height is planned; tooltips are composed only on hover.
/// </para>
/// </summary>
public sealed partial class DetailPane
{
    private const int MaxActions = 6;

    private static readonly string QuestionableIcon = FontAwesomeIcon.Play.ToIconString();
    private static readonly string AutoDutyIcon = FontAwesomeIcon.Dungeon.ToIconString();

    private enum ActionKind : byte
    {
        Flag,
        GoTo,
        Teleport,
        Walk,
        Questionable,
        AutoDuty,
    }

    /// <summary>One pill of the row as read this frame. The strings are the shared localized ones, so filling it allocates nothing.</summary>
    private struct ActionSlot
    {
        public ActionKind Kind;
        public PillIcon Icon;
        public string Label;
        public string Short;
        public PillTone Tone;
        public bool Enabled;
        public bool Stop;

        /// <summary>A travel pill that waits because the other one's trip is under way: its tooltip says to press Stop first.</summary>
        public bool WaitsForJourney;
    }

    private readonly ActionSlot[] actions = new ActionSlot[MaxActions];
    private readonly PillWidths[] actionWidths = new PillWidths[MaxActions];
    private readonly PillForm[] actionForms = new PillForm[MaxActions];
    private int actionCount;
    private int actionVisible;

    // Which pill of this pane started the trip under way (its pill turns into Stop); null when none did here.
    private ActionKind? travelStartedBy;

    // Start Questionable's reason to wait, asked at most every half second (it may compose a list of missing plugins).
    private string? questionableBlocker;
    private uint questionableBlockerRowId = uint.MaxValue;
    private double questionableBlockerAt = double.NegativeInfinity;

    // Run with AutoDuty: the duty row it runs and this frame's choice.
    private DutyRow? autoDutyRow;
    private AutoDutyChoice autoDutyChoice;

    /// <summary>Reads every pill's state for the quest shown, once per frame, before the bar's height is planned.</summary>
    private void PrepareActions(SessionState session, QuestRecord quest)
    {
        actionCount = 0;
        var journey = links.IsTraveling;
        if (!journey)
        {
            travelStartedBy = null;
        }

        // Without Lifestream (or with Teleport hidden by the automation level, 1.18), Flag on map leads the row, as the
        // primary action did before 1.10.
        if (links.FlagLeads)
        {
            var canFlag = links.CanFlagMap(quest);
            AddAction(ActionKind.Flag, ActionIcons.FlagIcon, Strings.FlagOnMap, Strings.FlagOnMap, PillTone.Normal, canFlag);
        }

        // With Walk and Go to giver both above the automation level, a trip under way still gets its Stop here.
        var stopOnly = journey && !links.GoToShown && !links.WalkShown;
        if (links.GoToShown || (stopOnly && goToCheck.Stoppable))
        {
            var go = goToCheck;
            var stop = go.Stoppable && (travelStartedBy != ActionKind.Walk || !links.WalkShown);
            AddAction(ActionKind.GoTo, stop ? StopIcon : ActionIcons.GoTo(quest), stop ? Strings.TravelStop : Strings.TravelGoTo, stop ? Strings.TravelStop : Strings.ActionGoToShort,
                stop ? PillTone.Danger : PillTone.Normal, stop || go.Ready, stop, waits: go.Stoppable && !stop);
        }

        if (links.TeleportShown)
        {
            var teleport = teleportCheck;
            AddAction(ActionKind.Teleport, ActionIcons.TeleportIcon, Strings.ActionTeleport, Strings.ActionTeleport, teleport.AlreadyHere ? PillTone.Quiet : PillTone.Normal, teleport.Ready);
        }

        if (links.WalkShown)
        {
            var walk = walkCheck;
            var stop = walk.Stoppable && (travelStartedBy == ActionKind.Walk || !links.GoToShown);
            AddAction(ActionKind.Walk, stop ? StopIcon : ActionIcons.WalkIcon, stop ? Strings.TravelStop : Strings.TravelWalk, stop ? Strings.TravelStop : Strings.TravelWalkShort,
                stop ? PillTone.Danger : PillTone.Normal, stop || walk.Ready, stop, waits: walk.Stoppable && !stop);
        }

        // The accent goes to the first travel pill that can start now (never a quiet Teleport or a Stop); Flag on map
        // wears it only when none can.
        var primary = -1;
        for (var i = 0; i < actionCount && primary < 0; i++)
        {
            if (actions[i].Enabled && actions[i].Tone == PillTone.Normal && actions[i].Kind != ActionKind.Flag)
            {
                primary = i;
            }
        }

        if (primary < 0 && actionCount > 0 && actions[0] is { Kind: ActionKind.Flag, Enabled: true })
        {
            primary = 0;
        }

        if (primary >= 0)
        {
            actions[primary].Tone = PillTone.Primary;
        }

        PrepareQuestionableAction(quest);
        PrepareAutoDutyAction(session, quest);
    }

    private void AddAction(ActionKind kind, PillIcon icon, string label, string shortLabel, PillTone tone, bool enabled, bool stop = false, bool waits = false)
    {
        actions[actionCount++] = new ActionSlot
        {
            Kind = kind,
            Icon = icon,
            Label = label,
            Short = shortLabel,
            Tone = tone,
            Enabled = enabled,
            Stop = stop,
            WaitsForJourney = waits,
        };
    }

    /// <summary>
    /// Start Questionable, for a quest not done or locked out, while Questionable is installed (loaded or not: a
    /// disabled pill names what is missing); Stop Questionable on every quest while it runs.
    /// </summary>
    private void PrepareQuestionableAction(QuestRecord quest)
    {
        if (QuestionableActions is not { } questionable)
        {
            return;
        }

        if (questionable.Running)
        {
            AddAction(ActionKind.Questionable, StopIcon, Strings.ActionQuestionableStop, Strings.QuestionableStopShort, PillTone.Danger, questionable.CanStop, stop: true);
            return;
        }

        // Above the automation level (1.18, A10) Start is hidden, not greyed; a Stop above always shows.
        if (!AutomationGate.Shows(AutomationButtons.Questionable))
        {
            return;
        }

        var installed = Companions?.Status(CompanionPlugin.Questionable).State is { } state ? state != CompanionState.Missing : questionable.Ipc.Available;
        if (!installed || model.State is QuestState.Completed or QuestState.DoneThisCycle or QuestState.Foreclosed)
        {
            return;
        }

        var now = ImGui.GetTime();
        if (questionableBlockerRowId != quest.RowId || now - questionableBlockerAt >= 0.5)
        {
            questionableBlockerRowId = quest.RowId;
            questionableBlockerAt = now;
            questionableBlocker = questionable.StartQuestBlocker(quest.RowId);
        }

        AddAction(ActionKind.Questionable, QuestionableIcon, Strings.ActionQuestionableStart, Strings.ActionQuestionableShort, PillTone.Normal, questionableBlocker is null);
    }

    /// <summary>
    /// Run with AutoDuty, for a quest with a duty to run: one it needs cleared first, else one it unlocks that the
    /// character has unlocked. Stop AutoDuty while AutoDuty runs. Nothing while AutoDuty is not installed.
    /// </summary>
    private void PrepareAutoDutyAction(SessionState session, QuestRecord quest)
    {
        autoDutyRow = null;
        if (Companions is not { } companions || AutoDuty is not { } autoDuty)
        {
            return;
        }

        var running = autoDuty.Available && !autoDuty.IsStopped;
        if (running)
        {
            AddAction(ActionKind.AutoDuty, StopIcon, Strings.AutoDutyStop, Strings.ActionStopShort, PillTone.Danger, true, stop: true);
            return;
        }

        if (!AutomationGate.Shows(AutomationButtons.AutoDuty) || companions.Status(CompanionPlugin.AutoDuty).State == CompanionState.Missing)
        {
            return;
        }

        RefreshDuties(session, quest);
        for (var i = 0; i < dutyRows.Count && autoDutyRow is null; i++)
        {
            if (dutyRows[i].Duty.Relation == QuestDutyRelation.Required)
            {
                autoDutyRow = dutyRows[i];
            }
        }

        for (var i = 0; i < dutyRows.Count && autoDutyRow is null; i++)
        {
            if (dutyRows[i].Unlocked == true)
            {
                autoDutyRow = dutyRows[i];
            }
        }

        if (autoDutyRow is not { } row)
        {
            return;
        }

        autoDutyChoice = AutoDutyPlan.Choose(row.Duty.Duty, AutoDutyInputsFor(companions, session, running: false) with { HasPath = row.HasPath, Unlocked = row.Unlocked });
        AddAction(ActionKind.AutoDuty, DutyPillIcon(row), Strings.AutoDutyRun, Strings.ActionAutoDutyShort, PillTone.Normal, autoDutyChoice.CanRun);
    }

    /// <summary>Fits the row to <paramref name="width"/>: each pill's form, and how many stay on it (the rest go into "…").</summary>
    private void LayoutActions(float width)
    {
        for (var i = 0; i < actionCount; i++)
        {
            actionWidths[i] = Chrome.ActionPillWidths(actions[i].Icon, actions[i].Label, actions[i].Short);
        }

        actionVisible = ActionPillFit.Fit(width, ActionGap, actionWidths.AsSpan(0, actionCount), actionForms.AsSpan(0, actionCount)).Visible;
    }

    private static float ActionGap => UiMetrics.Px(ActionPillFit.GapLogical);

    /// <summary>Run with AutoDuty's icon (UI-5e): the duty's own tile, else the Duty Finder's.</summary>
    private static PillIcon DutyPillIcon(DutyRow row) =>
        GameIconRef.Tile(row.Duty.Duty.Icon != 0 ? row.Duty.Duty.Icon : ActionIcons.DutyFinder);

    /// <summary>The row of pills.</summary>
    private void DrawActionRow(QuestRecord quest, uint rowId)
    {
        for (var i = 0; i < actionVisible; i++)
        {
            if (i > 0)
            {
                ImGui.SameLine(0f, ActionGap);
            }

            ref readonly var slot = ref actions[i];
            var form = actionForms[i];
            var label = form switch
            {
                PillForm.Full => slot.Label,
                PillForm.Short => slot.Short,
                _ => null,
            };
            if (Chrome.ActionPill(ActionId(slot.Kind), slot.Icon, label, slot.Tone, slot.Enabled))
            {
                RunAction(i, quest, rowId);
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ActionTooltip(i, quest, named: form != PillForm.Full);
            }
        }
    }

    /// <summary>The pills the row had no room for, as items at the top of the "…" menu; true when there were any.</summary>
    private bool DrawOverflowActions(QuestRecord quest, uint rowId)
    {
        if (actionVisible >= actionCount)
        {
            return false;
        }

        for (var i = actionVisible; i < actionCount; i++)
        {
            ref readonly var slot = ref actions[i];
            if (ImGui.MenuItem(slot.Label, string.Empty, false, slot.Enabled))
            {
                RunAction(i, quest, rowId);
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ActionTooltip(i, quest, named: false);
            }
        }

        return true;
    }

    private static string ActionId(ActionKind kind) => kind switch
    {
        ActionKind.Flag => "##actionFlag",
        ActionKind.GoTo => "##actionGoTo",
        ActionKind.Teleport => "##actionTeleport",
        ActionKind.Walk => "##actionWalk",
        ActionKind.Questionable => "##actionQuestionable",
        _ => "##actionAutoDuty",
    };

    /// <summary>What a click on pill <paramref name="index"/> does: start its action, or stop the hand-off it shows.</summary>
    private void RunAction(int index, QuestRecord quest, uint rowId)
    {
        ref readonly var slot = ref actions[index];
        if (!slot.Enabled)
        {
            return;
        }

        switch (slot.Kind)
        {
            case ActionKind.Flag:
                links.FlagMap(quest);
                break;
            case ActionKind.Teleport:
                links.TeleportToGiver(quest);
                break;
            case ActionKind.GoTo or ActionKind.Walk when slot.Stop:
                links.StopTravel();
                break;
            case ActionKind.GoTo:
                if (links.GoToGiver(quest))
                {
                    travelStartedBy = ActionKind.GoTo;
                }

                break;
            case ActionKind.Walk:
                if (links.WalkToGiver(quest))
                {
                    travelStartedBy = ActionKind.Walk;
                }

                break;
            case ActionKind.Questionable when QuestionableActions is { } questionable:
                if (slot.Stop)
                {
                    questionable.Stop(MainWindow.QuestionableHost);
                }
                else
                {
                    questionable.StartQuest(MainWindow.QuestionableHost, rowId);
                }

                break;
            case ActionKind.AutoDuty when AutoDuty is { } autoDuty:
                RunAutoDuty(autoDuty, quest, slot.Stop);
                break;
        }
    }

    private void RunAutoDuty(AutoDutyIpc autoDuty, QuestRecord quest, bool stop)
    {
        if (stop)
        {
            if (!autoDuty.Stop())
            {
                ShowCompanionNote(Strings.AutoDutyUnreachable);
            }

            return;
        }

        if (autoDutyRow is { } row && autoDutyChoice.CanRun)
        {
            StartAutoDuty(autoDuty, quest, row, autoDutyChoice);
        }
    }

    /// <summary>
    /// The tooltip of pill <paramref name="index"/>: what it does, Stop, or why it waits. A <paramref name="named"/>
    /// one (short label or icon only) puts its full label first, as its title.
    /// </summary>
    private void ActionTooltip(int index, QuestRecord quest, bool named)
    {
        ref readonly var slot = ref actions[index];
        var body = slot.Kind switch
        {
            ActionKind.Flag => slot.Enabled ? Strings.FlagOnMap : Strings.ActionFlagUnavailable,
            ActionKind.GoTo or ActionKind.Walk when slot.WaitsForJourney => Strings.TravelBusyJourney,
            ActionKind.GoTo => links.GoToTooltip(quest, goToCheck),
            ActionKind.Walk => links.WalkTooltip(quest, walkCheck),
            ActionKind.Teleport => links.TeleportTooltip(quest, teleportCheck),
            ActionKind.Questionable when slot.Stop => QuestionableActions?.StopTooltip() ?? Strings.QuestionableStopNoGate,
            ActionKind.Questionable => questionableBlocker ?? QuestionableActions?.StartTooltip ?? Strings.ActionQuestionableStartTooltip,
            ActionKind.AutoDuty when slot.Stop => Strings.AutoDutyStopTooltip,
            _ => AutoDutyTooltip(),
        };

        // Run with AutoDuty names its duty in the title; the others their label when it is not on screen.
        var title = slot.Kind == ActionKind.AutoDuty && !slot.Stop && autoDutyRow is { } row
            ? named ? slot.Label + Strings.AutoDutyCaptionSeparator + DutyLabel(row.Duty.Duty.Name) : DutyLabel(row.Duty.Duty.Name)
            : named && !string.Equals(slot.Label, body, StringComparison.Ordinal) ? slot.Label : null;
        if (title is null)
        {
            UiMetrics.Tooltip(body);
        }
        else
        {
            UiMetrics.Tooltip(title, body);
        }
    }

    private string AutoDutyTooltip() => autoDutyChoice.CanRun
        ? string.Format(CultureInfo.CurrentCulture, Strings.AutoDutyRunTooltipFormat, Strings.AutoDutyModeName(autoDutyChoice.Mode))
        : Companions is { } companions ? AutoDutyBlockerText(autoDutyChoice.Blocker, companions) : Strings.AutoDutyUnreachable;

    /// <summary>The height of the pill row with its item spacing.</summary>
    private float ActionRowHeight(float spacing) => actionCount > 0 ? Chrome.ActionPillHeight + spacing : 0f;
}
