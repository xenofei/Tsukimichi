using System.Collections.Generic;
using Tsukimichi.Core.Umbra;

namespace Tsukimichi.Config;

/// <summary>
/// Setting up Tsukimichi for Umbra from Tsukimichi (<c>Game/UmbraAddonSetupService.cs</c>): whether the player answered
/// the "Add Tsukimichi to your Umbra bar?" card, and the records of what Tsukimichi changed in Umbra, one per Umbra
/// configuration profile, which "Remove from Umbra" (Settings › About › Umbra) reverses on that profile only. Only
/// Tsukimichi's own changes are recorded.
/// </summary>
public sealed partial class Configuration
{
    /// <summary>The player answered the card (Not now, Agree and add, or closed it): it never shows again. Settings keeps the actions.</summary>
    public bool UmbraSetupOfferAnswered { get; set; }

    /// <summary>What Tsukimichi changed in Umbra, per Umbra profile (<see cref="UmbraSetupBook"/>).</summary>
    public List<UmbraSetupRecordData> UmbraSetupRecords { get; set; } = [];
}
