using System.Collections.Generic;

namespace Tsukimichi.Config;

/// <summary>
/// Setting up Tsukimichi for Umbra from Tsukimichi (<c>Game/UmbraAddonSetupService.cs</c>): whether the player answered
/// the "Add Tsukimichi to your Umbra bar?" card, and the record of what Tsukimichi changed in Umbra, which "Remove from
/// Umbra" (Settings › About › Umbra) reverses. Only Tsukimichi's own changes are recorded.
/// </summary>
public sealed partial class Configuration
{
    /// <summary>The player answered the card (Not now, Agree and add, or closed it): it never shows again. Settings keeps the actions.</summary>
    public bool UmbraSetupOfferAnswered { get; set; }

    /// <summary>Tsukimichi turned Umbra's custom plugins on (they were off); Remove from Umbra turns them off again.</summary>
    public bool UmbraSetupTurnedOnCustomPlugins { get; set; }

    /// <summary>Tsukimichi added the xenofei/Tsukimichi.Umbra repository to Umbra (it wasn't listed); Remove from Umbra removes it.</summary>
    public bool UmbraSetupAddedRepository { get; set; }

    /// <summary>The Umbra widget instances Tsukimichi placed (their instance ids); Remove from Umbra takes them off the bar.</summary>
    public List<string> UmbraSetupWidgetIds { get; set; } = [];
}
