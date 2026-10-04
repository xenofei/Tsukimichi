using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Characters;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Multibox;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Unique;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// Every character's standing for the All characters roster, alt goals and Up next (plan v7, 1.21.0 P3, N11, P1): the
/// rows (<see cref="RosterBoard"/>), each character's quest states and its goal's progress. One source, so the roster,
/// the goal card, the "your other characters" line under the detail hero, Up next and <c>/tsuki next</c> always agree.
/// <para>
/// <b>What it reads.</b> The character on view and the one logged in here come from the session as they are. The
/// others come from their last save: this config folder's (<c>characters\&lt;id&gt;.json</c>, kept current by the multibox
/// watcher, D11, within a few seconds of another client's save) and the linked launcher folders'
/// (<see cref="LinkedFolderService"/>, read only, every 5 seconds). A save is read (a file read and a JSON parse) and its
/// whole catalog resolved (15 to 40 ms a character) on a worker, once per capture time, catalog, reset cycle and server
/// festivals; the rows show what they had meanwhile, and a row not read yet keeps its height and says "being read". The
/// facts of the character on view and the one logged in (its story, Ready count, allowances, Moonlit left and level
/// gate) are worked out on a worker too, once per state map, never on the frame. Rows, goals and their strings are
/// rebuilt when an input changes, never per frame. Framework thread only.
/// </para>
/// </summary>
public sealed class RosterSource
{
    private readonly SessionState session;
    private readonly CharacterRoster roster;
    private readonly Func<ulong, CharacterSnapshot?> loadSnapshot;
    private readonly LinkedFolderService? linked;
    private readonly IPluginLog log;

    // Read and resolved saves per character (Facts null: unreadable), by the capture time, save, states, catalog,
    // festivals and rewards they were made with; a reset cycle clears them.
    private readonly Dictionary<ulong, Resolved> resolved = [];
    private readonly Dictionary<ulong, (ResolveKey Key, Task<Facts?> Task)> resolving = [];

    // Goals' progress, by the inputs they were evaluated with.
    private readonly Dictionary<ulong, (GoalKey Key, AltGoalProgress? Progress)> goals = [];
    private (ulong Id, AltGoal Goal, GoalKey Key, AltGoalProgress? Progress)? preview;

    private BuildKey builtKey;
    private int landedCount;
    private IReadOnlyList<RosterRow> rows = [];
    private readonly Dictionary<ulong, Member> members = [];
    private (DateTime Daily, DateTime Weekly) cycle;

    // Flying zones, once per index.
    private FlightIndex? zonesFrom;
    private IReadOnlyList<AltGoalZone> zones = [];
    private IReadOnlyList<(byte Expansion, string Name)> flyingExpansions = [];

    public RosterSource(SessionState session, CharacterRoster roster, Func<ulong, CharacterSnapshot?> loadSnapshot, LinkedFolderService? linked, IPluginLog log)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.roster = roster ?? throw new ArgumentNullException(nameof(roster));
        this.loadSnapshot = loadSnapshot ?? throw new ArgumentNullException(nameof(loadSnapshot));
        this.linked = linked;
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>The merged unique-reward catalog (Moonlit's), for the Moonlit column; null leaves the column unknown.</summary>
    public Func<UniqueRewardCatalog>? Rewards { get; set; }

    /// <summary>The duty index (the Duties board's), for the roulettes goal; null while it builds.</summary>
    public Func<DutyRunIndex?>? DutyRuns { get; set; }

    /// <summary>Which quests unlock which duty, for the roulettes goal's quests.</summary>
    public DutyUnlockIndexSource? DutyUnlocks { get; set; }

    /// <summary>The flying zones (Flight's index), for the flying goal; null while it builds.</summary>
    public Func<FlightIndex?>? Flight { get; set; }

    /// <summary>Bumps whenever the rows, a character's states or a goal's progress read differently.</summary>
    public int Revision { get; private set; }

    /// <summary>The settings every goal, star, role and nickname lives in (<c>user\characters.json</c>).</summary>
    public Core.Storage.CharacterSettingsBook Settings => roster.Settings;

    /// <summary>Every character as the roster shows it, in no particular order (the pane sorts).</summary>
    public IReadOnlyList<RosterRow> Rows
    {
        get
        {
            Refresh();
            return rows;
        }
    }

    /// <summary>The characters the lists show and the linked folders' ones, by content id, with their row (null before the first build).</summary>
    public RosterRow? RowOf(ulong contentId)
    {
        Refresh();
        foreach (var row in rows)
        {
            if (row.ContentId == contentId)
            {
                return row;
            }
        }

        return null;
    }

    /// <summary>A character's quest states: the session's for the one on view and the one logged in, else its last save resolved; null while being read or unreadable.</summary>
    public IReadOnlyDictionary<uint, QuestEvaluation>? StatesOf(ulong contentId)
    {
        Refresh();
        return members.TryGetValue(contentId, out var member) ? member.States : null;
    }

    /// <summary>A character's goal and what is left of it; null without a goal (or before its states are read).</summary>
    public AltGoalProgress? GoalProgressOf(ulong contentId)
    {
        Refresh();
        return RowOf(contentId)?.GoalProgress;
    }

    /// <summary>The next main scenario quest's level gate for a character; null when the story waits for no level.</summary>
    public MsqLevelGate? LevelGateOf(ulong contentId)
    {
        Refresh();
        return members.TryGetValue(contentId, out var member) ? member.Facts?.LevelGate : null;
    }

    /// <summary>
    /// What <paramref name="goal"/> would leave <paramref name="contentId"/> (the Set a goal popover's preview line),
    /// evaluated once per goal and inputs; null while the character's states are being read.
    /// </summary>
    public AltGoalProgress? Preview(ulong contentId, AltGoal goal)
    {
        ArgumentNullException.ThrowIfNull(goal);
        Refresh();
        if (!members.TryGetValue(contentId, out var member) || member.States is not { } states)
        {
            return null;
        }

        var key = KeyFor(member, goal);
        if (preview is { } last && last.Id == contentId && last.Goal == goal && last.Key == key)
        {
            return last.Progress;
        }

        var progress = Evaluate(member, goal, states);
        preview = (contentId, goal, key, progress);
        return progress;
    }

    /// <summary>The patches the story goal can aim at, oldest first ("7.0", "Dawntrail").</summary>
    public IReadOnlyList<(string Patch, string Part)> StoryPatches =>
        session.Bundle is { } bundle ? AltGoals.StoryPatches(bundle.Catalog) : [];

    /// <summary>The expansions with flying zones, in order, with their names.</summary>
    public IReadOnlyList<(byte Expansion, string Name)> FlyingExpansions
    {
        get
        {
            RefreshZones();
            return flyingExpansions;
        }
    }

    /// <summary>The characters a goal can match (every roster character but <paramref name="contentId"/>), as rows.</summary>
    public IEnumerable<RosterRow> Others(ulong contentId) => Rows.Where(r => r.ContentId != contentId);

    /// <summary>
    /// Whether no list knows the character any more (forgotten here, and in no linked folder): a goal matching it can
    /// never be read again. A hidden character is still known.
    /// </summary>
    public bool IsForgotten(ulong contentId)
    {
        foreach (var entry in roster.All)
        {
            if (entry.ContentId == contentId)
            {
                return false;
            }
        }

        if (linked is not null)
        {
            foreach (var other in linked.Characters)
            {
                if (other.ContentId == contentId)
                {
                    return false;
                }
            }
        }

        return true;
    }

    // ------------------------------------------------------------------ refresh

    private void Refresh()
    {
        TakeLanded();
        var now = DateTime.UtcNow;
        var nowCycle = GameResets.Cycle(now);
        if (nowCycle != cycle)
        {
            // A reset opens what the stored characters did before it: every save is resolved again.
            cycle = nowCycle;
            resolved.Clear();
            resolving.Clear();
        }

        RefreshZones();
        var key = new BuildKey(
            session.RosterVersion,
            roster.Version,
            roster.Settings.Version,
            linked?.Revision ?? 0,
            session.Bundle,
            landedCount,
            DutyRuns?.Invoke(),
            zonesFrom,
            Rewards?.Invoke(),
            Localization.Loc.Version,
            cycle);
        if (key == builtKey)
        {
            return;
        }

        builtKey = key;
        Build(now);
        Revision++;
    }

    private void Build(DateTime now)
    {
        members.Clear();
        if (session.Bundle is not { } bundle)
        {
            rows = [];
            return;
        }

        var entries = new List<Member>();
        var viewed = session.ViewedContentId;
        foreach (var entry in roster.All)
        {
            if (entry.Hidden && !entry.LiveHere && entry.ContentId != viewed)
            {
                continue;
            }

            entries.Add(new Member(entry.ContentId, entry.Name, entry.WorldName, RosterBoard.PlaceOf(entry.LiveHere, entry.LiveElsewhere, false), entry.Live, entry.TakenUtc, null));
        }

        if (linked is not null)
        {
            foreach (var other in linked.Characters)
            {
                entries.Add(new Member(other.ContentId, other.Snapshot.Name, WorldOf(other.Snapshot.World), RosterPlace.OtherFolder, other.Live, other.Snapshot.TakenUtc, other.Folder) { Linked = other.Snapshot });
            }
        }

        var rewards = Rewards?.Invoke().All;
        foreach (var member in entries)
        {
            Attach(member, bundle, rewards);
            members[member.ContentId] = member;
        }

        var built = new List<RosterRow>(entries.Count);
        foreach (var member in entries)
        {
            built.Add(RowFor(member, bundle));
        }

        rows = built;
    }

    /// <summary>
    /// Gives a member its save, states and facts: the session's states with their facts, or a stored save read and
    /// resolved; whatever is not worked out yet is scheduled on a worker (<see cref="Schedule"/>), and the last result
    /// stands in meanwhile. Nothing here reads a file or walks the catalog.
    /// </summary>
    private void Attach(Member member, CatalogBundle bundle, IReadOnlyList<UniqueRewardEntry>? rewards)
    {
        var id = member.ContentId;
        var catalog = bundle.Catalog;
        if (SessionStates(id) is { } own)
        {
            // The character on view or the one logged in: the session's states as they are; their facts (a catalog pass,
            // the level gate, the allied board, Moonlit left) once per state map, on a worker.
            var (snapshot, states, context) = own;
            member.Snapshot = snapshot;
            member.States = states;
            var ownKey = new ResolveKey(snapshot.TakenUtc, snapshot, states, bundle, 0, 0, rewards);
            Schedule(member, ownKey, () => FactsFor(catalog, states, snapshot, context, rewards, DateTime.UtcNow));
            return;
        }

        // Another character: its last save, read and resolved on a worker once per capture time (keyed on TakenUtc, so
        // another client's newer save is read again), catalog, festivals and rewards.
        var live = session.LiveSnapshot is { } logged ? ServerFestivals.Of(logged) : null;
        var linkedSave = member.Linked;
        member.Snapshot = linkedSave;
        var key = new ResolveKey(member.TakenUtc, linkedSave, null, bundle, live?.Ids.Count ?? -1, live is null ? 0 : string.Join(',', live.Ids).GetHashCode(StringComparison.Ordinal), rewards);
        var load = loadSnapshot;
        var contextFor = session.StoredContextFactory();
        var warn = log;
        Schedule(member, key, () =>
        {
            CharacterSnapshot? snapshot = linkedSave;
            if (snapshot is null)
            {
                try
                {
                    snapshot = load(id);
                }
                catch (Exception ex)
                {
                    warn.Warning(ex, "Could not load the snapshot of character {ContentId} for the roster", id);
                }
            }

            if (snapshot is null)
            {
                return null;
            }

            var context = contextFor(snapshot);
            var states = (IReadOnlyDictionary<uint, QuestEvaluation>)StateResolver.ResolveAll(catalog, snapshot, context);
            return FactsFor(catalog, states, snapshot, context, rewards, DateTime.UtcNow);
        });
    }

    /// <summary>The session's own save, states and context for the character on view or the one logged in; null for another.</summary>
    private (CharacterSnapshot Snapshot, IReadOnlyDictionary<uint, QuestEvaluation> States, EvalContext Context)? SessionStates(ulong id)
    {
        if (id == session.ViewedContentId && session.ViewedSnapshot is { } viewed && session.States.Count > 0)
        {
            return (viewed, session.States, session.Context);
        }

        if (id == session.LiveContentId && session.LiveSnapshot is { } live && session.LiveStates.Count > 0)
        {
            return (live, session.LiveStates, session.Context);
        }

        return null;
    }

    /// <summary>
    /// The member's facts for <paramref name="key"/>: the resolved ones when they match; else the last ones stand in
    /// (states included, unless the member has its own) while <paramref name="work"/> runs on a worker, once per key.
    /// </summary>
    private void Schedule(Member member, ResolveKey key, Func<Facts?> work)
    {
        var id = member.ContentId;
        if (resolved.TryGetValue(id, out var done))
        {
            if (done.Facts is { } facts)
            {
                member.Snapshot ??= facts.Snapshot;
                member.States ??= facts.States;
                member.Facts = facts;
            }

            if (done.Key == key)
            {
                return;
            }
        }

        if (resolving.TryGetValue(id, out var running) && running.Key == key)
        {
            return;
        }

        resolving[id] = (key, Task.Run(work));
    }

    /// <summary>Takes the resolves that finished; each one landing rebuilds the rows.</summary>
    private void TakeLanded()
    {
        if (resolving.Count == 0)
        {
            return;
        }

        List<ulong>? done = null;
        foreach (var (id, running) in resolving)
        {
            if (running.Task.IsCompleted)
            {
                (done ??= []).Add(id);
            }
        }

        if (done is null)
        {
            return;
        }

        foreach (var id in done)
        {
            var running = resolving[id];
            resolving.Remove(id);
            if (running.Task.IsCompletedSuccessfully)
            {
                // An unreadable save lands as null facts: kept, so it is not read again until its capture time moves.
                resolved[id] = new Resolved(running.Key, running.Task.Result);
            }
            else
            {
                log.Warning(running.Task.Exception?.GetBaseException(), "Character {ContentId} could not be evaluated for the roster", id);
            }
        }

        landedCount++;
    }

    /// <summary>The roster's facts of one character: its save, story, Ready count, today's allowances, Moonlit left and level gate. Pure; runs on a worker.</summary>
    private static Facts FactsFor(
        QuestCatalog catalog,
        IReadOnlyDictionary<uint, QuestEvaluation> states,
        CharacterSnapshot snapshot,
        EvalContext context,
        IReadOnlyList<UniqueRewardEntry>? rewards,
        DateTime nowUtc)
    {
        var gate = LevelAdvisor.MsqGate(catalog, states, snapshot, context);
        int? allowances = null;
        if (snapshot.Tribes.Count > 0)
        {
            allowances = AlliedSocietyBoard.Build(catalog, snapshot, null, null, nowUtc).AllowancesLeft;
        }

        return new Facts(
            snapshot,
            states,
            RosterBoard.StoryOf(catalog, states, gate?.Level ?? 0),
            RosterBoard.ReadyCount(catalog, states),
            allowances,
            snapshot.LeveAllowance,
            rewards is null ? null : RosterBoard.MoonlitLeft(rewards, CollectibleLookup.For(snapshot)),
            gate);
    }

    private RosterRow RowFor(Member member, CatalogBundle bundle)
    {
        var book = roster.Settings;
        var goal = book.Goal(member.ContentId);
        AltGoalProgress? progress = null;
        if (goal is not null && member.States is { } states)
        {
            progress = GoalFor(member, goal, states);
        }

        var snapshot = member.Snapshot;
        var job = snapshot?.CurrentJob ?? 0;
        var level = snapshot is not null && snapshot.JobLevels.TryGetValue(job, out var jobLevel) ? jobLevel : 0;
        return new RosterRow(
            member.ContentId,
            member.Name,
            member.World,
            member.Place,
            member.Live,
            member.TakenUtc,
            job,
            level,
            member.Facts?.Story,
            member.Facts?.Ready,
            member.Facts?.Allowances,
            member.Facts?.LeveAllowances,
            member.Facts?.MoonlitLeft)
        {
            Folder = member.Folder,
            Starred = book.IsStarred(member.ContentId),
            Role = book.Role(member.ContentId),
            Nickname = book.Nickname(member.ContentId),
            Goal = goal,
            GoalProgress = progress,
        };
    }

    /// <summary>A goal's progress for a member, memoized by the inputs it reads.</summary>
    private AltGoalProgress? GoalFor(Member member, AltGoal goal, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        var key = KeyFor(member, goal);
        if (goals.TryGetValue(member.ContentId, out var memo) && memo.Key == key)
        {
            return memo.Progress;
        }

        var progress = Evaluate(member, goal, states);
        goals[member.ContentId] = (key, progress);
        return progress;
    }

    private GoalKey KeyFor(Member member, AltGoal goal)
    {
        var other = goal is { Kind: AltGoalKind.MatchCharacter, Other: { } otherId } && members.TryGetValue(otherId, out var match) ? match.States : null;
        return new GoalKey(goal, member.States, member.Snapshot, other, session.Bundle, DutyRuns?.Invoke(), zonesFrom);
    }

    private AltGoalProgress? Evaluate(Member member, AltGoal goal, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        if (session.Bundle is not { } bundle)
        {
            return null;
        }

        var catalog = bundle.Catalog;
        try
        {
            switch (goal.Kind)
            {
                case AltGoalKind.Story when goal.Patch is { } patch:
                    return AltGoals.Story(catalog, states, patch);
                case AltGoalKind.MatchCharacter when goal.Other is { } otherId:
                    var other = members.TryGetValue(otherId, out var match) ? match.States : null;
                    return AltGoals.Match(catalog, states, other, session.FeatureQuestIds);
                case AltGoalKind.Flying when goal.Expansion is { } expansion:
                    return zonesFrom is null ? AltGoalProgress.Unknown(AltGoalKind.Flying) : AltGoals.Flying(catalog, states, zones, expansion);
                case AltGoalKind.Roulettes:
                    if (DutyRuns?.Invoke() is not { Roulettes.Count: > 0 } index || member.Snapshot is not { } snapshot)
                    {
                        return AltGoalProgress.Unknown(AltGoalKind.Roulettes);
                    }

                    var unlocks = DutyUnlocks?.Current;
                    var board = DutyBoard.Build(index, snapshot, condition => unlocks is null ? [] : DutyBoardSource.UnlockQuests(unlocks, condition, catalog, states), bundle.DutyJobs());
                    return AltGoals.Roulettes(states, board);
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "The goal of character {ContentId} could not be evaluated", member.ContentId);
        }

        return null;
    }

    private void RefreshZones()
    {
        var index = Flight?.Invoke();
        if (ReferenceEquals(index, zonesFrom))
        {
            return;
        }

        zonesFrom = index;
        if (index is null)
        {
            zones = [];
            flyingExpansions = [];
            return;
        }

        zones = index.Zones.Select(static z => new AltGoalZone(z.TerritoryId, z.Name, z.Expansion, z.QuestCurrents.Select(static c => c.QuestRowId).ToArray())).ToArray();
        var names = session.Bundle?.Names;
        flyingExpansions = index.Zones
            .Select(static z => z.Expansion)
            .Distinct()
            .Order()
            .Select(e => (e, names?.Expansion(e) is { Length: > 0 } named ? named : Expansions.Name(e)))
            .ToArray();
    }

    private string WorldOf(uint world)
    {
        foreach (var entry in roster.All)
        {
            if (entry.World == world)
            {
                return entry.WorldName;
            }
        }

        return world.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>One character while the rows are built.</summary>
    private sealed class Member(ulong contentId, string name, string world, RosterPlace place, bool live, DateTime takenUtc, string? folder)
    {
        public ulong ContentId { get; } = contentId;
        public string Name { get; } = name;
        public string World { get; } = world;
        public RosterPlace Place { get; } = place;
        public bool Live { get; } = live;
        public DateTime TakenUtc { get; } = takenUtc;
        public string? Folder { get; } = folder;
        public CharacterSnapshot? Linked { get; init; }
        public CharacterSnapshot? Snapshot { get; set; }
        public IReadOnlyDictionary<uint, QuestEvaluation>? States { get; set; }
        public Facts? Facts { get; set; }
    }

    private sealed record Facts(
        CharacterSnapshot Snapshot,
        IReadOnlyDictionary<uint, QuestEvaluation> States,
        RosterStory? Story,
        int Ready,
        int? Allowances,
        int LeveAllowances,
        int? MoonlitLeft,
        MsqLevelGate? LevelGate);

    private sealed record Resolved(ResolveKey Key, Facts? Facts);

    /// <summary>
    /// What a member's facts were made from: its capture time, the save itself when it was handed in (a linked folder's,
    /// or the session's), the session's states for the character on view or logged in, the catalog, the festivals a
    /// stored save was resolved with, and the rewards.
    /// </summary>
    private sealed record ResolveKey(
        DateTime Taken,
        CharacterSnapshot? Snapshot,
        IReadOnlyDictionary<uint, QuestEvaluation>? States,
        CatalogBundle Bundle,
        int FestivalCount,
        int FestivalHash,
        IReadOnlyList<UniqueRewardEntry>? Rewards);

    private readonly record struct BuildKey(
        int RosterVersion,
        int Roster,
        int Settings,
        int Linked,
        CatalogBundle? Bundle,
        int Landed,
        DutyRunIndex? DutyRuns,
        FlightIndex? Flight,
        UniqueRewardCatalog? Rewards,
        int Language,
        (DateTime Daily, DateTime Weekly) Cycle);

    private sealed record GoalKey(
        AltGoal Goal,
        IReadOnlyDictionary<uint, QuestEvaluation>? States,
        CharacterSnapshot? Snapshot,
        IReadOnlyDictionary<uint, QuestEvaluation>? Other,
        CatalogBundle? Bundle,
        DutyRunIndex? DutyRuns,
        FlightIndex? Flight);
}
