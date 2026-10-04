using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The status bar while a New Game+ session runs (feature plan v7, 1.19.0, C4; spec-1.19 "C4. New Game+"): the bar's
/// left side, at the bar's own height, becomes the New Game+ icon (000084, 16 px), "New Game+ · Shadowbringers - Part 2
/// · quest 87 of 112" in Text, "Your saved progress is kept" in Secondary and End session, a quiet button that asks
/// first ("Leave New Game+ in the game first. This only stops Tsukimichi's replay mode."). End session never touches the
/// game; it exists for detection that is stuck (<see cref="NewGamePlusSession.End"/>). The line is rebuilt only when the
/// session or the language changes. Also the 1.19.0 wiring of the rows' chips, Tonight's ending-soon cards and the
/// dock's ending-soon notice (C10).
/// </summary>
public sealed partial class MainWindow
{
    /// <summary>The game's New Game+ icon (the main menu's), drawn 16 px.</summary>
    public const uint NewGamePlusIcon = 84;

    private const string NewGamePlusEndPopup = "##ngEnd";

    /// <summary>
    /// 1.19.0 (C4, C10): the Journal rows' trailing chips ("Replaying", "Ends in 2 days") and the Tonight card's
    /// ending-soon cards. Until this is called neither shows.
    /// </summary>
    public void AttachReplayAndEvents(RowChipSource chips, EventWarningSource events)
    {
        ArgumentNullException.ThrowIfNull(chips);
        tablePane.RowChips = chips.For;
        tonightCard.EventWarnings = events ?? throw new ArgumentNullException(nameof(events));
        eventWarnings = events;
    }

    private EventWarningSource? eventWarnings;
    private (int Revision, int Language) eventEndingKey = (-1, -1);
    private string eventEndingText = string.Empty;

    /// <summary>
    /// The ending-soon notice in the dock (1.19.0, C10): once per session, the soonest event's title and what is left,
    /// only while the player turned the ending-soon notice on (Settings › Alerts; off by default). Tonight's card
    /// shows it whatever the setting.
    /// </summary>
    private bool EventEndingDue() =>
        plugin.Settings is { ChatNoticeSeasonalEnding: true, SeasonalWarnDays: > 0 } && eventWarnings is { Current.Count: > 0 };

    private string EventEndingText()
    {
        if (eventWarnings is not { } source || source.Current.Count == 0)
        {
            return string.Empty;
        }

        if (eventEndingKey != (source.Revision, Localization.Loc.Version))
        {
            eventEndingKey = (source.Revision, Localization.Loc.Version);
            var warning = source.Current[0];
            var left = warning.InJournal > 0
                ? warning.InJournal == 1 ? Strings.EventCardJournalOne : string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.EventCardJournalFormat, warning.InJournal)
                : warning.Left == 1 ? Strings.EventCardLeftOne : string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.EventCardLeftFormat, warning.Left);
            eventEndingText = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.EventChatFormat, EventWarningSource.Title(warning), left);
        }

        return eventEndingText;
    }

    /// <summary>Show the event: Tonight's card waits in the no-selection slot.</summary>
    private void DrawEventEndingActions()
    {
        if (ImGui.SmallButton(Strings.EventCardShow + "##dockEventShow"))
        {
            ui.SelectedRowId = null;
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.DockCardShowTooltip);
        }
    }

    private (int Version, int Language, CatalogBundle? Bundle) newGamePlusKey = (-1, -1, null);
    private string newGamePlusLine = string.Empty;
    private string newGamePlusTooltip = string.Empty;

    /// <summary>
    /// Draws the New Game+ segment from <paramref name="x"/> and returns where the next segment starts. What does not
    /// fit before the version ends in an ellipsis: the line first keeps its room, then the kept note, then the button.
    /// </summary>
    private float DrawNewGamePlus(SessionState session, CatalogBundle bundle, ImDrawListPtr dl, float x, float textY, float line, float gap, float versionX)
    {
        var replay = session.NewGamePlus;
        var key = (replay.Version, Localization.Loc.Version, bundle);
        if (key != newGamePlusKey)
        {
            newGamePlusKey = key;
            newGamePlusLine = NewGamePlusText.For(replay, bundle);
            newGamePlusTooltip = replay.Source == NewGamePlusSource.Game ? Strings.NewGamePlusTooltip : Strings.NewGamePlusTooltipReplay;
        }

        var left = x;
        var icon = MathF.Round(UiMetrics.Px(16f));
        var iconMin = new Vector2(x, MathF.Round(textY + ((line - icon) * 0.5f)));
        Orbit.DrawIcon(dl, textures, NodeIcon.Game(NewGamePlusIcon), iconMin, iconMin + new Vector2(icon, icon));
        x += icon + UiMetrics.Px(6f);

        var endLabel = Strings.NewGamePlusEnd;
        var padX = UiMetrics.Px(8f);
        var endWidth = ImGui.CalcTextSize(endLabel).X + (2f * padX);
        var kept = Strings.NewGamePlusKept;
        var keptWidth = ImGui.CalcTextSize(kept).X;
        var lineWidth = ImGui.CalcTextSize(newGamePlusLine).X;
        var room = MathF.Max(0f, versionX - gap - x);
        var lineRoom = MathF.Min(lineWidth, MathF.Max(0f, room - endWidth - (2f * gap)));
        ImGui.SetCursorScreenPos(new Vector2(x, textY));
        Chrome.EllipsisText(newGamePlusLine, lineRoom, Theme.U32(Theme.Surface.Text), lineWidth);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(newGamePlusLine, newGamePlusTooltip);
        }

        x += lineRoom;

        // "Your saved progress is kept", when it fits whole beside the button.
        if (x + (2f * gap) + keptWidth + gap + endWidth <= versionX)
        {
            x = StatusSeparatorAt(dl, x, textY, gap);
            x = StatusText(x, textY, kept, Theme.U32(Theme.Surface.TextSecondary));
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(newGamePlusTooltip);
            }
        }

        if (x + gap + endWidth <= versionX)
        {
            x += gap;
            if (QuietBarButton("##ngEndButton", endLabel, new Vector2(x, textY - UiMetrics.Px(1f)), new Vector2(endWidth, line + UiMetrics.Px(2f))))
            {
                ImGui.OpenPopup(NewGamePlusEndPopup);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.NewGamePlusEndTooltip);
            }

            x += endWidth;
        }

        DrawNewGamePlusEndConfirm(replay);
        return MathF.Max(x, left);
    }

    /// <summary>End session's question, anchored under the button: what it does, then End session or Keep.</summary>
    private static void DrawNewGamePlusEndConfirm(NewGamePlusSession replay)
    {
        using var popup = ImRaii.Popup(NewGamePlusEndPopup);
        if (!popup)
        {
            return;
        }

        using (Typography.Caption())
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + UiMetrics.Px(320f));
            ImGui.TextUnformatted(Strings.NewGamePlusEndBody);
            ImGui.PopTextWrapPos();
        }

        ImGui.Spacing();
        if (ImGui.Button(Strings.NewGamePlusEndConfirm))
        {
            replay.End();
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button(Strings.NewGamePlusEndKeep))
        {
            ImGui.CloseCurrentPopup();
        }
    }

    /// <summary>
    /// A quiet button in the status bar, at the bar's height: the label in Secondary inside a hairline pill, Text and the
    /// hover fill on hover, with the focus ring. True on click.
    /// </summary>
    private static bool QuietBarButton(string id, string label, Vector2 min, Vector2 size)
    {
        ImGui.SetCursorScreenPos(min);
        var clicked = ImGui.InvisibleButton(id, size);
        var hovered = ImGui.IsItemHovered();
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var rounding = size.Y * 0.5f;
        if (hovered)
        {
            dl.AddRectFilled(min, min + size, Theme.U32(s.Hover), rounding);
        }

        dl.AddRect(min, min + size, Theme.U32(Theme.Glyphs.HighContrast ? s.StrongLine : s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        var text = ImGui.CalcTextSize(label);
        dl.AddText(min + ((size - text) * 0.5f), Theme.U32(hovered ? s.Text : s.TextSecondary), label);
        Chrome.FocusRing(rounding);
        return clicked;
    }
}
