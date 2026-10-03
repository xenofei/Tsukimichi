using System;
using System.Collections.Generic;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The rail's Journal badge (plan v7, spec Revision 3 R3.2): keeps the viewed character's newly-ready quests
/// (<see cref="NewlyReady"/>) against the seen set stored in <c>user/characters.json</c>
/// (<see cref="CharacterSettings.SeenReady"/>), and gives the rail its number for the chosen
/// <see cref="JournalBadgeMode"/>. The bookkeeping is <see cref="NewlyReadyTracker"/>'s; this feeds it the session and
/// the window state. The tallies are recomputed when the evaluation changes (the session's version, the catalog or the
/// character), never per frame. A quest is marked seen when the player selects it, and every quest of the Newly ready
/// list when that list is closed or replaced. Framework thread only.
/// </summary>
public sealed class JournalBadge
{
    private readonly UiState ui;
    private readonly Func<CharacterSettingsBook?> book;
    private readonly NewlyReadyTracker tracker;

    private int sessionVersion = -1;
    private QuestCatalog? catalog;

    // The quest selected when last looked, so a selection is marked seen once.
    private uint? lastSelected;

    // The Newly ready list the badge opened, while it is the table's scope.
    private QuestScope? openScope;
    private uint serial;

    /// <param name="ui">The window state: the selection, the scope and the list the badge opens.</param>
    /// <param name="book">The per-character settings the seen set is kept in; null (tests, before load) keeps it in memory.</param>
    public JournalBadge(UiState ui, Func<CharacterSettingsBook?> book)
    {
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
        this.book = book ?? throw new ArgumentNullException(nameof(book));
        tracker = new NewlyReadyTracker(id => this.book()?.SeenReady(id), Save);
    }

    /// <summary>The viewed character's quests newly ready since the player last looked.</summary>
    public int NewCount => tracker.New.Length;

    /// <summary>The viewed character's quests Ready on the current job.</summary>
    public int ReadyCount => tracker.Tally.Ready;

    /// <summary>Of <see cref="ReadyCount"/>, the main scenario and unlock quests.</summary>
    public int StoryReadyCount => tracker.Tally.StoryReady;

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
        if (id != tracker.ContentId)
        {
            // The list of the character left closes without marking: its entry may just have been forgotten.
            openScope = null;
            tracker.SwitchTo(id);
            sessionVersion = -1;
        }

        if (id != 0 && current is not null && session.States.Count > 0 && (session.Version != sessionVersion || !ReferenceEquals(current, catalog)))
        {
            sessionVersion = session.Version;
            catalog = current;
            Recompute(session, current);
        }

        if (openScope is { } scope && ui.Scope != scope)
        {
            openScope = null;
            tracker.CloseList(markSeen: true);
        }

        if (ui.SelectedRowId != lastSelected)
        {
            lastSelected = ui.SelectedRowId;
            if (lastSelected is { } selected)
            {
                tracker.Select(selected);
            }
        }
    }

    /// <summary>
    /// The badge was clicked: the Journal opens on the quests it counts as newly ready (<see cref="UiState.ShowNewlyReady"/>).
    /// They stay new until the list is closed or replaced. Nothing happens with none; with that list already open the click
    /// only returns to the Journal.
    /// </summary>
    public void ShowNew()
    {
        if (openScope is { } scope && ui.Scope == scope)
        {
            // Already listed: a second click only brings the Journal back, filters and search left as they are.
            ui.Tab = NavTab.Journal;
            return;
        }

        if (tracker.Open() is not { } ids)
        {
            return;
        }

        serial++;
        ui.ShowNewlyReady(serial, ids);
        openScope = ui.Scope;
    }

    private void Recompute(SessionState session, QuestCatalog current)
    {
        var states = session.States;
        var tally = NewlyReady.Tally(current, states, session.FeatureQuestIds);

        // Only a capture with quest data in it seeds the seen set: one committed before the game sent it lists nothing.
        var settled = session.ViewedSnapshot is { } snapshot && !LoginReadiness.LooksEmpty(snapshot);
        tracker.Recompute(
            tally,
            rowId => NewlyReady.IsGone(current.ByRowId.GetValueOrDefault(rowId), states.GetValueOrDefault(rowId)),
            settled);
    }

    /// <summary>Stores a character's seen set, unless the player chose not to track it; false when not stored.</summary>
    private bool Save(ulong contentId, uint[] seen)
    {
        if (book() is not { } settings || !settings.IsTracked(contentId))
        {
            return false;
        }

        settings.Edit(CharacterSettingChange.Seen(contentId, seen));
        return true;
    }
}
