using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Text.ReadOnly;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// What every panel drawn beside a game window (1.7.0: the quest offer, the quest complete and the Journal windows)
/// shares with the Duty Finder unlock hint: lifecycle listeners on one addon, registered only while the panel's
/// setting is on and the shared <see cref="HookGate"/> allows game hooks (and following its
/// <see cref="HookGate.Changed"/>), so nothing here touches game memory while paused; the window's open state from
/// PostSetup and PreFinalize; and its screen rectangle for placing the panel, which reads false while the window is
/// hidden (with the UI, in a cutscene) so the panel is never drawn over nothing.
/// <para>
/// The subclass reads what it needs in <see cref="Read"/>, called on PostSetup, PostRefresh and PostRequestedUpdate
/// (the window got new data), on PostReceiveEvent when <see cref="ReadOnEvents"/> (a click in the window), and on
/// PostUpdate at most every <see cref="PollInterval"/> milliseconds while <see cref="WantsPoll"/> (a change no event
/// announces, such as a keyboard selection). Every call is on the framework thread; a read failure is the subclass's
/// to log (<see cref="WarnOnce"/>).
/// </para>
/// </summary>
public abstract unsafe class AddonPanelSource : IDisposable
{
    private readonly IAddonLifecycle lifecycle;
    private readonly IGameGui gameGui;
    private readonly HookGate gate;
    private readonly IAddonLifecycle.AddonEventDelegate onSetup;
    private readonly IAddonLifecycle.AddonEventDelegate onChange;
    private readonly IAddonLifecycle.AddonEventDelegate onEvent;
    private readonly IAddonLifecycle.AddonEventDelegate onUpdate;
    private readonly IAddonLifecycle.AddonEventDelegate onFinalize;

    private bool enabled = true;
    private bool registered;
    private bool disposed;
    private bool warned;
    private long lastPoll;

    /// <param name="addonName">The game window the panel follows.</param>
    /// <param name="pollInterval">Milliseconds between PostUpdate reads while <see cref="WantsPoll"/>; 0 never polls.</param>
    protected AddonPanelSource(IAddonLifecycle lifecycle, IGameGui gameGui, HookGate gate, IPluginLog log, string addonName, long pollInterval)
    {
        this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        this.gameGui = gameGui ?? throw new ArgumentNullException(nameof(gameGui));
        this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
        Log = log ?? throw new ArgumentNullException(nameof(log));
        AddonName = addonName;
        PollInterval = pollInterval;
        onSetup = OnSetup;
        onChange = OnChange;
        onEvent = OnEvent;
        onUpdate = OnUpdate;
        onFinalize = OnFinalize;
    }

    /// <summary>The game window this panel follows.</summary>
    public string AddonName { get; }

    /// <summary>Milliseconds between PostUpdate reads while <see cref="WantsPoll"/>; 0 never polls.</summary>
    public long PollInterval { get; }

    /// <summary>Follows the panel's setting; off unregisters the listeners and forgets the window.</summary>
    public bool Enabled
    {
        get => enabled;
        set
        {
            if (enabled == value || disposed)
            {
                return;
            }

            enabled = value;
            Apply();
        }
    }

    /// <summary>Whether the listeners are registered now: the setting is on and the <see cref="HookGate"/> allows game hooks.</summary>
    public bool IsActive => registered;

    /// <summary>The window is open (set up and not finalized) as far as the listeners saw.</summary>
    protected bool IsOpen { get; private set; }

    protected IPluginLog Log { get; }

    /// <summary>Whether a click in the window (PostReceiveEvent) is worth a read.</summary>
    protected virtual bool ReadOnEvents => false;

    /// <summary>Whether PostUpdate should read at most every <see cref="PollInterval"/> now.</summary>
    protected virtual bool WantsPoll => false;

    /// <summary>
    /// Screen rectangle of the window, or false when it is not visible (hidden with the UI, closing, or unreadable;
    /// logged once). Framework thread; allocation-free.
    /// </summary>
    public virtual bool TryGetWindowRect(out ScreenRect rect) => TryGetAddonRect(AddonName, out rect);

    /// <summary>Registers the listeners if the setting and the gate allow; the subclass calls it once constructed.</summary>
    protected void Start()
    {
        gate.Changed += Apply;
        Apply();
    }

    /// <summary>Reads what the panel needs from the open window. Framework thread.</summary>
    protected abstract void Read(AtkUnitBase* addon);

    /// <summary>Forgets what was read: the window closed or the listeners went away.</summary>
    protected abstract void Forget();

    /// <summary>A visible addon's rectangle by name; false when it is closed, hidden or unreadable.</summary>
    protected bool TryGetAddonRect(string name, out ScreenRect rect)
    {
        rect = default;
        if (!registered || !IsOpen)
        {
            return false;
        }

        try
        {
            var addon = gameGui.GetAddonByName(name);
            if (addon.IsNull || !addon.IsVisible)
            {
                return false;
            }

            var size = addon.ScaledSize;
            if (size.X <= 0f || size.Y <= 0f)
            {
                return false;
            }

            rect = ScreenRect.FromSize(addon.Position, size);
            return true;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, $"{name} window position unavailable; its Tsukimichi panel is not shown");
            return false;
        }
    }

    /// <summary>A text node's text as the player reads it (macros and icons dropped); empty for a null node.</summary>
    protected static string NodeText(AtkTextNode* node)
    {
        if (node == null)
        {
            return string.Empty;
        }

        return new ReadOnlySeStringSpan(node->NodeText.AsSpan()).ExtractText();
    }

    /// <summary>
    /// Adds every non-empty string value of the window (its AtkValues, the data the game handed it) to
    /// <paramref name="texts"/>, and every integer value to <paramref name="numbers"/> when given; at most
    /// <paramref name="max"/> values are looked at.
    /// </summary>
    protected static void CollectValues(AtkUnitBase* addon, List<string> texts, List<uint>? numbers, int max = 64)
    {
        if (addon == null || addon->AtkValues == null)
        {
            return;
        }

        var count = Math.Min((int)addon->AtkValuesCount, max);
        for (var i = 0; i < count; i++)
        {
            var value = addon->AtkValues[i];
            switch (value.Type)
            {
                case AtkValueType.String:
                case AtkValueType.ManagedString:
                case AtkValueType.ConstString:
                    if (value.String.HasValue)
                    {
                        var text = new ReadOnlySeStringSpan(value.String.AsSpan()).ExtractText();
                        if (text.Length > 0)
                        {
                            texts.Add(text);
                        }
                    }

                    break;

                case AtkValueType.Int:
                case AtkValueType.UInt:
                    numbers?.Add(value.UInt);
                    break;
            }
        }
    }

    protected void WarnOnce(Exception ex, string message)
    {
        if (warned)
        {
            Log.Debug(ex, message);
            return;
        }

        warned = true;
        Log.Warning(ex, message);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Unregisters the listeners and leaves the gate's event; a subclass adds its own clean-up.</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposed || !disposing)
        {
            return;
        }

        disposed = true;
        gate.Changed -= Apply;
        Apply();
    }

    /// <summary>Registers or unregisters the listeners to match the setting and the gate. Framework thread.</summary>
    private void Apply()
    {
        var want = enabled && !disposed && gate.HooksAllowed;
        if (want == registered)
        {
            return;
        }

        try
        {
            if (want)
            {
                lifecycle.RegisterListener(AddonEvent.PostSetup, AddonName, onSetup);
                lifecycle.RegisterListener(AddonEvent.PostRefresh, AddonName, onChange);
                lifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, AddonName, onChange);
                lifecycle.RegisterListener(AddonEvent.PostReceiveEvent, AddonName, onEvent);
                lifecycle.RegisterListener(AddonEvent.PostUpdate, AddonName, onUpdate);
                lifecycle.RegisterListener(AddonEvent.PreFinalize, AddonName, onFinalize);
            }
            else
            {
                lifecycle.UnregisterListener(AddonEvent.PostSetup, AddonName, onSetup);
                lifecycle.UnregisterListener(AddonEvent.PostRefresh, AddonName, onChange);
                lifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, AddonName, onChange);
                lifecycle.UnregisterListener(AddonEvent.PostReceiveEvent, AddonName, onEvent);
                lifecycle.UnregisterListener(AddonEvent.PostUpdate, AddonName, onUpdate);
                lifecycle.UnregisterListener(AddonEvent.PreFinalize, AddonName, onFinalize);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "{Addon} listeners could not be {Action}", AddonName, want ? "registered" : "unregistered");
        }

        registered = want;
        if (!want)
        {
            IsOpen = false;
            Forget();
        }
    }

    private void OnSetup(AddonEvent type, AddonArgs args)
    {
        // A listener whose unregister threw keeps firing after Apply() recorded the panel as off: read nothing then.
        if (!registered)
        {
            return;
        }

        IsOpen = true;
        ReadFrom(args);
    }

    private void OnChange(AddonEvent type, AddonArgs args)
    {
        if (!registered)
        {
            return;
        }

        IsOpen = true;
        ReadFrom(args);
    }

    private void OnEvent(AddonEvent type, AddonArgs args)
    {
        if (!registered || !ReadOnEvents)
        {
            return;
        }

        IsOpen = true;
        ReadFrom(args);
    }

    private void OnUpdate(AddonEvent type, AddonArgs args)
    {
        if (!registered)
        {
            return;
        }

        // Registered while the window was already up (the setting ticked, the gate opened): PostUpdate is the first
        // event this side sees, so it reads once.
        if (!IsOpen)
        {
            IsOpen = true;
            ReadFrom(args);
            return;
        }

        if (PollInterval <= 0 || !WantsPoll)
        {
            return;
        }

        var now = Environment.TickCount64;
        if (now - lastPoll >= PollInterval)
        {
            ReadFrom(args);
        }
    }

    private void OnFinalize(AddonEvent type, AddonArgs args)
    {
        IsOpen = false;
        Forget();
    }

    private void ReadFrom(AddonArgs args)
    {
        lastPoll = Environment.TickCount64;
        try
        {
            var address = args.Addon.Address;
            if (address == nint.Zero)
            {
                return;
            }

            Read((AtkUnitBase*)address);
        }
        catch (Exception ex)
        {
            WarnOnce(ex, $"{AddonName} window could not be read; its Tsukimichi panel is not shown");
            Forget();
        }
    }
}
