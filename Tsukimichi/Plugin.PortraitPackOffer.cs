using Tsukimichi.Ui;

namespace Tsukimichi;

/// <summary>
/// The first-run portrait pack offer (1.22.0, <see cref="PortraitPackOfferWindow"/>): once, to a player without the pack,
/// at the first quiet moment the What's new popup also waits for, and after What's new, the tour and Settings' own
/// download confirmation. Called once from the constructor after <see cref="InitializeWhatsNew"/>.
/// </summary>
public sealed partial class Plugin
{
    private void InitializePortraitPackOffer(ConfigWindow settingsWindow, string pluginVersion)
    {
        if (portraitPack is not { } pack)
        {
            return;
        }

        var offer = new PortraitPackOfferWindow(Settings, PluginInterface, pack, Log, pluginVersion)
        {
            Moment = WhatsNewMomentNow,
            OtherFirst = () => whatsNewPopup?.Due == true
                || tutorial?.Active == true
                || tutorial is TutorialOverlay { Offering: true }
                || settingsWindow.PackDialogShowing,
        };
        windowSystem.AddWindow(offer);
    }
}
