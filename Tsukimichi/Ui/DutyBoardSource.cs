using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Unique;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The Duties board's lines for the viewed character (feature plan v7 N4; <see cref="DutyBoard"/>): the open roulettes
/// on one line, each closed one with its reason and the duties still to unlock (each with the quest that unlocks it
/// and that quest's state), and the duties unlocked but never cleared, per category. Built when the session version,
/// the catalog, the language, the viewed character, the duty index or the unlock data changes; drawing allocates
/// nothing. Quest names go through the spoiler shield, and a duty whose every unlock quest the shield hides is named
/// "A duty further along the story". Framework thread only.
/// </summary>
public sealed class DutyBoardSource
{
    private static readonly IReadOnlyList<LockedLine> NoLocked = [];
    private static readonly IReadOnlyList<NeverLine> NoNever = [];

    private readonly SessionState session;
    private readonly Func<DutyRunIndex?> runs;
    private readonly DutyUnlockIndexSource unlocks;

    private (int Version, CatalogBundle? Bundle, int Language, ulong? Viewed, DutyRunIndex? Runs, DutyUnlockIndex? Unlocks, int Spoilers) builtKey =
        (-1, null, -1, null, null, null, 0);

    private bool visible;
    private bool hasRecords;
    private string openLine = string.Empty;
    private IReadOnlyList<LockedLine> locked = NoLocked;
    private IReadOnlyList<NeverLine> never = NoNever;

    public DutyBoardSource(SessionState session, Func<DutyRunIndex?> runs, DutyUnlockIndexSource unlocks)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.runs = runs ?? throw new ArgumentNullException(nameof(runs));
        this.unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));
    }

    /// <summary>A duty still to unlock: its name (or the shield's stand-in), "Lv 50", the line naming its unlock quest, that quest, its icon.</summary>
    public sealed record MissingLine(string Duty, string Level, string From, QuestRecord? Quest, uint Icon);

    /// <summary>A closed roulette: its short name, why it is closed, and the duties still to unlock.</summary>
    public sealed record LockedLine(string Name, string Reason, IReadOnlyList<MissingLine> Missing);

    /// <summary>One category of the never-cleared list: its name and the duties, comma-joined.</summary>
    public sealed record NeverLine(string Kind, string Duties);

    /// <summary>Whether the board has anything to show: the duty index holds roulettes and a character is viewed.</summary>
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

    /// <summary>"Open: Leveling · Trials · …"; empty when none is.</summary>
    public string OpenLine
    {
        get
        {
            Refresh();
            return openLine;
        }
    }

    /// <summary>The closed roulettes, in the Duty Finder's order.</summary>
    public IReadOnlyList<LockedLine> Locked
    {
        get
        {
            Refresh();
            return locked;
        }
    }

    /// <summary>The duties unlocked and never cleared, per category; empty when every unlocked duty is cleared.</summary>
    public IReadOnlyList<NeverLine> Never
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
        openLine = string.Empty;
        locked = NoLocked;
        never = NoNever;
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
        var model = DutyBoard.Build(index, snapshot, condition => UnlockQuests(unlockIndex, condition, catalog, states));
        var open = model.Roulettes.Where(static r => r.Lock == RouletteLock.Open).Select(static r => r.Roulette.ShortName).ToArray();
        openLine = open.Length == 0 ? string.Empty : string.Format(CultureInfo.CurrentCulture, Strings.DutyBoardOpenFormat, string.Join(Core.Ui.MsqText.Separator, open));

        var lockedLines = new List<LockedLine>();
        foreach (var line in model.Locked)
        {
            var reason = line.Lock switch
            {
                RouletteLock.NeedsExpansion => Strings.DutyBoardNeedsExpansion,
                RouletteLock.NeedsLevel => string.Format(CultureInfo.CurrentCulture, Strings.DutyBoardNeedsLevelFormat, line.Roulette.RequiredLevel),
                _ when line.NeedsEvery => string.Format(CultureInfo.CurrentCulture, Strings.DutyBoardNeedsEveryFormat, line.Left),
                _ => string.Format(CultureInfo.CurrentCulture, Strings.DutyBoardNeedsSomeFormat, line.Needed, line.Left),
            };
            lockedLines.Add(new LockedLine(line.Roulette.ShortName, reason, line.Missing.Select(Missing).ToArray()));
        }

        locked = lockedLines;
        never = model.NeverCleared.Select(static g => new NeverLine(KindName(g.ContentType), string.Join(Strings.PlanningListSeparator, g.Duties.Select(static d => d.Name)))).ToArray();
    }

    /// <summary>
    /// The quests that unlock a duty, those still to do first (Ready, then accepted, then the rest), completed ones last:
    /// a duty some quest of which is done is not missing for that reason.
    /// </summary>
    private static IReadOnlyList<QuestRecord> UnlockQuests(DutyUnlockIndex index, uint condition, QuestCatalog catalog, IReadOnlyDictionary<uint, Core.Evaluation.QuestEvaluation> states)
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

    private MissingLine Missing(BoardDuty duty)
    {
        var info = duty.Duty;
        var level = info.LevelRequired == 0 ? string.Empty : string.Format(CultureInfo.CurrentCulture, Strings.RouteLevelFormat, info.LevelRequired);
        var quest = duty.UnlockQuests.Count > 0 ? duty.UnlockQuests[0] : null;
        var hidden = duty.UnlockQuests.Count > 0 && duty.UnlockQuests.All(session.Spoilers.IsMasked);
        var name = hidden ? string.Format(CultureInfo.CurrentCulture, Strings.DutyBoardHiddenDutyFormat, info.LevelRequired) : info.Name;
        var from = quest is null
            ? Strings.DutyBoardNoQuest
            : string.Format(CultureInfo.CurrentCulture, Strings.DutyBoardFromFormat, session.Spoilers.DisplayName(quest), Strings.StateName(session.States.GetValueOrDefault(quest.RowId)?.State ?? QuestState.Unknown, quest));
        return new MissingLine(name, level, from, quest, hidden ? 0 : info.Icon);
    }

    private static string KindName(uint contentType) => contentType switch
    {
        DutyRunInfo.Dungeons => Strings.DutyBoardKindDungeons,
        DutyRunInfo.Trials => Strings.DutyBoardKindTrials,
        DutyRunInfo.Guildhests => Strings.DutyBoardKindGuildhests,
        _ => Strings.DutyBoardKindRaids,
    };
}
