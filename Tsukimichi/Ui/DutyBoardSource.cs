using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Route;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Unique;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The Duties card's lines for the viewed character (feature plan v7 N4; spec-1.19 N4; <see cref="DutyBoard"/>): the
/// caption ("1 roulette locked"), one block per roulette with something left (closed, or open with duties in it not
/// unlocked) with its state ("locked · needs a Lv 100 job · best is BLM 98", "open · 1 raid not unlocked"), a row per
/// duty not unlocked (its name, its C7 size badge, "not unlocked · with" the quest that unlocks it), and the route and
/// pins over those quests; then the duties unlocked but never cleared. Lists show what is left, never a tally of what
/// is done. Built when the session version, the catalog, the language, the viewed character, the duty index, the
/// unlock data or the spoiler shield changes; drawing allocates nothing. Quest names go through the spoiler shield, and
/// a duty whose every unlock quest the shield hides is named "A duty further along the story". Framework thread only.
/// </summary>
public sealed class DutyBoardSource
{
    private static readonly IReadOnlyList<Block> NoBlocks = [];
    private static readonly IReadOnlyList<Row> NoRows = [];

    private readonly SessionState session;
    private readonly Func<DutyRunIndex?> runs;
    private readonly DutyUnlockIndexSource unlocks;

    private (int Version, CatalogBundle? Bundle, int Language, ulong? Viewed, DutyRunIndex? Runs, DutyUnlockIndex? Unlocks, int Spoilers) builtKey =
        (-1, null, -1, null, null, null, 0);

    private bool visible;
    private bool hasRecords;
    private string caption = string.Empty;
    private IReadOnlyList<Block> blocks = NoBlocks;
    private IReadOnlyList<Row> never = NoRows;

    public DutyBoardSource(SessionState session, Func<DutyRunIndex?> runs, DutyUnlockIndexSource unlocks)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.runs = runs ?? throw new ArgumentNullException(nameof(runs));
        this.unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));
    }

    /// <summary>
    /// One duty row: its name (or the shield's stand-in), its size badge (none when unknown), the words before the unlock
    /// quest ("not unlocked · with "), that quest's name and the quest itself (null when none is known).
    /// </summary>
    public sealed record Row(string Duty, DutyBadges.Look? Badge, string Trailing, string QuestName, QuestRecord? Quest);

    /// <summary>
    /// One roulette: its id (for the card's "more" toggle), header ("Level Cap Dungeons roulette"), state, rows, the
    /// route over the unlock quests (null without one) and the quests "Pin all" pins.
    /// </summary>
    public sealed record Block(uint Id, string Header, string State, bool Locked, IReadOnlyList<Row> Rows, RouteTarget? Route, IReadOnlyList<QuestRecord> PinQuests);

    /// <summary>Whether the card has anything to show: the duty index holds roulettes and a character is viewed.</summary>
    public bool Visible
    {
        get
        {
            Refresh();
            return visible;
        }
    }

    /// <summary>Whether the viewed character's capture holds duty records; false shows "log in to read".</summary>
    public bool HasRecords
    {
        get
        {
            Refresh();
            return hasRecords;
        }
    }

    /// <summary>"1 roulette locked", "3 roulettes locked", or empty when every roulette is open.</summary>
    public string Caption
    {
        get
        {
            Refresh();
            return caption;
        }
    }

    /// <summary>The roulettes with something left, in the Duty Finder's order.</summary>
    public IReadOnlyList<Block> Blocks
    {
        get
        {
            Refresh();
            return blocks;
        }
    }

    /// <summary>The duties unlocked and never cleared: dungeons, trials, raids, guildhests, each lowest level first.</summary>
    public IReadOnlyList<Row> Never
    {
        get
        {
            Refresh();
            return never;
        }
    }

    private void Refresh()
    {
        var bundle = session.Bundle;
        var index = runs();
        var unlockIndex = unlocks.Current;
        var key = (session.Version, bundle, Localization.Loc.Version, session.ViewedContentId, index, unlockIndex, session.Spoilers.Fingerprint);
        if (key == builtKey)
        {
            return;
        }

        builtKey = key;
        visible = hasRecords = false;
        caption = string.Empty;
        blocks = NoBlocks;
        never = NoRows;
        if (bundle is null || index is not { Roulettes.Count: > 0 } || session.ViewedSnapshot is not { } snapshot)
        {
            return;
        }

        visible = true;
        hasRecords = snapshot.DutyRecords is not null;
        if (!hasRecords)
        {
            return;
        }

        var catalog = bundle.Catalog;
        var states = session.States;
        var model = DutyBoard.Build(index, snapshot, condition => UnlockQuests(unlockIndex, condition, catalog, states), bundle.DutyJobs());
        var locked = model.Locked.Count();
        caption = locked switch
        {
            0 => string.Empty,
            1 => Strings.DutyBoardLockedOne,
            _ => string.Format(CultureInfo.CurrentCulture, Strings.DutyBoardLockedFormat, locked),
        };

        var list = new List<Block>();
        foreach (var line in model.WithSomethingLeft)
        {
            var rows = line.Missing.Select(m => MissingRow(m, index)).ToArray();
            var withQuest = line.Missing.Where(static m => m.UnlockQuests.Count > 0).ToArray();
            var quests = withQuest.Select(static m => m.UnlockQuests[0]).DistinctBy(static q => q.RowId).ToArray();
            var route = quests.Length == 0
                ? null
                : RouteTarget.Union(RouteTargetKind.Duty, line.Roulette.ShortName, withQuest.Select(m => new RouteTarget(RouteTargetKind.Duty, DutyName(m), [m.UnlockQuests[0].RowId])));
            list.Add(new Block(
                line.Roulette.Id,
                string.Format(CultureInfo.CurrentCulture, Strings.DutyBoardRouletteFormat, line.Roulette.ShortName),
                State(line, snapshot, bundle),
                line.Lock != RouletteLock.Open,
                rows,
                route,
                quests));
        }

        blocks = list;
        never = model.NeverCleared.SelectMany(static g => g.Duties).Select(d => new Row(d.Name, SizeBadge(d, index), string.Empty, string.Empty, null)).ToArray();
    }

    /// <summary>"locked · needs a Lv 100 job · best is BLM 98", "locked · 2 dungeons not unlocked", "open · 1 raid not unlocked".</summary>
    internal static string State(RouletteLine line, CharacterSnapshot snapshot, CatalogBundle bundle)
    {
        switch (line.Lock)
        {
            case RouletteLock.NeedsExpansion:
                var expansion = bundle.Names.Expansion(line.Roulette.RequiredExpansion) is { Length: > 0 } named ? named : Core.Evaluation.Expansions.Name(line.Roulette.RequiredExpansion);
                return string.Format(CultureInfo.CurrentCulture, Strings.DutyBoardLockedExpansionFormat, expansion);
            case RouletteLock.NeedsLevel:
                // The best job that enters duties: a crafter's level never opens a roulette.
                var (job, level) = DutyBoard.CombatJob(snapshot, bundle.DutyJobs());
                return string.Format(CultureInfo.CurrentCulture, Strings.DutyBoardLockedLevelFormat, line.Roulette.RequiredLevel, bundle.Names.ClassJobAbbreviation(job), level);
            case RouletteLock.Unknown:
                return Strings.DutyBoardUnknownState;
            case RouletteLock.NeedsDuties when !line.NeedsEvery:
                return string.Format(CultureInfo.CurrentCulture, Strings.DutyBoardLockedSomeFormat, line.Left);
            case RouletteLock.NeedsDuties:
                return string.Format(CultureInfo.CurrentCulture, Strings.DutyBoardLockedEveryFormat, NotUnlocked(line.Missing));
            default:
                return string.Format(CultureInfo.CurrentCulture, Strings.DutyBoardOpenLeftFormat, NotUnlocked(line.Missing));
        }
    }

    /// <summary>"2 dungeons not unlocked", by the duties' one category, or "duties" when they mix.</summary>
    private static string NotUnlocked(IReadOnlyList<BoardDuty> missing)
    {
        var categories = missing.Select(static m => DutyBoard.CategoryOf(m.Duty)).Distinct().ToArray();
        var category = categories.Length == 1 ? categories[0] : 0u;
        var (one, many) = category switch
        {
            DutyRunInfo.Dungeons => (Strings.DutyBoardDungeonsOne, Strings.DutyBoardDungeonsFormat),
            DutyRunInfo.Trials => (Strings.DutyBoardTrialsOne, Strings.DutyBoardTrialsFormat),
            DutyRunInfo.Raids => (Strings.DutyBoardRaidsOne, Strings.DutyBoardRaidsFormat),
            DutyRunInfo.Guildhests => (Strings.DutyBoardGuildhestsOne, Strings.DutyBoardGuildhestsFormat),
            _ => (Strings.DutyBoardDutiesOne, Strings.DutyBoardDutiesFormat),
        };
        return missing.Count == 1 ? one : string.Format(CultureInfo.CurrentCulture, many, missing.Count);
    }

    private static DutyBadges.Look? SizeBadge(DutyRunInfo duty, DutyRunIndex index) =>
        DutyBadgeRules.Size(duty) is { } badge ? DutyBadges.Describe(badge, duty, index.Roulettes) : null;

    /// <summary>
    /// The quests that unlock a duty, those still to do first (Ready, then accepted, then the rest), completed ones last,
    /// leaving out another path's (another city's, Grand Company's).
    /// </summary>
    internal static IReadOnlyList<QuestRecord> UnlockQuests(DutyUnlockIndex index, uint condition, QuestCatalog catalog, IReadOnlyDictionary<uint, Core.Evaluation.QuestEvaluation> states)
    {
        var quests = index.Resolve(condition, catalog);
        if (quests.Count <= 1)
        {
            return quests;
        }

        return quests
            .Where(q => states.GetValueOrDefault(q.RowId) is not { LeavesTotals: true })
            .OrderBy(q => Rank(states.GetValueOrDefault(q.RowId)?.State ?? QuestState.Unknown))
            .ToArray();
    }

    private static int Rank(QuestState state) => state switch
    {
        QuestState.Ready => 0,
        QuestState.Accepted => 1,
        QuestState.ReadyOnOtherJob => 2,
        QuestState.Blocked => 3,
        QuestState.Completed => 5,
        _ => 4,
    };

    /// <summary>The duty's name, or the shield's stand-in when every quest that unlocks it is hidden.</summary>
    private string DutyName(BoardDuty duty) =>
        duty.UnlockQuests.Count > 0 && duty.UnlockQuests.All(session.Spoilers.IsMasked)
            ? string.Format(CultureInfo.CurrentCulture, Strings.DutyBoardHiddenDutyFormat, duty.Duty.LevelRequired)
            : duty.Duty.Name;

    private Row MissingRow(BoardDuty duty, DutyRunIndex index)
    {
        var info = duty.Duty;
        var quest = duty.UnlockQuests.Count > 0 ? duty.UnlockQuests[0] : null;
        var name = DutyName(duty);
        return quest is null
            ? new Row(name, SizeBadge(info, index), Strings.DutyBoardNoQuest, string.Empty, null)
            : new Row(name, SizeBadge(info, index), Strings.DutyBoardWith, session.Spoilers.DisplayName(quest), quest);
    }
}
