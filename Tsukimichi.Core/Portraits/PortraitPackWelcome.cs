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

    /// <summary>"Download portraits", or Enter once the player is engaged with the offer: the existing download starts.</summary>
    Download,

    /// <summary>"Not now", or Esc once the player is engaged with the offer: nothing downloads.</summary>
    NotNow,
}

/// <summary>What the offer's window shows once the player chose Download (<see cref="PortraitPackWelcome.ViewAfterDownload"/>).</summary>
public enum PortraitPackOfferView : byte
{
    /// <summary>The download runs: progress.</summary>
    Downloading,

    /// <summary>The file is checked and unpacked.</summary>
    Checking,

    /// <summary>The pack is in.</summary>
    Installed,

    /// <summary>The player cancelled it: nothing was saved.</summary>
    Cancelled,

    /// <summary>The download failed: the reason, and Try again.</summary>
    Failed,

    /// <summary>
    /// Nothing of the offer's to show: the pack was removed (in Settings) since, a removal runs, or nothing ran. The
    /// window closes rather than calling a removal a failed download.
    /// </summary>
    Gone,
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
/// due, What's new shows first and the offer at the next quiet moment after it closes. Once open, it steps aside
/// (<see cref="Visible"/>) whenever it is not in the world, a fight or a scene starts, or something that goes first
/// opens, and comes back after; stepping aside answers nothing.</item>
/// <item><b>Yes by default, and keys only by choice.</b> "Download portraits" is the primary pill, and a click on it
/// is the main way to say yes. Dalamud hands every key to the game and to ImGui alike, so a focused window is no sign
/// that a key was meant for it: the offer opens without taking the focus, and Enter (accept) and Esc (decline) count
/// only once the player is <i>engaged</i>, having clicked inside the offer since it last appeared and kept its focus;
/// Enter not before <see cref="KeySettleSeconds"/> either. An Enter or Esc meant for the game's chat or menu never
/// answers it.</item>
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
    /// Whether the open offer draws this frame: in the world (not the title or character select), not in a fight, a
    /// cutscene, group pose or a loading screen, and with nothing that goes first on screen (<paramref name="otherFirst"/>:
    /// What's new, the tour, Settings' own confirmation). Hidden, it stays open and unanswered and comes back after.
    /// </summary>
    public static bool Visible(in WhatsNewMoment moment, bool otherFirst) =>
        moment.InWorld && !moment.InCombat && !moment.InCutscene && !moment.GroupPose && !moment.Loading && !otherFirst;

    /// <summary>
    /// The player's answer this frame. A click answers at once. A key only once the player is
    /// <paramref name="engaged"/> with the offer (clicked inside it since it last appeared, and it still has the focus):
    /// Esc declines, Enter accepts once the offer has been up <see cref="KeySettleSeconds"/>. Not now wins over a Download
    /// click, and Esc over Enter, in the same frame.
    /// </summary>
    /// <param name="downloadClicked">"Download portraits" was clicked (or activated by the keyboard once focused).</param>
    /// <param name="notNowClicked">"Not now" was clicked.</param>
    /// <param name="enter">Enter was pressed this frame (a fresh press, not a repeat).</param>
    /// <param name="escape">Esc was pressed this frame.</param>
    /// <param name="engaged">The player clicked inside the offer since it last appeared, and it kept the focus.</param>
    /// <param name="sinceOpen">Seconds since the offer appeared.</param>
    public static PortraitPackWelcomeAnswer AnswerOf(bool downloadClicked, bool notNowClicked, bool enter, bool escape, bool engaged, double sinceOpen)
    {
        if (notNowClicked)
        {
            return PortraitPackWelcomeAnswer.NotNow;
        }

        if (downloadClicked)
        {
            return PortraitPackWelcomeAnswer.Download;
        }

        if (!engaged)
        {
            return PortraitPackWelcomeAnswer.None;
        }

        if (escape)
        {
            return PortraitPackWelcomeAnswer.NotNow;
        }

        return enter && sinceOpen >= KeySettleSeconds ? PortraitPackWelcomeAnswer.Download : PortraitPackWelcomeAnswer.None;
    }

    /// <summary>
    /// Whether the keys belong to the offer (Enter to accept, Esc to decline, and the keyboard focus on Download
    /// portraits): only once the player is engaged, the offer has settled, and it still asks.
    /// </summary>
    public static bool OwnsKeys(bool engaged, double sinceOpen, bool asking) => asking && engaged && sinceOpen >= KeySettleSeconds;

    /// <summary>
    /// What the window shows after the player chose Download, from the service's phase and its last run: progress,
    /// checking, installed, cancelled or failed; and <see cref="PortraitPackOfferView.Gone"/> when the pack was removed
    /// since (or a removal runs), or nothing has run, so a removal in Settings never reads as a failed download.
    /// </summary>
    /// <param name="downloading">The service is downloading.</param>
    /// <param name="installing">The service is checking and unpacking.</param>
    /// <param name="removing">The service is removing the pack.</param>
    /// <param name="finished">A run finished since load.</param>
    /// <param name="lastWasRemoval">The last run was a removal.</param>
    /// <param name="last">The last run's result.</param>
    /// <param name="installed">A pack is installed now.</param>
    public static PortraitPackOfferView ViewAfterDownload(bool downloading, bool installing, bool removing, bool finished, bool lastWasRemoval, PortraitPackFailure last, bool installed)
    {
        if (downloading)
        {
            return PortraitPackOfferView.Downloading;
        }

        if (installing)
        {
            return PortraitPackOfferView.Checking;
        }

        if (removing || lastWasRemoval || !finished)
        {
            return PortraitPackOfferView.Gone;
        }

        return last switch
        {
            PortraitPackFailure.None => installed ? PortraitPackOfferView.Installed : PortraitPackOfferView.Gone,
            PortraitPackFailure.Cancelled => PortraitPackOfferView.Cancelled,
            _ => PortraitPackOfferView.Failed,
        };
    }
}
