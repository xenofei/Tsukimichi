using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The Moon Road art of the Journal tree (design v4 §5, §7.2, feature plan v4 V2), drawn under Full and Quiet flair
/// (<see cref="Theme.ShowRules"/>): the header line ("✦ JOURNAL", a fading brass rule and the overall count in the
/// Numeral role), the moon-road dividers between the story, side and virtual blocks, and the road under each row, a
/// 2 px track whose gold walked part is the node's completion. Plain flair keeps the 1.3 tree (the mini bar and the
/// section rules). Everything is draw-list work over plain <c>Dummy</c> items, so keyboard navigation, the tiers and the
/// tour rects are untouched; texts are measured once per font size and language.
/// </summary>
public sealed partial class TreePane
{
    /// <summary>The header line's height (the mockup's 28), the sigil's size, and the gap after the sigil and around the rule.</summary>
    private const float HeaderLogical = 28f;
    private const float HeaderSigilLogical = 10f;
    private const float HeaderGapLogical = 7f;
    private const float HeaderPadLogical = 6f;

    /// <summary>A rule shorter than this is left out rather than drawn as a stub.</summary>
    private const float HeaderRuleMinLogical = 16f;

    /// <summary>A divider's line box and the height of its three phases (the mockup's 14 px svg with 4 px margins).</summary>
    private const float DividerLogical = 20f;
    private const float DividerPhasesLogical = 10f;
    private const float DividerPadLogical = 8f;

    /// <summary>The road: 2 px thick, lifted 2 px off the row's bottom edge, ending 8 px short of the pane's edge.</summary>
    private const float RoadLogical = 2f;
    private const float RoadLiftLogical = 2f;
    private const float RoadEndLogical = 8f;

    // The header's texts and widths, rebuilt when the language, the overall count or the font size changes.
    private string headerTitle = string.Empty;
    private int headerLanguage = -1;
    private float headerTitleWidth;
    private float headerCountWidth;
    private string headerCountMeasured = string.Empty;
    private float headerMeasuredAt = -1f;

    /// <summary>
    /// The header: the sigil star, "JOURNAL" in the Eyebrow role (uppercase; the caption role stands in where the game
    /// font lacks a glyph), a GiltRule fading toward the count, and the overall "done / total" right-aligned in the
    /// Numeral role. A narrow pane drops the count, then the rule; the title is never cut below the sigil.
    /// </summary>
    private void DrawHeader(float width)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = MathF.Round(MathF.Max(UiMetrics.Px(HeaderLogical), ImGui.GetTextLineHeight() + UiMetrics.Px(8f)));
        ImGui.Dummy(new Vector2(MathF.Max(1f, width), height));
        if (!ImGui.IsItemVisible())
        {
            return;
        }

        var hovered = ImGui.IsItemHovered();
        if (headerLanguage != Localization.Loc.Version)
        {
            headerLanguage = Localization.Loc.Version;
            headerTitle = Strings.TabJournal.ToUpper(CultureInfo.CurrentCulture);
            headerMeasuredAt = -1f;
        }

        var count = allNode.CountText;
        var fontSize = ImGui.GetFontSize();
        if (headerMeasuredAt != fontSize || !ReferenceEquals(headerCountMeasured, count))
        {
            using (Typography.Eyebrow(headerTitle))
            {
                headerTitleWidth = ImGui.CalcTextSize(headerTitle).X;
            }

            using (Typography.Numeral(count))
            {
                headerCountWidth = count.Length > 0 ? ImGui.CalcTextSize(count).X : 0f;
            }

            headerCountMeasured = count;
            headerMeasuredAt = fontSize;
        }

        var dl = ImGui.GetWindowDrawList();
        var centerY = MathF.Round(origin.Y + (height * 0.5f));
        var pad = UiMetrics.Px(HeaderPadLogical);
        var gap = UiMetrics.Px(HeaderGapLogical);
        var sigil = UiMetrics.Px(HeaderSigilLogical);
        var left = origin.X + pad;
        var right = origin.X + width - pad;
        Ornament.Sigil(dl, new Vector2(MathF.Round(left + (sigil * 0.5f)), centerY), sigil);

        var titleX = left + sigil + gap;
        var titleRoom = MathF.Max(0f, right - titleX);
        using (Typography.Eyebrow(headerTitle))
        {
            var y = MathF.Round(centerY - (ImGui.GetTextLineHeight() * 0.5f));
            Chrome.EllipsisTextAt(dl, new Vector2(titleX, y), titleRoom, headerTitle, Theme.U32(Theme.Surface.TextSecondary), headerTitleWidth);
        }

        var ruleX = titleX + headerTitleWidth + gap;
        var ruleMin = UiMetrics.Px(HeaderRuleMinLogical);
        var countX = right - headerCountWidth;
        var showCount = headerCountWidth > 0f && countX - gap - ruleMin >= ruleX;
        var ruleEnd = showCount ? countX - gap : right;
        if (ruleEnd - ruleX >= ruleMin)
        {
            Ornament.Rule(dl, new Vector2(ruleX, centerY), ruleEnd - ruleX);
        }

        if (showCount)
        {
            using (Typography.Numeral(count))
            {
                var y = MathF.Round(centerY - (ImGui.GetTextLineHeight() * 0.5f));
                dl.AddText(new Vector2(MathF.Round(countX), y), Theme.U32(Theme.Surface.TextSecondary), count);
            }

            if (hovered && ImGui.IsMouseHoveringRect(new Vector2(countX, origin.Y), new Vector2(right, origin.Y + height), false))
            {
                UiMetrics.Tooltip(Strings.FillingMoonTooltip, allNode.HoverText.Length > 0 ? allNode.HoverText : allNode.ProgressText);
            }
        }
    }

    /// <summary>
    /// A moon-road divider before a node of <paramref name="next"/> when it starts a new block (story → side → virtual;
    /// never after All quests), under Full and Quiet flair; <paramref name="block"/> tracks the block drawn last.
    /// </summary>
    private static void BlockDivider(ref JournalBlock block, JournalBlock next, float width, bool ornaments)
    {
        var divide = ornaments && JournalBlocks.DividerBetween(block, next);
        block = next;
        if (!divide)
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var height = MathF.Round(UiMetrics.Px(DividerLogical));
        ImGui.Dummy(new Vector2(MathF.Max(1f, width), height));
        if (!ImGui.IsItemVisible())
        {
            return;
        }

        var pad = UiMetrics.Px(DividerPadLogical);
        var center = new Vector2(MathF.Round(origin.X + (width * 0.5f)), MathF.Round(origin.Y + (height * 0.5f)));
        Ornament.Divider(ImGui.GetWindowDrawList(), center, width - (2f * pad), UiMetrics.Px(DividerPhasesLogical));
    }

    /// <summary>
    /// The road under a row, from <paramref name="left"/> to <paramref name="right"/> with its bottom at
    /// <paramref name="bottom"/>: the unwalked track in NightLine and the walked part, <paramref name="fraction"/> of the
    /// length, as a MoonDeep → Moon gradient (Moon → MoonHigh on the selected row). Under the high-contrast palette the
    /// walked part is solid Moon and the track VeilLine (§10.2). A started road always shows at least its own thickness.
    /// </summary>
    private static void DrawRoad(ImDrawListPtr dl, float left, float right, float bottom, float fraction, bool selected)
    {
        var length = MathF.Round(right - left);
        if (!(length > 0f))
        {
            return;
        }

        var thickness = MathF.Max(1f, MathF.Round(UiMetrics.Px(RoadLogical)));
        var max = new Vector2(MathF.Round(left) + length, MathF.Round(bottom));
        var min = new Vector2(MathF.Round(left), max.Y - thickness);
        var highContrast = Theme.Glyphs.HighContrast;
        dl.AddRectFilled(min, max, highContrast ? Theme.VeilLineU32 : Theme.NightLineU32, thickness * 0.5f);

        var walked = MathF.Round(TreeRoad.Walked(length, fraction, thickness));
        if (walked <= 0f)
        {
            return;
        }

        var end = new Vector2(min.X + walked, max.Y);
        if (highContrast)
        {
            dl.AddRectFilled(min, end, Theme.MoonU32, thickness * 0.5f);
            return;
        }

        var from = selected ? Theme.MoonU32 : Theme.MoonDeepU32;
        var to = selected ? Theme.MoonHighU32 : Theme.MoonU32;
        dl.AddRectFilledMultiColor(min, end, from, to, to, from);
    }
}
