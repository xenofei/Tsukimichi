using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Tsukimichi.Core.Model;
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

/// <summary>How tall an <see cref="Chrome.ActionPill"/> is (docs/design/v7/ui/spec-1.15.md B2).</summary>
public enum PillLayout : byte
{
    /// <summary>The action bar's pill at the Decoration level: 30 / 28 / 22 px with 18 / 16 / 14 px icons.</summary>
    Bar,

    /// <summary>The panels beside game windows: 26 px with 16 px icons at every level.</summary>
    Panel,

    /// <summary>A list row's small button: the text line tall (as the text-only small button it replaces), 14 px icons.</summary>
    Row,

    /// <summary>A toolbar's button beside framed ones: the frame's height, 16 px icons.</summary>
    Frame,
}

/// <summary>
/// The icon an <see cref="Chrome.ActionPill"/> or a round button wears (UI-5e): a game icon (<see cref="GameIconRef"/>,
/// a map symbol or an action tile), the approved Journal book glyph, or a FontAwesome glyph for an action the game has
/// no icon for. A string converts to a FontAwesome glyph and a <see cref="GameIconRef"/> to a game icon, so a call site
/// passes either.
/// </summary>
public readonly struct PillIcon
{
    private PillIcon(string? glyph, GameIconRef game, bool book)
    {
        Glyph = glyph;
        Game = game;
        Book = book;
    }

    /// <summary>The FontAwesome glyph; null for a game icon or the book.</summary>
    public string? Glyph { get; }

    /// <summary>The game icon; <see cref="GameIconRef.None"/> otherwise.</summary>
    public GameIconRef Game { get; }

    /// <summary>The Journal book glyph (<see cref="MedalArt.RowGlyph"/> of <see cref="MedalBadge.Journal"/>) in the button's ink.</summary>
    public bool Book { get; }

    /// <summary>"Read the journal": the approved book glyph, never the red "!" tile (spec-1.15 B1, Revision 2 ruling 4).</summary>
    public static PillIcon JournalBook { get; } = new(null, GameIconRef.None, true);

    /// <summary>A game icon, or <paramref name="fallback"/> (FontAwesome) when its id is 0.</summary>
    public static PillIcon GameOr(GameIconRef game, string fallback) =>
        game.HasIcon ? new PillIcon(null, game, false) : new PillIcon(fallback, GameIconRef.None, false);

    public static implicit operator PillIcon(string glyph) => new(glyph, GameIconRef.None, false);

    public static implicit operator PillIcon(GameIconRef game) => new(null, game, false);

    /// <summary>A number that changes when the icon does, for the pill's swap fade.</summary>
    internal uint Signature => Book ? 0xB00Bu : Glyph is { } g ? (uint)g.GetHashCode() : Game.IconId * 2654435761u;
}

public static partial class Chrome
{
    /// <summary>A brass edge drawn on a disabled pill keeps this much of the enabled one's alpha.</summary>
    private const float DisabledEdgeShare = 0.45f;

    /// <summary>A disabled button's game icon: this alpha, tinted <see cref="DisabledIconTint"/>, which also takes its colour out (spec-1.15 B2).</summary>
    private const float DisabledIconAlpha = 0.45f;

    private static readonly Vector4 DisabledIconTint = Core.Ui.ColorMath.FromHex(0x8A93B0);

    /// <summary>An action tile's corner radius, logical px (spec-1.15 B1).</summary>
    private const float TileRoundingLogical = 3f;

    /// <summary>The Journal book's mesh is drawn this many times the icon box, so the book (about two thirds of its badge box) fills the box.</summary>
    private const float BookOverscan = 1.45f;

    // 1.13 (feature plan v6 U8): a pill's hover glides in and out, and a new label or tone (Walk becoming Stop) fades in.
    private const uint PillHoverTag = 0x5049_4C48; // "PILH"
    private const uint PillSwapTag = 0x5049_4C53;  // "PILS"

    /// <summary>
    /// The height of an <see cref="ActionPill"/> at the Decoration level (docs/design/flair-v13 §1, "Spacing tokens"):
    /// 30 logical px at Full, 28 at Quiet, a 22 px button at Plain; never under the 24 px minimum target.
    /// </summary>
    public static float ActionPillHeight => PillHeight(PillLayout.Bar);

    /// <summary>The height of a pill of <paramref name="size"/>: the bar's (never under 24 px), the panels' 26 px, a row's text line.</summary>
    public static float PillHeight(PillLayout size) => size switch
    {
        PillLayout.Panel => MathF.Round(UiMetrics.Px(PillMetrics.Panel.Height)),
        PillLayout.Row => ImGui.GetTextLineHeight(),
        PillLayout.Frame => ImGui.GetFrameHeight(),
        _ => MathF.Max(UiMetrics.Px(Theme.Spacing.PillHeight), 24f),
    };

    private static PillMetrics MetricsOf(PillLayout size) => size switch
    {
        PillLayout.Panel => PillMetrics.Panel,
        PillLayout.Row => PillMetrics.Row,
        PillLayout.Frame => PillMetrics.Frame,
        _ => PillMetrics.For(Theme.Flair),
    };

    /// <summary>The icon's box in a pill of <paramref name="size"/>, pixels.</summary>
    private static float PillIconPx(PillLayout size) => ActionPillFit.IconPx(MetricsOf(size), PillHeight(size), UiMetrics.Scale);

    /// <summary>The width the icon takes in a labelled pill: its box, or a wider FontAwesome glyph's own width.</summary>
    private static float PillIconWidth(in PillIcon icon, float box) => icon.Glyph is { } glyph ? MathF.Max(box, IconWidth(glyph)) : box;

    /// <summary>A labelled pill's width with a label <paramref name="labelWidth"/> wide: the pads, the icon, the gap and the label.</summary>
    private static float LabelledPillWidth(in PillIcon icon, float labelWidth, PillLayout size) =>
        ActionPillFit.LabelledWidth(MetricsOf(size), PillIconWidth(icon, PillIconPx(size)), labelWidth, UiMetrics.Scale);

    /// <summary>The dark ink on Full's lit gold pill and Quiet's flat gold one.</summary>
    private static readonly Vector4 GoldInk = Core.Ui.ColorMath.FromHex(0x1A1406);

    /// <summary>Full's primary pill: a lit gold gradient, top to bottom, and its edge.</summary>
    private static readonly Vector4 GoldTop = Core.Ui.ColorMath.FromHex(0xFFE6A3);
    private static readonly Vector4 GoldFoot = Core.Ui.ColorMath.FromHex(0xD9B65F);
    private static readonly Vector4 GoldEdge = Core.Ui.ColorMath.FromHex(0xF6DFA0);


    /// <summary>
    /// The widths an <see cref="ActionPill"/> takes with <paramref name="label"/>, with <paramref name="shortLabel"/>
    /// and with its icon alone, for <see cref="ActionPillFit.Fit"/>.
    /// </summary>
    public static PillWidths ActionPillWidths(PillIcon icon, string label, string shortLabel, PillLayout size = PillLayout.Bar)
    {
        var full = LabelledPillWidth(icon, ImGui.CalcTextSize(label).X, size);
        var brief = ReferenceEquals(label, shortLabel) || string.Equals(label, shortLabel, StringComparison.Ordinal)
            ? full
            : LabelledPillWidth(icon, ImGui.CalcTextSize(shortLabel).X, size);
        return new PillWidths(full, MathF.Min(full, brief), ActionPillFit.IconOnlyWidth(PillHeight(size)));
    }

    /// <summary>The width of one <see cref="ActionPill"/>: labelled, or its icon alone when <paramref name="label"/> is null.</summary>
    public static float ActionPillWidth(PillIcon icon, string? label, PillLayout size = PillLayout.Bar) =>
        label is null
            ? ActionPillFit.IconOnlyWidth(PillHeight(size))
            : LabelledPillWidth(icon, ImGui.CalcTextSize(label).X, size);

    /// <summary>
    /// A labelled action pill (1.10, the detail pane's travel and automation row, the Hand in and Duties hand-offs; 1.15
    /// every travel and route button, UI-5e): an icon and a label, or the icon alone (still pill-shaped) when
    /// <paramref name="label"/> is null. The icon is the game's own where it has one (<see cref="PillIcon"/>; drawn by
    /// <see cref="DrawPillIcon"/>), at every Decoration level, Plain's buttons included; its size and the pads follow
    /// <paramref name="size"/> and the level (<see cref="PillMetrics"/>). Its look follows the Flair level: Full a brass
    /// edge, the <see cref="PillTone.Primary"/> one filled with the accent; Quiet the brass edge alone, the primary in
    /// accent ink; Plain the standard button fill, the primary keeping the accent wash it had in 1.3. Under the
    /// high-contrast palette the edge is a solid line. A disabled pill stays labelled and dimmed (its game icon at .45,
    /// tinted grey) and still shows its tooltip; <see cref="PillTone.Danger"/> (Stop) is Eclipse. Hover eases in and out
    /// (a press shows at once), and a new label, icon or tone fades its ink in over <see cref="MotionTokens.Reveal"/>. A
    /// real, focusable item with the focus ring; a null <paramref name="tooltip"/> leaves the hover text to the caller
    /// (check <c>IsItemHovered(AllowWhenDisabled)</c> after the call). Returns true when clicked while enabled.
    /// </summary>
    public static bool ActionPill(string id, PillIcon icon, string? label, PillTone tone, bool enabled, string? tooltip = null, PillLayout size = PillLayout.Bar)
    {
        var height = PillHeight(size);
        var metrics = MetricsOf(size);
        var iconBox = PillIconPx(size);
        var iconWidth = PillIconWidth(icon, iconBox);
        var labelSize = label is null ? Vector2.Zero : ImGui.CalcTextSize(label);
        var width = label is null
            ? ActionPillFit.IconOnlyWidth(height)
            : ActionPillFit.LabelledWidth(metrics, iconWidth, labelSize.X, UiMetrics.Scale);
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

        var signature = (uint)tone ^ (icon.Signature * 31u) ^ (uint)(label?.GetHashCode() ?? 0);
        var swap = Motion.Changed(Motion.Key(PillSwapTag, itemId), signature, MotionTokens.Reveal);
        var fade = swap >= 0f ? MotionMath.EaseOutCubic(swap) : 1f;
        if (fade < 1f)
        {
            ink = ScaleAlpha(ink, fade);
        }

        var dl = ImGui.GetWindowDrawList();
        // Full's lit pills need the palette's designed raised gradient (none under Follow Dalamud: flat pills).
        var lit = Theme.Palette.Pills;
        var full = Theme.Flair == Flair.Full && lit is not null;
        if (full && enabled && tone is PillTone.Primary or PillTone.Normal or PillTone.Quiet)
        {
            // Full: lit, raised surfaces. The primary is the one filled gold surface, with its own bloom and highlight.
            var primary = tone == PillTone.Primary;
            if (primary && Theme.ShowGlow)
            {
                for (var i = 3; i >= 1; i--)
                {
                    var grow = UiMetrics.Px(14f) * i / 3f;
                    dl.AddRectFilled(min - new Vector2(grow * 0.5f), max + new Vector2(grow * 0.5f), Theme.Glow(0.10f * (held ? 1.3f : hovered ? 1.15f : 1f)), rounding + (grow * 0.5f));
                }
            }

            var lift = held ? 0.92f : hovered ? 1.06f : 1f;
            var top = primary ? GoldTop : lit!.Value.RaisedTop;
            var foot = primary ? GoldFoot : lit!.Value.RaisedFoot;
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

            dl.AddLine(new Vector2(min.X + rounding, min.Y + 1f), new Vector2(max.X - rounding, min.Y + 1f), Theme.WithAlpha(primary ? Vector4.One : Theme.GoldHigh, primary ? 0.45f : 0.06f), 1f);
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

        // The icon sits in its box at the start pad (centred when alone); the label follows after the gap.
        var iconX = label is null ? min.X + ((width - iconWidth) * 0.5f) : min.X + UiMetrics.Px(metrics.PadStart);
        var boxMin = new Vector2(MathF.Round(iconX + ((iconWidth - iconBox) * 0.5f)), MathF.Round(min.Y + ((height - iconBox) * 0.5f)));
        DrawPillIcon(dl, icon, boxMin, iconBox, ink, enabled, fade);

        if (label is not null)
        {
            var labelX = iconX + iconWidth + UiMetrics.Px(metrics.IconGap);
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
    /// <paramref name="icon"/> in the square <paramref name="min"/>..<paramref name="min"/> + <paramref name="side"/>
    /// (spec-1.15 B1): a FontAwesome glyph or the Journal book in <paramref name="ink"/>; a map symbol bare over a 1 px
    /// shadow (the icon in Abyss at .5, one pixel down); an action tile rounded 3 with a 1 px Abyss .5 inset ring. Game
    /// icons keep their colours at every level, since they carry meaning, except on a disabled button: .45 alpha,
    /// tinted grey. Nothing is drawn while a game icon loads. <paramref name="fade"/> scales the whole icon (the pill's
    /// swap fade).
    /// </summary>
    internal static void DrawPillIcon(ImDrawListPtr dl, in PillIcon icon, Vector2 min, float side, uint ink, bool enabled, float fade = 1f)
    {
        if (!(side > 0f) || !(fade > 0f))
        {
            return;
        }

        if (icon.Glyph is { } glyph)
        {
            ImGui.PushFont(UiBuilder.IconFont);
            var size = ImGui.CalcTextSize(glyph);
            dl.AddText(new Vector2(MathF.Round(min.X + ((side - size.X) * 0.5f)), MathF.Round(min.Y + ((side - size.Y) * 0.5f))), ink, glyph);
            ImGui.PopFont();
            return;
        }

        if (icon.Book)
        {
            // The approved book in its flat cut (the high-contrast tokens), recoloured to the ink: pages in the ink, the
            // spine a dark crease.
            var mesh = MedalArt.RowGlyph(MedalBadge.Journal, MedalTokens.For(Theme.Glyphs.HighContrast ? Theme.Glyphs : GlyphPalette.HighContrastDark));
            var meshSide = MathF.Round(side * BookOverscan);
            var meshMin = new Vector2(MathF.Round(min.X + ((side - meshSide) * 0.5f)), MathF.Round(min.Y + ((side - meshSide) * 0.5f)));
            MedalGlyph.DrawMeshInk(dl, mesh, meshMin, meshSide, ink);
            return;
        }

        var game = icon.Game;
        if (!game.HasIcon || Plugin.TextureProvider is not { } textures || !GameIcon.TryGetWrap(textures, game.IconId, side, out var wrap))
        {
            return;
        }

        var max = min + new Vector2(side);
        var alpha = (enabled ? 1f : DisabledIconAlpha) * fade;
        var tint = Theme.WithAlpha(enabled ? Vector4.One : DisabledIconTint, alpha);
        var shadow = Theme.DropShadow(0.5f * alpha);
        if (game.Style == IconStyle.MapSymbol)
        {
            var (fitMin, fitMax) = GameIcon.Fit(wrap, min, max);
            var drop = new Vector2(0f, MathF.Max(1f, MathF.Round(UiMetrics.Px(1f))));
            dl.AddImage(wrap.Handle, fitMin + drop, fitMax + drop, Vector2.Zero, Vector2.One, shadow);
            dl.AddImage(wrap.Handle, fitMin, fitMax, Vector2.Zero, Vector2.One, tint);
            return;
        }

        var rounding = UiMetrics.Px(TileRoundingLogical);
        dl.AddImageRounded(wrap.Handle, min, max, Vector2.Zero, Vector2.One, tint, rounding);
        dl.AddRect(min + new Vector2(0.5f), max - new Vector2(0.5f), shadow, rounding, ImDrawFlags.None, 1f);
    }

    /// <summary>
    /// The fill, edge (0 for none) and ink of a pill at the frame's Decoration level and palette (docs/design/flair-v13
    /// §1, "Action pills"): Full raised pills with brass edges, the primary lit gold with dark ink (drawn as a gradient
    /// by <see cref="ActionPill"/>); Quiet flat pills with a neutral edge, the primary flat gold; Plain rectangular
    /// buttons, the primary set apart by gold text.
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

        if (tone == PillTone.Primary && Theme.Palette.Pills is not null && !highContrast)
        {
            switch (flair)
            {
                case Flair.Full:
                    return (Theme.GoldU32, Theme.U32(GoldEdge), Theme.U32(GoldInk));
                case Flair.Quiet:
                    var gold = held ? Theme.GoldDeep : hovered ? Vector4.Lerp(Theme.Gold, Theme.GoldHigh, 0.4f) : Theme.Gold;
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
                    ? Theme.WithAlpha(Theme.Danger, held ? 1f : hovered ? 0.9f : 0.75f)
                    : Theme.WithAlpha(Theme.Danger, wash + 0.08f);
                var edge = plain && !highContrast ? 0u : Theme.WithAlpha(Theme.Danger, highContrast ? 1f : 0.75f);
                var ink = plain ? Theme.U32(Theme.OnDanger) : Theme.U32(hovered ? s.Text : Theme.DangerText);
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
