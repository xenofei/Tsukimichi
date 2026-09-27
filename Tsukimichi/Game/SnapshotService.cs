using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Game;

/// <summary>
/// Owns the snapshot store and the login lifecycle. After <see cref="IClientState.Login"/> the character is not
/// readable at once: <see cref="CharacterReady"/> turns true on the first framework tick where the player is loaded
/// with a content id (see <see cref="GameStateReader.IsCharacterReadable"/>), or after <see cref="LoginSettleTimeout"/>
/// as a safety net. It tells the poller when it may capture. A character already logged in at load is ready at once.
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
            // Hot load with a character in the world: its state is already populated, no need to wait.
            CharacterReady = true;
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

    public CharacterSnapshot? Load(ulong contentId)
    {
        var snapshot = store.Load(contentId);
        LogStoreWarnings();
        return snapshot;
    }

    public void Save(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        store.Save(snapshot);
        characters.RemoveAll(c => c.ContentId == snapshot.ContentId);
        characters.Add(new SnapshotSummary(snapshot.ContentId, snapshot.Name, snapshot.World, snapshot.TakenUtc, CountBits(snapshot.CompletedBits)));
        SortCharacters();
        CharactersChanged?.Invoke();
    }

    public void Delete(ulong contentId)
    {
        store.Delete(contentId);
        if (characters.RemoveAll(c => c.ContentId == contentId) > 0)
        {
            CharactersChanged?.Invoke();
        }
    }

    public void DeleteAll()
    {
        foreach (var summary in characters.ToArray())
        {
            store.Delete(summary.ContentId);
        }

        characters.Clear();
        CharactersChanged?.Invoke();
    }

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

    private void OnLogin()
    {
        var generation = ++loginGeneration;
        CharacterReady = false;
        awaitingCharacter = true;
        log.Debug("Login: waiting for the character to become readable (or {Seconds} s)", LoginSettleTimeout.TotalSeconds);

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
            try
            {
                LoggingOut?.Invoke();
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Logout capture failed");
            }
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
