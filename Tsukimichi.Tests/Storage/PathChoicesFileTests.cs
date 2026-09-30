using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Storage;

/// <summary>Loading <c>curated/path_choices.json</c> (feature plan v4 D1): the three sections, file order, and each required field.</summary>
public sealed class PathChoicesFileTests : IDisposable
{
    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    private CuratedData Load(string json)
    {
        var dir = tmp.File("curated");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, CuratedData.PathChoicesFileName), json);
        return CuratedData.Load(dir);
    }

    [Fact]
    public void Cities_classes_and_company_tags_load_in_file_order()
    {
        var data = Load(
            """
            {
              "schema": 1,
              "note": "header",
              "cities": {
                "65575": { "label": "Gridania", "note": "a" },
                "$comment": { "label": "ignored" },
                "65643": { "label": "Limsa Lominsa", "note": "b" }
              },
              "classes": {
                "4": { "label": "Lancer", "closeToHome": 65621, "starter": "65559", "note": "c" }
              },
              "grandCompanies": {
                "66216": { "grandCompany": 2, "note": "d" }
              }
            }
            """);

        Assert.Empty(data.Warnings);
        Assert.Equal([65575u, 65643u], data.PathChoices.Cities.Select(c => c.Root));
        Assert.Equal(["Gridania", "Limsa Lominsa"], data.PathChoices.Cities.Select(c => c.Label));
        var lancer = Assert.Single(data.PathChoices.Classes);
        Assert.Equal(new ClassPin(4, "Lancer", 65621, 65559, "c"), lancer);
        Assert.Equal(2, data.PathChoices.GrandCompanies[66216].GrandCompany);
    }

    [Fact]
    public void An_entry_missing_its_note_or_an_id_is_skipped_with_a_warning()
    {
        var data = Load(
            """
            {
              "cities": { "65575": { "label": "Gridania" }, "x": { "label": "Nowhere", "note": "n" } },
              "classes": { "4": { "label": "Lancer", "starter": 65559, "note": "n" }, "0": { "label": "None", "closeToHome": 1, "starter": 1, "note": "n" } },
              "grandCompanies": { "66216": { "grandCompany": 4, "note": "n" } }
            }
            """);

        Assert.True(data.PathChoices.IsEmpty);
        Assert.Equal(5, data.Warnings.Count);
        Assert.Contains(data.Warnings, w => w.Contains("note is missing", StringComparison.Ordinal));
        Assert.Contains(data.Warnings, w => w.Contains("closeToHome is not a quest row id", StringComparison.Ordinal));
        Assert.Contains(data.Warnings, w => w.Contains("grandCompany is not 1, 2 or 3", StringComparison.Ordinal));
    }

    [Fact]
    public void A_missing_file_leaves_the_choices_empty()
    {
        var dir = tmp.File("empty");
        Directory.CreateDirectory(dir);
        Assert.True(CuratedData.Load(dir).PathChoices.IsEmpty);
    }
}
