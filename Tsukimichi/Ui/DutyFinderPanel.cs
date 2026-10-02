using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Model;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The Duty Finder unlock hint (P13), drawn side: a small borderless window beside the game's Duty Finder while a
/// padlocked duty is selected (<see cref="DutyFinderHint.Current"/>): "Locked duty", the duty's name, then per quest that
/// unlocks it "Unlocked by:" with its state moon and name, <see cref="Core.Evaluation.BlockerText.StatusText"/> under it
/// in the quieter tone (the quest done: in the dimmed accent), and the buttons Reveal in Tsukimichi and Flag giver.
/// <para>
/// Drawn from <c>UiBuilder.Draw</c>, outside the window system, so it has no chrome; unlike the item hover hint it takes
/// clicks. Since 1.8.0 it draws in the frame the 1.7 panels share (<see cref="GamePanelShell"/>, R3 #11): placed by
/// <see cref="Core.Ui.BesidePlacement"/> (right of the Duty Finder, else left, below or above, and not at all when no
/// side has room, so it never covers the game window), drawn transparent while a new duty or a new number of quests
/// settles its size, and coloured from the palette in use: Night, "Follow Dalamud colours" or high contrast, with the
/// sigil and brass rule of the caption while Flair draws rules.
/// Allocation-free per frame: every string is built by <see cref="DutyFinderHint"/> when the model changes, and the
/// content callback is a cached delegate.
/// </para>
/// </summary>
public sealed class DutyFinderPanel
{
    private readonly DutyFinderHint hint;
    private readonly GameLinks links;
    private readonly Action<QuestRecord> reveal;
    private readonly GamePanelShell shell = new(Strings.DutyHintWindowId);
    private readonly Action drawContent;

    // The model this frame's content draws.
    private DutyHintModel? model;

    /// <param name="reveal">Opens the main window on the quest (the Journal reveal every in-world surface uses).</param>
    public DutyFinderPanel(DutyFinderHint hint, GameLinks links, Action<QuestRecord> reveal)
    {
        this.hint = hint ?? throw new ArgumentNullException(nameof(hint));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.reveal = reveal ?? throw new ArgumentNullException(nameof(reveal));
        drawContent = DrawContent;
    }

    /// <summary>Opens the route to the duty (set by the plugin, which builds the target); null hides "Route to unlock".</summary>
    public Action<DutyHintModel>? OpenRoute { get; set; }

    /// <summary><c>UiBuilder.Draw</c> handler.</summary>
    public void Draw()
    {
        // Off, paused by the kill switch, closed, unlocked or unknown: the hint answers null before any game read.
        var current = hint.Current();
        if (current is null || !hint.TryGetWindowRect(out var target))
        {
            model = null;
            shell.Reset();
            return;
        }

        // A new duty or another number of quests changes the size; a session change that only rewords a status line
        // does not, so it keeps the panel visible and clickable.
        model = current;
        shell.Draw(in target, current.ContentFinderConditionId, current.Quests.Count, drawContent);
    }

    private void DrawContent()
    {
        if (model is not { } m)
        {
            return;
        }

        var lineHeight = ImGui.GetTextLineHeight();
        var glyph = UiMetrics.InlineGlyphSize(lineHeight);
        var indent = ImGui.CalcTextSize(Strings.DutyHintUnlockedBy).X + ImGui.GetStyle().ItemSpacing.X;

        GamePanelShell.Caption(Strings.DutyHintCaption);
        ImGui.TextUnformatted(m.DutyName);

        // "Route to unlock" (1.6.0): the route window on every quest that opens the duty, the cheapest way first.
        if (OpenRoute is { } openRoute && m.AllQuestRowIds.Count > 0)
        {
            ImGui.SameLine();
            if (shell.Button(Strings.DutyHintRoute, Strings.DutyHintRouteTooltip))
            {
                openRoute(m);
            }
        }

        var quests = m.Quests;
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
            using (ImRaii.PushColor(ImGuiCol.Text, line.Done ? Theme.AccentDim : GamePanelShell.QuietTone))
            {
                ImGui.TextUnformatted(line.StatusText);
            }

            if (shell.Button(Strings.DutyHintReveal, Strings.DutyHintRevealHint))
            {
                reveal(line.Quest);
            }

            ImGui.SameLine();
            var canFlag = links.CanFlagMap(line.Quest);
            if (shell.Button(Strings.DutyHintFlagGiver, canFlag ? Strings.DutyHintFlagGiverHint : Strings.DutyHintNoGiver, canFlag))
            {
                links.FlagMap(line.Quest);
            }
        }

        if (m.MoreText.Length > 0)
        {
            ImGui.TextDisabled(m.MoreText);
        }
    }
}
