using System;
using System.IO;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Game;

/// <summary>
/// Fills <see cref="DiagnosticInputs"/> from the session for "Report this quest" (feature plan v3 T18) and composes
/// the block on demand: the plugin version from the assembly, the client's game version (read once at load), the
/// data versions the loaders already keep (the <c>unique_quests.json</c> header, <c>curated/VERSION.json</c>) and the
/// viewed character's evaluation. Also owns the data stamp Settings › About and the status bar tooltip show, and the
/// warning when the reward data was generated for another game version than the one running.
/// </summary>
public sealed class DiagnosticBuilder
{
    /// <summary>The base game repository, whose version is the client's game version.</summary>
    private const string BaseRepository = "ffxiv";

    /// <summary>The version file beside the sqpack directory, the fallback when the repository carries no version.</summary>
    private const string GameVersionFile = "ffxivgame.ver";

    private readonly SessionState session;
    private readonly Func<JournalFiling> filing;

    /// <param name="filing">Reads the current journal filing setting, so the block says which mode produced the quest's genre.</param>
    public DiagnosticBuilder(SessionState session, string pluginVersion, string clientGameVersion, Func<JournalFiling> filing)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.filing = filing ?? throw new ArgumentNullException(nameof(filing));
        PluginVersion = pluginVersion ?? string.Empty;
        ClientGameVersion = clientGameVersion ?? string.Empty;

        var rewards = session.UniqueRewards;
        DataStampLine = DataStamp.Line(rewards.GameVersion, rewards.Entries.Count, rewards.GeneratedUtc == default ? null : rewards.GeneratedUtc, session.Curated.CuratedRevision);
        VersionMismatchWarning = DataStamp.MismatchWarning(rewards.GameVersion, ClientGameVersion);
    }

    public string PluginVersion { get; }

    public string ClientGameVersion { get; }

    /// <summary>"Data: game 2026.09.15 · unique rewards 3,464 (generated 2026-09-28) · curated 573d225", composed once.</summary>
    public string DataStampLine { get; }

    /// <summary>"Reward data was generated for game X; you are on Y", or null when the versions match or are unknown.</summary>
    public string? VersionMismatchWarning { get; }

    /// <summary>The block for a quest by row id; null without a catalog or for a row id it does not know.</summary>
    public string? Compose(uint rowId) => session.Bundle?.Catalog.GetByRowId(rowId) is { } quest ? Compose(quest) : null;

    /// <summary>The block for a quest, from the viewed character's evaluation (or none when no character is viewed). Allocates; call on click only.</summary>
    public string Compose(QuestRecord quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var snapshot = session.ViewedSnapshot;
        session.States.TryGetValue(quest.RowId, out var evaluation);
        session.Curated.Quirks.TryGetValue(quest.RowId, out var quirk);
        var rewards = session.UniqueRewards;
        return QuestDiagnostic.Compose(new DiagnosticInputs
        {
            PluginVersion = PluginVersion,
            ClientGameVersion = ClientGameVersion,
            DataGameVersion = rewards.GameVersion,
            DataGeneratedUtc = rewards.GeneratedUtc == default ? null : rewards.GeneratedUtc,
            CuratedRevision = session.Curated.CuratedRevision,
            SnapshotSchema = snapshot?.SchemaVersion ?? CharacterSnapshot.CurrentSchemaVersion,
            Quest = quest,
            FilingRule = DiagnosticInputs.DescribeFiling(quest, filing()),
            Evaluation = evaluation,
            QuirkNote = quirk?.Note,
            Names = session.Names,
            States = session.States.Count > 0 ? session.States : null,
            Snapshot = snapshot,
            Context = session.Context,
            IsLive = session.IsLive,
        });
    }

    /// <summary>Puts text on the system clipboard through ImGui; false (and a log line) when the platform refused.</summary>
    public static bool TryCopy(string text, IPluginLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        try
        {
            ImGui.SetClipboardText(text);
            return true;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Clipboard write failed");
            return false;
        }
    }

    /// <summary>The plugin's assembly version ("0.6.0.0"); empty when the assembly carries none.</summary>
    public static string PluginVersionText() => typeof(DiagnosticBuilder).Assembly.GetName().Version?.ToString() ?? string.Empty;

    /// <summary>
    /// The client's game version: the base repository's version from Lumina, else the <c>ffxivgame.ver</c> file
    /// beside the sqpack directory; empty when neither can be read (the warning and the block then say unknown).
    /// </summary>
    public static string ReadClientGameVersion(IDataManager data, IPluginLog log)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(log);
        try
        {
            if (data.GameData.Repositories.TryGetValue(BaseRepository, out var repository) && repository.Version is { Length: > 0 } version)
            {
                return version.Trim();
            }
        }
        catch (Exception ex)
        {
            log.Debug(ex, "Game version not readable from the {Repository} repository", BaseRepository);
        }

        try
        {
            var gameDir = data.GameData.DataPath.Parent?.FullName;
            if (gameDir is not null)
            {
                var path = Path.Combine(gameDir, GameVersionFile);
                if (File.Exists(path))
                {
                    return File.ReadAllText(path).Trim();
                }
            }
        }
        catch (Exception ex)
        {
            log.Debug(ex, "Game version not readable from {File}", GameVersionFile);
        }

        return string.Empty;
    }
}
