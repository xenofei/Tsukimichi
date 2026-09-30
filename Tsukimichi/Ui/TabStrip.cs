using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The main window's rail (feature plan v4 L7, design v4 §7.1), drawn in its own fixed pane left of the tree
/// (<see cref="PaneSplit"/>). Top to bottom: the crest (a click shows Journal › All quests), one station per tab (a
/// 22 px icon over a small label: Journal's icon is a filling moon of overall completion with the Ready count as a
/// badge on it, the others FontAwesome glyphs), and at the foot the overall gauge with its percentage and the round
/// Help and Settings buttons, which live here rather than in the toolbar. The rail is 64 logical px wide; on a window
/// under about 1,040 px, or by Settings › Display › Compact rail, it is a 44 px compact rail of icons whose labels are
/// in the tooltips (<see cref="LayoutBudgets.CompactRail"/>). On a short window the rail gives up height in a fixed
/// order (<see cref="LayoutBudgets.FitRail"/>) and scrolls only past that.
/// <para>
/// Each station is a real item (an <see cref="ImGui.InvisibleButton(string, Vector2)"/> with the focus ring), so
/// keyboard and gamepad navigation reach it. <see cref="UiState.Tab"/> is the single source of truth: a click writes
/// it, and a programmatic switch (Reveal, ShowIssuer, the tutorial, help) is simply the next frame's active station.
/// The union of the stations is recorded as <see cref="UiRects.Tabs"/>, and the foot's buttons as
/// <see cref="UiRects.HelpButton"/> and <see cref="UiRects.SettingsButton"/>, for the tutorial. Nothing allocates per
/// frame: labels are constants measured once per language and font size, and the tooltips and the percentage are
/// rebuilt only when their counts change.
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

    /// <summary>The overall gauge's fill motion (it moves only when the count changes).</summary>
    private static readonly ulong GaugeKey = Motion.Key(0x5241_494C, 0); // "RAIL"

    private readonly UiState ui;

    private int readyTooltipCount = -1;
    private int readyTooltipLanguage = -1;
    private string readyTooltip = string.Empty;

    // The labels' widths at their own size, measured again only when the language or the font size changes.
    private readonly float[] labelWidths = new float[Tabs.Length];
    private int labelsLanguage = -1;
    private float labelsFontSize = -1f;

    // The foot's texts, rebuilt when the overall count changes.
    private NodeCount gaugeCount = new(-1, -1, 0);
    private string percentText = string.Empty;
    private float percentWidth;
    private string progressText = string.Empty;

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
        var scale = MathF.Max(UiMetrics.Scale, 0.01f);
        var fit = LayoutBudgets.FitRail(avail.Y / scale, Tabs.Length, Compact);
        var dl = ImGui.GetWindowDrawList();
        var centerX = MathF.Round(origin.X + width * 0.5f);
        var gap = UiMetrics.Px(LayoutBudgets.RailGapLogical);
        var y = origin.Y + UiMetrics.Px(LayoutBudgets.RailPadLogical);

        MeasureLabels();
        RefreshGauge(overall);

        if (fit.Crest > 0f)
        {
            var crest = UiMetrics.Px(fit.Crest);
            DrawCrest(dl, new Vector2(centerX - crest * 0.5f, y), crest);
            y += crest + gap;
        }

        var stationHeight = MathF.Round(UiMetrics.Px(fit.Station));
        var first = y;
        for (var i = 0; i < Tabs.Length; i++)
        {
            DrawStation(dl, i, new Vector2(origin.X, y), width, stationHeight, overall.Fraction, ready);
            y += stationHeight;
        }

        ui.RecordRect(UiRects.Tabs, new Vector2(origin.X, first), new Vector2(origin.X + width, y));

        var footHeight = UiMetrics.Px(LayoutBudgets.FootHeight(fit.Percent, Compact));
        var bottom = origin.Y + avail.Y - UiMetrics.Px(LayoutBudgets.RailPadLogical);
        var footTop = fit.FootAnchored ? MathF.Max(y + gap, bottom - footHeight) : y + gap;
        var footBottom = DrawFoot(dl, centerX, footTop, fit.Percent, overall.Fraction, openHelp, openSettings);

        // The rail's content ends under the foot, so a rail taller than its pane scrolls to it.
        ImGui.SetCursorScreenPos(new Vector2(origin.X, footBottom + UiMetrics.Px(LayoutBudgets.RailPadLogical)));
        ImGui.Dummy(new Vector2(1f, 1f));
    }

    /// <summary>
    /// The crest (a stand-in until the ornament atlas lands): a gold moon in a soft halo above the horizon, with the
    /// road of light on the water under it, in a thin MoonDeep ring. A click shows Journal › All quests.
    /// </summary>
    private void DrawCrest(ImDrawListPtr dl, Vector2 min, float size)
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

        Chrome.FocusRing(size * 0.5f);
        if (hovered)
        {
            UiMetrics.Tooltip(Strings.AllQuests);
        }
    }

    /// <summary>
    /// One station: the icon (with the Journal badge) and, on the labelled rail, the label under it in the secondary
    /// tone (primary when active or hovered), ending in an ellipsis where it is wider than the station. The active
    /// station has the hover-tone fill, a soft glow behind its icon and a 2 px Moon bar on the left edge. The tooltip
    /// names the tab first whenever the label is not shown whole.
    /// </summary>
    private void DrawStation(ImDrawListPtr dl, int i, Vector2 min, float width, float height, float overallFraction, int ready)
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
        if (active)
        {
            dl.AddRectFilled(fillMin, fillMax, Theme.U32(s.Hover), rounding);
            var bar = MathF.Max(2f, MathF.Round(UiMetrics.Px(2f)));
            dl.AddRectFilled(new Vector2(min.X, min.Y + UiMetrics.Px(8f)), new Vector2(min.X + bar, max.Y - UiMetrics.Px(8f)), Theme.MoonU32, bar * 0.5f);
        }
        else if (hovered)
        {
            dl.AddRectFilled(fillMin, fillMax, Theme.WithAlpha(s.Hover, 0.6f), rounding);
        }

        // Icon, centred; on the labelled rail the icon and the label are centred together.
        var iconSize = UiMetrics.Px(LayoutBudgets.StationIconLogical);
        var labelHeight = ImGui.GetFontSize() * LayoutBudgets.RailLabelFraction;
        var contentHeight = Compact ? iconSize : iconSize + UiMetrics.Px(LayoutBudgets.StationGapLogical) + labelHeight;
        var top = min.Y + MathF.Max(0f, (height - contentHeight) * 0.5f);
        var iconCenter = new Vector2(MathF.Round(min.X + width * 0.5f), MathF.Round(top + iconSize * 0.5f));
        if (active)
        {
            dl.AddCircleFilled(iconCenter, UiMetrics.Px(18f), Theme.WithAlpha(Theme.Moon, 0.06f), 24);
        }

        if (tab == NavTab.Journal)
        {
            MoonGlyph.DrawFilling(dl, iconCenter, iconSize * 0.45f, overallFraction);
            if (ready > 0)
            {
                // The Ready count on the icon's top right.
                Chrome.Badge(dl, iconCenter + new Vector2(iconSize * 0.55f, -iconSize * 0.42f), ready, actionable: true);
            }
        }
        else
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
            var labelPos = new Vector2(MathF.Round(min.X + (width - shown) * 0.5f), MathF.Round(top + iconSize + UiMetrics.Px(LayoutBudgets.StationGapLogical)));
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
    /// The foot: the overall gauge (a halo filled to the overall completion, done / total on hover), its percentage
    /// under it where the height allows, then Help and Settings, side by side on the labelled rail and stacked on the
    /// compact one. Returns the foot's bottom edge.
    /// </summary>
    private float DrawFoot(ImDrawListPtr dl, float centerX, float top, bool percent, float fraction, Action? openHelp, Action? openSettings)
    {
        var gap = UiMetrics.Px(LayoutBudgets.RailGapLogical);
        var gauge = UiMetrics.Px(LayoutBudgets.RailGaugeLogical);
        var radius = gauge * 0.5f;
        var center = new Vector2(centerX, MathF.Round(top + radius));
        ImGui.SetCursorScreenPos(center - new Vector2(radius));
        ImGui.Dummy(new Vector2(gauge, gauge));
        var gaugeHovered = ImGui.IsItemHovered();
        MoonGlyph.DrawHalo(dl, center, radius - UiMetrics.Px(1f), Motion.Gauge(GaugeKey, fraction));
        var y = top + gauge + gap;

        if (percent)
        {
            ImGui.SetWindowFontScale(LayoutBudgets.RailLabelFraction);
            var line = ImGui.GetTextLineHeight();
            ImGui.SetCursorScreenPos(new Vector2(MathF.Round(centerX - percentWidth * ImGui.GetFontSize() * 0.5f), y));
            ImGui.TextUnformatted(percentText);
            gaugeHovered |= ImGui.IsItemHovered();
            ImGui.SetWindowFontScale(1f);
            y += line + gap;
        }

        if (gaugeHovered)
        {
            UiMetrics.Tooltip(Strings.FillingMoonTooltip, progressText);
        }

        var button = UiMetrics.MinTarget;
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

        return y + button;
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

    /// <summary>The foot's percentage and progress texts, rebuilt when the overall count changes.</summary>
    private void RefreshGauge(NodeCount overall)
    {
        if (overall == gaugeCount && percentText.Length > 0)
        {
            return;
        }

        gaugeCount = overall;
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
