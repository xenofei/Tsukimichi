using System;
using System.Collections.Generic;
using System.Globalization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Seasonal;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The ending-soon warnings for the viewed character (feature plan v7, 1.19.0, C10; spec-1.19 "C10. Events"), shared by
/// the Tonight card, the Todo overlay, the Journal rows' "Ends in 2 days" chip and the chat line: the running events
/// (<see cref="SeasonalNow"/>, with the player's entered dates) that end within Settings › Alerts › "Warn before an
/// event ends" days (<see cref="EventWarnings"/>). Built when the session, the setting or the minute changes; reading
/// it allocates nothing. Framework thread only.
/// </summary>
public sealed class EventWarningSource
{
    private readonly SessionState session;
    private readonly Func<int> warnDays;
    private readonly Func<QuestRecord, int>? rewardsMissing;

    private (int Version, int Days, long Minute, int Language) key = (-1, -1, -1, -1);
    private IReadOnlyList<EndingSoonEvent> current = [];
    private readonly Dictionary<ushort, string> chips = [];

    /// <param name="warnDays">The warning window in days (0 turns warnings off).</param>
    /// <param name="rewardsMissing">Rewards a quest gives that the viewed character lacks; null leaves rewards out.</param>
    public EventWarningSource(SessionState session, Func<int> warnDays, Func<QuestRecord, int>? rewardsMissing)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.warnDays = warnDays ?? throw new ArgumentNullException(nameof(warnDays));
        this.rewardsMissing = rewardsMissing;
    }

    /// <summary>Moves whenever <see cref="Current"/> was rebuilt.</summary>
    public int Revision { get; private set; }

    /// <summary>The events ending within the window, soonest first; empty when none, or warnings are off.</summary>
    public IReadOnlyList<EndingSoonEvent> Current
    {
        get
        {
            Refresh();
            return current;
        }
    }

    /// <summary>"Ends in 2 days" for a quest of an ending event that is in the journal; null otherwise.</summary>
    public string? Chip(QuestRecord quest, QuestState state)
    {
        ArgumentNullException.ThrowIfNull(quest);
        Refresh();
        return state == QuestState.Accepted && quest.Festival != 0 && chips.TryGetValue(quest.Festival, out var text) ? text : null;
    }

    /// <summary>Whether a quest's event ends soon and the quest is in the journal (it sorts first).</summary>
    public bool First(QuestRecord quest, QuestState state) => Chip(quest, state) is not null;

    /// <summary>"Ends in 2 days", "Ends tomorrow", "Ends today".</summary>
    public static string ChipText(int daysLeft) => daysLeft switch
    {
        0 => Strings.EventEndsToday,
        1 => Strings.EventEndsTomorrow,
        _ => string.Format(CultureInfo.CurrentCulture, Strings.EventEndsInDaysFormat, daysLeft),
    };

    /// <summary>"All Saints' Wake ends in 2 days", "… ends tomorrow", "… ends today, 07:59" (local time).</summary>
    public static string Title(EndingSoonEvent warning)
    {
        ArgumentNullException.ThrowIfNull(warning);
        var name = warning.Festival.Name;
        return warning.DaysLeft switch
        {
            0 => string.Format(CultureInfo.CurrentCulture, Strings.EventCardTitleTodayFormat, name, warning.EndUtc.ToLocalTime().ToString(Strings.TimeFormat, CultureInfo.CurrentCulture)),
            1 => string.Format(CultureInfo.CurrentCulture, Strings.EventCardTitleTomorrowFormat, name),
            _ => string.Format(CultureInfo.CurrentCulture, Strings.EventCardTitleFormat, name, warning.DaysLeft),
        };
    }

    private void Refresh()
    {
        var now = DateTime.UtcNow;
        var days = Math.Clamp(warnDays(), 0, EventWarnings.MaxWarnDays);
        var next = (session.Version, days, now.Ticks / TimeSpan.TicksPerMinute, Localization.Loc.Version);
        if (next == key)
        {
            return;
        }

        key = next;
        chips.Clear();
        if (days == 0 || session.Bundle is not { } bundle || session.ViewedSnapshot is null || session.States.Count == 0)
        {
            current = [];
            Revision++;
            return;
        }

        var running = SeasonalNow.Running(bundle.Catalog, session.ServerFestivals, session.States, session.Curated.Festivals, now, session.EnteredFestivalEnds);
        current = EventWarnings.EndingSoon(running, now, days, TimeZoneInfo.Local, rewardsMissing);
        foreach (var warning in current)
        {
            chips[warning.Festival.FestivalId] = ChipText(warning.DaysLeft);
        }

        Revision++;
    }
}
