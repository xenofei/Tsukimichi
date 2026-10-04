using Tsukimichi.Core.Portraits;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Portraits;

/// <summary>
/// The first-run portrait pack offer (1.22.0, <see cref="PortraitPackWelcome"/>): once on a fresh install and once for a
/// player who updated without ever answering it; never with the pack installed, once answered, or with nothing offered;
/// after What's new when both are due; stepping aside, unanswered, outside the world or while something goes first; yes by
/// default, Download downloading, Not now declining, and Enter and Esc answering only once the player clicked inside
/// the offer; and after Download, a removal in Settings never read as a failed download.
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


    // ------------------------------------------------------------------ once open: stepping aside

    [Fact]
    public void Once_open_it_steps_aside_outside_the_world_and_answers_nothing()
    {
        Assert.True(PortraitPackWelcome.Visible(Quiet, otherFirst: false));

        // The title screen or character select: hidden, still open, still unanswered.
        Assert.False(PortraitPackWelcome.Visible(Quiet with { InWorld = false }, otherFirst: false));
        Assert.False(PortraitPackWelcome.Visible(Quiet with { InCombat = true }, otherFirst: false));
        Assert.False(PortraitPackWelcome.Visible(Quiet with { InCutscene = true }, otherFirst: false));
        Assert.False(PortraitPackWelcome.Visible(Quiet with { GroupPose = true }, otherFirst: false));
        Assert.False(PortraitPackWelcome.Visible(Quiet with { Loading = true }, otherFirst: false));

        // Stepping aside is not an answer: the rule still owes the offer, and it comes back in the world.
        Assert.Equal(PortraitPackWelcomeStep.Show, PortraitPackWelcome.Next(Owed(), Quiet));
        Assert.True(PortraitPackWelcome.Visible(Quiet with { InWorld = true }, otherFirst: false));
    }

    [Fact]
    public void Once_open_it_steps_aside_while_something_that_goes_first_appears_and_comes_back_after()
    {
        // What's new from the history, the tour card or Settings' confirmation opens over the shown offer.
        Assert.False(PortraitPackWelcome.Visible(Quiet, otherFirst: true));
        Assert.Equal(PortraitPackWelcomeStep.Wait, PortraitPackWelcome.Next(Owed(otherFirst: true), Quiet));

        // Gone again: the offer is back, still unanswered.
        Assert.True(PortraitPackWelcome.Visible(Quiet, otherFirst: false));
        Assert.Equal(PortraitPackWelcomeStep.Show, PortraitPackWelcome.Next(Owed(), Quiet));
    }

    // ------------------------------------------------------------------ the answer

    [Fact]
    public void A_click_on_download_starts_the_download_engaged_or_not()
    {
        // The mouse is the main way to say yes: a click is a choice whatever the focus.
        Assert.Equal(PortraitPackWelcomeAnswer.Download, PortraitPackWelcome.AnswerOf(downloadClicked: true, notNowClicked: false, enter: false, escape: false, engaged: false, sinceOpen: 0));
        Assert.Equal(PortraitPackWelcomeAnswer.Download, PortraitPackWelcome.AnswerOf(downloadClicked: true, notNowClicked: false, enter: false, escape: false, engaged: true, sinceOpen: 30));
    }

    [Fact]
    public void Enter_downloads_once_engaged_and_settled()
    {
        var settled = PortraitPackWelcome.KeySettleSeconds;
        Assert.Equal(PortraitPackWelcomeAnswer.Download, PortraitPackWelcome.AnswerOf(false, false, enter: true, escape: false, engaged: true, sinceOpen: settled));
        Assert.Equal(PortraitPackWelcomeAnswer.Download, PortraitPackWelcome.AnswerOf(false, false, enter: true, escape: false, engaged: true, sinceOpen: 30));

        // Engaged but not settled: not yet.
        Assert.Equal(PortraitPackWelcomeAnswer.None, PortraitPackWelcome.AnswerOf(false, false, enter: true, escape: false, engaged: true, sinceOpen: 0));
        Assert.Equal(PortraitPackWelcomeAnswer.None, PortraitPackWelcome.AnswerOf(false, false, enter: true, escape: false, engaged: true, sinceOpen: settled - 0.01));
    }

    [Fact]
    public void Enter_never_downloads_before_the_player_engaged_however_long_it_is_up()
    {
        // The offer opens unasked and Dalamud gives the game and ImGui every key: an Enter meant for the game's chat,
        // however long after the offer appeared, is no answer until the player clicked inside the offer.
        foreach (var sinceOpen in new[] { 0, PortraitPackWelcome.KeySettleSeconds - 0.01, PortraitPackWelcome.KeySettleSeconds, 30, 3600 })
        {
            Assert.Equal(PortraitPackWelcomeAnswer.None, PortraitPackWelcome.AnswerOf(false, false, enter: true, escape: false, engaged: false, sinceOpen: sinceOpen));
        }
    }

    [Fact]
    public void Esc_is_ignored_before_engagement_and_declines_after()
    {
        // An Esc meant for the game's menu never declines the offer for good.
        Assert.Equal(PortraitPackWelcomeAnswer.None, PortraitPackWelcome.AnswerOf(false, false, enter: false, escape: true, engaged: false, sinceOpen: 30));

        // Engaged: Esc declines at once (no settle needed to say no).
        Assert.Equal(PortraitPackWelcomeAnswer.NotNow, PortraitPackWelcome.AnswerOf(false, false, enter: false, escape: true, engaged: true, sinceOpen: 0));
        Assert.Equal(PortraitPackWelcomeAnswer.NotNow, PortraitPackWelcome.AnswerOf(false, false, enter: false, escape: true, engaged: true, sinceOpen: 30));
    }

    [Fact]
    public void Not_now_declines_and_declining_wins_a_tie()
    {
        Assert.Equal(PortraitPackWelcomeAnswer.NotNow, PortraitPackWelcome.AnswerOf(false, notNowClicked: true, enter: false, escape: false, engaged: false, sinceOpen: 0));

        // Esc wins over Enter in the same frame, and Not now over a Download click: declining is the safe side.
        Assert.Equal(PortraitPackWelcomeAnswer.NotNow, PortraitPackWelcome.AnswerOf(false, false, enter: true, escape: true, engaged: true, sinceOpen: 30));
        Assert.Equal(PortraitPackWelcomeAnswer.NotNow, PortraitPackWelcome.AnswerOf(true, true, enter: false, escape: false, engaged: true, sinceOpen: 30));
    }

    [Fact]
    public void Nothing_pressed_is_no_answer()
    {
        Assert.Equal(PortraitPackWelcomeAnswer.None, PortraitPackWelcome.AnswerOf(false, false, false, false, engaged: true, sinceOpen: 30));
        Assert.Equal(PortraitPackWelcomeAnswer.None, PortraitPackWelcome.AnswerOf(false, false, false, false, engaged: false, sinceOpen: 30));
    }

    [Fact]
    public void The_keys_and_the_focus_belong_to_the_offer_only_once_engaged_and_settled()
    {
        var settled = PortraitPackWelcome.KeySettleSeconds;
        Assert.False(PortraitPackWelcome.OwnsKeys(engaged: false, sinceOpen: 30, asking: true));
        Assert.False(PortraitPackWelcome.OwnsKeys(engaged: true, sinceOpen: settled - 0.01, asking: true));
        Assert.True(PortraitPackWelcome.OwnsKeys(engaged: true, sinceOpen: settled, asking: true));

        // After Download the window shows progress: the keys go back to the game.
        Assert.False(PortraitPackWelcome.OwnsKeys(engaged: true, sinceOpen: 30, asking: false));
    }

    // ------------------------------------------------------------------ after Download

    [Fact]
    public void After_download_the_window_follows_the_run()
    {
        static PortraitPackOfferView View(bool downloading = false, bool installing = false, bool removing = false, bool finished = true, bool removal = false, PortraitPackFailure last = PortraitPackFailure.None, bool installed = false) =>
            PortraitPackWelcome.ViewAfterDownload(downloading, installing, removing, finished, removal, last, installed);

        Assert.Equal(PortraitPackOfferView.Downloading, View(downloading: true, finished: false));
        Assert.Equal(PortraitPackOfferView.Checking, View(installing: true, finished: false));
        Assert.Equal(PortraitPackOfferView.Installed, View(installed: true));
        Assert.Equal(PortraitPackOfferView.Cancelled, View(last: PortraitPackFailure.Cancelled));
        Assert.Equal(PortraitPackOfferView.Failed, View(last: PortraitPackFailure.Offline));
        Assert.Equal(PortraitPackOfferView.Failed, View(last: PortraitPackFailure.HashMismatch));
    }

    [Fact]
    public void A_removal_since_never_reads_as_a_failed_download()
    {
        static PortraitPackOfferView View(bool removing, bool removal, PortraitPackFailure last, bool installed) =>
            PortraitPackWelcome.ViewAfterDownload(downloading: false, installing: false, removing, finished: true, removal, last, installed);

        // Left on "Installed", then Remove pack in Settings: a removal running, done, or failed (the pack stays).
        Assert.Equal(PortraitPackOfferView.Gone, View(removing: true, removal: false, PortraitPackFailure.None, installed: true));
        Assert.Equal(PortraitPackOfferView.Gone, View(removing: false, removal: true, PortraitPackFailure.None, installed: false));
        Assert.Equal(PortraitPackOfferView.Gone, View(removing: false, removal: true, PortraitPackFailure.DiskError, installed: true));

        // A run that succeeded but left no pack, or no run at all: nothing to show either.
        Assert.Equal(PortraitPackOfferView.Gone, View(removing: false, removal: false, PortraitPackFailure.None, installed: false));
        Assert.Equal(PortraitPackOfferView.Gone, PortraitPackWelcome.ViewAfterDownload(false, false, false, finished: false, false, PortraitPackFailure.None, false));
    }
}
