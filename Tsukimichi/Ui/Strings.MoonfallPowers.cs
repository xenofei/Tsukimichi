using Tsukimichi.Core.Moonfall;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Moonfall's powers, characters and style shots (feature plan v9 G4, G5). The engine names them by stable ids
/// (<see cref="MoonfallPower"/>, <see cref="MoonfallStyleShot"/>); the names here may still change with the owner's
/// decision 8. English only until localization reopens.
/// </summary>
static partial class Strings
{
    public static string MoonfallPowerName(MoonfallPower power) => power switch
    {
        MoonfallPower.SuperGuide => Loc.Get("MoonfallPower.SuperGuide"),
        MoonfallPower.Multiball => Loc.Get("MoonfallPower.Multiball"),
        MoonfallPower.Wings => Loc.Get("MoonfallPower.Wings"),
        MoonfallPower.Burst => Loc.Get("MoonfallPower.Burst"),
        MoonfallPower.Flippers => Loc.Get("MoonfallPower.Flippers"),
        MoonfallPower.Gate => Loc.Get("MoonfallPower.Gate"),
        MoonfallPower.Bloom => Loc.Get("MoonfallPower.Bloom"),
        MoonfallPower.Draw => Loc.Get("MoonfallPower.Draw"),
        MoonfallPower.Fireball => Loc.Get("MoonfallPower.Fireball"),
        MoonfallPower.Path => Loc.Get("MoonfallPower.Path"),
        MoonfallPower.Bolt => Loc.Get("MoonfallPower.Bolt"),
        _ => Loc.Get("MoonfallPower.None"),
    };

    /// <summary>The character who carries <paramref name="power"/> (docs/design/v9/spec-moonfall.md, decision 1).</summary>
    public static string MoonfallCharacterName(MoonfallPower power) => power switch
    {
        MoonfallPower.SuperGuide => Loc.Get("MoonfallCharacter.SuperGuide"),
        MoonfallPower.Multiball => Loc.Get("MoonfallCharacter.Multiball"),
        MoonfallPower.Wings => Loc.Get("MoonfallCharacter.Wings"),
        MoonfallPower.Burst => Loc.Get("MoonfallCharacter.Burst"),
        MoonfallPower.Flippers => Loc.Get("MoonfallCharacter.Flippers"),
        MoonfallPower.Gate => Loc.Get("MoonfallCharacter.Gate"),
        MoonfallPower.Bloom => Loc.Get("MoonfallCharacter.Bloom"),
        MoonfallPower.Draw => Loc.Get("MoonfallCharacter.Draw"),
        MoonfallPower.Fireball => Loc.Get("MoonfallCharacter.Fireball"),
        MoonfallPower.Path => Loc.Get("MoonfallCharacter.Path"),
        MoonfallPower.Bolt => Loc.Get("MoonfallCharacter.Bolt"),
        _ => Loc.Get("MoonfallPower.None"),
    };

    /// <summary>What the power does, in one line, for the character picker's tooltip.</summary>
    public static string MoonfallPowerHint(MoonfallPower power) => power switch
    {
        MoonfallPower.SuperGuide => Loc.Get("MoonfallPowerHint.SuperGuide"),
        MoonfallPower.Multiball => Loc.Get("MoonfallPowerHint.Multiball"),
        MoonfallPower.Wings => Loc.Get("MoonfallPowerHint.Wings"),
        MoonfallPower.Burst => Loc.Get("MoonfallPowerHint.Burst"),
        MoonfallPower.Flippers => Loc.Get("MoonfallPowerHint.Flippers"),
        MoonfallPower.Gate => Loc.Get("MoonfallPowerHint.Gate"),
        MoonfallPower.Bloom => Loc.Get("MoonfallPowerHint.Bloom"),
        MoonfallPower.Draw => Loc.Get("MoonfallPowerHint.Draw"),
        MoonfallPower.Fireball => Loc.Get("MoonfallPowerHint.Fireball"),
        MoonfallPower.Path => Loc.Get("MoonfallPowerHint.Path"),
        MoonfallPower.Bolt => Loc.Get("MoonfallPowerHint.Bolt"),
        _ => string.Empty,
    };

    public static string MoonfallStyleShotName(MoonfallStyleShot kind) => kind switch
    {
        MoonfallStyleShot.OnePegCatch => Loc.Get("MoonfallStyle.OnePegCatch"),
        MoonfallStyleShot.LongShot => Loc.Get("MoonfallStyle.LongShot"),
        MoonfallStyleShot.SuperLongShot => Loc.Get("MoonfallStyle.SuperLongShot"),
        MoonfallStyleShot.DoubleLongShot => Loc.Get("MoonfallStyle.DoubleLongShot"),
        MoonfallStyleShot.OffTheWall => Loc.Get("MoonfallStyle.OffTheWall"),
        MoonfallStyleShot.RimShot => Loc.Get("MoonfallStyle.RimShot"),
        MoonfallStyleShot.LuckyBounce => Loc.Get("MoonfallStyle.LuckyBounce"),
        MoonfallStyleShot.OrangeSweep => Loc.Get("MoonfallStyle.OrangeSweep"),
        MoonfallStyleShot.LongSlide => Loc.Get("MoonfallStyle.LongSlide"),
        MoonfallStyleShot.ClearNight => Loc.Get("MoonfallStyle.ClearNight"),
        MoonfallStyleShot.LiveWire => Loc.Get("MoonfallStyle.LiveWire"),
        _ => string.Empty,
    };

    /// <summary>{0} = the style shot's name, {1} = its bonus.</summary>
    public static string MoonfallStyleShotFormat => Loc.Get("MoonfallStyleShotFormat");

    public static string MoonfallDrawFreeBall => Loc.Get("MoonfallDrawFreeBall");
    public static string MoonfallDrawTriple => Loc.Get("MoonfallDrawTriple");

    /// <summary>{0} = the power the drum gave.</summary>
    public static string MoonfallDrawPowerFormat => Loc.Get("MoonfallDrawPowerFormat");

    /// <summary>{0} = the character, {1} = their power.</summary>
    public static string MoonfallCharacterFormat => Loc.Get("MoonfallCharacterFormat");

    public static string MoonfallQuickPlay => Loc.Get("MoonfallQuickPlay");
    public static string MoonfallQuickPlayTooltip => Loc.Get("MoonfallQuickPlayTooltip");
    public static string MoonfallPickCharacter => Loc.Get("MoonfallPickCharacter");
    public static string MoonfallPickBeforeShot => Loc.Get("MoonfallPickBeforeShot");
    public static string MoonfallPowerTooltip => Loc.Get("MoonfallPowerTooltip");
    public static string MoonfallFlippersHint => Loc.Get("MoonfallFlippersHint");
}
