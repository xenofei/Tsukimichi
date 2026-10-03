using System.Numerics;
using System.Text.Json;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui.Themes;

/// <summary>
/// The approved palette designs as the tests read them: each role's hex from a design record in the repository
/// (<c>docs/design/v7/ui/1.16/palettes.json</c>, <c>1.17/palettes17.json</c>), the palette colour each role name
/// stands for, and the colour-vision distance research §8.2 gates gold against Locked out with.
/// </summary>
internal static class PaletteDesign
{
    /// <summary>The roles of palette <paramref name="key"/> in the design record at <paramref name="relativePath"/> (under docs/design/v7/ui).</summary>
    public static Dictionary<string, uint> Read(string relativePath, string key)
    {
        var path = Path.Combine([ResxFiles.RepositoryRoot(), "docs", "design", "v7", "ui", .. relativePath.Split('/')]);
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var roles = new Dictionary<string, uint>(StringComparer.Ordinal);
        foreach (var role in json.RootElement.GetProperty("palettes").GetProperty(key).EnumerateObject())
        {
            roles[role.Name] = Convert.ToUInt32(role.Value.GetString()!.TrimStart('#'), 16);
        }

        return roles;
    }

    /// <summary>The palette's colour for a role name in a design record; null for a name no palette role stands for.</summary>
    public static Vector4? Role(UiPalette p, string role)
    {
        var s = p.Surface;
        return role switch
        {
            "Window" => s.Window,
            "Sunken" => s.Sunken,
            "Raised" => s.Raised,
            "Hover" => s.Hover,
            "Line" => s.Line,
            "StrongLine" => s.StrongLine,
            "Text" => s.Text,
            "TextSecondary" => s.TextSecondary,
            "TextTertiary" => s.TextTertiary,
            "TextDisabled" => s.TextDisabled,
            "Deep" => s.Deep,
            "Top" => s.Top,
            "Zenith" => p.Scene.Zenith,
            "Ornament" => s.Ornament,
            "OrnamentHigh" => s.OrnamentHigh,
            "OrnamentLight" => p.OrnamentLight,
            "Cool" => s.Cool,
            "Accent" => p.Accent,
            "Ready" => p.States.Text(QuestState.Ready),
            "InJournal" => p.States.Text(QuestState.Accepted),
            "Completed" => p.States.Text(QuestState.Completed),
            "ReadyOnOtherJob" => p.States.Text(QuestState.ReadyOnOtherJob),
            "DoneThisCycle" => p.States.Text(QuestState.DoneThisCycle),
            "Blocked" => p.States.Text(QuestState.Blocked),
            "LockedOut" => p.States.Text(QuestState.Foreclosed),
            "NotChecked" => p.States.Text(QuestState.Unknown),
            "GaugeArc" => p.Gauges.Arc,
            "GaugeArcShade" => p.Gauges.OuterSlope[^1].Color,
            "Groove" => p.Gauges.Groove,
            "StripeGold" => p.States.Stripe(QuestState.Ready),
            "StripeCompleted" => p.States.Stripe(QuestState.Completed),
            "StripeSilver" => p.States.Stripe(QuestState.ReadyOnOtherJob),
            "StripeLocked" => p.States.Stripe(QuestState.Foreclosed),
            "StripeVeil" => p.States.Stripe(QuestState.Unknown),

            // The sky's horizon band: its brightest stop (a high-contrast form has no sky).
            "Horizon" when p.Scene.SkyStops is { Length: > 0 } stops => stops.MaxBy(static stop => ColorMath.Luminance(stop.Color)).Color,
            _ => null,
        };
    }

    /// <summary>
    /// Research §8.2's colour-vision check: the worst OKLab distance between <paramref name="a"/> and
    /// <paramref name="b"/> as Machado's protanopia, deuteranopia and tritanopia see them.
    /// </summary>
    public static float WorstColourVisionDistance(Vector4 a, Vector4 b) =>
        new[] { ColorVision.Protanopia, ColorVision.Deuteranopia, ColorVision.Tritanopia }
            .Min(mode => Vector3.Distance(Oklab(ColorVisionSimulation.Simulate(a, mode)), Oklab(ColorVisionSimulation.Simulate(b, mode))));

    /// <summary>An sRGB colour in OKLab (Björn Ottosson's matrices).</summary>
    public static Vector3 Oklab(Vector4 srgb)
    {
        var r = ColorVisionSimulation.ToLinear(srgb.X);
        var g = ColorVisionSimulation.ToLinear(srgb.Y);
        var b = ColorVisionSimulation.ToLinear(srgb.Z);
        var l = MathF.Cbrt((0.4122214708f * r) + (0.5363325363f * g) + (0.0514459929f * b));
        var m = MathF.Cbrt((0.2119034982f * r) + (0.6806995451f * g) + (0.1073969566f * b));
        var s = MathF.Cbrt((0.0883024619f * r) + (0.2817188376f * g) + (0.6299787005f * b));
        return new Vector3(
            (0.2104542553f * l) + (0.7936177850f * m) - (0.0040720468f * s),
            (1.9779984951f * l) - (2.4285922050f * m) + (0.4505937099f * s),
            (0.0259040371f * l) + (0.7827717662f * m) - (0.8086757660f * s));
    }
}
