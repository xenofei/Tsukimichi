using System;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Umbra;
using Tsukimichi.Game;
using Tsukimichi.Localization;

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

    // The lines with a value in them, built when the value or the language changes; the labels once per language.
    private (int Height, int Language) umbraUnreadKey = (-1, -1);
    private string umbraUnreadLine = string.Empty;
    private (string? Version, bool Present, int Language) umbraAddonKey = (null, false, -1);
    private string umbraAddonLine = string.Empty;
    private readonly LocText umbraHowLabel = new(static () => Strings.UmbraHowToAdd + "##umbraHow");
    private readonly LocText umbraHowOpenLabel = new(static () => Strings.UmbraHowToAddOpen + "##umbraHow");
    private readonly LocText umbraHowStep2 = new(static () => string.Format(CultureInfo.CurrentCulture, Strings.UmbraHowStep2Format, UmbraAddonRepository));
    private readonly LocText umbraThemesLink = new(static () => Strings.FollowUmbraThemesLink + "##umbraThemes");
    private readonly LocText umbraCopyLabel = new(static () => Strings.UmbraCopyRepository + "##umbraCopy");

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
            line = UmbraUnreadLine(settings.UmbraAssumedBarHeight);
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

        // The bar height assumed while Umbra's settings can't be read (the fallback, with a setting). A drag moves the
        // clearance live and saves once, when the slider is let go.
        if (toolbar is null)
        {
            var height = settings.UmbraAssumedBarHeight;
            if (Setting(Strings.UmbraAssumedLabel, Strings.UmbraAssumedHint, UmbraKeywords + " height offset assumed", sub: true))
            {
                ImGui.SetNextItemWidth(ControlWidth);
                if (ImGui.SliderInt("##umbraAssumed", ref height, 0, 96, Strings.UmbraAssumedFormat))
                {
                    settings.UmbraAssumedBarHeight = Math.Clamp(height, 0, 96);
                    umbra.SettingsChanged();
                }

                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    Save();
                }

                EndSetting();
            }
        }

        // Tsukimichi for Umbra: added (with its version) or not, and how to add it.
        var addon = UmbraAddonLine(umbra.AddonVersion, umbra.AddonPresent);
        var how = umbraHowOpen ? Strings.UmbraHowToAddOpen : Strings.UmbraHowToAdd;
        if (Setting(Strings.UmbraAddonLabel, addon, UmbraKeywords + " how to add repository xenofei", ImGui.CalcTextSize(how).X + (ImGui.GetStyle().FramePadding.X * 2f)))
        {
            if (ImGui.SmallButton(umbraHowOpen ? umbraHowOpenLabel.Value : umbraHowLabel.Value))
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
                    ImGui.TextWrapped(umbraHowStep2.Value);
                    ImGui.TextWrapped(Strings.UmbraHowStep3);
                }

                if (ImGui.SmallButton(umbraCopyLabel.Value))
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
        if (ButtonRow(Strings.FollowUmbraLabel, follow, umbraThemesLink.Value, UmbraKeywords + " themes palette"))
        {
            OpenAt(SettingsSection.Themes);
        }
    }

    /// <summary>"Umbra's settings can't be read · assuming a 32 px bar", rebuilt when the height or the language changes.</summary>
    private string UmbraUnreadLine(int height)
    {
        var key = (height, Loc.Version);
        if (key != umbraUnreadKey)
        {
            umbraUnreadKey = key;
            umbraUnreadLine = string.Format(CultureInfo.CurrentCulture, Strings.UmbraUnreadFormat, height);
        }

        return umbraUnreadLine;
    }

    /// <summary>Tsukimichi for Umbra added (with its version), listed, or missing; rebuilt when that or the language changes.</summary>
    private string UmbraAddonLine(string? version, bool present)
    {
        var key = (version, present, Loc.Version);
        if (key != umbraAddonKey || umbraAddonLine.Length == 0)
        {
            umbraAddonKey = key;
            umbraAddonLine = version is not null
                ? string.Format(CultureInfo.CurrentCulture, Strings.UmbraAddonAddedFormat, version)
                : present ? Strings.UmbraAddonListed : Strings.UmbraAddonMissing;
        }

        return umbraAddonLine;
    }

    /// <summary>The add-on's repository, as Umbra's Plugins settings take it.</summary>
    public const string UmbraAddonRepository = "xenofei/Tsukimichi.Umbra";
}
