using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Tsukimichi.Ui;

/// <summary>
/// "Before Evercold" in the Tonight card (feature plan v7, 1.20.0, N7): the viewed character's checklist for Patch 8.0
/// (<see cref="BeforeEvercoldCard"/>) in the plain keyline card of the ending-soon events, under them. Gone once the
/// player hides it for the character, and by itself on Evercold's data or date.
/// </summary>
public sealed partial class TonightCard
{
    /// <summary>The Before Evercold card; set by the plugin. Null draws none.</summary>
    public BeforeEvercoldCard? BeforeEvercold { get; set; }

    // The card's height as drawn last frame: its frame goes under the words before they are drawn (as the event cards').
    private float evercoldHeight;

    private void DrawBeforeEvercold()
    {
        if (BeforeEvercold is not { } card || !card.ShowsOnTonight)
        {
            evercoldHeight = 0f;
            return;
        }

        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        var width = Chrome.RoomX();
        var pad = UiMetrics.Px(10f);
        if (evercoldHeight > 0f)
        {
            DrawEventCardFrame(dl, min, new Vector2(min.X + width, min.Y + evercoldHeight), bar: false);
        }

        ImGui.SetCursorScreenPos(new Vector2(min.X + pad, min.Y + pad));
        card.DrawCard(ui, MathF.Max(1f, width - (2f * pad)));

        var bottom = ImGui.GetCursorScreenPos().Y + pad - ImGui.GetStyle().ItemSpacing.Y;
        var height = MathF.Ceiling(MathF.Max((2f * pad) + ImGui.GetTextLineHeight(), bottom - min.Y));
        if (evercoldHeight <= 0f)
        {
            // The first frame: the words are drawn, the frame is not yet; draw it now, over the empty ground only.
            DrawEventCardFrame(dl, min, new Vector2(min.X + width, min.Y + height), bar: false, outlineOnly: true);
        }

        evercoldHeight = height;
        ImGui.SetCursorScreenPos(new Vector2(min.X, min.Y + height));
        ImGui.Dummy(new Vector2(width, 0f));
        ImGui.Spacing();
    }
}
