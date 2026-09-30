using System;
using System.Diagnostics;
using System.IO;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Export;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Data › Export (P12): format (JSON or CSV), "Include character name" (off by default), the full-journal
/// option, the output folder (empty for the default <c>exports</c> folder), "Export completed quests" and "Export
/// Moonlit collection", then the path written last with an "Open folder" button. Settings save as they change.
/// </summary>
public sealed class ExportSection(Configuration settings, ExportService exports, Action save, IPluginLog log)
{
    private const int FolderMaxLength = 512;

    private string? line;
    private bool lineOk;

    public void Draw()
    {
        using var id = ImRaii.PushId("export");
        ImGui.TextUnformatted(Strings.ExportHeader);
        ImGui.TextWrapped(Strings.ExportIntro);

        ImGui.TextUnformatted(Strings.ExportFormatLabel);
        ImGui.SameLine();
        if (ImGui.RadioButton(Strings.ExportFormatJson, settings.ExportFormat == ExportFormat.Json) && settings.ExportFormat != ExportFormat.Json)
        {
            settings.ExportFormat = ExportFormat.Json;
            save();
        }

        ImGui.SameLine();
        if (ImGui.RadioButton(Strings.ExportFormatCsv, settings.ExportFormat == ExportFormat.Csv) && settings.ExportFormat != ExportFormat.Csv)
        {
            settings.ExportFormat = ExportFormat.Csv;
            save();
        }

        var includeName = settings.ExportIncludeCharacterName;
        if (ImGui.Checkbox(Strings.ExportIncludeName, ref includeName))
        {
            settings.ExportIncludeCharacterName = includeName;
            save();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Strings.ExportIncludeNameHint);
        }

        var incomplete = settings.ExportIncludeIncomplete;
        if (ImGui.Checkbox(Strings.ExportIncludeIncomplete, ref incomplete))
        {
            settings.ExportIncludeIncomplete = incomplete;
            save();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Strings.ExportIncludeIncompleteHint);
        }

        DrawFolder();

        if (ImGui.Button(Strings.ExportQuests))
        {
            Show(exports.Export(ExportKind.Quests));
        }

        ImGui.SameLine();
        if (ImGui.Button(Strings.ExportMoonlit))
        {
            Show(exports.Export(ExportKind.Moonlit));
        }

        DrawLastLine();
    }

    private void DrawFolder()
    {
        var folder = settings.ExportFolder ?? string.Empty;
        ImGui.SetNextItemWidth(260f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputTextWithHint(Strings.ExportFolderLabel + "##folder", exports.Folder, ref folder, FolderMaxLength))
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
            ImGui.SetTooltip(Strings.ExportFolderDefaultTooltip);
        }
    }

    /// <summary>The path written last (or why nothing was), with "Open folder" beside a success.</summary>
    private void DrawLastLine()
    {
        if (line is null)
        {
            return;
        }

        using (Theme.PushText(lineOk ? Theme.Moon : Theme.Eclipse))
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

    private void OpenFolder(string folder)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            log.Warning(ex, "Could not open {Folder}", folder);
            line = Strings.ExportOpenFolderFailed + ex.Message;
            lineOk = false;
        }
    }
}
