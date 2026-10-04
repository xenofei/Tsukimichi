using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Ui;

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

/// <summary>A change of the installed pack, for Giver portraits (<see cref="PortraitPackStatus.ModeAfter"/>).</summary>
public enum PortraitPackChange : byte
{
    /// <summary>A pack installed where none was.</summary>
    FirstInstall,

    /// <summary>A newer pack installed over the one in use.</summary>
    Update,

    /// <summary>The pack downloaded again over a damaged copy.</summary>
    Repair,

    /// <summary>The pack removed.</summary>
    Removed,
}

/// <summary>Which error the Settings row shows for the last run (<see cref="PortraitPackStatus.FailureRowOf"/>).</summary>
public enum PortraitPackFailureRow : byte
{
    /// <summary>No error: the row shows the state.</summary>
    None,

    /// <summary>A download with no pack in use (a first download, or Download again on a damaged pack) failed.</summary>
    Download,

    /// <summary>An update failed: the installed pack stays in use.</summary>
    Update,

    /// <summary>Remove pack failed: the pack stays installed.</summary>
    Removal,
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

    /// <summary>What a finished install was: the first pack, a newer one over the old, or the same pack over a damaged copy.</summary>
    public static PortraitPackChange ChangeOf(bool hadPack, bool wasDamaged) =>
        hadPack ? PortraitPackChange.Update : wasDamaged ? PortraitPackChange.Repair : PortraitPackChange.FirstInstall;

    /// <summary>
    /// Giver portraits after a pack change (spec-1.20 F4). A first install switches it to Game art + pack by itself (the
    /// player asked for the pack); an update or a repair keeps the player's choice, unless they started it by picking
    /// Game art + pack (<paramref name="askedForPack"/>). Removing the pack takes Game art + pack back to Game art.
    /// </summary>
    public static GiverPortraitMode ModeAfter(PortraitPackChange change, GiverPortraitMode current, bool askedForPack) => change switch
    {
        PortraitPackChange.Removed => current == GiverPortraitMode.GameArtAndPack ? GiverPortraitMode.GameArt : current,
        PortraitPackChange.FirstInstall => GiverPortraitMode.GameArtAndPack,
        _ => askedForPack ? GiverPortraitMode.GameArtAndPack : current,
    };

    /// <summary>
    /// Which error the Settings row shows after the last run (<paramref name="finished"/>: one ran since load), by what
    /// it was and where things stand now: a failed removal always (the pack stays); a failed download while the pack is
    /// still to get (<see cref="PortraitPackState.Available"/>, <see cref="PortraitPackState.Damaged"/>) or to update
    /// (<see cref="PortraitPackState.UpdateAvailable"/>, the installed pack staying in use), when the offer is there to
    /// try again. Otherwise none: the row shows its state.
    /// </summary>
    public static PortraitPackFailureRow FailureRowOf(PortraitPackState state, bool offered, PortraitPackFailure last, bool lastWasRemoval, bool finished)
    {
        if (!finished || last == PortraitPackFailure.None)
        {
            return PortraitPackFailureRow.None;
        }

        if (lastWasRemoval)
        {
            return PortraitPackFailureRow.Removal;
        }

        if (!offered)
        {
            return PortraitPackFailureRow.None;
        }

        return state switch
        {
            PortraitPackState.Available or PortraitPackState.Damaged => PortraitPackFailureRow.Download,
            PortraitPackState.UpdateAvailable => PortraitPackFailureRow.Update,
            _ => PortraitPackFailureRow.None,
        };
    }

    /// <summary>Compares pack releases by number ("portraits-1" &lt; "portraits-2"); a tag that does not parse sorts first.</summary>
    public static int CompareTags(string? a, string? b) =>
        PortraitPackOffer.PackNumberOf(a).CompareTo(PortraitPackOffer.PackNumberOf(b));
}
