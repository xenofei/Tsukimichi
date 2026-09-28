using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Config;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// Every pixel size the main window's panes use, derived once per frame from Dalamud's global scale and the user's
/// UI and icon scales (<see cref="Configuration.UiScale"/>, <see cref="Configuration.IconScale"/>, arithmetic in
/// <see cref="ScaleMetrics"/>). <see cref="Update"/> runs at the top of <c>MainWindow.Draw</c>; the values default
/// to plain global scale before that.
///
/// Font scale: ImGui multiplies a window's own font scale by its parent's, so <see cref="ApplyFontScale"/> belongs
/// only in windows whose parent is not already scaled: the main window itself, tooltips (no parent), popups opened
/// from a direct child of the main window, and the inner window a scrolling table creates inside such a child.
/// Direct children of the main window inherit it and must not call it again, or they would scale twice.
/// </summary>
public static class UiMetrics
{
    /// <summary>Pixels per logical unit for layout (widths, paddings): global scale × UI scale.</summary>
    public static float Scale { get; private set; } = 1f;

    /// <summary>The UI scale alone, what <see cref="ImGui.SetWindowFontScale"/> takes on top of Dalamud's fonts.</summary>
    public static float FontScale { get; private set; } = 1f;

    /// <summary>Pixels per logical unit for moons, icons and banners: <see cref="Scale"/> × icon scale.</summary>
    public static float IconScale { get; private set; } = 1f;

    /// <summary><see cref="Configuration.ReduceMotion"/> as of the last <see cref="Update"/>: animated gauges draw as text.</summary>
    public static bool ReduceMotion { get; private set; }

    /// <summary>Recomputes the factors from the live global scale and the settings; call once per frame before drawing.</summary>
    public static void Update(Configuration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var global = ImGuiHelpers.GlobalScale;
        FontScale = ScaleMetrics.ClampUiScale(settings.UiScale);
        Scale = ScaleMetrics.LayoutFactor(global, settings.UiScale);
        IconScale = ScaleMetrics.IconFactor(global, settings.UiScale, settings.IconScale);
        ReduceMotion = settings.ReduceMotion;
    }

    /// <summary>Applies the font scale to the current window (see the class remarks for where that is right).</summary>
    public static void ApplyFontScale() => ImGui.SetWindowFontScale(FontScale);

    /// <summary>A layout size in pixels.</summary>
    public static float Px(float logical) => logical * Scale;

    /// <summary>An icon or glyph size in pixels.</summary>
    public static float Icon(float logical) => logical * IconScale;

    // Moons.
    public static float RowGlyphRadius => Icon(6f);
    public static float TreeMoonRadius => Icon(5f);
    public static float HeaderMoonRadius => Icon(17f);
    public static float PathGlyphRadius => Icon(6f);
    public static float RequirementMoonRadius => Icon(4.5f);
    public static float StatusMoonRadius => Icon(4.5f);
    public static float EmptyStateMoonRadius => Icon(28f);

    /// <summary>Square reserved for an inline state moon so its radius is <see cref="RowGlyphRadius"/>, never shorter than the line.</summary>
    public static float InlineGlyphSize(float lineHeight) => MathF.Max(lineHeight, RowGlyphRadius / MoonGlyph.InlineRadiusFraction);

    // Icons.
    public static float RowIconSize => Icon(14f);
    public static float DetailIconSize => Icon(17f);
    public static float JobIconSize => Icon(17f);
    public static float TooltipIconSize => Icon(56f);
    public static float BannerTooltipWidth => Px(240f);
    public static float BannerBadgeSize => Icon(22f);
    public static float BannerMaxHeight => Px(200f);

    /// <summary>Content height of a table row holding a moon, an icon and a line of text.</summary>
    public static float RowContentHeight(float lineHeight) => MathF.Max(lineHeight, MathF.Max(RowIconSize, RowGlyphRadius * 2.4f));

    // Layout.
    public static float ChipHeight => ImGui.GetFrameHeight();
    public static float LeftColumnWidth => Px(240f);
    public static float RightColumnWidth => Px(360f);
    public static float SearchWidth => Px(280f);
    public static float CharacterComboWidth => Px(240f);
    public static float MinChipStripWidth => Px(40f);
    public static float MinBodyHeight => Px(120f);
    public static float Stripe => MathF.Max(1f, Px(2f));
    public static float Hairline => MathF.Max(1f, Px(1f));

    /// <summary>A plain text tooltip drawn with the window's font scale (SetTooltip cannot be scaled).</summary>
    public static void Tooltip(string text)
    {
        using var tooltip = ImRaii.Tooltip();
        ApplyFontScale();
        ImGui.TextUnformatted(text);
    }

    /// <summary>Square size vector helper.</summary>
    public static Vector2 Square(float size) => new(size, size);
}
