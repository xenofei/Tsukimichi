using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Model;
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
/// <see cref="Enabled"/> follows <c>Configuration.ItemContextMenuEnabled</c> and subscribes or unsubscribes the
/// <see cref="IContextMenu.OnMenuOpened"/> handler; <see cref="Dispose"/> unsubscribes for good.
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

    private bool enabled;
    private bool disposed;
    private bool warned;

    /// <param name="reveal">Opens the main window on the quest; called on the framework thread from the menu click.</param>
    public ItemHooks(IContextMenu contextMenu, RewardLookupSource lookup, Action<QuestRecord> reveal, IPluginLog log)
    {
        this.contextMenu = contextMenu ?? throw new ArgumentNullException(nameof(contextMenu));
        this.lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        this.reveal = reveal ?? throw new ArgumentNullException(nameof(reveal));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Whether the handler is subscribed (default off until the plugin applies the setting).</summary>
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
            if (value)
            {
                contextMenu.OnMenuOpened += OnMenuOpened;
            }
            else
            {
                contextMenu.OnMenuOpened -= OnMenuOpened;
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Enabled = false;
        disposed = true;
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
            if (entries.Count == 0)
            {
                return;
            }

            var quests = DistinctQuests(current, entries);
            if (quests.Count == 0)
            {
                return;
            }

            args.AddMenuItem(BuildMenuItem(quests));
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

    private MenuItem BuildMenuItem(List<QuestRecord> quests)
    {
        if (quests.Count == 1)
        {
            var quest = quests[0];
            return new MenuItem
            {
                Name = string.Format(CultureInfo.CurrentCulture, Strings.ItemsMenuSingleFormat, quest.Name),
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
                Name = quest.Name,
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
