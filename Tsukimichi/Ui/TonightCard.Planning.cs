using Dalamud.Bindings.ImGui;

namespace Tsukimichi.Ui;

/// <summary>
/// The 1.9.0 planning lines in the Tonight card (feature plan v5 "Planning extras"; R6 C, F), under the main scenario
/// row: when the next main scenario quest waits only for a level, what levelling the job up to it opens ("The next MSQ
/// quest needs level 56: levelling DRG 52→56 opens 7 quests (2 unlock quests, MSQ)"), and the catch-up line ("To reach
/// the latest story: 143 quests, Lv 90–100, 6 duties"; hover for each expansion). Both from <see cref="PlanningSource"/>.
/// Kept apart from the MSQ row so that row can change without touching this.
/// </summary>
public sealed partial class TonightCard
{
    /// <summary>The planning lines; set by the plugin. Null draws nothing.</summary>
    public PlanningSource? Planning { get; set; }

    private void DrawPlanning()
    {
        if (Planning is not { } planning)
        {
            return;
        }

        using var mist = Theme.PushText(Theme.Surface.TextSecondary);
        var gate = planning.MsqGateLine;
        if (gate.Length > 0)
        {
            TextFlow.Wrapped(gate, Chrome.RoomX());
        }

        // The MSQ row already says when the story is complete.
        if (planning.CatchUp is { IsComplete: false })
        {
            TextFlow.Wrapped(planning.CatchUpLine, Chrome.RoomX());
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(planning.CatchUpTooltip);
            }
        }
    }
}
