using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;
using Tsukimichi.Localization;
using Tsukimichi.Ui.Themes;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Themes (1.16, plan v7 T9; docs/design/v7/ui/spec-1.16.md §B). One column:
/// <list type="bullet">
/// <item><b>Theme</b>: a card per offered theme (206 × 196, three to a row, wrapping by width only), each its eight
/// moons at 28 px on the theme's own pane, a sample row, its name, frames and palette, and four palette chips. The theme
/// in use has the 2 px gilt edge (never a tick); a hovered card a 1 px line. Nothing moves or lifts.</item>
/// <item><b>Preview</b>: a fixed panel of six quest rows and a 64 px hero medal, drawn under the hovered card's
/// appearance (<see cref="GlyphSeam.PushAppearance"/>) or the saved one, crossfading over
/// <see cref="MotionTokens.Swap"/> (at once under Reduce motion).</item>
/// <item><b>Colours</b>: the palette tiles (Follow Dalamud is one), High contrast, and Frames.</item>
/// <item><b>Share</b> (1.17): the look's share code with Copy, and a paste field that previews a code before Apply
/// (<c>ConfigWindow.Themes.Share.cs</c>).</item>
/// <item><b>Reset appearance</b>: one click with Undo (<see cref="GuardedAction.ResetAppearance"/>).</item>
/// </list>
/// Hover previews, click applies at once, Undo follows (<see cref="UndoToast"/>); only a pasted share code waits for an
/// Apply, after its preview. Moon style,
/// Moon colours and Follow Dalamud colours moved here from General › Look: the cards, High contrast and the palette tiles,
/// with their search words.
/// </summary>
public sealed partial class ConfigWindow
{
    private const string ThemeCardsKeywords = "theme themes moon moons medal medals glyph glyphs style look medallion classic legacy ishgard glass aether crystal compare";
    private const string ThemeColoursKeywords = "colours colors palette night ishgard snow light dark follow dalamud style high contrast colour blind color blind accessibility frames";

    // Spec-1.16 §B2, in logical px.
    private const float CardWidthLogical = 206f;
    private const float ThemeCardGapLogical = 14f;
    private const float CardFaceLogical = 28f;
    private const float CardPadLogical = 12f;
    private const float CardRoundingLogical = 6f;
    private const float ChipWidthLogical = 22f;
    private const float ChipHeightLogical = 6f;
    private const float ThemePreviewWidthLogical = 660f;
    private const float ThemePreviewHeightLogical = 268f;
    private const float PreviewHeroLogical = 64f;
    private const float PreviewCardWidthLogical = 200f;
    private const float TileWidthLogical = 76f;
    private const float TileHeightLogical = 48f;
    private const float TileGapLogical = 10f;

    /// <summary>How long the Preview keeps the last hovered card after the pointer leaves it, so crossing the gap between cards does not fade to the saved look and back.</summary>
    private const double PreviewHoldSeconds = 0.1;

    // Motion keys: the card's gilt edge ("THCS"), its hover line ("THCH"), the preview's swap ("THPV"), a tile's edge ("THPT").
    private const uint CardSelectTag = 0x5448_4353;
    private const uint CardHoverTag = 0x5448_4348;
    private const uint PreviewSwapTag = 0x5448_5056;
    private const uint TileSelectTag = 0x5448_5054;

    /// <summary>The preview's six sample rows, one of each kind of state (spec §B2).</summary>
    private static readonly QuestState[] ThemePreviewStates =
    [
        QuestState.Ready, QuestState.Accepted, QuestState.Completed, QuestState.Blocked, QuestState.Foreclosed, QuestState.Unknown,
    ];

    /// <summary>The Frames choice: From theme, then the offered kits.</summary>
    private static readonly LocArray FrameOptions = new(static () =>
    {
        var kits = ThemesPage.Kits;
        var options = new string[kits.Count + 1];
        options[0] = Strings.ThemesFramesFromTheme;
        for (var i = 0; i < kits.Count; i++)
        {
            options[i + 1] = KitName(kits[i].Id);
        }

        return options;
    });

    private readonly ThemesPreview themesPreview = new();

    // The card hovered this frame (the Preview block, drawn after the cards, reads it), and the frame it was hovered in.
    private ThemePreset? hoveredTheme;
    private int hoveredThemeFrame = -1;
    private double hoveredThemeAt = double.NegativeInfinity;

    // The Preview panel's crossfade: what it shows (its signature), and its fill and rule now and as the swap began.
    private uint previewSignature = uint.MaxValue;
    private (Vector4 Window, Vector4 Line) previewPanel;
    private (Vector4 Window, Vector4 Line) previewPanelFrom;

    // The palette tiles, listed once (the registry is fixed for the session).
    private PaletteInfo[]? paletteTiles;

    // The cards' second lines, rebuilt when the language changes.
    private string[] cardSubtitles = [];
    private int cardSubtitlesLanguage = -1;

    // The preview header, rebuilt when what it names changes.
    private string previewHeader = string.Empty;
    private string previewInUse = string.Empty;
    private (ThemeId Theme, bool Previewing, ThemeId InUse, bool Custom, int Language) previewHeaderKey = (0, false, 0, false, -1);

    // The user's own Dalamud style, read in PreDraw before the Night window style is pushed, for the Follow Dalamud tile.
    private Vector4 hostWindow, hostFrame, hostFrameHovered, hostBorder, hostText, hostTextDisabled;
    private UiPalette? hostPalette;
    private (Vector4, Vector4, Vector4, Vector4, Vector4, Vector4) hostPaletteFrom;

    /// <summary>The theme's name as the page shows it.</summary>
    internal static string ThemeName(ThemeId id) => id switch
    {
        ThemeId.Classic => Strings.ThemeNameClassic,
        ThemeId.AetherCrystal => Strings.ThemeNameAetherCrystal,
        ThemeId.IshgardGlass => Strings.ThemeNameIshgardGlass,
        ThemeId.Orrery => Strings.ThemeNameOrrery,
        ThemeId.Sumi => Strings.ThemeNameSumi,
        _ => Strings.ThemeNameMedallion,
    };

    /// <summary>The palette's name as the page shows it.</summary>
    internal static string PaletteName(PaletteId id) => id switch
    {
        PaletteId.Dawn => Strings.PaletteNameDawn,
        PaletteId.IshgardSnow => Strings.PaletteNameIshgardSnow,
        PaletteId.KuganeLacquer => Strings.PaletteNameKuganeLacquer,
        PaletteId.FollowDalamud => Strings.PaletteNameFollowDalamud,
        _ => Strings.PaletteNameNight,
    };

    /// <summary>The frame kit's name as the page shows it.</summary>
    internal static string KitName(FrameKitId id) => id switch
    {
        FrameKitId.Silver => Strings.FrameKitSilver,
        FrameKitId.Came => Strings.FrameKitCame,
        FrameKitId.Astrolabe => Strings.FrameKitAstrolabe,
        FrameKitId.Kirikane => Strings.FrameKitKirikane,
        _ => Strings.FrameKitBrass,
    };

    /// <summary>Reads the user's Dalamud style colours (call before the window pushes its own): the Follow Dalamud tile draws them.</summary>
    private void CaptureHostStyle()
    {
        var colors = ImGui.GetStyle().Colors;
        hostWindow = colors[(int)ImGuiCol.WindowBg];
        hostFrame = colors[(int)ImGuiCol.FrameBg];
        hostFrameHovered = colors[(int)ImGuiCol.FrameBgHovered];
        hostBorder = colors[(int)ImGuiCol.Border];
        hostText = colors[(int)ImGuiCol.Text];
        hostTextDisabled = colors[(int)ImGuiCol.TextDisabled];
    }

    /// <summary>The Follow Dalamud palette from the style read in <see cref="CaptureHostStyle"/>, rebuilt only when it changes.</summary>
    private UiPalette HostPalette()
    {
        var from = (hostWindow, hostFrame, hostFrameHovered, hostBorder, hostText, hostTextDisabled);
        if (hostPalette is null || from != hostPaletteFrom)
        {
            hostPaletteFrom = from;
            hostPalette = UiPalettes.FollowDalamud(hostWindow, hostFrame, hostFrameHovered, hostBorder, hostText, hostTextDisabled);
        }

        return hostPalette;
    }

    /// <summary>The palette <paramref name="id"/> draws in on this page (Night for one not registered yet), in its high-contrast form under <paramref name="highContrast"/>.</summary>
    private UiPalette PaletteFor(PaletteId id, bool highContrast)
    {
        var drawable = ThemesPage.Drawable(id, UiPalettes.IsRegistered);
        var palette = drawable == PaletteId.FollowDalamud ? HostPalette() : UiPalettes.Get(drawable);
        return highContrast ? palette.HighContrast : palette;
    }

    /// <summary>
    /// Settings › Themes › Theme: the cards. Hovering one previews it below; a click applies it at once with the Undo
    /// toast "Theme: Ishgard Glass · Undo". Under High contrast the cards show the shared moons, with a line saying so
    /// (its height is always kept, so nothing moves when it appears).
    /// </summary>
    private void DrawThemeCards()
    {
        Header(Strings.ThemesHeadingTheme);
        if (!BareRow(Strings.ThemesCards, Strings.ThemesCardsHint, ThemeCardsKeywords))
        {
            return;
        }

        var saved = settings.Appearance;
        var inUse = GlyphSeam.Appearance;
        var themes = ThemesPage.Themes;
        RefreshCardSubtitles(themes);

        var avail = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var gap = UiMetrics.Px(ThemeCardGapLogical);
        var width = MathF.Min(UiMetrics.Px(CardWidthLogical), avail);
        var columns = ThemesPage.Columns(avail, width, gap);
        var metrics = CardMetrics.Measure();
        var height = metrics.Height;
        var origin = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        for (var i = 0; i < themes.Count; i++)
        {
            var theme = themes[i];
            var min = origin + new Vector2((i % columns) * (width + gap), (i / columns) * (height + gap));
            ImGui.SetCursorScreenPos(min);
            using var id = ImRaii.PushId(theme.Key);
            var clicked = ImGui.InvisibleButton("##themeCard", new Vector2(width, height));
            var hovered = ImGui.IsItemHovered();
            if (hovered)
            {
                hoveredTheme = theme;
                hoveredThemeFrame = ImGui.GetFrameCount();
                hoveredThemeAt = ImGui.GetTime();
            }

            var card = themesPreview.Card(saved, theme);
            DrawThemeCard(dl, min, new Vector2(width, height), metrics, theme, card, cardSubtitles[i], ThemesPage.InUse(inUse, theme), hovered, i);
            if (clicked)
            {
                ApplyThemeCard(theme);
            }
        }

        var rows = ThemesPage.Rows(themes.Count, columns);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(avail, MathF.Max(1f, (rows * height) + (MathF.Max(0, rows - 1) * gap))));

        // The high-contrast line's place is always kept.
        using (Typography.Caption())
        {
            var line = ImGui.GetTextLineHeight();
            var at = ImGui.GetCursorScreenPos();
            if (saved.HighContrast)
            {
                Chrome.EllipsisTextAt(dl, at, avail, Strings.ThemesHighContrastNote, Theme.U32(Theme.Surface.TextSecondary));
            }

            ImGui.Dummy(new Vector2(avail, line));
        }

        EndBareRow();
    }

    /// <summary>Applies <paramref name="theme"/> (its palette and frames follow it; high contrast stays), with Undo.</summary>
    private void ApplyThemeCard(ThemePreset theme)
    {
        var saved = settings.Appearance;
        if (ThemesPage.WhatIf(saved, theme).SameAs(saved))
        {
            return;
        }

        var before = saved.Clone();
        AppearanceEdits.ApplyTheme(saved, theme);
        Save();
        UndoToast.Show(string.Format(CultureInfo.CurrentCulture, Strings.UndoToastThemeFormat, ThemeName(theme.Id)), () => RestoreAppearance(before));
    }

    /// <summary>Undo of any change on this page: the appearance as it was.</summary>
    private void RestoreAppearance(AppearanceConfig before)
    {
        settings.Appearance = before.Clone();
        Save();
    }

    /// <summary>The cards' second lines ("Brass frames · Night", "Ishgard Snow", "The 1.11 moons · Night"), once per language.</summary>
    private void RefreshCardSubtitles(System.Collections.Generic.IReadOnlyList<ThemePreset> themes)
    {
        if (cardSubtitlesLanguage == Loc.Version && cardSubtitles.Length == themes.Count)
        {
            return;
        }

        cardSubtitlesLanguage = Loc.Version;
        cardSubtitles = new string[themes.Count];
        for (var i = 0; i < themes.Count; i++)
        {
            var theme = themes[i];
            var palette = PaletteName(ThemesPage.Drawable(theme.Palette, UiPalettes.IsRegistered));

            // A kit is named only once it draws its own metal (1.17 T11); until then the palette alone, never a promise.
            cardSubtitles[i] = theme.Legacy
                ? string.Format(CultureInfo.CurrentCulture, Strings.ThemesCardSubtitleClassicFormat, palette)
                : FrameKitRenderers.HasOwnMetal(theme.Frames)
                    ? string.Format(CultureInfo.CurrentCulture, Strings.ThemesCardSubtitleFormat, KitName(theme.Frames), palette)
                    : palette;
        }
    }

    /// <summary>One theme card (spec §B2 "Card anatomy"); the caller has laid its button over it.</summary>
    private void DrawThemeCard(ImDrawListPtr dl, Vector2 min, Vector2 size, CardMetrics m, ThemePreset theme, ResolvedAppearance card, string subtitle, bool inUse, bool hovered, int index)
    {
        var s = Theme.Surface;
        var max = min + size;
        var rounding = UiMetrics.Px(CardRoundingLogical);
        var pad = UiMetrics.Px(CardPadLogical);
        var palette = PaletteFor(card.Palette, card.HighContrast);
        var ps = palette.Surface;

        // The card, then the theme's own pane: its sky over its window, as the theme draws them.
        dl.AddRectFilled(min, max, Theme.WithAlpha(s.Raised, 0.55f), rounding);
        var paneMax = new Vector2(max.X, min.Y + m.Pane);
        var skyEnd = min.Y + (m.Pane * 0.7f);
        dl.AddRectFilled(min, new Vector2(max.X, min.Y + rounding), Theme.U32(palette.Scene.Zenith with { W = 1f }), rounding, ImDrawFlags.RoundCornersTop);
        dl.AddRectFilledMultiColor(new Vector2(min.X, min.Y + rounding), new Vector2(max.X, skyEnd), Theme.U32(palette.Scene.Zenith with { W = 1f }), Theme.U32(palette.Scene.Zenith with { W = 1f }), Theme.U32(ps.Window with { W = 1f }), Theme.U32(ps.Window with { W = 1f }));
        dl.AddRectFilled(new Vector2(min.X, skyEnd), paneMax, Theme.U32(ps.Window with { W = 1f }));

        // Eight faces, 4 × 2 in state order, drawn by the card's own appearance.
        var face = UiMetrics.Px(CardFaceLogical);
        var radius = face * MoonGlyph.InlineRadiusFraction;
        var columnWidth = (size.X - (pad * 2f)) / 4f;
        var firstRow = min.Y + pad + (face * 0.5f);
        var rowStep = face + UiMetrics.Px(10f);

        // The card's appearance and its palette's glyph inputs (light or dark, gauges, washes, the high-contrast ladder).
        using (GlyphSeam.PushAppearance(card))
        using (Theme.PushPalette(PaletteFor(card.Palette, highContrast: false), card.GlyphPalette))
        {
            var states = AppearanceStates.All;
            for (var i = 0; i < states.Count; i++)
            {
                var center = new Vector2(MathF.Round(min.X + pad + ((i % 4) + 0.5f) * columnWidth), MathF.Round(firstRow + ((i / 4) * rowStep)));
                GlyphSeam.Draw(dl, center, radius, states[i], 0);
            }

            // The sample row: a Ready medal, "Firmament", and Ready in the palette's accent.
            var rowMin = new Vector2(min.X + UiMetrics.Px(8f), paneMax.Y - UiMetrics.Px(8f) - m.SampleRow);
            var rowMax = new Vector2(max.X - UiMetrics.Px(8f), paneMax.Y - UiMetrics.Px(8f));
            dl.AddRectFilled(rowMin, rowMax, Theme.U32(ps.Raised with { W = 1f }), UiMetrics.Px(4f));
            var glyph = UiMetrics.InlineGlyphSize(m.Line) * 0.9f;
            var mid = (rowMin.Y + rowMax.Y) * 0.5f;
            GlyphSeam.Draw(dl, new Vector2(rowMin.X + UiMetrics.Px(6f) + (glyph * 0.5f), mid), glyph * MoonGlyph.InlineRadiusFraction, QuestState.Ready, 0);
            var ready = Strings.StateName(QuestState.Ready);
            var readyWidth = ImGui.CalcTextSize(ready).X;
            var textY = mid - (m.Line * 0.5f);
            var nameX = rowMin.X + UiMetrics.Px(12f) + glyph;
            var readyX = rowMax.X - UiMetrics.Px(8f) - readyWidth;
            dl.AddText(new Vector2(readyX, textY), Theme.U32(palette.Accent), ready);
            Chrome.EllipsisTextAt(dl, new Vector2(nameX, textY), MathF.Max(1f, readyX - nameX - UiMetrics.Px(6f)), Strings.ThemesSampleQuest, Theme.U32(ps.Text));
        }

        // Under the pane: the name (Legacy beside Classic's), the frames and palette, the chips and "In use".
        var x = min.X + pad;
        var room = size.X - (pad * 2f);
        var y = paneMax.Y + UiMetrics.Px(10f);
        var name = ThemeName(theme.Id);
        var nameWidth = DrawCardName(dl, new Vector2(x, y), room, name, m.Title);
        if (theme.Legacy)
        {
            using (Typography.Caption())
            {
                var tag = Strings.ThemesLegacyTag;
                var tagPad = UiMetrics.Px(5f);
                var tagSize = ImGui.CalcTextSize(tag) + new Vector2(tagPad * 2f, UiMetrics.Px(2f));
                var tagMin = new Vector2(x + nameWidth + UiMetrics.Px(8f), y + ((m.Title - tagSize.Y) * 0.5f));
                if (tagMin.X + tagSize.X <= x + room)
                {
                    dl.AddRectFilled(tagMin, tagMin + tagSize, Theme.U32(s.Sunken), tagSize.Y * 0.5f);
                    dl.AddText(tagMin + new Vector2(tagPad, UiMetrics.Px(1f)), Theme.U32(s.TextSecondary), tag);
                }
            }
        }

        y += m.Title + UiMetrics.Px(2f);
        using (Typography.Caption())
        {
            Chrome.EllipsisTextAt(dl, new Vector2(x, y), room, subtitle, Theme.U32(s.TextTertiary));
            y += m.Caption + UiMetrics.Px(8f);

            var chip = new Vector2(UiMetrics.Px(ChipWidthLogical), UiMetrics.Px(ChipHeightLogical));
            var chipY = y + ((m.Caption - chip.Y) * 0.5f);
            ReadOnlySpan<Vector4> chips = [ps.Window, ps.Raised, ps.Text, palette.Accent];
            for (var i = 0; i < chips.Length; i++)
            {
                var chipMin = new Vector2(x + (i * (chip.X + UiMetrics.Px(4f))), chipY);
                dl.AddRectFilled(chipMin, chipMin + chip, Theme.U32(chips[i] with { W = 1f }), UiMetrics.Px(1.5f));
                dl.AddRect(chipMin, chipMin + chip, Theme.WithAlpha(s.Line, 0.9f), UiMetrics.Px(1.5f), ImDrawFlags.None, UiMetrics.Hairline);
            }

            if (inUse)
            {
                var label = Strings.ThemesInUse;
                dl.AddText(new Vector2(x + room - ImGui.CalcTextSize(label).X, y), Theme.AccentU32, label);
            }
        }

        // The edges: a 1 px line while hovered, the 2 px gilt edge on the theme in use (it settles over Select).
        var hover = Motion.Hover(Motion.Key(CardHoverTag, (uint)index), hovered);
        var chosen = Motion.Select(Motion.Key(CardSelectTag, (uint)index), inUse);
        dl.AddRect(min, max, Theme.WithAlpha(s.Line, 0.8f), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        if (hover > 0.01f && chosen < 0.99f)
        {
            dl.AddRect(min, max, Theme.WithAlpha(s.StrongLine, hover * (1f - chosen)), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        }

        if (chosen > 0.01f)
        {
            var edge = MathF.Max(1f, UiMetrics.Px(2f));
            var inset = edge * 0.5f;
            dl.AddRect(min + new Vector2(inset), max - new Vector2(inset), Theme.WithAlpha(Theme.GoldLine, chosen), rounding, ImDrawFlags.None, edge);
        }
    }

    /// <summary>The card's name in the Title role, or the display face when the Title face is too wide for it; returns its width.</summary>
    private static float DrawCardName(ImDrawListPtr dl, Vector2 at, float room, string name, float titleHeight)
    {
        var color = Theme.U32(Theme.Surface.Text);
        using (Typography.Title(name))
        {
            var width = ImGui.CalcTextSize(name).X;
            if (width <= room)
            {
                dl.AddText(at, color, name);
                return width;
            }
        }

        using (Typography.Display())
        {
            var y = at.Y + MathF.Max(0f, (titleHeight - ImGui.GetTextLineHeight()) * 0.5f);
            Chrome.EllipsisTextAt(dl, new Vector2(at.X, y), room, name, color);
            return MathF.Min(room, ImGui.CalcTextSize(name).X);
        }
    }

    /// <summary>
    /// Settings › Themes › Preview: the fixed panel. It draws the hovered card's appearance ("Previewing: Ishgard Glass ·
    /// In use: Menphina's Medallion") or the saved one ("Preview · Menphina's Medallion"), in that appearance's palette,
    /// crossfading when it changes. Its size depends only on the window and the text size, never on what it shows.
    /// </summary>
    private void DrawThemePreview()
    {
        Header(Strings.ThemesHeadingPreview);
        if (!BareRow(Strings.ThemesHeadingPreview, null, ThemeCardsKeywords))
        {
            return;
        }

        var saved = settings.Appearance;
        var inUse = GlyphSeam.Appearance;
        // The card hovered this frame, or the last one for a moment after (crossing the gap to the next card keeps it).
        var hovered = hoveredThemeFrame == ImGui.GetFrameCount() || ImGui.GetTime() - hoveredThemeAt < PreviewHoldSeconds ? hoveredTheme : null;
        var target = themesPreview.Target(saved, inUse, hovered);
        var palette = target.Previewing ? PaletteFor(target.Appearance.Palette, target.Appearance.HighContrast) : Theme.Palette;
        var ps = palette.Surface;

        // What is drawn, not whether it is a preview: clicking the hovered card applies what is already shown, so no dip.
        var signature = (uint)target.Theme.Id | ((uint)target.Appearance.Palette << 12) | (target.Appearance.HighContrast ? 1u << 20 : 0u);
        if (signature != previewSignature)
        {
            previewPanelFrom = previewSignature == uint.MaxValue ? (ps.Window, ps.Line) : previewPanel;
            previewSignature = signature;
        }

        var swap = Motion.Changed(Motion.Key(PreviewSwapTag, 0), signature, MotionTokens.Swap);
        var alpha = MotionTokens.SwapAlpha(swap);
        uint Ink(Vector4 color) => Theme.WithAlpha(color, color.W * alpha);

        // The panel's fill and rules cross from the last palette to this one over the same swap, as the content dips.
        var blend = swap is >= 0f and < 1f ? MotionMath.EaseInOutCubic(swap) : 1f;
        previewPanel = (Vector4.Lerp(previewPanelFrom.Window, ps.Window, blend), Vector4.Lerp(previewPanelFrom.Line, ps.Line, blend));

        var avail = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var line = ImGui.GetTextLineHeight();
        var pad = UiMetrics.Px(14f);
        var header = line + UiMetrics.Px(16f);
        var rowHeight = UiMetrics.InlineGlyphSize(line) + UiMetrics.Px(12f);
        var size = new Vector2(
            MathF.Min(avail, UiMetrics.Px(ThemePreviewWidthLogical)),
            MathF.Max(UiMetrics.Px(ThemePreviewHeightLogical), header + (ThemePreviewStates.Length * rowHeight) + (pad * 2f)));
        var min = ImGui.GetCursorScreenPos();
        var max = min + size;
        var dl = ImGui.GetWindowDrawList();
        var rounding = UiMetrics.Px(CardRoundingLogical);
        var panelLine = Theme.U32(previewPanel.Line with { W = 1f });
        dl.AddRectFilled(min, max, Theme.U32(previewPanel.Window with { W = 1f }), rounding);
        dl.AddRect(min, max, panelLine, rounding, ImDrawFlags.None, UiMetrics.Hairline);

        // The header: what is previewed, and what is in use beside it while previewing.
        RefreshPreviewHeader(target, inUse.Theme.Id, AppearanceEdits.IsCustom(saved));
        var headerY = min.Y + ((header - line) * 0.5f);
        var right = max.X - pad;
        if (target.Previewing)
        {
            using (Typography.Caption())
            {
                var width = ImGui.CalcTextSize(previewInUse).X;
                dl.AddText(new Vector2(right - width, headerY + ((line - ImGui.GetTextLineHeight()) * 0.5f)), Ink(ps.TextSecondary), previewInUse);
                right -= width + UiMetrics.Px(12f);
            }
        }

        Chrome.EllipsisTextAt(dl, new Vector2(min.X + pad, headerY), MathF.Max(1f, right - min.X - pad), previewHeader, Ink(ps.Text));
        dl.AddLine(new Vector2(min.X, min.Y + header), new Vector2(max.X, min.Y + header), panelLine, UiMetrics.Hairline);

        // The hero card on the right (when there is room), the rows on the left.
        var heroWidth = UiMetrics.Px(PreviewCardWidthLogical);
        var showHero = size.X >= heroWidth * 2.6f;
        var rowsRight = showHero ? max.X - pad - heroWidth - pad : max.X - pad;
        var top = min.Y + header + UiMetrics.Px(4f);
        using (GlyphSeam.PushAppearance(target.Appearance))
        using (target.Previewing ? Theme.PushPalette(PaletteFor(target.Appearance.Palette, highContrast: false), target.Appearance.GlyphPalette) : default)
        {
            var glyph = UiMetrics.InlineGlyphSize(line);
            for (var i = 0; i < ThemePreviewStates.Length; i++)
            {
                var state = ThemePreviewStates[i];
                var rowTop = top + (i * rowHeight);
                var mid = rowTop + (rowHeight * 0.5f);
                if (i > 0)
                {
                    dl.AddLine(new Vector2(min.X + pad, rowTop), new Vector2(rowsRight, rowTop), Ink(ps.Line), UiMetrics.Hairline);
                }

                GlyphSeam.Draw(dl, new Vector2(min.X + pad + (glyph * 0.5f), mid), UiMetrics.RowGlyphRadius, state, 0, alpha);
                var textY = mid - (line * 0.5f);
                var (name, detail) = PreviewSample(state);
                var word = Strings.StateName(state);
                using (Typography.Caption())
                {
                    var captionY = mid - (ImGui.GetTextLineHeight() * 0.5f);
                    var separator = Strings.UndoToastSeparator;
                    var detailX = rowsRight - ImGui.CalcTextSize(detail).X;
                    var separatorX = detailX - ImGui.CalcTextSize(separator).X;
                    var wordX = separatorX - ImGui.CalcTextSize(word).X;
                    dl.AddText(new Vector2(detailX, captionY), Ink(ps.TextSecondary), detail);
                    dl.AddText(new Vector2(separatorX, captionY), Ink(ps.TextTertiary), separator);
                    dl.AddText(new Vector2(wordX, captionY), Ink(palette.States.Text(state)), word);
                    var nameX = min.X + pad + glyph + UiMetrics.Px(8f);
                    Chrome.EllipsisTextAt(dl, new Vector2(nameX, textY), MathF.Max(1f, wordX - nameX - UiMetrics.Px(10f)), name, Ink(ps.Text));
                }
            }

            if (showHero)
            {
                var cardMin = new Vector2(max.X - pad - heroWidth, top + UiMetrics.Px(4f));
                var cardMax = new Vector2(max.X - pad, max.Y - pad);
                dl.AddRectFilled(cardMin, cardMax, Ink(ps.Raised with { W = 1f }), UiMetrics.Px(4f));
                dl.AddRect(cardMin, cardMax, Ink(ps.Line with { W = 1f }), UiMetrics.Px(4f), ImDrawFlags.None, UiMetrics.Hairline);
                var hero = UiMetrics.Px(PreviewHeroLogical);
                var centerX = (cardMin.X + cardMax.X) * 0.5f;
                var heroCenter = new Vector2(MathF.Round(centerX), MathF.Round(cardMin.Y + pad + (hero * 0.5f)));
                GlyphSeam.Draw(dl, heroCenter, hero * MoonGlyph.InlineRadiusFraction, QuestState.Accepted, 0, alpha);
                var y = heroCenter.Y + (hero * 0.5f) + UiMetrics.Px(10f);
                var heading = SectionHeading.Label(Strings.StateName(QuestState.Accepted));
                using (Typography.Eyebrow(heading))
                {
                    var width = MathF.Min(heroWidth - pad, ImGui.CalcTextSize(heading).X);
                    Chrome.EllipsisTextAt(dl, new Vector2(centerX - (width * 0.5f), y), heroWidth - pad, heading, Ink(palette.OrnamentLight));
                    y += ImGui.GetTextLineHeight() + UiMetrics.Px(2f);
                }

                using (Typography.Caption())
                {
                    var lineText = Strings.ThemesSampleHeroLine;
                    var width = MathF.Min(heroWidth - pad, ImGui.CalcTextSize(lineText).X);
                    Chrome.EllipsisTextAt(dl, new Vector2(centerX - (width * 0.5f), y), heroWidth - pad, lineText, Ink(ps.TextSecondary));
                }
            }
        }

        ImGui.Dummy(size);
        EndBareRow();
    }

    /// <summary>A preview row's sample quest name and its detail.</summary>
    private static (string Name, string Detail) PreviewSample(QuestState state) => state switch
    {
        QuestState.Ready => (Strings.ThemesSampleReady, Strings.ThemesSampleReadyDetail),
        QuestState.Accepted => (Strings.ThemesSampleJournal, Strings.ThemesSampleJournalDetail),
        QuestState.Completed => (Strings.ThemesSampleCompleted, Strings.ThemesSampleCompletedDetail),
        QuestState.Blocked => (Strings.ThemesSampleBlocked, Strings.ThemesSampleBlockedDetail),
        QuestState.Foreclosed => (Strings.ThemesSampleLocked, Strings.ThemesSampleLockedDetail),
        _ => (Strings.ThemesSampleUnknown, Strings.ThemesSampleUnknownDetail),
    };

    /// <summary>The preview header's text, rebuilt only when what it names changes.</summary>
    private void RefreshPreviewHeader(PreviewTarget target, ThemeId inUse, bool custom)
    {
        var key = (target.Theme.Id, target.Previewing, inUse, custom, Loc.Version);
        if (key == previewHeaderKey)
        {
            return;
        }

        previewHeaderKey = key;
        var inUseName = custom ? string.Format(CultureInfo.CurrentCulture, Strings.ThemesCustomisedFormat, ThemeName(inUse)) : ThemeName(inUse);
        previewInUse = string.Format(CultureInfo.CurrentCulture, Strings.ThemesPreviewInUseFormat, inUseName);
        previewHeader = target.Previewing
            ? string.Format(CultureInfo.CurrentCulture, Strings.ThemesPreviewingFormat, ThemeName(target.Theme.Id))
            : string.Format(CultureInfo.CurrentCulture, Strings.ThemesPreviewFormat, inUseName);
    }

    /// <summary>
    /// Settings › Themes › Colours: the palette tiles (Night, Ishgard Snow, Dawn, Kugane Lacquer, Follow Dalamud), High
    /// contrast (the old Moon colours) and Frames. Each applies at once, with Undo.
    /// </summary>
    private void DrawThemeColours()
    {
        Header(Strings.ThemesHeadingColours);
        DrawPaletteTiles();

        var saved = settings.Appearance;
        var highContrast = saved.HighContrast;
        if (Toggle(Strings.ThemesHighContrast, Strings.ThemesHighContrastHint, ref highContrast, "moon colours colors high contrast colour blind color blind accessibility low vision glyph standard"))
        {
            var before = saved.Clone();
            AppearanceEdits.SetHighContrast(saved, highContrast);
            Save();
            UndoToast.Show(highContrast ? Strings.UndoToastHighContrastOn : Strings.UndoToastHighContrastOff, () => RestoreAppearance(before));
        }

        // Frames: a choice once two kits draw a metal of their own (1.17 T11: Brass, Silver, Lead came, Astrolabe); a
        // build with fewer shows the kit that really draws (Brass, for a theme whose own kit has no metal yet).
        var choosable = ThemesPage.FramesChoosable(FrameKitRenderers.HasOwnMetal);
        var resolved = GlyphSeam.Appearance;
        var frames = choosable ? ThemesPage.FramesIndex(saved) : 1 + IndexOfKit(FrameKitRenderers.HasOwnMetal(resolved.Frames) ? resolved.Frames : FrameKitId.Brass);
        var options = FrameOptions.Value;
        if (!Setting(Strings.ThemesFrames, Strings.ThemesFramesHint, "frames frame metal brass silver came lead astrolabe rim border", Chrome.SegmentedWidth(options), UiMetrics.MinTarget, choosable, reason: Strings.ThemesFramesFixedReason))
        {
            return;
        }

        var changed = Chrome.Segmented("##choice", ref frames, options, ControlWidth) && choosable;
        if (choosable)
        {
            DrawFramesWarning(resolved);
        }

        EndSetting();
        if (changed)
        {
            var before = saved.Clone();
            var kit = ThemesPage.KitAt(frames);
            AppearanceEdits.SetFrames(saved, kit);
            Save();
            UndoToast.Show(string.Format(CultureInfo.CurrentCulture, Strings.UndoToastFramesFormat, kit is null ? Strings.ThemesFramesFromTheme : KitName(kit.Id)), () => RestoreAppearance(before));
        }
    }

    // The Frames row's warning line, rebuilt when the appearance or the language changes.
    private ResolvedAppearance? framesWarningFor;
    private int framesWarningLanguage = -1;
    private string framesWarning = string.Empty;
    private string framesWarningAll = string.Empty;

    /// <summary>
    /// Under the Frames control, one calm line (its place always kept, so nothing moves): what the chosen kit plus the
    /// faces drawn in it miss (<see cref="FrameKitChecks"/>), in plain words naming the states, the first in full and the
    /// rest in its tooltip. Nothing is blocked; the line only says so.
    /// </summary>
    private void DrawFramesWarning(ResolvedAppearance resolved)
    {
        if (!ReferenceEquals(resolved, framesWarningFor) || framesWarningLanguage != Loc.Version)
        {
            framesWarningFor = resolved;
            framesWarningLanguage = Loc.Version;
            var flags = FrameKitChecks.For(resolved);
            var lines = flags.Select(FramesWarningText).ToArray();
            framesWarningAll = string.Join("\n", lines);
            framesWarning = lines.Length switch
            {
                0 => string.Empty,
                1 => lines[0],
                _ => string.Format(CultureInfo.CurrentCulture, Strings.ThemesFramesMoreFormat, lines[0], lines.Length - 1),
            };
        }

        SettingBelow();
        using (Typography.Caption())
        {
            var at = ImGui.GetCursorScreenPos();
            var width = MathF.Max(1f, row.Right - at.X);
            var line = ImGui.GetTextLineHeight();
            if (framesWarning.Length > 0)
            {
                Chrome.EllipsisTextAt(ImGui.GetWindowDrawList(), at, width, framesWarning, Theme.U32(Theme.Surface.TextSecondary));
            }

            ImGui.Dummy(new Vector2(width, line));
            if (framesWarning.Length > 0 && ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(framesWarningAll);
            }
        }
    }

    private static string FramesWarningText(KitFlag flag)
    {
        var kit = KitName(flag.Kit);
        var set = ThemeName((ThemeId)(byte)flag.Set);
        return flag.Kind switch
        {
            KitFlagKind.ReadyLead => string.Format(CultureInfo.CurrentCulture, Strings.ThemesFramesReadyFormat, kit, set),
            KitFlagKind.CompletedRecedes => string.Format(CultureInfo.CurrentCulture, Strings.ThemesFramesCompletedFormat, kit, set),
            _ => string.Format(
                CultureInfo.CurrentCulture,
                flag.Hard
                    ? flag.ColourVision ? Strings.ThemesFramesHardColourFormat : Strings.ThemesFramesHardFormat
                    : flag.ColourVision ? Strings.ThemesFramesCloseColourFormat : Strings.ThemesFramesCloseFormat,
                kit,
                Strings.StateName(flag.A),
                Strings.StateName(flag.B),
                set),
        };
    }

    private static int IndexOfKit(FrameKitId id)
    {
        var kits = ThemesPage.Kits;
        for (var i = 0; i < kits.Count; i++)
        {
            if (kits[i].Id == id)
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>The Palette row: a mini window per palette (sky, two bars, a gold dot) with its name under it.</summary>
    private void DrawPaletteTiles()
    {
        paletteTiles ??= [.. ThemesPage.Palettes(UiPalettes.IsRegistered)];
        var tiles = paletteTiles;
        var tile = new Vector2(UiMetrics.Px(TileWidthLogical), UiMetrics.Px(TileHeightLogical));
        float labelHeight;
        var widest = tile.X;
        using (Typography.Caption())
        {
            labelHeight = ImGui.GetTextLineHeight();
            foreach (var palette in tiles)
            {
                widest = MathF.Max(widest, ImGui.CalcTextSize(PaletteName(palette.Id)).X);
            }
        }

        var gap = UiMetrics.Px(TileGapLogical);
        var pitch = widest + gap;
        var controlHeight = tile.Y + UiMetrics.Px(4f) + labelHeight;
        if (!Setting(Strings.ThemesPalette, Strings.ThemesPaletteHint, "palette colours colors window sky night ishgard snow dawn kugane lacquer light dark follow dalamud style theme", (pitch * tiles.Length) - gap, controlHeight))
        {
            return;
        }

        var saved = settings.Appearance;
        var resolved = GlyphSeam.Appearance;
        var current = ThemesPage.Drawable(resolved.Palette, UiPalettes.IsRegistered);
        pitch = MathF.Min(pitch, (ControlWidth + gap) / MathF.Max(1, tiles.Length));
        var origin = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        var s = Theme.Surface;
        for (var i = 0; i < tiles.Length; i++)
        {
            var info = tiles[i];
            var cell = origin + new Vector2(i * pitch, 0f);
            var min = cell + new Vector2(MathF.Round(((pitch - gap) - tile.X) * 0.5f), 0f);
            ImGui.SetCursorScreenPos(min);
            using var id = ImRaii.PushId(info.Key);
            var clicked = ImGui.InvisibleButton("##paletteTile", tile);
            var hovered = ImGui.IsItemHovered();
            var selected = info.Id == current;
            DrawPaletteTile(dl, min, tile, PaletteFor(info.Id, saved.HighContrast));

            var hover = Motion.Hover(Motion.Key(CardHoverTag, 0x100u + (uint)i), hovered);
            var chosen = Motion.Select(Motion.Key(TileSelectTag, (uint)i), selected);
            var rounding = UiMetrics.Px(4f);
            dl.AddRect(min, min + tile, Theme.WithAlpha(s.Line, 0.9f), rounding, ImDrawFlags.None, UiMetrics.Hairline);
            if (hover > 0.01f && chosen < 0.99f)
            {
                dl.AddRect(min, min + tile, Theme.WithAlpha(s.StrongLine, hover * (1f - chosen)), rounding, ImDrawFlags.None, UiMetrics.Hairline);
            }

            if (chosen > 0.01f)
            {
                var edge = MathF.Max(1f, UiMetrics.Px(2f));
                dl.AddRect(min - new Vector2(edge * 0.5f), min + tile + new Vector2(edge * 0.5f), Theme.WithAlpha(Theme.GoldLine, chosen), rounding + (edge * 0.5f), ImDrawFlags.None, edge);
            }

            using (Typography.Caption())
            {
                var name = PaletteName(info.Id);
                var width = MathF.Min(pitch - gap, ImGui.CalcTextSize(name).X);
                Chrome.EllipsisTextAt(dl, new Vector2(cell.X + MathF.Round(((pitch - gap) - width) * 0.5f), min.Y + tile.Y + UiMetrics.Px(4f)), pitch - gap, name, Theme.U32(selected ? s.Text : s.TextSecondary));
            }

            if (clicked && !selected)
            {
                var before = saved.Clone();
                AppearanceEdits.SetPalette(saved, info);
                Save();
                UndoToast.Show(string.Format(CultureInfo.CurrentCulture, Strings.UndoToastPaletteFormat, PaletteName(info.Id)), () => RestoreAppearance(before));
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(MathF.Max(1f, ControlWidth), controlHeight));
        EndSetting();
    }

    /// <summary>A palette as a mini window: its sky over its window, a title bar in its text, two card bars and its gold.</summary>
    private static void DrawPaletteTile(ImDrawListPtr dl, Vector2 min, Vector2 size, UiPalette palette)
    {
        var ps = palette.Surface;
        var max = min + size;
        var rounding = UiMetrics.Px(4f);
        var zenith = Theme.U32(palette.Scene.Zenith with { W = 1f });
        var window = Theme.U32(ps.Window with { W = 1f });
        dl.AddRectFilled(min, max, window, rounding);
        var inset = UiMetrics.Hairline;
        dl.AddRectFilledMultiColor(min + new Vector2(inset), new Vector2(max.X - inset, min.Y + (size.Y * 0.75f)), zenith, zenith, window, window);

        var pad = UiMetrics.Px(7f);
        var bar = UiMetrics.Px(3f);
        dl.AddRectFilled(new Vector2(min.X + pad, min.Y + pad), new Vector2(min.X + pad + UiMetrics.Px(16f), min.Y + pad + bar), Theme.U32(ps.Text with { W = 1f }), bar * 0.5f);
        var card = Theme.U32(ps.Raised with { W = 1f });
        var thick = UiMetrics.Px(5f);
        var first = min.Y + pad + bar + UiMetrics.Px(6f);
        dl.AddRectFilled(new Vector2(min.X + pad, first), new Vector2(max.X - pad, first + thick), card, UiMetrics.Px(1.5f));
        var second = first + thick + UiMetrics.Px(4f);
        dl.AddRectFilled(new Vector2(min.X + pad, second), new Vector2(min.X + pad + ((size.X - (pad * 2f)) * 0.6f), second + thick), card, UiMetrics.Px(1.5f));
        var dot = UiMetrics.Px(3f);
        dl.AddCircleFilled(new Vector2(max.X - pad - dot, min.Y + pad + (bar * 0.5f)), dot, Theme.U32(palette.Accent with { W = 1f }), 12);
    }

    /// <summary>
    /// Settings › Themes › Reset appearance (spec §B5): Menphina's Medallion on Night with Brass frames and high contrast
    /// off, in one click; everything it discards comes back with Undo ("Appearance reset · Undo").
    /// </summary>
    private void DrawThemeReset()
    {
        var saved = settings.Appearance;
        // The frame's resolved appearance (the saved one: no preview is pushed here), so nothing resolves per frame.
        var isDefault = AppearanceEdits.IsDefault(saved, GlyphSeam.Appearance);
        if (!ButtonRow(Strings.ThemesReset, Strings.ThemesResetHint, Strings.ThemesResetButton, "reset appearance default theme restore look", enabled: !isDefault, reason: Strings.ThemesResetDefaultReason) || isDefault)
        {
            return;
        }

        var before = saved.Clone();
        AppearanceEdits.Reset(saved);
        Save();
        if (SafetyRules.OffersUndo(GuardedAction.ResetAppearance))
        {
            UndoToast.Show(Strings.UndoToastAppearanceReset, () => RestoreAppearance(before));
        }
    }

    /// <summary>A theme card's measures this frame: the same for every card, so the grid never shifts.</summary>
    private readonly record struct CardMetrics(float Pane, float SampleRow, float Line, float Title, float Caption, float Height)
    {
        public static CardMetrics Measure()
        {
            var line = ImGui.GetTextLineHeight();
            float title;
            using (Typography.Title(Strings.ThemeNameMedallion))
            {
                title = ImGui.GetTextLineHeight();
            }

            float caption;
            using (Typography.Caption())
            {
                caption = ImGui.GetTextLineHeight();
            }

            var face = UiMetrics.Px(CardFaceLogical);
            var sampleRow = MathF.Max(UiMetrics.Px(24f), line + UiMetrics.Px(6f));
            var pane = UiMetrics.Px(CardPadLogical) + (face * 2f) + UiMetrics.Px(10f) + UiMetrics.Px(10f) + sampleRow + UiMetrics.Px(8f);
            var below = UiMetrics.Px(10f) + title + UiMetrics.Px(2f) + caption + UiMetrics.Px(8f) + caption + UiMetrics.Px(10f);
            return new CardMetrics(pane, sampleRow, line, title, caption, pane + below);
        }
    }
}
