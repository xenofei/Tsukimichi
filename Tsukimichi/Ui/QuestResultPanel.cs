using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Model;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The "What this opened" panel (1.7.0), drawn side: beside the game's quest-complete window while
/// <see cref="QuestResultHint.Current"/> knows the quest being turned in. "What this opened", the quest's moon and name,
/// the summary ("Opens 1 main scenario quest, 2 unlock quests"), each quest it opens with its moon, name, Flag giver
/// and Pin (the first five, then "and N more"), what it unlocks, and the next quest of its chain with the same two
/// buttons. Names go through the logged-in character's spoiler shield. It never presses Complete.
/// </summary>
public sealed class QuestResultPanel
{
    private readonly QuestResultHint hint;
    private readonly SessionState session;
    private readonly QueryRunner runner;
    private readonly GameLinks links;
    private readonly GamePanelShell shell = new(Strings.GamePanelResultWindowId);
    private readonly Action drawContent;
    private QuestResultModel? current;

    public QuestResultPanel(QuestResultHint hint, SessionState session, QueryRunner runner, GameLinks links)
    {
        this.hint = hint ?? throw new ArgumentNullException(nameof(hint));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
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

        var lines = 3 + current.Opened.Count + current.Brief.Unlocks.Count + (current.ChainNext is null ? 0 : 2) + (current.MoreText.Length > 0 ? 1 : 0);
        shell.Draw(in target, current.Brief.Quest.RowId, lines, drawContent);
    }

    private void DrawContent()
    {
        if (current is not { } model)
        {
            return;
        }

        var brief = model.Brief;
        GamePanelShell.Caption(Strings.GamePanelResultCaption);
        GamePanelShell.QuestLine(brief.State, brief.Name);
        GamePanelShell.Quiet(model.Summary);

        for (var i = 0; i < model.Opened.Count; i++)
        {
            using var id = ImRaii.PushId(i);
            QuestRow(model.Opened[i]);
        }

        if (model.MoreText.Length > 0)
        {
            ImGui.TextDisabled(model.MoreText);
        }

        if (!brief.Masked && brief.Unlocks.Count > 0)
        {
            ImGui.Spacing();
            ImGui.TextDisabled(Strings.GamePanelUnlocksHeading);
            foreach (var unlock in brief.Unlocks)
            {
                using (ImRaii.PushIndent(UiMetrics.Px(8f), scaled: false))
                {
                    ImGui.TextUnformatted(unlock);
                }
            }
        }

        if (model.ChainNext is { } next)
        {
            ImGui.Spacing();
            if (brief.ChainLine.Length > 0)
            {
                GamePanelShell.Quiet(brief.ChainLine);
            }

            using var id = ImRaii.PushId("next");
            ImGui.TextDisabled(Strings.GamePanelChainNext);
            ImGui.SameLine();
            QuestRow(next);
        }
    }

    /// <summary>One quest: its moon and name, then Flag giver and Pin on the same line.</summary>
    private void QuestRow(OpenedLine line)
    {
        GamePanelShell.QuestLine(line.State, line.Name);
        ImGui.SameLine();
        var canFlag = links.CanFlagMap(line.Quest);
        if (shell.Button(Strings.DutyHintFlagGiver, canFlag ? Strings.DutyHintFlagGiverHint : Strings.DutyHintNoGiver, canFlag))
        {
            links.FlagMap(line.Quest);
        }

        ImGui.SameLine();
        var canPin = GamePanelShell.CanPinLive(session, runner);
        var pinned = canPin && runner.IsPinned(line.Quest.RowId);
        if (shell.Button(pinned ? Strings.GamePanelUnpin : Strings.GamePanelPin, canPin ? Strings.GamePanelPinHint : Strings.GamePanelPinUnavailable, canPin))
        {
            runner.TogglePin(line.Quest.RowId);
        }
    }
}
