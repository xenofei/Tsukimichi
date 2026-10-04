using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The two-line storyline rows of the Side stories and Loose ends cards (feature plan v7 P5, N8; spec-1.21 decision 17):
/// 44 px (scaled with the body text), the line's icon, its name on line 1 and what is left on line 2 ("3 left · next:
/// Forever in Our Hearts · Ready", "Finale · 1 left · Lv 80 · ◑ A Harmony from the Heavens"), and a reserved slot at the
/// trailing end where Teleport and "…" show on hover or keyboard navigation, so a hover never covers text and nothing
/// moves. The quest name is a link that shows the quest in the Journal. Drawn on the draw list; nothing allocates per
/// frame.
/// </summary>
public sealed partial class CharactersPane
{
    /// <summary>The side-quest icon (spec-1.21: 061411), for side stories and the lines that are not a job's.</summary>
    internal const uint SideQuestIcon = 61411;

    /// <summary>A row's height at 100 %, logical px.</summary>
    private const float StoryRowLogical = 44f;

    /// <summary>
    /// One storyline row as built (not per frame).
    /// </summary>
    /// <param name="IconId">The game icon on the left; 0 for none.</param>
    /// <param name="Name">Line 1.</param>
    /// <param name="Chip">The neutral chip that leads line 2 ("Finale"); null for none.</param>
    /// <param name="CaughtUp">Line 2 leads with "Caught up" in silver, semibold.</param>
    /// <param name="Lead">Line 2's words before the quest ("3 left · next: ", "1 left · Lv 80 · ").</param>
    /// <param name="Quest">The quest line 2 names (a link); null names none.</param>
    /// <param name="QuestName">Its name through the spoiler shield.</param>
    /// <param name="Moon">Draw the quest's state moon before its name (Loose ends).</param>
    /// <param name="State">The quest's state, for the moon.</param>
    /// <param name="Veiled">The quest lies past the story point: the moon disc instead of its moon, and no actions.</param>
    /// <param name="Tail">Line 2's words after the quest (" · Ready").</param>
    /// <param name="RowIds">The line's quests, for Send to Questionable.</param>
    /// <param name="RecapQuest">A quest naming the line for the story recap; 0 offers none.</param>
    private sealed record StoryRowView(
        uint IconId,
        string Name,
        string? Chip,
        bool CaughtUp,
        string Lead,
        QuestRecord? Quest,
        string QuestName,
        bool Moon,
        QuestState State,
        bool Veiled,
        string Tail,
        IReadOnlyList<uint> RowIds,
        uint RecapQuest = 0)
    {
        /// <summary>The slot's actions apply: a quest to travel to that the shield does not hide.</summary>
        public bool HasActions => Quest is not null && !Veiled;
    }

    /// <summary>A row's height at the current scale and text size: 44 px at 100 %, never less than two lines and their air.</summary>
    private static float StoryRowHeight() =>
        MathF.Max(UiMetrics.Px(StoryRowLogical), (ImGui.GetTextLineHeight() * 2f) + UiMetrics.Px(10f));

    /// <summary>The reserved slot's width: Teleport (when the automation level shows it) and "…".</summary>
    private float StorySlotWidth()
    {
        var more = MoreSize(ImGui.GetTextLineHeight());
        var gap = ImGui.GetStyle().ItemSpacing.X;
        var teleport = Links is { TeleportShown: true } ? TravelControls.RowButtonWidth(ActionIcons.TeleportIcon, Strings.StoriesTeleport) + gap : 0f;
        return teleport + more;
    }

    private void DrawStoryRows(UiState ui, string id, IReadOnlyList<StoryRowView> rows, int count)
    {
        using var scope = ImRaii.PushId(id);
        var slot = StorySlotWidth();
        for (var i = 0; i < count && i < rows.Count; i++)
        {
            using var rowId = ImRaii.PushId(i);
            DrawStoryRow(ui, rows[i], slot);
        }
    }

    private void DrawStoryRow(UiState ui, StoryRowView row, float slot)
    {
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var line = ImGui.GetTextLineHeight();
        var height = StoryRowHeight();
        var start = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var max = start + new Vector2(width, height);
        const string MenuId = "##storyRowMenu";
        var menuOpen = ImGui.IsPopupOpen(MenuId);
        var hovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem | ImGuiHoveredFlags.AllowWhenBlockedByPopup)
                      && ImGui.IsMouseHoveringRect(start, max);
        if (hovered || menuOpen)
        {
            dl.AddRectFilled(start, max, Theme.U32(s.Hover), Theme.Flair == Core.Ui.Flair.Plain ? 0f : UiMetrics.Px(4f));
        }

        // The icon, centred on the two lines.
        var pad = UiMetrics.Px(6f);
        var icon = MathF.Round(MathF.Min(UiMetrics.Px(24f), height - (2f * pad)));
        var iconMin = new Vector2(start.X + pad, start.Y + MathF.Round((height - icon) * 0.5f));
        if (row.IconId != 0 && textures is not null)
        {
            GameIcon.DrawAt(dl, textures, row.IconId, iconMin, iconMin + new Vector2(icon));
        }

        var left = iconMin.X + icon + UiMetrics.Px(8f);
        var right = max.X - slot - UiMetrics.Px(8f);
        var room = MathF.Max(1f, right - left);
        var gapY = MathF.Max(0f, (height - (line * 2f)) / 3f);
        var y1 = start.Y + gapY;
        var y2 = y1 + line + gapY;

        // Line 1: the line's name.
        if (Chrome.EllipsisTextAt(dl, new Vector2(left, y1), room, row.Name, Theme.U32(s.Text)) && hovered && ImGui.IsMouseHoveringRect(new Vector2(left, y1), new Vector2(right, y1 + line)))
        {
            UiMetrics.Tooltip(row.Name);
        }

        // Line 2: the chip, the lead, the moon, the quest, the tail; each clipped to what is left of the room.
        var x = left;
        if (row.Chip is { } chip)
        {
            using var caption = Typography.Caption();
            var size = ImGui.CalcTextSize(chip) + new Vector2(UiMetrics.Px(14f), UiMetrics.Px(4f));
            var chipMin = new Vector2(x, y2 + MathF.Round((line - size.Y) * 0.5f));
            Chrome.PillAt(dl, chipMin, size, chip, Theme.U32(s.Raised), Theme.U32(s.Line), Theme.U32(s.Text));
            if (hovered && ImGui.IsMouseHoveringRect(chipMin, chipMin + size))
            {
                UiMetrics.Tooltip(Strings.LooseEndsFinaleTooltip);
            }

            x += size.X + UiMetrics.Px(6f);
        }

        if (row.CaughtUp)
        {
            x += DrawSemiboldAt(dl, new Vector2(x, y2), Strings.StoriesCaughtUp, Theme.U32(s.TextSecondary), right - x);
        }

        if (row.Lead.Length > 0 && x < right)
        {
            Chrome.EllipsisTextAt(dl, new Vector2(x, y2), right - x, row.Lead, Theme.U32(s.TextSecondary));
            x += ImGui.CalcTextSize(row.Lead).X;
        }

        if (row.Moon && x < right)
        {
            var glyph = UiMetrics.InlineGlyphSize(line);
            var center = new Vector2(x + (glyph * 0.5f), y2 + (line * 0.5f));
            if (row.Veiled)
            {
                MoonGlyph.DrawVeiled(dl, center, glyph * 0.5f, 1f);
            }
            else
            {
                MoonGlyph.Draw(dl, center, glyph * 0.5f, row.State);
            }

            x += glyph + UiMetrics.Px(4f);
        }

        if (row.QuestName.Length > 0 && x < right)
        {
            var nameWidth = ImGui.CalcTextSize(row.QuestName).X;
            var shown = MathF.Min(nameWidth, right - x);
            var cut = Chrome.EllipsisTextAt(dl, new Vector2(x, y2), shown, row.QuestName, Theme.U32(row.Veiled ? s.TextSecondary : s.Text), nameWidth);
            if (row.Quest is { } quest && !row.Veiled)
            {
                ImGui.SetCursorScreenPos(new Vector2(x, y2));
                if (ImGui.InvisibleButton("##quest", new Vector2(MathF.Max(1f, shown), line)))
                {
                    Reveal(ui, quest);
                }

                if (ImGui.IsItemHovered())
                {
                    ShowInJournalTooltip(row.QuestName, cut);
                }
            }

            x += shown;
        }

        if (row.Tail.Length > 0 && x < right)
        {
            Chrome.EllipsisTextAt(dl, new Vector2(x, y2), right - x, row.Tail, Theme.U32(s.TextSecondary));
        }

        // The reserved slot: Teleport and "…" on hover, while the row's menu is open, or under keyboard navigation.
        if (row.HasActions && row.Quest is { } target && (hovered || menuOpen || ImGui.GetIO().NavVisible))
        {
            var more = MoreSize(line);
            var slotLeft = max.X - slot;
            var rowMid = start.Y + (height * 0.5f);
            if (Links is { TeleportShown: true } links)
            {
                ImGui.SetCursorScreenPos(new Vector2(slotLeft, rowMid - (ImGui.GetFrameHeight() * 0.5f)));
                TravelControls.TeleportButton(links, target, Strings.StoriesTeleport);
            }

            Keyboard.MoreButton("##more", MenuId, new Vector2(max.X - more, rowMid - (more * 0.5f)), more);
            DrawStoryRowMenu(ui, MenuId, row, target);
        }

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(width, height));
    }

    /// <summary>Text drawn twice half a pixel apart (semibold) at <paramref name="pos"/>, cut to <paramref name="room"/>; returns the advance with a space after.</summary>
    private static float DrawSemiboldAt(ImDrawListPtr dl, Vector2 pos, string text, uint ink, float room)
    {
        var shift = MathF.Max(0.5f, UiMetrics.Px(0.5f));
        Chrome.EllipsisTextAt(dl, pos, MathF.Max(1f, room), text, ink);
        Chrome.EllipsisTextAt(dl, pos + new Vector2(shift, 0f), MathF.Max(1f, room - shift), text, ink);
        return ImGui.CalcTextSize(text).X + shift;
    }

    /// <summary>
    /// A row's "…" menu: Show in the Journal, Flag the giver, travel (Teleport, Walk, Go to giver, as the automation level
    /// shows them), Send to Questionable (at Full hand-offs) and, for a story the character started, Read the story so far.
    /// </summary>
    private void DrawStoryRowMenu(UiState ui, string menuId, StoryRowView row, QuestRecord quest)
    {
        using var popup = ImRaii.Popup(menuId);
        if (!popup)
        {
            return;
        }

        UiMetrics.ApplyFontScale();
        if (ImGui.MenuItem(Strings.StoriesShowInJournal))
        {
            Reveal(ui, quest);
        }

        if (Links is { } links)
        {
            if (ImGui.MenuItem(Strings.StoriesFlagGiver, enabled: links.CanFlagMap(quest)))
            {
                links.FlagMap(quest);
            }

            TravelControls.MenuItems(links, quest, Strings.StoriesTeleport);
        }

        if (row.RowIds.Count > 0 && AutomationGate.Questionable(Questionable) is { } questionable)
        {
            questionable.DrawSubmenu(MainWindow.QuestionableHost, Strings.QuestionableSendButton, row.RowIds, static rows => rows);
        }

        if (row.RecapQuest != 0 && ImGui.MenuItem(Strings.RecapReadChain))
        {
            ui.OpenRecap(new RecapRequest(row.RecapQuest));
        }
    }
}
