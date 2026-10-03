using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Unlocks;

namespace Tsukimichi.GameData;

/// <summary>
/// Reads the icons of <see cref="FeatureArt"/>'s rows (<c>MainCommand.Icon</c>, <c>ContentType.Icon</c>,
/// <c>Item.Icon</c>) from the client's own sheets, so a patch that redraws one needs no data run. Each sheet is read on
/// its own: one that cannot be read leaves only its rows out, and is logged. Standalone (takes an
/// <see cref="ExcelModule"/>) so tests read it against game data without Dalamud.
/// </summary>
public static class FeatureIconReader
{
    public static FeatureIcons Read(ExcelModule excel, Language language = Language.None, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(excel);
        var icons = new Dictionary<FeatureArtRef, uint>();
        var wanted = FeatureArt.All().ToList();

        void Part(FeatureArtSheet sheet, Func<uint, uint> read)
        {
            try
            {
                foreach (var art in wanted)
                {
                    if (art.Sheet == sheet)
                    {
                        icons[art] = read(art.Row);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log?.Invoke($"The {sheet} icons of the feature unlocks could not be read ({ex.GetType().Name}: {ex.Message}); those rows keep their stand-in");
            }
        }

        Part(FeatureArtSheet.MainCommand, row => excel.GetSheet<MainCommand>(language).GetRowOrDefault(row) is { Icon: > 0 } command ? (uint)command.Icon : 0u);
        Part(FeatureArtSheet.ContentType, row => excel.GetSheet<ContentType>(language).GetRowOrDefault(row)?.Icon ?? 0u);
        Part(FeatureArtSheet.Item, row => excel.GetSheet<Item>(language).GetRowOrDefault(row)?.Icon ?? 0u);
        return new FeatureIcons(icons);
    }
}
