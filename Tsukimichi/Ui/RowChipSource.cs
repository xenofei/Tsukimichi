using System;
using System.Collections.Generic;
using System.Globalization;
using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The Journal rows' trailing chips (1.19.0): "Replaying" on a quest the logged-in character's New Game+ session
/// replays (C4; it keeps Completed, silver, with its first date), "Ends in 2 days" on a quest in the journal whose
/// event ends soon (C10, <see cref="EventWarningSource"/>), and what the game itself showed (C1; spec-1.19 "In the
/// table"): "seen in game" on a Ready row the game offered, "game disagrees" on a Blocked one it offered lately. The New
/// Game+ chip shows only while the window views the character the session belongs to. The C1 chips are built once per
/// quest and kept until the session, the game's offers, the language or the minute move, so a frame allocates nothing. Framework
/// thread only.
/// </summary>
public sealed class RowChipSource
{
    private readonly SessionState session;
    private readonly EventWarningSource events;

    // The C1 chips by row id (null for none), as of the key.
    private readonly Dictionary<uint, RowChip?> gameChips = [];
    private (int Version, int Offers, int Language, long Minute) gameKey = (-1, -1, -1, -1);

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

        if (events.Chip(quest, state) is { } ends)
        {
            return new RowChip(ends, Strings.EventEndsChipTooltip);
        }

        return state is QuestState.Ready or QuestState.ReadyOnOtherJob or QuestState.Blocked or QuestState.Foreclosed
            ? GameChip(quest)
            : null;
    }

    /// <summary>"seen in game" or "game disagrees" for the character on view, built once per quest.</summary>
    private RowChip? GameChip(QuestRecord quest)
    {
        // The minute too: a disagreement lapses an hour after the game last showed the quest (GameOfferChecks.Fresh).
        var key = (session.Version, session.GameOffersRevision, Localization.Loc.Version, DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute);
        if (key != gameKey)
        {
            gameKey = key;
            gameChips.Clear();
        }

        if (!gameChips.TryGetValue(quest.RowId, out var chip))
        {
            chip = BuildGameChip(quest);
            gameChips[quest.RowId] = chip;
        }

        return chip;
    }

    private RowChip? BuildGameChip(QuestRecord quest)
    {
        if (session.ViewedContentId is not { } contentId
            || session.GameOffer(contentId, quest.RowId) is not { } sighting
            || !session.States.TryGetValue(quest.RowId, out var evaluation))
        {
            return null;
        }

        var check = GameOfferChecks.Judge(quest, evaluation, sighting, 0, DateTime.UtcNow);
        var date = UiFormat.ShortDate(sighting.FirstSeenUtc);
        if (evaluation.State is QuestState.Ready or QuestState.ReadyOnOtherJob)
        {
            var format = evaluation.ByGame == GameAnswer.Override ? Strings.SeenInGameOverrideTooltipFormat : Strings.SeenInGameTooltipFormat;
            return new RowChip(Strings.SeenInGameChip, string.Format(CultureInfo.CurrentCulture, format, date), RowChipLook.Word);
        }

        if (check.Verdict != GameOfferVerdict.Disagrees)
        {
            return null;
        }

        var (first, reason) = GameOfferChecks.Expectation(evaluation, quest, session.Names, session.States);
        var tooltip = first is { } expected
            ? string.Format(CultureInfo.CurrentCulture, Strings.GameDisagreesTooltipFormat, session.Spoilers.DisplayName(session.Names.Catalog, expected, expected.ToString(CultureInfo.InvariantCulture)))
            : string.Format(CultureInfo.CurrentCulture, Strings.GameDisagreesReasonTooltipFormat, Strings.StateName(evaluation.State, quest), reason);
        return new RowChip(Strings.GameDisagreesChip, tooltip, RowChipLook.Dotted);
    }
}
