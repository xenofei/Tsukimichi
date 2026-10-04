using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Route;
using Tsukimichi.Core.Unlocks;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// Find by unlock in the toolbar search (plan v7, 1.19.0 K3; spec-1.19 "The toolbar search"): while the search pill
/// has the caret (or the pointer is on the list) and its text matches what quests open, an <b>Unlocks</b> group hangs
/// under the pill. Each row has the kind chip, the name in Text, "via &lt;quest&gt;" (or, for flying, the zone's aether
/// currents) in Secondary, and a quiet <b>Route to unlock</b>. The quests themselves stay in the table, which the same
/// text filters (an unlock name finds its quest there too). What a quest the spoiler shield masks opens is never
/// listed. The matches are worked out once per query, index and shield, so drawing them allocates nothing.
/// </summary>
public sealed partial class MainWindow
{
    private const int MaxUnlockResults = 6;
    private const int MinUnlockQueryLength = 2;
    private const float UnlockResultsMinLogical = 420f;
    private const string UnlockResultsWindowId = "##tsukiUnlockResults";

    private Vector2 searchPillMin;
    private Vector2 searchPillMax;
    private bool unlockResultsHovered;

    // The matches for the last query, index revision, shield and reach, with their printed lines.
    private string unlockQuery = string.Empty;
    private int unlockRevision = -1;
    private int unlockShield = int.MinValue;
    private byte unlockReach;
    private bool unlockFlight;
    private UnlockResultRow[] unlockRows = [];

    /// <summary>The flying zones, for a flying row's "10 aether currents, 4 from quests"; null while they are read. Set by the plugin.</summary>
    public Func<FlightIndex?>? FlightZones { get; set; }

    /// <summary>One row of the group, every string ready to draw.</summary>
    private sealed record UnlockResultRow(UnlockFind Find, string Kind, string Label, string Via, string Tooltip);

    /// <summary>The Unlocks group under the search pill, drawn as a small window of its own so it floats over the panes.</summary>
    private void DrawUnlockResults(SessionState session)
    {
        var rows = UnlockRows(session);
        if (rows.Length == 0 || !(searchActive || unlockResultsHovered))
        {
            unlockResultsHovered = false;
            return;
        }

        var width = MathF.Max(searchPillMax.X - searchPillMin.X, UiMetrics.Px(UnlockResultsMinLogical));
        var room = ImGui.GetWindowPos().X + ImGui.GetWindowSize().X - searchPillMin.X - UiMetrics.Px(8f);
        width = MathF.Max(searchPillMax.X - searchPillMin.X, MathF.Min(width, room));
        ImGui.SetNextWindowPos(new Vector2(searchPillMin.X, searchPillMax.Y + UiMetrics.Px(4f)), ImGuiCond.Always);
        ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0f), new Vector2(width, float.MaxValue));
        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav
            | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoDocking;
        var popupOpen = ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel);
        using var style = GamePanelShell.PushPanelStyle(false);
        var hovered = false;
        try
        {
            if (ImGui.Begin(UnlockResultsWindowId, flags))
            {
                // Over the main window even after a click brought it forward, but never over an open popup.
                if (!popupOpen)
                {
                    ImGuiP.BringWindowToDisplayFront(ImGuiP.GetCurrentWindow());
                }

                UiMetrics.ApplyFontScale();
                hovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
                DrawUnlockRows(rows);
            }
        }
        finally
        {
            ImGui.End();
        }

        unlockResultsHovered = hovered;
    }

    private void DrawUnlockRows(UnlockResultRow[] rows)
    {
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        // The group's head in the small caps the pane sections use (the tracked Eyebrow role where the look has it).
        var moonRoad = Theme.Sectioned;
        var heading = moonRoad ? SectionHeading.Label(Strings.FindUnlocksHeading) : Strings.FindUnlocksHeading;
        using (moonRoad ? Typography.Eyebrow(heading) : Typography.Caption())
        using (Theme.PushText(moonRoad ? s.OrnamentHigh : s.TextSecondary))
        {
            ImGui.TextUnformatted(heading);
        }

        var routeWidth = Chrome.ActionPillWidth(ActionGlyphs.Route, Strings.RouteToUnlockAction, PillLayout.Row);
        var rowHeight = MathF.Max(Chrome.PillHeight(PillLayout.Row), ImGui.GetTextLineHeight()) + UiMetrics.Px(6f);
        var gap = UiMetrics.Px(8f);
        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            var left = ImGui.GetCursorScreenPos();
            var right = left.X + ImGui.GetContentRegionAvail().X;
            var textY = MathF.Round(left.Y + ((rowHeight - ImGui.GetTextLineHeight()) * 0.5f));

            // The kind chip: a hairline capsule with the kind's word, in Secondary.
            float chipWidth;
            using (Typography.Caption())
            {
                var line = ImGui.GetTextLineHeight();
                chipWidth = ImGui.CalcTextSize(row.Kind).X + UiMetrics.Px(14f);
                var chipMin = new Vector2(left.X, MathF.Round(left.Y + ((rowHeight - line - UiMetrics.Px(4f)) * 0.5f)));
                var chipMax = chipMin + new Vector2(chipWidth, line + UiMetrics.Px(4f));
                dl.AddRect(chipMin, chipMax, Theme.U32(s.Line), (chipMax.Y - chipMin.Y) * 0.5f, ImDrawFlags.None, UiMetrics.Hairline);
                dl.AddText(new Vector2(chipMin.X + UiMetrics.Px(7f), chipMin.Y + UiMetrics.Px(2f)), Theme.U32(s.TextSecondary), row.Kind);
            }

            var x = left.X + chipWidth + gap;
            var routeX = right - routeWidth;
            var room = MathF.Max(1f, routeX - gap - x);
            var labelWidth = ImGui.CalcTextSize(row.Label).X;
            Chrome.EllipsisTextAt(dl, new Vector2(x, textY), room, row.Label, Theme.U32(s.Text), labelWidth);
            var viaX = x + MathF.Min(labelWidth, room) + UiMetrics.Px(8f);
            if (row.Via.Length > 0 && viaX < routeX - gap)
            {
                Chrome.EllipsisTextAt(dl, new Vector2(viaX, textY), routeX - gap - viaX, row.Via, Theme.U32(s.TextSecondary));
            }

            ImGui.SetCursorScreenPos(new Vector2(left.X, left.Y));
            ImGui.Dummy(new Vector2(MathF.Max(1f, routeX - gap - left.X), rowHeight));
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(row.Tooltip);
            }

            ImGui.SetCursorScreenPos(new Vector2(routeX, MathF.Round(left.Y + ((rowHeight - Chrome.PillHeight(PillLayout.Row)) * 0.5f))));
            using (ImRaii.PushId(i))
            {
                if (Chrome.ActionPill("##routeToUnlock", ActionGlyphs.Route, Strings.RouteToUnlockAction, PillTone.Quiet, true, Strings.RouteToUnlockTooltip, PillLayout.Row))
                {
                    ui.OpenRoute(RouteToUnlock(row.Find));
                    unlockResultsHovered = false;
                }
            }

            ImGui.SetCursorScreenPos(new Vector2(left.X, left.Y + rowHeight));
            if (i < rows.Length - 1)
            {
                Chrome.Hairline();
            }
        }

        ImGui.Dummy(new Vector2(1f, UiMetrics.Px(2f)));
    }

    /// <summary>Route to unlock <paramref name="find"/>: flying carries its zone, so the Route window adds the field currents.</summary>
    private RouteTarget RouteToUnlock(UnlockFind find) =>
        RouteTarget.ForUnlock(find, find.NeedsAll && FlightZones?.Invoke()?.ZoneOfQuests(find.Quests) is { } zone ? zone.TerritoryId : 0);

    /// <summary>This frame's rows: worked out again only when the search, the index, the shield, the reach or the flying zones changed.</summary>
    private UnlockResultRow[] UnlockRows(SessionState session)
    {
        var query = SearchIndex.Normalize(ui.SearchText);
        var source = runner.Unlocks;
        var flight = FlightZones?.Invoke() is not null;
        var revision = source?.Revision ?? -1;
        var shield = session.Spoilers.Fingerprint;
        var reach = runner.UnlockReach;
        if (string.Equals(query, unlockQuery, StringComparison.Ordinal) && revision == unlockRevision && shield == unlockShield && reach == unlockReach && flight == unlockFlight)
        {
            return unlockRows;
        }

        unlockQuery = query;
        unlockRevision = revision;
        unlockShield = shield;
        unlockReach = reach;
        unlockFlight = flight;
        unlockRows = [];
        if (source is null || query.Replace(" ", string.Empty, StringComparison.Ordinal).Length < MinUnlockQueryLength || session.Bundle is not { } bundle)
        {
            return unlockRows;
        }

        var spoilers = session.Spoilers;
        var catalog = bundle.Catalog;
        var matches = source.Current.Find(query, rowId => !spoilers.IsMasked(rowId), reach, MaxUnlockResults);
        var rows = new List<UnlockResultRow>(matches.Count);
        var index = FlightZones?.Invoke();
        foreach (var match in matches)
        {
            var find = match.Find;
            var via = catalog.GetByRowId(match.Via) is { } quest ? string.Format(CultureInfo.CurrentCulture, Strings.FindUnlockViaFormat, spoilers.DisplayName(quest)) : string.Empty;
            if (find.NeedsAll && index?.ZoneOfQuests(find.Quests) is { } zone)
            {
                via = UnlockFindKinds.FlyingSummary(zone.TotalCurrents, zone.QuestCurrents.Count);
            }

            var kind = find.Kind is { } k ? UnlockFindKinds.Name(k) : UnlockTargets.Name(find.Target);
            var tooltip = find.Label + "\n" + UnlockTargets.Name(find.Target) + (via.Length > 0 ? " · " + via : string.Empty);
            rows.Add(new UnlockResultRow(find, kind, find.Label, via, tooltip));
        }

        unlockRows = [.. rows];
        return unlockRows;
    }
}
