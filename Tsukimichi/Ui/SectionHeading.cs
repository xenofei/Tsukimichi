using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Ui;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// The open-section heading of the Moon Road panes (proposal §5, "section header treatment"): a sigil star, the
/// heading in the Eyebrow role (TrumpGothic, uppercase in English; the Caption role in sentence case in other
/// languages) in the secondary text tone, a brass rule fading out to the right, and an optional caption on the far
/// right in the tertiary tone. No box: the rule is the structure. Drawn at Flair Full and Quiet; under Plain the
/// heading is the disabled-tone line it was before 1.4. The high-contrast palette draws the rule opaque
/// (<see cref="Ornament.Rule"/>). One item as wide as the room it was given, so a caller can put a button after it on
/// the same line. The uppercase forms are cached per string, so nothing allocates on the frames in between.
/// </summary>
public static class SectionHeading
{
    private const int MaxCached = 256;

    private static readonly Dictionary<string, string> Upper = new(StringComparer.Ordinal);

    /// <summary>
    /// The heading for <paramref name="text"/> at the current flair, with <paramref name="caption"/> on the right
    /// (omitted when there is no room), leaving <paramref name="reserve"/> pixels free at the end of the line for an
    /// item the caller places after it with <c>SameLine</c>.
    /// </summary>
    public static void Draw(string text, string? caption = null, float reserve = 0f, bool sigil = true) =>
        Draw(text, caption, reserve, sigil, Theme.Flair);

    /// <summary><see cref="Draw(string, string?, float, bool)"/> at an explicit <paramref name="flair"/> (the settings preview).</summary>
    public static void Draw(string text, string? caption, float reserve, bool sigil, Flair flair)
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

            return;
        }

        var start = ImGui.GetCursorScreenPos();
        var room = MathF.Max(0f, ImGui.GetContentRegionAvail().X - MathF.Max(0f, reserve));
        var dl = ImGui.GetWindowDrawList();
        var s = Theme.Surface;
        var gap = UiMetrics.Px(6f);
        var english = Loc.Language is Loc.English or Loc.PseudoLanguage;
        var label = english ? UpperOf(text) : text;

        // The caption first, in its own role, so the eyebrow knows how much room it has.
        var captionWidth = 0f;
        var captionHeight = 0f;
        if (caption is { Length: > 0 })
        {
            using var role = Typography.Caption();
            captionWidth = ImGui.CalcTextSize(caption).X;
            captionHeight = ImGui.GetTextLineHeight();
        }

        float line;
        float x;
        using (var role = english ? Typography.Eyebrow(label) : Typography.Caption())
        {
            line = ImGui.GetTextLineHeight();
            x = start.X;
            if (sigil)
            {
                var size = MathF.Round(MathF.Min(line * 0.7f, UiMetrics.Px(10f)));
                Ornament.Sigil(dl, new Vector2(x + (size * 0.5f), start.Y + (line * 0.5f)), size);
                x += size + gap;
            }

            var textRoom = MathF.Max(0f, start.X + room - x - (captionWidth > 0f ? captionWidth + gap : 0f));
            var width = ImGui.CalcTextSize(label).X;
            Chrome.EllipsisTextAt(dl, new Vector2(x, start.Y), textRoom, label, Theme.U32(s.TextSecondary), width);
            x += MathF.Min(width, textRoom) + gap;
        }

        var height = MathF.Max(line, captionHeight);
        var captionX = start.X + room - captionWidth;
        var showCaption = captionWidth > 0f && captionX >= x;
        var ruleEnd = showCaption ? captionX - gap : start.X + room;
        Ornament.Rule(dl, new Vector2(x, start.Y + MathF.Round(line * 0.55f)), ruleEnd - x);
        if (showCaption)
        {
            using var role = Typography.Caption();
            dl.AddText(new Vector2(captionX, start.Y + MathF.Max(0f, (line - captionHeight) * 0.5f)), Theme.U32(s.TextTertiary), caption!);
        }

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(room, height));
        if (caption is { Length: > 0 } && !showCaption && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(caption);
        }
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

    /// <summary>The uppercase form of an English heading, cached per string (a heading is drawn every frame).</summary>
    private static string UpperOf(string text)
    {
        if (Upper.TryGetValue(text, out var upper))
        {
            return upper;
        }

        if (Upper.Count >= MaxCached)
        {
            Upper.Clear();
        }

        upper = text.ToUpperInvariant();
        Upper[text] = upper;
        return upper;
    }
}
