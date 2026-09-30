using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tsukimichi.Core.Model;

namespace Tsukimichi.GameData;

/// <summary>
/// The mapped catalog frozen to a file: every <see cref="QuestRecord"/>, the name tables and the ClassJobCategory
/// membership, stamped with the game version the sheets came from. <c>Tsukimichi.DataGen --dump-catalog</c> writes it
/// and the tests read it, so record-only tests run without the game files. The records are the sheet's own filing
/// (<see cref="JournalFiling.Legacy"/>); <see cref="CatalogFixtureFile.Read(string, JournalFiling, Core.Storage.CuratedData)"/>
/// runs the refiler on the way in, so the frozen data serves both filings and the refiler's own tests. Test content;
/// never shipped in the plugin.
/// </summary>
public sealed record CatalogFixtureData(
    string GameVersion,
    string Language,
    IReadOnlyList<QuestRecord> Quests,
    GameNames Names,
    IReadOnlyDictionary<uint, byte[]> JobCategories,
    int JobColumns);

/// <summary>Reads and writes <see cref="CatalogFixtureData"/> as gzipped JSON (<c>catalog-&lt;gameVersion&gt;.json.gz</c>).</summary>
public static class CatalogFixtureFile
{
    public const string FilePrefix = "catalog-";
    public const string FileSuffix = ".json.gz";

    /// <summary>Compact JSON with enums as names; the same options serve the write, the read and record comparison.</summary>
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false,
    };

    /// <summary>The conventional file name for a game version, e.g. <c>catalog-2026.09.15.0000.0000.json.gz</c>.</summary>
    public static string FileName(string gameVersion) => FilePrefix + gameVersion + FileSuffix;

    public static void Write(string path, CatalogBundle bundle, string gameVersion)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var data = new CatalogFixtureData(
            gameVersion,
            bundle.Language,
            bundle.Catalog.All,
            bundle.Names,
            bundle.Jobs.ToMembership(),
            bundle.Jobs.JobColumns);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var file = File.Create(path);
        using var gzip = new GZipStream(file, CompressionLevel.SmallestSize);
        JsonSerializer.Serialize(gzip, data, Options);
    }

    public static CatalogFixtureData ReadData(string path)
    {
        using var file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        return JsonSerializer.Deserialize<CatalogFixtureData>(gzip, Options)
               ?? throw new InvalidDataException($"{path} holds no catalog.");
    }

    /// <summary>Loads the file as written: the sheet's own filing, no refiler.</summary>
    public static (CatalogBundle Bundle, string GameVersion) Read(string path) => Read(path, JournalFiling.Legacy, Core.Storage.CuratedData.Empty);

    /// <summary>
    /// Loads the file and rebuilds the bundle the mapper would have produced under <paramref name="filing"/> with
    /// <paramref name="curated"/> and, when given, <paramref name="patches"/> (the file holds the sheet's own data, so
    /// <see cref="QuestRecord.AddedIn"/> is empty in it and laid over here, as the plugin's catalog build does).
    /// </summary>
    public static (CatalogBundle Bundle, string GameVersion) Read(string path, JournalFiling filing, Core.Storage.CuratedData curated, Core.Storage.QuestPatches? patches = null)
    {
        ArgumentNullException.ThrowIfNull(curated);
        var data = ReadData(path);
        var jobs = ClassJobCategoryLookup.FromMembership(
            data.JobCategories.Select(kv => new KeyValuePair<uint, IEnumerable<byte>>(kv.Key, kv.Value)),
            data.JobColumns);
        var dated = patches is null ? data.Quests : patches.Apply(data.Quests);
        var quests = filing == JournalFiling.Refiled ? JournalRefiler.Apply(dated, curated) : dated;
        var catalog = QuestCatalog.Build(quests);
        Core.Evaluation.PathIndex.Attach(catalog, curated.PathChoices);
        var bundle = new CatalogBundle(catalog, data.Names, jobs, data.Language);
        return (bundle, data.GameVersion);
    }

    /// <summary>One record as the file would hold it; equal strings mean equal records, arrays included.</summary>
    public static string ToJson(QuestRecord quest) => JsonSerializer.Serialize(quest, Options);
}
