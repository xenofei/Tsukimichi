namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Automation › Allagan Tools (1.6.0 hand-in items): "Count with Allagan Tools" (the Hand in section's
/// retainer counts and Moonlit's relic ownership). Whether Allagan Tools is loaded is listed under Companion plugins;
/// the item hint's "quests that need an item" lives under In game › Menus and tooltips. Read per use, so no callback.
/// </summary>
public sealed partial class ConfigWindow
{
    private void DrawHandInIntegrations()
    {
        Header(Strings.ConfigSectionHandIn);
        var allagan = settings.HandInAllaganTools;
        if (Toggle(Strings.ConfigHandInAllagan, Strings.ConfigHandInAllaganHint, ref allagan, "allagan tools retainers inventory hand-in items count"))
        {
            settings.HandInAllaganTools = allagan;
            Save();
        }
    }
}
