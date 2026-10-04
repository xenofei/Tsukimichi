using System;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Umbra;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › About › Umbra (plan v8 M3; spec-1.22 M3 "Settings › About › Umbra", about-history-1.22.png): one line when
/// Umbra isn't installed; otherwise where its toolbar is and that the moon icon, the Todo overlay and Needs you keep clear
/// of it (or that its settings can't be read, with the height assumed instead), Tsukimichi for Umbra with "How to add it"
/// in three steps, and the Follow Umbra palette with a link to Settings › Themes, where it is chosen (decision 17).
/// </summary>
public sealed partial class ConfigWindow
{
    private const string UmbraKeywords = "umbra toolbar bar add-on addon plugin widgets custom keep clear overlap follow palette colours colors";

    /// <summary>Umbra, read-only (M3); set by the plugin. Null hides the block.</summary>
    public UmbraProbe? Umbra { get; set; }

    private bool umbraHowOpen;

    private void DrawUmbraAbout()
    {
        if (Umbra is not { } umbra)
        {
            return;
        }

        Header(Strings.SettingsUmbraHeading);
        if (!umbra.Loaded)
        {
            Note(Strings.UmbraNotInstalled, Strings.UmbraNotInstalledHint, UmbraKeywords);
            return;
        }

        var clearance = UmbraLayout.Clearance;
        var toolbar = umbra.Read?.Toolbar;
        string line;
        if (toolbar is null)
        {
            line = string.Format(CultureInfo.CurrentCulture, Strings.UmbraUnreadFormat, settings.UmbraAssumedBarHeight);
        }
        else if (!toolbar.HoldsEdge)
        {
            line = !toolbar.Enabled ? Strings.UmbraBarHidden : Strings.UmbraBarFloats;
        }
        else
        {
            line = clearance.TopAligned ? Strings.UmbraBarTop : Strings.UmbraBarBottom;
        }

        Note(Strings.UmbraRunning, line, UmbraKeywords);

        // The bar height assumed while Umbra's settings can't be read (the fallback, with a setting).
        if (toolbar is null)
        {
            var height = settings.UmbraAssumedBarHeight;
            if (Setting(Strings.UmbraAssumedLabel, Strings.UmbraAssumedHint, UmbraKeywords + " height offset assumed", sub: true))
            {
                ImGui.SetNextItemWidth(ControlWidth);
                if (ImGui.SliderInt("##umbraAssumed", ref height, 0, 96, Strings.UmbraAssumedFormat))
                {
                    settings.UmbraAssumedBarHeight = Math.Clamp(height, 0, 96);
                    Save();
                    umbra.SettingsChanged();
                }

                EndSetting();
            }
        }

        // Tsukimichi for Umbra: added (with its version) or not, and how to add it.
        var addon = umbra.AddonVersion is { } version
            ? string.Format(CultureInfo.CurrentCulture, Strings.UmbraAddonAddedFormat, version)
            : umbra.AddonPresent ? Strings.UmbraAddonListed : Strings.UmbraAddonMissing;
        var how = umbraHowOpen ? Strings.UmbraHowToAddOpen : Strings.UmbraHowToAdd;
        if (Setting(Strings.UmbraAddonLabel, addon, UmbraKeywords + " how to add repository xenofei", ImGui.CalcTextSize(how).X + (ImGui.GetStyle().FramePadding.X * 2f)))
        {
            if (ImGui.SmallButton(how + "##umbraHow"))
            {
                umbraHowOpen = !umbraHowOpen;
            }

            if (umbraHowOpen)
            {
                SettingBelow();
                using (Typography.Caption())
                using (Theme.PushText(Theme.Surface.TextSecondary))
                {
                    ImGui.TextWrapped(Strings.UmbraHowLead);
                    ImGui.TextWrapped(Strings.UmbraHowStep1);
                    ImGui.TextWrapped(string.Format(CultureInfo.CurrentCulture, Strings.UmbraHowStep2Format, UmbraAddonRepository));
                    ImGui.TextWrapped(Strings.UmbraHowStep3);
                }

                if (ImGui.SmallButton(Strings.UmbraCopyRepository + "##umbraCopy"))
                {
                    ImGui.SetClipboardText(UmbraAddonRepository);
                }
            }

            EndSetting();
        }

        // Follow Umbra lives with the palettes (decision 17); About says whether it is in use and links there.
        var follow = settings.FollowUmbraPalette
            ? umbra.PaletteFellBack ? Strings.FollowUmbraFellBack : Strings.FollowUmbraInUse
            : Strings.FollowUmbraNotInUse;
        if (ButtonRow(Strings.FollowUmbraLabel, follow, Strings.FollowUmbraThemesLink + "##umbraThemes", UmbraKeywords + " themes palette"))
        {
            OpenAt(SettingsSection.Themes);
        }
    }

    /// <summary>The add-on's repository, as Umbra's Plugins settings take it.</summary>
    public const string UmbraAddonRepository = "xenofei/Tsukimichi.Umbra";
}
