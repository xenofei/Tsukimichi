using Tsukimichi.Commands;

namespace Tsukimichi;

/// <summary>
/// 1.19.0 "Right answers" wiring (feature plan v7): the game's own offers (C1, <see cref="Game.OfferObserver"/>) feed
/// the detail pane, <c>/tsuki why</c>, the Report block and Settings › Advanced › Diagnostics; the quest offer window
/// hands it each quest it shows; and Switch gearset (C8, <see cref="Game.GearsetSwitcher"/>) joins the detail pane. Both
/// follow the shared hook gate. Called once from the constructor and unwound by <see cref="DisposeRightAnswers"/>.
/// </summary>
public sealed partial class Plugin
{
    private Game.OfferObserver? offerObserver;

    private void InitializeRightAnswers(Core.Runtime.HookGate gate, Game.DiagnosticBuilder diagnostics, WhyCommand why)
    {
        var offers = new Game.OfferObserver(Framework, ClientState, Session, Snapshots, Writer, gate, Log);
        offerObserver = offers;
        diagnostics.Offers = offers;
        why.GameOffer = offers.Check;
        if (gamePanels is { } panels)
        {
            panels.OfferIdentified = offers.Offered;
        }

        mainWindow.AttachRightAnswers(offers, new Game.GearsetSwitcher(Condition, ClientState, gate, Log));
        mainWindow.AttachGateMarks(CharacterBook);
    }

    private void DisposeRightAnswers()
    {
        if (gamePanels is { } panels)
        {
            panels.OfferIdentified = null;
        }

        offerObserver?.Dispose();
    }
}
