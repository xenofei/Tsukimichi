using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Journal;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The journal's room on the Characters dashboard (feature plan v7, C9): the section's header says what is left
/// ("3 journal slots left", "Journal full"), and under it "Make room" lists the viewed character's journal quests by
/// what freeing their slot costs (<see cref="MakeRoom"/>): hand in, safe to drop, keep, each with its reason. Opens by
/// itself while the journal is nearly full. Tsukimichi never abandons a quest: the note says how to in the game, and the
/// Abandoned list keeps any quest dropped. Rows are rebuilt once per session version.
/// </summary>
public sealed partial class CharactersPane
{
    private const int RoomQuest = 0;
    private const int RoomAdviceColumn = 1;
    private const int RoomActions = 2;
    private readonly ColumnFit roomColumns = new(3);

    private RoomRowView[] roomRows = [];
    private string roomHeader = string.Empty;
    private JournalRoom roomLevel;
    private int roomVersion = -1;
    private int roomLanguage = -1;

    private void DrawJournalRoom(UiState ui)
    {
        RefreshJournalRoom();
        if (roomHeader.Length == 0)
        {
            return;
        }

        using var id = ImRaii.PushId("journalRoom");
        bool open;
        using (Theme.PushText(Theme.DangerText, roomLevel == JournalRoom.Full))
        {
            // A full journal reads in the error tone; nearly full opens the section by itself.
            open = ImGui.CollapsingHeader(roomHeader, roomLevel == JournalRoom.Room ? ImGuiTreeNodeFlags.None : ImGuiTreeNodeFlags.DefaultOpen);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.JournalRoomTooltip);
        }

        if (!open)
        {
            return;
        }

        if (roomRows.Length == 0)
        {
            ImGui.TextDisabled(Strings.JournalRoomEmpty);
            return;
        }

        TextFlow.Wrapped(Strings.JournalRoomNote, 0f, ImGui.GetColorU32(ImGuiCol.TextDisabled));

        const ImGuiTableFlags Flags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH;
        var width = ImGui.GetContentRegionAvail().X;
        var nameMin = UiMetrics.Px(LayoutBudgets.RowNameMinLogical);
        var adviceMin = RoomAdviceMin();
        var actions = TravelControls.RowButtonWidth(ActionGlyphs.Reveal, Strings.AbandonedReveal);
        Span<ColumnSpec> specs = stackalloc ColumnSpec[3];
        specs[RoomQuest] = new ColumnSpec(0, nameMin, nameMin, 3f);
        specs[RoomAdviceColumn] = new ColumnSpec(0, adviceMin, adviceMin, 2f);
        specs[RoomActions] = new ColumnSpec(0, actions, actions);
        roomColumns.Plan(width, specs);
        using var table = roomColumns.Begin("##journalRoom", Flags);
        if (!table.Success)
        {
            return;
        }

        roomColumns.Setup(RoomQuest, Strings.CharactersColumnQuest);
        roomColumns.Setup(RoomAdviceColumn, Strings.JournalRoomColumnAdvice);
        roomColumns.Setup(RoomActions, "##actions");
        for (var i = 0; i < roomRows.Length; i++)
        {
            var row = roomRows[i];
            using var rowId = ImRaii.PushId(i);
            ImGui.TableNextRow();
            if (roomColumns.Next(RoomQuest))
            {
                if (row.Quest is { } quest)
                {
                    if (Chrome.EllipsisSelectable(row.Name, false, 0f, out _))
                    {
                        Reveal(ui, quest);
                    }

                    if (ImGui.IsItemHovered())
                    {
                        UiMetrics.Tooltip(row.NameAndReason);
                    }
                }
                else
                {
                    Chrome.FitText(row.Name, ImGui.GetColorU32(ImGuiCol.TextDisabled));
                }
            }

            if (roomColumns.Next(RoomAdviceColumn))
            {
                var s = Theme.Surface;
                Chrome.StatusText(row.Status, ImGui.GetContentRegionAvail().X, row.Advice == RoomAdvice.Keep ? s.TextSecondary : s.Text, s.TextSecondary);
                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(row.Reason);
                }
            }

            if (roomColumns.Next(RoomActions) && row.Quest is { } target)
            {
                if (TravelControls.RowButton("##reveal", ActionGlyphs.Reveal, Strings.AbandonedReveal))
                {
                    Reveal(ui, target);
                }

                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(Strings.AbandonedRevealTooltip);
                }
            }
        }
    }

    /// <summary>The advice column's least width: the widest advice word, never cut.</summary>
    private float RoomAdviceMin()
    {
        var widest = 0f;
        foreach (var row in roomRows)
        {
            widest = MathF.Max(widest, ImGui.CalcTextSize(row.Word).X);
        }

        return widest + UiMetrics.Px(24f);
    }

    /// <summary>Rebuilds the header and rows from the viewed character's journal when the session or the language changed.</summary>
    private void RefreshJournalRoom()
    {
        if (roomVersion == session.Version && roomLanguage == Localization.Loc.Version)
        {
            return;
        }

        roomVersion = session.Version;
        roomLanguage = Localization.Loc.Version;
        if (session.ViewedSnapshot is not { } snapshot || session.Bundle is not { } bundle)
        {
            roomHeader = string.Empty;
            roomRows = [];
            return;
        }

        var slots = JournalSlots.Of(snapshot, bundle.Catalog);
        roomLevel = slots.Room;
        roomHeader = slots.Text + "###journalRoomHeader";
        var ranked = MakeRoom.Rank(snapshot, bundle.Catalog);
        var rows = new List<RoomRowView>(ranked.Count);
        foreach (var row in ranked)
        {
            // Through the spoiler shield (T19), as every quest name on the dashboard.
            var name = row.Quest is { } quest
                ? session.Spoilers.DisplayName(quest)
                : string.Format(CultureInfo.InvariantCulture, Strings.MoonlitQuestFormat, 0x10000u | row.Entry.QuestId);
            var word = row.Advice switch
            {
                RoomAdvice.HandIn => Strings.JournalRoomHandIn,
                RoomAdvice.SafeToDrop => Strings.JournalRoomSafeToDrop,
                _ => Strings.JournalRoomKeep,
            };
            var step = row.StepText;
            var status = step.Length == 0 ? word : word + Core.Evaluation.BlockerText.Separator + step;
            rows.Add(new RoomRowView(row.Quest, name, row.Advice, word, status, row.Reason, name + "\n" + row.Reason));
        }

        roomRows = rows.ToArray();
    }

    /// <param name="Word">The advice alone ("Safe to drop"), for the column's width.</param>
    /// <param name="Status">The advice and the step ("Safe to drop · step 1 of 4").</param>
    /// <param name="NameAndReason">The name over the reason, the name's hover text.</param>
    private sealed record RoomRowView(QuestRecord? Quest, string Name, RoomAdvice Advice, string Word, string Status, string Reason, string NameAndReason);
}
