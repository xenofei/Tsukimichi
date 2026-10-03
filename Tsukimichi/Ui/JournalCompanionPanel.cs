using System;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Model;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The Journal companion (1.7.0), drawn side: beside the game's Journal (its list and detail halves together) while
/// <see cref="JournalCompanionHint.Current"/> knows the selected quest. Tsukimichi's verdict on it for the logged-in
/// character: its moon and name, the verdict line, its status, Moonlit rewards, what it unlocks, its chain step and
/// the quest after it, and the facts line; then Open in Tsukimichi and Route to this. Spoiler-safe like every panel
/// beside a game window.
/// </summary>
public sealed class JournalCompanionPanel
{
    private readonly JournalCompanionHint hint;
    private readonly Action<QuestRecord> reveal;
    private readonly Action<QuestRecord, string> route;
    private readonly GamePanelShell shell = new(Strings.GamePanelJournalWindowId);
    private readonly Action drawContent;
    private QuestBrief? current;

    /// <param name="reveal">Opens the main window on the quest.</param>
    /// <param name="route">Opens the route window on the quest, under the name given.</param>
    public JournalCompanionPanel(JournalCompanionHint hint, Action<QuestRecord> reveal, Action<QuestRecord, string> route)
    {
        this.hint = hint ?? throw new ArgumentNullException(nameof(hint));
        this.reveal = reveal ?? throw new ArgumentNullException(nameof(reveal));
        this.route = route ?? throw new ArgumentNullException(nameof(route));
        drawContent = DrawContent;
    }

    /// <summary><c>UiBuilder.Draw</c> handler.</summary>
    public void Draw()
    {
        var next = hint.Current();
        if (next is null || !hint.TryGetWindowRect(out var target))
        {
            // The last quest's panel lingers a moment (feature plan v6 M2): stepping through the list keeps it up.
            shell.Linger(drawContent);
            return;
        }

        current = next;

        shell.Draw(in target, current.Quest.RowId, GamePanelShell.BriefLines(current) + (current.ChainNext is null ? 0 : 1), drawContent);
    }

    private void DrawContent()
    {
        if (current is not { } brief)
        {
            return;
        }

        GamePanelShell.Caption(Strings.GamePanelJournalCaption);
        GamePanelShell.BriefBody(brief);
        if (!brief.Masked && brief.ChainNext is not null)
        {
            ImGui.TextDisabled(Strings.GamePanelChainNext);
            ImGui.SameLine();
            ImGui.TextUnformatted(brief.ChainNextName);
        }

        ImGui.Spacing();
        if (shell.Button(Strings.GamePanelOpen, Strings.GamePanelOpenHint))
        {
            reveal(brief.Quest);
        }

        ImGui.SameLine();
        if (shell.Button(Strings.GamePanelRoute, Strings.GamePanelRouteHint))
        {
            route(brief.Quest, brief.Name);
        }
    }
}
