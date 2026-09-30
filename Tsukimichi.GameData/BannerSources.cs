using System.Collections.Frozen;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Unique;

namespace Tsukimichi.GameData;

/// <summary>
/// The sheet side of the hero banner chain (<see cref="BannerIndex"/>): each duty's Duty Finder banner
/// (<c>ContentFinderCondition.Image</c>, an icon id in the same 376 × 120 format as a journal banner), which duties a
/// quest unlocks, and each territory's loading-screen image (<c>TerritoryType.LoadingImage</c> →
/// <c>LoadingImage.FileName</c> → <c>ui/loadingimage/&lt;FileName&gt;.tex</c>, checked against the game data).
/// Standalone (takes an <see cref="ExcelModule"/>) so tests run it without Dalamud.
/// </summary>
public sealed class BannerSources : IBannerLookups
{
    /// <summary>
    /// The loading image's texture path: the 1920 × 1080 file. The <c>_hr1</c> twin (3840 × 2160, four times the memory)
    /// exists for every row too, but a banner at most a pane wide never needs it.
    /// </summary>
    public const string LoadingImagePathFormat = "ui/loadingimage/{0}.tex";

    private readonly FrozenDictionary<uint, (uint Icon, uint Condition)> dutyBannerByQuest;
    private readonly FrozenDictionary<uint, string> zonePathByTerritory;

    private BannerSources(FrozenDictionary<uint, (uint, uint)> dutyBannerByQuest, FrozenDictionary<uint, string> zonePathByTerritory)
    {
        this.dutyBannerByQuest = dutyBannerByQuest;
        this.zonePathByTerritory = zonePathByTerritory;
    }

    /// <summary>How many quests have a duty banner, and how many territories a loading image.</summary>
    public int DutyQuestCount => dutyBannerByQuest.Count;

    public int ZoneCount => zonePathByTerritory.Count;

    /// <summary>
    /// Reads the sheets once. A quest's duties, in order: the <paramref name="dutyUnlocks"/> index (curated unlocks and
    /// the reward data; the lowest-numbered duty with an image when it names several), then the quest's own sheet
    /// rewards (<see cref="RewardKind.DutyUnlock"/> with a ContentFinderCondition id, <see cref="RewardKind.Instance"/>
    /// with an InstanceContent id) in reward order.
    /// </summary>
    public static BannerSources Build(ExcelModule excel, QuestCatalog catalog, DutyUnlockIndex? dutyUnlocks = null)
    {
        ArgumentNullException.ThrowIfNull(excel);
        ArgumentNullException.ThrowIfNull(catalog);

        var imageByCondition = new Dictionary<uint, uint>();
        var conditionByInstance = new Dictionary<uint, uint>();
        foreach (var row in excel.GetSheet<ContentFinderCondition>())
        {
            if (row.Image != 0)
            {
                imageByCondition[row.RowId] = row.Image;
                if (row.ContentLinkType == DutyIndex.InstanceContentLink && row.Content.RowId != 0)
                {
                    conditionByInstance.TryAdd(row.Content.RowId, row.RowId);
                }
            }
        }

        var byQuest = new Dictionary<uint, (uint, uint)>();
        if (dutyUnlocks is not null)
        {
            foreach (var condition in imageByCondition.Keys.Order())
            {
                foreach (var quest in dutyUnlocks.QuestsFor(condition))
                {
                    // A quest that unlocks several duties keeps the lowest-numbered one with an image (the earliest duty).
                    byQuest.TryAdd(quest, (imageByCondition[condition], condition));
                }
            }
        }

        foreach (var quest in catalog.All)
        {
            if (byQuest.ContainsKey(quest.RowId))
            {
                continue;
            }

            foreach (var reward in quest.Rewards)
            {
                var condition = reward.Kind switch
                {
                    RewardKind.DutyUnlock => reward.Id,
                    RewardKind.Instance => conditionByInstance.GetValueOrDefault(reward.Id),
                    _ => 0u,
                };
                if (condition != 0 && imageByCondition.TryGetValue(condition, out var image))
                {
                    byQuest[quest.RowId] = (image, condition);
                    break;
                }
            }
        }

        var byFile = new Dictionary<string, string>(StringComparer.Ordinal);
        var zones = new Dictionary<uint, string>();
        foreach (var row in excel.GetSheet<TerritoryType>())
        {
            if (row.LoadingImage.RowId == 0 || row.LoadingImage.ValueNullable is not { } image)
            {
                continue;
            }

            var file = image.FileName.ExtractText();
            if (file.Length == 0)
            {
                continue;
            }

            if (!byFile.TryGetValue(file, out var path))
            {
                path = string.Format(System.Globalization.CultureInfo.InvariantCulture, LoadingImagePathFormat, file);
                byFile[file] = path;
            }

            zones[row.RowId] = path;
        }

        return new BannerSources(byQuest.ToFrozenDictionary(), zones.ToFrozenDictionary());
    }

    /// <summary>The whole chain for this catalog: <see cref="BannerIndex.Build"/> over these lookups.</summary>
    public BannerIndex Resolve(QuestCatalog catalog) => BannerIndex.Build(catalog, this);

    public bool TryGetDutyBanner(QuestRecord quest, out uint iconId, out uint contentFinderConditionId)
    {
        ArgumentNullException.ThrowIfNull(quest);
        if (dutyBannerByQuest.TryGetValue(quest.RowId, out var hit))
        {
            (iconId, contentFinderConditionId) = hit;
            return true;
        }

        iconId = contentFinderConditionId = 0;
        return false;
    }

    public string? ZoneBannerPath(uint territoryId) => zonePathByTerritory.GetValueOrDefault(territoryId);
}
