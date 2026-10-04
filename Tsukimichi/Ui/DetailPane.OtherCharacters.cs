using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// "Your other characters" under the detail hero (plan v7, 1.21.0 P3; spec-1.21 "Your other characters, under the detail
/// hero"): one fixed-height line, the 14 px state moons and first names of the characters who can act on the quest
/// (Ready or in the journal), then the rest in words ("Done on 4", "2 can't take it yet"). Its hover is the account
/// table: every character's state of the quest with its reason, from each one's last save (<see cref="RosterSource"/>,
/// the linked launcher folders' characters included). The line is there for every quest while the player has more than
/// one character, so the hero never changes height between quests. Built when the quest or the roster changes.
/// </summary>
public sealed partial class DetailPane
{
    /// <summary>Every character's states (1.21.0 P3); set by the plugin. Null draws no line.</summary>
    public RosterSource? Roster { get; set; }

    private (uint RowId, int Roster, int Language, ulong? Viewed) othersKey = (uint.MaxValue, -1, -1, null);
    private readonly List<(QuestState State, string Name)> othersCan = [];
    private string othersRest = string.Empty;
    private string othersTable = string.Empty;
    private bool othersShown;

    private void DrawOtherCharacters(SessionState session, QuestRecord quest)
    {
        if (Roster is not { } board)
        {
            return;
        }

        RefreshOthers(session, board, quest);
        if (!othersShown)
        {
            return;
        }

        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var room = MathF.Max(1f, RoomTo(bodyRight));
        var line = ImGui.GetTextLineHeight();
        var height = MathF.Max(UiMetrics.Px(22f), line);
        var textY = origin.Y + MathF.Round((height - line) * 0.5f);
        var right = origin.X + room;
        var gap = UiMetrics.Px(10f);
        var x = origin.X;

        dl.PushClipRect(origin, new Vector2(right, origin.Y + height), true);
        dl.AddText(new Vector2(x, textY), Theme.U32(s.TextSecondary), Strings.OthersLabel);
        x += ImGui.CalcTextSize(Strings.OthersLabel).X + gap;
        var moon = UiMetrics.Px(7f);
        foreach (var (state, name) in othersCan)
        {
            MoonGlyph.Draw(dl, new Vector2(x + moon, origin.Y + (height * 0.5f)), moon, state);
            x += (moon * 2f) + UiMetrics.Px(4f);
            dl.AddText(new Vector2(x, textY), Theme.U32(s.Text), name);
            x += ImGui.CalcTextSize(name).X + gap;
        }

        if (othersRest.Length > 0)
        {
            dl.AddText(new Vector2(x, textY), Theme.U32(s.TextSecondary), othersRest);
        }

        dl.PopClipRect();
        ImGui.Dummy(new Vector2(room, height));
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(string.Format(CultureInfo.CurrentCulture, Strings.OthersTableTitleFormat, session.Spoilers.DisplayName(quest)), othersTable);
        }
    }

    private void RefreshOthers(SessionState session, RosterSource board, QuestRecord quest)
    {
        var rows = board.Rows;
        var key = (quest.RowId, board.Revision, Localization.Loc.Version, session.ViewedContentId);
        if (key == othersKey)
        {
            return;
        }

        othersKey = key;
        othersCan.Clear();
        othersRest = string.Empty;
        othersTable = string.Empty;
        othersShown = rows.Count > 1;
        if (!othersShown)
        {
            return;
        }

        var done = 0;
        var cannot = 0;
        var reading = 0;
        var table = new StringBuilder();
        foreach (var row in rows)
        {
            var first = GoalText.FirstName(row.Name);
            var states = board.StatesOf(row.ContentId);
            QuestEvaluation? evaluation = null;
            states?.TryGetValue(quest.RowId, out evaluation);
            if (table.Length > 0)
            {
                table.Append('\n');
            }

            table.Append(first).Append(Strings.GoalSeparator);
            if (evaluation is null)
            {
                table.Append(states is null ? Strings.RosterBeingRead : Strings.StateName(QuestState.Unknown, quest));
            }
            else
            {
                table.Append(Strings.StateName(evaluation.State, quest));
                var reason = BlockerText.Reason(evaluation, quest, session.Names, states);
                if (reason.Length > 0)
                {
                    table.Append(Strings.GoalSeparator).Append(reason);
                }
            }

            if (row.ContentId == session.ViewedContentId)
            {
                continue;
            }

            switch (evaluation?.State)
            {
                case QuestState.Ready or QuestState.Accepted:
                    othersCan.Add((evaluation.State, first));
                    break;
                case QuestState.Completed or QuestState.DoneThisCycle:
                    done++;
                    break;
                case null when states is null:
                    reading++;
                    break;
                default:
                    cannot++;
                    break;
            }
        }

        var rest = new List<string>(3);
        if (done > 0)
        {
            rest.Add(string.Format(CultureInfo.CurrentCulture, Strings.OthersDoneFormat, done));
        }

        if (cannot > 0)
        {
            rest.Add(cannot == 1 ? Strings.OthersCannotOne : string.Format(CultureInfo.CurrentCulture, Strings.OthersCannotFormat, cannot));
        }

        if (reading > 0)
        {
            rest.Add(string.Format(CultureInfo.CurrentCulture, Strings.OthersReadingFormat, reading));
        }

        othersRest = string.Join("  ", rest);
        othersTable = table.ToString();
    }
}
