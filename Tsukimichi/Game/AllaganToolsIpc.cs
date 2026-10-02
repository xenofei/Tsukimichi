using System;
using System.Collections.Generic;
using System.Threading;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;

namespace Tsukimichi.Game;

/// <summary>
/// Item counts through Allagan Tools' IPC (internal name <c>InventoryTools</c>; gates as registered in
/// Critical-Impact/InventoryTools <c>InventoryTools/IPC/IPCService.cs</c> at 70a9f41, 2026-09-13):
/// <list type="bullet">
/// <item><c>AllaganTools.IsInitialized() -&gt; bool</c></item>
/// <item><c>AllaganTools.CurrentCharacter() -&gt; ulong</c>: the active character's id (its content id).</item>
/// <item><c>AllaganTools.ItemCountOwnedByCategory(uint itemId, bool currentCharacterOnly, uint[] inventoryCategories,
/// bool includeSharedStorage) -&gt; uint</c>: NQ and HQ, an empty category array counting every category (bags, armoury,
/// saddlebag, armoire, glamour dresser, retainers).</item>
/// <item><c>AllaganTools.GetItemCountsByCharacter(same arguments) -&gt; Dictionary&lt;ulong, uint&gt;</c>: the same,
/// per owner (the character and each retainer).</item>
/// <item>Messages <c>AllaganTools.ItemAdded</c> / <c>ItemRemoved</c> (a tuple whose second member is Allagan Tools'
/// own flags enum, so it is subscribed as <see cref="object"/> and its content ignored), <c>RetainerChanged</c>
/// (<c>ulong?</c>) and <c>Initialized</c> (<c>bool</c>).</item>
/// </list>
/// Read-only: nothing here changes Allagan Tools' lists. Counts are always for the active character and its retainers
/// (<c>currentCharacterOnly</c> true, shared storage left out). Answers are cached per item and dropped whenever
/// <see cref="Generation"/> moves: one of the messages above, or Dalamud's plugin list changing. Every call is wrapped;
/// a gate that is not ready reads as unavailable, any other failure as unknown, logged once.
/// </summary>
public sealed class AllaganToolsIpc : IDisposable
{
    public const string PluginInternalName = "InventoryTools";
    private const string IsInitializedGate = "AllaganTools.IsInitialized";
    private const string CurrentCharacterGate = "AllaganTools.CurrentCharacter";
    private const string CountOwnedByCategoryGate = "AllaganTools.ItemCountOwnedByCategory";
    private const string CountsByCharacterGate = "AllaganTools.GetItemCountsByCharacter";
    private const string ItemAddedMessage = "AllaganTools.ItemAdded";
    private const string ItemRemovedMessage = "AllaganTools.ItemRemoved";
    private const string RetainerChangedMessage = "AllaganTools.RetainerChanged";
    private const string InitializedMessage = "AllaganTools.Initialized";

    private static readonly uint[] EveryCategory = [];

    private readonly IPluginLog log;
    private readonly PluginPresence presence;
    private readonly ICallGateSubscriber<bool>? isInitialized;
    private readonly ICallGateSubscriber<ulong>? currentCharacter;
    private readonly ICallGateSubscriber<uint, bool, uint[], bool, uint>? countOwned;
    private readonly ICallGateSubscriber<uint, bool, uint[], bool, Dictionary<ulong, uint>>? countsByCharacter;
    private readonly List<(ICallGateSubscriber<object?, bool> Gate, Action<object?> Handler)> messages = [];
    private readonly Dictionary<uint, ItemCounts?> counts = [];

    // Raised by Allagan Tools' messages, which it sends from its own threads; consumed on the next read.
    private int changes;
    private int consumedChanges;
    private bool? available;

    // The plugin-list generation in which a count gate answered "not ready" (an Allagan Tools before 1.15.0.12): until
    // the list changes again it is treated as unavailable, rather than asked again every frame.
    private int gateMissingIn = -1;
    private int consumedPresence = -1;
    private bool warned;
    private bool disposed;

    public AllaganToolsIpc(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        presence = new PluginPresence(pluginInterface, log, PluginInternalName);
        try
        {
            isInitialized = pluginInterface.GetIpcSubscriber<bool>(IsInitializedGate);
            currentCharacter = pluginInterface.GetIpcSubscriber<ulong>(CurrentCharacterGate);
            countOwned = pluginInterface.GetIpcSubscriber<uint, bool, uint[], bool, uint>(CountOwnedByCategoryGate);
            countsByCharacter = pluginInterface.GetIpcSubscriber<uint, bool, uint[], bool, Dictionary<ulong, uint>>(CountsByCharacterGate);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Allagan Tools IPC subscribers unavailable");
        }

        foreach (var name in new[] { ItemAddedMessage, ItemRemovedMessage, RetainerChangedMessage, InitializedMessage })
        {
            try
            {
                var gate = pluginInterface.GetIpcSubscriber<object?, bool>(name);
                Action<object?> handler = _ => Interlocked.Increment(ref changes);
                gate.Subscribe(handler);
                messages.Add((gate, handler));
            }
            catch (Exception ex)
            {
                log.Debug(ex, "Allagan Tools message {Message} could not be subscribed; counts refresh on plugin changes only", name);
            }
        }
    }

    /// <summary>The active character's count of an item: held by the character itself, and by its retainers.</summary>
    /// <param name="Total">Everything the character and its retainers hold, every category.</param>
    /// <param name="Character">What the character itself holds (bags, armoury, saddlebag, armoire, dresser).</param>
    public readonly record struct ItemCounts(uint Total, uint Character)
    {
        /// <summary>What the retainers hold.</summary>
        public uint Retainers => Total > Character ? Total - Character : 0;
    }

    /// <summary>True while Allagan Tools is loaded and says it is initialized. Cached until <see cref="Generation"/> moves.</summary>
    public bool Available
    {
        get
        {
            Sync();
            available ??= AskAvailable();
            return available.Value;
        }
    }

    private bool AskAvailable()
    {
        if (disposed || countOwned is null || !presence.Loaded || gateMissingIn == presence.Generation)
        {
            return false;
        }

        try
        {
            return isInitialized?.InvokeFunc() ?? true;
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "AllaganTools.IsInitialized failed");
            return false;
        }
    }

    /// <summary>Moves when Allagan Tools reports an inventory or retainer change, initializes, or comes or goes.</summary>
    public int Generation
    {
        get
        {
            Sync();
            return consumedChanges + consumedPresence;
        }
    }

    /// <summary>
    /// How many of <paramref name="itemId"/> the active character and its retainers hold (NQ and HQ); null when Allagan
    /// Tools is absent, not initialized or failed. Cached until <see cref="Generation"/> moves. Framework thread.
    /// </summary>
    public ItemCounts? Counts(uint itemId)
    {
        if (itemId == 0)
        {
            return null;
        }

        Sync();
        if (counts.TryGetValue(itemId, out var cached))
        {
            return cached;
        }

        var result = Available ? Query(itemId) : null;
        counts[itemId] = result;
        return result;
    }

    /// <summary>The total of <see cref="Counts"/>; null when unknown.</summary>
    public uint? CountOwned(uint itemId) => Counts(itemId)?.Total;

    private ItemCounts? Query(uint itemId)
    {
        try
        {
            var total = countOwned!.InvokeFunc(itemId, true, EveryCategory, false);
            var character = 0u;
            if (total > 0 && countsByCharacter is not null && currentCharacter is not null)
            {
                var self = currentCharacter.InvokeFunc();
                var byOwner = countsByCharacter.InvokeFunc(itemId, true, EveryCategory, false);
                character = byOwner is not null && byOwner.TryGetValue(self, out var own) ? own : 0;
            }
            else if (total > 0)
            {
                character = total;
            }

            return new ItemCounts(total, Math.Min(character, total));
        }
        catch (IpcNotReadyError)
        {
            // An Allagan Tools without the gate (before 1.15.0.12) or mid-reload: unknown until the plugin list changes.
            gateMissingIn = presence.Generation;
            available = false;
            return null;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Allagan Tools item count failed");
            return null;
        }
    }

    /// <summary>Drops the cached counts when a message arrived or the plugin list changed since the last read.</summary>
    private void Sync()
    {
        _ = presence.Loaded;
        var seen = Volatile.Read(ref changes);
        var generation = presence.Generation;
        if (seen != consumedChanges || generation != consumedPresence)
        {
            consumedChanges = seen;
            consumedPresence = generation;
            counts.Clear();
            available = null;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        foreach (var (gate, handler) in messages)
        {
            try
            {
                gate.Unsubscribe(handler);
            }
            catch (Exception ex)
            {
                log.Debug(ex, "Allagan Tools message unsubscribe failed");
            }
        }

        messages.Clear();
        presence.Dispose();
    }

    private void WarnOnce(Exception ex, string message)
    {
        if (warned)
        {
            log.Debug(ex, message);
            return;
        }

        warned = true;
        log.Warning(ex, message);
    }
}
