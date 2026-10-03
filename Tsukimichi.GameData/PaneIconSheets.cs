using System.Collections.Frozen;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.GameData;

/// <summary>
/// The sheet columns of <see cref="PaneIcons"/> (UI-5d), read once: <c>ContentType.Icon</c>, <c>BeastTribe.Icon</c> and
/// <c>IconReputation</c>, the three <c>GrandCompanyRank</c> insignia columns and <c>Achievement.Icon</c>. Each sheet is
/// read on its own: one that cannot be read leaves only its icons out, and is logged. Standalone (takes an
/// <see cref="ExcelModule"/>) so tests read it without Dalamud. Icons are language-independent, so the sheets are read
/// in the module's default language. Immutable.
/// </summary>
public sealed class PaneIconSheets : IPaneIconSheets
{
    public static readonly PaneIconSheets Empty = new(
        FrozenDictionary<uint, uint>.Empty,
        FrozenDictionary<byte, (uint, uint)>.Empty,
        FrozenDictionary<(byte, byte), uint>.Empty,
        FrozenDictionary<uint, uint>.Empty);

    private readonly FrozenDictionary<uint, uint> contentTypes;
    private readonly FrozenDictionary<byte, (uint Emblem, uint Reputation)> tribes;
    private readonly FrozenDictionary<(byte Company, byte Rank), uint> ranks;
    private readonly FrozenDictionary<uint, uint> achievements;

    private PaneIconSheets(
        FrozenDictionary<uint, uint> contentTypes,
        FrozenDictionary<byte, (uint, uint)> tribes,
        FrozenDictionary<(byte, byte), uint> ranks,
        FrozenDictionary<uint, uint> achievements)
    {
        this.contentTypes = contentTypes;
        this.tribes = tribes;
        this.ranks = ranks;
        this.achievements = achievements;
    }

    /// <summary>How many icons were read, for the warm-up log line.</summary>
    public int Count => contentTypes.Count + tribes.Count + ranks.Count + achievements.Count;

    /// <summary>Reads the four sheets once.</summary>
    public static PaneIconSheets Build(ExcelModule excel, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(excel);

        void Warn(string sheet, Exception ex) =>
            log?.Invoke($"The {sheet} icons could not be read ({ex.GetType().Name}: {ex.Message}); those rows keep their stand-in");

        var contentTypes = new Dictionary<uint, uint>();
        try
        {
            foreach (var row in excel.GetSheet<ContentType>())
            {
                if (row.Icon != 0)
                {
                    contentTypes[row.RowId] = row.Icon;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Warn(nameof(ContentType), ex);
        }

        var tribes = new Dictionary<byte, (uint, uint)>();
        try
        {
            foreach (var row in excel.GetSheet<BeastTribe>())
            {
                if (row.RowId is > 0 and <= byte.MaxValue && (row.Icon != 0 || row.IconReputation != 0))
                {
                    tribes[(byte)row.RowId] = (row.Icon, row.IconReputation);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Warn(nameof(BeastTribe), ex);
        }

        var ranks = new Dictionary<(byte, byte), uint>();
        try
        {
            foreach (var row in excel.GetSheet<GrandCompanyRank>())
            {
                if (row.RowId is 0 or > byte.MaxValue)
                {
                    continue;
                }

                var rank = (byte)row.RowId;
                Add(ranks, (1, rank), row.IconMaelstrom);
                Add(ranks, (2, rank), row.IconSerpents);
                Add(ranks, (3, rank), row.IconFlames);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Warn(nameof(GrandCompanyRank), ex);
        }

        var achievements = new Dictionary<uint, uint>();
        try
        {
            foreach (var row in excel.GetSheet<Achievement>())
            {
                if (row.Icon != 0)
                {
                    achievements[row.RowId] = row.Icon;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Warn(nameof(Achievement), ex);
        }

        return new PaneIconSheets(contentTypes.ToFrozenDictionary(), tribes.ToFrozenDictionary(), ranks.ToFrozenDictionary(), achievements.ToFrozenDictionary());
    }

    public uint ContentTypeIcon(uint contentType) => contentTypes.GetValueOrDefault(contentType);

    public uint TribeIcon(byte tribe) => tribes.TryGetValue(tribe, out var t) ? t.Emblem : 0;

    public uint TribeReputationIcon(byte tribe) => tribes.TryGetValue(tribe, out var t) ? t.Reputation : 0;

    public uint GrandCompanyRankIcon(byte grandCompany, byte rank) => ranks.GetValueOrDefault((grandCompany, rank));

    public uint AchievementIcon(uint achievement) => achievements.GetValueOrDefault(achievement);

    /// <summary>Every allied society with an icon, for the game-data test.</summary>
    public IEnumerable<byte> Tribes => tribes.Keys;

    /// <summary>Every Grand Company rank with an insignia, for the game-data test.</summary>
    public IEnumerable<(byte Company, byte Rank)> Ranks => ranks.Keys;

    private static void Add(Dictionary<(byte, byte), uint> ranks, (byte, byte) key, int icon)
    {
        if (icon > 0)
        {
            ranks[key] = (uint)icon;
        }
    }
}
