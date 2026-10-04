using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The "Why it stopped" card in the main window (feature plan v7, 1.18.0, A2): a floating layer in the notice dock's
/// column (<see cref="FloatingLayer.Stop"/>), above the detail pane's action bar and above a notice, placed by the one
/// slot manager, so the panes never move. 360–420 px wide. When the player selects another quest it folds to the status
/// bar's activity slot (its cue dot and title; a click opens it again).
/// </summary>
public sealed partial class MainWindow
{
    private const float StopCardMinLogical = 360f;
    private const float StopCardMaxLogical = 420f;
    private const float StopCardShare = 0.45f;
    private const float StopCardGuessLogical = 150f;

    private RunStops? runStops;

    // The card's height as last measured (0 until it first draws, and again once it has gone), and the stop it showed.
    private float stopCardHeight;
    private (StopReason Reason, StopHandOff HandOff, uint Quest) stopCardKey;

    /// <summary>Shows the "Why it stopped" card from <paramref name="stops"/>.</summary>
    public void AttachRunStops(RunStops stops) => runStops = stops ?? throw new ArgumentNullException(nameof(stops));

    /// <summary>Before the floating layers are placed: the card asks for its slot (not while folded).</summary>
    private void WantStopCard(in ScreenRect body)
    {
        if (runStops is not { } stops || stops.Dock.Current is null)
        {
            stopCardHeight = 0f;
            return;
        }

        stops.Dock.NoteSelection(ui.SelectedRowId);
        if (stops.Dock.Folded)
        {
            return;
        }

        var width = StopCardWidth(body.Width);
        if (width <= 0f)
        {
            return;
        }

        FloatingLayers.Want(FloatingLayer.Stop, new Vector2(width, stopCardHeight > 0f ? stopCardHeight : UiMetrics.Px(StopCardGuessLogical)));
    }

    private static float StopCardWidth(float bodyWidth)
    {
        var room = bodyWidth - UiMetrics.Px(20f);
        var width = Math.Clamp(bodyWidth * StopCardShare, UiMetrics.Px(StopCardMinLogical), UiMetrics.Px(StopCardMaxLogical));
        return room < UiMetrics.Px(240f) ? 0f : MathF.Min(width, room);
    }

    /// <summary>The card at its slot: it fades in over Rise and out over Leave; a new stop is drawn unseen for one frame.</summary>
    private void DrawStopCard()
    {
        if (runStops is not { } stops || stops.Dock.Current is not { } card || stops.Dock.Folded)
        {
            return;
        }

        if (!FloatingLayers.TryGet(FloatingLayer.Stop, FloatingLayers.OwnerWindowId(), out var place))
        {
            return;
        }

        // A new stop is drawn unseen for one frame, so its slot takes its height first. Anything else that reshapes the
        // card (its width, the look, "2 min ago" turning "1 h ago", "Report copied", Stop all coming or going) is drawn
        // at once: the card paints its frame to the height it took this frame, and a leaving card keeps fading.
        var key = card.Key;
        var unseen = stopCardHeight <= 0f || (key != stopCardKey && !stops.Dock.Leaving);
        var now = stops.Now;
        var reduce = UiMetrics.ReduceMotion;
        var alpha = unseen ? 0f : stops.Dock.Alpha(now, reduce);
        var interactive = !unseen && stops.Dock.Interactive(now, reduce);
        var hovered = false;
        ImGui.SetCursorScreenPos(place.Min);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero).Push(ImGuiStyleVar.ChildBorderSize, 0f))
        using (ImRaii.PushColor(ImGuiCol.ChildBg, Vector4.Zero))
        using (var child = ImRaii.Child("##stopCard", place.Size, false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoSavedSettings))
        {
            if (child)
            {
                var dl = ImGui.GetWindowDrawList();
                var start = dl.VtxBuffer.Size;
                ImGui.SetCursorScreenPos(place.Min);
                var height = StopCardView.Draw(stops, card, place.Width, interactive, QuestionableHost, ShowStatusNote);
                hovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
                if (alpha < 1f)
                {
                    Chrome.FadeVertices(dl, start, alpha);
                }

                stopCardHeight = height;
                stopCardKey = key;
            }
        }

        stops.NoteShown(hovered);
    }

    /// <summary>
    /// A folded card in the status bar's activity slot, while nothing runs and no note shows: its cue dot and title; a
    /// click opens the card again. False when there is none to draw.
    /// </summary>
    private bool DrawFoldedStopNote(ImDrawListPtr dl, ref float x, float textY, float gap, float separatorWidth, float versionX, float left)
    {
        if (runStops is not { } stops || stops.Dock.Current is not { } card || !stops.Dock.Folded || stops.Dock.Leaving)
        {
            return false;
        }

        var dot = UiMetrics.Px(7f);
        var title = card.Title;
        var textWidth = ImGui.CalcTextSize(title).X;
        var start = x > left ? x + separatorWidth : x;
        var room = MathF.Min(textWidth, versionX - gap - start - dot - gap);
        if (room < UiMetrics.Px(48f))
        {
            return false;
        }

        x = x > left ? StatusSeparatorAt(dl, x, textY, gap) : x;
        var line = ImGui.GetTextLineHeight();
        ImGui.SetCursorScreenPos(new Vector2(x, textY));
        var clicked = ImGui.InvisibleButton("##stopFolded", new Vector2(dot + gap + room, line));
        var hovered = ImGui.IsItemHovered();
        dl.AddCircleFilled(new Vector2(x + (dot * 0.5f), textY + (line * 0.5f)), dot * 0.5f, Theme.U32(StopCardView.CueColor(card.Cue)));
        ImGui.SetCursorScreenPos(new Vector2(x + dot + gap, textY));
        Chrome.EllipsisText(title, room, Theme.U32(hovered ? Theme.Surface.Text : Theme.Surface.TextSecondary), textWidth);
        if (hovered)
        {
            UiMetrics.Tooltip(title, Strings.StopFoldedTooltip);
        }

        if (clicked)
        {
            stops.Dock.Unfold();
        }

        stops.NoteShown(hovered);
        x += dot + gap + room;
        return true;
    }

}
