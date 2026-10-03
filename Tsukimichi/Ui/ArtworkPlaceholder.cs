using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Tsukimichi.Ui;

/// <summary>
/// What the spoiler shield draws where journal artwork would be: a raised Night card, banner-shaped, with
/// <see cref="Strings.ArtworkHidden"/> centred in Dusk. Drawn on the window draw list over a dummy item, so layout and
/// hover behave as an image would.
/// </summary>
internal static class ArtworkPlaceholder
{
    /// <summary>Height over width of the card: about the shape of a journal banner.</summary>
    private const float Aspect = 0.28f;

    public static void Draw(float width)
    {
        var dl = ImGui.GetWindowDrawList();
        var pad = UiMetrics.Px(8f);
        var wrap = MathF.Max(UiMetrics.Px(40f), width - pad * 2f);
        var text = ImGui.CalcTextSize(Strings.ArtworkHidden, false, wrap);
        var height = MathF.Max(width * Aspect, text.Y + pad * 2f);
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(width, height);
        dl.AddRectFilled(min, max, Theme.U32(Theme.Surface.Raised), UiMetrics.Px(4f));
        var textPos = new Vector2(min.X + MathF.Max(pad, (width - text.X) * 0.5f), min.Y + (height - text.Y) * 0.5f);
        dl.AddText(ImGui.GetFont(), ImGui.GetFontSize(), textPos, Theme.U32(Theme.Surface.TextTertiary), Strings.ArtworkHidden, wrap);
        ImGui.Dummy(new Vector2(width, height));
    }
}
