using Tsukimichi.Ui;

namespace Tsukimichi;

/// <summary>
/// The first-run portrait pack offer (1.22.0, <see cref="PortraitPackOfferWindow"/>): once, to a player without the pack,
/// at the first quiet moment the What's new popup also waits for, and after What's new, the tour (and its offer) and
/// Settings' own download confirmation; it steps aside while any of them is on screen. Enter and Esc are kept from the
/// game only while the offer owns them (<see cref="PortraitPackOfferWindow.ConsumeKeys"/>). Called once from the
/// constructor after <see cref="InitializeWhatsNew"/>; unwound in <see cref="TearDownPortraitPackOffer"/>.
/// </summary>
public sealed partial class Plugin
{
    private PortraitPackOfferWindow? portraitPackOffer;

    private void InitializePortraitPackOffer(ConfigWindow settingsWindow, string pluginVersion)
    {
        if (portraitPack is not { } pack)
        {
            return;
        }

        var offer = new PortraitPackOfferWindow(Settings, PluginInterface, pack, Log, pluginVersion)
        {
            Moment = WhatsNewMomentNow,

            // The tour's offer card counts as Active too (it is the tour's first step).
            OtherFirst = () => whatsNewPopup?.Due == true
                || tutorial?.Active == true
                || settingsWindow.PackDialogShowing
                || umbraAddonCard?.IsOpen == true,
            KeyState = KeyState,
        };
        portraitPackOffer = offer;
        windowSystem.AddWindow(offer);
        Framework.Update += offer.ConsumeKeys;
    }

    /// <summary>Stops keeping keys from the game (from <see cref="TearDown"/>); the window goes with the window system.</summary>
    private void TearDownPortraitPackOffer()
    {
        if (portraitPackOffer is { } offer)
        {
            Framework.Update -= offer.ConsumeKeys;
        }
    }
}
