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

    // The values typed in the menu, kept while it is closed. Until the player types a time, it is suggested afresh each
    // time the menu opens, so it never offers a time already gone.
    private int stopAfterQuests = 5;
    private string stopAtText = string.Empty;
    private bool stopAtTyped;
    private int stopLaterFrame = -10;

    // "Today at 21:30" or "Tomorrow at 02:00" under the time field, composed again when the time or the day changes.
    private DateTime stopAtWhenFor;
    private int stopAtWhenLanguage = -1;
    private string stopAtWhen = string.Empty;

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
        var now = DateTime.Now;
        var frame = ImGui.GetFrameCount();
        var opened = frame - stopLaterFrame > 1;
        stopLaterFrame = frame;
        if (stopAtText.Length == 0 || (opened && !stopAtTyped))
        {
            // An hour from now, to the quarter: a sensible first value to edit.
            var suggestion = now.AddHours(1);
            stopAtText = new DateTime(suggestion.Year, suggestion.Month, suggestion.Day, suggestion.Hour, suggestion.Minute / 15 * 15, 0, DateTimeKind.Local)
                .ToString("HH:mm", CultureInfo.InvariantCulture);
            stopAtTyped = false;
        }

        ImGui.SetNextItemWidth(UiMetrics.Px(96f));
        if (ImGui.InputText("##stopAt", ref stopAtText, 8))
        {
            stopAtTyped = stopAtText.Length > 0;
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.QuestionableStopAtTooltip);
        }

        ImGui.SameLine();
        var parsed = QuestionableRunGuard.TryParseClock(stopAtText, out var hour, out var minute);
        var at = parsed ? QuestionableRunGuard.NextAt(now, hour, minute) : default;
        using (ImRaii.Disabled(!parsed))
        {
            if (ImGui.SmallButton(Strings.QuestionableStopSet + "##at"))
            {
                Arm(runs, QuestionableStopCondition.AtTime(at.ToUniversalTime()));
                stopAtTyped = false;
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.QuestionableStopAtTooltip);
        }

        // When the typed time falls, today or tomorrow; the line keeps its place while the text does not read as a time.
        ImGui.TextDisabled(parsed ? StopAtWhen(at, now) : " ");

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
            _ when condition.AtUtc.ToLocalTime().Date > DateTime.Today => string.Format(CultureInfo.CurrentCulture, Strings.QuestionableStopArmedAtTomorrowFormat, ClockText(condition.AtUtc)),
            _ => string.Format(CultureInfo.CurrentCulture, Strings.QuestionableStopArmedAtFormat, ClockText(condition.AtUtc)),
        });
    }

    /// <summary>"Today at 21:30" or "Tomorrow at 02:00" for <paramref name="at"/> (local), composed again only when it or the language changes.</summary>
    private string StopAtWhen(DateTime at, DateTime now)
    {
        if (at != stopAtWhenFor || stopAtWhenLanguage != Localization.Loc.Version)
        {
            stopAtWhenFor = at;
            stopAtWhenLanguage = Localization.Loc.Version;
            var clock = at.ToString("t", CultureInfo.CurrentCulture);
            stopAtWhen = string.Format(CultureInfo.CurrentCulture, at.Date > now.Date ? Strings.QuestionableStopAtTomorrowFormat : Strings.QuestionableStopAtTodayFormat, clock);
        }

        return stopAtWhen;
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
