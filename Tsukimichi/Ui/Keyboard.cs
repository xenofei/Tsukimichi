using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Tsukimichi.Ui;

/// <summary>
/// Hand-rolled shortcuts (T17, accessibility A6/A7, dalamud-developer panel §4): the binding has no <c>ImGui.Shortcut</c>,
/// so every key is <see cref="ImGui.IsKeyPressed(ImGuiKey, bool)"/> gated on the window having focus and no text field
/// wanting the keyboard. Dalamud passes every key on to the game as well (only a text field swallows them), so only
/// Ctrl+F, Esc and Back and Forward (Alt+Left and Alt+Right, mouse buttons 4 and 5; feature plan v7 N1, with a switch
/// of its own) are bound by default; the Ctrl+1..5, F, Enter and P shortcuts are opt-in under Settings › Keyboard.
/// Also the row "…" button every right-click menu gets, so no action needs a mouse's right button.
/// </summary>
public static class Keyboard
{
    private static readonly string MoreGlyph = Chrome.Icon(FontAwesomeIcon.EllipsisH);

    /// <summary>
    /// Whether the current window (or a child or popup of it) has keyboard focus and no text field wants the keys:
    /// the gate for every shortcut.
    /// </summary>
    public static bool WindowHasKeys() =>
        !ImGui.GetIO().WantTextInput && ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);

    /// <summary>The Menu (Apps) key or Shift+F10 went down this frame, with no text field wanting the keys.</summary>
    public static bool MenuKeyPressed()
    {
        var io = ImGui.GetIO();
        if (io.WantTextInput)
        {
            return false;
        }

        return ImGui.IsKeyPressed(ImGuiKey.Menu, false) || (io.KeyShift && !io.KeyCtrl && !io.KeyAlt && ImGui.IsKeyPressed(ImGuiKey.F10, false));
    }

    /// <summary>
    /// Opens the popup <paramref name="popupId"/> (a row's context menu, in the row's id scope) when the last item has
    /// keyboard focus and the Menu key or Shift+F10 went down. Call right after the row's item, before its
    /// <c>ContextPopupItem</c>, so the menu opens the same frame. Returns whether it opened.
    /// </summary>
    public static bool OpenMenuOnKey(string popupId)
    {
        if (!ImGui.IsItemFocused() || !MenuKeyPressed())
        {
            return false;
        }

        ImGui.OpenPopup(popupId);
        return true;
    }

    /// <summary>A bare letter shortcut: the key went down with no Ctrl, Alt or Shift held.</summary>
    public static bool LetterPressed(ImGuiKey key)
    {
        var io = ImGui.GetIO();
        return !io.KeyCtrl && !io.KeyAlt && !io.KeyShift && ImGui.IsKeyPressed(key, false);
    }

    /// <summary>Alt and the key went down (no Ctrl, no Shift): Back and Forward's Alt+Left and Alt+Right (feature plan v7 N1).</summary>
    public static bool AltPressed(ImGuiKey key)
    {
        var io = ImGui.GetIO();
        return io.KeyAlt && !io.KeyCtrl && !io.KeyShift && ImGui.IsKeyPressed(key, false);
    }

    /// <summary>Ctrl and the key went down (no Alt, no Shift).</summary>
    public static bool CtrlPressed(ImGuiKey key)
    {
        var io = ImGui.GetIO();
        return io.KeyCtrl && !io.KeyAlt && !io.KeyShift && ImGui.IsKeyPressed(key, false);
    }

    /// <summary>
    /// The "…" row button (accessibility A6): a square target at <paramref name="min"/> with three dots that opens
    /// <paramref name="popupId"/> with a left click, Enter or Space, so every action of a row's right-click menu is
    /// reachable without the right button. It is a real, focusable item with the focus ring and a tooltip saying the
    /// other ways in. Returns whether it opened the menu.
    /// </summary>
    public static bool MoreButton(string id, string popupId, Vector2 min, float size)
    {
        ImGui.SetCursorScreenPos(min);
        var clicked = ImGui.InvisibleButton(id, new Vector2(size, size));
        var hovered = ImGui.IsItemHovered();
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var center = min + new Vector2(size * 0.5f);
        var rounding = UiMetrics.Px(4f);
        dl.AddRectFilled(min, min + new Vector2(size), Theme.U32(hovered ? s.Hover : s.Raised), rounding);
        // The same glyph as the todo overlay's and Nearby's "…".
        ImGui.PushFont(UiBuilder.IconFont);
        var glyph = ImGui.CalcTextSize(MoreGlyph);
        dl.AddText(center - (glyph * 0.5f), Theme.U32(hovered ? s.Text : s.TextSecondary), MoreGlyph);
        ImGui.PopFont();

        Chrome.FocusRing(rounding);
        if (hovered)
        {
            UiMetrics.Tooltip(Strings.RowMenuButtonTooltip);
        }

        if (!clicked)
        {
            return false;
        }

        ImGui.OpenPopup(popupId);
        return true;
    }
}
