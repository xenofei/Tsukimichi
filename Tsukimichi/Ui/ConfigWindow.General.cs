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

            DrawFlairPreview(settings.Flair);
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
    /// The live preview under the Decoration picker (docs/design/flair-v13 §2): a 3-up miniature of the three looks, Full,
    /// Quiet and Plain side by side in the picker's order, each drawn by the same code the main window draws with
    /// (<see cref="Theme.PushFlair"/>), so the choice is visible before it is made. The chosen look is ringed in the
    /// accent and its name is in the text tone; the others' names are quieter. Each miniature is a quest table (its
    /// header and three rows at the level's height with their medals, a Ready row and the selected row as the level draws
    /// them) over a card, on the level's pane tones. A miniature can be clicked to choose its look, and names it on
    /// hover. Right-aligned in the control column (under the label on a narrow page), at one fixed size, so choosing
    /// another look never moves the page. The chosen look's one line follows, and while game fonts are on at another
    /// level, that only Full uses them. Under the high-contrast palette Full previews as Quiet, as it draws.
    /// </summary>
    private void DrawFlairPreview(Flair level)
    {
        var top = ImGui.GetItemRectMax().Y + UiMetrics.Px(8f);
        var right = row.Right;
        var width = MathF.Min(MathF.Max(ControlWidth, UiMetrics.Px(PreviewWidthLogical)), right - row.Left);
        var gap = UiMetrics.Px(8f);
        var tileWidth = MathF.Floor(MathF.Max(1f, (width - (gap * 2f)) / 3f));
        var height = UiMetrics.Px(PreviewHeightLogical);
        float captionLine;
        using (Typography.Caption())
        {
            captionLine = ImGui.GetTextLineHeight();
        }

        var captionGap = UiMetrics.Px(4f);

        // Right-aligned under the picker, and never over the label or its hint.
        var min = new Vector2(MathF.Round(right - width), MathF.Round(MathF.Max(top, row.LabelBottom + UiMetrics.Px(8f))));
        var dl = ImGui.GetWindowDrawList();
        var s = Theme.Surface;
        var options = FlairOptions.Value;
        for (var i = 0; i < PreviewLooks.Length; i++)
        {
            var look = PreviewLooks[i];
            var chosen = look == level;
            var tileMin = new Vector2(min.X + (i * (tileWidth + gap)), min.Y);
            var tileMax = tileMin + new Vector2(tileWidth, height);
            dl.PushClipRect(tileMin, tileMax, true);
            using (Theme.PushFlair(look))
            {
                DrawPreviewSample(dl, tileMin, tileMax);
            }

            dl.PopClipRect();

            ImGui.SetCursorScreenPos(tileMin);
            ImGui.PushID(i);
            var clicked = ImGui.InvisibleButton("##flairLook", new Vector2(tileWidth, height + captionGap + captionLine));
            ImGui.PopID();
            var hovered = ImGui.IsItemHovered();

            // The chosen look ringed in the accent; the others a hairline, a little stronger under the pointer.
            var rounding = UiMetrics.Px(4f);
            if (chosen)
            {
                var ring = MathF.Max(2f, MathF.Round(UiMetrics.Px(2f)));
                dl.AddRect(tileMin - new Vector2(ring * 0.5f), tileMax + new Vector2(ring * 0.5f), Theme.Glyphs.HighContrast ? Theme.U32(s.Text) : Theme.AccentU32, rounding, ImDrawFlags.None, ring);
            }
            else
            {
                dl.AddRect(tileMin, tileMax, Theme.U32(hovered ? s.TextTertiary : s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
            }

            using (Typography.Caption())
            {
                var name = i < options.Length ? options[i] : string.Empty;
                var nameWidth = MathF.Min(ImGui.CalcTextSize(name).X, tileWidth);
                var at = new Vector2(MathF.Round(tileMin.X + ((tileWidth - nameWidth) * 0.5f)), tileMax.Y + captionGap);
                Chrome.EllipsisTextAt(dl, at, tileWidth, name, Theme.U32(chosen ? s.Text : s.TextTertiary));
            }

            if (hovered)
            {
                UiMetrics.Tooltip(LookNote(look));
            }

            if (clicked && !chosen)
            {
                settings.Flair = look;
                Save();
            }
        }

        ImGui.SetCursorScreenPos(new Vector2(min.X, min.Y + height + captionGap + captionLine + UiMetrics.Px(4f)));
        ImGui.Dummy(new Vector2(width, 1f));

        // The look in one line, then the game-font note while it applies.
        var note = LookNote(level);
        using (Typography.Caption())
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.SetCursorScreenPos(new Vector2(min.X, ImGui.GetCursorScreenPos().Y));
            ImGui.PushTextWrapPos(min.X - ImGui.GetWindowPos().X + width);
            ImGui.TextUnformatted(note);
            if (level != Flair.Full && settings.GameHeadingFonts)
            {
                ImGui.TextUnformatted(Strings.ConfigFlairPreviewPlainNote);
            }

            ImGui.PopTextWrapPos();
        }
    }

    /// <summary>The look's one line, as drawn (under the high-contrast palette Full draws as Quiet).</summary>
    private static string LookNote(Flair look) => FlairRules.Effective(look, Theme.Glyphs.HighContrast) switch
    {
        Flair.Full => Strings.ConfigFlairFullNote,
        Flair.Quiet => Strings.ConfigFlairQuietNote,
        _ => Strings.ConfigFlairPlainNote,
    };

    /// <summary>The three miniatures, in the picker's order.</summary>
    private static readonly Flair[] PreviewLooks = [Flair.Full, Flair.Quiet, Flair.Plain];

    /// <summary>
    /// The preview's size, logical px: three miniatures of about 130 each with their gaps, each as tall as the header,
    /// three Full rows (the tallest) and a card under them, with a little air.
    /// </summary>
    private const float PreviewWidthLogical = 420f;

    private const float PreviewHeightLogical = 212f;

    /// <summary>The sample's three rows: a Ready quest, the selected quest in the journal, a completed one.</summary>
    private static readonly QuestState[] PreviewStates = [QuestState.Ready, QuestState.Accepted, QuestState.Completed];

    /// <summary>Full's star field in the preview's sky: seeded once, so it never shimmers.</summary>
    private static readonly Star[] PreviewStars = StarField.Generate(53, 6);

    /// <summary>
    /// One miniature, at the level pushed: the panes' tones (or the sky and its stars), the table's header and rows on
    /// top, and a card on the detail tone under them. Draw-list only; nothing allocates (the strings are the language's
    /// own, the headings cased once).
    /// </summary>
    private static void DrawPreviewSample(ImDrawListPtr dl, Vector2 min, Vector2 max)
    {
        var s = Theme.Surface;
        var tones = Theme.Tones;
        var flair = Theme.Flair;
        var pad = UiMetrics.Px(8f);

        // Where the table ends and the detail begins: under the header and three rows at the level's height.
        var x0 = min.X + (flair == Flair.Plain ? UiMetrics.Px(4f) : pad);
        var x1 = max.X - UiMetrics.Px(4f);
        var line = ImGui.GetTextLineHeight();
        var header = MathF.Round(MathF.Max(line, UiMetrics.Px(flair == Flair.Plain ? 20f : 24f)));
        var rowHeight = MathF.Round(ScaleMetrics.TableRowTarget(flair, RowDensity.Comfortable) * UiMetrics.Scale);
        var y = min.Y + (flair == Flair.Plain ? 0f : UiMetrics.Px(4f));
        var split = MathF.Round(y + header + UiMetrics.Px(flair == Flair.Plain ? 0f : 2f) + (rowHeight * PreviewStates.Length) + UiMetrics.Px(4f));

        // The panes: the sky over water at Full, two tones at Quiet (the table and the lighter detail), flat at Plain.
        dl.AddRectFilled(min, max, Theme.U32(Theme.ShowPaneGradient ? s.Window : tones.Table));
        if (Theme.ShowPaneGradient)
        {
            Ornament.SkyOverWater(dl, min, max);
        }
        else
        {
            dl.AddRectFilled(new Vector2(min.X, split), max, Theme.U32(tones.Detail));
        }

        if (Theme.RuleStyle != RuleStyle.MoonRoad)
        {
            dl.AddRectFilled(new Vector2(min.X, split), new Vector2(max.X, split + 1f), Theme.U32(Theme.RuleColor));
        }

        // The table's header.
        DrawPreviewHeader(dl, new Vector2(x0, y), new Vector2(x1, y + header));
        y += header + UiMetrics.Px(flair == Flair.Plain ? 0f : 2f);

        // Three rows at the level's height.
        var content = rowHeight - UiMetrics.Px(4f);
        var radius = TableGeometry.GlyphRadius(content, UiMetrics.TableGlyphRadius);
        for (var i = 0; i < PreviewStates.Length; i++)
        {
            var rowMin = new Vector2(x0, y);
            var rowMax = new Vector2(x1, y + rowHeight);
            DrawPreviewRow(dl, rowMin, rowMax, PreviewStates[i], i, radius);
            y += rowHeight;
        }

        // A card on the detail side, as the level frames it, with Full's stars in the sky over it.
        var cardTop = split + UiMetrics.Px(flair == Flair.Plain ? 6f : 12f);
        if (Theme.ShowStars)
        {
            Ornament.Stars(dl, new Vector2(min.X + pad, split + UiMetrics.Px(1f)), new Vector2(max.X - pad, cardTop - UiMetrics.Px(1f)), PreviewStars);
        }

        var cardMin = new Vector2(min.X + pad, cardTop);
        var cardMax = new Vector2(max.X - pad, cardMin.Y + (line * 2f) + UiMetrics.Px(flair == Flair.Plain ? 8f : 24f));
        Chrome.CardSurface(dl, cardMin, cardMax);
        var inset = flair == Flair.Plain ? Vector2.Zero : new Vector2(UiMetrics.Px(Theme.Spacing.CardPad.X), UiMetrics.Px(Theme.Spacing.CardPad.Y));
        var textMin = cardMin + inset;
        var room = MathF.Max(1f, cardMax.X - inset.X - textMin.X);
        var heading = Theme.MoonRoadArt ? SectionHeading.Label(Strings.ConfigFlairPreviewCard) : Strings.ConfigFlairPreviewCard;
        using (Theme.MoonRoadArt ? Typography.Eyebrow(heading) : default)
        {
            Chrome.EllipsisTextAt(dl, textMin, room, heading, Theme.U32(Theme.MoonRoadArt ? s.OrnamentHigh : s.Text));
            textMin.Y += ImGui.GetTextLineHeight() + UiMetrics.Px(3f);
        }

        if (flair == Flair.Plain)
        {
            dl.AddRectFilled(new Vector2(cardMin.X, textMin.Y - UiMetrics.Px(2f)), new Vector2(cardMax.X, textMin.Y - UiMetrics.Px(1f)), Theme.U32(Theme.RuleColor));
        }

        var dot = UiMetrics.Px(3f);
        dl.AddCircleFilled(new Vector2(textMin.X + dot, textMin.Y + (line * 0.5f)), dot, Theme.MoonDeepU32, 12);
        Chrome.EllipsisTextAt(dl, new Vector2(textMin.X + (dot * 2f) + UiMetrics.Px(6f), textMin.Y), MathF.Max(1f, room - (dot * 2f) - UiMetrics.Px(6f)), Strings.ConfigFlairPreviewCardLine, Theme.U32(s.Text));
    }

    /// <summary>The sample table's header: Full's tracked caps over a brass rule, Quiet's caption over a hairline, Plain's band.</summary>
    private static void DrawPreviewHeader(ImDrawListPtr dl, Vector2 min, Vector2 max)
    {
        var s = Theme.Surface;
        var style = Theme.TableHeader;
        var moonRoad = style == TableHeaderStyle.MoonRoad;
        if (style == TableHeaderStyle.Raised)
        {
            dl.AddRectFilled(min, max, Theme.U32(Theme.Tones.HeaderBand));
        }

        var name = moonRoad ? SectionHeading.Label(Strings.ColumnName) : Strings.ColumnName;
        using (moonRoad ? Typography.Eyebrow(name) : Typography.Caption())
        {
            var textY = MathF.Round((min.Y + max.Y - ImGui.GetTextLineHeight()) * 0.5f);
            var lead = UiMetrics.Px(26f);
            Chrome.EllipsisTextAt(dl, new Vector2(min.X + lead, textY), MathF.Max(1f, max.X - min.X - lead), name, Theme.U32(moonRoad ? s.OrnamentHigh : s.Text));
        }

        switch (style)
        {
            case TableHeaderStyle.MoonRoad when !Theme.Glyphs.HighContrast:
            {
                // The brass rule, brightest at 18 % across.
                var at = min.X + ((max.X - min.X) * 0.18f);
                var edge = Theme.WithAlpha(s.Ornament, 0f);
                var peak = Theme.WithAlpha(s.OrnamentHigh, 0.8f);
                var tail = Theme.WithAlpha(s.Ornament, 0.4f);
                dl.AddRectFilledMultiColor(new Vector2(min.X, max.Y - 1f), new Vector2(at, max.Y), edge, peak, peak, edge);
                dl.AddRectFilledMultiColor(new Vector2(at, max.Y - 1f), new Vector2(max.X, max.Y), peak, tail, tail, peak);
                break;
            }

            default:
                dl.AddRectFilled(new Vector2(min.X, max.Y - 1f), max, Theme.U32(style == TableHeaderStyle.Raised ? Theme.Tones.HeaderLine : Theme.RuleColor));
                break;
        }
    }

    /// <summary>One sample row: its stripe, zebra and selection as the level draws them, the medal and the name.</summary>
    private static void DrawPreviewRow(ImDrawListPtr dl, Vector2 min, Vector2 max, QuestState state, int index, float radius)
    {
        var s = Theme.Surface;
        var flair = Theme.Flair;
        var selected = state == QuestState.Accepted;
        if (flair == Flair.Plain && index % 2 == 1)
        {
            dl.AddRectFilled(min, max, Theme.WithAlpha(s.Text, 0.03f));
        }

        if (selected)
        {
            switch (flair)
            {
                case Flair.Full:
                    dl.AddRectFilledMultiColor(min, max, Theme.WithAlpha(Theme.Moon, 0.13f), Theme.WithAlpha(Theme.Moon, 0.02f), Theme.WithAlpha(Theme.Moon, 0.02f), Theme.WithAlpha(Theme.Moon, 0.13f));
                    dl.AddRectFilled(min, new Vector2(max.X, min.Y + 1f), Theme.WithAlpha(s.OrnamentHigh, 0.35f));
                    dl.AddRectFilled(new Vector2(min.X, max.Y - 1f), max, Theme.WithAlpha(s.OrnamentHigh, 0.35f));
                    break;
                case Flair.Quiet:
                    dl.AddRectFilled(min, max, Theme.WithAlpha(s.Text, 0.06f));
                    dl.AddRect(min, max, Theme.U32(Theme.Glyphs.HighContrast ? s.Text : Theme.Veil), 0f, ImDrawFlags.None, 1f);
                    break;
                default:
                    dl.AddRectFilled(min, max, Theme.WithAlpha(s.Text, 0.09f));
                    break;
            }
        }
        else
        {
            dl.AddRectFilled(new Vector2(min.X, max.Y - 1f), max, Theme.U32(flair == Flair.Full ? s.Line with { W = 0.35f } : Theme.Tones.Rule with { W = 0.6f }));
        }

        // The state stripe at the left edge (3 px at Full, 2 elsewhere), in the state's colour.
        var stripe = MathF.Max(2f, MathF.Round(UiMetrics.Px(flair == Flair.Full ? 3f : 2f)));
        dl.AddRectFilled(min, new Vector2(min.X + stripe, max.Y), Theme.WithAlpha(Theme.StateColor(state), state == QuestState.Completed ? 0.55f : 1f));

        var center = new Vector2(MathF.Round(min.X + UiMetrics.Px(14f)), MathF.Round((min.Y + max.Y) * 0.5f));
        if (Theme.MoonRoadArt && !Theme.ClassicMoons)
        {
            if (state == QuestState.Ready && Theme.ShowGlow)
            {
                dl.AddCircleFilled(center, radius + UiMetrics.Px(4f), Theme.WithAlpha(Theme.Moon, 0.10f), 32);
                dl.AddCircleFilled(center, radius + UiMetrics.Px(2f), Theme.WithAlpha(Theme.Moon, 0.16f), 32);
            }

            dl.AddCircleFilled(center + new Vector2(0f, UiMetrics.Px(1.5f)), radius, Theme.WithAlpha(Theme.Abyss, 0.55f), 24);
        }

        MoonGlyph.Draw(dl, center, radius, state);

        var line = ImGui.GetTextLineHeight();
        var textY = MathF.Round((min.Y + max.Y - line) * 0.5f);
        var nameX = center.X + UiMetrics.Px(14f);
        var name = state switch
        {
            QuestState.Ready => Strings.ConfigFlairPreviewReady,
            QuestState.Accepted => Strings.ConfigFlairPreviewJournal,
            _ => Strings.ConfigFlairPreviewDone,
        };
        Chrome.EllipsisTextAt(dl, new Vector2(nameX, textY), MathF.Max(1f, max.X - nameX - UiMetrics.Px(4f)), name, Theme.U32(state == QuestState.Completed ? s.TextSecondary : s.Text));

        if (state == QuestState.Ready && FlairRules.ReadyRoad(flair))
        {
            Ornament.Rule(dl, new Vector2(nameX, max.Y - 1f), (max.X - nameX) * 0.7f, 0.55f, 1f, Theme.Moon);
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
