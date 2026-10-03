using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Ui;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// The one Moon Road heading line (proposal §5, "section header treatment"), used by every pane's section headings, the
/// detail pane's open sections (<see cref="OpenSection"/>) and the Journal header: a sigil star, the heading in the
/// Eyebrow role (TrumpGothic, upper-cased in English; the Caption role in sentence case in other languages,
/// <see cref="HeadingCase"/>) in the secondary text tone, a brass rule fading out to the right, and an optional caption
/// on the far right. Its metrics and fitting are <see cref="HeadingLayout"/>'s. No box: the rule is the structure.
/// Drawn so at Decoration Full; at Quiet the heading is in the body font in sentence case, with no sigil, over a flat
/// hairline (docs/design/flair-v13 §1, "Headings font"); under Plain it is the disabled-tone line it was before 1.4. The high-contrast
/// palette draws the rule opaque (<see cref="Ornament.Rule"/>). One item as wide as the room it was given, so a caller
/// can put a button after it on the same line. Nothing allocates on the frames after a heading's first.
/// </summary>
public static class SectionHeading
{
    private static readonly HeadingCase Case = new();

    /// <summary>Whether headings are upper-cased in the current language (English and the pseudo-language).</summary>
    public static bool Capitals => Loc.Language is Loc.English or Loc.PseudoLanguage;

    /// <summary><paramref name="text"/> as a heading prints it in the current language (<see cref="HeadingCase"/>): cached, so it allocates only once per string.</summary>
    public static string Label(string text) => Case.For(text, Capitals);

    /// <summary>
    /// The heading for <paramref name="text"/> at the current flair, with <paramref name="caption"/> on the right
    /// (named on hover when there is no room), leaving <paramref name="reserve"/> pixels free at the end of the line for
    /// an item the caller places after it with <c>SameLine</c>.
    /// </summary>
    public static void Draw(string text, string? caption = null, float reserve = 0f, bool sigil = true) =>
        Draw(text, caption, reserve, sigil, Theme.Flair);

    /// <summary><see cref="Draw(string, string?, float, bool)"/> at an explicit <paramref name="flair"/> (the settings preview).</summary>
    public static void Draw(string text, string? caption, float reserve, bool sigil, Flair flair) =>
        DrawLine(text, caption, Theme.U32(Theme.Surface.TextTertiary), reserve, sigil, flair, CaptionOverflow.Tooltip);

    /// <summary>
    /// How tall <see cref="Draw(string, string?, float, bool)"/> draws <paramref name="text"/> without a caption at the
    /// current flair, and how far down its title is centred: for an item placed beside the heading on its centre line
    /// (Flight's expansion marks). Allocates nothing after the heading's first frame.
    /// </summary>
    public static (float Height, float MidY) Measure(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (Theme.RuleStyle == RuleStyle.Line)
        {
            var line = ImGui.GetTextLineHeight();
            return (line, line * 0.5f);
        }

        var capitals = Capitals && Theme.MoonRoadArt;
        var label = Case.For(text, capitals);
        float titleLine;
        using (TitleRole(label, capitals))
        {
            titleLine = ImGui.GetTextLineHeight();
        }

        var g = HeadingLayout.Compute(0f, 0f, UiMetrics.Scale, titleLine, 0f, 0f, 0f, sigil: false, CaptionOverflow.Tooltip);
        return (g.TotalHeight, g.MidY);
    }

    /// <summary>
    /// The heading line itself: <paramref name="text"/> (cased by <see cref="Label"/>), the rule, and
    /// <paramref name="caption"/> in <paramref name="captionColor"/> on the right, in the Caption role or, with
    /// <paramref name="numeral"/>, the Numeral role. A caption that would crowd the heading follows
    /// <paramref name="captionOverflow"/>: left out and named on hover, or wrapped under the heading. Returns where the
    /// caption landed on the line, for a caller's own hover text.
    /// </summary>
    /// <param name="role">
    /// <see cref="TypeRole.Eyebrow"/> (the default: pane headings, the Journal header) or <see cref="TypeRole.Section"/>
    /// (plan v7 §1: the detail pane's open sections, the filter drawer's section heads): at Full the Section role,
    /// tracked, in the lighter gilt over a 1 px shadow; at Quiet the Lead face in the primary tone; at Plain a band.
    /// </param>
    public static HeadingDrawn DrawLine(string text, string? caption, uint captionColor, float reserve, bool sigil, Flair flair, CaptionOverflow captionOverflow, bool numeral = false, TypeRole role = TypeRole.Eyebrow)
    {
        ArgumentNullException.ThrowIfNull(text);
        var style = FlairRules.Rule(flair);
        if (role == TypeRole.Section)
        {
            return style == RuleStyle.Line
                ? DrawBand(text, caption, captionColor, reserve, captionOverflow, numeral)
                : DrawSectionLine(text, caption, captionColor, reserve, sigil, style == RuleStyle.MoonRoad, captionOverflow, numeral);
        }

        if (style == RuleStyle.Line)
        {
            ImGui.TextDisabled(text);
            if (caption is { Length: > 0 })
            {
                Chrome.SameLineOrWrap(ImGui.CalcTextSize(caption).X);
                ImGui.TextDisabled(caption);
            }

            return default;
        }

        return DrawRuled(text, caption, captionColor, reserve, sigil, style == RuleStyle.MoonRoad, captionOverflow, numeral, section: false);
    }

    /// <summary>The Section role's heading line at Full and Quiet (<see cref="DrawLine"/> with <see cref="TypeRole.Section"/>).</summary>
    private static HeadingDrawn DrawSectionLine(string text, string? caption, uint captionColor, float reserve, bool sigil, bool moonRoad, CaptionOverflow captionOverflow, bool numeral) =>
        DrawRuled(text, caption, captionColor, reserve, sigil, moonRoad, captionOverflow, numeral, section: true);

    /// <summary>
    /// The heading line with its rule (Full and Quiet): the sigil, the title, the rule and the caption, laid out by
    /// <see cref="HeadingLayout.Compute"/>. A <paramref name="section"/> heading's title is in the Section role (tracked
    /// gilt capitals over a 1 px shadow at Full, sentence case where the game face falls back; the Lead face at Quiet);
    /// otherwise in the Eyebrow role in the secondary tone.
    /// </summary>
    private static HeadingDrawn DrawRuled(string text, string? caption, uint captionColor, float reserve, bool sigil, bool moonRoad, CaptionOverflow captionOverflow, bool numeral, bool section)
    {
        var start = ImGui.GetCursorScreenPos();
        var room = MathF.Max(0f, ImGui.GetContentRegionAvail().X - MathF.Max(0f, reserve));
        var dl = ImGui.GetWindowDrawList();
        var capitals = Capitals && moonRoad;
        var label = Case.For(text, capitals);
        sigil &= moonRoad;

        // Measured every frame, in the face each scope actually draws (a heading font still building falls back).
        var captionWidth = 0f;
        var captionLine = 0f;
        if (caption is { Length: > 0 })
        {
            using var role = numeral ? Typography.Numeral(caption) : Typography.Caption();
            captionWidth = ImGui.CalcTextSize(caption).X;
            captionLine = ImGui.GetTextLineHeight();
        }

        // One scope measures and draws the title, so both are in the face it resolved to: a second scope for the
        // sentence-case fallback could find the game face covering it and draw what the Lead face measured.
        HeadingGeometry g;
        float midY;
        using (var role = section ? SectionRole(label, moonRoad) : TitleRole(label, capitals))
        {
            if (section && !role.GameFace)
            {
                // Where the game face cannot draw it, the Section heading takes the Lead face's sentence case.
                label = text;
            }

            var tracking = section ? Typography.SectionTracking(in role) : 0f;
            var titleLine = ImGui.GetTextLineHeight();
            var titleWidth = Chrome.TrackedTextWidth(label, tracking);
            g = HeadingLayout.Compute(start.X, room, UiMetrics.Scale, titleLine, titleWidth, captionWidth, captionLine, sigil, captionOverflow);
            midY = start.Y + g.MidY;
            if (g.SigilSize > 0f)
            {
                Ornament.Sigil(dl, new Vector2(g.SigilCenterX, midY), g.SigilSize);
            }

            var at = new Vector2(g.TitleX, MathF.Round(midY - (titleLine * 0.5f)));
            if (section)
            {
                var ink = moonRoad ? Theme.Surface.OrnamentLight : Theme.Surface.Text;
                Chrome.TrackedTextAt(dl, at, g.TitleRoom, label, Theme.U32(ink), tracking, moonRoad ? Theme.WithAlpha(Theme.Abyss, 0.55f) : 0u);
            }
            else
            {
                var ink = moonRoad ? Theme.Surface.TextSecondary : Theme.Surface.Text;
                Chrome.EllipsisTextAt(dl, at, g.TitleRoom, label, Theme.U32(ink), titleWidth);
            }
        }

        if (g.HasRule)
        {
            Ornament.Rule(dl, new Vector2(g.RuleStart, midY), g.RuleEnd - g.RuleStart);
        }

        var captionMin = Vector2.Zero;
        var captionMax = Vector2.Zero;
        if (g.CaptionShown)
        {
            using var role = numeral ? Typography.Numeral(caption) : Typography.Caption();
            captionMin = new Vector2(MathF.Round(g.CaptionX), MathF.Round(midY - (captionLine * 0.5f)));
            captionMax = captionMin + new Vector2(captionWidth, captionLine);
            dl.AddText(captionMin, captionColor, caption!);
        }

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(MathF.Max(1f, room), g.TotalHeight));
        PlaceCaption(caption, captionColor, g.CaptionShown, g.CaptionBelow, g.TitleX, start.X + room);
        return new HeadingDrawn(g.CaptionShown, captionMin, captionMax);
    }

    /// <summary>
    /// The Section role's heading at Plain (docs/design/v7/ui/spec.md §1): a band (<see cref="HeadingLayout.SectionRow"/>,
    /// 22 px) in the ledger's band tone with a 1 px line under it, the title at the body size in the primary tone and
    /// the caption at the band's right end while it clears the title; else wrapped under the band or named on hover,
    /// as <paramref name="captionOverflow"/> says. The content follows 2 px under the line.
    /// </summary>
    private static HeadingDrawn DrawBand(string text, string? caption, uint captionColor, float reserve, CaptionOverflow captionOverflow, bool numeral)
    {
        var start = ImGui.GetCursorScreenPos();
        var room = MathF.Max(0f, ImGui.GetContentRegionAvail().X - MathF.Max(0f, reserve));
        var dl = ImGui.GetWindowDrawList();
        var line = ImGui.GetTextLineHeight();
        var row = HeadingLayout.SectionRowHeight(Flair.Plain, UiMetrics.Scale, line);
        var pad = UiMetrics.Px(6f);
        dl.AddRectFilled(start, new Vector2(start.X + room, start.Y + row), Theme.U32(Theme.Tones.Band));
        dl.AddRectFilled(new Vector2(start.X, start.Y + row), new Vector2(start.X + room, start.Y + row + 1f), Theme.U32(Theme.Tones.Rule));

        var captionWidth = 0f;
        var captionLine = 0f;
        if (caption is { Length: > 0 })
        {
            using var role = numeral ? Typography.Numeral(caption) : Typography.Caption();
            captionWidth = ImGui.CalcTextSize(caption).X;
            captionLine = ImGui.GetTextLineHeight();
        }

        var titleX = start.X + pad;
        var titleWidth = ImGui.CalcTextSize(text).X;
        var captionX = start.X + room - pad - captionWidth;
        var captionShown = captionWidth > 0f && captionX >= titleX + titleWidth + UiMetrics.Px(HeadingLayout.RuleGapLogical);
        var titleRoom = MathF.Max(0f, (captionShown ? captionX - UiMetrics.Px(HeadingLayout.RuleGapLogical) : start.X + room - pad) - titleX);
        Chrome.EllipsisTextAt(dl, new Vector2(titleX, start.Y + MathF.Round((row - line) * 0.5f)), titleRoom, text, Theme.U32(Theme.Surface.Text), titleWidth);

        var captionMin = Vector2.Zero;
        var captionMax = Vector2.Zero;
        if (captionShown)
        {
            using var role = numeral ? Typography.Numeral(caption) : Typography.Caption();
            captionMin = new Vector2(MathF.Round(captionX), start.Y + MathF.Round((row - captionLine) * 0.5f));
            captionMax = captionMin + new Vector2(captionWidth, captionLine);
            dl.AddText(captionMin, captionColor, caption!);
        }

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(MathF.Max(1f, room), row + 1f));
        PlaceCaption(caption, captionColor, captionShown, captionOverflow == CaptionOverflow.Below, titleX, start.X + room);
        ImGui.SetCursorScreenPos(new Vector2(start.X, ImGui.GetItemRectMax().Y + UiMetrics.Px(HeadingLayout.SectionRow(Flair.Plain).Gap)));
        return new HeadingDrawn(captionShown, captionMin, captionMax);
    }

    /// <summary>A caption that is not on the heading's line: wrapped under it from <paramref name="left"/>, or named while the heading is hovered.</summary>
    private static void PlaceCaption(string? caption, uint captionColor, bool shown, bool below, float left, float right)
    {
        if (caption is not { Length: > 0 } || shown)
        {
            return;
        }

        if (below)
        {
            using var role = Typography.Caption();
            ImGui.SetCursorScreenPos(new Vector2(left, ImGui.GetCursorScreenPos().Y));
            TextFlow.Wrapped(caption, MathF.Max(1f, right - left), captionColor);
        }
        else if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(caption);
        }
    }

    /// <summary>
    /// The heading line inside a row the caller already laid out (R3 #9: the route's milestones, the Todo overlay's and
    /// Nearby's section captions), draw list only, so a fixed-height list keeps its rows: the sigil, the label ending in
    /// an ellipsis, and the brass rule fading out to the row's end, all centred on the row. With
    /// <paramref name="eyebrow"/> the label is in the Eyebrow role (upper-cased in English) like every other heading;
    /// without it, the row's own font in the label's own case (the overlays, whose rows are sized for it).
    /// <paramref name="outlined"/> outlines the label for game scenes; <paramref name="ruleAlpha"/> lets the overlay fade
    /// the rule with its own opacity (the high-contrast rule stays solid). Under Plain it draws nothing and returns
    /// false, so the caller draws the line it always had. <paramref name="cut"/> says whether the label was ellipsised.
    /// </summary>
    public static bool DrawInRow(ImDrawListPtr dl, Vector2 min, float width, float height, string text, uint color, out bool cut, bool eyebrow = true, bool outlined = false, float ruleAlpha = Ornament.RuleAlpha)
    {
        ArgumentNullException.ThrowIfNull(text);
        cut = false;
        if (!Theme.Sectioned || !(width > 0f))
        {
            return false;
        }

        var moonRoad = Theme.MoonRoadArt;
        var capitals = eyebrow && Capitals && moonRoad;
        var label = capitals ? Case.For(text, true) : text;
        using var role = eyebrow && moonRoad ? TitleRole(label, capitals) : default;
        var line = ImGui.GetTextLineHeight();
        var labelWidth = ImGui.CalcTextSize(label).X;
        var g = HeadingLayout.Compute(min.X, width, UiMetrics.Scale, line, labelWidth, 0f, 0f, sigil: moonRoad, CaptionOverflow.Tooltip);
        var midY = MathF.Round(min.Y + (height * 0.5f));
        if (g.SigilSize > 0f)
        {
            Ornament.Sigil(dl, new Vector2(g.SigilCenterX, midY), g.SigilSize);
        }

        var at = new Vector2(g.TitleX, MathF.Round(midY - (line * 0.5f)));
        cut = outlined
            ? Chrome.OutlinedEllipsisAt(dl, at, g.TitleRoom, label, color, labelWidth)
            : Chrome.EllipsisTextAt(dl, at, g.TitleRoom, label, color, labelWidth);
        if (g.HasRule)
        {
            Ornament.Rule(dl, new Vector2(g.RuleStart, midY), g.RuleEnd - g.RuleStart, ruleAlpha);
        }

        return true;
    }

    /// <summary>
    /// A pane's one title line (proposal §7.5, §7.6: "Minions", a character's name): <paramref name="text"/> in the Title
    /// role in the primary text tone, ending in an ellipsis in <paramref name="width"/> pixels, as one item. Returns
    /// whether it was cut.
    /// </summary>
    public static bool Title(string text, float width, uint? color = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        using var role = Typography.Title(text);
        return Chrome.EllipsisText(text, width, color ?? Theme.U32(Theme.Surface.Text));
    }

    /// <summary>
    /// The heading's role: the Eyebrow in capitals, the Caption in sentence case; at Quiet the body font (no role), as
    /// the level draws every heading.
    /// </summary>
    private static Typography.Scope TitleRole(string label, bool capitals) =>
        capitals ? Typography.Eyebrow(label) : Theme.MoonRoadArt ? Typography.Caption() : default;

    /// <summary>The Section heading's role: the Section role at Full (falling back to the Lead face), the Lead face at Quiet.</summary>
    private static Typography.Scope SectionRole(string label, bool moonRoad) =>
        moonRoad ? Typography.Section(label) : Typography.Lead();
}

/// <summary>Where <see cref="SectionHeading.DrawLine"/> put its caption: <see cref="CaptionShown"/> false when it is not on the line.</summary>
public readonly record struct HeadingDrawn(bool CaptionShown, Vector2 CaptionMin, Vector2 CaptionMax);
