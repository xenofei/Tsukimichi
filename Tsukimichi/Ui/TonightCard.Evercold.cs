using Dalamud.Bindings.ImGui;

namespace Tsukimichi.Ui;

/// <summary>
/// "Before Evercold" in the Tonight card (feature plan v7, 1.20.0, N7; spec-1.20 "Where it lives"): the viewed
/// character's card (<see cref="EvercoldCardModel"/>, drawn by <see cref="EvercoldCardView"/>) below the story line and
/// the events line, above the pinned quests. The ending-soon event cards stay above it, since they are dated and closer.
/// Gone while hidden for the character, and by itself on Evercold's data or its early access day.
/// </summary>
public sealed partial class TonightCard
{
    /// <summary>The Before Evercold card; set by the plugin. Null draws none.</summary>
    public EvercoldCardModel? Evercold { get; set; }

    private void DrawBeforeEvercold()
    {
        // The Characters tab already shows the card on its dashboard this frame: one copy, so × leaves as one card.
        if (Evercold is not { } card || card.DashboardFrame == ImGui.GetFrameCount())
        {
            return;
        }

        var start = ImGui.GetCursorScreenPos().Y;
        EvercoldCardView.Draw(card, ui, EvercoldSurface.Tonight, Chrome.RoomX());
        if (ImGui.GetCursorScreenPos().Y > start)
        {
            ImGui.Spacing();
        }
    }
}
