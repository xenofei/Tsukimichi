using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The main window's rail (feature plan v4 L7, design v4 §7.1), drawn in its own fixed pane left of the tree
/// (<see cref="PaneSplit"/>). Top to bottom: the crest (a click shows Journal › All quests), one station per tab (a
/// 22 px icon over a small label, the Journal station carrying the Ready count as a badge), and at the foot the overall
/// gauge with its percentage and the round Help and Settings buttons, which live here rather than in the toolbar. The
/// rail is 64 logical px wide; on a window under about 1,040 px, or by Settings › Display › Compact rail, it is a 44 px
/// compact rail of icons whose labels are in the tooltips (<see cref="LayoutBudgets.CompactRail"/>). On a short window
/// the rail gives up height in a fixed order (<see cref="LayoutBudgets.FitRail"/>) and scrolls only past that.
/// <para>
/// The Moon Road look (feature plan v4 V2), under Full and Quiet flair: the rail sits on the deepest surface (Abyss);
/// the crest is the ornament atlas's moon over night water with a fading brass rule under it; the stations are strung
/// on a thin brass thread, and the active one is lit by a short gold bar on the thread with a moon bead at its head
/// (with a soft glow behind its icon under Full). The Journal station and the foot gauge are orbits round a filling
/// moon; Moonlit and Flight use the kit's glyphs and My blues the game's blue unlock-quest marker (FontAwesome stands
/// in while a texture loads, and for Characters). Under Full flair with motion on, the orbits fill when first shown
/// and the active station's bar lights over half a second after a tab change. Plain flair keeps the 1.3 rail.
/// </para>
/// <para>
/// Each station is a real item (an <see cref="ImGui.InvisibleButton(string, Vector2)"/> with the focus ring), so
/// keyboard and gamepad navigation reach it. <see cref="UiState.Tab"/> is the single source of truth: a click writes
/// it, and a programmatic switch (Reveal, ShowIssuer, the tutorial, help) is simply the next frame's active station.
/// The union of the stations is recorded as <see cref="UiRects.Tabs"/>, and the foot's buttons as
/// <see cref="UiRects.HelpButton"/> and <see cref="UiRects.SettingsButton"/>, for the tutorial. Nothing allocates per
/// frame: labels are constants measured once per language and font size, and the tooltips and the percentage are
/// rebuilt only when their counts or the language change. Everything is placed by <see cref="LayoutBudgets.PlaceRail"/>,
/// so a rail that fits its pane never overflows it by a rounding pixel and turns wheel-scrollable.
/// </para>
/// </summary>
public sealed class TabStrip
{
    private static readonly NavTab[] Tabs = [NavTab.Journal, NavTab.Moonlit, NavTab.Characters, NavTab.Flight, NavTab.Plan];

    private static string[] Labels => labelsText.Value;

    private static readonly Localization.LocArray labelsText = new(static () =>
        [Strings.TabJournal, Strings.TabMoonlit, Strings.TabCharacters, Strings.TabFlight, Strings.PlanTab]);

    private static readonly string[] Ids = ["##tabJournal", "##tabMoonlit", "##tabCharacters", "##tabFlight", "##tabPlan"];

    private static string[] Tooltips => tooltipsText.Value;

    private static readonly Localization.LocArray tooltipsText = new(static () =>
        [Strings.TabJournalTooltip, Strings.TabMoonlitTooltip, Strings.TabCharactersTooltip, Strings.TabFlightTooltip, Strings.PlanTabTooltip]);

    private static readonly string GemIcon = FontAwesomeIcon.Gem.ToIconString();
    private static readonly string UsersIcon = FontAwesomeIcon.Users.ToIconString();
    private static readonly string PlaneIcon = FontAwesomeIcon.Plane.ToIconString();
    private static readonly string PlanIcon = FontAwesomeIcon.ClipboardList.ToIconString();
    private static readonly string HelpIcon = FontAwesomeIcon.QuestionCircle.ToIconString();
    private static readonly string SettingsIcon = FontAwesomeIcon.Cog.ToIconString();

    /// <summary>The overall gauge's fill motion (it moves only when the count changes, or fills once under Full flair).</summary>
    private static readonly ulong GaugeKey = Motion.Key(0x5241_494C, 0); // "RAIL"

    /// <summary>The Journal station's orbit fill.</summary>
    private static readonly ulong JournalKey = Motion.Key(0x5241_494C, 1);

    /// <summary>The active station lighting up after a tab change.</summary>
    private static readonly ulong StationKey = Motion.Key(0x5241_494C, 2);

    /// <summary>The Moon Road rail's sizes: the lit bar on the thread, the gap between the thread and an icon or label, the crest rule.</summary>
    private const float LitBarLogical = 22f;
    private const float ThreadGapLogical = 3f;
    private const float ThreadAlpha = 0.35f;
    private const float CrestRuleLogical = 40f;
    private const float GlowRadiusLogical = 18f;

    /// <summary>Station icon alpha by state (the kit's glyphs and the game icon carry their own colours).</summary>
    private const float IdleIconAlpha = 0.72f;
    private const float HoverIconAlpha = 0.9f;

    private readonly UiState ui;

    private int readyTooltipCount = -1;
    private int readyTooltipLanguage = -1;
    private string readyTooltip = string.Empty;

    // The labels' widths at their own size, measured again only when the language or the font size changes.
    private readonly float[] labelWidths = new float[Tabs.Length];
    private int labelsLanguage = -1;
    private float labelsFontSize = -1f;

    // The foot's texts, rebuilt when the overall count or the language changes.
    private NodeCount gaugeCount = new(-1, -1, 0);
    private int gaugeLanguage = -1;
    private string percentText = string.Empty;
    private float percentWidth;
    private string progressText = string.Empty;

    /// <summary>The tab the lit bar last lit for; a change starts the light-up.</summary>
    private NavTab? litTab;

    public TabStrip(UiState ui)
    {
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
    }

    /// <summary>Whether the rail is the compact icon rail this frame (<see cref="UpdateMode"/>).</summary>
    public bool Compact { get; private set; }

    /// <summary>The rail's logical width: <see cref="ScaleMetrics.RailLogical"/>, or <see cref="ScaleMetrics.RailCompactLogical"/> while compact.</summary>
    public float RailLogicalWidth => Compact ? ScaleMetrics.RailCompactLogical : ScaleMetrics.RailLogical;

    /// <summary>The rail's width in pixels.</summary>
    public float RailWidth => MathF.Round(UiMetrics.Px(RailLogicalWidth));

    /// <summary>
    /// Decides the rail's mode for this frame (<see cref="LayoutBudgets.CompactRail"/>): compact when the user chose
    /// it, or while the main window is narrow.
    /// </summary>
    /// <param name="windowWidth">The main window's width in Dalamud-scaled units (pixels over the global scale).</param>
    /// <param name="uiScale">The UI scale.</param>
    /// <param name="forced">Settings › Display › Compact rail.</param>
    public void UpdateMode(float windowWidth, float uiScale, bool forced) =>
        Compact = LayoutBudgets.CompactRail(windowWidth, uiScale, Compact, forced);

    /// <summary>
    /// Draws the rail from the cursor down, filling the current (rail) child window: crest, stations and foot.
    /// </summary>
    /// <param name="overall">Overall completion for the Journal moon and the foot's gauge (0 / 0 with no character).</param>
    /// <param name="ready">Ready quests for the viewed character; 0 hides the Journal badge.</param>
    /// <param name="openHelp">Opens the help window; null draws the button disabled.</param>
    /// <param name="openSettings">Opens Settings; null draws the button disabled.</param>
    public void Draw(NodeCount overall, int ready, Action? openHelp, Action? openSettings)
    {
        var origin = ImGui.GetCursorScreenPos();
        var avail = ImGui.GetContentRegionAvail();
        var width = MathF.Max(1f, avail.X);
        // Everything is placed by the fit, with the foot's buttons at the size they are drawn and whole-pixel stations,
        // so a rail that fits its pane ends inside it and never becomes wheel-scrollable.
        var place = LayoutBudgets.PlaceRail(avail.Y, UiMetrics.Scale, Tabs.Length, Compact, UiMetrics.MinTarget);
        var dl = ImGui.GetWindowDrawList();
        var centerX = MathF.Round(origin.X + width * 0.5f);
        var moonRoad = Theme.ShowRules;

        MeasureLabels();
        RefreshGauge(overall);

        // The station's light-up (Full flair, motion on): restarted whenever the active tab changes.
        if (litTab != ui.Tab)
        {
            if (litTab is not null && Theme.FlairMotion)
            {
                Motion.Trigger(StationKey);
            }

            litTab = ui.Tab;
        }

        if (moonRoad)
        {
            // The deepest surface under the rail (Abyss, or the host's window darkened), at the window's opacity.
            var windowMin = ImGui.GetWindowPos();
            dl.AddRectFilled(windowMin, windowMin + ImGui.GetWindowSize(), Theme.WithAlpha(Theme.Surface.Deep, Theme.WindowAlpha));
        }

        if (place.Crest > 0f)
        {
            DrawCrest(dl, new Vector2(centerX - place.Crest * 0.5f, origin.Y + place.CrestTop), place.Crest, moonRoad);
        }

        var y = origin.Y + place.StationsTop;
        var first = y;
        // The thread starts under the crest (in the middle of the gap its rule sits in), or at the stations' top.
        var threadTop = place.Crest > 0f ? origin.Y + place.CrestTop + place.Crest + (UiMetrics.Px(LayoutBudgets.RailGapLogical) * 0.5f) : first;
        if (moonRoad)
        {
            DrawThread(dl, centerX, threadTop, first, place.Station);
        }

        for (var i = 0; i < Tabs.Length; i++)
        {
            DrawStation(dl, i, new Vector2(origin.X, y), width, place.Station, overall.Fraction, ready, moonRoad, threadTop);
            y += place.Station;
        }

        ui.RecordRect(UiRects.Tabs, new Vector2(origin.X, first), new Vector2(origin.X + width, y));
        DrawFoot(dl, centerX, origin.Y + place.FootTop, place.Fit.Percent, place.Button, overall.Fraction, openHelp, openSettings, moonRoad);

        // The rail's content ends under the foot (a 1 px item whose bottom is the content's), so a rail taller than
        // its pane scrolls to it, and one that fits does not scroll at all.
        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + place.ContentBottom - 1f));
        ImGui.Dummy(new Vector2(1f, 1f));
    }

    /// <summary>
    /// The crest (a click shows Journal › All quests): under the Moon Road look the ornament atlas's moon over night
    /// water in its double brass ring, with a fading brass rule under it; under Plain flair, or while the atlas loads, a
    /// drawn stand-in (a gold moon in a soft halo above the horizon, the road of light under it, in a thin MoonDeep ring).
    /// </summary>
    private void DrawCrest(ImDrawListPtr dl, Vector2 min, float size, bool moonRoad)
    {
        ImGui.SetCursorScreenPos(min);
        if (ImGui.InvisibleButton("##railCrest", new Vector2(size, size)))
        {
            ui.Tab = NavTab.Journal;
            if (ui.Scope != QuestScope.None)
            {
                ui.Scope = QuestScope.None;
                ui.MarkQueryDirty();
            }
        }

        var hovered = ImGui.IsItemHovered();
        var c = min + new Vector2(size * 0.5f);
        var drawn = false;
        if (moonRoad)
        {
            if (hovered && Theme.ShowGlow)
            {
                dl.AddCircleFilled(c, size * 0.5f, Theme.WithAlpha(Theme.Moon, 0.08f), 32);
            }

            drawn = OrnamentAtlas.Draw(dl, OrnamentSprite.Crest, min, min + new Vector2(size), Theme.WithAlpha(Vector4.One, hovered ? 1f : 0.92f));

            // The crest rule: a brass hairline fading out to both sides, in the gap under the crest.
            var ruleY = MathF.Floor(min.Y + size + UiMetrics.Px(LayoutBudgets.RailGapLogical) * 0.5f);
            var half = MathF.Round(UiMetrics.Px(CrestRuleLogical) * 0.5f);
            var line = Theme.Surface.Ornament;
            var peak = Theme.WithAlpha(line, Theme.OrnamentAlpha(0.8f));
            var clear = Theme.WithAlpha(line, 0f);
            dl.AddRectFilledMultiColor(new Vector2(c.X - half, ruleY), new Vector2(c.X, ruleY + 1f), clear, peak, peak, clear);
            dl.AddRectFilledMultiColor(new Vector2(c.X, ruleY), new Vector2(c.X + half, ruleY + 1f), peak, clear, clear, peak);
        }

        if (!drawn)
        {
            DrawCrestStandIn(dl, c, size, hovered);
        }

        Chrome.FocusRing(size * 0.5f);
        if (hovered)
        {
            UiMetrics.Tooltip(Strings.AllQuests);
        }
    }

    private static void DrawCrestStandIn(ImDrawListPtr dl, Vector2 c, float size, bool hovered)
    {
        var line = MathF.Max(1f, size / 32f);
        dl.AddCircle(c, size * 0.4625f, Theme.WithAlpha(Theme.MoonDeep, hovered ? 0.95f : 0.75f), 32, line);

        // The moon, with a halo that brightens on hover.
        var moon = c - new Vector2(0f, size * 0.125f);
        dl.AddCircleFilled(moon, size * 0.30f, Theme.WithAlpha(Theme.Moon, hovered ? 0.16f : 0.10f), 24);
        dl.AddCircleFilled(moon, size * 0.205f, Theme.MoonU32, 24);

        // The horizon, then the road of light: four gold strokes narrowing and fading toward the viewer.
        var horizonY = c.Y + size * 0.1375f;
        dl.AddLine(new Vector2(c.X - size * 0.3625f, horizonY), new Vector2(c.X + size * 0.3625f, horizonY), Theme.WithAlpha(Theme.Silver, 0.55f), line);
        ReadOnlySpan<float> rows = [0.205f, 0.27f, 0.33f, 0.385f];
        ReadOnlySpan<float> halves = [0.1375f, 0.095f, 0.06f, 0.03f];
        ReadOnlySpan<float> alphas = [1f, 0.8f, 0.6f, 0.4f];
        for (var i = 0; i < rows.Length; i++)
        {
            var ry = c.Y + size * rows[i];
            dl.AddLine(new Vector2(c.X - size * halves[i], ry), new Vector2(c.X + size * halves[i], ry), Theme.WithAlpha(Theme.Moon, alphas[i]), line * 1.2f);
        }
    }

    /// <summary>Where a station's icon and label sit: the icon's centre and top, and the bottom of its content (the label, or the icon on the compact rail).</summary>
    private (Vector2 IconCenter, float IconTop, float ContentBottom) StationLayout(Vector2 min, float width, float height)
    {
        var iconSize = UiMetrics.Px(LayoutBudgets.StationIconLogical);
        var labelHeight = ImGui.GetFontSize() * LayoutBudgets.RailLabelFraction;
        var contentHeight = Compact ? iconSize : iconSize + UiMetrics.Px(LayoutBudgets.StationGapLogical) + labelHeight;
        var top = min.Y + MathF.Max(0f, (height - contentHeight) * 0.5f);
        var iconCenter = new Vector2(MathF.Round(min.X + width * 0.5f), MathF.Round(top + iconSize * 0.5f));
        return (iconCenter, iconCenter.Y - iconSize * 0.5f, top + contentHeight);
    }

    /// <summary>
    /// The thread (Moon Road): one brass hairline down the rail's middle from under the crest to the last station, broken
    /// around each station's icon and label so it strings them rather than crossing them.
    /// </summary>
    private void DrawThread(ImDrawListPtr dl, float centerX, float top, float firstStation, float station)
    {
        var color = Theme.WithAlpha(Theme.Surface.Ornament, Theme.OrnamentAlpha(ThreadAlpha));
        var gap = UiMetrics.Px(ThreadGapLogical);
        var from = top;
        for (var i = 0; i < Tabs.Length; i++)
        {
            var (_, iconTop, contentBottom) = StationLayout(new Vector2(0f, firstStation + (i * station)), 0f, station);
            var to = iconTop - gap;
            if (to - from >= 1f)
            {
                dl.AddRectFilled(new Vector2(centerX, MathF.Round(from)), new Vector2(centerX + 1f, MathF.Round(to)), color);
            }

            from = contentBottom + gap;
        }
    }

    /// <summary>
    /// One station: the icon (with the Journal badge) and, on the labelled rail, the label under it in the secondary
    /// tone (primary when active or hovered), ending in an ellipsis where it is wider than the station. Under the Moon
    /// Road look the active station is lit on the thread (a short gold bar ending at its icon, a moon bead at the bar's
    /// head, and under Full flair a soft glow behind the icon); under Plain it has the hover-tone fill, the glow and a
    /// 2 px Moon bar on the left edge. The tooltip names the tab first whenever the label is not shown whole.
    /// </summary>
    private void DrawStation(ImDrawListPtr dl, int i, Vector2 min, float width, float height, float overallFraction, int ready, bool moonRoad, float threadTop)
    {
        var tab = Tabs[i];
        var active = ui.Tab == tab;
        ImGui.SetCursorScreenPos(min);
        if (ImGui.InvisibleButton(Ids[i], new Vector2(width, height)) && !active)
        {
            ui.Tab = tab;
            active = true;
        }

        var hovered = ImGui.IsItemHovered();
        var s = Theme.Surface;
        var max = min + new Vector2(width, height);
        var inset = UiMetrics.Px(3f);
        var rounding = UiMetrics.Px(5f);
        var fillMin = new Vector2(min.X + inset, min.Y + 1f);
        var fillMax = new Vector2(max.X - inset, max.Y - 1f);
        var (iconCenter, iconTop, contentBottom) = StationLayout(min, width, height);
        var iconSize = UiMetrics.Px(LayoutBudgets.StationIconLogical);

        if (active && !moonRoad)
        {
            dl.AddRectFilled(fillMin, fillMax, Theme.U32(s.Hover), rounding);
            var bar = MathF.Max(2f, MathF.Round(UiMetrics.Px(2f)));
            dl.AddRectFilled(new Vector2(min.X, min.Y + UiMetrics.Px(8f)), new Vector2(min.X + bar, max.Y - UiMetrics.Px(8f)), Theme.MoonU32, bar * 0.5f);
        }
        else if (hovered)
        {
            dl.AddRectFilled(fillMin, fillMax, Theme.WithAlpha(s.Hover, 0.6f), rounding);
        }

        if (active)
        {
            if (moonRoad)
            {
                // The thread's segment above this station: from the crest, or from under the station above.
                var segmentTop = i == 0 ? threadTop : contentBottom - height + UiMetrics.Px(ThreadGapLogical);
                DrawLit(dl, iconCenter, iconTop, segmentTop);
            }
            else
            {
                dl.AddCircleFilled(iconCenter, UiMetrics.Px(GlowRadiusLogical), Theme.WithAlpha(Theme.Moon, 0.06f), 24);
            }
        }

        if (tab == NavTab.Journal)
        {
            if (moonRoad)
            {
                var box = MathF.Round(iconSize);
                Orbit.DrawMoon(dl, iconCenter - new Vector2(box * 0.5f), box, Motion.Fill(JournalKey, overallFraction), Theme.Glyphs.HighContrast);
            }
            else
            {
                MoonGlyph.DrawFilling(dl, iconCenter, iconSize * 0.45f, overallFraction);
            }

            if (ready > 0)
            {
                // The Ready count on the icon's top right, held inside the station: on the 44 px compact rail a "99+"
                // pill there would run past the rail's edge and be clipped.
                var badge = Chrome.BadgeSize(ready);
                var edge = UiMetrics.Px(1f);
                var badgeX = MathF.Min(iconCenter.X + iconSize * 0.55f, max.X - edge - badge.X * 0.5f);
                var badgeY = MathF.Max(iconCenter.Y - iconSize * 0.42f, min.Y + edge + badge.Y * 0.5f);
                Chrome.Badge(dl, new Vector2(badgeX, badgeY), ready, actionable: true);
            }
        }
        else if (!moonRoad || !DrawArtIcon(dl, tab, iconCenter, iconSize, active ? 1f : hovered ? HoverIconAlpha : IdleIconAlpha))
        {
            var ink = active ? Theme.AccentU32 : Theme.U32(hovered ? s.TextSecondary : s.TextTertiary);
            DrawIcon(dl, iconCenter, tab == NavTab.Moonlit ? GemIcon : tab == NavTab.Characters ? UsersIcon : tab == NavTab.Flight ? PlaneIcon : PlanIcon, ink);
        }

        var cut = Compact;
        if (!Compact)
        {
            ImGui.SetWindowFontScale(LayoutBudgets.RailLabelFraction);
            var room = MathF.Max(0f, width - 2f * UiMetrics.Px(LayoutBudgets.RailLabelPadLogical));
            var labelWidth = labelWidths[i];
            var shown = MathF.Min(labelWidth, room);
            var labelPos = new Vector2(MathF.Round(min.X + (width - shown) * 0.5f), MathF.Round(iconTop + iconSize + UiMetrics.Px(LayoutBudgets.StationGapLogical)));
            cut = Chrome.EllipsisTextAt(dl, labelPos, room, Labels[i], Theme.U32(active || hovered ? s.Text : s.TextSecondary), labelWidth);
            ImGui.SetWindowFontScale(1f);
        }

        Chrome.FocusRing(rounding);
        if (hovered)
        {
            var description = tab == NavTab.Journal ? JournalTooltip(ready) : Tooltips[i];
            if (cut)
            {
                UiMetrics.Tooltip(Labels[i], description);
            }
            else
            {
                UiMetrics.Tooltip(description);
            }
        }
    }

    /// <summary>
    /// The active station lit on the thread: a 2 px gold bar (MoonHigh → MoonDeep) ending just above the icon, a moon bead
    /// at its head, and under Full flair a soft glow behind the icon. After a tab change under Full flair it grows from a
    /// fifth of its length and fades in over <see cref="MotionMath.StationLightSeconds"/>; otherwise it is simply lit.
    /// </summary>
    private static void DrawLit(ImDrawListPtr dl, Vector2 iconCenter, float iconTop, float segmentTop)
    {
        var progress = Theme.FlairMotion ? Motion.Pulse(StationKey, MotionMath.StationLightSeconds) : -1f;
        var light = progress < 0f ? 1f : MotionMath.EaseOutCubic(progress);
        if (Theme.ShowGlow)
        {
            dl.AddCircleFilled(iconCenter, UiMetrics.Px(GlowRadiusLogical), Theme.WithAlpha(Theme.Moon, 0.06f * light), 24);
        }

        var bottom = MathF.Round(iconTop - UiMetrics.Px(ThreadGapLogical));
        var room = bottom - segmentTop;
        var length = MathF.Min(UiMetrics.Px(LitBarLogical), MathF.Max(0f, room)) * (0.2f + (0.8f * light));
        if (length < 1f)
        {
            return;
        }

        var bar = MathF.Max(2f, MathF.Round(UiMetrics.Px(2f)));
        var left = MathF.Round(iconCenter.X - ((bar - 1f) * 0.5f));
        var top = MathF.Round(bottom - length);
        dl.AddRectFilledMultiColor(
            new Vector2(left, top),
            new Vector2(left + bar, bottom),
            Theme.WithAlpha(Theme.MoonHigh, light),
            Theme.WithAlpha(Theme.MoonHigh, light),
            Theme.WithAlpha(Theme.MoonDeep, light),
            Theme.WithAlpha(Theme.MoonDeep, light));

        var bead = new Vector2(left + (bar * 0.5f), top);
        dl.AddCircleFilled(bead, UiMetrics.Px(Orbit.BeadRimLogical), Theme.WithAlpha(Theme.Night, light));
        dl.AddCircleFilled(bead, UiMetrics.Px(Orbit.BeadLogical), Theme.WithAlpha(Theme.MoonHigh, light));
    }

    /// <summary>
    /// A station's art icon under the Moon Road look: the kit's Moonlit and Flight glyphs, the game's blue unlock-quest
    /// marker for My blues. False, with nothing drawn, for Characters (a FontAwesome silhouette) or while the texture loads.
    /// </summary>
    private static bool DrawArtIcon(ImDrawListPtr dl, NavTab tab, Vector2 center, float size, float alpha)
    {
        var half = new Vector2(MathF.Round(size * 0.5f));
        var min = center - half;
        var max = center + half;
        var tint = Theme.WithAlpha(Vector4.One, alpha);
        switch (tab)
        {
            case NavTab.Moonlit:
                return OrnamentAtlas.IsReady && OrnamentAtlas.Draw(dl, OrnamentGlyph.Moonlit, min, max, tint);
            case NavTab.Flight:
                return OrnamentAtlas.IsReady && OrnamentAtlas.Draw(dl, OrnamentGlyph.Flight, min, max, tint);
            case NavTab.Plan when Plugin.TextureProvider is { } textures:
                var lookup = new GameIconLookup(NodeIcons.FeatureMarker, false, max.X - min.X > Orbit.LowResMaxPx);
                if (!textures.TryGetFromGameIcon(lookup, out var texture) || !texture.TryGetWrap(out var wrap, out _))
                {
                    return false;
                }

                dl.AddImage(wrap.Handle, min, max, Vector2.Zero, Vector2.One, tint);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// The foot: the overall gauge (an orbit round a filling moon under the Moon Road look, the 1.3 halo under Plain;
    /// done / total on hover), its percentage under it where the height allows, then Help and Settings, side by side on
    /// the labelled rail and stacked on the compact one. Each part takes the height <see cref="LayoutBudgets.FootHeight"/>
    /// gives it, so the foot is exactly as tall as the fit reserved; <paramref name="button"/> is the buttons' side as
    /// drawn (<see cref="UiMetrics.MinTarget"/>).
    /// </summary>
    private void DrawFoot(ImDrawListPtr dl, float centerX, float top, bool percent, float button, float fraction, Action? openHelp, Action? openSettings, bool moonRoad)
    {
        var gap = UiMetrics.Px(LayoutBudgets.RailGapLogical);
        var gauge = UiMetrics.Px(LayoutBudgets.RailGaugeLogical);
        var radius = gauge * 0.5f;
        var center = new Vector2(centerX, MathF.Round(top + radius));
        ImGui.SetCursorScreenPos(center - new Vector2(radius));
        ImGui.Dummy(new Vector2(gauge, gauge));
        var gaugeHovered = ImGui.IsItemHovered();
        if (moonRoad)
        {
            var box = MathF.Round(gauge);
            Orbit.DrawMoon(dl, center - new Vector2(box * 0.5f), box, Motion.Fill(GaugeKey, fraction), Theme.Glyphs.HighContrast);
        }
        else
        {
            MoonGlyph.DrawHalo(dl, center, radius - UiMetrics.Px(1f), Motion.Gauge(GaugeKey, fraction));
        }

        var y = top + gauge + gap;

        if (percent)
        {
            ImGui.SetWindowFontScale(LayoutBudgets.RailLabelFraction);
            ImGui.SetCursorScreenPos(new Vector2(MathF.Round(centerX - percentWidth * ImGui.GetFontSize() * 0.5f), y));
            ImGui.TextUnformatted(percentText);
            gaugeHovered |= ImGui.IsItemHovered();
            ImGui.SetWindowFontScale(1f);
            y += UiMetrics.Px(LayoutBudgets.PercentLine(percent: true));
        }

        if (gaugeHovered)
        {
            UiMetrics.Tooltip(Strings.FillingMoonTooltip, progressText);
        }

        var between = UiMetrics.Px(4f);
        if (Compact)
        {
            FootButton("##railHelp", HelpIcon, Strings.HelpButtonTooltip, openHelp, UiRects.HelpButton, new Vector2(centerX - button * 0.5f, y));
            y += button + gap;
            FootButton("##railSettings", SettingsIcon, Strings.SettingsButtonTooltip, openSettings, UiRects.SettingsButton, new Vector2(centerX - button * 0.5f, y));
        }
        else
        {
            var left = MathF.Round(centerX - button - between * 0.5f);
            FootButton("##railHelp", HelpIcon, Strings.HelpButtonTooltip, openHelp, UiRects.HelpButton, new Vector2(left, y));
            FootButton("##railSettings", SettingsIcon, Strings.SettingsButtonTooltip, openSettings, UiRects.SettingsButton, new Vector2(left + button + between, y));
        }
    }

    private void FootButton(string id, string icon, string tooltip, Action? action, string rectKey, Vector2 pos)
    {
        ImGui.SetCursorScreenPos(pos);
        if (Chrome.IconButtonRound(id, icon, action is null ? Strings.ActionUnavailable : tooltip, enabled: action is not null))
        {
            action?.Invoke();
        }

        ui.RecordItem(rectKey);
    }

    private static void DrawIcon(ImDrawListPtr dl, Vector2 center, string icon, uint color)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        var size = ImGui.CalcTextSize(icon);
        dl.AddText(center - size * 0.5f, color, icon);
        ImGui.PopFont();
    }

    /// <summary>Measures the labels at their own size when the language or the font size changed.</summary>
    private void MeasureLabels()
    {
        var fontSize = ImGui.GetFontSize();
        if (labelsLanguage == Localization.Loc.Version && labelsFontSize == fontSize)
        {
            return;
        }

        labelsLanguage = Localization.Loc.Version;
        labelsFontSize = fontSize;
        ImGui.SetWindowFontScale(LayoutBudgets.RailLabelFraction);
        var labels = Labels;
        for (var i = 0; i < labels.Length; i++)
        {
            labelWidths[i] = ImGui.CalcTextSize(labels[i]).X;
        }

        ImGui.SetWindowFontScale(1f);
    }

    /// <summary>The foot's percentage and progress texts, rebuilt when the overall count or the language changes.</summary>
    private void RefreshGauge(NodeCount overall)
    {
        if (overall == gaugeCount && percentText.Length > 0 && gaugeLanguage == Localization.Loc.Version)
        {
            return;
        }

        gaugeCount = overall;
        gaugeLanguage = Localization.Loc.Version;
        var percent = overall.Total <= 0 ? 0 : (int)MathF.Floor(100f * overall.Done / overall.Total);
        percentText = string.Format(CultureInfo.CurrentCulture, Strings.StatusPercentFormat, percent);
        // Stored per unit of font size, so it follows the UI scale without measuring again.
        percentWidth = ImGui.CalcTextSize(percentText).X / MathF.Max(ImGui.GetFontSize(), 1f);
        progressText = UiFormat.Progress(Math.Max(0, overall.Done), Math.Max(0, overall.Total));
    }

    /// <summary>The Journal station's hover text: what it holds, then the Ready count; rebuilt only when the count or the language changes.</summary>
    private string JournalTooltip(int ready)
    {
        if (ready != readyTooltipCount || readyTooltipLanguage != Localization.Loc.Version)
        {
            readyTooltipCount = ready;
            readyTooltipLanguage = Localization.Loc.Version;
            readyTooltip = ready > 0
                ? Strings.TabJournalTooltip + "\n" + string.Format(CultureInfo.CurrentCulture, Strings.TreeReadyBadgeFormat, ready)
                : Strings.TabJournalTooltip;
        }

        return readyTooltip;
    }
}
