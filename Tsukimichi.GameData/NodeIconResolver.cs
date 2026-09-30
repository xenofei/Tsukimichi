using System.Collections.Frozen;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.GameData;

/// <summary>
/// The journal tree's node icons from the game sheets (Moon Road proposal §6.2): reads <c>JournalGenre.Icon</c>,
/// <c>ExVersion.Icon</c>, <c>BeastTribe.IconReputation</c> and <c>Icon</c>, and <c>ContentType.Icon</c> once, then
/// hands them to the pure rules in <see cref="NodeIcons"/>. Standalone (takes an <see cref="ExcelModule"/>) so tests
/// run it without Dalamud. Icons are language-independent, so the sheets are read in the module's default language.
/// </summary>
public sealed class NodeIconResolver : INodeIconSheets
{
    private readonly FrozenDictionary<uint, uint> genres;
    private readonly FrozenDictionary<uint, uint> expansions;
    private readonly FrozenDictionary<uint, (uint Reputation, uint Emblem)> tribes;
    private readonly FrozenDictionary<uint, uint> contentTypes;

    private NodeIconResolver(
        FrozenDictionary<uint, uint> genres,
        FrozenDictionary<uint, uint> expansions,
        FrozenDictionary<uint, (uint, uint)> tribes,
        FrozenDictionary<uint, uint> contentTypes)
    {
        this.genres = genres;
        this.expansions = expansions;
        this.tribes = tribes;
        this.contentTypes = contentTypes;
    }

    /// <summary>Reads the four sheets once.</summary>
    public static NodeIconResolver Build(ExcelModule excel)
    {
        ArgumentNullException.ThrowIfNull(excel);
        var genres = new Dictionary<uint, uint>();
        foreach (var row in excel.GetSheet<JournalGenre>())
        {
            if (row.Icon > 0)
            {
                genres[row.RowId] = (uint)row.Icon;
            }
        }

        var expansions = new Dictionary<uint, uint>();
        foreach (var row in excel.GetSheet<ExVersion>())
        {
            if (row.Icon != 0)
            {
                expansions[row.RowId] = row.Icon;
            }
        }

        var tribes = new Dictionary<uint, (uint, uint)>();
        foreach (var row in excel.GetSheet<BeastTribe>())
        {
            if (row.IconReputation != 0 || row.Icon != 0)
            {
                tribes[row.RowId] = (row.IconReputation, row.Icon);
            }
        }

        var contentTypes = new Dictionary<uint, uint>();
        foreach (var row in excel.GetSheet<ContentType>())
        {
            if (row.Icon != 0)
            {
                contentTypes[row.RowId] = row.Icon;
            }
        }

        return new NodeIconResolver(genres.ToFrozenDictionary(), expansions.ToFrozenDictionary(), tribes.ToFrozenDictionary(), contentTypes.ToFrozenDictionary());
    }

    /// <summary>Every tree node's icon for this catalog; call once per catalog (the tree pane's EnsureNodes).</summary>
    public NodeIconMap Resolve(QuestCatalog catalog) => NodeIcons.Resolve(catalog, this);

    public uint GenreIcon(uint genreId) => genres.GetValueOrDefault(genreId);

    public uint ExpansionIcon(byte expansion) => expansions.GetValueOrDefault(expansion);

    public uint TribeReputationIcon(byte tribe) => tribes.TryGetValue(tribe, out var t) ? t.Reputation : 0;

    public uint TribeIcon(byte tribe) => tribes.TryGetValue(tribe, out var t) ? t.Emblem : 0;

    public uint ContentTypeIcon(uint contentType) => contentTypes.GetValueOrDefault(contentType);
}
