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
/// (<see cref="LinkedFolderService"/>, read only, every 5 seconds). A save is loaded again only when its capture time
/// moved, and its whole catalog is resolved on a worker (15 to 40 ms a character) once per save, catalog, reset cycle
/// and server festivals; the rows show what they had meanwhile. Rows, goals and their strings are rebuilt when an input
/// changes, never per frame. Framework thread only.
/// </para>
/// </summary>
public sealed class RosterSource
{
    private readonly SessionState session;
    private readonly CharacterRoster roster;
    private readonly Func<ulong, CharacterSnapshot?> loadSnapshot;
    private readonly LinkedFolderService? linked;
    private readonly IPluginLog log;

    // Stored saves loaded here, by capture time (null when unreadable).
    private readonly Dictionary<ulong, (DateTime Taken, CharacterSnapshot? Snapshot)> loaded = [];

    // Resolved states and facts per character, by the save instance, catalog, festivals and cycle they were made with.
    private readonly Dictionary<ulong, Resolved> resolved = [];
    private readonly Dictionary<ulong, (ResolveKey Key, Task<Facts> Task)> resolving = [];

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
            Localization.Loc.Version);
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

    /// <summary>Gives a member its save, states and facts: the session's, or a resolved one (scheduling a resolve when needed).</summary>
    private void Attach(Member member, CatalogBundle bundle, IReadOnlyList<UniqueRewardEntry>? rewards)
    {
        var id = member.ContentId;
        if (id == session.ViewedContentId && session.ViewedSnapshot is { } viewedSnapshot && session.States.Count > 0)
        {
            member.Snapshot = viewedSnapshot;
            member.States = session.States;
            member.Facts = FactsFor(bundle.Catalog, session.States, viewedSnapshot, session.Context, rewards, DateTime.UtcNow);
            return;
        }

        if (id == session.LiveContentId && session.LiveSnapshot is { } liveSnapshot && session.LiveStates.Count > 0)
        {
            member.Snapshot = liveSnapshot;
            member.States = session.LiveStates;
            member.Facts = FactsFor(bundle.Catalog, session.LiveStates, liveSnapshot, session.Context, rewards, DateTime.UtcNow);
            return;
        }

        var snapshot = member.Linked ?? Load(id, member.TakenUtc);
        member.Snapshot = snapshot;
        if (snapshot is null)
        {
            return;
        }

        var live = session.LiveSnapshot is { } logged ? ServerFestivals.Of(logged) : null;
        var key = new ResolveKey(snapshot, bundle, live?.Ids.Count ?? -1, live is null ? 0 : string.Join(',', live.Ids).GetHashCode(StringComparison.Ordinal), rewards);
        if (resolved.TryGetValue(id, out var done))
        {
            // The last result stands in while a newer save (another client's) resolves.
            member.States = done.Facts.States;
            member.Facts = done.Facts;
            if (done.Key == key)
            {
                return;
            }
        }

        if (resolving.TryGetValue(id, out var running) && running.Key == key)
        {
            return;
        }

        var context = session.ContextForStored(snapshot);
        var catalog = bundle.Catalog;
        resolving[id] = (key, Task.Run(() =>
        {
            var states = (IReadOnlyDictionary<uint, QuestEvaluation>)StateResolver.ResolveAll(catalog, snapshot, context);
            return FactsFor(catalog, states, snapshot, context, rewards, DateTime.UtcNow);
        }));
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
                resolved[id] = new Resolved(running.Key, running.Task.Result);
            }
            else
            {
                log.Warning(running.Task.Exception?.GetBaseException(), "Character {ContentId} could not be evaluated for the roster", id);
            }
        }

        landedCount++;
    }

    private CharacterSnapshot? Load(ulong contentId, DateTime taken)
    {
        if (loaded.TryGetValue(contentId, out var cached) && cached.Taken == taken)
        {
            return cached.Snapshot;
        }

        CharacterSnapshot? snapshot = null;
        try
        {
            snapshot = loadSnapshot(contentId);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not load the snapshot of character {ContentId} for the roster", contentId);
        }

        loaded[contentId] = (taken, snapshot);
        return snapshot;
    }

    /// <summary>The roster's facts of one character: its story, Ready count, today's allowances, Moonlit left and level gate. Pure; runs on a worker for a stored save.</summary>
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
        IReadOnlyDictionary<uint, QuestEvaluation> States,
        RosterStory? Story,
        int Ready,
        int? Allowances,
        int LeveAllowances,
        int? MoonlitLeft,
        MsqLevelGate? LevelGate);

    private sealed record Resolved(ResolveKey Key, Facts Facts);

    private sealed record ResolveKey(CharacterSnapshot Snapshot, CatalogBundle Bundle, int FestivalCount, int FestivalHash, IReadOnlyList<UniqueRewardEntry>? Rewards);

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
        int Language);

    private sealed record GoalKey(
        AltGoal Goal,
        IReadOnlyDictionary<uint, QuestEvaluation>? States,
        CharacterSnapshot? Snapshot,
        IReadOnlyDictionary<uint, QuestEvaluation>? Other,
        CatalogBundle? Bundle,
        DutyRunIndex? DutyRuns,
        FlightIndex? Flight);
}
