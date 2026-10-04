using Tsukimichi.Core.Companions;

namespace Tsukimichi.Ui;

/// <summary>
/// The automation level's travel answers (1.18, A10): whether Teleport and the aethernet hop are shown at all
/// (<see cref="AutomationGate"/>), and whether Flag on map leads the detail pane's pills, as it does without Lifestream.
/// Walk and Go to giver keep their own <see cref="WalkShown"/> and <see cref="GoToShown"/>, which the plugin wires to the
/// same gate.
/// </summary>
public sealed partial class GameLinks
{
    /// <summary>Whether Teleport and aethernet buttons and menu items are drawn at all (the automation level).</summary>
    public bool TeleportShown => AutomationGate.Shows(AutomationButtons.Teleport);

    /// <summary>
    /// Flag on map leads the detail pane's pills (and leaves the round buttons) when Teleport cannot be offered: without
    /// Lifestream, or with Teleport hidden by the automation level.
    /// </summary>
    public bool FlagLeads => !TeleportAvailable || !TeleportShown;
}
