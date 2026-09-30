using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Tsukimichi.Ui;

/// <summary>
/// An open section (Moon Road proposal §5 "section header treatment", §7.4.2): no box, no fill, just a heading line
/// <c>✦ REQUIREMENTS ────── all met</c> (the sigil star, the eyebrow in the Eyebrow role and the secondary tone, a Gilt
/// rule fading to the right, and a caption at the far right in the caption role), then the content, inset a few pixels
/// so a row's left marker has room. A caption that would crowd the eyebrow drops to its own line under it, wrapped
/// between words (L5). Content wraps at the section's right edge. Sections do not nest. Drawn at Flair Full and Quiet;
/// callers draw <see cref="Chrome.BeginCard(string, string?, string?, CardKind, Vector4?, bool)"/> at Plain.
/// Allocation-free.
/// </summary>
public static class OpenSection
{
    private static bool open;
    private static int openFrame = -1;
    private static Vector2 start;
    private static float width;

    /// <summary>The content's left inset, in logical pixels: room for a row's 2 px marker left of its mark.</summary>
    public const float InsetLogical = 6f;

    /// <summary>The content's left inset this frame.</summary>
    public static float Inset => UiMetrics.Px(InsetLogical);

    /// <summary>
    /// Opens a section the width of the content region and draws its heading; the content follows at
    /// <see cref="Inset"/>. <paramref name="eyebrow"/> is drawn as given (callers pass it upper-cased, built once per
    /// language). An empty <paramref name="caption"/> leaves the rule running to the right edge.
    /// </summary>
    public static void Begin(string id, string eyebrow, string caption, uint captionColor)
    {
        var frame = ImGui.GetFrameCount();
        if (open && openFrame == frame)
        {
            throw new InvalidOperationException("Open sections do not nest; call End first.");
        }

        open = true;
        openFrame = frame;
        ImGui.PushID(id);
        var dl = ImGui.GetWindowDrawList();
        start = ImGui.GetCursorScreenPos();
        width = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var right = start.X + width;
        var top = start.Y + UiMetrics.Px(4f);

        // The heading line: its height is the eyebrow's line, at least a comfortable 20 px.
        float lineHeight;
        float eyebrowWidth;
        using (Typography.Eyebrow(eyebrow))
        {
            lineHeight = ImGui.GetTextLineHeight();
            eyebrowWidth = ImGui.CalcTextSize(eyebrow).X;
        }

        var height = MathF.Max(lineHeight, UiMetrics.Px(20f));
        var midY = top + (height * 0.5f);
        var sigil = UiMetrics.Px(10f);
        Ornament.Sigil(dl, new Vector2(start.X + (sigil * 0.5f), midY), sigil);
        var textX = start.X + sigil + UiMetrics.Px(7f);
        var textEnd = textX + eyebrowWidth;
        using (Typography.Eyebrow(eyebrow))
        {
            dl.AddText(new Vector2(textX, midY - (lineHeight * 0.5f)), Theme.U32(Theme.Surface.TextSecondary), eyebrow);
        }

        var ruleStart = textEnd + UiMetrics.Px(8f);
        var minRule = UiMetrics.Px(16f);
        var captionBelow = false;
        if (caption.Length > 0)
        {
            using var role = Typography.Caption();
            var captionWidth = ImGui.CalcTextSize(caption).X;
            var captionX = right - captionWidth;
            if (captionX - UiMetrics.Px(8f) >= ruleStart + minRule)
            {
                dl.AddText(new Vector2(captionX, midY - (ImGui.GetFontSize() * 0.5f)), captionColor, caption);
                right = captionX - UiMetrics.Px(8f);
            }
            else
            {
                captionBelow = true;
            }
        }

        Ornament.Rule(dl, new Vector2(ruleStart, midY), right - ruleStart);
        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(width, (top - start.Y) + height));

        var inset = Inset;
        if (captionBelow)
        {
            using var role = Typography.Caption();
            ImGui.SetCursorScreenPos(new Vector2(start.X + inset, ImGui.GetCursorScreenPos().Y));
            TextFlow.Wrapped(caption, MathF.Max(1f, width - inset), captionColor);
        }

        ImGui.SetCursorScreenPos(new Vector2(start.X + inset, ImGui.GetCursorScreenPos().Y + UiMetrics.Px(2f)));
        ImGui.BeginGroup();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + MathF.Max(1f, width - inset));
    }

    /// <summary>
    /// Closes the section opened by <see cref="Begin"/>: leaves one item spanning the whole section (heading and
    /// content), so <c>GetItemRectMin/Max</c> refer to it afterwards, then a little room before the next.
    /// </summary>
    public static void End()
    {
        if (!open)
        {
            throw new InvalidOperationException("End without Begin.");
        }

        open = false;
        ImGui.PopTextWrapPos();
        ImGui.EndGroup();
        var bottom = ImGui.GetItemRectMax().Y + UiMetrics.Px(6f);
        ImGui.PopID();
        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(width, MathF.Max(1f, bottom - start.Y)));
    }
}
