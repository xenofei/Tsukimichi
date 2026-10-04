using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Tsukimichi.Ui;

/// <summary>
/// 1.22 (spec-1.22 W1, W4): where the window last drew, so the What's new popup can centre on it, and the "New
/// chapters" line, which moved from the retired What's new card into the Tonight card.
/// </summary>
public sealed partial class MainWindow
{
    private Vector2 drawnPos;
    private Vector2 drawnSize;
    private int drawnFrame = -10;

    /// <summary>The window's place and size when it drew this frame or the last; null while it is closed.</summary>
    public (Vector2 Pos, Vector2 Size)? DrawnRect => IsOpen && ImGui.GetFrameCount() - drawnFrame <= 1 ? (drawnPos, drawnSize) : null;

    /// <summary>Attaches the New chapters line (1.21.0 P5) to the Tonight card.</summary>
    public void AttachNewChapters(NewChaptersSource source) =>
        tonightCard.NewChapters = source ?? throw new ArgumentNullException(nameof(source));

    private void NoteDrawn()
    {
        drawnPos = ImGui.GetWindowPos();
        drawnSize = ImGui.GetWindowSize();
        drawnFrame = ImGui.GetFrameCount();
    }
}
