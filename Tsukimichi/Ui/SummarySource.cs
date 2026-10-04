using System;
using System.Collections.Generic;
using System.Globalization;
using Tsukimichi.Commands;
using Tsukimichi.Core.Characters;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Journal;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Tonight in a few lines for the logged-in character (plan v8 M1 and M2): Up next and its step, the Ready count, how
/// many quests start in this zone, the journal, the events ending soon and the story meter, as one immutable
/// <see cref="TsukimichiSummary"/>. The server info bar's tooltip and the summary IPC gates read it; the moon icon's quick
/// card can too. Every name goes through the character's spoiler shield, so nothing here names what the shield hides.
/// <para>
/// Built on the framework thread when an input moves (the session, the language, the shield, the events, the zone's
/// count) and at most every <see cref="RefreshSeconds"/> otherwise (a step moves with the player); reading
/// <see cref="Current"/> from any thread allocates nothing.
/// </para>
/// </summary>
public sealed class SummarySource
{
    /// <summary>The longest a capture is kept while nothing it keys on moved (the current step follows the player).</summary>
    public const double RefreshSeconds = 10.0;

    private readonly SessionState session;
    private readonly GuidanceCommand guidance;
    private readonly Func<int> hereCount;
    private readonly EventWarningSource? events;

    private (int Version, ulong? Character, int Language, int Spoilers, int Events, int Here) key = (-1, null, -1, -1, -1, -1);
    private double builtAt = double.NegativeInfinity;
    private volatile TsukimichiSummary current = TsukimichiSummary.Empty;

    /// <param name="hereCount">The quests that can start in the current zone (the Nearby window's count).</param>
    /// <param name="events">The ending-soon events; null leaves them out.</param>
    public SummarySource(SessionState session, GuidanceCommand guidance, Func<int> hereCount, EventWarningSource? events)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.guidance = guidance ?? throw new ArgumentNullException(nameof(guidance));
        this.hereCount = hereCount ?? throw new ArgumentNullException(nameof(hereCount));
        this.events = events;
    }

    /// <summary>The last capture; <see cref="TsukimichiSummary.Empty"/> while nobody is logged in. Any thread.</summary>
    public TsukimichiSummary Current => current;

    /// <summary>Moves whenever <see cref="Current"/> says something new.</summary>
    public int Revision { get; private set; }

    /// <summary>Raised on the framework thread after <see cref="Revision"/> moved.</summary>
    public event Action? Changed;

    /// <summary>Framework thread: rebuilds the capture when an input moved or it is older than <see cref="RefreshSeconds"/>.</summary>
    public void Refresh(double nowSeconds)
    {
        var eventsRevision = events?.Revision ?? 0;
        var next = (session.Version, session.LiveContentId, Localization.Loc.Version, session.LiveContentId is null ? 0 : session.LiveSpoilers.Fingerprint, eventsRevision, hereCount());
        if (next == key && nowSeconds - builtAt < RefreshSeconds)
        {
            return;
        }

        key = next;
        builtAt = nowSeconds;
        var built = Build();
        if (!built.Same(current))
        {
            current = built;
            Revision++;
            Changed?.Invoke();
        }
    }

    private TsukimichiSummary Build()
    {
        if (session.Bundle is not { } bundle || session.LiveContentId is null || session.LiveSnapshot is not { } snapshot)
        {
            return TsukimichiSummary.Empty;
        }

        var catalog = bundle.Catalog;
        var states = session.LiveStates;
        var spoilers = session.LiveSpoilers;
        var job = bundle.Names.ClassJobAbbreviation(snapshot.CurrentJob) ?? string.Empty;
        var level = snapshot.JobLevels.TryGetValue(snapshot.CurrentJob, out var l) ? l : (short)0;

        uint upNextRow = 0;
        string upNextName = string.Empty, upNextStep = string.Empty;
        if (guidance.UpNextWithStep() is { } pick)
        {
            upNextRow = pick.Quest.RowId;
            upNextName = spoilers.DisplayName(pick.Quest);
            upNextStep = PlaceLine(pick.Quest, pick.Step, guidance.StepZone(pick.Step), spoilers);
        }

        var slots = JournalSlots.Of(snapshot, catalog);
        var ending = new List<(string Name, int DaysLeft)>();
        if (events is not null)
        {
            foreach (var warning in events.Current)
            {
                ending.Add((warning.Festival.Name, warning.DaysLeft));
            }
        }

        // IPC GetReadyTonight: the first Ready quests in Tonight's order, named and placed through the same shield.
        var readyTonight = new List<(uint RowId, string Name, string Place)>();
        foreach (var quest in guidance.ReadyInTonightOrder(TsukimichiSummary.MaxReadyTonight))
        {
            readyTonight.Add((quest.RowId, spoilers.DisplayName(quest), GiverPortraits.Place(quest, spoilers) ?? string.Empty));
        }

        var story = RosterBoard.StoryOf(catalog, states);
        return new TsukimichiSummary(
            Ready: true,
            Character: snapshot.Name,
            Job: job,
            Level: level,
            UpNextRowId: upNextRow,
            UpNextName: upNextName,
            UpNextStep: upNextStep,
            ReadyCount: RosterBoard.ReadyCount(catalog, states),
            HereCount: Math.Max(0, hereCount()),
            JournalUsed: slots.Used,
            JournalCap: slots.Cap,
            EndingSoon: ending,
            StoryPart: story?.Part ?? string.Empty,
            StoryLeft: story?.LeftToLatest ?? 0,
            CaughtUp: story?.CaughtUp ?? false)
        {
            ReadyTonight = readyTonight,
        };
    }

    /// <summary>
    /// "Step 3: Speak with Erenville. · Shaaloani", or "Talk to … · place", as Tonight's Up next writes it. The step's zone
    /// is its own name (<paramref name="zone"/>) named here through the logged-in character's shield, never the step
    /// view's <see cref="StepView.Zone"/>, which the viewed character's shield names for the panes.
    /// </summary>
    private static string PlaceLine(Core.Model.QuestRecord quest, StepView? step, string? zone, Core.Query.SpoilerMask spoilers)
    {
        if (step is not null)
        {
            return SummaryText.StepLine(step.Step.Step, step.Objective, zone, spoilers, Strings.UpNextStepFormat, Strings.StepNoObjective, Strings.UpNextSeparator);
        }

        var giver = GiverPortraits.Name(quest, spoilers);
        if (giver.Length == 0)
        {
            return string.Empty;
        }

        var talk = string.Format(CultureInfo.CurrentCulture, Strings.UpNextTalkFormat, giver);
        return GiverPortraits.Place(quest, spoilers) is { Length: > 0 } where ? talk + Strings.UpNextSeparator + where : talk;
    }
}
