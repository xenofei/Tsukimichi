using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Characters &amp; data › Characters › "Also read characters from these folders" (plan v7, 1.21.0 P3): other
/// XIVLauncher roaming folders whose Tsukimichi characters the All characters roster lists, read only, marked "other
/// folder" (the standard multibox setup gives each client its own roaming folder). Auto-detect lists the Tsukimichi
/// folders beside this client's and under %AppData%; a folder is added by its path. Nothing in a linked folder is ever
/// written.
/// </summary>
public sealed partial class ConfigWindow
{
    private string linkedPath = string.Empty;
    private List<string>? linkedDetected;
    private string? linkedNote;

    /// <summary>The linked launcher folders' reader; set by the plugin. Null hides the setting.</summary>
    public LinkedFolderService? LinkedFolders { get; set; }

    private void DrawLinkedFolders()
    {
        if (LinkedFolders is not { } linked)
        {
            return;
        }

        if (!Setting(Strings.LinkedFoldersLabel, Strings.LinkedFoldersHint, "multibox roaming folder launcher other client characters roster read", 0f))
        {
            return;
        }

        SettingBelow();
        var remove = -1;
        var folders = settings.LinkedCharacterFolders;
        for (var i = 0; i < folders.Count; i++)
        {
            using var id = ImRaii.PushId(i);
            Chrome.FitText(folders[i], ImGui.GetColorU32(ImGuiCol.Text));
            ImGui.SameLine();
            if (ImGui.SmallButton(Strings.LinkedFoldersRemove))
            {
                remove = i;
            }
        }

        if (remove >= 0)
        {
            linked.Unlink(folders[remove]);
            Save();
        }

        ImGui.SetNextItemWidth(UiMetrics.Px(320f));
        ImGui.InputTextWithHint("##linkedPath", Strings.LinkedFoldersPathHint, ref linkedPath, 512);
        ImGui.SameLine();
        if (ImGui.SmallButton(Strings.LinkedFoldersAdd))
        {
            if (linked.Link(linkedPath))
            {
                linkedPath = string.Empty;
                linkedNote = null;
                Save();
            }
            else
            {
                linkedNote = Strings.LinkedFoldersNotFound;
            }
        }

        ImGui.SameLine();
        if (ImGui.SmallButton(Strings.LinkedFoldersDetect))
        {
            linkedDetected = LinkedFolderService.Detect(linked.OwnConfigDir);
            linkedNote = linkedDetected.Count == 0 ? Strings.LinkedFoldersNoneFound : null;
        }

        if (linkedDetected is { Count: > 0 } found)
        {
            for (var i = 0; i < found.Count; i++)
            {
                if (folders.Contains(found[i], StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                using var id = ImRaii.PushId(1000 + i);
                Chrome.FitText(found[i], ImGui.GetColorU32(ImGuiCol.TextDisabled));
                ImGui.SameLine();
                if (ImGui.SmallButton(Strings.LinkedFoldersAdd) && linked.Link(found[i]))
                {
                    Save();
                }
            }
        }

        if (linkedNote is { } note)
        {
            ImGui.TextColored(Theme.Surface.TextSecondary, note);
        }

        var count = linked.Characters.Count;
        if (folders.Count > 0)
        {
            ImGui.TextColored(Theme.Surface.TextSecondary, count == 1 ? Strings.LinkedFoldersReadOne : string.Format(CultureInfo.CurrentCulture, Strings.LinkedFoldersReadFormat, count));
        }

        EndSetting();
    }
}
