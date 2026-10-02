using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Multibox;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;

namespace Tsukimichi.Game;

/// <summary>
/// Multibox sharing (D11): which characters are live in another game client, and a stored character on view taking
/// in what another client saved. Everything here runs on the framework thread like the rest of the session;
/// <see cref="MultiboxService"/> does the file work on a worker and hands the results over.
/// </summary>
public sealed partial class SessionState
{
    private static readonly IReadOnlyDictionary<ulong, Heartbeat> NoLiveElsewhere = new Dictionary<ulong, Heartbeat>();

    private IReadOnlyDictionary<ulong, Heartbeat> liveElsewhere = NoLiveElsewhere;

    /// <summary>This game client, as its heartbeat files name it.</summary>
    internal ClientIdentity Me { get; } = ClientIdentity.ForThisProcess();

    /// <summary>Characters logged in on another game client right now (a fresh heartbeat of another process), by content id.</summary>
    public IReadOnlyDictionary<ulong, Heartbeat> LiveElsewhere => liveElsewhere;

    /// <summary>True when the character is logged in on another game client: shown "live in another client", never forgotten or written here.</summary>
    public bool IsLiveElsewhere(ulong contentId) => liveElsewhere.ContainsKey(contentId);

    /// <summary>The multibox worker's latest view of the other clients; bumps only when what the player sees changes.</summary>
    internal void SetLiveElsewhere(IReadOnlyDictionary<ulong, Heartbeat> live)
    {
        ArgumentNullException.ThrowIfNull(live);
        if (LiveClients.SameCharacters(liveElsewhere, live))
        {
            liveElsewhere = live;
            return;
        }

        liveElsewhere = live;
        Bump();
    }

    private static readonly IReadOnlyDictionary<ulong, SharedLoad> NoProblems = new Dictionary<ulong, SharedLoad>();

    private IReadOnlyDictionary<ulong, SharedLoad> notUpdating = NoProblems;

    /// <summary>
    /// Characters whose file another game client saved and this client cannot read (1.8.0, R7 G): <see cref="SharedLoad.Newer"/>
    /// when a newer Tsukimichi wrote it, <see cref="SharedLoad.Invalid"/> when it does not parse. What is shown for them
    /// is their last readable save, and it does not update until the file reads again; the lists say so.
    /// </summary>
    public IReadOnlyDictionary<ulong, SharedLoad> NotUpdating => notUpdating;

    /// <summary>The multibox worker's latest set of unreadable files; bumps only when it changed.</summary>
    internal void SetNotUpdating(IReadOnlyDictionary<ulong, SharedLoad> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        if (problems.Count == notUpdating.Count && problems.All(p => notUpdating.TryGetValue(p.Key, out var was) && was == p.Value))
        {
            return;
        }

        notUpdating = problems;
        Bump();
    }

    /// <summary>
    /// Another game client deleted characters' files (a Forget there, 1.8.0, R7 G): when the stored character on view is
    /// one of them, the window follows the live character again rather than showing a character that no longer exists.
    /// </summary>
    internal void FollowLiveIfRemoved(IReadOnlyList<ulong> removed)
    {
        ArgumentNullException.ThrowIfNull(removed);
        if (!followLive && viewedContentId is { } viewed && viewed != LiveContentId && removed.Contains(viewed))
        {
            FollowLive();
        }
    }

    /// <summary>
    /// The accepted-time and abandoned sidecars of the stored character on view changed on disk without its snapshot
    /// (the owning client writes them just after it, 1.8.0, R7 F5): they replace what is shown, unless the view moved on.
    /// </summary>
    internal void ApplyStoredSidecars(ulong contentId, IReadOnlyDictionary<ushort, DateTime> acceptedSince, IReadOnlyDictionary<ushort, AbandonedEntry> abandoned)
    {
        ArgumentNullException.ThrowIfNull(acceptedSince);
        ArgumentNullException.ThrowIfNull(abandoned);
        if (IsLive || followLive || ViewedContentId != contentId)
        {
            return;
        }

        AcceptedSince = acceptedSince;
        Abandoned = abandoned;
        Bump();
    }

    /// <summary>
    /// What a worker needs to resolve a newer copy of the stored character on view, or null when <paramref name="fresh"/>
    /// is not that character, it is the live one here, or the copy shown is already this one. Without a catalog the
    /// newer copy is shown at once (the catalog build resolves it when it lands) and null is returned.
    /// </summary>
    internal (CatalogBundle Bundle, EvalContext Context)? PrepareStoredRefresh(CharacterSnapshot fresh)
    {
        ArgumentNullException.ThrowIfNull(fresh);
        if (IsLive || followLive || ViewedContentId != fresh.ContentId || ViewedSnapshot is not { } shown || shown.TakenUtc == fresh.TakenUtc)
        {
            return null;
        }

        if (Bundle is not { } bundle)
        {
            ViewedSnapshot = fresh;
            Bump();
            return null;
        }

        return (bundle, StoredContext(fresh));
    }

    /// <summary>
    /// A worker resolved a newer copy of the stored character on view: it replaces what is shown, unless the view,
    /// the live state or the catalog moved on meanwhile.
    /// </summary>
    internal void ApplyStoredRefresh(
        CharacterSnapshot fresh,
        CatalogBundle bundle,
        EvalContext context,
        IReadOnlyDictionary<uint, QuestEvaluation> states,
        IReadOnlyDictionary<ushort, DateTime> acceptedSince,
        IReadOnlyDictionary<ushort, AbandonedEntry> abandoned)
    {
        if (IsLive || followLive || ViewedContentId != fresh.ContentId || !ReferenceEquals(Bundle, bundle))
        {
            return;
        }

        ViewedSnapshot = fresh;
        Context = context;
        States = states;
        AcceptedSince = acceptedSince;
        Abandoned = abandoned;
        Bump();
    }

    /// <summary>
    /// The background queue pins and overrides saves go through (set by the plugin). "Delete all data" queues its
    /// rewrite of both files there, behind any save in flight, so that save cannot bring deleted pins back.
    /// </summary>
    internal SerialWriter? Writer { get; set; }

    /// <summary>
    /// "Delete all data" for <c>user/pins.json</c> and <c>user/overrides.json</c> (D11). Neither is deleted: under the
    /// cross-client lock, the pins file is rewritten with only the characters live in another game client (whose pins
    /// that client still shows) and the overrides file is emptied, so a merge in flight in another client cannot
    /// resurrect what was deleted. Runs on the background writer; the listeners of <see cref="DataDeleted"/> drop
    /// their in-memory copies the same way.
    /// </summary>
    private void ResetUserFiles()
    {
        var keep = new HashSet<ulong>(liveElsewhere.Keys);
        var pinsPath = paths.PinsFile;
        var overridesPath = paths.OverridesFile;
        SerialWriter.Submit(
            Writer,
            () =>
            {
                var warnings = new List<string>();
                PinsFile.KeepOnly(pinsPath, keep.Contains, warnings);
                OverridesFile.Clear(overridesPath);
                return warnings;
            },
            (warnings, error) =>
            {
                if (error is not null)
                {
                    log?.Warning(error, "Delete all data: pins or overrides could not be reset");
                }

                foreach (var warning in warnings ?? [])
                {
                    log?.Warning("Delete all data: {Warning}", warning);
                }
            });
    }

    /// <summary>Deletes one character's heartbeat when it is this client's own or stale; another client's fresh one stays.</summary>
    private void DeleteHeartbeat(ulong contentId)
    {
        try
        {
            HeartbeatFile.DeleteIfAllowed(paths.CharactersDir, contentId, Me, DateTime.UtcNow);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log?.Warning(ex, "Could not delete the heartbeat of {ContentId}; it is left in place", contentId);
        }
    }

    /// <summary>"Delete all data": every heartbeat that is this client's own or stale; the multibox service writes its own again at once.</summary>
    private void DeleteHeartbeats()
    {
        if (!Directory.Exists(paths.CharactersDir))
        {
            return;
        }

        try
        {
                // Listed first: deleting while enumerating the folder is not reliable.
            foreach (var path in Directory.GetFiles(paths.CharactersDir, "*" + HeartbeatFile.Suffix))
            {
                if (HeartbeatFile.ContentIdOf(path) is { } id && !IsLiveElsewhere(id))
                {
                    DeleteHeartbeat(id);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log?.Warning(ex, "Could not list the heartbeat files");
        }
    }

    /// <summary>The content id a sidecar file (<c>&lt;contentId&gt;.accepted.json</c> and the like) belongs to, or null.</summary>
    private static ulong? SidecarOwner(string path)
    {
        var name = Path.GetFileName(path);
        var dot = name.IndexOf('.', StringComparison.Ordinal);
        return dot > 0 && ulong.TryParse(name.AsSpan(0, dot), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
    }
}
