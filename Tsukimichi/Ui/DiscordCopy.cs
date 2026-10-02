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
/// (<see cref="Configuration.DiscordCopyLinks"/>, shared by every button). Nothing is allocated per frame: the ids and
/// the label are cached and rebuilt only when the id, the part shown or the language changes, and the text is built
/// through a static callback with its state passed in, so callers need no capturing lambda.
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

    // The cached ImGui ids for the last id drawn, and the label for the last text, part and part count.
    private string? cachedId;
    private string labelSuffix = string.Empty;
    private string popupId = string.Empty;
    private string label = string.Empty;
    private string? labelText;
    private int labelNext = -1;
    private int labelCount = -1;

    /// <summary>
    /// Draws the button (small or regular) and handles a click. <paramref name="key"/> names what the text is built
    /// from (a route, a plan view, a list instance): when it changes, the parts are built again.
    /// </summary>
    /// <param name="state">What <paramref name="build"/> reads, so it can be a static lambda.</param>
    /// <param name="build">Builds the whole text from <paramref name="state"/>; the flag says whether to add links. Called only on a click.</param>
    public void Draw<TState>(string id, object key, TState state, Func<TState, bool, string> build, bool small = false)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(build);
        var links = AddLinks;
        if (!ReferenceEquals(key, source) || links != sourceLinks)
        {
            source = key;
            sourceLinks = links;
            parts = [];
            next = 0;
        }

        var clicked = small ? ImGui.SmallButton(Label(id)) : ImGui.Button(Label(id));
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.LinksCopyDiscordTooltip);
        }

        using (var popup = ImRaii.ContextPopupItem(popupId))
        {
            if (popup)
            {
                // A popup is its own window: it scales itself.
                UiMetrics.ApplyFontScale();
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
                parts = DiscordText.Parts(build(state, links));
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

    /// <summary>"Copy for Discord" or "Copy part 2/3", with the id; rebuilt only when the id, the text or the part changes.</summary>
    private string Label(string id)
    {
        if (!string.Equals(id, cachedId, StringComparison.Ordinal))
        {
            cachedId = id;
            labelSuffix = "##discord" + id;
            popupId = "##discordMenu" + id;
            labelText = null;
        }

        var showPart = parts.Count > 1 && next > 0;
        var text = showPart ? Strings.LinksCopyPartFormat : Strings.LinksCopyDiscord;
        if (!ReferenceEquals(text, labelText) || (showPart && (next != labelNext || parts.Count != labelCount)))
        {
            labelText = text;
            labelNext = next;
            labelCount = parts.Count;
            label = (showPart ? string.Format(CultureInfo.CurrentCulture, text, next + 1, parts.Count) : text) + labelSuffix;
        }

        return label;
    }
}
