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
/// Drawn at Flair Full and Quiet; under Plain the heading is the disabled-tone line it was before 1.4. The high-contrast
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
    /// The heading line itself: <paramref name="text"/> (cased by <see cref="Label"/>), the rule, and
    /// <paramref name="caption"/> in <paramref name="captionColor"/> on the right, in the Caption role or, with
    /// <paramref name="numeral"/>, the Numeral role. A caption that would crowd the heading follows
    /// <paramref name="captionOverflow"/>: left out and named on hover, or wrapped under the heading. Returns where the
    /// caption landed on the line, for a caller's own hover text.
    /// </summary>
    public static HeadingDrawn DrawLine(string text, string? caption, uint captionColor, float reserve, bool sigil, Flair flair, CaptionOverflow captionOverflow, bool numeral = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!FlairRules.Rules(flair))
        {
            ImGui.TextDisabled(text);
            if (caption is { Length: > 0 })
            {
                Chrome.SameLineOrWrap(ImGui.CalcTextSize(caption).X);
                ImGui.TextDisabled(caption);
            }

            return default;
        }

        var start = ImGui.GetCursorScreenPos();
        var room = MathF.Max(0f, ImGui.GetContentRegionAvail().X - MathF.Max(0f, reserve));
        var dl = ImGui.GetWindowDrawList();
        var capitals = Capitals;
        var label = Case.For(text, capitals);

        // Measured every frame, in the face each scope actually draws (a heading font still building falls back).
        var captionWidth = 0f;
        var captionLine = 0f;
        if (caption is { Length: > 0 })
        {
            using var role = numeral ? Typography.Numeral(caption) : Typography.Caption();
            captionWidth = ImGui.CalcTextSize(caption).X;
            captionLine = ImGui.GetTextLineHeight();
        }

        float titleLine;
        float titleWidth;
        using (TitleRole(label, capitals))
        {
            titleLine = ImGui.GetTextLineHeight();
            titleWidth = ImGui.CalcTextSize(label).X;
        }

        var g = HeadingLayout.Compute(start.X, room, UiMetrics.Scale, titleLine, titleWidth, captionWidth, captionLine, sigil, captionOverflow);
        var midY = start.Y + g.MidY;
        if (g.SigilSize > 0f)
        {
            Ornament.Sigil(dl, new Vector2(g.SigilCenterX, midY), g.SigilSize);
        }

        using (TitleRole(label, capitals))
        {
            Chrome.EllipsisTextAt(dl, new Vector2(g.TitleX, MathF.Round(midY - (titleLine * 0.5f))), g.TitleRoom, label, Theme.U32(Theme.Surface.TextSecondary), titleWidth);
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
        if (caption is { Length: > 0 } && !g.CaptionShown)
        {
            if (g.CaptionBelow)
            {
                using var role = Typography.Caption();
                ImGui.SetCursorScreenPos(new Vector2(g.TitleX, ImGui.GetCursorScreenPos().Y));
                TextFlow.Wrapped(caption, MathF.Max(1f, start.X + room - g.TitleX), captionColor);
            }
            else if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(caption);
            }
        }

        return new HeadingDrawn(g.CaptionShown, captionMin, captionMax);
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

    /// <summary>The heading's role: the Eyebrow in capitals, the Caption in sentence case.</summary>
    private static Typography.Scope TitleRole(string label, bool capitals) => capitals ? Typography.Eyebrow(label) : Typography.Caption();
}

/// <summary>Where <see cref="SectionHeading.DrawLine"/> put its caption: <see cref="CaptionShown"/> false when it is not on the line.</summary>
public readonly record struct HeadingDrawn(bool CaptionShown, Vector2 CaptionMin, Vector2 CaptionMax);
