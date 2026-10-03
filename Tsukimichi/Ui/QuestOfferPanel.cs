using System;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Model;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The "Worth it?" panel (1.7.0), drawn side: beside the game's quest-offer window while
/// <see cref="QuestOfferHint.Current"/> knows the offered quest. "Worth it?", the quest's moon and name, the verdict
/// line in the accent colour ("Unlocks Aglaia", "Moonlit: Wind-up Sun"), its status, its Moonlit rewards with owned or
/// not, what it unlocks, its chain step and the facts line ("Added in 7.5 · Repeatable"), then Open in Tsukimichi and
/// Pin. Spoiler-safe: a quest the logged-in character's shield masks shows only its placeholder. It never presses
/// Accept: the game's buttons stay the only way to take the quest.
/// </summary>
public sealed class QuestOfferPanel
{
    private readonly QuestOfferHint hint;
    private readonly SessionState session;
    private readonly QueryRunner runner;
    private readonly Action<QuestRecord> reveal;
    private readonly GamePanelShell shell = new(Strings.GamePanelOfferWindowId);
    private readonly Action drawContent;
    private QuestOfferModel? current;

    /// <param name="reveal">Opens the main window on the quest (the Journal reveal every in-world surface uses).</param>
    public QuestOfferPanel(QuestOfferHint hint, SessionState session, QueryRunner runner, Action<QuestRecord> reveal)
    {
        this.hint = hint ?? throw new ArgumentNullException(nameof(hint));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.reveal = reveal ?? throw new ArgumentNullException(nameof(reveal));
        drawContent = DrawContent;
    }

    /// <summary><c>UiBuilder.Draw</c> handler.</summary>
    public void Draw()
    {
        // Off, paused by the kill switch, closed or unknown: the hint answers null before any game read.
        var next = hint.Current();
        if (next is null || !hint.TryGetWindowRect(out var target))
        {
            // The last quest's panel lingers a moment (feature plan v6 M2): stepping through the list keeps it up.
            shell.Linger(drawContent);
            return;
        }

        current = next;

        shell.Draw(in target, current.Brief.Quest.RowId, GamePanelShell.BriefLines(current.Brief), drawContent);
    }

    private void DrawContent()
    {
        if (current is not { } model)
        {
            return;
        }

        var brief = model.Brief;
        GamePanelShell.Caption(Strings.GamePanelOfferCaption);
        GamePanelShell.BriefBody(brief);
        ImGui.Spacing();
        if (shell.Button(ActionGlyphs.Open, Strings.GamePanelOpen, Strings.GamePanelOpenHint))
        {
            reveal(brief.Quest);
        }

        ImGui.SameLine();
        var canPin = GamePanelShell.CanPinLive(session, runner);
        var pinned = canPin && runner.IsPinned(brief.Quest.RowId);
        if (shell.Button(ActionGlyphs.Pin, pinned ? Strings.GamePanelUnpin : Strings.GamePanelPin, canPin ? Strings.GamePanelPinHint : Strings.GamePanelPinUnavailable, canPin))
        {
            runner.TogglePin(brief.Quest.RowId);
        }
    }
}
