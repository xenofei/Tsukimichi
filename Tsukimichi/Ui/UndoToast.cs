using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The one floating Undo (feature plan v6 S2): after a change to the user's data ("Marked unique · Undo · Add note",
/// "Unpinned Return from the Void · Undo") a small panel floats at the bottom centre of the window the change was made
/// in, above its status bar, for <see cref="SafetyRules.UndoSeconds"/>; the pointer resting on it stops the clock. It is
/// its own window drawn after the others (<see cref="Draw"/>, on <c>UiBuilder.Draw</c>), so it never takes a line of
/// the layout and nothing under it moves. One slot: a newer change replaces an older toast, whose change then stays.
/// It closes with its window. It settles its size unseen for <see cref="GamePanelShell.SettleFrames"/> frames, then
/// fades in over 180 ms (at once under Reduce motion), so it never jumps once shown. While any popup is open (a context
/// menu) it stays behind it and takes no hover or click, so the menu under the pointer keeps them.
/// </summary>
public static class UndoToast
{
    private const string WindowId = "##tsukimichiUndoToast";
    private const float FadeSeconds = 0.18f;
    private const float MarginLogical = 10f;

    private const ImGuiWindowFlags Flags =
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize |
        ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoDocking |
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoFocusOnAppearing;

    private static readonly UndoTimer Timer = new();

    private static string message = string.Empty;
    private static Action? undo;
    private static string? actionLabel;
    private static Action? action;
    private static uint ownerId;
    private static int token;
    private static Vector2 size;
    private static int settled;
    private static double shownAt;

    // The bottom edge the toast keeps above in its owner (the main window's status bar), reported each frame.
    private static uint clearOwner;
    private static float clearBottom;
    private static int clearFrame = -1;

    /// <summary>Whether a toast is up.</summary>
    public static bool Showing => Timer.Showing;

    /// <summary>
    /// Shows <paramref name="text"/> with Undo (and, with <paramref name="extraLabel"/>, a second action such as "Add
    /// note") over the window being drawn now. Call it inside that window's draw, menus and popups included. Returns a
    /// token for <see cref="Dismiss"/>.
    /// </summary>
    public static int Show(string text, Action onUndo, string? extraLabel = null, Action? onExtra = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(onUndo);
        message = text;
        undo = onUndo;
        actionLabel = onExtra is null ? null : extraLabel;
        action = onExtra;
        ownerId = OwnerWindowId();
        size = Vector2.Zero;
        settled = 0;
        Timer.Start(ImGui.GetTime());
        return ++token;
    }

    /// <summary>Takes the toast down if it is still the one <paramref name="shown"/> returned (what it would undo is gone).</summary>
    public static void Dismiss(int shown)
    {
        if (shown == token)
        {
            Timer.Stop();
        }
    }

    /// <summary>
    /// Keeps the toast above <paramref name="bottomY"/> (screen) in the window being drawn now: the main window reports
    /// the top of its status bar every frame, so the toast never covers it.
    /// </summary>
    public static void KeepAbove(float bottomY)
    {
        clearOwner = OwnerWindowId();
        clearBottom = bottomY;
        clearFrame = ImGui.GetFrameCount();
    }

    /// <summary>Draws the toast over its window; once per frame, after the window system.</summary>
    public static void Draw()
    {
        if (!Timer.Showing)
        {
            return;
        }

        var owner = ImGuiP.FindWindowByID(ownerId);
        if (owner.IsNull || !owner.Active || owner.Hidden)
        {
            // The window closed (or collapsed): the change stays and its toast goes with it.
            Timer.Stop();
            return;
        }

        var ownerPos = owner.Pos;
        var ownerSize = owner.Size;
        var margin = UiMetrics.Px(MarginLogical);
        var bottom = clearOwner == ownerId && clearFrame == ImGui.GetFrameCount() ? clearBottom : ownerPos.Y + ownerSize.Y;
        var pos = new Vector2(
            MathF.Round(ownerPos.X + ((ownerSize.X - size.X) * 0.5f)),
            MathF.Round(bottom - margin - size.Y));
        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);

        var now = ImGui.GetTime();
        var measuring = settled < GamePanelShell.SettleFrames;
        var fade = measuring ? 0f : UiMetrics.ReduceMotion ? 1f : Math.Clamp((float)((now - shownAt) / FadeSeconds), 0f, 1f);

        // A context menu (any popup) stays on top and keeps the pointer; so does everything under a toast still settling.
        var popupOpen = ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel);
        var flags = measuring || popupOpen ? Flags | ImGuiWindowFlags.NoInputs : Flags;
        using var style = GamePanelShell.PushPanelStyle(measuring);
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, fade);
        var hovered = false;
        try
        {
            if (ImGui.Begin(WindowId, flags))
            {
                // Above its owner even after a click brought the owner forward, but never above an open popup.
                if (!popupOpen)
                {
                    ImGuiP.BringWindowToDisplayFront(ImGuiP.GetCurrentWindow());
                }

                UiMetrics.ApplyFontScale();
                hovered = !measuring && !popupOpen && ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows);
                DrawBody();
                size = ImGui.GetWindowSize();
                if (measuring && ++settled >= GamePanelShell.SettleFrames)
                {
                    shownAt = now;
                }
            }
        }
        finally
        {
            ImGui.End();
            ImGui.PopStyleVar();
        }

        Timer.Tick(now, hovered);
    }

    private static void DrawBody()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(message);
        ImGui.SameLine(0f, 0f);
        ImGui.TextDisabled(Strings.UndoToastSeparator);
        ImGui.SameLine(0f, 0f);
        using (Theme.PushText(Theme.Accent))
        {
            if (ImGui.SmallButton(Strings.UndoToastUndo))
            {
                var run = undo;
                Clear();
                run?.Invoke();
                return;
            }
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.UndoToastUndoTooltip);
        }

        if (actionLabel is not { } label || action is null)
        {
            return;
        }

        ImGui.SameLine(0f, 0f);
        ImGui.TextDisabled(Strings.UndoToastSeparator);
        ImGui.SameLine(0f, 0f);
        if (ImGui.SmallButton(label))
        {
            var run = action;
            Clear();
            run?.Invoke();
        }
    }

    private static void Clear()
    {
        Timer.Stop();
        undo = null;
        action = null;
        actionLabel = null;
    }

    /// <summary>The top window the current draw belongs to, through child windows and popups (a context menu's opener).</summary>
    private static uint OwnerWindowId()
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
