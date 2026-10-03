using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>What the rail's Journal badge shows this frame (plan v7, spec Revision 3 R3.2).</summary>
/// <param name="Mode">What the badge counts (Settings › Main window › Journal badge).</param>
/// <param name="Count">The number on it; 0 draws none.</param>
/// <param name="New">Quests newly ready since the player last looked.</param>
/// <param name="Ready">Quests Ready on the current job.</param>
/// <param name="StoryReady">Of those, the main scenario and unlock quests.</param>
public readonly record struct RailBadge(JournalBadgeMode Mode, int Count, int New, int Ready, int StoryReady);

/// <summary>
/// The main window's rail (feature plan v4 L7, design v4 §7.1, plan v7 UI-4), drawn in its own fixed pane left of the
/// tree (<see cref="PaneSplit"/>). Top to bottom: the crest (a click shows Journal › All quests), one station per tab,
/// and at the foot the overall gauge with its percentage and the round Todo overlay, Nearby (1.7.0), Help and Settings
/// buttons. The rail is 70 logical px wide at Full and 66 at Quiet; on a window under about 1,040 px, by Settings ›
/// Display › Compact rail, and always at Plain, it is a 44 px compact rail of icons whose labels are in the tooltips
/// (<see cref="LayoutBudgets.CompactRail"/>). The stations share the rail's height (<see cref="LayoutBudgets.Stations"/>);
/// on a short window the rail gives up height in a fixed order (<see cref="LayoutBudgets.FitRail"/>) and scrolls only
/// past that.
/// <para>
/// A station (plan v7 UI-4, spec §6 with Revisions 2 and 3) is a 30 px icon (28 at Quiet, 20 at Plain) over its label, on
/// a plate inset 3 px from the rail's sides. The label is fitted to the plate (<see cref="RailLabel.Fit"/>): tracked,
/// shrunk to no less than 10 px, wrapped, or left to the tooltip with the icon alone, never cut and never overflowing.
/// Hovering lifts the icon 2 px (1 at Quiet) and lays a plate with a darker foot; the selected station keeps three marks
/// only: its plate, the icon in gold, and the moon bead on the rail's left edge, which travels from the station left
/// behind to the new one. Labels never move, only their ink changes. Every motion takes its pace from
/// <see cref="MotionTokens"/>, and under Reduce motion (or at Plain) everything lands at once. The thread and the lit bar
/// of 1.4–1.13 are gone. The Journal station carries the Journal badge (<see cref="RailBadge"/>): one fixed size, hidden
/// at 0, and a click on it opens the quests it counts as newly ready.
/// </para>
/// <para>
/// Each station is a real item (an <see cref="ImGui.InvisibleButton(string, Vector2)"/> with the focus ring), so
/// keyboard and gamepad navigation reach it. <see cref="UiState.Tab"/> is the single source of truth: a click writes
/// it, and a programmatic switch (Reveal, ShowIssuer, the tutorial, help) is simply the next frame's active station.
/// The union of the stations is recorded as <see cref="UiRects.Tabs"/>, and the foot's buttons as
/// <see cref="UiRects.HelpButton"/> and <see cref="UiRects.SettingsButton"/>, for the tutorial. Nothing allocates per
/// frame: the labels' fits are measured once per language, size and room, and the tooltips and the percentage are
/// rebuilt only when their counts or the language change. Everything is placed by <see cref="LayoutBudgets.PlaceRail"/>,
/// so a rail that fits its pane never overflows it by a rounding pixel and turns wheel-scrollable.
/// </para>
/// </summary>
public sealed class TabStrip
{
    private static readonly NavTab[] Tabs = [NavTab.Journal, NavTab.Moonlit, NavTab.Characters, NavTab.Flight, NavTab.Plan];

    /// <summary>Full's star field in the rail's empty sky: seeded once, so it never shimmers.</summary>
    private static readonly Core.Ui.Star[] RailStars = Core.Ui.StarField.Generate(7, 9);

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
    private static readonly string OverlayIcon = FontAwesomeIcon.Tasks.ToIconString();
    private static readonly string NearbyIcon = FontAwesomeIcon.MapMarkerAlt.ToIconString();
    private static readonly string SettingsIcon = FontAwesomeIcon.Cog.ToIconString();

    /// <summary>A label's width at a font size of one, in the body font: what the fit scales.</summary>
    private static readonly EmMeasure MeasureEm = static text => ImGui.CalcTextSize(text).X / MathF.Max(1f, ImGui.GetFontSize());

    /// <summary>The overall gauge's fill motion (it moves only when the count changes, or fills once under Full flair).</summary>
    private static readonly ulong GaugeKey = Motion.Key(0x5241_494C, 0); // "RAIL"

    /// <summary>The Journal station's orbit fill.</summary>
    private static readonly ulong JournalKey = Motion.Key(0x5241_494C, 1);

    /// <summary>The bead's travel after a tab change.</summary>
    private static readonly ulong StationKey = Motion.Key(0x5241_494C, 2);

    /// <summary>The crest's and the stations' hover (by station index; the crest after them).</summary>
    private const uint HoverTag = 0x5241_4948; // "RAIH"

    /// <summary>The stations' selection (by station index): the plate and the gold ink settling in and out.</summary>
    private const uint SelectTag = 0x5241_4953; // "RAIS"

    private const float CrestRuleLogical = 40f;

    /// <summary>A plate's corner radius at Full and Quiet.</summary>
    private const float PlateRadiusLogical = 10f;

    /// <summary>How far a hovered icon rises: 2 px at Full, 1 at Quiet, none at Plain.</summary>
    private const float RiseLogical = 2f;

    private const float QuietRiseLogical = 1f;

    /// <summary>The hovered plate's wash (the hover tone) and its darker foot (Abyss), at full hover.</summary>
    private const float PlateHoverAlpha = 0.55f;

    private const float PlainHoverAlpha = 0.62f;
    private const float PlateFootAlpha = 0.38f;

    /// <summary>The selected plate: Moon at Full, the text tone with a VeilLine border at Quiet.</summary>
    private const float SelectedPlateAlpha = 0.07f;

    private const float QuietSelectedPlateAlpha = 0.06f;
    private const float QuietSelectedBorderAlpha = 0.55f;

    /// <summary>The bead: 7 px MoonHigh with a 1.5 px Night rim at Full, its centre 4.5 px in from the rail's left edge; a 5 px Moon dot at Quiet.</summary>
    private const float BeadRadiusLogical = 3.5f;

    private const float BeadRimLogical = 1.5f;
    private const float BeadInsetLogical = 4.5f;
    private const float QuietBeadRadiusLogical = 2.5f;
    private const float QuietBeadInsetLogical = 4f;

    /// <summary>Station icon alpha by state (the kit's glyphs and the game icon carry their own colours).</summary>
    private const float IdleIconAlpha = 0.72f;

    private const float HoverIconAlpha = 0.95f;
    private const float PlainIconAlpha = 0.8f;

    /// <summary>The Journal badge: one fixed size (16 px tall, at least 16 wide, 10 px text), its left edge 4 px inside the icon's right and its top 5 px over the icon.</summary>
    private const float BadgeLogical = 16f;

    private const float BadgeTextLogical = 10f;
    private const float BadgePadLogical = 4f;
    private const float BadgeOverlapLogical = 4f;
    private const float BadgeRiseLogical = 5f;

    private readonly UiState ui;

    // The Journal tooltip, rebuilt only when what it says changes.
    private RailBadge tooltipBadge;
    private int tooltipEveryReady = -1;
    private bool tooltipOnBadge;
    private int tooltipLanguage = -1;
    private string journalTooltip = string.Empty;

    // The labels' fits (RailLabel.Fit), measured again only when the language, the label size, the room or the lines change.
    private readonly RailLabelFit[] fits = new RailLabelFit[Tabs.Length];
    private int fitsLanguage = -1;
    private float fitsSize = -1f;
    private float fitsRoom = -1f;
    private int fitsLines = -1;

    // The foot's texts, rebuilt when the overall count or the language changes.
    private NodeCount gaugeCount = new(-1, -1, 0);
    private int gaugeLanguage = -1;
    private string percentText = string.Empty;
    private float percentWidth;
    private string progressText = string.Empty;

    /// <summary>The tab the bead last stood at; a change starts its travel.</summary>
    private NavTab? beadTab;

    /// <summary>The station the bead travels from after a tab change (feature plan v6 M1), or -1.</summary>
    private int travelFrom = -1;

    // This frame's station geometry: where each icon's centre line is, for the bead.
    private readonly float[] iconCenters = new float[Tabs.Length];

    public TabStrip(UiState ui)
    {
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
    }

    /// <summary>Whether the rail is the compact icon rail this frame (<see cref="UpdateMode"/>).</summary>
    public bool Compact { get; private set; }

    /// <summary>The rail's logical width at the Decoration level in effect (<see cref="LayoutBudgets.RailWidthLogical"/>).</summary>
    public float RailLogicalWidth => LayoutBudgets.RailWidthLogical(Theme.Flair, Compact);

    /// <summary>The rail's width in pixels.</summary>
    public float RailWidth => MathF.Round(UiMetrics.Px(RailLogicalWidth));

    /// <summary>Opens the Journal on the quests the badge counts as newly ready; null leaves the badge a plain number.</summary>
    public Action? ShowNewlyReady { get; set; }

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
    /// <param name="badge">The Journal badge; a count of 0 hides it.</param>
    /// <param name="everyReady">Every Ready quest (the tree's count), for the tooltip.</param>
    /// <param name="openHelp">Opens the help window; null draws the button disabled.</param>
    /// <param name="openSettings">Opens Settings; null draws the button disabled.</param>
    public void Draw(NodeCount overall, RailBadge badge, int everyReady, Action? openHelp, Action? openSettings)
    {
        var origin = ImGui.GetCursorScreenPos();
        var avail = ImGui.GetContentRegionAvail();
        var width = MathF.Max(1f, avail.X);
        var flair = Theme.Flair;
        // Everything is placed by the fit, with the foot's buttons at the size they are drawn and whole-pixel stations,
        // so a rail that fits its pane ends inside it and never becomes wheel-scrollable.
        var place = LayoutBudgets.PlaceRail(avail.Y, UiMetrics.Scale, Tabs.Length, Compact, UiMetrics.MinTarget, flair);
        var dl = ImGui.GetWindowDrawList();
        var centerX = MathF.Round(origin.X + width * 0.5f);
        var moonRoad = Theme.MoonRoadArt;

        FitLabels(place.Station);
        RefreshGauge(overall);

        // The bead's travel (Full and Quiet, motion on; feature plan v6 M1): on a tab change it runs the rail's left edge
        // from the old station to the new one while the new plate fades in.
        if (beadTab != ui.Tab)
        {
            if (beadTab is { } previous && Theme.UiMotion)
            {
                travelFrom = Array.IndexOf(Tabs, previous);
                Motion.Trigger(StationKey);
            }

            beadTab = ui.Tab;
        }

        {
            // The rail's own surface at the window's opacity (docs/design/flair-v13 §1): the deepest one under the Moon
            // Road (Abyss at 0.9, or the host's window darkened), Quiet's rail tone, Plain's Deep.
            var windowMin = ImGui.GetWindowPos();
            var windowMax = windowMin + ImGui.GetWindowSize();
            var tone = moonRoad ? Theme.Surface.Deep : Theme.Tones.Rail;
            dl.AddRectFilled(windowMin, windowMax, Theme.WithAlpha(tone, Theme.WindowAlpha * (moonRoad ? 0.9f : 1f)));
            if (Theme.ShowStars)
            {
                // Full: a faint seeded star field in the rail's empty sky, between the last station and the foot.
                var skyTop = origin.Y + place.StationsTop + (place.Station * Tabs.Length) + UiMetrics.Px(6f);
                var skyFoot = origin.Y + place.FootTop - UiMetrics.Px(6f);
                Ornament.Stars(dl, new Vector2(windowMin.X, skyTop), new Vector2(windowMax.X, skyFoot), RailStars);
            }
        }

        if (place.Crest > 0f)
        {
            DrawCrest(dl, new Vector2(centerX - place.Crest * 0.5f, origin.Y + place.CrestTop), place.Crest, moonRoad);
        }

        var y = origin.Y + place.StationsTop;
        var first = y;
        for (var i = 0; i < Tabs.Length; i++)
        {
            DrawStation(dl, i, new Vector2(origin.X, y), width, place.Station, overall.Fraction, badge, everyReady, moonRoad);
            y += place.Station;
        }

        DrawBead(dl, origin.X);
        ui.RecordRect(UiRects.Tabs, new Vector2(origin.X, first), new Vector2(origin.X + width, y));
        DrawFoot(dl, centerX, origin.Y + place.FootTop, place.Fit, place.Button, overall.Fraction, openHelp, openSettings, moonRoad);

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
        var glow = Motion.Hover(Motion.Key(HoverTag, (uint)Tabs.Length), hovered);
        var c = min + new Vector2(size * 0.5f);
        var drawn = false;
        if (moonRoad)
        {
            if (glow > 0.004f && Theme.ShowGlow)
            {
                dl.AddCircleFilled(c, size * 0.5f, Theme.WithAlpha(Theme.Moon, 0.08f * glow), 32);
            }

            drawn = OrnamentAtlas.Draw(dl, OrnamentSprite.Crest, min, min + new Vector2(size), Theme.WithAlpha(Vector4.One, 0.92f + (0.08f * glow)));

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
            DrawCrestStandIn(dl, c, size, glow);
        }

        Chrome.FocusRing(size * 0.5f);
        if (hovered)
        {
            UiMetrics.Tooltip(Strings.AllQuests);
        }
    }

    private static void DrawCrestStandIn(ImDrawListPtr dl, Vector2 c, float size, float hover)
    {
        var line = MathF.Max(1f, size / 32f);
        dl.AddCircle(c, size * 0.4625f, Theme.WithAlpha(Theme.MoonDeep, 0.75f + (0.2f * hover)), 32, line);

        // The moon, with a halo that brightens on hover.
        var moon = c - new Vector2(0f, size * 0.125f);
        dl.AddCircleFilled(moon, size * 0.30f, Theme.WithAlpha(Theme.Moon, 0.10f + (0.06f * hover)), 24);
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

    /// <summary>The rail labels' size in pixels this frame: 0.75 of the body, held to 10–12 logical px (<see cref="LayoutBudgets.RailLabelLogical"/>).</summary>
    private static float LabelPx()
    {
        var scale = MathF.Max(0.01f, UiMetrics.Scale);
        return LayoutBudgets.RailLabelLogical(ImGui.GetFontSize() / scale) * scale;
    }

    /// <summary>
    /// Where a station's icon and label sit: the icon's centre (before any hover rise) and the label's top, the content
    /// (icon, gap, label lines) centred in the station; the compact rail and an icon-only label centre the icon alone.
    /// </summary>
    private (Vector2 IconCenter, float LabelTop) StationLayout(int i, Vector2 min, float width, float height)
    {
        var iconSize = MathF.Round(UiMetrics.Px(LayoutBudgets.StationIcon(Theme.Flair)));
        var fit = fits[i];
        var labelled = !Compact && !fit.IconOnly;
        var gap = UiMetrics.Px(LayoutBudgets.StationGapLogical);
        var labelHeight = labelled ? LabelBlockHeight(fit) : 0f;
        var contentHeight = labelled ? iconSize + gap + labelHeight : iconSize;
        var top = min.Y + MathF.Max(0f, (height - contentHeight) * 0.5f);
        var iconCenter = new Vector2(MathF.Round(min.X + width * 0.5f), MathF.Round(top + iconSize * 0.5f));
        return (iconCenter, MathF.Round(top + iconSize + gap));
    }

    /// <summary>A fitted label's height in pixels: its lines at its size.</summary>
    private static float LabelBlockHeight(RailLabelFit fit)
    {
        var size = UiMetrics.Px(fit.Size);
        return fit.Lines <= 1 ? size : size * (1f + RailLabel.LineHeight);
    }

    /// <summary>
    /// One station: its plate, its icon (with the Journal badge) and, on the labelled rail, its fitted label. Hover lays the
    /// plate with a darker foot and lifts the icon; selection settles the selected plate and the gold ink in (the bead is
    /// drawn after the stations, <see cref="DrawBead"/>). The tooltip names the tab first whenever the label is not shown.
    /// </summary>
    private void DrawStation(ImDrawListPtr dl, int i, Vector2 min, float width, float height, float overallFraction, RailBadge badge, int everyReady, bool moonRoad)
    {
        var tab = Tabs[i];
        var flair = Theme.Flair;
        var plain = flair == Flair.Plain;
        var max = min + new Vector2(width, height);
        var (iconCenter, labelTop) = StationLayout(i, min, width, height);
        iconCenters[i] = iconCenter.Y;
        var iconSize = MathF.Round(UiMetrics.Px(LayoutBudgets.StationIcon(flair)));

        // The badge's place is fixed (it rides the icon's rise when drawn), so it can be hit-tested before the click.
        var journal = tab == NavTab.Journal;
        var badgeText = journal ? NewlyReady.BadgeText(badge.Count) : string.Empty;
        var (badgeMin, badgeMax) = badgeText.Length > 0 ? BadgeRect(badgeText, iconCenter, iconSize, min, max, plain) : (Vector2.Zero, Vector2.Zero);
        var onBadge = false;

        ImGui.SetCursorScreenPos(min);
        var clicked = ImGui.InvisibleButton(Ids[i], new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();
        if (badgeText.Length > 0 && hovered)
        {
            var mouse = ImGui.GetMousePos();
            var slack = UiMetrics.Px(2f);
            onBadge = mouse.X >= badgeMin.X - slack && mouse.X <= badgeMax.X + slack && mouse.Y >= badgeMin.Y - slack && mouse.Y <= badgeMax.Y + slack;
        }

        var newlyReady = onBadge && badge.Mode == JournalBadgeMode.NewlyReady && badge.New > 0 && ShowNewlyReady is not null;
        if (clicked)
        {
            if (newlyReady)
            {
                ShowNewlyReady!();
            }
            else if (ui.Tab != tab)
            {
                ui.Tab = tab;
            }
        }

        var active = ui.Tab == tab;

        // Hover glides in and out (U8); selection settles in over Select and out over the shorter Leave.
        var hover = Motion.Hover(Motion.Key(HoverTag, (uint)i), hovered);
        var select = Motion.LerpAsym(Motion.Key(SelectTag, (uint)i), active ? 1f : 0f, MotionMath.SelectRate, MotionTokens.RateFor(MotionTokens.Leave));
        var lift = hover * (1f - select);

        DrawPlate(dl, min, max, flair, lift, select);

        // The icon rises on hover and settles to its place when selected; the label never moves.
        var rise = plain ? 0f : UiMetrics.Px(flair == Flair.Quiet ? QuietRiseLogical : RiseLogical) * lift;
        var icon = iconCenter - new Vector2(0f, rise);
        var alpha = plain
            ? PlainIconAlpha + ((1f - PlainIconAlpha) * select)
            : MathF.Min(1f, IdleIconAlpha + ((HoverIconAlpha - IdleIconAlpha) * hover) + ((1f - HoverIconAlpha) * select));
        var s = Theme.Surface;
        var selectedInk = plain ? s.Text : Theme.Accent;
        var ink = Vector4.Lerp(Vector4.Lerp(s.TextTertiary, s.TextSecondary, hover), selectedInk, select);
        if (journal)
        {
            if (moonRoad)
            {
                Orbit.DrawMoon(dl, icon - new Vector2(iconSize * 0.5f), iconSize, Motion.Fill(JournalKey, overallFraction), Theme.Glyphs.HighContrast);
            }
            else
            {
                MoonGlyph.DrawFilling(dl, icon, iconSize * 0.45f, overallFraction);
            }

            if (badgeText.Length > 0)
            {
                DrawBadge(dl, badgeText, badgeMin - new Vector2(0f, rise), badgeMax - new Vector2(0f, rise), plain);
            }
        }
        else if (!moonRoad || !DrawArtIcon(dl, tab, icon, iconSize, alpha))
        {
            DrawIcon(dl, icon, iconSize, tab == NavTab.Moonlit ? GemIcon : tab == NavTab.Characters ? UsersIcon : tab == NavTab.Flight ? PlaneIcon : PlanIcon, Theme.WithAlpha(ink, alpha));
        }

        var fit = fits[i];
        var shown = !Compact && !fit.IconOnly;
        if (shown)
        {
            var labelInk = Theme.U32(Vector4.Lerp(s.TextSecondary, s.Text, MathF.Max(hover, select)));
            DrawLabel(dl, Labels[i], fit, min.X + width * 0.5f, labelTop, labelInk);
        }

        Chrome.FocusRing(UiMetrics.Px(PlateRadiusLogical));
        if (hovered)
        {
            var description = journal ? JournalTooltip(badge, everyReady, newlyReady) : Tooltips[i];
            if (shown)
            {
                UiMetrics.Tooltip(description);
            }
            else
            {
                UiMetrics.Tooltip(Labels[i], description);
            }
        }
    }

    /// <summary>
    /// A station's plate (spec §6, Revision 2): at Full and Quiet inset 3 px with a 10 px radius, at Plain full bleed and
    /// square. Hover (<paramref name="lift"/>, none while selected) lays the hover tone at .55 with, at Full, a 1 px darker
    /// foot under it, so the icon's rise has a cause. Selected (<paramref name="select"/>): Moon at .07 at Full, the text
    /// tone at .06 inside a VeilLine border at Quiet, Plain's band with a 2 px text-coloured bar on the left edge. No glow,
    /// no hairline.
    /// </summary>
    private static void DrawPlate(ImDrawListPtr dl, Vector2 min, Vector2 max, Flair flair, float lift, float select)
    {
        var s = Theme.Surface;
        if (flair == Flair.Plain)
        {
            if (select > 0.004f)
            {
                dl.AddRectFilled(min, max, Theme.WithAlpha(Theme.Tones.Band, select));
                var bar = MathF.Max(2f, MathF.Round(UiMetrics.Px(2f)));
                dl.AddRectFilled(min, new Vector2(min.X + bar, max.Y), Theme.WithAlpha(s.Text, select));
            }

            if (lift > 0.004f)
            {
                dl.AddRectFilled(min, max, Theme.WithAlpha(s.Hover, PlainHoverAlpha * lift));
            }

            return;
        }

        var inset = MathF.Round(UiMetrics.Px(LayoutBudgets.RailPlateInsetLogical));
        var plateMin = min + new Vector2(inset);
        var plateMax = max - new Vector2(inset);
        var radius = UiMetrics.Px(PlateRadiusLogical);
        if (lift > 0.004f)
        {
            dl.AddRectFilled(plateMin, plateMax, Theme.WithAlpha(s.Hover, PlateHoverAlpha * lift), radius);
            if (flair == Flair.Full)
            {
                // The foot: the plate's bottom pixel row, clear of its rounded corners.
                var foot = MathF.Max(1f, MathF.Round(UiMetrics.Px(1f)));
                var curve = MathF.Round(radius * 0.7f);
                dl.AddRectFilled(new Vector2(plateMin.X + curve, plateMax.Y - foot), new Vector2(plateMax.X - curve, plateMax.Y), Theme.WithAlpha(Theme.Abyss, PlateFootAlpha * lift));
            }
        }

        if (select > 0.004f)
        {
            if (flair == Flair.Quiet)
            {
                dl.AddRectFilled(plateMin, plateMax, Theme.WithAlpha(s.Text, QuietSelectedPlateAlpha * select), radius);
                dl.AddRect(plateMin, plateMax, Theme.WithAlpha(s.StrongLine, QuietSelectedBorderAlpha * select), radius, ImDrawFlags.None, UiMetrics.Hairline);
            }
            else
            {
                dl.AddRectFilled(plateMin, plateMax, Theme.WithAlpha(Theme.Moon, SelectedPlateAlpha * select), radius);
            }
        }
    }

    /// <summary>
    /// The moon bead on the rail's left edge, at the selected station's icon line: 7 px MoonHigh in a 1.5 px Night rim
    /// with a soft glow at Full, a 5 px Moon dot at Quiet, none at Plain or on a station off the rail. After a tab change it
    /// runs from the station left behind to the new one over <see cref="MotionTokens.Travel"/>, eased in and out, and lands
    /// as the new plate finishes; with motion off it simply stands there.
    /// </summary>
    private void DrawBead(ImDrawListPtr dl, float railLeft)
    {
        var flair = Theme.Flair;
        var station = Array.IndexOf(Tabs, ui.Tab);
        if (flair == Flair.Plain || station < 0)
        {
            travelFrom = -1;
            return;
        }

        var progress = Theme.UiMotion ? Motion.Pulse(StationKey, MotionTokens.Travel) : -1f;
        var y = iconCenters[station];
        if (progress < 0f)
        {
            travelFrom = -1;
        }
        else if (travelFrom >= 0 && travelFrom < Tabs.Length && travelFrom != station)
        {
            var from = iconCenters[travelFrom];
            y = from + ((y - from) * MotionMath.EaseInOutCubic(progress));
        }

        if (flair == Flair.Quiet)
        {
            var dot = new Vector2(railLeft + UiMetrics.Px(QuietBeadInsetLogical), y);
            dl.AddCircleFilled(dot, UiMetrics.Px(QuietBeadRadiusLogical), Theme.MoonU32, 12);
            return;
        }

        var c = new Vector2(railLeft + UiMetrics.Px(BeadInsetLogical), y);
        var r = UiMetrics.Px(BeadRadiusLogical);
        if (Theme.ShowGlow)
        {
            // The bead's own light: a soft MoonHigh halo, faint at its edge.
            dl.AddCircleFilled(c, r + UiMetrics.Px(6f), Theme.WithAlpha(Theme.MoonHigh, 0.7f * 0.05f), 20);
            dl.AddCircleFilled(c, r + UiMetrics.Px(3.5f), Theme.WithAlpha(Theme.MoonHigh, 0.7f * 0.10f), 20);
        }

        dl.AddCircleFilled(c, r + UiMetrics.Px(BeadRimLogical), Theme.WithAlpha(Theme.Night, 1f), 16);
        dl.AddCircleFilled(c, r, Theme.MoonHighU32, 16);
        // Lit from the upper left, as every light on the Moon Road.
        dl.AddCircleFilled(c - new Vector2(r * 0.25f, r * 0.3f), r * 0.45f, Theme.WithAlpha(Vector4.One, 0.55f), 10);
    }

    /// <summary>
    /// Where the Journal badge sits: one fixed size (16 px tall, at least 16 wide), its left edge just inside the icon's
    /// right and its top over the icon, held inside the station. At Plain it is the gold number alone.
    /// </summary>
    private static (Vector2 Min, Vector2 Max) BadgeRect(string text, Vector2 iconCenter, float iconSize, Vector2 stationMin, Vector2 stationMax, bool plain)
    {
        var font = UiMetrics.Px(BadgeTextLogical);
        var textWidth = ImGui.CalcTextSize(text).X * font / MathF.Max(1f, ImGui.GetFontSize());
        var height = MathF.Round(UiMetrics.Px(BadgeLogical));
        var width = plain ? MathF.Ceiling(textWidth) : MathF.Max(height, MathF.Round(textWidth + (2f * UiMetrics.Px(BadgePadLogical))));
        var edge = UiMetrics.Px(1f);
        var left = MathF.Round(iconCenter.X + (iconSize * 0.5f) - UiMetrics.Px(BadgeOverlapLogical));
        left = MathF.Min(left, stationMax.X - edge - width);
        var top = MathF.Round(iconCenter.Y - (iconSize * 0.5f) - UiMetrics.Px(BadgeRiseLogical));
        top = MathF.Max(top, stationMin.Y + edge);
        return (new Vector2(left, top), new Vector2(left + width, top + height));
    }

    /// <summary>The Journal badge: Moon with Night text in a thin Abyss rim at full strength whatever the station's state; at Plain a gold number.</summary>
    private static void DrawBadge(ImDrawListPtr dl, string text, Vector2 min, Vector2 max, bool plain)
    {
        var font = ImGui.GetFont();
        var size = UiMetrics.Px(BadgeTextLogical);
        var textWidth = ImGui.CalcTextSize(text).X * size / MathF.Max(1f, ImGui.GetFontSize());
        var center = (min + max) * 0.5f;
        var textPos = new Vector2(MathF.Round(center.X - (textWidth * 0.5f)), MathF.Round(center.Y - (size * 0.5f)));
        if (plain)
        {
            dl.AddText(font, size, textPos, Theme.MoonU32, text);
            return;
        }

        var radius = (max.Y - min.Y) * 0.5f;
        var rim = UiMetrics.Px(1.5f);
        dl.AddRectFilled(min - new Vector2(rim), max + new Vector2(rim), Theme.AbyssU32, radius + rim);
        dl.AddRectFilled(min, max, Theme.MoonU32, radius);
        dl.AddText(font, size, textPos, Theme.NightU32, text);
    }

    /// <summary>
    /// A fitted label (<see cref="RailLabel.Fit"/>) centred on <paramref name="centerX"/> from <paramref name="top"/>: one
    /// or two lines at the fit's size, glyph by glyph when it is tracked (ImGui has no letter spacing).
    /// </summary>
    private static void DrawLabel(ImDrawListPtr dl, string label, RailLabelFit fit, float centerX, float top, uint color)
    {
        var text = label.AsSpan().Trim();
        var offset = label.Length - label.AsSpan().TrimStart().Length;
        var size = UiMetrics.Px(fit.Size);
        var tracking = UiMetrics.Px(fit.Tracking);
        if (fit.Lines >= 2 && fit.Break - offset > 0 && fit.Break - offset < text.Length)
        {
            var at = fit.Break - offset;
            DrawLine(dl, text[..at].TrimEnd(), size, tracking, centerX, top, color);
            DrawLine(dl, text[(at + 1)..].TrimStart(), size, tracking, centerX, MathF.Round(top + (size * RailLabel.LineHeight)), color);
            return;
        }

        DrawLine(dl, text, size, tracking, centerX, top, color);
    }

    private static void DrawLine(ImDrawListPtr dl, ReadOnlySpan<char> text, float size, float tracking, float centerX, float top, uint color)
    {
        var font = ImGui.GetFont();
        var perPx = size / MathF.Max(1f, ImGui.GetFontSize());
        var width = (ImGui.CalcTextSize(text).X * perPx) + (tracking * (text.Length - 1));
        var x = MathF.Round(centerX - (width * 0.5f));
        if (tracking == 0f)
        {
            dl.AddText(font, size, new Vector2(x, top), color, text);
            return;
        }

        var pen = x;
        for (var i = 0; i < text.Length; i++)
        {
            var glyph = text.Slice(i, 1);
            dl.AddText(font, size, new Vector2(MathF.Round(pen), top), color, glyph);
            pen += (ImGui.CalcTextSize(glyph).X * perPx) + tracking;
        }
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
    /// done / total on hover; left out on a short window, <see cref="RailFit.GaugeHidden"/>), its percentage under it
    /// where the height allows, then the Todo overlay and Nearby buttons (1.7.0) over Help and Settings, two a row on
    /// the labelled rail and stacked on the compact one. Each part takes the height <see cref="LayoutBudgets.FootHeight(RailFit, bool, float)"/>
    /// gives it, so the foot is exactly as tall as the fit reserved; <paramref name="button"/> is the buttons' side as
    /// drawn (<see cref="UiMetrics.MinTarget"/>).
    /// </summary>
    private void DrawFoot(ImDrawListPtr dl, float centerX, float top, RailFit fit, float button, float fraction, Action? openHelp, Action? openSettings, bool moonRoad)
    {
        var gap = UiMetrics.Px(LayoutBudgets.RailGapLogical);
        if (fit.GaugeHidden)
        {
            DrawFootButtons(centerX, top, button, gap, openHelp, openSettings);
            return;
        }

        var percent = fit.Percent;
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
            // At the rail labels' size, as one item so its hover shows the gauge's tooltip.
            var size = LabelPx();
            var width = percentWidth * size;
            var pos = new Vector2(MathF.Round(centerX - width * 0.5f), y);
            ImGui.SetCursorScreenPos(pos);
            ImGui.Dummy(new Vector2(MathF.Max(1f, width), size));
            gaugeHovered |= ImGui.IsItemHovered();
            dl.AddText(ImGui.GetFont(), size, pos, ImGui.GetColorU32(ImGuiCol.Text), percentText);
            y += UiMetrics.Px(LayoutBudgets.PercentLine(percent: true));
        }

        if (gaugeHovered)
        {
            UiMetrics.Tooltip(Strings.FillingMoonTooltip, progressText);
        }

        DrawFootButtons(centerX, y, button, gap, openHelp, openSettings);
    }

    /// <summary>
    /// The foot's buttons from <paramref name="y"/> down: Overlay and Nearby, then Help and Settings; two a row on the
    /// labelled rail, stacked on the compact one (<see cref="LayoutBudgets.FootButtonsHeight"/>).
    /// </summary>
    private void DrawFootButtons(float centerX, float y, float button, float gap, Action? openHelp, Action? openSettings)
    {
        var overlayOn = OverlayOn?.Invoke() == true;
        var nearbyOpen = NearbyOpen?.Invoke() == true;
        var overlayTip = overlayOn ? Strings.RailOverlayOnTooltip : Strings.RailOverlayOffTooltip;
        var between = UiMetrics.Px(4f);
        if (Compact)
        {
            var x = centerX - button * 0.5f;
            FootButton("##railOverlay", OverlayIcon, overlayTip, ToggleOverlay, UiRects.OverlayButton, new Vector2(x, y), overlayOn);
            y += button + gap;
            FootButton("##railNearby", NearbyIcon, Strings.RailNearbyTooltip, ToggleNearby, UiRects.NearbyButton, new Vector2(x, y), nearbyOpen);
            y += button + gap;
            FootButton("##railHelp", HelpIcon, Strings.HelpButtonTooltip, openHelp, UiRects.HelpButton, new Vector2(x, y));
            y += button + gap;
            FootButton("##railSettings", SettingsIcon, Strings.SettingsButtonTooltip, openSettings, UiRects.SettingsButton, new Vector2(x, y));
        }
        else
        {
            var left = MathF.Round(centerX - button - between * 0.5f);
            FootButton("##railOverlay", OverlayIcon, overlayTip, ToggleOverlay, UiRects.OverlayButton, new Vector2(left, y), overlayOn);
            FootButton("##railNearby", NearbyIcon, Strings.RailNearbyTooltip, ToggleNearby, UiRects.NearbyButton, new Vector2(left + button + between, y), nearbyOpen);
            y += button + gap;
            FootButton("##railHelp", HelpIcon, Strings.HelpButtonTooltip, openHelp, UiRects.HelpButton, new Vector2(left, y));
            FootButton("##railSettings", SettingsIcon, Strings.SettingsButtonTooltip, openSettings, UiRects.SettingsButton, new Vector2(left + button + between, y));
        }
    }

    /// <summary>The Todo overlay button (1.7.0): shows or hides the overlay; null draws it disabled.</summary>
    public Action? ToggleOverlay { get; set; }

    /// <summary>Whether the Todo overlay is on, so its button reads as pressed.</summary>
    public Func<bool>? OverlayOn { get; set; }

    /// <summary>The Nearby button (1.7.0): opens or closes the Nearby quests window; null draws it disabled.</summary>
    public Action? ToggleNearby { get; set; }

    /// <summary>Whether the Nearby quests window is open, so its button reads as pressed.</summary>
    public Func<bool>? NearbyOpen { get; set; }

    private void FootButton(string id, string icon, string tooltip, Action? action, string rectKey, Vector2 pos, bool active = false)
    {
        ImGui.SetCursorScreenPos(pos);
        if (Chrome.IconButtonRound(id, icon, action is null ? Strings.ActionUnavailable : tooltip, active: active, enabled: action is not null))
        {
            action?.Invoke();
        }

        ui.RecordItem(rectKey);
    }

    /// <summary>
    /// A FontAwesome station icon filling its <paramref name="box"/>: drawn at the size that fits the glyph's height and
    /// width in the box (FontAwesome glyphs sit a little inside their em, and Users is wider than tall).
    /// </summary>
    private static void DrawIcon(ImDrawListPtr dl, Vector2 center, float box, string icon, uint color)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        try
        {
            var font = ImGui.GetFont();
            var native = ImGui.CalcTextSize(icon);
            var em = MathF.Max(1f, ImGui.GetFontSize());
            var size = MathF.Round(MathF.Min(box * 0.86f, box * em / MathF.Max(1f, native.X)));
            var drawn = native * (size / em);
            dl.AddText(font, size, new Vector2(MathF.Round(center.X - drawn.X * 0.5f), MathF.Round(center.Y - drawn.Y * 0.5f)), color, icon);
        }
        finally
        {
            ImGui.PopFont();
        }
    }

    /// <summary>
    /// Fits the labels to their plates (<see cref="RailLabel.Fit"/>) when the language, the label size, the room or the
    /// lines the stations take changed: in logical px, so the UI scale moves the rail, the plate and the label together.
    /// </summary>
    private void FitLabels(float stationPx)
    {
        var scale = MathF.Max(0.01f, UiMetrics.Scale);
        var size = LayoutBudgets.RailLabelLogical(ImGui.GetFontSize() / scale);
        var flair = Theme.Flair;
        var room = LayoutBudgets.RailLabelRoom(flair);
        // Two lines where the station has the height for them under its icon, inside its plate.
        var below = (stationPx / scale) - (2f * LayoutBudgets.RailPlateInsetLogical) - LayoutBudgets.StationIcon(flair) - LayoutBudgets.StationGapLogical;
        var lines = below >= size * (1f + RailLabel.LineHeight) ? 2 : 1;
        if (fitsLanguage == Localization.Loc.Version && fitsSize == size && fitsRoom == room && fitsLines == lines)
        {
            return;
        }

        fitsLanguage = Localization.Loc.Version;
        fitsSize = size;
        fitsRoom = room;
        fitsLines = lines;
        var labels = Labels;
        for (var i = 0; i < labels.Length; i++)
        {
            fits[i] = RailLabel.Fit(labels[i], size, LayoutBudgets.RailLabelMinLogical, room, lines, MeasureEm);
        }
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

    /// <summary>
    /// The Journal station's hover text: what it holds, then what the badge counts (the newly ready quests and the Ready
    /// ones, the story and unlock quests, or the Ready count), then "Click to show the new ones" while the pointer is on a
    /// badge that opens them. Rebuilt only when one of those or the language changes.
    /// </summary>
    private string JournalTooltip(RailBadge badge, int everyReady, bool onBadge)
    {
        if (badge == tooltipBadge && everyReady == tooltipEveryReady && onBadge == tooltipOnBadge && tooltipLanguage == Localization.Loc.Version && journalTooltip.Length > 0)
        {
            return journalTooltip;
        }

        tooltipBadge = badge;
        tooltipEveryReady = everyReady;
        tooltipOnBadge = onBadge;
        tooltipLanguage = Localization.Loc.Version;
        var c = CultureInfo.CurrentCulture;
        var ready = string.Format(c, Strings.TreeReadyBadgeFormat, badge.Mode == JournalBadgeMode.EveryReady ? everyReady : badge.Ready);
        var counts = badge.Mode switch
        {
            JournalBadgeMode.NewlyReady when badge.New > 0 => string.Format(c, Strings.RailNewlyReadyFormat, badge.New) + Strings.RailCountSeparator + ready,
            JournalBadgeMode.StoryAndUnlock when badge.StoryReady > 0 => string.Format(c, Strings.RailStoryReadyFormat, badge.StoryReady) + Strings.RailCountSeparator + ready,
            JournalBadgeMode.EveryReady when everyReady > 0 => ready,
            _ => badge.Ready > 0 ? ready : string.Empty,
        };

        journalTooltip = Strings.TabJournalTooltip
            + (counts.Length > 0 ? "\n" + counts : string.Empty)
            + (onBadge ? "\n" + Strings.RailNewlyReadyClick : string.Empty);
        return journalTooltip;
    }
}
