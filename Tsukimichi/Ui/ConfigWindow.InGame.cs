using System;
using Dalamud.Bindings.ImGui;

namespace Tsukimichi.Ui;

/// <summary>
/// The 1.7.0 "In the game" settings (feature plan v5), two blocks: Settings › Notices › Chat actions (the clickable
/// "[Open] [Pin] [Route]", the "Opened:" line, quest toasts), read per use; and Settings › Integrations › Chat 2 and
/// nameplates ("Open in Tsukimichi" in Chat 2's menu, nameplate marks), which subscribe and unsubscribe, so their
/// changes go through <see cref="ChatTwoToggled"/> and <see cref="NamePlateMarksToggled"/>.
/// </summary>
public sealed partial class ConfigWindow
{
    /// <summary>Settings › Integrations › Chat 2 changed; the plugin registers or unregisters with Chat 2.</summary>
    public Action<bool>? ChatTwoToggled { get; set; }

    /// <summary>Settings › Integrations › nameplate marks changed; the plugin subscribes or unsubscribes the nameplate hook.</summary>
    public Action<bool>? NamePlateMarksToggled { get; set; }

    private void DrawChatActions()
    {
        Header(Strings.ConfigSectionChatActions);
        if (Row(Strings.ConfigChatLinkActions, Strings.ConfigChatLinkActionsHint, "chat links open pin route click"))
        {
            var actions = settings.ChatLinkActions;
            if (ImGui.Checkbox(Strings.ConfigChatLinkActions, ref actions))
            {
                settings.ChatLinkActions = actions;
                Save();
            }

            HintOnHover(Strings.ConfigChatLinkActionsHint);
        }

        if (Row(Strings.ConfigChatNoticeOpened, Strings.ConfigChatNoticeOpenedHint, "chat notice opened completed turn-in unlocked available"))
        {
            var opened = settings.ChatNoticeOpened;
            if (ImGui.Checkbox(Strings.ConfigChatNoticeOpened, ref opened))
            {
                settings.ChatNoticeOpened = opened;
                Save();
            }

            HintOnHover(Strings.ConfigChatNoticeOpenedHint);
        }

        if (Row(Strings.ConfigQuestToasts, Strings.ConfigQuestToastsHint, "toast banner moonlit reward duty unlocked"))
        {
            var toasts = settings.QuestToasts;
            if (ImGui.Checkbox(Strings.ConfigQuestToasts, ref toasts))
            {
                settings.QuestToasts = toasts;
                Save();
            }

            HintOnHover(Strings.ConfigQuestToastsHint);
        }
    }

    private void DrawChatTwoAndNamePlates()
    {
        Header(Strings.ConfigSectionChatTwoNamePlates);
        if (Row(Strings.ConfigChatTwo, Strings.ConfigChatTwoHint, "chat 2 chattwo context menu integrations quest item link"))
        {
            var chatTwo = settings.ChatTwoIntegration;
            if (ImGui.Checkbox(Strings.ConfigChatTwo, ref chatTwo))
            {
                settings.ChatTwoIntegration = chatTwo;
                Save();
                ChatTwoToggled?.Invoke(chatTwo);
            }

            HintOnHover(Strings.ConfigChatTwoHint);
        }

        if (Row(Strings.ConfigNamePlateMarks, Strings.ConfigNamePlateMarksHint, "nameplate name plate title npc giver marks moonlit pinned"))
        {
            var namePlates = settings.NamePlateMarks;
            if (ImGui.Checkbox(Strings.ConfigNamePlateMarks, ref namePlates))
            {
                settings.NamePlateMarks = namePlates;
                Save();
                NamePlateMarksToggled?.Invoke(namePlates);
            }

            HintOnHover(Strings.ConfigNamePlateMarksHint);
        }
    }
}
