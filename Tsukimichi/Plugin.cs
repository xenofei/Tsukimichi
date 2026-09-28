using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Tsukimichi.Commands;
using Tsukimichi.Data;
using Tsukimichi.GameData;
using Tsukimichi.Ui;

namespace Tsukimichi;

/// <summary>
/// Plugin entry point. Starts the catalog build on load; windows and commands are added in later tasks.
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    // UI
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    // /UI

    private static readonly TimeSpan DisposeWait = TimeSpan.FromSeconds(5);

    private readonly CancellationTokenSource catalogCts = new();

    /// <summary>The catalog build started at load. Faulted or cancelled when the build did not finish.</summary>
    internal Task<CatalogBundle> CatalogTask { get; }

    // UI
    private readonly WindowSystem windowSystem = new("Tsukimichi");
    private readonly GlyphDebugWindow glyphDebugWindow;
    private readonly TsukimichiCommand command;
    private readonly UiState ui;
    private readonly GameLinks gameLinks;
    private readonly QueryRunner queryRunner;
    private readonly MainWindow mainWindow;
    private MoonlitPane? moonlitPane;
    private CharactersPane? charactersPane;
    private ConfigWindow? configWindow;
    private HelpWindow? helpWindow;

    /// <summary>
    /// Retry hook for the "Catalog unavailable" panel: rebuilds the catalog and hands it to the session on the
    /// framework thread. The returned task completes when the session has been updated either way.
    /// </summary>
    internal async Task RetryCatalogAsync()
    {
        var loader = new LuminaCatalogLoader(DataManager, Log);
        try
        {
            var bundle = await loader.BuildBundleAsync(DataManager.Language, catalogCts.Token).ConfigureAwait(false);
            await Framework.RunOnFrameworkThread(() =>
            {
                if (!gameStateDisposed)
                {
                    Session.SetCatalog(bundle);
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Log.Debug("Catalog retry cancelled");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Catalog unavailable");
            var message = ex.GetBaseException().Message;
            await Framework.RunOnFrameworkThread(() =>
            {
                if (!gameStateDisposed)
                {
                    Session.SetCatalogError(message);
                }
            }).ConfigureAwait(false);
        }
    }
    // /UI
    // ---- Game state (T3.2/T3.3) ----
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;

    internal Config.Configuration Settings { get; private set; } = null!;
    internal Core.Storage.PluginPaths Paths { get; private set; } = null!;
    internal Game.SessionState Session { get; private set; } = null!;
    internal Game.SnapshotService Snapshots { get; private set; } = null!;
    internal Game.StatePoller Poller { get; private set; } = null!;

    /// <summary>Read from the catalog continuation off-thread, so it must be volatile.</summary>
    private volatile bool gameStateDisposed;

    /// <summary>Config, shipped data, snapshot store, session state and poller; hands the catalog to the session when built.</summary>
    private void InitializeGameState()
    {
        Settings = Config.Configuration.Load(PluginInterface, Log);

        var assemblyDir = System.IO.Path.GetDirectoryName(PluginInterface.AssemblyLocation.FullName) ?? PluginInterface.AssemblyLocation.DirectoryName ?? ".";
        // The csproj copies Data\** to <plugin>\Data; PluginPaths expects the shipped files at its plugin root.
        var dataDir = System.IO.Path.Combine(assemblyDir, "Data");
        Paths = new Core.Storage.PluginPaths(PluginInterface.GetPluginConfigDirectory(), System.IO.Directory.Exists(dataDir) ? dataDir : assemblyDir);

        var uniqueRewards = Core.Storage.UniqueRewardsFile.Load(Paths.UniqueRewardsFile);
        foreach (var warning in uniqueRewards.Warnings)
        {
            Log.Warning("Unique rewards: {Warning}", warning);
        }

        var curated = Core.Storage.CuratedData.Load(Paths.CuratedDir);
        foreach (var warning in curated.Warnings)
        {
            Log.Warning("Curated data: {Warning}", warning);
        }

        Log.Information(
            "Shipped data: {Entries} unique reward entries (game {GameVersion}), {SystemUnlocks} system unlocks, {DutyUnlocks} duty unlocks, {FeatureQuests} feature quests, {Festivals} festivals",
            uniqueRewards.Entries.Count,
            uniqueRewards.GameVersion,
            curated.SystemUnlocks.Count,
            curated.DutyUnlocks.Count,
            curated.FeatureQuests.Count,
            curated.Festivals.Count);

        var reader = new Game.GameStateReader(Framework, PlayerState, DataManager, Log);
        Snapshots = new Game.SnapshotService(new Core.Storage.JsonSnapshotStore(Paths.ConfigDir), ClientState, Framework, Log, reader);
        Session = new Game.SessionState(Snapshots, Paths, uniqueRewards, curated);
        if (Settings.ViewedContentId is { } viewed && !Session.ViewCharacter(viewed))
        {
            Settings.ViewedContentId = null;
            Settings.Save(PluginInterface);
        }

        Session.Changed += PersistViewedCharacter;

        Poller = new Game.StatePoller(Framework, ClientState, Log, reader, Snapshots, Session, Settings);

        CatalogTask.ContinueWith(
            t =>
            {
                if (t.IsCanceled || gameStateDisposed)
                {
                    return;
                }

                var error = t.IsFaulted ? t.Exception?.GetBaseException().Message ?? "unknown error" : null;
                var bundle = t.IsFaulted ? null : t.Result;
                Framework.RunOnFrameworkThread(() =>
                {
                    if (gameStateDisposed)
                    {
                        return;
                    }

                    if (bundle is not null)
                    {
                        Session.SetCatalog(bundle);
                    }
                    else
                    {
                        Session.SetCatalogError(error ?? "unknown error");
                    }
                }).ContinueWith(static r => _ = r.Exception, TaskContinuationOptions.OnlyOnFaulted);
            },
            catalogCts.Token,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>Remembers an explicit character choice across sessions; following the live character stores null.</summary>
    private void PersistViewedCharacter()
    {
        var explicitId = Session.IsFollowingLive ? null : Session.ViewedContentId;
        if (Settings.ViewedContentId == explicitId)
        {
            return;
        }

        Settings.ViewedContentId = explicitId;
        Settings.Save(PluginInterface);
    }

    /// <summary>Poller first (unsubscribe, final save), then the snapshot service; the catalog build is cancelled by the caller.</summary>
    private void DisposeGameState()
    {
        gameStateDisposed = true;
        if (Session is not null)
        {
            Session.Changed -= PersistViewedCharacter;
        }

        Poller?.Dispose();
        Snapshots?.Dispose();
    }
    // ---- end game state ----

    public Plugin()
    {
        Log.Information("Tsukimichi loaded (config directory: {Dir})", PluginInterface.GetPluginConfigDirectory());

        var loader = new LuminaCatalogLoader(DataManager, Log);
        CatalogTask = loader.BuildBundleAsync(DataManager.Language, catalogCts.Token);
        CatalogTask.ContinueWith(
            static t =>
            {
                if (t.IsCanceled)
                {
                    Log.Debug("Catalog build cancelled");
                }
                else if (t.IsFaulted)
                {
                    Log.Error(t.Exception?.GetBaseException(), "Catalog unavailable");
                }
                else
                {
                    Log.Information("Catalog ready: {Count} quests, {Sections} journal sections", t.Result.Catalog.Count, t.Result.Catalog.BySection.Count);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        // Dalamud does not call Dispose on a plugin whose constructor threw, so anything hooked up from here on is
        // unwound by hand before the exception leaves.
        try
        {
            // UI
            glyphDebugWindow = new GlyphDebugWindow();
            windowSystem.AddWindow(glyphDebugWindow);

            // The main window reads Session/Settings/Paths lazily; they are initialized by the game-state block below.
            ui = new UiState();
            gameLinks = new GameLinks(GameGui, ChatGui, DataManager, Log);
            queryRunner = new QueryRunner(this, ui, Log);
            mainWindow = new MainWindow(this, ui, queryRunner, gameLinks, TextureProvider, PluginInterface, Log, RetryCatalogAsync);
            windowSystem.AddWindow(mainWindow);
            PluginInterface.UiBuilder.Draw += windowSystem.Draw;
            PluginInterface.UiBuilder.OpenMainUi += mainWindow.Toggle;

            command = new TsukimichiCommand(CommandManager, toggleMainWindow: mainWindow.Toggle, toggleGlyphWindow: glyphDebugWindow.Toggle, search: mainWindow.SearchAndPrint);
            // /UI
            // ---- Game state (T3.2/T3.3) ----
            InitializeGameState();
            // ---- end game state ----

            // UI (session-dependent surfaces)
            var unlockReader = new Game.RewardUnlockReader(Session, DataManager, Framework, Log);
            moonlitPane = new MoonlitPane(Session, TextureProvider, unlockReader, Paths, Log);
            charactersPane = new CharactersPane(Session, Paths, Log, Snapshots.Load, DataManager, TextureProvider);
            charactersPane.MoonlitCounts = moonlitPane.CountsFor;
            mainWindow.AttachPanes(moonlitPane, charactersPane);

            configWindow = new ConfigWindow(Settings, Session, PluginInterface, _ => ui.MarkQueryDirty());
            windowSystem.AddWindow(configWindow);
            PluginInterface.UiBuilder.OpenConfigUi += configWindow.Toggle;
            command.ToggleConfigWindow = configWindow.Toggle;

            helpWindow = new HelpWindow(Settings, PluginInterface, mainWindow);
            windowSystem.AddWindow(helpWindow);
            PluginInterface.UiBuilder.Draw += helpWindow.CheckFirstRun;
            configWindow.ShowHelp = helpWindow.Show;
            // MERGE: uncomment once TsukimichiCommand.ToggleHelpWindow lands (added on the other branch).
            // command.ToggleHelpWindow = helpWindow.Toggle;
            // /UI
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Tsukimichi failed to load; unwinding partial setup");
            AbortLoad();
            throw;
        }
    }

    public void Dispose()
    {
        // UI
        command.Dispose();
        if (configWindow is not null)
        {
            PluginInterface.UiBuilder.OpenConfigUi -= configWindow.Toggle;
        }

        if (helpWindow is not null)
        {
            PluginInterface.UiBuilder.Draw -= helpWindow.CheckFirstRun;
        }

        PluginInterface.UiBuilder.OpenMainUi -= mainWindow.Toggle;
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        windowSystem.RemoveAllWindows();
        mainWindow.Dispose();
        moonlitPane?.Dispose();
        queryRunner.Dispose();
        // /UI

        // ---- Game state dispose ----
        DisposeGameState();
        // ---- end game state dispose ----
        StopCatalogBuild();
        Log.Information("Tsukimichi unloaded");
    }

    /// <summary>Best-effort teardown after a failed constructor; every step is isolated so one failure cannot hide another.</summary>
    private void AbortLoad()
    {
        Unwind("draw hook", () =>
        {
            if (configWindow is not null)
            {
                PluginInterface.UiBuilder.OpenConfigUi -= configWindow.Toggle;
            }

            if (helpWindow is not null)
            {
                PluginInterface.UiBuilder.Draw -= helpWindow.CheckFirstRun;
            }

            if (mainWindow is not null)
            {
                PluginInterface.UiBuilder.OpenMainUi -= mainWindow.Toggle;
            }

            PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
            windowSystem.RemoveAllWindows();
        });
        Unwind("command", () => command?.Dispose());
        Unwind("main window", () => mainWindow?.Dispose());
        Unwind("moonlit pane", () => moonlitPane?.Dispose());
        Unwind("query runner", () => queryRunner?.Dispose());
        Unwind("game state", DisposeGameState);
        Unwind("catalog build", StopCatalogBuild);
    }

    private static void Unwind(string what, Action step)
    {
        try
        {
            step();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Unwinding {What} failed", what);
        }
    }

    /// <summary>Cancels the catalog build, waits briefly for it to stop and releases the token source.</summary>
    private void StopCatalogBuild()
    {
        catalogCts.Cancel();
        try
        {
            if (!CatalogTask.Wait(DisposeWait))
            {
                Log.Warning("Catalog build did not stop within {Seconds} s", DisposeWait.TotalSeconds);
            }
        }
        catch (AggregateException)
        {
            // Cancelled or faulted builds surface here; both were already logged.
        }

        catalogCts.Dispose();
    }
}
