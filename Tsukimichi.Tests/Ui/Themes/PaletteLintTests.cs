using System.Text.RegularExpressions;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui.Themes;

/// <summary>
/// Source lint for the palette (plan v7 T2, theme-system §8.4): the chrome draws with the palette, so a light palette
/// works everywhere. Outside glyph code no file reads the fixed glyph tokens (<c>Theme.Moon</c>, <c>Silver</c>,
/// <c>Dusk</c> …: the chrome uses <c>Theme.Accent</c>, <c>Gold</c>, <c>Surface.*</c> and friends), and no file writes a
/// colour literal (<c>FromHex(0x…)</c>, <c>Rgb(0x…)</c>, an opaque <c>new Vector4(r, g, b, …)</c>) except the few
/// listed below with the reason each stays fixed. The test project references Core and GameData only, so it reads the
/// plugin's sources from the repository.
/// </summary>
public sealed class PaletteLintTests
{
    // A fixed glyph token read through Theme: the 1.15 names, packed or not.
    private static readonly Regex FixedToken = new(
        @"\bTheme\.(Night|Moon|Silver|Dusk|Eclipse|Veil|Shadow|VeilLine|UnlitDisc|Umbra|MoonHigh|MoonDeep|SilverHigh|SilverDeep|MoonDim|NightRaised|NightSunken|NightHover|NightLine|Mist|VeilText|EclipseText|Abyss|NightTop|Gilt|GiltHigh|Tide|TideDeep)(U32)?\b",
        RegexOptions.Compiled);

    // A colour literal: a 0xRRGGBB through FromHex or Rgb, or a Vector4 built from three float channels.
    private static readonly Regex ColorLiteral = new(
        @"\b(FromHex|Rgb)\(\s*0x[0-9A-Fa-f]{6}|new\s+Vector4\(\s*[0-9.]+f\s*,\s*[0-9.]+f\s*,\s*[0-9.]+f",
        RegexOptions.Compiled);

    /// <summary>Glyph code, which draws the moons in their own fixed colours (the palette never recolours a glyph).</summary>
    private static readonly string[] GlyphFiles = ["Theme.cs", "LegacyMoonGlyph.cs", "GlyphDebugWindow.cs", "MoonGlyph.cs", "MedalGlyph.cs", "MedalGauge.cs", "MedalAtlas.cs"];

    /// <summary>The colour literals that stay fixed, and why.</summary>
    private static readonly Dictionary<string, string> FixedLiterals = new(StringComparer.Ordinal)
    {
        ["Theme.cs"] = "the fixed glyph tokens themselves (Umbra, the Classic moons' maria)",
        ["LegacyMoonGlyph.cs"] = "the Classic moons' art",
        ["GlyphDebugWindow.cs"] = "the glyph window's test grounds (white, Dalamud grey)",
        ["Chrome.ActionPill.cs"] = "the tint that takes a disabled game icon's colour out: a picture tint, not chrome",
    };

    private static IEnumerable<string> UiSources() =>
        Directory.GetFiles(Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi", "Ui"), "*.cs", SearchOption.AllDirectories)
            .Where(static f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(static f => f, StringComparer.Ordinal);

    /// <summary>Glyph code by name or place: the glyph files, and the glyph sets and frame kits under Ui/Themes.</summary>
    private static bool IsGlyphCode(string path) =>
        GlyphFiles.Contains(Path.GetFileName(path), StringComparer.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}Themes{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    [Fact]
    public void The_sources_are_found()
    {
        Assert.Contains(UiSources(), static f => f.EndsWith("TablePane.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Chrome_reads_the_palette_not_the_fixed_glyph_tokens()
    {
        var offenders = new List<string>();
        foreach (var path in UiSources().Where(static p => !IsGlyphCode(p)))
        {
            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                if (FixedToken.Match(lines[i]) is { Success: true } m)
                {
                    offenders.Add($"{Path.GetFileName(path)}:{i + 1}: {m.Value} (use the palette: Theme.Accent, Gold, Surface.*, Danger…)");
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void No_colour_literals_outside_the_listed_fixed_ones()
    {
        var offenders = new List<string>();
        foreach (var path in UiSources())
        {
            if (FixedLiterals.ContainsKey(Path.GetFileName(path)))
            {
                continue;
            }

            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                if (ColorLiteral.Match(lines[i]) is { Success: true } m)
                {
                    offenders.Add($"{Path.GetFileName(path)}:{i + 1}: {m.Value} (add a palette role in Core/Ui/Themes)");
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void The_lint_catches_a_fixed_token_and_a_literal()
    {
        Assert.Matches(FixedToken, "using (Theme.PushText(Theme.Moon))");
        Assert.Matches(FixedToken, "dl.AddText(pos, Theme.DuskU32, text);");
        Assert.DoesNotMatch(FixedToken, "Theme.MoonStyle == MoonStyle.Classic");
        Assert.DoesNotMatch(FixedToken, "Theme.Surface.TextTertiary");
        Assert.Matches(ColorLiteral, "private static readonly Vector4 X = Core.Ui.ColorMath.FromHex(0x1A1406);");
        Assert.Matches(ColorLiteral, "var c = new Vector4(0.5f, 0.5f, 0.5f, 1f);");
        Assert.DoesNotMatch(ColorLiteral, "dl.AddRect(min, max, 0xFFFFFFFFu, rounding);");
        Assert.DoesNotMatch(ColorLiteral, "var v = new Vector4(MathF.Min(1f, top.X * lift), 1f, 1f, 1f);");
    }
}
