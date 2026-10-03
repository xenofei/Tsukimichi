using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// Tooltips, popups and menus ease in over <see cref="MotionTokens.Popup"/> instead of popping (feature plan v6 U8).
/// <para>
/// A tooltip's fade starts when the item it belongs to changes (the hovered ImGui id when <see cref="Theme.Tooltip"/>
/// runs) or when no tooltip showed the frame before, and never restarts while the pointer moves within one item, so a
/// tooltip does not flicker as the mouse travels over its row. A popup's starts when it opens. They leave at once, as
/// ImGui closes them.
/// </para>
/// <para>
/// The fade is applied after every Tsukimichi window drew (<see cref="EndFrame"/>, the plugin's last draw handler), to
/// the whole tooltip or popup window's draw list: its frame, its text and the art drawn by hand (moons, icons), which a
/// style alpha would not reach. Only windows Tsukimichi began this frame are touched (their begin order lies between
/// <see cref="BeginFrame"/> and <see cref="EndFrame"/>), never another plugin's. Off under Reduce motion and whenever
/// <see cref="Motion.Enabled"/> is off (Decoration Plain, a scroll in progress). Allocation-free.
/// </para>
/// </summary>
public static class PopupFade
{
    private static int windowsAtStart;
    private static int notedFrame = -10;
    private static int tooltipFrame = -10;
    private static uint tooltipOwner;
    private static double tooltipStart;
    private static uint popupId;
    private static int popupOpenFrame = -1;
    private static double popupStart;

    /// <summary>Once per frame, in the plugin's first draw handler: notes where this frame's Tsukimichi windows start.</summary>
    public static void BeginFrame() => windowsAtStart = ImGui.GetCurrentContext().WindowsActiveCount;

    /// <summary>
    /// A tooltip is about to begin (<see cref="Theme.Tooltip"/> calls this): its fade restarts when it belongs to
    /// another item than the last frame's tooltip, or when none showed last frame.
    /// </summary>
    public static void NoteTooltip()
    {
        var frame = ImGui.GetFrameCount();
        if (frame == notedFrame)
        {
            return;
        }

        var owner = ImGui.GetCurrentContext().HoveredId;
        if (owner == 0)
        {
            owner = ImGuiP.GetItemID();
        }

        if (owner != tooltipOwner || frame - tooltipFrame > 1)
        {
            tooltipOwner = owner;
            tooltipStart = ImGui.GetTime();
        }

        tooltipFrame = frame;
        notedFrame = frame;
    }

    /// <summary>The plugin's last draw handler: fades the tooltip and the newest popup Tsukimichi opened, while their fade runs.</summary>
    public static unsafe void EndFrame()
    {
        // Off under Reduce motion, and wherever the interface does not animate (Plain draws every state at once).
        if (UiMetrics.ReduceMotion || !Motion.Enabled)
        {
            return;
        }

        var context = ImGui.GetCurrentContext();
        var windowsAtEnd = context.WindowsActiveCount;
        var now = ImGui.GetTime();
        var frame = ImGui.GetFrameCount();
        if (notedFrame == frame && now - tooltipStart < MotionTokens.Popup)
        {
            var tooltip = ImGuiP.FindWindowByName("##Tooltip_00"u8);
            Fade(tooltip, frame, windowsAtEnd, (float)((now - tooltipStart) / MotionTokens.Popup));
        }

        var stack = context.OpenPopupStack;
        if (stack.Size == 0)
        {
            popupOpenFrame = -1;
            return;
        }

        var top = stack[stack.Size - 1];
        if (top.PopupId != popupId || top.OpenFrameCount != popupOpenFrame)
        {
            popupId = top.PopupId;
            popupOpenFrame = top.OpenFrameCount;

            // A popup that has been open a while (a submenu closed over it) is not faded in again.
            popupStart = frame - top.OpenFrameCount <= 2 ? now : double.NegativeInfinity;
        }

        if (now - popupStart < MotionTokens.Popup)
        {
            Fade(new ImGuiWindowPtr(top.Window), frame, windowsAtEnd, (float)((now - popupStart) / MotionTokens.Popup));
        }
    }

    private static unsafe void Fade(ImGuiWindowPtr window, int frame, int windowsAtEnd, float progress)
    {
        if (window.IsNull || window.LastFrameActive != frame)
        {
            return;
        }

        int order = window.BeginOrderWithinContext;
        if (order < windowsAtStart || order >= windowsAtEnd)
        {
            return;
        }

        Chrome.FadeVertices(new ImDrawListPtr(window.DrawList), 0, MotionMath.EaseOutCubic(progress));
    }
}
