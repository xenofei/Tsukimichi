using System;
using System.Diagnostics;
using System.IO;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Export;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Characters &amp; data › Export (P12): the output folder field (empty for the default <c>exports</c>
/// folder), "Export completed quests" and "Export Moonlit collection", then the path written last with an "Open
/// folder" button. The format and the two options are Settings rows the window draws itself (feature plan v6 U7).
/// </summary>
public sealed class ExportSection(Configuration settings, ExportService exports, Action save, IPluginLog log)
{
    private const int FolderMaxLength = 512;

    private string? line;
    private bool lineOk;

    // The folder field's hint (the resolved export folder), rebuilt when the folder text changes rather than per frame.
    private string? hintFolder;
    private string hint = string.Empty;

    /// <summary>Writes the viewed character's completed quests (or every quest) and shows the result under the buttons.</summary>
    public void ExportQuests() => Show(exports.Export(ExportKind.Quests));

    /// <summary>Writes the viewed character's Moonlit collection and shows the result under the buttons.</summary>
    public void ExportMoonlit() => Show(exports.Export(ExportKind.Moonlit));

    /// <summary>
    /// The two export buttons on one line (wrapping when narrow), then the path written last (or why nothing was) with
    /// "Open folder" beside a success.
    /// </summary>
    public void DrawButtons()
    {
        using var id = ImRaii.PushId("export");
        if (ImGui.Button(Strings.ExportQuests))
        {
            ExportQuests();
        }

        Chrome.SameLineOrWrap(ImGui.CalcTextSize(Strings.ExportMoonlit).X + (ImGui.GetStyle().FramePadding.X * 2f));
        if (ImGui.Button(Strings.ExportMoonlit))
        {
            ExportMoonlit();
        }

        DrawLastLine();
    }

    /// <summary>The output folder field, <paramref name="width"/> wide, with Default beside it once a folder is typed; saved when the field is left.</summary>
    public void DrawFolder(float width)
    {
        var folder = settings.ExportFolder ?? string.Empty;
        if (hintFolder is null || !string.Equals(hintFolder, folder, StringComparison.Ordinal))
        {
            hintFolder = folder;
            hint = exports.Folder;
        }

        // Default sits beside the field once a folder is typed, inside the same width.
        var style = ImGui.GetStyle();
        var room = string.IsNullOrEmpty(settings.ExportFolder) ? width : width - ImGui.CalcTextSize(Strings.ExportFolderDefault).X - (style.FramePadding.X * 2f) - style.ItemSpacing.X;
        ImGui.SetNextItemWidth(MathF.Max(UiMetrics.Px(60f), room));
        if (ImGui.InputTextWithHint("##exportFolder", hint, ref folder, FolderMaxLength))
        {
            settings.ExportFolder = folder;
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            save();
        }

        if (string.IsNullOrEmpty(settings.ExportFolder))
        {
            return;
        }

        ImGui.SameLine();
        if (ImGui.SmallButton(Strings.ExportFolderDefault))
        {
            settings.ExportFolder = string.Empty;
            save();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.ExportFolderDefaultTooltip);
        }
    }

    /// <summary>The path written last (or why nothing was), with "Open folder" beside a success.</summary>
    private void DrawLastLine()
    {
        if (line is null)
        {
            return;
        }

        using (Theme.PushText(lineOk ? Theme.Silver : Theme.EclipseText))
        {
            ImGui.TextWrapped(line);
        }

        if (!lineOk || exports.LastPath is not { } path)
        {
            return;
        }

        if (ImGui.SmallButton(Strings.ExportOpenFolder))
        {
            OpenFolder(Path.GetDirectoryName(path) ?? exports.Folder);
        }
    }

    private void Show(ExportResult result)
    {
        line = result.Message;
        lineOk = result.Ok;
    }

    /// <summary>
    /// Opens <paramref name="folder"/> in Explorer: explorer.exe with the path as its one argument, so no shell verb or
    /// file association is involved. Any failure lands in the status line instead of escaping into the draw.
    /// </summary>
    private void OpenFolder(string folder)
    {
        try
        {
            var start = new ProcessStartInfo { FileName = "explorer.exe", UseShellExecute = false };
            start.ArgumentList.Add(folder);
            using var process = Process.Start(start);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not open {Folder}", folder);
            line = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.ExportOpenFolderFailedFormat, ex.Message);
            lineOk = false;
        }
    }
}
