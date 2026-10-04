using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Tsukimichi.Ui;

/// <summary>
/// The Duties card on the Characters dashboard (feature plan v7 N4; spec-1.19 N4), after Allied societies: "why is my
/// Level Cap roulette locked?" answered from the character's own duty records. The caption reads "1 roulette locked",
/// or nothing when every roulette is open. Each roulette with something left is a block: its name and, at the
/// trailing end in Secondary, its state ("locked · needs a Lv 100 job · best is BLM 98", "open · 1 raid not
/// unlocked"); a row per duty not unlocked with its C7 size badge and "not unlocked · with" the unlock quest (click to
/// show it); then Route (the Route window over those quests) and Pin both / Pin all. "Unlocked, never cleared" follows
/// with its count, the first two duties with badges and "12 more ›", which opens the full list in the same card. A
/// block also folds past its first <see cref="BlockRows"/> rows. A stored character keeps the card of its last
/// capture; one never captured with records reads "log in to read". Everything is <see cref="DutyBoardSource"/>'s.
/// </summary>
public sealed partial class CharactersPane
{
    /// <summary>Rows a roulette block shows before "N more ›".</summary>
    private const int BlockRows = 3;

    /// <summary>Duties "Unlocked, never cleared" shows before "N more ›" (spec-1.19 N4: the first 2).</summary>
    private const int NeverRows = 2;

    private readonly HashSet<uint> openBlocks = [];
    private bool neverOpen;

    /// <summary>The card's lines; set by the plugin. Null hides the card.</summary>
    public DutyBoardSource? DutyBoard { get; set; }

    private void DrawDutyBoard(UiState ui)
    {
        if (DutyBoard is not { Visible: true } board)
        {
            return;
        }

        using var id = ImRaii.PushId("dutyBoard");
        Gap();
        SectionHeading.Draw(Strings.DutyBoardHeading, board.HasRecords && board.Caption.Length > 0 ? board.Caption : null);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.DutyBoardTooltip);
        }

        if (!board.HasRecords)
        {
            TextFlow.Wrapped(Strings.DutyBoardNotRead, 0f, ImGui.GetColorU32(ImGuiCol.TextDisabled));
            return;
        }

        var blocks = board.Blocks;
        for (var i = 0; i < blocks.Count; i++)
        {
            using var blockId = ImRaii.PushId(i);
            if (i > 0)
            {
                ImGui.Spacing();
            }

            DrawRouletteBlock(ui, blocks[i]);
        }

        var never = board.Never;
        if (never.Count == 0)
        {
            return;
        }

        ImGui.Spacing();
        HeaderLine(Strings.DutyBoardNeverHeading, never.Count.ToString(CultureInfo.CurrentCulture));
        var shown = neverOpen ? never.Count : Math.Min(NeverRows, never.Count);
        for (var i = 0; i < shown; i++)
        {
            using var rowId = ImRaii.PushId(1000 + i);
            DrawDutyRow(ui, never[i]);
        }

        DrawMoreToggle(never.Count - NeverRows, ref neverOpen);
    }

    private void DrawRouletteBlock(UiState ui, DutyBoardSource.Block block)
    {
        HeaderLine(block.Header, block.State);
        var open = openBlocks.Contains(block.Id);
        var shown = open ? block.Rows.Count : Math.Min(BlockRows, block.Rows.Count);
        for (var j = 0; j < shown; j++)
        {
            using var rowId = ImRaii.PushId(j);
            DrawDutyRow(ui, block.Rows[j]);
        }

        if (block.Rows.Count > BlockRows)
        {
            var toggled = open;
            DrawMoreToggle(block.Rows.Count - BlockRows, ref toggled);
            if (toggled != open && !openBlocks.Remove(block.Id))
            {
                openBlocks.Add(block.Id);
            }
        }

        DrawBlockActions(ui, block);
    }

    /// <summary>A block's header: the name in Text, semibold, and its state in Secondary at the trailing end (under it when there is no room).</summary>
    private static void HeaderLine(string name, string state)
    {
        Chrome.SemiboldText(name, Theme.Surface.Text);
        if (state.Length == 0)
        {
            return;
        }

        var width = ImGui.CalcTextSize(state).X;
        var right = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
        ImGui.SameLine();
        var x = ImGui.GetCursorScreenPos().X;
        if (x + width > right)
        {
            ImGui.NewLine();
            TextFlow.Wrapped(state, 0f, Theme.U32(Theme.Surface.TextSecondary));
            return;
        }

        ImGui.SetCursorScreenPos(new Vector2(right - width, ImGui.GetCursorScreenPos().Y));
        ImGui.TextColored(Theme.Surface.TextSecondary, state);
    }

    /// <summary>A duty: its name, its size badge, and at the trailing end "not unlocked · with" the quest (click shows it).</summary>
    private void DrawDutyRow(UiState ui, DutyBoardSource.Row row)
    {
        Chrome.FitText(row.Duty, ImGui.GetColorU32(ImGuiCol.Text));
        if (row.Badge is { } badge)
        {
            Chrome.SameLineOrWrap(DutyBadges.Width(badge));
            DutyBadges.Draw(badge, textures);
        }

        if (row.Trailing.Length == 0)
        {
            return;
        }

        var lead = ImGui.CalcTextSize(row.Trailing).X;
        var questWidth = row.QuestName.Length > 0 ? ImGui.CalcTextSize(row.QuestName).X : 0f;
        Chrome.SameLineOrWrap(lead + questWidth);
        var right = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
        var x = MathF.Max(ImGui.GetCursorScreenPos().X, right - lead - questWidth);
        var y = ImGui.GetCursorScreenPos().Y;
        ImGui.SetCursorScreenPos(new Vector2(x, y));
        ImGui.TextColored(Theme.Surface.TextSecondary, row.Trailing);
        if (row.Quest is not { } quest)
        {
            return;
        }

        ImGui.SameLine(0f, 0f);
        if (ImGui.Selectable(row.QuestName, false, ImGuiSelectableFlags.None, new Vector2(questWidth, 0f)))
        {
            ui.Reveal(quest);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(row.QuestName, Strings.DutyBoardQuestTip);
        }
    }

    /// <summary>Route and Pin both / Pin all under a block, when its duties have unlock quests.</summary>
    private void DrawBlockActions(UiState ui, DutyBoardSource.Block block)
    {
        if (block.Route is not { } route || block.PinQuests.Count == 0)
        {
            return;
        }

        using var indent = ImRaii.PushIndent(UiMetrics.Px(8f));
        if (ImGui.SmallButton(Strings.DutyBoardRoute))
        {
            ui.OpenRoute(route);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.DutyBoardRouteTip);
        }

        if (Pins is not { CanPin: true } pins)
        {
            return;
        }

        var label = block.PinQuests.Count switch
        {
            1 => Strings.DutyBoardPinOne,
            2 => Strings.DutyBoardPinBoth,
            _ => Strings.DutyBoardPinAll,
        };
        var unpinned = 0;
        foreach (var quest in block.PinQuests)
        {
            if (!pins.IsPinned(quest.RowId))
            {
                unpinned++;
            }
        }

        Chrome.SameLineOrWrap(ImGui.CalcTextSize(label).X + (ImGui.GetStyle().FramePadding.X * 2f));
        using (ImRaii.Disabled(unpinned == 0))
        {
            if (ImGui.SmallButton(label))
            {
                // Pins only: a quest already pinned stays pinned (no click here ever unpins).
                foreach (var quest in block.PinQuests)
                {
                    if (!pins.IsPinned(quest.RowId))
                    {
                        pins.TogglePin(quest.RowId);
                    }
                }
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(unpinned == 0 ? Strings.DutyBoardPinnedTip : Strings.DutyBoardPinTip);
        }
    }

    /// <summary>"12 more ›" while folded, "Show fewer" while open; nothing when <paramref name="more"/> is not positive.</summary>
    private static void DrawMoreToggle(int more, ref bool open)
    {
        if (more <= 0)
        {
            return;
        }

        var label = open ? Strings.DutyBoardFewer : string.Format(CultureInfo.CurrentCulture, Strings.DutyBoardMoreFormat, more);
        if (Chrome.EllipsisSelectable(label, false, ImGui.CalcTextSize(label).X, out _))
        {
            open = !open;
        }
    }
}
