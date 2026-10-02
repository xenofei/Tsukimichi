using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Unique;
using Tsukimichi.GameData;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>One Wotsit search entry: what it shows, what it matches, its icon, what happens when it is picked, and the quest or reward it stands for.</summary>
public sealed record WotsitEntry(string DisplayName, string SearchText, uint IconId, Action Invoke, WotsitItem Item);

/// <summary>
/// Registers every catalog quest and every Moonlit reward with Wotsit (internal name <c>Dalamud.FindAnything</c>)
/// through its IPC: <c>FA.RegisterWithSearch(pluginName, displayName, searchText, iconId) -> guid</c>,
/// <c>FA.UnregisterOne(pluginName, guid)</c>, <c>FA.UnregisterAll(pluginName)</c>, <c>FA.Invoke</c> (message carrying
/// the guid of the picked entry) and <c>FA.Available</c> (message sent when Wotsit loads). Picking an entry reveals
/// the quest in the Journal.
/// <para>
/// Registration is batched on the framework thread: each tick registers as many entries as fit in
/// <see cref="TickBudgetMs"/>, so a few thousand calls never stall a frame, and the total is logged once. A call that
/// throws is retried from the same entry on the next tick (<see cref="BatchCursor"/>); after
/// <see cref="MaxRegisterAttempts"/> failures on one entry the rest of the batch is abandoned with a warning. The
/// entries are rebuilt (after an <c>UnregisterAll</c>) whenever the catalog or the Moonlit catalog is a new instance,
/// and again whenever Wotsit announces itself, since a reloaded Wotsit has forgotten them.
/// </para>
/// <para>
/// Order matters: Wotsit keeps only the first 26 matches per plugin, counted in the order the entries were registered,
/// and sorts by score afterwards (Dalamud.FindAnything <c>PluginSettingsModule.Search</c>), and its default fuzzy
/// match lets a short query hit hundreds of names. So each entry matches on its name alone, and the entries are
/// registered by <see cref="WotsitPriority"/>: quests in the journal or Ready, then Moonlit rewards, then the other
/// open quests, then Completed and Locked out ones (<see cref="WotsitOrder"/>). The groups follow the logged-in
/// character's states, or the viewed character's while nobody is logged in; with no states yet every quest counts as
/// open.
/// </para>
/// <para>
/// Wotsit only appends: an entry registered again moves to the end. When an entry changes group (checked at most once
/// every <see cref="ReorderIntervalMs"/>, and only when the states are a new instance) or its text changes (the
/// spoiler shield moved, the language changed), the longest prefix of the new order that Wotsit already holds in that
/// order stays, and every entry after it is replaced in order, each through <c>FA.UnregisterOne</c> and a new
/// registration (<see cref="RegistrationDiff.KeptPrefix"/>), so Wotsit never holds fewer entries than before. A state
/// change inside a group (accepting a Ready quest) costs nothing. A Wotsit without <c>FA.UnregisterOne</c> gets the
/// full rebuild instead.
/// </para>
/// <para>
/// A masked main scenario quest is registered under its placeholder, without its banner, so Wotsit never finds it by
/// name. Only the masked set of the logged-in character's spoiler shield counts (<see cref="SpoilerMask.Fingerprint"/>;
/// viewing another character changes nothing here).
/// </para>
/// <para>
/// Dalamud's plugin-list event and Wotsit's messages may arrive off the framework thread, so they only raise flags
/// that the next tick acts on.
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

    /// <summary>Consecutive failed <c>FA.RegisterWithSearch</c> calls on one entry before the batch is given up.</summary>
    public const int MaxRegisterAttempts = BatchCursor.DefaultMaxAttempts;

    /// <summary>Shortest time between two checks of whether quest state changes moved an entry to another group.</summary>
    public const long ReorderIntervalMs = 10_000;

    private const string RegisterWithSearchGate = "FA.RegisterWithSearch";
    private const string UnregisterOneGate = "FA.UnregisterOne";
    private const string UnregisterAllGate = "FA.UnregisterAll";
    private const string InvokeGate = "FA.Invoke";
    private const string AvailableGate = "FA.Available";
    private const string IsAvailableGate = "FA.IsAvailable";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly ICallGateSubscriber<string, string, string, uint, string>? registerWithSearch;
    private readonly ICallGateSubscriber<string, string, bool>? unregisterOne;
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
    private Func<SpoilerMask>? spoilers;
    private Func<IReadOnlyDictionary<uint, QuestEvaluation>?>? states;

    private CatalogBundle? registeredBundle;
    private UniqueRewardCatalog? registeredRewards;
    private int registeredSpoilers;

    // The entries registered (or being registered) and each one's guid, by position; a null guid was never
    // registered (the batch gave up) or is between its unregister and its re-registration.
    private List<WotsitEntry>? entries;
    private string?[] guids = [];

    // Each position's group (the registration order is WotsitOrder.Order of these) and when it was registered, which
    // is the order Wotsit holds the entries in; 0 = not registered.
    private WotsitPriority[] priorities = [];
    private long[] sequence = [];
    private long nextSequence;

    // The states the groups were last computed from, and when (Environment.TickCount64), for the reorder rate limit.
    private IReadOnlyDictionary<uint, QuestEvaluation>? orderedStates;
    private long lastOrderCheckMs;

    // The positions the current batch still has to (re)register, and where it is.
    private List<int>? pending;
    private BatchCursor? cursor;
    private int pendingTicks;
    private double pendingMs;
    private bool registered;

    // FA.UnregisterOne failed (a Wotsit without it): spoiler changes rebuild everything until Wotsit reloads.
    private bool unregisterOneUnsupported;
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
            unregisterOne = pluginInterface.GetIpcSubscriber<string, string, bool>(UnregisterOneGate);
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
            unregisterOne = null;
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

    /// <summary>The UI language the registered entries were written in.</summary>
    private int registeredLanguage = -1;

    /// <summary>
    /// Sources for the entries. <paramref name="bundle"/> and <paramref name="rewards"/> are polled each tick and a
    /// new instance of either triggers a rebuild; <paramref name="rewardIcon"/> answers the icon for a reward entry;
    /// <paramref name="reveal"/> runs on the framework thread when an entry is picked; <paramref name="spoilers"/> is
    /// polled each tick too, and a change of its masked set re-registers the entries. <paramref name="states"/> (keyed
    /// by quest row id; null or empty while unknown) sets the registration order; a new instance is looked at no more
    /// than once every <see cref="ReorderIntervalMs"/>.
    /// </summary>
    public void Attach(Func<CatalogBundle?> bundle, Func<UniqueRewardCatalog> rewards, Func<QuestRecord?, UniqueRewardEntry, uint> rewardIcon, Action<QuestRecord> reveal, Func<SpoilerMask>? spoilers = null, Func<IReadOnlyDictionary<uint, QuestEvaluation>?>? states = null)
    {
        this.spoilers = spoilers;
        this.states = states;
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

    /// <summary>
    /// Builds the entry list from the catalog and the Moonlit catalog (<see cref="WotsitOrder.Items"/>), in catalog
    /// order: quests, then rewards. Pure. Each entry matches on its name alone. A quest <paramref name="spoilers"/> masks
    /// is listed under its placeholder, searchable by it alone, and without its banner.
    /// </summary>
    public static List<WotsitEntry> BuildEntries(
        CatalogBundle bundle,
        UniqueRewardCatalog rewards,
        Func<QuestRecord?, UniqueRewardEntry, uint> rewardIcon,
        Action<QuestRecord> reveal,
        SpoilerMask? spoilers = null)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(rewards);
        ArgumentNullException.ThrowIfNull(rewardIcon);
        ArgumentNullException.ThrowIfNull(reveal);
        spoilers ??= SpoilerMask.None;

        var items = WotsitOrder.Items(bundle.Catalog, rewards, Strings.MoonlitKindName, bundle.Language, spoilers);
        var entries = new List<WotsitEntry>(items.Count);
        foreach (var item in items)
        {
            var target = item.Quest;
            if (item.Reward is { } reward)
            {
                entries.Add(new WotsitEntry(
                    string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.WotsitRewardFormat, item.Name, Strings.MoonlitKindName(reward.Kind)),
                    item.SearchText,
                    rewardIcon(target, reward),
                    () => reveal(target),
                    item));
            }
            else
            {
                entries.Add(new WotsitEntry(
                    string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.WotsitQuestFormat, item.Name),
                    item.SearchText,
                    spoilers.IsMasked(target) ? 0 : target.Icon,
                    () => reveal(target),
                    item));
            }
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
        var currentSpoilers = spoilers?.Invoke() ?? SpoilerMask.None;
        var sameCatalogs = registered && entries is not null
            && ReferenceEquals(currentBundle, registeredBundle) && ReferenceEquals(currentRewards, registeredRewards);
        if (sameCatalogs && currentSpoilers.Fingerprint == registeredSpoilers && registeredLanguage == Localization.Loc.Version)
        {
            Reorder();
            return;
        }

        // A language switch (V2-19) changes the entries' "Quest: …" and "Reward: …" text, and so takes the same paths.
        registeredLanguage = Localization.Loc.Version;

        var next = BuildEntries(currentBundle, currentRewards, rewardIcon, reveal, currentSpoilers);

        // Same catalogs, another mask: the list has the same shape and the entries keep their groups, so only those
        // whose text or icon changed are replaced, with whatever follows them in the order (an MSQ completion
        // unmasks one quest).
        if (sameCatalogs && CanReplaceOne && entries!.Count == next.Count && priorities.Length == next.Count)
        {
            registeredSpoilers = currentSpoilers.Fingerprint;
            var previous = entries;
            entries = next;
            StartBatch(Replacements(previous));
            return;
        }

        // A new catalog (or a Moonlit rebuild after an override) replaces every entry; the old guids die with
        // UnregisterAll.
        Unregister();
        registeredBundle = currentBundle;
        registeredRewards = currentRewards;
        registeredSpoilers = currentSpoilers.Fingerprint;
        entries = next;
        guids = new string?[next.Count];
        sequence = new long[next.Count];
        orderedStates = CurrentStates();
        lastOrderCheckMs = Environment.TickCount64;
        priorities = orderedStates is { } known
            ? WotsitOrder.Priorities(next, EntryItem, known)
            : WotsitOrder.Unevaluated(next, EntryItem);
        StartBatch(WotsitOrder.Order(priorities));
        registered = true;
    }

    /// <summary>
    /// Quest states changed: when the states are a new instance and <see cref="ReorderIntervalMs"/> has passed since
    /// the last check, recomputes every entry's group and, if any entry changed group, re-registers what the new order
    /// needs. Unknown states (logged out with nothing viewed, or a first pass still running) keep the order as it is.
    /// </summary>
    private void Reorder()
    {
        if (entries is null || CurrentStates() is not { } current || ReferenceEquals(current, orderedStates))
        {
            return;
        }

        var now = Environment.TickCount64;
        if (now - lastOrderCheckMs < ReorderIntervalMs)
        {
            return;
        }

        lastOrderCheckMs = now;
        orderedStates = current;
        var next = WotsitOrder.Priorities(entries, EntryItem, current);
        if (!WotsitOrder.GroupsChanged(priorities, next))
        {
            return;
        }

        if (!CanReplaceOne)
        {
            // A full rebuild on the next tick, which reads these same states.
            registered = false;
            return;
        }

        priorities = next;
        var replace = Replacements(entries);
        log.Debug("Wotsit: entries changed group; re-registering {Count} of {Total} in order", replace.Count, entries.Count);
        StartBatch(replace);
    }

    /// <summary>
    /// The positions to register again, in order, so that Wotsit ends up holding <see cref="entries"/> in the order
    /// of <see cref="priorities"/>: everything after the longest prefix it already holds in that order, unchanged
    /// since <paramref name="previous"/>.
    /// </summary>
    private List<int> Replacements(List<WotsitEntry> previous)
    {
        var current = entries!;
        var order = WotsitOrder.Order(priorities);
        var kept = RegistrationDiff.KeptPrefix(order, sequence, position => SameEntry(previous[position], current[position]));
        return order.GetRange(kept, order.Count - kept);
    }

    /// <summary>The states that set the order, or null while none are known.</summary>
    private IReadOnlyDictionary<uint, QuestEvaluation>? CurrentStates() =>
        states?.Invoke() is { Count: > 0 } current ? current : null;

    private bool CanReplaceOne => !unregisterOneUnsupported && unregisterOne is not null;

    private static WotsitItem EntryItem(WotsitEntry entry) => entry.Item;

    /// <summary>Two entries register the same way in Wotsit: same text, search text and icon. The action is not compared (same target by position).</summary>
    private static bool SameEntry(WotsitEntry a, WotsitEntry b) =>
        a.IconId == b.IconId
        && string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal)
        && string.Equals(a.SearchText, b.SearchText, StringComparison.Ordinal);

    private void StartBatch(List<int> positions)
    {
        pending = positions.Count > 0 ? positions : null;
        cursor = pending is null ? null : new BatchCursor(pending.Count, MaxRegisterAttempts);
        pendingTicks = 0;
        pendingMs = 0;
    }

    /// <summary>
    /// Registers the pending positions until the tick budget is spent; finishes the list over as many ticks as needed.
    /// A position that still holds a guid (a spoiler update) is first unregistered with <c>FA.UnregisterOne</c>. A
    /// register call that throws ends the tick and is retried from that entry next tick; once the cursor gives up on
    /// an entry, the batch is dropped with the entries registered so far kept (a partial list beats none) until the
    /// catalog or Wotsit itself changes. An <c>FA.UnregisterOne</c> that throws turns partial updates off and queues a
    /// full rebuild.
    /// </summary>
    private void RegisterBatch()
    {
        if (pending is null || cursor is null || entries is null || registerWithSearch is null)
        {
            return;
        }

        batchClock.Restart();
        pendingTicks++;
        while (!cursor.IsDone)
        {
            var position = pending[cursor.Index];
            var entry = entries[position];
            if (guids[position] is { } old)
            {
                try
                {
                    unregisterOne?.InvokeFunc(PluginName, old);
                }
                catch (Exception ex)
                {
                    // An older Wotsit without FA.UnregisterOne (or one that went away: the full rebuild's
                    // UnregisterAll finds that out). Either way everything is registered again from scratch.
                    log.Debug(ex, "Wotsit FA.UnregisterOne failed; rebuilding every entry");
                    unregisterOneUnsupported = true;
                    pending = null;
                    cursor = null;
                    registered = false;
                    return;
                }

                actions.Remove(old);
                guids[position] = null;
                sequence[position] = 0;
            }

            try
            {
                var guid = registerWithSearch.InvokeFunc(PluginName, entry.DisplayName, entry.SearchText, entry.IconId);
                if (!string.IsNullOrEmpty(guid))
                {
                    actions[guid] = entry.Invoke;
                    guids[position] = guid;
                    sequence[position] = ++nextSequence;
                }
            }
            catch (IpcNotReadyError)
            {
                // Wotsit went away mid-batch; FA.Available brings the rest back.
                wotsitLoaded = false;
                pending = null;
                cursor = null;
                registered = false;
                return;
            }
            catch (Exception ex)
            {
                if (cursor.Fail())
                {
                    log.Warning(ex, "Wotsit FA.RegisterWithSearch failed {Attempts} times at entry {Index} of {Count}; giving up on the rest", cursor.Attempts, cursor.Index, cursor.Count);
                    pending = null;
                    cursor = null;
                }
                else
                {
                    log.Debug(ex, "Wotsit FA.RegisterWithSearch failed at entry {Index} of {Count} (attempt {Attempts}); retrying next tick", cursor.Index, cursor.Count, cursor.Attempts);
                }

                pendingMs += batchClock.Elapsed.TotalMilliseconds;
                return;
            }

            cursor.Advance();
            if (batchClock.Elapsed.TotalMilliseconds >= TickBudgetMs)
            {
                break;
            }
        }

        pendingMs += batchClock.Elapsed.TotalMilliseconds;
        if (cursor.IsDone)
        {
            log.Information("Wotsit: registered {Count} entries in {Ms:0.0} ms over {Ticks} tick(s)", pending.Count, pendingMs, pendingTicks);
            pending = null;
            cursor = null;
        }
    }

    private void Unregister()
    {
        pending = null;
        cursor = null;
        actions.Clear();
        entries = null;
        guids = [];
        sequence = [];
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

    /// <summary>
    /// Forgets every registered guid and queues a full rebuild on the next tick (framework thread only). A reloaded
    /// Wotsit may be a newer one, so partial updates are tried again.
    /// </summary>
    private void ResetRegistration()
    {
        actions.Clear();
        pending = null;
        cursor = null;
        entries = null;
        guids = [];
        sequence = [];
        registered = false;
        unregisterOneUnsupported = false;
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
