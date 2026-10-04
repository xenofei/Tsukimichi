using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Tsukimichi.Ui;

/// <summary>
/// The way back to Tonight (plan v7, 1.21.0 P1; spec-1.21 "The way back to Tonight"): a Tonight button (the moon and
/// the word) at the right end of the detail pane's header row, always drawn while a quest is shown so the pane below it
/// never moves. It clears the selection, and the column cross-fades to the Tonight card; Back (‹) then returns to the
/// quest. Esc (MainWindow) and a second click on the selected row (TablePane) do the same.
/// </summary>
public sealed partial class DetailPane
{
    private static readonly string TonightGlyph = FontAwesomeIcon.Moon.ToIconString();

    /// <summary>The header row with the Tonight button on its right; the pane's sections start under it.</summary>
    private void DrawTonightHeader()
    {
        var origin = ImGui.GetCursorScreenPos();
        var room = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var height = Chrome.ChipHeightPx();
        var label = Strings.UpNextBackToTonight;
        var width = Chrome.ActionPillWidth(TonightGlyph, label, PillLayout.Row);
        ImGui.SetCursorScreenPos(new Vector2(origin.X + room - width, origin.Y));
        if (Chrome.ActionPill("##backToTonight", TonightGlyph, label, PillTone.Quiet, true, Strings.UpNextBackToTonightTooltip, PillLayout.Row))
        {
            ui.SelectedRowId = null;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(room, MathF.Max(height, Chrome.PillHeight(PillLayout.Row))));
    }
}
