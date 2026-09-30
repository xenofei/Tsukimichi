using System;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// The addon kill switch's (T20) voice: writes the <see cref="HookGate"/>'s decision to the log whenever it changes,
/// and prints <see cref="Strings.HooksPausedNotice"/> in chat once per load while the hooks are paused, at load when
/// already logged in or at the next login. Settings › Integrations shows the same line for as long as the pause lasts.
/// <para>
/// When the client's game version could not be read at load, the gate starts as unknown (hooks allowed); each login
/// reads it again and hands it to the gate, which re-applies every hook through <see cref="HookGate.Changed"/>.
/// Framework thread.
/// </para>
/// </summary>
public sealed class HookGateNotice : IDisposable
{
    private readonly HookGate gate;
    private readonly IClientState clientState;
    private readonly IChatGui chat;
    private readonly IPluginLog log;
    private readonly Func<string> readRunningVersion;
    private bool printed;
    private bool disposed;

    /// <param name="readRunningVersion">Reads the client's game version (empty when unreadable); called at login while the gate has none.</param>
    public HookGateNotice(HookGate gate, IClientState clientState, IChatGui chat, IPluginLog log, Func<string> readRunningVersion)
    {
        this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
        this.clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
        this.chat = chat ?? throw new ArgumentNullException(nameof(chat));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.readRunningVersion = readRunningVersion ?? throw new ArgumentNullException(nameof(readRunningVersion));

        LogDecision();
        gate.Changed += OnGateChanged;
        clientState.Login += OnLogin;
        TryPrint();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        gate.Changed -= OnGateChanged;
        clientState.Login -= OnLogin;
    }

    private void OnGateChanged()
    {
        LogDecision();
        TryPrint();
    }

    private void OnLogin()
    {
        if (gate.RunningVersion.Length == 0)
        {
            // Raises Changed (and so logs and prints) when the version read now moves the decision.
            gate.SetRunningVersion(readRunningVersion());
        }

        TryPrint();
    }

    private void LogDecision()
    {
        var decision = gate.Decision;
        if (decision.LogNote is not { } note)
        {
            log.Information("Game hooks on tested game {Version}", decision.RunningVersion);
        }
        else if (decision.Verdict == HookGateVerdict.Paused)
        {
            log.Warning("{Note}", note);
        }
        else
        {
            log.Information("{Note}", note);
        }
    }

    private void TryPrint()
    {
        if (disposed || printed || !gate.IsPaused || !clientState.IsLoggedIn)
        {
            return;
        }

        printed = true;
        try
        {
            chat.Print(Strings.HooksPausedNotice, Strings.ChatTag);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Chat print failed");
        }
    }
}
