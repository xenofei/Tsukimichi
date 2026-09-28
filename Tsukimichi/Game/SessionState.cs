using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
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

    private static readonly IReadOnlyDictionary<uint, QuestEvaluation> NoStates = new Dictionary<uint, QuestEvaluation>();
    private static readonly IReadOnlyDictionary<ushort, DateTime> NoAcceptedSince = new Dictionary<ushort, DateTime>();

    private readonly SnapshotService snapshots;
    private readonly PluginPaths paths;
    private readonly List<QuestEvent> recentEvents = [];

    private EvalContext baseContext = EvalContext.Default;
    private CharacterSnapshot? liveSnapshot;
    private IReadOnlyDictionary<uint, QuestEvaluation> liveStates = NoStates;
    private EvalContext liveContext = EvalContext.Default;
    private IReadOnlyDictionary<ushort, DateTime> liveAcceptedSince = NoAcceptedSince;
    private bool followLive = true;
    private ulong? viewedContentId;

    public SessionState(SnapshotService snapshots, PluginPaths paths, UniqueRewardsData uniqueRewards, CuratedData curated)
    {
        this.snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
        UniqueRewards = uniqueRewards ?? throw new ArgumentNullException(nameof(uniqueRewards));
        Curated = curated ?? throw new ArgumentNullException(nameof(curated));
        snapshots.CharactersChanged += Bump;
    }

    /// <summary>The built catalog; null while loading or after a failure.</summary>
    public CatalogBundle? Bundle { get; private set; }

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

    public ReversePrereqIndex? Index { get; private set; }

    /// <summary>Context the current <see cref="States"/> were resolved with.</summary>
    public EvalContext Context { get; private set; } = EvalContext.Default;

    /// <summary>
    /// When each of <see cref="ViewedSnapshot"/>'s accepted quests entered the journal (runtime quest id to UTC), from
    /// the poller for the live character or the <c>.accepted.json</c> sidecar for a stored one; empty when the
    /// character has no sidecar. Feeds the Stalled preset.
    /// </summary>
    public IReadOnlyDictionary<ushort, DateTime> AcceptedSince { get; private set; } = NoAcceptedSince;

    public UniqueRewardsData UniqueRewards { get; }

    public CuratedData Curated { get; }

    /// <summary>
    /// Row ids of the feature ("blue") quests, derived once per catalog by <see cref="FeaturePresets.Derive"/> from the
    /// curated files and the quests' rewards. Backs the Feature Unlocks node, the Feature quests preset and chat notices.
    /// </summary>
    public IReadOnlySet<uint> FeatureQuestIds { get; private set; } = FrozenSet<uint>.Empty;

    /// <summary>False while the poller is backing off after an exception; the sync glyph shows veiled.</summary>
    public bool PollerHealthy { get; private set; } = true;

    /// <summary>Newest first, capped at <see cref="MaxRecentEvents"/>; cleared on logout.</summary>
    public IReadOnlyList<QuestEvent> RecentEvents => recentEvents;

    /// <summary>Increments on every change; the UI compares it to rebuild its query.</summary>
    public int Version { get; private set; }

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
        DeleteIfExists(AcceptedSinceFile.PathFor(paths.CharactersDir, contentId));
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
        if (Directory.Exists(paths.CharactersDir))
        {
            foreach (var sidecar in Directory.GetFiles(paths.CharactersDir, "*" + AcceptedSinceFile.FileSuffix))
            {
                DeleteIfExists(sidecar);
            }
        }

        DeleteIfExists(paths.PinsFile);
        DeleteIfExists(paths.OverridesFile);
        recentEvents.Clear();
        FollowLive();
        DataDeleted?.Invoke();
    }

    internal void SetCatalog(CatalogBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        Bundle = bundle;
        CatalogError = null;
        CatalogLoading = false;
        Index = ReversePrereqIndex.Build(bundle.Catalog);
        FeatureQuestIds = FeaturePresets.Derive(bundle.Catalog, Curated, UniqueRewards.Entries);
        baseContext = EvalContext.Default with { ClassJobs = bundle.Jobs };

        if (ViewedSnapshot is { } viewed && !IsLive)
        {
            Context = baseContext;
            States = StateResolver.ResolveAll(bundle.Catalog, viewed, baseContext);
        }

        Bump();
    }

    internal void SetCatalogError(string error)
    {
        CatalogError = error;
        CatalogLoading = false;
        Bump();
    }

    /// <summary>The poller's latest capture and evaluations. Shown when following live or when the viewed character is this one.</summary>
    /// <param name="acceptedSince">The poller's accepted-time map for this character; null keeps whatever was published last.</param>
    internal void SetLive(CharacterSnapshot snapshot, IReadOnlyDictionary<uint, QuestEvaluation> states, EvalContext context, IReadOnlyDictionary<ushort, DateTime>? acceptedSince = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(context);

        liveSnapshot = snapshot;
        liveStates = states;
        liveContext = context;
        liveAcceptedSince = acceptedSince ?? liveAcceptedSince;
        LiveContentId = snapshot.ContentId;

        if (followLive || ViewedContentId == snapshot.ContentId)
        {
            followLive = true;
            viewedContentId = snapshot.ContentId;
            ViewedSnapshot = snapshot;
            States = states;
            Context = context;
            AcceptedSince = liveAcceptedSince;
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
        recentEvents.Clear();
        Bump();
    }

    internal void AddEvents(IReadOnlyList<QuestEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0)
        {
            return;
        }

        for (var i = events.Count - 1; i >= 0; i--)
        {
            recentEvents.Insert(0, events[i]);
        }

        if (recentEvents.Count > MaxRecentEvents)
        {
            recentEvents.RemoveRange(MaxRecentEvents, recentEvents.Count - MaxRecentEvents);
        }

        Bump();
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

    /// <summary>The base context for stored characters: category lookup from the bundle, no daily offer.</summary>
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
        }
        else
        {
            viewedContentId = null;
            ViewedSnapshot = null;
            States = NoStates;
            Context = baseContext;
            AcceptedSince = NoAcceptedSince;
        }

        Bump();
    }

    private void Bump()
    {
        Version++;
        Changed?.Invoke();
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
