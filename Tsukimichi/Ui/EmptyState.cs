using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Ui;

/// <summary>
/// The shared empty state of a pane: a large veiled moon centred in the remaining space with one line of guidance in
/// Dusk beneath it. Drawn straight to the draw list, so it reserves no layout and allocates nothing.
/// </summary>
public static class EmptyState
{
    /// <summary>Draws the moon and <paramref name="guidance"/> centred in the current window's remaining region.</summary>
    public static void Draw(string guidance)
    {
        ArgumentNullException.ThrowIfNull(guidance);
        var dl = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var avail = ImGui.GetContentRegionAvail();
        var radius = UiMetrics.EmptyStateMoonRadius;
        var gap = UiMetrics.Px(14f);
        var textSize = ImGui.CalcTextSize(guidance);
        var blockHeight = radius * 2f + gap + textSize.Y;

        // A little above the geometric centre reads as centred.
        var top = origin.Y + MathF.Max(0f, (avail.Y - blockHeight) * 0.4f);
        var centerX = origin.X + avail.X * 0.5f;
        MoonGlyph.Draw(dl, new Vector2(centerX, top + radius), radius, QuestState.Unknown);
        dl.AddText(new Vector2(centerX - textSize.X * 0.5f, top + radius * 2f + gap), Theme.DuskU32, guidance);
    }
}
