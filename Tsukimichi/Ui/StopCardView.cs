using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Draws the "Why it stopped" card (feature plan v7, 1.18.0, A2; docs/design/v7/ui/spec-1.18.md, stop-card.png and
/// stop-card-looks.png): one design for all eight reasons, at each Decoration level and on every palette.
/// <list type="bullet">
/// <item>Full: the kit's card (brass frame, corner marks) on an opaque ground, padding 11 × 18; the hand-off's game icon
/// at 18 px, the title in the Title face, "2 min ago" in tertiary and ×.</item>
/// <item>Quiet: the tonal card; the title semibold in the body face.</item>
/// <item>Plain: a 22 px band header over a flat sheet with a 1 px line; the icon at 14 px.</item>
/// </list>
/// The one colour cue is a bar on the left (3 px inset 6, 12 px from top and foot; 2 px full height at Plain): gold
/// finished, silver you stopped it, copper it needs you. It is never the only carrier: the title says the same in words,
/// and <see cref="StopDock.Raise"/> refuses a card without one. Then the reason in plain words, the context line, the
/// receipt on a Finished card, up to two safe fixes as pills (the first the primary, unless Copy report is), Stop all
/// while a hand-off still runs (so a run whose own button the automation level hides is always stoppable), and Copy
/// report right-aligned. A disabled fix keeps its label and says why under the row. Drawn by the notice dock
/// (<see cref="MainWindow"/>) and, in its title's line, by the Todo overlay (<see cref="DrawInline"/>). The fixes' state
/// is asked at most every half second, and their tooltips only on hover.
/// </summary>
internal static class StopCardView
{
    private const float PadXLogical = 18f;
    private const float PadYLogical = 11f;
    private const float PlainPadXLogical = 10f;
    private const float PlainPadYLogical = 8f;
    private const float PlainBandLogical = 22f;
    private const float IconLogical = 18f;
    private const float PlainIconLogical = 14f;
    private const float BarLogical = 3f;
    private const float PlainBarLogical = 2f;
    private const float BarInsetLogical = 6f;
    private const float BarEndsLogical = 12f;
    private const float GapLogical = 8f;
    private const float RowGapLogical = 6f;

    private static readonly string CloseIcon = FontAwesomeIcon.Times.ToIconString();
    private static readonly string CopyIcon = FontAwesomeIcon.Copy.ToIconString();
    private static readonly string StopIcon = FontAwesomeIcon.Stop.ToIconString();

    private static int agoMinute = -1;
    private static int agoLanguage = -1;
    private static string agoText = string.Empty;
    private static StopCard? waitingCard;
    private static int waitingLanguage = -1;
    private static string waitingText = string.Empty;

    // The card's two fixes, whether each shows and why it cannot act, asked at most every half second per card (a
    // blocker may compose a list of missing plugins, and a retry's check builds its plan); a click asks again at once.
    private static readonly FixState[] FixStates = new FixState[2];
    private static StopCard? fixesCard;
    private static int fixesLanguage = -1;
    private static double fixesAt = double.NegativeInfinity;

    /// <summary>
    /// What every "Stop all" (the card, the "Needs you" panel, the Todo overlay's title line) runs: every running
    /// hand-off, as <c>/tsuki stop</c>; null leaves the button out. The one place to point them all somewhere else.
    /// </summary>
    public static Action? StopAll(RunStops stops) => stops.StopAll;

    /// <summary>The cue's ink: gold finished well, silver you did it, copper it needs you.</summary>
    public static Vector4 CueColor(StopCue cue) => cue switch
    {
        StopCue.Gold => Theme.Palette.Inks.GoldLine,
        StopCue.Silver => Theme.Surface.TextSecondary,
        _ => Theme.Copper,
    };

    /// <summary>
    /// The card in the current window, filling <paramref name="width"/> from the cursor. The words and buttons are drawn
    /// first and the frame under them after, to the height they took this frame, so the frame always fits what it holds
    /// (a longer "2 min ago", "Report copied", Stop all coming or going). Returns that height. Buttons act only while
    /// <paramref name="interactive"/>; <paramref name="host"/> is where Questionable's confirmation opens;
    /// <paramref name="note"/> takes "Report copied" for the status bar.
    /// </summary>
    public static float Draw(RunStops stops, StopCard card, float width, bool interactive, string host, Action<string>? note)
    {
        var flair = Theme.Flair;
        var plain = flair == Flair.Plain;
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        var max = new Vector2(min.X + width, min.Y);
        var s = Theme.Surface;
        var rounding = UiMetrics.Px(Theme.Spacing.CardRounding);

        // The frame goes on channel 0 once the content (channel 1) has its height.
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);

        var padX = UiMetrics.Px(plain ? PlainPadXLogical : PadXLogical);
        var padY = UiMetrics.Px(plain ? PlainPadYLogical : PadYLogical);
        var left = min.X + padX;
        var right = max.X - padX;
        var y = min.Y + padY;

        // The header: icon, title, "2 min ago", ×.
        var side = ImGui.GetFrameHeight();
        var closeMin = new Vector2(right - side + UiMetrics.Px(4f), plain ? min.Y + MathF.Round((UiMetrics.Px(PlainBandLogical) - side) * 0.5f) : y - UiMetrics.Px(2f));
        var ago = Ago(stops);
        float agoWidth;
        using (Typography.Caption())
        {
            agoWidth = ImGui.CalcTextSize(ago).X;
        }

        var titleRight = closeMin.X - UiMetrics.Px(GapLogical) - agoWidth - UiMetrics.Px(GapLogical);
        var icon = UiMetrics.Px(plain ? PlainIconLogical : IconLogical);
        float headerBottom;
        if (plain)
        {
            var band = UiMetrics.Px(PlainBandLogical);
            dl.AddRectFilled(min, new Vector2(max.X, min.Y + band), Theme.U32(Theme.Tones.Band), rounding, ImDrawFlags.RoundCornersTop);
            dl.AddRectFilled(new Vector2(min.X, min.Y + band), new Vector2(max.X, min.Y + band + 1f), Theme.U32(Theme.Tones.Rule));
            var line = ImGui.GetTextLineHeight();
            Chrome.DrawPillIcon(dl, stops.Icon(card), new Vector2(left, MathF.Round(min.Y + ((band - icon) * 0.5f))), icon, Theme.U32(s.Text), enabled: true);
            ImGui.SetCursorScreenPos(new Vector2(left + icon + UiMetrics.Px(6f), MathF.Round(min.Y + ((band - line) * 0.5f))));
            ImGui.PushClipRect(min, new Vector2(titleRight, min.Y + band), true);
            Chrome.SemiboldText(card.Title, s.Text);
            ImGui.PopClipRect();
            DrawAgo(dl, ago, new Vector2(closeMin.X - UiMetrics.Px(GapLogical) - agoWidth, min.Y + ((band - CaptionLine()) * 0.5f)));
            headerBottom = min.Y + band + 1f + padY;
        }
        else
        {
            ImGui.SetCursorScreenPos(new Vector2(left + icon + UiMetrics.Px(GapLogical), y));
            var titleTop = y;
            if (flair == Flair.Full)
            {
                using (Typography.Title(card.Title))
                {
                    ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + MathF.Max(1f, titleRight - ImGui.GetCursorScreenPos().X));
                    using (Theme.PushText(s.Text))
                    {
                        ImGui.TextWrapped(card.Title);
                    }

                    ImGui.PopTextWrapPos();
                }
            }
            else
            {
                Chrome.SemiboldTextWrapped(card.Title, s.Text, MathF.Max(1f, titleRight - ImGui.GetCursorScreenPos().X));
            }

            var titleBottom = ImGui.GetItemRectMax().Y;
            var firstLine = MathF.Min(titleBottom - titleTop, ImGui.GetTextLineHeight() * 1.6f);
            Chrome.DrawPillIcon(dl, stops.Icon(card), new Vector2(left, MathF.Round(titleTop + ((firstLine - icon) * 0.5f))), icon, Theme.U32(s.Text), enabled: true);
            DrawAgo(dl, ago, new Vector2(closeMin.X - UiMetrics.Px(GapLogical) - agoWidth, titleTop + MathF.Max(0f, (firstLine - CaptionLine()) * 0.5f)));
            headerBottom = titleBottom + UiMetrics.Px(4f);
        }

        ImGui.SetCursorScreenPos(closeMin);
        if (CloseButton(side, interactive))
        {
            stops.Dismiss();
        }

        // The reason, the context, the receipt and, while it waits, Keep going after it's line.
        var wrap = MathF.Max(1f, right - left);
        ImGui.SetCursorScreenPos(new Vector2(left, headerBottom));
        BeginWrapped(left, wrap);
        using (Theme.PushText(s.Text))
        {
            ImGui.TextWrapped(card.Why);
        }

        using (Typography.Caption())
        using (Theme.PushText(s.TextSecondary))
        {
            if (card.Context.Length > 0)
            {
                ImGui.TextWrapped(card.Context);
            }

            if (card.Receipt.Length > 0)
            {
                ImGui.TextWrapped(card.Receipt);
            }

            if (stops.WaitingForDuty && card.Reason == StopReason.DutyGuard)
            {
                ImGui.TextWrapped(WaitingLine(card));
            }
        }

        EndWrapped();

        // The actions: the fixes, Stop all while a hand-off runs, and Copy report at the right end.
        var actionsTop = ImGui.GetCursorScreenPos().Y + UiMetrics.Px(RowGapLogical);
        var bottom = DrawActions(stops, card, left, right, actionsTop, interactive, host, note);
        var height = MathF.Ceiling(bottom + padY - min.Y);
        max.Y = min.Y + MathF.Max(1f, height);

        // The ground is opaque: the card floats over the panes.
        dl.ChannelsSetCurrent(0);
        dl.AddRectFilled(min, max, Theme.U32(s.Window with { W = 1f }), rounding);
        if (plain)
        {
            dl.AddRect(min, max, Theme.U32(Theme.Glyphs.HighContrast ? s.StrongLine : s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        }
        else
        {
            if (FlairRules.Card(flair) == CardFrame.Tonal)
            {
                dl.AddRectFilled(min, max, Theme.U32(Theme.Tones.Card with { W = 1f }), rounding);
            }

            Chrome.CardSurface(dl, min, max);
        }

        // The cue: one bar, beside the words above.
        var cue = Theme.U32(CueColor(card.Cue));
        if (plain)
        {
            dl.AddRectFilled(min, new Vector2(min.X + UiMetrics.Px(PlainBarLogical), max.Y), cue);
        }
        else
        {
            var inset = UiMetrics.Px(BarInsetLogical);
            var ends = UiMetrics.Px(BarEndsLogical);
            dl.AddRectFilled(new Vector2(min.X + inset, min.Y + ends), new Vector2(min.X + inset + UiMetrics.Px(BarLogical), MathF.Max(min.Y + ends + 1f, max.Y - ends)), cue);
        }

        dl.ChannelsMerge();
        return height;
    }

    /// <summary>
    /// The Todo overlay's form, in the panel's title line between <paramref name="left"/> and <paramref name="right"/>
    /// (a row of <paramref name="rowHeight"/> from <paramref name="top"/>, its text at <paramref name="textY"/>), so the
    /// quest rows never move when a card comes or goes: the cue dot, the title outlined and cut to the room (the title
    /// and the reason in full on hover) and, with <paramref name="buttons"/>, the first fix as an icon pill and × at the
    /// right. Returns whether the pointer is on it.
    /// </summary>
    public static bool DrawInline(RunStops stops, StopCard card, float left, float right, float top, float rowHeight, float textY, bool buttons, string host)
    {
        var dl = ImGui.GetWindowDrawList();
        var line = ImGui.GetTextLineHeight();
        var gap = UiMetrics.Px(6f);
        var dot = UiMetrics.Px(7f);
        var interactive = stops.Dock.Interactive(stops.Now, UiMetrics.ReduceMotion);
        var x = right;
        if (buttons && right - line - gap - dot >= left)
        {
            x -= line;
            ImGui.SetCursorScreenPos(new Vector2(x, textY));
            if (CloseButton(line, interactive))
            {
                stops.Dismiss();
            }

            var first = Fixes(stops, card)[0];
            if (first.Shows)
            {
                var icon = stops.FixIcon(card, first.Fix);
                var width = Chrome.ActionPillWidth(icon, null, PillLayout.Row);
                if (x - gap - width - dot - gap >= left)
                {
                    x -= gap + width;
                    ImGui.SetCursorScreenPos(new Vector2(x, MathF.Round(top + ((rowHeight - Chrome.PillHeight(PillLayout.Row)) * 0.5f))));
                    var enabled = first.Blocker is null && !(first.Fix == StopFix.ReloadAndRetry && stops.Retrying);
                    if (Chrome.ActionPill("##stopRowFix", icon, null, PillTone.Normal, enabled, null, PillLayout.Row) && interactive && enabled)
                    {
                        RunFix(stops, card, first.Fix, host);
                    }

                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    {
                        UiMetrics.Tooltip(stops.Label(first.Fix), first.Blocker ?? stops.Tooltip(card, first.Fix));
                    }
                }
            }

            x -= gap;
        }

        dl.AddCircleFilled(new Vector2(left + (dot * 0.5f), textY + (line * 0.5f)), dot * 0.5f, Theme.U32(CueColor(card.Cue)));
        var textMin = new Vector2(left + dot + gap, textY);
        var room = x - textMin.X;
        if (room >= UiMetrics.Px(24f))
        {
            Chrome.OutlinedEllipsisAt(dl, textMin, room, card.Title, Theme.U32(Theme.Surface.Text));
        }

        var hovered = ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(new Vector2(left, top), new Vector2(right, top + rowHeight));
        if (hovered && ImGui.IsMouseHoveringRect(new Vector2(left, top), new Vector2(x, top + rowHeight)))
        {
            UiMetrics.Tooltip(card.Title, card.Why);
        }

        return hovered;
    }

    /// <summary>One fix of the card: whether it shows and why it cannot act now (null when it can).</summary>
    private readonly record struct FixState(StopFix Fix, bool Shows, string? Blocker);

    /// <summary>The card's first and second fix, asked again at most every half second (and at once for another card or language).</summary>
    private static ReadOnlySpan<FixState> Fixes(RunStops stops, StopCard card)
    {
        var now = stops.Now;
        if (!ReferenceEquals(card, fixesCard) || fixesLanguage != Localization.Loc.Version || now - fixesAt >= 0.5 || now < fixesAt)
        {
            fixesCard = card;
            fixesLanguage = Localization.Loc.Version;
            fixesAt = now;
            var (first, second) = RunStopClassifier.Fixes(card.Reason);
            FixStates[0] = State(stops, card, first);
            FixStates[1] = State(stops, card, second);
        }

        return FixStates;

        static FixState State(RunStops stops, StopCard card, StopFix fix) =>
            fix != StopFix.None && stops.Shows(card, fix) ? new FixState(fix, true, stops.Blocker(card, fix)) : new FixState(fix, false, null);
    }

    /// <summary>Runs a fix and asks the fixes again next frame (the fix may have changed what they can do).</summary>
    private static void RunFix(RunStops stops, StopCard card, StopFix fix, string host)
    {
        stops.Fix(card, fix, host);
        fixesAt = double.NegativeInfinity;
    }

    /// <summary>The fixes, Stop all and Copy report, flowing onto a second row when they do not fit; returns the bottom.</summary>
    private static float DrawActions(RunStops stops, StopCard card, float left, float right, float top, bool interactive, string host, Action<string>? note)
    {
        var fixes = Fixes(stops, card);
        var reportPrimary = RunStopClassifier.ReportIsPrimary(card.Reason);
        var height = Chrome.PillHeight(PillLayout.Panel);
        var gap = UiMetrics.Px(GapLogical);
        var x = left;
        var y = top;
        string? reason = null;

        void Place(float width)
        {
            if (x > left && x + width > right)
            {
                x = left;
                y += height + UiMetrics.Px(RowGapLogical);
            }

            ImGui.SetCursorScreenPos(new Vector2(x, y));
            x += width + gap;
        }

        var primaryTaken = reportPrimary;
        foreach (var state in fixes)
        {
            if (!state.Shows)
            {
                continue;
            }

            var fix = state.Fix;
            var label = stops.Label(fix);
            var icon = stops.FixIcon(card, fix);
            var blocker = state.Blocker;
            var busy = (fix == StopFix.ReloadAndRetry && stops.Retrying) || (fix == StopFix.KeepGoingAfterDuty && stops.WaitingForDuty);
            var tone = !primaryTaken && !busy ? PillTone.Primary : PillTone.Normal;
            primaryTaken = true;
            Place(Chrome.ActionPillWidth(icon, label, PillLayout.Panel));
            var enabled = blocker is null && !(fix == StopFix.ReloadAndRetry && stops.Retrying);
            ImGui.PushID((int)fix);
            if (Chrome.ActionPill("##stopFix", icon, label, tone, enabled, null, PillLayout.Panel) && interactive)
            {
                RunFix(stops, card, fix, host);
            }

            // Composed only on hover: the tooltip may name a quest.
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && (enabled ? stops.Tooltip(card, fix) : blocker) is { Length: > 0 } tooltip)
            {
                UiMetrics.Tooltip(tooltip);
            }

            ImGui.PopID();
            if (fix == StopFix.TryAgain)
            {
                // Its line is kept while it is enabled too, so the card does not change height when the player gets up.
                reason = blocker ?? string.Empty;
            }
        }

        if (stops.AnyRunning && StopAll(stops) is { } stopAll)
        {
            Place(Chrome.ActionPillWidth(StopIcon, Strings.NeedsYouStopAll, PillLayout.Panel));
            if (Chrome.ActionPill("##stopAll", StopIcon, Strings.NeedsYouStopAll, PillTone.Danger, true, Strings.NeedsYouStopAllTooltip, PillLayout.Panel) && interactive)
            {
                stopAll();
            }
        }

        // Copy report: a quiet text action at the right end, or a pill when it is the card's primary (an error).
        var label2 = stops.JustCopied ? Strings.StopReportCopied : Strings.StopCopyReport;
        bool copy;
        if (reportPrimary)
        {
            var width = Chrome.ActionPillWidth(CopyIcon, label2, PillLayout.Panel);
            PlaceRight(ref x, ref y, left, right, width, height, gap);
            copy = Chrome.ActionPill("##stopCopy", CopyIcon, label2, PillTone.Primary, true, Strings.StopCopyReportTooltip, PillLayout.Panel);
        }
        else
        {
            var width = QuietActionWidth(label2);
            PlaceRight(ref x, ref y, left, right, width, height, gap);
            copy = QuietAction("##stopCopy", label2, width, height, Strings.StopCopyReportTooltip);
        }

        if (copy && interactive)
        {
            ImGui.SetClipboardText(stops.Report(card));
            note?.Invoke(Strings.StopReportCopied);
        }

        var bottom = y + height;
        if (reason is not null)
        {
            ImGui.SetCursorScreenPos(new Vector2(left, bottom + UiMetrics.Px(3f)));
            using (Typography.Caption())
            using (Theme.PushText(GamePanelShell.QuietTone))
            {
                if (reason.Length > 0)
                {
                    ImGui.TextUnformatted(reason);
                }
                else
                {
                    ImGui.Dummy(new Vector2(1f, ImGui.GetTextLineHeight()));
                }
            }

            bottom = ImGui.GetItemRectMax().Y;
        }

        return bottom;
    }

    /// <summary>Puts the next item at the right end of the row (a new row when it does not fit after what is there).</summary>
    private static void PlaceRight(ref float x, ref float y, float left, float right, float width, float height, float gap)
    {
        if (x > left && x + width > right)
        {
            y += height + UiMetrics.Px(RowGapLogical);
        }

        ImGui.SetCursorScreenPos(new Vector2(MathF.Max(left, right - width), y));
        x = right + gap;
    }

    private static float QuietActionWidth(string label)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        var icon = ImGui.CalcTextSize(CopyIcon).X;
        ImGui.PopFont();
        return icon + UiMetrics.Px(6f) + ImGui.CalcTextSize(label).X + UiMetrics.Px(8f);
    }

    /// <summary>A quiet text action: the copy glyph and the label in the secondary tone, Text on hover, with the focus ring.</summary>
    private static bool QuietAction(string id, string label, float width, float height, string tooltip)
    {
        var clicked = ImGui.InvisibleButton(id, new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();
        var min = ImGui.GetItemRectMin();
        var dl = ImGui.GetWindowDrawList();
        var ink = Theme.U32(hovered ? Theme.Surface.Text : Theme.Surface.TextSecondary);
        ImGui.PushFont(UiBuilder.IconFont);
        var iconSize = ImGui.CalcTextSize(CopyIcon);
        dl.AddText(min + new Vector2(UiMetrics.Px(4f), (height - iconSize.Y) * 0.5f), ink, CopyIcon);
        ImGui.PopFont();
        var textY = (height - ImGui.GetTextLineHeight()) * 0.5f;
        dl.AddText(min + new Vector2(UiMetrics.Px(4f) + iconSize.X + UiMetrics.Px(6f), textY), ink, label);
        Chrome.FocusRing(UiMetrics.Px(4f));
        if (hovered)
        {
            UiMetrics.Tooltip(tooltip);
        }

        return clicked;
    }

    /// <summary>The × of the card or the row; true on click while it acts.</summary>
    private static bool CloseButton(float side, bool interactive)
    {
        var clicked = ImGui.InvisibleButton("##stopClose", new Vector2(side));
        var hovered = ImGui.IsItemHovered();
        var min = ImGui.GetItemRectMin();
        var dl = ImGui.GetWindowDrawList();
        var s = Theme.Surface;
        if (hovered)
        {
            dl.AddRectFilled(min, min + new Vector2(side), Theme.U32(s.Hover), side * 0.5f);
        }

        ImGui.PushFont(UiBuilder.IconFont);
        var size = ImGui.CalcTextSize(CloseIcon);
        dl.AddText(min + ((new Vector2(side) - size) * 0.5f), Theme.U32(hovered ? s.Text : s.TextTertiary), CloseIcon);
        ImGui.PopFont();
        Chrome.FocusRing(side * 0.5f);
        if (hovered)
        {
            UiMetrics.Tooltip(Strings.StopDismissTooltip);
        }

        return clicked && interactive;
    }

    private static void DrawAgo(ImDrawListPtr dl, string ago, Vector2 at)
    {
        using (Typography.Caption())
        {
            dl.AddText(ImGui.GetFont(), ImGui.GetFontSize(), new Vector2(MathF.Round(at.X), MathF.Round(at.Y)), Theme.U32(GamePanelShell.QuietTone), ago);
        }
    }

    private static float CaptionLine()
    {
        using (Typography.Caption())
        {
            return ImGui.GetTextLineHeight();
        }
    }

    /// <summary>"2 min ago", composed again only when the minute or the language changes.</summary>
    private static string Ago(RunStops stops)
    {
        var minute = (int)((stops.Now - stops.Dock.RaisedAt) / 60.0);
        if (minute != agoMinute || agoLanguage != Localization.Loc.Version)
        {
            agoMinute = minute;
            agoLanguage = Localization.Loc.Version;
            agoText = stops.AgoText();
        }

        return agoText;
    }

    /// <summary>Starts a block whose wrapped lines start at <paramref name="left"/> and wrap at <paramref name="width"/>.</summary>
    private static void BeginWrapped(float left, float width)
    {
        ImGui.PushTextWrapPos(left - ImGui.GetWindowPos().X + width);
        ImGui.BeginGroup();
    }

    private static void EndWrapped()
    {
        ImGui.EndGroup();
        ImGui.PopTextWrapPos();
    }

    /// <summary>"Waiting: Questionable starts again once you have cleared …", composed again only when the card changes.</summary>
    private static string WaitingLine(StopCard card)
    {
        if (!ReferenceEquals(card, waitingCard) || waitingLanguage != Localization.Loc.Version)
        {
            waitingCard = card;
            waitingLanguage = Localization.Loc.Version;
            waitingText = string.Format(CultureInfo.CurrentCulture, Strings.StopWaitingDutyFormat, card.DutyName.Length > 0 ? card.DutyName : Strings.DutyGuardHiddenDuty);
        }

        return waitingText;
    }
}
