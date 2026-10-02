namespace Tsukimichi.Core.Ipc;

/// <summary>
/// Which of a companion plugin's IPC gates turned out not to be registered (Dalamud's <c>IpcNotReadyError</c>), kept
/// per gate so one missing optional gate does not grey out the whole plugin. A build of Lifestream without
/// <c>GetActiveAetheryte</c> still teleports; only a missing core gate (Lifestream's <c>Teleport</c>, vnavmesh's
/// <c>PathfindAndMoveCloseTo</c> or <c>Nav.IsReady</c>) makes the plugin unavailable. Forgotten on
/// <see cref="Reset"/>, which the wrappers call when Dalamud's plugin list changes (a new build may register them).
/// </summary>
public sealed class IpcGateHealth
{
    private readonly HashSet<string> core;
    private readonly HashSet<string> missing = new(StringComparer.Ordinal);

    public IpcGateHealth(params string[] coreGates)
    {
        ArgumentNullException.ThrowIfNull(coreGates);
        core = new HashSet<string>(coreGates, StringComparer.Ordinal);
    }

    /// <summary>A core gate is missing: the plugin cannot do its main job and reads as unavailable.</summary>
    public bool CoreMissing { get; private set; }

    /// <summary>True when the gate (or a core gate) was found missing since the last <see cref="Reset"/>; the feature it serves is not offered.</summary>
    public bool IsMissing(string gate) => CoreMissing || missing.Contains(gate);

    /// <summary>Records that the gate is not registered.</summary>
    public void MarkMissing(string gate)
    {
        ArgumentNullException.ThrowIfNull(gate);
        missing.Add(gate);
        if (core.Contains(gate))
        {
            CoreMissing = true;
        }
    }

    /// <summary>Forgets every missing gate.</summary>
    public void Reset()
    {
        missing.Clear();
        CoreMissing = false;
    }
}
