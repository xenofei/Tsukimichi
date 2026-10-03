using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>How a <see cref="Chrome.BeginCard"/> surface is painted.</summary>
public enum CardKind
{
    /// <summary>Raised fill with a hairline border (ui-revamp §2.5 cards).</summary>
    Raised,

    /// <summary>Sunken well with a hairline border.</summary>
    Sunken,

    /// <summary>A faint wash with a 3 px rule on the left edge (tips, callouts); no border.</summary>
    Callout,
}

/// <summary>
/// The chrome primitives every pane draws with (ui-revamp §6.1, T13): rounded surfaces, pills, chips, badges,
/// hairlines, the two-line lift, a segmented control with an explicit "All" segment, round icon buttons, a gradient
/// scrim, a cover-cropped image, the keyboard focus ring, text outlined for game scenes and the hold-to-confirm
/// button. Colours come from <see cref="Theme.Surface"/> (Night, or the user's Dalamud colours), so a helper looks
/// right in both palettes; gold appears only where the caller says the thing is actionable. Every size goes through
/// <see cref="UiMetrics.Px"/> or <see cref="UiMetrics.MinTarget"/>; every clickable helper is a real ImGui item
/// (keyboard and gamepad navigation reach it, <c>IsItemHovered</c> works after it). Nothing allocates per frame:
/// strings are the caller's, icon strings and badge counts are cached.
/// </summary>
public static partial class Chrome
{
    /// <summary>Every <see cref="HoldButton"/> label ends with this so its id survives the countdown text.</summary>
    public const string HoldIdSuffix = "###hold";

    // Logical sizes (ui-revamp §4.1).
    private const float CardPadX = 10f;
    private const float CardPadY = 8f;
    private const float CardRounding = 6f;
    private const float CalloutRule = 3f;
    private const float PillPadX = 7f;
    private const float PillPadY = 2f;
    private const float ChipPadX = 9f;
    private const float ChipGlyph = 8f;
    private const float SegmentPadX = Core.Ui.LayoutBudgets.SegmentPadLogical;
    private const float BadgeMin = 14f;
    private const float FocusThickness = 1.5f;
    private const float HoldOutset = 2f;
    private const float HoldThickness = 2f;

    private static readonly Dictionary<FontAwesomeIcon, string> IconStrings = [];
    private static readonly string[] CountStrings = BuildCounts();

    // Countdown labels under Reduce motion, index = tenths of a second left (1..20, the longest hold Settings offers).
    private static string[] Countdown => countdownText.Value;

    private static readonly Tsukimichi.Localization.LocArray countdownText = new(BuildCountdown);

    // The open card: channel-split surfaces cannot nest on one draw list, so there is one at a time.
    private static bool cardOpen;
    private static int cardFrame = -1;
    private static Vector2 cardStart;
    private static float cardWidth;
    private static CardKind cardKind;
    private static Vector4 cardAccent;

    /// <summary>The FontAwesome glyph as a string, built once per icon.</summary>
    public static string Icon(FontAwesomeIcon icon)
    {
        if (!IconStrings.TryGetValue(icon, out var text))
        {
            text = icon.ToIconString();
            IconStrings[icon] = text;
        }

        return text;
    }

    // ------------------------------------------------------------------ cards

    /// <summary>
    /// Opens a rounded surface the width of the content region: content drawn until <see cref="EndCard"/> lands on it,
    /// padded 10 × 8, and wrapped text wraps at its inner edge. The surface is painted underneath once the content's
    /// height is known (a two-channel split, as <c>DetailPane.DrawHeaderCard</c> did). With a <paramref name="title"/>
    /// the first line is the <paramref name="icon"/> (a FontAwesome string, see <see cref="Icon"/>) in the secondary
    /// tone and the title in the primary. Cards do not nest.
    /// </summary>
    /// <param name="id">Pushed as an id scope for the card's content.</param>
    /// <param name="accent">The callout rule's colour; default is VeilLine (neutral). Ignored by the other kinds.</param>
    /// <param name="eyebrow">
    /// The title as a Section heading at the Decoration level (plan v7 §1: TrumpGothic, tracked and cased for the
    /// language at Full, the Lead face at Quiet, a band at Plain; never smaller than the body) rather than the caption
    /// role, with the icon at caption size centred on it. Pass the title in its own case.
    /// </param>
    public static void BeginCard(string id, string? title = null, string? icon = null, CardKind kind = CardKind.Raised, Vector4? accent = null, bool eyebrow = false)
    {
        OpenCard(kind, accent);
        ImGui.PushID(id);
        CardContent(title, icon, eyebrow);
    }

    /// <summary><see cref="BeginCard(string, string?, string?, CardKind, Vector4?, bool)"/> with an integer id (no string to build per card).</summary>
    public static void BeginCard(int id, string? title = null, string? icon = null, CardKind kind = CardKind.Raised, Vector4? accent = null, bool eyebrow = false)
    {
        OpenCard(kind, accent);
        ImGui.PushID(id);
        CardContent(title, icon, eyebrow);
    }

    private static void OpenCard(CardKind kind, Vector4? accent)
    {
        // A card left open by an earlier frame (its Draw threw between Begin and End) is stale: the draw list and its
        // channel split were reset with the frame, so only the flag needs clearing. Within one frame it is a nesting bug.
        var frame = ImGui.GetFrameCount();
        if (cardOpen && cardFrame == frame)
        {
            throw new InvalidOperationException("Chrome cards do not nest; call EndCard first.");
        }

        cardFrame = frame;
        cardOpen = true;
        cardKind = kind;
        cardAccent = accent ?? Theme.Surface.StrongLine;
        cardStart = ImGui.GetCursorScreenPos();
        cardWidth = MathF.Max(0f, ImGui.GetContentRegionAvail().X);
    }

    private static void CardContent(string? title, string? icon, bool eyebrow)
    {
        var dl = ImGui.GetWindowDrawList();
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);

        var inset = Inset();
        ImGui.SetCursorScreenPos(cardStart + inset);
        ImGui.BeginGroup();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + MathF.Max(1f, cardWidth - inset.X - UiMetrics.Px(CardPadX)));
        if (title is null)
        {
            return;
        }

        if (eyebrow)
        {
            SectionTitle(title, icon);
            return;
        }

        // The title in the caption role (ui-revamp §4.2 "Section title": 0.85× + icon, same as Caption), the icon at
        // the same size, so the header row is one size.
        using var caption = Typography.Caption();
        if (icon is not null)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.Surface.TextSecondary);
            Typography.Icon(icon);
            ImGui.PopStyleColor();
            ImGui.SameLine(0f, UiMetrics.Px(6f));
        }

        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Surface.Text);
        ImGui.TextUnformatted(title);
        ImGui.PopStyleColor();
    }

    /// <summary>
    /// A card title in the Section role at the card's Decoration level (docs/design/v7/ui/spec.md §1), on a heading row
    /// of the level's height (<see cref="HeadingLayout.SectionRow"/>) with the title centred on it:
    /// <list type="bullet">
    /// <item>Full: TrumpGothic at 1.80× the body (tapering at large Text sizes), capitals in English, tracked +0.08 em,
    /// in the lighter gilt over a 1 px Abyss shadow; where the game face cannot draw it (game fonts off, a script it
    /// lacks), the Lead face in sentence case, still gilt.</item>
    /// <item>Quiet: the Lead face (the body face at 1.15×) in the primary tone.</item>
    /// <item>Plain: the body size in the primary tone on a full-bleed band with a 1 px line under it.</item>
    /// </list>
    /// The optional icon sits at caption size in the secondary tone before it. The heading row is the last item (as
    /// wide as the title), so a caption placed from its rect centres on the row; the content starts the level's gap
    /// under it. A cut title names itself on hover.
    /// </summary>
    private static void SectionTitle(string title, string? icon)
    {
        var level = FlairRules.Card(Theme.Flair) switch
        {
            CardFrame.BrassCorners => Flair.Full,
            CardFrame.Tonal => Flair.Quiet,
            _ => Flair.Plain,
        };
        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var room = RoomX();
        var upper = SectionHeading.Label(title);
        using var role = level switch
        {
            Flair.Full => Typography.Section(upper),
            Flair.Quiet => Typography.Lead(),
            _ => default,
        };

        var gameFace = role.GameFace;
        var text = gameFace ? upper : title;
        var tracking = gameFace ? Typography.SectionTracking(in role) : 0f;
        var line = ImGui.GetTextLineHeight();
        var width = TrackedTextWidth(text, tracking);
        var row = HeadingLayout.SectionRowHeight(level, UiMetrics.Scale, line);

        if (level == Flair.Plain)
        {
            // The ledger's band, full bleed (a little past the content either side), with its line under it.
            var bleed = UiMetrics.Px(4f);
            var left = cardStart.X - bleed;
            var right = cardStart.X + cardWidth + bleed;
            dl.AddRectFilled(new Vector2(left, start.Y), new Vector2(right, start.Y + row), Theme.U32(Theme.Tones.Band));
            dl.AddRectFilled(new Vector2(left, start.Y + row), new Vector2(right, start.Y + row + 1f), Theme.U32(Theme.Tones.Rule));
        }

        var x = start.X;
        if (icon is not null)
        {
            using var caption = Typography.Caption();
            var iconLine = ImGui.GetTextLineHeight();
            ImGui.SetCursorScreenPos(new Vector2(x, start.Y + MathF.Round((row - iconLine) * 0.5f)));
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.Surface.TextSecondary);
            Typography.Icon(icon);
            ImGui.PopStyleColor();
            x = ImGui.GetItemRectMax().X + UiMetrics.Px(6f);
        }

        var full = level == Flair.Full;
        var ink = Theme.U32(full ? Theme.OrnamentLight : Theme.Surface.Text);
        var shadow = full ? Theme.DropShadow(0.55f) : 0u;
        var textRoom = MathF.Max(1f, start.X + room - x);
        var cut = TrackedTextAt(dl, new Vector2(x, start.Y + MathF.Round((row - line) * 0.5f)), textRoom, text, ink, tracking, shadow);

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(MathF.Max(1f, x - start.X + MathF.Min(width, textRoom)), row));
        if (cut && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(title);
        }

        // The content starts the level's gap under the row (under Plain's line), whatever ImGui's item spacing is.
        var gap = UiMetrics.Px(HeadingLayout.SectionRow(level).Gap) + (level == Flair.Plain ? 1f : 0f);
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + row + gap));
    }

    /// <summary>
    /// Closes the card opened by <see cref="BeginCard"/>: paints the surface under the content, then leaves one item
    /// spanning the whole card, so <c>GetItemRectMin/Max</c> and <c>IsItemHovered</c> refer to the card afterwards
    /// (a tutorial can record it) and the cursor sits under it.
    /// </summary>
    public static void EndCard()
    {
        if (!cardOpen)
        {
            throw new InvalidOperationException("EndCard without BeginCard.");
        }

        cardOpen = false;
        ImGui.PopTextWrapPos();
        ImGui.EndGroup();
        var contentMax = ImGui.GetItemRectMax();
        var max = new Vector2(cardStart.X + cardWidth, contentMax.Y + Inset().Y);

        var dl = ImGui.GetWindowDrawList();
        dl.ChannelsSetCurrent(0);
        var rounding = UiMetrics.Px(CardRounding);
        var s = Theme.Surface;
        switch (cardKind)
        {
            case CardKind.Callout:
                dl.AddRectFilled(cardStart, max, Theme.WithAlpha(s.Text, 0.05f), rounding);
                dl.AddRectFilled(cardStart, new Vector2(cardStart.X + UiMetrics.Px(CalloutRule), max.Y), Theme.U32(cardAccent), rounding, ImDrawFlags.RoundCornersLeft);
                break;
            case CardKind.Sunken:
                dl.AddRectFilled(cardStart, max, Theme.U32(s.Sunken), rounding);
                dl.AddRect(cardStart, max, Theme.U32(s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
                break;
            default:
                CardSurface(dl, cardStart, max);
                break;
        }

        dl.ChannelsMerge();
        ImGui.PopID();
        ImGui.SetCursorScreenPos(cardStart);
        ImGui.Dummy(new Vector2(cardWidth, max.Y - cardStart.Y));
    }

    /// <summary>
    /// A raised card's surface at the frame's Decoration level (docs/design/flair-v13 §1, "Card frame",
    /// <see cref="FlairRules.Card"/>): at Full gilt brass, a soft shadow falling straight down (0 3 10 at 0.38), a fill of
    /// the raised tone at 0.82 darkening a little toward the foot, a MoonHigh highlight along the top edge only, the brass
    /// border lit from the upper left and a corner mark at each corner overlapping it; at Quiet a borderless plane a tone
    /// lighter than the pane (with a VeilLine border under the high-contrast palette, since tone alone is not a boundary);
    /// at Plain nothing (the heading row's line is the structure). Allocation-free.
    /// </summary>
    public static void CardSurface(ImDrawListPtr dl, Vector2 min, Vector2 max)
    {
        var s = Theme.Surface;
        var spacing = Theme.Spacing;
        var rounding = UiMetrics.Px(spacing.CardRounding);
        switch (FlairRules.Card(Theme.Flair))
        {
            case CardFrame.BrassCorners:
            {
                Ornament.DropShadow(dl, min, max, rounding, UiMetrics.Px(3f), UiMetrics.Px(10f), 0.38f);
                var top = s.Raised with { W = 0.82f };
                var foot = Vector4.Lerp(s.Raised, s.Window, 0.35f) with { W = 0.86f };
                dl.AddRectFilledMultiColor(min, max, Theme.U32(top), Theme.U32(top), Theme.U32(foot), Theme.U32(foot));
                var line = UiMetrics.Hairline;
                dl.AddRectFilled(new Vector2(min.X + rounding, min.Y + line), new Vector2(max.X - rounding, min.Y + (2f * line)), Theme.TopHighlight(0.07f));
                Ornament.BrassBorder(dl, min, max, rounding, line);
                Ornament.CornerMarks(dl, min, max, MathF.Round(UiMetrics.Px(CardCornerLogical)));
                break;
            }

            case CardFrame.Tonal:
                dl.AddRectFilled(min, max, Theme.U32(Theme.Tones.Card), rounding);
                if (FlairRules.CardBorder(Theme.Flair, Theme.Glyphs.HighContrast))
                {
                    dl.AddRect(min, max, Theme.U32(s.StrongLine), rounding, ImDrawFlags.None, UiMetrics.Hairline);
                }

                break;
        }
    }

    /// <summary>
    /// A card's border alone at the frame's level, for surfaces painted elsewhere (a bordered child drawn as a card):
    /// the brass and its corner marks at Full, the VeilLine border of a high-contrast tonal card, else nothing.
    /// </summary>
    public static void CardBorder(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding)
    {
        switch (FlairRules.Card(Theme.Flair))
        {
            case CardFrame.BrassCorners:
                Ornament.BrassBorder(dl, min, max, rounding, UiMetrics.Hairline);
                Ornament.CornerMarks(dl, min, max, MathF.Round(UiMetrics.Px(CardCornerLogical)));
                break;
            case CardFrame.Tonal when FlairRules.CardBorder(Theme.Flair, Theme.Glyphs.HighContrast):
                dl.AddRect(min, max, Theme.U32(Theme.Surface.StrongLine), rounding, ImDrawFlags.None, UiMetrics.Hairline);
                break;
        }
    }

    /// <summary>
    /// The card frame for a bordered child window drawn as a card (What's new, Since you were away, Set up your road):
    /// call first thing inside the child and dispose the result when its content ends. At Full it paints the brass
    /// frame around the whole child, outside its clip rect, under the content; at Quiet and Plain the child keeps its own
    /// border (<see cref="CardChildBorder"/>). Push <see cref="CardChildBorder"/> as the child's border colour so the two
    /// do not double up: the returned scope gives the content the palette's border colour back (frames, popups) until it
    /// is disposed.
    /// </summary>
    public static Theme.StyleScope CardFrameInWindow()
    {
        if (FlairRules.Card(Theme.Flair) != CardFrame.BrassCorners)
        {
            return default;
        }

        var min = ImGui.GetWindowPos();
        var max = min + ImGui.GetWindowSize();
        var dl = ImGui.GetWindowDrawList();
        dl.PushClipRect(min - new Vector2(UiMetrics.Px(3f)), max + new Vector2(UiMetrics.Px(3f)), false);
        CardBorder(dl, min, max, ImGui.GetStyle().ChildRounding);
        dl.PopClipRect();
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.Surface.Line);
        return new Theme.StyleScope(1, 0);
    }

    /// <summary>
    /// The border colour for a child window framed by <see cref="CardFrameInWindow"/>: clear while the brass frame
    /// draws (Full), else the palette's line (what <see cref="Theme.PushNightPanel"/> pushes).
    /// </summary>
    public static Vector4 CardChildBorder => FlairRules.Card(Theme.Flair) == CardFrame.BrassCorners ? Vector4.Zero : Theme.Surface.Line;

    /// <summary>The brass of an action pill's border (Gilt at 0.55 on Night), before the high-contrast palette makes it opaque.</summary>
    public const float CardBrassAlpha = 0.55f;

    /// <summary>A card's corner mark, logical px (the 8 px L of the design).</summary>
    private const float CardCornerLogical = 8f;

    private static Vector2 Inset()
    {
        if (cardKind == CardKind.Raised)
        {
            // The level's card padding: 14 × 11 at Full, 11 × 9 at Quiet; at Plain there is no card, only a little air.
            var pad = Theme.Spacing.CardPad;
            return pad.X > 0f ? new Vector2(UiMetrics.Px(pad.X), UiMetrics.Px(pad.Y)) : new Vector2(0f, UiMetrics.Px(3f));
        }

        var x = UiMetrics.Px(CardPadX) + (cardKind == CardKind.Callout ? UiMetrics.Px(CalloutRule) : 0f);
        var y = UiMetrics.Px(cardKind == CardKind.Callout ? CardPadY * 0.9f : CardPadY);
        return new Vector2(x, y);
    }

    // ------------------------------------------------------------------ pills, chips, badges

    /// <summary>Size of a <see cref="Pill(string, Vector4, bool)"/> for <paramref name="text"/> in the caption role.</summary>
    public static Vector2 PillSize(string text)
    {
        using var caption = Typography.Caption();
        return PillSizeNow(text);
    }

    private static Vector2 PillSizeNow(string text) =>
        ImGui.CalcTextSize(text) + new Vector2(UiMetrics.Px(PillPadX) * 2f, UiMetrics.Px(PillPadY) * 2f);

    /// <summary>
    /// A tinted pill as an item: fill <paramref name="tone"/> at 16 %, border at 55 %, text in the tone, in the caption
    /// role (ui-revamp §4.2). With <paramref name="wrap"/> it moves to the next line instead of running past the content edge.
    /// </summary>
    public static void Pill(string text, Vector4 tone, bool wrap = false)
    {
        using var caption = Typography.Caption();
        var size = PillSizeNow(text);
        var pos = ImGui.GetCursorScreenPos();
        if (wrap && pos.X + size.X > ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X)
        {
            ImGui.NewLine();
            pos = ImGui.GetCursorScreenPos();
        }

        ImGui.Dummy(size);
        PillAt(ImGui.GetWindowDrawList(), pos, size, text, Theme.WithAlpha(tone, 0.16f), Theme.WithAlpha(tone, 0.55f), Theme.WithAlpha(tone, 1f));
    }

    /// <summary>A pill painted at <paramref name="min"/> without an item (row overlays): fill, optional border (0 = none), centred text.</summary>
    public static void PillAt(ImDrawListPtr dl, Vector2 min, Vector2 size, string text, uint fill, uint border, uint textColor)
    {
        var max = min + size;
        var rounding = size.Y * 0.5f;
        dl.AddRectFilled(min, max, fill, rounding);
        if (border != 0)
        {
            dl.AddRect(min, max, border, rounding, ImDrawFlags.None, UiMetrics.Hairline);
        }

        var textSize = ImGui.CalcTextSize(text);
        dl.AddText(min + (size - textSize) * 0.5f, textColor, text);
    }

    /// <summary>
    /// A removable filter chip (ui-revamp §2.1 chip row): a raised pill with the label in the secondary tone and a ×
    /// that brightens on hover. The whole chip is the clear button and a keyboard-focusable item; returns true on the
    /// click that clears it. Hang the tooltip on it afterwards.
    /// </summary>
    public static bool Chip(string id, string label)
    {
        // The whole chip is a click target, so it never stands under the minimum target (T14).
        var height = ChipHeightPx();
        var padX = UiMetrics.Px(ChipPadX);
        var glyph = UiMetrics.Px(ChipGlyph);
        var labelSize = ImGui.CalcTextSize(label);
        var size = new Vector2(ChipWidth(label), height);
        var pos = ImGui.GetCursorScreenPos();

        var clicked = ImGui.InvisibleButton(id, size);
        var hovered = ImGui.IsItemHovered();
        var hover = Motion.Lerp(ImGuiP.GetItemID(), hovered ? 1f : 0f);

        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var rounding = height * 0.5f;
        var max = pos + size;
        dl.AddRectFilled(pos, max, Theme.U32(Vector4.Lerp(s.Raised, s.Hover, hover)), rounding);
        dl.AddRect(pos, max, Theme.U32(s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        dl.AddText(new Vector2(pos.X + padX, pos.Y + (height - labelSize.Y) * 0.5f), Theme.U32(hovered ? s.Text : s.TextSecondary), label);

        var center = new Vector2(max.X - padX * 0.7f - glyph * 0.5f, pos.Y + height * 0.5f);
        var half = glyph * 0.5f;
        var cross = Theme.U32(hovered ? s.Text : s.TextTertiary);
        var thickness = MathF.Max(1f, UiMetrics.Px(1.5f));
        dl.AddLine(center - new Vector2(half), center + new Vector2(half), cross, thickness);
        dl.AddLine(center + new Vector2(-half, half), center + new Vector2(half, -half), cross, thickness);
        FocusRing(rounding);
        return clicked;
    }

    /// <summary>
    /// A chip that does something other than clear (the chip lane's "+N" and "Selected quest hidden · Show"): the same
    /// pill as <see cref="Chip"/> without the ×, its label in the accent tone when <paramref name="accent"/>. Returns true
    /// on click; hang the tooltip on it afterwards.
    /// </summary>
    public static bool ActionChip(string id, string label, bool accent = false)
    {
        var height = ChipHeightPx();
        var labelSize = ImGui.CalcTextSize(label);
        var size = new Vector2(ActionChipWidth(label), height);
        var pos = ImGui.GetCursorScreenPos();

        var clicked = ImGui.InvisibleButton(id, size);
        var hovered = ImGui.IsItemHovered();
        var hover = Motion.Lerp(ImGuiP.GetItemID(), hovered ? 1f : 0f);

        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var rounding = height * 0.5f;
        dl.AddRectFilled(pos, pos + size, Theme.U32(Vector4.Lerp(s.Raised, s.Hover, hover)), rounding);
        dl.AddRect(pos, pos + size, Theme.U32(s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        var ink = accent ? Theme.AccentU32 : Theme.U32(hovered ? s.Text : s.TextSecondary);
        dl.AddText(pos + ((size - labelSize) * 0.5f), ink, label);
        FocusRing(rounding);
        return clicked;
    }

    /// <summary>The width an <see cref="ActionChip"/> with this label takes at the current font.</summary>
    public static float ActionChipWidth(string label) => (2f * UiMetrics.Px(ChipPadX)) + ImGui.CalcTextSize(label).X;

    /// <summary>The width a <see cref="Chip"/> with this label takes at the current font, for callers that flow chips onto lines.</summary>
    public static float ChipWidth(string label)
    {
        var padX = UiMetrics.Px(ChipPadX);
        return padX + ImGui.CalcTextSize(label).X + padX * 0.7f + UiMetrics.Px(ChipGlyph) + padX * 0.7f;
    }

    /// <summary>The height a <see cref="Chip"/> takes at the current font, for callers that flow chips onto lines.</summary>
    public static float ChipHeightPx() =>
        ChromeBands.ChipHeight(UiMetrics.Scale, ImGui.GetTextLineHeight(), UiMetrics.MinTarget);

    /// <summary>
    /// A count badge centred on <paramref name="center"/> (draw list only): a circle, or a pill once the number is wide.
    /// <paramref name="actionable"/> (Ready counts and the like) paints it Moon with Night text; anything else is a
    /// generic badge in VeilLine with the primary text, never gold (game UX panel finding 2). Counts above 99 read "99+".
    /// </summary>
    public static void Badge(ImDrawListPtr dl, Vector2 center, int count, bool actionable) =>
        Badge(dl, center, CountStrings[Math.Clamp(count, 0, CountStrings.Length - 1)], actionable);

    /// <summary>The size <see cref="Badge(ImDrawListPtr, Vector2, int, bool)"/> draws for <paramref name="count"/> at the current font, for callers that keep it inside a box.</summary>
    public static Vector2 BadgeSize(int count)
    {
        var text = CountStrings[Math.Clamp(count, 0, CountStrings.Length - 1)];
        var (width, height) = BadgeSize(ImGui.CalcTextSize(text).X * 0.72f, ImGui.GetFontSize() * 0.72f);
        return new Vector2(width, height);
    }

    private static (float Width, float Height) BadgeSize(float textWidth, float fontSize)
    {
        var height = MathF.Max(UiMetrics.Px(BadgeMin), fontSize + UiMetrics.Px(3f));
        return (MathF.Max(height, textWidth + UiMetrics.Px(6f)), height);
    }

    /// <summary>A badge with preformatted text; see <see cref="Badge(ImDrawListPtr, Vector2, int, bool)"/>.</summary>
    public static void Badge(ImDrawListPtr dl, Vector2 center, string text, bool actionable)
    {
        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize() * 0.72f;
        var textSize = ImGui.CalcTextSize(text) * 0.72f;
        var (width, height) = BadgeSize(textSize.X, fontSize);
        var min = center - new Vector2(width, height) * 0.5f;
        var fill = actionable ? Theme.GoldU32 : Theme.U32(Theme.Surface.StrongLine);
        var ink = actionable ? Theme.OnGoldU32 : Theme.U32(Theme.Surface.Text);
        dl.AddRectFilled(min, min + new Vector2(width, height), fill, height * 0.5f);
        dl.AddText(font, fontSize, center - textSize * 0.5f, ink, text);
    }

    // ------------------------------------------------------------------ lines and surfaces

    /// <summary>
    /// A hairline as an item (it takes one line of spacing): <see cref="Theme.Surface"/>'s Line, or StrongLine when
    /// <paramref name="strong"/>. <paramref name="width"/> 0 spans the content region; auto-resizing windows pass their
    /// content width instead, or the line would hold the window at its old width.
    /// </summary>
    public static void Hairline(bool strong = false, float width = 0f)
    {
        var pos = ImGui.GetCursorScreenPos();
        width = width > 0f ? width : ImGui.GetContentRegionAvail().X;
        var thickness = UiMetrics.Hairline;
        ImGui.Dummy(new Vector2(width, thickness));
        var s = Theme.Surface;
        var y = pos.Y + thickness * 0.5f;
        ImGui.GetWindowDrawList().AddLine(new Vector2(pos.X, y), new Vector2(pos.X + width, y), Theme.U32(strong ? s.StrongLine : s.Line), thickness);
    }

    /// <summary>
    /// <see cref="Hairline"/> at the level's rule style (R3 #9, #10; flair v13): at Full a brass rule fading out to the
    /// right (<see cref="Ornament.Rule"/>, solid under high contrast), at Quiet a flat hairline, under Plain the line.
    /// One item, as tall.
    /// </summary>
    public static void Rule(float width = 0f, float alpha = Ornament.RuleAlpha)
    {
        if (Theme.RuleStyle == RuleStyle.Line)
        {
            Hairline(width: width);
            return;
        }

        var pos = ImGui.GetCursorScreenPos();
        width = width > 0f ? width : ImGui.GetContentRegionAvail().X;
        var thickness = UiMetrics.Hairline;
        ImGui.Dummy(new Vector2(width, thickness));
        Ornament.Rule(ImGui.GetWindowDrawList(), pos, width, alpha, thickness);
    }

    /// <summary>
    /// Faux elevation on a hovered row or card (ui-revamp §2.4): a 1 px light line along the top edge and a 1 px shadow
    /// along the bottom, both at low alpha, painted over whatever is there. In a light palette the shadow is softer.
    /// </summary>
    public static void Lift(ImDrawListPtr dl, Vector2 min, Vector2 max)
    {
        var thickness = UiMetrics.Hairline;
        var light = Theme.Surface.Light;
        var top = light ? Theme.WithAlpha(Vector4.One, 0.5f) : Theme.WithAlpha(Theme.Surface.Text, 0.08f);
        var bottom = light ? Theme.WithAlpha(Vector4.UnitW, 0.12f) : Theme.WithAlpha(Theme.Surface.Window, 0.6f);
        dl.AddLine(new Vector2(min.X, min.Y + thickness * 0.5f), new Vector2(max.X, min.Y + thickness * 0.5f), top, thickness);
        dl.AddLine(new Vector2(min.X, max.Y - thickness * 0.5f), new Vector2(max.X, max.Y - thickness * 0.5f), bottom, thickness);
    }

    /// <summary>
    /// A vertical gradient of the window colour from <paramref name="fromAlpha"/> at the top to <paramref name="toAlpha"/>
    /// at the bottom (the hero banner's name strip: 0 → 0.92). Fakes the blur ImGui cannot do.
    /// </summary>
    public static void Scrim(ImDrawListPtr dl, Vector2 min, Vector2 max, float fromAlpha = 0f, float toAlpha = 0.92f) =>
        Scrim(dl, min, max, Theme.Surface.Window, fromAlpha, toAlpha);

    /// <summary>
    /// Scales the alpha of every vertex drawn into <paramref name="dl"/> since vertex <paramref name="from"/> by
    /// <paramref name="alpha"/>: a fade-in of draw-list shapes, which the style's Alpha does not reach (their colours are
    /// packed by hand). Allocation-free.
    /// </summary>
    public static void FadeVertices(ImDrawListPtr dl, int from, float alpha)
    {
        var vertices = dl.VtxBuffer;
        var scale = Math.Clamp(alpha, 0f, 1f);
        for (var i = Math.Max(0, from); i < vertices.Size; i++)
        {
            var vertex = vertices[i];
            var a = (uint)MathF.Round((vertex.Col >> 24) * scale);
            vertex.Col = (vertex.Col & 0x00FFFFFFu) | (a << 24);
            vertices[i] = vertex;
        }
    }

    /// <summary>The scrim in an explicit colour.</summary>
    public static void Scrim(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 color, float fromAlpha, float toAlpha)
    {
        if (max.X <= min.X || max.Y <= min.Y)
        {
            return;
        }

        var top = Theme.WithAlpha(color, fromAlpha);
        var bottom = Theme.WithAlpha(color, toAlpha);
        dl.AddRectFilledMultiColor(min, max, top, top, bottom, bottom);
    }

    /// <summary>
    /// An image filling <paramref name="size"/> at its own aspect, the excess cropped around the focus (the centre by
    /// default; <see cref="ImageCover.Uv"/>), with rounded corners, as an item.
    /// </summary>
    public static void ImageCover(ImTextureID texture, Vector2 size, Vector2 textureSize, float rounding = 0f, float focusX = 0.5f, float focusY = 0.5f)
    {
        var min = ImGui.GetCursorScreenPos();
        ImGui.Dummy(size);
        ImageCoverAt(ImGui.GetWindowDrawList(), texture, min, min + size, textureSize, rounding, focusX, focusY);
    }

    /// <summary>The cover-cropped image painted into a rectangle without an item.</summary>
    public static void ImageCoverAt(ImDrawListPtr dl, ImTextureID texture, Vector2 min, Vector2 max, Vector2 textureSize, float rounding = 0f, float focusX = 0.5f, float focusY = 0.5f) =>
        ImageCoverAt(dl, texture, min, max, textureSize, 0xFFFFFFFFu, rounding, focusX, focusY);

    /// <summary>The cover-cropped image multiplied by <paramref name="tint"/> (a draw-time grade, packed IM_COL32).</summary>
    public static void ImageCoverAt(ImDrawListPtr dl, ImTextureID texture, Vector2 min, Vector2 max, Vector2 textureSize, uint tint, float rounding, float focusX = 0.5f, float focusY = 0.5f)
    {
        var (uv0, uv1) = Core.Ui.ImageCover.Uv(max.X - min.X, max.Y - min.Y, textureSize.X, textureSize.Y, focusX, focusY);
        dl.AddImageRounded(texture, min, max, uv0, uv1, tint, rounding);
    }

    // ------------------------------------------------------------------ controls

    /// <summary>
    /// A segmented control (ui-revamp §2.1 presets) with an explicit "All" segment first: <paramref name="selected"/> is
    /// -1 for All, otherwise the index into <paramref name="labels"/>. Height <see cref="UiMetrics.MinTarget"/>; the
    /// active segment is a neutral wash with primary text (a selection, not a call to action); a segment whose
    /// <paramref name="enabled"/> entry is false is drawn in the disabled tone and ignores clicks. Every segment is a
    /// focusable item with the focus ring, and <paramref name="tooltips"/> (index 0 for All) show on hover. Returns true
    /// on the frame the selection changed.
    /// </summary>
    public static bool SegmentedControl(string id, ref int selected, string allLabel, ReadOnlySpan<string> labels, ReadOnlySpan<bool> enabled = default, ReadOnlySpan<string> tooltips = default)
    {
        var height = UiMetrics.MinTarget;
        var padX = UiMetrics.Px(SegmentPadX);
        var origin = ImGui.GetCursorScreenPos();
        var total = SegmentWidth(allLabel, padX);
        foreach (var label in labels)
        {
            total += SegmentWidth(label, padX);
        }

        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var rounding = height * 0.5f;
        var max = origin + new Vector2(total, height);
        dl.AddRectFilled(origin, max, Theme.U32(s.Sunken), rounding);

        ImGui.PushID(id);
        var changed = false;
        var x = origin.X;
        var count = labels.Length + 1;
        for (var i = 0; i < count; i++)
        {
            var label = i == 0 ? allLabel : labels[i - 1];
            var value = i - 1;
            var width = SegmentWidth(label, padX);
            var isEnabled = i == 0 || enabled.IsEmpty || (i - 1 < enabled.Length && enabled[i - 1]);
            var active = selected == value;
            var min = new Vector2(x, origin.Y);
            var segMax = new Vector2(x + width, max.Y);

            ImGui.SetCursorScreenPos(min);
            ImGui.PushID(i);
            ImGui.BeginDisabled(!isEnabled);
            if (ImGui.InvisibleButton("##seg", new Vector2(width, height)) && !active)
            {
                selected = value;
                changed = true;
                active = true;
            }

            ImGui.EndDisabled();
            var hovered = isEnabled && ImGui.IsItemHovered();
            var hover = Motion.Lerp(ImGuiP.GetItemID(), hovered && !active ? 1f : 0f);
            var corners = i == 0 ? ImDrawFlags.RoundCornersLeft : i == count - 1 ? ImDrawFlags.RoundCornersRight : ImDrawFlags.RoundCornersNone;
            if (active)
            {
                dl.AddRectFilled(min, segMax, Theme.WithAlpha(s.Text, 0.12f), rounding, corners);
            }
            else if (hover > 0f)
            {
                dl.AddRectFilled(min, segMax, Theme.WithAlpha(s.Hover, hover), rounding, corners);
            }

            if (i > 0)
            {
                dl.AddLine(new Vector2(x, origin.Y + UiMetrics.Px(4f)), new Vector2(x, max.Y - UiMetrics.Px(4f)), Theme.U32(s.Line), UiMetrics.Hairline);
            }

            var ink = !isEnabled ? s.TextDisabled : active || hovered ? s.Text : s.TextSecondary;
            var textSize = ImGui.CalcTextSize(label);
            dl.AddText(new Vector2(x + (width - textSize.X) * 0.5f, origin.Y + (height - textSize.Y) * 0.5f), Theme.U32(ink), label);
            FocusRing(rounding);
            if (i < tooltips.Length && tooltips[i].Length > 0 && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(tooltips[i]);
            }

            ImGui.PopID();
            x += width;
        }

        ImGui.PopID();
        dl.AddRect(origin, max, Theme.U32(s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);

        // One item spanning the control, so SameLine and the tutorial see it as a whole.
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(max - origin);
        return changed;
    }

    /// <summary>The width <see cref="SegmentedControl"/> takes for these labels at the current font, for layouts that place it first.</summary>
    public static float SegmentedControlWidth(string allLabel, ReadOnlySpan<string> labels)
    {
        var padX = UiMetrics.Px(SegmentPadX);
        var total = SegmentWidth(allLabel, padX);
        foreach (var label in labels)
        {
            total += SegmentWidth(label, padX);
        }

        return total;
    }

    private static float SegmentWidth(string label, float padX) => ImGui.CalcTextSize(label).X + padX * 2f;

    /// <summary>A round button's game icon or book, logical px (spec-1.15 B2): 14, and 12 at Plain.</summary>
    private const float RoundIconLogical = 14f;

    private const float RoundIconPlainLogical = 12f;

    /// <summary>
    /// A round icon button (ui-revamp §2.1 right cluster), <see cref="UiMetrics.MinTarget"/> across so it never drops
    /// under 24 px (accessibility B4): transparent at rest, the hover fill easing in, the icon in the secondary tone
    /// turning primary on hover. <paramref name="active"/> keeps a neutral wash (an open panel). Disabled buttons still
    /// show their tooltip. Returns true when clicked. 1.15 (UI-5e): the icon may be the game's own (Flag on the map, the
    /// aethernet hop) or the Journal book (<see cref="PillIcon"/>), drawn 14 px (12 at Plain) in the colours it carries,
    /// at .45 and grey when disabled; a FontAwesome glyph keeps the ink.
    /// </summary>
    /// <param name="icon">A FontAwesome string (<see cref="Icon"/>), a game icon or the Journal book.</param>
    public static bool IconButtonRound(string id, PillIcon icon, string? tooltip = null, bool active = false, bool enabled = true)
    {
        var size = UiMetrics.MinTarget;
        var pos = ImGui.GetCursorScreenPos();
        ImGui.BeginDisabled(!enabled);
        var clicked = ImGui.InvisibleButton(id, new Vector2(size, size));
        ImGui.EndDisabled();
        var hovered = enabled && ImGui.IsItemHovered();
        var held = enabled && ImGui.IsItemActive();
        var hover = Motion.Lerp(ImGuiP.GetItemID(), hovered ? 1f : 0f);

        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var center = pos + new Vector2(size * 0.5f);
        var radius = size * 0.5f;
        if (active || held)
        {
            dl.AddCircleFilled(center, radius, Theme.WithAlpha(s.Text, held ? 0.18f : 0.12f));
        }
        else if (hover > 0f)
        {
            dl.AddCircleFilled(center, radius, Theme.WithAlpha(s.Hover, hover));
        }

        var ink = !enabled ? s.TextDisabled : hovered || active ? s.Text : s.TextSecondary;
        if (icon.Glyph is { } glyph)
        {
            ImGui.PushFont(UiBuilder.IconFont);
            var iconSize = ImGui.CalcTextSize(glyph);
            dl.AddText(center - iconSize * 0.5f, Theme.U32(ink), glyph);
            ImGui.PopFont();
        }
        else
        {
            var side = MathF.Round(UiMetrics.Px(Theme.Flair == Flair.Plain ? RoundIconPlainLogical : RoundIconLogical));
            DrawPillIcon(dl, icon, new Vector2(MathF.Round(center.X - (side * 0.5f)), MathF.Round(center.Y - (side * 0.5f))), side, Theme.U32(ink), enabled);
        }

        FocusRing(radius);
        if (tooltip is not null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(tooltip);
        }

        return clicked && enabled;
    }

    /// <summary>
    /// The keyboard focus ring on the last item (ui-revamp §5.3): drawn only while ImGui navigation is visible and the
    /// item holds focus, 1.5 px in the primary text tone, never gold. Items ImGui already rings (buttons, selectables)
    /// get its own <c>NavHighlight</c>, which the Night chrome sets to the same tone; call this after custom items.
    /// </summary>
    /// <param name="rounding">Corner rounding; negative uses the style's frame rounding.</param>
    public static void FocusRing(float rounding = -1f)
    {
        if (!ImGui.GetIO().NavVisible || !ImGui.IsItemFocused())
        {
            return;
        }

        var inset = UiMetrics.Px(1f);
        var r = rounding < 0f ? ImGui.GetStyle().FrameRounding : rounding;
        ImGui.GetWindowDrawList().AddRect(
            ImGui.GetItemRectMin() - new Vector2(inset),
            ImGui.GetItemRectMax() + new Vector2(inset),
            Theme.WithAlpha(Theme.Surface.Text, 0.9f),
            r + inset,
            ImDrawFlags.None,
            MathF.Max(1.5f, UiMetrics.Px(FocusThickness)));
    }

    // ------------------------------------------------------------------ text over game scenes

    /// <summary>
    /// Text as an item in <paramref name="color"/> over a 1 px outline in the window colour (four offsets), so it reads
    /// over snow, sand and sky through a translucent panel (accessibility A8, game UX panel finding 5).
    /// </summary>
    public static void OutlinedText(string text, Vector4 color)
    {
        OutlineAt(ImGui.GetWindowDrawList(), ImGui.GetCursorScreenPos(), text);
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    /// <summary>Outlined text painted at <paramref name="pos"/> without an item.</summary>
    public static void OutlinedTextAt(ImDrawListPtr dl, Vector2 pos, string text, uint color)
    {
        OutlineAt(dl, pos, text);
        dl.AddText(pos, color, text);
    }

    private static void OutlineAt(ImDrawListPtr dl, Vector2 pos, string text)
    {
        var o = UiMetrics.Hairline;
        var outline = Theme.OutlineU32;
        dl.AddText(pos + new Vector2(-o, 0f), outline, text);
        dl.AddText(pos + new Vector2(o, 0f), outline, text);
        dl.AddText(pos + new Vector2(0f, -o), outline, text);
        dl.AddText(pos + new Vector2(0f, o), outline, text);
    }

    // ------------------------------------------------------------------ hold to confirm

    /// <summary>
    /// A button guarded by a <see cref="ConfirmGate"/> (T7, accessibility §2.4; the Hold tier of the safety table,
    /// <see cref="SafetyRules"/>): Ctrl or Shift and a click confirm at once; otherwise the button must be held for the
    /// hold length chosen in Settings while a Moon arc, stroked along the outline, closes clockwise from the top centre
    /// over a line-tone track. Under Reduce motion the arc gives way to a countdown in the label ("Hold… (0.4 s)"). In
    /// two-click mode (hand strain) the first click turns the label to "Click again" and a second click confirms.
    /// <paramref name="label"/> must end in <see cref="HoldIdSuffix"/> so the countdown can replace it without changing
    /// the id. Returns true on the frame the action is confirmed; the button stays the last item so the caller can hang
    /// a tooltip on it (<see cref="Safety.Tooltip"/>).
    /// </summary>
    public static bool HoldButton(string label, ConfirmGate gate)
    {
        ArgumentNullException.ThrowIfNull(gate);
        var io = ImGui.GetIO();
        var reduceMotion = UiMetrics.ReduceMotion;
        var safety = Safety.Settings;
        gate.SetHoldSeconds(safety.HoldSeconds);
        var chord = io.KeyShift || io.KeyCtrl;

        // A fixed width keeps the row still when the countdown or "Click again" replaces the label.
        var width = HoldButtonWidth(label);
        if (safety.TwoClick)
        {
            var now = ImGui.GetTime();
            var awaiting = gate.AwaitingSecond(now);
            var clicked = ImGui.Button(awaiting ? ClickAgainHoldLabel : label, new Vector2(width, 0f));
            if (awaiting)
            {
                DrawHoldArc(ImGui.GetWindowDrawList(), ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.GetStyle().FrameRounding, 0.5f);
            }

            return clicked && gate.ClickTwice(chord, now);
        }

        var text = reduceMotion && gate.Holding ? Countdown[Math.Clamp(gate.RemainingTenths, 1, Countdown.Length - 1)] : label;
        ImGui.Button(text, new Vector2(width, 0f));
        var active = ImGui.IsItemActive();

        var confirmed = gate.Update(chord, active, io.DeltaTime);
        if (!reduceMotion && gate.Holding)
        {
            DrawHoldArc(ImGui.GetWindowDrawList(), ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.GetStyle().FrameRounding, gate.Progress);
        }

        return confirmed;
    }

    /// <summary>The width <see cref="HoldButton"/> takes for <paramref name="label"/> (its countdown and "Click again" fit in it), so a caller can right-align it.</summary>
    public static float HoldButtonWidth(string label)
    {
        var longest = MathF.Max(ImGui.CalcTextSize(Countdown[^1], true, -1f).X, ImGui.CalcTextSize(ClickAgainHoldLabel, true, -1f).X);
        return MathF.Max(ImGui.CalcTextSize(label, true, -1f).X, longest) + (ImGui.GetStyle().FramePadding.X * 2f);
    }

    /// <summary>"Click again" with the hold id, so the label swap keeps the button's id.</summary>
    private static string ClickAgainHoldLabel => clickAgainHoldText.Value;

    private static readonly Tsukimichi.Localization.LocText clickAgainHoldText = new(static () => Strings.SafetyClickAgain + HoldIdSuffix);

    /// <summary>
    /// The hold track (the full outline) and the Moon arc over the first <paramref name="fraction"/> of it, walked
    /// clockwise from the top centre: straight edges as lines, rounded corners as quarter arcs.
    /// </summary>
    public static void DrawHoldArc(ImDrawListPtr dl, Vector2 itemMin, Vector2 itemMax, float frameRounding, float fraction)
    {
        var outset = UiMetrics.Px(HoldOutset);
        var thickness = MathF.Max(1.5f, UiMetrics.Px(HoldThickness));
        var min = itemMin - new Vector2(outset, outset);
        var max = itemMax + new Vector2(outset, outset);
        var w = max.X - min.X;
        var h = max.Y - min.Y;
        if (w <= 0f || h <= 0f)
        {
            return;
        }

        var r = MathF.Max(0f, MathF.Min(frameRounding + outset, MathF.Min(w, h) * 0.5f));
        dl.AddRect(min, max, Theme.WithAlpha(Theme.Surface.StrongLine, 0.9f), r, ImDrawFlags.RoundCornersAll, thickness);
        if (fraction <= 0f)
        {
            return;
        }

        var quarter = MathF.PI * 0.5f;
        var arcLength = quarter * r;
        var total = 2f * (w + h) - 8f * r + 4f * arcLength;
        var budget = Math.Clamp(fraction, 0f, 1f) * total;
        var segments = Math.Max(3, (int)MathF.Ceiling(r / 3f));

        var topCentre = new Vector2(min.X + w * 0.5f, min.Y);
        dl.PathClear();
        dl.PathLineTo(topCentre);

        // Each step returns false once the budget ran out inside it, which ends the walk.
        if (Edge(dl, topCentre, new Vector2(max.X - r, min.Y), ref budget)
            && Corner(dl, new Vector2(max.X - r, min.Y + r), r, -quarter, arcLength, segments, ref budget)
            && Edge(dl, new Vector2(max.X, min.Y + r), new Vector2(max.X, max.Y - r), ref budget)
            && Corner(dl, new Vector2(max.X - r, max.Y - r), r, 0f, arcLength, segments, ref budget)
            && Edge(dl, new Vector2(max.X - r, max.Y), new Vector2(min.X + r, max.Y), ref budget)
            && Corner(dl, new Vector2(min.X + r, max.Y - r), r, quarter, arcLength, segments, ref budget)
            && Edge(dl, new Vector2(min.X, max.Y - r), new Vector2(min.X, min.Y + r), ref budget)
            && Corner(dl, new Vector2(min.X + r, min.Y + r), r, 2f * quarter, arcLength, segments, ref budget))
        {
            Edge(dl, new Vector2(min.X + r, min.Y), topCentre, ref budget);
        }

        dl.PathStroke(Theme.GoldU32, ImDrawFlags.None, thickness);
    }

    /// <summary>Adds a straight run up to the budget; false once the budget ran out inside it.</summary>
    private static bool Edge(ImDrawListPtr dl, Vector2 from, Vector2 to, ref float budget)
    {
        var length = Vector2.Distance(from, to);
        if (length <= 0f)
        {
            return true;
        }

        if (budget >= length)
        {
            dl.PathLineTo(to);
            budget -= length;
            return true;
        }

        dl.PathLineTo(Vector2.Lerp(from, to, budget / length));
        budget = 0f;
        return false;
    }

    /// <summary>Adds a quarter arc (or the part of it the budget allows); false once the budget ran out inside it.</summary>
    private static bool Corner(ImDrawListPtr dl, Vector2 centre, float radius, float startAngle, float arcLength, int segments, ref float budget)
    {
        if (radius <= 0f || arcLength <= 0f)
        {
            return true;
        }

        var quarter = MathF.PI * 0.5f;
        if (budget >= arcLength)
        {
            dl.PathArcTo(centre, radius, startAngle, startAngle + quarter, segments);
            budget -= arcLength;
            return true;
        }

        dl.PathArcTo(centre, radius, startAngle, startAngle + quarter * (budget / arcLength), segments);
        budget = 0f;
        return false;
    }

    private static string[] BuildCountdown()
    {
        // Every hold Settings offers, up to the longest, has its labels.
        var labels = new string[SafetyRules.MaxHoldTenths + 1];
        labels[0] = string.Empty;
        for (var i = 1; i < labels.Length; i++)
        {
            labels[i] = string.Format(CultureInfo.InvariantCulture, Strings.VerdictHoldCountdownFormat, i / 10f) + HoldIdSuffix;
        }

        return labels;
    }

    private static string[] BuildCounts()
    {
        var counts = new string[101];
        for (var i = 0; i < 100; i++)
        {
            counts[i] = i.ToString(CultureInfo.CurrentCulture);
        }

        counts[100] = "99+";
        return counts;
    }
}
