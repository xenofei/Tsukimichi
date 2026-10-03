namespace Tsukimichi.Ui;

/// <summary>
/// Settings › In game › Nearby and server info bar (1.7.0, merged from the cog that Nearby quests had): the "☾ N" count
/// in the server info bar, keeping it at zero, and listing quests ready on another job. The values still live in
/// <c>user/discovery.json</c> (<see cref="Core.Discovery.DiscoverySettings"/>); <see cref="DiscoveryWindow.SettingsChanged"/>
/// saves them and refreshes the window and the entry. Nearby's cog opens Settings on this block.
/// </summary>
public sealed partial class ConfigWindow
{
    /// <summary>The Nearby quests window, which owns the settings; set by the plugin. Null hides the block.</summary>
    public DiscoveryWindow? Nearby { get; set; }

    private void DrawNearbySettings()
    {
        if (Nearby is not { } nearby)
        {
            return;
        }

        var discovery = nearby.Settings;
        Header(Strings.ConfigSectionNearby);
        var show = discovery.ShowDtrEntry;
        if (Toggle(Strings.DiscoveryShowDtrLabel, Strings.DiscoveryShowDtrHint, ref show, "server info bar dtr nearby count"))
        {
            discovery.ShowDtrEntry = show;
            nearby.SettingsChanged(rowsChanged: false);
        }

        var whenEmpty = discovery.DtrShowWhenEmpty;
        if (Toggle(Strings.DiscoveryDtrShowWhenEmptyLabel, Strings.DiscoveryDtrShowWhenEmptyHint, ref whenEmpty, "server info bar dtr nearby zero empty", discovery.ShowDtrEntry, sub: true, reason: Strings.SettingsDtrOffReason))
        {
            discovery.DtrShowWhenEmpty = whenEmpty;
            nearby.SettingsChanged(rowsChanged: false);
        }

        var otherJob = discovery.NearbyIncludeOtherJob;
        if (Toggle(Strings.DiscoveryIncludeOtherJobLabel, Strings.DiscoveryIncludeOtherJobHint, ref otherJob, "nearby quests other job class"))
        {
            discovery.NearbyIncludeOtherJob = otherJob;
            nearby.SettingsChanged(rowsChanged: true);
        }
    }
}
