namespace Tsukimichi.Ui;

/// <summary>
/// "Before you continue" (P5) in the Tonight card: the payoff gate lines right under the main scenario row, drawn by
/// <see cref="PayoffGateLines"/> for the viewed character. Kept apart from the MSQ row so that row can change
/// (branching routes, P14) without touching this.
/// </summary>
public sealed partial class TonightCard
{
    /// <summary>The payoff gate lines; set by the plugin. Null draws nothing.</summary>
    public PayoffGateLines? PayoffLines { get; set; }

    private void DrawPayoffGates() => PayoffLines?.Draw(ui, "tonightPayoff");
}
