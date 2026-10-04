using System;
using System.Collections.Generic;
using System.Globalization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Set aside (feature plan v7 P4, spec-1.21 "Set aside"): "Set aside for later", "Not for me" and Bring back, for the
/// character on view, kept in <c>user/characters.json</c> (<see cref="CharacterSettings.SetAside"/>,
/// <see cref="CharacterSettings.NotForMe"/>) so other game clients merge them like the other per-character choices.
/// Every change takes effect at once (the session drops the quests from the counts, Nearby, the server info bar, the
/// overlay and notices) and offers the floating Undo for 8 s. One quest needs no confirmation; a whole group asks first
/// in the pane. Framework thread only.
/// </summary>
public sealed class SetAsideActions
{
    private readonly SessionState session;
    private readonly CharacterSettingsBook book;

    public SetAsideActions(SessionState session, CharacterSettingsBook book)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.book = book ?? throw new ArgumentNullException(nameof(book));
    }

    /// <summary>Whether a character is on view, whose quests can be set aside.</summary>
    public bool CanSetAside => session.ViewedContentId is not null;

    /// <summary>Whether the quest is set aside for the character on view.</summary>
    public bool IsSetAside(uint questRowId) => session.ViewedContentId is { } id && book.IsSetAside(id, questRowId);

    /// <summary>Whether the quest is marked "Not for me" for the character on view.</summary>
    public bool IsNotForMe(uint questRowId) => session.ViewedContentId is { } id && book.IsNotForMe(id, questRowId);

    /// <summary>Sets one quest aside ("Set aside for later", or "Not for me"), with Undo.</summary>
    public void SetAside(QuestRecord quest, bool notForMe)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var format = notForMe ? Strings.BluesNotForMeToastFormat : Strings.BluesSetAsideToastFormat;
        Apply([quest.RowId], aside: true, notForMe, string.Format(CultureInfo.CurrentCulture, format, session.Spoilers.DisplayName(quest)));
    }

    /// <summary>Sets a whole group aside for later (the pane asked first), with Undo.</summary>
    public void SetAside(IReadOnlyList<uint> questRowIds)
    {
        ArgumentNullException.ThrowIfNull(questRowIds);
        Apply(questRowIds, aside: true, notForMe: false, string.Format(CultureInfo.CurrentCulture, Strings.BluesSetAsideGroupToastFormat, questRowIds.Count));
    }

    /// <summary>Brings a set-aside quest back into the counts, with Undo (which sets it aside as it was).</summary>
    public void BringBack(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        if (session.ViewedContentId is not { } contentId)
        {
            return;
        }

        var rowId = quest.RowId;
        var notForMe = book.IsNotForMe(contentId, rowId);
        book.Edit(Changes(contentId, [rowId], aside: false, notForMe: false));
        UndoToast.Show(
            string.Format(CultureInfo.CurrentCulture, Strings.BluesBroughtBackToastFormat, session.Spoilers.DisplayName(quest)),
            () => book.Edit(Changes(contentId, [rowId], aside: true, notForMe)));
    }

    private void Apply(IReadOnlyList<uint> rowIds, bool aside, bool notForMe, string toast)
    {
        if (session.ViewedContentId is not { } contentId || rowIds.Count == 0)
        {
            return;
        }

        var ids = new List<uint>(rowIds);
        book.Edit(Changes(contentId, ids, aside, notForMe));
        UndoToast.Show(toast, () => book.Edit(Changes(contentId, ids, aside: false, notForMe: false)));
    }

    /// <summary>
    /// The edits that set <paramref name="rowIds"/> aside (in one list, out of the other) or bring them back (out of both).
    /// </summary>
    private static List<CharacterSettingChange> Changes(ulong contentId, IReadOnlyList<uint> rowIds, bool aside, bool notForMe)
    {
        var changes = new List<CharacterSettingChange>(rowIds.Count * 2);
        foreach (var rowId in rowIds)
        {
            changes.Add(CharacterSettingChange.SetAside(contentId, rowId, aside && !notForMe));
            changes.Add(CharacterSettingChange.NotForMe(contentId, rowId, aside && notForMe));
        }

        return changes;
    }
}
