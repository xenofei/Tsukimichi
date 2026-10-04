using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Route;
using Tsukimichi.Core.Triad;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The Triple Triad card on the Characters dashboard (feature plan v7, 1.21.0 P6; spec-1.21 P6), after Duties: the
/// caption counts the opponents still to unlock ("11 opponents to unlock"), its hover the ones finished; one fixed row
/// of chips (Plays you · Locked · Cards left); "Plays you" with the opponents that still have something for you
/// ("3 cards you don't have", "beaten · 2 cards left"); "Locked behind a quest" with the quest each waits on, its moon,
/// state and blocker ("after Criminal Phrenology · Blocked: All the Little Angels first"); the first opponent past the
/// story point masked and the rest folded ("6 more past Kiri's story · names hidden"); then Route to the opponents and
/// Pin the quests. Rows are two-line, 44 px, with the card icon and a reserved slot for Teleport and "…" (Flag, Show
/// the quest, Send to Questionable at Full hand-offs). A stored character keeps the records of its last capture; one
/// never captured with them reads "log in to read". Everything is <see cref="TriadBoardSource"/>'s.
/// </summary>
public sealed partial class CharactersPane
{
    /// <summary>Rows a group shows before "N more ›".</summary>
    private const int TriadRows = 3;

    private const uint TriadHoverTag = 0x5454_5244; // "TTRD"

    private TriadChips triadChips = TriadChips.All;
    private bool triadPlaysOpen;
    private bool triadLockedOpen;

    /// <summary>The card's lines; set by the plugin. Null hides the card.</summary>
    public TriadBoardSource? TriadBoard { get; set; }

    /// <summary>Teleport, map flags and the aetheryte index for the card's rows; set by the plugin with <see cref="TriadBoard"/>.</summary>
    public GameLinks? TriadLinks { get; set; }

    private void DrawTriadBoard(UiState ui)
    {
        if (TriadBoard is not { Visible: true } board)
        {
            return;
        }

        using var id = ImRaii.PushId("triadBoard");
        Gap();
        SectionHeading.Draw(Strings.TriadHeading, board.Caption.Length > 0 ? board.Caption : null);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.TriadTooltip, board.CaptionTip);
        }

        DrawTriadChips(board);
        var width = MathF.Max(1f, ImGui.GetContentRegionAvail().X - UiMetrics.Px(4f));

        if (triadChips.PlaysYou || triadChips.CardsLeft)
        {
            ImGui.Spacing();
            GroupLine(Strings.TriadPlaysYouGroup, Strings.TriadPlaysYouSub);
            if (!board.Model.HasRecords)
            {
                TextFlow.Wrapped(Strings.TriadNotRead, 0f, Theme.U32(Theme.Surface.TextSecondary));
            }
            else
            {
                DrawTriadRows(ui, board.PlaysYou, width, ref triadPlaysOpen, 0);
            }
        }

        var kept = new List<TriadBoardSource.Row>(board.Named.Count);
        if (triadChips.Locked || triadChips.CardsLeft)
        {
            foreach (var row in board.Named)
            {
                if (triadChips.Keeps(row.Model))
                {
                    kept.Add(row);
                }
            }

            if (kept.Count > 0 || board.Masked.Count > 0)
            {
                ImGui.Spacing();
                GroupLine(Strings.TriadLockedGroup, Strings.TriadLockedSub);
                var shown = DrawTriadRows(ui, kept, width, ref triadLockedOpen, 10_000);
                kept.RemoveRange(shown, kept.Count - shown);
                DrawTriadMasked(ui, board, width);
            }
        }

        DrawTriadActions(ui, kept);
    }

    /// <summary>The chips, one fixed row: Plays you · Locked · Cards left, each with its count.</summary>
    private void DrawTriadChips(TriadBoardSource board)
    {
        var (plays, locked, cards) = board.Chips;
        var gap = UiMetrics.Px(6f);
        if (BoardChips.Chip("##chipPlays", plays, triadChips.PlaysYou, Strings.TriadChipPlaysYouTip))
        {
            triadChips = triadChips with { PlaysYou = !triadChips.PlaysYou };
        }

        ImGui.SameLine(0f, gap);
        if (BoardChips.Chip("##chipLocked", locked, triadChips.Locked, Strings.TriadChipLockedTip))
        {
            triadChips = triadChips with { Locked = !triadChips.Locked };
        }

        Chrome.SameLineOrWrap(BoardChips.Width(cards) + gap);
        if (BoardChips.Chip("##chipCards", cards, triadChips.CardsLeft, Strings.TriadChipCardsLeftTip))
        {
            triadChips = triadChips with { CardsLeft = !triadChips.CardsLeft };
        }
    }

    /// <summary>A group's line: its name in semibold Text, its sub-label in Secondary after it.</summary>
    private static void GroupLine(string name, string sub)
    {
        Chrome.SemiboldText(name, Theme.Surface.Text);
        ImGui.SameLine(0f, UiMetrics.Px(8f));
        var start = ImGui.GetCursorScreenPos();
        var right = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
        Chrome.EllipsisText(sub, MathF.Max(1f, right - start.X), Theme.U32(Theme.Surface.TextSecondary));
    }

    /// <summary>The rows the chips keep, the first <see cref="TriadRows"/> unless opened; returns how many were drawn.</summary>
    private int DrawTriadRows(UiState ui, IReadOnlyList<TriadBoardSource.Row> rows, float width, ref bool open, int idBase)
    {
        var drawn = 0;
        var keptCount = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            if (!triadChips.Keeps(rows[i].Model))
            {
                continue;
            }

            keptCount++;
            if (!open && drawn >= TriadRows)
            {
                continue;
            }

            using var rowId = ImRaii.PushId(idBase + i);
            DrawTriadRow(ui, rows[i], width);
            drawn++;
        }

        DrawMoreToggle(keptCount - TriadRows, ref open);
        return drawn;
    }

    /// <summary>The first opponent past the story point as a row, the rest folded into one line.</summary>
    private void DrawTriadMasked(UiState ui, TriadBoardSource board, float width)
    {
        var masked = board.Masked;
        if (masked.Count == 0 || !triadChips.Locked)
        {
            return;
        }

        using (ImRaii.PushId("masked"))
        {
            DrawTriadRow(ui, masked[0], width);
        }

        if (masked.Count > 1)
        {
            var text = string.Format(CultureInfo.CurrentCulture, Strings.TriadMoreMaskedFormat, masked.Count - 1, board.FirstName);
            TextFlow.Wrapped(text, 0f, Theme.U32(Theme.Surface.TextSecondary));
        }
    }

    private void DrawTriadRow(UiState ui, TriadBoardSource.Row row, float width)
    {
        var links = TriadLinks;
        var actions = !row.Masked && links is not null;
        bool? teleport = actions && links!.TeleportShown ? row.AetheryteId != 0 && links.CanTeleportTo(row.AetheryteId) : null;
        var tip = !actions || row.AetheryteId == 0 ? Strings.TriadNoAetheryte
            : links!.TeleportToBlocked(row.AetheryteId) ?? string.Format(CultureInfo.CurrentCulture, Strings.TriadTeleportFormat, row.AetheryteName);
        var result = BoardRow.Draw(
            "##triadRow",
            Motion.Key(TriadHoverTag, row.Opponent.ResidentId),
            width,
            RouteTarget.TriadCardIcon,
            textures,
            row.Name,
            row.Place,
            factAfterName: false,
            chip: null,
            row.Line2,
            teleport,
            Strings.PlanTeleport,
            tip,
            slot: actions);
        if (result.Teleport && links is not null)
        {
            links.TeleportTo(row.AetheryteId, row.AetheryteName);
        }

        if (result.MenuRequested && actions)
        {
            ImGui.OpenPopup(BoardRow.MenuId);
        }

        if (actions)
        {
            DrawTriadMenu(ui, row, links!);
        }
    }

    /// <summary>The row's "…" menu: Flag, Show the quest, Send to Questionable (at Full hand-offs).</summary>
    private void DrawTriadMenu(UiState ui, TriadBoardSource.Row row, GameLinks links)
    {
        using var popup = ImRaii.Popup(BoardRow.MenuId);
        if (!popup)
        {
            return;
        }

        UiMetrics.ApplyFontScale();
        var spot = row.Opponent.Spot;
        if (ImGui.MenuItem(Strings.FlagOnMap, string.Empty, false, spot is not null && links.CanFlagSpot(spot.TerritoryId)) && spot is not null)
        {
            links.FlagSpot(spot.TerritoryId, new Vector3(spot.X, 0f, spot.Z));
        }

        if (row.Quest is { } quest)
        {
            if (ImGui.MenuItem(Strings.TriadMenuShowQuest))
            {
                ui.Reveal(quest);
            }

            AutomationGate.Questionable(Questionable)?.DrawSubmenu(MainWindow.QuestionableHost, Strings.QuestionableSendButton, new[] { quest.RowId }, static rows => rows);
        }
    }

    /// <summary>Route to the opponents the card names and Pin the quests they wait on.</summary>
    private void DrawTriadActions(UiState ui, List<TriadBoardSource.Row> named)
    {
        if (named.Count == 0)
        {
            return;
        }

        using var indent = ImRaii.PushIndent(UiMetrics.Px(8f));
        var label = named.Count == 1 ? Strings.TriadRouteOne : string.Format(CultureInfo.CurrentCulture, Strings.TriadRouteFormat, named.Count);
        if (ImGui.SmallButton(label))
        {
            var title = named.Count == 1 ? Strings.TriadRouteLabelOne : string.Format(CultureInfo.CurrentCulture, Strings.TriadRouteLabelFormat, named.Count);
            var opponents = new List<TriadOpponent>(named.Count);
            foreach (var row in named)
            {
                opponents.Add(row.Opponent with { Name = row.Name });
            }

            ui.OpenRoute(RouteTarget.ForTriad(opponents, title));
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.TriadRouteTip);
        }

        if (Pins is not { CanPin: true } pins)
        {
            return;
        }

        var quests = new List<uint>(named.Count);
        foreach (var row in named)
        {
            if (row.Quest is { } quest && !quests.Contains(quest.RowId))
            {
                quests.Add(quest.RowId);
            }
        }

        var unpinned = 0;
        foreach (var rowId in quests)
        {
            if (!pins.IsPinned(rowId))
            {
                unpinned++;
            }
        }

        Chrome.SameLineOrWrap(ImGui.CalcTextSize(Strings.TriadPinQuests).X + (ImGui.GetStyle().FramePadding.X * 2f));
        using (ImRaii.Disabled(unpinned == 0))
        {
            if (ImGui.SmallButton(Strings.TriadPinQuests))
            {
                // Pins only: a quest already pinned stays pinned (no click here ever unpins).
                foreach (var rowId in quests)
                {
                    if (!pins.IsPinned(rowId))
                    {
                        pins.TogglePin(rowId);
                    }
                }
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(unpinned == 0 ? Strings.TriadPinnedTip : Strings.TriadPinTip);
        }
    }
}
