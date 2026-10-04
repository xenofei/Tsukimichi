using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Travel;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Setup's "Before a walk" (feature plan v7 A9; Settings › Automation, the row under the companion plugins, as
/// spec-1.18 places it): the travel preflight, one line per check in the companion Setup list's pattern (check mark, cross or question mark, the check's name, a plain status under it,
/// why it matters in the tooltip). A fix runs only from its own button and changes one setting, with Undo while the
/// setting still reads what the fix set: "Switch to Standard" (the game's movement type; Undo reads "Restore Legacy"),
/// "Allow movement" (vnavmesh's runtime switch). A conflict's button opens Dalamud's plugin installer: Tsukimichi never
/// turns another plugin off. The camera has no fix; its line says what to do. Read live (cached briefly by the
/// service), so there is no Check again. The designer's styling (dots, columns) is left to the 1.18 UI pass.
/// </summary>
public sealed partial class ConfigWindow
{
    private const double PreflightNoteSeconds = 8.0;

    private string? preflightNote;
    private double preflightNoteUntil;

    /// <summary>The travel preflight; set by the plugin. Null hides the row.</summary>
    public TravelPreflightService? TravelPreflight { get; set; }

    private void DrawTravelPreflight()
    {
        if (TravelPreflight is not { } preflight)
        {
            return;
        }

        if (!Setting(Strings.ConfigTravelPreflight, Strings.ConfigTravelPreflightHint, "travel preflight before walk movement type legacy standard controller camera first person vnavmesh paused conflict WrongWarpFinder Lifestream", 0f))
        {
            return;
        }

        SettingBelow();
        foreach (var result in preflight.Results)
        {
            using var id = ImRaii.PushId((int)result.Item);
            DrawPreflightLine(preflight, result);
        }

        if (preflightNote is { } note && ImGui.GetTime() < preflightNoteUntil)
        {
            using var mist = Theme.PushText(Theme.Surface.TextSecondary);
            ImGui.TextUnformatted(note);
        }

        EndSetting();
    }

    private void DrawPreflightLine(TravelPreflightService preflight, PreflightResult result)
    {
        var (icon, color) = result.State switch
        {
            PreflightState.Ok => (SetupOkIcon, Theme.Accent),
            PreflightState.Warn => (SetupChangeIcon, Theme.DangerText),
            _ => (SetupUnknownIcon, Theme.Surface.TextSecondary),
        };

        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (Theme.PushText(color))
        {
            ImGui.TextUnformatted(icon);
        }

        var hovered = ImGui.IsItemHovered();
        ImGui.SameLine();
        ImGui.TextWrapped(Strings.TravelPreflightLabel(result.Item));
        hovered |= ImGui.IsItemHovered();
        using (ImRaii.PushIndent(ImGui.GetFrameHeight(), false))
        {
            using (Theme.PushText(Theme.Surface.TextTertiary))
            {
                ImGui.TextWrapped(Strings.TravelPreflightStatus(result.Item, result.State));
                hovered |= ImGui.IsItemHovered();
                foreach (var conflict in result.Conflicts)
                {
                    ImGui.TextWrapped(string.Format(CultureInfo.CurrentCulture, Strings.TravelPreflightConflictFormat, conflict.InternalName, Strings.TravelPreflightConflict(conflict.Key)));
                    hovered |= ImGui.IsItemHovered();
                }
            }

            DrawPreflightFix(preflight, result);
        }

        if (hovered)
        {
            UiMetrics.Tooltip(Strings.TravelPreflightWord(result.State), Strings.TravelPreflightWhy(result.Item));
        }
    }

    /// <summary>The line's fix button, and Undo while what the fix changed still reads as the fix set it.</summary>
    private void DrawPreflightFix(TravelPreflightService preflight, PreflightResult result)
    {
        if (result.Fix != PreflightFix.None)
        {
            if (ImGui.SmallButton(Strings.TravelPreflightFixLabel(result.Fix)))
            {
                var done = preflight.Fix(result.Item);
                if (result.Fix != PreflightFix.OpenPluginInstaller || !done)
                {
                    ShowPreflightNote(done ? Strings.TravelPreflightFixed(result.Item) : Strings.TravelPreflightFixFailed);
                }
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.TravelPreflightFixTooltip(result.Fix));
            }
        }

        if (!preflight.CanUndo(result.Item))
        {
            return;
        }

        if (result.Fix != PreflightFix.None)
        {
            ImGui.SameLine();
        }

        if (ImGui.SmallButton(Strings.TravelPreflightUndoLabel(result.Item)))
        {
            ShowPreflightNote(preflight.Undo(result.Item) ? Strings.TravelPreflightUndone : Strings.TravelPreflightFixFailed);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.TravelPreflightUndoTooltip);
        }
    }

    private void ShowPreflightNote(string note)
    {
        preflightNote = note;
        preflightNoteUntil = ImGui.GetTime() + PreflightNoteSeconds;
    }
}
