using Tsukimichi.Core.Ui;

namespace Tsukimichi.Core.Portraits;

/// <summary>What the first-run portrait pack offer does this frame (<see cref="PortraitPackWelcome.Next"/>).</summary>
public enum PortraitPackWelcomeStep : byte
{
    /// <summary>Nothing to do: the offer was answered, or this build offers no pack.</summary>
    Nothing,

    /// <summary>Owed, but not yet: the pack is still being read, What's new or another first-run card goes first, or it is not a quiet moment.</summary>
    Wait,

    /// <summary>Show the offer now.</summary>
    Show,

    /// <summary>
    /// Never show it, and record it as answered: the player already has the pack (installed, an update offered or a
    /// damaged copy) or started its download in Settings.
    /// </summary>
    Retire,
}

/// <summary>How the player answered the offer (<see cref="PortraitPackWelcome.AnswerOf"/>).</summary>
public enum PortraitPackWelcomeAnswer : byte
{
    /// <summary>Not answered this frame.</summary>
    None,

    /// <summary>"Download portraits", or Enter: the existing download starts.</summary>
    Download,

    /// <summary>"Not now", Esc, or the window closed: nothing downloads.</summary>
    NotNow,
}

/// <summary>Where the offer stands, as far as <see cref="PortraitPackWelcome.Next"/> cares.</summary>
/// <param name="Answered">The player answered the offer before (persisted; the offer is once only).</param>
/// <param name="Loaded">The installed pack has been read at start; until then "no pack" may only mean "not read yet".</param>
/// <param name="Pack">Where the pack stands (<see cref="PortraitPackStatus.Of"/>).</param>
/// <param name="Busy">A download, install or removal is running (one the player started in Settings).</param>
/// <param name="OtherFirst">
/// Something else goes first: the What's new popup is due or open, the tour or its offer is on screen, or Settings'
/// own download confirmation is open. The offer never shows alongside them; it follows them.
/// </param>
public readonly record struct PortraitPackWelcomeState(bool Answered, bool Loaded, PortraitPackState Pack, bool Busy, bool OtherFirst);

/// <summary>
/// The first-run portrait pack offer (1.22.0, the owner's request: "after a user installs the app, give them a choice to
/// install the portrait pack, yes by default"). Pure, so it is tested; the plugin persists the answer and draws the window.
/// <list type="bullet">
/// <item><b>Once.</b> One persisted flag covers both readings of "after a user installs": a fresh install (no prior
/// configuration, which <see cref="WhatsNew.Decide(string?, string?, bool, bool)"/> records silently, so nothing else
/// is due) and a player who updated without ever having seen it. Once answered, it never shows again.</item>
/// <item><b>Only to someone without the pack.</b> Installed, an update offered or a damaged copy retires it; so does a
/// download the player started in Settings. A build that offers no pack never shows it (and records nothing, so a later
/// build that offers one still may).</item>
/// <item><b>At the first quiet moment</b> (<see cref="WhatsNew.IsQuietMoment"/>), and after What's new: when both are
/// due, What's new shows first and the offer at the next quiet moment after it closes.</item>
/// <item><b>Yes by default.</b> "Download portraits" has the keyboard focus and Enter accepts; "Not now", Esc and
/// closing the window decline. Enter counts only on the offer's focused window, on a fresh press, and not before
/// <see cref="KeySettleSeconds"/>: the offer opens unasked, so an Enter meant for the game's chat never downloads.</item>
/// </list>
/// The privacy promise holds: nothing goes online unless the player chooses Download.
/// </summary>
public static class PortraitPackWelcome
{
    /// <summary>How long the offer is on screen before Enter can accept it.</summary>
    public const double KeySettleSeconds = 0.6;

    /// <summary>What the offer does this frame, given where it stands and what the player is doing.</summary>
    public static PortraitPackWelcomeStep Next(in PortraitPackWelcomeState state, in WhatsNewMoment moment)
    {
        if (state.Answered)
        {
            return PortraitPackWelcomeStep.Nothing;
        }

        if (!state.Loaded)
        {
            return PortraitPackWelcomeStep.Wait;
        }

        if (state.Busy)
        {
            // The player is already downloading it from Settings: that is their answer.
            return PortraitPackWelcomeStep.Retire;
        }

        switch (state.Pack)
        {
            case PortraitPackState.NotOffered:
                return PortraitPackWelcomeStep.Nothing;
            case PortraitPackState.Installed or PortraitPackState.UpdateAvailable or PortraitPackState.Damaged:
                return PortraitPackWelcomeStep.Retire;
        }

        return state.OtherFirst || !WhatsNew.IsQuietMoment(moment) ? PortraitPackWelcomeStep.Wait : PortraitPackWelcomeStep.Show;
    }

    /// <summary>
    /// The player's answer this frame. A click answers at once; a key only on the offer's focused window: Esc declines,
    /// Enter accepts once the offer has been up <see cref="KeySettleSeconds"/>. Esc wins over Enter in the same frame.
    /// </summary>
    /// <param name="downloadClicked">"Download portraits" was clicked (or activated by the keyboard).</param>
    /// <param name="notNowClicked">"Not now" was clicked.</param>
    /// <param name="enter">Enter was pressed this frame (a fresh press, not a repeat).</param>
    /// <param name="escape">Esc was pressed this frame.</param>
    /// <param name="focused">The offer's window has the keyboard.</param>
    /// <param name="sinceOpen">Seconds since the offer appeared.</param>
    public static PortraitPackWelcomeAnswer AnswerOf(bool downloadClicked, bool notNowClicked, bool enter, bool escape, bool focused, double sinceOpen)
    {
        if (notNowClicked)
        {
            return PortraitPackWelcomeAnswer.NotNow;
        }

        if (downloadClicked)
        {
            return PortraitPackWelcomeAnswer.Download;
        }

        if (!focused)
        {
            return PortraitPackWelcomeAnswer.None;
        }

        if (escape)
        {
            return PortraitPackWelcomeAnswer.NotNow;
        }

        return enter && sinceOpen >= KeySettleSeconds ? PortraitPackWelcomeAnswer.Download : PortraitPackWelcomeAnswer.None;
    }
}
