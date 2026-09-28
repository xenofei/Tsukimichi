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
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IDtrBar DtrBar { get; private set; } = null!;
    [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
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
    private readonly Game.LifestreamIpc lifestream;
    private readonly QueryRunner queryRunner;
    private readonly MainWindow mainWindow;
    private MoonlitPane? moonlitPane;
    private Game.WotsitIpc? wotsit;
    private CharactersPane? charactersPane;
    private FlightPane? flightPane;
    private ConfigWindow? configWindow;
    private HelpWindow? helpWindow;
    private ITutorial? tutorial;
    private Game.ChatNotifier? chatNotifier;
    private DiscoveryWindow? discoveryWindow;
    private Game.DtrEntry? dtrEntry;
    private HoverHint? hoverHint;
    private Game.ItemHooks? itemHooks;
    private TodoOverlay? todoOverlay;

    /// <summary>The Moonlit pane's override store as the detail pane's <see cref="IUniqueOverrides"/>.</summary>
    private sealed class MoonlitOverrides(MoonlitPane pane) : IUniqueOverrides
    {
        public Core.Storage.UniqueOverride? Get(uint rowId) => pane.Overrides.TryGetValue(rowId, out var verdict) ? verdict : null;

        public void Clear(uint rowId) => pane.ClearOverride(rowId);

        public void Set(uint rowId, bool unique, string? note) => pane.SetOverride(rowId, unique, note);
    }

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
            lifestream = new Game.LifestreamIpc(PluginInterface, Log);
            gameLinks.Lifestream = lifestream;
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
            moonlitPane = new MoonlitPane(Session, TextureProvider, unlockReader, Paths, Log, DataManager);
            MoonlitPane moonlit = moonlitPane;
            wotsit = new Game.WotsitIpc(PluginInterface, Framework, Log);
            wotsit.Enabled = Settings.WotsitIntegration;
            wotsit.Attach(() => Session.Bundle, () => moonlit.Catalog, moonlit.Icons.Resolve, quest =>
            {
                mainWindow.IsOpen = true;
                mainWindow.BringToFront();
                MoonlitPane.Reveal(ui, quest);
            });
            // Item hover hints and context-menu links (V2-14). The lookup follows the Moonlit catalog reference (rebuilt
            // after an override change) and the quest catalog (set once the build finishes); both are read per use.
            var rewardLookup = new Core.Unique.RewardLookupSource(() => moonlit.Catalog, () => Session.Bundle?.Catalog);
            hoverHint = new HoverHint(GameGui, Session, unlockReader, rewardLookup, Log) { Enabled = Settings.ItemHintsEnabled };
            PluginInterface.UiBuilder.Draw += hoverHint.Draw;
            itemHooks = new Game.ItemHooks(ContextMenu, rewardLookup, quest =>
            {
                mainWindow.IsOpen = true;
                mainWindow.BringToFront();
                MoonlitPane.Reveal(ui, quest);
            }, Log) { Enabled = Settings.ItemContextMenuEnabled };
            var discovery = new DiscoveryCommands(Session, ClientState, TargetManager, gameLinks);
            command.ListZoneQuests = discovery.Zone;
            command.ListTargetQuests = discovery.Which;

            // Nearby quests window and the server info bar entry; settings in user/discovery.json until they move into Configuration.
            var discoverySettingsPath = Core.Discovery.DiscoverySettings.PathFor(Paths);
            var discoveryWarnings = new System.Collections.Generic.List<string>();
            var discoverySettings = Core.Discovery.DiscoverySettings.Load(discoverySettingsPath, discoveryWarnings);
            foreach (var warning in discoveryWarnings)
            {
                Log.Warning("Discovery settings: {Warning}", warning);
            }

            discoveryWindow = new DiscoveryWindow(Session, ClientState, gameLinks, queryRunner, DataManager, discoverySettings, discoverySettingsPath, Log, quest =>
            {
                mainWindow.IsOpen = true;
                mainWindow.BringToFront();
                MoonlitPane.Reveal(ui, quest);
            });
            windowSystem.AddWindow(discoveryWindow);
            dtrEntry = new Game.DtrEntry(DtrBar, discoveryWindow, discoverySettings, Log);
            command.ToggleNearbyWindow = discoveryWindow.Toggle;
            charactersPane = new CharactersPane(Session, Paths, Log, Snapshots.Load, DataManager, TextureProvider);
            charactersPane.MoonlitCounts = moonlitPane.CountsFor;
            charactersPane.UniqueRewards = () => moonlit.Catalog;
            mainWindow.AttachPanes(moonlitPane, charactersPane);
            mainWindow.AttachOverrides(new MoonlitOverrides(moonlitPane));
            // The flight index (a few small sheets) is built on the pane's first draw, on the framework thread.
            flightPane = new FlightPane(Session, unlockReader, gameLinks, TextureProvider, Log, () => ClientState.TerritoryType, () => FlightIndex.Build(DataManager.Excel, Dalamud.Utility.ClientLanguageExtensions.ToLumina(DataManager.Language)));
            mainWindow.AttachFlight(flightPane);
            chatNotifier = new Game.ChatNotifier(Session, Settings, Paths, gameLinks, ChatGui, Log);

            configWindow = new ConfigWindow(Settings, Session, PluginInterface, _ => ui.MarkQueryDirty());
            Game.WotsitIpc wotsitIpc = wotsit;
            configWindow.WotsitToggled = enabled => wotsitIpc.Enabled = enabled;
            if (hoverHint is { } hint) { configWindow.ItemHintsToggled = enabled => hint.Enabled = enabled; }
            if (itemHooks is { } hooks) { configWindow.ItemContextMenuToggled = enabled => hooks.Enabled = enabled; }
            windowSystem.AddWindow(configWindow);
            PluginInterface.UiBuilder.OpenConfigUi += configWindow.Toggle;
            command.ToggleConfigWindow = configWindow.Toggle;

            // Todo overlay (V2-13): follows Settings.TodoOverlayEnabled; /tsuki todo and the settings window flip it.
            todoOverlay = new TodoOverlay(Settings, Session, gameLinks, quest =>
            {
                mainWindow.IsOpen = true;
                mainWindow.BringToFront();
                MoonlitPane.Reveal(ui, quest);
            }, ClientState, Condition, Paths, PluginInterface, Log);
            windowSystem.AddWindow(todoOverlay);
            command.ToggleTodoOverlay = todoOverlay.ToggleEnabled;
            configWindow.ResetTodoPosition = todoOverlay.ResetPosition;

            // The tutorial draws over the main window (ITutorial.Draw at the end of MainWindow.Draw) and offers itself
            // the first time the main window opens (CheckFirstRun on UiBuilder.Draw).
            TutorialOverlay tutorial = new(Settings, PluginInterface, ui);
            this.tutorial = tutorial;
            tutorial.WatchedWindow = mainWindow;
            PluginInterface.UiBuilder.Draw += tutorial.CheckFirstRun;
            mainWindow.AttachTutorial(tutorial);

            // Each action opens the main window in front of the help window it was clicked in.
            var helpActions = new HelpActions(
                OpenFilters: () =>
                {
                    // The filter panel lives on the Journal tab.
                    ui.Tab = NavTab.Journal;
                    ui.FilterPanelOpen = true;
                    mainWindow.IsOpen = true;
                    mainWindow.BringToFront();
                },
                ShowTab: tab =>
                {
                    ui.Tab = tab;
                    mainWindow.IsOpen = true;
                    mainWindow.BringToFront();
                },
                StartTutorial: () =>
                {
                    mainWindow.IsOpen = true;
                    mainWindow.BringToFront();
                    tutorial.Start();
                },
                OpenSettings: configWindow.Toggle);
            helpWindow = new HelpWindow(helpActions, PluginInterface);
            windowSystem.AddWindow(helpWindow);
            tutorial.OpenHelp = helpWindow.Show;
            configWindow.ShowHelp = helpWindow.Show;
            configWindow.StartTutorial = helpActions.StartTutorial;
            glyphDebugWindow.StartTutorial = helpActions.StartTutorial;

            command.ToggleHelpWindow = helpWindow.Toggle;

            // Toolbar buttons on the main window (help, tutorial, settings).
            mainWindow.AttachActions(configWindow.Toggle, helpWindow.Toggle, helpActions.StartTutorial);
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

        if (tutorial is TutorialOverlay overlay)
        {
            PluginInterface.UiBuilder.Draw -= overlay.CheckFirstRun;
        }

        if (hoverHint is not null)
        {
            PluginInterface.UiBuilder.Draw -= hoverHint.Draw;
        }

        itemHooks?.Dispose();
        PluginInterface.UiBuilder.OpenMainUi -= mainWindow.Toggle;
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        windowSystem.RemoveAllWindows();
        todoOverlay?.Dispose();
        dtrEntry?.Dispose();
        discoveryWindow?.Dispose();
        mainWindow.Dispose();
        wotsit?.Dispose();
        moonlitPane?.Dispose();
        chatNotifier?.Dispose();
        queryRunner.Dispose();
        lifestream.Dispose();
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

            if (tutorial is TutorialOverlay overlay)
            {
                PluginInterface.UiBuilder.Draw -= overlay.CheckFirstRun;
            }

            if (mainWindow is not null)
            {
                PluginInterface.UiBuilder.OpenMainUi -= mainWindow.Toggle;
            }

            if (hoverHint is not null)
            {
                PluginInterface.UiBuilder.Draw -= hoverHint.Draw;
            }

            PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
            windowSystem.RemoveAllWindows();
        });
        Unwind("item hooks", () => itemHooks?.Dispose());
        Unwind("command", () => command?.Dispose());
        Unwind("todo overlay", () => todoOverlay?.Dispose());
        Unwind("server bar entry", () => dtrEntry?.Dispose());
        Unwind("nearby window", () => discoveryWindow?.Dispose());
        Unwind("main window", () => mainWindow?.Dispose());
        Unwind("wotsit ipc", () => wotsit?.Dispose());
        Unwind("moonlit pane", () => moonlitPane?.Dispose());
        Unwind("chat notifier", () => chatNotifier?.Dispose());
        Unwind("query runner", () => queryRunner?.Dispose());
        Unwind("lifestream ipc", () => lifestream?.Dispose());
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
