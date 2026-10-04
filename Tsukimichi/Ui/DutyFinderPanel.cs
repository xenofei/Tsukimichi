using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The Duty Finder unlock hint (P13), drawn side: a small borderless window beside the game's Duty Finder while a
/// padlocked duty is selected (<see cref="DutyFinderHint.Current"/>): "Locked duty", the duty's icon and name, its clear
/// badges (1.19.0, C7), then per quest that unlocks it "Unlocked by:" with its state moon and name,
/// <see cref="Core.Evaluation.BlockerText.StatusText"/> under it in the quieter tone (the quest done: in the dimmed
/// accent), and the buttons Reveal in Tsukimichi and Flag giver. For a selected roulette with something left
/// (<see cref="DutyFinderHint.CurrentRoulette"/>, 1.19.0 N4) it shows the Duties card's block instead: the roulette and
/// its state, then its first duties not unlocked, each with its size badge, "not unlocked · with" the quest that unlocks
/// it and Reveal, and Route to unlock over those quests.
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
    private readonly ITextureProvider textures;
    private readonly Action<QuestRecord> reveal;
    private readonly GamePanelShell shell = new(Strings.DutyHintWindowId);
    private readonly Action drawContent;

    /// <summary>The panel's size key of a roulette: apart from every ContentFinderCondition id.</summary>
    private const uint RouletteKey = 0x8000_0000;

    // The model this frame's content draws: a duty's, or a roulette's.
    private DutyHintModel? model;
    private RouletteHintModel? roulette;

    /// <param name="textures">Draws the duty's icon.</param>
    /// <param name="reveal">Opens the main window on the quest (the Journal reveal every in-world surface uses).</param>
    public DutyFinderPanel(DutyFinderHint hint, GameLinks links, ITextureProvider textures, Action<QuestRecord> reveal)
    {
        this.hint = hint ?? throw new ArgumentNullException(nameof(hint));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.textures = textures ?? throw new ArgumentNullException(nameof(textures));
        this.reveal = reveal ?? throw new ArgumentNullException(nameof(reveal));
        drawContent = DrawContent;
    }

    /// <summary>Opens the route to the duty (set by the plugin, which builds the target); null hides "Route to unlock".</summary>
    public Action<DutyHintModel>? OpenRoute { get; set; }

    /// <summary>Opens the route over a roulette's unlock quests (1.19.0, N4); null hides its "Route to unlock".</summary>
    public Action<Core.Route.RouteTarget>? OpenRouteTarget { get; set; }

    /// <summary><c>UiBuilder.Draw</c> handler.</summary>
    public void Draw()
    {
        // Off, paused by the kill switch, closed, unlocked or unknown: the hint answers null before any game read.
        var current = hint.Current();
        var currentRoulette = current is null ? hint.CurrentRoulette() : null;
        if ((current is null && currentRoulette is null) || !hint.TryGetWindowRect(out var target))
        {
            // The last duty's panel lingers a moment (feature plan v6 M2), so arrowing past an unlocked duty keeps it up.
            shell.Linger(drawContent);
            return;
        }

        // A new duty or another number of quests changes the size; a session change that only rewords a status line
        // does not, so it keeps the panel visible and clickable.
        model = current;
        roulette = currentRoulette;
        if (current is not null)
        {
            shell.Draw(in target, current.ContentFinderConditionId, current.Quests.Count, drawContent);
        }
        else if (currentRoulette is not null)
        {
            shell.Draw(in target, RouletteKey | currentRoulette.RouletteId, currentRoulette.Rows.Count, drawContent);
        }
    }

    private void DrawContent()
    {
        if (model is not { } m)
        {
            if (roulette is { } r)
            {
                DrawRoulette(r);
            }

            return;
        }

        var lineHeight = ImGui.GetTextLineHeight();
        var glyph = UiMetrics.InlineGlyphSize(lineHeight);
        var indent = ImGui.CalcTextSize(Strings.DutyHintUnlockedBy).X + ImGui.GetStyle().ItemSpacing.X;

        GamePanelShell.Caption(Strings.DutyHintCaption);

        // The duty's icon, as the Duty Finder lists it, a little taller than the name it stands before.
        if (m.Icon != 0)
        {
            var side = MathF.Round(lineHeight * 1.25f);
            var top = ImGui.GetCursorScreenPos();
            GameIcon.Draw(textures, m.Icon, side);
            ImGui.SameLine();
            ImGui.SetCursorScreenPos(new System.Numerics.Vector2(ImGui.GetCursorScreenPos().X, top.Y + ((side - lineHeight) * 0.5f)));
        }

        ImGui.TextUnformatted(m.DutyName);

        // "Route to unlock" (1.6.0): the route window on every quest that opens the duty, the cheapest way first.
        if (OpenRoute is { } openRoute && m.AllQuestRowIds.Count > 0)
        {
            ImGui.SameLine();
            if (shell.Button(ActionGlyphs.Route, Strings.DutyHintRoute, Strings.DutyHintRouteTooltip))
            {
                openRoute(m);
            }
        }

        // How you'll clear it (C7): the duty's badges on a line of their own under its name.
        DrawBadges(m.Badges);

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

            if (shell.Button(ActionGlyphs.Reveal, Strings.DutyHintReveal, Strings.DutyHintRevealHint))
            {
                reveal(line.Quest);
            }

            ImGui.SameLine();
            var canFlag = links.CanFlagMap(line.Quest);
            if (shell.Button(ActionIcons.FlagIcon, Strings.DutyHintFlagGiver, canFlag ? Strings.DutyHintFlagGiverHint : Strings.DutyHintNoGiver, canFlag))
            {
                links.FlagMap(line.Quest);
            }
        }

        if (m.MoreText.Length > 0)
        {
            ImGui.TextDisabled(m.MoreText);
        }
    }

    /// <summary>A run of badges, side by side, as one line; nothing for none.</summary>
    private void DrawBadges(IReadOnlyList<DutyBadges.Look> badges)
    {
        for (var i = 0; i < badges.Count; i++)
        {
            if (i > 0)
            {
                ImGui.SameLine(0f, DutyBadges.RunGap);
            }

            DutyBadges.Draw(badges[i], textures);
        }
    }

    /// <summary>
    /// A roulette with something left, as the Duties card's block (spec-1.19 N4): its name and Route to unlock, its state
    /// in the quieter tone, then per duty not unlocked its name and size badge, "not unlocked · with" the quest, and
    /// Reveal.
    /// </summary>
    private void DrawRoulette(RouletteHintModel r)
    {
        GamePanelShell.Caption(Strings.DutyHintRouletteCaption);
        Chrome.SemiboldText(r.Header, Theme.Surface.Text);
        if (OpenRouteTarget is { } openRoute && r.Route is { } route)
        {
            ImGui.SameLine();
            if (shell.Button(ActionGlyphs.Route, Strings.DutyHintRoute, Strings.DutyHintRouletteRouteTooltip))
            {
                openRoute(route);
            }
        }

        GamePanelShell.Quiet(r.State);
        var rows = r.Rows;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            using var id = ImRaii.PushId(i);
            ImGui.Spacing();
            ImGui.TextUnformatted(row.Duty);
            if (row.Badges.Count > 0)
            {
                ImGui.SameLine();
                DrawBadges(row.Badges);
            }

            using (ImRaii.PushColor(ImGuiCol.Text, GamePanelShell.QuietTone))
            {
                ImGui.TextUnformatted(row.Trailing);
            }

            if (row.Quest is not { } quest)
            {
                continue;
            }

            ImGui.SameLine(0f, 0f);
            ImGui.TextUnformatted(row.QuestName);
            if (shell.Button(ActionGlyphs.Reveal, Strings.DutyHintReveal, Strings.DutyHintRevealHint))
            {
                reveal(quest);
            }
        }

        if (r.MoreText.Length > 0)
        {
            ImGui.TextDisabled(r.MoreText);
        }
    }
}
