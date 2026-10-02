using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;
using AcceptedSinceFile = Tsukimichi.Core.Runtime.AcceptedSince;

namespace Tsukimichi.Game;

/// <summary>
/// The work a stored character needs off the framework thread (R4 F2, F3): opening one (its file read and the
/// catalog-wide resolve, 15–40 ms of a frame before), resolving it again when the server's festivals change or a reset
/// passes, and everything a new catalog needs (<see cref="PrepareCatalog"/>). Each runs on a worker over immutable
/// inputs and is taken on the framework thread when it lands (<see cref="TakePendingView"/>), unless the view or the
/// catalog moved on meanwhile; what is on view stays until then. The multibox refresh of a stored character another
/// client saved works the same way (<see cref="ApplyStoredRefresh"/>).
/// </summary>
public sealed partial class SessionState
{
    /// <summary>The newest view request on a worker; null while none is. Framework thread only.</summary>
    private PendingView? pendingView;

    /// <summary>
    /// The stored character on view and the live capture, for the catalog worker (<see cref="PrepareCatalog"/>), which
    /// resolves that character against the new catalog before it lands. Replaced on every bump; null while the live
    /// character or nobody is on view.
    /// </summary>
    private volatile ViewHint? viewHint;

    /// <summary>
    /// The stored character being opened on a worker (<see cref="ViewCharacter"/>); null while none is. The windows
    /// can mark it as loading; the character on view stays the previous one until it lands.
    /// </summary>
    public ulong? LoadingContentId => pendingView is { Refresh: false } pending ? pending.ContentId : null;

    /// <summary>
    /// Framework thread, every frame: takes a stored character resolved on a worker, if one landed. The request is
    /// started again against a catalog that replaced the one it used; a file the worker could not read is opened here
    /// instead, so a corrupt one is quarantined and reported as before.
    /// </summary>
    internal void TakePendingView()
    {
        if (pendingView is not { Task.IsCompleted: true } done)
        {
            return;
        }

        pendingView = null;
        if (Bundle is not { } bundle)
        {
            return;
        }

        if (!ReferenceEquals(done.Bundle, bundle))
        {
            if (!done.Refresh || (!IsLive && ViewedContentId == done.ContentId))
            {
                StartView(done.ContentId, bundle, done.Known, done.Refresh);
            }

            return;
        }

        if (!done.Task.IsCompletedSuccessfully)
        {
            log?.Warning(done.Task.Exception?.GetBaseException(), "Character {ContentId} could not be evaluated", done.ContentId);
            return;
        }

        if (done.Task.Result is not { } view)
        {
            if (!done.Refresh && !ViewCharacterNow(done.ContentId))
            {
                log?.Warning("Character {ContentId} could not be viewed; its snapshot is unreadable", done.ContentId);
            }

            return;
        }

        if (done.ContentId == LiveContentId && liveSnapshot is not null)
        {
            // Logged in meanwhile: the live capture is the newer one.
            if (!done.Refresh)
            {
                FollowLive();
            }

            return;
        }

        if (done.Refresh)
        {
            // A refresh of the capture on view (which may be the last live one, still followed after a logout): only
            // while that capture is still the one shown.
            if (IsLive || ViewedContentId != done.ContentId || !ReferenceEquals(ViewedSnapshot, view.Snapshot))
            {
                return;
            }
        }
        else
        {
            followLive = false;
        }

        viewedContentId = done.ContentId;
        ViewedSnapshot = view.Snapshot;
        Context = view.Context;
        States = view.States;
        if (view.AcceptedSince is { } acceptedSince)
        {
            AcceptedSince = acceptedSince;
        }

        if (view.Abandoned is { } abandoned)
        {
            Abandoned = abandoned;
        }

        Bump();

        // A daily or weekly reset passed while it resolved (WatchResets leaves a character still being opened alone):
        // what landed reads the cycle before it, so it is resolved again at once.
        if (GameResets.PassedBetween(done.StartedUtc, DateTime.UtcNow))
        {
            StartView(done.ContentId, bundle, view.Snapshot, refresh: true);
            return;
        }

        // The live character's festivals may have moved while it resolved.
        RefreshStoredFestivals();
    }

    /// <summary>
    /// Worker-safe: everything a new catalog needs before it lands, so the framework thread only assigns
    /// (<see cref="SetCatalog"/>): the derived indexes (<see cref="CatalogIndexes"/>), the blocker names, the base
    /// context and, with <paramref name="resolveView"/>, the stored character on view resolved against it. Reads only
    /// the shipped data and <see cref="viewHint"/>.
    /// </summary>
    internal PreparedCatalog PrepareCatalog(CatalogBundle bundle, bool resolveView = true)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        var started = Stopwatch.GetTimestamp();
        var indexes = CatalogIndexes.Build(bundle.Catalog, Curated, UniqueRewards.Entries);
        var names = bundle.BlockerNames();
        var context = EvalContextBuilder.Build(
            Curated.Festivals,
            bundle.Jobs,
            static () => DateTime.UtcNow,
            jobParents: bundle.JobParents(),
            satisfactionNpcName: id => bundle.Names.SatisfactionNpc(id),
            jobRoles: bundle.JobRoles());

        // 8.0 readiness: requirement details name an expansion from the ExVersion sheet, as every other text does.
        context = context with { ExpansionName = names.Expansion, ItemName = bundle.GateItemName, ItemJobCategory = bundle.GateItemJobCategory };

        StoredView? view = null;
        if (resolveView && viewHint is { } hint)
        {
            view = ResolveView(snapshots, hint.Viewed.ContentId, hint.Viewed, bundle, context, hint.Live, Curated.Festivals, paths.CharactersDir, sidecars: false);
        }

        return new PreparedCatalog(bundle, indexes, names, context, view, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    /// <summary>Whether the character is in the stored list.</summary>
    private bool IsStored(ulong contentId)
    {
        foreach (var summary in snapshots.Characters)
        {
            if (summary.ContentId == contentId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Opens a stored character on a worker, unless it is already on view or on its way.</summary>
    private void RequestView(ulong contentId, CatalogBundle bundle)
    {
        if (pendingView is { Refresh: false } pending && pending.ContentId == contentId && ReferenceEquals(pending.Bundle, bundle))
        {
            return;
        }

        if (pendingView is null && !followLive && viewedContentId == contentId && ViewedSnapshot is not null)
        {
            return;
        }

        StartView(contentId, bundle, known: null, refresh: false);
    }

    /// <summary>
    /// Resolves a stored character on a worker; the newest request wins. <paramref name="known"/> is the capture to
    /// resolve (a refresh of the one on view), null to read the stored file. A refresh keeps the sidecars on view.
    /// </summary>
    private void StartView(ulong contentId, CatalogBundle bundle, CharacterSnapshot? known, bool refresh)
    {
        CancelPendingView();
        var store = snapshots;
        var context = baseContext;
        var live = liveSnapshot;
        var festivals = Curated.Festivals;
        var dir = paths.CharactersDir;
        var started = DateTime.UtcNow;
        var task = Task.Run(() => ResolveView(store, contentId, known, bundle, context, live, festivals, dir, sidecars: !refresh));
        pendingView = new PendingView(contentId, bundle, known, refresh, task, started);
    }

    /// <summary>Forgets the view request in flight; its worker finishes on its own and its result is ignored.</summary>
    private void CancelPendingView()
    {
        if (pendingView is { } pending)
        {
            pendingView = null;
            pending.Task.ContinueWith(static t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    /// <summary>
    /// Worker: reads the stored capture unless it is given (without quarantining or reporting anything: a file that
    /// does not read returns null and the framework thread opens it again), resolves it with the festivals running on
    /// the server, and loads its sidecars when asked.
    /// </summary>
    private static StoredView? ResolveView(
        SnapshotService store,
        ulong contentId,
        CharacterSnapshot? known,
        CatalogBundle bundle,
        EvalContext baseContext,
        CharacterSnapshot? live,
        IReadOnlyDictionary<ushort, FestivalInfo> festivals,
        string charactersDir,
        bool sidecars)
    {
        var snapshot = known;
        if (snapshot is null)
        {
            var read = store.ReadStored(contentId);
            if (!read.IsLoaded)
            {
                return null;
            }

            snapshot = read.Value!;
        }

        var server = ServerFestivals.For(snapshot, live, festivals, DateTime.UtcNow);
        var context = baseContext.ForStoredCharacter(server, StoredCycleClock);
        var states = StateResolver.ResolveAll(bundle.Catalog, snapshot, context);

        // Derived data: a sidecar that cannot be read simply reads as unknown accepted times.
        var acceptedSince = sidecars ? AcceptedSinceFile.Load(AcceptedSinceFile.PathFor(charactersDir, contentId)) : null;
        var abandoned = sidecars ? AbandonedLedger.Load(AbandonedLedger.PathFor(charactersDir, contentId)) : null;
        return new StoredView(snapshot, server, context, states, acceptedSince, abandoned);
    }

    /// <summary>Keeps <see cref="viewHint"/> on the stored character on view; called on every bump.</summary>
    private void PublishViewHint()
    {
        var viewed = IsLive ? null : ViewedSnapshot;
        var hint = viewHint;
        if (viewed is null)
        {
            if (hint is not null)
            {
                viewHint = null;
            }

            return;
        }

        if (hint is null || !ReferenceEquals(hint.Viewed, viewed) || !ReferenceEquals(hint.Live, liveSnapshot))
        {
            viewHint = new ViewHint(viewed, liveSnapshot);
        }
    }

    /// <summary>
    /// A view request on a worker. <paramref name="Refresh"/> re-resolves the capture on view (<paramref name="Known"/>);
    /// <paramref name="StartedUtc"/> is when it was asked for, so a reset passing before it lands is noticed.
    /// </summary>
    private sealed record PendingView(ulong ContentId, CatalogBundle Bundle, CharacterSnapshot? Known, bool Refresh, Task<StoredView?> Task, DateTime StartedUtc);

    private sealed record ViewHint(CharacterSnapshot Viewed, CharacterSnapshot? Live);
}

/// <summary>
/// A stored character resolved on a worker: the capture, the festivals running on the server it was resolved with,
/// its context and evaluations, and its sidecars (null when a refresh kept the ones on view).
/// </summary>
internal sealed record StoredView(
    CharacterSnapshot Snapshot,
    ServerFestivals Server,
    EvalContext Context,
    IReadOnlyDictionary<uint, QuestEvaluation> States,
    IReadOnlyDictionary<ushort, DateTime>? AcceptedSince,
    IReadOnlyDictionary<ushort, AbandonedEntry>? Abandoned);

/// <summary>
/// What the catalog worker builds for a new bundle before it lands (<see cref="SessionState.PrepareCatalog"/>): the
/// derived indexes, the blocker names, the base context and the stored character on view resolved against it.
/// </summary>
/// <param name="View">The stored character that was on view while the build ran; the session uses it when that is still the one.</param>
/// <param name="PrepareMs">How long the worker took.</param>
internal sealed record PreparedCatalog(
    CatalogBundle Bundle,
    CatalogIndexes Indexes,
    BlockerNames Names,
    EvalContext Context,
    StoredView? View,
    double PrepareMs);
