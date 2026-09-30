using System;
using System.IO;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Export;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>The outcome of one export: the file written, or why none was.</summary>
public sealed record ExportResult(bool Ok, string? Path, string Message);

/// <summary>
/// Writes the P12 exports of the viewed character (Settings › Data › Export and <c>/tsuki export</c>): the completed
/// quests from its snapshot, and the Moonlit collection with each reward's obtained state as the Moonlit pane reads it
/// (<see cref="RewardUnlockReader"/>: flag-backed kinds are known only for the logged-in character). Format, folder,
/// the character-name opt-in and the full-journal option come from <see cref="Configuration"/>. Local files only;
/// never an upload. Runs on the framework thread (the unlock flags are read there).
/// </summary>
public sealed class ExportService(
    SessionState session,
    Configuration settings,
    PluginPaths paths,
    RewardUnlockReader unlocks,
    Func<UniqueRewardCatalog> rewards,
    string pluginVersion,
    string gameVersion,
    IPluginLog log)
{
    /// <summary>The last file written this session, for the Settings line; null before the first.</summary>
    public string? LastPath { get; private set; }

    /// <summary>
    /// The folder exports go to: the configured one, or <see cref="PluginPaths.ExportsDir"/> when none is set. A
    /// relative folder is taken under the plugin's config directory, never the game's working directory.
    /// </summary>
    public string Folder
    {
        get
        {
            var folder = settings.ExportFolder?.Trim();
            if (string.IsNullOrEmpty(folder))
            {
                return paths.ExportsDir;
            }

            return Path.IsPathRooted(folder) ? folder : Path.Combine(paths.ConfigDir, folder);
        }
    }

    public ExportResult Export(ExportKind kind) => Export(kind, settings.ExportFormat);

    public ExportResult Export(ExportKind kind, ExportFormat format)
    {
        if (session.Bundle is not { } bundle)
        {
            return new ExportResult(false, null, Strings.ExportCatalogLoading);
        }

        if (session.ViewedSnapshot is not { } snapshot)
        {
            return new ExportResult(false, null, Strings.ExportNoCharacter);
        }

        var header = ExportWriter.Header(pluginVersion, gameVersion, DateTime.UtcNow, snapshot, settings.ExportIncludeCharacterName);
        string content;
        if (kind == ExportKind.Quests)
        {
            var rows = ExportWriter.QuestRows(bundle.Catalog, snapshot, id => bundle.Names.Expansion(id), settings.ExportIncludeIncomplete);
            content = format == ExportFormat.Json ? ExportWriter.QuestsJson(header, rows) : ExportWriter.QuestsCsv(rows);
        }
        else
        {
            var rows = ExportWriter.MoonlitRows(rewards().View(null, unlocks.IsObtained));
            content = format == ExportFormat.Json ? ExportWriter.MoonlitJson(header, rows) : ExportWriter.MoonlitCsv(rows);
        }

        try
        {
            var path = ExportWriter.Write(Folder, ExportWriter.FileName(kind, format, header), content);
            LastPath = path;
            log.Information("Exported {Kind} as {Format} to {Path}", kind, format, path);
            return new ExportResult(true, path, Strings.ExportWrittenPrefix + path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            log.Warning(ex, "Export of {Kind} failed", kind);
            return new ExportResult(false, null, Strings.ExportFailedPrefix + ex.Message);
        }
    }
}
