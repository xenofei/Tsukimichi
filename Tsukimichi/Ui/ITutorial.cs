namespace Tsukimichi.Ui;

/// <summary>
/// Contract between the main window and the interactive tutorial overlay. The main window calls
/// <see cref="Draw"/> at the very end of its own Draw (after every pane has recorded its rectangles in
/// <see cref="UiState.Rects"/>); the tutorial draws its dimming overlay, highlight and step card on the
/// foreground draw list and may change <see cref="UiState.Tab"/> to bring a region into view.
/// </summary>
public interface ITutorial
{
    /// <summary>True while a tutorial is running.</summary>
    bool Active { get; }

    /// <summary>Starts (or restarts) the tutorial from the first step.</summary>
    void Start();

    /// <summary>Stops the tutorial without marking it completed.</summary>
    void Stop();

    /// <summary>Draws the current step over the main window. No-op when not active.</summary>
    void Draw(UiState ui);
}
