using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The "What's new" card at the top of the detail column: after an update, the first time the main window opens, it
/// shows every CHANGELOG.md section the player skipped (feature plan v5, 1.7.0): each version above
/// <see cref="Configuration.LastSeenVersion"/> up to the running one, newest first (embedded in the assembly, parsed by
/// <see cref="ChangelogSection.Since"/>; no network), with Close and Help. Each version opens on its highlights (the
/// bold lead or first sentence of each top-level bullet) with More for the full text. It stays until Close is pressed,
/// then <see cref="Configuration.LastSeenVersion"/> records the version. A fresh install records the version silently
/// and never sees the card; so does a build with nothing new in its changelog.
/// </summary>
public sealed class WhatsNewCard
{
    /// <summary>Manifest resource name of the embedded changelog (see the csproj's EmbeddedResource item).</summary>
    public const string ResourceName = "Tsukimichi.CHANGELOG.md";

    /// <summary>The card never takes more than this share of the column, and scrolls past it.</summary>
    private const float MaxHeightFraction = 0.42f;
    private const float MaxHeightPx = 340f;
    private const float Pad = 8f;

    private readonly Configuration settings;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;
    private readonly Action openHelp;
    private readonly Func<string?> readChangelog;
    private readonly string version;

    private IReadOnlyList<ChangelogSection> sections = [];
    private Localization.LocText? title;
    private bool checkedThisLoad;

    // Per section, composed once when the card is prepared: its heading (when several show), its highlights with the
    // bullet in front, its full lines per group (sub-bullets marked and indented), and whether More is open.
    private string[] headings = [];
    private string[][] highlights = [];
    private (string Title, (string Text, bool Nested)[] Lines)[][] fullText = [];
    private bool[] expanded = [];

    /// <param name="settings">Holds <see cref="Configuration.LastSeenVersion"/>.</param>
    /// <param name="pluginInterface">To save the configuration.</param>
    /// <param name="log">For a warning when the save fails.</param>
    /// <param name="openHelp">The Help button: opens the help window.</param>
    /// <param name="readChangelog">Reads the changelog text; defaults to the embedded resource. Called once, only when the version changed.</param>
    /// <param name="runningVersion">Defaults to the plugin assembly's version.</param>
    public WhatsNewCard(Configuration settings, IDalamudPluginInterface pluginInterface, IPluginLog log, Action openHelp, Func<string?>? readChangelog = null, string? runningVersion = null)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.openHelp = openHelp ?? throw new ArgumentNullException(nameof(openHelp));
        this.readChangelog = readChangelog ?? ReadEmbeddedChangelog;
        version = ChangelogSection.NormalizeVersion(runningVersion ?? typeof(WhatsNewCard).Assembly.GetName().Version?.ToString(3));
    }

    /// <summary>The "New chapters" line (1.21.0 P5) at the top of the card; set by the plugin. Null shows none.</summary>
    public NewChaptersSource? NewChapters { get; set; }

    /// <summary>True while the card has a section to show.</summary>
    public bool Visible => sections.Count > 0;

    /// <summary>The card's title ("What's new in 1.12.0"), for the notice that says it is waiting; empty while hidden.</summary>
    public string Title => title?.Value ?? string.Empty;

    /// <summary>
    /// Decides once per plugin load, when the main window first draws, whether to show the card, and records the
    /// version at once when there is nothing to show (fresh install, same version, or nothing new in the changelog).
    /// </summary>
    public void CheckOnOpen()
    {
        if (checkedThisLoad)
        {
            return;
        }

        checkedThisLoad = true;
        var seen = settings.LastSeenVersion;
        IReadOnlyList<ChangelogSection> found = [];
        if ((seen.Length > 0 || settings.HasPriorConfig) && ChangelogSection.NormalizeVersion(seen) != version)
        {
            try
            {
                found = ChangelogSection.Since(readChangelog(), seen, version);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Could not read the embedded changelog");
            }
        }

        switch (WhatsNew.Decide(seen, version, found.Count > 0, settings.HasPriorConfig))
        {
            case WhatsNewDecision.Show:
                Prepare(found, ChangelogSection.NormalizeVersion(seen));
                break;
            case WhatsNewDecision.RecordSilently:
                MarkSeen();
                break;
        }
    }

    /// <summary>Composes the title and every section's lines once, so drawing allocates nothing.</summary>
    private void Prepare(IReadOnlyList<ChangelogSection> found, string seen)
    {
        sections = found;
        var several = found.Count > 1;
        title = several && seen.Length > 0
            ? new Localization.LocText(() => string.Format(CultureInfo.CurrentCulture, Strings.WhatsNew.SinceFormat, seen))
            : new Localization.LocText(() => string.Format(CultureInfo.CurrentCulture, Strings.WhatsNew.TitleFormat, found[0].Version));
        headings = new string[found.Count];
        highlights = new string[found.Count][];
        fullText = new (string, (string, bool)[])[found.Count][];
        expanded = new bool[found.Count];
        for (var s = 0; s < found.Count; s++)
        {
            var section = found[s];
            headings[s] = section.Date.Length > 0
                ? string.Format(CultureInfo.InvariantCulture, Strings.WhatsNew.SectionFormat, section.Version, section.Date)
                : section.Version;

            var lead = new List<string>();
            var groups = new (string, (string, bool)[])[section.Groups.Count];
            for (var g = 0; g < groups.Length; g++)
            {
                var group = section.Groups[g];
                var lines = new (string, bool)[group.Items.Count];
                for (var i = 0; i < lines.Length; i++)
                {
                    var nested = group.IsNested(i);
                    lines[i] = ((nested ? SubBullet : Strings.WhatsNew.Bullet) + ChangelogSection.Plain(group.Items[i]), nested);
                    if (!nested && ChangelogSection.Highlight(group.Items[i]) is { Length: > 0 } highlight)
                    {
                        lead.Add(Strings.WhatsNew.Bullet + highlight);
                    }
                }

                groups[g] = (group.Title, lines);
            }

            highlights[s] = lead.ToArray();
            fullText[s] = groups;
        }
    }

    /// <summary>A sub-bullet's mark in the full text.</summary>
    private const string SubBullet = "– ";

    /// <summary>Hides the card and records the running version.</summary>
    public void Dismiss()
    {
        sections = [];
        MarkSeen();
    }

    /// <summary>Draws the card when visible and returns the height it used (0 when hidden), so the caller can shrink the pane below it.</summary>
    public float Draw(float availableHeight)
    {
        if (sections.Count == 0)
        {
            return 0f;
        }

        // Scaled like every other pane (global scale times the UI scale), so the padding keeps pace with the text.
        var pad = UiMetrics.Px(Pad);
        var height = MathF.Min(availableHeight * MaxHeightFraction, UiMetrics.Px(MaxHeightPx));
        var start = ImGui.GetCursorPosY();

        using (Theme.PushNightPanel())
        using (ImRaii.PushColor(ImGuiCol.Border, Chrome.CardChildBorder))
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(pad, pad)))
        using (var child = ImRaii.Child("##whatsNew", new Vector2(0f, height), true))
        {
            if (child)
            {
                // Brass at Full and Quiet, the corner marks at Full (R3 #8).
                using var frame = Chrome.CardFrameInWindow();
                DrawBody(pad);
            }
        }

        ImGui.Spacing();
        return ImGui.GetCursorPosY() - start;
    }

    private void DrawBody(float pad)
    {
        // The title, then Help and Close at the right end of the line, never over the title: on the next line,
        // right-aligned, when the two would run into it (feature plan v4 L6).
        Chrome.FitText(title?.Value ?? string.Empty, Theme.AccentU32);
        var closeWidth = ImGuiHelpers.GetButtonSize(Strings.WhatsNew.Close).X;
        var helpWidth = ImGuiHelpers.GetButtonSize(Strings.WhatsNew.Help).X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        Chrome.SameLineRightOrWrap(closeWidth + helpWidth + spacing);
        if (ImGui.SmallButton(Strings.WhatsNew.Help))
        {
            openHelp();
        }

        ImGui.SameLine();
        if (ImGui.SmallButton(Strings.WhatsNew.Close))
        {
            Dismiss();
            return;
        }

        ImGui.Spacing();
        NewChapters?.Draw();
        var wrap = ImGui.GetWindowContentRegionMax().X;
        using var wrapPos = ImRaii.TextWrapPos(wrap);
        var several = sections.Count > 1;
        for (var s = 0; s < sections.Count; s++)
        {
            using var id = ImRaii.PushId(s);
            if (several)
            {
                if (s > 0)
                {
                    Chrome.Hairline();
                    ImGui.Spacing();
                }

                using (Theme.PushText(Theme.Accent))
                {
                    ImGui.TextUnformatted(headings[s]);
                }
            }

            if (expanded[s])
            {
                DrawFull(s, pad);
            }
            else
            {
                using (ImRaii.PushIndent(pad, false))
                using (Theme.PushText(Theme.Surface.Text))
                {
                    foreach (var line in highlights[s])
                    {
                        ImGui.TextUnformatted(line);
                    }
                }
            }

            if (ImGui.SmallButton(expanded[s] ? Strings.WhatsNew.Less : Strings.WhatsNew.More))
            {
                expanded[s] = !expanded[s];
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(expanded[s] ? Strings.WhatsNew.LessTooltip : Strings.WhatsNew.MoreTooltip);
            }

            ImGui.Spacing();
        }
    }

    /// <summary>One version's every change, by group, sub-bullets indented under their bullet.</summary>
    private void DrawFull(int s, float pad)
    {
        foreach (var (groupTitle, lines) in fullText[s])
        {
            if (groupTitle.Length > 0)
            {
                using (Theme.PushText(Theme.Surface.TextTertiary))
                {
                    ImGui.TextUnformatted(groupTitle);
                }
            }

            using (ImRaii.PushIndent(pad, false))
            using (Theme.PushText(Theme.Surface.Text))
            {
                foreach (var (text, nested) in lines)
                {
                    if (nested)
                    {
                        using (ImRaii.PushIndent(pad * 2f, false))
                        {
                            ImGui.TextUnformatted(text);
                        }
                    }
                    else
                    {
                        ImGui.TextUnformatted(text);
                    }
                }
            }

            ImGui.Spacing();
        }
    }

    private void MarkSeen()
    {
        if (settings.LastSeenVersion == version)
        {
            return;
        }

        settings.LastSeenVersion = version;
        try
        {
            settings.Save(pluginInterface);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not save the last seen version");
        }
    }

    /// <summary>The CHANGELOG.md embedded at build time, or null when the resource is missing.</summary>
    public static string? ReadEmbeddedChangelog()
    {
        using var stream = typeof(WhatsNewCard).Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
