using System.Globalization;
using System.Text.Json;
using Tsukimichi.Core.Ui;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// Text widths in Dalamud's default UI font, from the advance table in <c>Fixtures/font-advances.json</c> (written by
/// <c>tools/font-advances.py</c>, the same table <see cref="LayoutBudgetTests"/> measures with): one em for a CJK
/// character the table lacks, 0.6 em for anything else it lacks.
/// </summary>
internal static class FontAdvances
{
    private static readonly Lazy<Dictionary<int, float>> Advances = new(Load);

    /// <summary>A <see cref="MeasureText"/> at <paramref name="px"/> pixels per em.</summary>
    public static MeasureText At(float px) => text => Width(text, px);

    /// <summary>The advance width of <paramref name="text"/> at <paramref name="px"/> pixels per em.</summary>
    public static float Width(ReadOnlySpan<char> text, float px)
    {
        var em = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            em += Advances.Value.TryGetValue(rune.Value, out var advance) ? advance : rune.Value >= 0x2E80 ? 1f : 0.6f;
        }

        return em * px;
    }

    private static Dictionary<int, float> Load()
    {
        var path = Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi.Tests", "Fixtures", "font-advances.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var result = new Dictionary<int, float>();
        foreach (var entry in doc.RootElement.GetProperty("advances").EnumerateObject())
        {
            result[int.Parse(entry.Name, NumberStyles.HexNumber, CultureInfo.InvariantCulture)] = entry.Value.GetSingle();
        }

        return result;
    }
}
