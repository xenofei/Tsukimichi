using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.HandIn;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Unique;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// Adds "Tsukimichi: quest reward (quest)" to the context menu of an inventory item that is a quest-exclusive reward
/// (V2-14). Clicking it opens the main window on that quest; an item several quests hand out gets
/// "quest rewards (N)" with a submenu listing them. Only <see cref="MenuTargetInventory"/> menus (inventory,
/// armoury, saddlebag, retainers and the like) carry an item; a default menu such as a chat item link exposes
/// none, so nothing is added there.
/// <para>
/// 1.6.0: an item an open quest asks for (in the journal, or ready to take; <see cref="HandInIndex"/>) also gets
/// "Tsukimichi: needed for (quest)", or "needed for quests (N)" with a submenu, while <see cref="NeededForEnabled"/>
/// says so. The quest names follow the same spoiler shield as the reward entry.
/// </para>
/// <para>
/// <see cref="Enabled"/> follows <c>Configuration.ItemContextMenuEnabled</c>; the <see cref="IContextMenu.OnMenuOpened"/>
/// handler is subscribed while it is on and the shared <see cref="HookGate"/> (the addon kill switch, T20) allows game
/// hooks, and follows the gate's changes. <see cref="Dispose"/> unsubscribes for good.
/// </para>
/// </summary>
public sealed class ItemHooks : IDisposable
{
    /// <summary>Boxed letter shown before the entry in place of Dalamud's default red D.</summary>
    public const char PrefixLetter = 'T';

    private readonly IContextMenu contextMenu;
    private readonly RewardLookupSource lookup;
    private readonly Action<QuestRecord> reveal;
    private readonly IPluginLog log;

    private readonly HookGate gate;
    private bool enabled;
    private bool subscribed;
    private bool disposed;
    private bool warned;

    /// <param name="reveal">Opens the main window on the quest; called on the framework thread from the menu click.</param>
    /// <param name="gate">The shared addon kill switch; the handler stays off while it pauses game hooks.</param>
    public ItemHooks(IContextMenu contextMenu, RewardLookupSource lookup, Action<QuestRecord> reveal, HookGate gate, IPluginLog log)
    {
        this.contextMenu = contextMenu ?? throw new ArgumentNullException(nameof(contextMenu));
        this.lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        this.reveal = reveal ?? throw new ArgumentNullException(nameof(reveal));
        this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        gate.Changed += Apply;
    }

    /// <summary>
    /// The name a quest prints in the menu (the session's spoiler shield), attached by the plugin; the quest's own name
    /// until then.
    /// </summary>
    public Func<QuestRecord, string>? QuestName { get; set; }

    /// <summary>Item to the quests that ask for it; null leaves the "needed for" entry out.</summary>
    public HandInIndexSource? HandIns { get; set; }

    /// <summary>The logged-in character's quest states (the item is in its inventory); null leaves the entry out.</summary>
    public Func<IReadOnlyDictionary<uint, QuestEvaluation>>? NeededStates { get; set; }

    /// <summary>Reads Settings › "Say which open quests need an item"; null reads as on.</summary>
    public Func<bool>? NeededForEnabled { get; set; }

    /// <summary>
    /// The player's setting (default off until the plugin applies it). The handler is subscribed only while this is on
    /// and the <see cref="HookGate"/> allows game hooks on the running game version.
    /// </summary>
    public bool Enabled
    {
        get => enabled;
        set
        {
            if (enabled == value || disposed)
            {
                return;
            }

            enabled = value;
            Apply();
        }
    }

    /// <summary>Whether the handler is subscribed now (the setting is on and the gate allows it).</summary>
    public bool IsActive => subscribed;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        gate.Changed -= Apply;
        Apply();
    }

    /// <summary>Subscribes or unsubscribes the handler to match the setting and the gate. Framework thread.</summary>
    private void Apply()
    {
        var want = enabled && !disposed && gate.HooksAllowed;
        if (want == subscribed)
        {
            return;
        }

        subscribed = want;
        if (want)
        {
            contextMenu.OnMenuOpened += OnMenuOpened;
        }
        else
        {
            contextMenu.OnMenuOpened -= OnMenuOpened;
        }
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        try
        {
            if (args.Target is not MenuTargetInventory { TargetItem: { } item })
            {
                return;
            }

            var current = lookup.Current;
            var entries = current.ByItem(item.BaseItemId);
            var quests = entries.Count == 0 ? [] : DistinctQuests(current, entries);
            if (quests.Count > 0)
            {
                args.AddMenuItem(BuildMenuItem(quests));
            }

            if (NeededForEnabled?.Invoke() != false && HandIns is { } handIns && NeededStates?.Invoke() is { } states)
            {
                var needed = handIns.Current.NeededFor(item.BaseItemId, states);
                if (needed.Count > 0)
                {
                    args.AddMenuItem(BuildNeededItem(needed));
                }
            }
        }
        catch (Exception ex)
        {
            if (!warned)
            {
                warned = true;
                log.Warning(ex, "Item context menu entry failed");
            }
        }
    }

    /// <summary>The quests behind the entries, each once, in entry order; entries without a quest record (catalog not loaded) are skipped.</summary>
    private static List<QuestRecord> DistinctQuests(RewardLookup current, IReadOnlyList<UniqueRewardEntry> entries)
    {
        var quests = new List<QuestRecord>(entries.Count);
        foreach (var entry in entries)
        {
            var quest = current.QuestFor(entry);
            if (quest is null)
            {
                continue;
            }

            var seen = false;
            foreach (var known in quests)
            {
                if (known.RowId == quest.RowId)
                {
                    seen = true;
                    break;
                }
            }

            if (!seen)
            {
                quests.Add(quest);
            }
        }

        return quests;
    }

    /// <summary>"needed for (quest)", or "needed for quests (N)" opening one line per quest.</summary>
    private MenuItem BuildNeededItem(IReadOnlyList<QuestRecord> quests)
    {
        if (quests.Count == 1)
        {
            var quest = quests[0];
            return new MenuItem
            {
                Name = string.Format(CultureInfo.CurrentCulture, Strings.ItemsMenuNeededFormat, QuestName?.Invoke(quest) ?? quest.Name),
                PrefixChar = PrefixLetter,
                OnClicked = _ => reveal(quest),
            };
        }

        var subItems = new MenuItem[quests.Count];
        for (var i = 0; i < subItems.Length; i++)
        {
            var quest = quests[i];
            subItems[i] = new MenuItem
            {
                Name = QuestName?.Invoke(quest) ?? quest.Name,
                PrefixChar = PrefixLetter,
                OnClicked = _ => reveal(quest),
            };
        }

        return new MenuItem
        {
            Name = string.Format(CultureInfo.CurrentCulture, Strings.ItemsMenuNeededManyFormat, quests.Count),
            PrefixChar = PrefixLetter,
            IsSubmenu = true,
            OnClicked = clicked => clicked.OpenSubmenu(subItems),
        };
    }

    private MenuItem BuildMenuItem(List<QuestRecord> quests)
    {
        if (quests.Count == 1)
        {
            var quest = quests[0];
            return new MenuItem
            {
                Name = string.Format(CultureInfo.CurrentCulture, Strings.ItemsMenuSingleFormat, QuestName?.Invoke(quest) ?? quest.Name),
                PrefixChar = PrefixLetter,
                OnClicked = _ => reveal(quest),
            };
        }

        var subItems = new MenuItem[quests.Count];
        for (var i = 0; i < subItems.Length; i++)
        {
            var quest = quests[i];
            subItems[i] = new MenuItem
            {
                Name = QuestName?.Invoke(quest) ?? quest.Name,
                PrefixChar = PrefixLetter,
                OnClicked = _ => reveal(quest),
            };
        }

        return new MenuItem
        {
            Name = string.Format(CultureInfo.CurrentCulture, Strings.ItemsMenuManyFormat, quests.Count),
            PrefixChar = PrefixLetter,
            IsSubmenu = true,
            OnClicked = clicked => clicked.OpenSubmenu(subItems),
        };
    }
}
