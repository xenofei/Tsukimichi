using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The frozen catalog under <c>Tsukimichi.Tests/Fixtures/catalog-&lt;gameVersion&gt;.json.gz</c>, loaded once per test
/// class. It runs everywhere, game files or not; <see cref="GameDataFixture"/> is the live counterpart and
/// <c>Fixture_matches_live_sheets</c> keeps the two in step. The file holds the sheet's own filing;
/// <see cref="Bundle"/> is the refiled catalog the plugin builds (the <see cref="JournalRefiler"/> over the shipped
/// curated files) and <see cref="LegacyBundle"/> the file as written. Regenerate with
/// <c>dotnet run --project Tsukimichi.DataGen -- --dump-catalog Tsukimichi.Tests/Fixtures --game "&lt;sqpack&gt;"</c>.
/// </summary>
public sealed class FixtureCatalog
{
    public const string RegenerateHint =
        "regenerate it: dotnet run --project Tsukimichi.DataGen -c Release -- --dump-catalog Tsukimichi.Tests/Fixtures --game \"<sqpack path>\" (delete the old catalog-*.json.gz)";

    private readonly Lazy<(CatalogBundle Bundle, string GameVersion, string Path)> loaded =
        new(() => Load(JournalFiling.Refiled), LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly Lazy<(CatalogBundle Bundle, string GameVersion, string Path)> legacy =
        new(() => Load(JournalFiling.Legacy), LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly Lazy<CuratedData> curated = new(() => CuratedData.Load(CuratedDir()), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The fixture files as copied next to the test assembly.</summary>
    public static string FixturesDir => System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures");

    /// <summary>The one committed catalog dump; more than one means a stale file was left behind.</summary>
    public static string CatalogPath()
    {
        var files = Directory.GetFiles(FixturesDir, CatalogFixtureFile.FilePrefix + "*" + CatalogFixtureFile.FileSuffix);
        Assert.True(files.Length == 1, $"expected exactly one catalog fixture under {FixturesDir}, found {files.Length}; {RegenerateHint}");
        return files[0];
    }

    /// <summary>The plugin's shipped data directory (<c>Tsukimichi/Data</c>), found by walking up to the solution file.</summary>
    public static string ShippedDataDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(System.IO.Path.Combine(dir, "Tsukimichi.sln")))
        {
            dir = System.IO.Path.GetDirectoryName(dir);
        }

        Assert.NotNull(dir);
        return System.IO.Path.Combine(dir, "Tsukimichi", "Data");
    }

    /// <summary>The shipped curated directory (<c>Tsukimichi/Data/curated</c>).</summary>
    public static string CuratedDir() => System.IO.Path.Combine(ShippedDataDir(), "curated");

    /// <summary>The catalog as the plugin builds it: refiled with the shipped curated overlay.</summary>
    public CatalogBundle Bundle => loaded.Value.Bundle;

    /// <summary>The catalog under <see cref="JournalFiling.Legacy"/>: the sheet's own genres, no refiler.</summary>
    public CatalogBundle LegacyBundle => legacy.Value.Bundle;

    /// <summary>The shipped curated overlay, loaded once.</summary>
    public CuratedData Curated => curated.Value;

    public string GameVersion => loaded.Value.GameVersion;

    public string Path => loaded.Value.Path;

    private static (CatalogBundle, string, string) Load(JournalFiling filing)
    {
        var path = CatalogPath();
        var (bundle, gameVersion) = CatalogFixtureFile.Read(path, filing, CuratedData.Load(CuratedDir()));
        return (bundle, gameVersion, path);
    }
}
