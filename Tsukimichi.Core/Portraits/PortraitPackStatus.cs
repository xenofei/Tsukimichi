using Tsukimichi.Core.Diagnostics;

namespace Tsukimichi.Core.Portraits;

/// <summary>Where the portrait pack stands, for the Settings row (feature plan v7 F4).</summary>
public enum PortraitPackState : byte
{
    /// <summary>This build offers no pack and none is installed: the row says so and offers nothing.</summary>
    NotOffered,

    /// <summary>A pack is offered and none is installed: Download.</summary>
    Available,

    /// <summary>The installed pack is the offered one (or newer): Remove.</summary>
    Installed,

    /// <summary>A pack is installed and this build offers a different one: Update (asked, never automatic) or Remove.</summary>
    UpdateAvailable,

    /// <summary><c>current.json</c> names a pack that is not whole: Download again (when offered) or Remove.</summary>
    Damaged,
}

/// <summary>The Settings row's decisions about the pack, pure so they are tested.</summary>
public static class PortraitPackStatus
{
    /// <summary>
    /// The state for an <paramref name="offer"/> (null: this build offers none) and an <paramref name="installed"/> pack
    /// (null: none, or <paramref name="damaged"/>). An installed pack differing from the offer is an update only when the
    /// offer's release is not older than the installed one's, so going back to an older plugin never offers to replace a
    /// newer pack with an older one.
    /// </summary>
    public static PortraitPackState Of(PortraitPackOffer? offer, PortraitPack? installed, bool damaged)
    {
        if (installed is not null)
        {
            if (offer is null || string.Equals(offer.Sha256, installed.Sha256, StringComparison.Ordinal))
            {
                return PortraitPackState.Installed;
            }

            return CompareTags(offer.Tag, installed.Tag) >= 0 ? PortraitPackState.UpdateAvailable : PortraitPackState.Installed;
        }

        if (damaged)
        {
            return PortraitPackState.Damaged;
        }

        return offer is null ? PortraitPackState.NotOffered : PortraitPackState.Available;
    }

    /// <summary>
    /// Whether the pack was built for an older game version than the client runs (<see cref="DataFreshness.Compare"/>).
    /// The pack still works: givers added since then show game art or a fallback. False when either version is unknown.
    /// </summary>
    public static bool ForOlderGame(string packGameVersion, string clientGameVersion) =>
        !string.IsNullOrWhiteSpace(packGameVersion) && !string.IsNullOrWhiteSpace(clientGameVersion)
        && DataFreshness.Compare(packGameVersion, clientGameVersion) == FreshnessVerdict.NewerClient;

    /// <summary>Compares pack releases by number ("portraits-1" &lt; "portraits-2"); a tag that does not parse sorts first.</summary>
    public static int CompareTags(string? a, string? b) =>
        PortraitPackOffer.PackNumberOf(a).CompareTo(PortraitPackOffer.PackNumberOf(b));
}
