using System;

namespace Tsukimichi.Ui;

/// <summary>1.21.0 storylines (feature plan v7 N8): the Tonight card's finale line reads the Loose ends.</summary>
public sealed partial class MainWindow
{
    /// <summary>Attaches Loose ends: the Tonight card shows a finale that can be taken now while <paramref name="showFinales"/> says so.</summary>
    public void AttachLooseEnds(LooseEndsSource looseEnds, Func<bool> showFinales)
    {
        tonightCard.LooseEnds = looseEnds ?? throw new ArgumentNullException(nameof(looseEnds));
        tonightCard.ShowFinales = showFinales ?? throw new ArgumentNullException(nameof(showFinales));
    }
}
