using System;
using Tsukimichi.Core.Companions;
using Tsukimichi.GameData;

namespace Tsukimichi;

/// <summary>
/// 1.20.0 "Before Evercold" wiring (feature plan v7, N7): one <see cref="Ui.EvercoldCardModel"/> for the viewed
/// character, shared by the Tonight card and the Characters dashboard, reading the flight index (and the live
/// character's attuned currents) and the duty index as they arrive. Called once from the constructor after both exist.
/// </summary>
public sealed partial class Plugin
{
    private void InitializeBeforeEvercold(Ui.CharactersPane charactersPane, Ui.UiState ui, Func<FlightIndex?> flight, Func<DutyRunIndex?> duties, Func<uint, bool?> attuned)
    {
        var card = new Ui.EvercoldCardModel(Session, CharacterBook)
        {
            Flight = flight,
            Duties = duties,
            Attuned = attuned,
            ShowDutyBoard = () =>
            {
                ui.Tab = Ui.NavTab.Characters;
                charactersPane.RequestDutyBoard();
            },
        };
        charactersPane.Evercold = card;
        mainWindow.AttachEvercold(card);
    }
}
