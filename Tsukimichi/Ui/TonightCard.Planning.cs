using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Unlocks;

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

    /// <summary>The game's textures, for the job icon leading the main scenario level line; null draws the line alone.</summary>
    public ITextureProvider? Textures { get; init; }

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
            // The job to level leads the line with its icon, at the text's height so the line keeps its spacing (UI-5d).
            if (Textures is { } textures && planning.MsqGate is { Job: > 0 } level)
            {
                var size = MathF.Round(ImGui.GetTextLineHeight());
                var min = ImGui.GetCursorScreenPos();
                ImGui.Dummy(new Vector2(size, size));
                if (ImGui.IsItemVisible())
                {
                    Orbit.DrawIcon(ImGui.GetWindowDrawList(), textures, NodeIcon.Game(QuestUnlocks.JobIconBase + level.Job), min, min + new Vector2(size, size));
                }

                ImGui.SameLine(0f, MathF.Round(UiMetrics.Px(6f)));
            }

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

            // How the story's duties ahead can be cleared (1.19.0, C7).
            if (planning.CatchUpOthersLine is { Length: > 0 } others)
            {
                TextFlow.Wrapped(others, Chrome.RoomX(), Theme.U32(Theme.Surface.TextSecondary));
            }
        }
    }
}
