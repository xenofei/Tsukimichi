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

    // Where each quest moved this session stood before its last move, per character, for a kept row's Undo.
    private readonly Dictionary<(ulong ContentId, uint RowId), SetAsideEdits.Before> lastBefore = [];

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
        Apply(session.ViewedContentId, [quest.RowId], aside: true, notForMe, keepSetAside: false, string.Format(CultureInfo.CurrentCulture, format, session.Spoilers.DisplayName(quest)));
    }

    /// <summary>
    /// Sets a whole group aside for later (the pane asked first), with Undo, for <paramref name="contentId"/>: the
    /// character the pane asked about. A quest already set aside, for later or as "Not for me", stays as it is.
    /// </summary>
    public void SetAside(ulong contentId, IReadOnlyList<uint> questRowIds)
    {
        ArgumentNullException.ThrowIfNull(questRowIds);
        Apply(contentId, questRowIds, aside: true, notForMe: false, keepSetAside: true, string.Format(CultureInfo.CurrentCulture, Strings.BluesSetAsideGroupToastFormat, questRowIds.Count));
    }

    /// <summary>Brings a set-aside quest back into the counts, with Undo (which sets it aside as it was).</summary>
    public void BringBack(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        Apply(session.ViewedContentId, [quest.RowId], aside: false, notForMe: false, keepSetAside: false, string.Format(CultureInfo.CurrentCulture, Strings.BluesBroughtBackToastFormat, session.Spoilers.DisplayName(quest)));
    }

    /// <summary>The edit, then an Undo that puts every quest back exactly as it was (<see cref="SetAsideEdits.Restore"/>).</summary>
    private void Apply(ulong? owner, IReadOnlyList<uint> rowIds, bool aside, bool notForMe, bool keepSetAside, string toast)
    {
        if (owner is not { } contentId || rowIds.Count == 0)
        {
            return;
        }

        var before = SetAsideEdits.Capture(book, contentId, rowIds);
        foreach (var quest in before)
        {
            lastBefore[(contentId, quest.RowId)] = quest;
        }

        book.Edit(SetAsideEdits.Changes(before, contentId, aside, notForMe, keepSetAside));
        UndoToast.Show(toast, () => book.Edit(SetAsideEdits.Restore(before, contentId)));
    }

    /// <summary>
    /// The Undo of a row kept in place (My blues' quiet line): the quest back where it stood before its last move on
    /// the character on view, exactly ("Not for me" stays "Not for me"). Nothing when this session did not move it.
    /// </summary>
    public void Undo(uint questRowId)
    {
        if (session.ViewedContentId is not { } contentId || !lastBefore.Remove((contentId, questRowId), out var before))
        {
            return;
        }

        book.Edit(SetAsideEdits.Restore([before], contentId));
    }
}
