using System;

namespace Tsukimichi.Ui;

/// <summary>"Before Evercold" (feature plan v7, 1.20.0, N7): the Tonight card draws the viewed character's card.</summary>
public sealed partial class MainWindow
{
    public void AttachEvercold(EvercoldCardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        tonightCard.Evercold = card;

        // The journal line's Make room is C9's popover; "Show them" persists the Journal's filters as Tonight's does.
        card.MakeRoom = () => makeRoom;
        card.FiltersChanged = OnFiltersChanged;
    }
}
