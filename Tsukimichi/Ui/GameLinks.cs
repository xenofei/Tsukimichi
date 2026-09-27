using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Ui;

/// <summary>
/// The only place UI code touches the game: map flags, the in-game journal, chat links and the few sheet lookups
/// (Map, World, ClassJobCategory) the panes need for labels. Every game call is wrapped; a failure logs one warning
/// and the UI carries on. Sheet lookups are cached per id since the sheets never change at runtime.
/// </summary>
public sealed class GameLinks(IGameGui gameGui, IChatGui chat, IDataManager data, IPluginLog log)
{
    /// <summary>What the Map sheet says about one map: scale, offsets and names.</summary>
    public sealed record MapInfo(uint MapId, ushort SizeFactor, short OffsetX, short OffsetY, string PlaceName, string Region);

    private readonly Dictionary<uint, MapInfo?> maps = [];
    private readonly Dictionary<uint, string> worlds = [];
    private readonly Dictionary<uint, string> classJobCategories = [];

    /// <summary>True when the issuer has a territory and map to flag.</summary>
    public bool CanFlagMap(QuestRecord quest) => quest.Issuer is { TerritoryId: > 0, MapId: > 0 };

    /// <summary>Human-readable map coordinates of the issuer, or null without a map.</summary>
    public Vector2? MapCoordinates(QuestRecord quest)
    {
        if (quest.Issuer is not { } issuer || Map(issuer.MapId) is not { } map)
        {
            return null;
        }

        return new Vector2(ToMapCoordinate(issuer.X, map.OffsetX, map.SizeFactor), ToMapCoordinate(issuer.Z, map.OffsetY, map.SizeFactor));
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
    public void PrintQuestLink(QuestRecord quest)
    {
        try
        {
            chat.Print(BuildQuestLine(quest), Strings.ChatTag);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Chat link for quest {RowId} failed", quest.RowId);
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

    private SeString BuildQuestLine(QuestRecord quest)
    {
        var builder = new SeStringBuilder()
            .Add(new QuestPayload(quest.RowId))
            .AddText(quest.Name)
            .Add(RawPayload.LinkTerminator);

        if (MapLink(quest) is { } link)
        {
            builder.AddText("  ")
                   .Add(link)
                   .AddText(link.PlaceName + " " + link.CoordinateString)
                   .Add(RawPayload.LinkTerminator);
        }

        return builder.Build();
    }
}
