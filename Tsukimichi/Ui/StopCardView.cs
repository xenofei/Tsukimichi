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
/// (<see cref="MainWindow"/>) and, as a compact row, by the Todo overlay (<see cref="DrawRow"/>).
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

    /// <summary>The cue's ink: gold finished well, silver you did it, copper it needs you.</summary>
    public static Vector4 CueColor(StopCue cue) => cue switch
    {
        StopCue.Gold => Theme.Palette.Inks.GoldLine,
        StopCue.Silver => Theme.Surface.TextSecondary,
        _ => Theme.Copper,
    };

    /// <summary>
    /// The card in the current window, filling <paramref name="width"/> from the cursor; its frame painted to
    /// <paramref name="frameHeight"/> (last frame's measure, the caller's child height). Returns the height the content
    /// needs. Buttons act only while <paramref name="interactive"/>; <paramref name="host"/> is where Questionable's
    /// confirmation opens; <paramref name="note"/> takes "Report copied" for the status bar.
    /// </summary>
    public static float Draw(RunStops stops, StopCard card, float width, float frameHeight, bool interactive, string host, Action<string>? note)
    {
        var flair = Theme.Flair;
        var plain = flair == Flair.Plain;
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(width, MathF.Max(1f, frameHeight));
        var s = Theme.Surface;
        var rounding = UiMetrics.Px(Theme.Spacing.CardRounding);

        // The ground is opaque: the card floats over the panes.
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
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + MathF.Max(1f, titleRight - ImGui.GetCursorScreenPos().X));
                Chrome.SemiboldTextWrapped(card.Title, s.Text);
                ImGui.PopTextWrapPos();
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

        return MathF.Ceiling(bottom + padY - min.Y);
    }

    /// <summary>
    /// The Todo overlay's top row: the cue bar, the title (semibold), × and the reason in the caption role under it,
    /// within <paramref name="width"/>; the first fix as a row pill when it shows. Returns whether the row was hovered.
    /// </summary>
    public static bool DrawRow(RunStops stops, StopCard card, float width, string host)
    {
        var dl = ImGui.GetWindowDrawList();
        var s = Theme.Surface;
        var start = ImGui.GetCursorScreenPos();
        var bar = UiMetrics.Px(BarLogical);
        var left = start.X + bar + UiMetrics.Px(GapLogical);
        var side = ImGui.GetTextLineHeight();
        var right = start.X + width;
        var interactive = stops.Dock.Interactive(stops.Now, UiMetrics.ReduceMotion);
        ImGui.SetCursorScreenPos(new Vector2(left, start.Y));
        ImGui.PushClipRect(start, new Vector2(right - side - UiMetrics.Px(4f), start.Y + side + 2f), true);
        Chrome.SemiboldText(card.Title, s.Text);
        ImGui.PopClipRect();
        ImGui.SetCursorScreenPos(new Vector2(right - side, start.Y));
        if (CloseButton(side, interactive))
        {
            stops.Dismiss();
        }

        ImGui.SetCursorScreenPos(new Vector2(left, start.Y + side + UiMetrics.Px(2f)));
        BeginWrapped(left, MathF.Max(1f, right - left));
        using (Typography.Caption())
        using (Theme.PushText(s.TextSecondary))
        {
            ImGui.TextWrapped(card.Why);
        }

        EndWrapped();

        var (first, _) = RunStopClassifier.Fixes(card.Reason);
        if (stops.Shows(card, first) && first != StopFix.None)
        {
            ImGui.SetCursorScreenPos(new Vector2(left, ImGui.GetCursorScreenPos().Y + UiMetrics.Px(2f)));
            var blocker = stops.Blocker(card, first);
            if (Chrome.ActionPill("##stopRowFix", stops.FixIcon(card, first), stops.Label(first), PillTone.Normal, blocker is null, blocker ?? stops.Tooltip(card, first), PillLayout.Row) && interactive)
            {
                stops.Fix(card, first, host);
            }
        }

        var end = ImGui.GetCursorScreenPos().Y;
        dl.AddRectFilled(new Vector2(start.X, start.Y + 1f), new Vector2(start.X + bar, MathF.Max(start.Y + 2f, end - ImGui.GetStyle().ItemSpacing.Y - 1f)), Theme.U32(CueColor(card.Cue)));
        var hovered = ImGui.IsMouseHoveringRect(start, new Vector2(right, end));
        ImGui.SetCursorScreenPos(new Vector2(start.X, end + UiMetrics.Px(4f)));
        ImGui.Dummy(new Vector2(width, 1f));
        return hovered;
    }

    /// <summary>The fixes, Stop all and Copy report, flowing onto a second row when they do not fit; returns the bottom.</summary>
    private static float DrawActions(RunStops stops, StopCard card, float left, float right, float top, bool interactive, string host, Action<string>? note)
    {
        var (first, second) = RunStopClassifier.Fixes(card.Reason);
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
        foreach (var fix in (ReadOnlySpan<StopFix>)[first, second])
        {
            if (fix == StopFix.None || !stops.Shows(card, fix))
            {
                continue;
            }

            var label = stops.Label(fix);
            var icon = stops.FixIcon(card, fix);
            var blocker = stops.Blocker(card, fix);
            var busy = (fix == StopFix.ReloadAndRetry && stops.Retrying) || (fix == StopFix.KeepGoingAfterDuty && stops.WaitingForDuty);
            var tone = !primaryTaken && !busy ? PillTone.Primary : PillTone.Normal;
            primaryTaken = true;
            Place(Chrome.ActionPillWidth(icon, label, PillLayout.Panel));
            var enabled = blocker is null && !(fix == StopFix.ReloadAndRetry && stops.Retrying);
            if (Chrome.ActionPill("##stopFix" + (int)fix, icon, label, tone, enabled, enabled ? stops.Tooltip(card, fix) : blocker, PillLayout.Panel) && interactive)
            {
                stops.Fix(card, fix, host);
            }

            if (fix == StopFix.TryAgain && blocker is not null)
            {
                reason = blocker;
            }
        }

        if (stops.AnyRunning && stops.StopAll is { } stopAll)
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
                ImGui.TextUnformatted(reason);
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
