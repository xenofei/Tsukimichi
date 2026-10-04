using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// "Send 9 to Questionable" on an alt goal's card (plan v7, 1.21.0 N11): the primary pill opening the same send menu
/// (Add, Add and start, Replace…) and confirmations as every other "Send to Questionable", with the quests the goal's
/// card counts as doable now. Only the character logged in on this client ever shows it (the card says why otherwise).
/// </summary>
public sealed partial class QuestionableActions
{
    /// <summary>The primary pill with <paramref name="label"/>, opening the send menu; disabled without Questionable.</summary>
    public void DrawPill<T>(string host, string id, string label, T state, Func<T, IEnumerable<uint>> rowIds)
    {
        ArgumentNullException.ThrowIfNull(rowIds);
        using var scope = ImRaii.PushId(id);
        var available = ipc.Available;
        if (Chrome.ActionPill("##sendPill", SendIcon, label, PillTone.Primary, available, available ? Strings.QuestionableSendTooltip : Strings.QuestionableNeedsPlugin, PillLayout.Row))
        {
            ImGui.OpenPopup(MenuId);
        }

        DrawMenuPopup(host, state, rowIds);
    }
}
