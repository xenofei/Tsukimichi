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
/// Integrations › "Show Questionable hand-off" is ticked and Questionable offers the gate, a round "…" button at the
/// end of the action bar whose menu holds "Add to Questionable priority". Questionable is asked when the selection,
/// the session version or Questionable's load state changes, never per frame; nothing shows while it is not loaded.
/// </summary>
public sealed partial class DetailPane
{
    private const string QuestionableMenuId = "##detailMoreMenu";
    private const double QuestionableNoteSeconds = 5.0;

    private static readonly string MoreIcon = FontAwesomeIcon.EllipsisH.ToIconString();

    // The cross-check as of the last refresh, keyed like the model plus Questionable's load generation.
    private uint questionableRowId = uint.MaxValue;
    private int questionableVersion = -1;
    private int questionableGeneration = -1;
    private bool questionableHandoff;
    private string? questionableLine;
    private bool questionableDisagrees;
    private bool questionableSupportsPriority;
    private bool questionableCanAdd;

    // "Added to Questionable's priority list" in place of the line for a few seconds after the hand-off.
    private string? questionableNote;
    private double questionableNoteUntil;
    private uint questionableNoteRowId;

    /// <summary>Questionable's IPC; null until the plugin attaches it, which hides the line and the hand-off.</summary>
    public QuestionableIpc? Questionable { get; set; }

    /// <summary>Reads Settings › Integrations › "Show Questionable hand-off" (off by default); null reads as off.</summary>
    public Func<bool>? QuestionableHandoff { get; set; }

    /// <summary>The "…" button is drawn: Questionable is loaded, registers the priority gate, and the setting is ticked.</summary>
    private bool ShowsQuestionableMore => Questionable is not null && questionableSupportsPriority && QuestionableHandoff?.Invoke() == true;

    /// <summary>Re-asks Questionable when the quest, the session version or its load state changed; otherwise free.</summary>
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

        questionableRowId = quest.RowId;
        questionableVersion = session.Version;
        questionableGeneration = generation;
        questionableHandoff = handoff;
        questionableLine = null;
        questionableDisagrees = false;
        questionableSupportsPriority = available && questionable.SupportsPriority;
        questionableCanAdd = false;
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
            ImGui.TextWrapped(note);
            return;
        }

        if (questionableLine is not { } line)
        {
            return;
        }

        using (Theme.PushText(questionableDisagrees ? Theme.Surface.Text : Theme.Surface.TextSecondary))
        {
            ImGui.TextWrapped(line);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.QuestionableLineTooltip);
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
