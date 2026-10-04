using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Companions;

namespace Tsukimichi.Ui;

/// <summary>The status bar's companion activity segment (feature plan v6, U4).</summary>
public sealed partial class MainWindow
{
    private readonly TextFade activityFade = new();

    /// <summary>
    /// Leaves a finished action's note ("Exported to …", "Copied") in the status bar's activity segment for a few
    /// seconds, for panes that would otherwise insert it as a line (feature plan v6, U4).
    /// </summary>
    private void ShowStatusNote(string note) => detailPane.Activity.Note(note, ImGui.GetTime());

    /// <summary>
    /// What the hand-offs are doing, after the MSQ pill in the room the bar has left (feature plan v6, U4; Questionable's
    /// status since 1.6.0): the first running hand-off in <see cref="StopAll.Order"/> (travel, Questionable, AutoDuty,
    /// an Artisan craft Tsukimichi started) in gold, ending in an ellipsis with the whole text on hover, and a small
    /// Stop. For a few seconds after a hand-off button is pressed its note ("Sent Maple Lumber to Artisan.") takes the
    /// text, in the body tone, while the running hand-off keeps its Stop. A new text fades in by alpha alone. The
    /// segment lives in the fixed bar, so nothing in the panes moves when a hand-off starts or stops. Questionable is
    /// polled at most once a second, and only while this window draws.
    /// </summary>
    private void DrawCompanionActivity(ImDrawListPtr dl, ref float x, float textY, float gap, float separatorWidth, float versionX, float left)
    {
        detailPane.PollActivity();
        var feed = detailPane.Activity;
        var note = feed.NoteAt(ImGui.GetTime());
        var live = feed.Live;
        var text = note ?? live?.Text;
        var alpha = activityFade.Alpha(text);
        if (text is null)
        {
            // Nothing runs and no note shows: a folded "Why it stopped" card (1.18, A2) waits here.
            DrawFoldedStopNote(dl, ref x, textY, gap, separatorWidth, versionX, left);
            return;
        }

        // Questionable's Stop shows even without its gate (disabled, saying why), as it always did.
        var stopKind = live is { } running && (running.CanStop || running.Kind == StopTarget.Questionable) ? running.Kind : StopTarget.None;
        var stopLabel = stopKind == StopTarget.Questionable ? Strings.QuestionableStopShort : Strings.ActionStopShort;
        var stopWidth = stopKind == StopTarget.None ? 0f : ImGui.CalcTextSize(stopLabel).X + (ImGui.GetStyle().FramePadding.X * 2f) + gap;
        var textWidth = ImGui.CalcTextSize(text).X;
        var start = x > left ? x + separatorWidth : x;
        var room = MathF.Min(textWidth, versionX - gap - start - stopWidth);
        if (room < UiMetrics.Px(48f))
        {
            return;
        }

        x = x > left ? StatusSeparatorAt(dl, x, textY, gap) : x;
        ImGui.SetCursorScreenPos(new Vector2(x, textY));
        var color = note is null ? Theme.Accent : Theme.Surface.Text;
        Chrome.EllipsisText(text, room, Theme.WithAlpha(color, color.W * alpha), textWidth);
        if (ImGui.IsItemHovered())
        {
            var about = note is null && live?.Kind == StopTarget.Questionable ? Strings.QuestionableStatusTooltip : null;
            if (textWidth > room || about is null)
            {
                UiMetrics.Tooltip(text, about);
            }
            else
            {
                UiMetrics.Tooltip(about);
            }
        }

        x += room;
        if (stopKind == StopTarget.None)
        {
            return;
        }

        x += gap;
        ImGui.SetCursorScreenPos(new Vector2(x, textY));
        using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(ImGui.GetStyle().FramePadding.X, 0f)))
        {
            if (stopKind == StopTarget.Questionable && questionableActions is { } questionable)
            {
                questionable.DrawStopSmallButton(QuestionableHost, "##activityStop");
            }
            else
            {
                using (ImRaii.PushId("##activityStop"))
                {
                    if (ImGui.SmallButton(stopLabel))
                    {
                        detailPane.StopActivity(stopKind);
                    }
                }

                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(detailPane.ActivityStopTooltip(stopKind));
                }
            }
        }

        x = ImGui.GetItemRectMax().X;
    }
}
