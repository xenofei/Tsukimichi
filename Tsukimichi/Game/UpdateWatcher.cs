using System;
using System.Globalization;
using System.Threading.Tasks;
using Dalamud.Interface;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Updates;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// "Tsukimichi 1.23.0 is ready" (plan v8 U1; spec-1.22 U1): asks Dalamud whether a newer Tsukimichi is waiting, at login
/// and every <see cref="UpdateRules.Interval"/> while <see cref="Configuration.UpdateCheck"/> is on, and keeps the answer
/// in <see cref="Current"/> for the status-bar note, the moon icon's dot and Settings › About.
/// <para>
/// <b>No network of its own.</b> <see cref="IDalamudPluginInterface.CheckForUpdateAsync"/> "does not actually re-request
/// data from the remote repository": it reads the repository data Dalamud already refreshes about every ten minutes.
/// <b>Update</b> opens Dalamud's installer on "Can be updated" (<see cref="OpenInstaller"/>), and Dalamud installs;
/// Tsukimichi never downloads itself.
/// </para>
/// <para>
/// <b>Later</b> (<see cref="Later"/>) hides the note and the dot until a newer version appears; the version is kept in
/// <see cref="Configuration.UpdateDismissedVersion"/>. Nothing pops up, and a chat line is printed only with "Also say it
/// in chat" on (decision 6), once per version. Framework thread: the answer arrives on a worker and is applied on the
/// next framework tick.
/// </para>
/// </summary>
public sealed class UpdateWatcher : IDisposable, Core.Ui.IUpdateState
{
    /// <summary>The installer's search text: the plugin's name as Dalamud lists it.</summary>
    public const string InstallerSearch = "Tsukimichi";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly IChatGui chat;
    private readonly Configuration settings;
    private readonly IPluginLog log;
    private readonly string running;

    private DateTime? lastAskedUtc;
    private volatile bool asking;
    private volatile Answer? pending;
    private bool disposed;
    private bool warned;

    /// <summary>An answer from the worker, applied on the framework thread.</summary>
    private sealed record Answer(string? Version, string? Changelog, DateTime AtUtc);

    /// <param name="running">The running build's version ("1.22.0").</param>
    public UpdateWatcher(IDalamudPluginInterface pluginInterface, IFramework framework, IClientState clientState, IChatGui chat, Configuration settings, string running, IPluginLog log)
    {
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
        this.chat = chat ?? throw new ArgumentNullException(nameof(chat));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.running = UpdateRules.Normalise(running ?? string.Empty);
        clientState.Login += OnLogin;
        framework.Update += OnUpdate;
    }

    /// <summary>What is known about an update: the note and the dot show while <see cref="UpdateState.ShowsNote"/>.</summary>
    public UpdateState Current { get; private set; } = UpdateState.Unknown;

    /// <summary>Moves whenever <see cref="Current"/> changes (the status bar and the icon rebuild their words on it).</summary>
    public int Revision { get; private set; }

    /// <summary>Raised on the framework thread after <see cref="Current"/> changed.</summary>
    public event Action? Changed;

    /// <summary>
    /// The plain notes for a version (W's <c>WhatsNewNotes.For</c> once it is on main); null or empty falls back to the
    /// changelog Dalamud has (<c>PluginUpdate.Changelog</c>), which the manifest fills with the same plain notes.
    /// </summary>
    public Func<string, string?>? PlainNotes { get; set; }

    /// <summary>The moon icon's dot (H1): the ready version while its note shows; null when none is, or after Later.</summary>
    public string? ReadyVersion => Current.ShowsNote ? Current.Available : null;

    /// <summary>The running build's version, as the note compares it.</summary>
    public string Running => running;

    /// <summary>When Dalamud was last asked (UTC); null before the first answer this session.</summary>
    public DateTime? LastAskedUtc => Current.CheckedUtc;

    /// <summary>Update: Dalamud's installer on "Can be updated", searched for Tsukimichi. Dalamud does the install.</summary>
    public bool OpenInstaller()
    {
        try
        {
            return pluginInterface.OpenPluginInstallerTo(PluginInstallerOpenKind.UpdateablePlugins, InstallerSearch);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Dalamud's plugin installer could not be opened");
            return false;
        }
    }

    /// <summary>Later (×): hides the note and the dot until a newer version than this one appears. Saved at once.</summary>
    public void Later()
    {
        if (Current is not { Status: UpdateStatus.Ready, Available: { } version })
        {
            return;
        }

        settings.UpdateDismissedVersion = version;
        settings.Save(pluginInterface);
        Set(UpdateRules.Dismiss(Current));
    }

    /// <summary>The switch changed: on asks at once (the next tick), off forgets what was known.</summary>
    public void SettingChanged()
    {
        if (settings.UpdateCheck)
        {
            lastAskedUtc = null;
        }
        else if (Current.Status != UpdateStatus.None)
        {
            Set(UpdateRules.Off(Current));
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        clientState.Login -= OnLogin;
        framework.Update -= OnUpdate;
    }

    /// <summary>A login asks again (spec-1.22 U1: "at login and every 3 hours").</summary>
    private void OnLogin() => lastAskedUtc = null;

    private void OnUpdate(IFramework _)
    {
        if (disposed)
        {
            return;
        }

        if (pending is { } answer)
        {
            pending = null;
            Apply(answer);
        }

        var now = DateTime.UtcNow;
        if (asking || !clientState.IsLoggedIn || !UpdateRules.Due(settings.UpdateCheck, lastAskedUtc, now))
        {
            return;
        }

        lastAskedUtc = now;
        Ask();
    }

    private void Ask()
    {
        asking = true;
        Task<Dalamud.Plugin.PluginUpdate?> task;
        try
        {
            task = pluginInterface.CheckForUpdateAsync();
        }
        catch (Exception ex)
        {
            asking = false;
            WarnOnce(ex);
            return;
        }

        task.ContinueWith(
            t =>
            {
                try
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        var found = t.Result;
                        pending = new Answer(found?.Version?.ToString(), found?.Changelog, DateTime.UtcNow);
                    }
                    else
                    {
                        WarnOnce(t.Exception?.GetBaseException());
                    }
                }
                finally
                {
                    asking = false;
                }
            },
            TaskScheduler.Default);
    }

    /// <summary>Framework thread: the answer becomes the state; a newly ready version may print its chat line.</summary>
    private void Apply(Answer answer)
    {
        if (!settings.UpdateCheck)
        {
            return;
        }

        var notes = answer.Changelog;
        if (answer.Version is { } version && PlainNotes?.Invoke(UpdateRules.Normalise(version)) is { Length: > 0 } plain)
        {
            notes = plain;
        }

        var next = UpdateRules.Answer(running, answer.Version, notes, settings.UpdateDismissedVersion, answer.AtUtc);
        Set(next);
        if (next is { Status: UpdateStatus.Ready, Available: { } ready } && settings.UpdateChatLine
            && !string.Equals(settings.UpdateChatSaidVersion, ready, StringComparison.Ordinal))
        {
            settings.UpdateChatSaidVersion = ready;
            settings.Save(pluginInterface);
            try
            {
                chat.Print(string.Format(CultureInfo.CurrentCulture, Strings.UpdateChatFormat, ready), Strings.ChatTag);
            }
            catch (Exception ex)
            {
                log.Debug(ex, "The update chat line could not be printed");
            }
        }
    }

    private void Set(UpdateState next)
    {
        if (next == Current)
        {
            return;
        }

        Current = next;
        Revision++;
        Changed?.Invoke();
    }

    private void WarnOnce(Exception? ex)
    {
        if (warned)
        {
            log.Debug(ex, "Asking Dalamud for an update failed");
            return;
        }

        warned = true;
        log.Warning(ex, "Asking Dalamud for an update failed; further failures are logged at debug level");
    }
}
