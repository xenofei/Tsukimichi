using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Unique;
using Tsukimichi.GameData;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>One Wotsit search entry: what it shows, what it matches, its icon and what happens when it is picked.</summary>
public sealed record WotsitEntry(string DisplayName, string SearchText, uint IconId, Action Invoke);

/// <summary>
/// Registers every catalog quest and every Moonlit reward with Wotsit (internal name <c>Dalamud.FindAnything</c>)
/// through its IPC: <c>FA.RegisterWithSearch(pluginName, displayName, searchText, iconId) -> guid</c>,
/// <c>FA.UnregisterAll(pluginName)</c>, <c>FA.Invoke</c> (message carrying the guid of the picked entry) and
/// <c>FA.Available</c> (message sent when Wotsit loads). Picking an entry reveals the quest in the Journal.
/// <para>
/// Registration is batched on the framework thread: each tick registers as many entries as fit in
/// <see cref="TickBudgetMs"/>, so a few thousand calls never stall a frame, and the total is logged once. The
/// entries are rebuilt (after an <c>UnregisterAll</c>) whenever the catalog or the Moonlit catalog is a new instance,
/// and again whenever Wotsit announces itself, since a reloaded Wotsit has forgotten them. Dalamud's plugin-list
/// event and Wotsit's messages may arrive off the framework thread, so they only raise flags that the next tick acts on.
/// </para>
/// <para>
/// <see cref="Enabled"/> follows the <c>Configuration.WotsitIntegration</c> setting: the plugin sets it after
/// construction and the settings window's checkbox flips it; turning it off unregisters everything at once.
/// </para>
/// </summary>
public sealed class WotsitIpc : IDisposable
{
    public const string PluginName = "Tsukimichi";
    public const string WotsitInternalName = "Dalamud.FindAnything";

    /// <summary>Wall time one tick may spend registering before the rest waits for the next tick.</summary>
    public const double TickBudgetMs = 4.0;

    private const string RegisterWithSearchGate = "FA.RegisterWithSearch";
    private const string UnregisterAllGate = "FA.UnregisterAll";
    private const string InvokeGate = "FA.Invoke";
    private const string AvailableGate = "FA.Available";
    private const string IsAvailableGate = "FA.IsAvailable";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly ICallGateSubscriber<string, string, string, uint, string>? registerWithSearch;
    private readonly ICallGateSubscriber<string, bool>? unregisterAll;
    private readonly ICallGateSubscriber<string, bool>? invoke;
    private readonly ICallGateSubscriber<bool>? available;
    private readonly ICallGateSubscriber<bool>? isAvailable;

    private readonly Dictionary<string, Action> actions = new(StringComparer.Ordinal);
    private readonly Stopwatch batchClock = new();

    private Func<CatalogBundle?>? bundle;
    private Func<UniqueRewardCatalog>? rewards;
    private Func<QuestRecord?, UniqueRewardEntry, uint>? rewardIcon;
    private Action<QuestRecord>? reveal;

    private CatalogBundle? registeredBundle;
    private UniqueRewardCatalog? registeredRewards;
    private List<WotsitEntry>? pending;
    private int pendingIndex;
    private int pendingTicks;
    private double pendingMs;
    private bool registered;
    private bool wotsitLoaded;
    private bool warned;
    private bool disposed;

    // Raised off the framework thread (ActivePluginsChanged, FA.Available) and consumed at the top of OnUpdate, so
    // pending/actions are only ever touched on the framework thread.
    private volatile bool pluginListDirty;
    private volatile bool wotsitAnnounced;

    public WotsitIpc(IDalamudPluginInterface pluginInterface, IFramework framework, IPluginLog log)
    {
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.log = log ?? throw new ArgumentNullException(nameof(log));

        try
        {
            registerWithSearch = pluginInterface.GetIpcSubscriber<string, string, string, uint, string>(RegisterWithSearchGate);
            unregisterAll = pluginInterface.GetIpcSubscriber<string, bool>(UnregisterAllGate);
            invoke = pluginInterface.GetIpcSubscriber<string, bool>(InvokeGate);
            available = pluginInterface.GetIpcSubscriber<bool>(AvailableGate);
            isAvailable = pluginInterface.GetIpcSubscriber<bool>(IsAvailableGate);
            invoke.Subscribe(OnInvoke);
            available.Subscribe(OnWotsitAvailable);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Wotsit IPC subscribers unavailable");
            registerWithSearch = null;
            unregisterAll = null;
            invoke = null;
            available = null;
            isAvailable = null;
        }

        pluginInterface.ActivePluginsChanged += OnActivePluginsChanged;
        wotsitLoaded = IsWotsitLoaded();
        framework.Update += OnUpdate;
    }

    /// <summary>Whether entries are registered at all (default on). Turning it off unregisters every entry now; turning it on registers them again on the next tick.</summary>
    public bool Enabled
    {
        get => enabled;
        set
        {
            if (enabled == value)
            {
                return;
            }

            enabled = value;
            if (!value)
            {
                Unregister();
            }
            else
            {
                registered = false;
            }
        }
    }

    private bool enabled = true;

    /// <summary>True while Wotsit is installed and loaded.</summary>
    public bool Available => registerWithSearch is not null && wotsitLoaded;

    /// <summary>Entries currently registered (or queued), for diagnostics.</summary>
    public int EntryCount => actions.Count;

    /// <summary>
    /// Sources for the entries. <paramref name="bundle"/> and <paramref name="rewards"/> are polled each tick and a
    /// new instance of either triggers a rebuild; <paramref name="rewardIcon"/> answers the icon for a reward entry;
    /// <paramref name="reveal"/> runs on the framework thread when an entry is picked.
    /// </summary>
    public void Attach(Func<CatalogBundle?> bundle, Func<UniqueRewardCatalog> rewards, Func<QuestRecord?, UniqueRewardEntry, uint> rewardIcon, Action<QuestRecord> reveal)
    {
        this.bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
        this.rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
        this.rewardIcon = rewardIcon ?? throw new ArgumentNullException(nameof(rewardIcon));
        this.reveal = reveal ?? throw new ArgumentNullException(nameof(reveal));
        registered = false;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        framework.Update -= OnUpdate;
        pluginInterface.ActivePluginsChanged -= OnActivePluginsChanged;
        try
        {
            invoke?.Unsubscribe(OnInvoke);
            available?.Unsubscribe(OnWotsitAvailable);
        }
        catch (Exception ex)
        {
            log.Debug(ex, "Wotsit IPC unsubscribe failed");
        }

        Unregister();
    }

    /// <summary>Builds the entry list from the catalog and the Moonlit catalog. Pure; exposed for tests of the labels.</summary>
    public static List<WotsitEntry> BuildEntries(
        CatalogBundle bundle,
        UniqueRewardCatalog rewards,
        Func<QuestRecord?, UniqueRewardEntry, uint> rewardIcon,
        Action<QuestRecord> reveal)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(rewards);
        ArgumentNullException.ThrowIfNull(rewardIcon);
        ArgumentNullException.ThrowIfNull(reveal);

        var catalog = bundle.Catalog;
        var entries = new List<WotsitEntry>(catalog.Count + rewards.Count);
        foreach (var quest in catalog.All)
        {
            var target = quest;
            var expansion = bundle.Names.Expansion(quest.Expansion);
            entries.Add(new WotsitEntry(
                Strings.WotsitQuestPrefix + quest.Name,
                quest.Name + " " + quest.Journal.GenreName + " " + expansion,
                quest.Icon,
                () => reveal(target)));
        }

        foreach (var entry in rewards.All)
        {
            if (catalog.GetByRowId(entry.QuestRowId) is not { } quest)
            {
                continue;
            }

            var target = quest;
            var kind = Strings.MoonlitKindName(entry.Kind);
            var name = string.IsNullOrWhiteSpace(entry.RewardName) ? kind : entry.RewardName;
            entries.Add(new WotsitEntry(
                Strings.WotsitRewardPrefix + name + " (" + kind + ")",
                name + " " + kind + " " + quest.Name,
                rewardIcon(quest, entry),
                () => reveal(target)));
        }

        return entries;
    }

    private void OnUpdate(IFramework _)
    {
        if (disposed)
        {
            return;
        }

        if (pluginListDirty)
        {
            pluginListDirty = false;
            var loaded = IsWotsitLoaded();
            if (loaded != wotsitLoaded)
            {
                wotsitLoaded = loaded;
                if (!loaded)
                {
                    ResetRegistration();
                }
            }
        }

        if (wotsitAnnounced)
        {
            wotsitAnnounced = false;
            wotsitLoaded = true;
            ResetRegistration();
        }

        if (!enabled || !Available || bundle is null || rewards is null || rewardIcon is null || reveal is null)
        {
            return;
        }

        if (pending is not null)
        {
            RegisterBatch();
            return;
        }

        var currentBundle = bundle();
        if (currentBundle is null)
        {
            return;
        }

        var currentRewards = rewards();
        if (registered && ReferenceEquals(currentBundle, registeredBundle) && ReferenceEquals(currentRewards, registeredRewards))
        {
            return;
        }

        // A new catalog (or a Moonlit rebuild after an override) replaces every entry; the old guids die with UnregisterAll.
        Unregister();
        registeredBundle = currentBundle;
        registeredRewards = currentRewards;
        pending = BuildEntries(currentBundle, currentRewards, rewardIcon, reveal);
        pendingIndex = 0;
        pendingTicks = 0;
        pendingMs = 0;
        registered = true;
    }

    /// <summary>Registers entries until the tick budget is spent; finishes the list over as many ticks as needed.</summary>
    private void RegisterBatch()
    {
        if (pending is null || registerWithSearch is null)
        {
            return;
        }

        batchClock.Restart();
        pendingTicks++;
        while (pendingIndex < pending.Count)
        {
            var entry = pending[pendingIndex];
            try
            {
                var guid = registerWithSearch.InvokeFunc(PluginName, entry.DisplayName, entry.SearchText, entry.IconId);
                if (!string.IsNullOrEmpty(guid))
                {
                    actions[guid] = entry.Invoke;
                }
            }
            catch (IpcNotReadyError)
            {
                // Wotsit went away mid-batch; FA.Available brings the rest back.
                wotsitLoaded = false;
                pending = null;
                registered = false;
                return;
            }
            catch (Exception ex)
            {
                WarnOnce(ex, "Wotsit FA.RegisterWithSearch failed; registration stopped");
                pending = null;
                return;
            }

            pendingIndex++;
            if (batchClock.Elapsed.TotalMilliseconds >= TickBudgetMs)
            {
                break;
            }
        }

        pendingMs += batchClock.Elapsed.TotalMilliseconds;
        if (pendingIndex >= pending.Count)
        {
            log.Information("Wotsit: registered {Count} entries in {Ms:0.0} ms over {Ticks} tick(s)", pending.Count, pendingMs, pendingTicks);
            pending = null;
        }
    }

    private void Unregister()
    {
        pending = null;
        actions.Clear();
        if (unregisterAll is null || !wotsitLoaded)
        {
            return;
        }

        try
        {
            unregisterAll.InvokeFunc(PluginName);
        }
        catch (IpcNotReadyError)
        {
            wotsitLoaded = false;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Wotsit FA.UnregisterAll failed");
        }
    }

    /// <summary>Wotsit picked an entry. The guid lookup happens on the framework thread, the only place <see cref="actions"/> is touched.</summary>
    private void OnInvoke(string guid)
    {
        if (disposed)
        {
            return;
        }

        if (framework.IsInFrameworkUpdateThread)
        {
            Run(guid);
        }
        else
        {
            framework.RunOnFrameworkThread(() => Run(guid)).ContinueWith(static t => _ = t.Exception, System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    private void Run(string guid)
    {
        if (disposed || !actions.TryGetValue(guid, out var action))
        {
            return;
        }

        try
        {
            action();
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Wotsit entry action failed");
        }
    }

    /// <summary>Wotsit (re)loaded: everything must be registered again. Acted on by the next tick.</summary>
    private void OnWotsitAvailable() => wotsitAnnounced = true;

    /// <summary>Dalamud's plugin list changed: the next tick re-checks whether Wotsit is loaded.</summary>
    private void OnActivePluginsChanged(IActivePluginsChangedEventArgs args) => pluginListDirty = true;

    /// <summary>Forgets every registered guid and queues a full rebuild on the next tick (framework thread only).</summary>
    private void ResetRegistration()
    {
        actions.Clear();
        pending = null;
        registered = false;
    }

    private bool IsWotsitLoaded()
    {
        try
        {
            foreach (var plugin in pluginInterface.InstalledPlugins)
            {
                if (plugin.IsLoaded && string.Equals(plugin.InternalName, WotsitInternalName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Installed plugin list unavailable");
        }

        // The plugin list can lag a load; the IsAvailable func is the authority when it answers.
        if (isAvailable is not null)
        {
            try
            {
                return isAvailable.InvokeFunc();
            }
            catch (IpcNotReadyError)
            {
                return false;
            }
            catch (Exception ex)
            {
                log.Debug(ex, "Wotsit FA.IsAvailable failed");
            }
        }

        return false;
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
