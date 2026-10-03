using System;
using System.Collections.Generic;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The rail's Journal badge (plan v7, spec Revision 3 R3.2): keeps the viewed character's newly-ready quests
/// (<see cref="NewlyReady"/>) against the seen set stored in <c>user/characters.json</c>
/// (<see cref="CharacterSettings.SeenReady"/>), and gives the rail its number for the chosen
/// <see cref="JournalBadgeMode"/>. The tallies are recomputed when the evaluation changes (the session's version, the
/// catalog or the character), never per frame. A quest is marked seen when the player selects it, and every quest of
/// the Newly ready list when that list is closed or replaced. Framework thread only.
/// </summary>
public sealed class JournalBadge
{
    private readonly UiState ui;
    private readonly Func<CharacterSettingsBook?> book;

    private ulong contentId;
    private int sessionVersion = -1;
    private QuestCatalog? catalog;
    private ReadyTally tally = ReadyTally.Empty;
    private HashSet<uint> available = [];
    private uint[] newIds = [];
    private uint[] seen = [];

    // The quest selected when last looked, so a selection is marked seen once.
    private uint? lastSelected;

    // The Newly ready list the badge opened, while it is the table's scope.
    private QuestScope? openScope;
    private uint[] openIds = [];
    private uint serial;

    /// <param name="ui">The window state: the selection, the scope and the list the badge opens.</param>
    /// <param name="book">The per-character settings the seen set is kept in; null (tests, before load) keeps it in memory.</param>
    public JournalBadge(UiState ui, Func<CharacterSettingsBook?> book)
    {
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
        this.book = book ?? throw new ArgumentNullException(nameof(book));
    }

    /// <summary>The viewed character's quests newly ready since the player last looked.</summary>
    public int NewCount => newIds.Length;

    /// <summary>The viewed character's quests Ready on the current job.</summary>
    public int ReadyCount => tally.Ready;

    /// <summary>Of <see cref="ReadyCount"/>, the main scenario and unlock quests.</summary>
    public int StoryReadyCount => tally.StoryReady;

    /// <summary>The number on the badge for <paramref name="mode"/> (<paramref name="everyReady"/> is the tree's Ready count); 0 shows none.</summary>
    public int Count(JournalBadgeMode mode, int everyReady) =>
        NewlyReady.BadgeCount(mode, NewCount, StoryReadyCount, everyReady);

    /// <summary>
    /// Once per frame after the query runner: recomputes the tallies when the evaluation changed, closes the Newly ready
    /// list when the scope moved off it (marking its quests seen), and marks a newly selected quest seen.
    /// </summary>
    public void Update(SessionState session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var id = session.ViewedContentId ?? 0;
        var current = session.Bundle?.Catalog;
        if (id != contentId)
        {
            CloseList(markSeen: true);
            contentId = id;
            sessionVersion = -1;
            tally = ReadyTally.Empty;
            available = [];
            newIds = [];
            seen = [];
        }

        if (id != 0 && current is not null && session.States.Count > 0 && (session.Version != sessionVersion || !ReferenceEquals(current, catalog)))
        {
            sessionVersion = session.Version;
            catalog = current;
            Recompute(session, current);
        }

        if (openScope is { } scope && ui.Scope != scope)
        {
            CloseList(markSeen: true);
        }

        if (ui.SelectedRowId != lastSelected)
        {
            lastSelected = ui.SelectedRowId;
            if (lastSelected is { } selected && Array.BinarySearch(newIds, selected) >= 0)
            {
                MarkSeen([selected]);
            }
        }
    }

    /// <summary>
    /// The badge was clicked: the Journal opens on the quests it counts as newly ready (<see cref="UiState.ShowNewlyReady"/>).
    /// They stay new until the list is closed or replaced. Nothing happens with none.
    /// </summary>
    public void ShowNew()
    {
        if (newIds.Length == 0)
        {
            return;
        }

        CloseList(markSeen: true);
        serial++;
        ui.ShowNewlyReady(serial, newIds);
        openScope = ui.Scope;
        openIds = newIds;
    }

    private void Recompute(SessionState session, QuestCatalog current)
    {
        tally = NewlyReady.Tally(current, session.States, session.FeatureQuestIds);
        available = new HashSet<uint>(tally.Available);
        var states = session.States;
        var state = NewlyReady.Reconcile(
            tally.Available,
            book()?.SeenReady(contentId),
            rowId => NewlyReady.IsGone(current.ByRowId.GetValueOrDefault(rowId), states.GetValueOrDefault(rowId)));
        newIds = state.New;
        seen = state.Seen;
        if (state.SeenChanged)
        {
            Save();
        }
    }

    private void CloseList(bool markSeen)
    {
        if (openScope is null)
        {
            return;
        }

        var ids = openIds;
        openScope = null;
        openIds = [];
        if (markSeen)
        {
            MarkSeen(ids);
        }
    }

    private void MarkSeen(IEnumerable<uint> rowIds)
    {
        if (NewlyReady.MarkSeen(seen, rowIds, available) is not { } next)
        {
            return;
        }

        seen = next;
        newIds = NewlyReady.Reconcile(tally.Available, seen).New;
        Save();
    }

    /// <summary>Stores the seen set for the viewed character, unless the player chose not to track it.</summary>
    private void Save()
    {
        if (contentId == 0 || book() is not { } settings || !settings.IsTracked(contentId))
        {
            return;
        }

        settings.Edit(CharacterSettingChange.Seen(contentId, seen));
    }
}
