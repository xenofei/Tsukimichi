using System;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// "Stop later" (feature plan v7 A4): stop a running Questionable after the quest under way, after a number of quests,
/// or at a time, whoever started it. The items sit in every menu that offers Stop while Questionable runs (the Send to
/// Questionable menus and the detail pane's "…" menu) and on a right-click of the status line's Stop. The run watch
/// (<see cref="Runs"/>) holds the condition, polls Questionable while it is set and calls Stop when it is met; the status
/// line says what is set ("stops after 2 more quests"). Questionable's own "Stop after current quest" toggle cannot be
/// reached over IPC, so this is Tsukimichi's. Nothing here without a Stop gate (the WigglyMuffin fork): the items show
/// disabled, saying why.
/// </summary>
public sealed partial class QuestionableActions
{
    private const string StopLaterContextId = "##questionableStopLater";

    // The values typed in the menu, kept while it is closed.
    private int stopAfterQuests = 5;
    private string stopAtText = string.Empty;

    /// <summary>"Stop later" as a submenu of an open popup; disabled, saying why, without a Stop gate.</summary>
    public void DrawStopLaterMenu()
    {
        if (Runs is null || !status.Running)
        {
            return;
        }

        var canStop = ipc.CanStop;
        if (ImGui.BeginMenu(Strings.QuestionableStopLater, canStop))
        {
            // A submenu is its own popup window: it scales itself.
            UiMetrics.ApplyFontScale();
            DrawStopLaterItems();
            ImGui.EndMenu();
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            // A stop asked over IPC runs Questionable's "command after stop": a stop set for later does too.
            var after = canStop && CommandAfterStop?.Invoke() is { } command
                ? string.Format(CultureInfo.CurrentCulture, Strings.QuestionableStopCommandFormat, CommandText(command))
                : null;
            UiMetrics.Tooltip(canStop ? Strings.QuestionableStopLaterTooltip : Strings.QuestionableStopNoGate, after);
        }
    }

    /// <summary>The right-click menu of the last item drawn (a status line's Stop), holding the "Stop later" items.</summary>
    private void DrawStopLaterContext(string id)
    {
        if (Runs is null || !ipc.CanStop)
        {
            return;
        }

        var popupId = id + StopLaterContextId;
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            ImGui.OpenPopup(popupId);
        }

        if (!ImGui.IsPopupOpen(popupId))
        {
            return;
        }

        using var style = Theme.PushPopup();
        using var popup = ImRaii.Popup(popupId);
        if (!popup)
        {
            return;
        }

        UiMetrics.ApplyFontScale();
        ImGui.TextDisabled(Strings.QuestionableStopLater);
        DrawStopLaterItems();
    }

    private void DrawStopLaterItems()
    {
        if (Runs is not { } runs)
        {
            return;
        }

        var condition = runs.Condition;
        var current = status.RowId ?? (runs.CurrentRowId != 0 ? runs.CurrentRowId : null);

        if (ImGui.MenuItem(Strings.QuestionableStopAfterCurrent, string.Empty, condition.Rule == QuestionableStopRule.AfterCurrent))
        {
            Arm(runs, QuestionableStopCondition.AfterCurrent(current));
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(current is { } rowId
                ? string.Format(CultureInfo.CurrentCulture, Strings.QuestionableStopAfterCurrentFormat, NameOf(rowId))
                : Strings.QuestionableStopAfterNextTooltip);
        }

        ImGui.Spacing();
        ImGui.TextUnformatted(Strings.QuestionableStopAfterQuestsLabel);
        ImGui.SetNextItemWidth(UiMetrics.Px(96f));
        if (ImGui.InputInt("##stopAfterQuests", ref stopAfterQuests))
        {
            stopAfterQuests = Math.Clamp(stopAfterQuests, 1, QuestionableRunGuard.MaxQuests);
        }

        ImGui.SameLine();
        if (ImGui.SmallButton(Strings.QuestionableStopSet + "##quests"))
        {
            Arm(runs, QuestionableStopCondition.AfterQuests(stopAfterQuests));
        }

        ImGui.Spacing();
        ImGui.TextUnformatted(Strings.QuestionableStopAtLabel);
        if (stopAtText.Length == 0)
        {
            // An hour from now, to the quarter: a sensible first value to edit.
            var suggestion = DateTime.Now.AddHours(1);
            stopAtText = new DateTime(suggestion.Year, suggestion.Month, suggestion.Day, suggestion.Hour, suggestion.Minute / 15 * 15, 0, DateTimeKind.Local)
                .ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        ImGui.SetNextItemWidth(UiMetrics.Px(96f));
        ImGui.InputText("##stopAt", ref stopAtText, 8);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.QuestionableStopAtTooltip);
        }

        ImGui.SameLine();
        var parsed = QuestionableRunGuard.TryParseClock(stopAtText, out var hour, out var minute);
        using (ImRaii.Disabled(!parsed))
        {
            if (ImGui.SmallButton(Strings.QuestionableStopSet + "##at"))
            {
                Arm(runs, QuestionableStopCondition.AtTime(QuestionableRunGuard.NextAt(DateTime.Now, hour, minute).ToUniversalTime()));
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.QuestionableStopAtTooltip);
        }

        if (condition.Rule == QuestionableStopRule.None)
        {
            return;
        }

        ImGui.Separator();
        if (ImGui.MenuItem(Strings.QuestionableStopClear))
        {
            runs.Disarm();
            print(Strings.QuestionableStopCleared);
        }
    }

    /// <summary>Sets the condition and says in chat what it will do; a run the watch has not seen yet is read first.</summary>
    private void Arm(QuestionableRunWatch runs, QuestionableStopCondition condition)
    {
        if (!runs.Arm(condition))
        {
            print(Strings.QuestionableStopLaterFailed);
            return;
        }

        print(condition.Rule switch
        {
            QuestionableStopRule.AfterCurrent when condition.QuestRowId != 0 => string.Format(CultureInfo.CurrentCulture, Strings.QuestionableStopArmedAfterCurrentFormat, NameOf(condition.QuestRowId)),
            QuestionableStopRule.AfterCurrent => Strings.QuestionableStopArmedAfterNext,
            QuestionableStopRule.AfterQuests when condition.Quests == 1 => Strings.QuestionableStopArmedAfterOne,
            QuestionableStopRule.AfterQuests => string.Format(CultureInfo.CurrentCulture, Strings.QuestionableStopArmedAfterQuestsFormat, condition.Quests),
            _ => string.Format(CultureInfo.CurrentCulture, Strings.QuestionableStopArmedAtFormat, ClockText(condition.AtUtc)),
        });
    }

    /// <summary>What the status line's run suffix depends on.</summary>
    private (QuestionableStopCondition Condition, int Left, QuestionableRunOrigin Origin) RunKey() =>
        Runs is { } runs ? (runs.Condition, runs.QuestsLeft, runs.Origin) : (QuestionableStopCondition.None, 0, QuestionableRunOrigin.Elsewhere);

    /// <summary>" · stops after 2 more quests", " · stops at 21:30", " · then stops" (a single-quest run), or empty.</summary>
    private string RunSuffix()
    {
        if (Runs is not { } runs)
        {
            return string.Empty;
        }

        var condition = runs.Condition;
        var text = condition.Rule switch
        {
            QuestionableStopRule.AfterCurrent => Strings.QuestionableStatusStopsAfterThis,
            QuestionableStopRule.AfterQuests when runs.QuestsLeft <= 1 => Strings.QuestionableStatusStopsAfterOne,
            QuestionableStopRule.AfterQuests => string.Format(CultureInfo.CurrentCulture, Strings.QuestionableStatusStopsAfterFormat, runs.QuestsLeft),
            QuestionableStopRule.AtTime => string.Format(CultureInfo.CurrentCulture, Strings.QuestionableStatusStopsAtFormat, ClockText(condition.AtUtc)),
            _ when runs.Origin == QuestionableRunOrigin.SingleQuest => Strings.QuestionableStatusThenStops,
            _ => null,
        };
        return text is null ? string.Empty : " · " + text;
    }

    /// <summary>A UTC moment as the player's clock reads it ("21:30").</summary>
    internal static string ClockText(DateTime utc) => utc.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);
}
