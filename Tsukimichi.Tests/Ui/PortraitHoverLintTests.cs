using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// Source lint for hovering a portrait (spec A5, owner request 4 October 2026): every place that draws a portrait plate
/// also opens the larger portrait (<c>Chrome.PortraitTooltip</c>) when the plate is hovered, the "Who's in it" row
/// included. The test project references Core and GameData only, so it reads the plugin's sources from the repository.
/// </summary>
public sealed class PortraitHoverLintTests
{
    private static string UiFolder => Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Ui");

    [Fact]
    public void Every_file_that_draws_a_portrait_plate_opens_the_larger_portrait_on_hover()
    {
        var missing = Directory.EnumerateFiles(UiFolder, "*.cs")
            .Where(file => Path.GetFileName(file) != "Chrome.Portrait.cs")
            .Select(file => (Name: Path.GetFileName(file), Text: File.ReadAllText(file)))
            .Where(f => f.Text.Contains("Chrome.Portrait(", StringComparison.Ordinal)
                && !f.Text.Contains("Chrome.PortraitTooltip(", StringComparison.Ordinal))
            .Select(f => f.Name)
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void The_cast_row_opens_the_hovered_member_s_portrait()
    {
        var source = File.ReadAllText(Path.Combine(UiFolder, "DetailPane.Stories.cs"));
        Assert.Contains("Chrome.PortraitTooltip(line.Plates[hovered], line.PlateNames[hovered]", source, StringComparison.Ordinal);
    }
}
