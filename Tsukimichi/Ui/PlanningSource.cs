using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The 1.9.0 planning extras for the viewed character (feature plan v5 "Planning extras"; R6 C, E, F), shared by the
/// Characters dashboard, the Tonight card and "Since you were away": the main scenario catch-up line
/// (<see cref="MsqCatchUp"/>), what levelling each job opens (<see cref="LevelAdvisor"/>), the line for a next main
/// scenario quest that waits for a level, and the allied society board (<see cref="AlliedSocietyBoard"/>). Every line
/// is built when the session version, the catalog, the language or the viewed character changes, and the board's reset
/// countdown once a minute; drawing allocates nothing. Quest names go through the spoiler shield. Framework thread only.
/// </summary>
public sealed class PlanningSource
{
    /// <summary>Quest names a job's tooltip lists per level before "and N more".</summary>
    public const int MaxNamesPerStep = 5;

    /// <summary>Levels a job's tooltip lists.</summary>
    public const int MaxSteps = 6;

    private readonly SessionState session;
    private readonly GameLinks? links;
    private readonly Func<CatchUpDutySource?>? duties;

    private (int Version, CatalogBundle? Bundle, int Language, ulong? Viewed, CatchUpDutySource? Duties) builtKey = (-1, null, -1, null, null);
    private long boardMinute = -1;

    private MsqCatchUpSummary? catchUp;
    private string catchUpLine = string.Empty;
    private string catchUpTooltip = string.Empty;
    private readonly Dictionary<byte, AdviceText> advice = [];
    private MsqLevelGate? msqGate;
    private string msqGateLine = string.Empty;
    private AlliedSocietyBoardModel board = AlliedSocietyBoardModel.Empty;
    private BoardLine[] boardLines = [];
    private string boardHeader = string.Empty;

    /// <param name="links">Map names and Teleport for the board; null leaves the zone column empty.</param>
    /// <param name="duties">The duties the main scenario quests unlock (<see cref="DutySource"/>); null counts the required ones only.</param>
    public PlanningSource(SessionState session, GameLinks? links, Func<CatchUpDutySource?>? duties = null)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.links = links;
        this.duties = duties;
    }

    /// <summary>
    /// The catch-up's duty source over the curated overlay, the merged reward catalog and the duty index (which maps an
    /// instance to its Duty Finder entry), rebuilt only when one of them is another instance. Shared with "Since you
    /// were away" so both count the same duties.
    /// </summary>
    public static Func<CatchUpDutySource?> DutySource(Func<CuratedData> curated, Func<UniqueRewardCatalog> rewards, Func<DutyRunIndex?> runs)
    {
        ArgumentNullException.ThrowIfNull(curated);
        ArgumentNullException.ThrowIfNull(rewards);
        ArgumentNullException.ThrowIfNull(runs);
        (CuratedData? Curated, UniqueRewardCatalog? Rewards, DutyRunIndex? Runs, CatchUpDutySource? Source) built = default;
        return () =>
        {
            var c = curated();
            var r = rewards();
            var d = runs();
            if (built.Source is null || !ReferenceEquals(built.Curated, c) || !ReferenceEquals(built.Rewards, r) || !ReferenceEquals(built.Runs, d))
            {
                Func<uint, uint>? conditionOf = d is null ? null : instance => d.ByInstance(instance)?.ContentFinderConditionId ?? 0u;
                built = (c, r, d, CatchUpDutySource.From(c, r, conditionOf));
            }

            return built.Source;
        };
    }

    /// <summary>One job's advice as drawn: the row text ("52→56 opens 7 quests (2 unlock quests, MSQ)"), the headline naming the job, the tooltip (the headline, then every level with its quests) and the first quest.</summary>
    public sealed record AdviceText(JobLevelAdvice Advice, string Row, string Headline, string Tooltip, QuestRecord? First);

    /// <summary>One society on the board, as drawn.</summary>
    public sealed record BoardLine(AlliedSocietyRow Row, string Society, string Rank, string RankTooltip, string Today, string TodayTooltip, string Zone)
    {
        /// <summary>
        /// The line under the society (1.19.0, C5; spec-1.19 "C5. Allied societies"): a carried-over daily ("0
        /// allowances until you turn in …"), the rank-up hint, or what is left today.
        /// </summary>
        public string Line { get; init; } = string.Empty;

        /// <summary>The line needs the player: a daily carried over the reset (a copper dot beside the words, which say the same).</summary>
        public bool NeedsYou { get; init; }

        /// <summary>The line's hover.</summary>
        public string LineTooltip { get; init; } = string.Empty;

        /// <summary>"· stored alt Kiri Tsukikage" after the rank on a stored character's carried row; empty otherwise.</summary>
        public string AltNote { get; init; } = string.Empty;
    }

    /// <summary>The catch-up summary; null without a catalog, states or main scenario.</summary>
    public MsqCatchUpSummary? CatchUp
    {
        get
        {
            Refresh();
            return catchUp;
        }
    }

    /// <summary>"To reach the latest story: 143 quests, Lv 90–100, 6 duties", or the caught-up line; empty without data.</summary>
    public string CatchUpLine
    {
        get
        {
            Refresh();
            return catchUpLine;
        }
    }

    /// <summary>The catch-up per expansion, one line each, after the explanation.</summary>
    public string CatchUpTooltip
    {
        get
        {
            Refresh();
            return catchUpTooltip;
        }
    }

    /// <summary>The next main scenario quest when it waits for a level only; null otherwise.</summary>
    public MsqLevelGate? MsqGate
    {
        get
        {
            Refresh();
            return msqGate;
        }
    }

    /// <summary>"The next MSQ quest needs level 56: levelling DRG 52→56 opens 7 quests (…)"; empty when it does not wait for a level.</summary>
    public string MsqGateLine
    {
        get
        {
            Refresh();
            return msqGateLine;
        }
    }

    /// <summary>The advice for a job the viewed character has levelled; null when levelling it opens nothing.</summary>
    public AdviceText? AdviceFor(uint job)
    {
        Refresh();
        return job <= byte.MaxValue && advice.TryGetValue((byte)job, out var text) ? text : null;
    }

    /// <summary>Whether any job's levelling opens something.</summary>
    public bool HasAdvice
    {
        get
        {
            Refresh();
            return advice.Count > 0;
        }
    }

    /// <summary>The board's societies, as drawn.</summary>
    public IReadOnlyList<BoardLine> BoardLines
    {
        get
        {
            Refresh();
            return boardLines;
        }
    }

    /// <summary>"8 allowances left today · daily reset: resets in 3 h".</summary>
    public string BoardHeader
    {
        get
        {
            Refresh();
            return boardHeader;
        }
    }

    /// <summary>Formats "1 quest" / "7 quests".</summary>
    public static string Quests(int count) =>
        count == 1 ? Strings.PlanningQuestsOne : string.Format(CultureInfo.CurrentCulture, Strings.PlanningQuestsFormat, count);

    /// <summary>Formats "no duties" / "1 duty" / "6 duties".</summary>
    public static string Duties(int count) => count switch
    {
        0 => Strings.PlanningDutiesNone,
        1 => Strings.PlanningDutiesOne,
        _ => string.Format(CultureInfo.CurrentCulture, Strings.PlanningDutiesFormat, count),
    };

    /// <summary>"7 quests (2 unlock quests, MSQ)": the count, then what is among it when anything notable is.</summary>
    public static string Opens(int count, int unlocks, int mainScenario)
    {
        var quests = Quests(count);
        var among = new List<string>(2);
        if (unlocks > 0)
        {
            among.Add(unlocks == 1 ? Strings.PlanningUnlocksOne : string.Format(CultureInfo.CurrentCulture, Strings.PlanningUnlocksFormat, unlocks));
        }

        if (mainScenario > 0)
        {
            among.Add(Strings.PlanningMsq);
        }

        return among.Count == 0 ? quests : string.Format(CultureInfo.CurrentCulture, Strings.PlanningCountDetailFormat, quests, string.Join(Strings.PlanningListSeparator, among));
    }

    /// <summary>"To reach the latest story: 143 quests, Lv 90–100, 6 duties", or the caught-up line.</summary>
    public static string CatchUpText(MsqCatchUpSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (summary.IsComplete)
        {
            return Strings.PlanningCatchUpDone;
        }

        return summary.MinLevel == summary.MaxLevel
            ? string.Format(CultureInfo.CurrentCulture, Strings.PlanningCatchUpOneLevelFormat, Quests(summary.Quests), summary.MinLevel, Duties(summary.Duties))
            : string.Format(CultureInfo.CurrentCulture, Strings.PlanningCatchUpFormat, Quests(summary.Quests), summary.MinLevel, summary.MaxLevel, Duties(summary.Duties));
    }

    private void Refresh()
    {
        var bundle = session.Bundle;
        // The duty source moves once, when the duty index lands from its worker: the catch-up then counts duties.
        var dutySource = duties?.Invoke();
        var key = (session.Version, bundle, Localization.Loc.Version, session.ViewedContentId, dutySource);
        var minute = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute;
        if (key == builtKey)
        {
            if (minute != boardMinute)
            {
                RefreshBoard(bundle, minute);
            }

            return;
        }

        builtKey = key;
        catchUp = null;
        catchUpLine = catchUpTooltip = msqGateLine = string.Empty;
        advice.Clear();
        msqGate = null;
        if (bundle is null || session.ViewedSnapshot is not { } snapshot || session.States.Count == 0)
        {
            board = AlliedSocietyBoardModel.Empty;
            boardLines = [];
            boardHeader = string.Empty;
            boardMinute = minute;
            return;
        }

        BuildCatchUp(bundle, snapshot, dutySource);
        BuildAdvice(bundle, snapshot);

        // Another character, catalog or language: the lines are built again even when the numbers match.
        board = AlliedSocietyBoardModel.Empty;
        RefreshBoard(bundle, minute);
    }

    private void BuildCatchUp(CatalogBundle bundle, CharacterSnapshot snapshot, CatchUpDutySource? dutySource)
    {
        catchUp = MsqCatchUp.Compute(bundle.Catalog, session.States, snapshot, dutySource);
        if (catchUp is not { } summary)
        {
            return;
        }

        if (summary.IsComplete)
        {
            catchUpLine = Strings.PlanningCatchUpDone;
            catchUpTooltip = Strings.PlanningCatchUpTooltip;
            return;
        }

        catchUpLine = CatchUpText(summary);
        var text = new StringBuilder(Strings.PlanningCatchUpTooltip);
        foreach (var part in summary.Expansions)
        {
            var name = bundle.Names.Expansion(part.Expansion) is { Length: > 0 } named ? named : Expansions.Name(part.Expansion);
            text.Append('\n');
            text.Append(part.MinLevel == part.MaxLevel
                ? string.Format(CultureInfo.CurrentCulture, Strings.PlanningCatchUpExpansionOneLevelFormat, name, Quests(part.Quests), part.MinLevel, Duties(part.Duties.Count))
                : string.Format(CultureInfo.CurrentCulture, Strings.PlanningCatchUpExpansionFormat, name, Quests(part.Quests), part.MinLevel, part.MaxLevel, Duties(part.Duties.Count)));
        }

        catchUpTooltip = text.ToString();
    }

    private void BuildAdvice(CatalogBundle bundle, CharacterSnapshot snapshot)
    {
        var jobs = new List<byte>(snapshot.JobLevels.Count);
        foreach (var (job, level) in snapshot.JobLevels)
        {
            if (level > 0)
            {
                jobs.Add(job);
            }
        }

        jobs.Sort();
        var all = LevelAdvisor.Compute(bundle.Catalog, session.States, snapshot, session.Context, jobs, session.FeatureQuestIds);
        foreach (var job in all)
        {
            if (job.Next is not { } next)
            {
                continue;
            }

            var opens = Opens(next.Count, next.Unlocks, next.MainScenario);
            var abbreviation = bundle.Names.ClassJobAbbreviation(job.Job);
            var row = string.Format(CultureInfo.CurrentCulture, Strings.PlanningLevelRowFormat, job.Level, next.Level, opens);
            var headline = string.Format(CultureInfo.CurrentCulture, Strings.PlanningLevelOpensFormat, abbreviation, job.Level, next.Level, opens);
            advice[job.Job] = new AdviceText(job, row, headline, headline + "\n\n" + Tooltip(job), next.Quests[0]);
        }

        msqGate = LevelAdvisor.MsqGate(bundle.Catalog, session.States, snapshot, session.Context);
        if (msqGate is { } gate)
        {
            // What levelling the gate's job up to the quest's level opens in all, the main scenario quest among it.
            var gated = LevelAdvisor.Compute(bundle.Catalog, session.States, snapshot, session.Context, [gate.Job], session.FeatureQuestIds);
            int count = 0, unlocks = 0, msq = 0;
            foreach (var job in gated)
            {
                foreach (var step in job.Steps)
                {
                    if (step.Level <= gate.Level)
                    {
                        count += step.Count;
                        unlocks += step.Unlocks;
                        msq += step.MainScenario;
                    }
                }
            }

            msqGateLine = string.Format(
                CultureInfo.CurrentCulture,
                Strings.PlanningTonightLevelFormat,
                gate.Level,
                bundle.Names.ClassJobAbbreviation(gate.Job),
                gate.JobLevel,
                Opens(Math.Max(1, count), unlocks, Math.Max(1, msq)));
        }
    }

    /// <summary>One line per level (up to <see cref="MaxSteps"/>), each with up to <see cref="MaxNamesPerStep"/> names under it.</summary>
    private string Tooltip(JobLevelAdvice job)
    {
        var text = new StringBuilder();
        var shown = 0;
        foreach (var step in job.Steps)
        {
            if (shown++ == MaxSteps)
            {
                break;
            }

            if (text.Length > 0)
            {
                text.Append('\n');
            }

            text.Append(string.Format(CultureInfo.CurrentCulture, Strings.PlanningLevelStepFormat, step.Level, Opens(step.Count, step.Unlocks, step.MainScenario)));
            for (var i = 0; i < step.Quests.Count && i < MaxNamesPerStep; i++)
            {
                text.Append("\n  ").Append(session.Spoilers.DisplayName(step.Quests[i]));
            }

            if (step.Quests.Count > MaxNamesPerStep)
            {
                text.Append("\n  ").Append(string.Format(CultureInfo.CurrentCulture, Strings.PlanningLevelMoreFormat, step.Quests.Count - MaxNamesPerStep));
            }
        }

        return text.ToString();
    }

    private void RefreshBoard(CatalogBundle? bundle, long minute)
    {
        boardMinute = minute;
        if (bundle is null || session.ViewedSnapshot is not { } snapshot)
        {
            board = AlliedSocietyBoardModel.Empty;
            boardLines = [];
            boardHeader = string.Empty;
            return;
        }

        // Today's offer is read for the logged-in character only; a stored one's reads as unknown.
        var context = session.Context;
        var now = DateTime.UtcNow;
        var built = session.IsLive
            ? AlliedSocietyBoard.Build(bundle.Catalog, snapshot, context.TodaysDailyOffer, context.DailyOfferTribes, now, session.AcceptedSince)
            : AlliedSocietyBoard.Build(bundle.Catalog, snapshot, null, null, now, session.AcceptedSince);
        // A stored character's allowances are projected from its last login (1.19.0, C5): the header says so, and says
        // when a daily it held then carries over the reset.
        boardHeader = !session.IsLive && built.Projected
            ? built.Carried.Count > 0
                ? string.Format(CultureInfo.CurrentCulture, Strings.AlliedStoredHoldsFormat, UiFormat.Age(built.TakenUtc, now))
                : string.Format(CultureInfo.CurrentCulture, Strings.AlliedStoredProjectedFormat, built.AllowancesLeft, UiFormat.Age(built.TakenUtc, now))
            : string.Format(CultureInfo.CurrentCulture, Strings.PlanningBoardAllowancesFormat, built.AllowancesLeft, GameResets.ResetsIn(built.NextReset - now));
        if (ReferenceEquals(board, built) || SameRows(board, built))
        {
            board = built;
            return;
        }

        board = built;
        var lines = new BoardLine[built.Rows.Count];
        for (var i = 0; i < lines.Length; i++)
        {
            var row = built.Rows[i];
            var society = bundle.Names.Tribe(row.Tribe) is { Length: > 0 } tribe ? tribe : string.Format(CultureInfo.InvariantCulture, Strings.CharactersTribeFormat, row.Tribe);
            var rankName = bundle.Names.TribeRank(row.Rank) is { Length: > 0 } named ? named : TribeRanks.Name(row.Rank);
            var rank = row.AtLastRank || row.RankMax is null ? rankName
                : row.Maxed ? string.Format(CultureInfo.CurrentCulture, Strings.PlanningBoardRankFullFormat, rankName)
                : string.Format(CultureInfo.CurrentCulture, Strings.PlanningBoardRankFormat, rankName, row.Reputation, row.RankMax);
            // What is left today ("1 left", "all done"), the tally on hover (feature plan v6 U5).
            var today = row.OfferedToday is { } offered
                ? Core.Ui.LeftText.LeftOrDone(row.DoneToday, offered)
                : string.Format(CultureInfo.CurrentCulture, Strings.PlanningBoardTodayUnknownFormat, row.DoneToday);
            var zone = row.Giver?.Issuer is { } issuer && links?.Map(issuer.MapId) is { } map ? map.PlaceName : string.Empty;
            var todayTooltip = row.OfferedToday is { } shown ? Core.Ui.LeftText.Tally(row.DoneToday, shown) : Strings.PlanningBoardTodayUnknownTooltip;
            lines[i] = new BoardLine(
                row,
                society,
                rank,
                row.RankedUpToday ? Strings.PlanningBoardRankedUpToday : string.Empty,
                today,
                todayTooltip,
                zone)
            {
                // What the row says first (spec-1.19 C5): a daily carried over the reset, the rank-up hint, or today.
                Line = row.Carried is { } carried
                    ? string.Format(CultureInfo.CurrentCulture, Strings.AlliedCarriedFormat, session.Spoilers.DisplayName(carried))
                    : row.RankUpBonus ? Strings.AlliedRankUpReady : today,
                NeedsYou = row.Carried is not null,
                LineTooltip = row.Carried is not null ? Strings.AlliedCarriedTooltip
                    : row.RankUpBonus ? Strings.AlliedRankUpReadyTooltip : todayTooltip,
                AltNote = row.Carried is not null && !session.IsLive ? string.Format(CultureInfo.CurrentCulture, Strings.AlliedStoredAltFormat, snapshot.Name) : string.Empty,
            };
        }

        boardLines = lines;
    }

    /// <summary>The same societies with the same numbers: the lines stay, only the countdown moved.</summary>
    private static bool SameRows(AlliedSocietyBoardModel a, AlliedSocietyBoardModel b)
    {
        if (a.Rows.Count != b.Rows.Count || a.Rows.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < a.Rows.Count; i++)
        {
            if (a.Rows[i] != b.Rows[i])
            {
                return false;
            }
        }

        return true;
    }
}
