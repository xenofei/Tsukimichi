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

    /// <summary>The assembly metadata key the csproj writes the tested game version under.</summary>
    private const string TestedGameVersionKey = "TestedGameVersion";

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
        mismatchWarning = DataStamp.MismatchWarning(rewards.GameVersion, ClientGameVersion);
    }

    private string? mismatchWarning;
    private int mismatchNewQuests;

    public string PluginVersion { get; }

    public string ClientGameVersion { get; }

    /// <summary>"Data: game 2026.09.15 · unique rewards 3,464 (generated 2026-09-28) · curated 573d225", composed once.</summary>
    public string DataStampLine { get; }

    /// <summary>
    /// The "Game updated" report the main window's strip shows (<see cref="DataFreshnessSource.Current"/>); the
    /// mismatch line adds its count of quests newer than the data, so Settings › About and the strip agree. Null
    /// leaves the count out.
    /// </summary>
    public Func<DataFreshnessReport>? Freshness { get; set; }

    /// <summary>
    /// "Reward data was generated for game X; you are on Y" (with ", which has N quests newer than the data" once the
    /// catalog shows some), or null when the versions match or are unknown. Recomposed only when the count moves.
    /// </summary>
    public string? VersionMismatchWarning
    {
        get
        {
            var newQuests = Freshness?.Invoke().NewQuests ?? 0;
            if (newQuests != mismatchNewQuests)
            {
                mismatchNewQuests = newQuests;
                mismatchWarning = DataStamp.MismatchWarning(session.UniqueRewards.GameVersion, ClientGameVersion, newQuests);
            }

            return mismatchWarning;
        }
    }

    /// <summary>
    /// Questionable's answer for a quest compared with the viewed character's evaluation (V2-17), printed as the
    /// block's "questionable:" line; null (or a null answer) leaves the line out. The plugin points it at
    /// <see cref="QuestionableIpc.Check"/>.
    /// </summary>
    public Func<QuestRecord, Core.Ipc.CrossCheckResult?>? CrossCheck { get; set; }

    /// <summary>
    /// The wider Questionable cross-check for a quest (feature plan v5, 1.6.0): its path, list place, unobtainable and
    /// active-events answers, printed as the block's "questionable more:" line. The plugin points it at
    /// <see cref="QuestionableIpc.Wider"/>.
    /// </summary>
    public Func<QuestRecord, Core.Ipc.QuestionableWider?>? CrossCheckMore { get; set; }

    /// <summary>
    /// The game's own offers (feature plan v7, C1): the block's "in game:" line and the report Settings › Advanced ›
    /// Diagnostics copies. Null leaves both out.
    /// </summary>
    public OfferObserver? Offers { get; set; }

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
            Questionable = QuestionableCheck(quest),
            QuestionableMore = QuestionableMore(quest),
            GameOffer = Offers?.Check(quest, evaluation),
        });
    }

    /// <summary>The cross-check for the block; a failure leaves the line out rather than the whole block.</summary>
    private Core.Ipc.CrossCheckResult? QuestionableCheck(QuestRecord quest)
    {
        try
        {
            return CrossCheck?.Invoke(quest);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>The wider cross-check for the block; a failure leaves the line out.</summary>
    private Core.Ipc.QuestionableWider? QuestionableMore(QuestRecord quest)
    {
        try
        {
            return CrossCheckMore?.Invoke(quest);
        }
        catch (Exception)
        {
            return null;
        }
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
    /// The game version the hooks were play-tested on: the csproj's <c>TsukimichiTestedGameVersion</c>, stamped into
    /// the assembly as <c>AssemblyMetadata("TestedGameVersion")</c>; empty when the build carries none.
    /// </summary>
    public static string TestedGameVersionText()
    {
        foreach (var attribute in typeof(DiagnosticBuilder).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false))
        {
            if (attribute is System.Reflection.AssemblyMetadataAttribute { Key: TestedGameVersionKey } metadata)
            {
                return metadata.Value?.Trim() ?? string.Empty;
            }
        }

        return string.Empty;
    }

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
