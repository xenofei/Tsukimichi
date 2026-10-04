using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Travel;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Automation › Travel › "Before a walk" (feature plan v7 A9): the travel preflight, one line per check in
/// the companion Setup list's pattern (check mark, cross or question mark, the check's name, a plain status under it,
/// why it matters in the tooltip). A fix runs only from its own button: "Switch to Standard" changes the game's movement
/// type and offers Undo, which puts the old value back while the game still reads Standard; "Let vnavmesh move" turns
/// vnavmesh's runtime switch back on. The camera and the conflicts have no fix Tsukimichi may make; their lines say
/// what to do. Read live (cached briefly by the service), so there is no Check again.
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

    /// <summary>The line's fix button, and Undo after the movement type was switched.</summary>
    private void DrawPreflightFix(TravelPreflightService preflight, PreflightResult result)
    {
        switch (result.Fix)
        {
            case PreflightFix.StandardMovement:
                if (ImGui.SmallButton(Strings.TravelPreflightSwitchStandard))
                {
                    ShowPreflightNote(preflight.SwitchToStandardMovement() ? Strings.TravelPreflightSwitched : Strings.TravelPreflightFixFailed);
                }

                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(Strings.TravelPreflightSwitchStandardTooltip);
                }

                break;
            case PreflightFix.AllowVnavmeshMovement:
                if (ImGui.SmallButton(Strings.TravelPreflightAllowVnavmesh) && !preflight.AllowVnavmeshMovement())
                {
                    ShowPreflightNote(Strings.TravelPreflightFixFailed);
                }

                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(Strings.TravelPreflightAllowVnavmeshTooltip);
                }

                break;
        }

        if (result.Item == PreflightItem.MovementType && preflight.CanUndo)
        {
            if (ImGui.SmallButton(Strings.TravelPreflightUndo))
            {
                ShowPreflightNote(preflight.UndoMovement() ? Strings.TravelPreflightUndone : Strings.TravelPreflightFixFailed);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.TravelPreflightUndoTooltip);
            }
        }
    }

    private void ShowPreflightNote(string note)
    {
        preflightNote = note;
        preflightNoteUntil = ImGui.GetTime() + PreflightNoteSeconds;
    }
}
