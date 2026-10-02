using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Integrations › Nearby and server info bar (1.7.0, merged from the cog that Nearby quests had): show the
/// "☾ N" count in the server info bar, keep it at zero, and list quests ready on another job. The values still live in
/// <c>user/discovery.json</c> (<see cref="Core.Discovery.DiscoverySettings"/>); <see cref="DiscoveryWindow.SettingsChanged"/>
/// saves them and refreshes the window and the entry. Nearby's cog opens Settings on this section.
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
        if (Row(Strings.DiscoveryShowDtrLabel, Strings.DiscoveryShowDtrHint, "server info bar dtr nearby count"))
        {
            var show = discovery.ShowDtrEntry;
            if (ImGui.Checkbox(Strings.DiscoveryShowDtrLabel, ref show))
            {
                discovery.ShowDtrEntry = show;
                nearby.SettingsChanged(rowsChanged: false);
            }

            HintOnHover(Strings.DiscoveryShowDtrHint);
        }

        if (Row(Strings.DiscoveryDtrShowWhenEmptyLabel, null, "server info bar dtr nearby zero empty"))
        {
            using (ImRaii.PushIndent())
            using (ImRaii.Disabled(!discovery.ShowDtrEntry))
            {
                var whenEmpty = discovery.DtrShowWhenEmpty;
                if (ImGui.Checkbox(Strings.DiscoveryDtrShowWhenEmptyLabel, ref whenEmpty))
                {
                    discovery.DtrShowWhenEmpty = whenEmpty;
                    nearby.SettingsChanged(rowsChanged: false);
                }
            }
        }

        if (Row(Strings.DiscoveryIncludeOtherJobLabel, null, "nearby quests other job class"))
        {
            var otherJob = discovery.NearbyIncludeOtherJob;
            if (ImGui.Checkbox(Strings.DiscoveryIncludeOtherJobLabel, ref otherJob))
            {
                discovery.NearbyIncludeOtherJob = otherJob;
                nearby.SettingsChanged(rowsChanged: true);
            }
        }
    }
}
