using System.Globalization;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Overlay &amp; routes (feature plan v6 U7): the Todo overlay (show, lock in place, compact, background
/// opacity, reset position) and what it shows, each section a toggle. The overlay reads the configuration every frame,
/// so every change shows at once. While the overlay is off its settings stay in place, dimmed, saying why.
/// </summary>
public sealed partial class ConfigWindow
{
    private static readonly LocText UnpinPlanLabel = new(static () => Strings.Unpin + "##todoPlanUnpin");

    // The "Pinned: {expansion}" line, rebuilt when the pinned expansion or the names change.
    private int planPinnedExpansion = int.MinValue;
    private Core.Evaluation.BlockerNames? planPinnedNames;
    private int planPinnedLanguage = -1;
    private string planPinnedLabel = string.Empty;

    private void DrawTodoOverlay()
    {
        Header(Strings.TodoConfigSection);
        var enabled = settings.TodoOverlayEnabled;
        if (Toggle(Strings.TodoConfigEnabled, Strings.TodoConfigEnabledHint, ref enabled, "todo overlay show panel"))
        {
            settings.TodoOverlayEnabled = enabled;
            Save();
        }

        var on = settings.TodoOverlayEnabled;
        var off = Strings.SettingsOverlayOffReason;
        if (ToggleSetting(Strings.TodoConfigLocked, Strings.TodoConfigLockedHint, "overlay lock click-through clicks locked", on, sub: true, reason: off))
        {
            var locked = settings.TodoOverlayLocked;
            if (RowToggle(ref locked))
            {
                settings.TodoOverlayLocked = locked;
                if (!locked)
                {
                    // Unlocked: the upgrade notice has done its job.
                    settings.TodoLockNoticeDue = false;
                }

                Save();
            }

            if (settings.TodoLockNoticeDue && settings.TodoOverlayLocked)
            {
                SettingNote(Strings.TodoLockUpgradeNotice, Theme.Accent);
            }

            EndSetting();
        }

        var compact = settings.TodoOverlayCompact;
        if (Toggle(Strings.TodoConfigCompact, Strings.TodoConfigCompactHint, ref compact, "overlay compact small", on, sub: true, reason: off))
        {
            settings.TodoOverlayCompact = compact;
            Save();
        }

        if (Setting(Strings.TodoConfigOpacity, Strings.TodoConfigOpacityHint, "overlay opacity transparency alpha background", enabled: on, sub: true, reason: off))
        {
            var opacity = TodoOverlay.ClampOpacity(settings.TodoOverlayOpacity);
            ImGui.SetNextItemWidth(ControlWidth);
            if (ImGui.SliderFloat("##opacity", ref opacity, TodoOverlay.MinOpacity, TodoOverlay.MaxOpacity, "%.2f", ImGuiSliderFlags.AlwaysClamp))
            {
                settings.TodoOverlayOpacity = opacity;
                SaveSoon();
            }

            EndSetting();
        }

        if (ResetTodoPosition is { } reset && ButtonRow(Strings.TodoConfigResetPosition, Strings.TodoConfigResetPositionHint, Strings.SettingsResetButton, "overlay move position corner", on, sub: true, reason: off))
        {
            reset();
        }
    }

    /// <summary>Settings › Overlay &amp; routes › Show in overlay: one toggle per section of the overlay, dimmed while it is off.</summary>
    private void DrawTodoSections()
    {
        Header(Strings.SettingsShowInOverlayHeading);
        var on = settings.TodoOverlayEnabled;
        var off = Strings.SettingsOverlayOffReason;

        var pins = settings.TodoShowPins;
        if (Toggle(Strings.TodoConfigShowPins, Strings.TodoConfigShowPinsHint, ref pins, "overlay section pinned pins", on, reason: off))
        {
            settings.TodoShowPins = pins;
            Save();
        }

        DrawTodoRouteToggles(on);

        var seasonal = settings.TodoShowSeasonal;
        if (Toggle(Strings.TodoConfigShowSeasonal, Strings.TodoConfigShowSeasonalHint, ref seasonal, "overlay section seasonal event festival", on, reason: off))
        {
            settings.TodoShowSeasonal = seasonal;
            Save();
        }

        DrawTodoPlanToggle(on);

        var nearby = settings.TodoShowNearbyFeature;
        if (Toggle(Strings.TodoConfigShowNearby, Strings.TodoConfigShowNearbyHint, ref nearby, "overlay section nearby feature unlock zone", on, reason: off))
        {
            settings.TodoShowNearbyFeature = nearby;
            Save();
        }

        var msq = settings.TodoShowMsq;
        if (Toggle(Strings.TodoConfigShowMsq, Strings.TodoConfigShowMsqHint, ref msq, "overlay section main scenario msq story", on, reason: off))
        {
            settings.TodoShowMsq = msq;
            Save();
        }

        var jobs = settings.TodoShowJobQuests;
        if (Toggle(Strings.TodoConfigShowJobQuests, Strings.TodoConfigShowJobQuestsHint, ref jobs, "overlay section job class role quests", on, reason: off))
        {
            settings.TodoShowJobQuests = jobs;
            Save();
        }
    }

    /// <summary>
    /// The overlay's "Clear my blues" section (P3): its toggle, which expansion is pinned and Unpin; dimmed while the
    /// overlay is off (<paramref name="overlayOn"/>).
    /// </summary>
    private void DrawTodoPlanToggle(bool overlayOn)
    {
        if (!ToggleSetting(Strings.PlanTodoConfig, Strings.PlanTodoConfigHint, "overlay my blues plan expansion pinned", overlayOn, reason: Strings.SettingsOverlayOffReason))
        {
            return;
        }

        var plan = settings.TodoShowPlan;
        if (RowToggle(ref plan))
        {
            settings.TodoShowPlan = plan;
            Save();
        }

        var expansion = settings.TodoPlanExpansion;
        var pinned = expansion is >= 0 and <= byte.MaxValue;
        if (expansion != planPinnedExpansion || !ReferenceEquals(session.Names, planPinnedNames) || planPinnedLanguage != Loc.Version)
        {
            planPinnedExpansion = expansion;
            planPinnedLanguage = Loc.Version;
            planPinnedNames = session.Names;
            planPinnedLabel = pinned
                ? string.Format(CultureInfo.CurrentCulture, Strings.PlanTodoConfigPinnedFormat, session.Names.Expansion((byte)expansion))
                : Strings.PlanTodoConfigNone;
        }

        SettingNote(planPinnedLabel);
        if (pinned)
        {
            if (ImGui.SmallButton(UnpinPlanLabel.Value))
            {
                settings.TodoPlanExpansion = -1;
                Save();
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(Strings.PlanUnpinTooltip);
            }
        }

        EndSetting();
    }
}
