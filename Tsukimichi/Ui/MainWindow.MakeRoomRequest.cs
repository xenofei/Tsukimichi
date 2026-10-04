using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Tsukimichi.Ui;

/// <summary>
/// IPC <c>OpenAt("makeroom")</c> (1.22.0): the Make room popover (C9, <see cref="MakeRoomView"/>) opened from outside the
/// window. The popover opens in the status bar's ID scope, so the request waits for the window's next draw; it opens a
/// window only. Over the status bar's Make room button when the journal is full, else over the bar's left end.
/// </summary>
public sealed partial class MainWindow
{
    private bool makeRoomRequested;

    /// <summary>Opens Make room on the window's next draw (the caller opens the window).</summary>
    public void RequestMakeRoom() => makeRoomRequested = true;

    /// <summary>In the status bar's ID scope, before the popover draws: opens it when it was asked for.</summary>
    private void OpenRequestedMakeRoom()
    {
        if (!makeRoomRequested || makeRoom is not { } view)
        {
            return;
        }

        makeRoomRequested = false;
        if (makeRoomAnchor == default)
        {
            // No Make room button drawn yet (the journal is not full): over the status bar's left end.
            makeRoomAnchor = ImGui.GetWindowPos() + new Vector2(UiMetrics.Px(12f), ImGui.GetWindowSize().Y - UiMetrics.Px(32f));
        }

        view.Open();
    }
}
