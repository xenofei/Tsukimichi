using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The detail pane's Story card (1.9.0 collector extras, feature plan v5): whether New Game+ can replay the quest
/// ("Replayable in New Game+" or "Once only", R9 F7, from the game's New Game+ chapters), the achievements that need
/// several quests and list this one ("Tales of War · 3 of 5 quests", with the quests still to do and whether it is
/// earned, R5 F6), and "Read the story so far" for a quest on a story chain the character has started (R9 F5). The
/// card is left out when it has nothing to say. Lines are built when the selection, the session or the game's
/// achievement flag change, not per frame.
/// </summary>
public sealed partial class DetailPane
{
    private static readonly string StoryIcon = FontAwesomeIcon.BookReader.ToIconString();
    private static readonly Localization.LocText RecapChainLabel = new(static () => Strings.RecapReadChain + "##recapChain");

    /// <summary>How often the achievement lines re-read the game's flag (it changes when the Achievements window loads).</summary>
    private const double CollectorRefreshSeconds = 5.0;

    /// <summary>The game's achievement flag for the viewed character (<c>RewardUnlockReader.AchievementEarned</c>); null lets the quests decide.</summary>
    public Func<uint, bool?>? AchievementEarned { get; set; }

    private sealed record LadderLine(string Text, float Fraction, string Tooltip, IReadOnlyList<(uint RowId, string Name)> Remaining);

    private (uint RowId, int Version, CatalogBundle? Bundle, long Tick) collectorKey = (uint.MaxValue, -1, null, -1);
    private string? replayText;
    private string replayTooltip = string.Empty;
    private readonly List<LadderLine> ladderLines = [];
    private bool recapOffered;

    private void DrawCollector(SessionState session, QuestRecord quest)
    {
        if (model.Bundle is not { } bundle)
        {
            return;
        }

        RefreshCollector(session, bundle, quest);
        if (replayText is null && ladderLines.Count == 0 && !recapOffered)
        {
            return;
        }

        Gap();
        BeginSection("##collector", Strings.CollectorCard, StoryIcon);
        if (replayText is { } replay)
        {
            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                TextFlow.Wrapped(replay, RoomTo(cardRight));
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(replayTooltip);
            }
        }

        for (var i = 0; i < ladderLines.Count; i++)
        {
            using var id = ImRaii.PushId(i);
            DrawLadderLine(ladderLines[i]);
        }

        if (recapOffered)
        {
            if (ImGui.SmallButton(RecapChainLabel.Value))
            {
                ui.OpenRecap(new RecapRequest(quest.RowId));
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.RecapReadChainTooltip);
            }
        }

        EndSection();
    }

    private void DrawLadderLine(LadderLine line)
    {
        var lineHeight = ImGui.GetTextLineHeight();
        var size = UiMetrics.HaloBoxSize(lineHeight);
        MoonGlyph.DrawHaloInline(line.Fraction, size, onCard: true);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(line.Tooltip);
        }

        ImGui.SameLine();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ((size - lineHeight) * 0.5f));
        TextFlow.Wrapped(line.Text, RoomTo(cardRight), Theme.U32(Theme.Surface.TextSecondary));
        if (line.Remaining.Count == 0)
        {
            return;
        }

        using var indent = ImRaii.PushIndent(size + ImGui.GetStyle().ItemSpacing.X);
        ImGui.TextDisabled(Strings.LadderStillToDo);
        for (var i = 0; i < line.Remaining.Count; i++)
        {
            var (rowId, name) = line.Remaining[i];
            using var id = ImRaii.PushId((int)rowId);
            bool cut;
            using (Theme.PushText(Theme.Moon))
            {
                // A long name ends in an ellipsis inside the card; the tooltip then carries it whole.
                if (Chrome.EllipsisSelectable(name, false, RoomTo(cardRight), out cut))
                {
                    RevealRow(rowId);
                }
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(cut ? name : Strings.DetailChainNextTooltip, cut ? Strings.DetailChainNextTooltip : null);
            }
        }
    }

    private void RefreshCollector(SessionState session, CatalogBundle bundle, QuestRecord quest)
    {
        var key = (quest.RowId, session.Version, (CatalogBundle?)bundle, (long)(ImGui.GetTime() / CollectorRefreshSeconds));
        if (key == collectorKey)
        {
            return;
        }

        collectorKey = key;
        replayText = NewGamePlus.Of(quest, bundle.NewGamePlus) switch
        {
            ReplayKind.Replayable => Strings.ReplayableInNewGamePlus,
            ReplayKind.OnceOnly => Strings.OnceOnly,
            _ => null,
        };
        replayTooltip = replayText == Strings.OnceOnly ? Strings.OnceOnlyTooltip : Strings.ReplayableInNewGamePlusTooltip;

        var snapshot = session.ViewedSnapshot;
        bool Done(uint rowId) => snapshot?.IsCompleted(QuestRecord.ToQuestId(rowId)) == true;
        ladderLines.Clear();
        foreach (var ladder in bundle.AchievementLadders.ForQuest(quest.RowId))
        {
            ladderLines.Add(BuildLadderLine(session, bundle, ladder, snapshot is not null, Done));
        }

        recapOffered = snapshot is not null && session.Chains.ForQuest(quest.RowId) is { } chain && StoryRecap.HasStarted(chain, Done);
    }

    private LadderLine BuildLadderLine(SessionState session, CatalogBundle bundle, AchievementLadder ladder, bool hasSnapshot, Func<uint, bool> done)
    {
        if (!hasSnapshot)
        {
            // Browsing without a character: what the achievement asks for, no progress.
            return new LadderLine(
                string.Format(CultureInfo.CurrentCulture, Strings.LadderNeedsFormat, ladder.Name, ladder.RowIds.Count),
                0f,
                Strings.LadderTooltip,
                []);
        }

        var progress = AchievementLadders.Progress(ladder, done, AchievementEarned?.Invoke(ladder.AchievementId));
        var remaining = new List<(uint, string)>(progress.Remaining.Count);
        foreach (var rowId in progress.Remaining)
        {
            remaining.Add((rowId, session.Spoilers.DisplayName(bundle.Catalog, rowId, rowId.ToString(CultureInfo.InvariantCulture))));
        }

        var earned = progress.Earned ? Strings.LadderEarned : Strings.LadderNotEarned;
        var text = string.Format(CultureInfo.CurrentCulture, Strings.LadderLineFormat, ladder.Name, progress.Done, progress.Total, earned);
        var tooltip = progress.FromGame ? Strings.LadderTooltipFromGame : Strings.LadderTooltip;
        return new LadderLine(text, progress.Fraction, tooltip, remaining);
    }
}
