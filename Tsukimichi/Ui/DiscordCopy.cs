using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Config;
using Tsukimichi.Core.Text;

namespace Tsukimichi.Ui;

/// <summary>
/// One "Copy for Discord" button (1.8.0, research C9 #3), shared by the route window, My blues and Compare. The first
/// click builds the text (<see cref="DiscordText"/>), splits it at 2,000 characters and copies part 1; while parts are
/// left the button reads "Copy part 2/3" and each click copies the next, then it starts over. A new source (another
/// route, a changed filter) starts over too. Right-clicking the button offers "Add links to quest names"
/// (<see cref="Configuration.DiscordCopyLinks"/>, shared by every button).
/// </summary>
public sealed class DiscordCopy
{
    private const double CopiedSeconds = 2.5;

    /// <summary>The setting the links option reads and writes, and how to save it; attached by the plugin (null: no links, nothing saved).</summary>
    public static Configuration? Settings { get; set; }

    /// <summary>Saves <see cref="Settings"/> after the links option changed.</summary>
    public static Action? SaveSettings { get; set; }

    private static bool AddLinks => Settings?.DiscordCopyLinks == true;

    private IReadOnlyList<string> parts = [];
    private object? source;
    private bool sourceLinks;
    private int next;
    private double copiedAt = -100.0;

    /// <summary>
    /// Draws the button (small or regular) and handles a click. <paramref name="key"/> names what the text is built
    /// from (a route, a plan view, a list instance): when it changes, the parts are built again.
    /// </summary>
    /// <param name="build">Builds the whole text; the argument says whether to add links.</param>
    public void Draw(string id, object key, Func<bool, string> build, bool small = false)
    {
        ArgumentNullException.ThrowIfNull(build);
        var links = AddLinks;
        if (!ReferenceEquals(key, source) || links != sourceLinks)
        {
            source = key;
            sourceLinks = links;
            parts = [];
            next = 0;
        }

        var label = (parts.Count > 1 && next > 0
            ? string.Format(CultureInfo.CurrentCulture, Strings.LinksCopyPartFormat, next + 1, parts.Count)
            : Strings.LinksCopyDiscord) + "##discord" + id;
        var clicked = small ? ImGui.SmallButton(label) : ImGui.Button(label);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.LinksCopyDiscordTooltip);
        }

        using (var popup = ImRaii.ContextPopupItem("##discordMenu" + id))
        {
            if (popup)
            {
                var add = links;
                if (ImGui.Checkbox(Strings.LinksDiscordAddLinks, ref add) && Settings is { } settings)
                {
                    settings.DiscordCopyLinks = add;
                    SaveSettings?.Invoke();
                }
            }
        }

        if (clicked)
        {
            if (next == 0)
            {
                parts = DiscordText.Parts(build(links));
            }

            if (parts.Count > 0)
            {
                ImGui.SetClipboardText(parts[next]);
                next = (next + 1) % parts.Count;
                copiedAt = ImGui.GetTime();
            }
        }

        if (ImGui.GetTime() - copiedAt < CopiedSeconds)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(Strings.LinksDiscordCopied);
        }
    }
}
