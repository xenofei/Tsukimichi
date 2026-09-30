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
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    // /UI

    private static readonly TimeSpan DisposeWait = TimeSpan.FromSeconds(5);

    /// <summary>
    /// One build at a time: every build (the one at load, a retry, a filing flip) takes a ticket from
    /// <see cref="catalogGeneration"/> and its own token source; starting the next cancels the one in flight, and a
    /// build whose ticket is no longer current drops its result on the framework thread instead of swapping in a
    /// catalog of the other filing mode over the newer one.
    /// </summary>
    private readonly Core.Runtime.BuildGeneration catalogGeneration = new();
    private readonly object catalogBuildLock = new();
    private CancellationTokenSource catalogCts = new();

    /// <summary>The ticket of the build started at load, checked by its continuation.</summary>
    private readonly int initialCatalogGeneration;

    /// <summary>The latest rebuild (retry or filing flip); waited on at unload beside <see cref="CatalogTask"/>.</summary>
    private Task? catalogRebuild;

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
    private Game.NpcHooks? npcHooks;
    private Game.HookGateNotice? hookGateNotice;
    private TodoOverlay? todoOverlay;

    /// <summary>
    /// Rebuilds the catalog under the current <see cref="Config.Configuration.JournalFiling"/> and hands it to the
    /// session on the framework thread: the retry hook of the "Catalog unavailable" panel, and what a filing change
    /// in Settings runs. Called on the framework thread (both callers draw). The build in flight, if any, is
    /// cancelled and superseded; the session shows the catalog as loading until this build lands. The returned task
    /// completes when the session has been updated either way, or the build was superseded.
    /// </summary>
    internal Task RetryCatalogAsync()
    {
        var (generation, token) = StartCatalogBuild();
        Session.SetCatalogRebuilding();
        var rebuild = BuildCatalogAsync(generation, token);
        catalogRebuild = rebuild;
        return rebuild;
    }

    /// <summary>Takes the next build ticket and a fresh token, cancelling the build in flight (its result would be stale).</summary>
    private (int Generation, CancellationToken Token) StartCatalogBuild()
    {
        lock (catalogBuildLock)
        {
            var previous = catalogCts;
            // Not disposed: the superseded build still holds its token and may register on it while it winds down.
            previous.Cancel();
            catalogCts = new CancellationTokenSource();
            return (catalogGeneration.Start(), catalogCts.Token);
        }
    }

    private async Task BuildCatalogAsync(int generation, CancellationToken token)
    {
        var loader = new LuminaCatalogLoader(DataManager, Log, curated);
        // Read on the caller's (framework) thread, before the first await.
        var filing = Settings.JournalFiling;
        try
        {
            var bundle = await loader.BuildBundleAsync(DataManager.Language, filing, token).ConfigureAwait(false);
            await Framework.RunOnFrameworkThread(() => PublishCatalog(generation, bundle, null)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Log.Debug("Catalog build {Generation} cancelled", generation);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Catalog unavailable");
            var message = ex.GetBaseException().Message;
            await Framework.RunOnFrameworkThread(() => PublishCatalog(generation, null, message)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Framework thread. Hands a finished build to the session, unless the plugin is unloading or a newer build has
    /// started since (a filing flip or retry during this one): then this result is stale and is dropped, and the
    /// newer build's result is the one the session gets.
    /// </summary>
    private void PublishCatalog(int generation, CatalogBundle? bundle, string? error)
    {
        if (gameStateDisposed)
        {
            return;
        }

        if (!catalogGeneration.IsCurrent(generation))
        {
            Log.Debug("Catalog build {Generation} superseded by build {Current}; its result is dropped", generation, catalogGeneration.Current);
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

    /// <summary>The curated overlay, read once before the first catalog build (the refiler needs it) and kept for rebuilds.</summary>
    private Core.Storage.CuratedData curated = Core.Storage.CuratedData.Empty;

    /// <summary>Settings, paths and the curated overlay: what the catalog build needs before anything else starts.</summary>
    private void LoadSettingsAndCurated()
    {
        Settings = Config.Configuration.Load(PluginInterface, Log);

        var assemblyDir = System.IO.Path.GetDirectoryName(PluginInterface.AssemblyLocation.FullName) ?? PluginInterface.AssemblyLocation.DirectoryName ?? ".";
        // The csproj copies Data\** to <plugin>\Data; PluginPaths expects the shipped files at its plugin root.
        var dataDir = System.IO.Path.Combine(assemblyDir, "Data");
        Paths = new Core.Storage.PluginPaths(PluginInterface.GetPluginConfigDirectory(), System.IO.Directory.Exists(dataDir) ? dataDir : assemblyDir);

        curated = Core.Storage.CuratedData.Load(Paths.CuratedDir);
        foreach (var warning in curated.Warnings)
        {
            Log.Warning("Curated data: {Warning}", warning);
        }
    }

    /// <summary>Shipped reward data, snapshot store, session state and poller; hands the catalog to the session when built.</summary>
    private void InitializeGameState()
    {
        var uniqueRewards = Core.Storage.UniqueRewardsFile.Load(Paths.UniqueRewardsFile);
        foreach (var warning in uniqueRewards.Warnings)
        {
            Log.Warning("Unique rewards: {Warning}", warning);
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
        Session = new Game.SessionState(Snapshots, Paths, uniqueRewards, curated, Log);
        // Spoiler shield (T19): Settings > Spoilers with the viewed character's override.
        Session.SpoilerOptionsFor = Settings.SpoilerOptionsFor;
        Session.CharacterForgotten += ForgetSpoilerOverride;
        Session.DataDeleted += ClearSpoilerOverrides;
        if (Settings.ViewedContentId is { } viewed && !Session.ViewCharacter(viewed))
        {
            Settings.ViewedContentId = null;
            Settings.Save(PluginInterface);
        }

        Session.Changed += PersistViewedCharacter;

        Poller = new Game.StatePoller(Framework, ClientState, Log, reader, Snapshots, Session, Settings);

        // The initial build's ticket: a filing flip from Settings during the startup build supersedes it, and
        // PublishCatalog drops its result on the framework thread.
        var generation = initialCatalogGeneration;
        CatalogTask.ContinueWith(
            t =>
            {
                if (t.IsCanceled || gameStateDisposed)
                {
                    return;
                }

                var error = t.IsFaulted ? t.Exception?.GetBaseException().Message ?? "unknown error" : null;
                var bundle = t.IsFaulted ? null : t.Result;
                Framework.RunOnFrameworkThread(() => PublishCatalog(generation, bundle, error))
                    .ContinueWith(static r => _ = r.Exception, TaskContinuationOptions.OnlyOnFaulted);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>Remembers an explicit character choice across sessions; following the live character stores null.</summary>
    /// <summary>
    /// Once per frame, before the window system draws: the scale factors every window reads, the palette (Night or the
    /// user's Dalamud colours, read while nothing is pushed yet) and the motion clock (scroll pause, key pruning).
    /// </summary>
    private void UpdateUiMetrics()
    {
        if (Settings is { } settings)
        {
            Ui.UiMetrics.Update(settings);
            Ui.Typography.Update();
            Ui.Theme.Refresh(settings.FollowDalamudColours);
            Ui.Motion.BeginFrame();
        }
    }

    /// <summary>
    /// A forgotten character takes its spoiler override with it. The session bumped before this ran (or, for the live
    /// character, not at all), so the masks are refreshed to drop the override now rather than at the next change.
    /// </summary>
    private void ForgetSpoilerOverride(ulong contentId)
    {
        if (Settings.SpoilerShieldByCharacter.Remove(contentId))
        {
            Settings.Save(PluginInterface);
            Session.RefreshSpoilers();
        }
    }

    /// <summary>
    /// "Delete all data" drops every per-character spoiler override. <see cref="Game.SessionState.DeleteAllData"/>
    /// raises this before it follows the live character, and that bump is the refresh: every listener rebuilds with
    /// the overrides already gone.
    /// </summary>
    private void ClearSpoilerOverrides()
    {
        if (Settings.SpoilerShieldByCharacter.Count > 0)
        {
            Settings.SpoilerShieldByCharacter.Clear();
            Settings.Save(PluginInterface);
        }
    }

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
            Session.CharacterForgotten -= ForgetSpoilerOverride;
            Session.DataDeleted -= ClearSpoilerOverrides;
        }

        Poller?.Dispose();
        Snapshots?.Dispose();
    }
    // ---- end game state ----

    public Plugin()
    {
        Log.Information("Tsukimichi loaded (config directory: {Dir})", PluginInterface.GetPluginConfigDirectory());

        // The catalog build reads the filing setting and the curated overlay, so those come before it starts.
        LoadSettingsAndCurated();
        var loader = new LuminaCatalogLoader(DataManager, Log, curated);
        var (initialGeneration, initialToken) = StartCatalogBuild();
        initialCatalogGeneration = initialGeneration;
        CatalogTask = loader.BuildBundleAsync(DataManager.Language, Settings.JournalFiling, initialToken);
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
            // Subscribed before the window system so every window (the todo overlay and Nearby too) draws with
            // this frame's scale factors.
            Ui.Typography.Initialize(PluginInterface.UiBuilder.FontAtlas, Log);
            PluginInterface.UiBuilder.Draw += UpdateUiMetrics;
            PluginInterface.UiBuilder.Draw += windowSystem.Draw;
            PluginInterface.UiBuilder.OpenMainUi += mainWindow.Toggle;

            command = new TsukimichiCommand(CommandManager, toggleMainWindow: mainWindow.Toggle, toggleGlyphWindow: glyphDebugWindow.Toggle, search: mainWindow.SearchAndPrint);
            // /UI
            // ---- Game state (T3.2/T3.3) ----
            InitializeGameState();
            // ---- end game state ----

            // UI (session-dependent surfaces)
            var unlockReader = new Game.RewardUnlockReader(Session, DataManager, Framework, Log);
            moonlitPane = new MoonlitPane(Session, TextureProvider, unlockReader, Paths, Log, DataManager, Settings, PluginInterface, gameLinks);
            MoonlitPane moonlit = moonlitPane;
            // Reward tooltips (table icons, detail rows) say "Store only" for rewards the Online Store also sells and
            // "Also drops in …" for rewards a duty also drops.
            gameLinks.IsStoreResell = reward => Session.StoreResells.Contains(reward);
            // Chat links print a masked main scenario quest under its placeholder (T19). Chat, the item menu, hover
            // hints and Wotsit speak for the logged-in character, so they use its mask, not the viewed character's.
            gameLinks.QuestName = quest => Session.LiveSpoilers.DisplayName(quest);
            gameLinks.DropWhere = reward => Session.StoreResells.DropWhere(reward);
            wotsit = new Game.WotsitIpc(PluginInterface, Framework, Log);
            wotsit.Enabled = Settings.WotsitIntegration;
            wotsit.Attach(() => Session.Bundle, () => moonlit.Catalog, moonlit.Icons.Resolve, quest =>
            {
                mainWindow.IsOpen = true;
                mainWindow.BringToFront();
                MoonlitPane.Reveal(ui, quest);
            }, () => Session.LiveSpoilers);
            // Item hover hints and context-menu links (V2-14). The lookup follows the Moonlit catalog reference (rebuilt
            // after an override change) and the quest catalog (set once the build finishes); both are read per use.
            var rewardLookup = new Core.Unique.RewardLookupSource(() => moonlit.Catalog, () => Session.Bundle?.Catalog);
            // Addon kill switch (T20): the four game hooks below (hover hint, item and NPC menu entries, server info
            // bar entry) run only while this gate allows them, that is on the game version they were play-tested on
            // (the csproj's TsukimichiTestedGameVersion) or with "Enable game hooks on this untested version" ticked for the running version. It is
            // one shared service: anything else drawn beside a game addon, the Duty Finder unlock hint of 0.9.0 (P13)
            // first, takes this instance and follows its Changed event. The client version is read once here.
            var clientGameVersion = Game.DiagnosticBuilder.ReadClientGameVersion(DataManager, Log);
            var gate = new Core.Runtime.HookGate(Game.DiagnosticBuilder.TestedGameVersionText(), clientGameVersion, Settings.EnableHooksOnUntestedVersion);
            hookGateNotice = new Game.HookGateNotice(gate, ClientState, ChatGui, Log, () => Game.DiagnosticBuilder.ReadClientGameVersion(DataManager, Log));
            hoverHint = new HoverHint(GameGui, Session, unlockReader, rewardLookup, gate, Log) { Enabled = Settings.ItemHintsEnabled };
            PluginInterface.UiBuilder.Draw += hoverHint.Draw;
            itemHooks = new Game.ItemHooks(ContextMenu, rewardLookup, quest =>
            {
                mainWindow.IsOpen = true;
                mainWindow.BringToFront();
                MoonlitPane.Reveal(ui, quest);
            }, gate, Log) { Enabled = Settings.ItemContextMenuEnabled, QuestName = quest => Session.LiveSpoilers.DisplayName(quest) };
            var discovery = new DiscoveryCommands(Session, ClientState, TargetManager, gameLinks);
            command.ListZoneQuests = discovery.Zone;
            command.ListTargetQuests = discovery.Which;
            // "Why not offered?" (P2): the NPC context-menu entry opens the Journal on the NPC's quests; /tsuki why
            // prints a quest's blockers. The hook reads the target's kind and base id only; nothing is stored.
            npcHooks = new Game.NpcHooks(ContextMenu, Session, TargetManager, npcId =>
            {
                mainWindow.IsOpen = true;
                mainWindow.BringToFront();
                ui.ShowIssuer(npcId);
            }, gate, Log) { Enabled = Settings.NpcContextMenuEnabled };
            var why = new WhyCommand(Session, ui, gameLinks);
            command.Why = why.Run;

            // "Report this quest": the diagnostic block (detail pane button and /tsuki report), the data stamp in
            // Settings > About and on the status bar, and the game-version warning. The client version is the one read
            // for the hook gate above.
            var diagnostics = new Game.DiagnosticBuilder(Session, Game.DiagnosticBuilder.PluginVersionText(), clientGameVersion, () => Settings.JournalFiling);
            if (diagnostics.VersionMismatchWarning is { } versionWarning)
            {
                Log.Warning("{Warning}", versionWarning);
            }

            mainWindow.AttachDiagnostics(diagnostics);
            var report = new ReportCommand(Session, ui, gameLinks, diagnostics, Log);
            command.Report = report.Run;

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
            dtrEntry = new Game.DtrEntry(DtrBar, discoveryWindow, discoverySettings, gate, Log);
            command.ToggleNearbyWindow = discoveryWindow.Toggle;
            charactersPane = new CharactersPane(Session, Paths, Log, Snapshots.Load, DataManager, TextureProvider);
            charactersPane.MoonlitCounts = moonlitPane.CountsFor;
            charactersPane.UniqueRewards = () => moonlit.Catalog;
            charactersPane.Pins = queryRunner;
            charactersPane.Links = gameLinks;
            mainWindow.AttachPanes(moonlitPane, charactersPane);
            mainWindow.AttachOverrides(moonlitPane);
            // The flight index (a few small sheets) is built on the pane's first draw, on the framework thread.
            flightPane = new FlightPane(Session, unlockReader, gameLinks, TextureProvider, Log, () => ClientState.TerritoryType, () => FlightIndex.Build(DataManager.Excel, Dalamud.Utility.ClientLanguageExtensions.ToLumina(DataManager.Language)));
            mainWindow.AttachFlight(flightPane);
            chatNotifier = new Game.ChatNotifier(Session, Settings, Paths, gameLinks, ChatGui, Log);

            configWindow = new ConfigWindow(Settings, Session, PluginInterface, diagnostics, _ => ui.MarkQueryDirty());
            configWindow.Overrides = moonlitPane;
            // Exports (P12): Settings › Data › Export and /tsuki export write local files; nothing is uploaded.
            var exportService = new Game.ExportService(Session, Settings, Paths, unlockReader, () => moonlit.Catalog, diagnostics.PluginVersion, diagnostics.ClientGameVersion, Log);
            configWindow.Export = new ExportSection(Settings, exportService, () => Settings.Save(PluginInterface), Log);
            command.Export = new ExportCommand(exportService, Settings, gameLinks).Run;
            // A filing change rebuilds the catalog off-thread; the session swaps it in on the framework thread.
            configWindow.JournalFilingChanged = filing => _ = RetryCatalogAsync();
            Game.WotsitIpc wotsitIpc = wotsit;
            configWindow.WotsitToggled = enabled => wotsitIpc.Enabled = enabled;
            if (hoverHint is { } hint) { configWindow.ItemHintsToggled = enabled => hint.Enabled = enabled; }
            if (itemHooks is { } hooks) { configWindow.ItemContextMenuToggled = enabled => hooks.Enabled = enabled; }
            if (npcHooks is { } npcMenu) { configWindow.NpcContextMenuToggled = enabled => npcMenu.Enabled = enabled; }
            configWindow.HookGate = gate;
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
            // The tour's keys (Enter, arrows, Backspace, Esc) are kept from the game while the tour has the keyboard.
            tutorial.KeyState = KeyState;
            Framework.Update += tutorial.ConsumeKeys;
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

            // "What's new" after an update: decided on the main window's first draw, drawn above the detail pane.
            mainWindow.AttachWhatsNew(new WhatsNewCard(Settings, PluginInterface, Log, helpWindow.Show));

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
            Framework.Update -= overlay.ConsumeKeys;
        }

        if (hoverHint is not null)
        {
            PluginInterface.UiBuilder.Draw -= hoverHint.Draw;
        }

        itemHooks?.Dispose();
        npcHooks?.Dispose();
        hookGateNotice?.Dispose();
        PluginInterface.UiBuilder.OpenMainUi -= mainWindow.Toggle;
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.Draw -= UpdateUiMetrics;
        windowSystem.RemoveAllWindows();
        Ui.Typography.Dispose();
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
                Framework.Update -= overlay.ConsumeKeys;
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
            PluginInterface.UiBuilder.Draw -= UpdateUiMetrics;
            windowSystem.RemoveAllWindows();
            Ui.Typography.Dispose();
        });
        Unwind("item hooks", () => itemHooks?.Dispose());
        Unwind("npc hooks", () => npcHooks?.Dispose());
        Unwind("hook gate notice", () => hookGateNotice?.Dispose());
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

    /// <summary>Cancels the catalog build in flight, waits briefly for the builds to stop and releases the token source.</summary>
    private void StopCatalogBuild()
    {
        CancellationTokenSource cts;
        lock (catalogBuildLock)
        {
            cts = catalogCts;
        }

        cts.Cancel();
        try
        {
            var builds = catalogRebuild is { } rebuild ? new Task[] { CatalogTask, rebuild } : [CatalogTask];
            if (!Task.WaitAll(builds, DisposeWait))
            {
                Log.Warning("Catalog build did not stop within {Seconds} s", DisposeWait.TotalSeconds);
            }
        }
        catch (AggregateException)
        {
            // Cancelled or faulted builds surface here; both were already logged.
        }

        cts.Dispose();
    }
}
