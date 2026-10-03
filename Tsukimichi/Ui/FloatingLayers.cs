using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The one slot manager for the layers that float over the main window's body (feature plan v6 U2): the notice dock,
/// the Undo toast and hints. Each frame the main window reports its body (down to the status bar's top) and the
/// selected row's rectangle once its panes have drawn (<see cref="Frame"/>); every layer that will show says so with its
/// size before then (<see cref="Want"/>), and the places are handed out together by <see cref="FloatingSlots"/>, in
/// <see cref="FloatingLayer"/> order, so no layer covers the status bar, the selected row or another layer. A layer
/// drawn later in the frame (the Undo toast, a window of its own) reads its place with <see cref="TryGet"/>; outside
/// the main window it keeps its own placement. Allocation-free.
/// </summary>
public static class FloatingLayers
{
    private const float MarginLogical = 10f;
    private static readonly int LayerCount = Enum.GetValues<FloatingLayer>().Length;

    private static readonly Vector2[] Sizes = new Vector2[LayerCount];
    private static readonly int[] WantedFrame = new int[LayerCount];
    private static readonly ScreenRect[] Placed = new ScreenRect[LayerCount];
    private static readonly bool[] HasPlace = new bool[LayerCount];

    private static uint owner;
    private static int placedFrame = -1;

    /// <summary>
    /// <paramref name="layer"/> will show this frame, <paramref name="size"/> big (its size as last measured). Call it
    /// before the main window's <see cref="Frame"/>; a layer drawn after that (the Undo toast) calls it from the main
    /// window's draw on its behalf.
    /// </summary>
    public static void Want(FloatingLayer layer, Vector2 size)
    {
        Sizes[(int)layer] = size;
        WantedFrame[(int)layer] = ImGui.GetFrameCount();
    }

    /// <summary>
    /// The main window, once per frame after its status bar: <paramref name="body"/> is the area the layers float in
    /// (its bottom at the status bar's top) and <paramref name="selectedRow"/> the selected row on screen (empty when
    /// none shows). Places every layer that asked this frame.
    /// </summary>
    public static void Frame(in ScreenRect body, in ScreenRect selectedRow)
    {
        var frame = ImGui.GetFrameCount();
        owner = OwnerWindowId();
        placedFrame = frame;
        var margin = UiMetrics.Px(MarginLogical);
        Span<ScreenRect> taken = stackalloc ScreenRect[LayerCount];
        var count = 0;
        for (var i = 0; i < LayerCount; i++)
        {
            HasPlace[i] = WantedFrame[i] == frame && Sizes[i].X > 0f && Sizes[i].Y > 0f;
            if (!HasPlace[i])
            {
                continue;
            }

            Placed[i] = FloatingSlots.Place((FloatingLayer)i, Sizes[i], in body, in selectedRow, taken[..count], margin);
            taken[count++] = Placed[i];
        }
    }

    /// <summary>
    /// Where <paramref name="layer"/> goes this frame over the window <paramref name="windowId"/>; false when that is not
    /// the main window, or the layer did not ask this frame.
    /// </summary>
    public static bool TryGet(FloatingLayer layer, uint windowId, out ScreenRect place)
    {
        place = Placed[(int)layer];
        return placedFrame == ImGui.GetFrameCount() && windowId == owner && HasPlace[(int)layer];
    }

    /// <summary>The top window the current draw belongs to, through child windows and popups.</summary>
    internal static uint OwnerWindowId()
    {
        var window = ImGuiP.GetCurrentWindow();
        if (window.IsNull)
        {
            return 0;
        }

        var root = window.RootWindowPopupTree;
        return root.IsNull ? window.RootWindow.ID : root.RootWindow.ID;
    }
}
