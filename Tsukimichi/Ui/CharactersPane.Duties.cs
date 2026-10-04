using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The Duties board on the Characters dashboard (feature plan v7 N4), under the planning extras: "why is my Level Cap
/// roulette locked?" answered from the character's own duty records. The open roulettes on one line; each closed one
/// with its reason and the duties still to unlock, each with the quest that unlocks it (click to show it); then the
/// duties unlocked but never cleared, per category. A stored character keeps the board of its last capture; one never
/// captured with records reads "log in to read". Everything is <see cref="DutyBoardSource"/>'s; nothing allocates per
/// frame.
/// </summary>
public sealed partial class CharactersPane
{
    /// <summary>The board's lines; set by the plugin. Null hides the board.</summary>
    public DutyBoardSource? DutyBoard { get; set; }

    private void DrawDutyBoard(UiState ui)
    {
        if (DutyBoard is not { Visible: true } board)
        {
            return;
        }

        using var id = ImRaii.PushId("dutyBoard");
        Gap();
        SectionHeading.Draw(Strings.DutyBoardHeading);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.DutyBoardTooltip);
        }

        if (!board.HasRecords)
        {
            TextFlow.Wrapped(Strings.DutyBoardNotRead, 0f, ImGui.GetColorU32(ImGuiCol.TextDisabled));
            return;
        }

        if (board.OpenLine.Length > 0)
        {
            TextFlow.Wrapped(board.OpenLine, 0f, Theme.U32(Theme.Surface.TextSecondary));
        }

        var locked = board.Locked;
        var iconSize = MathF.Round(UiMetrics.JobIconSize);
        for (var i = 0; i < locked.Count; i++)
        {
            var line = locked[i];
            using var rowId = ImRaii.PushId(i);
            ImGui.Spacing();
            TextFlow.Wrapped(line.Name);
            TextFlow.Wrapped(line.Reason, 0f, Theme.U32(Theme.Surface.TextSecondary));
            using var indent = ImRaii.PushIndent(UiMetrics.Px(16f));
            for (var j = 0; j < line.Missing.Count; j++)
            {
                DrawMissingDuty(ui, line.Missing[j], j, iconSize);
            }
        }

        Gap();
        SectionHeading.Draw(Strings.DutyBoardNeverHeading);
        var never = board.Never;
        if (never.Count == 0)
        {
            TextFlow.Wrapped(Strings.DutyBoardNeverNone, 0f, ImGui.GetColorU32(ImGuiCol.TextDisabled));
            return;
        }

        foreach (var group in never)
        {
            TextFlow.Wrapped(group.Kind, 0f, Theme.U32(Theme.Surface.TextSecondary));
            using var indent = ImRaii.PushIndent(UiMetrics.Px(16f));
            TextFlow.Wrapped(group.Duties);
        }
    }

    /// <summary>One duty still to unlock: its icon and name with its level, then the quest that unlocks it (click shows it).</summary>
    private void DrawMissingDuty(UiState ui, DutyBoardSource.MissingLine missing, int index, float iconSize)
    {
        using var id = ImRaii.PushId(index);
        if (missing.Icon != 0)
        {
            DrawLeadIcon(NodeIcon.Game(missing.Icon), iconSize);
        }

        var name = missing.Level.Length > 0 ? missing.Duty + MsqText.Separator + missing.Level : missing.Duty;
        if (!Chrome.FitText(name, ImGui.GetColorU32(ImGuiCol.Text)) && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(name);
        }

        using var indent = ImRaii.PushIndent(UiMetrics.Px(16f));
        if (missing.Quest is { } quest)
        {
            if (Chrome.EllipsisSelectable(missing.From, false, 0f, out _))
            {
                ui.Reveal(quest);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(missing.From, Strings.DutyBoardQuestTip);
            }
        }
        else
        {
            TextFlow.Wrapped(missing.From, 0f, ImGui.GetColorU32(ImGuiCol.TextDisabled));
        }
    }
}
