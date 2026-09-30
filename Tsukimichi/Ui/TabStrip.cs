using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Tsukimichi.Ui;

/// <summary>
/// The main window's vertical tab rail (T14, ui-revamp §2.2), drawn in its own fixed column of the layout table so the
/// tree column keeps its width (Dalamud developer panel §4). One row per tab: an icon (Journal's is a small filling
/// moon of overall completion, the others FontAwesome glyphs from a family that does not mean a state), the label and,
/// on Journal only, a badge with the Ready count (badges are a count of things to do, game UX panel §2). The active
/// tab has a hover-tone fill, a 3 px Moon bar on the left edge and a primary label; inactive tabs read in the
/// secondary tone. Each row is a real item (an <see cref="ImGui.InvisibleButton(string, Vector2)"/> with the focus
/// ring), so keyboard and gamepad navigation reach it. <see cref="UiState.Tab"/> is the single source of truth: a
/// click writes it, and a programmatic switch (Reveal, ShowIssuer, ShowAbandoned, the tutorial, help) is simply the
/// next frame's active row. The union of the rows is recorded as <see cref="UiRects.Tabs"/> for the tutorial.
/// Nothing allocates per frame: labels are constants and the Ready tooltip is rebuilt only when the count changes.
/// </summary>
public sealed class TabStrip
{
    private const float RowLogical = 30f;
    private const float PadLogical = 6f;
    private const float InsetLogical = 10f;
    private const float IconLogical = 16f;
    private const float GapLogical = 8f;
    private const float BarLogical = 3f;

    private static readonly NavTab[] Tabs = [NavTab.Journal, NavTab.Moonlit, NavTab.Characters, NavTab.Flight, NavTab.Plan];

    private static readonly string[] Labels = [Strings.TabJournal, Strings.TabMoonlit, Strings.TabCharacters, Strings.TabFlight, Strings.PlanTab];

    private static readonly string[] Ids = ["##tabJournal", "##tabMoonlit", "##tabCharacters", "##tabFlight", "##tabPlan"];

    private static readonly string[] Tooltips = [Strings.TabJournalTooltip, Strings.TabMoonlitTooltip, Strings.TabCharactersTooltip, Strings.TabFlightTooltip, Strings.PlanTabTooltip];

    private static readonly string GemIcon = FontAwesomeIcon.Gem.ToIconString();
    private static readonly string UsersIcon = FontAwesomeIcon.Users.ToIconString();
    private static readonly string PlaneIcon = FontAwesomeIcon.Plane.ToIconString();
    private static readonly string PlanIcon = FontAwesomeIcon.ClipboardList.ToIconString();

    private readonly UiState ui;

    private int readyTooltipCount = -1;
    private string readyTooltip = Strings.TabJournalTooltip;

    public TabStrip(UiState ui)
    {
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
    }

    /// <summary>Height of one tab row: 30 logical px, never under the minimum click target.</summary>
    public static float RowHeight => MathF.Max(UiMetrics.Px(RowLogical), UiMetrics.MinTarget);

    /// <summary>
    /// Draws the four rows from the cursor down, the width of the content region, and records their union.
    /// </summary>
    /// <param name="overallFraction">Overall completion for the Journal moon (0 with no character).</param>
    /// <param name="ready">Ready quests for the viewed character; 0 hides the Journal badge.</param>
    public void Draw(float overallFraction, int ready)
    {
        var width = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var height = RowHeight;
        var spacing = UiMetrics.Px(2f);
        var first = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        var s = Theme.Surface;

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0f, spacing));
        for (var i = 0; i < Tabs.Length; i++)
        {
            var tab = Tabs[i];
            var min = ImGui.GetCursorScreenPos();
            var max = min + new Vector2(width, height);
            var active = ui.Tab == tab;
            if (ImGui.InvisibleButton(Ids[i], new Vector2(width, height)) && !active)
            {
                ui.Tab = tab;
                active = true;
            }

            var hovered = ImGui.IsItemHovered();
            var rounding = UiMetrics.Px(4f);
            if (active)
            {
                dl.AddRectFilled(min, max, Theme.U32(s.Hover), rounding);
                var barInset = UiMetrics.Px(PadLogical);
                dl.AddRectFilled(
                    new Vector2(min.X, min.Y + barInset),
                    new Vector2(min.X + UiMetrics.Px(BarLogical), max.Y - barInset),
                    Theme.MoonU32,
                    UiMetrics.Px(BarLogical) * 0.5f);
            }
            else if (hovered)
            {
                dl.AddRectFilled(min, max, Theme.WithAlpha(s.Hover, 0.6f), rounding);
            }

            // Icon.
            var iconSize = UiMetrics.Px(IconLogical);
            var iconCenter = new Vector2(min.X + UiMetrics.Px(InsetLogical) + iconSize * 0.5f, min.Y + height * 0.5f);
            var iconInk = active ? Theme.AccentU32 : Theme.U32(s.TextTertiary);
            if (tab == NavTab.Journal)
            {
                MoonGlyph.DrawFilling(dl, iconCenter, iconSize * 0.5f, overallFraction);
            }
            else
            {
                DrawIcon(dl, iconCenter, tab == NavTab.Moonlit ? GemIcon : tab == NavTab.Characters ? UsersIcon : tab == NavTab.Flight ? PlaneIcon : PlanIcon, iconInk);
            }

            // Badge (Journal only), right-aligned; the label is clipped short of it.
            var labelX = iconCenter.X + iconSize * 0.5f + UiMetrics.Px(GapLogical);
            var labelRight = max.X - UiMetrics.Px(PadLogical);
            if (tab == NavTab.Journal && ready > 0)
            {
                var badgeHeight = MathF.Max(UiMetrics.Px(14f), ImGui.GetFontSize() * 0.72f + UiMetrics.Px(3f));
                var badgeCenter = new Vector2(labelRight - badgeHeight * 0.9f, min.Y + height * 0.5f);
                Chrome.Badge(dl, badgeCenter, ready, actionable: true);
                labelRight = badgeCenter.X - badgeHeight * 0.9f - UiMetrics.Px(4f);
            }

            var label = Labels[i];
            var textSize = ImGui.CalcTextSize(label);
            var textPos = new Vector2(labelX, min.Y + (height - textSize.Y) * 0.5f);
            dl.PushClipRect(new Vector2(labelX, min.Y), new Vector2(MathF.Max(labelX, labelRight), max.Y), true);
            dl.AddText(textPos, Theme.U32(active || hovered ? s.Text : s.TextSecondary), label);
            dl.PopClipRect();

            Chrome.FocusRing(rounding);
            if (hovered)
            {
                UiMetrics.Tooltip(tab == NavTab.Journal ? JournalTooltip(ready) : Tooltips[i]);
            }
        }

        ImGui.PopStyleVar();
        var last = ImGui.GetItemRectMax();
        ui.RecordRect(UiRects.Tabs, first, new Vector2(first.X + width, last.Y));
    }

    private static void DrawIcon(ImDrawListPtr dl, Vector2 center, string icon, uint color)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        var size = ImGui.CalcTextSize(icon);
        dl.AddText(center - size * 0.5f, color, icon);
        ImGui.PopFont();
    }

    /// <summary>The Journal tab's hover text: what it holds, then the Ready count; rebuilt only when the count changes.</summary>
    private string JournalTooltip(int ready)
    {
        if (ready != readyTooltipCount)
        {
            readyTooltipCount = ready;
            readyTooltip = ready > 0
                ? Strings.TabJournalTooltip + "\n" + string.Format(CultureInfo.CurrentCulture, Strings.TreeReadyBadgeFormat, ready)
                : Strings.TabJournalTooltip;
        }

        return readyTooltip;
    }
}
