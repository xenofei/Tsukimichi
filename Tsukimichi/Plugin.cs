using System;
using System.Diagnostics;
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
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static ISeStringEvaluator SeStringEvaluator { get; private set; } = null!;
    // /UI

    /// <summary>The longest unload waits in all: the last saves, the multibox loop and the catalog builds share it.</summary>
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
    private Game.QuestionableIpc? questionable;
    private CharactersPane? charactersPane;
    private FlightPane? flightPane;
    private PlanSource? planSource;
    private ConfigWindow? configWindow;
    private HelpWindow? helpWindow;
    private ITutorial? tutorial;
    private Game.ChatNotifier? chatNotifier;
    private DiscoveryWindow? discoveryWindow;
    private Game.DtrEntry? dtrEntry;
    private HoverHint? hoverHint;
    private Game.ItemHooks? itemHooks;
    private Game.NpcHooks? npcHooks;
    private Game.DutyFinderHint? dutyFinderHint;

    /// <summary>The hero banners' index source; polled once when a catalog lands so the index starts building before the first selection.</summary>
    private Core.Ui.BannerIndexSource<Core.Unique.DutyUnlockIndex>? banners;
    private DutyFinderPanel? dutyFinderPanel;
    private Game.HookGateNotice? hookGateNotice;
    private Game.TodoLockNotice? todoLockNotice;
    private Game.WelcomeBackSource? welcomeBack;
    private Game.IpcProvider? ipcProvider;
    private TodoOverlay? todoOverlay;
    private Localization.LocService? loc;
    private RouteWindow? routeWindow;

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
        var loader = new LuminaCatalogLoader(DataManager, Log, curated, questPatches);
        // Read on the caller's (framework) thread, before the first await.
        var filing = Settings.JournalFiling;
        CatalogBundle bundle;
        Core.Ui.NodeIconMap icons;
        try
        {
            bundle = await loader.BuildBundleAsync(DataManager.Language, filing, token).ConfigureAwait(false);
            icons = ResolveNodeIcons(bundle);
        }
        catch (OperationCanceledException)
        {
            Log.Debug("Catalog build {Generation} cancelled", generation);
            return;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Catalog unavailable");
            var message = ex.GetBaseException().Message;
            await PublishOnFrameworkThreadAsync(generation, null, message).ConfigureAwait(false);
            return;
        }

        // Only the build's own failure is "Catalog unavailable" here: a failure while the session takes the catalog is
        // handled by PublishCatalog, which knows the previous catalog is still the one in use.
        await PublishOnFrameworkThreadAsync(generation, bundle, null, icons).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs <see cref="PublishCatalog"/> on the framework thread. Failing to get there (the framework going away at
    /// unload) is logged rather than thrown: nobody awaits a rebuild's outcome but the retry button.
    /// </summary>
    private async Task PublishOnFrameworkThreadAsync(int generation, CatalogBundle? bundle, string? error, Core.Ui.NodeIconMap? icons = null)
    {
        try
        {
            await Framework.RunOnFrameworkThread(() => PublishCatalog(generation, bundle, error, icons)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Catalog build {Generation} finished but could not be handed to the session", generation);
        }
    }

    /// <summary>
    /// The Journal tree's node icons for a finished build (Moon Road proposal §6.2), read from the sheets off the
    /// framework thread, once per catalog. A failure is logged and leaves the tree on its moon halos.
    /// </summary>
    private static Core.Ui.NodeIconMap ResolveNodeIcons(CatalogBundle bundle)
    {
        try
        {
            return NodeIconResolver.Build(DataManager.Excel).Resolve(bundle.Catalog);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Journal node icons could not be resolved; the tree keeps its moon halos");
            return Core.Ui.NodeIconMap.Empty;
        }
    }

    /// <summary>
    /// Framework thread. Hands a finished build to the session, unless the plugin is unloading or a newer build has
    /// started since (a filing flip or retry during this one): then this result is stale and is dropped, and the
    /// newer build's result is the one the session gets.
    /// </summary>
    private void PublishCatalog(int generation, CatalogBundle? bundle, string? error, Core.Ui.NodeIconMap? nodeIcons = null)
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

        if (bundle is null)
        {
            Session.SetCatalogError(error ?? "unknown error");
            return;
        }

        try
        {
            Session.SetCatalog(bundle, nodeIcons);
        }
        catch (Exception ex)
        {
            // SetCatalog derives everything before it swaps anything in, so the previous catalog (if any) is still
            // whole and in use: the new one is the one unavailable.
            Log.Error(ex, "Catalog built but the session could not take it; the previous catalog stays in use");
            Session.SetCatalogError(ex.GetBaseException().Message);
            return;
        }

        // Start the hero banner index now rather than on the first selection, which would otherwise show its
        // category art for a frame or two while the index builds.
        try
        {
            banners?.Poll();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Hero banner index could not start; it is tried again on the next selection");
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

    /// <summary>
    /// The one background queue for snapshot, sidecar, pins and overrides saves: the framework thread never waits on the
    /// disk or the cross-client lock. Its completions run on the framework thread (<see cref="DrainWriter"/>).
    /// </summary>
    internal Core.Storage.SerialWriter Writer { get; } = new();

    /// <summary>Multibox sharing with other game clients (D11); null until the game state is initialized.</summary>
    internal Game.MultiboxService? Multibox { get; private set; }

    /// <summary>The journal text reader and its opt-in search index (P9); null until the constructor creates it.</summary>
    internal Game.QuestTextService? QuestText { get; private set; }

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

        questPatches = Core.Storage.QuestPatches.Load(Paths.QuestPatchesFile);
        foreach (var warning in questPatches.Warnings)
        {
            Log.Warning("Quest patches: {Warning}", warning);
        }
    }

    /// <summary>quest_patches.json (P8), read with the curated overlay and kept for rebuilds.</summary>
    private Core.Storage.QuestPatches questPatches = Core.Storage.QuestPatches.Empty;

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
        Session.CharacterForgotten += ForgetPayoffGates;
        Session.DataDeleted += ClearPayoffGates;
        if (Settings.ViewedContentId is { } viewed && !Session.ViewCharacter(viewed))
        {
            Settings.ViewedContentId = null;
            Settings.Save(PluginInterface);
        }

        Session.Changed += PersistViewedCharacter;

        Session.Writer = Writer;
        Framework.Update += DrainWriter;
        Poller = new Game.StatePoller(Framework, ClientState, Log, reader, Snapshots, Session, Settings, Writer);
        // Multibox (D11): heartbeats and other game clients' saves, through the shared config folder only.
        Multibox = new Game.MultiboxService(Framework, Log, Session, Snapshots, Paths);

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
                var icons = bundle is null ? null : ResolveNodeIcons(bundle);
                _ = PublishOnFrameworkThreadAsync(generation, bundle, error, icons);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>
    /// After a language switch (framework thread): the session bumps so every label cached per session version is
    /// rebuilt, the query re-runs (its status texts are composed), and the windows take their titles in the new
    /// language. The window ids after "###" never change, so ImGui keeps positions and sizes.
    /// </summary>
    private void OnTextChanged()
    {
        Session?.RefreshText();
        ui?.MarkQueryDirty();
        if (mainWindow is not null)
        {
            mainWindow.WindowName = Strings.MainWindowTitle;
        }

        if (configWindow is not null)
        {
            configWindow.WindowName = Strings.ConfigWindowTitle;
        }

        if (helpWindow is not null)
        {
            helpWindow.WindowName = Strings.Help.WindowTitle;
        }

        if (discoveryWindow is not null)
        {
            discoveryWindow.WindowName = Strings.DiscoveryWindowTitle;
        }

        if (todoOverlay is not null)
        {
            todoOverlay.WindowName = Strings.TodoWindowTitle;
        }

        if (routeWindow is not null)
        {
            routeWindow.WindowName = Strings.RouteWindowTitle;
        }
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
            Ui.Typography.Update(Core.Ui.FlairRules.GameHeadingFonts(settings.Flair, settings.GameHeadingFonts));
            Ui.Theme.Refresh(settings.FollowDalamudColours, settings.GlyphPalette, settings.Flair);
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

    /// <summary>A forgotten character takes its "Before you continue" notices and open "why?" disclosures with it (P5).</summary>
    private void ForgetPayoffGates(ulong contentId)
    {
        if (Settings.ForgetPayoffGates(contentId))
        {
            Settings.Save(PluginInterface);
        }
    }

    /// <summary>"Delete all data" drops every character's "Before you continue" notices and disclosures (P5).</summary>
    private void ClearPayoffGates()
    {
        if (Settings.ClearPayoffGates())
        {
            Settings.Save(PluginInterface);
        }
    }

    /// <summary>
    /// Multibox (D11), framework thread: <c>user/pins.json</c> or <c>user/overrides.json</c> changed on disk. Each owner
    /// merges the file into its copy, keeping its own changes not saved yet.
    /// </summary>
    private void OnUserFilesChanged()
    {
        queryRunner?.ReloadPinsFromDisk();
        moonlitPane?.MergeOverridesFromDisk();
    }

    /// <summary>Framework thread, every tick: the outcomes of saves the background writer finished.</summary>
    private void DrainWriter(IFramework _)
    {
        try
        {
            Writer.DrainCompletions();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "A save's completion failed");
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

    /// <summary>
    /// Poller first (unsubscribe, final save), then the snapshot service; the catalog build is cancelled by the caller.
    /// Each step is isolated like the rest of <see cref="TearDown"/>; the writer's drain and the multibox loop wait
    /// only for what is left of the unload's one <see cref="DisposeWait"/> (<paramref name="remaining"/>).
    /// </summary>
    private void DisposeGameState(Func<TimeSpan> remaining)
    {
        gameStateDisposed = true;
        Unwind("session listeners", () =>
        {
            if (Session is not null)
            {
                Session.Changed -= PersistViewedCharacter;
                Session.CharacterForgotten -= ForgetSpoilerOverride;
                Session.DataDeleted -= ClearSpoilerOverrides;
                Session.CharacterForgotten -= ForgetPayoffGates;
                Session.DataDeleted -= ClearPayoffGates;
            }
        });
        Unwind("state poller", () => Poller?.Dispose());
        // The last saves (the poller's, the pins the query runner queued) land before the heartbeat goes.
        Unwind("save writer", () =>
        {
            Framework.Update -= DrainWriter;
            var wait = remaining();
            if (!Writer.Close(wait))
            {
                Log.Warning("Saves still queued after {Seconds:0.#} s; they finish in the background", wait.TotalSeconds);
            }
        });
        // After the poller's last save: the heartbeat goes once nothing more is written for the character.
        Unwind("multibox", () =>
        {
            if (Multibox is not null)
            {
                Multibox.UserFilesChanged -= OnUserFilesChanged;
                Multibox.Dispose(remaining());
            }
        });
        Unwind("snapshot service", () => Snapshots?.Dispose());
    }
    // ---- end game state ----

    public Plugin()
    {
        Log.Information("Tsukimichi loaded (config directory: {Dir})", PluginInterface.GetPluginConfigDirectory());

        // The catalog build reads the filing setting and the curated overlay, so those come before it starts.
        LoadSettingsAndCurated();

        // The UI language (V2-19) before any window is built, so every title and label starts in it.
        loc = new Localization.LocService(PluginInterface, Settings, Framework, Log);
        var loader = new LuminaCatalogLoader(DataManager, Log, curated, questPatches);
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
            moonlitPane = new MoonlitPane(Session, TextureProvider, unlockReader, Paths, Log, DataManager, Settings, PluginInterface, gameLinks) { Writer = Writer };
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
            }, () => Session.LiveSpoilers,
            // The registration order (B7) follows the same character as the spoiler shield: the logged-in one, or the
            // viewed one while nobody is logged in.
            () => Session.IsLive || Session.LiveContentId is null ? Session.States : Session.LiveStates);
            // Item hover hints and context-menu links (V2-14). The lookup follows the Moonlit catalog reference (rebuilt
            // after an override change) and the quest catalog (set once the build finishes); both are read per use.
            var rewardLookup = new Core.Unique.RewardLookupSource(() => moonlit.Catalog, () => Session.Bundle?.Catalog);
            // Addon kill switch (T20): the five game hooks below (hover hint, item and NPC menu entries, Duty Finder
            // unlock hint, server info bar entry) run only while this gate allows them, that is on the game version they were play-tested on
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
            // Duty Finder unlock hint (P13): the quest behind a padlocked duty, beside the Duty Finder. Its listeners take
            // the same kill switch; the lookup follows the curated overlay and the Moonlit catalog like the item hint's.
            var dutyUnlocks = new Core.Unique.DutyUnlockIndexSource(() => Session.Curated, () => moonlit.Catalog);
            dutyFinderHint = new Game.DutyFinderHint(AddonLifecycle, GameGui, DataManager, Session, dutyUnlocks, gate, Log) { Enabled = Settings.DutyFinderHintEnabled };
            // Hero banners (V4): every quest's banner through the fallback chain, resolved off the frame once per catalog
            // and duty unlock index; the duty step reads the same index as the Duty Finder hint.
            banners = new Core.Ui.BannerIndexSource<Core.Unique.DutyUnlockIndex>(
                () => Session.Bundle?.Catalog,
                () => dutyUnlocks.Current,
                (catalog, duties) => BannerSources.Build(DataManager.Excel, catalog, duties).Resolve(catalog),
                onError: ex => Log.Warning(ex, "Hero banners unavailable; quests show their own banner or category art"));
            mainWindow.AttachBanners(banners);
            dutyFinderPanel = new DutyFinderPanel(dutyFinderHint, gameLinks, quest =>
            {
                mainWindow.IsOpen = true;
                mainWindow.BringToFront();
                MoonlitPane.Reveal(ui, quest);
            });
            PluginInterface.UiBuilder.Draw += dutyFinderPanel.Draw;
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

            // Questionable cross-check (V2-17): the detail pane's "Questionable agrees" line, the diagnostic block's
            // "questionable:" line, and the opt-in "Add to Questionable priority" behind Settings › Integrations.
            questionable = new Game.QuestionableIpc(PluginInterface, Log);
            Game.QuestionableIpc questionableIpc = questionable;
            diagnostics.CrossCheck = quest => questionableIpc.Check(quest, Session);
            mainWindow.AttachQuestionable(questionableIpc, () => Settings.QuestionableHandoff);
            mainWindow.AttachDiagnostics(diagnostics);

            // Journal text (P9): the detail pane's Journal card, and with Settings › Journal text the search box's journal
            // words, from an index kept per game version under the config directory.
            QuestText = new Game.QuestTextService(DataManager, SeStringEvaluator, ClientState, Log, Paths.ConfigDir, clientGameVersion);
            QuestText.SetEnabled(Settings.JournalTextSearch);
            mainWindow.AttachQuestText(QuestText);
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
            // Multibox (D11): pins and overrides another game client saved are merged in as they land.
            if (Multibox is not null)
            {
                Multibox.UserFilesChanged += OnUserFilesChanged;
            }
            // Unlock route (P6): the detail pane, a Moonlit row's menu and the Characters job rows ask UiState for it.
            routeWindow = new RouteWindow(Session, queryRunner, quest =>
            {
                mainWindow.IsOpen = true;
                mainWindow.BringToFront();
                MoonlitPane.Reveal(ui, quest);
            });
            windowSystem.AddWindow(routeWindow);
            ui.RouteRequested += routeWindow.Show;
            // The flight index (a few small sheets) is built on the pane's first draw, on the framework thread.
            flightPane = new FlightPane(Session, unlockReader, gameLinks, TextureProvider, Log, () => ClientState.TerritoryType, () => FlightIndex.Build(DataManager.Excel, Dalamud.Utility.ClientLanguageExtensions.ToLumina(DataManager.Language), Strings.FlightAllZonesFormat));
            mainWindow.AttachFlight(flightPane);
            // Clear my blues (P3): the duty kinds (ContentFinderCondition) are read on the plan's first use.
            planSource = new PlanSource(Session, () => DutyIndex.Build(DataManager.Excel, Dalamud.Utility.ClientLanguageExtensions.ToLumina(DataManager.Language)), Log);
            mainWindow.AttachPlan(new PlanPane(Session, planSource, gameLinks, Settings, () => Settings.Save(PluginInterface)));
            chatNotifier = new Game.ChatNotifier(Session, Settings, Paths, gameLinks, ChatGui, Log);
            // "Before you continue" (P5): the dashboard and the Tonight card lines, and the once-per-character chat line.
            var payoffGates = new Game.PayoffGateSource(Session, Log);
            var payoffLines = new PayoffGateLines(payoffGates, Session, Settings, () => Settings.Save(PluginInterface));
            charactersPane.PayoffLines = payoffLines;
            mainWindow.AttachPayoffLines(payoffLines);
            chatNotifier.PayoffGates = payoffGates;
            chatNotifier.SaveSettings = () => Settings.Save(PluginInterface);

            configWindow = new ConfigWindow(Settings, Session, PluginInterface, diagnostics, _ => ui.MarkQueryDirty());
            configWindow.Language = loc;
            configWindow.RunNextTick = action => _ = Framework.RunOnTick(action);
            configWindow.Overrides = moonlitPane;
            configWindow.QuestText = QuestText;
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
            if (dutyFinderHint is { } dutyHint) { configWindow.DutyFinderHintToggled = enabled => dutyHint.Enabled = enabled; }
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
            todoOverlay.Plan = planSource;
            todoOverlay.ShowPins = mainWindow.ShowPinned;
            windowSystem.AddWindow(todoOverlay);
            // 0.8.0: Locked became click-through; a player who upgraded with it on is told once in chat.
            todoLockNotice = new Game.TodoLockNotice(Settings, ClientState, ChatGui, PluginInterface, Log);
            command.ToggleTodoOverlay = todoOverlay.ToggleEnabled;
            configWindow.ResetTodoPosition = todoOverlay.ResetPosition;

            // The tutorial draws over the main window (ITutorial.Draw at the end of MainWindow.Draw) and offers itself
            // the first time the main window opens (CheckFirstRun on UiBuilder.Draw).
            TutorialOverlay tutorial = new(Settings, PluginInterface, ui);
            this.tutorial = tutorial;
            tutorial.WatchedWindow = mainWindow;
            PluginInterface.UiBuilder.Draw += tutorial.CheckFirstRun;
            // Enter, Backspace and Esc are kept from the game while the tour card has the keyboard (the arrows never are).
            tutorial.KeyState = KeyState;
            Framework.Update += tutorial.ConsumeKeys;
            mainWindow.AttachTutorial(tutorial);
            // Esc that closes a popup or the filter panel is kept from the game (the Esc ladder, T17).
            mainWindow.KeyState = KeyState;
            Framework.Update += mainWindow.ConsumeEscape;

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
            // Every window exists now: a language switch retitles them and refreshes the session's caches.
            Localization.Loc.Changed += OnTextChanged;
            tutorial.OpenHelp = helpWindow.Show;
            configWindow.ShowHelp = helpWindow.Show;
            configWindow.StartTutorial = helpActions.StartTutorial;
            glyphDebugWindow.StartTutorial = helpActions.StartTutorial;

            command.ToggleHelpWindow = helpWindow.Toggle;

            // "What's new" after an update: decided on the main window's first draw, drawn above the detail pane.
            mainWindow.AttachWhatsNew(new WhatsNewCard(Settings, PluginInterface, Log, helpWindow.Show));

            // "Since you were away" (P7): the stored captures are kept from before this login's first save; the card
            // sits above the detail pane (after What's new) and the Characters dashboard opens it for any character.
            welcomeBack = new Game.WelcomeBackSource(Session, Snapshots, ClientState, Settings, Log);
            mainWindow.AttachWelcomeBack(welcomeBack, Session);
            Game.WelcomeBackSource welcomeBackSource = welcomeBack;
            charactersPane.OpenWelcomeBack = welcomeBackSource.Open;

            // Toolbar buttons on the main window (help, tutorial, settings).
            mainWindow.AttachActions(configWindow.Toggle, helpWindow.Toggle, helpActions.StartTutorial);

            // Tsukimichi's own IPC gates (V2-16, docs/ipc.md): other plugins read the logged-in character's states
            // and blockers and can open a quest here.
            ipcProvider = new Game.IpcProvider(PluginInterface, Framework, Log, Session, quest =>
            {
                mainWindow.IsOpen = true;
                mainWindow.BringToFront();
                MoonlitPane.Reveal(ui, quest);
            });
            // /UI
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Tsukimichi failed to load; unwinding partial setup");
            TearDown();
            throw;
        }
    }

    public void Dispose()
    {
        TearDown();
        Log.Information("Tsukimichi unloaded");
    }

    /// <summary>
    /// Unload, and the best-effort unwind after a failed constructor: every step is isolated, so one failure can neither
    /// hide another nor leave a later hook (context menus, addon listeners, Framework.Update, IPC) subscribed into an
    /// unloaded assembly. The waits at the end (the writer's last saves, the multibox loop, the catalog builds) share
    /// one <see cref="DisposeWait"/> rather than taking one each.
    /// </summary>
    private void TearDown()
    {
        var unloading = Stopwatch.StartNew();
        TimeSpan Remaining()
        {
            var left = DisposeWait - unloading.Elapsed;
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }

        // Other plugins stop reaching in first, before anything they could reach is torn down.
        Unwind("tsukimichi ipc", () => ipcProvider?.Dispose());
        Unwind("localization", () =>
        {
            Localization.Loc.Changed -= OnTextChanged;
            loc?.Dispose();
        });
        Unwind("command", () => command?.Dispose());
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
                Framework.Update -= mainWindow.ConsumeEscape;
            }

            if (hoverHint is not null)
            {
                PluginInterface.UiBuilder.Draw -= hoverHint.Draw;
            }

            if (dutyFinderPanel is not null)
            {
                PluginInterface.UiBuilder.Draw -= dutyFinderPanel.Draw;
            }

            PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
            PluginInterface.UiBuilder.Draw -= UpdateUiMetrics;
        });
        Unwind("windows", windowSystem.RemoveAllWindows);
        Unwind("fonts", Ui.Typography.Dispose);
        Unwind("item hooks", () => itemHooks?.Dispose());
        Unwind("npc hooks", () => npcHooks?.Dispose());
        Unwind("duty finder hint", () => dutyFinderHint?.Dispose());
        Unwind("hook gate notice", () => hookGateNotice?.Dispose());
        Unwind("todo lock notice", () => todoLockNotice?.Dispose());
        Unwind("since you were away", () => welcomeBack?.Dispose());
        Unwind("todo overlay", () => todoOverlay?.Dispose());
        Unwind("server bar entry", () => dtrEntry?.Dispose());
        Unwind("nearby window", () => discoveryWindow?.Dispose());
        Unwind("main window", () => mainWindow?.Dispose());
        Unwind("wotsit ipc", () => wotsit?.Dispose());
        Unwind("questionable ipc", () => questionable?.Dispose());
        Unwind("moonlit pane", () => moonlitPane?.Dispose());
        Unwind("chat notifier", () => chatNotifier?.Dispose());
        Unwind("query runner", () => queryRunner?.Dispose());
        Unwind("journal text", () => QuestText?.Dispose());
        Unwind("lifestream ipc", () => lifestream?.Dispose());
        // Each of its steps is isolated on its own.
        DisposeGameState(Remaining);
        Unwind("catalog build", () => StopCatalogBuild(Remaining()));
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

    /// <summary>
    /// Cancels the catalog build in flight, waits up to <paramref name="wait"/> (what is left of the unload's
    /// <see cref="DisposeWait"/>) for the builds to stop and releases the token source.
    /// </summary>
    private void StopCatalogBuild(TimeSpan wait)
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
            if (!Task.WaitAll(builds, wait))
            {
                Log.Warning("Catalog build did not stop within {Seconds:0.#} s", wait.TotalSeconds);
            }
        }
        catch (AggregateException)
        {
            // Cancelled or faulted builds surface here; both were already logged.
        }

        cts.Dispose();
    }
}
