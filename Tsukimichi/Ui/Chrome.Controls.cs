using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The Settings window's controls (feature plan v6 U7, decision 7): a moon toggle in place of a checkbox, and a
/// segmented picker in place of a sentence of radio buttons. Both are one item the size of the control, focusable,
/// keyboard-operable (Space or Enter), show their hover and state in the Night surfaces, and ease between states with
/// <see cref="Motion"/> (at once under Reduce motion or while scrolling).
/// </summary>
public static partial class Chrome
{
    /// <summary>The moon toggle's logical size: a 36 × 20 pill.</summary>
    public const float ToggleWidthLogical = 36f;
    public const float ToggleHeightLogical = 20f;

    // Motion key tags ("TGL", "SEG") with the item id in the low half.
    private const uint ToggleTag = 0x0054_474C;
    private const uint SegmentTag = 0x0053_4547;

    /// <summary>The moon toggle's size in pixels: never shorter than 18 px so the knob stays a target at UiScale 0.9.</summary>
    public static Vector2 ToggleSize => new(MathF.Max(32f, UiMetrics.Px(ToggleWidthLogical)), MathF.Max(18f, UiMetrics.Px(ToggleHeightLogical)));

    /// <summary>
    /// A moon toggle: a pill whose knob slides right and turns into a gold crescent on a gold-washed track when on, and
    /// rests left as a plain disc on the sunken track when off. Returns true on the frame it was flipped (the value is
    /// already flipped). Disabled through <c>ImGui.BeginDisabled</c> it dims and ignores clicks.
    /// </summary>
    public static bool MoonToggle(string id, ref bool value)
    {
        var size = ToggleSize;
        var min = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, size);
        if (clicked)
        {
            value = !value;
        }

        var itemId = ImGuiP.GetItemID();
        var hovered = ImGui.IsItemHovered();
        var t = Motion.Lerp(Motion.Key(ToggleTag, itemId), value ? 1f : 0f, MotionMath.SelectRate);
        var hover = Motion.Lerp(itemId, hovered ? 1f : 0f);
        var disabled = ImGui.GetStyle().Alpha < 0.99f;
        var alpha = ImGui.GetStyle().Alpha;

        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var max = min + size;
        var radius = size.Y * 0.5f;

        // The track: sunken when off, a gold wash when on; the border goes gold with it.
        var off = Theme.WithAlphaVector(s.Sunken, alpha);
        var on = Theme.WithAlphaVector(Vector4.Lerp(s.Sunken, Theme.Moon, 0.38f), alpha);
        dl.AddRectFilled(min, max, Theme.U32(Vector4.Lerp(off, on, t)), radius);
        if (hover > 0f && !disabled)
        {
            dl.AddRectFilled(min, max, Theme.WithAlpha(s.Hover, 0.5f * hover * alpha), radius);
        }

        var border = Vector4.Lerp(s.Line, Theme.Moon, t);
        dl.AddRect(min, max, Theme.WithAlpha(border, alpha), radius, ImDrawFlags.None, UiMetrics.Hairline);

        // The knob: a disc in the secondary tone when off, a crescent of moonlight when on.
        var inset = MathF.Max(2f, UiMetrics.Px(3f));
        var knob = radius - inset;
        var left = min.X + radius;
        var right = max.X - radius;
        var center = new Vector2(left + ((right - left) * t), min.Y + radius);
        var knobInk = Vector4.Lerp(s.TextSecondary, Theme.MoonHigh, t);
        dl.AddCircleFilled(center, knob, Theme.WithAlpha(knobInk, alpha), 24);
        if (t > 0.01f)
        {
            // The crescent: a disc of the track colour bites the knob from the upper left as it turns on.
            var bite = new Vector2(center.X - (knob * 0.55f * t), center.Y - (knob * 0.35f * t));
            dl.AddCircleFilled(bite, knob * 0.82f * t, Theme.U32(Vector4.Lerp(off, on, t)), 24);
        }

        FocusRing(radius);
        return clicked;
    }

    /// <summary>
    /// The width a <see cref="Segmented"/> picker takes for <paramref name="labels"/>: equal cells as wide as the widest
    /// label and its padding, or <paramref name="room"/> when that is less (the labels then ellipsise).
    /// </summary>
    public static float SegmentedWidth(ReadOnlySpan<string> labels, float room = float.MaxValue)
    {
        var widest = 0f;
        foreach (var label in labels)
        {
            widest = MathF.Max(widest, ImGui.CalcTextSize(label).X);
        }

        var cell = widest + (2f * UiMetrics.Px(SegmentPadX));
        return MathF.Min(cell * labels.Length, MathF.Max(0f, room));
    }

    /// <summary>
    /// A segmented picker (feature plan v6 U7): equal cells on a sunken track, the chosen one raised with a 2 px gold
    /// underline that slides to a new choice. <paramref name="selected"/> is the index into <paramref name="labels"/>;
    /// returns true on the frame it changed. Each cell is a focusable item; <paramref name="tooltips"/> show on hover.
    /// </summary>
    public static bool Segmented(string id, ref int selected, ReadOnlySpan<string> labels, float width, ReadOnlySpan<string> tooltips = default)
    {
        var count = labels.Length;
        if (count == 0)
        {
            return false;
        }

        var height = UiMetrics.MinTarget;
        var origin = ImGui.GetCursorScreenPos();
        var cell = MathF.Floor(MathF.Max(1f, width) / count);
        var total = cell * count;
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var rounding = UiMetrics.Px(5f);
        var max = origin + new Vector2(total, height);
        var alpha = ImGui.GetStyle().Alpha;
        dl.AddRectFilled(origin, max, Theme.WithAlpha(s.Sunken, alpha), rounding);

        ImGui.PushID(id);
        var groupId = ImGui.GetID("##segments");
        var changed = false;
        var padX = UiMetrics.Px(SegmentPadX) * 0.5f;
        for (var i = 0; i < count; i++)
        {
            var min = new Vector2(origin.X + (i * cell), origin.Y);
            var cellMax = new Vector2(min.X + cell, max.Y);
            ImGui.SetCursorScreenPos(min);
            ImGui.PushID(i);
            if (ImGui.InvisibleButton("##seg", new Vector2(cell, height)) && selected != i)
            {
                selected = i;
                changed = true;
            }

            var hovered = ImGui.IsItemHovered();
            var hover = Motion.Lerp(ImGuiP.GetItemID(), hovered && selected != i ? 1f : 0f);
            if (hover > 0f)
            {
                dl.AddRectFilled(min, cellMax, Theme.WithAlpha(s.Hover, hover * alpha), rounding);
            }

            if (i > 0)
            {
                dl.AddLine(new Vector2(min.X, origin.Y + UiMetrics.Px(5f)), new Vector2(min.X, max.Y - UiMetrics.Px(5f)), Theme.WithAlpha(s.Line, alpha), UiMetrics.Hairline);
            }

            FocusRing(rounding);
            if (i < tooltips.Length && tooltips[i].Length > 0 && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(tooltips[i]);
            }

            ImGui.PopID();
        }

        // The chosen cell raised, its underline sliding from the last choice.
        var at = Motion.Lerp(Motion.Key(SegmentTag, groupId), Math.Clamp(selected, 0, count - 1), MotionMath.SelectRate);
        var chosenMin = new Vector2(origin.X + (at * cell), origin.Y);
        var chosenMax = new Vector2(chosenMin.X + cell, max.Y);
        dl.AddRectFilled(chosenMin, chosenMax, Theme.WithAlpha(s.Raised, alpha), rounding);
        dl.AddRect(chosenMin, chosenMax, Theme.WithAlpha(s.Line, alpha), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        var bar = MathF.Max(1f, UiMetrics.Px(2f));
        var barInset = UiMetrics.Px(8f);
        dl.AddRectFilled(new Vector2(chosenMin.X + barInset, chosenMax.Y - bar - UiMetrics.Px(2f)), new Vector2(chosenMax.X - barInset, chosenMax.Y - UiMetrics.Px(2f)), Theme.WithAlpha(Theme.Moon, alpha), bar * 0.5f);

        for (var i = 0; i < count; i++)
        {
            var label = labels[i];
            var chosen = selected == i;
            var ink = chosen ? s.Text : s.TextSecondary;
            var room = cell - (2f * padX);
            var textSize = ImGui.CalcTextSize(label);
            var x = origin.X + (i * cell) + padX + MathF.Max(0f, (room - textSize.X) * 0.5f);
            var y = origin.Y + ((height - textSize.Y) * 0.5f);
            EllipsisTextAt(dl, new Vector2(x, y), room, label, Theme.WithAlpha(ink, alpha), textSize.X);
        }

        ImGui.PopID();
        dl.AddRect(origin, max, Theme.WithAlpha(s.Line, alpha), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(max - origin);
        return changed;
    }
}
