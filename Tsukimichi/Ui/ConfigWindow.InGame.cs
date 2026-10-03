using System;

namespace Tsukimichi.Ui;

/// <summary>
/// The 1.7.0 "In the game" settings (feature plan v5), placed by the 1.13.0 rebuild (v6 U7): Settings › Alerts › Chat
/// extras (the clickable "[Open] [Pin] [Route]", the "Opened:" line, quest toasts), read per use; and Settings › In
/// game › Menus and tooltips (the NPC menu entry, item hints and the item menu, the quests that need an item, Chat 2's
/// menu and nameplate marks). The ones that subscribe and unsubscribe go through their callbacks.
/// </summary>
public sealed partial class ConfigWindow
{
    /// <summary>Settings › In game › Chat 2 changed; the plugin registers or unregisters with Chat 2.</summary>
    public Action<bool>? ChatTwoToggled { get; set; }

    /// <summary>Settings › In game › nameplate marks changed; the plugin subscribes or unsubscribes the nameplate hook.</summary>
    public Action<bool>? NamePlateMarksToggled { get; set; }

    private void DrawChatActions()
    {
        Header(Strings.ConfigSectionChatActions);
        var actions = settings.ChatLinkActions;
        if (Toggle(Strings.ConfigChatLinkActions, Strings.ConfigChatLinkActionsHint, ref actions, "chat links open pin route click"))
        {
            settings.ChatLinkActions = actions;
            Save();
        }

        var opened = settings.ChatNoticeOpened;
        if (Toggle(Strings.ConfigChatNoticeOpened, Strings.ConfigChatNoticeOpenedHint, ref opened, "chat notice opened completed turn-in unlocked available"))
        {
            settings.ChatNoticeOpened = opened;
            Save();
        }

        var toasts = settings.QuestToasts;
        if (Toggle(Strings.ConfigQuestToasts, Strings.ConfigQuestToastsHint, ref toasts, "toast banner moonlit reward duty unlocked"))
        {
            settings.QuestToasts = toasts;
            Save();
        }
    }

    /// <summary>
    /// Settings › In game › Menus and tooltips: the NPC menu entry, the item hint, the item menu entry, the quests that
    /// need an item, Chat 2's menu and nameplate marks.
    /// </summary>
    private void DrawMenusAndTooltips()
    {
        Header(Strings.ConfigSectionGameWindows);
        var npcMenu = settings.NpcContextMenuEnabled;
        if (Toggle(Strings.ConfigNpcContextMenu, Strings.ConfigNpcContextMenuHint, ref npcMenu, "npc target menu quest giver right-click"))
        {
            settings.NpcContextMenuEnabled = npcMenu;
            Save();
            NpcContextMenuToggled?.Invoke(npcMenu);
        }

        var hints = settings.ItemHintsEnabled;
        if (Toggle(Strings.ConfigItemHints, Strings.ConfigItemHintsHint, ref hints, "item tooltip reward hover hint"))
        {
            settings.ItemHintsEnabled = hints;
            Save();
            ItemHintsToggled?.Invoke(hints);
        }

        var contextMenu = settings.ItemContextMenuEnabled;
        if (Toggle(Strings.ConfigItemContextMenu, Strings.ConfigItemContextMenuHint, ref contextMenu, "item right-click menu reward"))
        {
            settings.ItemContextMenuEnabled = contextMenu;
            Save();
            ItemContextMenuToggled?.Invoke(contextMenu);
        }

        var neededFor = settings.ItemNeededForEnabled;
        if (Toggle(Strings.ConfigItemNeededFor, Strings.ConfigItemNeededForHint, ref neededFor, "hand-in items needed quest item hint menu"))
        {
            settings.ItemNeededForEnabled = neededFor;
            Save();
        }

        var chatTwo = settings.ChatTwoIntegration;
        if (Toggle(Strings.ConfigChatTwo, Strings.ConfigChatTwoHint, ref chatTwo, "chat 2 chattwo context menu integrations quest item link"))
        {
            settings.ChatTwoIntegration = chatTwo;
            Save();
            ChatTwoToggled?.Invoke(chatTwo);
        }

        var namePlates = settings.NamePlateMarks;
        if (Toggle(Strings.ConfigNamePlateMarks, Strings.ConfigNamePlateMarksHint, ref namePlates, "nameplate name plate title npc giver marks moonlit pinned"))
        {
            settings.NamePlateMarks = namePlates;
            Save();
            NamePlateMarksToggled?.Invoke(namePlates);
        }
    }

    /// <summary>Settings › In game › Wotsit: register quests and rewards as Wotsit search entries.</summary>
    private void DrawWotsit()
    {
        Header(Strings.ConfigSectionWotsit);
        var wotsit = settings.WotsitIntegration;
        if (Toggle(Strings.ConfigWotsitIntegration, Strings.ConfigWotsitIntegrationHint, ref wotsit, "wotsit search launcher"))
        {
            settings.WotsitIntegration = wotsit;
            Save();
            WotsitToggled?.Invoke(wotsit);
        }
    }
}
