using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The Duty Finder unlock hint (P13), drawn side: a small borderless window beside the game's Duty Finder while a
/// padlocked duty is selected (<see cref="DutyFinderHint.Current"/>): "Locked duty", the duty's name, then per quest that
/// unlocks it "Unlocked by:" with its state moon and name, <see cref="Core.Evaluation.BlockerText.StatusText"/> under it
/// in Dusk (the quest done: in the quieter MoonDim), and the buttons Reveal in Tsukimichi and Flag giver.
/// <para>
/// Drawn from <c>UiBuilder.Draw</c>, outside the window system, so it has no chrome; unlike the item hover hint it takes
/// clicks. Placed by <see cref="BesidePlacement"/>: right of the Duty Finder, else left, below or above, sliding along
/// that side to stay on screen, and not drawn at all when no side has room, so it never covers the game window. A new
/// duty, or a new number of quests, is drawn transparent for <see cref="SettleFrames"/> frames while ImGui settles its
/// auto-resized size; a session change that keeps both redraws in place.
/// Allocation-free per frame: every string is built by <see cref="DutyFinderHint"/> when the model changes.
/// </para>
/// </summary>
public sealed class DutyFinderPanel
{
    private const int SettleFrames = 2;
    private const float GapPx = 6f;
    private const float RoundingPx = 4f;

    private const ImGuiWindowFlags PanelFlags =
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize |
        ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoDocking |
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoNav |
        ImGuiWindowFlags.NoFocusOnAppearing;

    private static readonly Vector4 PanelBackground = Theme.Night with { W = 0.96f };
    private static readonly Vector4 ButtonFill = Theme.Veil with { W = 0.55f };
    private static readonly Vector4 ButtonHover = Theme.VeilLine with { W = 0.85f };

    private readonly DutyFinderHint hint;
    private readonly GameLinks links;
    private readonly Action<QuestRecord> reveal;

    // The shape last measured: a new duty or another number of quests changes the size; a session change that only
    // rewords a status line does not, so it keeps the panel visible and clickable.
    private uint drawnCondition;
    private int drawnCount = -1;
    private Vector2 size;
    private int settled;

    /// <param name="reveal">Opens the main window on the quest (the Journal reveal every in-world surface uses).</param>
    public DutyFinderPanel(DutyFinderHint hint, GameLinks links, Action<QuestRecord> reveal)
    {
        this.hint = hint ?? throw new ArgumentNullException(nameof(hint));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.reveal = reveal ?? throw new ArgumentNullException(nameof(reveal));
    }

    /// <summary>Opens the route to the duty (set by the plugin, which builds the target); null hides "Route to unlock".</summary>
    public Action<DutyHintModel>? OpenRoute { get; set; }

    /// <summary><c>UiBuilder.Draw</c> handler.</summary>
    public void Draw()
    {
        // Off, paused by the kill switch, closed, unlocked or unknown: the hint answers null before any game read.
        var model = hint.Current();
        if (model is null || !hint.TryGetWindowRect(out var target))
        {
            drawnCondition = 0;
            drawnCount = -1;
            return;
        }

        if (model.ContentFinderConditionId != drawnCondition || model.Quests.Count != drawnCount)
        {
            drawnCondition = model.ContentFinderConditionId;
            drawnCount = model.Quests.Count;
            settled = 0;
        }

        var viewport = ImGuiHelpers.MainViewport;
        var bounds = new ScreenRect(viewport.Pos, viewport.Pos + viewport.Size);
        var gap = GapPx * UiMetrics.Scale;
        Vector2 pos;
        if (settled < SettleFrames)
        {
            // Measuring: drawn transparent where it will most likely go.
            pos = new Vector2(target.Max.X + gap, target.Min.Y);
        }
        else if (!BesidePlacement.TryPlace(in target, size, in bounds, gap, out pos, out _))
        {
            return;
        }

        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, PanelBackground);
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.Veil);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Silver);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, Theme.Dusk);
        ImGui.PushStyleColor(ImGuiCol.Button, ButtonFill);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ButtonHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.Veil);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, RoundingPx * UiMetrics.Scale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8f, 6f) * UiMetrics.Scale);
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, settled < SettleFrames ? 0f : 1f);
        try
        {
            // Drawn from a raw UiBuilder.Draw handler, so nothing rebalances a Begin left open: End runs whatever
            // Begin returned and whatever DrawContent throws.
            var visible = ImGui.Begin(Strings.DutyHintWindowId, PanelFlags);
            try
            {
                if (visible)
                {
                    size = ImGui.GetWindowSize();
                    if (settled < SettleFrames)
                    {
                        settled++;
                    }

                    DrawContent(model);
                }
            }
            finally
            {
                ImGui.End();
            }
        }
        finally
        {
            ImGui.PopStyleVar(4);
            ImGui.PopStyleColor(7);
        }
    }

    private void DrawContent(DutyHintModel model)
    {
        UiMetrics.ApplyFontScale();
        var lineHeight = ImGui.GetTextLineHeight();
        var glyph = UiMetrics.InlineGlyphSize(lineHeight);
        var indent = ImGui.CalcTextSize(Strings.DutyHintUnlockedBy).X + ImGui.GetStyle().ItemSpacing.X;
        var interactive = settled >= SettleFrames;

        ImGui.TextDisabled(Strings.DutyHintCaption);
        ImGui.TextUnformatted(model.DutyName);

        // "Route to unlock" (1.6.0): the route window on every quest that opens the duty, the cheapest way first.
        if (OpenRoute is { } openRoute && model.AllQuestRowIds.Count > 0)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton(Strings.DutyHintRoute) && interactive)
            {
                openRoute(model);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.DutyHintRouteTooltip);
            }
        }

        var quests = model.Quests;
        for (var i = 0; i < quests.Count; i++)
        {
            var line = quests[i];
            using var id = ImRaii.PushId(i);
            ImGui.Spacing();
            ImGui.TextDisabled(Strings.DutyHintUnlockedBy);
            ImGui.SameLine();
            MoonGlyph.DrawInline(line.State, glyph);
            ImGui.SameLine();
            ImGui.TextUnformatted(line.Name);

            using var indented = ImRaii.PushIndent(indent, scaled: false);
            using (ImRaii.PushColor(ImGuiCol.Text, line.Done ? Theme.MoonDim : Theme.Dusk))
            {
                ImGui.TextUnformatted(line.StatusText);
            }

            if (ImGui.SmallButton(Strings.DutyHintReveal) && interactive)
            {
                reveal(line.Quest);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.DutyHintRevealHint);
            }

            ImGui.SameLine();
            var canFlag = links.CanFlagMap(line.Quest);
            using (ImRaii.Disabled(!canFlag))
            {
                if (ImGui.SmallButton(Strings.DutyHintFlagGiver) && interactive)
                {
                    links.FlagMap(line.Quest);
                }
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(canFlag ? Strings.DutyHintFlagGiverHint : Strings.DutyHintNoGiver);
            }
        }

        if (model.MoreText.Length > 0)
        {
            ImGui.TextDisabled(model.MoreText);
        }
    }
}
