using Dalamud.Bindings.ImGui;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Integrations: hand-in items (1.6.0), its own block under Integrations: "Count with Allagan Tools" (the
/// Hand in section's retainer counts and Moonlit's relic ownership), "Say which open quests need an item" (the item
/// hint and menu). Whether Allagan Tools, Artisan and GatherBuddy are loaded is listed under Companion plugins. Both
/// settings are read per use, so no callback is needed.
/// </summary>
public sealed partial class ConfigWindow
{
    private void DrawHandInIntegrations()
    {
        Header(Strings.ConfigSectionHandIn);
        var allagan = settings.HandInAllaganTools;
        if (ImGui.Checkbox(Strings.ConfigHandInAllagan, ref allagan))
        {
            settings.HandInAllaganTools = allagan;
            Save();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.ConfigHandInAllaganHint);
        }

        var neededFor = settings.ItemNeededForEnabled;
        if (ImGui.Checkbox(Strings.ConfigItemNeededFor, ref neededFor))
        {
            settings.ItemNeededForEnabled = neededFor;
            Save();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.ConfigItemNeededForHint);
        }
    }
}
