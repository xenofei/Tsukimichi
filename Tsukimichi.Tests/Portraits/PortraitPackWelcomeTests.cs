using Tsukimichi.Core.Portraits;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Portraits;

/// <summary>
/// The first-run portrait pack offer (1.22.0, <see cref="PortraitPackWelcome"/>): once on a fresh install and once for a
/// player who updated without ever answering it; never with the pack installed, once answered, or with nothing offered;
/// after What's new when both are due; and yes by default, Enter and Download downloading, Esc and Not now declining.
/// </summary>
public sealed class PortraitPackWelcomeTests
{
    private static readonly WhatsNewMoment Quiet = new(InWorld: true, SecondsInWorld: 30, InCombat: false, InDuty: false, InCutscene: false, GroupPose: false, Loading: false);

    private static PortraitPackWelcomeState Owed(bool otherFirst = false) =>
        new(Answered: false, Loaded: true, Pack: PortraitPackState.Available, Busy: false, OtherFirst: otherFirst);

    /// <summary>
    /// The plugin's loop in miniature: each frame the offer is shown when due, and the player's answer is kept. Returns how
    /// many times it showed over <paramref name="frames"/> frames; <paramref name="whatsNewDueFor"/> frames of What's new
    /// come first.
    /// </summary>
    private static (int Shown, int FirstShownAt) Run(PortraitPackState pack, int frames, int whatsNewDueFor = 0, bool answered = false)
    {
        var shown = 0;
        var firstAt = -1;
        for (var frame = 0; frame < frames; frame++)
        {
            var state = new PortraitPackWelcomeState(answered, Loaded: true, pack, Busy: false, OtherFirst: frame < whatsNewDueFor);
            switch (PortraitPackWelcome.Next(state, Quiet))
            {
                case PortraitPackWelcomeStep.Show:
                    shown++;
                    firstAt = firstAt < 0 ? frame : firstAt;

                    // Any answer (Download, Not now, Esc, closing it) is kept.
                    answered = true;
                    break;
                case PortraitPackWelcomeStep.Retire:
                    answered = true;
                    break;
            }
        }

        return (shown, firstAt);
    }

    // ------------------------------------------------------------------ when it shows

    [Fact]
    public void A_fresh_install_shows_it_once_at_the_first_quiet_moment()
    {
        // No prior configuration: What's new records silently, so nothing goes before the offer.
        Assert.Equal(WhatsNewDecision.RecordSilently, WhatsNew.Decide(string.Empty, "1.22.0", hasSection: true, hasPriorConfig: false));
        Assert.Equal(PortraitPackWelcomeStep.Show, PortraitPackWelcome.Next(Owed(), Quiet));
        Assert.Equal((1, 0), Run(PortraitPackState.Available, frames: 50));
    }

    [Fact]
    public void An_existing_player_who_never_answered_sees_it_once_too()
    {
        // An update brings What's new; the offer follows it, once.
        Assert.Equal(WhatsNewDecision.Show, WhatsNew.Decide("1.21.0", "1.22.0", hasSection: true, hasPriorConfig: true));
        Assert.Equal((1, 20), Run(PortraitPackState.Available, frames: 50, whatsNewDueFor: 20));

        // An update with nothing to show in What's new: the offer alone.
        Assert.Equal(WhatsNewDecision.RecordSilently, WhatsNew.Decide("1.21.0", "1.22.0", hasSection: false, hasPriorConfig: true));
        Assert.Equal((1, 0), Run(PortraitPackState.Available, frames: 50));
    }

    [Fact]
    public void It_waits_for_the_first_quiet_moment()
    {
        var notQuiet = new[]
        {
            Quiet with { InWorld = false },
            Quiet with { SecondsInWorld = WhatsNew.SettleSeconds - 1 },
            Quiet with { InCombat = true },
            Quiet with { InDuty = true },
            Quiet with { InCutscene = true },
            Quiet with { GroupPose = true },
            Quiet with { Loading = true },
        };

        foreach (var moment in notQuiet)
        {
            Assert.Equal(PortraitPackWelcomeStep.Wait, PortraitPackWelcome.Next(Owed(), moment));
        }

        Assert.Equal(PortraitPackWelcomeStep.Show, PortraitPackWelcome.Next(Owed(), Quiet with { SecondsInWorld = WhatsNew.SettleSeconds }));
    }

    [Fact]
    public void It_waits_until_the_installed_pack_has_been_read()
    {
        // Before the start-up read, "no pack" may only mean "not read yet".
        Assert.Equal(PortraitPackWelcomeStep.Wait, PortraitPackWelcome.Next(Owed() with { Loaded = false }, Quiet));
        Assert.Equal(PortraitPackWelcomeStep.Wait, PortraitPackWelcome.Next(Owed() with { Loaded = false, Pack = PortraitPackState.Installed }, Quiet));
    }

    // ------------------------------------------------------------------ when it never shows

    [Theory]
    [InlineData(PortraitPackState.Installed)]
    [InlineData(PortraitPackState.UpdateAvailable)]
    [InlineData(PortraitPackState.Damaged)]
    public void A_player_with_the_pack_never_sees_it_and_it_is_retired(PortraitPackState pack)
    {
        Assert.Equal(PortraitPackWelcomeStep.Retire, PortraitPackWelcome.Next(Owed() with { Pack = pack }, Quiet));
        Assert.Equal((0, -1), Run(pack, frames: 50));

        // Retired even outside a quiet moment, and even while What's new is due: there is nothing to ask.
        Assert.Equal(PortraitPackWelcomeStep.Retire, PortraitPackWelcome.Next(Owed(otherFirst: true) with { Pack = pack }, Quiet with { InCombat = true }));
    }

    [Fact]
    public void A_download_started_in_settings_answers_it()
    {
        Assert.Equal(PortraitPackWelcomeStep.Retire, PortraitPackWelcome.Next(Owed() with { Busy = true }, Quiet));
    }

    [Fact]
    public void Once_answered_it_never_shows_again()
    {
        Assert.Equal(PortraitPackWelcomeStep.Nothing, PortraitPackWelcome.Next(Owed() with { Answered = true }, Quiet));
        Assert.Equal((0, -1), Run(PortraitPackState.Available, frames: 50, answered: true));

    }

    [Fact]
    public void A_pack_removed_later_does_not_bring_it_back()
    {
        // At start the pack is installed: the offer is retired, and the plugin keeps that as the answer.
        var answered = PortraitPackWelcome.Next(Owed() with { Pack = PortraitPackState.Installed }, Quiet) == PortraitPackWelcomeStep.Retire;
        Assert.True(answered);

        // The player removes the pack: it is available again, but the offer was answered.
        Assert.Equal(PortraitPackWelcomeStep.Nothing, PortraitPackWelcome.Next(Owed() with { Answered = answered }, Quiet));
    }

    [Fact]
    public void With_no_pack_offered_it_never_shows_and_records_nothing()
    {
        // Nothing (not Retire): a later build that offers a pack may still ask a player who never answered.
        Assert.Equal(PortraitPackWelcomeStep.Nothing, PortraitPackWelcome.Next(Owed() with { Pack = PortraitPackState.NotOffered }, Quiet));
        Assert.Equal((0, -1), Run(PortraitPackState.NotOffered, frames: 50));
    }

    // ------------------------------------------------------------------ after What's new

    [Fact]
    public void It_never_shows_alongside_whats_new_but_right_after_it()
    {
        Assert.Equal(PortraitPackWelcomeStep.Wait, PortraitPackWelcome.Next(Owed(otherFirst: true), Quiet));

        // What's new due for 30 frames (waiting, then open): the offer shows on the first frame after it, once.
        var (shown, at) = Run(PortraitPackState.Available, frames: 100, whatsNewDueFor: 30);
        Assert.Equal(1, shown);
        Assert.Equal(30, at);
    }

    [Fact]
    public void After_whats_new_it_still_waits_for_a_quiet_moment()
    {
        // What's new closed, but the player is now in a duty: the offer waits for the next quiet moment.
        Assert.Equal(PortraitPackWelcomeStep.Wait, PortraitPackWelcome.Next(Owed(), Quiet with { InDuty = true }));
        Assert.Equal(PortraitPackWelcomeStep.Show, PortraitPackWelcome.Next(Owed(), Quiet));
    }

    // ------------------------------------------------------------------ the answer

    [Fact]
    public void Download_and_enter_start_the_download()
    {
        var settled = PortraitPackWelcome.KeySettleSeconds;
        Assert.Equal(PortraitPackWelcomeAnswer.Download, PortraitPackWelcome.AnswerOf(downloadClicked: true, notNowClicked: false, enter: false, escape: false, focused: false, sinceOpen: 0));
        Assert.Equal(PortraitPackWelcomeAnswer.Download, PortraitPackWelcome.AnswerOf(false, false, enter: true, escape: false, focused: true, sinceOpen: settled));
        Assert.Equal(PortraitPackWelcomeAnswer.Download, PortraitPackWelcome.AnswerOf(false, false, enter: true, escape: false, focused: true, sinceOpen: 30));
    }

    [Fact]
    public void Enter_never_downloads_before_the_offer_settles_or_off_its_window()
    {
        // The offer opens unasked: an Enter meant for the game's chat must not download.
        Assert.Equal(PortraitPackWelcomeAnswer.None, PortraitPackWelcome.AnswerOf(false, false, enter: true, escape: false, focused: true, sinceOpen: 0));
        Assert.Equal(PortraitPackWelcomeAnswer.None, PortraitPackWelcome.AnswerOf(false, false, enter: true, escape: false, focused: true, sinceOpen: PortraitPackWelcome.KeySettleSeconds - 0.01));
        Assert.Equal(PortraitPackWelcomeAnswer.None, PortraitPackWelcome.AnswerOf(false, false, enter: true, escape: false, focused: false, sinceOpen: 30));
    }

    [Fact]
    public void Esc_and_not_now_decline()
    {
        Assert.Equal(PortraitPackWelcomeAnswer.NotNow, PortraitPackWelcome.AnswerOf(false, false, enter: false, escape: true, focused: true, sinceOpen: 0));
        Assert.Equal(PortraitPackWelcomeAnswer.NotNow, PortraitPackWelcome.AnswerOf(false, notNowClicked: true, enter: false, escape: false, focused: false, sinceOpen: 0));

        // Esc wins over Enter in the same frame, and Not now over a Download click: declining is the safe side.
        Assert.Equal(PortraitPackWelcomeAnswer.NotNow, PortraitPackWelcome.AnswerOf(false, false, enter: true, escape: true, focused: true, sinceOpen: 30));
        Assert.Equal(PortraitPackWelcomeAnswer.NotNow, PortraitPackWelcome.AnswerOf(true, true, enter: false, escape: false, focused: true, sinceOpen: 30));

        // Esc on another window is not an answer.
        Assert.Equal(PortraitPackWelcomeAnswer.None, PortraitPackWelcome.AnswerOf(false, false, enter: false, escape: true, focused: false, sinceOpen: 30));
    }

    [Fact]
    public void Nothing_pressed_is_no_answer()
    {
        Assert.Equal(PortraitPackWelcomeAnswer.None, PortraitPackWelcome.AnswerOf(false, false, false, false, focused: true, sinceOpen: 30));
    }
}
