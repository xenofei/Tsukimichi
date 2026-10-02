using Tsukimichi.Core.Ipc;

namespace Tsukimichi.Tests.Ipc;

/// <summary>
/// Missing IPC gates are tracked one by one (<see cref="IpcGateHealth"/>): an optional gate turns only its feature off,
/// a core gate the whole plugin, and a plugin list change forgets both.
/// </summary>
public class IpcGateHealthTests
{
    private const string Teleport = "Lifestream.Teleport";
    private const string Active = "Lifestream.GetActiveAetheryte";
    private const string Busy = "Lifestream.IsBusy";

    [Fact]
    public void A_missing_optional_gate_turns_off_only_itself()
    {
        var gates = new IpcGateHealth(Teleport);
        gates.MarkMissing(Active);

        Assert.False(gates.CoreMissing);
        Assert.True(gates.IsMissing(Active));
        Assert.False(gates.IsMissing(Busy));
        Assert.False(gates.IsMissing(Teleport));
    }

    [Fact]
    public void A_missing_core_gate_turns_off_everything()
    {
        var gates = new IpcGateHealth(Teleport);
        gates.MarkMissing(Teleport);

        Assert.True(gates.CoreMissing);
        Assert.True(gates.IsMissing(Busy));
    }

    [Fact]
    public void Any_of_several_core_gates_counts()
    {
        var gates = new IpcGateHealth("vnavmesh.SimpleMove.PathfindAndMoveCloseTo", "vnavmesh.Nav.IsReady");
        gates.MarkMissing("vnavmesh.Path.IsRunning");
        Assert.False(gates.CoreMissing);

        gates.MarkMissing("vnavmesh.Nav.IsReady");
        Assert.True(gates.CoreMissing);
    }

    [Fact]
    public void Reset_forgets_every_missing_gate()
    {
        var gates = new IpcGateHealth(Teleport);
        gates.MarkMissing(Teleport);
        gates.MarkMissing(Active);

        gates.Reset();

        Assert.False(gates.CoreMissing);
        Assert.False(gates.IsMissing(Active));
    }
}
