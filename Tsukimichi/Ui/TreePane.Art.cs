using System;
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
/// tour rects are untouched.
/// </summary>
public sealed partial class TreePane
{
    /// <summary>The header's inset from the pane's edges.</summary>
    private const float HeaderPadLogical = 6f;

    /// <summary>A divider's line box and the height of its three phases (the mockup's 14 px svg with 4 px margins).</summary>
    private const float DividerLogical = 20f;
    private const float DividerPhasesLogical = 10f;
    private const float DividerPadLogical = 8f;

    /// <summary>The road: 2 px thick, lifted 2 px off the row's bottom edge, ending 8 px short of the pane's edge.</summary>
    private const float RoadLogical = 2f;
    private const float RoadLiftLogical = 2f;
    private const float RoadEndLogical = 8f;

    /// <summary>The fraction each orbit last showed, so a row whose tween state was pruned out of view does not refill.</summary>
    private readonly FillMemory filled = new();

    /// <summary>The character the remembered fills belong to (the viewed content id, 0 for none).</summary>
    private ulong filledOwner;

    /// <summary>
    /// The header: the shared heading line (<see cref="SectionHeading.DrawLine"/>: the sigil star, "JOURNAL" cased for
    /// the language, the rule) with the overall "done / total" right-aligned in the Numeral role, inset from the pane's
    /// edges and as tall as the other headings plus their top pad again below. A narrow pane drops the count (named on
    /// hover), then the rule; the title is cut last. Measured every frame, so a heading font that finishes building
    /// mid-session is measured in the face it draws in.
    /// </summary>
    private void DrawHeader(float width)
    {
        var origin = ImGui.GetCursorScreenPos();
        var pad = UiMetrics.Px(HeaderPadLogical);
        var count = allNode.CountText;
        ImGui.SetCursorScreenPos(new Vector2(origin.X + pad, origin.Y));
        var drawn = SectionHeading.DrawLine(Strings.TabJournal, count, Theme.U32(Theme.Surface.TextSecondary), pad, sigil: true, Theme.Flair, CaptionOverflow.Tooltip, numeral: true);
        if (drawn.CaptionShown && ImGui.IsItemHovered())
        {
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            if (ImGui.IsMouseHoveringRect(new Vector2(drawn.CaptionMin.X, min.Y), new Vector2(drawn.CaptionMax.X, max.Y), false))
            {
                UiMetrics.Tooltip(Strings.FillingMoonTooltip, allNode.HoverText.Length > 0 ? allNode.HoverText : allNode.ProgressText);
            }
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, ImGui.GetCursorScreenPos().Y));
        ImGui.Dummy(new Vector2(MathF.Max(1f, width), UiMetrics.Px(HeadingLayout.TopPadLogical)));
    }

    /// <summary>
    /// An orbit's shown fraction (<see cref="Motion.Fill"/>): a node never shown fills from empty, one shown before
    /// starts from the fraction it last showed, so a row coming back into view does not replay its fill.
    /// </summary>
    private float NodeFill(ulong key, float fraction)
    {
        var shown = Motion.Fill(key, fraction, filled.StartFor(key));
        filled.Remember(key, fraction);
        return shown;
    }

    /// <summary>Forgets the remembered fills when another character is viewed (their fractions mean another journal).</summary>
    private void ForgetFillsOnNewCharacter(QueryRunner runner)
    {
        var owner = runner.PinOwner ?? 0;
        if (owner != filledOwner)
        {
            filledOwner = owner;
            filled.Clear();
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
