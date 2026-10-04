using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Ipc;
using Tsukimichi.Core.Ipc;

namespace Tsukimichi.Game;

/// <summary>
/// The 1.22.0 summary gates (plan v8 M2; docs/ipc.md "The summary"): read-only answers about tonight for the logged-in
/// character, for Tsukimichi for Umbra and any other plugin, versioned by <see cref="IpcChannels.SummaryVersion"/>, and
/// <c>OpenAt</c>, which opens Tsukimichi's own windows. No gate starts travel, a route or a run: anything that does still
/// needs the player's click inside Tsukimichi. Every answer comes from one immutable <see cref="TsukimichiSummary"/>, so a
/// caller on any thread reads a consistent capture; names in it are already shielded.
/// </summary>
public sealed partial class IpcProvider
{
    private ICallGateProvider<int>? getSummaryVersion;
    private ICallGateProvider<(string, string, int)>? getCharacter;
    private ICallGateProvider<(uint, string, string)>? getUpNext;
    private ICallGateProvider<(int, int, string)>? getReadyCount;
    private ICallGateProvider<(int, int)>? getJournalRoom;
    private ICallGateProvider<(string, int)[]>? getEndingSoon;
    private ICallGateProvider<(string, int, bool)>? getStoryMeter;
    private ICallGateProvider<string>? getTheme;
    private ICallGateProvider<string, bool>? openAt;
    private ICallGateProvider<string, string, int>? addonHello;
    private ICallGateProvider<object>? summaryChanged;

    /// <summary>The summary the gates answer from (<c>Ui.SummarySource.Current</c>); any thread. Null answers as not ready.</summary>
    public Func<TsukimichiSummary>? Summary { get; set; }

    /// <summary>The theme in use, by key ("medallion"); framework thread.</summary>
    public Func<string>? ThemeKey { get; set; }

    /// <summary>Opens Tsukimichi at a place (<see cref="IpcPlaces"/>); called on the framework thread. Never travels.</summary>
    public Action<string>? OpenPlace { get; set; }

    /// <summary>An add-on said hello (its name and version); called on the framework thread.</summary>
    public Action<string, string>? Hello { get; set; }

    /// <summary>Framework thread: tells subscribers the summary changed (<see cref="IpcChannels.SummaryChangedGate"/>).</summary>
    public void AnnounceSummary()
    {
        if (disposed || summaryChanged is null)
        {
            return;
        }

        try
        {
            summaryChanged.SendMessage();
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Tsukimichi.SummaryChanged subscriber failed");
        }
    }

    private void RegisterSummary()
    {
        getSummaryVersion = pluginInterface.GetIpcProvider<int>(IpcChannels.GetSummaryVersionGate);
        getCharacter = pluginInterface.GetIpcProvider<(string, string, int)>(IpcChannels.GetCharacterGate);
        getUpNext = pluginInterface.GetIpcProvider<(uint, string, string)>(IpcChannels.GetUpNextGate);
        getReadyCount = pluginInterface.GetIpcProvider<(int, int, string)>(IpcChannels.GetReadyCountGate);
        getJournalRoom = pluginInterface.GetIpcProvider<(int, int)>(IpcChannels.GetJournalRoomGate);
        getEndingSoon = pluginInterface.GetIpcProvider<(string, int)[]>(IpcChannels.GetEndingSoonGate);
        getStoryMeter = pluginInterface.GetIpcProvider<(string, int, bool)>(IpcChannels.GetStoryMeterGate);
        getTheme = pluginInterface.GetIpcProvider<string>(IpcChannels.GetThemeGate);
        openAt = pluginInterface.GetIpcProvider<string, bool>(IpcChannels.OpenAtGate);
        addonHello = pluginInterface.GetIpcProvider<string, string, int>(IpcChannels.AddonHelloGate);
        summaryChanged = pluginInterface.GetIpcProvider<object>(IpcChannels.SummaryChangedGate);

        getSummaryVersion.RegisterFunc(static () => IpcChannels.SummaryVersion);
        getCharacter.RegisterFunc(() => FromSummary(IpcChannels.GetCharacterGate, (string.Empty, string.Empty, 0), static s => s.CharacterAnswer()));
        getUpNext.RegisterFunc(() => FromSummary(IpcChannels.GetUpNextGate, (0u, string.Empty, string.Empty), static s => s.UpNextAnswer()));
        getReadyCount.RegisterFunc(() => FromSummary(IpcChannels.GetReadyCountGate, (0, 0, string.Empty), static s => s.ReadyAnswer()));
        getJournalRoom.RegisterFunc(() => FromSummary(IpcChannels.GetJournalRoomGate, (-1, 0), static s => s.JournalAnswer()));
        getEndingSoon.RegisterFunc(() => FromSummary(IpcChannels.GetEndingSoonGate, Array.Empty<(string, int)>(), static s => s.EndingSoonAnswer()));
        getStoryMeter.RegisterFunc(() => FromSummary(IpcChannels.GetStoryMeterGate, (string.Empty, 0, false), static s => s.StoryAnswer()));
        getTheme.RegisterFunc(GetTheme);
        openAt.RegisterFunc(OpenAt);
        addonHello.RegisterFunc(AddonHello);
    }

    private void UnregisterSummary()
    {
        Unregister(getSummaryVersion);
        Unregister(getCharacter);
        Unregister(getUpNext);
        Unregister(getReadyCount);
        Unregister(getJournalRoom);
        Unregister(getEndingSoon);
        Unregister(getStoryMeter);
        Unregister(getTheme);
        Unregister(openAt);
        Unregister(addonHello);
    }

    /// <summary>For the <c>/tsuki ipc</c> window: the summary gates' providers.</summary>
    private ICallGateProvider? SummaryProvider(string gate) => gate switch
    {
        IpcChannels.GetSummaryVersionGate => getSummaryVersion,
        IpcChannels.GetCharacterGate => getCharacter,
        IpcChannels.GetUpNextGate => getUpNext,
        IpcChannels.GetReadyCountGate => getReadyCount,
        IpcChannels.GetJournalRoomGate => getJournalRoom,
        IpcChannels.GetEndingSoonGate => getEndingSoon,
        IpcChannels.GetStoryMeterGate => getStoryMeter,
        IpcChannels.GetThemeGate => getTheme,
        IpcChannels.OpenAtGate => openAt,
        IpcChannels.AddonHelloGate => addonHello,
        IpcChannels.SummaryChangedGate => summaryChanged,
        _ => null,
    };

    private static bool IsSummaryGate(string gate) => gate is IpcChannels.GetSummaryVersionGate or IpcChannels.GetCharacterGate
        or IpcChannels.GetUpNextGate or IpcChannels.GetReadyCountGate or IpcChannels.GetJournalRoomGate or IpcChannels.GetEndingSoonGate
        or IpcChannels.GetStoryMeterGate or IpcChannels.GetThemeGate or IpcChannels.OpenAtGate;

    /// <summary>For the <c>/tsuki ipc</c> window: a summary gate called through Dalamud's subscriber, as another plugin would.</summary>
    private object? SummaryTestCall(string gate, string arguments) => gate switch
    {
        IpcChannels.GetSummaryVersionGate => pluginInterface.GetIpcSubscriber<int>(gate).InvokeFunc(),
        IpcChannels.GetCharacterGate => pluginInterface.GetIpcSubscriber<(string, string, int)>(gate).InvokeFunc(),
        IpcChannels.GetUpNextGate => pluginInterface.GetIpcSubscriber<(uint, string, string)>(gate).InvokeFunc(),
        IpcChannels.GetReadyCountGate => pluginInterface.GetIpcSubscriber<(int, int, string)>(gate).InvokeFunc(),
        IpcChannels.GetJournalRoomGate => pluginInterface.GetIpcSubscriber<(int, int)>(gate).InvokeFunc(),
        IpcChannels.GetEndingSoonGate => pluginInterface.GetIpcSubscriber<(string, int)[]>(gate).InvokeFunc(),
        IpcChannels.GetStoryMeterGate => pluginInterface.GetIpcSubscriber<(string, int, bool)>(gate).InvokeFunc(),
        IpcChannels.GetThemeGate => pluginInterface.GetIpcSubscriber<string>(gate).InvokeFunc(),
        IpcChannels.OpenAtGate => pluginInterface.GetIpcSubscriber<string, bool>(gate).InvokeFunc(arguments.Trim()),
        _ => throw new FormatException("A message cannot be called; subscribe to it instead."),
    };

    /// <summary>An answer from the current summary; <paramref name="fallback"/> before one, or on any failure.</summary>
    private T FromSummary<T>(string gate, T fallback, Func<TsukimichiSummary, T> answer)
    {
        if (disposed)
        {
            return fallback;
        }

        try
        {
            consumed = true;
            return Summary?.Invoke() is { Ready: true } current ? answer(current) : fallback;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, gate + " failed");
            return fallback;
        }
    }

    private string GetTheme()
    {
        if (disposed)
        {
            return string.Empty;
        }

        try
        {
            return ThemeKey?.Invoke() ?? string.Empty;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, IpcChannels.GetThemeGate + " failed");
            return string.Empty;
        }
    }

    /// <summary>
    /// OpenAt: a known place opens on the framework thread (at once when the caller is there); true when the place is one
    /// this build knows. Only Tsukimichi's own windows open: nothing travels, routes or runs.
    /// </summary>
    private bool OpenAt(string place)
    {
        if (disposed || IpcPlaces.Parse(place) is not { } known || OpenPlace is not { } open)
        {
            return false;
        }

        try
        {
            if (framework.IsInFrameworkUpdateThread)
            {
                open(known);
                return true;
            }

            framework.RunOnFrameworkThread(() =>
            {
                if (!disposed)
                {
                    open(known);
                }
            }).ContinueWith(t => WarnOnce(t.Exception?.GetBaseException(), "Tsukimichi.OpenAt failed"), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            return true;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Tsukimichi.OpenAt failed");
            return false;
        }
    }

    /// <summary>AddonHello: remembers the add-on (Settings › About shows its version) and answers the summary's version.</summary>
    private int AddonHello(string addon, string version)
    {
        if (disposed)
        {
            return IpcChannels.SummaryVersion;
        }

        try
        {
            var name = addon ?? string.Empty;
            var shown = version ?? string.Empty;
            if (framework.IsInFrameworkUpdateThread)
            {
                Hello?.Invoke(name, shown);
            }
            else
            {
                _ = framework.RunOnFrameworkThread(() =>
                {
                    if (!disposed)
                    {
                        Hello?.Invoke(name, shown);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            WarnOnce(ex, IpcChannels.AddonHelloGate + " failed");
        }

        return IpcChannels.SummaryVersion;
    }
}
