using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Todo overlay, the 1.6.0 rows (R6 A and B, C3 C): the followed route's section, whether its map flag
/// moves on to the next stop as steps are turned in, and the optional Next stops section (off by default).
/// </summary>
public sealed partial class ConfigWindow
{
    private void DrawTodoRouteToggles()
    {
        var route = settings.TodoShowRoute;
        if (ImGui.Checkbox(Strings.TodoConfigShowRoute, ref route))
        {
            settings.TodoShowRoute = route;
            Save();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.TodoConfigShowRouteHint);
        }

        using (ImRaii.PushIndent())
        {
            var advance = settings.RouteFlagAdvance;
            if (ImGui.Checkbox(Strings.TodoConfigRouteFlagAdvance, ref advance))
            {
                settings.RouteFlagAdvance = advance;
                Save();
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.TodoConfigRouteFlagAdvanceHint);
            }
        }

        var stops = settings.TodoShowNextStops;
        if (ImGui.Checkbox(Strings.TodoConfigShowNextStops, ref stops))
        {
            settings.TodoShowNextStops = stops;
            Save();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.TodoConfigShowNextStopsHint);
        }
    }
}
