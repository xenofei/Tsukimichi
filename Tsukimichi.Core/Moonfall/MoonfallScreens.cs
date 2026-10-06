namespace Tsukimichi.Core.Moonfall;

/// <summary>Moonfall's screens (plan v9 G7, spec-rich2.md §4): the menus, and the board in play.</summary>
public enum MoonfallScreen : byte
{
    /// <summary>MOONFALL, the modes and the Continue card (the default focus).</summary>
    Title,

    /// <summary>Adventure's map: The Moon Road and The Far Shore, their stops and the legend.</summary>
    Map,

    /// <summary>A stage's five levels and the Play strip.</summary>
    Levels,

    /// <summary>The eleven companions and the detail panel.</summary>
    Characters,

    /// <summary>Quick Play's level and companion picker.</summary>
    QuickPlay,

    /// <summary>The challenges list (sealed until The Moon Road is won).</summary>
    Challenges,

    /// <summary>A duel's setup: the opponent, the difficulty, the level and the player's companion.</summary>
    Duel,

    /// <summary>All of Moonfall's settings in one panel.</summary>
    Options,

    /// <summary>The board (with the pause menu over it while paused, and the tally once the level ends).</summary>
    Play,
}

/// <summary>How the board in play was started, so its tally knows the way on and where Leave goes.</summary>
public enum MoonfallPlayKind : byte
{
    Adventure,
    QuickPlay,
    Challenge,
    Duel,
}

/// <summary>What Back (Esc, the gamepad's back button, a Back or Map button) did.</summary>
public enum MoonfallBack : byte
{
    /// <summary>Nothing (no screen to go back to).</summary>
    None,

    /// <summary>The previous screen is back.</summary>
    Screen,

    /// <summary>In play: the board paused (the pause menu opens).</summary>
    Pause,

    /// <summary>In play and paused: the board runs again.</summary>
    Resume,

    /// <summary>The level is over: the board was left for the screen its mode returns to.</summary>
    Leave,

    /// <summary>On the title: the window closes.</summary>
    Close,
}

/// <summary>
/// Moonfall's screen state machine (plan v9 G7): which screen shows, what Back does from it, and which entries are
/// locked. Pure: no ImGui, no clock. The window asks it every frame and draws the screen it names; <see cref="Version"/>
/// moves on each change, so the window can reset its focus and its cached views once per screen.
/// <para>
/// Screens stack: opening one remembers the one it was opened from, and Back returns there. The board is entered with
/// <see cref="Play"/> (the screen it was started from is remembered, so Options from the pause menu comes back to the
/// paused board) and left with <see cref="Leave"/>, which goes to its mode's screen: Adventure to the map, Quick Play to
/// its picker, a challenge to the list, a duel to its setup, always over the title. In play, Back pauses the board,
/// then resumes it; over the tally it leaves. On the title Back closes the window. Challenges stay locked until
/// The Moon Road is won (<see cref="MoonfallChallenges.Open"/>).
/// </para>
/// </summary>
public sealed class MoonfallScreenFlow
{
    /// <summary>The deepest stack the screens can build (title, map, levels, play, options and a little room).</summary>
    public const int MaxDepth = 8;

    private readonly MoonfallScreen[] below = new MoonfallScreen[MaxDepth];
    private int depth;

    /// <summary>The screen showing.</summary>
    public MoonfallScreen Current { get; private set; } = MoonfallScreen.Title;

    /// <summary>Moves on with every change of screen (the window resets its focus and views once per screen).</summary>
    public int Version { get; private set; }

    /// <summary>How the board was last started (meaningful while <see cref="InPlay"/>).</summary>
    public MoonfallPlayKind Playing { get; private set; }

    /// <summary>Whether a board is under way: showing, or under Options opened from its pause menu.</summary>
    public bool InPlay => Current == MoonfallScreen.Play || Contains(MoonfallScreen.Play);

    /// <summary>How many screens are under the current one.</summary>
    public int Depth => depth;

    /// <summary>The screen Back would return to, or null on the title (Back closes the window there).</summary>
    public MoonfallScreen? Previous => depth > 0 ? below[depth - 1] : null;

    /// <summary>Whether <paramref name="screen"/> can be opened from a menu: challenges only once they are open; the board only through <see cref="Play"/>.</summary>
    public static bool CanOpen(MoonfallScreen screen, bool challengesOpen) =>
        screen switch
        {
            MoonfallScreen.Play => false,
            MoonfallScreen.Challenges => challengesOpen,
            _ => true,
        };

    /// <summary>
    /// Opens <paramref name="screen"/> over the current one; false (nothing changes) when it is locked, is the board, is
    /// already showing, or the stack is full.
    /// </summary>
    public bool Open(MoonfallScreen screen, bool challengesOpen)
    {
        if (!CanOpen(screen, challengesOpen) || screen == Current || depth >= MaxDepth)
        {
            return false;
        }

        // A screen already under this one is returned to rather than stacked twice (the map's Back and the title's
        // Adventure both lead to the map, never to a loop).
        for (var i = depth - 1; i >= 0; i--)
        {
            if (below[i] == screen)
            {
                depth = i;
                Set(screen);
                return true;
            }
        }

        below[depth++] = Current;
        Set(screen);
        return true;
    }

    /// <summary>
    /// The board starts (or starts again: Replay, Next, the next level of a challenge) for <paramref name="kind"/>. From
    /// a menu, that menu is remembered under it; from the board itself nothing is stacked.
    /// </summary>
    public void Play(MoonfallPlayKind kind)
    {
        Playing = kind;
        if (Current == MoonfallScreen.Play)
        {
            Version++;
            return;
        }

        // Options over a paused board: the board is under it already.
        var at = IndexOf(MoonfallScreen.Play);
        if (at >= 0)
        {
            depth = at;
            Set(MoonfallScreen.Play);
            return;
        }

        if (depth < MaxDepth)
        {
            below[depth++] = Current;
        }

        Set(MoonfallScreen.Play);
    }

    /// <summary>The screen the board returns to when left: its mode's own.</summary>
    public static MoonfallScreen HomeOf(MoonfallPlayKind kind) => kind switch
    {
        MoonfallPlayKind.QuickPlay => MoonfallScreen.QuickPlay,
        MoonfallPlayKind.Challenge => MoonfallScreen.Challenges,
        MoonfallPlayKind.Duel => MoonfallScreen.Duel,
        _ => MoonfallScreen.Map,
    };

    /// <summary>
    /// The board is left (Leave to the map held, the tally's Map, Back over the tally): its mode's screen shows over the
    /// title, whatever was stacked under the board.
    /// </summary>
    public void Leave()
    {
        depth = 0;
        below[depth++] = MoonfallScreen.Title;
        Set(HomeOf(Playing));
    }

    /// <summary>Back to the title from anywhere, nothing stacked (the window reopened on a finished board, say).</summary>
    public void Home()
    {
        depth = 0;
        Set(MoonfallScreen.Title);
    }

    /// <summary>
    /// Back, as Esc and the gamepad's back button ask it: on the title the window closes; in play the board pauses,
    /// then resumes (<paramref name="paused"/>), and over the tally (<paramref name="levelOver"/>) it is left; on any
    /// other screen the previous one returns. The caller pauses or resumes the board as told.
    /// </summary>
    public MoonfallBack Back(bool paused, bool levelOver)
    {
        switch (Current)
        {
            case MoonfallScreen.Title:
                return MoonfallBack.Close;

            case MoonfallScreen.Play when levelOver:
                Leave();
                return MoonfallBack.Leave;

            case MoonfallScreen.Play:
                return paused ? MoonfallBack.Resume : MoonfallBack.Pause;
        }

        if (depth == 0)
        {
            Set(MoonfallScreen.Title);
            return MoonfallBack.Screen;
        }

        Set(below[--depth]);
        return MoonfallBack.Screen;
    }

    private void Set(MoonfallScreen screen)
    {
        Current = screen;
        Version++;
    }

    private bool Contains(MoonfallScreen screen) => IndexOf(screen) >= 0;

    private int IndexOf(MoonfallScreen screen)
    {
        for (var i = 0; i < depth; i++)
        {
            if (below[i] == screen)
            {
                return i;
            }
        }

        return -1;
    }
}

/// <summary>
/// Hold-to-confirm (the owner's rule for destructive clicks: Restart and Leave, spec-rich2.md §4 "Pause"): the button
/// must be held for <see cref="Seconds"/>; letting go early starts it over, and once it fires it waits for a release
/// before it can fire again. The fill sweeps the pill as it is held (<see cref="Progress"/>). Pure; no allocation.
/// </summary>
public struct MoonfallHold
{
    /// <summary>How long the hold takes.</summary>
    public const double Seconds = 0.9;

    private double held;
    private bool spent;

    /// <summary>How far the hold has come, 0 to 1.</summary>
    public readonly float Progress => (float)Math.Clamp(held / Seconds, 0, 1);

    /// <summary>Whether it is being held (and has not yet fired).</summary>
    public readonly bool Holding => held > 0 && !spent;

    /// <summary>
    /// One frame of <paramref name="seconds"/> with the button <paramref name="down"/> or not; true on the one frame the
    /// hold completes.
    /// </summary>
    public bool Update(bool down, double seconds)
    {
        if (!down)
        {
            held = 0;
            spent = false;
            return false;
        }

        if (spent)
        {
            return false;
        }

        held += Math.Max(0, seconds);
        if (held + 1e-9 < Seconds)
        {
            return false;
        }

        held = Seconds;
        spent = true;
        return true;
    }

    /// <summary>Starts over (the screen changed under it).</summary>
    public void Reset()
    {
        held = 0;
        spent = false;
    }
}

/// <summary>
/// Whether the window drew on the last frame (Dalamud skips <c>Draw</c> while the window is collapsed, and calls
/// <c>PreOpenCheck</c> on every frame regardless): a frame with no draw since the one before means the game, its sound
/// and its clock must hold, as when the board pauses. Pure; no allocation.
/// </summary>
public struct MoonfallDrawWatch
{
    private int drawn;
    private bool any;

    /// <summary>The window drew on <paramref name="frame"/>.</summary>
    public void Drew(int frame)
    {
        drawn = frame;
        any = true;
    }

    /// <summary>Whether the window has not drawn since the frame before <paramref name="frame"/> (collapsed, closed, or never drawn).</summary>
    public readonly bool Missed(int frame) => !any || frame - drawn > 1;
}

/// <summary>The board's shooter: a level's game, or a duel (which keeps the sides). The window shoots through this alone.</summary>
public interface IMoonfallShooter
{
    /// <summary>Shoots at <paramref name="angleDegrees"/>; false when no shot may be taken now.</summary>
    bool Shoot(double angleDegrees);
}
