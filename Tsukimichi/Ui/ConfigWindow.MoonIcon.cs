using System;
using Tsukimichi.Core.Ui;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › In game › Moon icon (feature plan v8 H1; spec-1.22 H1): show it, its size, lock it, when it steps aside
/// (cutscenes and Group Pose on by default, duties off) and Reset position. The icon reads them every frame.
/// </summary>
public sealed partial class ConfigWindow
{
    private static readonly LocArray MoonIconSizeOptions = new(static () => [Strings.ConfigMoonIconSizeSmall, Strings.ConfigMoonIconSizeMedium, Strings.ConfigMoonIconSizeLarge]);

    /// <summary>Puts the moon icon back where it starts on this screen size; set once the icon exists.</summary>
    public Action? ResetMoonIconPosition { get; set; }

    private void DrawMoonIcon()
    {
        Header(Strings.ConfigSectionMoonIcon);
        var shown = settings.MoonIconEnabled;
        if (Toggle(Strings.ConfigMoonIconShow, Strings.ConfigMoonIconShowHint, ref shown, "moon icon hud button launcher show hide tsuki icon"))
        {
            settings.MoonIconEnabled = shown;
            Save();
        }

        var off = Strings.ConfigMoonIconOffReason;
        var size = Enum.IsDefined(settings.MoonIconSize) ? (int)settings.MoonIconSize : (int)MoonIconSize.Medium;
        if (Choice(Strings.ConfigMoonIconSize, Strings.ConfigMoonIconSizeHint, ref size, MoonIconSizeOptions.Value, "moon icon size small medium large", shown, sub: true, reason: off))
        {
            settings.MoonIconSize = (MoonIconSize)size;
            Save();
        }

        var locked = settings.MoonIconLocked;
        if (Toggle(Strings.ConfigMoonIconLock, Strings.ConfigMoonIconLockHint, ref locked, "moon icon lock move drag position", shown, sub: true, reason: off))
        {
            settings.MoonIconLocked = locked;
            Save();
        }

        var cutscenes = settings.MoonIconHideInCutscenes;
        if (Toggle(Strings.ConfigMoonIconHideCutscenes, Strings.ConfigMoonIconHideCutscenesHint, ref cutscenes, "moon icon hide cutscene", shown, sub: true, reason: off))
        {
            settings.MoonIconHideInCutscenes = cutscenes;
            Save();
        }

        var groupPose = settings.MoonIconHideInGroupPose;
        if (Toggle(Strings.ConfigMoonIconHideGroupPose, Strings.ConfigMoonIconHideGroupPoseHint, ref groupPose, "moon icon hide group pose gpose screenshot", shown, sub: true, reason: off))
        {
            settings.MoonIconHideInGroupPose = groupPose;
            Save();
        }

        var duties = settings.MoonIconHideInDuties;
        if (Toggle(Strings.ConfigMoonIconHideDuties, Strings.ConfigMoonIconHideDutiesHint, ref duties, "moon icon hide duty dungeon trial raid instance", shown, sub: true, reason: off))
        {
            settings.MoonIconHideInDuties = duties;
            Save();
        }

        if (ResetMoonIconPosition is { } reset && ButtonRow(Strings.ConfigMoonIconReset, Strings.ConfigMoonIconResetHint, Strings.SettingsResetButton, "moon icon move position corner reset", shown, sub: true, reason: off))
        {
            reset();
        }
    }
}
