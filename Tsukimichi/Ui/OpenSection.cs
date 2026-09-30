using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// An open section (Moon Road proposal §5 "section header treatment", §7.4.2): no box, no fill, just the heading line
/// every pane shares (<see cref="SectionHeading.DrawLine"/>: <c>✦ REQUIREMENTS ────── all met</c>), then the content,
/// inset a few pixels so a row's left marker has room. A caption that would crowd the heading drops to its own lines
/// under it, wrapped between words (L5). Content wraps at the section's right edge. Sections do not nest. Drawn at Flair Full and Quiet;
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
    /// Opens a section the width of the content region under its heading line (<see cref="SectionHeading.DrawLine"/>:
    /// <paramref name="title"/> cased for the language, <paramref name="caption"/> on the right or wrapped under the
    /// heading when it would crowd it); the content follows at <see cref="Inset"/>. An empty
    /// <paramref name="caption"/> leaves the rule running to the right edge.
    /// </summary>
    public static void Begin(string id, string title, string caption, uint captionColor)
    {
        var frame = ImGui.GetFrameCount();
        if (open && openFrame == frame)
        {
            throw new InvalidOperationException("Open sections do not nest; call End first.");
        }

        open = true;
        openFrame = frame;
        ImGui.PushID(id);
        start = ImGui.GetCursorScreenPos();
        width = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        SectionHeading.DrawLine(title, caption, captionColor, 0f, sigil: true, Theme.Flair, CaptionOverflow.Below);

        var inset = Inset;
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
