using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Umbra;

namespace Tsukimichi.Game;

/// <summary>
/// Umbra, driven through Umbra's own code by reflection (<see cref="IUmbraControl"/>): the same members Umbra's
/// settings call, so Umbra's own checks, saving and loading run. Written against Umbra 3.1.18.0: every member and its
/// shape was checked against the metadata of the installed 3.1.18.0 <c>Umbra.dll</c> and <c>Umbra.Common.dll</c>
/// (that check is the authority; the behaviour was read in the source at tag 3.1.18):
/// <list type="bullet">
/// <item>custom plugins: <c>ConfigManager.Set("CustomPlugins.Enabled", …)</c>, what Umbra's "I agree" button calls
/// (SettingsWindowPluginsModule.cs), then <c>PluginRepository.LoadPluginEntries()</c>, which Umbra runs at start, so the
/// player's stored add-ons are in memory before the list is saved again;</item>
/// <item>the repository: <c>PluginFetcher.Fetch</c>, <c>PluginFetcher.DownloadRelease</c> and <c>PluginRepository.AddEntry</c>,
/// in the order Umbra's "Add repository" runs them (RepositoryInstallerNode.cs); <c>PluginRepository.RemoveEntry</c> as
/// Umbra's Remove button;</item>
/// <item>loading: <c>Framework.Restart()</c>, Umbra's own Restart button (SettingsWindow.cs); Umbra loads add-ons only
/// when it starts (PluginManager.LoadCustomPlugins);</item>
/// <item>the widget: <c>WidgetManager.CreateWidget(id, panel, …)</c>, as Umbra's "Add widget" (WidgetControlColumnNode.cs),
/// and <c>RemoveWidget</c>; both save the active toolbar profile themselves;</item>
/// <item>which profile: <c>ConfigManager.GetActiveProfileName()</c> and <c>Framework.LocalCharacterId</c>.</item>
/// </list>
/// Umbra saves through its own <c>ConfigManager</c>; Tsukimichi never writes Umbra's files.
/// <para>
/// <b>Version guard.</b> <see cref="Open"/> finds Umbra's assemblies in their load context, checks that exactly one Umbra
/// is live (its framework compiled for the logged-in character), and resolves every member by name with its exact
/// parameter and return types into the session's own <see cref="Members"/>. Any member missing or changed stops the
/// setup before anything changes (<see cref="UmbraFailure.UnknownUmbra"/>), whatever Umbra's version number says. Every
/// call runs on the framework thread, as Umbra's own buttons do. A restart still running after its limit refuses every
/// later session until it ends; every wait is cancelled when Tsukimichi unloads.
/// </para>
/// </summary>
public sealed class UmbraControl : IUmbraControl, IDisposable
{
    /// <summary>How long Umbra may take to read the latest release on GitHub.</summary>
    public static readonly TimeSpan FetchLimit = TimeSpan.FromSeconds(30);

    /// <summary>How long Umbra may take to download and check the release.</summary>
    public static readonly TimeSpan DownloadLimit = TimeSpan.FromSeconds(120);

    /// <summary>How long Umbra's restart may take.</summary>
    public static readonly TimeSpan RestartLimit = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The wait before a restart: Umbra saves a changed setting 0.1 s after it changes, on a timer its restart disposes
    /// (ConfigManager.Set and Dispose), so a restart sooner could drop the change.
    /// </summary>
    public static readonly TimeSpan SaveSettle = TimeSpan.FromMilliseconds(500);

    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private const string CustomPluginsVariable = "CustomPlugins.Enabled";

    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly CancellationTokenSource lifetime = new();
    private volatile Task? restarting;
    private volatile bool disposed;

    public UmbraControl(IFramework framework, IPluginLog log)
    {
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Stops every later call and cancels every wait (Tsukimichi unloads): a run in progress ends at its next step.</summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        lifetime.Cancel();
        lifetime.Dispose();
    }

    public Task<UmbraOpened> Open() => OnFramework(() =>
    {
        if (restarting is { IsCompleted: false })
        {
            // An earlier restart outlived its limit and is still running: driving Umbra now would race it.
            return new UmbraOpened(UmbraFailure.NotRunning, null);
        }

        var (found, failure) = Find();
        return found is null ? new UmbraOpened(failure, null) : new UmbraOpened(UmbraFailure.None, new Session(this, found));
    });

    // ------------------------------------------------------------------ one run's session

    /// <summary>One run's handle on Umbra: the members it resolved, never replaced while it runs.</summary>
    private sealed class Session(UmbraControl owner, Members m) : IUmbraSession
    {
        public Task<UmbraLook> Look() => owner.OnFramework(() =>
        {
            var on = m.CustomPluginsEnabled.GetValue(null) is true;
            var listed = m.FindEntry.Invoke(null, [UmbraAddon.RepositoryOwner, UmbraAddon.RepositoryName]) is not null;
            var others = Entries(m).Count(entry => !IsOurs(m, entry));
            var manager = WidgetManager(m);
            var registered = m.GetWidgetInfo.Invoke(manager, [UmbraAddon.WidgetId]) is not null;
            var ours = Instances(m, manager)
                .Where(widget => string.Equals(InfoIdOf(m, widget), UmbraAddon.WidgetId, StringComparison.Ordinal))
                .Select(widget => IdOf(m, widget) ?? string.Empty)
                .ToList();
            var profile = m.ActiveProfile.Invoke(null, null) as string ?? UmbraSettings.DefaultProfile;
            var character = m.LocalCharacterId.GetValue(null) is ulong id ? id : 0UL;
            return new UmbraLook(on, listed, registered, ours.Count > 0, others, profile, character, ours);
        });

        public Task SetCustomPlugins(bool on) => owner.OnFramework(() =>
        {
            m.ConfigSet.Invoke(null, [CustomPluginsVariable, on, true]);
            if (on)
            {
                // Umbra reads its stored add-on list only at start, and only while custom plugins are on; read it now, as
                // its start would, so adding an entry saves the player's earlier ones too, not a list of one.
                m.LoadEntries.Invoke(null, null);
            }

            return true;
        });

        public async Task<UmbraFailure> AddRepository()
        {
            var fetch = await owner.OnFramework(() => Started(m.Fetch.Invoke(null, [UmbraAddon.RepositoryOwner, UmbraAddon.RepositoryName]))).ConfigureAwait(false);
            if (!await owner.Within(fetch, FetchLimit).ConfigureAwait(false))
            {
                return UmbraFailure.TimedOut;
            }

            if (fetch.IsFaulted || fetch.IsCanceled || ResultOf(fetch) is not { } pair)
            {
                return UmbraFailure.ReleaseUnreachable;
            }

            // (FetchResult, Release?): anything but NewerVersionAvailable with a release means Umbra found nothing to add.
            var result = pair.GetType().GetField("Item1")?.GetValue(pair);
            var release = pair.GetType().GetField("Item2")?.GetValue(pair);
            if (!Equals(result, m.NewerVersionAvailable) || release is null)
            {
                return UmbraFailure.ReleaseUnreachable;
            }

            var download = await owner.OnFramework(() => Started(m.Download.Invoke(null, [UmbraAddon.RepositoryOwner, UmbraAddon.RepositoryName, release]))).ConfigureAwait(false);
            if (!await owner.Within(download, DownloadLimit).ConfigureAwait(false))
            {
                return UmbraFailure.TimedOut;
            }

            if (download.IsFaulted || download.IsCanceled)
            {
                return UmbraFailure.ReleaseUnreachable;
            }

            // Umbra checked each file as it would load it (DownloadRelease): none passing means it would not take the release.
            if (ResultOf(download) is not IList entries || entries.Count == 0)
            {
                return UmbraFailure.ReleaseRefused;
            }

            return await owner.OnFramework(() =>
            {
                foreach (var entry in entries)
                {
                    m.AddEntry.Invoke(null, [entry, false]);
                }

                return m.FindEntry.Invoke(null, [UmbraAddon.RepositoryOwner, UmbraAddon.RepositoryName]) is null ? UmbraFailure.ReleaseRefused : UmbraFailure.None;
            }).ConfigureAwait(false);
        }

        public Task<int> RemoveRepository() => owner.OnFramework(() =>
        {
            var ours = Entries(m).Where(entry => IsOurs(m, entry)).ToList();
            foreach (var entry in ours)
            {
                m.RemoveEntry.Invoke(null, [entry]);
            }

            return ours.Count;
        });

        public async Task<UmbraFailure> Restart()
        {
            await Task.Delay(SaveSettle, owner.Token).ConfigureAwait(false);

            // Looked at again in the same framework call that restarts: Umbra or the player may have gone during the wait.
            var restart = await owner.OnFramework(() => Live(m) ? Started(m.Restart.Invoke(null, null)) : null).ConfigureAwait(false);
            if (restart is null)
            {
                owner.log.Information("Umbra setup: Umbra went away before its restart; nothing was restarted");
                return UmbraFailure.NotRunning;
            }

            owner.log.Information("Umbra setup: restarting Umbra's toolbar (Umbra's own Restart)");
            owner.restarting = restart;
            if (!await owner.Within(restart, RestartLimit).ConfigureAwait(false))
            {
                owner.log.Warning("Umbra setup: Umbra's restart is still running after {Seconds} s; nothing more is changed", RestartLimit.TotalSeconds);
                return UmbraFailure.RestartTimedOut;
            }

            if (restart.IsFaulted || restart.IsCanceled)
            {
                return UmbraFailure.Unexpected;
            }

            // Umbra's start catches its own errors (CrashLogger.Guard): it is back only if its services answer again.
            return await owner.OnFramework(() => Live(m) ? UmbraFailure.None : UmbraFailure.Unexpected).ConfigureAwait(false);
        }

        public Task<bool> PlaceWidget(string widgetId) => owner.OnFramework(() =>
        {
            var manager = WidgetManager(m);

            // Umbra's CreateWidget shows its crash window for a widget it doesn't know: only ever asked for a known one.
            if (m.GetWidgetInfo.Invoke(manager, [UmbraAddon.WidgetId]) is null)
            {
                return false;
            }

            m.CreateWidget.Invoke(manager, [UmbraAddon.WidgetId, UmbraAddon.WidgetPanel, null, widgetId, null, true]);
            return Instances(m, manager).Any(widget => string.Equals(IdOf(m, widget), widgetId, StringComparison.Ordinal));
        });

        public Task<bool> RemoveWidget(string widgetId) => owner.OnFramework(() =>
        {
            var manager = WidgetManager(m);
            if (!Instances(m, manager).Any(widget => string.Equals(IdOf(m, widget), widgetId, StringComparison.Ordinal)))
            {
                return false;
            }

            m.RemoveWidget.Invoke(manager, [widgetId, true]);
            return true;
        });
    }

    // ------------------------------------------------------------------ finding Umbra

    /// <summary>
    /// Umbra's members in the one live Umbra: the load context holding both Umbra and Umbra.Common whose framework is
    /// compiled (a context Dalamud is still unloading, or Umbra logged out, has its settings cleared and is not live).
    /// </summary>
    private (Members? Found, UmbraFailure Failure) Find()
    {
        var live = new List<Members>();
        var unknown = false;
        foreach (var context in AssemblyLoadContext.All)
        {
            Assembly? umbra = null;
            Assembly? common = null;
            foreach (var assembly in context.Assemblies)
            {
                switch (assembly.GetName().Name)
                {
                    case "Umbra":
                        umbra = assembly;
                        break;
                    case "Umbra.Common":
                        common = assembly;
                        break;
                }
            }

            if (umbra is null || common is null)
            {
                continue;
            }

            try
            {
                var found = Members.Resolve(umbra, common);
                if (Live(found))
                {
                    live.Add(found);
                }
            }
            catch (MissingMemberException ex)
            {
                unknown = true;
                log.Warning("Umbra setup: Umbra {Version} lacks {Member}; nothing was changed", umbra.GetName().Version?.ToString() ?? "?", ex.Message);
            }
        }

        if (live.Count == 1)
        {
            var found = live[0];
            if (!string.Equals(found.Version?.ToString(), UmbraAddon.TestedUmbraVersion, StringComparison.Ordinal))
            {
                log.Information("Umbra setup: Umbra {Version} (written against {Tested}); every member matched", found.Version?.ToString() ?? "?", UmbraAddon.TestedUmbraVersion);
            }

            return (found, UmbraFailure.None);
        }

        if (live.Count > 1)
        {
            log.Warning("Umbra setup: {Count} live copies of Umbra found; nothing was changed", live.Count);
            return (null, UmbraFailure.SeveralUmbras);
        }

        return (null, unknown ? UmbraFailure.UnknownUmbra : UmbraFailure.NotRunning);
    }

    /// <summary>Umbra is compiled and running for this character: its plugin interface is Umbra's, its settings answer, its toolbar exists.</summary>
    private static bool Live(Members m)
    {
        try
        {
            if (m.DalamudPlugin.GetValue(null) is not IDalamudPluginInterface plugin || !string.Equals(plugin.InternalName, UmbraSettings.InternalName, StringComparison.Ordinal))
            {
                return false;
            }

            m.ConfigGet.Invoke(null, [CustomPluginsVariable]);
            return m.Service.Invoke(null, [m.WidgetManagerType]) is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private CancellationToken Token
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return lifetime.Token;
        }
    }

    private static object WidgetManager(Members m) =>
        m.Service.Invoke(null, [m.WidgetManagerType]) ?? throw new InvalidOperationException("Umbra's toolbar is not running");

    private static IEnumerable<object> Entries(Members m) =>
        m.Entries.GetValue(null) is IEnumerable list ? list.Cast<object>().ToList() : [];

    private static IEnumerable<object> Instances(Members m, object manager) =>
        m.GetWidgetInstances.Invoke(manager, null) is IEnumerable list ? list.Cast<object>().ToList() : [];

    private static bool IsOurs(Members m, object entry) =>
        string.Equals(m.EntryOwner.GetValue(entry) as string, UmbraAddon.RepositoryOwner, StringComparison.OrdinalIgnoreCase)
        && string.Equals(m.EntryName.GetValue(entry) as string, UmbraAddon.RepositoryName, StringComparison.OrdinalIgnoreCase);

    private static string? IdOf(Members m, object widget) => m.WidgetId.GetValue(widget) as string;

    private static string? InfoIdOf(Members m, object widget) =>
        m.WidgetInfo.GetValue(widget) is { } info ? m.InfoId.GetValue(info) as string : null;

    private static Task Started(object? task) => task as Task ?? throw new InvalidOperationException("Umbra returned no task");

    private static object? ResultOf(Task task) => task.GetType().GetProperty("Result")?.GetValue(task);

    /// <summary>Whether <paramref name="task"/> ends within <paramref name="limit"/>; the timer is cancelled either way, and on unload.</summary>
    private async Task<bool> Within(Task task, TimeSpan limit)
    {
        using var timer = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var delay = Task.Delay(limit, timer.Token);
        var first = await Task.WhenAny(task, delay).ConfigureAwait(false);
        await timer.CancelAsync().ConfigureAwait(false);
        ObjectDisposedException.ThrowIf(disposed, this);
        return first == task;
    }

    private Task<T> OnFramework<T>(Func<T> work)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return framework.RunOnFrameworkThread(work);
    }

    /// <summary>Every member the steps use, resolved by name with its exact shape; <see cref="MissingMemberException"/> names the first that isn't there.</summary>
    private sealed record Members(
        Version? Version,
        PropertyInfo DalamudPlugin,
        PropertyInfo LocalCharacterId,
        MethodInfo Restart,
        MethodInfo Service,
        MethodInfo ConfigSet,
        MethodInfo ConfigGet,
        MethodInfo ActiveProfile,
        PropertyInfo CustomPluginsEnabled,
        MethodInfo FindEntry,
        PropertyInfo Entries,
        MethodInfo AddEntry,
        MethodInfo RemoveEntry,
        MethodInfo LoadEntries,
        PropertyInfo EntryOwner,
        PropertyInfo EntryName,
        MethodInfo Fetch,
        MethodInfo Download,
        object NewerVersionAvailable,
        Type WidgetManagerType,
        MethodInfo GetWidgetInfo,
        MethodInfo GetWidgetInstances,
        MethodInfo CreateWidget,
        MethodInfo RemoveWidget,
        PropertyInfo WidgetId,
        PropertyInfo WidgetInfo,
        PropertyInfo InfoId)
    {
        public static Members Resolve(Assembly umbra, Assembly common)
        {
            var framework = TypeOf(common, "Umbra.Common.Framework");
            var config = TypeOf(common, "Umbra.Common.ConfigManager");
            var pluginManager = TypeOf(umbra, "Umbra.Plugins.PluginManager");
            var repository = TypeOf(umbra, "Umbra.Plugins.Repository.PluginRepository");
            var fetcher = TypeOf(umbra, "Umbra.Plugins.Repository.PluginFetcher");
            var entry = TypeOf(umbra, "Umbra.Plugins.Repository.PluginEntry");
            var manager = TypeOf(umbra, "Umbra.Widgets.System.WidgetManager");
            var widget = TypeOf(umbra, "Umbra.Widgets.ToolbarWidget");
            var info = TypeOf(umbra, "Umbra.Widgets.WidgetInfo");
            var fetchResult = fetcher.GetNestedType("FetchResult", All) ?? throw new MissingMemberException("PluginFetcher.FetchResult");
            var release = fetcher.GetNestedType("Release", All) ?? throw new MissingMemberException("PluginFetcher.Release");
            if (!Enum.TryParse(fetchResult, "NewerVersionAvailable", false, out var newer) || newer is null)
            {
                throw new MissingMemberException("PluginFetcher.FetchResult.NewerVersionAvailable");
            }

            var entries = typeof(List<>).MakeGenericType(entry);
            var fetched = typeof(Task<>).MakeGenericType(typeof(ValueTuple<,>).MakeGenericType(fetchResult, typeof(Nullable<>).MakeGenericType(release)));
            return new Members(
                umbra.GetName().Version,
                Property(framework, "DalamudPlugin", typeof(IDalamudPluginInterface)),
                Property(framework, "LocalCharacterId", typeof(ulong)),
                Method(framework, "Restart", [], typeof(Task)),
                Generic(framework, "Service", [typeof(Type)]),
                Method(config, "Set", [typeof(string), typeof(object), typeof(bool)], typeof(void)),
                Generic(config, "Get", [typeof(string)]),
                Method(config, "GetActiveProfileName", [], typeof(string)),
                Property(pluginManager, "CustomPluginsEnabled", typeof(bool)),
                Method(repository, "FindEntryFromRepository", [typeof(string), typeof(string)], entry),
                Property(repository, "Entries", entries),
                Method(repository, "AddEntry", [entry, typeof(bool)], typeof(void)),
                Method(repository, "RemoveEntry", [entry], typeof(void)),
                Method(repository, "LoadPluginEntries", [], typeof(void)),
                Property(entry, "RepositoryOwner", typeof(string)),
                Property(entry, "RepositoryName", typeof(string)),
                Method(fetcher, "Fetch", [typeof(string), typeof(string)], fetched),
                Method(fetcher, "DownloadRelease", [typeof(string), typeof(string), release], typeof(Task<>).MakeGenericType(entries)),
                newer,
                manager,
                Method(manager, "GetWidgetInfo", [typeof(string)], info),
                Method(manager, "GetWidgetInstances", [], typeof(List<>).MakeGenericType(widget)),
                Method(manager, "CreateWidget", [typeof(string), typeof(string), typeof(int?), typeof(string), typeof(Dictionary<string, object>), typeof(bool)], typeof(void)),
                Method(manager, "RemoveWidget", [typeof(string), typeof(bool)], typeof(void)),
                Property(widget, "Id", typeof(string)),
                Property(widget, "Info", info),
                Property(info, "Id", typeof(string)));
        }

        private static Type TypeOf(Assembly assembly, string name) =>
            assembly.GetType(name, throwOnError: false) ?? throw new MissingMemberException(name);

        private static MethodInfo Method(Type type, string name, Type[] parameters, Type returns)
        {
            var method = type.GetMethod(name, All, binder: null, parameters, modifiers: null);
            return method is not null && method.ReturnType == returns ? method : throw new MissingMemberException($"{type.Name}.{name}");
        }

        /// <summary>A generic method of one type parameter, closed over <see cref="object"/>.</summary>
        private static MethodInfo Generic(Type type, string name, Type[] parameters)
        {
            var method = type.GetMethods(All).FirstOrDefault(m =>
                m.Name == name
                && m.IsGenericMethodDefinition
                && m.GetGenericArguments().Length == 1
                && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters));
            return method?.MakeGenericMethod(typeof(object)) ?? throw new MissingMemberException($"{type.Name}.{name}<T>");
        }

        private static PropertyInfo Property(Type type, string name, Type of)
        {
            var property = type.GetProperty(name, All);
            return property is not null && property.PropertyType == of && property.GetMethod is not null ? property : throw new MissingMemberException($"{type.Name}.{name}");
        }
    }
}
