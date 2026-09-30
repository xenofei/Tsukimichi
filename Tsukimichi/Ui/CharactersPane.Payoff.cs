namespace Tsukimichi.Ui;

/// <summary>
/// "Before you continue" (P5) on the Characters dashboard: the payoff gate lines under the MSQ line, drawn by
/// <see cref="PayoffGateLines"/> for the viewed character. Kept apart from the MSQ line itself so that line can change
/// (branching routes, P14) without touching this.
/// </summary>
public sealed partial class CharactersPane
{
    /// <summary>The payoff gate lines; set by the plugin. Null draws nothing.</summary>
    public PayoffGateLines? PayoffLines { get; set; }

    private void DrawPayoffGates(UiState ui) => PayoffLines?.Draw(ui, "dashboardPayoff");
}
