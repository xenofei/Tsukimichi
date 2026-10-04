using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Characters;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Route;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Ui;

/// <summary>
/// Alt goals (plan v7, 1.21.0 N11; spec-1.21 N11): the "Set a goal" popover the roster's Goal column opens ("Goal for
/// Kiri · Catch this character up to…": the story up to a patch, another character's unlocks, flying in an expansion,
/// every duty roulette open, each with its picker, and a preview line counting what the goal adds before it is set),
/// and the goal card on that character's dashboard: what is left, Ready first, with the hand-off where this client may
/// make it (Send N to Questionable at Full hand-offs, the character logged in here) and the reason in words where it
/// may not. Setting and clearing a goal both have the 8-second Undo. Goals live in <c>user\characters.json</c>, so
/// any client may set one for any character.
/// </summary>
public sealed partial class CharactersPane
{
    private const int GoalCardRows = 4;
    private const int GoalCardMaxRows = 25;

    // The popover: whose goal, the existing one, and the choice being made.
    private ulong goalFor;
    private string goalForName = string.Empty;
    private AltGoal? goalBefore;
    private int goalKind;
    private int goalPatch;
    private int goalOther;
    private int goalExpansion;

    // The goal card's "N more" toggle, per character.
    private ulong goalCardAll;

    private void OpenGoal(RosterSource board, RosterRow row)
    {
        goalFor = row.ContentId;
        goalForName = GoalText.FirstName(row.Name);
        goalBefore = row.Goal;
        goalKind = 0;
        goalPatch = Math.Max(0, board.StoryPatches.Count - 1);
        goalOther = 0;
        goalExpansion = Math.Max(0, board.FlyingExpansions.Count - 1);
        if (row.Goal is { } goal)
        {
            goalKind = goal.Kind switch
            {
                AltGoalKind.MatchCharacter => 1,
                AltGoalKind.Flying => 2,
                AltGoalKind.Roulettes => 3,
                _ => 0,
            };
            var patch = IndexOf(board.StoryPatches.Select(static p => p.Patch), goal.Patch ?? string.Empty);
            goalPatch = patch >= 0 ? patch : goalPatch;
            var other = IndexOf(board.Others(goalFor).Select(static r => r.ContentId), goal.Other ?? 0UL);
            goalOther = Math.Max(0, other);
            var expansion = IndexOf(board.FlyingExpansions.Select(static e => e.Expansion), goal.Expansion ?? byte.MaxValue);
            goalExpansion = expansion >= 0 ? expansion : goalExpansion;
        }

        openGoalPopup = true;
    }

    private static int IndexOf<T>(IEnumerable<T> items, T value)
    {
        var i = 0;
        foreach (var item in items)
        {
            if (EqualityComparer<T>.Default.Equals(item, value))
            {
                return i;
            }

            i++;
        }

        return -1;
    }

    /// <summary>The goal the popover's choice makes; null when its picker has nothing to choose.</summary>
    private AltGoal? ChosenGoal(RosterSource board)
    {
        switch (goalKind)
        {
            case 0:
                var patches = board.StoryPatches;
                return patches.Count == 0 ? null : AltGoal.Story(patches[Math.Clamp(goalPatch, 0, patches.Count - 1)].Patch);
            case 1:
                var others = board.Others(goalFor).ToList();
                return others.Count == 0 ? null : AltGoal.Match(others[Math.Clamp(goalOther, 0, others.Count - 1)].ContentId);
            case 2:
                var expansions = board.FlyingExpansions;
                return expansions.Count == 0 ? null : AltGoal.Flying(expansions[Math.Clamp(goalExpansion, 0, expansions.Count - 1)].Expansion);
            default:
                return AltGoal.Roulettes();
        }
    }

    /// <summary>The Set a goal popover (opened by <see cref="OpenGoal"/>).</summary>
    private void DrawGoalPopover(RosterSource board)
    {
        using var style = Theme.PushPopup();
        using var popup = ImRaii.Popup(GoalPopupId);
        if (!popup)
        {
            return;
        }

        UiMetrics.ApplyFontScale();
        var s = Theme.Surface;
        using (Typography.Lead())
        {
            ImGui.TextUnformatted(string.Format(CultureInfo.CurrentCulture, Strings.GoalPopoverTitleFormat, goalForName));
        }

        ImGui.TextColored(s.TextSecondary, Strings.GoalPopoverSubtitle);
        ImGui.Spacing();
        var picker = UiMetrics.Px(170f);
        var labelRoom = UiMetrics.Px(220f);

        var patches = board.StoryPatches;
        GoalRadio(0, Strings.GoalKindStory, patches.Count > 0, Strings.GoalNoPatches, labelRoom);
        PickerCombo("##goalPatch", ref goalPatch, patches.Select(static p => p.Patch + " " + p.Part).ToArray(), picker);

        var others = board.Others(goalFor).ToList();
        GoalRadio(1, Strings.GoalKindMatch, others.Count > 0, Strings.GoalNoOthers, labelRoom);
        PickerCombo("##goalOther", ref goalOther, others.Select(static r => r.Name).ToArray(), picker);

        var expansions = board.FlyingExpansions;
        GoalRadio(2, Strings.GoalKindFlying, expansions.Count > 0, Strings.GoalNoFlying, labelRoom);
        PickerCombo("##goalExpansion", ref goalExpansion, expansions.Select(static e => e.Name).ToArray(), picker);

        GoalRadio(3, Strings.GoalKindRoulettes, true, string.Empty, labelRoom);
        ImGui.NewLine();

        Chrome.Hairline();
        var goal = ChosenGoal(board);
        TextFlow.Wrapped(PreviewLine(board, goal), labelRoom + picker, Theme.U32(s.TextSecondary));
        ImGui.Spacing();

        var book = board.Settings;
        using (ImRaii.Disabled(goal is null))
        {
            if (Chrome.ActionChip("##goalSet", Strings.GoalSetButton, accent: true) && goal is not null)
            {
                SetGoal(book, goalFor, goalForName, goal, goalBefore);
                ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();
        if (Chrome.ActionChip("##goalCancel", Strings.Cancel))
        {
            ImGui.CloseCurrentPopup();
        }

        if (goalBefore is { } before)
        {
            ImGui.SameLine();
            if (Chrome.ActionChip("##goalClear", Strings.GoalClear))
            {
                ClearGoal(board, goalFor, goalForName, before);
                ImGui.CloseCurrentPopup();
            }
        }
    }

    /// <summary>One radio row; disabled with its reason when its picker has nothing to choose.</summary>
    private void GoalRadio(int index, string label, bool enabled, string disabledWhy, float room)
    {
        using (ImRaii.Disabled(!enabled))
        {
            ImGui.RadioButton(label + "##goalKind" + index.ToString(CultureInfo.InvariantCulture), ref goalKind, index);
        }

        if (!enabled && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(disabledWhy);
        }

        ImGui.SameLine(room);
    }

    private static void PickerCombo(string id, ref int index, string[] items, float width)
    {
        if (items.Length == 0)
        {
            ImGui.NewLine();
            return;
        }

        index = Math.Clamp(index, 0, items.Length - 1);
        ImGui.SetNextItemWidth(width);
        ImGui.Combo(id, ref index, items, items.Length);
    }

    /// <summary>
    /// What the goal adds before it is set: "13 unlock quests Michiru has done and Kiri hasn't: 9 can be done now, 4 wait
    /// for Kiri's story."; "Nothing left: Kiri is there already."; a goal that cannot be read yet says why.
    /// </summary>
    private string PreviewLine(RosterSource board, AltGoal? goal)
    {
        if (goal is null)
        {
            return Strings.GoalPreviewNone;
        }

        if (board.Preview(goalFor, goal) is not { } progress)
        {
            return Strings.GoalBeingRead;
        }

        if (!progress.Known)
        {
            return goal.Kind == AltGoalKind.Roulettes
                ? string.Format(CultureInfo.CurrentCulture, Strings.GoalPreviewNoDutiesFormat, goalForName)
                : string.Format(CultureInfo.CurrentCulture, Strings.GoalPreviewOtherUnreadFormat, GoalText.OtherName(goal, board));
        }

        if (progress.Reached)
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.GoalPreviewReachedFormat, goalForName);
        }

        var what = goal.Kind switch
        {
            AltGoalKind.Story => string.Format(CultureInfo.CurrentCulture, Strings.GoalPreviewStoryFormat, progress.Left, goal.Patch),
            AltGoalKind.MatchCharacter => string.Format(CultureInfo.CurrentCulture, Strings.GoalPreviewMatchFormat, progress.Left, GoalText.OtherName(goal, board), goalForName),
            AltGoalKind.Flying => string.Format(CultureInfo.CurrentCulture, Strings.GoalPreviewFlyingFormat, progress.Left, board.FlyingExpansions.FirstOrDefault(e => e.Expansion == goal.Expansion).Name ?? string.Empty),
            _ => string.Format(CultureInfo.CurrentCulture, Strings.GoalPreviewRoulettesFormat, progress.Left),
        };
        var now = string.Format(CultureInfo.CurrentCulture, Strings.GoalPreviewNowFormat, progress.Doable);
        return progress.Waiting > 0
            ? what + now + string.Format(CultureInfo.CurrentCulture, Strings.GoalPreviewWaitFormat, progress.Waiting, goalForName)
            : what + now + ".";
    }

    /// <summary>Sets a goal, with the floating Undo that puts back the one before (or none).</summary>
    private static void SetGoal(CharacterSettingsBook book, ulong contentId, string name, AltGoal goal, AltGoal? before)
    {
        book.Edit(CharacterSettingChange.SetGoal(contentId, goal));
        UndoToast.Show(
            string.Format(CultureInfo.CurrentCulture, Strings.GoalSetToastFormat, name),
            () => book.Edit(CharacterSettingChange.SetGoal(contentId, before)));
    }

    /// <summary>Clears a goal, with the floating Undo that sets it again.</summary>
    private static void ClearGoal(RosterSource board, ulong contentId, string name, AltGoal before)
    {
        var book = board.Settings;
        book.Edit(CharacterSettingChange.SetGoal(contentId, null));
        UndoToast.Show(
            string.Format(CultureInfo.CurrentCulture, Strings.GoalClearedToastFormat, GoalText.FirstName(name)),
            () => book.Edit(CharacterSettingChange.SetGoal(contentId, before)));
    }

    // ------------------------------------------------------------------ the goal card

    /// <summary>
    /// The goal card on the viewed character's dashboard: "Goal: match Michiru's unlocks", what is left (Ready first,
    /// past the character's story point in the shield's words), then the actions where this client may act.
    /// </summary>
    private void DrawGoalCard(UiState ui, CharacterSnapshot snapshot)
    {
        if (Board is not { } board || board.RowOf(snapshot.ContentId) is not { Goal: { } goal } row || session.Bundle is not { } bundle)
        {
            return;
        }

        var s = Theme.Surface;
        var names = bundle.Names;
        Func<byte, string> expansion = e => names.Expansion(e);
        var first = GoalText.FirstName(row.Name);
        var title = string.Format(CultureInfo.CurrentCulture, Strings.GoalCardTitleFormat, GoalText.Phrase(goal, board, expansion));
        var cardTop = ImGui.GetCursorScreenPos();
        var cardRight = cardTop.X + ImGui.GetContentRegionAvail().X;
        Chrome.BeginCard("##goalCard", title, null, eyebrow: true);
        var caption = row.GoalProgress is { Known: true } known && !known.Reached
            ? string.Format(CultureInfo.CurrentCulture, Strings.GoalLeftFormat, known.Left)
            : GoalText.Left(row.GoalProgress);
        var heading = ImGui.GetItemRectMin();
        var captionWidth = ImGui.CalcTextSize(caption).X;
        var captionX = cardRight - UiMetrics.Px(Theme.Spacing.CardPad.X) - captionWidth;
        if (captionX > ImGui.GetItemRectMax().X + UiMetrics.Px(8f))
        {
            ImGui.GetWindowDrawList().AddText(new Vector2(captionX, heading.Y), Theme.U32(s.TextSecondary), caption);
        }

        var progress = row.GoalProgress;
        if (progress is null || !progress.Known)
        {
            TextFlow.Wrapped(GoalText.Left(progress), Chrome.RoomX(), Theme.U32(s.TextSecondary));
        }
        else if (progress.Reached)
        {
            Chrome.SemiboldText(Strings.GoalReached, s.TextSecondary);
        }
        else
        {
            DrawGoalRows(ui, progress, first);
        }

        ImGui.Spacing();
        DrawGoalActions(ui, board, row, goal, first, title);
        Chrome.EndCard();
    }

    private void DrawGoalRows(UiState ui, AltGoalProgress progress, string first)
    {
        var s = Theme.Surface;
        var spoilers = session.Spoilers;
        var states = session.States;
        var all = goalCardAll == session.ViewedContentId;
        var shown = Math.Min(progress.Quests.Count, all ? GoalCardMaxRows : GoalCardRows);
        var line = ImGui.GetTextLineHeight();
        for (var i = 0; i < shown; i++)
        {
            var quest = progress.Quests[i];
            using var id = ImRaii.PushId(i);
            var state = states.TryGetValue(quest.RowId, out var evaluation) ? evaluation.State : QuestState.Unknown;
            MoonGlyph.DrawInline(state, UiMetrics.InlineGlyphSize(line));
            ImGui.SameLine();
            var name = spoilers.DisplayName(quest);
            var masked = spoilers.IsMasked(quest);
            var detail = Strings.StateName(state, quest);
            if (GiverPortraits.Place(quest, spoilers) is { Length: > 0 } place)
            {
                detail += Strings.GoalSeparator + place;
            }

            if (!AltGoals.IsDoable(state) && state == QuestState.Blocked)
            {
                detail += Strings.GoalSeparator + string.Format(CultureInfo.CurrentCulture, Strings.GoalWaitsForStoryFormat, first);
            }

            // The name (a placeholder in Secondary past the story point), then the state and place, cut to the card.
            var text = name + "  " + detail;
            using (Theme.PushText(masked ? s.TextSecondary : s.Text))
            {
                if (Chrome.EllipsisSelectable(text, false, 0f, out var cut))
                {
                    ui.Reveal(quest);
                }

                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(cut ? text : name, Strings.TonightRowTooltip);
                }
            }
        }

        var more = progress.Quests.Count - shown;
        if (more > 0 || all)
        {
            var label = all ? Strings.GoalFewer : string.Format(CultureInfo.CurrentCulture, Strings.GoalMoreFormat, more);
            if (ImGui.SmallButton(label + "##goalMore"))
            {
                goalCardAll = all ? 0 : session.ViewedContentId ?? 0;
            }
        }
    }

    /// <summary>
    /// The card's actions by where the character is (spec-1.21 N11): logged in here at Full hand-offs, Send N to
    /// Questionable, Route and Clear goal; here below Full, Route and Clear goal with why there is no Send; live in
    /// another client or stored, the words saying where to act, and Route (a plan to read, not a run).
    /// </summary>
    private void DrawGoalActions(UiState ui, RosterSource board, RosterRow row, AltGoal goal, string first, string title)
    {
        var s = Theme.Surface;
        var progress = row.GoalProgress;
        var doable = progress?.Quests.Where(q => AltGoals.IsDoable(session.States.TryGetValue(q.RowId, out var e) ? e.State : null)).Select(static q => q.RowId).ToArray() ?? [];
        if (row.CanHandOff)
        {
            if (AutomationGate.Questionable(Questionable) is { } questionable && doable.Length > 0)
            {
                questionable.DrawPill(MainWindow.QuestionableHost, "##goalSend", string.Format(CultureInfo.CurrentCulture, Strings.GoalSendFormat, doable.Length), doable, static ids => ids);
                ImGui.SameLine();
            }
            else if (doable.Length > 0)
            {
                TextFlow.Wrapped(Strings.GoalSendNeedsFull, Chrome.RoomX(), Theme.U32(s.TextSecondary));
            }
        }
        else
        {
            var why = row.Place switch
            {
                RosterPlace.OtherClient => string.Format(CultureInfo.CurrentCulture, Strings.GoalReadOnlyClientFormat, first),
                _ => string.Format(CultureInfo.CurrentCulture, Strings.GoalReadOnlyStoredFormat, first),
            };
            TextFlow.Wrapped(why, Chrome.RoomX(), Theme.U32(s.TextSecondary));
        }

        if (progress is { Quests.Count: > 0 } && Chrome.ActionChip("##goalRoute", Strings.GoalRoute))
        {
            var parts = progress.Quests.Take(GoalCardMaxRows).Select(q => new RouteTarget(RouteTargetKind.Quest, session.Spoilers.DisplayName(q), [q.RowId])).ToArray();
            ui.OpenRoute(RouteTarget.Union(RouteTargetKind.Quest, title, parts));
        }

        if (row.CanHandOff)
        {
            ImGui.SameLine();
            if (Chrome.ActionChip("##goalClearCard", Strings.GoalClear))
            {
                ClearGoal(board, row.ContentId, row.Name, goal);
            }
        }
    }
}
