using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for multibox sharing (D11): the "live in another client" badge wherever characters are listed, its
/// tooltip, the banner over a character shown from another client's saves, and the Forget hint. Constant names carry
/// the <c>Multibox</c> prefix so this part of the partial class never collides with the others.
/// </summary>
static partial class Strings
{
    /// <summary>The badge after a character's name, lower case.</summary>
    public static string MultiboxLiveElsewhere => Loc.Get("Multibox.LiveElsewhere");

    public static string MultiboxLiveElsewhereTooltip => Loc.Get("Multibox.LiveElsewhereTooltip");

    /// <summary>{0} = name, {1} = world, {2} = time of the last save.</summary>
    public static string MultiboxBannerFormat => Loc.Get("Multibox.BannerFormat");

    public static string MultiboxForgetHint => Loc.Get("Multibox.ForgetHint");

    /// <summary>
    /// The dot before a character live in another client: a ringed dot beside the live marker's filled one (◎ is in
    /// the game font's JIS symbols and in Noto Sans CJK, so it draws in every font the plugin uses).
    /// </summary>
    public const string MultiboxMarker = "◎ ";
}
