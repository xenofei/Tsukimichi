using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Seasonal;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The Tonight card's 1.19.0 lines (feature plan v7; spec-1.19 C4 and C10), above the Ready count: the New Game+ line
/// while a session runs (the status bar's), and an ending-soon card per event that ends within the warning window
/// (<see cref="EventWarningSource"/>): "All Saints' Wake ends in 2 days", what is left ("1 quest in your journal, 4
/// rewards you don't have."), when ("Ends 3 Nov, 07:59 local · usually October") and Show the event. The 1.18 attention
/// card's copper bar marks it only while an event quest is in the journal, which the game takes away when the event
/// ends; otherwise it is the plain keyline card. Kept apart from the rest of the card.
/// </summary>
public sealed partial class TonightCard
{
    /// <summary>Cards the Tonight card shows at most.</summary>
    public const int MaxEventCards = 2;

    private const int EventRowIdBase = 300;

    /// <summary>The ending-soon warnings; set by the plugin. Null draws none.</summary>
    public EventWarningSource? EventWarnings { get; set; }

    private readonly NewGamePlusText newGamePlusText = new();
    private (int Revision, int Language) eventCardsKey = (-1, -1);
    private EventCardText[] eventCards = [];

    // Each card's height as drawn last frame: its frame goes under the words before they are drawn.
    private float[] eventCardHeights = [];

    private sealed record EventCardText(EndingSoonEvent Warning, string Title, string Why, string Context, QuestRecord? First, uint Icon);

    private void DrawReplayAndEvents(SessionState session, CatalogBundle bundle)
    {
        var line = newGamePlusText.Line(session, bundle);
        if (line.Length > 0)
        {
            using (Theme.PushText(Theme.Surface.Text))
            {
                TextFlow.Wrapped(line, Chrome.RoomX());
            }

            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                TextFlow.Wrapped(Strings.NewGamePlusKept, Chrome.RoomX());
            }

            Chrome.Hairline();
        }

        if (EventWarnings is not { } source)
        {
            return;
        }

        var warnings = source.Current;
        if (eventCardsKey != (source.Revision, Localization.Loc.Version))
        {
            eventCardsKey = (source.Revision, Localization.Loc.Version);
            eventCards = BuildEventCards(warnings, session);
            eventCardHeights = new float[eventCards.Length];
        }

        for (var i = 0; i < eventCards.Length; i++)
        {
            using var id = ImRaii.PushId(EventRowIdBase + i);
            DrawEventCard(eventCards[i], i);
            ImGui.Spacing();
        }
    }

    private static EventCardText[] BuildEventCards(IReadOnlyList<EndingSoonEvent> warnings, SessionState session)
    {
        var count = Math.Min(MaxEventCards, warnings.Count);
        var cards = new EventCardText[count];
        var calendar = SeasonalCalendar.Events(session.Curated.Festivals, DateTime.UtcNow);
        for (var i = 0; i < count; i++)
        {
            var warning = warnings[i];
            var festival = warning.Festival;
            var endLocal = warning.EndUtc.ToLocalTime();
            var title = EventWarningSource.Title(warning);

            var parts = new List<string>(3);
            if (warning.InJournal > 0)
            {
                parts.Add(warning.InJournal == 1 ? Strings.EventCardJournalOne : string.Format(CultureInfo.CurrentCulture, Strings.EventCardJournalFormat, warning.InJournal));
            }

            if (warning.Left > 0)
            {
                parts.Add(warning.Left == 1 ? Strings.EventCardLeftOne : string.Format(CultureInfo.CurrentCulture, Strings.EventCardLeftFormat, warning.Left));
            }

            if (warning.RewardsMissing > 0)
            {
                parts.Add(warning.RewardsMissing == 1 ? Strings.EventCardRewardsOne : string.Format(CultureInfo.CurrentCulture, Strings.EventCardRewardsFormat, warning.RewardsMissing));
            }

            var why = string.Format(CultureInfo.CurrentCulture, Strings.EventCardWhyFormat, string.Join(Strings.EventCardListSeparator, parts));
            var when = string.Format(CultureInfo.CurrentCulture, Strings.EventCardEndsFormat, endLocal.ToString(Strings.EventCardDateFormat, CultureInfo.CurrentCulture), endLocal.ToString(Strings.TimeFormat, CultureInfo.CurrentCulture));
            var source = festival.EndSource == FestivalEndSource.Entered ? Strings.EventCardYouEntered : string.Empty;
            var usual = string.Empty;
            foreach (var line in calendar)
            {
                if (line.Name == festival.Name && line.UsualMonth is { } month)
                {
                    usual = string.Format(CultureInfo.CurrentCulture, Strings.EventCardUsuallyFormat, CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month));
                    break;
                }
            }

            var context = when;
            foreach (var extra in new[] { source, usual })
            {
                if (extra.Length > 0)
                {
                    context += Strings.StateReasonSeparator + extra;
                }
            }

            QuestRecord? first = null;
            uint icon = 0;
            foreach (var quest in festival.Quests)
            {
                icon = icon == 0 ? quest.Quest.IconSpecial : icon;
                if (first is null && quest.IsActionable)
                {
                    first = quest.Quest;
                }

                if (quest.State == QuestState.Accepted && !quest.IsSpareAlternative)
                {
                    first = quest.Quest;
                    break;
                }
            }

            cards[i] = new EventCardText(warning, title, why, context, first, icon);
        }

        return cards;
    }

    /// <summary>
    /// One ending-soon card: the plain keyline card, with the 1.18 copper bar on its left while an event quest is in the
    /// journal. The Tonight card already splits the draw list's channels for its own frame, so this frame is drawn
    /// before the words at the height they took last frame (<see cref="eventCardHeights"/>); the words never move.
    /// </summary>
    private void DrawEventCard(EventCardText card, int index)
    {
        var dl = ImGui.GetWindowDrawList();
        var s = Theme.Surface;
        var min = ImGui.GetCursorScreenPos();
        var width = Chrome.RoomX();
        var pad = UiMetrics.Px(10f);
        var bar = card.Warning.InJournal > 0;
        var left = min.X + pad + (bar ? UiMetrics.Px(6f) : 0f);
        var wrap = MathF.Max(1f, min.X + width - pad - left);
        if (index < eventCardHeights.Length && eventCardHeights[index] > 0f)
        {
            DrawEventCardFrame(dl, min, new Vector2(min.X + width, min.Y + eventCardHeights[index]), bar);
        }

        ImGui.SetCursorScreenPos(new Vector2(left, min.Y + pad));
        var line = ImGui.GetTextLineHeight();
        var titleLeft = left;
        if (card.Icon != 0 && Textures is { } textures)
        {
            var icon = MathF.Round(line);
            Orbit.DrawIcon(dl, textures, NodeIcon.Game(card.Icon), new Vector2(left, min.Y + pad), new Vector2(left + icon, min.Y + pad + icon));
            titleLeft = left + icon + UiMetrics.Px(6f);
        }

        ImGui.SetCursorScreenPos(new Vector2(titleLeft, min.Y + pad));
        Chrome.SemiboldTextWrapped(card.Title, s.Text, MathF.Max(1f, min.X + width - pad - titleLeft));
        ImGui.SetCursorScreenPos(new Vector2(left, ImGui.GetCursorScreenPos().Y));
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + wrap);
        using (Theme.PushText(s.Text))
        {
            ImGui.TextWrapped(card.Why);
        }

        ImGui.SetCursorScreenPos(new Vector2(left, ImGui.GetCursorScreenPos().Y));
        using (Typography.Caption())
        using (Theme.PushText(s.TextSecondary))
        {
            ImGui.TextWrapped(card.Context);
        }

        ImGui.PopTextWrapPos();
        if (card.First is { } first)
        {
            ImGui.SetCursorScreenPos(new Vector2(left, ImGui.GetCursorScreenPos().Y + UiMetrics.Px(4f)));
            if (Chrome.ActionPill("##showEvent", ActionGlyphs.Reveal, Strings.EventCardShow, PillTone.Primary, true, size: PillLayout.Row))
            {
                ui.Reveal(first);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.EventCardShowTooltip);
            }
        }

        var bottom = ImGui.GetCursorScreenPos().Y + pad - ImGui.GetStyle().ItemSpacing.Y;
        var height = MathF.Ceiling(MathF.Max((2f * pad) + line, bottom - min.Y));
        if (index < eventCardHeights.Length)
        {
            if (eventCardHeights[index] <= 0f)
            {
                // The first frame: the words are drawn, the frame is not yet; draw it now, over the empty ground only.
                DrawEventCardFrame(dl, min, new Vector2(min.X + width, min.Y + height), bar, outlineOnly: true);
            }

            eventCardHeights[index] = height;
        }

        ImGui.SetCursorScreenPos(new Vector2(min.X, min.Y + height));
        ImGui.Dummy(new Vector2(width, 0f));
    }

    /// <summary>The card's ground and keyline, and the copper bar beside the words while an event quest is in the journal.</summary>
    private static void DrawEventCardFrame(ImDrawListPtr dl, Vector2 min, Vector2 max, bool bar, bool outlineOnly = false)
    {
        var s = Theme.Surface;
        var rounding = UiMetrics.Px(Theme.Spacing.CardRounding);
        if (!outlineOnly)
        {
            dl.AddRectFilled(min, max, Theme.U32(s.Raised), rounding);
        }

        dl.AddRect(min, max, Theme.U32(Theme.Glyphs.HighContrast ? s.StrongLine : s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        if (bar)
        {
            var inset = UiMetrics.Px(6f);
            var ends = UiMetrics.Px(10f);
            dl.AddRectFilled(new Vector2(min.X + inset, min.Y + ends), new Vector2(min.X + inset + UiMetrics.Px(3f), MathF.Max(min.Y + ends + 1f, max.Y - ends)), Theme.U32(Theme.Copper));
        }
    }
}
