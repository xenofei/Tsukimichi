using System;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Ui;

/// <summary>
/// "Since you were away…" (P7) on the Characters dashboard: a small button beside the capture line that opens the
/// card above the detail pane for the viewed character, whether or not it opened on its own at login.
/// </summary>
public sealed partial class CharactersPane
{
    /// <summary>Opens "Since you were away" for a character (content id); set by the plugin. Null hides the button.</summary>
    public Action<ulong>? OpenWelcomeBack { get; set; }

    private void DrawWelcomeBackButton(CharacterSnapshot snapshot)
    {
        if (OpenWelcomeBack is not { } open)
        {
            return;
        }

        ImGui.SameLine();
        if (ImGui.SmallButton(Strings.WelcomeBackOpen))
        {
            open(snapshot.ContentId);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.WelcomeBackOpenTooltip);
        }
    }
}
