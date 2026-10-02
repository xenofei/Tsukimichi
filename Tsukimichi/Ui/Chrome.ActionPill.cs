using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>What an <see cref="Chrome.ActionPill"/> says about its action.</summary>
public enum PillTone : byte
{
    /// <summary>The one action the pane suggests first: the accent.</summary>
    Primary,

    /// <summary>Another action of the same weight.</summary>
    Normal,

    /// <summary>Clickable, but there is little to gain (Teleport while the player already stands closer to the giver).</summary>
    Quiet,

    /// <summary>Stops what a hand-off started.</summary>
    Danger,
}

public static partial class Chrome
{
    /// <summary>A brass edge drawn on a disabled pill keeps this much of the enabled one's alpha.</summary>
    private const float DisabledEdgeShare = 0.45f;

    /// <summary>The height of an <see cref="ActionPill"/>: one <see cref="UiMetrics.MinTarget"/>.</summary>
    public static float ActionPillHeight => UiMetrics.MinTarget;

    /// <summary>
    /// The widths an <see cref="ActionPill"/> takes with <paramref name="label"/>, with <paramref name="shortLabel"/>
    /// and with its icon alone, for <see cref="ActionPillFit.Fit"/>.
    /// </summary>
    public static PillWidths ActionPillWidths(string icon, string label, string shortLabel)
    {
        var iconWidth = IconWidth(icon);
        var full = ActionPillFit.LabelledWidth(iconWidth, ImGui.CalcTextSize(label).X, UiMetrics.Scale);
        var brief = ReferenceEquals(label, shortLabel) || string.Equals(label, shortLabel, StringComparison.Ordinal)
            ? full
            : ActionPillFit.LabelledWidth(iconWidth, ImGui.CalcTextSize(shortLabel).X, UiMetrics.Scale);
        return new PillWidths(full, MathF.Min(full, brief), ActionPillFit.IconOnlyWidth(ActionPillHeight));
    }

    /// <summary>The width of one <see cref="ActionPill"/>: labelled, or its icon alone when <paramref name="label"/> is null.</summary>
    public static float ActionPillWidth(string icon, string? label) =>
        label is null
            ? ActionPillFit.IconOnlyWidth(ActionPillHeight)
            : ActionPillFit.LabelledWidth(IconWidth(icon), ImGui.CalcTextSize(label).X, UiMetrics.Scale);

    /// <summary>
    /// A labelled action pill (1.10, the detail pane's travel and automation row, the Hand in and Duties hand-offs):
    /// <see cref="UiMetrics.MinTarget"/> tall, an icon and a label, or the icon alone (still pill-shaped) when
    /// <paramref name="label"/> is null. Its look follows the Flair level: Full a brass edge, the
    /// <see cref="PillTone.Primary"/> one filled with the accent; Quiet the brass edge alone, the primary in accent ink;
    /// Plain the standard button fill, still labelled, the primary keeping the accent wash it had in 1.3. Under the
    /// high-contrast palette the edge is a solid line. A disabled pill stays labelled and dimmed and still shows its
    /// tooltip; <see cref="PillTone.Danger"/> (Stop) is Eclipse. Nothing animates. A real, focusable item with the
    /// focus ring; a null <paramref name="tooltip"/> leaves the hover text to the caller (check
    /// <c>IsItemHovered(AllowWhenDisabled)</c> after the call). Returns true when clicked while enabled.
    /// </summary>
    public static bool ActionPill(string id, string icon, string? label, PillTone tone, bool enabled, string? tooltip = null)
    {
        var height = ActionPillHeight;
        var iconSize = IconSize(icon);
        var labelSize = label is null ? Vector2.Zero : ImGui.CalcTextSize(label);
        var width = label is null
            ? ActionPillFit.IconOnlyWidth(height)
            : ActionPillFit.LabelledWidth(iconSize.X, labelSize.X, UiMetrics.Scale);
        var min = ImGui.GetCursorScreenPos();
        ImGui.BeginDisabled(!enabled);
        var clicked = ImGui.InvisibleButton(id, new Vector2(width, height));
        ImGui.EndDisabled();
        var hovered = enabled && ImGui.IsItemHovered();
        var held = enabled && ImGui.IsItemActive();

        var max = min + new Vector2(width, height);
        var rounding = height * 0.5f;
        var (fill, edge, ink) = PillColors(tone, enabled, hovered, held);
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(min, max, fill, rounding);
        if (edge != 0)
        {
            var thickness = Theme.Glyphs.HighContrast ? MathF.Max(1.5f, UiMetrics.Px(1.5f)) : UiMetrics.Hairline;
            dl.AddRect(min, max, edge, rounding, ImDrawFlags.None, thickness);
        }

        ImGui.PushFont(UiBuilder.IconFont);
        var iconX = label is null ? min.X + ((width - iconSize.X) * 0.5f) : min.X + UiMetrics.Px(ActionPillFit.PadStartLogical);
        dl.AddText(new Vector2(MathF.Round(iconX), MathF.Round(min.Y + ((height - iconSize.Y) * 0.5f))), ink, icon);
        ImGui.PopFont();
        if (label is not null)
        {
            var labelX = iconX + iconSize.X + UiMetrics.Px(ActionPillFit.IconGapLogical);
            dl.AddText(new Vector2(MathF.Round(labelX), MathF.Round(min.Y + ((height - labelSize.Y) * 0.5f))), ink, label);
        }

        FocusRing(rounding);
        if (tooltip is not null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(tooltip);
        }

        return clicked && enabled;
    }

    /// <summary>The fill, edge (0 for none) and ink of a pill at the frame's Flair level and palette.</summary>
    private static (uint Fill, uint Edge, uint Ink) PillColors(PillTone tone, bool enabled, bool hovered, bool held)
    {
        var s = Theme.Surface;
        var flair = Theme.Flair;
        var highContrast = Theme.Glyphs.HighContrast;
        var plain = flair == Flair.Plain;
        var brass = Theme.WithAlpha(s.Ornament, Theme.OrnamentAlpha(CardBrassAlpha));
        var wash = held ? 0.28f : hovered ? 0.22f : 0.16f;

        if (!enabled)
        {
            // Dimmed, never hidden: the label stays readable in the disabled tone.
            var dimEdge = highContrast ? Theme.U32(s.TextDisabled)
                : plain ? 0u
                : Theme.WithAlpha(s.Ornament, CardBrassAlpha * DisabledEdgeShare);
            return (Theme.WithAlpha(s.Raised, 0.6f * s.Raised.W), dimEdge, Theme.U32(s.TextDisabled));
        }

        switch (tone)
        {
            case PillTone.Danger:
            {
                var fill = plain
                    ? Theme.WithAlpha(Theme.Eclipse, held ? 1f : hovered ? 0.9f : 0.75f)
                    : Theme.WithAlpha(Theme.Eclipse, wash + 0.08f);
                var edge = plain && !highContrast ? 0u : Theme.WithAlpha(Theme.Eclipse, highContrast ? 1f : 0.75f);
                var ink = plain ? Theme.U32(Theme.Silver) : Theme.U32(hovered ? s.Text : Theme.EclipseText);
                return (fill, edge, ink);
            }

            case PillTone.Primary when flair == Flair.Full || plain:
            {
                // Full: accent fill inside the brass edge. Plain: the 1.3 primary, an accent wash and edge.
                var edge = plain
                    ? Theme.WithAlpha(Theme.Accent, highContrast ? 1f : 0.45f)
                    : brass;
                return (Theme.WithAlpha(Theme.Accent, wash), edge, Theme.AccentU32);
            }

            case PillTone.Primary:
                // Quiet: the brass edge alone, the accent in the ink.
                return (RaisedFill(s, hovered, held), brass, Theme.AccentU32);

            default:
            {
                var ink = tone == PillTone.Quiet && !hovered ? Theme.U32(s.TextSecondary) : Theme.U32(s.Text);
                if (plain)
                {
                    var fill = ImGui.GetColorU32(held ? ImGuiCol.ButtonActive : hovered ? ImGuiCol.ButtonHovered : ImGuiCol.Button);
                    return (fill, highContrast ? Theme.U32(s.StrongLine with { W = 1f }) : 0u, ink);
                }

                return (RaisedFill(s, hovered, held), brass, ink);
            }
        }
    }

    private static uint RaisedFill(SurfaceColors s, bool hovered, bool held) =>
        held ? Theme.WithAlpha(s.Text, 0.18f) : hovered ? Theme.WithAlpha(s.Hover, 1f) : Theme.U32(s.Raised);

    private static Vector2 IconSize(string icon)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        var size = ImGui.CalcTextSize(icon);
        ImGui.PopFont();
        return size;
    }

    private static float IconWidth(string icon) => IconSize(icon).X;
}
