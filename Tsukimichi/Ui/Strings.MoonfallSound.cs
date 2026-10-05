using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>UI strings for Moonfall's sound setting (feature plan v9 G8), on the pause screen. English only until localization reopens.</summary>
static partial class Strings
{
    public static string MoonfallSound => Loc.Get("MoonfallSound");

    /// <summary>The volume at 0.</summary>
    public static string MoonfallSoundOff => Loc.Get("MoonfallSoundOff");

    /// <summary>{0} = the volume in percent.</summary>
    public static string MoonfallSoundPercentFormat => Loc.Get("MoonfallSoundPercentFormat");

    public static string MoonfallSoundQuieter => Loc.Get("MoonfallSoundQuieter");
    public static string MoonfallSoundLouder => Loc.Get("MoonfallSoundLouder");
    public static string MoonfallSoundTooltip => Loc.Get("MoonfallSoundTooltip");
}
