using System;
using Tsukimichi.Core.Triad;

namespace Tsukimichi.Ui;

/// <summary>1.21.0 P6: the Triple Triad opponents for the detail pane's Unlocks line.</summary>
public sealed partial class MainWindow
{
    /// <summary>The Triple Triad opponents (read once from the sheets) for the detail pane's Unlocks line.</summary>
    public void AttachTriad(Func<TriadOpponents?> opponents)
    {
        detailPane.TriadOpponents = opponents ?? throw new ArgumentNullException(nameof(opponents));
    }
}
