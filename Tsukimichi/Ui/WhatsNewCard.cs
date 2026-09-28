using System;
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
/// shows the CHANGELOG.md section for the running version (embedded in the assembly, parsed by
/// <see cref="ChangelogSection"/>; no network) with Close and Help. It stays until Close is pressed, then
/// <see cref="Configuration.LastSeenVersion"/> records the version. A fresh install records the version silently and
/// never sees the card; so does a build whose version has no changelog section.
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

    private ChangelogSection? section;
    private string title = string.Empty;
    private bool checkedThisLoad;

    // The section's items per group with the bullet already in front, composed once when the card is prepared.
    private string[][] groupLines = [];

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

    /// <summary>True while the card has a section to show.</summary>
    public bool Visible => section is not null;

    /// <summary>
    /// Decides once per plugin load, when the main window first draws, whether to show the card, and records the
    /// version at once when there is nothing to show (fresh install, same version, or no section for this version).
    /// </summary>
    public void CheckOnOpen()
    {
        if (checkedThisLoad)
        {
            return;
        }

        checkedThisLoad = true;
        var seen = settings.LastSeenVersion;
        ChangelogSection? found = null;
        if (seen.Length > 0 && ChangelogSection.NormalizeVersion(seen) != version)
        {
            try
            {
                found = ChangelogSection.Find(readChangelog(), version);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Could not read the embedded changelog");
            }
        }

        switch (WhatsNew.Decide(seen, version, found is not null))
        {
            case WhatsNewDecision.Show:
                section = found;
                title = string.Format(CultureInfo.CurrentCulture, Strings.WhatsNew.TitleFormat, version);
                groupLines = new string[found!.Groups.Count][];
                for (var g = 0; g < groupLines.Length; g++)
                {
                    var items = found.Groups[g].Items;
                    var lines = new string[items.Count];
                    for (var i = 0; i < lines.Length; i++)
                    {
                        lines[i] = Strings.WhatsNew.Bullet + items[i];
                    }

                    groupLines[g] = lines;
                }

                break;
            case WhatsNewDecision.RecordSilently:
                MarkSeen();
                break;
        }
    }

    /// <summary>Hides the card and records the running version.</summary>
    public void Dismiss()
    {
        section = null;
        MarkSeen();
    }

    /// <summary>Draws the card when visible and returns the height it used (0 when hidden), so the caller can shrink the pane below it.</summary>
    public float Draw(float availableHeight)
    {
        if (section is not { } current)
        {
            return 0f;
        }

        // Scaled like every other pane (global scale times the UI scale), so the padding keeps pace with the text.
        var pad = UiMetrics.Px(Pad);
        var height = MathF.Min(availableHeight * MaxHeightFraction, UiMetrics.Px(MaxHeightPx));
        var start = ImGui.GetCursorPosY();

        using (Theme.PushNightPanel())
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(pad, pad)))
        using (var child = ImRaii.Child("##whatsNew", new Vector2(0f, height), true))
        {
            if (child)
            {
                DrawBody(current, pad);
            }
        }

        ImGui.Spacing();
        return ImGui.GetCursorPosY() - start;
    }

    private void DrawBody(ChangelogSection current, float pad)
    {
        using (Theme.PushText(Theme.Moon))
        {
            ImGui.TextUnformatted(title);
        }

        ImGui.SameLine();
        var closeWidth = ImGuiHelpers.GetButtonSize(Strings.WhatsNew.Close).X;
        var helpWidth = ImGuiHelpers.GetButtonSize(Strings.WhatsNew.Help).X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - closeWidth - helpWidth - spacing);
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
        var wrap = ImGui.GetWindowContentRegionMax().X;
        using var wrapPos = ImRaii.TextWrapPos(wrap);
        for (var g = 0; g < current.Groups.Count; g++)
        {
            var group = current.Groups[g];
            if (group.Title.Length > 0)
            {
                using (Theme.PushText(Theme.Dusk))
                {
                    ImGui.TextUnformatted(group.Title);
                }
            }

            using (ImRaii.PushIndent(pad, false))
            using (Theme.PushText(Theme.Silver))
            {
                var lines = groupLines[g];
                for (var i = 0; i < lines.Length; i++)
                {
                    ImGui.TextUnformatted(lines[i]);
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
