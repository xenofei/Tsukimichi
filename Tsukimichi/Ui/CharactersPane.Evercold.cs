using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// "Before Evercold" on the Characters dashboard (feature plan v7, 1.20.0, N7; spec-1.20 "Where it lives"): the same card
/// as Tonight's (<see cref="EvercoldCardModel"/>) for the viewed character, below the header. It works for any stored
/// character, read from its last snapshot, with "As of the last login, 2 days ago" as its sub-line. Once hidden it
/// leaves one quiet line, "Before Evercold is hidden for Michiru." with Show again. Gone by itself on Evercold's data or
/// its early access day.
/// </summary>
public sealed partial class CharactersPane
{
    // "Duties board" on the card asked for the Duties board (N4): it scrolls into view when the board is next drawn,
    // within a frame or two, or not at all (a request nothing reached lapses rather than firing later).
    private FrameRequest scrollToDutyBoard;

    /// <summary>The Before Evercold card; set by the plugin. Null draws nothing.</summary>
    public EvercoldCardModel? Evercold { get; set; }

    /// <summary>Scrolls the Duties board (N4) into view on its next draw: the card's "Duties board" button.</summary>
    public void RequestDutyBoard() => scrollToDutyBoard.Request(ImGui.GetFrameCount());

    private void DrawBeforeEvercold(UiState ui)
    {
        if (Evercold is not { } card)
        {
            return;
        }

        // Tonight (the detail column, drawn after this) leaves its copy out while the dashboard shows the card.
        card.DashboardFrame = ImGui.GetFrameCount();
        var start = ImGui.GetCursorScreenPos().Y;
        EvercoldCardView.Draw(card, ui, EvercoldSurface.Dashboard, ImGui.GetContentRegionAvail().X);
        if (ImGui.GetCursorScreenPos().Y > start)
        {
            Gap();
        }
    }

    /// <summary>Called where the Duties board's heading is drawn: brings it into view once after a request.</summary>
    private void ScrollToDutyBoardIfAsked()
    {
        if (scrollToDutyBoard.Take(ImGui.GetFrameCount()))
        {
            ImGui.SetScrollHereY(0f);
        }
    }
}
