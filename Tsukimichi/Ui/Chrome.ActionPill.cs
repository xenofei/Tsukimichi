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

    // 1.13 (feature plan v6 U8): a pill's hover glides in and out, and a new label or tone (Walk becoming Stop) fades in.
    private const uint PillHoverTag = 0x5049_4C48; // "PILH"
    private const uint PillSwapTag = 0x5049_4C53;  // "PILS"

    /// <summary>
    /// The height of an <see cref="ActionPill"/> at the Decoration level (docs/design/flair-v13 §1, "Spacing tokens"):
    /// 30 logical px at Full, 28 at Quiet, a 22 px button at Plain; never under the 24 px minimum target.
    /// </summary>
    public static float ActionPillHeight => MathF.Max(UiMetrics.Px(Theme.Spacing.PillHeight), 24f);

    /// <summary>Plain draws its action buttons as text only (no icon), so their widths leave the icon and its gap out.</summary>
    private static bool TextOnlyPills => Theme.Flair == Flair.Plain;

    /// <summary>A labelled pill's width with a label <paramref name="labelWidth"/> wide: the icon, the gap and the label, or at Plain the label alone.</summary>
    private static float LabelledPillWidth(string icon, float labelWidth) =>
        TextOnlyPills
            ? ActionPillFit.TextOnlyWidth(labelWidth, UiMetrics.Scale)
            : ActionPillFit.LabelledWidth(IconWidth(icon), labelWidth, UiMetrics.Scale);

    /// <summary>The dark ink on Full's lit gold pill and Quiet's flat gold one.</summary>
    private static readonly Vector4 GoldInk = Core.Ui.ColorMath.FromHex(0x1A1406);

    /// <summary>Full's primary pill: a lit gold gradient, top to bottom, and its edge.</summary>
    private static readonly Vector4 GoldTop = Core.Ui.ColorMath.FromHex(0xFFE6A3);
    private static readonly Vector4 GoldFoot = Core.Ui.ColorMath.FromHex(0xD9B65F);
    private static readonly Vector4 GoldEdge = Core.Ui.ColorMath.FromHex(0xF6DFA0);

    /// <summary>Full's other pills: a raised gradient, top to bottom.</summary>
    private static readonly Vector4 RaisedTop = Core.Ui.ColorMath.FromHex(0x252B45);
    private static readonly Vector4 RaisedFoot = Core.Ui.ColorMath.FromHex(0x1C2138);

    /// <summary>
    /// The widths an <see cref="ActionPill"/> takes with <paramref name="label"/>, with <paramref name="shortLabel"/>
    /// and with its icon alone, for <see cref="ActionPillFit.Fit"/>.
    /// </summary>
    public static PillWidths ActionPillWidths(string icon, string label, string shortLabel)
    {
        var full = LabelledPillWidth(icon, ImGui.CalcTextSize(label).X);
        var brief = ReferenceEquals(label, shortLabel) || string.Equals(label, shortLabel, StringComparison.Ordinal)
            ? full
            : LabelledPillWidth(icon, ImGui.CalcTextSize(shortLabel).X);
        return new PillWidths(full, MathF.Min(full, brief), ActionPillFit.IconOnlyWidth(ActionPillHeight));
    }

    /// <summary>The width of one <see cref="ActionPill"/>: labelled, or its icon alone when <paramref name="label"/> is null.</summary>
    public static float ActionPillWidth(string icon, string? label) =>
        label is null
            ? ActionPillFit.IconOnlyWidth(ActionPillHeight)
            : LabelledPillWidth(icon, ImGui.CalcTextSize(label).X);

    /// <summary>
    /// A labelled action pill (1.10, the detail pane's travel and automation row, the Hand in and Duties hand-offs):
    /// <see cref="UiMetrics.MinTarget"/> tall, an icon and a label, or the icon alone (still pill-shaped) when
    /// <paramref name="label"/> is null. Its look follows the Flair level: Full a brass edge, the
    /// <see cref="PillTone.Primary"/> one filled with the accent; Quiet the brass edge alone, the primary in accent ink;
    /// Plain the standard button fill, still labelled, the primary keeping the accent wash it had in 1.3. Under the
    /// high-contrast palette the edge is a solid line. A disabled pill stays labelled and dimmed and still shows its
    /// tooltip; <see cref="PillTone.Danger"/> (Stop) is Eclipse. Hover eases in and out (a press shows at once), and a
    /// new label, icon or tone fades its ink in over <see cref="MotionTokens.Reveal"/>. A real, focusable item with the
    /// focus ring; a null <paramref name="tooltip"/> leaves the hover text to the caller (check
    /// <c>IsItemHovered(AllowWhenDisabled)</c> after the call). Returns true when clicked while enabled.
    /// </summary>
    public static bool ActionPill(string id, string icon, string? label, PillTone tone, bool enabled, string? tooltip = null)
    {
        var height = ActionPillHeight;
        var iconSize = IconSize(icon);
        var labelSize = label is null ? Vector2.Zero : ImGui.CalcTextSize(label);
        var textOnly = label is not null && TextOnlyPills;
        var width = label is null
            ? ActionPillFit.IconOnlyWidth(height)
            : textOnly
                ? ActionPillFit.TextOnlyWidth(labelSize.X, UiMetrics.Scale)
                : ActionPillFit.LabelledWidth(iconSize.X, labelSize.X, UiMetrics.Scale);
        var min = ImGui.GetCursorScreenPos();
        ImGui.BeginDisabled(!enabled);
        var clicked = ImGui.InvisibleButton(id, new Vector2(width, height));
        ImGui.EndDisabled();
        var hovered = enabled && ImGui.IsItemHovered();
        var held = enabled && ImGui.IsItemActive();

        var max = min + new Vector2(width, height);
        var rounding = Theme.Flair == Flair.Plain ? UiMetrics.Px(Theme.Spacing.CardRounding) : height * 0.5f;
        var itemId = ImGuiP.GetItemID();
        var hover = Motion.Hover(Motion.Key(PillHoverTag, itemId), hovered);
        var (fill, edge, ink) = PillColors(tone, enabled, hovered, held);
        if (!held && hover > 0f && hover < 1f)
        {
            // Between rest and hover: the two looks blended by the eased hover.
            var (restFill, restEdge, restInk) = PillColors(tone, enabled, false, false);
            var (hoverFill, hoverEdge, hoverInk) = PillColors(tone, enabled, true, false);
            fill = LerpColor(restFill, hoverFill, hover);
            edge = LerpColor(restEdge, hoverEdge, hover);
            ink = LerpColor(restInk, hoverInk, hover);
        }

        var signature = (uint)tone ^ ((uint)icon.GetHashCode() * 31u) ^ (uint)(label?.GetHashCode() ?? 0);
        var swap = Motion.Changed(Motion.Key(PillSwapTag, itemId), signature, MotionTokens.Reveal);
        if (swap >= 0f)
        {
            ink = ScaleAlpha(ink, MotionMath.EaseOutCubic(swap));
        }

        var dl = ImGui.GetWindowDrawList();
        var full = Theme.Flair == Flair.Full && !Theme.FollowingDalamud;
        if (full && enabled && tone is PillTone.Primary or PillTone.Normal or PillTone.Quiet)
        {
            // Full: lit, raised surfaces. The primary is the one filled gold surface, with its own bloom and highlight.
            var primary = tone == PillTone.Primary;
            if (primary && Theme.ShowGlow)
            {
                for (var i = 3; i >= 1; i--)
                {
                    var grow = UiMetrics.Px(14f) * i / 3f;
                    dl.AddRectFilled(min - new Vector2(grow * 0.5f), max + new Vector2(grow * 0.5f), Theme.WithAlpha(Theme.Moon, 0.10f * (held ? 1.3f : hovered ? 1.15f : 1f)), rounding + (grow * 0.5f));
                }
            }

            var lift = held ? 0.92f : hovered ? 1.06f : 1f;
            var top = primary ? GoldTop : RaisedTop;
            var foot = primary ? GoldFoot : RaisedFoot;
            top = new Vector4(MathF.Min(1f, top.X * lift), MathF.Min(1f, top.Y * lift), MathF.Min(1f, top.Z * lift), 1f);
            foot = new Vector4(MathF.Min(1f, foot.X * lift), MathF.Min(1f, foot.Y * lift), MathF.Min(1f, foot.Z * lift), 1f);
            var first = dl.VtxBuffer.Size;
            dl.AddRectFilled(min, max, 0xFFFFFFFFu, rounding);
            var vertices = dl.VtxBuffer;
            for (var i = first; i < vertices.Size; i++)
            {
                var vertex = vertices[i];
                var c = Vector4.Lerp(top, foot, Math.Clamp((vertex.Pos.Y - min.Y) / height, 0f, 1f));
                c.W = (vertex.Col >> 24) / 255f;
                vertex.Col = Theme.U32(c);
                vertices[i] = vertex;
            }

            dl.AddLine(new Vector2(min.X + rounding, min.Y + 1f), new Vector2(max.X - rounding, min.Y + 1f), Theme.WithAlpha(primary ? Vector4.One : Theme.MoonHigh, primary ? 0.45f : 0.06f), 1f);
        }
        else
        {
            dl.AddRectFilled(min, max, fill, rounding);
        }

        if (edge != 0)
        {
            var thickness = Theme.Glyphs.HighContrast ? MathF.Max(1.5f, UiMetrics.Px(1.5f)) : UiMetrics.Hairline;
            dl.AddRect(min, max, edge, rounding, ImDrawFlags.None, thickness);
        }

        var iconX = label is null ? min.X + ((width - iconSize.X) * 0.5f) : min.X + UiMetrics.Px(ActionPillFit.PadStartLogical);
        if (!textOnly)
        {
            ImGui.PushFont(UiBuilder.IconFont);
            dl.AddText(new Vector2(MathF.Round(iconX), MathF.Round(min.Y + ((height - iconSize.Y) * 0.5f))), ink, icon);
            ImGui.PopFont();
        }

        if (label is not null)
        {
            // Plain's text-only button centres its label; the others set it after the icon and the gap.
            var labelX = textOnly ? min.X + ((width - labelSize.X) * 0.5f) : iconX + iconSize.X + UiMetrics.Px(ActionPillFit.IconGapLogical);
            dl.AddText(new Vector2(MathF.Round(labelX), MathF.Round(min.Y + ((height - labelSize.Y) * 0.5f))), ink, label);
        }

        FocusRing(rounding);
        if (tooltip is not null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(tooltip);
        }

        return clicked && enabled;
    }

    /// <summary>
    /// The fill, edge (0 for none) and ink of a pill at the frame's Decoration level and palette (docs/design/flair-v13
    /// §1, "Action pills"): Full raised pills with brass edges, the primary lit gold with dark ink (drawn as a gradient
    /// by <see cref="ActionPill"/>); Quiet flat pills with a neutral edge, the primary flat gold; Plain rectangular
    /// buttons, text only, the primary set apart by gold text.
    /// </summary>
    private static (uint Fill, uint Edge, uint Ink) PillColors(PillTone tone, bool enabled, bool hovered, bool held)
    {
        var s = Theme.Surface;
        var flair = Theme.Flair;
        var highContrast = Theme.Glyphs.HighContrast;
        var plain = flair == Flair.Plain;
        var quiet = flair == Flair.Quiet;
        var brass = Theme.WithAlpha(s.Ornament, Theme.OrnamentAlpha(CardBrassAlpha));
        var neutral = highContrast ? Theme.U32(s.StrongLine with { W = 1f }) : Theme.U32(plain ? Theme.Tones.HeaderLine : s.Line);
        var wash = held ? 0.28f : hovered ? 0.22f : 0.16f;

        if (!enabled)
        {
            // Dimmed, never hidden: the label stays readable in the disabled tone.
            var dimEdge = highContrast ? Theme.U32(s.TextDisabled)
                : plain || quiet ? neutral
                : Theme.WithAlpha(s.Ornament, CardBrassAlpha * DisabledEdgeShare);
            return (Theme.WithAlpha(s.Raised, 0.6f * s.Raised.W), dimEdge, Theme.U32(s.TextDisabled));
        }

        if (tone == PillTone.Primary && !Theme.FollowingDalamud && !highContrast)
        {
            switch (flair)
            {
                case Flair.Full:
                    return (Theme.MoonU32, Theme.U32(GoldEdge), Theme.U32(GoldInk));
                case Flair.Quiet:
                    var gold = held ? Theme.MoonDeep : hovered ? Vector4.Lerp(Theme.Moon, Theme.MoonHigh, 0.4f) : Theme.Moon;
                    return (Theme.U32(gold), Theme.U32(gold), Theme.U32(GoldInk));
            }
        }

        if (plain && tone is PillTone.Primary or PillTone.Normal or PillTone.Quiet)
        {
            var fill = ImGui.GetColorU32(held ? ImGuiCol.ButtonActive : hovered ? ImGuiCol.ButtonHovered : ImGuiCol.Button);
            var ink = tone == PillTone.Primary ? Theme.AccentU32 : tone == PillTone.Quiet && !hovered ? Theme.U32(s.TextSecondary) : Theme.U32(s.Text);
            var edge = tone == PillTone.Primary && !highContrast ? Theme.WithAlpha(Theme.Accent, 0.3f) : neutral;
            return (fill, edge, ink);
        }

        if (quiet && tone is PillTone.Normal or PillTone.Quiet)
        {
            var ink = tone == PillTone.Quiet && !hovered ? Theme.U32(s.TextSecondary) : Theme.U32(s.Text);
            var fill = held ? Theme.WithAlpha(s.Text, 0.18f) : hovered ? Theme.WithAlpha(s.Hover, 1f) : Theme.U32(Theme.Tones.Card);
            return (fill, neutral, ink);
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

    /// <summary>Two packed colours (IM_COL32) blended channel by channel; <paramref name="t"/> 0 is <paramref name="from"/>.</summary>
    private static uint LerpColor(uint from, uint to, float t)
    {
        if (from == to)
        {
            return from;
        }

        var result = 0u;
        for (var shift = 0; shift < 32; shift += 8)
        {
            var a = (from >> shift) & 0xFFu;
            var b = (to >> shift) & 0xFFu;
            var c = (uint)Math.Clamp((int)MathF.Round(a + ((b - (float)a) * t)), 0, 255);
            result |= c << shift;
        }

        return result;
    }

    /// <summary>A packed colour with its alpha scaled by <paramref name="scale"/>.</summary>
    private static uint ScaleAlpha(uint color, float scale) =>
        (color & 0x00FFFFFFu) | ((uint)MathF.Round((color >> 24) * Math.Clamp(scale, 0f, 1f)) << 24);

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
