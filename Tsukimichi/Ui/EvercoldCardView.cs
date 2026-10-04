using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>Where the Before Evercold card is drawn.</summary>
public enum EvercoldSurface : byte
{
    /// <summary>The Tonight card, below the story and events lines, above the pinned quests.</summary>
    Tonight,

    /// <summary>The Characters dashboard, below the header, for any viewed character.</summary>
    Dashboard,
}

/// <summary>
/// Draws <see cref="EvercoldCardModel"/> (spec-1.20 N7, "Anatomy"): at Full the kit's card with its corner marks, padded
/// 11 × 14, the title in the Title face; at Quiet a tonal card with a semibold title; at Plain a 22 px band header over
/// ledger lines. Then the sub-line, the open lines (a 14 px checkbox with a 3 px radius, 2 px at Plain, a Sunken fill and
/// a 1.5 px Secondary outline; an 18 px icon; the label over the detail; quiet buttons), and the one "Done: …" line, or
/// "Ready for Evercold. Nothing left on Michiru." with the gold check.
/// <para>
/// The buttons sit in a fixed 150 px trailing column when the card is 460 px wide or more and every line's buttons fit
/// it, else in a fixed 24 px row under the words: chosen per card, never per line. A tick puts "you said so" in the
/// buttons' own slot and the label turns Secondary, so a line keeps its height. × has a 24 px target. The card leaves over
/// <see cref="MotionTokens.Leave"/>, comes back over <see cref="MotionTokens.Rise"/> rising 4 px, and a tick's check fades in
/// over <see cref="MotionTokens.HoverIn"/>; all instant under Reduce motion. No copper anywhere.
/// </para>
/// The frame goes under the words at the height they took last frame (the Tonight card already splits the draw list), so
/// the words never move.
/// </summary>
public static class EvercoldCardView
{
    private const float WideFromLogical = 460f;
    private const float SlotLogical = 150f;
    private const float RowLogical = 24f;
    private const float BoxLogical = 14f;
    private const float BoxColumnLogical = 18f;
    private const float IconLogical = 18f;
    private const float IconColumnLogical = 20f;
    private const float GapLogical = 9f;
    private const float LinePadLogical = 8f;
    private const float BandLogical = 22f;
    private const float CloseLogical = 24f;
    private const float CloseGlyphLogical = 10f;
    private const float ButtonPadLogical = 10f;
    private const float ButtonGapLogical = 6f;
    private const float DoneIconAlpha = 0.6f;
    private const uint CheckTag = 0x4556_4348; // "EVCH"

    /// <summary>An approach rate that lands within a frame: a check going away never fades.</summary>
    private const float InstantRate = 1000f;

    // Per surface: the card's height last frame, and when × was clicked there (the leave).
    private static readonly float[] Heights = new float[2];
    private static readonly double[] LeftAt = [double.NegativeInfinity, double.NegativeInfinity];

    /// <summary>
    /// The card in the current window at the cursor, <paramref name="width"/> wide, for the viewed character. Draws
    /// nothing when there is no card (no character, retired) or, on Tonight, while it is hidden; on the dashboard a
    /// hidden card leaves its quiet "Show again" line.
    /// </summary>
    public static void Draw(EvercoldCardModel model, UiState ui, EvercoldSurface surface, float width)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(ui);
        var index = (int)surface;
        if (!model.Refresh())
        {
            Heights[index] = 0f;
            return;
        }

        using var id = ImRaii.PushId(surface == EvercoldSurface.Tonight ? "evercoldTonight" : "evercoldDashboard");
        var now = ImGui.GetTime();
        var animate = Motion.Enabled && !UiMetrics.ReduceMotion;
        var leaving = animate && now - LeftAt[index] < MotionTokens.Leave;
        if (model.Hidden && !leaving)
        {
            Heights[index] = 0f;
            if (surface == EvercoldSurface.Dashboard)
            {
                DrawHiddenLine(model, width);
            }

            return;
        }

        var alpha = 1f;
        var rise = 0f;
        if (leaving)
        {
            alpha = 1f - Math.Clamp((float)((now - LeftAt[index]) / MotionTokens.Leave), 0f, 1f);
        }
        else if (animate && now - model.ShownAt < MotionTokens.Rise)
        {
            // Back from Undo or Show again: it rises into place.
            var t = MotionMath.EaseOutCubic(Math.Clamp((float)((now - model.ShownAt) / MotionTokens.Rise), 0f, 1f));
            alpha = t;
            rise = MathF.Round((1f - t) * UiMetrics.Px(MotionTokens.RiseLogical));
        }

        var dl = ImGui.GetWindowDrawList();
        var firstVertex = dl.VtxBuffer.Size;
        var origin = ImGui.GetCursorScreenPos();
        var min = origin with { Y = origin.Y + rise };
        var height = DrawCard(model, ui, surface, min, width, Heights[index], interactive: !leaving, out var hide);
        Heights[index] = height;
        if (alpha < 1f)
        {
            Chrome.FadeVertices(dl, firstVertex, alpha);
        }

        if (hide)
        {
            LeftAt[index] = animate ? now : double.NegativeInfinity;
            model.Hide();
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + height));
        ImGui.Dummy(new Vector2(width, 0f));
    }

    /// <summary>Lays out and draws the card at <paramref name="min"/>; returns its height. <paramref name="hide"/> on a click of ×.</summary>
    private static float DrawCard(EvercoldCardModel model, UiState ui, EvercoldSurface surface, Vector2 min, float width, float lastHeight, bool interactive, out bool hide)
    {
        hide = false;
        var dl = ImGui.GetWindowDrawList();
        var s = Theme.Surface;
        var frame = FlairRules.Card(Theme.Flair);
        var plain = frame == CardFrame.None;
        var pad = plain ? new Vector2(UiMetrics.Px(4f), 0f) : new Vector2(UiMetrics.Px(Theme.Spacing.CardPad.X), UiMetrics.Px(Theme.Spacing.CardPad.Y));
        if (frame == CardFrame.BrassCorners)
        {
            // The spec's Full padding: 11 above and below, 14 at the sides.
            pad = new Vector2(UiMetrics.Px(14f), UiMetrics.Px(11f));
        }

        if (lastHeight > 0f)
        {
            Chrome.CardSurface(dl, min, new Vector2(min.X + width, min.Y + lastHeight));
        }

        var left = min.X + pad.X;
        var right = min.X + width - pad.X;
        var inner = MathF.Max(1f, right - left);
        var y = min.Y + pad.Y;

        // Title row: the title, and × at the trailing end with its 24 px target.
        var close = UiMetrics.Px(CloseLogical);
        if (plain)
        {
            // The 22 px band; ×'s 24 px target overhangs it by a pixel either side.
            var band = MathF.Round(UiMetrics.Px(BandLogical));
            dl.AddRectFilled(new Vector2(min.X, y), new Vector2(min.X + width, y + band), Theme.U32(Theme.Tones.Band));
            dl.AddRectFilled(new Vector2(min.X, y + band), new Vector2(min.X + width, y + band + 1f), Theme.U32(Theme.Tones.Rule));
            ImGui.SetCursorScreenPos(new Vector2(left, y + MathF.Round((band - ImGui.GetTextLineHeight()) * 0.5f)));
            ImGui.TextColored(s.Text, Strings.PrepTitle);
            hide = CloseButton(new Vector2(right - close + UiMetrics.Px(4f), y + MathF.Round((band - close) * 0.5f)), close, interactive);
            y += band + 1f + UiMetrics.Px(3f);
        }
        else
        {
            float titleHeight;
            ImGui.SetCursorScreenPos(new Vector2(left, y));
            if (frame == CardFrame.BrassCorners)
            {
                using (Typography.Title(Strings.PrepTitle))
                {
                    titleHeight = ImGui.GetTextLineHeight();
                    var titleY = y + MathF.Max(0f, MathF.Round((close - titleHeight) * 0.5f));
                    ImGui.SetCursorScreenPos(new Vector2(left, titleY));
                    ImGui.TextColored(s.Text, Strings.PrepTitle);
                }
            }
            else
            {
                titleHeight = ImGui.GetTextLineHeight();
                ImGui.SetCursorScreenPos(new Vector2(left, y + MathF.Max(0f, MathF.Round((close - titleHeight) * 0.5f))));
                Chrome.SemiboldText(Strings.PrepTitle, s.Text);
            }

            var row = MathF.Max(titleHeight, close);
            hide = CloseButton(new Vector2(right - close + UiMetrics.Px(6f), y + MathF.Round((row - close) * 0.5f)), close, interactive);
            y += row;
        }

        // Sub-line: "For Michiru · early access 22 Jan (expected)", or the dashboard's "As of the last login, …".
        y += UiMetrics.Px(3f);
        ImGui.SetCursorScreenPos(new Vector2(left, y));
        using (Typography.Caption())
        {
            TextFlow.Wrapped(surface == EvercoldSurface.Tonight ? model.SubTonight : model.SubDashboard, inner, Theme.U32(s.TextSecondary));
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(model.SubTooltip);
        }

        y = ImGui.GetItemRectMax().Y + UiMetrics.Px(6f);

        if (model.AllDoneNow)
        {
            y = DrawAllDone(model, left, y + UiMetrics.Px(4f), inner) + UiMetrics.Px(2f);
        }
        else
        {
            var wide = IsWide(model, width);
            var lines = model.Lines;
            for (var i = 0; i < lines.Length && i < model.Open.Count; i++)
            {
                using var lineId = ImRaii.PushId((int)lines[i].Kind);
                if (i > 0)
                {
                    dl.AddRectFilled(new Vector2(left, y), new Vector2(right, y + UiMetrics.Hairline), Theme.U32(s.Line));
                }

                y = DrawLine(model, ui, lines[i], model.Open[i].Check, left, y, inner, wide, interactive);
            }

            if (model.Fold.Length > 0)
            {
                dl.AddRectFilled(new Vector2(left, y), new Vector2(right, y + UiMetrics.Hairline), Theme.U32(s.Line));
                y += UiMetrics.Px(7f);
                ImGui.SetCursorScreenPos(new Vector2(left, y));
                using (Typography.Caption())
                {
                    TextFlow.Wrapped(model.Fold, inner, Theme.U32(s.TextTertiary));
                }

                y = ImGui.GetItemRectMax().Y;
            }
        }

        var height = MathF.Ceiling(y + pad.Y - min.Y);
        if (lastHeight <= 0f)
        {
            // The first frame: the words are drawn, the frame is not yet; draw its border now, over the empty ground only.
            Chrome.CardBorder(dl, min, new Vector2(min.X + width, min.Y + height), UiMetrics.Px(Theme.Spacing.CardRounding));
        }

        return height;
    }

    /// <summary>
    /// Whether the card uses the wide form: 460 px or more at the current scale, and every line's buttons fit the 150 px
    /// column. Decided for the whole card, so lines never differ.
    /// </summary>
    private static bool IsWide(EvercoldCardModel model, float width)
    {
        if (width < UiMetrics.Px(WideFromLogical))
        {
            return false;
        }

        var slot = UiMetrics.Px(SlotLogical);
        foreach (var line in model.Lines)
        {
            if (ButtonsWidth(line) > slot)
            {
                return false;
            }
        }

        return true;
    }

    private static float ButtonsWidth(EvercoldCardModel.LineView line)
    {
        using var caption = Typography.Caption();
        var total = 0f;
        foreach (var button in line.Buttons)
        {
            total += ButtonWidth(button.Label) + (total > 0f ? UiMetrics.Px(ButtonGapLogical) : 0f);
        }

        return total;
    }

    private static float ButtonWidth(string label) => ImGui.CalcTextSize(label).X + (2f * UiMetrics.Px(ButtonPadLogical));

    /// <summary>One open line; returns the y under it.</summary>
    private static float DrawLine(EvercoldCardModel model, UiState ui, EvercoldCardModel.LineView line, PrepCheck check, float left, float top, float inner, bool wide, bool interactive)
    {
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var pad = UiMetrics.Px(LinePadLogical);
        var gap = UiMetrics.Px(GapLogical);
        var y = top + pad;
        var done = check != PrepCheck.Open;

        // Checkbox column (18 px) and the icon column (20 px).
        DrawCheckbox(model, line.Kind, check, new Vector2(left, y), interactive);
        var iconX = left + UiMetrics.Px(BoxColumnLogical) + gap;
        var iconSide = MathF.Round(UiMetrics.Px(IconLogical));
        var iconStart = dl.VtxBuffer.Size;
        Chrome.DrawPillIcon(dl, line.Icon, new Vector2(iconX, y), iconSide, Theme.U32(s.TextSecondary), enabled: true);
        if (done)
        {
            Chrome.FadeVertices(dl, iconStart, DoneIconAlpha);
        }

        // The words: the label over the detail, wrapped beside the slot (wide) or to the edge (narrow).
        var textX = iconX + UiMetrics.Px(IconColumnLogical) + gap;
        var slot = UiMetrics.Px(SlotLogical);
        var textRight = wide ? left + inner - slot - gap : left + inner;
        var textWidth = MathF.Max(1f, textRight - textX);
        ImGui.SetCursorScreenPos(new Vector2(textX, y));
        TextFlow.Wrapped(line.Label, textWidth, Theme.U32(done ? s.TextSecondary : s.Text));
        var textMin = new Vector2(textX, y);
        var bottom = ImGui.GetItemRectMax().Y;
        ImGui.SetCursorScreenPos(new Vector2(textX, bottom + UiMetrics.Px(1f)));
        using (Typography.Caption())
        {
            TextFlow.Wrapped(line.Detail, textWidth, Theme.U32(done ? s.TextTertiary : s.TextSecondary));
        }

        bottom = ImGui.GetItemRectMax().Y;
        if (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(textMin, new Vector2(textRight, bottom)))
        {
            Hover(line);
        }

        // The buttons' slot: a fixed column at the trailing end (wide), or a fixed 24 px row under the words.
        var row = UiMetrics.Px(RowLogical);
        Vector2 slotMin;
        float slotWidth;
        float end;
        if (wide)
        {
            slotMin = new Vector2(left + inner - slot, y);
            slotWidth = slot;
            end = MathF.Max(bottom, y + row);
        }
        else
        {
            slotMin = new Vector2(textX, bottom + UiMetrics.Px(5f));
            slotWidth = textWidth;
            end = slotMin.Y + row;
        }

        DrawSlot(model, ui, line, check, slotMin, slotWidth, row, alignRight: wide, interactive);
        return end + pad;
    }

    /// <summary>The slot: the line's quiet buttons, "you said so" after a tick, nothing once the game says it is done.</summary>
    private static void DrawSlot(EvercoldCardModel model, UiState ui, EvercoldCardModel.LineView line, PrepCheck check, Vector2 min, float width, float row, bool alignRight, bool interactive)
    {
        var s = Theme.Surface;
        using var caption = Typography.Caption();
        if (check == PrepCheck.You)
        {
            var size = ImGui.CalcTextSize(Strings.PrepYouSaidSo);
            var x = alignRight ? min.X + width - size.X : min.X + UiMetrics.Px(2f);
            ImGui.GetWindowDrawList().AddText(new Vector2(x, min.Y + MathF.Round((row - size.Y) * 0.5f)), Theme.U32(s.TextTertiary), Strings.PrepYouSaidSo);
        }
        else if (check == PrepCheck.Open && line.Buttons.Length > 0)
        {
            var total = 0f;
            foreach (var button in line.Buttons)
            {
                total += ButtonWidth(button.Label) + (total > 0f ? UiMetrics.Px(ButtonGapLogical) : 0f);
            }

            var x = alignRight ? min.X + width - total : min.X;
            foreach (var button in line.Buttons)
            {
                var buttonWidth = ButtonWidth(button.Label);
                var buttonMin = new Vector2(x, min.Y);
                if (QuietButton(button.Label, buttonMin, new Vector2(buttonWidth, row), button.Tooltip) && interactive)
                {
                    model.Do(button.Act, line, ui);
                }

                if (button.Act == EvercoldCardModel.Act.MakeRoom)
                {
                    // C9's popover opens under its button, in this line's ID scope.
                    model.MakeRoom?.Invoke()?.DrawPopover(new Vector2(buttonMin.X, buttonMin.Y + row + UiMetrics.Px(4f)), above: false, ImGui.GetMainViewport().Size.Y);
                }

                x += buttonWidth + UiMetrics.Px(ButtonGapLogical);
            }
        }
    }

    /// <summary>A quiet button (the 1.18/1.19 kind): 24 px tall (a 20 px square-cornered one centred in it at Plain), the label in Secondary inside a hairline, Text and the hover fill on hover.</summary>
    private static bool QuietButton(string label, Vector2 min, Vector2 size, string tooltip)
    {
        var plain = FlairRules.Card(Theme.Flair) == CardFrame.None;
        var drawMin = plain ? new Vector2(min.X, min.Y + MathF.Round(UiMetrics.Px(2f))) : min;
        var drawSize = plain ? new Vector2(size.X, size.Y - (2f * MathF.Round(UiMetrics.Px(2f)))) : size;
        var rounding = plain ? UiMetrics.Px(2f) : drawSize.Y * 0.5f;
        ImGui.SetCursorScreenPos(min);
        var clicked = ImGui.InvisibleButton(label, size);
        var hovered = ImGui.IsItemHovered();
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        if (hovered)
        {
            dl.AddRectFilled(drawMin, drawMin + drawSize, Theme.U32(s.Hover), rounding);
            UiMetrics.Tooltip(tooltip);
        }

        dl.AddRect(drawMin, drawMin + drawSize, Theme.U32(Theme.Glyphs.HighContrast ? s.StrongLine : s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        var text = ImGui.CalcTextSize(label);
        dl.AddText(new Vector2(MathF.Round(drawMin.X + ((drawSize.X - text.X) * 0.5f)), MathF.Round(drawMin.Y + ((drawSize.Y - text.Y) * 0.5f))), Theme.U32(hovered ? s.Text : s.TextSecondary), label);
        Chrome.FocusRing(rounding);
        return clicked;
    }

    /// <summary>
    /// The 14 px checkbox: a Sunken fill, a 1.5 px Secondary outline, the check in Text fading in over HoverIn. Its
    /// target is the 18 × 24 px column cell. A line the game says is done is checked and cannot be unticked.
    /// </summary>
    private static void DrawCheckbox(EvercoldCardModel model, PrepLineKind kind, PrepCheck check, Vector2 at, bool interactive)
    {
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var box = MathF.Round(UiMetrics.Px(BoxLogical));
        var cell = new Vector2(UiMetrics.Px(BoxColumnLogical), UiMetrics.Px(RowLogical));
        var boxMin = new Vector2(at.X, at.Y + UiMetrics.Px(2f));
        ImGui.SetCursorScreenPos(at);
        var clicked = ImGui.InvisibleButton("##tick", cell);
        var hovered = ImGui.IsItemHovered();
        var rounding = UiMetrics.Px(FlairRules.Card(Theme.Flair) == CardFrame.None ? 2f : 3f);
        dl.AddRectFilled(boxMin, boxMin + new Vector2(box), Theme.U32(hovered && check != PrepCheck.Game ? s.Hover : s.Sunken), rounding);
        dl.AddRect(boxMin, boxMin + new Vector2(box), Theme.U32(s.TextSecondary), rounding, ImDrawFlags.None, MathF.Max(1f, UiMetrics.Px(1.5f)));
        // The check fades in over HoverIn and goes at once (another character's line never fades out).
        var shown = Motion.LerpAsym(Motion.Key(CheckTag, (uint)kind), check == PrepCheck.Open ? 0f : 1f, MotionTokens.RateFor(MotionTokens.HoverIn), InstantRate);
        if (shown > 0.01f)
        {
            DrawCheck(dl, boxMin, box, Theme.U32(s.Text with { W = s.Text.W * shown }));
        }

        Chrome.FocusRing(rounding);
        if (hovered)
        {
            UiMetrics.Tooltip(check switch
            {
                PrepCheck.Game => Strings.PrepGameDoneTooltip,
                PrepCheck.You => Strings.PrepUntickTooltip,
                _ => Strings.PrepTickTooltip,
            });
        }

        if (clicked && interactive && check != PrepCheck.Game)
        {
            model.SetTicked(kind, check == PrepCheck.Open);
        }
    }

    private static void DrawCheck(ImDrawListPtr dl, Vector2 min, float side, uint ink)
    {
        var thickness = MathF.Max(1.5f, UiMetrics.Px(1.6f));
        var a = new Vector2(min.X + (side * 0.24f), min.Y + (side * 0.52f));
        var b = new Vector2(min.X + (side * 0.43f), min.Y + (side * 0.70f));
        var c = new Vector2(min.X + (side * 0.77f), min.Y + (side * 0.32f));
        dl.AddLine(a, b, ink, thickness);
        dl.AddLine(b, c, ink, thickness);
    }

    /// <summary>All done: one line with the gold check (finished). Returns the y under it.</summary>
    private static float DrawAllDone(EvercoldCardModel model, float left, float y, float inner)
    {
        var dl = ImGui.GetWindowDrawList();
        var line = ImGui.GetTextLineHeight();
        var side = MathF.Round(UiMetrics.Px(13f));
        DrawCheck(dl, new Vector2(left, y + MathF.Round((line - side) * 0.5f)), side, Theme.U32(Theme.Accent));
        var textX = left + side + UiMetrics.Px(8f);
        ImGui.SetCursorScreenPos(new Vector2(textX, y));
        TextFlow.Wrapped(model.AllDone, MathF.Max(1f, inner - (textX - left)), Theme.U32(Theme.Surface.Text));
        return ImGui.GetItemRectMax().Y;
    }

    /// <summary>× : a 10 px glyph on a 24 px round target, Tertiary, Text and the hover fill on hover; tooltip "Hide for this character".</summary>
    private static bool CloseButton(Vector2 min, float side, bool interactive)
    {
        ImGui.SetCursorScreenPos(min);
        var clicked = ImGui.InvisibleButton("##hideEvercold", new Vector2(side));
        var hovered = ImGui.IsItemHovered();
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        if (hovered)
        {
            dl.AddRectFilled(min, min + new Vector2(side), Theme.U32(s.Hover), side * 0.5f);
            Safety.Tooltip(Strings.PrepHideTooltip, GuardedAction.HideEvercoldCard);
        }

        var glyph = UiMetrics.Px(CloseGlyphLogical) * 0.5f;
        var center = min + new Vector2(side * 0.5f);
        var ink = Theme.U32(hovered ? s.Text : s.TextTertiary);
        var thickness = MathF.Max(1f, UiMetrics.Px(1.3f));
        dl.AddLine(center - new Vector2(glyph), center + new Vector2(glyph), ink, thickness);
        dl.AddLine(center + new Vector2(glyph, -glyph), center + new Vector2(-glyph, glyph), ink, thickness);
        Chrome.FocusRing(side * 0.5f);
        return clicked && interactive && SafetyRules.TierOf(GuardedAction.HideEvercoldCard) == SafetyTier.None;
    }

    /// <summary>A line's hover: its label (Text, semibold), why it is there and when it counts as done (Secondary), then "Tick it yourself if …" (Tertiary).</summary>
    private static void Hover(EvercoldCardModel.LineView line)
    {
        using var tooltip = Theme.Tooltip();
        using var body = Typography.Body();
        UiMetrics.ApplyFontScale();
        using (UiMetrics.TooltipWrap())
        {
            var s = Theme.Surface;
            Chrome.SemiboldTextWrapped(line.Label, s.Text, UiMetrics.TooltipWrapWidth);
            ImGui.PushStyleColor(ImGuiCol.Text, s.TextSecondary);
            ImGui.TextWrapped(line.Why);
            ImGui.PopStyleColor();
            ImGui.PushStyleColor(ImGuiCol.Text, s.TextTertiary);
            ImGui.TextWrapped(line.Tick);
            ImGui.PopStyleColor();
        }
    }

    /// <summary>The dashboard's way back: "Before Evercold is hidden for Michiru." in a quiet keyline box with Show again.</summary>
    private static void DrawHiddenLine(EvercoldCardModel model, float width)
    {
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        var padX = UiMetrics.Px(12f);
        var padY = UiMetrics.Px(8f);
        var row = UiMetrics.Px(RowLogical);
        float buttonWidth;
        using (Typography.Caption())
        {
            buttonWidth = ButtonWidth(Strings.PrepShowAgain);
        }

        var textWidth = MathF.Max(1f, width - (2f * padX) - buttonWidth - UiMetrics.Px(10f));
        ImGui.SetCursorScreenPos(new Vector2(min.X + padX, min.Y + padY + MathF.Max(0f, MathF.Round((row - ImGui.GetTextLineHeight()) * 0.5f))));
        TextFlow.Wrapped(model.HiddenLine, textWidth, Theme.U32(s.TextSecondary));
        var height = MathF.Max(ImGui.GetItemRectMax().Y + padY, min.Y + padY + row + padY) - min.Y;
        bool clicked;
        using (Typography.Caption())
        {
            clicked = QuietButton(Strings.PrepShowAgain, new Vector2(min.X + width - padX - buttonWidth, min.Y + MathF.Round((height - row) * 0.5f)), new Vector2(buttonWidth, row), Strings.PrepShowAgainTooltip);
        }

        if (clicked)
        {
            model.ShowAgain();
        }

        dl.AddRect(min, new Vector2(min.X + width, min.Y + height), Theme.U32(Theme.Glyphs.HighContrast ? s.StrongLine : s.Line), UiMetrics.Px(4f), ImDrawFlags.None, UiMetrics.Hairline);
        ImGui.SetCursorScreenPos(new Vector2(min.X, min.Y + height));
        ImGui.Dummy(new Vector2(width, 0f));
    }
}
