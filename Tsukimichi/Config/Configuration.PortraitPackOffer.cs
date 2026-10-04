namespace Tsukimichi.Config;

/// <summary>
/// 1.22.0, the first-run portrait pack offer (<c>Core.Portraits.PortraitPackWelcome</c>, <c>Ui/PortraitPackOfferWindow.cs</c>).
/// </summary>
public sealed partial class Configuration
{
    /// <summary>
    /// Whether the first-run portrait pack offer was answered (Download portraits, Not now, Esc or closing it), or retired
    /// because the player already had the pack or downloaded it from Settings. Set once; the offer then never shows again.
    /// </summary>
    public bool PortraitPackOfferAnswered { get; set; }
}
