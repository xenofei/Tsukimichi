using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;
using Tsukimichi.GameData;

namespace Tsukimichi.Verify.Game;

/// <summary>Sheet facts the verifier compares that <see cref="QuestRecord"/> does not carry: names for the ids, the issuer's zone, the starting class.</summary>
internal sealed record QuestExtras(
    string PlaceName,
    string ClassJobRequiredName,
    string ClassJobRequiredAbbreviation,
    string ClassJobCategoryName,
    string GrandCompanyName,
    IReadOnlyList<string> InstanceContentNames,
    string ExpansionName,
    uint SystemRewardUnlock,
    uint SatisfactionNpc,
    uint DeliveryQuest);

/// <summary>
/// Opens the local install through Lumina, maps the catalog with the plugin's own <see cref="CatalogMapper"/> (so the
/// tool verifies exactly what the plugin shows) and reads the handful of extra columns the sources can be compared with.
/// </summary>
internal sealed class GameCatalog
{
    public string GameVersion { get; }
    public CatalogBundle Bundle { get; }
    public QuestCatalog Catalog => Bundle.Catalog;
    public IReadOnlyDictionary<uint, QuestExtras> Extras { get; }

    /// <summary>ContentFinderCondition names by CFC row id, for the curated duty unlocks.</summary>
    public IReadOnlyDictionary<uint, string> ContentFinderConditionNames { get; }

    /// <summary>Journal sections (id, name) that the Lodestone lists (255 is the unlisted placeholder).</summary>
    public IReadOnlyDictionary<uint, string> SectionNames { get; }
    public IReadOnlyDictionary<uint, string> CategoryNames { get; }

    /// <summary>JournalCategory id → JournalSection id.</summary>
    public IReadOnlyDictionary<uint, uint> CategorySection { get; }

    /// <param name="curated">The curated overlay the plugin loads (refile overrides, retired quests), so the refiled catalog is the one players see.</param>
    public GameCatalog(string sqpackPath, Tsukimichi.Core.Storage.CuratedData curated, TextWriter log)
    {
        var data = new Lumina.GameData(sqpackPath, new LuminaOptions { PanicOnSheetChecksumMismatch = false });
        GameVersion = ReadGameVersion(sqpackPath);
        Curated = curated;
        Bundle = CatalogMapper.Map(data.Excel, Language.English, default, line => log.WriteLine("catalog: " + line), JournalFiling.Refiled, curated);

        // The sheet's own filing: the Lodestone and the wiki file a quest by the journal genre the game gives it, not by
        // the plugin's refiling, so those comparisons read this record.
        var legacy = CatalogMapper.Map(data.Excel, Language.English, default, null, JournalFiling.Legacy);
        SheetRecords = legacy.Catalog.All.ToDictionary(q => q.RowId);

        var quests = data.GetExcelSheet<Quest>(Language.English) ?? throw new InvalidOperationException("Quest sheet missing");
        var categories = data.GetExcelSheet<ClassJobCategory>(Language.English) ?? throw new InvalidOperationException("ClassJobCategory sheet missing");
        var cfcs = data.GetExcelSheet<ContentFinderCondition>(Language.English) ?? throw new InvalidOperationException("ContentFinderCondition sheet missing");
        var sections = data.GetExcelSheet<JournalSection>(Language.English) ?? throw new InvalidOperationException("JournalSection sheet missing");
        var journalCategories = data.GetExcelSheet<JournalCategory>(Language.English) ?? throw new InvalidOperationException("JournalCategory sheet missing");

        var categoryNames = new Dictionary<uint, string>();
        foreach (var c in categories)
        {
            categoryNames[c.RowId] = c.Name.ExtractText();
        }

        var cfcNames = new Dictionary<uint, string>();
        foreach (var c in cfcs)
        {
            var name = c.Name.ExtractText();
            if (name.Length > 0)
            {
                cfcNames[c.RowId] = name;
            }
        }

        ContentFinderConditionNames = cfcNames;

        var sectionNames = new Dictionary<uint, string>();
        foreach (var s in sections)
        {
            sectionNames[s.RowId] = s.Name.ExtractText();
        }

        SectionNames = sectionNames;

        var jcNames = new Dictionary<uint, string>();
        var jcSection = new Dictionary<uint, uint>();
        foreach (var c in journalCategories)
        {
            jcNames[c.RowId] = c.Name.ExtractText();
            jcSection[c.RowId] = c.JournalSection.RowId;
        }

        CategoryNames = jcNames;
        CategorySection = jcSection;

        var extras = new Dictionary<uint, QuestExtras>(Catalog.Count);
        foreach (var record in Catalog.All)
        {
            if (quests.GetRowOrDefault(record.RowId) is not { } quest)
            {
                continue;
            }

            var instanceNames = new List<string>();
            foreach (var ic in quest.InstanceContent)
            {
                if (ic.RowId == 0)
                {
                    continue;
                }

                var name = ic.ValueNullable?.ContentFinderCondition.ValueNullable?.Name.ExtractText() ?? string.Empty;
                instanceNames.Add(name.Length > 0 ? name : $"InstanceContent {ic.RowId}");
            }

            extras[record.RowId] = new QuestExtras(
                quest.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty,
                quest.ClassJobRequired.RowId != 0 ? NameCase.Title(quest.ClassJobRequired.ValueNullable?.Name.ExtractText() ?? string.Empty) : string.Empty,
                quest.ClassJobRequired.RowId != 0 ? Bundle.Names.ClassJobAbbreviation(quest.ClassJobRequired.RowId) : string.Empty,
                categoryNames.GetValueOrDefault(quest.ClassJobCategory0.RowId, string.Empty),
                quest.GrandCompany.RowId != 0 ? Bundle.Names.GrandCompany(quest.GrandCompany.RowId) : string.Empty,
                instanceNames,
                Bundle.Names.Expansion(quest.Expansion.RowId),
                quest.SystemReward.Count > 1 ? quest.SystemReward[1] : 0u,
                quest.SatisfactionNpc.RowId,
                quest.DeliveryQuest.RowId);
        }

        Extras = extras;
    }

    /// <summary>The curated overlay the catalog was mapped with (quirk notes, retired quests, online store marks).</summary>
    public Tsukimichi.Core.Storage.CuratedData Curated { get; }

    /// <summary>Records as the sheet files them (<see cref="JournalFiling.Legacy"/>), by row id.</summary>
    public IReadOnlyDictionary<uint, QuestRecord> SheetRecords { get; }

    /// <summary>The quest as the sheet files it: its own journal genre, before the refiler.</summary>
    public QuestRecord SheetRecord(QuestRecord q) => SheetRecords.TryGetValue(q.RowId, out var s) ? s : q;

    public QuestExtras ExtrasOf(uint rowId) => Extras.TryGetValue(rowId, out var e) ? e : new QuestExtras(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, [], string.Empty, 0, 0, 0);

    /// <summary>Display level as feature plan T2 defines it.</summary>
    public static int DisplayLevel(QuestRecord q) => q.Level + q.LevelOffset;

    /// <summary>The game directory holds ffxivgame.ver one level above sqpack.</summary>
    public static string ReadGameVersion(string sqpackPath)
    {
        var full = Path.GetFullPath(sqpackPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var gameDir = Path.GetDirectoryName(full) ?? full;
        var verFile = Path.Combine(gameDir, "ffxivgame.ver");
        if (!File.Exists(verFile))
        {
            return "unknown";
        }

        var line = File.ReadLines(verFile).FirstOrDefault()?.Trim();
        return string.IsNullOrEmpty(line) ? "unknown" : line;
    }
}
