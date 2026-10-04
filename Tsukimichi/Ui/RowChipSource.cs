using System;
using Tsukimichi.Core.Model;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The Journal rows' trailing chips (1.19.0): "Replaying" on a quest the logged-in character's New Game+ session
/// replays (C4; it keeps Completed, silver, with its first date) and "Ends in 2 days" on a quest in the journal whose
/// event ends soon (C10, <see cref="EventWarningSource"/>). The New Game+ chip shows only while the window views the
/// character the session belongs to. Framework thread only.
/// </summary>
public sealed class RowChipSource
{
    private readonly SessionState session;
    private readonly EventWarningSource events;

    public RowChipSource(SessionState session, EventWarningSource events)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    /// <summary>The chip for one row; null for none.</summary>
    public RowChip? For(QuestRecord quest, QuestState state)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var replay = session.NewGamePlus;
        if (replay.Active && session.ViewedContentId == replay.ContentId && replay.IsReplaying(quest.QuestId))
        {
            return new RowChip(Strings.ReplayingChip, Strings.ReplayingChipTooltip);
        }

        return events.Chip(quest, state) is { } ends ? new RowChip(ends, Strings.EventEndsChipTooltip) : null;
    }
}
