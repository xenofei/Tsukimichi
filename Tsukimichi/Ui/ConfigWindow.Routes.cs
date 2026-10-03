namespace Tsukimichi.Ui;

/// <summary>
/// The 1.6.0 route rows (R6 A and B, C3 C). Settings › Overlay &amp; routes › Routes: whether the followed route's map
/// flag moves on to the next stop as steps are turned in, which works with the Todo overlay off. Show in overlay: the
/// followed route's section and the optional Next stops section (off by default).
/// </summary>
public sealed partial class ConfigWindow
{
    private void DrawRoutes()
    {
        Header(Strings.ConfigSectionRoutes);
        var advance = settings.RouteFlagAdvance;
        if (Toggle(Strings.TodoConfigRouteFlagAdvance, Strings.TodoConfigRouteFlagAdvanceHint, ref advance, "route map flag next stop"))
        {
            settings.RouteFlagAdvance = advance;
            Save();
        }
    }

    /// <summary>The overlay's route sections; drawn among Show in overlay, dimmed while it is off (<paramref name="overlayOn"/>).</summary>
    private void DrawTodoRouteToggles(bool overlayOn)
    {
        var off = Strings.SettingsOverlayOffReason;
        var route = settings.TodoShowRoute;
        if (Toggle(Strings.TodoConfigShowRoute, Strings.TodoConfigShowRouteHint, ref route, "overlay section route followed", overlayOn, reason: off))
        {
            settings.TodoShowRoute = route;
            Save();
        }

        var stops = settings.TodoShowNextStops;
        if (Toggle(Strings.TodoConfigShowNextStops, Strings.TodoConfigShowNextStopsHint, ref stops, "overlay section next stops aetheryte", overlayOn, reason: off))
        {
            settings.TodoShowNextStops = stops;
            Save();
        }
    }
}
