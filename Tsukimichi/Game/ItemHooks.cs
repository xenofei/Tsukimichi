using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
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
/// "quest rewards (N)" with a submenu listing them. <see cref="MenuTargetInventory"/> menus (inventory, armoury,
/// saddlebag, retainers and the like) carry their item.
/// <para>
/// 1.7.0: so does an item link in the game's own chat log (R8 E, C6 #4). Its menu is a default one (AddonName
/// "ChatLog") whose item Dalamud does not expose; the item is <c>AgentChatLog.ContextItemId</c> (FFXIVClientStructs,
/// offset 0x9C0), read only when the uint 8 bytes after it says the menu is an item's (3), the check GatherBuddy makes
/// (Ottermandias/GatherBuddy 1e39592, <c>Plugin/ContextMenu.cs</c> HandleChatLog), since the field keeps the last
/// item after a player-name or tab menu. The raw id is reduced to the base item (HQ and collectable offsets). Behind
/// the same <see cref="HookGate"/> as the rest, so a patch that moves the field pauses it.
/// </para>
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

    /// <summary>"Re-buyable · 100 gil" for a quest reward a shop sells back (item id, quest row id); null leaves the line out (1.19, C6).</summary>
    public Func<uint, uint, string?>? BuyBack { get; set; }

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
            var itemId = args.Target is MenuTargetInventory { TargetItem: { } item } ? item.BaseItemId : ChatLogItemId(args);
            if (itemId == 0)
            {
                return;
            }

            var current = lookup.Current;
            var entries = current.ByItem(itemId);
            var quests = entries.Count == 0 ? [] : DistinctQuests(current, entries);
            if (quests.Count > 0)
            {
                args.AddMenuItem(BuildMenuItem(quests));

                // Safe to discard? "Re-buyable · 100 gil" when a shop sells the reward back (1.19, C6): a line, not an action.
                if (BuyBack?.Invoke(itemId, quests[0].RowId) is { Length: > 0 } buyBack)
                {
                    args.AddMenuItem(new MenuItem { Name = buyBack, PrefixChar = PrefixLetter, IsEnabled = false });
                }
            }

            if (NeededForEnabled?.Invoke() != false && HandIns is { } handIns && NeededStates?.Invoke() is { } states)
            {
                var needed = handIns.Current.NeededFor(itemId, states);
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

    /// <summary>The offset from <c>ContextItemId</c> to the uint that says what the chat menu was opened on; 3 is an item link.</summary>
    private const int ChatLogContextKindOffset = 8;

    private const uint ChatLogContextKindItem = 3;

    /// <summary>The base item id of a chat log item link's menu, or 0 for any other menu.</summary>
    private static unsafe uint ChatLogItemId(IMenuOpenedArgs args)
    {
        if (args.AddonName != "ChatLog" || args.Target is not MenuTargetDefault)
        {
            return 0;
        }

        var agent = AgentChatLog.Instance();
        if (agent == null)
        {
            return 0;
        }

        var raw = agent->ContextItemId;
        if (raw == 0 || *(uint*)((nint)(&agent->ContextItemId) + ChatLogContextKindOffset) != ChatLogContextKindItem)
        {
            return 0;
        }

        var (itemId, _) = ItemUtil.GetBaseId(raw);
        return itemId;
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
