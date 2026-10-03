using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Companion setup in Settings › Automation › Companion plugins: the one-line summary at the top ("Ready for full
/// automation", "2 plugins need setup"), and under each loaded plugin a "Setup" expander listing the settings that
/// matter to Tsukimichi's hand-offs (<see cref="CompanionSetupCatalog"/>) with a check mark (as recommended), a cross
/// (set otherwise) or a question mark (Tsukimichi cannot read it), and, where the plugin's own IPC can set them,
/// "Apply recommended settings": a confirmation lists exactly what changes, and nothing else is touched.
/// </summary>
public sealed partial class ConfigWindow
{
    private const double CompanionAppliedSeconds = 8.0;

    private static readonly string SetupOkIcon = FontAwesomeIcon.Check.ToIconString();
    private static readonly string SetupChangeIcon = FontAwesomeIcon.Times.ToIconString();
    private static readonly string SetupUnknownIcon = FontAwesomeIcon.Question.ToIconString();

    private CompanionPlugin? applyPlugin;
    private bool applyPopupPending;
    private string applyQuestion = string.Empty;
    private List<string> applyLines = [];
    private List<string> applyIds = [];
    private string? applyResult;
    private double applyResultUntil;

    /// <summary>The companions' recommended settings; set by the plugin. Null hides the setup lines.</summary>
    public CompanionSetupService? CompanionSetup { get; set; }

    /// <summary>The summary line with its glyph, and Check again; its tooltip names the plugins.</summary>
    private void DrawCompanionSetupSummary(CompanionPlugins companions)
    {
        if (CompanionSetup is not { } setup)
        {
            return;
        }

        var summary = setup.Summary;
        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (Theme.PushText(summary.Ready ? Theme.Accent : Theme.DangerText))
        {
            ImGui.TextUnformatted(summary.Ready ? LoadedIcon : OutdatedIcon);
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(Strings.CompanionSetupSummaryLine(summary));
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(SummaryTooltip(summary, companions));
        }

        ImGui.SameLine();
        if (ImGui.SmallButton(Strings.CompanionSetupCheckAgain))
        {
            setup.Invalidate();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.CompanionSetupCheckAgainTooltip);
        }

        if (applyResult is { } result && ImGui.GetTime() < applyResultUntil)
        {
            using var mist = Theme.PushText(Theme.Surface.TextSecondary);
            ImGui.TextUnformatted(result);
        }
    }

    private static string SummaryTooltip(CompanionSetupSummary summary, CompanionPlugins companions)
    {
        var lines = new List<string>();
        foreach (var plugin in summary.NeedSetup)
        {
            lines.Add(companions.Status(plugin).DisplayName + ": " + Strings.CompanionSetupNeedsChange);
        }

        foreach (var plugin in summary.NotLoaded)
        {
            var status = companions.Status(plugin);
            lines.Add(CompanionPlugins.NeedName(status) + ": " + Strings.CompanionStateName(status.State));
        }

        if (summary.Unknown > 0)
        {
            lines.Add(string.Format(CultureInfo.CurrentCulture, Strings.CompanionSetupUnknownFormat, summary.Unknown));
        }

        return lines.Count == 0 ? Strings.CompanionSetupReady : string.Join("\n", lines);
    }

    /// <summary>"Needed by Questionable" under a companion row that another plugin needs.</summary>
    private static void DrawNeededBy(CompanionStatus status, IReadOnlyList<CompanionStatus> all)
    {
        var dependents = status.Definition.Dependents;
        if (dependents.Count == 0)
        {
            return;
        }

        var names = dependents.Select(p => all.First(s => s.Plugin == p).Definition.DisplayName).ToList();
        var joined = names.Count == 1
            ? names[0]
            : string.Format(CultureInfo.CurrentCulture, Strings.CompanionAndFormat, string.Join(", ", names.Take(names.Count - 1)), names[^1]);
        using var tertiary = Theme.PushText(Theme.Surface.TextTertiary);
        ImGui.TextWrapped(string.Format(CultureInfo.CurrentCulture, Strings.CompanionNeededByFormat, joined));
    }

    /// <summary>The "Setup" expander under a loaded plugin with recommended settings: one line per setting, then Apply.</summary>
    private void DrawCompanionSetup(CompanionStatus status)
    {
        if (CompanionSetup is not { } service || !status.IsLoaded)
        {
            return;
        }

        var setup = service.For(status.Plugin);
        if (setup.Results.Count == 0)
        {
            return;
        }

        var title = setup.State switch
        {
            PluginSetupState.NeedsSetup => string.Format(CultureInfo.CurrentCulture, Strings.CompanionSetupNodeChangeFormat, setup.NeedsChangeCount),
            _ when setup.UnknownCount == setup.Results.Count => Strings.CompanionSetupNodeUnknown,
            _ => Strings.CompanionSetupNodeOk,
        };

        bool open;
        using (Theme.PushText(setup.State == PluginSetupState.NeedsSetup ? Theme.DangerText : Theme.Surface.TextSecondary))
        {
            open = ImGui.TreeNode(title + "###setup");
        }

        if (open)
        {
            DrawCompanionSetupBody(service, status, setup);
            ImGui.TreePop();
        }
    }

    private void DrawCompanionSetupBody(CompanionSetupService service, CompanionStatus status, PluginSetup setup)
    {
        foreach (var result in setup.Results)
        {
            DrawSetupLine(result);
        }

        using (Theme.PushText(Theme.Surface.TextTertiary))
        {
            ImGui.TextWrapped(Strings.CompanionSetupWhere);
        }

        if (!setup.Results.Any(static r => r.Requirement.CanApply))
        {
            return;
        }

        var applicable = setup.Applicable;
        var canNow = service.CanApplyNow(status.Plugin);
        using (ImRaii.Disabled(applicable.Count == 0 || !canNow))
        {
            if (ImGui.SmallButton(Strings.CompanionSetupApply))
            {
                PrepareApply(status, applicable);
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(
                applicable.Count == 0 ? Strings.CompanionSetupApplyNothing
                : !canNow ? Strings.CompanionSetupApplyBusy
                : string.Format(CultureInfo.CurrentCulture, Strings.CompanionSetupApplyTooltipFormat, status.DisplayName));
        }
    }

    private static void DrawSetupLine(SetupResult result)
    {
        var id = result.Requirement.Id;
        var (icon, color, word) = result.Check switch
        {
            SetupCheck.Ok => (SetupOkIcon, Theme.Accent, result.Covered ? Strings.CompanionSetupCovered : Strings.CompanionSetupOk),
            SetupCheck.NeedsChange => (SetupChangeIcon, Theme.DangerText, Strings.CompanionSetupNeedsChange),
            _ => (SetupUnknownIcon, Theme.Surface.TextSecondary, Strings.CompanionSetupUnknown),
        };

        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (Theme.PushText(color))
        {
            ImGui.TextUnformatted(icon);
        }

        var hovered = ImGui.IsItemHovered();
        ImGui.SameLine();
        ImGui.TextWrapped(Strings.CompanionSetupLabel(id));
        hovered |= ImGui.IsItemHovered();
        using (ImRaii.PushIndent(ImGui.GetFrameHeight(), false))
        using (Theme.PushText(Theme.Surface.TextTertiary))
        {
            ImGui.TextWrapped(string.Format(CultureInfo.CurrentCulture, Strings.CompanionSetupRecommendedFormat, Strings.CompanionSetupRecommended(id)));
        }

        hovered |= ImGui.IsItemHovered();
        if (!hovered)
        {
            return;
        }

        var detail = Strings.CompanionSetupWhy(id);
        if (result.Value is { } value && result.Requirement.Source != SetupSource.Manual)
        {
            detail += "\n" + string.Format(CultureInfo.CurrentCulture, Strings.CompanionSetupNowFormat, Strings.CompanionSetupValue(id, value));
        }

        if (result.Check == SetupCheck.NeedsChange && result.Requirement.Impact == SetupImpact.Blocking)
        {
            detail += "\n" + Strings.CompanionSetupBlocking;
        }

        UiMetrics.Tooltip(word, detail);
    }

    /// <summary>Composes the confirmation for the settings Apply would change and asks for it to open.</summary>
    private void PrepareApply(CompanionStatus status, IReadOnlyList<SetupResult> applicable)
    {
        applyPlugin = status.Plugin;
        applyQuestion = string.Format(CultureInfo.CurrentCulture, Strings.CompanionSetupApplyQuestionFormat, status.DisplayName);
        applyLines = applicable
            .Select(r => string.Format(
                CultureInfo.CurrentCulture,
                Strings.CompanionSetupApplyLineFormat,
                Strings.CompanionSetupLabel(r.Requirement.Id),
                Strings.CompanionSetupValue(r.Requirement.Id, r.Value),
                Strings.CompanionSetupValue(r.Requirement.Id, r.Requirement.ApplyValue)))
            .ToList();

        // Apply sets exactly these (those still not as recommended when confirmed), never a list worked out again then.
        applyIds = applicable.Select(static r => r.Requirement.Id).ToList();
        applyPopupPending = true;
    }

    private readonly ConfirmGate applyGate = new();

    private static string ApplyConfirmLabel => applyConfirmLabelText.Value;

    private static readonly Localization.LocText applyConfirmLabelText = new(static () => Strings.CompanionSetupApplyConfirm + Chrome.HoldIdSuffix);

    /// <summary>The confirmation, opened outside the table so its id does not depend on the row.</summary>
    private void DrawCompanionApplyConfirm(CompanionPlugins companions)
    {
        if (applyPopupPending)
        {
            applyPopupPending = false;
            applyGate.Cancel();
            ImGui.OpenPopup(Strings.CompanionSetupApplyPopup);
        }

        using var modal = ImRaii.PopupModal(Strings.CompanionSetupApplyPopup, ImGuiWindowFlags.AlwaysAutoResize);
        if (!modal)
        {
            return;
        }

        // A popup is its own window: it scales itself.
        UiMetrics.ApplyFontScale();
        ImGui.TextWrapped(applyQuestion);
        ImGui.Spacing();
        foreach (var line in applyLines)
        {
            ImGui.BulletText(line);
        }

        ImGui.Spacing();

        // Apply rewrites another plugin's settings, which Tsukimichi cannot undo: press and hold (feature plan v6 S2).
        var confirmed = Chrome.HoldButton(ApplyConfirmLabel, applyGate);
        if (ImGui.IsItemHovered())
        {
            Safety.Tooltip(Strings.CompanionSetupApplyConfirmTooltip, GuardedAction.CompanionApply);
        }

        if (confirmed && applyPlugin is { } plugin && CompanionSetup is { } setup)
        {
            var (applied, failed) = setup.Apply(plugin, applyIds);
            applyResult = string.Format(CultureInfo.CurrentCulture, Strings.CompanionSetupAppliedFormat, companions.Status(plugin).DisplayName, applied, applied + failed);
            applyResultUntil = ImGui.GetTime() + CompanionAppliedSeconds;
            applyPlugin = null;
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button(Strings.ConfigCancel))
        {
            applyPlugin = null;
            ImGui.CloseCurrentPopup();
        }
    }
}
