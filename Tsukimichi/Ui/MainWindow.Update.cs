using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Updates;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// "Tsukimichi 1.23.0 is ready" on the right of the status bar (plan v8 U1; spec-1.22 U1 "The status-bar note",
/// update-ready-1.22.png): a 7 px Tide dot, the words, <b>Update</b> (a quiet pill) and × (Later). News, not a call to act,
/// so Tide, never gold (decision 7). At Full a Tide-tinted pill with a hairline; at Quiet the hairline pill on the flat
/// bar; at Plain the ledger: no pill, a square Update, and the dot kept, so the meaning is never colour alone. Its hover
/// gives the new version's plain notes. It never pops up and never moves the bar: it takes the room right of the counts,
/// before the version, only while a version is ready.
/// </summary>
public sealed partial class MainWindow
{
    /// <summary>The update watcher (U1); set by the plugin. Null shows no note.</summary>
    public UpdateWatcher? Updates { get; set; }

    private int updateNoteRevision = -1;
    private int updateNoteLanguage = -1;
    private string updateNoteText = string.Empty;
    private string updateNoteTitle = string.Empty;
    private string updateNoteNotes = string.Empty;
    private string updateNoteHover = string.Empty;

    /// <summary>Whether the note shows this frame (a version is ready and not put off with Later).</summary>
    private bool UpdateNoteShown => Updates is { Current.ShowsNote: true };

    /// <summary>
    /// The note's width without its words, the gap before it included: its padding, the dot, Update and ×, which keep
    /// their size however narrow the window (<see cref="UpdateNoteFit"/>); 0 while it does not show.
    /// </summary>
    private float UpdateNoteFixedWidth(float gap)
    {
        if (!UpdateNoteShown)
        {
            return 0f;
        }

        var pad = UpdatePad();
        return gap + pad + UiMetrics.Px(7f) + UiMetrics.Px(5f) + UiMetrics.Px(8f) + UpdateButtonWidth() + UiMetrics.Px(4f) + UpdateCloseWidth() + pad;
    }

    /// <summary>The note's words at full width.</summary>
    private float UpdateNoteTextWidth()
    {
        RefreshUpdateNote();
        return ImGui.CalcTextSize(updateNoteText).X;
    }

    /// <summary>
    /// Draws the note from <paramref name="x"/> (its gap first), its words within <paramref name="textRoom"/> (ending in
    /// an ellipsis when the window is narrow), and returns where it ends.
    /// </summary>
    private float DrawUpdateNote(ImDrawListPtr dl, float x, float textY, float line, float gap, float textRoom)
    {
        if (!UpdateNoteShown || Updates is not { } updates || textRoom < 0f)
        {
            return x;
        }

        RefreshUpdateNote();
        var flair = Theme.Flair;
        var s = Theme.Surface;
        var cool = s.Cool;
        var pad = UpdatePad();
        var min = new Vector2(x + gap, textY - UiMetrics.Px(2f));
        var width = UpdateNoteFixedWidth(gap) - gap + textRoom;
        var max = new Vector2(min.X + width, textY + line + UiMetrics.Px(2f));
        var rounding = (max.Y - min.Y) * 0.5f;
        switch (flair)
        {
            case Flair.Full:
                dl.AddRectFilled(min, max, Theme.WithAlpha(cool, Theme.IsLight ? 0.10f : 0.14f), rounding);
                dl.AddRect(min, max, Theme.WithAlpha(cool, 0.35f), rounding, ImDrawFlags.None, UiMetrics.Hairline);
                break;
            case Flair.Quiet:
                dl.AddRect(min, max, Theme.U32(Theme.Glyphs.HighContrast ? s.StrongLine : s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
                break;
        }

        // The dot, then the words: the dot is never the only carrier.
        var cx = min.X + pad + UiMetrics.Px(3.5f);
        dl.AddCircleFilled(new Vector2(cx, textY + (line * 0.5f)), UiMetrics.Px(3.5f), Theme.U32(cool), 12);
        var textX = min.X + pad + UiMetrics.Px(12f);
        ImGui.SetCursorScreenPos(new Vector2(textX, textY));
        Chrome.EllipsisText(updateNoteText, textRoom, Theme.U32(s.Text));
        var textEnd = textX + textRoom;
        if (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(new Vector2(min.X, min.Y), new Vector2(textEnd, max.Y)))
        {
            UiMetrics.Tooltip(updateNoteHover, Strings.UpdateHoverFoot);
        }

        // Update: Dalamud's installer on "Can be updated". A quiet pill, a square at Plain.
        var buttonMin = new Vector2(textEnd + UiMetrics.Px(8f), textY - UiMetrics.Px(1f));
        var buttonSize = new Vector2(UpdateButtonWidth(), line + UiMetrics.Px(2f));
        if (UpdateButton("##updateNow", Strings.UpdateButton, buttonMin, buttonSize, flair))
        {
            updates.OpenInstaller();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.UpdateButtonTooltip);
        }

        // × is Later: hidden until a newer version. Nothing is lost, so no Undo (decision 7).
        var closeMin = new Vector2(buttonMin.X + buttonSize.X + UiMetrics.Px(4f), buttonMin.Y);
        var closeSize = new Vector2(UpdateCloseWidth(), buttonSize.Y);
        ImGui.SetCursorScreenPos(closeMin);
        if (ImGui.InvisibleButton("##updateLater", closeSize))
        {
            updates.Later();
        }

        var closeHovered = ImGui.IsItemHovered();
        if (closeHovered)
        {
            dl.AddRectFilled(closeMin, closeMin + closeSize, Theme.U32(s.Hover), flair == Flair.Plain ? 0f : closeSize.Y * 0.5f);
            UiMetrics.Tooltip(Strings.UpdateLaterTooltip);
        }

        var cross = ImGui.CalcTextSize(UpdateCloseGlyph);
        dl.AddText(closeMin + ((closeSize - cross) * 0.5f), Theme.U32(closeHovered ? s.Text : s.TextSecondary), UpdateCloseGlyph);
        Chrome.FocusRing(closeSize.Y * 0.5f);
        return max.X;
    }

    /// <summary>The × of the note: a plain multiplication sign in the body font.</summary>
    private const string UpdateCloseGlyph = "×";

    private static float UpdatePad() => Theme.Flair == Flair.Plain ? 0f : UiMetrics.Px(8f);

    private static float UpdateButtonWidth() => ImGui.CalcTextSize(Strings.UpdateButton).X + (2f * UiMetrics.Px(8f));

    private static float UpdateCloseWidth() => ImGui.CalcTextSize(UpdateCloseGlyph).X + (2f * UiMetrics.Px(5f));

    /// <summary>Update: a quiet Tide pill (Full, Quiet) or a square ledger button (Plain).</summary>
    private static bool UpdateButton(string id, string label, Vector2 min, Vector2 size, Flair flair)
    {
        ImGui.SetCursorScreenPos(min);
        var clicked = ImGui.InvisibleButton(id, size);
        var hovered = ImGui.IsItemHovered();
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var rounding = flair == Flair.Plain ? 0f : size.Y * 0.5f;
        var fill = flair == Flair.Plain ? (hovered ? s.Hover : s.Raised) : Theme.WithAlphaVector(s.Cool, hovered ? 0.30f : 0.20f);
        dl.AddRectFilled(min, min + size, Theme.U32(fill), rounding);
        dl.AddRect(min, min + size, Theme.U32(flair == Flair.Plain ? s.StrongLine : Theme.WithAlphaVector(s.Cool, 0.45f)), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        var text = ImGui.CalcTextSize(label);
        dl.AddText(min + ((size - text) * 0.5f), Theme.U32(s.Text), label);
        Chrome.FocusRing(rounding);
        return clicked;
    }

    /// <summary>The note's words and hover, rebuilt only when the watcher's state or the language changed.</summary>
    private void RefreshUpdateNote()
    {
        if (Updates is not { } updates || (updateNoteRevision == updates.Revision && updateNoteLanguage == Localization.Loc.Version))
        {
            return;
        }

        updateNoteRevision = updates.Revision;
        updateNoteLanguage = Localization.Loc.Version;
        var state = updates.Current;
        var version = state.Available ?? string.Empty;
        updateNoteText = string.Format(CultureInfo.CurrentCulture, Strings.UpdateReadyFormat, version);
        updateNoteTitle = string.Format(CultureInfo.CurrentCulture, Strings.UpdateHoverTitleFormat, version);
        updateNoteNotes = state.Notes.Length > 0 ? UpdateNotes.Plain(state.Notes) : Strings.UpdateNoNotes;
        updateNoteHover = updateNoteTitle + "\n\n" + updateNoteNotes;
    }
}
