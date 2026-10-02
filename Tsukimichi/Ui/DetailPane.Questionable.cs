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
/// Integrations › "Show Questionable hand-off" is ticked and Questionable offers the gate and a working reason gate, a
/// round "…" button at the end of the action bar whose menu holds "Add to Questionable priority". Questionable is asked
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
    private const double QuestionableNoteSeconds = 5.0;

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

    // "Added to Questionable's priority list" in place of the line for a few seconds after the hand-off.
    private string? questionableNote;
    private double questionableNoteUntil;
    private uint questionableNoteRowId;

    /// <summary>Questionable's IPC; null until the plugin attaches it, which hides the line and the hand-off.</summary>
    public QuestionableIpc? Questionable { get; set; }

    /// <summary>The shared Questionable hand-offs (1.6.0): the badge line; null hides it.</summary>
    public QuestionableActions? QuestionableActions { get; set; }

    /// <summary>Reads Settings › Integrations › "Show Questionable hand-off" (off by default); null reads as off.</summary>
    public Func<bool>? QuestionableHandoff { get; set; }

    /// <summary>
    /// The "…" button is drawn: Questionable is loaded, registers the priority gate and a working reason gate, and the
    /// setting is ticked. A fork without the reason gate gets no button rather than one whose item is always disabled.
    /// </summary>
    private bool ShowsQuestionableMore => Questionable is not null && questionableSupportsPriority && QuestionableHandoff?.Invoke() == true;

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

    /// <summary>The line under the status: agreement in the secondary tone, a difference in the body tone; the hand-off note for a few seconds after it.</summary>
    private void DrawQuestionableLine(SessionState session, QuestRecord quest)
    {
        RefreshQuestionable(session, quest);
        if (questionableNote is { } note && questionableNoteRowId == quest.RowId && ImGui.GetTime() < questionableNoteUntil)
        {
            using var mist = Theme.PushText(Theme.Surface.TextSecondary);
            TextFlow.Wrapped(note, RoomTo(bodyRight));
            return;
        }

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
    /// The round "…" button and its menu, last on the action bar, when <see cref="ShowsQuestionableMore"/>. The menu's
    /// one item calls Questionable's own <c>AddQuestPriority</c> gate; it is disabled, saying why, for a quest
    /// Questionable has no path for.
    /// </summary>
    private void DrawQuestionableMore(ref float used, float width, uint rowId)
    {
        if (!ShowsQuestionableMore || Questionable is not { } questionable)
        {
            return;
        }

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

        // The menu opens after a refresh with the hand-off on, so questionableCanAdd is current.
        if (questionableRowId != rowId)
        {
            return;
        }

        if (ImGui.MenuItem(Strings.QuestionableAddToPriority, enabled: questionableCanAdd))
        {
            var added = questionable.AddToPriority(rowId);
            questionableNote = added ? Strings.QuestionableAdded : Strings.QuestionableAddFailed;
            questionableNoteUntil = ImGui.GetTime() + QuestionableNoteSeconds;
            questionableNoteRowId = rowId;
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(questionableCanAdd ? Strings.QuestionableAddToPriorityTooltip : Strings.QuestionableAddToPriorityNoPath);
        }
    }
}
