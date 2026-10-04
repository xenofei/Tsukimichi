using System;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Model;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The detail pane's Questionable pieces (V2-17): a line under the status saying whether Questionable's own lock check
/// agrees with Tsukimichi ("Questionable agrees", "Questionable says: Prev quest (1)"), and, when Settings ›
/// Integrations › "Show Questionable hand-off" is ticked and Questionable offers the gate and a working reason gate,
/// "Add to Questionable priority" in the menu of the round "…" button at the end of the action bar (which since 1.8.0
/// always shows, for "Open on…"). Questionable is asked
/// when the selection, the session version or Dalamud's plugin list changes, never per frame; nothing shows while it
/// is not loaded.
/// <para>
/// 1.6.0 (feature plan v5): under that line, "On Questionable's list (#3) · Questionable has a path" when Questionable
/// knows (<see cref="QuestionableActions.BadgeLine"/>; the list is read again when another quest is opened), and a line
/// when its unobtainable or active-event answer differs from Tsukimichi ("Questionable says it can no longer be done").
/// </para>
/// </summary>
public sealed partial class DetailPane
{
    private const string QuestionableMenuId = "##detailMoreMenu";

    private static readonly string MoreIcon = FontAwesomeIcon.EllipsisH.ToIconString();

    // The cross-check as of the last refresh, keyed like the model plus Questionable's generation (plugin list changes, path reloads).
    private uint questionableRowId = uint.MaxValue;
    private int questionableVersion = -1;
    private int questionableGeneration = -1;
    private bool questionableHandoff;
    private string? questionableLine;
    private bool questionableDisagrees;
    private bool questionableSupportsPriority;
    private bool questionableCanAdd;
    private string? questionableWiderLine;

    /// <summary>Questionable's IPC; null until the plugin attaches it, which hides the line and the hand-off.</summary>
    public QuestionableIpc? Questionable { get; set; }

    /// <summary>The shared Questionable hand-offs (1.6.0): the badge line; null hides it.</summary>
    public QuestionableActions? QuestionableActions { get; set; }

    /// <summary>Reads Settings › Integrations › "Show Questionable hand-off" (off by default); null reads as off.</summary>
    public Func<bool>? QuestionableHandoff { get; set; }

    /// <summary>
    /// The "…" menu's Questionable items are drawn: Questionable is loaded, registers the priority gate and a working
    /// reason gate, the setting is ticked and the automation level shows Questionable (1.18, A10; "Start here and keep
    /// going" follows the Start pill, which the level hides the same way). A fork without the reason gate gets no item
    /// rather than one that is always disabled.
    /// </summary>
    private bool ShowsQuestionableMore => Questionable is not null && questionableSupportsPriority && QuestionableHandoff?.Invoke() == true
        && AutomationGate.Shows(Core.Companions.AutomationButtons.Questionable);

    /// <summary>Re-asks Questionable when the quest, the session version or Dalamud's plugin list changed, or Questionable reloaded its paths; otherwise free.</summary>
    private void RefreshQuestionable(SessionState session, QuestRecord quest)
    {
        if (Questionable is not { } questionable)
        {
            questionableLine = null;
            questionableSupportsPriority = false;
            return;
        }

        var available = questionable.Available;
        var generation = questionable.Generation;
        var handoff = QuestionableHandoff?.Invoke() == true;
        if (questionableRowId == quest.RowId && questionableVersion == session.Version && questionableGeneration == generation && questionableHandoff == handoff)
        {
            return;
        }

        if (questionableRowId != quest.RowId)
        {
            // A quest opened: read Questionable's list again for its badge (once, not per frame).
            questionable.MarkListStale();
        }

        questionableRowId = quest.RowId;
        questionableVersion = session.Version;
        questionableGeneration = generation;
        questionableHandoff = handoff;
        questionableLine = null;
        questionableDisagrees = false;
        questionableSupportsPriority = false;
        questionableCanAdd = false;
        questionableWiderLine = null;
        if (!available)
        {
            return;
        }

        var result = questionable.Check(quest, session);
        questionableLine = result?.Outcome switch
        {
            CrossCheckOutcome.Agrees => result.LevelAside ? Strings.QuestionableAgreesLevelAside : Strings.QuestionableAgrees,
            CrossCheckOutcome.QuestionableLocked => string.Format(CultureInfo.CurrentCulture, Strings.QuestionableSaysFormat, result.Answer!.ReasonText),
            CrossCheckOutcome.QuestionableOpen => string.Format(CultureInfo.CurrentCulture, Strings.QuestionableSaysFormat, Strings.QuestionableNotLocked),
            _ => null,
        };
        questionableDisagrees = result?.Disagrees == true;

        // The wider cross-check (1.6.0): Questionable's unobtainable and active-event answers, for the character logged in.
        session.States.TryGetValue(quest.RowId, out var evaluation);
        var wider = questionable.Wider(quest, evaluation, session.IsLive, session.Version, QuestionableActions?.FestivalRunning(quest) ?? false);
        questionableWiderLine = wider?.UnobtainableOutcome switch
        {
            UnobtainableOutcome.QuestionableUnobtainable => Strings.QuestionableSaysUnobtainable,
            UnobtainableOutcome.QuestionableObtainable => Strings.QuestionableSaysObtainable,
            _ => wider?.EventOutcome == EventOutcome.QuestionableListsInactive ? Strings.QuestionableSaysEventRunning : null,
        };

        // Read after the check: a reason gate found broken by it withdraws the hand-off (no "…" without a way to
        // tell a quest Questionable has a path for).
        questionableSupportsPriority = questionable.SupportsPriority;

        // Only with the hand-off on; the answer is the one Check just cached, so Questionable is not asked again.
        if (questionableSupportsPriority && handoff)
        {
            questionableCanAdd = questionable.CanAddToPriority(quest.RowId, session.Version);
        }
    }

    /// <summary>The line under the status: agreement in the secondary tone, a difference in the body tone. "Added to Questionable's priority list" goes to the status bar.</summary>
    private void DrawQuestionableLine(SessionState session, QuestRecord quest)
    {
        RefreshQuestionable(session, quest);
        if (questionableLine is { } line)
        {
            using (Theme.PushText(questionableDisagrees ? Theme.Surface.Text : Theme.Surface.TextSecondary))
            {
                TextFlow.Wrapped(line, RoomTo(bodyRight));
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.QuestionableLineTooltip);
            }
        }

        if (questionableWiderLine is { } wider)
        {
            using (Theme.PushText(Theme.Surface.Text))
            {
                TextFlow.Wrapped(wider, RoomTo(bodyRight));
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.QuestionableLineTooltip);
            }
        }

        if (QuestionableActions?.BadgeLine(quest.RowId) is { Length: > 0 } badges)
        {
            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                TextFlow.Wrapped(badges, RoomTo(bodyRight));
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.QuestionableBadgesTooltip);
            }
        }
    }

    /// <summary>
    /// The round "…" button and its menu, last on the action bar (always shown since 1.8.0): first the travel and
    /// automation pills the row had no room for (1.10), then "Start here and keep going" or, while Questionable runs,
    /// "Stop later" (1.18.0, <see cref="DrawQuestionableRunItems"/>), then "Open on…" (the quest's
    /// page on the Lodestone, Garland Tools, the wiki or Teamcraft; a masked quest asks first), then, when
    /// <see cref="ShowsQuestionableMore"/>, "Add to Questionable priority", which calls Questionable's own
    /// <c>AddQuestPriority</c> gate and is disabled, saying why, for a quest Questionable has no path for, and "Do this
    /// next" (1.18.0), the same quest first on its list, also disabled while Questionable runs.
    /// </summary>
    private void DrawMoreMenu(ref float used, float width, QuestRecord quest, uint rowId)
    {
        NextRound(ref used, width);
        if (Chrome.IconButtonRound("##more", MoreIcon, Strings.QuestionableMoreTooltip))
        {
            ImGui.OpenPopup(QuestionableMenuId);
        }

        using var popup = ImRaii.Popup(QuestionableMenuId);
        if (!popup)
        {
            return;
        }

        // The travel and automation pills the first row had no room for (1.10), then "Open on…".
        if (DrawOverflowActions(quest, rowId))
        {
            ImGui.Separator();
        }

        if (DrawQuestionableRunItems(rowId))
        {
            ImGui.Separator();
        }

        var spoilers = runner.Spoilers;
        links.DrawOpenOnMenu(quest, spoilers.IsMasked(quest), spoilers.DisplayName(quest));
        DrawGameAnswerMenuItem(quest);

        // The menu opens after a refresh with the hand-off on, so questionableCanAdd is current.
        if (!ShowsQuestionableMore || Questionable is not { } questionable || questionableRowId != rowId)
        {
            return;
        }

        ImGui.Separator();

        if (ImGui.MenuItem(Strings.QuestionableAddToPriority, enabled: questionableCanAdd))
        {
            var added = questionable.AddToPriority(rowId);
            ShowCompanionNote(added ? Strings.QuestionableAdded : Strings.QuestionableAddFailed);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(questionableCanAdd ? Strings.QuestionableAddToPriorityTooltip : Strings.QuestionableAddToPriorityNoPath);
        }

        // "Do this next" (feature plan v7 A6): first on Questionable's list, only for a quest it has a path for, and not
        // while it runs (Questionable#45).
        var doNextBlocker = !questionableCanAdd
            ? Strings.QuestionableAddToPriorityNoPath
            : QuestionableActions is { } actions ? actions.DoThisNextBlocker() : Strings.QuestionableDoNextNoGate;
        if (ImGui.MenuItem(Strings.QuestionableDoNext, enabled: doNextBlocker is null))
        {
            QuestionableActions?.DoThisNext(rowId);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(doNextBlocker ?? Strings.QuestionableDoNextTooltip);
        }
    }

    /// <summary>
    /// The Questionable run items of the "…" menu (feature plan v7 A4, A6): "Start here and keep going" (the Start
    /// pill's behaviour before 1.18.0) while the pill offers Start and Questionable can do a single quest, or "Stop
    /// later" while it runs. True when anything was drawn.
    /// </summary>
    private bool DrawQuestionableRunItems(uint rowId)
    {
        if (QuestionableActions is not { } questionable)
        {
            return false;
        }

        var index = -1;
        for (var i = 0; i < actionCount && index < 0; i++)
        {
            if (actions[i].Kind == ActionKind.Questionable)
            {
                index = i;
            }
        }

        if (index < 0)
        {
            return false;
        }

        if (actions[index].Stop)
        {
            if (questionable.Runs is null || !questionable.Running)
            {
                return false;
            }

            questionable.DrawStopLaterMenu();
            return true;
        }

        // Without the single-quest gate the pill itself keeps going: no second item for the same thing.
        if (!questionable.Ipc.CanStartSingle)
        {
            return false;
        }

        if (ImGui.MenuItem(Strings.QuestionableKeepGoing, string.Empty, false, actions[index].Enabled))
        {
            questionable.StartKeepGoing(MainWindow.QuestionableHost, rowId);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(questionableBlocker ?? Strings.ActionQuestionableStartTooltip);
        }

        return true;
    }
}
