using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.HandIn;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Sources;
using Tsukimichi.Core.Unique;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// "Rewards you can buy back" (feature plan v7, 1.19.0, C6; spec-1.19 "C6") in the Rewards section of a finished quest:
/// under the tiles, one line per item reward that has something to say, its name on a chip with a Silver border and
/// its state after it in Secondary (<see cref="RewardStates"/>): "Done, not learned · reclaim at a recompense officer,
/// 100 gil" for a roll, minion, card or emote book the game has not learned; "Not on you · buy it back from a Calamity
/// salvager, 100 gil" for an item the character no longer carries; where it is when it sits elsewhere ("In your
/// armoury chest"); "Not offered by the Salvager" for a reward only the quest gives when no shop sells it back. Only the
/// buy-back line wears the gold dot, and only it offers Flag and Teleport (the latter at the automation level). The
/// counts and the learned flags are the logged-in character's; another character on view, or an item the reads cannot
/// place (the saddlebag not opened this session, the retainers without Allagan Tools), gets the buy-back facts without
/// "Not on you", the gold dot or the actions ("If you no longer have it · buy it back from …"). It never says which
/// optional reward was picked: the game does not record it.
/// </summary>
public sealed partial class DetailPane
{
    /// <summary>
    /// Whether the game has learned a reward (its unlock flag: the reward, the quest that gives it); null when it cannot
    /// tell. Attached by the plugin (<c>RewardUnlockReader</c>); null leaves collectibles to their buy-back facts alone.
    /// </summary>
    public Func<RewardRef, uint, bool?>? RewardLearned { get; set; }

    private sealed class RewardStateRow(RewardTile tile)
    {
        public RewardTile Tile { get; } = tile;

        /// <summary>What the line was composed from; it is composed again only when one of them moves.</summary>
        public (RewardWhereabouts Where, bool? Learned, BuyBack? BuyBack) Key { get; set; }

        public bool Composed { get; set; }

        public RewardStateLine? Line { get; set; }
    }

    private readonly List<RewardStateRow> rewardStateRows = [];
    private uint rewardStateRowId = uint.MaxValue;
    private int rewardStateVersion = -1;
    private ItemSourceIndex? rewardStateSources;

    /// <summary>The state lines under a finished quest's reward tiles; nothing for a quest not done or a reward with nothing to say.</summary>
    private void DrawRewardStates(SessionState session, QuestRecord quest)
    {
        if (model.State is not (QuestState.Completed or QuestState.DoneThisCycle))
        {
            return;
        }

        RefreshRewardStates();
        var first = true;
        foreach (var row in rewardStateRows)
        {
            if (ComposeRewardState(session, quest, row) is not { } line)
            {
                continue;
            }

            using var id = ImRaii.PushId((int)row.Tile.Reward.ItemId);
            if (first)
            {
                ImGui.Dummy(new Vector2(1f, UiMetrics.Px(4f)));
                first = false;
            }

            DrawRewardStateRow(row.Tile.Reward.Name, line);
        }
    }

    /// <summary>The rows: every item reward of the quest, built again when the model or the item sources change.</summary>
    private void RefreshRewardStates()
    {
        var sources = links.ItemSources;
        if (rewardStateRowId == model.RowId && rewardStateVersion == model.Version && ReferenceEquals(rewardStateSources, sources))
        {
            return;
        }

        rewardStateRowId = model.RowId;
        rewardStateVersion = model.Version;
        rewardStateSources = sources;
        rewardStateRows.Clear();
        foreach (var tile in model.Rewards)
        {
            if (tile.Reward.ItemId != 0)
            {
                rewardStateRows.Add(new RewardStateRow(tile));
            }
        }
    }

    /// <summary>The row's line for this frame's reads, composed again only when they moved.</summary>
    private RewardStateLine? ComposeRewardState(SessionState session, QuestRecord quest, RewardStateRow row)
    {
        var reward = row.Tile.Reward;
        var collectible = Collectibles.IsStored(reward.Kind);
        var learned = collectible ? RewardLearned?.Invoke(reward, quest.RowId) : null;
        var where = session.IsLive && Stock is { } stock ? RewardStates.WhereaboutsOf(stock.For(reward.ItemId)) : RewardWhereabouts.Unknown;
        var buyBack = links.BuyBackOf(reward.ItemId, quest.RowId)?.BuyBack;
        var key = (where, learned, buyBack);
        if (!row.Composed || row.Key != key)
        {
            row.Composed = true;
            row.Key = key;
            row.Line = RewardStates.For(collectible, learned, where, buyBack, row.Tile.Unique);
        }

        return row.Line;
    }

    /// <summary>
    /// One row: the reward's name on a chip with a Silver border, then its state (the gold dot before a buy-back line),
    /// wrapping beside the chip; Flag and Teleport under a buy-back line whose vendor the data places.
    /// </summary>
    private void DrawRewardStateRow(string name, RewardStateLine line)
    {
        var dl = ImGui.GetWindowDrawList();
        var padX = UiMetrics.Px(8f);
        var chipHeight = ImGui.GetTextLineHeight() + UiMetrics.Px(6f);
        var nameSize = ImGui.CalcTextSize(name);
        var chipWidth = MathF.Min(nameSize.X + (2f * padX), MathF.Max(UiMetrics.Px(60f), RoomTo(cardRight) * 0.45f));
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(chipWidth, chipHeight);
        ImGui.Dummy(max - min);
        var rounding = chipHeight * 0.5f;
        dl.AddRectFilled(min, max, Theme.U32(Theme.Surface.Raised), rounding);
        // Silver is the palette's Done tone (Completed in the hero); gold stays for the buy-back line's dot.
        dl.AddRect(min, max, Theme.StateColorU32(QuestState.Completed), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        var textPos = new Vector2(min.X + padX, min.Y + ((chipHeight - nameSize.Y) * 0.5f));
        var cut = Chrome.EllipsisTextAt(dl, textPos, chipWidth - (2f * padX), name, Theme.U32(Theme.Surface.Text));
        if (cut && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(name);
        }

        ImGui.SameLine(0f, UiMetrics.Px(10f));
        var at = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(new Vector2(at.X, min.Y + ((chipHeight - ImGui.GetTextLineHeight()) * 0.5f)));
        using (ImRaii.Group())
        {
            var room = MathF.Max(1f, RoomTo(cardRight));
            if (line.IsBuyBack)
            {
                // The gold dot: only the buy-back line wears it (spec-1.19 approval record), so gold keeps meaning "act now".
                var dot = UiMetrics.Px(6f);
                var dotAt = ImGui.GetCursorScreenPos();
                dl.AddCircleFilled(new Vector2(dotAt.X + (dot * 0.5f), dotAt.Y + (ImGui.GetTextLineHeight() * 0.5f)), dot * 0.5f, Theme.GoldU32, 12);
                ImGui.Dummy(new Vector2(dot, ImGui.GetTextLineHeight()));
                ImGui.SameLine(0f, UiMetrics.Px(6f));
                room = MathF.Max(1f, RoomTo(cardRight));
            }

            TextFlow.Wrapped(line.Text, room, Theme.U32(Theme.Surface.TextSecondary));
            if (line.BuyBack is { } buyBack && ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(BuyBacks.Tooltip(buyBack));
            }

            if (line.IsBuyBack && line.BuyBack?.Vendor.Spot is { } spot)
            {
                DrawSpotActions(spot);
            }
        }

        ImGui.Dummy(new Vector2(1f, UiMetrics.Px(2f)));
    }
}
