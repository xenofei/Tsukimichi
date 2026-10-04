using System;

namespace Tsukimichi.Ui;

/// <summary>"Before Evercold" (feature plan v7, 1.20.0, N7): the Tonight card draws the viewed character's checklist.</summary>
public sealed partial class MainWindow
{
    public void AttachBeforeEvercold(BeforeEvercoldCard card)
    {
        tonightCard.BeforeEvercold = card ?? throw new ArgumentNullException(nameof(card));
    }
}
