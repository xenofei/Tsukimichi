using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Companions;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Integrations › Companion plugins (feature plan v5, decision 1): one row per plugin Tsukimichi works with,
/// its state as a glyph (loaded, turned off, outdated, not installed; the word, the installed version and the minimum
/// in the tooltip), its name, what it unlocks in Tsukimichi and a "Copy repo URL" button for a custom repository
/// ("Official repository" for one in Dalamud's own list), then plain-text steps for adding a custom repository and the
/// AutoDuty "regular Duty Finder" setting (its own block, <see cref="DrawAutoDutySettings"/>). The table reads <see cref="CompanionPlugins.All"/>, which re-reads Dalamud's
/// list only after it changed.
/// </summary>
public sealed partial class ConfigWindow
{
    private const double CompanionCopiedSeconds = 5.0;

    private static readonly string LoadedIcon = FontAwesomeIcon.CheckCircle.ToIconString();
    private static readonly string DisabledIcon = FontAwesomeIcon.PowerOff.ToIconString();
    private static readonly string OutdatedIcon = FontAwesomeIcon.ExclamationTriangle.ToIconString();
    private static readonly string MissingIcon = FontAwesomeIcon.MinusCircle.ToIconString();

    private string? companionCopied;
    private double companionCopiedUntil;

    /// <summary>The companion plugin registry; set by the plugin. Null hides the table.</summary>
    public CompanionPlugins? Companions { get; set; }

    private void DrawCompanionPlugins()
    {
        if (Companions is not { } companions)
        {
            return;
        }

        Header(Strings.CompanionsHeading);
        if (!Row(Strings.CompanionsHeading, Strings.CompanionsIntro, CompanionKeywords))
        {
            return;
        }

        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.TextWrapped(Strings.CompanionsIntro);
        }

        var flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.PadOuterX;
        using (var table = ImRaii.Table("##companions", 4, flags))
        {
            if (table)
            {
                ImGui.TableSetupColumn("##state", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn(Strings.CompanionsColumnPlugin, ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn(Strings.CompanionsColumnUnlocks, ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn(Strings.CompanionsColumnRepository, ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableHeadersRow();

                var all = companions.All;
                for (var i = 0; i < all.Count; i++)
                {
                    if (all[i].Definition.Listed)
                    {
                        DrawCompanionRow(i, all[i]);
                    }
                }
            }
        }

        if (companionCopied is { } copied && ImGui.GetTime() < companionCopiedUntil)
        {
            using var mist = Theme.PushText(Theme.Surface.TextSecondary);
            ImGui.TextUnformatted(copied);
        }

        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.TextWrapped(Strings.CompanionAddRepoHowTo);
        }
    }

    /// <summary>Every plugin the table lists, for the search box (the names are the plugins' own, never translated).</summary>
    private const string CompanionKeywords = "plugins installed missing outdated repository url Questionable AutoDuty Artisan GatherBuddy vnavmesh Lifestream Allagan Tools Quest Map Chat 2";

    /// <summary>
    /// Settings › Integrations › AutoDuty: whether "Run with AutoDuty" may use the regular Duty Finder when a duty has no
    /// Duty Support or Trust. Read per press by the detail pane, so no callback is needed.
    /// </summary>
    private void DrawAutoDutySettings()
    {
        Header(Strings.ConfigSectionAutoDuty);
        if (!Row(Strings.CompanionAutoDutyAllowDutyFinder, Strings.CompanionAutoDutyAllowDutyFinderHint, "autoduty duty support trust duty finder dungeon"))
        {
            return;
        }

        var allowDutyFinder = settings.AutoDutyAllowDutyFinder;
        if (ImGui.Checkbox(Strings.CompanionAutoDutyAllowDutyFinder, ref allowDutyFinder))
        {
            settings.AutoDutyAllowDutyFinder = allowDutyFinder;
            Save();
        }

        HintOnHover(Strings.CompanionAutoDutyAllowDutyFinderHint);
    }

    private void DrawCompanionRow(int index, CompanionStatus status)
    {
        using var id = ImRaii.PushId(index);
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        var (icon, color) = status.State switch
        {
            CompanionState.Loaded => (LoadedIcon, Theme.Moon),
            CompanionState.Disabled => (DisabledIcon, Theme.Surface.TextSecondary),
            CompanionState.Outdated => (OutdatedIcon, Theme.EclipseText),
            _ => (MissingIcon, Theme.Surface.TextDisabled),
        };
        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (Theme.PushText(color))
        {
            ImGui.TextUnformatted(icon);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(StateTooltip(status));
        }

        ImGui.TableNextColumn();
        using (Theme.PushText(status.IsLoaded ? Theme.Surface.Text : Theme.Surface.TextSecondary))
        {
            ImGui.TextUnformatted(status.Definition.DisplayName);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(StateTooltip(status));
        }

        ImGui.TableNextColumn();
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.TextWrapped(Strings.CompanionUnlocks(status.Plugin));
        }

        ImGui.TableNextColumn();
        var variant = status.Definition.Primary;
        if (variant.RepositoryUrl is { } url)
        {
            if (ImGui.SmallButton(Strings.CompanionCopyRepo))
            {
                ImGui.SetClipboardText(url);
                companionCopied = string.Format(CultureInfo.CurrentCulture, Strings.CompanionCopiedFormat, variant.DisplayName);
                companionCopiedUntil = ImGui.GetTime() + CompanionCopiedSeconds;
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(string.Format(CultureInfo.CurrentCulture, Strings.CompanionCopyRepoTooltipFormat, url));
            }
        }
        else
        {
            ImGui.TextDisabled(Strings.CompanionOfficial);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.CompanionOfficialTooltip);
            }
        }
    }

    /// <summary>"Loaded · Installed: 1.2.3" and, below the minimum, what Tsukimichi needs; the reason for any other state.</summary>
    private static string StateTooltip(CompanionStatus status)
    {
        var text = Strings.CompanionStateName(status.State);
        if (status.InstalledVersion is { } version)
        {
            text += "\n" + string.Format(CultureInfo.CurrentCulture, Strings.CompanionInstalledVersionFormat, version);
        }

        if (status.State == CompanionState.Outdated && status.MinimumVersion is { } minimum)
        {
            text += "\n" + string.Format(CultureInfo.CurrentCulture, Strings.CompanionMinimumVersionFormat, minimum);
        }

        if (status.Definition.Variants.Count > 1)
        {
            text += "\n" + CompanionPlugins.NeedName(status);
        }

        return text;
    }
}
