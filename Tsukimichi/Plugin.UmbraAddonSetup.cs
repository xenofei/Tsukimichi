using Tsukimichi.Ui;

namespace Tsukimichi;

/// <summary>
/// Setting up Tsukimichi for Umbra from Tsukimichi: Umbra driven through its own code (<see cref="Game.UmbraControl"/>,
/// made here and nowhere else), the service the card and Settings share (<see cref="Game.UmbraAddonSetupService"/>), and
/// the "Add Tsukimichi to your Umbra bar?" card (<see cref="UmbraAddonCard"/>), which waits for What's new and the tour.
/// Called once from the constructor after <see cref="InitializeWelcomeHome"/> (it needs the Umbra probe); unwound in
/// <see cref="TearDownUmbraAddonSetup"/>.
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
        var setup = new Game.UmbraAddonSetupService(control, Settings, PluginInterface, Framework, umbra, Log) { Moment = WhatsNewMomentNow };
        umbraSetup = setup;
        var card = new UmbraAddonCard(Settings, PluginInterface, setup, umbra, Log)
        {
            Moment = WhatsNewMomentNow,

            // The tour's offer card counts as Active too (it is the tour's first step).
            OtherFirst = () => whatsNewPopup?.Due == true || tutorial?.Active == true,
        };
        umbraAddonCard = card;
        windowSystem.AddWindow(card);
        settingsWindow.UmbraSetup = setup;
        settingsWindow.ShowUmbraConfirm = card.ShowConfirm;
    }

    /// <summary>Stops the setup's calls into Umbra and its per-frame check (from <see cref="TearDown"/>); the card goes with the window system.</summary>
    private void TearDownUmbraAddonSetup()
    {
        umbraControl?.Dispose();
        umbraSetup?.Dispose();
    }
}
