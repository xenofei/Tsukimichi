using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.GameData;
using AcceptedSinceFile = Tsukimichi.Core.Runtime.AcceptedSince;

namespace Tsukimichi.Game;

/// <summary>
/// Everything the UI reads: catalog, viewed character, its evaluations, shipped data and recent events.
/// Written by the poller and the plugin on the framework thread, read by the UI on that same thread, so version
/// counters plus <see cref="Changed"/> and <see cref="CharactersChanged"/> are all the synchronization needed. Every
/// mutation of what the viewed character shows bumps <see cref="Version"/>; a change of the stored character list
/// alone (a save, another game client's save of another character, who is live elsewhere) bumps only
/// <see cref="CharactersVersion"/>, so it rebuilds the lists and the comparisons rather than the query and every pane.
/// </summary>
public sealed partial class SessionState
{
    public const int MaxRecentEvents = 100;

    /// <summary>Pause before the one retry of a delete that hit the first-pass worker's read of the same file.</summary>
    private const int DeleteRetryDelayMs = 10;

    private static readonly IReadOnlyDictionary<uint, QuestEvaluation> NoStates = new Dictionary<uint, QuestEvaluation>();
    private static readonly IReadOnlyDictionary<ushort, DateTime> NoAcceptedSince = new Dictionary<ushort, DateTime>();
    private static readonly IReadOnlyDictionary<ushort, AbandonedEntry> NoAbandoned = new Dictionary<ushort, AbandonedEntry>();

    private readonly SnapshotService snapshots;
    private readonly PluginPaths paths;
    private readonly IPluginLog? log;
    private readonly RecentEventsTracker recentEvents = new(MaxRecentEvents);

    /// <summary>Raises <see cref="Changed"/>, <see cref="DataDeleted"/> and <see cref="CharacterForgotten"/> one listener at a time.</summary>
    private readonly ListenerIsolation listeners;

    private EvalContext baseContext = EvalContext.Default;
    private DateTime nextReset = DateTime.MinValue;
    private CharacterSnapshot? liveSnapshot;
    private IReadOnlyDictionary<uint, QuestEvaluation> liveStates = NoStates;
    private EvalContext liveContext = EvalContext.Default;
    private IReadOnlyDictionary<ushort, DateTime> liveAcceptedSince = NoAcceptedSince;
    private IReadOnlyDictionary<ushort, AbandonedEntry> liveAbandoned = NoAbandoned;
    private bool followLive = true;
    private ulong? viewedContentId;

    // Spoiler shield (T19): the viewed character's mask, rebuilt at most once per Version, and the names the player
    // revealed one by one this session.
    private SpoilerMask spoilers = SpoilerMask.None;
    private int spoilersVersion = -1;
    private SpoilerMask liveSpoilers = SpoilerMask.None;
    private int liveSpoilersVersion = -1;
    private readonly HashSet<uint> revealedNames = [];

    // The wider shield (plan v7, 1.20.0 N6): where zone, duty, reward and NPC names sit in the story, from the unlock
    // index of the loaded catalog (UseSpoilerNames).
    private SpoilerNames spoilerNames = SpoilerNames.Empty;

    public SessionState(SnapshotService snapshots, PluginPaths paths, UniqueRewardsData uniqueRewards, CuratedData curated, IPluginLog? log = null)
    {
        this.snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
        this.log = log;
        listeners = new ListenerIsolation((listener, ex, held) =>
            log?.Warning(ex, "Session listener {Listener} failed ({Held} more failures since the last report); the other listeners still ran", listener, held));
        UniqueRewards = uniqueRewards ?? throw new ArgumentNullException(nameof(uniqueRewards));
        Curated = curated ?? throw new ArgumentNullException(nameof(curated));
        StoreResells = StoreResells.Build(UniqueRewards.Entries);
        snapshots.CharactersChanged += BumpCharacters;
    }

    /// <summary>The built catalog; null while loading or after a failure.</summary>
    public CatalogBundle? Bundle { get; private set; }

    /// <summary>
    /// The Journal tree's node icons for <see cref="Bundle"/> (Moon Road proposal §6.2), resolved once per catalog off
    /// the framework thread; <see cref="Core.Ui.NodeIconMap.Empty"/> until then or when the sheets could not be read.
    /// </summary>
    public Core.Ui.NodeIconMap NodeIcons { get; private set; } = Core.Ui.NodeIconMap.Empty;

    /// <summary>Name lookups for <see cref="BlockerText"/> over the current bundle; <see cref="BlockerNames.Default"/> until the catalog is built.</summary>
    public BlockerNames Names { get; private set; } = BlockerNames.Default;

    /// <summary>
    /// <see cref="Names"/> with quest names routed through <see cref="LiveSpoilers"/> instead of the viewed
    /// character's mask, for the surfaces in the game world that speak for the logged-in character (chat lines, the
    /// item context menu, hover hints, Wotsit). The same names as <see cref="Names"/> while that character is viewed
    /// or nobody is logged in.
    /// </summary>
    public BlockerNames LiveNames { get; private set; } = BlockerNames.Default;

    /// <summary>Why the catalog is unavailable, for the "Catalog unavailable" panel.</summary>
    public string? CatalogError { get; private set; }

    public bool CatalogLoading { get; private set; } = true;

    /// <summary>Stored characters, newest capture first.</summary>
    public IReadOnlyList<SnapshotSummary> Characters => snapshots.Characters;

    /// <summary>Content id of the logged-in character once captured; null when logged out.</summary>
    public ulong? LiveContentId { get; private set; }

    /// <summary>
    /// The character shown in the window. Setting it to a stored character views that snapshot; setting it to the
    /// live character (or null) follows the live character again.
    /// </summary>
    public ulong? ViewedContentId
    {
        get => viewedContentId;
        set
        {
            if (value is { } id)
            {
                ViewCharacter(id);
            }
            else
            {
                FollowLive();
            }
        }
    }

    public CharacterSnapshot? ViewedSnapshot { get; private set; }

    /// <summary>True when the viewed character is the one logged in, so the poller keeps it fresh.</summary>
    public bool IsLive => LiveContentId is not null && ViewedContentId == LiveContentId;

    /// <summary>True while no explicit character was chosen; the view tracks logins.</summary>
    public bool IsFollowingLive => followLive;

    /// <summary>Evaluations for <see cref="ViewedSnapshot"/> keyed by quest row id; empty without a catalog or a character.</summary>
    public IReadOnlyDictionary<uint, QuestEvaluation> States { get; private set; } = NoStates;

    /// <summary>The poller's latest capture of the logged-in character, whichever character is viewed; null when logged out.</summary>
    public CharacterSnapshot? LiveSnapshot => liveSnapshot;

    /// <summary>Evaluations for <see cref="LiveSnapshot"/> keyed by quest row id; empty when logged out.</summary>
    public IReadOnlyDictionary<uint, QuestEvaluation> LiveStates => liveStates;

    public ReversePrereqIndex? Index { get; private set; }

    /// <summary>Context the current <see cref="States"/> were resolved with.</summary>
    public EvalContext Context { get; private set; } = EvalContext.Default;

    /// <summary>
    /// When each of <see cref="ViewedSnapshot"/>'s accepted quests entered the journal (runtime quest id to UTC), from
    /// the poller for the live character or the <c>.accepted.json</c> sidecar for a stored one; empty when the
    /// character has no sidecar. Feeds the Stalled preset.
    /// </summary>
    public IReadOnlyDictionary<ushort, DateTime> AcceptedSince { get; private set; } = NoAcceptedSince;

    /// <summary>
    /// The logged-in character's New Game+ session (1.19.0, C4): fed by <see cref="NewGamePlusWatch"/> (the game's HUD)
    /// and the poller (the replays the plausibility guard restores), read by the status bar, the rows and the notices.
    /// Never saved.
    /// </summary>
    public NewGamePlusSession NewGamePlus { get; } = new();

    /// <summary>
    /// Quests <see cref="ViewedSnapshot"/>'s character abandoned (runtime quest id to entry), from the poller for the
    /// live character or the <c>.abandoned.json</c> sidecar for a stored one; empty when there is none. Feeds the
    /// Characters dashboard's Abandoned section and the Abandoned filter.
    /// </summary>
    public IReadOnlyDictionary<ushort, AbandonedEntry> Abandoned { get; private set; } = NoAbandoned;

    /// <summary>The logged-in character's abandoned ledger, whichever character is viewed; empty when logged out.</summary>
    public IReadOnlyDictionary<ushort, AbandonedEntry> LiveAbandoned => liveAbandoned;

    public UniqueRewardsData UniqueRewards { get; }

    public CuratedData Curated { get; }

    /// <summary>Rewards the FFXIV Online Store also sells, from the shipped entries' <c>otherSources</c>; the reward tooltip reads it.</summary>
    public StoreResells StoreResells { get; }

    /// <summary>
    /// Row ids of the feature ("blue") quests, derived once per catalog by <see cref="FeaturePresets.Derive"/> from the
    /// curated files and the quests' rewards. Backs the Unlock quests node, the Unlocks quick view and chat notices.
    /// </summary>
    public IReadOnlySet<uint> FeatureQuestIds { get; private set; } = FrozenSet<uint>.Empty;

    /// <summary>
    /// Story sidequests (artwork sidequests that are not unlock quests, plus the aether current story lines) and their
    /// side stories, derived once per catalog after <see cref="FeatureQuestIds"/>. Backs the Story sidequests quick view and the table's book badge.
    /// </summary>
    public StorySidequests Stories { get; private set; } = StorySidequests.Empty;

    /// <summary>Every chain of the catalog, the side stories of <see cref="Stories"/> included; the detail pane's chain line and the book badge read it.</summary>
    public ChainCatalog Chains { get; private set; } = ChainCatalog.Empty;

    /// <summary>False while the poller is backing off after an exception; the sync glyph shows veiled.</summary>
    public bool PollerHealthy { get; private set; } = true;

    /// <summary>Newest first, capped at <see cref="MaxRecentEvents"/>; cleared on logout and when another character becomes live.</summary>
    public IReadOnlyList<QuestEvent> RecentEvents => recentEvents.Events;

    /// <summary>
    /// Increments on every change of what the viewed character shows (its capture, evaluations, sidecars, the catalog,
    /// the spoiler shield, the text); the UI compares it to rebuild its query. Not on a change of the stored list alone
    /// (<see cref="CharactersVersion"/>).
    /// </summary>
    public int Version { get; private set; }

    /// <summary>
    /// Increments when the stored character list changed and the viewed character did not: this client's own saves,
    /// another game client's saves of other characters, a forget, who is live in another client. Caches that read the
    /// list or other characters key on <see cref="RosterVersion"/>.
    /// </summary>
    public int CharactersVersion { get; private set; }

    /// <summary>Changes whenever <see cref="Version"/> or <see cref="CharactersVersion"/> does (both only grow): the key of a cache that reads the viewed character and the list.</summary>
    public int RosterVersion => Version + CharactersVersion;

    /// <summary>
    /// The spoiler options for a character (content id; null in browse mode): the Settings › Spoilers values with the
    /// character's own override applied. The plugin points it at the configuration; the default shields everything.
    /// </summary>
    internal Func<ulong?, SpoilerOptions> SpoilerOptionsFor { get; set; } = static _ => SpoilerOptions.Default;

    /// <summary>
    /// The viewed character's spoiler shield: which main scenario names print as "Main scenario quest (Lv 83)" and
    /// which banners stay hidden. Rebuilt on first read after any change (<see cref="Version"/>), so a settings change
    /// or a reveal goes through <see cref="RefreshSpoilers"/> or <see cref="RevealName"/>, which bump it.
    /// <see cref="SpoilerMask.None"/> until the catalog is built.
    /// </summary>
    public SpoilerMask Spoilers
    {
        get
        {
            if (spoilersVersion != Version)
            {
                spoilersVersion = Version;
                spoilers = Bundle is { } bundle
                    ? SpoilerMask.Build(bundle.Catalog, States, SpoilerOptionsFor(ViewedContentId), revealedNames, Abandoned, spoilerNames)
                    : SpoilerMask.None;
            }

            return spoilers;
        }
    }

    /// <summary>
    /// The logged-in character's shield, for the surfaces in the game world (chat lines, item menus and hints, Wotsit)
    /// while another character is viewed; the same instance as <see cref="Spoilers"/> when the live character is the
    /// one shown or nobody is logged in.
    /// </summary>
    public SpoilerMask LiveSpoilers
    {
        get
        {
            if (IsLive || LiveContentId is not { } live || Bundle is not { } bundle)
            {
                return Spoilers;
            }

            if (liveSpoilersVersion != Version)
            {
                liveSpoilersVersion = Version;
                liveSpoilers = SpoilerMask.Build(bundle.Catalog, liveStates, SpoilerOptionsFor(live), revealedNames, liveAbandoned, spoilerNames);
            }

            return liveSpoilers;
        }
    }

    /// <summary>"Reveal this name": the quest's real name shows everywhere until the plugin unloads.</summary>
    public void RevealName(uint rowId)
    {
        if (revealedNames.Add(rowId))
        {
            Bump();
        }
    }

    /// <summary>A spoiler setting changed: every surface re-reads the mask.</summary>
    public void RefreshSpoilers() => Bump();

    /// <summary>
    /// Where zone, duty, reward and NPC names sit in the story (plan v7, 1.20.0 N6), from the unlock index of the loaded
    /// catalog: the plugin hands it over each frame, and a new one (the index of a new catalog landing) makes every
    /// surface re-read the mask.
    /// </summary>
    public void UseSpoilerNames(SpoilerNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        if (ReferenceEquals(spoilerNames, names))
        {
            return;
        }

        spoilerNames = names;
        Bump();
    }

    /// <summary>
    /// The end dates the player entered for running events with no known end (1.19.0, C10: Characters › Seasonal
    /// events › Set end date…), Festival id to UTC end; passed to every running-events list (<see cref="Core.Seasonal.SeasonalNow"/>).
    /// Curated data replaces an entry when it gives one.
    /// </summary>
    public IReadOnlyDictionary<ushort, DateTime> EnteredFestivalEnds { get; private set; } = new Dictionary<ushort, DateTime>();

    /// <summary>The player entered or cleared an event's end date: every surface reads the new set.</summary>
    public void SetEnteredFestivalEnds(IReadOnlyDictionary<ushort, DateTime> ends)
    {
        EnteredFestivalEnds = ends ?? throw new ArgumentNullException(nameof(ends));
        Bump();
    }

    /// <summary>
    /// The UI language changed (V2-19): every cache keyed by <see cref="Version"/> (the panes' labels, the spoiler
    /// masks' placeholders, the query's status texts) rebuilds on its next read.
    /// </summary>
    public void RefreshText() => Bump();

    /// <summary>Wall time of the last poll (capture, diff and resolve) in milliseconds; 0 before the first poll.</summary>
    public double LastPollMs { get; private set; }

    /// <summary>Exponential moving average of <see cref="LastPollMs"/> over the session.</summary>
    public double AveragePollMs { get; private set; }

    /// <summary>Polls completed this session, including ones that found nothing changed.</summary>
    public int PollCount { get; private set; }

    /// <summary>Weight of the newest poll in <see cref="AveragePollMs"/>.</summary>
    private const double PollAverageWeight = 0.1;

    /// <summary>Records one poll's cost. Does not bump <see cref="Version"/>: timing is not state the UI must rebuild for.</summary>
    internal void RecordPoll(double milliseconds)
    {
        if (!double.IsFinite(milliseconds) || milliseconds < 0)
        {
            return;
        }

        LastPollMs = milliseconds;
        PollCount++;
        AveragePollMs = PollCount == 1 ? milliseconds : AveragePollMs + (milliseconds - AveragePollMs) * PollAverageWeight;
    }

    /// <summary>
    /// Raised after every <see cref="Version"/> bump. Each listener runs even when an earlier one throws; a failure is
    /// logged (once a minute per listener) and does not reach the code that changed the session.
    /// </summary>
    public event Action? Changed;

    /// <summary>Raised after every <see cref="CharactersVersion"/> bump, each listener isolated as for <see cref="Changed"/>.</summary>
    public event Action? CharactersChanged;

    /// <summary>Raised after <see cref="DeleteAllData"/> removed the stored files, so in-memory copies (pins, overrides) can drop them.</summary>
    public event Action? DataDeleted;

    /// <summary>Raised after <see cref="ForgetCharacter"/> deleted that character's snapshot, with its content id.</summary>
    public event Action<ulong>? CharacterForgotten;

    /// <summary>
    /// Shows a character. The live one shows at once. A stored one is read and resolved on a worker (15–40 ms of a
    /// frame before) and shown on the frame it lands (<see cref="TakePendingView"/>); the character on view stays until
    /// then and <see cref="LoadingContentId"/> names the one coming. Without a catalog there is nothing to resolve, so it
    /// shows at once. Returns false when the character is neither live nor stored.
    /// </summary>
    public bool ViewCharacter(ulong contentId)
    {
        if (contentId == LiveContentId && liveSnapshot is not null)
        {
            FollowLive();
            return true;
        }

        if (Bundle is { } bundle && IsStored(contentId))
        {
            RequestView(contentId, bundle);
            return true;
        }

        return ViewCharacterNow(contentId);
    }

    /// <summary>
    /// Shows a stored character on this thread: reads its file (quarantining a corrupt one, as the store does) and
    /// resolves it here. The path without a catalog, and the fallback for a file the worker could not read.
    /// </summary>
    private bool ViewCharacterNow(ulong contentId)
    {
        var snapshot = snapshots.Load(contentId);
        if (snapshot is null)
        {
            return false;
        }

        CancelPendingView();
        followLive = false;
        viewedContentId = contentId;
        ViewedSnapshot = snapshot;
        Context = StoredContext(snapshot);
        States = Bundle is { } bundle ? StateResolver.ResolveAll(bundle.Catalog, snapshot, Context) : NoStates;
        // Derived data: a sidecar that cannot be read simply reads as unknown accepted times.
        AcceptedSince = AcceptedSinceFile.Load(AcceptedSinceFile.PathFor(paths.CharactersDir, contentId));
        Abandoned = AbandonedLedger.Load(AbandonedLedger.PathFor(paths.CharactersDir, contentId));
        Bump();
        return true;
    }

    /// <summary>
    /// Deletes a stored snapshot. The live character stays in memory and is written again on its next change; forgetting
    /// it only makes sense after logging out. Returns how many <see cref="CharacterForgotten"/> listeners failed (each
    /// is logged): a failed one may still hold the character's pins or overrides in memory and write them back, so the
    /// caller says so rather than reporting a clean forget. 0 also when nothing was forgotten (live in another client).
    /// </summary>
    public int ForgetCharacter(ulong contentId)
    {
        // Multibox (D11): a character live in another game client belongs to that client, which would write it again
        // within seconds; the Characters pane disables Forget for it, and this guards any other caller.
        if (IsLiveElsewhere(contentId))
        {
            log?.Warning("Character {ContentId} is live in another game client; it was not forgotten", contentId);
            return 0;
        }

        snapshots.Delete(contentId);
        foreach (var sidecar in CharacterSidecars.PathsFor(paths.CharactersDir, contentId))
        {
            DeleteIfExists(sidecar);
        }

        DeleteHeartbeat(contentId);

        if (pendingView?.ContentId == contentId)
        {
            CancelPendingView();
        }

        if (ViewedContentId == contentId && contentId != LiveContentId)
        {
            FollowLive();
        }

        return listeners.Raise(CharacterForgotten, contentId);
    }

    /// <summary>
    /// Removes every stored snapshot plus pins and overrides. See <see cref="ForgetCharacter"/> for the live character.
    /// Multibox (D11): the snapshot, sidecars and heartbeat of a character live in another game client stay (that
    /// client owns them), and so do its pins; every other heartbeat this client may delete goes (<see cref="DeleteHeartbeats"/>).
    /// Pins and overrides are rewritten under the cross-client lock rather than deleted (<see cref="ResetUserFiles"/>).
    /// Returns how many <see cref="DataDeleted"/> listeners failed (each is logged): the files are gone, but a failed
    /// listener may still hold its in-memory copy and write it back, so the caller says so rather than reporting success.
    /// </summary>
    public int DeleteAllData()
    {
        snapshots.DeleteAll(IsLiveElsewhere);
        foreach (var sidecar in CharacterSidecars.FindAll(paths.CharactersDir))
        {
            if (SidecarOwner(sidecar) is { } owner && IsLiveElsewhere(owner))
            {
                continue;
            }

            DeleteIfExists(sidecar);
        }

        DeleteHeartbeats();

        ResetUserFiles();
        DeleteJournalIndex();
        recentEvents.Clear();
        // Listeners drop their in-memory copies (pins, overrides, spoiler overrides) first, so the bump in FollowLive
        // is the last one: whatever rebuilds on it, the spoiler masks included, sees the data already gone.
        var failed = listeners.Raise(DataDeleted);
        FollowLive();
        return failed;
    }

    /// <summary>
    /// Swaps a finished build in. Everything derived from it is computed before anything is assigned, so a throw here
    /// leaves the previous catalog whole (the caller then reports the new one as unavailable) rather than a session
    /// holding the new bundle beside the old indexes. The derived indexes, and the evaluations of the stored character
    /// on view, normally come <paramref name="prepared"/> on the catalog worker (<see cref="PrepareCatalog"/>), so this
    /// frame only assigns; without them, or for a view that changed since, they are built here as before.
    /// </summary>
    internal void SetCatalog(CatalogBundle bundle, Core.Ui.NodeIconMap? nodeIcons = null, PreparedCatalog? prepared = null)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        if (prepared is null || !ReferenceEquals(prepared.Bundle, bundle))
        {
            prepared = PrepareCatalog(bundle, resolveView: false);
        }

        var indexes = prepared.Indexes;
        // Every blocker, status line, todo row and diagnostic names quests (and duties past the story point, 1.20.0
        // N6) through the viewed character's shield.
        var dutyName = prepared.Names.Duty;
        var clientName = prepared.Names.SatisfactionNpc;
        var names = prepared.Names with
        {
            QuestName = quest => Spoilers.DisplayName(quest),
            Duty = id => Spoilers.Name(SpoilerKind.Duty, dutyName(id)),
            SatisfactionNpc = id => Spoilers.Name(SpoilerKind.Npc, clientName(id)),
        };
        var context = prepared.Context;

        // The live evaluations belong to the previous catalog (a filing flip retires or restores rows): shown
        // against this one they would read "Locked out · removed from the game" on rows no longer retired, or Ready
        // on retired ones, until the poller's next pass. The poller sees the new bundle on its next poll and starts
        // a first pass; until it commits, the live character reads Not checked.
        var viewedContext = Context;
        var viewedStates = States;
        if (ViewedSnapshot is { } viewed && !IsLive)
        {
            var server = ServerFestivals.For(viewed, liveSnapshot, Curated.Festivals, DateTime.UtcNow);
            if (prepared.View is { } view && ReferenceEquals(view.Snapshot, viewed) && server.SameAs(view.Server))
            {
                viewedContext = view.Context;
                viewedStates = view.States;
            }
            else
            {
                viewedContext = StoredContext(server, context);
                viewedStates = StateResolver.ResolveAll(bundle.Catalog, viewed, viewedContext);
            }
        }
        else if (IsLive)
        {
            viewedStates = NoStates;
        }

        if (indexes.ChainsError is { } chainsError)
        {
            log?.Warning(chainsError, "Story sidequests or chains could not be built");
        }

        foreach (var warning in indexes.Chains.Warnings)
        {
            log?.Warning("Chains: {Warning}", warning);
        }

        Bundle = bundle;
        NodeIcons = nodeIcons ?? Core.Ui.NodeIconMap.Empty;
        CatalogError = null;
        CatalogLoading = false;
        Index = indexes.Index;
        FeatureQuestIds = indexes.FeatureQuestIds;
        Stories = indexes.Stories;
        Chains = indexes.Chains;
        Names = names;
        // Chat, item menus and hints speak for the logged-in character, whichever one the window shows.
        LiveNames = names with
        {
            QuestName = quest => LiveSpoilers.DisplayName(quest),
            Duty = id => LiveSpoilers.Name(SpoilerKind.Duty, dutyName(id)),
            SatisfactionNpc = id => LiveSpoilers.Name(SpoilerKind.Npc, clientName(id)),
        };
        baseContext = context;
        liveStates = NoStates;
        Context = viewedContext;
        States = viewedStates;
        Bump();
    }

    internal void SetCatalogError(string error)
    {
        CatalogError = error;
        CatalogLoading = false;
        Bump();
    }

    /// <summary>
    /// A rebuild started (a filing flip or a retry): the windows show the catalog as loading until the build lands.
    /// The current <see cref="Bundle"/> stays in place for the poller and the integrations meanwhile.
    /// </summary>
    internal void SetCatalogRebuilding()
    {
        if (CatalogLoading)
        {
            return;
        }

        CatalogLoading = true;
        Bump();
    }

    /// <summary>The poller's latest capture and evaluations. Shown when following live or when the viewed character is this one.</summary>
    /// <param name="acceptedSince">The poller's accepted-time map for this character; null keeps whatever was published last.</param>
    /// <param name="abandoned">The poller's abandoned ledger for this character; null keeps whatever was published last.</param>
    internal void SetLive(CharacterSnapshot snapshot, IReadOnlyDictionary<uint, QuestEvaluation> states, EvalContext context, IReadOnlyDictionary<ushort, DateTime>? acceptedSince = null, IReadOnlyDictionary<ushort, AbandonedEntry>? abandoned = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(context);

        liveSnapshot = snapshot;
        liveStates = states;
        // The live flags are the server's: the same context then resolves any stored character (the Characters
        // comparison) against what runs now, not what ran when it was saved. The live states are unchanged by it.
        liveContext = context with { ServerFestivals = ServerFestivals.Of(snapshot) };
        liveAcceptedSince = acceptedSince ?? liveAcceptedSince;
        liveAbandoned = abandoned ?? liveAbandoned;
        var wasLive = LiveContentId;
        LiveContentId = snapshot.ContentId;
        // Another character without a logout gap in between: its Recent activity must not start with the previous one's.
        recentEvents.Follow(snapshot.ContentId);

        if (followLive || ViewedContentId == snapshot.ContentId)
        {
            followLive = true;
            viewedContentId = snapshot.ContentId;
            ViewedSnapshot = snapshot;
            States = states;
            Context = liveContext;
            AcceptedSince = liveAcceptedSince;
            Abandoned = liveAbandoned;
        }
        else
        {
            RefreshStoredFestivals();
        }

        if (wasLive != snapshot.ContentId)
        {
            // Logged in here: its file's "not updating" mark no longer shows (the bump below covers the dashboard).
            ShowNotUpdating(bumpViewed: false);
        }

        Bump();
    }

    /// <summary>
    /// Logout: the last live snapshot stays viewable as a stale snapshot, resolved from now on as a stored character
    /// is (<see cref="StoredContext(CharacterSnapshot, EvalContext?)"/>): the live context it was shown with carries
    /// today's allied society offer and reads its dailies as the game cleared them, neither of which holds once the
    /// character is gone (the offer would outlive its day, and the next reset would find no clock to read it against).
    /// </summary>
    internal void ClearLive()
    {
        LiveContentId = null;
        liveSnapshot = null;
        liveStates = NoStates;
        liveContext = EvalContext.Default;
        liveAcceptedSince = NoAcceptedSince;
        liveAbandoned = NoAbandoned;
        recentEvents.Clear();
        RefreshStoredFestivals(force: true);
        ShowNotUpdating(bumpViewed: false);
        Bump();
    }

    /// <summary>
    /// The seasonal events running on the server for the viewed character (<see cref="ServerFestivals.For"/>): the
    /// live character's flags while someone is logged in, else the viewed snapshot's less those a passed curated end
    /// shows to be stale. The Todo overlay and the Characters dashboard list running events from it; the viewed
    /// character's <see cref="States"/> are resolved with the same set.
    /// </summary>
    public ServerFestivals ServerFestivals => ServerFestivals.For(ViewedSnapshot, liveSnapshot, Curated.Festivals, DateTime.UtcNow);

    /// <summary>
    /// The context a stored character is resolved with: the base context, the festivals running on the server now,
    /// and the clock that reads its dailies and weeklies from before the last reset as cleared (<see cref="GameResets.AsOf"/>).
    /// </summary>
    private EvalContext StoredContext(CharacterSnapshot snapshot, EvalContext? context = null) =>
        StoredContext(ServerFestivals.For(snapshot, liveSnapshot, Curated.Festivals, DateTime.UtcNow), context);

    private EvalContext StoredContext(ServerFestivals server, EvalContext? context = null) =>
        (context ?? baseContext).ForStoredCharacter(server, StoredCycleClock);

    /// <summary>The clock a stored character's cycle data is read against (<see cref="EvalContext.CycleClock"/>).</summary>
    internal static readonly Func<DateTime> StoredCycleClock = static () => DateTime.UtcNow;

    /// <summary>
    /// Framework thread, every frame, one compare: once the daily (15:00 UTC) or weekly (Tuesday 08:00 UTC) reset
    /// passes, a stored character on view is resolved again, its dailies and weeklies done before the reset now open,
    /// and the session bumps so every surface (the "resets in" tooltips, the Characters comparison) catches up. The
    /// live character needs nothing here: the game clears its data and the poller's next capture shows it.
    /// </summary>
    internal void WatchResets(DateTime nowUtc)
    {
        if (nowUtc < nextReset)
        {
            return;
        }

        var first = nextReset == DateTime.MinValue;
        var daily = GameResets.NextDaily(nowUtc);
        var weekly = GameResets.NextWeekly(nowUtc);
        nextReset = daily < weekly ? daily : weekly;
        if (first)
        {
            return;
        }

        // A stored character on view is resolved again on a worker; until it lands it keeps its states, and the bump
        // below already moves the "resets in" texts. A character still being opened is left alone: it notices the reset
        // itself when it lands (TakePendingView) and is resolved again then.
        if (!IsLive && ViewedSnapshot is { } viewed && Bundle is { } bundle && pendingView is not { Refresh: false })
        {
            StartView(viewed.ContentId, bundle, viewed, refresh: true);
        }

        Bump();
    }

    /// <summary>
    /// A stored character on view is resolved again when the server's running festivals changed under it (a login, a
    /// logout, an event starting or ending on the live character); nothing else about it can change here, except at a
    /// logout, when the character that was live stays on view and leaves its live context (<paramref name="force"/>).
    /// The resolve runs on a worker (<see cref="StartView"/>); the states on view stay until it lands. A character
    /// still being opened checks the festivals itself when it lands.
    /// </summary>
    private void RefreshStoredFestivals(bool force = false)
    {
        if (IsLive || ViewedSnapshot is not { } viewed || Bundle is not { } bundle || pendingView is { Refresh: false })
        {
            return;
        }

        var server = ServerFestivals.For(viewed, liveSnapshot, Curated.Festivals, DateTime.UtcNow);
        if (!force && server.SameAs(Context.ServerFestivals))
        {
            return;
        }

        StartView(viewed.ContentId, bundle, viewed, refresh: true);
    }

    /// <summary>One poll's events for the character they belong to; events of another character are dropped first.</summary>
    internal void AddEvents(ulong contentId, IReadOnlyList<QuestEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (recentEvents.Add(contentId, events))
        {
            Bump();
        }
    }

    internal void SetPollerHealthy(bool healthy)
    {
        if (PollerHealthy == healthy)
        {
            return;
        }

        PollerHealthy = healthy;
        Bump();
    }

    /// <summary>The context every character is resolved with: category lookup from the bundle and curated festival ends; the daily offer stays unknown.</summary>
    internal EvalContext BaseContext => baseContext;

    /// <summary>Where the snapshots and their sidecars live, for the poller.</summary>
    internal PluginPaths Paths => paths;

    private void FollowLive()
    {
        CancelPendingView();
        followLive = true;
        if (liveSnapshot is { } live)
        {
            viewedContentId = live.ContentId;
            ViewedSnapshot = live;
            States = liveStates;
            Context = liveContext;
            AcceptedSince = liveAcceptedSince;
            Abandoned = liveAbandoned;
        }
        else
        {
            viewedContentId = null;
            ViewedSnapshot = null;
            States = NoStates;
            Context = baseContext;
            AcceptedSince = NoAcceptedSince;
            Abandoned = NoAbandoned;
        }

        Bump();
    }

    private void Bump()
    {
        Version++;
        PublishViewHint();
        listeners.Raise(Changed);
    }

    /// <summary>The stored list changed, the viewed character did not (<see cref="CharactersVersion"/>).</summary>
    private void BumpCharacters()
    {
        CharactersVersion++;
        listeners.Raise(CharactersChanged);
    }

    /// <summary>
    /// Deletes a file when it exists. The poller's first-pass worker reads the accepted-time sidecar with
    /// <see cref="File.ReadAllText(string)"/>, whose share mode refuses a delete for the milliseconds the read takes,
    /// so a sharing violation is retried once after a short pause; a second failure is logged and the file stays
    /// (the next flush rewrites the pair, the next forget removes it) rather than surfacing from a button click.
    /// </summary>
    private void DeleteIfExists(string path)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return;
            }
            catch (IOException) when (attempt == 1)
            {
                Thread.Sleep(DeleteRetryDelayMs);
            }
            catch (IOException ex)
            {
                log?.Warning(ex, "Could not delete {Path}; it is left in place", path);
                return;
            }
        }
    }

    /// <summary>
    /// The journal search index (<c>cache/journal-index.*.bin</c>) and any temporary file a save left. It holds words
    /// and quest ids only, but "Delete all data" means every file Tsukimichi wrote; a later enable builds it again.
    /// </summary>
    private void DeleteJournalIndex()
    {
        try
        {
            Core.Text.JournalIndexStore.DeleteOthers(paths.ConfigDir, null);
            // A temporary file younger than a minute may be another game client's build in flight (D11).
            Core.Text.JournalIndexStore.DeleteTemp(paths.ConfigDir, TimeSpan.FromMinutes(1));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log?.Warning(ex, "Could not delete the journal search index; it is left in place");
        }
    }
}
