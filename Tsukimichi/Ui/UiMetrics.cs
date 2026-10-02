using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Config;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// Every pixel size the main window's panes use, derived once per frame from Dalamud's global scale and the user's
/// UI and icon scales (<see cref="Configuration.UiScale"/>, <see cref="Configuration.IconScale"/>, arithmetic in
/// <see cref="ScaleMetrics"/>). <see cref="Update"/> runs once per frame before the window system draws (and again
/// at the top of <c>MainWindow.Draw</c>); the values default to plain global scale before that.
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

    /// <summary>The user's icon scale alone (clamped), without the global and UI scales: the tree glyph's line factor.</summary>
    public static float IconFactor { get; private set; } = 1f;

    /// <summary>Dalamud's global scale alone, as of the last <see cref="Update"/>.</summary>
    public static float GlobalScale { get; private set; } = 1f;

    /// <summary><see cref="Configuration.Density"/> as of the last <see cref="Update"/>: the quest table's row height.</summary>
    public static RowDensity Density { get; private set; } = RowDensity.Comfortable;

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
        IconFactor = ScaleMetrics.ClampIconScale(settings.IconScale);
        GlobalScale = ScaleMetrics.SafeGlobalScale(global);
        Density = settings.Density;
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

    /// <summary>
    /// Half-size of a Journal tree node's halo gauge: half the text line scaled by the icon factor, clamped to 12–18 px
    /// so the halo box is never under 24 px (glyph proposal §3.5, accessibility A4) whatever the icon scale.
    /// </summary>
    public static float TreeGlyphRadius(float lineHeight) => ScaleMetrics.TreeGlyphRadius(lineHeight, IconFactor);

    /// <summary>Height of a Journal tree row: the halo box plus 6 logical px, at least 30 px and one text line (T11).</summary>
    public static float TreeRowHeight(float lineHeight) => ScaleMetrics.TreeRowHeight(lineHeight, TreeGlyphRadius(lineHeight), Scale);

    /// <summary>A halo's inline box where the row has room for it: the inline glyph square, never under 24 px so the core shows.</summary>
    public static float HaloBoxSize(float lineHeight) => MathF.Max(InlineGlyphSize(lineHeight), 2f * GaugeGeometry.CoreMinRadius);

    public static float HeaderMoonRadius => Icon(17f);
    public static float PathGlyphRadius => Icon(6f);
    /// <summary>Box of a requirement line's check or cross mark.</summary>
    public static float RequirementMarkSize => Icon(12f);
    /// <summary>The status bar's halo: track and arc only (R 8–12) with the percentage beside it.</summary>
    public static float StatusHaloRadius => Math.Clamp(Icon(6.5f), GaugeGeometry.RingMinRadius, GaugeGeometry.CoreMinRadius - 0.5f);
    public static float EmptyStateMoonRadius => Icon(28f);

    /// <summary>Square reserved for an inline state moon so its radius is <see cref="RowGlyphRadius"/>, never shorter than the line.</summary>
    public static float InlineGlyphSize(float lineHeight) => MathF.Max(lineHeight, RowGlyphRadius / MoonGlyph.InlineRadiusFraction);

    /// <summary>
    /// <see cref="InlineGlyphSize"/> on the surfaces read while playing (Todo overlay, Nearby): grown so the moon is
    /// never under <see cref="ScaleMetrics.PlayingGlyphMinDiameter"/> (14 px) across, whatever the scales.
    /// </summary>
    public static float PlayingGlyphSize(float lineHeight) =>
        ScaleMetrics.GlyphBoxWithFloor(InlineGlyphSize(lineHeight), MoonGlyph.InlineRadiusFraction);

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

    /// <summary>
    /// Content height of a quest table row under the current <see cref="Density"/>: 24 or 32 px at Dalamud's scale less
    /// the cell padding, never less than <see cref="RowContentHeight"/> (T12).
    /// </summary>
    public static float TableRowContentHeight(float lineHeight, float cellPaddingY) =>
        ScaleMetrics.TableRowContent(Density, GlobalScale, RowContentHeight(lineHeight), cellPaddingY);

    // Layout.
    public static float CharacterComboWidth => Px(240f);
    public static float MinBodyHeight => Px(120f);
    public static float Stripe => MathF.Max(1f, Px(2f));
    public static float Hairline => MathF.Max(1f, Px(1f));

    /// <summary>
    /// Smallest side of a click target (icon buttons, the search clear, chip close targets): Px(26), never under
    /// 24 px, so UiScale 0.9 keeps the WCAG 2.5.8 minimum. Tree chevrons keep TreeNodeEx's arrow slot instead.
    /// </summary>
    public static float MinTarget => MathF.Max(Px(26f), 24f);

    /// <summary>Widest a tooltip's text runs before it wraps, in ems (font sizes): long hints wrap, short ones keep their width.</summary>
    public const float TooltipWrapEm = 32f;

    /// <summary>
    /// Wraps every text line of the current tooltip at <see cref="TooltipWrapEm"/> from where its content starts. Call
    /// inside the tooltip after its font and font scale are set; <c>TextUnformatted</c> and <c>TextDisabled</c> honour it
    /// as <c>TextWrapped</c> does, and a line shorter than the wrap width draws exactly as before.
    /// </summary>
    public static ImRaii.TextWrapDisposable TooltipWrap() => ImRaii.TextWrapPos(ImGui.GetCursorPosX() + (ImGui.GetFontSize() * TooltipWrapEm));

    /// <summary>
    /// A plain text tooltip, Night styled and drawn with the UI scale (SetTooltip can be neither). It is always in the
    /// default font, even when hung off an item drawn in a <see cref="Typography"/> role, and wraps at <see cref="TooltipWrapEm"/>.
    /// </summary>
    public static void Tooltip(string text)
    {
        using var tooltipStyle = Theme.PushTooltip();
        using var tooltip = ImRaii.Tooltip();
        ImGui.PushFont(UiBuilder.DefaultFont);
        ApplyFontScale();
        using (TooltipWrap())
        {
            ImGui.TextUnformatted(text);
        }

        ImGui.PopFont();
    }

    /// <summary>A two-line tooltip: <paramref name="text"/>, then <paramref name="detail"/> in the disabled tone when it is not empty; both wrap.</summary>
    public static void Tooltip(string text, string? detail)
    {
        using var tooltipStyle = Theme.PushTooltip();
        using var tooltip = ImRaii.Tooltip();
        ImGui.PushFont(UiBuilder.DefaultFont);
        ApplyFontScale();
        using (TooltipWrap())
        {
            ImGui.TextUnformatted(text);
            if (!string.IsNullOrEmpty(detail))
            {
                ImGui.TextDisabled(detail);
            }
        }

        ImGui.PopFont();
    }

    // The last reason line composed for a moon tooltip: one moon is hovered at a time, and an evaluation is an
    // immutable record replaced on every resolve, so (evaluation, quest, states) identifies the line; the states map
    // is part of the key because the blocker picks its prerequisite differently with and without it.
    private static QuestEvaluation? reasonEvaluation;
    private static QuestRecord? reasonQuest;
    private static IReadOnlyDictionary<uint, QuestEvaluation>? reasonStates;
    private static string? reasonText;

    /// <summary>
    /// The tooltip of a state moon: <see cref="Strings.StateTooltip(QuestState, QuestRecord?)"/> (name and shape
    /// hint), then the decisive blocker under it (<see cref="Strings.StateReason"/>, the same words as the Status
    /// column) when the evaluation has one. Call after the moon's item while it is hovered. The name line is
    /// precomposed and the reason is cached per evaluation, so a hover held over frames allocates nothing.
    /// </summary>
    /// <param name="names">Name lookups for the blocker line, normally the session's.</param>
    /// <param name="states">Every quest's evaluation for the same character when at hand; null for another character's evaluation.</param>
    private static int reasonLanguage = -1;

    public static void StateTooltip(QuestState state, QuestEvaluation? evaluation, QuestRecord? quest, BlockerNames names, IReadOnlyDictionary<uint, QuestEvaluation>? states)
    {
        if (!ReferenceEquals(evaluation, reasonEvaluation) || !ReferenceEquals(quest, reasonQuest) || !ReferenceEquals(states, reasonStates) || reasonLanguage != Localization.Loc.Version)
        {
            reasonLanguage = Localization.Loc.Version;
            reasonEvaluation = evaluation;
            reasonQuest = quest;
            reasonStates = states;
            reasonText = Strings.StateReason(state, evaluation, quest, names, states);
        }

        Tooltip(Strings.StateTooltip(state, quest), reasonText);
    }

    /// <summary>Square size vector helper.</summary>
    public static Vector2 Square(float size) => new(size, size);
}
