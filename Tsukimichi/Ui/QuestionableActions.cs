using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Config;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Model;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The Questionable hand-offs every pane shares (feature plan v5, 1.6.0, decision 1): "Send to Questionable" with its
/// menu (Add to Questionable's list, Add and start, Replace Questionable's list…, Stop while it runs), the two
/// confirmations, the result line in chat, the live status text, the row Questionable works on, and the "on its list"
/// and "has a path" badges. A pane draws a button (<see cref="DrawButton{T}"/>, <see cref="DrawIconButton{T}"/>) or a
/// submenu of its own context menu (<see cref="DrawSubmenu{T}"/>) with the quests it would send (a state and a static
/// function of it, so drawing the button allocates nothing; the quests are listed only when a menu opens), and calls
/// <see cref="DrawModals"/> once per frame at its window's root with its own host name, where the confirmations open.
/// <para>
/// Without Questionable every button and item stays visible, disabled, saying the plugin is needed. Start also needs
/// Questionable's own required plugins (vnavmesh, TextAdvance, Lifestream) and names the missing ones. The quests sent
/// are worked out when a menu opens (<see cref="QuestionableList.Plan"/>: done, in-journal and locked-out quests left
/// out, order kept), not per frame, from the logged-in character's states, since Questionable plays that character
/// whoever is viewed; "Add and start" also waits until the viewed character is the one logged in.
/// </para>
/// </summary>
public sealed class QuestionableActions
{
    private const string MenuId = "##questionableSend";
    private static readonly string SendIcon = Chrome.Icon(FontAwesomeIcon.PaperPlane);

    private readonly QuestionableIpc ipc;
    private readonly SessionState session;
    private readonly Configuration settings;
    private readonly Action save;
    private readonly Action<string> print;

    // The plan of the menu last opened, by the ImGui id of its button and the session version it was built for.
    private uint planId;
    private int planVersion = -1;
    private int planGeneration = -1;
    private int planLanguage = -1;
    private int planFrame = int.MinValue / 2;
    private QuestionableSendPlan plan = QuestionableSendPlan.Empty;
    private string appendLabel = string.Empty;
    private string startLabel = string.Empty;
    private string skippedText = string.Empty;

    // A confirmation waiting to open in its host window.
    private PendingKind pending;
    private string pendingHost = string.Empty;
    private bool pendingOpen;
    private QuestionableSendPlan pendingPlan = QuestionableSendPlan.Empty;
    private string pendingQuestion = string.Empty;
    private bool dontAskAgain = true;

    // The status as last polled, and its text.
    private QuestionableStatus status = QuestionableStatus.Idle;
    private QuestionableStatus? statusFor;
    private int statusVersion = -1;
    private int statusLanguage = -1;
    private string statusText = string.Empty;

    // Badge texts, rebuilt when what they show changes.
    private (uint RowId, int? Position, bool? Path, int Language) badgeKey = (0, null, null, -1);
    private string badgeText = string.Empty;
    private readonly Dictionary<int, string> markTexts = [];
    private readonly Dictionary<int, string> markTooltips = [];
    private int markLanguage = -1;

    public QuestionableActions(QuestionableIpc ipc, SessionState session, Configuration settings, Action save, Action<string> print)
    {
        this.ipc = ipc ?? throw new ArgumentNullException(nameof(ipc));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.save = save ?? throw new ArgumentNullException(nameof(save));
        this.print = print ?? throw new ArgumentNullException(nameof(print));
    }

    private enum PendingKind : byte
    {
        None,
        Replace,
        Start,
    }

    /// <summary>Questionable's IPC.</summary>
    public QuestionableIpc Ipc => ipc;

    /// <summary>
    /// The states the quests sent are judged by: the logged-in character's, whoever is viewed, since Questionable plays
    /// that character; the viewed character's only while nobody is logged in.
    /// </summary>
    private IReadOnlyDictionary<uint, QuestEvaluation> ActingStates => session.LiveContentId is not null ? session.LiveStates : session.States;

    /// <summary>
    /// Whether the game's festival flags have the quest's seasonal event running for the character logged in (false
    /// for a quest of no event, and while a stored character is viewed): Tsukimichi's side of the event cross-check.
    /// </summary>
    public bool FestivalRunning(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        if (quest.Festival == 0 || !session.IsLive || session.ViewedSnapshot is not { } snapshot)
        {
            return false;
        }

        foreach (var id in snapshot.ActiveFestivals)
        {
            if (id == quest.Festival)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The quest Questionable works on as last polled, when it runs; null otherwise. Does not ask Questionable.</summary>
    public uint? RunningRowId => status.Running ? status.RowId : null;

    /// <summary>
    /// Polls Questionable's live status (at most once a second, inside <see cref="QuestionableIpc.PollStatus"/>) and
    /// returns its text, or null when it is not running. Call only from a visible window's draw.
    /// </summary>
    public string? PollStatusText()
    {
        status = ipc.PollStatus();
        if (!status.Running)
        {
            return null;
        }

        if (!ReferenceEquals(statusFor, status) || statusVersion != session.Version || statusLanguage != Localization.Loc.Version)
        {
            statusFor = status;
            statusVersion = session.Version;
            statusLanguage = Localization.Loc.Version;
            statusText = ComposeStatus(status);
        }

        return statusText;
    }

    /// <summary>
    /// A small "Stop" button for a status line; disabled with the reason on a Questionable that has no Stop gate (the
    /// WigglyMuffin fork).
    /// </summary>
    public void DrawStopSmallButton(string id)
    {
        var canStop = ipc.CanStop;
        using (ImRaii.PushId(id))
        using (ImRaii.Disabled(!canStop))
        {
            if (ImGui.SmallButton(Strings.QuestionableStopShort))
            {
                DoStop();
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(canStop ? Strings.QuestionableStopTooltip : Strings.QuestionableStopNoGate);
        }
    }

    /// <summary>"Send to Questionable" as a text button opening the menu; visible and disabled without Questionable.</summary>
    public void DrawButton<T>(string host, string id, T state, Func<T, IEnumerable<uint>> rowIds)
    {
        ArgumentNullException.ThrowIfNull(rowIds);
        using var scope = ImRaii.PushId(id);
        var available = ipc.Available;
        using (ImRaii.Disabled(!available))
        {
            if (ImGui.Button(Strings.QuestionableSendButton))
            {
                ImGui.OpenPopup(MenuId);
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(available ? Strings.QuestionableSendTooltip : Strings.QuestionableNeedsPlugin);
        }

        DrawMenuPopup(host, state, rowIds);
    }

    /// <summary>The width <see cref="DrawButton{T}"/> takes.</summary>
    public static float ButtonWidth => ImGui.CalcTextSize(Strings.QuestionableSendButton).X + (ImGui.GetStyle().FramePadding.X * 2f);

    /// <summary>A round paper-plane button opening the menu (<see cref="UiMetrics.MinTarget"/> across); disabled without Questionable.</summary>
    public void DrawIconButton<T>(string host, string id, T state, Func<T, IEnumerable<uint>> rowIds, string tooltip)
    {
        ArgumentNullException.ThrowIfNull(rowIds);
        using var scope = ImRaii.PushId(id);
        var available = ipc.Available;
        if (Chrome.IconButtonRound("##send", SendIcon, available ? tooltip : Strings.QuestionableNeedsPlugin, enabled: available))
        {
            ImGui.OpenPopup(MenuId);
        }

        DrawMenuPopup(host, state, rowIds);
    }

    /// <summary>
    /// A submenu <paramref name="label"/> inside an open popup or context menu, holding the send items; a disabled item
    /// saying Questionable is needed when it is not loaded.
    /// </summary>
    public void DrawSubmenu<T>(string host, string label, T state, Func<T, IEnumerable<uint>> rowIds)
    {
        ArgumentNullException.ThrowIfNull(rowIds);
        if (!ipc.Available)
        {
            ImGui.MenuItem(label, string.Empty, false, false);
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(Strings.QuestionableNeedsPlugin);
            }

            return;
        }

        // The id is taken in the calling popup, which is per row; inside the submenu every row's menu would share one.
        var id = ImGui.GetID(label);
        using var menu = ImRaii.Menu(label);
        if (menu)
        {
            DrawItems(host, id, state, rowIds);
        }
    }

    /// <summary>
    /// The confirmations (Replace, the first Start) for the menus of <paramref name="host"/>; call once per frame at
    /// the window's root, outside any child or popup, so the modal opens where its request was made.
    /// </summary>
    public void DrawModals(string host)
    {
        if (pending == PendingKind.None || !string.Equals(pendingHost, host, StringComparison.Ordinal))
        {
            return;
        }

        var popupId = pending == PendingKind.Replace ? Strings.QuestionableReplacePopup : Strings.QuestionableStartPopup;
        if (pendingOpen)
        {
            pendingOpen = false;
            ImGui.OpenPopup(popupId);
        }

        using var modal = ImRaii.PopupModal(popupId, ImGuiWindowFlags.AlwaysAutoResize);
        if (!modal)
        {
            if (!ImGui.IsPopupOpen(popupId))
            {
                // Closed by Esc or the window going away: forget the request.
                pending = PendingKind.None;
            }

            return;
        }

        UiMetrics.ApplyFontScale();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + UiMetrics.Px(420f));
        ImGui.TextUnformatted(pendingQuestion);
        ImGui.PopTextWrapPos();
        ImGui.Spacing();
        if (pending == PendingKind.Start)
        {
            ImGui.Checkbox(Strings.QuestionableStartDontAsk, ref dontAskAgain);
            ImGui.Spacing();
        }

        var confirmLabel = pending == PendingKind.Replace ? Strings.QuestionableReplaceConfirm : Strings.QuestionableStartConfirm;
        bool confirmed;
        using (Theme.PushDestructiveButton(pending == PendingKind.Replace))
        {
            confirmed = ImGui.Button(confirmLabel);
        }

        ImGui.SameLine();
        var cancelled = ImGui.Button(Strings.QuestionableCancel);
        if (confirmed)
        {
            var kind = pending;
            var sendPlan = pendingPlan;
            pending = PendingKind.None;
            ImGui.CloseCurrentPopup();
            if (kind == PendingKind.Replace)
            {
                DoSend(sendPlan, replace: true, start: false);
            }
            else
            {
                if (dontAskAgain && settings.QuestionableConfirmStart)
                {
                    settings.QuestionableConfirmStart = false;
                    save();
                }

                DoSend(sendPlan, replace: false, start: true);
            }
        }
        else if (cancelled)
        {
            pending = PendingKind.None;
            ImGui.CloseCurrentPopup();
        }
    }

    /// <summary>
    /// "On Questionable's list (#3) · Questionable has a path" for the detail pane, empty when nothing is known. Reads
    /// the list and the path answer through their caches (<see cref="QuestionableBadges"/>).
    /// </summary>
    public string BadgeLine(uint rowId)
    {
        if (!ipc.Available)
        {
            return string.Empty;
        }

        var position = ipc.ListPosition(rowId);
        var path = ipc.HasPath(rowId, ImGui.GetFrameCount());
        var key = (rowId, position, path, Localization.Loc.Version);
        if (key == badgeKey)
        {
            return badgeText;
        }

        badgeKey = key;
        var parts = new List<string>(2);
        if (position is { } place)
        {
            parts.Add(string.Format(CultureInfo.CurrentCulture, Strings.QuestionableOnListFormat, place));
        }

        if (path is { } has)
        {
            parts.Add(has ? Strings.QuestionableHasPath : Strings.QuestionableNoPath);
        }

        badgeText = string.Join(" · ", parts);
        return badgeText;
    }

    /// <summary>
    /// The small mark a route step shows: "Q #3" when the quest is on Questionable's list, "Q no path" when Questionable
    /// has no path for it, empty otherwise, with its tooltip. Path answers are asked lazily, a few per frame.
    /// </summary>
    public (string Text, string Tooltip) StepMark(uint rowId)
    {
        if (!ipc.Available)
        {
            return (string.Empty, string.Empty);
        }

        if (markLanguage != Localization.Loc.Version)
        {
            markLanguage = Localization.Loc.Version;
            markTexts.Clear();
            markTooltips.Clear();
        }

        if (ipc.ListPosition(rowId) is { } position)
        {
            if (!markTexts.TryGetValue(position, out var text))
            {
                text = string.Format(CultureInfo.CurrentCulture, Strings.QuestionableMarkListFormat, position);
                markTexts[position] = text;
                markTooltips[position] = string.Format(CultureInfo.CurrentCulture, Strings.QuestionableMarkListTooltipFormat, position);
            }

            return (text, markTooltips[position]);
        }

        return ipc.HasPath(rowId, ImGui.GetFrameCount()) == false
            ? (Strings.QuestionableMarkNoPath, Strings.QuestionableMarkNoPathTooltip)
            : (string.Empty, string.Empty);
    }

    private void DrawMenuPopup<T>(string host, T state, Func<T, IEnumerable<uint>> rowIds)
    {
        if (!ImGui.IsPopupOpen(MenuId))
        {
            return;
        }

        using var style = Theme.PushPopup();
        using var popup = ImRaii.Popup(MenuId);
        if (!popup)
        {
            return;
        }

        UiMetrics.ApplyFontScale();
        DrawItems(host, ImGui.GetID(MenuId), state, rowIds);
    }

    /// <summary>The menu's items: Add, Add and start, Replace…, and Stop while Questionable runs.</summary>
    private void DrawItems<T>(string host, uint id, T state, Func<T, IEnumerable<uint>> rowIds)
    {
        var sendPlan = PlanFor(id, state, rowIds);
        if (sendPlan.Count == 0)
        {
            ImGui.TextDisabled(Strings.QuestionableNothingToSend);
        }

        var canSend = ipc.CanSend && sendPlan.Count > 0;
        if (ImGui.MenuItem(appendLabel, string.Empty, false, canSend))
        {
            DoSend(sendPlan, replace: false, start: false);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.QuestionableSendAppendTooltip, skippedText.Length > 0 ? skippedText : null);
        }

        var startBlocker = StartBlocker();
        if (ImGui.MenuItem(startLabel, string.Empty, false, canSend && startBlocker is null))
        {
            RequestStart(host, sendPlan);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(startBlocker ?? Strings.QuestionableSendStartTooltip);
        }

        var canReplace = ipc.CanReplace;
        if (ImGui.MenuItem(Strings.QuestionableReplace, string.Empty, false, canReplace && sendPlan.Count > 0))
        {
            RequestReplace(host, sendPlan);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(canReplace ? Strings.QuestionableReplaceTooltip : Strings.QuestionableReplaceNoGate);
        }

        if (!status.Running)
        {
            return;
        }

        ImGui.Separator();
        var canStop = ipc.CanStop;
        if (ImGui.MenuItem(Strings.QuestionableStop, string.Empty, false, canStop))
        {
            DoStop();
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(canStop ? Strings.QuestionableStopTooltip : Strings.QuestionableStopNoGate);
        }
    }

    /// <summary>Why "Add and start" is disabled, or null when it can be offered.</summary>
    private string? StartBlocker()
    {
        if (!settings.QuestionableAllowStart)
        {
            return Strings.QuestionableStartOffSetting;
        }

        if (!ipc.CanStart)
        {
            return Strings.QuestionableStartNoGate;
        }

        if (ipc.MissingRequiredPlugins is { Count: > 0 } missing)
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.QuestionableStartMissingFormat, string.Join(", ", missing));
        }

        // Questionable plays the character logged in; starting it while a stored character is viewed would set off
        // quests chosen for someone else.
        if (!session.IsLive)
        {
            return Strings.QuestionableStartNotLive;
        }

        return status.Running ? Strings.QuestionableAlreadyRunning : null;
    }

    /// <summary>The plan for the menu with ImGui id <paramref name="id"/>, rebuilt when another menu opens or the session or language changed.</summary>
    private QuestionableSendPlan PlanFor<T>(uint id, T state, Func<T, IEnumerable<uint>> rowIds)
    {
        // Rebuilt each time a menu opens (it was not drawn the frame before), so pins or a route changed since are seen.
        var frame = ImGui.GetFrameCount();
        var stillOpen = frame - planFrame <= 1;
        planFrame = frame;
        if (stillOpen && id == planId && planVersion == session.Version && planGeneration == ipc.Generation && planLanguage == Localization.Loc.Version)
        {
            return plan;
        }

        planId = id;
        planVersion = session.Version;
        planGeneration = ipc.Generation;
        planLanguage = Localization.Loc.Version;
        IEnumerable<uint> rows;
        try
        {
            rows = rowIds(state);
        }
        catch (InvalidOperationException)
        {
            rows = [];
        }

        plan = QuestionableList.Plan(rows, ActingStates);
        appendLabel = string.Format(CultureInfo.CurrentCulture, Strings.QuestionableSendAppendFormat, plan.Count);
        startLabel = string.Format(CultureInfo.CurrentCulture, Strings.QuestionableSendStartFormat, plan.Count);
        skippedText = plan.Skipped > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.QuestionableSkippedFormat, plan.Skipped) : string.Empty;
        return plan;
    }

    private void RequestReplace(string host, QuestionableSendPlan sendPlan)
    {
        var onList = ipc.ListQuestCount;
        pendingQuestion = onList is { } count
            ? string.Format(CultureInfo.CurrentCulture, Strings.QuestionableReplaceQuestionFormat, count, sendPlan.Count)
            : string.Format(CultureInfo.CurrentCulture, Strings.QuestionableReplaceQuestionUnknownFormat, sendPlan.Count);
        Request(PendingKind.Replace, host, sendPlan);
    }

    private void RequestStart(string host, QuestionableSendPlan sendPlan)
    {
        if (!settings.QuestionableConfirmStart)
        {
            DoSend(sendPlan, replace: false, start: true);
            return;
        }

        var first = PreferredStart(sendPlan, null);
        pendingQuestion = string.Format(CultureInfo.CurrentCulture, Strings.QuestionableStartQuestionFormat, first is { } rowId ? NameOf(rowId) : string.Empty);
        dontAskAgain = true;
        Request(PendingKind.Start, host, sendPlan);
    }

    private void Request(PendingKind kind, string host, QuestionableSendPlan sendPlan)
    {
        pending = kind;
        pendingHost = host;
        pendingPlan = sendPlan;
        pendingOpen = true;
    }

    /// <summary>Sends, says how it went in chat, and starts Questionable when asked.</summary>
    private void DoSend(QuestionableSendPlan sendPlan, bool replace, bool start)
    {
        var result = ipc.Send(sendPlan, replace, out var replaced);
        if (result is null)
        {
            print(replaced switch
            {
                QuestionableReplaceOutcome.NotRead => Strings.QuestionableReplaceNotRead,
                QuestionableReplaceOutcome.FailedRestored => Strings.QuestionableReplaceRestored,
                QuestionableReplaceOutcome.FailedNotRestored => Strings.QuestionableReplaceNotRestored,
                _ => Strings.QuestionableSendFailed,
            });
            return;
        }

        print(ResultLine(result));
        if (!start || result.OnList == 0)
        {
            return;
        }

        if (PreferredStart(sendPlan, result) is { } rowId && ipc.Start(rowId))
        {
            print(string.Format(CultureInfo.CurrentCulture, Strings.QuestionableStartedFormat, NameOf(rowId)));
        }
        else
        {
            print(Strings.QuestionableStartFailed);
        }
    }

    private void DoStop() => print(ipc.Stop() ? Strings.QuestionableStopped : Strings.QuestionableStopFailed);

    /// <summary>"Questionable: sent 14 of 17 (3 have no Questionable path)." and how many were already on its list.</summary>
    private static string ResultLine(QuestionableSendResult result)
    {
        string line;
        if (!result.Verified)
        {
            line = string.Format(CultureInfo.CurrentCulture, Strings.QuestionableSentUnverifiedFormat, result.Sent);
        }
        else if (result.OnList == 0)
        {
            line = string.Format(CultureInfo.CurrentCulture, Strings.QuestionableSentNoneFormat, result.Sent);
        }
        else if (result.NoPath > 0)
        {
            line = string.Format(CultureInfo.CurrentCulture, Strings.QuestionableSentFormat, result.OnList, result.Sent, result.NoPath);
        }
        else
        {
            line = string.Format(CultureInfo.CurrentCulture, Strings.QuestionableSentAllFormat, result.Sent);
        }

        return result.AlreadyThere > 0
            ? line + " " + string.Format(CultureInfo.CurrentCulture, Strings.QuestionableAlreadyOnFormat, result.AlreadyThere)
            : line;
    }

    /// <summary>
    /// The quest to start Questionable on: the first one, in the order sent, that Questionable took (all of them when
    /// the list was not read back), preferring one the character can pick up now. Null when none was taken.
    /// </summary>
    private uint? PreferredStart(QuestionableSendPlan sendPlan, QuestionableSendResult? result)
    {
        uint? first = null;
        foreach (var rowId in sendPlan.RowIds)
        {
            if (result is { Verified: true } && !result.Positions.ContainsKey(rowId))
            {
                continue;
            }

            if (ActingStates.TryGetValue(rowId, out var evaluation) && evaluation.State == QuestState.Ready)
            {
                return rowId;
            }

            first ??= rowId;
        }

        return first;
    }

    private string NameOf(uint rowId) =>
        session.Bundle?.Catalog is { } catalog
            ? session.Spoilers.DisplayName(catalog, rowId, rowId.ToString(CultureInfo.InvariantCulture))
            : rowId.ToString(CultureInfo.InvariantCulture);

    /// <summary>"Questionable: running · Brotherhood of Ash · step 3 of 7".</summary>
    private string ComposeStatus(QuestionableStatus live)
    {
        if (live.RowId is not { } rowId)
        {
            return Strings.QuestionableStatusRunning;
        }

        var name = NameOf(rowId);
        if (live.Sequence is not { } sequence)
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.QuestionableStatusQuestFormat, name);
        }

        var stepCount = session.Bundle?.Catalog.GetByRowId(rowId)?.StepCount ?? 0;
        var step = sequence == 0 ? Strings.QuestionableStatusPickUp : BlockerText.StepText(sequence, stepCount);
        return string.Format(CultureInfo.CurrentCulture, Strings.QuestionableStatusStepFormat, name, step);
    }
}
