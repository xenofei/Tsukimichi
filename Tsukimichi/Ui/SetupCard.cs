using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;

namespace Tsukimichi.Ui;

/// <summary>
/// One switch of the "Set up your road" card: its label and one-line value, how to read and set it, and whether
/// Recommended turns it on.
/// </summary>
/// <param name="Id">The ImGui id of its checkbox.</param>
/// <param name="Label">The switch's name, in the current language.</param>
/// <param name="Value">One line on what it gives the player.</param>
/// <param name="Get">Reads the setting.</param>
/// <param name="Set">Applies and saves the setting at once (the services that follow it are told).</param>
/// <param name="Recommended">Whether Recommended turns it on; the Todo overlay never is (decision 7).</param>
public sealed record SetupToggle(string Id, Func<string> Label, Func<string> Value, Func<bool> Get, Action<bool> Set, bool Recommended);

/// <summary>
/// "Set up your road" (feature plan v5, 1.7.0, decision 7; onboarding proposal 2): a card at the top of the detail
/// column, shown once on a fresh install after the tour offer is answered (and after the tour, when taken), with the
/// switches that matter while playing, each with a one-line value: the Todo overlay, the chat notice for new quests,
/// the "Opened" line after a turn-in, the Nearby count in the server info bar, item hints and the Duty Finder hint;
/// then a line pointing at the companion
/// plugins. Every switch applies at once. Recommended turns on everything but the overlay, which stays off until the
/// player turns it on. It never shows by itself again (<see cref="Configuration.SetupCardSeen"/>); Help › Quick start
/// opens it again (<see cref="Show"/>).
/// </summary>
public sealed class SetupCard
{
    /// <summary>The card never takes more than this share of the column, and scrolls past it.</summary>
    private const float MaxHeightFraction = 0.6f;
    private const float MaxHeightPx = 460f;
    private const float Pad = 8f;

    private readonly Configuration settings;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;
    private readonly IReadOnlyList<SetupToggle> toggles;
    private readonly Action openCompanions;

    private bool visible;

    /// <param name="settings">Holds <see cref="Configuration.SetupCardSeen"/>.</param>
    /// <param name="pluginInterface">To save the configuration.</param>
    /// <param name="log">For a warning when the save fails.</param>
    /// <param name="toggles">The switches, in the order shown.</param>
    /// <param name="openCompanions">Opens the companion plugins list (Help's Companion plugins topic).</param>
    public SetupCard(Configuration settings, IDalamudPluginInterface pluginInterface, IPluginLog log, IReadOnlyList<SetupToggle> toggles, Action openCompanions)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.toggles = toggles ?? throw new ArgumentNullException(nameof(toggles));
        this.openCompanions = openCompanions ?? throw new ArgumentNullException(nameof(openCompanions));
    }

    /// <summary>True while the card shows.</summary>
    public bool Visible => visible;

    /// <summary>
    /// Shows the card by itself once: on a fresh install (<see cref="Configuration.SetupCardSeen"/> false), and only
    /// while no tour or tour offer is on screen (<paramref name="tourOnScreen"/>), so it comes after the offer and the
    /// tour. Recorded as seen the moment it shows.
    /// </summary>
    public void CheckDue(bool tourOnScreen)
    {
        if (visible || tourOnScreen || settings.SetupCardSeen != false)
        {
            return;
        }

        visible = true;
        settings.SetupCardSeen = true;
        Save();
    }

    /// <summary>Opens the card again (Help › Quick start).</summary>
    public void Show() => visible = true;

    /// <summary>Hides the card.</summary>
    public void Close() => visible = false;

    /// <summary>Draws the card when visible and returns the height it used (0 when hidden), so the caller can shrink the pane below it.</summary>
    public float Draw(float availableHeight)
    {
        if (!visible)
        {
            return 0f;
        }

        var pad = UiMetrics.Px(Pad);
        var height = MathF.Min(availableHeight * MaxHeightFraction, UiMetrics.Px(MaxHeightPx));
        var start = ImGui.GetCursorPosY();
        using (Theme.PushNightPanel())
        using (ImRaii.PushColor(ImGuiCol.Border, Chrome.CardChildBorder))
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(pad, pad)))
        using (var child = ImRaii.Child("##setupCard", new Vector2(0f, height), true))
        {
            if (child)
            {
                // Brass at Full and Quiet, the corner marks at Full (R3 #8).
                using var frame = Chrome.CardFrameInWindow();
                DrawBody();
            }
        }

        ImGui.Spacing();
        return ImGui.GetCursorPosY() - start;
    }

    private void DrawBody()
    {
        // The title, then Recommended and Done at the right end of the line, or under it when the two would run into it.
        Chrome.FitText(Strings.Setup.Title, Theme.U32(Theme.Moon));
        var recommendedWidth = ImGuiHelpers.GetButtonSize(Strings.Setup.Recommended).X;
        var doneWidth = ImGuiHelpers.GetButtonSize(Strings.Setup.Done).X;
        Chrome.SameLineRightOrWrap(recommendedWidth + doneWidth + ImGui.GetStyle().ItemSpacing.X);
        if (ImGui.SmallButton(Strings.Setup.Recommended))
        {
            foreach (var toggle in toggles)
            {
                if (toggle.Recommended && !toggle.Get())
                {
                    toggle.Set(true);
                }
            }
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.Setup.RecommendedTooltip);
        }

        ImGui.SameLine();
        if (ImGui.SmallButton(Strings.Setup.Done))
        {
            Close();
            return;
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.Setup.DoneTooltip);
        }

        var wrap = ImGui.GetWindowContentRegionMax().X;
        using var wrapPos = ImRaii.TextWrapPos(wrap);
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.TextUnformatted(Strings.Setup.Lede);
        }

        ImGui.Spacing();
        var indent = ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X;
        foreach (var toggle in toggles)
        {
            var on = toggle.Get();
            using (ImRaii.PushId(toggle.Id))
            {
                if (ImGui.Checkbox(toggle.Label(), ref on))
                {
                    toggle.Set(on);
                }
            }

            using (ImRaii.PushIndent(indent, false))
            using (Theme.PushText(Theme.Surface.TextTertiary))
            {
                ImGui.TextUnformatted(toggle.Value());
            }
        }

        ImGui.Spacing();
        Chrome.Hairline();
        ImGui.Spacing();
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.TextUnformatted(Strings.Setup.Companions);
        }

        if (ImGui.SmallButton(Strings.Setup.CompanionsButton))
        {
            openCompanions();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.Setup.CompanionsTooltip);
        }
    }

    private void Save()
    {
        try
        {
            settings.Save(pluginInterface);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not save the setup card's state");
        }
    }
}
