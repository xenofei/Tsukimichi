using Dalamud.Bindings.ImGui;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Display › Planning (1.9.0, R6 E and G): the Journal's EXP column (off by default) and the allied society
/// board on the Characters dashboard (on by default).
/// </summary>
public sealed partial class ConfigWindow
{
    private void DrawPlanning()
    {
        Header(Strings.PlanningConfigHeading);
        if (Row(Strings.PlanningConfigExpColumn, Strings.PlanningConfigExpColumnHint, "exp experience column journal table reward"))
        {
            var exp = settings.JournalShowExpColumn;
            if (ImGui.Checkbox(Strings.PlanningConfigExpColumn, ref exp))
            {
                settings.JournalShowExpColumn = exp;
                Save();
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(Strings.PlanningConfigExpColumnHint);
            }
        }

        if (Row(Strings.PlanningConfigBoard, Strings.PlanningConfigBoardHint, "allied society beast tribe daily board reset allowances"))
        {
            var board = settings.ShowAlliedSocietyBoard;
            if (ImGui.Checkbox(Strings.PlanningConfigBoard, ref board))
            {
                settings.ShowAlliedSocietyBoard = board;
                Save();
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(Strings.PlanningConfigBoardHint);
            }
        }
    }
}
