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
public sealed partial class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    // UI
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    // The glyph window's colour-vision copies of the medal atlas (feature plan v6 G3).
    [PluginService] internal static ITextureReadbackProvider TextureReadback { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IDtrBar DtrBar { get; private set; } = null!;
    [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static ISeStringEvaluator SeStringEvaluator { get; private set; } = null!;
    // Travel (1.6.0): where the player stands and which aetherytes are attuned.
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IAetheryteList AetheryteList { get; private set; } = null!;
    // /UI

    /// <summary>
    /// The deadline the unload's last waits share: the save writer's drain, the multibox loop and the catalog builds, in
    /// that order. Its clock starts at the writer's drain (<see cref="Core.Runtime.WaitBudget"/>), so the final saves get
    /// all of it whatever the steps before took, and the multibox loop and the builds share only what the writer left.
    /// The one wait before it is the journal index's (<see cref="Game.QuestTextService.DisposeWait"/>): the unload
    /// waits at most that plus this, 2 s + 5 s = 7 s.
    /// </summary>
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

    /// <summary>
    /// The latest rebuild's build itself (retry or filing flip), without its hand-off to the session; waited on at unload
    /// beside <see cref="CatalogTask"/>. The hand-off is not waited on: it is a no-op once unloading.
    /// </summary>
    private volatile Task? catalogRebuild;

    /// <summary>The catalog build started at load. Faulted or cancelled when the build did not finish.</summary>
    internal Task<CatalogBundle> CatalogTask { get; }

    // UI
    private readonly WindowSystem windowSystem = new("Tsukimichi");
    private readonly GlyphDebugWindow glyphDebugWindow;
    private readonly TsukimichiCommand command;
    private StopCommand? stopCommand;
    private readonly UiState ui;
    private readonly GameLinks gameLinks;
    private readonly Game.LifestreamIpc lifestream;
    private readonly Game.VnavmeshIpc vnavmesh;
    private readonly Game.TravelService travel;
    private readonly Game.CompanionPlugins companions;
    private readonly Game.CompanionSetupService companionSetup;
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
    private GamePanels? gamePanels;
    private Game.HookGateNotice? hookGateNotice;
    private Game.DailyOfferReader? dailyOffers;
    private Game.GameStateReader? stateReader;
    private Game.TodoLockNotice? todoLockNotice;
    private Game.WelcomeBackSource? welcomeBack;
    private Game.IpcProvider? ipcProvider;
    private IpcWindow? ipcWindow;
    private LinkConfirmWindow? linkConfirmWindow;
    private TodoOverlay? todoOverlay;
    private Localization.LocService? loc;
    private RouteWindow? routeWindow;
    private Game.ActiveRouteService? activeRoutes;

    // Hand-in items (1.6.0): read-only counts through Allagan Tools, and the Artisan and GatherBuddy hand-offs.
    private Game.AllaganToolsIpc? allaganTools;
    private Game.ArtisanIpc? artisan;
    private Game.GatherBuddyCommands? gatherBuddy;

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
        return BuildCatalogAsync(generation, token);
    }

    /// <summary>
    /// Takes the next build ticket and a fresh token, cancelling the build in flight (its result would be stale). Once
    /// the unload cancelled the builds (<see cref="CancelCatalogBuilds"/>) the token handed out is that cancelled one,
    /// so a build started that late stops at once.
    /// </summary>
    private (int Generation, CancellationToken Token) StartCatalogBuild()
    {
        lock (catalogBuildLock)
        {
            if (!catalogBuildsStopped)
            {
                var previous = catalogCts;
                // Not disposed: the superseded build still holds its token and may register on it while it winds down.
                previous.Cancel();
                catalogCts = new CancellationTokenSource();
            }

            return (catalogGeneration.Start(), catalogCts.Token);
        }
    }

    /// <summary>Set by <see cref="CancelCatalogBuilds"/> under <see cref="catalogBuildLock"/>: no build gets a live token after it.</summary>
    private bool catalogBuildsStopped;

    /// <summary>The unload's first step: cancels the build in flight and every later one, so they wind down while the rest unloads.</summary>
    private void CancelCatalogBuilds()
    {
        lock (catalogBuildLock)
        {
            catalogBuildsStopped = true;
            catalogCts.Cancel();
        }
    }

    private async Task BuildCatalogAsync(int generation, CancellationToken token)
    {
        // Read on the caller's (framework) thread, before the first await.
        var filing = Settings.JournalFiling;
        // What the unload waits for: the build and its icons (they read the sheets), never the hand-off below, which
        // is a no-op once unloading and needs the framework thread the unload may itself be running on.
        var build = BuildBundleWithIconsAsync(filing, token);
        catalogRebuild = build;
        CatalogBundle bundle;
        Core.Ui.NodeIconMap icons;
        Game.PreparedCatalog? prepared;
        try
        {
            (bundle, icons, prepared) = await build.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
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
        await PublishOnFrameworkThreadAsync(generation, bundle, null, icons, prepared).ConfigureAwait(false);
    }

    /// <summary>
    /// A rebuild's off-thread part: the bundle, its node icons, then what the session derives from it
    /// (<see cref="PrepareCatalog"/>). Starts on the caller's thread.
    /// </summary>
    private async Task<(CatalogBundle Bundle, Core.Ui.NodeIconMap Icons, Game.PreparedCatalog? Prepared)> BuildBundleWithIconsAsync(Core.Model.JournalFiling filing, CancellationToken token)
    {
        var loader = new LuminaCatalogLoader(DataManager, Log, curated, questPatches);
        var bundle = await loader.BuildBundleAsync(DataManager.Language, filing, token).ConfigureAwait(false);
        var icons = ResolveNodeIcons(bundle);
        token.ThrowIfCancellationRequested();
        return (bundle, icons, PrepareCatalog(bundle));
    }

    /// <summary>
    /// Off the framework thread, for a finished build: the session's derived indexes and the stored character on view
    /// resolved against it (<see cref="Game.SessionState.PrepareCatalog"/>), so the frame it lands on only swaps them
    /// in. A failure is logged and leaves the derivation to that frame, which reports it as it always did.
    /// </summary>
    private Game.PreparedCatalog? PrepareCatalog(CatalogBundle bundle)
    {
        if (Session is not { } session || gameStateDisposed)
        {
            return null;
        }

        try
        {
            return session.PrepareCatalog(bundle);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Catalog indexes could not be built off the framework thread; they are built when the catalog lands");
            return null;
        }
    }

    /// <summary>
    /// Runs <see cref="PublishCatalog"/> on the framework thread. Failing to get there (the framework going away at
    /// unload) is logged rather than thrown: nobody awaits a rebuild's outcome but the retry button.
    /// </summary>
    private async Task PublishOnFrameworkThreadAsync(int generation, CatalogBundle? bundle, string? error, Core.Ui.NodeIconMap? icons = null, Game.PreparedCatalog? prepared = null)
    {
        try
        {
            await Framework.RunOnFrameworkThread(() => PublishCatalog(generation, bundle, error, icons, prepared)).ConfigureAwait(false);
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
    private void PublishCatalog(int generation, CatalogBundle? bundle, string? error, Core.Ui.NodeIconMap? nodeIcons = null, Game.PreparedCatalog? prepared = null)
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

        var started = Stopwatch.GetTimestamp();
        try
        {
            Session.SetCatalog(bundle, nodeIcons, prepared);
        }
        catch (Exception ex)
        {
            // SetCatalog derives everything before it swaps anything in, so the previous catalog (if any) is still
            // whole and in use: the new one is the one unavailable.
            Log.Error(ex, "Catalog built but the session could not take it; the previous catalog stays in use");
            Session.SetCatalogError(ex.GetBaseException().Message);
            return;
        }

        OnCollectorCatalog(bundle);

        // The ways into the interiors givers stand in, resolved ahead on a worker: Next stops and the route window
        // would otherwise resolve dozens on the draw thread the first time they group givers by aetheryte.
        try
        {
            gameLinks?.WarmEntrances(bundle.Catalog);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Interior entrances could not be resolved ahead; they are resolved on first use");
        }

        // The landing frame's own cost (the session's listeners included), against what the worker did before it.
        var landedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (prepared is not null)
        {
            Log.Information(
                "Catalog handed to the session in {LandedMs:F1} ms on the framework thread; derived indexes built in {PrepareMs:F0} ms on a worker ({Indexes}), the stored character on view {View}",
                landedMs,
                prepared.PrepareMs,
                prepared.Indexes.Describe(),
                prepared.View is null ? "none" : "resolved there too");
        }
        else
        {
            Log.Information("Catalog handed to the session in {LandedMs:F1} ms on the framework thread (indexes built there)", landedMs);
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
    [PluginService] internal static IUnlockState UnlockState { get; private set; } = null!;

    /// <summary>The collectible unlock flags (IUnlockState) the capture saves and the Moonlit pane reads; null until the game state is initialized.</summary>
    internal Game.CollectibleReader? CollectibleFlags { get; private set; }

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

    /// <summary>
    /// Per-character settings every game client shares (1.8.0, R7 D): <c>user/characters.json</c>, saved through a locked
    /// merge like the pins (spoiler overrides, "Before you continue" notices, hidden, not tracked, Compare target).
    /// </summary>
    internal Core.Storage.CharacterSettingsBook CharacterBook { get; private set; } = null!;

    /// <summary>Every character in the alt lists' one stable order (1.8.0, R7 B); built with the game state.</summary>
    internal Game.CharacterRoster Roster { get; private set; } = null!;

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

    /// <summary>The "Game updated" report for the current catalog (1.5.0); null until the UI is wired.</summary>
    internal Game.DataFreshnessSource? Freshness { get; private set; }

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

        CollectibleFlags = new Game.CollectibleReader(UnlockState, DataManager, Log);
        var reader = new Game.GameStateReader(Framework, PlayerState, DataManager, Log)
        {
            // Owned collectibles are saved with each capture (decision 9), for the rewards the Moonlit tab lists.
            CollectibleTargets = Core.Unique.Collectibles.Targets(uniqueRewards, curated),
            CollectibleFlags = CollectibleFlags,
        };
        stateReader = reader;
        // The store keeps two backup generations per character (<id>.prev.json refreshed once a day, <id>.prev2.json
        // the one before, 1.5.0) and never refreshes them from a file that lost many completed quests (judged with the
        // live catalog); a failed backup is logged and the save goes on.
        var store = new Core.Storage.JsonSnapshotStore(Paths.ConfigDir)
        {
            BackupFailed = (path, ex) => Log.Warning(ex, "Snapshot backup before saving {File} failed; the save goes ahead", System.IO.Path.GetFileName(path)),
            Catalog = () => Session?.Bundle?.Catalog,
        };
        Snapshots = new Game.SnapshotService(store, ClientState, Framework, Log, reader);
        Session = new Game.SessionState(Snapshots, Paths, uniqueRewards, curated, Log);
        // Per-character settings (1.8.0): user/characters.json, shared by every game client; 1.7 kept them in Settings.
        CharacterBook = new Core.Storage.CharacterSettingsBook(Paths.CharacterSettingsFile, Writer, WarnCharacterSettings);
        CharacterBook.Load();
        MigrateCharacterSettings();
        CharacterBook.Changed += OnCharacterSettingsChanged;
        Roster = new Game.CharacterRoster(Session, CharacterBook, DataManager, Log);
        // "Don't track this character": nothing of it is written while it is logged in.
        Snapshots.IsTracked = CharacterBook.IsTracked;
        // Spoiler shield (T19): Settings > Spoilers with the viewed character's override.
        Session.SpoilerOptionsFor = id => Settings.SpoilerOptionsFor(id, id is { } contentId ? CharacterBook.SpoilerShield(contentId) : null);
        Session.CharacterForgotten += ForgetCharacterSettings;
        Session.DataDeleted += ClearCharacterSettings;
        if (Settings.ViewedContentId is { } viewed && !Session.ViewCharacter(viewed))
        {
            Settings.ViewedContentId = null;
            Settings.Save(PluginInterface);
        }

        Session.Changed += PersistViewedCharacter;

        Session.Writer = Writer;
        Poller = new Game.StatePoller(Framework, ClientState, Log, reader, Snapshots, Session, Settings, Writer)
        {
            PrintNotice = line => ChatGui.Print(line, Ui.Strings.ChatTag),
        };
        // Today's allied society offer from the game's own calculation; it waits for the hook gate (set with the UI).
        dailyOffers = new Game.DailyOfferReader(Log);
        Poller.DailyOffers = dailyOffers;
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
                var prepared = bundle is null ? null : PrepareCatalog(bundle);
                _ = PublishOnFrameworkThreadAsync(generation, bundle, error, icons, prepared);
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
    /// user's Dalamud colours, read while nothing is pushed yet), the motion clock (scroll pause, key pruning, the waxing
    /// moons of quests just completed), the Full sky's clock and meteors, and the
    /// first clicks of armed menu items whose menu closed.
    /// </summary>
    private void UpdateUiMetrics()
    {
        if (Settings is { } settings)
        {
            Ui.UiMetrics.Update(settings);
            Ui.Typography.Update(Core.Ui.FlairRules.GameHeadingFonts(settings.Flair, settings.GameHeadingFonts));
            // The appearance first (theme, moons per state, palette, frames, high contrast), then the palette it names.
            var appearance = Ui.Themes.GlyphSeam.Refresh(settings.Appearance);
            Ui.Theme.Refresh(appearance.FollowDalamud, appearance.GlyphPalette, settings.Flair, appearance.Palette);
            Ui.Motion.BeginFrame();
            Ui.PopupFade.BeginFrame();
            if (Session is { } session)
            {
                // No moment plays in combat (feature plan v6 M1): the completions are noted and let go.
                Ui.Motion.NoteCompletions(session.LiveContentId, session.RecentEvents, session.IsLive, Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat]);
            }

            Ui.NightSky.BeginFrame(settings);

            Ui.Chrome.BeginFrame();
        }
    }

    /// <summary>
    /// Every Tsukimichi window, drawn in the body font at the text size (Settings › General › Text size, feature plan
    /// v6 U7): one push around the window system, so each window, its popups and its tooltips read the same font on the
    /// frame the setting changes. The handlers below do the same for what draws outside the window system.
    /// </summary>
    private void DrawWindows()
    {
        using var body = Ui.Typography.Body();
        windowSystem.Draw();
    }

    private void DrawUndoToast()
    {
        using var body = Ui.Typography.Body();
        Ui.UndoToast.Draw();
    }

    private void DrawHoverHint()
    {
        using var body = Ui.Typography.Body();
        hoverHint?.Draw();
    }

    // The panel's frame (GamePanelShell) pushes the body font at the text size itself, as the 1.7 panels' does.
    private void DrawDutyFinderPanel() => dutyFinderPanel?.Draw();

    /// <summary>
    /// The one-time move of the per-character settings 1.7 kept in Settings (spoiler overrides, notices, open
    /// disclosures) into <c>user/characters.json</c> (1.8.0). They read from the file at once; Settings is emptied once
    /// the file holds them, and a failed save leaves them there for the next load. Running in two clients at once, or
    /// again after an older client saved Settings back, never overrides what the file says.
    /// </summary>
    private void MigrateCharacterSettings()
    {
        var legacy = Settings.TakeLegacyCharacterSettings();
        if (legacy.IsEmpty)
        {
            return;
        }

        CharacterBook.MigrateLegacy(legacy, saved =>
        {
            if (saved && Settings.ClearLegacyCharacterSettings())
            {
                Settings.Save(PluginInterface);
                Log.Information("Per-character settings moved to user/characters.json");
            }
        });
    }

    private void WarnCharacterSettings(string message, Exception? ex)
    {
        if (ex is null)
        {
            Log.Warning("{Message}", message);
        }
        else
        {
            Log.Warning(ex, "{Message}", message);
        }
    }

    /// <summary>The character settings changed (here or in another client): a spoiler override change rebuilds every mask.</summary>
    private void OnCharacterSettingsChanged(bool spoilers)
    {
        if (spoilers)
        {
            Session?.RefreshSpoilers();
        }
    }

    /// <summary>
    /// A forgotten character takes its own settings with it (spoiler override, notices, Compare), except hidden and not
    /// tracked: forgetting an untracked character deletes its old file and must not start saving it again.
    /// </summary>
    private void ForgetCharacterSettings(ulong contentId) => CharacterBook.Edit(Core.Storage.CharacterSettingChange.Forget(contentId));

    /// <summary>
    /// "Delete all data" drops every character's settings except those of characters live in another game client, and
    /// keeps every character's hidden and not-tracked choices (an untracked character logged in here is not written).
    /// <see cref="Game.SessionState.DeleteAllData"/> raises this before it follows the live character, and that bump is
    /// the refresh: every listener rebuilds with the overrides already gone.
    /// </summary>
    private void ClearCharacterSettings() => CharacterBook.Reset(Session.IsLiveElsewhere);

    /// <summary>
    /// Multibox (D11), framework thread: <c>user/pins.json</c> or <c>user/overrides.json</c> changed on disk. Each owner
    /// merges the file into its copy, keeping its own changes not saved yet.
    /// </summary>
    private void OnUserFilesChanged()
    {
        queryRunner?.ReloadPinsFromDisk();
        moonlitPane?.MergeOverridesFromDisk();
        CharacterBook?.ReloadFromDisk();
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

    /// <summary>
    /// Framework thread, every tick: a stored character resolved on a worker is taken in, and one on view reads its
    /// dailies and weeklies cleared once the reset passes (one compare a frame), and the unlock index is polled so its
    /// build starts when the catalog loads.
    /// </summary>
    private void SessionTick(IFramework _)
    {
        try
        {
            Session?.TakePendingView();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Showing the character resolved in the background failed");
        }

        try
        {
            Session?.WatchResets(DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Re-reading the stored character after the reset failed");
        }

        try
        {
            // The unlock index starts building as soon as a catalog loads and is collected here, so the first chat
            // "Unlocked:" line and the first Unlocks section read a built index, not the empty one a lazy start gives.
            questUnlocks?.Poll();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Collecting what quests open failed");
        }

        try
        {
            // The plan's tags start on a worker as the catalog lands, not on the Plan tab's first draw (A11).
            planSource?.Warm();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Starting the plan's tags failed");
        }

        try
        {
            // Companion settings are read here, when a hand-off button or Settings asked for them and a read is due, so
            // a draw only ever reads the last answer (A11).
            companionSetup.Tick();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Reading the companion plugins' settings failed");
        }
    }

    /// <summary>
    /// The constructor's last step (R4 F9): the per-frame work starts only once every window, listener and gate is in
    /// place. The poller's first update would otherwise read the game before the hook gate guarded those reads, and
    /// publish to a session half of whose listeners had not joined.
    /// </summary>
    private void StartFrameworkUpdates()
    {
        Framework.Update += DrainWriter;
        Framework.Update += SessionTick;
        Multibox?.Start();
        Poller.Start();
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
    /// Each step is isolated like the rest of <see cref="TearDown"/>. The writer's drain is the first wait on the unload's
    /// <paramref name="budget"/>, so it starts the clock and gets all of <see cref="DisposeWait"/>; the multibox loop
    /// waits only for what the writer left.
    /// </summary>
    private void DisposeGameState(Core.Runtime.WaitBudget budget)
    {
        gameStateDisposed = true;
        Unwind("session listeners", () =>
        {
            if (Session is not null)
            {
                Session.Changed -= PersistViewedCharacter;
                Session.CharacterForgotten -= ForgetCharacterSettings;
                Session.DataDeleted -= ClearCharacterSettings;
            }

            if (CharacterBook is not null)
            {
                CharacterBook.Changed -= OnCharacterSettingsChanged;
            }
        });
        Unwind("session tick", () => Framework.Update -= SessionTick);
        Unwind("state poller", () => Poller?.Dispose());
        // Character settings edits still queued go on the writer with the rest.
        Unwind("character settings", () => CharacterBook?.Save(final: true));
        // The last saves (the poller's, the pins the query runner queued) land before the heartbeat goes.
        Unwind("save writer", () =>
        {
            Framework.Update -= DrainWriter;
            var wait = budget.Remaining();
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
                Multibox.Dispose(budget.Remaining());
            }
        });
        Unwind("snapshot service", () => Snapshots?.Dispose());
        Unwind("collectible flags", () => CollectibleFlags?.Dispose());
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
            // Companion plugins (feature plan v5, decision 1): which plugins Tsukimichi hands work to are loaded,
            // turned off, outdated or missing; every hand-off button asks it why it is disabled.
            companions = new Game.CompanionPlugins(PluginInterface, Log);

            // Companion setup: the settings of those plugins that matter to a hand-off, read from their own files and
            // getter gates, set only through their own IPC on "Apply recommended settings". A blocking one makes the
            // registry's disabled reason name it.
            companionSetup = new Game.CompanionSetupService(PluginInterface, companions, Log);
            companionSetup.UseGates(Core.Companions.CompanionPlugin.TextAdvance, new Game.BoolSettingGates(PluginInterface, companions, Core.Companions.CompanionPlugin.TextAdvance, "TextAdvance.", Log));
            companionSetup.UseGates(Core.Companions.CompanionPlugin.Vnavmesh, new Game.BoolSettingGates(PluginInterface, companions, Core.Companions.CompanionPlugin.Vnavmesh, "vnavmesh.", Log));
            companions.Setup = companionSetup;
            lifestream = new Game.LifestreamIpc(PluginInterface, Log);
            gameLinks.Lifestream = lifestream;
            // Travel (1.6.0): attunement-aware Teleport, the aethernet hop, Walk to giver and Go to giver. Lifestream and
            // vnavmesh stay optional; the character only moves on an explicit click (decision 1).
            vnavmesh = new Game.VnavmeshIpc(PluginInterface, Log);
            travel = new Game.TravelService(Framework, ClientState, Condition, ObjectTable, AetheryteList, DataManager, UnlockState, lifestream, vnavmesh, Log)
            {
                Print = line => ChatGui.Print(line, Strings.ChatTag),
                Index = () => gameLinks.Aetherytes,
                // Getting there faster (1.10): mount, fly and sprint as Settings › Integrations › Travel say.
                Options = () => Settings.TravelOptions(),
                MountChoice = () => Settings.TravelMountId,
            };
            gameLinks.Travel = travel;
            gameLinks.ShowWalk = () => Settings.ShowWalkToGiver;
            gameLinks.ShowGoTo = () => Settings.ShowGoToGiver;
            queryRunner = new QueryRunner(this, ui, Log);
            mainWindow = new MainWindow(this, ui, queryRunner, gameLinks, TextureProvider, PluginInterface, Log, RetryCatalogAsync);
            // The status line under the detail pane's pills while a trip runs: "Mounting…", "Flying to Varshahn…".
            mainWindow.AttachTravelStatus(gameLinks.TravelStatusText);
            windowSystem.AddWindow(mainWindow);
            // Subscribed before the window system so every window (the todo overlay and Nearby too) draws with
            // this frame's scale factors.
            Ui.Typography.Initialize(PluginInterface.UiBuilder.FontAtlas, Log);
            PluginInterface.UiBuilder.Draw += UpdateUiMetrics;
            PluginInterface.UiBuilder.Draw += DrawWindows;

            // The floating Undo (feature plan v6 S2) draws after every window, over the one it belongs to.
            PluginInterface.UiBuilder.Draw += DrawUndoToast;
            PluginInterface.UiBuilder.OpenMainUi += mainWindow.Toggle;

            // /tsukimichi and /tsuki, plus /ts, /moon and the player's own aliases (1.11.0, A12); an alias Dalamud, the
            // game or another plugin already answers to is skipped and named in Settings › Keyboard.
            var gameCommands = new Game.GameTextCommands(DataManager, Log);
            command = new TsukimichiCommand(
                CommandManager,
                toggleMainWindow: mainWindow.Toggle,
                toggleGlyphWindow: glyphDebugWindow.Toggle,
                search: mainWindow.SearchAndPrint,
                userAliases: Settings.CommandAliases,
                isGameCommand: gameCommands.Contains,
                log: Log);
            // /UI
            // ---- Game state (T3.2/T3.3) ----
            InitializeGameState();
            // ---- end game state ----

            // No first-open freezes (feature plan v6 A11): the sheet indexes the Flight, Plan and Moonlit panes, travel
            // and the Duties section read are built now on workers, in the client's language; a pane shows its loading
            // line for the moment one takes, and never builds one on its first draw.
            var warmer = new Game.IndexWarmer(DataManager, Session, Strings.FlightAllZonesFormat, Log);
            _ = warmer.Start();
            gameLinks.AetheryteWarmup = warmer.Aetherytes;
            queryRunner.IconSheets = () => warmer.PaneIcons.Value;
            queryRunner.IconSheetsSettled = () => warmer.PaneIcons.IsDone;

            // Giver portraits (1.15, F2/F5): the index is warmed with the others; every plate shows its fallback until it lands.
            Ui.GiverPortraits.Index = () => warmer.Portraits.Value;
            Ui.GiverPortraits.Mode = () => Settings.GiverPortraits;
            Ui.GiverPortraits.PlaceOf = quest => quest.Issuer is { } issuer && gameLinks.Map(issuer.MapId) is { } map
                ? map.Region.Length > 0 && map.Region != map.PlaceName
                    ? string.Format(System.Globalization.CultureInfo.CurrentCulture, Ui.Strings.JournalPathFormat, map.Region, map.PlaceName)
                    : map.PlaceName
                : null;

            // "Open on…" (1.8.0): the shipped link table; the browser opens the pages, the plugin stays offline (decision 8).
            var externalIds = Core.Links.ExternalIds.Load(Paths.ExternalIdsFile);
            foreach (var warning in externalIds.Warnings)
            {
                Log.Warning("External links: {Warning}", warning);
            }

            gameLinks.ExternalIds = externalIds;
            gameLinks.SiteLanguage = () => Core.Links.ExternalLinks.SiteLanguage(Session.Bundle?.Language);
            linkConfirmWindow = new LinkConfirmWindow(gameLinks.OpenUrl);
            windowSystem.AddWindow(linkConfirmWindow);
            gameLinks.ConfirmLink = linkConfirmWindow.Ask;
            DiscordCopy.Settings = Settings;
            DiscordCopy.SaveSettings = () => Settings.Save(PluginInterface);

            // UI (session-dependent surfaces)
            var unlockReader = new Game.RewardUnlockReader(Session, DataManager, Framework, Log, CollectibleFlags!);
            moonlitPane = new MoonlitPane(Session, TextureProvider, unlockReader, Paths, Log, DataManager, Settings, PluginInterface, gameLinks) { Writer = Writer };
            MoonlitPane moonlit = moonlitPane;
            // Every reward's own game art (feature plan v6 G6), read at load off the frame; the rows show what the quest
            // data knows until it lands, then are built again with it.
            moonlit.Icons.Art = () => warmer.RewardArt.Value;
            moonlit.WarmCatalog();
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
            // unlock hint, server info bar entry) run only while this gate allows them, that is on the patch date they were play-tested on
            // (the csproj's TsukimichiTestedGameVersion; hotfixes count as the same patch) or with "Enable game hooks on this untested version" ticked for the running patch. It is
            // one shared service: anything else drawn beside a game addon, the Duty Finder unlock hint of 0.9.0 (P13)
            // first, takes this instance and follows its Changed event. The client version is read once here.
            var clientGameVersion = Game.DiagnosticBuilder.ReadClientGameVersion(DataManager, Log);
            var gate = new Core.Runtime.HookGate(Game.DiagnosticBuilder.TestedGameVersionText(), clientGameVersion, Settings.EnableHooksOnUntestedVersion);
            hookGateNotice = new Game.HookGateNotice(gate, ClientState, ChatGui, Log, () => Game.DiagnosticBuilder.ReadClientGameVersion(DataManager, Log));
            // The daily offer calls a game function, so it follows the same kill switch as the hooks.
            if (dailyOffers is not null)
            {
                dailyOffers.Gate = gate;
            }

            // So do the repeat flags each capture reads (QuestManager.IsQuestRepeatFlagSet).
            if (stateReader is not null)
            {
                stateReader.Gate = gate;
            }

            // And the aethernet shard attunement read (UIState.IsAetheryteUnlocked).
            travel.Gate = gate;

            // Hand-in items (1.6.0): the detail pane's Hand in section, the "Needed for" hint and menu entry, Moonlit's
            // relic ownership through Allagan Tools, and the Artisan and GatherBuddy hand-offs (decision 1).
            allaganTools = new Game.AllaganToolsIpc(PluginInterface, Log);
            artisan = new Game.ArtisanIpc(PluginInterface, Log);
            gatherBuddy = new Game.GatherBuddyCommands(PluginInterface, CommandManager, Log);
            unlockReader.Allagan = allaganTools;
            unlockReader.AllaganEnabled = () => Settings.HandInAllaganTools;
            var handInStock = new Game.HandInStock(ClientState, Framework, gate, Log) { Allagan = allaganTools, AllaganEnabled = () => Settings.HandInAllaganTools };
            mainWindow.AttachHandIns(handInStock, artisan, gatherBuddy);
            var handIns = new Core.HandIn.HandInIndexSource(() => Session.Bundle?.Catalog);
            hoverHint = new HoverHint(GameGui, Session, unlockReader, rewardLookup, gate, Log)
            {
                Enabled = Settings.ItemHintsEnabled,
                HandIns = handIns,
                NeededForEnabled = () => Settings.ItemNeededForEnabled,
            };
            PluginInterface.UiBuilder.Draw += DrawHoverHint;
            itemHooks = new Game.ItemHooks(ContextMenu, rewardLookup, quest =>
            {
                mainWindow.IsOpen = true;
                mainWindow.BringToFront();
                MoonlitPane.Reveal(ui, quest);
            }, gate, Log)
            {
                Enabled = Settings.ItemContextMenuEnabled,
                QuestName = quest => Session.LiveSpoilers.DisplayName(quest),
                HandIns = handIns,
                // The item is in the logged-in character's inventory: its states decide what is open.
                NeededStates = () => Session.LiveStates,
                NeededForEnabled = () => Settings.ItemNeededForEnabled,
            };
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
            // What every quest opens (feature plan v6 K1): built once per catalog off the frame, from the client's own
            // sheets (warps, map regions, aethernet gates, objectives) and the shipped and curated data. The unlock
            // rows' map and Duty Finder calls take the same kill switch as the hooks.
            questUnlocks = BuildQuestUnlocksSource(warmer);
            queryRunner.Unlocks = questUnlocks;
            moonlit.Unlocks = questUnlocks;
            moonlit.UnlockReach = () => queryRunner.UnlockReach;
            gameLinks.GameCallsAllowed = () => gate.HooksAllowed;
            // A row or tile the sheets give no icon of its own (a title) wears its kind's menu icon, as Moonlit's kinds list does.
            mainWindow.AttachUnlocks(
                entry => unlockReader.IsObtained(entry),
                (quest, entry) =>
                {
                    var icon = moonlit.Icons.Resolve(quest, entry);
                    return icon != 0 ? icon : moonlit.Icons.KindIcon(entry.Kind).IconId;
                });
            // Hero banners (V4): every quest's banner through the fallback chain, resolved off the frame once per catalog
            // and duty unlock index; the duty step reads the same index as the Duty Finder hint.
            banners = new Core.Ui.BannerIndexSource<Core.Unique.DutyUnlockIndex>(
                () => Session.Bundle?.Catalog,
                () => dutyUnlocks.Current,
                (catalog, duties) => BannerSources.Build(DataManager.Excel, catalog, duties).Resolve(catalog),
                onError: ex => Log.Warning(ex, "Hero banners unavailable; quests show their own banner or category art"));
            mainWindow.AttachBanners(banners);
            dutyFinderPanel = new DutyFinderPanel(dutyFinderHint, gameLinks, TextureProvider, quest =>
            {
                mainWindow.IsOpen = true;
                mainWindow.BringToFront();
                MoonlitPane.Reveal(ui, quest);
            });
            PluginInterface.UiBuilder.Draw += DrawDutyFinderPanel;
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

            // Patch-day honesty (1.5.0): quests the shipped quest_patches.json does not list are new since the data was
            // built. The main window's "Game updated" strip, the Added in filter's New since data and the About line read
            // one report per catalog; the data's game version is the one quest_patches.json was written against.
            var freshness = new Game.DataFreshnessSource(
                () => Session.Bundle?.Catalog,
                questPatches,
                questPatches.GameVersion.Length > 0 ? questPatches.GameVersion : Session.UniqueRewards.GameVersion,
                clientGameVersion,
                Log);
            Freshness = freshness;
            diagnostics.Freshness = () => freshness.Current;
            mainWindow.AttachFreshness(freshness);

            // Questionable cross-check (V2-17): the detail pane's "Questionable agrees" line, the diagnostic block's
            // "questionable:" line, and the opt-in "Add to Questionable priority" behind Settings › Integrations.
            questionable = new Game.QuestionableIpc(PluginInterface, Log);
            Game.QuestionableIpc questionableIpc = questionable;
            diagnostics.CrossCheck = quest => questionableIpc.Check(quest, Session);
            // Send to Questionable, Start and Stop, the list and path badges and the live status (1.6.0, decision 1):
            // every pane that offers them shares one QuestionableActions; the result of a send is a chat line.
            var questionableActions = new QuestionableActions(questionableIpc, Session, Settings, () => Settings.Save(PluginInterface), line => ChatGui.Print(line, Ui.Strings.ChatTag));
            diagnostics.CrossCheckMore = quest => questionableIpc.Wider(quest, (Session.States.TryGetValue(quest.RowId, out var evaluation) ? evaluation : null), Session.IsLive, Session.Version, questionableActions.FestivalRunning(quest));
            mainWindow.AttachQuestionable(questionableIpc, () => Settings.QuestionableHandoff, questionableActions);

            // AutoDuty and Quest Map (decision 1): the detail pane's Duties section ("Run with AutoDuty", Duty Support or
            // Trust unless Settings allows the Duty Finder) and "Open in Quest Map"; /tsuki why points at the latter. The
            // duty index is read from the sheets once, in the client's language, on a worker started now: the draw only
            // reads it, and the Duties section stays hidden for the moment it builds.
            var autoDuty = new Game.AutoDutyIpc(PluginInterface, companions, Log);
            companionSetup.UseGates(Core.Companions.CompanionPlugin.AutoDuty, new Game.AutoDutySettingGates(PluginInterface, autoDuty, Log));
            var questMap = new Game.QuestMapIpc(PluginInterface, companions, Log);
            var dutyRuns = warmer.DutyRuns;
            mainWindow.AttachCompanions(
                companions,
                autoDuty,
                questMap,
                () => dutyRuns.Value,
                rowId => moonlit.Catalog.ForQuest(rowId),
                // Dalamud's IUnlockState, as the Moonlit reads use it: no game call of Tsukimichi's own from the draw.
                instanceContentId => CollectibleFlags?.IsUnlocked(Core.Model.RewardKind.Instance, instanceContentId),
                () => Settings.AutoDutyAllowDutyFinder);
            why.QuestMap = questMap;
            // Walk to giver and Go to giver wait while Questionable or AutoDuty drives the character (both move it
            // through vnavmesh too). Both reads are cached inside their IPC wrappers.
            gameLinks.QuestionableRunning = () => questionableIpc.PollStatus().Running;
            gameLinks.AutoDutyRunning = () => !autoDuty.IsStopped;
            // Either one started from its own window during a trip takes the character over: the trip ends and leaves
            // vnavmesh to it. The other way round, Start Questionable and Run with AutoDuty wait while a trip runs.
            travel.QuestionableRunning = gameLinks.QuestionableRunning;
            travel.AutoDutyRunning = gameLinks.AutoDutyRunning;
            questionableActions.Traveling = () => travel.JourneyActive;
            // Questionable runs its "command after stop" (default /li auto) on any stop asked over IPC: Stop says so.
            questionableActions.CommandAfterStop = companionSetup.QuestionableCommandAfterStop;
            questionableActions.CommandAfterStopNow = companionSetup.QuestionableCommandAfterStopNow;
            // /tsuki stop (1.11.0, A1): one Stop for every hand-off, for a macro or a single key, through each Stop
            // button's own call; one chat line says what stopped.
            stopCommand = new StopCommand(Framework, travel, lifestream, autoDuty, artisan, questionableActions, () => questionableIpc.PollStatus().Running, gameLinks.PrintText, Log);
            command.Stop = stopCommand.Run;
            mainWindow.AttachDiagnostics(diagnostics);

            // Journal text (P9): the detail pane's Journal card, and with Settings › Journal text the search box's journal
            // words, from an index kept per game version under the config directory.
            QuestText = new Game.QuestTextService(DataManager, SeStringEvaluator, ClientState, Log, Paths.ConfigDir, clientGameVersion);
            QuestText.SetEnabled(Settings.JournalTextSearch);
            mainWindow.AttachQuestText(QuestText);
            var report = new ReportCommand(Session, ui, gameLinks, diagnostics, Log);
            command.Report = report.Run;
            // /tsuki route [quest name] (1.7.0) opens the route window through UiState, as Route to this does; an
            // unknown word that finds nothing gets "Did you mean /tsuki …?" in chat.
            command.Route = new RouteCommand(Session, ui, gameLinks).Run;
            command.Print = gameLinks.PrintText;

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
            charactersPane = new CharactersPane(Session, Paths, Log, Snapshots.Load, Roster, Settings, () => Settings.Save(PluginInterface), DataManager, TextureProvider);
            charactersPane.MoonlitCounts = moonlitPane.CountsFor;
            charactersPane.UniqueRewards = () => moonlit.Catalog;
            charactersPane.Pins = queryRunner;
            charactersPane.Links = gameLinks;
            charactersPane.Questionable = questionableActions;
            // Role, society, Grand Company and achievement icons (UI-5d), read off the frame; Moonlit's for the collection.
            charactersPane.IconSheetsSource = () => warmer.PaneIcons.Value;
            mainWindow.AttachIconSheets(() => warmer.PaneIcons.Value);
            charactersPane.MoonlitIcons = moonlitPane.Icons;
            mainWindow.AttachPanes(moonlitPane, charactersPane);
            mainWindow.AttachOverrides(moonlitPane);
            // Multibox (D11): pins and overrides another game client saved are merged in as they land.
            if (Multibox is not null)
            {
                Multibox.UserFilesChanged += OnUserFilesChanged;
            }
            // The followed route (1.6.0): stored in Settings, rebuilt from the owner's states; its map flag follows the
            // next stop as steps are turned in, through a game call that obeys the hooks' kill switch.
            activeRoutes = new Game.ActiveRouteService(Settings, Session, gameLinks, new Game.MapFlag(Log) { Gate = gate }, () => Settings.Save(PluginInterface), Log);
            Game.ActiveRouteService followed = activeRoutes;
            // Unlock route (P6): the detail pane, a Moonlit row's menu, the Characters job rows, My blues, the Duty
            // Finder panel and the todo overlay's pins ask UiState for it.
            routeWindow = new RouteWindow(Session, queryRunner, gameLinks, followed, quest =>
            {
                mainWindow.IsOpen = true;
                mainWindow.BringToFront();
                MoonlitPane.Reveal(ui, quest);
            });
            routeWindow.Questionable = questionableActions;
            windowSystem.AddWindow(routeWindow);
            ui.RouteRequested += routeWindow.Show;
            RouteWindow routes = routeWindow;
            // "Route to unlock" beside the Duty Finder: every quest that opens the selected duty.
            dutyFinderPanel.OpenRoute = model =>
            {
                if (Session.Bundle is not { } bundle)
                {
                    return;
                }

                var duty = Core.Route.RouteTarget.ForDuty(bundle.Catalog, Core.Model.RewardKind.DutyUnlock, model.ContentFinderConditionId, model.DutyName, moonlit.Catalog.All);
                var quests = new System.Collections.Generic.List<uint>(duty.QuestRowIds);
                foreach (var rowId in model.AllQuestRowIds)
                {
                    if (!quests.Contains(rowId))
                    {
                        quests.Add(rowId);
                    }
                }

                ui.OpenRoute(duty with { QuestRowIds = quests, Icon = model.Icon });
            };
            // The flight index (a few small sheets) is warmed at load; the pane says it is reading them until it lands.
            flightPane = new FlightPane(Session, unlockReader, gameLinks, TextureProvider, Log, () => ClientState.TerritoryType, () => warmer.Flight.IsDone ? warmer.Flight.Value ?? FlightIndex.Empty : null);
            mainWindow.AttachFlight(flightPane);
            // Clear my blues (P3): the duty kinds (ContentFinderCondition) are warmed at load; the plan's tags are built
            // off the frame once they and the catalog are in.
            planSource = new PlanSource(Session, () => warmer.Duties.IsDone ? warmer.Duties.Value ?? Core.Plan.PlanDuties.Empty : null, Log)
            {
                // Zones of a level band are walked region by region (1.6.0), the region read from the giver's map.
                RegionOfMap = mapId => gameLinks.Map(mapId)?.Region ?? string.Empty,
            };
            mainWindow.AttachPlan(new PlanPane(Session, planSource, gameLinks, Settings, () => Settings.Save(PluginInterface))
            {
                RewardEntries = () => moonlit.Catalog.All,
                Questionable = questionableActions,
                Textures = TextureProvider,
                IconSheets = () => warmer.PaneIcons.Value,
            });
            // Panels beside game windows (1.7.0): "Worth it?" on quest offers, "What this opened" on completions and the
            // Journal companion. Their reads take the same kill switch as the Duty Finder hint.
            PlanSource plans = planSource;
            gamePanels = new GamePanels(PluginInterface.UiBuilder, AddonLifecycle, GameGui, TargetManager, Settings,
                new Game.QuestBriefBuilder(Session, () => moonlit.Catalog, unlockReader, () => plans.Tags)
                {
                    QuestUnlocks = () => questUnlocks?.Current,
                    MoonlitIcon = moonlit.Icons.Resolve,
                    IconSheets = () => warmer.PaneIcons.Value,
                },
                queryRunner, gameLinks, gate, Log, quest =>
                {
                    mainWindow.IsOpen = true;
                    mainWindow.BringToFront();
                    MoonlitPane.Reveal(ui, quest);
                }, ui.OpenRoute);
            // Next stops (1.6.0): Ready quests batched by aetheryte, for the Tonight card and the todo overlay.
            var nextStops = new NextStopsSource(Session, gameLinks, queryRunner, planSource, followed, Settings, () => ClientState.TerritoryType);
            mainWindow.AttachNextStops(nextStops);
            chatNotifier = new Game.ChatNotifier(Session, Settings, Paths, gameLinks, ChatGui, Log) { QuestUnlocks = () => questUnlocks?.Current };
            // "Before you continue" (P5): the dashboard and the Tonight card lines, and the once-per-character chat line.
            var payoffGates = new Game.PayoffGateSource(Session, Log);
            var payoffLines = new PayoffGateLines(payoffGates, Session, Settings, CharacterBook);
            charactersPane.PayoffLines = payoffLines;
            mainWindow.AttachPayoffLines(payoffLines);
            // Planning extras (1.9.0): the level advisor, the main scenario catch-up and the allied society board.
            var catchUpDuties = PlanningSource.DutySource(() => Session.Curated, () => moonlit.Catalog, () => dutyRuns.Value);
            var planning = new PlanningSource(Session, gameLinks, catchUpDuties);
            charactersPane.Planning = planning;
            mainWindow.AttachPlanning(planning);
            chatNotifier.PayoffGates = payoffGates;
            chatNotifier.CharacterSettings = CharacterBook;

            configWindow = new ConfigWindow(Settings, Session, PluginInterface, diagnostics, _ => ui.MarkQueryDirty());
            configWindow.Language = loc;
            configWindow.RunNextTick = action => _ = Framework.RunOnTick(action);
            configWindow.Overrides = moonlitPane;
            configWindow.Roster = Roster;
            configWindow.QuestText = QuestText;
            // Exports (P12): Settings › Data › Export and /tsuki export write local files; nothing is uploaded.
            var exportService = new Game.ExportService(Session, Settings, Paths, unlockReader, () => moonlit.Catalog, diagnostics.PluginVersion, diagnostics.ClientGameVersion, Log)
            {
                Ids = externalIds,
            };
            configWindow.Export = new ExportSection(Settings, exportService, () => Settings.Save(PluginInterface), Log);
            command.Export = new ExportCommand(exportService, Settings, gameLinks).Run;
            // A filing change rebuilds the catalog off-thread; the session swaps it in on the framework thread.
            configWindow.JournalFilingChanged = filing => _ = RetryCatalogAsync();
            configWindow.RetryCatalog = () => _ = RetryCatalogAsync();
            Game.WotsitIpc wotsitIpc = wotsit;
            configWindow.WotsitToggled = enabled => wotsitIpc.Enabled = enabled;
            if (hoverHint is { } hint) { configWindow.ItemHintsToggled = enabled => hint.Enabled = enabled; }
            if (itemHooks is { } hooks) { configWindow.ItemContextMenuToggled = enabled => hooks.Enabled = enabled; }
            if (npcHooks is { } npcMenu) { configWindow.NpcContextMenuToggled = enabled => npcMenu.Enabled = enabled; }
            if (dutyFinderHint is { } dutyHint) { configWindow.DutyFinderHintToggled = enabled => dutyHint.Enabled = enabled; }
            configWindow.HookGate = gate;
            configWindow.OwnedMounts = OwnedMounts;
            gamePanels.Attach(configWindow);
            configWindow.Companions = companions;
            configWindow.CompanionSetup = companionSetup;
            configWindow.Questionable = questionableIpc;
            configWindow.Nearby = discoveryWindow;
            var settingsWindow = configWindow;
            discoveryWindow.OpenSettings = () => settingsWindow.OpenAt(Core.Ui.SettingsSection.InGame, Core.Ui.SettingsAnchor.Nearby);
            InitializeInGame(gate, rewardLookup, handIns, moonlit);
            InitializeCollector(unlockReader);
            windowSystem.AddWindow(configWindow);
            PluginInterface.UiBuilder.OpenConfigUi += configWindow.Toggle;
            command.ToggleConfigWindow = configWindow.Toggle;
            configWindow.Command = command;

            // Todo overlay (V2-13): follows Settings.TodoOverlayEnabled; /tsuki todo and the settings window flip it.
            todoOverlay = new TodoOverlay(Settings, Session, gameLinks, quest =>
            {
                mainWindow.IsOpen = true;
                mainWindow.BringToFront();
                MoonlitPane.Reveal(ui, quest);
            }, ClientState, Condition, queryRunner, PluginInterface);
            todoOverlay.Plan = planSource;
            todoOverlay.Questionable = questionableActions;
            todoOverlay.ShowPins = mainWindow.ShowPinned;
            todoOverlay.ActiveRoutes = followed;
            todoOverlay.NextStops = nextStops;
            todoOverlay.OpenRoute = ui.OpenRoute;
            todoOverlay.ShowFollowedRoute = () => routes.ShowFollowed();
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
            // Last of the draw handlers: the tooltips and popups Tsukimichi opened this frame fade in (feature plan v6 U8).
            PluginInterface.UiBuilder.Draw += Ui.PopupFade.EndFrame;
            // Enter, Backspace and Esc are kept from the game while the tour card has the keyboard (the arrows never are).
            tutorial.KeyState = KeyState;
            Framework.Update += tutorial.ConsumeKeys;
            // The Read chapter selects a real quest (1.7.0): the next main scenario quest when Blocked, else a Blocked row.
            tutorial.SampleQuest = mainWindow.TourSampleQuest;
            mainWindow.AttachTutorial(tutorial);

            // The rail's Overlay and Nearby buttons and the first-pin prompt (1.7.0).
            TodoOverlay overlay = todoOverlay;
            DiscoveryWindow nearbyWindow = discoveryWindow;
            void OpenNearby()
            {
                nearbyWindow.IsOpen = true;
                nearbyWindow.BringToFront();
            }

            mainWindow.AttachPlay(overlay.ToggleEnabled, () => Settings.TodoOverlayEnabled, nearbyWindow.Toggle, () => nearbyWindow.IsOpen);
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
                OpenSettings: configWindow.Toggle,
                ToggleTodo: overlay.ToggleEnabled,
                OpenNearby: OpenNearby,
                ShowSetup: mainWindow.ShowSetup);
            helpWindow = new HelpWindow(helpActions, PluginInterface);
            command.StartTour = helpActions.StartTutorial;
            command.ShowTab = helpActions.ShowTab;
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

            // "Set up your road" (1.7.0, decision 7): once on a fresh install, after the tour offer; Help reopens it.
            // Each switch applies at once and tells the service that follows it, as Settings does.
            mainWindow.AttachSetup(new SetupCard(Settings, PluginInterface, Log, BuildSetupToggles(overlay, nearbyWindow), () => settingsWindow.OpenAt(Core.Ui.SettingsSection.Automation, Core.Ui.SettingsAnchor.CompanionPlugins)));

            // "Since you were away" (P7): the stored captures are kept from before this login's first save; the card
            // sits above the detail pane (after What's new) and the Characters dashboard opens it for any character.
            welcomeBack = new Game.WelcomeBackSource(Session, Snapshots, ClientState, Settings, Log) { CatchUpDuties = catchUpDuties };
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
            })
            {
                // 1.8.0: the reward and duty lookups the Moonlit and Duty Finder pieces use, the game's own owned
                // answers, and the pins (Tsukimichi's own list only).
                Rewards = () => rewardLookup.Current,
                DutyUnlocks = () => dutyUnlocks.Current,
                LiveOwned = entry => unlockReader.CanReadLive ? unlockReader.IsObtained(entry) : null,
                PinsOf = queryRunner.PinsOf,
                SetPin = queryRunner.SetPin,
                // Any character's pins, not the viewed one's: GetPins answers for the logged-in character while an alt is on view.
                PinsVersion = () => queryRunner.AllPinsVersion,
            };

            // /tsuki ipc (1.8.0, not in /tsuki help): every gate with its subscriber count and a test-call box.
            ipcWindow = new IpcWindow(ipcProvider);
            windowSystem.AddWindow(ipcWindow);
            command.ToggleIpcWindow = ipcWindow.Toggle;
            // /UI

            // Last: nothing runs per frame before the plugin is whole.
            StartFrameworkUpdates();
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
    /// The "Set up your road" card's switches (1.7.0), each applied and saved at once and passed on to the service that
    /// follows it, as Settings does. The overlay is never among Recommended (decision 7).
    /// </summary>
    private SetupToggle[] BuildSetupToggles(TodoOverlay overlay, DiscoveryWindow nearby)
    {
        void Save() => Settings.Save(PluginInterface);
        return
        [
            new("##setupOverlay", static () => Strings.Setup.OverlayLabel, static () => Strings.Setup.OverlayValue,
                () => Settings.TodoOverlayEnabled,
                on =>
                {
                    if (Settings.TodoOverlayEnabled != on)
                    {
                        overlay.ToggleEnabled();
                    }
                },
                Recommended: false),
            new("##setupNotice", static () => Strings.Setup.NoticeLabel, static () => Strings.Setup.NoticeValue,
                () => Settings.ChatNoticeNewlyAvailable,
                on =>
                {
                    Settings.ChatNoticeNewlyAvailable = on;
                    Save();
                },
                Recommended: true),
            new("##setupOpened", static () => Strings.Setup.OpenedLabel, static () => Strings.Setup.OpenedValue,
                () => Settings.ChatNoticeOpened,
                on =>
                {
                    Settings.ChatNoticeOpened = on;
                    Save();
                },
                Recommended: true),
            new("##setupServerBar", static () => Strings.Setup.ServerBarLabel, static () => Strings.Setup.ServerBarValue,
                () => nearby.Settings.ShowDtrEntry,
                on =>
                {
                    nearby.Settings.ShowDtrEntry = on;
                    nearby.SettingsChanged(rowsChanged: false);
                },
                Recommended: true),
            new("##setupItemHints", static () => Strings.Setup.ItemHintsLabel, static () => Strings.Setup.ItemHintsValue,
                () => Settings.ItemHintsEnabled,
                on =>
                {
                    Settings.ItemHintsEnabled = on;
                    Save();
                    if (hoverHint is { } hint)
                    {
                        hint.Enabled = on;
                    }
                },
                Recommended: true),
            new("##setupDutyFinder", static () => Strings.Setup.DutyFinderLabel, static () => Strings.Setup.DutyFinderValue,
                () => Settings.DutyFinderHintEnabled,
                on =>
                {
                    Settings.DutyFinderHintEnabled = on;
                    Save();
                    if (dutyFinderHint is { } dutyHint)
                    {
                        dutyHint.Enabled = on;
                    }
                },
                Recommended: true),
        ];
    }

    /// <summary>
    /// Unload, and the best-effort unwind after a failed constructor: every step is isolated, so one failure can neither
    /// hide another nor leave a later hook (context menus, addon listeners, Framework.Update, IPC) subscribed into an
    /// unloaded assembly. The catalog builds are cancelled first, so they wind down while the rest unloads. The waits at
    /// the end (the writer's last saves, the multibox loop, the catalog builds) share one <see cref="DisposeWait"/>
    /// whose clock starts at the writer's drain, so nothing before it eats the final saves' time; the journal index's
    /// short wait comes before it (worst case in all: see <see cref="DisposeWait"/>).
    /// </summary>
    private void TearDown()
    {
        // Before anything else: nothing below needs a build, and a cancelled one stops while the windows go.
        Unwind("catalog build cancel", CancelCatalogBuilds);
        var budget = new Core.Runtime.WaitBudget(DisposeWait);

        // Other plugins stop reaching in first, before anything they could reach is torn down.
        Unwind("tsukimichi ipc", () => ipcProvider?.Dispose());
        Unwind("localization", () =>
        {
            Localization.Loc.Changed -= OnTextChanged;
            loc?.Dispose();
        });
        Unwind("command", () => command?.Dispose());
        Unwind("stop command", () => stopCommand?.Dispose());
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
                PluginInterface.UiBuilder.Draw -= DrawHoverHint;
            }

            if (dutyFinderPanel is not null)
            {
                PluginInterface.UiBuilder.Draw -= DrawDutyFinderPanel;
            }

            PluginInterface.UiBuilder.Draw -= Ui.PopupFade.EndFrame;
            PluginInterface.UiBuilder.Draw -= DrawUndoToast;
            PluginInterface.UiBuilder.Draw -= DrawWindows;
            PluginInterface.UiBuilder.Draw -= UpdateUiMetrics;
        });
        Unwind("windows", windowSystem.RemoveAllWindows);
        Unwind("settings window", () => configWindow?.Dispose());
        Unwind("fonts", Ui.Typography.Dispose);
        Unwind("banner grades", Ui.BannerGrading.Dispose);
        Unwind("portrait grades", Ui.PortraitGrading.Dispose);
        Unwind("in the game", DisposeInGame);
        Unwind("item hooks", () => itemHooks?.Dispose());
        Unwind("npc hooks", () => npcHooks?.Dispose());
        Unwind("duty finder hint", () => dutyFinderHint?.Dispose());
        Unwind("game panels", () => gamePanels?.Dispose());
        Unwind("hook gate notice", () => hookGateNotice?.Dispose());
        Unwind("todo lock notice", () => todoLockNotice?.Dispose());
        Unwind("since you were away", () => welcomeBack?.Dispose());
        Unwind("todo overlay", () => todoOverlay?.Dispose());
        Unwind("followed route", () => activeRoutes?.Dispose());
        Unwind("server bar entry", () => dtrEntry?.Dispose());
        Unwind("nearby window", () => discoveryWindow?.Dispose());
        Unwind("glyph window", () => glyphDebugWindow?.Dispose());
        Unwind("main window", () => mainWindow?.Dispose());
        Unwind("wotsit ipc", () => wotsit?.Dispose());
        Unwind("questionable ipc", () => questionable?.Dispose());
        Unwind("moonlit pane", () => moonlitPane?.Dispose());
        Unwind("chat notifier", () => chatNotifier?.Dispose());
        Unwind("query runner", () => queryRunner?.Dispose());
        Unwind("journal text", () => QuestText?.Dispose());
        // A walk or Go to giver this plugin started stops before the IPC wrappers go.
        Unwind("travel", () => travel?.Dispose());
        Unwind("interior entrances", () => gameLinks?.StopWarmingEntrances());
        Unwind("vnavmesh ipc", () => vnavmesh?.Dispose());
        Unwind("lifestream ipc", () => lifestream?.Dispose());
        Unwind("allagan tools ipc", () => allaganTools?.Dispose());
        Unwind("artisan ipc", () => artisan?.Dispose());
        Unwind("gatherbuddy commands", () => gatherBuddy?.Dispose());
        Unwind("companion setup", () => companionSetup?.Dispose());
        Unwind("companion plugins", () => companions?.Dispose());
        // Each of its steps is isolated on its own. Its save writer drain starts the budget's clock.
        DisposeGameState(budget);
        Unwind("catalog build", () => StopCatalogBuild(budget.Remaining()));
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
    /// After <see cref="CancelCatalogBuilds"/>: waits up to <paramref name="wait"/> (what is left of the unload's
    /// <see cref="DisposeWait"/>) for the builds to stop and releases the token source. Only the builds themselves are
    /// waited on, not a rebuild's hand-off to the session (a no-op once unloading).
    /// </summary>
    private void StopCatalogBuild(TimeSpan wait)
    {
        CancellationTokenSource cts;
        lock (catalogBuildLock)
        {
            cts = catalogCts;
        }

        // Already cancelled by the unload's first step; again in case that step failed.
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
