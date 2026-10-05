namespace Tsukimichi.Core.Moonfall;

/// <summary>Why Moonfall is paused (plan v9 G9): the player's own pause, or what the game is doing around it.</summary>
[Flags]
public enum MoonfallPauseReason
{
    None = 0,

    /// <summary>The player pressed Pause.</summary>
    Player = 1,

    Combat = 2,
    Duty = 4,
    Cutscene = 8,

    /// <summary>The window lost focus (the player clicked into the game or another window).</summary>
    Unfocused = 16,

    /// <summary>The window was closed and opened again.</summary>
    Reopened = 32,
}

/// <summary>
/// Moonfall's pause (plan v9 G9): it pauses itself in combat, in duties, in cutscenes and when its window loses focus,
/// and the player can pause it. An automatic pause never lifts behind the player's back: once its cause has ended the
/// board waits for a click ("click to resume"), and that click resumes without shooting. While a cause still holds,
/// Resume does nothing.
/// </summary>
public sealed class MoonfallPauseState
{
    /// <summary>What holds the pause now: the player's pause and the causes still in force.</summary>
    public MoonfallPauseReason Active { get; private set; }

    /// <summary>An automatic pause's cause has ended and the board waits for a click.</summary>
    public bool AwaitingResume { get; private set; }

    public bool Paused => Active != MoonfallPauseReason.None || AwaitingResume;

    /// <summary>Whether a cause other than the player's own pause holds it.</summary>
    public bool Held => (Active & ~MoonfallPauseReason.Player) != MoonfallPauseReason.None;

    /// <summary>The automatic causes this frame (combat, duty, cutscene, focus).</summary>
    public void Update(MoonfallPauseReason causes)
    {
        causes &= ~MoonfallPauseReason.Player;
        Active = (Active & MoonfallPauseReason.Player) | causes;
        if (causes != MoonfallPauseReason.None)
        {
            AwaitingResume = true;
        }
    }

    /// <summary>Pauses for <paramref name="reason"/> now (the player's Pause, or the window reopening).</summary>
    public void Pause(MoonfallPauseReason reason)
    {
        if (reason == MoonfallPauseReason.Player)
        {
            Active |= MoonfallPauseReason.Player;
        }
        else if (reason != MoonfallPauseReason.None)
        {
            AwaitingResume = true;
        }
    }

    /// <summary>Resumes, unless a cause still holds; true when the board runs again.</summary>
    public bool TryResume()
    {
        if (Held)
        {
            return false;
        }

        Active = MoonfallPauseReason.None;
        AwaitingResume = false;
        return true;
    }

    /// <summary>The cause to name to the player: the first in force of combat, duty, cutscene, focus, then the player's own.</summary>
    public MoonfallPauseReason Shown =>
        (Active & MoonfallPauseReason.Combat) != 0 ? MoonfallPauseReason.Combat
        : (Active & MoonfallPauseReason.Duty) != 0 ? MoonfallPauseReason.Duty
        : (Active & MoonfallPauseReason.Cutscene) != 0 ? MoonfallPauseReason.Cutscene
        : (Active & MoonfallPauseReason.Unfocused) != 0 ? MoonfallPauseReason.Unfocused
        : (Active & MoonfallPauseReason.Player) != 0 ? MoonfallPauseReason.Player
        : AwaitingResume ? MoonfallPauseReason.Reopened
        : MoonfallPauseReason.None;
}
