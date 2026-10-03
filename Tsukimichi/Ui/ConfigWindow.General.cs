using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Config;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › General (feature plan v6 U7): Size (window scale, text size and icon size, each applying on the frame it
/// changes in every Tsukimichi window), Look (Decoration with its previews, moon style, moon colours with the legend,
/// Dalamud colours, game fonts for headings, Reduce motion with the Motion line), the main window's tabs, and the tour.
/// </summary>
public sealed partial class ConfigWindow
{
    private static readonly LocArray FlairOptions = new(static () => [Strings.ConfigFlairFull, Strings.ConfigFlairQuiet, Strings.ConfigFlairPlain]);
    private static readonly LocArray MoonStyleOptions = new(static () => [Strings.ConfigMoonStyleMedallion, Strings.ConfigMoonStyleClassic]);
    private static readonly LocArray PaletteOptions = new(static () => [Strings.ConfigGlyphPaletteStandard, Strings.ConfigGlyphPaletteHighContrast]);

    // The window-scale slider keeps its screen place and width while it is held: the scale applies live, so it would
    // otherwise move and resize under the pointer as it is dragged.
    private bool uiScaleHeld;
    private float uiScaleHeldX;
    private float uiScaleHeldWidth;

    /// <summary>
    /// Settings › General › Size: the window scale (live in every window as the slider moves; the slider holds its place
    /// while dragged), the text size (80–150% in 10% steps; the fonts rebuild at once, <see cref="Typography"/>), the
    /// icon size, and Reset. Each change is saved once it has been still a moment.
    /// </summary>
    private void DrawSize()
    {
        Header(Strings.SettingsSizeHeading);
        if (Setting(Strings.ConfigUiScale, Strings.ConfigUiScaleHint, "ui scale size zoom bigger smaller window layout"))
        {
            var uiScale = ScaleMetrics.ClampUiScale(settings.UiScale);
            if (uiScaleHeld)
            {
                ImGui.SetCursorScreenPos(new Vector2(uiScaleHeldX, ImGui.GetCursorScreenPos().Y));
            }

            var x = ImGui.GetCursorScreenPos().X;
            var width = uiScaleHeld ? uiScaleHeldWidth : ControlWidth;
            ImGui.SetNextItemWidth(width);
            if (ImGui.SliderFloat("##uiScale", ref uiScale, ScaleMetrics.MinUiScale, ScaleMetrics.MaxUiScale, "%.2f", ImGuiSliderFlags.AlwaysClamp))
            {
                settings.UiScale = uiScale;
                SaveSoon();
            }

            if (ImGui.IsItemActive())
            {
                if (!uiScaleHeld)
                {
                    uiScaleHeld = true;
                    uiScaleHeldX = x;
                    uiScaleHeldWidth = width;
                }
            }
            else
            {
                uiScaleHeld = false;
            }

            EndSetting();
        }

        if (Setting(Strings.SettingsTextSize, Strings.SettingsTextSizeHint, "text size font bigger smaller larger read"))
        {
            var percent = ScaleMetrics.TextScalePercent(settings.TextScale);
            ImGui.SetNextItemWidth(ControlWidth);
            var min = (int)MathF.Round(ScaleMetrics.MinTextScale * 100f);
            var max = (int)MathF.Round(ScaleMetrics.MaxTextScale * 100f);
            if (ImGui.SliderInt("##textScale", ref percent, min, max, "%d%%", ImGuiSliderFlags.AlwaysClamp))
            {
                var next = ScaleMetrics.TextScaleFromPercent(percent);
                if (MathF.Abs(next - ScaleMetrics.ClampTextScale(settings.TextScale)) > 0.001f)
                {
                    settings.TextScale = next;
                    SaveSoon();
                }
            }

            EndSetting();
        }

        if (Setting(Strings.ConfigIconScale, Strings.ConfigIconScaleHint, "icon size moons glyphs"))
        {
            var iconScale = ScaleMetrics.ClampIconScale(settings.IconScale);
            ImGui.SetNextItemWidth(ControlWidth);
            if (ImGui.SliderFloat("##iconScale", ref iconScale, ScaleMetrics.MinIconScale, ScaleMetrics.MaxIconScale, "%.2f", ImGuiSliderFlags.AlwaysClamp))
            {
                settings.IconScale = iconScale;
                SaveSoon();
            }

            EndSetting();
        }

        var isDefault = settings.UiScale == ScaleMetrics.DefaultUiScale && settings.IconScale == ScaleMetrics.DefaultIconScale && ScaleMetrics.ClampTextScale(settings.TextScale) == ScaleMetrics.DefaultTextScale;
        if (ButtonRow(Strings.SettingsResetSizes, Strings.SettingsResetSizesHint, Strings.SettingsResetButton, "reset default size scale", enabled: !isDefault))
        {
            settings.UiScale = ScaleMetrics.DefaultUiScale;
            settings.IconScale = ScaleMetrics.DefaultIconScale;
            settings.TextScale = ScaleMetrics.DefaultTextScale;
            Save();
        }
    }

    /// <summary>
    /// Settings › General › Look (moon-road proposal §7.9, feature plan v4 V1/V3, v6 G3 and U7): Decoration (Full, Quiet,
    /// Plain) with its previews, moon style, moon colours with the eight moons as a legend, Dalamud colours, game fonts
    /// for headings (not used under Plain) and Reduce motion with the Motion line. Saved at once and applied from the
    /// next frame (<see cref="Theme.Refresh"/>, <see cref="Typography.Update"/>).
    /// </summary>
    private void DrawLook()
    {
        Header(Strings.ConfigLook);
        var flair = Enum.IsDefined(settings.Flair) ? (int)settings.Flair : 0;
        if (Setting(Strings.ConfigFlair, Strings.ConfigFlairHint, "decoration flair full quiet plain moon road ornament style look", Chrome.SegmentedWidth(FlairOptions.Value), UiMetrics.MinTarget))
        {
            if (Chrome.Segmented("##flair", ref flair, FlairOptions.Value, ControlWidth))
            {
                settings.Flair = (Flair)flair;
                Save();
            }

            SettingBelow();
            DrawFlairPreviews();
            EndSetting();
        }

        var moonStyle = Enum.IsDefined(settings.MoonStyle) ? (int)settings.MoonStyle : 0;
        if (Choice(Strings.ConfigMoonStyle, Strings.ConfigMoonStyleHint, ref moonStyle, MoonStyleOptions.Value, "medallion classic moon medal glyph style compare"))
        {
            settings.MoonStyle = (MoonStyle)moonStyle;
            Save();
        }

        DrawGlyphPalette();

        var followDalamud = settings.FollowDalamudColours;
        if (Toggle(Strings.ConfigFollowDalamudColours, Strings.ConfigFollowDalamudColoursHint, ref followDalamud, "colors colours theme style night palette"))
        {
            settings.FollowDalamudColours = followDalamud;
            Save();
        }

        var headingFonts = settings.GameHeadingFonts;
        if (Toggle(Strings.ConfigGameHeadingFonts, Strings.ConfigGameHeadingFontsHint, ref headingFonts, "font typeface headings", enabled: settings.Flair != Flair.Plain, reason: Strings.SettingsGameFontsPlainReason))
        {
            settings.GameHeadingFonts = headingFonts;
            Save();
        }

        if (ToggleSetting(Strings.ConfigReduceMotion, Strings.ConfigReduceMotionHint, "animation accessibility motion windows"))
        {
            var reduceMotion = settings.ReduceMotion;
            if (RowToggle(ref reduceMotion))
            {
                settings.ReduceMotion = reduceMotion;
                settings.ReduceMotionChosen = true;
                Save();
            }

            DrawMotionStatus();
            EndSetting();
        }
    }

    /// <summary>Settings › General › Main window: icon-only tabs.</summary>
    private void DrawMainWindow()
    {
        Header(Strings.SettingsMainWindowHeading);
        var compactRail = settings.CompactRail;
        if (Toggle(Strings.ConfigCompactRail, Strings.ConfigCompactRailHint, ref compactRail, "tabs rail sidebar icons compact"))
        {
            settings.CompactRail = compactRail;
            Save();
        }
    }

    /// <summary>Settings › General › Help: start the tour, and whether it is offered when the main window next opens.</summary>
    private void DrawHelp()
    {
        Header(Strings.ConfigSectionHelp);
        if (StartTutorial is { } startTutorial && ButtonRow(Strings.ConfigStartTutorial, Strings.SettingsStartTutorialHint, Strings.SettingsStartButton, "help guide tour tutorial"))
        {
            startTutorial();
        }

        var offer = !settings.TutorialCompleted && settings.TutorialLaterCount < TutorialOverlay.LaterLimit;
        if (Toggle(Strings.ConfigOfferTutorial, Strings.ConfigOfferTutorialHint, ref offer, "tour tutorial welcome first run"))
        {
            settings.TutorialCompleted = !offer;
            settings.TutorialLaterCount = 0;
            Save();
        }
    }

    /// <summary>How often the Motion line re-reads Windows' "Show animations" switch while Settings is drawn.</summary>
    private const double OsMotionRereadSeconds = 2.0;

    private double osMotionReadAt = double.NegativeInfinity;
    private bool? osAnimationsOff;

    /// <summary>
    /// The Motion line under Reduce motion (feature plan v6 decision 3): whether Tsukimichi animates and why, with a
    /// one-click override for Tsukimichi alone when Windows' "Show animations" switch is what turned it off, and the way
    /// back to following Windows once overridden. The switch is re-read every <see cref="OsMotionRereadSeconds"/> while
    /// Settings is open; until the player chooses, Reduce motion follows it live, as a load would.
    /// </summary>
    private void DrawMotionStatus()
    {
        var now = ImGui.GetTime();
        if (now - osMotionReadAt >= OsMotionRereadSeconds || now < osMotionReadAt)
        {
            osMotionReadAt = now;
            osAnimationsOff = OsMotion.AnimationsOff();
            if (!settings.ReduceMotionChosen && osAnimationsOff is { } off)
            {
                settings.ReduceMotion = off;
            }
        }

        var status = MotionStatus.Of(settings.ReduceMotion, settings.ReduceMotionChosen, osAnimationsOff);
        SettingNote(status.Text, status.On ? Theme.Surface.TextSecondary : Theme.Surface.TextTertiary);
        if (status.Fix == MotionFix.None || !ImGui.SmallButton(status.FixLabel + "##motionFix"))
        {
            return;
        }

        if (status.Fix == MotionFix.AnimateAnyway)
        {
            settings.ReduceMotion = false;
            settings.ReduceMotionChosen = true;
        }
        else
        {
            settings.ReduceMotionChosen = false;
            settings.ReduceMotion = OsMotion.AnimationsOff() ?? false;
        }

        Save();
    }

    /// <summary>
    /// Live previews of the three Decoration levels (proposal §7.9), side by side when three cells of
    /// <see cref="PreviewCellLogical"/> fit and stacked otherwise: each a section heading and a small journal row drawn
    /// exactly as that level draws them. Under the high-contrast palette Full previews as Quiet, as it draws. The level in
    /// use is outlined; a click on a preview chooses it. The game's heading fonts are not built at Plain, so while Plain
    /// is chosen a note under the previews says the Full and Quiet ones show the usual font until another is chosen.
    /// </summary>
    private void DrawFlairPreviews()
    {
        var room = ImGui.GetContentRegionAvail().X;
        var gap = UiMetrics.Px(10f);
        var side = room >= (3f * UiMetrics.Px(PreviewCellLogical)) + (2f * gap);
        var cell = side ? MathF.Floor((room - (2f * gap)) / 3f) : MathF.Min(room, UiMetrics.Px(260f));
        var origin = ImGui.GetCursorScreenPos();
        var contentRight = origin.X + room;
        var bottom = origin.Y;
        var dl = ImGui.GetWindowDrawList();
        var pad = UiMetrics.Px(6f);
        var levels = PreviewLevels;
        for (var i = 0; i < levels.Length; i++)
        {
            var level = levels[i];
            var flair = FlairRules.Effective(level, Theme.Glyphs.HighContrast);
            var min = side ? new Vector2(origin.X + (i * (cell + gap)), origin.Y) : new Vector2(origin.X, bottom);
            ImGui.PushID(i);
            dl.ChannelsSplit(2);
            dl.ChannelsSetCurrent(1);
            ImGui.SetCursorScreenPos(min + new Vector2(pad));
            ImGui.BeginGroup();
            using (Typography.Caption())
            {
                ImGui.TextDisabled(level switch
                {
                    Flair.Full => Strings.ConfigFlairFull,
                    Flair.Quiet => Strings.ConfigFlairQuiet,
                    _ => Strings.ConfigFlairPlain,
                });
            }

            SectionHeading.Draw(Strings.CharactersSectionCompletion, null, contentRight - (min.X + cell - pad), true, flair);
            PreviewRow(dl, flair, cell - (2f * pad));
            ImGui.EndGroup();
            var max = new Vector2(min.X + cell, ImGui.GetItemRectMax().Y + pad);

            // Behind the content: the pane gradient at Full, the outline of the level in use.
            dl.ChannelsSetCurrent(0);
            dl.AddRectFilled(min, max, Theme.U32(Theme.Surface.Window), UiMetrics.Px(4f));
            if (FlairRules.PaneGradient(flair))
            {
                Ornament.PaneGradient(dl, min, max);
            }

            var chosen = settings.Flair == level;
            dl.AddRect(min, max, chosen ? Theme.U32(Theme.Surface.Text) : Theme.U32(Theme.Surface.Line), UiMetrics.Px(4f), ImDrawFlags.None, chosen ? MathF.Max(1.5f, UiMetrics.Px(1.5f)) : UiMetrics.Hairline);
            dl.ChannelsMerge();

            ImGui.SetCursorScreenPos(min);
            if (ImGui.InvisibleButton("##flairPreview", max - min) && !chosen)
            {
                settings.Flair = level;
                Save();
            }

            HintOnHover(Strings.ConfigFlairHint);
            ImGui.PopID();
            bottom = side ? MathF.Max(bottom, max.Y) : max.Y + gap;
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, bottom + (side ? gap : 0f)));
        ImGui.Dummy(Vector2.Zero);
        if (settings.Flair == Flair.Plain && settings.GameHeadingFonts)
        {
            using (Typography.Caption())
            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                ImGui.TextWrapped(Strings.ConfigFlairPreviewPlainNote);
            }
        }
    }

    /// <summary>The least width of a Decoration preview cell side by side.</summary>
    private const float PreviewCellLogical = 140f;

    private static readonly Flair[] PreviewLevels = [Flair.Full, Flair.Quiet, Flair.Plain];

    /// <summary>A small journal row drawn as <paramref name="flair"/> draws it: an orbit, the name, the count and the road, or the filling moon at Plain.</summary>
    private static void PreviewRow(ImDrawListPtr dl, Flair flair, float width)
    {
        const float Fraction = 0.65f;
        var line = ImGui.GetTextLineHeight();
        var box = MathF.Round(MathF.Max(line, UiMetrics.Icon(22f)));
        var road = MathF.Max(1f, UiMetrics.Px(2f));
        var min = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(MathF.Max(1f, width), box + (2f * road)));
        var art = FlairRules.Rules(flair);
        var textures = Plugin.TextureProvider;
        if (art && textures is not null)
        {
            Orbit.Draw(dl, textures, min, box, NodeIcon.Of(OrnamentGlyph.AllQuests), Fraction, highContrast: Theme.Glyphs.HighContrast);
        }
        else
        {
            MoonGlyph.DrawHalo(dl, min + new Vector2(box * 0.5f), box * 0.5f, Fraction);
        }

        var s = Theme.Surface;
        var textX = min.X + box + UiMetrics.Px(6f);
        var textY = min.Y + MathF.Max(0f, (box - line) * 0.5f);
        const string Count = "65%";
        float countWidth;
        if (art)
        {
            using var numeral = Typography.Numeral(Count);
            countWidth = ImGui.CalcTextSize(Count).X;
            dl.AddText(new Vector2(min.X + width - countWidth, textY), Theme.U32(s.TextSecondary), Count);
        }
        else
        {
            countWidth = ImGui.CalcTextSize(Count).X;
            dl.AddText(new Vector2(min.X + width - countWidth, textY), Theme.U32(s.TextSecondary), Count);
        }

        Chrome.EllipsisTextAt(dl, new Vector2(textX, textY), MathF.Max(0f, min.X + width - countWidth - UiMetrics.Px(6f) - textX), Strings.CharactersAllQuests, Theme.U32(s.Text));
        if (!art)
        {
            return;
        }

        // The road under the row: the walked part gold, the rest the track (solid colours under high contrast).
        var y = min.Y + box + road;
        var walked = textX + ((min.X + width - textX) * Fraction);
        var highContrast = Theme.Glyphs.HighContrast;
        dl.AddRectFilled(new Vector2(textX, y), new Vector2(min.X + width, y + road), highContrast ? Theme.VeilLineU32 : Theme.NightLineU32);
        if (highContrast)
        {
            dl.AddRectFilled(new Vector2(textX, y), new Vector2(walked, y + road), Theme.MoonU32);
        }
        else
        {
            dl.AddRectFilledMultiColor(new Vector2(textX, y), new Vector2(walked, y + road), Theme.MoonDeepU32, Theme.MoonU32, Theme.MoonU32, Theme.MoonDeepU32);
        }
    }

    /// <summary>States in the order the moon colours legend shows them (the Help legend's order).</summary>
    private static readonly QuestState[] PalettePreviewStates =
    [
        QuestState.Completed, QuestState.Accepted, QuestState.Ready, QuestState.ReadyOnOtherJob,
        QuestState.DoneThisCycle, QuestState.Blocked, QuestState.Foreclosed, QuestState.Unknown,
    ];

    /// <summary>
    /// Settings › General › Moon colours (accessibility panel §2.2): Standard or High contrast, saved at once and applied
    /// from the next frame (<see cref="Theme.Refresh"/>), with the eight state moons under it in the palette in effect so
    /// the choice can be seen before closing the window. Each moon's tooltip names its state and shape.
    /// </summary>
    private void DrawGlyphPalette()
    {
        if (!Setting(Strings.ConfigGlyphPalette, Strings.ConfigGlyphPaletteHint, "moon colours colors palette standard high contrast colour blind accessibility glyph", Chrome.SegmentedWidth(PaletteOptions.Value), UiMetrics.MinTarget))
        {
            return;
        }

        var palette = settings.GlyphPalette == GlyphPaletteKind.HighContrast ? 1 : 0;
        if (Chrome.Segmented("##palette", ref palette, PaletteOptions.Value, ControlWidth))
        {
            settings.GlyphPalette = palette == 1 ? GlyphPaletteKind.HighContrast : GlyphPaletteKind.Standard;
            Save();
        }

        SettingBelow();
        var glyph = UiMetrics.InlineGlyphSize(ImGui.GetTextLineHeight());
        var gap = UiMetrics.Px(6f);
        for (var i = 0; i < PalettePreviewStates.Length; i++)
        {
            if (i > 0)
            {
                ImGui.SameLine(0f, gap);
            }

            MoonGlyph.DrawInline(PalettePreviewStates[i], glyph);
            HintOnHover(Strings.StateTooltip(PalettePreviewStates[i]));
        }

        EndSetting();
    }
}
