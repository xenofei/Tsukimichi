using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The armed items of the safety table (feature plan v6 S1/S2, <see cref="SafetyRules"/>): a button, a menu item and a
/// checkbox that act only while Ctrl or Shift is held (<see cref="ClickGuard"/>), or on two clicks when the user chose
/// that for hand strain. They look like any other item until hovered: the cue (a Moon outline, a gold wash while the key
/// is down, the tooltip's last line) shows on hover only, never as a permanent glyph. A plain click does nothing but
/// flash the outline and say which key arms it. Labels keep their width when "Click again" replaces them, so nothing
/// moves.
/// </summary>
public static partial class Chrome
{
    private const uint ArmedTag = 0x41524D44; // "ARMD"
    private const uint RefusedTag = 0x52465344; // "RFSD"
    private const float RefusedFlashSeconds = 0.45f;
    private const string ArmedId = "###armed";

    // Each label with the armed id, made once per label so the items allocate nothing per frame.
    private static readonly Dictionary<string, string> ArmedLabels = new(StringComparer.Ordinal);

    // The guards holding a first click made in a context menu (two-click mode), checked once a frame by BeginFrame.
    private static readonly HashSet<ClickGuard> MenuArmedGuards = [];
    private static readonly List<ClickGuard> MenuGuardsClosed = [];

    /// <summary>
    /// Once per frame, before any window draws: a first click made in a context menu (<see cref="ArmedMenuItem"/>) is
    /// forgotten once its menu was not drawn, so "Click again" never outlives the popup.
    /// </summary>
    public static void BeginFrame()
    {
        if (MenuArmedGuards.Count == 0)
        {
            return;
        }

        var frame = ImGui.GetFrameCount();
        foreach (var guard in MenuArmedGuards)
        {
            if (!guard.KeepMenuArm(frame))
            {
                MenuGuardsClosed.Add(guard);
            }
        }

        foreach (var guard in MenuGuardsClosed)
        {
            MenuArmedGuards.Remove(guard);
        }

        MenuGuardsClosed.Clear();
    }

    /// <summary>The width <see cref="ArmedButton"/> takes for <paramref name="label"/>, so a caller can wrap it whole.</summary>
    public static float ArmedButtonWidth(string label) =>
        MathF.Max(ImGui.CalcTextSize(label).X, ImGui.CalcTextSize(Strings.SafetyClickAgain).X) + (ImGui.GetStyle().FramePadding.X * 2f);

    /// <summary>
    /// A small button guarded by <paramref name="guard"/> (<see cref="SafetyTier.Armed"/>): returns true on the click that
    /// should act. Hovering shows <paramref name="tooltip"/> with the action's safety line from <see cref="Safety"/>.
    /// A guard shared by a list of buttons (a Restore per row) takes each row's <paramref name="target"/>.
    /// </summary>
    public static bool ArmedButton(string label, ClickGuard guard, GuardedAction action, string tooltip, ulong target = 0)
    {
        ArgumentNullException.ThrowIfNull(guard);
        var now = ImGui.GetTime();
        var twoClick = Safety.Settings.TwoClick;
        var awaiting = twoClick && guard.AwaitingSecond(now, target);
        var style = ImGui.GetStyle();
        var width = ArmedButtonWidth(label);

        bool clicked;
        using (ImRaii.PushId(label))
        using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(style.FramePadding.X, 0f)))
        {
            // As short as SmallButton (no vertical padding), and as wide as "Click again" so the swap moves nothing.
            clicked = ImGui.Button(WithArmedId(awaiting ? Strings.SafetyClickAgain : label), new Vector2(width, 0f));
        }

        var hovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
        var fired = clicked && guard.Click(Safety.KeyHeld, twoClick, now, target);
        DrawArmedCue(ImGuiP.GetItemID(), hovered, awaiting, clicked && !fired, ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
        if (hovered)
        {
            ArmedTooltip(guard, action, tooltip, now, target);
        }

        return fired;
    }

    /// <summary>
    /// A context-menu item guarded by <paramref name="guard"/>: dimmed until Ctrl or Shift is held (or the first of two
    /// clicks landed), a plain click keeps the menu open and says which key arms it, and the click that acts closes the
    /// menu and returns true. A first click lasts only while the menu stays open (<see cref="BeginFrame"/>).
    /// </summary>
    public static bool ArmedMenuItem(string label, ClickGuard guard, GuardedAction action, string tooltip, ulong target = 0)
    {
        ArgumentNullException.ThrowIfNull(guard);
        var now = ImGui.GetTime();
        var twoClick = Safety.Settings.TwoClick;
        var awaiting = twoClick && guard.AwaitingSecond(now, target, inMenu: true);
        var armed = Safety.KeyHeld || awaiting;

        bool clicked;
        using (ImRaii.PushId(label))
        using (Theme.PushText(Theme.Surface.TextDisabled, !armed))
        {
            clicked = ImGui.Selectable(WithArmedId(awaiting ? Strings.SafetyClickAgain : label), false, ImGuiSelectableFlags.DontClosePopups);
        }

        var hovered = ImGui.IsItemHovered();
        var fired = clicked && guard.Click(Safety.KeyHeld, twoClick, now, target, inMenu: true);

        // "Click again" lasts only as long as the menu: BeginFrame forgets the first click once the menu is not drawn.
        if (guard.MenuShown(ImGui.GetFrameCount(), target))
        {
            MenuArmedGuards.Add(guard);
        }

        DrawArmedCue(ImGuiP.GetItemID(), hovered, awaiting, clicked && !fired, ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
        if (hovered)
        {
            ArmedTooltip(guard, action, tooltip, now, target);
        }

        if (fired)
        {
            ImGui.CloseCurrentPopup();
        }

        return fired;
    }

    /// <summary>
    /// A checkbox whose turn to <paramref name="guardedValue"/> is guarded by <paramref name="guard"/> (Don't track this
    /// character); the turn back is one plain click. Returns true when <paramref name="value"/> changed.
    /// </summary>
    public static bool ArmedCheckbox(string label, ref bool value, bool guardedValue, ClickGuard guard, GuardedAction action, string tooltip, ulong target = 0)
    {
        ArgumentNullException.ThrowIfNull(guard);
        var now = ImGui.GetTime();
        var twoClick = Safety.Settings.TwoClick;
        var awaiting = twoClick && guard.AwaitingSecond(now, target);
        var next = value;
        var clicked = ImGui.Checkbox(label, ref next);
        var hovered = ImGui.IsItemHovered();
        var guarded = clicked && next == guardedValue;
        var fired = clicked && (!guarded || guard.Click(Safety.KeyHeld, twoClick, now, target));
        var frameMin = ImGui.GetItemRectMin();
        var box = ImGui.GetFrameHeight();
        DrawArmedCue(ImGuiP.GetItemID(), hovered && value != guardedValue, awaiting, guarded && !fired, frameMin, frameMin + new Vector2(box, box));
        if (hovered)
        {
            if (value != guardedValue)
            {
                ArmedTooltip(guard, action, tooltip, now, target);
            }
            else
            {
                UiMetrics.Tooltip(tooltip);
            }
        }

        if (fired)
        {
            value = next;
        }

        return fired;
    }

    /// <summary><paramref name="label"/> with the armed id, so "Click again" keeps the item's id.</summary>
    private static string WithArmedId(string label)
    {
        if (!ArmedLabels.TryGetValue(label, out var withId))
        {
            withId = label + ArmedId;
            ArmedLabels[label] = withId;
        }

        return withId;
    }

    /// <summary>The tooltip: the "hold Shift or Ctrl" hint first for a moment after a refused click, then the body and the safety line.</summary>
    private static void ArmedTooltip(ClickGuard guard, GuardedAction action, string tooltip, double now, ulong target)
    {
        if (guard.ShowRefusedHint(now, target))
        {
            UiMetrics.Tooltip(Strings.SafetyKeyHint, tooltip);
        }
        else
        {
            Safety.Tooltip(tooltip, action);
        }
    }

    /// <summary>
    /// The hover-only cue on an armed item: a faint Moon outline on hover, a gold wash eased in over about 120 ms while
    /// the key is down (or the second click is awaited), and a short flash of the outline after a refused click.
    /// </summary>
    private static void DrawArmedCue(uint itemId, bool hovered, bool awaiting, bool refused, Vector2 min, Vector2 max)
    {
        var armed = awaiting || (hovered && Safety.KeyHeld);
        var wash = Motion.Lerp(Motion.Key(ArmedTag, itemId), armed ? 1f : 0f);
        var flashKey = Motion.Key(RefusedTag, itemId);
        if (refused)
        {
            Motion.Trigger(flashKey);
        }

        var flash = Motion.Pulse(flashKey, RefusedFlashSeconds);
        var outline = MathF.Max(hovered ? 0.45f : 0f, wash * 0.9f);
        if (flash >= 0f)
        {
            outline = MathF.Max(outline, 1f - flash);
        }
        else if (refused)
        {
            // Reduce motion: no flash, a steady outline for the frame of the click is enough with the tooltip's hint.
            outline = MathF.Max(outline, 0.9f);
        }

        if (wash <= 0.001f && outline <= 0.001f)
        {
            return;
        }

        var dl = ImGui.GetWindowDrawList();
        var rounding = ImGui.GetStyle().FrameRounding;
        if (wash > 0.001f)
        {
            dl.AddRectFilled(min, max, Theme.WithAlpha(Theme.Gold, 0.22f * wash), rounding);
        }

        dl.AddRect(min, max, Theme.WithAlpha(Theme.Gold, outline), rounding, ImDrawFlags.RoundCornersAll, MathF.Max(1f, UiMetrics.Hairline));
    }
}
