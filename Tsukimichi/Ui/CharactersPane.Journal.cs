using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Journal;

namespace Tsukimichi.Ui;

/// <summary>
/// The journal's room on the Characters dashboard (feature plan v7, C9): the section's header says what is left
/// ("3 journal slots left", "Journal full"), and under it the same Make room body as the status bar's popover
/// (spec-1.19 "Make room", <see cref="MakeRoomView"/>): the viewed character's journal quests grouped by what finishing
/// them takes, the ones safe to drop marked, and Open in journal on the hovered row. Opens by itself when the journal
/// crosses into nearly full (<see cref="JournalRoomSection"/>), once, so the player can still close it. Tsukimichi never abandons a quest: the footer says how to drop one in the game, and the Abandoned list
/// keeps the step it had reached.
/// </summary>
public sealed partial class CharactersPane
{
    private MakeRoomView? makeRoom;
    private string roomHeader = string.Empty;
    private JournalRoom roomLevel;
    private JournalRoom? roomLevelSeen;
    private bool roomOpenPending;
    private int roomVersion = -1;
    private int roomLanguage = -1;

    /// <summary>The Make room body the window built (<see cref="MainWindow"/>); until set the section is hidden.</summary>
    internal void AttachMakeRoom(MakeRoomView view) => makeRoom = view;

    private void DrawJournalRoom()
    {
        RefreshJournalRoom();
        if (roomHeader.Length == 0 || makeRoom is not { } view)
        {
            return;
        }

        using var id = ImRaii.PushId("journalRoom");

        // Crossed into nearly full: opened once, on this frame only, so a close afterwards sticks.
        if (roomOpenPending)
        {
            roomOpenPending = false;
            ImGui.SetNextItemOpen(true);
        }

        // A full journal needs the player, it isn't an error (spec-1.19, "Colour language"): the header keeps the text tone.
        var open = ImGui.CollapsingHeader(roomHeader);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.JournalRoomTooltip);
        }

        if (open)
        {
            view.DrawBody(ImGui.GetContentRegionAvail().X, title: false);
        }
    }

    /// <summary>Rebuilds the header from the viewed character's journal when the session or the language changed.</summary>
    private void RefreshJournalRoom()
    {
        if (roomVersion == session.Version && roomLanguage == Localization.Loc.Version)
        {
            return;
        }

        roomVersion = session.Version;
        roomLanguage = Localization.Loc.Version;
        if (session.ViewedSnapshot is not { } snapshot || session.Bundle is not { } bundle)
        {
            roomHeader = string.Empty;
            roomLevelSeen = null;
            roomOpenPending = false;
            return;
        }

        var slots = JournalSlots.Of(snapshot, bundle.Catalog);
        roomLevel = slots.Room;
        // Open when it crosses into nearly full (kept until the section is next drawn), never while it stays there.
        roomOpenPending = JournalRoomSection.OpensOnChange(roomLevelSeen, roomLevel) || (roomOpenPending && roomLevel != JournalRoom.Room);
        roomLevelSeen = roomLevel;
        roomHeader = slots.Text + "###journalRoomHeader";
    }
}
