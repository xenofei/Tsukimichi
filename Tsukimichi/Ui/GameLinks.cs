using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The only place UI code touches the game: map flags, the in-game journal, chat links, travel (Lifestream teleports,
/// aethernet hops, vnavmesh walks: GameLinks.Travel.cs) and the few sheet lookups (Map, World, ClassJobCategory,
/// Aetheryte) the panes need for labels. Every game call is wrapped; a failure logs one warning and the UI carries on.
/// Sheet lookups are cached per id since the sheets never change at runtime.
/// </summary>
public sealed partial class GameLinks(IGameGui gameGui, IChatGui chat, IDataManager data, IPluginLog log)
{
    /// <summary>
    /// Whether the FFXIV Online Store also sells a reward (the session's <see cref="Core.Unique.StoreResells"/>), attached
    /// by the plugin so <see cref="RewardTooltip"/> can say "Store only"; null (never) until then.
    /// </summary>
    public Func<RewardRef, bool>? IsStoreResell { get; set; }

    /// <summary>
    /// The name a quest link prints (the session's spoiler shield, <see cref="Core.Query.SpoilerMask.DisplayName(QuestRecord)"/>),
    /// attached by the plugin; the quest's own name until then. A masked quest's link still points at the quest.
    /// </summary>
    public Func<QuestRecord, string>? QuestName { get; set; }

    /// <summary>The name to print for a quest: <see cref="QuestName"/> when attached, else the quest's own name.</summary>
    public string NameOf(QuestRecord quest) => QuestName?.Invoke(quest) ?? quest.Name;

    /// <summary>
    /// Where a duty also drops a reward (<see cref="Core.Unique.StoreResells.DropWhere(RewardRef)"/>: the duties, empty
    /// when unnamed, null when it does not drop), attached by the plugin so <see cref="RewardTooltip"/> can say
    /// "Also drops in …"; null (never) until then.
    /// </summary>
    public Func<RewardRef, string?>? DropWhere { get; set; }

    /// <summary>What the Map sheet says about one map: scale, offsets and names.</summary>
    public sealed record MapInfo(uint MapId, ushort SizeFactor, short OffsetX, short OffsetY, string PlaceName, string Region);

    /// <summary>What the Item sheet says about one item for the reward tooltip. <see cref="Summary"/> is "iLv N · Category", prebuilt.</summary>
    public sealed record ItemInfo(uint ItemLevel, string Category, string Description, string Summary);

    private readonly Dictionary<uint, MapInfo?> maps = [];
    private readonly Dictionary<uint, string> worlds = [];
    private readonly Dictionary<uint, string> classJobCategories = [];
    private readonly ItemInfoCache itemInfo = new();

    /// <summary>Item sheet row for the reward tooltip, read once per item id; null when the id is unknown.</summary>
    public ItemInfo? Item(uint itemId) => itemInfo.Item(itemId, data, log);

    /// <summary>Description of an emote, action or general action reward, read once per reward; empty for other kinds or when the sheet has none.</summary>
    public string RewardDescription(RewardRef reward) => itemInfo.Description(reward, data, log);

    /// <summary>Lazily loaded sheet text for the reward tooltip, keyed by item id and by (kind, id) for the description-only kinds.</summary>
    private sealed class ItemInfoCache
    {
        private readonly Dictionary<uint, ItemInfo?> items = [];
        private readonly Dictionary<(RewardKind Kind, uint Id), string> descriptions = [];

        public ItemInfo? Item(uint itemId, IDataManager data, IPluginLog log)
        {
            if (itemId == 0)
            {
                return null;
            }

            if (items.TryGetValue(itemId, out var cached))
            {
                return cached;
            }

            ItemInfo? info = null;
            try
            {
                if (data.GetExcelSheet<Item>()?.GetRowOrDefault(itemId) is { } row)
                {
                    var level = row.LevelItem.RowId;
                    var category = UiFormat.CleanSheetText(row.ItemUICategory.ValueNullable?.Name.ExtractText() ?? string.Empty);
                    var description = UiFormat.CleanSheetText(row.Description.ExtractText());
                    var summary = level > 1 && category.Length > 0
                        ? string.Format(CultureInfo.CurrentCulture, Strings.ItemSummaryFormat, level, category)
                        : level > 1
                            ? string.Format(CultureInfo.CurrentCulture, Strings.ItemLevelFormat, level)
                            : category;
                    info = new ItemInfo(level, category, description, summary);
                }
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Item sheet lookup for {ItemId} failed", itemId);
            }

            items[itemId] = info;
            return info;
        }

        public string Description(RewardRef reward, IDataManager data, IPluginLog log)
        {
            if (reward.Kind is not (RewardKind.Emote or RewardKind.Action or RewardKind.GeneralAction) || reward.Id == 0)
            {
                return string.Empty;
            }

            var key = (reward.Kind, reward.Id);
            if (descriptions.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var text = string.Empty;
            try
            {
                text = reward.Kind switch
                {
                    RewardKind.Emote => data.GetExcelSheet<Emote>()?.GetRowOrDefault(reward.Id)?.TextCommand.ValueNullable?.Description.ExtractText(),
                    RewardKind.Action => data.GetExcelSheet<ActionTransient>()?.GetRowOrDefault(reward.Id)?.Description.ExtractText(),
                    _ => data.GetExcelSheet<GeneralAction>()?.GetRowOrDefault(reward.Id)?.Description.ExtractText(),
                } ?? string.Empty;
                text = UiFormat.CleanSheetText(text);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Description lookup for {Kind} {Id} failed", reward.Kind, reward.Id);
            }

            descriptions[key] = text;
            return text;
        }
    }

    /// <summary>True when the issuer has a territory and map to flag.</summary>
    public bool CanFlagMap(QuestRecord quest) => quest.Issuer is { TerritoryId: > 0, MapId: > 0 };

    /// <summary>
    /// True when the game journal can show the quest: it only holds accepted and completed quests (a repeatable done
    /// this cycle counts as completed), so every other state has no journal page to open.
    /// </summary>
    public static bool CanOpenJournal(QuestRecord quest, QuestState state) =>
        quest.QuestId != 0 && state is QuestState.Accepted or QuestState.Completed or QuestState.DoneThisCycle;

    /// <summary>Human-readable map coordinates of the issuer, or null without a map.</summary>
    public Vector2? MapCoordinates(QuestRecord quest)
    {
        if (quest.Issuer is not { } issuer || Map(issuer.MapId) is not { } map)
        {
            return null;
        }

        return new Vector2(ToMapCoordinate(issuer.X, map.OffsetX, map.SizeFactor), ToMapCoordinate(issuer.Z, map.OffsetY, map.SizeFactor));
    }

    /// <summary>"Place (x.x, y.y)" for the clipboard, or null without a map. Allocates; call on click, not per frame.</summary>
    public string? CoordinateText(QuestRecord quest)
    {
        if (quest.Issuer is not { } issuer || Map(issuer.MapId) is not { } map || MapCoordinates(quest) is not { } coords)
        {
            return null;
        }

        return string.Format(CultureInfo.CurrentCulture, Strings.CoordinateClipboardFormat, map.PlaceName, coords.X, coords.Y);
    }

    /// <summary>Map link for the issuer in map coordinates, or null when the quest has no mappable issuer.</summary>
    public MapLinkPayload? MapLink(QuestRecord quest)
    {
        if (!CanFlagMap(quest) || quest.Issuer is not { } issuer || MapCoordinates(quest) is not { } coords)
        {
            return null;
        }

        try
        {
            return new MapLinkPayload(issuer.TerritoryId, issuer.MapId, coords.X, coords.Y);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Map link for quest {RowId} failed", quest.RowId);
            return null;
        }
    }

    /// <summary>Opens the in-game map with a flag on the issuer.</summary>
    public void FlagMap(QuestRecord quest)
    {
        try
        {
            if (MapLink(quest) is { } link && !gameGui.OpenMapWithMapLink(link))
            {
                log.Warning("Map did not open for quest {RowId}", quest.RowId);
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Flag map for quest {RowId} failed", quest.RowId);
        }
    }

    /// <summary>Opens the journal on the quest; falls back to a clickable chat link when the agent is unavailable.</summary>
    public unsafe void OpenJournal(QuestRecord quest)
    {
        try
        {
            var agent = AgentQuestJournal.Instance();
            if (agent == null)
            {
                PrintQuestLink(quest);
                return;
            }

            agent->OpenForQuest(quest.QuestId, 1, 0, false);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Open journal for quest {RowId} failed", quest.RowId);
        }
    }

    /// <summary>One chat line: the quest link, then the issuer's map link when there is one.</summary>
    public void PrintQuestLink(QuestRecord quest) => PrintQuestLink(quest, null);

    /// <summary>One chat line: the quest link, the issuer's map link when there is one, then <paramref name="suffix"/> (e.g. a state name).</summary>
    public void PrintQuestLink(QuestRecord quest, string? suffix)
    {
        try
        {
            chat.Print(BuildQuestLine(quest, suffix), Strings.ChatTag);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Chat link for quest {RowId} failed", quest.RowId);
        }
    }

    /// <summary>
    /// The first line of <c>/tsuki why</c>: the quest link, the separator and <paramref name="headline"/>, and, when
    /// <paramref name="linkGiver"/> is set and the giver is mappable, " in " followed by the giver's zone and
    /// coordinates as a map link ("Ready — talk to Gerolt in [Northern Thanalan (23.1, 14.2)]").
    /// </summary>
    public void PrintHeadline(QuestRecord quest, string headline, bool linkGiver)
    {
        try
        {
            var builder = new SeStringBuilder()
                .Add(new QuestPayload(quest.RowId))
                .AddText(NameOf(quest))
                .Add(RawPayload.LinkTerminator)
                .AddText(Strings.ChatSuffixSeparator + headline);

            if (linkGiver && MapLink(quest) is { } link && MapCoordinates(quest) is { } coords)
            {
                builder.AddText(Strings.WhyGiverIn)
                       .Add(link)
                       .AddText(string.Format(CultureInfo.CurrentCulture, Strings.WhyGiverPlaceFormat, link.PlaceName, coords.X, coords.Y))
                       .Add(RawPayload.LinkTerminator);
            }

            chat.Print(builder.Build(), Strings.ChatTag);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Chat headline for quest {RowId} failed", quest.RowId);
        }
    }

    /// <summary>Prints plain text under the plugin's chat tag.</summary>
    public void PrintText(string text)
    {
        try
        {
            chat.Print(text, Strings.ChatTag);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Chat print failed");
        }
    }

    /// <summary>Map sheet row for a map id; null when the id is unknown.</summary>
    public MapInfo? Map(uint mapId)
    {
        if (mapId == 0)
        {
            return null;
        }

        if (maps.TryGetValue(mapId, out var cached))
        {
            return cached;
        }

        MapInfo? info = null;
        try
        {
            if (data.GetExcelSheet<Map>()?.GetRowOrDefault(mapId) is { } row)
            {
                var placeName = row.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
                var region = row.PlaceNameRegion.ValueNullable?.Name.ExtractText() ?? string.Empty;
                info = new MapInfo(mapId, row.SizeFactor, row.OffsetX, row.OffsetY, placeName, region);
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Map sheet lookup for {MapId} failed", mapId);
        }

        maps[mapId] = info;
        return info;
    }

    /// <summary>World name for a world id; the id as text when unknown.</summary>
    public string WorldName(uint worldId)
    {
        if (worlds.TryGetValue(worldId, out var cached))
        {
            return cached;
        }

        var name = worldId.ToString(CultureInfo.InvariantCulture);
        try
        {
            var text = data.GetExcelSheet<World>()?.GetRowOrDefault(worldId)?.Name.ExtractText();
            if (!string.IsNullOrEmpty(text))
            {
                name = text;
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "World sheet lookup for {WorldId} failed", worldId);
        }

        worlds[worldId] = name;
        return name;
    }

    /// <summary>ClassJobCategory sheet name; empty when unknown.</summary>
    public string ClassJobCategoryName(uint categoryId)
    {
        if (classJobCategories.TryGetValue(categoryId, out var cached))
        {
            return cached;
        }

        var name = string.Empty;
        try
        {
            name = data.GetExcelSheet<ClassJobCategory>()?.GetRowOrDefault(categoryId)?.Name.ExtractText() ?? string.Empty;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "ClassJobCategory sheet lookup for {CategoryId} failed", categoryId);
        }

        classJobCategories[categoryId] = name;
        return name;
    }

    /// <summary>Raw Level coordinate to the map coordinate the game shows, using the Map sheet's scale and offset.</summary>
    public static float ToMapCoordinate(float raw, short offset, ushort sizeFactor)
    {
        var scale = sizeFactor / 100f;
        if (scale <= 0f)
        {
            scale = 1f;
        }

        return 41f / scale * ((raw + offset) * scale + 1024f) / 2048f + 1f;
    }

    private SeString BuildQuestLine(QuestRecord quest, string? suffix)
    {
        var builder = new SeStringBuilder()
            .Add(new QuestPayload(quest.RowId))
            .AddText(NameOf(quest))
            .Add(RawPayload.LinkTerminator);

        if (MapLink(quest) is { } link)
        {
            builder.AddText("  ")
                   .Add(link)
                   .AddText(link.PlaceName + " " + link.CoordinateString)
                   .Add(RawPayload.LinkTerminator);
        }

        if (!string.IsNullOrEmpty(suffix))
        {
            builder.AddText(Strings.ChatSuffixSeparator + suffix);
        }

        return builder.Build();
    }
}
