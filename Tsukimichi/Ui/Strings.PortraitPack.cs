using Tsukimichi.Core.Portraits;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for 1.20.0's optional portrait pack (feature plan v7 F4, decision 8): the Settings row, its two
/// confirmations, and the line each result leaves. English only until localization reopens.
/// </summary>
static partial class Strings
{
    public static string PortraitPackLabel => Loc.Get("Settings.PortraitPack");

    /// <summary>{0} = givers, {1} = size ("14.2 MB"), {2} = release tag ("v1.20.0").</summary>
    public static string PortraitPackHintAvailable => Loc.Get("Settings.PortraitPackHintAvailable");

    public static string PortraitPackHintNotOffered => Loc.Get("Settings.PortraitPackHintNotOffered");

    /// <summary>{0} = givers, {1} = release tag.</summary>
    public static string PortraitPackHintInstalled => Loc.Get("Settings.PortraitPackHintInstalled");

    /// <summary>{0} = size, {1} = release tag.</summary>
    public static string PortraitPackHintUpdate => Loc.Get("Settings.PortraitPackHintUpdate");

    public static string PortraitPackHintDamaged => Loc.Get("Settings.PortraitPackHintDamaged");

    public static string PortraitPackDownload => Loc.Get("Settings.PortraitPackDownload");

    public static string PortraitPackUpdate => Loc.Get("Settings.PortraitPackUpdate");

    public static string PortraitPackRemove => Loc.Get("Settings.PortraitPackRemove");

    public static string PortraitPackCancel => Loc.Get("Settings.PortraitPackCancel");

    /// <summary>{0} = received ("4.1 MB"), {1} = total.</summary>
    public static string PortraitPackProgress => Loc.Get("Settings.PortraitPackProgress");

    public static string PortraitPackInstalling => Loc.Get("Settings.PortraitPackInstalling");

    public static string PortraitPackRemoving => Loc.Get("Settings.PortraitPackRemoving");

    /// <summary>{0} = the pack's game version, {1} = the client's.</summary>
    public static string PortraitPackOlderGame => Loc.Get("Settings.PortraitPackOlderGame");

    public static string PortraitPackNotInUse => Loc.Get("Settings.PortraitPackNotInUse");

    public static string PortraitPackCredit => Loc.Get("Settings.PortraitPackCredit");

    public static string PortraitPackConfirmTitle => Loc.Get("Settings.PortraitPackConfirmTitle");

    /// <summary>{0} = size, {1} = release tag, {2} = the address.</summary>
    public static string PortraitPackConfirmText => Loc.Get("Settings.PortraitPackConfirmText");

    public static string PortraitPackConfirmButton => Loc.Get("Settings.PortraitPackConfirmButton");

    public static string PortraitPackRemoveTitle => Loc.Get("Settings.PortraitPackRemoveTitle");

    public static string PortraitPackRemoveText => Loc.Get("Settings.PortraitPackRemoveText");

    public static string PortraitPackRemoveButton => Loc.Get("Settings.PortraitPackRemoveButton");

    public static string PortraitPackInstalled => Loc.Get("Settings.PortraitPackDone");

    public static string PortraitPackRemoved => Loc.Get("Settings.PortraitPackRemoved");

    public static string PortraitPackRemoveIncomplete => Loc.Get("Settings.PortraitPackRemoveIncomplete");

    /// <summary>The line a download or install that stopped leaves: what happened, and that nothing was installed.</summary>
    public static string PortraitPackFailed(PortraitPackFailure failure) => failure switch
    {
        PortraitPackFailure.Offline => Loc.Get("Settings.PortraitPackFailedOffline"),
        PortraitPackFailure.NotFound => Loc.Get("Settings.PortraitPackFailedNotFound"),
        PortraitPackFailure.Redirected => Loc.Get("Settings.PortraitPackFailedRedirected"),
        PortraitPackFailure.TooLarge or PortraitPackFailure.SizeMismatch => Loc.Get("Settings.PortraitPackFailedSize"),
        PortraitPackFailure.HashMismatch => Loc.Get("Settings.PortraitPackFailedHash"),
        PortraitPackFailure.BadArchive or PortraitPackFailure.UnsafeEntry or PortraitPackFailure.BadManifest or PortraitPackFailure.BadImage => Loc.Get("Settings.PortraitPackFailedChecks"),
        PortraitPackFailure.DiskFull => Loc.Get("Settings.PortraitPackFailedDiskFull"),
        PortraitPackFailure.DiskError => Loc.Get("Settings.PortraitPackFailedDisk"),
        PortraitPackFailure.Cancelled => Loc.Get("Settings.PortraitPackFailedCancelled"),
        _ => Loc.Get("Settings.PortraitPackFailedHttp"),
    };
}
