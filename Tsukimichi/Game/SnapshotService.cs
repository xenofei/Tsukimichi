using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Game;

/// <summary>
/// Owns the snapshot store and the login lifecycle. After <see cref="IClientState.Login"/> the character is not
/// readable at once: <see cref="CharacterReady"/> turns true on the first framework tick where the player is loaded
/// with a content id (see <see cref="GameStateReader.IsCharacterReadable"/>), or after <see cref="LoginSettleTimeout"/>
/// as a safety net. It tells the poller when it may capture. A character already logged in at load (a hot load) goes
/// through the same probe: the content id can still be 0 for a few ticks, and marking it ready at once would make the
/// poller's first capture throw and back off for up to <see cref="StatePoller.MaxBackoff"/>.
/// Every event here fires on the framework thread.
/// </summary>
public sealed class SnapshotService : IDisposable
{
    public static readonly TimeSpan LoginSettleTimeout = TimeSpan.FromSeconds(5);

    private readonly JsonSnapshotStore store;
    private readonly IClientState clientState;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly GameStateReader? reader;
    private readonly CancellationTokenSource lifetime = new();
    private readonly List<SnapshotSummary> characters = [];

    /// <summary>Raises <see cref="CharactersChanged"/> and <see cref="LoggingOut"/> one listener at a time.</summary>
    private readonly ListenerIsolation listeners;

    /// <summary>Snapshots other game clients saved, as the multibox worker read them (D11); framework thread only.</summary>
    private readonly Dictionary<ulong, CharacterSnapshot> external = [];

    private int loginGeneration;
    private bool awaitingCharacter;
    private bool disposed;

    /// <param name="reader">
    /// Readiness probe after login; without one, only <see cref="LoginSettleTimeout"/> marks the character ready.
    /// </param>
    public SnapshotService(JsonSnapshotStore store, IClientState clientState, IFramework framework, IPluginLog log, GameStateReader? reader = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.reader = reader;
        listeners = new ListenerIsolation((listener, ex, held) =>
            log.Warning(ex, "Snapshot listener {Listener} failed ({Held} more failures since the last report); the other listeners still ran", listener, held));

        try
        {
            characters.AddRange(store.List());
        }
        catch (Exception ex)
        {
            // A broken config directory must not keep the plugin from loading; the live character still works in memory.
            characters.Clear();
            log.Warning(ex, "Could not list stored snapshots; starting with none");
        }

        SortCharacters();
        LogStoreWarnings();
        log.Debug("Snapshot store: {Count} character(s)", characters.Count);

        clientState.Login += OnLogin;
        clientState.Logout += OnLogout;
        framework.Update += OnUpdate;

        if (clientState.IsLoggedIn)
        {
            // Hot load with a character in the world: readable on the next tick in the usual case, so the probe marks
            // it ready then; the settle timeout covers a client that is still loading.
            BeginSettle("Hot load");
        }
    }

    /// <summary>Stored characters, newest capture first. Updated on every save and delete.</summary>
    public IReadOnlyList<SnapshotSummary> Characters => characters;

    /// <summary>True between login settling and logout: the client holds a readable character.</summary>
    public bool CharacterReady { get; private set; }

    /// <summary>Raised when <see cref="Characters"/> changed.</summary>
    public event Action? CharactersChanged;

    /// <summary>Raised from the Logout event, before <see cref="CharacterReady"/> turns false, for a last capture.</summary>
    public event Action? LoggingOut;

    /// <summary>
    /// Multibox (D11): whether this client may write a character's snapshot and sidecars. Set by the multibox service;
    /// false only for a logged-in character another client holds with a newer claim. Null allows every write.
    /// </summary>
    public Func<ulong, bool>? MayWrite { get; set; }

    /// <summary><see cref="MayWrite"/> for one character; true when no gate is set.</summary>
    public bool CanWrite(ulong contentId) => MayWrite?.Invoke(contentId) ?? true;

    /// <summary>
    /// Loads a stored character. A snapshot another game client saved and the multibox service already read on its
    /// worker (D11) is served from memory, so the Characters pane's comparison and account view do not read the file
    /// on the draw thread.
    /// </summary>
    public CharacterSnapshot? Load(ulong contentId)
    {
        if (external.TryGetValue(contentId, out var cached))
        {
            return cached;
        }

        // A character live in another game client is that client's file: a corrupt copy is left for it to replace
        // rather than quarantined here (a file a newer plugin wrote is never quarantined by anyone).
        var snapshot = store.Load(contentId, quarantine: LiveElsewhere?.Invoke(contentId) != true);
        LogStoreWarnings();
        return snapshot;
    }

    /// <summary>
    /// Reads a character's stored file as it is on disk, for a worker: nothing is quarantined, no warning is queued and
    /// no framework-thread state is touched. Null when there is none, or it cannot be read here (locked, corrupt, newer).
    /// </summary>
    public CharacterSnapshot? ReadStored(ulong contentId)
    {
        var read = store.LoadShared(contentId);
        return read.Status == SharedLoad.Loaded ? read.Value : null;
    }

    /// <summary>Multibox (D11): whether a character is live in another game client right now. Set by the multibox service.</summary>
    public Func<ulong, bool>? LiveElsewhere { get; set; }

    public void Save(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        store.Save(snapshot);
        Record(snapshot);
    }

    /// <summary>
    /// Writes a snapshot to the store and nothing else: safe on the background writer, which is the only caller.
    /// <see cref="Record"/> updates the list on the framework thread once the write landed.
    /// </summary>
    public void WriteToStore(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        store.Save(snapshot);
    }

    /// <summary>
    /// Multibox (D11), framework thread: the character is now logged in here. A copy another client saved while it was
    /// live there is dropped, so <see cref="Load"/> reads this client's file rather than that older copy.
    /// </summary>
    public void ForgetExternal(ulong contentId) => external.Remove(contentId);

    /// <summary>Framework thread: a snapshot this client wrote is on disk; the list follows it.</summary>
    public void Record(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        external.Remove(snapshot.ContentId);
        characters.RemoveAll(c => c.ContentId == snapshot.ContentId);
        characters.Add(Summarize(snapshot));
        SortCharacters();
        listeners.Raise(CharactersChanged);
    }

    public void Delete(ulong contentId)
    {
        store.Delete(contentId);
        external.Remove(contentId);
        if (characters.RemoveAll(c => c.ContentId == contentId) > 0)
        {
            listeners.Raise(CharactersChanged);
        }
    }

    /// <summary>
    /// Deletes every stored snapshot except those <paramref name="keep"/> selects (multibox, D11: characters live in
    /// another client, whose snapshot that client owns and would write again within seconds).
    /// </summary>
    public void DeleteAll(Func<ulong, bool>? keep = null)
    {
        foreach (var summary in characters.ToArray())
        {
            if (keep?.Invoke(summary.ContentId) == true)
            {
                continue;
            }

            store.Delete(summary.ContentId);
            external.Remove(summary.ContentId);
            characters.Remove(summary);
        }

        listeners.Raise(CharactersChanged);
    }

    /// <summary>
    /// Multibox (D11), framework thread: snapshots another game client saved, read on the multibox worker, and the
    /// characters whose file another client deleted. The list and the in-memory copies follow; this client's own
    /// logged-in character (<paramref name="ownLive"/>) is left alone, its copy in memory being newer. Raises
    /// <see cref="CharactersChanged"/> when the list changed; returns the snapshots taken in.
    /// </summary>
    public IReadOnlyList<CharacterSnapshot> ApplyExternal(IReadOnlyList<CharacterSnapshot> changed, IReadOnlyList<ulong> removed, ulong? ownLive)
    {
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(removed);
        var listChanged = false;
        var taken = new List<CharacterSnapshot>(changed.Count);
        foreach (var snapshot in changed)
        {
            if (snapshot.ContentId == ownLive)
            {
                continue;
            }

            external[snapshot.ContentId] = snapshot;
            taken.Add(snapshot);
            var summary = Summarize(snapshot);
            var index = characters.FindIndex(c => c.ContentId == snapshot.ContentId);
            if (index >= 0 && characters[index] == summary)
            {
                continue;
            }

            if (index >= 0)
            {
                characters.RemoveAt(index);
            }

            characters.Add(summary);
            listChanged = true;
        }

        foreach (var id in removed)
        {
            if (id == ownLive)
            {
                continue;
            }

            external.Remove(id);
            listChanged |= characters.RemoveAll(c => c.ContentId == id) > 0;
        }

        if (listChanged)
        {
            SortCharacters();
            listeners.Raise(CharactersChanged);
        }

        return taken;
    }

    private static SnapshotSummary Summarize(CharacterSnapshot snapshot) =>
        new(snapshot.ContentId, snapshot.Name, snapshot.World, snapshot.TakenUtc, CountBits(snapshot.CompletedBits));

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        clientState.Login -= OnLogin;
        clientState.Logout -= OnLogout;
        framework.Update -= OnUpdate;
        lifetime.Cancel();
        lifetime.Dispose();
    }

    private void OnLogin() => BeginSettle("Login");

    /// <summary>Starts the readiness probe (<see cref="OnUpdate"/>) with the settle timeout as its safety net.</summary>
    private void BeginSettle(string reason)
    {
        var generation = ++loginGeneration;
        CharacterReady = false;
        awaitingCharacter = true;
        log.Debug("{Reason}: waiting for the character to become readable (or {Seconds} s)", reason, LoginSettleTimeout.TotalSeconds);

        framework.RunOnTick(() => OnLoginTimeout(generation), delay: LoginSettleTimeout, cancellationToken: lifetime.Token)
            .ContinueWith(static t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>Cheap per-tick probe while a login is settling; a no-op otherwise.</summary>
    private void OnUpdate(IFramework _)
    {
        if (!awaitingCharacter || disposed || reader is null)
        {
            return;
        }

        bool readable;
        try
        {
            readable = reader.IsCharacterReadable();
        }
        catch (Exception ex)
        {
            // Leave it to the timeout; the poller's own backoff handles a client that keeps throwing.
            awaitingCharacter = false;
            log.Debug(ex, "Readiness probe failed; falling back to the login settle timeout");
            return;
        }

        if (readable)
        {
            log.Debug("Character readable after login; marked ready");
            MarkReady();
        }
    }

    private void OnLoginTimeout(int generation)
    {
        if (disposed || generation != loginGeneration || CharacterReady || !clientState.IsLoggedIn)
        {
            return;
        }

        log.Debug("Login settle timeout reached; character marked ready");
        MarkReady();
    }

    private void OnLogout(int type, int code)
    {
        loginGeneration++;
        awaitingCharacter = false;
        if (CharacterReady)
        {
            // A failing capture is logged by the listener isolation; logout goes on either way.
            listeners.Raise(LoggingOut);
        }

        CharacterReady = false;
    }

    private void MarkReady()
    {
        awaitingCharacter = false;
        CharacterReady = clientState.IsLoggedIn;
    }

    private void SortCharacters() => characters.Sort((a, b) => b.TakenUtc.CompareTo(a.TakenUtc));

    private void LogStoreWarnings()
    {
        foreach (var warning in store.Warnings)
        {
            log.Warning("{Warning}", warning);
        }

        store.ClearWarnings();
    }

    private static int CountBits(byte[] bits)
    {
        var count = 0;
        foreach (var b in bits)
        {
            count += BitOperations.PopCount(b);
        }

        return count;
    }
}
