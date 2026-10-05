using Tsukimichi.Core.Portraits;
using Tsukimichi.Core.Ui;
using Tsukimichi.Ui;

namespace Tsukimichi;

/// <summary>
/// Setting up Tsukimichi for Umbra from Tsukimichi: Umbra driven through its own code (<see cref="Game.UmbraControl"/>,
/// made here and nowhere else), the service the card and Settings share (<see cref="Game.UmbraAddonSetupService"/>), and
/// the "Add Tsukimichi to your Umbra bar?" card (<see cref="UmbraAddonCard"/>), which waits for What's new, the tour, the
/// portrait pack offer and Settings' own confirmation. Called once from the constructor after
/// <see cref="InitializeWelcomeHome"/> (it needs the Umbra probe); unwound in <see cref="TearDownUmbraAddonSetup"/>.
/// </summary>
public sealed partial class Plugin
{
    private Game.UmbraControl? umbraControl;
    private Game.UmbraAddonSetupService? umbraSetup;
    private UmbraAddonCard? umbraAddonCard;

    private void InitializeUmbraAddonSetup(ConfigWindow settingsWindow)
    {
        if (umbraProbe is not { } umbra)
        {
            return;
        }

        var control = new Game.UmbraControl(Framework, Log);
        umbraControl = control;
        var setup = new Game.UmbraAddonSetupService(control, Settings, PluginInterface, Framework, umbra, Log);
        umbraSetup = setup;
        var card = new UmbraAddonCard(Settings, PluginInterface, setup, umbra, Log)
        {
            Moment = WhatsNewMomentNow,
            OtherFirst = () => whatsNewPopup?.Due == true
                || tutorial?.Active == true
                || settingsWindow.PackDialogShowing
                || portraitPackOffer?.IsOpen == true
                || PortraitPackOfferOwed(),
        };
        umbraAddonCard = card;
        windowSystem.AddWindow(card);
        settingsWindow.UmbraSetup = setup;
        settingsWindow.ShowUmbraConfirm = card.ShowConfirm;
    }

    /// <summary>
    /// The portrait pack offer is still to come (it would show at a quiet moment, or its pack isn't read yet): the Umbra
    /// card waits for it, so the two never meet.
    /// </summary>
    private bool PortraitPackOfferOwed()
    {
        if (Settings.PortraitPackOfferAnswered || portraitPack is not { } pack)
        {
            return false;
        }

        var quiet = new WhatsNewMoment(true, WhatsNew.SettleSeconds, false, false, false, false, false);
        var next = PortraitPackWelcome.Next(new PortraitPackWelcomeState(false, pack.Loaded, pack.State, pack.Busy, false), quiet);
        return next is PortraitPackWelcomeStep.Show or PortraitPackWelcomeStep.Wait;
    }

    /// <summary>Stops the setup's calls into Umbra and its per-frame check (from <see cref="TearDown"/>); the card goes with the window system.</summary>
    private void TearDownUmbraAddonSetup()
    {
        umbraControl?.Dispose();
        umbraSetup?.Dispose();
    }
}
