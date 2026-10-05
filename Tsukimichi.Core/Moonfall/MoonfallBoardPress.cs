namespace Tsukimichi.Core.Moonfall;

/// <summary>
/// The board's mouse rule (the window's click on the board): a click shoots only when its press began while a ball
/// waited to be aimed and the board was running. A press held through a flight raises the flippers; if the ball drains
/// and the next one arrives while it is still held, letting go is not a shot. The window notes each press as it starts
/// (<see cref="Pressed"/>) and asks on the release (<see cref="Released"/>).
/// </summary>
public struct MoonfallBoardPress
{
    private bool armed;

    /// <summary>The press started, in <paramref name="phase"/>, with the board <paramref name="paused"/> or not.</summary>
    public void Pressed(MoonfallPhase phase, bool paused) => armed = phase == MoonfallPhase.Aiming && !paused;

    /// <summary>
    /// The press was let go over the board: whether it shoots. Only a press that began while aiming, let go while still
    /// aiming and running, shoots; either way the press is spent.
    /// </summary>
    public bool Released(MoonfallPhase phase, bool paused)
    {
        var shoot = armed && phase == MoonfallPhase.Aiming && !paused;
        armed = false;
        return shoot;
    }
}
