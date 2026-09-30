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
/// Written by the poller and the plugin on the framework thread, read by the UI on that same thread, so a version
/// counter plus <see cref="Changed"/> is all the synchronization needed. Every mutation bumps <see cref="Version"/>.
/// </summary>
public sealed class SessionState
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

    private EvalContext baseContext = EvalContext.Default;
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

    public SessionState(SnapshotService snapshots, PluginPaths paths, UniqueRewardsData uniqueRewards, CuratedData curated, IPluginLog? log = null)
    {
        this.snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
        this.log = log;
        UniqueRewards = uniqueRewards ?? throw new ArgumentNullException(nameof(uniqueRewards));
        Curated = curated ?? throw new ArgumentNullException(nameof(curated));
        StoreResells = StoreResells.Build(UniqueRewards.Entries);
        snapshots.CharactersChanged += Bump;
    }

    /// <summary>The built catalog; null while loading or after a failure.</summary>
    public CatalogBundle? Bundle { get; private set; }

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
    /// Story sidequests (artwork sidequests that are not unlock quests) and their side stories, derived once per
    /// catalog after <see cref="FeatureQuestIds"/>. Backs the Story sidequests quick view and the table's book badge.
    /// </summary>
    public StorySidequests Stories { get; private set; } = StorySidequests.Empty;

    /// <summary>Every chain of the catalog, the side stories of <see cref="Stories"/> included; the detail pane's chain line and the book badge read it.</summary>
    public ChainCatalog Chains { get; private set; } = ChainCatalog.Empty;

    /// <summary>False while the poller is backing off after an exception; the sync glyph shows veiled.</summary>
    public bool PollerHealthy { get; private set; } = true;

    /// <summary>Newest first, capped at <see cref="MaxRecentEvents"/>; cleared on logout and when another character becomes live.</summary>
    public IReadOnlyList<QuestEvent> RecentEvents => recentEvents.Events;

    /// <summary>Increments on every change; the UI compares it to rebuild its query.</summary>
    public int Version { get; private set; }

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
                    ? SpoilerMask.Build(bundle.Catalog, States, SpoilerOptionsFor(ViewedContentId), revealedNames, Abandoned)
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
                liveSpoilers = SpoilerMask.Build(bundle.Catalog, liveStates, SpoilerOptionsFor(live), revealedNames, liveAbandoned);
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

    public event Action? Changed;

    /// <summary>Raised after <see cref="DeleteAllData"/> removed the stored files, so in-memory copies (pins, overrides) can drop them.</summary>
    public event Action? DataDeleted;

    /// <summary>Raised after <see cref="ForgetCharacter"/> deleted that character's snapshot, with its content id.</summary>
    public event Action<ulong>? CharacterForgotten;

    /// <summary>Shows a character. Returns false when it is neither live nor stored.</summary>
    public bool ViewCharacter(ulong contentId)
    {
        if (contentId == LiveContentId && liveSnapshot is not null)
        {
            FollowLive();
            return true;
        }

        var snapshot = snapshots.Load(contentId);
        if (snapshot is null)
        {
            return false;
        }

        followLive = false;
        viewedContentId = contentId;
        ViewedSnapshot = snapshot;
        Context = baseContext;
        States = Bundle is { } bundle ? StateResolver.ResolveAll(bundle.Catalog, snapshot, baseContext) : NoStates;
        // Derived data: a sidecar that cannot be read simply reads as unknown accepted times.
        AcceptedSince = AcceptedSinceFile.Load(AcceptedSinceFile.PathFor(paths.CharactersDir, contentId));
        Abandoned = AbandonedLedger.Load(AbandonedLedger.PathFor(paths.CharactersDir, contentId));
        Bump();
        return true;
    }

    /// <summary>
    /// Deletes a stored snapshot. The live character stays in memory and is written again on its next change; forgetting
    /// it only makes sense after logging out.
    /// </summary>
    public void ForgetCharacter(ulong contentId)
    {
        snapshots.Delete(contentId);
        foreach (var sidecar in CharacterSidecars.PathsFor(paths.CharactersDir, contentId))
        {
            DeleteIfExists(sidecar);
        }

        if (ViewedContentId == contentId && contentId != LiveContentId)
        {
            FollowLive();
        }

        CharacterForgotten?.Invoke(contentId);
    }

    /// <summary>Removes every stored snapshot plus pins and overrides. See <see cref="ForgetCharacter"/> for the live character.</summary>
    public void DeleteAllData()
    {
        snapshots.DeleteAll();
        foreach (var sidecar in CharacterSidecars.FindAll(paths.CharactersDir))
        {
            DeleteIfExists(sidecar);
        }

        DeleteIfExists(paths.PinsFile);
        DeleteIfExists(paths.OverridesFile);
        recentEvents.Clear();
        // Listeners drop their in-memory copies (pins, overrides, spoiler overrides) first, so the bump in FollowLive
        // is the last one: whatever rebuilds on it, the spoiler masks included, sees the data already gone.
        DataDeleted?.Invoke();
        FollowLive();
    }

    /// <summary>Story sidequests and the chain catalog for a new bundle; a failure leaves both empty rather than failing the load.</summary>
    private void BuildChains(CatalogBundle bundle)
    {
        try
        {
            Stories = StorySidequests.Build(bundle.Catalog, FeatureQuestIds);
            Chains = ChainCatalog.Build(bundle.Catalog, Curated, Stories);
            foreach (var warning in Chains.Warnings)
            {
                log?.Warning("Chains: {Warning}", warning);
            }
        }
        catch (Exception ex)
        {
            Stories = StorySidequests.Empty;
            Chains = ChainCatalog.Empty;
            log?.Warning(ex, "Story sidequests or chains could not be built");
        }
    }

    internal void SetCatalog(CatalogBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        Bundle = bundle;
        CatalogError = null;
        CatalogLoading = false;
        Index = ReversePrereqIndex.Build(bundle.Catalog);
        FeatureQuestIds = FeaturePresets.Derive(bundle.Catalog, Curated, UniqueRewards.Entries);
        BuildChains(bundle);
        // Every blocker, status line, todo row and diagnostic names quests through the viewed character's shield.
        Names = bundle.BlockerNames() with { QuestName = quest => Spoilers.DisplayName(quest) };
        // Chat, item menus and hints speak for the logged-in character, whichever one the window shows.
        LiveNames = Names with { QuestName = quest => LiveSpoilers.DisplayName(quest) };
        baseContext = EvalContextBuilder.Build(
            Curated.Festivals,
            bundle.Jobs,
            static () => DateTime.UtcNow,
            jobParents: bundle.JobParents(),
            satisfactionNpcName: id => bundle.Names.SatisfactionNpc(id));

        // The live evaluations belong to the previous catalog (a filing flip retires or restores rows): shown
        // against this one they would read "Locked out · removed from the game" on rows no longer retired, or Ready
        // on retired ones, until the poller's next pass. The poller sees the new bundle on its next poll and starts
        // a first pass; until it commits, the live character reads Not checked.
        liveStates = NoStates;
        if (ViewedSnapshot is { } viewed && !IsLive)
        {
            Context = baseContext;
            States = StateResolver.ResolveAll(bundle.Catalog, viewed, baseContext);
        }
        else if (IsLive)
        {
            States = NoStates;
        }

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
        liveContext = context;
        liveAcceptedSince = acceptedSince ?? liveAcceptedSince;
        liveAbandoned = abandoned ?? liveAbandoned;
        LiveContentId = snapshot.ContentId;
        // Another character without a logout gap in between: its Recent activity must not start with the previous one's.
        recentEvents.Follow(snapshot.ContentId);

        if (followLive || ViewedContentId == snapshot.ContentId)
        {
            followLive = true;
            viewedContentId = snapshot.ContentId;
            ViewedSnapshot = snapshot;
            States = states;
            Context = context;
            AcceptedSince = liveAcceptedSince;
            Abandoned = liveAbandoned;
        }

        Bump();
    }

    /// <summary>Logout: the last live snapshot stays viewable as a stale snapshot.</summary>
    internal void ClearLive()
    {
        LiveContentId = null;
        liveSnapshot = null;
        liveStates = NoStates;
        liveContext = EvalContext.Default;
        liveAcceptedSince = NoAcceptedSince;
        liveAbandoned = NoAbandoned;
        recentEvents.Clear();
        Bump();
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
        Changed?.Invoke();
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
}
