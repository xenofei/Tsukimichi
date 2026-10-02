using Dalamud.Bindings.ImGui;

namespace Tsukimichi.Ui;

/// <summary>
/// The 1.6.0 route rows (R6 A and B, C3 C). Settings › Routes: whether the followed route's map flag moves on to the
/// next stop as steps are turned in, which works with the Todo overlay off, so it sits outside the overlay's block.
/// Settings › Todo overlay: the followed route's section and the optional Next stops section (off by default).
/// </summary>
public sealed partial class ConfigWindow
{
    private void DrawRoutes()
    {
        Header(Strings.ConfigSectionRoutes);
        if (!Row(Strings.TodoConfigRouteFlagAdvance, Strings.TodoConfigRouteFlagAdvanceHint, "route map flag next stop"))
        {
            return;
        }

        var advance = settings.RouteFlagAdvance;
        if (ImGui.Checkbox(Strings.TodoConfigRouteFlagAdvance, ref advance))
        {
            settings.RouteFlagAdvance = advance;
            Save();
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.TodoConfigRouteFlagAdvanceHint);
        }
    }

    /// <summary>The overlay's route sections; drawn inside the overlay's block, greyed while it is off (<paramref name="overlayOn"/>).</summary>
    private void DrawTodoRouteToggles(bool overlayOn)
    {
        if (Row(Strings.TodoConfigShowRoute, Strings.TodoConfigShowRouteHint, "overlay section route followed"))
        {
            using var sub = SubSetting(overlayOn);
            var route = settings.TodoShowRoute;
            if (ImGui.Checkbox(Strings.TodoConfigShowRoute, ref route))
            {
                settings.TodoShowRoute = route;
                Save();
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(Strings.TodoConfigShowRouteHint);
            }
        }

        if (Row(Strings.TodoConfigShowNextStops, Strings.TodoConfigShowNextStopsHint, "overlay section next stops aetheryte"))
        {
            using var sub = SubSetting(overlayOn);
            var stops = settings.TodoShowNextStops;
            if (ImGui.Checkbox(Strings.TodoConfigShowNextStops, ref stops))
            {
                settings.TodoShowNextStops = stops;
                Save();
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(Strings.TodoConfigShowNextStopsHint);
            }
        }
    }
}
