using System.Collections.Generic;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Config;

/// <summary>
/// 1.22.0 moon icon settings (feature plan v8 H1; spec-1.22 H1): shown, its size, locked, where it sits on each screen
/// size, when it steps aside, and whether its "Right-click for options" hint was seen. Drawn under Settings › In game ›
/// Moon icon (<c>Ui/ConfigWindow.MoonIcon.cs</c>); its right-click menu and <c>/tsuki icon</c> change them too.
/// </summary>
public sealed partial class Configuration
{
    /// <summary>The moon icon shows on the HUD (decision 5: on for a fresh install and an update). On by default.</summary>
    public bool MoonIconEnabled { get; set; } = true;

    /// <summary>Small 32, Medium 40 or Large 48 logical px. Medium by default.</summary>
    public MoonIconSize MoonIconSize { get; set; } = MoonIconSize.Medium;

    /// <summary>Locked in place: a drag does nothing and says so. Off by default.</summary>
    public bool MoonIconLocked { get; set; }

    /// <summary>The icon steps aside while a cutscene plays. On by default.</summary>
    public bool MoonIconHideInCutscenes { get; set; } = true;

    /// <summary>The icon steps aside in Group Pose, so it stays out of screenshots. On by default.</summary>
    public bool MoonIconHideInGroupPose { get; set; } = true;

    /// <summary>The icon steps aside while bound by a duty. Off by default.</summary>
    public bool MoonIconHideInDuties { get; set; }

    /// <summary>"Right-click for options" has shown once (decision 5); it never shows again.</summary>
    public bool MoonIconHintSeen { get; set; }

    /// <summary>
    /// Where the player put the icon, per screen size ("1920x1080"), so a new resolution never strands it off screen
    /// (<see cref="MoonIconRules.Resolve"/>). Empty until the icon is first moved.
    /// </summary>
    public Dictionary<string, MoonIconPlace> MoonIconPlaces { get; set; } = [];

    /// <summary>The three hiding options as the icon reads them (a method, so the settings file does not store it twice).</summary>
    public MoonIconHideRules MoonIconHiding() => new(MoonIconHideInCutscenes, MoonIconHideInGroupPose, MoonIconHideInDuties);
}
