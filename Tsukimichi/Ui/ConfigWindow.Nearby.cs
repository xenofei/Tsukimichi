using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › In game › Nearby and server info bar (1.7.0, merged from the cog that Nearby quests had): the "☾ N" count
/// in the server info bar, keeping it at zero, and listing quests ready on another job. The values still live in
/// <c>user/discovery.json</c> (<see cref="Core.Discovery.DiscoverySettings"/>); <see cref="DiscoveryWindow.SettingsChanged"/>
/// saves them and refreshes the window and the entry. Nearby's cog opens Settings on this block.
/// </summary>
public sealed partial class ConfigWindow
{
    private static readonly LocArray ServerBarCountsOptions = new(static () => [Strings.ServerBarCountsReady, Strings.ServerBarCountsZone]);

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

        // 1.22.0 M1: one entry. Until the player sets the switch it follows the default (on while Umbra is installed
        // without Tsukimichi for Umbra); setting it makes it theirs.
        var show = Core.Discovery.ServerInfoBar.Shown(discovery.DtrEntryChosen, discovery.ShowDtrEntry, Umbra?.Loaded == true, Umbra?.AddonPresent == true);
        if (Toggle(Strings.DiscoveryShowDtrLabel, Strings.ServerBarShowHint, ref show, "server info bar dtr nearby count ready umbra"))
        {
            discovery.ShowDtrEntry = show;
            discovery.DtrEntryChosen = true;
            nearby.SettingsChanged(rowsChanged: false);
        }

        var counts = (int)discovery.DtrCounts;
        if (Choice(Strings.ServerBarCountsLabel, Strings.ServerBarCountsHint, ref counts, ServerBarCountsOptions.Value, "server info bar dtr entry counts ready zone here nearby", show, sub: true, reason: Strings.SettingsDtrOffReason))
        {
            discovery.DtrCounts = counts == 1 ? Core.Discovery.DtrCounts.Zone : Core.Discovery.DtrCounts.Ready;
            nearby.SettingsChanged(rowsChanged: false);
        }

        var whenEmpty = discovery.DtrShowWhenEmpty;
        if (Toggle(Strings.DiscoveryDtrShowWhenEmptyLabel, Strings.DiscoveryDtrShowWhenEmptyHint, ref whenEmpty, "server info bar dtr nearby zero empty", show, sub: true, reason: Strings.SettingsDtrOffReason))
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
