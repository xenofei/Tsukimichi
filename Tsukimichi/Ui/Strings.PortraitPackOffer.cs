using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for 1.22.0's first-run portrait pack offer (<see cref="PortraitPackOfferWindow"/>). The facts, the spoiler
/// line and the error lines are the Settings confirmation's own (<c>Strings.PortraitPack.cs</c>). English only until
/// localization reopens.
/// </summary>
static partial class Strings
{
    public static string PackOfferWhat => Loc.Get("PackOffer.What");

    public static string PackOfferPromiseLead => Loc.Get("PackOffer.PromiseLead");

    public static string PackOfferPromise => Loc.Get("PackOffer.Promise");

    public static string PackOfferLater => Loc.Get("PackOffer.Later");

    public static string PackOfferDownload => Loc.Get("PackOffer.Download");

    public static string PackOfferNotNow => Loc.Get("PackOffer.NotNow");

    public static string PackOfferDownloading => Loc.Get("PackOffer.Downloading");

    public static string PackOfferChecking => Loc.Get("PackOffer.Checking");

    public static string PackOfferInstalled => Loc.Get("PackOffer.Installed");

    public static string PackOfferInstalledLine => Loc.Get("PackOffer.InstalledLine");

    public static string PackOfferKeepsGoing => Loc.Get("PackOffer.KeepsGoing");

    public static string PackOfferClose => Loc.Get("PackOffer.Close");
}
