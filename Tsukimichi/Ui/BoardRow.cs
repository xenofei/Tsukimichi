using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>One run of a board row's second line: plain text, a quest name in semibold after its moon, or a separator.</summary>
/// <param name="Text">The words.</param>
/// <param name="Moon">A quest's state: the moon drawn before <paramref name="Text"/>, which is then semibold Text; null for plain Secondary words.</param>
public readonly record struct BoardRun(string Text, QuestState? Moon = null);

/// <summary>What a board row's interaction did this frame.</summary>
internal readonly record struct BoardRowResult(bool Hovered, bool Teleport, bool MenuRequested);

/// <summary>
/// The two-line board rows of 1.21 (spec-1.21 decision 17: the Triple Triad card, the zones board): 44 px at 100 %,
/// scaled with the body text; an optional game icon; line 1 the name in Text with its fact (a place or a level span)
/// and an optional neutral chip; line 2 what is left, in Secondary words; a reserved slot at the trailing end holding
/// Teleport and "…", shown while the row is hovered or focused, so a hover never covers text and nothing moves.
/// </summary>
internal static class BoardRow
{
    /// <summary>The row's height at 100 %, logical px.</summary>
    public const float RowLogical = 44f;

    private const float IconLogical = 26f;
    private const float GapLogical = 8f;

    private static readonly string MoreGlyph = Chrome.Icon(Dalamud.Interface.FontAwesomeIcon.EllipsisH);

    /// <summary>The row's height: 44 px at 100 %, never under two body lines and their air.</summary>
    public static float Height => MathF.Round(MathF.Max(UiMetrics.Px(RowLogical), (2f * ImGui.GetTextLineHeight()) + UiMetrics.Px(8f)));

    /// <summary>The reserved slot's width: Teleport (when shown at the automation level) and "…".</summary>
    public static float SlotWidth(bool teleport, string teleportLabel) =>
        (teleport ? TravelControls.RowButtonWidth(ActionIcons.TeleportIcon, teleportLabel) + UiMetrics.Px(6f) : 0f) + UiMetrics.MinTarget;

    /// <summary>
    /// Draws the row at the cursor, <paramref name="width"/> wide, and moves the cursor under it.
    /// </summary>
    /// <param name="hoverKey">A key unique to the row, for its hover wash (<see cref="Motion.Hover"/>).</param>
    /// <param name="icon">The game icon at the leading end; 0 draws none and the text starts at the edge.</param>
    /// <param name="name">Line 1, in Text.</param>
    /// <param name="fact">Line 1's fact: the place at the trailing end of the text column (<paramref name="factAfterName"/> false) or the level span after the name.</param>
    /// <param name="chip">A neutral chip after the fact ("new since 7.4"); null for none.</param>
    /// <param name="line2">Line 2's runs.</param>
    /// <param name="teleport">Teleport in the slot: null hides it; false shows it disabled.</param>
    /// <param name="teleportTooltip">Teleport's hover text (why it cannot run, or where it goes).</param>
    /// <param name="slot">Whether the slot holds anything at all (a masked row has no actions).</param>
    public static BoardRowResult Draw(
        string id,
        ulong hoverKey,
        float width,
        uint icon,
        ITextureProvider? textures,
        string name,
        string fact,
        bool factAfterName,
        string? chip,
        ReadOnlySpan<BoardRun> line2,
        bool? teleport,
        string teleportLabel,
        string teleportTooltip,
        bool slot)
    {
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        var height = Height;
        var max = min + new Vector2(width, height);
        var line = ImGui.GetTextLineHeight();
        var gap = UiMetrics.Px(GapLogical);
        ImGui.PushID(id);
        try
        {
            var rowHovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows) && ImGui.IsMouseHoveringRect(min, max);
            var hover = Motion.Hover(hoverKey, rowHovered);
            var rounding = Theme.Flair == Flair.Plain ? UiMetrics.Px(2f) : UiMetrics.Px(6f);
            if (hover > 0.01f)
            {
                dl.AddRectFilled(min - new Vector2(UiMetrics.Px(4f), 0f), max + new Vector2(UiMetrics.Px(4f), 0f), Theme.WithAlpha(s.Hover, s.Hover.W * hover), rounding);
            }

            // The icon on its own column, centred on the two lines.
            var x = min.X;
            if (icon != 0)
            {
                var side = MathF.Round(MathF.Min(UiMetrics.Px(IconLogical), height - UiMetrics.Px(8f)));
                var iconMin = new Vector2(x, MathF.Round(min.Y + ((height - side) * 0.5f)));
                if (textures is null || !GameIcon.DrawAt(dl, textures, icon, iconMin, iconMin + new Vector2(side), UiMetrics.Px(3f)))
                {
                    MoonGlyph.DrawVeiled(dl, iconMin + new Vector2(side * 0.5f), side * 0.32f, 0.6f);
                }

                x += side + gap;
            }

            var slotWidth = slot ? SlotWidth(teleport is not null, teleportLabel) : 0f;
            var textRight = max.X - slotWidth - (slot ? gap : 0f);
            var top = MathF.Round(min.Y + ((height - (2f * line) - UiMetrics.Px(2f)) * 0.5f));
            var second = top + line + UiMetrics.Px(2f);
            var room = MathF.Max(1f, textRight - x);

            // Line 1: the name, then the fact (after the name, or at the column's end), then the chip.
            var factWidth = fact.Length > 0 ? ImGui.CalcTextSize(fact).X : 0f;
            float chipWidth = 0f;
            Vector2 chipText = default;
            if (chip is { Length: > 0 })
            {
                using (Typography.Caption())
                {
                    chipText = ImGui.CalcTextSize(chip);
                }

                chipWidth = chipText.X + UiMetrics.Px(14f);
            }

            var tail = (factWidth > 0f ? factWidth + gap : 0f) + (chipWidth > 0f ? chipWidth + gap : 0f);
            var nameWidth = ImGui.CalcTextSize(name).X;
            var nameRoom = MathF.Max(UiMetrics.Px(60f), room - tail);
            var nameCut = Chrome.EllipsisTextAt(dl, new Vector2(x, top), nameRoom, name, Theme.U32(s.Text), nameWidth);
            var after = x + MathF.Min(nameWidth, nameRoom) + gap;
            if (factWidth > 0f)
            {
                var factX = factAfterName ? after : MathF.Max(after, textRight - factWidth - (chipWidth > 0f ? chipWidth + gap : 0f));
                var factInk = Theme.U32(factAfterName ? s.TextTertiary : s.TextSecondary);
                Chrome.EllipsisTextAt(dl, new Vector2(factX, top), MathF.Max(1f, textRight - factX), fact, factInk, factWidth);
                after = factX + factWidth + gap;
            }

            if (chipWidth > 0f && after + chipWidth <= textRight)
            {
                using (Typography.Caption())
                {
                    var chipMin = new Vector2(after, top + ((line - chipText.Y) * 0.5f) - UiMetrics.Px(1f));
                    var chipMax = chipMin + new Vector2(chipWidth, chipText.Y + UiMetrics.Px(2f));
                    var chipRound = Theme.Flair == Flair.Plain ? UiMetrics.Px(2f) : (chipMax.Y - chipMin.Y) * 0.5f;
                    dl.AddRect(chipMin, chipMax, Theme.U32(Theme.Glyphs.HighContrast ? s.StrongLine : s.Line), chipRound, ImDrawFlags.None, Theme.Glyphs.HighContrast ? MathF.Max(1.5f, UiMetrics.Hairline) : UiMetrics.Hairline);
                    dl.AddText(new Vector2(chipMin.X + UiMetrics.Px(7f), chipMin.Y + UiMetrics.Px(1f)), Theme.U32(s.Text), chip);
                }
            }

            // Line 2: Secondary words; a quest's moon and its name in semibold Text.
            var cut2 = DrawRuns(dl, new Vector2(x, second), textRight, line, line2);

            // The reserved slot: Teleport and "…", painted while the row is hovered (or one of them has focus).
            var result = new BoardRowResult(rowHovered, false, rowHovered && ImGui.IsMouseReleased(ImGuiMouseButton.Right));
            if (slot)
            {
                var slotX = max.X - slotWidth;
                var frame = ImGui.GetFrameHeight();
                var buttonY = MathF.Round(min.Y + ((height - frame) * 0.5f));
                var shown = rowHovered || ImGui.IsPopupOpen(MenuId);
                if (shown && teleport is { } canTeleport)
                {
                    ImGui.SetCursorScreenPos(new Vector2(slotX, buttonY));
                    var clicked = Chrome.ActionPill("##teleport", ActionIcons.TeleportIcon, teleportLabel, PillTone.Normal, canTeleport, size: PillLayout.Row);
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && teleportTooltip.Length > 0)
                    {
                        UiMetrics.Tooltip(teleportTooltip);
                    }

                    result = result with { Teleport = clicked && canTeleport };
                }

                if (shown)
                {
                    var more = UiMetrics.MinTarget;
                    ImGui.SetCursorScreenPos(new Vector2(max.X - more, MathF.Round(min.Y + ((height - more) * 0.5f))));
                    if (Chrome.IconButtonRound("##more", MoreGlyph, Strings.BoardRowMoreTooltip))
                    {
                        result = result with { MenuRequested = true };
                    }
                }
            }

            if (rowHovered && !ImGui.IsAnyItemHovered() && (nameCut || cut2))
            {
                UiMetrics.Tooltip(name, Joined(line2));
            }

            ImGui.SetCursorScreenPos(min);
            ImGui.Dummy(new Vector2(width, height));
            return result;
        }
        finally
        {
            ImGui.PopID();
        }
    }

    /// <summary>The "…" menu's popup id, in the row's id scope: open it on <see cref="BoardRowResult.MenuRequested"/>.</summary>
    public const string MenuId = "##boardRowMenu";

    /// <summary>Draws the runs from <paramref name="pos"/> up to <paramref name="right"/>; the last run that does not fit ends in an ellipsis. Returns whether anything was cut.</summary>
    private static bool DrawRuns(ImDrawListPtr dl, Vector2 pos, float right, float line, ReadOnlySpan<BoardRun> runs)
    {
        var s = Theme.Surface;
        var x = pos.X;
        foreach (var run in runs)
        {
            if (run.Text.Length == 0 && run.Moon is null)
            {
                continue;
            }

            if (run.Moon is { } state)
            {
                var glyph = MathF.Min(line, UiMetrics.InlineGlyphSize(line));
                if (x + glyph > right)
                {
                    return true;
                }

                MoonGlyph.Draw(dl, new Vector2(x + (glyph * 0.5f), pos.Y + (line * 0.5f)), glyph * 0.42f, state);
                x += glyph + UiMetrics.Px(4f);
                var width = ImGui.CalcTextSize(run.Text).X + MathF.Max(0.5f, UiMetrics.Px(0.5f));
                var ink = Theme.U32(s.Text);
                var cut = Chrome.EllipsisTextAt(dl, pos with { X = x }, MathF.Max(1f, right - x), run.Text, ink, width);
                if (!cut)
                {
                    dl.AddText(new Vector2(x + MathF.Max(0.5f, UiMetrics.Px(0.5f)), pos.Y), ink, run.Text);
                }

                x += width;
                if (cut)
                {
                    return true;
                }

                continue;
            }

            var size = ImGui.CalcTextSize(run.Text).X;
            if (Chrome.EllipsisTextAt(dl, pos with { X = x }, MathF.Max(1f, right - x), run.Text, Theme.U32(s.TextSecondary), size))
            {
                return true;
            }

            x += size;
        }

        return false;
    }

    private static string Joined(ReadOnlySpan<BoardRun> runs)
    {
        var text = string.Empty;
        foreach (var run in runs)
        {
            text += run.Text;
        }

        return text;
    }
}
