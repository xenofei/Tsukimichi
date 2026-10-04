using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Automation › Automation buttons (plan v7, 1.18.0, A10; spec-1.18 §A10, render automation-level.png): four
/// equal level cards in a row (two by two on a narrow page), each with a radio, its name, one sentence and, at its
/// foot, the pills it adds drawn with their real icons. Full hand-offs is a first-class choice: the same size, words and
/// weight as the others, no warning and no extra confirmation. One click applies a level at once, with Undo; buttons
/// above it are hidden everywhere (<see cref="AutomationGate"/>). "Fine-tune each button" opens the per-button toggles
/// as overrides, and the cards read "Custom" (no card chosen, a line under them says so) while those match no level.
/// Under them, "About automation" opens the card (<see cref="AboutAutomationWindow"/>). The layout is static: the
/// Custom line's place is always kept, and the selected card only changes its edge and radio.
/// </summary>
public sealed partial class ConfigWindow
{
    private const string AutomationKeywords = "automation level buttons tracker only travel walking full hand-offs handoff teleport walk questionable autoduty artisan hide";
    private const float LevelGapLogical = 10f;
    private const float LevelPadXLogical = 12f;
    private const float LevelPadYLogical = 10f;
    private const float LevelMinHeightLogical = 150f;
    private const float LevelRoundingLogical = 7f;
    private const float LevelRadioLogical = 7f;
    private const float LevelFourColumnsLogical = 600f;

    private static readonly string LevelStartIcon = FontAwesomeIcon.Play.ToIconString();
    private static readonly string LevelCraftIcon = FontAwesomeIcon.Hammer.ToIconString();

    private bool automationFineTuneOpen;

    /// <summary>Opens About automation; set by the plugin. Null leaves the row out.</summary>
    public Action? OpenAboutAutomation { get; set; }

    /// <summary>The pills a level's card shows at its foot: the buttons that level adds, as they look in the panes.</summary>
    private static (PillIcon Icon, string Label)[] LevelPills(AutomationLevel level) => level switch
    {
        AutomationLevel.TrackerOnly => [(ActionIcons.FlagIcon, Strings.AutomationPillFlag), (ActionIcons.MapIcon, Strings.AutomationPillMap)],
        AutomationLevel.Travel => [(ActionIcons.TeleportIcon, Strings.ActionTeleport), (ActionIcons.AethernetIcon, Strings.AutomationPillAethernet)],
        AutomationLevel.TravelAndWalking => [(ActionIcons.WalkIcon, Strings.TravelWalk), (ActionIcons.GoTo(null), Strings.TravelGoTo)],
        _ => [(LevelStartIcon, Strings.ActionQuestionableStart), (ActionIcons.DutyFinderIcon, Strings.AutoDutyRun), (LevelCraftIcon, Strings.HandInCraftShort)],
    };

    private void DrawAutomationLevel()
    {
        Header(Strings.AutomationButtonsHeading);
        if (BareRow(Strings.AutomationButtonsHeading, Strings.AutomationButtonsHint, AutomationKeywords))
        {
            var avail = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
            using (Typography.Caption())
            {
                TextFlow.Wrapped(Strings.AutomationButtonsHint, avail, Theme.U32(Theme.Surface.TextSecondary));
            }

            ImGui.Dummy(new Vector2(0f, UiMetrics.Px(4f)));
            DrawLevelCards(avail);
            EndBareRow();
        }

        DrawFineTune();

        if (OpenAboutAutomation is { } openAbout && ButtonRow(Strings.AboutAutomationTitle, Strings.AboutAutomationRowHint, Strings.AboutAutomationOpen, "about automation rules user agreement safe ban local network"))
        {
            openAbout();
        }
    }

    /// <summary>The four cards, then the Custom line (its place kept when the buttons match a level).</summary>
    private void DrawLevelCards(float avail)
    {
        var levels = AutomationLevels.All;
        var shown = settings.Automation;
        var chosen = AutomationLevels.LevelOf(shown);
        var gap = UiMetrics.Px(LevelGapLogical);
        var columns = avail >= UiMetrics.Px(LevelFourColumnsLogical) ? levels.Count : 2;
        var rows = (levels.Count + columns - 1) / columns;
        var width = MathF.Max(1f, (avail - (gap * (columns - 1))) / columns);
        var padX = UiMetrics.Px(LevelPadXLogical);
        var padY = UiMetrics.Px(LevelPadYLogical);
        var inner = MathF.Max(1f, width - (2f * padX));
        var line = ImGui.GetTextLineHeight();
        var pillHeight = Chrome.PillHeight(PillLayout.Row);
        var pillGap = UiMetrics.Px(5f);

        // One height for every card: the tallest content, at least the spec's 150 px.
        var height = UiMetrics.Px(LevelMinHeightLogical);
        foreach (var level in levels)
        {
            float body;
            using (Typography.Caption())
            {
                body = TextFlow.Height(Strings.AutomationLevelBody(level), inner);
            }

            var pills = PillRows(LevelPills(level), inner, pillGap) * (pillHeight + pillGap);
            height = MathF.Max(height, padY + line + UiMetrics.Px(6f) + body + UiMetrics.Px(10f) + pills + padY);
        }

        var origin = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        var plain = Theme.Flair == Flair.Plain;
        var rounding = plain ? 0f : UiMetrics.Px(LevelRoundingLogical);
        var s = Theme.Surface;
        for (var i = 0; i < levels.Count; i++)
        {
            var level = levels[i];
            var min = origin + new Vector2((i % columns) * (width + gap), (i / columns) * (height + gap));
            var max = min + new Vector2(width, height);
            using var id = ImRaii.PushId((int)level);
            ImGui.SetCursorScreenPos(min);

            // The card's button first, so it keeps the hover over the pills drawn on it.
            var clicked = ImGui.InvisibleButton("##level", new Vector2(width, height));
            var hovered = ImGui.IsItemHovered();
            var selected = chosen == level;
            dl.AddRectFilled(min, max, Theme.U32(hovered ? s.Hover : s.Raised), rounding);
            dl.AddRect(min, max, Theme.U32(s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
            if (selected)
            {
                // The selected card: a 2 px gilt inside edge (Plain: a 1 px Text edge).
                var edge = plain ? UiMetrics.Hairline : UiMetrics.Px(2f);
                var half = edge * 0.5f;
                dl.AddRect(min + new Vector2(half), max - new Vector2(half), Theme.U32(plain ? s.Text : s.Ornament), rounding, ImDrawFlags.None, edge);
            }

            // The radio, filled Moon when chosen; then the level's name.
            var radius = UiMetrics.Px(LevelRadioLogical);
            var centre = new Vector2(min.X + padX + radius, min.Y + padY + (line * 0.5f));
            dl.AddCircle(centre, radius - 0.75f, Theme.U32(selected ? Theme.Gold : s.TextTertiary), 0, UiMetrics.Px(1.5f));
            if (selected)
            {
                dl.AddCircleFilled(centre, radius * 0.5f, Theme.GoldU32);
            }

            var nameX = centre.X + radius + UiMetrics.Px(8f);
            Chrome.EllipsisTextAt(dl, new Vector2(nameX, min.Y + padY), MathF.Max(1f, max.X - padX - nameX), Strings.AutomationLevelName(level), Theme.U32(s.Text));

            var bodyTop = min.Y + padY + line + UiMetrics.Px(6f);
            using (Typography.Caption())
            {
                TextFlow.DrawClamped(dl, new Vector2(min.X + padX, bodyTop), Strings.AutomationLevelBody(level), inner, 6, Theme.U32(s.TextSecondary));
            }

            DrawLevelPills(LevelPills(level), new Vector2(min.X + padX, max.Y - padY), inner, pillHeight, pillGap);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (clicked)
            {
                ApplyAutomationLevel(level);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(avail, (rows * height) + (MathF.Max(0, rows - 1) * gap)));

        // The Custom line's place is always kept, so nothing moves when a toggle below makes the mix match no level.
        using (Typography.Caption())
        {
            var at = ImGui.GetCursorScreenPos();
            if (chosen is null)
            {
                Chrome.EllipsisTextAt(dl, at, avail, Strings.AutomationCustomNote, Theme.U32(s.TextSecondary));
            }

            ImGui.Dummy(new Vector2(avail, ImGui.GetTextLineHeight()));
        }
    }

    /// <summary>How many rows <paramref name="pills"/> take, wrapped in <paramref name="width"/>.</summary>
    private static int PillRows((PillIcon Icon, string Label)[] pills, float width, float gap)
    {
        var rows = pills.Length > 0 ? 1 : 0;
        var x = 0f;
        foreach (var (icon, label) in pills)
        {
            var w = Chrome.ActionPillWidth(icon, label, PillLayout.Row);
            if (x > 0f && x + w > width)
            {
                rows++;
                x = 0f;
            }

            x += w + gap;
        }

        return rows;
    }

    /// <summary>
    /// The pills at a card's foot, wrapped in <paramref name="width"/> and bottom-aligned at <paramref name="foot"/>. Drawn
    /// after the card's button, which keeps the hover, so they show what the level adds and take no click of their own.
    /// </summary>
    private static void DrawLevelPills((PillIcon Icon, string Label)[] pills, Vector2 foot, float width, float height, float gap)
    {
        var rows = PillRows(pills, width, gap);
        var y = foot.Y - (rows * height) - (MathF.Max(0, rows - 1) * gap);
        var x = 0f;
        for (var i = 0; i < pills.Length; i++)
        {
            var (icon, label) = pills[i];
            var w = Chrome.ActionPillWidth(icon, label, PillLayout.Row);
            if (x > 0f && x + w > width)
            {
                y += height + gap;
                x = 0f;
            }

            ImGui.SetCursorScreenPos(new Vector2(foot.X + x, y));
            using var id = ImRaii.PushId(i);
            Chrome.ActionPill("##levelPill", icon, label, PillTone.Normal, true, size: PillLayout.Row);
            x += w + gap;
        }
    }

    /// <summary>Applies <paramref name="level"/>'s buttons at once, with Undo back to the buttons before.</summary>
    private void ApplyAutomationLevel(AutomationLevel level)
    {
        var before = settings.Automation;
        var after = AutomationLevels.ButtonsOf(level);
        if (before == after)
        {
            return;
        }

        settings.AutomationShown = after;
        Save();
        UndoToast.Show(string.Format(CultureInfo.CurrentCulture, Strings.UndoToastAutomationFormat, Strings.AutomationLevelName(level)), () =>
        {
            settings.AutomationShown = before;
            Save();
        });
    }

    /// <summary>"Fine-tune each button ›": the per-button toggles, open on a click or while the search finds them.</summary>
    private void DrawFineTune()
    {
        if (Row(Strings.AutomationFineTune, null, AutomationKeywords + " fine-tune each button override custom"))
        {
            var start = ImGui.GetCursorScreenPos();
            var width = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
            if (ImGui.Selectable(Strings.AutomationFineTune + "##fineTune", false, ImGuiSelectableFlags.None, new Vector2(width, 0f)))
            {
                automationFineTuneOpen = !automationFineTuneOpen;
            }

            // The chevron at the row's right edge: right while closed, down while open.
            var chevron = (automationFineTuneOpen ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronRight).ToIconString();
            ImGui.PushFont(UiBuilder.IconFont);
            var size = ImGui.CalcTextSize(chevron);
            ImGui.GetWindowDrawList().AddText(new Vector2(start.X + width - size.X, start.Y + ((ImGui.GetTextLineHeight() - size.Y) * 0.5f)), Theme.U32(Theme.Surface.TextTertiary), chevron);
            ImGui.PopFont();
        }

        if (!automationFineTuneOpen && searchText.Length == 0)
        {
            return;
        }

        FineTuneToggle(AutomationButtons.Teleport, Strings.AutomationButtonTeleport, Strings.AutomationButtonTeleportHint, "teleport aethernet lifestream");
        FineTuneToggle(AutomationButtons.Gather, Strings.AutomationButtonGather, Strings.AutomationButtonGatherHint, "gather gatherbuddy fish");
        FineTuneToggle(AutomationButtons.Walk, Strings.ConfigShowWalk, Strings.ConfigShowWalkHint, "travel vnavmesh walk move button");
        FineTuneToggle(AutomationButtons.GoTo, Strings.ConfigShowGoTo, Strings.ConfigShowGoToHint, "travel teleport lifestream aethernet vnavmesh go to giver button");
        FineTuneToggle(AutomationButtons.Questionable, Strings.AutomationButtonQuestionable, Strings.AutomationButtonQuestionableHint, "questionable start send priority");
        FineTuneToggle(AutomationButtons.AutoDuty, Strings.AutomationButtonAutoDuty, Strings.AutomationButtonAutoDutyHint, "autoduty duty support trust");
        FineTuneToggle(AutomationButtons.Artisan, Strings.AutomationButtonArtisan, Strings.AutomationButtonArtisanHint, "artisan craft hand in");
    }

    /// <summary>One fine-tuning toggle: shows or hides <paramref name="button"/> whatever the level says.</summary>
    private void FineTuneToggle(AutomationButtons button, string label, string hint, string keywords)
    {
        var on = AutomationLevels.Shows(settings.Automation, button);
        if (Toggle(label, hint, ref on, keywords + " automation button fine-tune", sub: true))
        {
            settings.AutomationShown = AutomationLevels.With(settings.Automation, button, on);
            Save();
        }
    }
}
