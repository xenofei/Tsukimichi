using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Unlocks;

namespace Tsukimichi.GameData;

/// <summary>
/// Reads <see cref="DutyArt"/>'s fields from the client's sheets: every Duty Finder entry's icon through the chain
/// (<see cref="DutyIcons"/>), and the name of a nameless one from its territory. Every reader of the
/// ContentFinderCondition sheet that names or draws a duty goes through here (the unlock rows, Moonlit's art, the quest
/// rewards, the required duties, AutoDuty's duty list), so they agree. Standalone (takes an <see cref="ExcelModule"/>)
/// so tests read it against game data without Dalamud.
/// </summary>
public static class DutyArtReader
{
    /// <summary><c>ContentFinderCondition.ContentLinkType</c> value whose <c>Content</c> is an InstanceContent row.</summary>
    public const byte InstanceContentLink = 1;

    /// <summary>
    /// Every Duty Finder entry's icon, named or not, by ContentFinderCondition id and by InstanceContent id (the first
    /// entry per instance), with the Duty Finder menu icon as the fallback. A sheet that cannot be read gives
    /// <see cref="DutyIcons.Empty"/> and one line to <paramref name="log"/>.
    /// </summary>
    public static DutyIcons Read(ExcelModule excel, Language language, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(excel);
        try
        {
            var shared = Shared.Read(excel, language);
            var byCondition = new Dictionary<uint, uint>();
            var byInstance = new Dictionary<uint, uint>();
            foreach (var row in excel.GetSheet<ContentFinderCondition>(language))
            {
                var icon = Icon(in row, in shared);
                if (icon == 0)
                {
                    continue;
                }

                byCondition[row.RowId] = icon;
                if (row.ContentLinkType == InstanceContentLink && row.Content.RowId != 0)
                {
                    byInstance.TryAdd(row.Content.RowId, icon);
                }
            }

            return new DutyIcons(byCondition, byInstance, shared.DutyFinderIcon);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log?.Invoke($"The duty icons could not be read ({ex.GetType().Name}: {ex.Message}); duties show their stand-in");
            return DutyIcons.Empty;
        }
    }

    /// <summary>The sheet fields <see cref="DutyArt.Resolve"/> takes for one Duty Finder entry.</summary>
    public static DutyArtSources Sources(in ContentFinderCondition row)
    {
        var type = row.ContentType.RowId != 0 ? row.ContentType.ValueNullable : null;
        var genre = row.JournalGenre.RowId != 0 ? row.JournalGenre.ValueNullable : null;
        return new DutyArtSources(
            row.Icon,
            type?.Icon ?? 0u,
            type?.IconDutyFinder ?? 0u,
            genre is { Icon: > 0 } g ? (uint)g.Icon : 0u,
            IsPvpInstance(in row));
    }

    /// <summary>The entry's icon through the chain; 0 only when not even the Duty Finder menu icon was read.</summary>
    public static uint Icon(in ContentFinderCondition row, in Shared shared) => DutyArt.Icon(Sources(in row), shared.PvpIcon, shared.DutyFinderIcon);

    /// <summary>The entry's name (<see cref="DutyArt.Name"/>): its own, else its territory's, else PvP's for a PvP instance.</summary>
    public static string Name(in ContentFinderCondition row, in Shared shared) =>
        DutyArt.Name(
            row.Name.ExtractText(),
            row.TerritoryType.RowId != 0 ? row.TerritoryType.ValueNullable?.PlaceName.ValueNullable?.Name.ExtractText() : null,
            IsPvpInstance(in row),
            shared.PvpName);

    /// <summary>Whether the entry links an InstanceContent row of the PvP instance type.</summary>
    public static bool IsPvpInstance(in ContentFinderCondition row) =>
        row.ContentLinkType == InstanceContentLink && row.Content.RowId != 0
        && row.Content.GetValueOrDefault<InstanceContent>() is { } instance
        && instance.InstanceContentType.RowId == DutyArt.PvpInstanceContentType;

    /// <summary>What every entry's chain shares: PvP's tile and name, and the Duty Finder menu icon.</summary>
    public readonly record struct Shared(uint PvpIcon, string PvpName, uint DutyFinderIcon)
    {
        /// <summary>Reads them; a row the sheet lacks reads as 0 or empty.</summary>
        public static Shared Read(ExcelModule excel, Language language)
        {
            ArgumentNullException.ThrowIfNull(excel);
            var pvp = excel.GetSheet<ContentType>(language).GetRowOrDefault(DutyArt.PvpContentType);
            var menu = excel.GetSheet<MainCommand>(language).GetRowOrDefault(DutyArt.DutyFinderMenu);
            return new Shared(pvp?.Icon ?? 0u, pvp?.Name.ExtractText() ?? string.Empty, menu is { Icon: > 0 } m ? (uint)m.Icon : 0u);
        }
    }
}
