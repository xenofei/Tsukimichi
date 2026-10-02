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
    private const float ChipHeight = 22f;
    private const float ChipPadX = 9f;
    private const float ChipGlyph = 8f;
    private const float SegmentPadX = Core.Ui.LayoutBudgets.SegmentPadLogical;
    private const float BadgeMin = 14f;
    private const float FocusThickness = 1.5f;
    private const float HoldOutset = 2f;
    private const float HoldThickness = 2f;

    private static readonly Dictionary<FontAwesomeIcon, string> IconStrings = [];
    private static readonly string[] CountStrings = BuildCounts();

    // Countdown labels under Reduce motion, index = tenths of a second left (1..6 for the default hold).
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
    /// The title in the Eyebrow role (TrumpGothic, moon-road proposal §4) rather than the caption role, with the icon at
    /// caption size centred on it; the caption role still stands in while game heading fonts are off.
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
            EyebrowTitle(title, icon);
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
    /// A card title in the Eyebrow role: the icon at caption size in the secondary tone, centred on the title's line,
    /// then the title in the primary tone. The title's item is the last, so a caption placed from its rect lines up.
    /// When the eyebrow falls back to the caption role both are one size and this draws as the caption title does.
    /// </summary>
    private static void EyebrowTitle(string title, string? icon)
    {
        var y = ImGui.GetCursorPosY();
        float line;
        using (Typography.Eyebrow(title))
        {
            line = ImGui.GetTextLineHeight();
        }

        if (icon is not null)
        {
            using var caption = Typography.Caption();
            ImGui.SetCursorPosY(y + MathF.Max(0f, (line - ImGui.GetTextLineHeight()) * 0.5f));
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.Surface.TextSecondary);
            Typography.Icon(icon);
            ImGui.PopStyleColor();
            ImGui.SameLine(0f, UiMetrics.Px(6f));
            ImGui.SetCursorPosY(y);
        }

        using var role = Typography.Eyebrow(title);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Surface.Text);
        ImGui.TextUnformatted(title);
        ImGui.PopStyleColor();
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
                CardBorder(dl, cardStart, max, rounding);
                break;
            default:
                dl.AddRectFilled(cardStart, max, Theme.U32(s.Raised), rounding);
                CardBorder(dl, cardStart, max, rounding);
                break;
        }

        dl.ChannelsMerge();
        ImGui.PopID();
        ImGui.SetCursorScreenPos(cardStart);
        ImGui.Dummy(new Vector2(cardWidth, max.Y - cardStart.Y));
    }

    /// <summary>
    /// A card's border at the frame's flair (R3 #8, <see cref="FlairRules.Card"/>): the palette's hairline under Plain;
    /// otherwise brass (<see cref="SurfaceColors.Ornament"/> at <see cref="CardBrassAlpha"/>, opaque VeilLine under the
    /// high-contrast palette) and, at Full, a corner mark in each corner of the first framed card of each window this
    /// frame: corner marks frame one moment per pane, never every box (proposal P4), so a stack of cards reads as one
    /// brass-cornered card and its brass-edged neighbours. Allocation-free.
    /// </summary>
    public static void CardBorder(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding)
    {
        var frame = FlairRules.Card(Theme.Flair);
        if (frame == CardFrame.Hairline)
        {
            dl.AddRect(min, max, Theme.U32(Theme.Surface.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
            return;
        }

        dl.AddRect(min, max, BrassU32(), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        if (frame == CardFrame.BrassCorners && ClaimCorners())
        {
            CardCorners(dl, min, max);
        }
    }

    /// <summary>
    /// The card frame for a bordered child window drawn as a card (What's new, Since you were away, Set up your road):
    /// call first thing inside the child and dispose the result when its content ends. Under Plain it draws nothing and
    /// the child keeps its own border; otherwise it paints <see cref="CardBorder"/> around the whole child, outside its
    /// clip rect, under the content, and the cards inside it keep to brass edges. Push <see cref="CardChildBorder"/> as
    /// the child's border colour so the two do not double up: the returned scope gives the content the palette's border
    /// colour back (frames, popups) until it is disposed.
    /// </summary>
    public static Theme.StyleScope CardFrameInWindow()
    {
        if (FlairRules.Card(Theme.Flair) == CardFrame.Hairline)
        {
            return default;
        }

        var min = ImGui.GetWindowPos();
        var max = min + ImGui.GetWindowSize();
        var dl = ImGui.GetWindowDrawList();
        dl.PushClipRect(min, max, false);
        CardBorder(dl, min, max, ImGui.GetStyle().ChildRounding);
        dl.PopClipRect();
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.Surface.Line);
        return new Theme.StyleScope(1, 0);
    }

    /// <summary>
    /// The border colour for a child window framed by <see cref="CardFrameInWindow"/>: clear while the brass frame
    /// draws, else the palette's line (what <see cref="Theme.PushNightPanel"/> pushes).
    /// </summary>
    public static Vector4 CardChildBorder => FlairRules.Card(Theme.Flair) == CardFrame.Hairline ? Theme.Surface.Line : Vector4.Zero;

    /// <summary>The brass of a card's border (Gilt at 0.55 on Night), before the high-contrast palette makes it opaque.</summary>
    public const float CardBrassAlpha = 0.55f;

    private const float CardCornerLogical = 9f;
    private const float CardCornerInsetLogical = 2f;

    // The windows whose one corner-marked card is drawn this frame (a few per frame; more simply go without).
    private static readonly uint[] CornerWindows = new uint[16];
    private static int cornerCount;
    private static int cornerFrame = -1;

    /// <summary>Whether the current window may still draw a corner-marked card this frame; records that it did.</summary>
    private static bool ClaimCorners()
    {
        var frame = ImGui.GetFrameCount();
        if (frame != cornerFrame)
        {
            cornerFrame = frame;
            cornerCount = 0;
        }

        var window = ImGuiP.GetCurrentWindow().ID;
        for (var i = 0; i < cornerCount; i++)
        {
            if (CornerWindows[i] == window)
            {
                return false;
            }
        }

        if (cornerCount == CornerWindows.Length)
        {
            return false;
        }

        CornerWindows[cornerCount++] = window;
        return true;
    }

    private static uint BrassU32() => Theme.WithAlpha(Theme.Surface.Ornament, Theme.OrnamentAlpha(CardBrassAlpha));

    private static void CardCorners(ImDrawListPtr dl, Vector2 min, Vector2 max)
    {
        var size = MathF.Round(UiMetrics.Px(CardCornerLogical));
        var inset = MathF.Round(UiMetrics.Px(CardCornerInsetLogical));

        // Room for four marks and a gap between them, or none: a card shorter than that stays a plain brass box.
        if (max.X - min.X < (size + inset) * 2f + size || max.Y - min.Y < (size + inset) * 2f)
        {
            return;
        }

        // The atlas mark is brass already, so it is drawn untinted; its line fallback takes the border's brass.
        OrnamentAtlas.Corners(dl, min, max, size, inset, OrnamentAtlas.IsReady ? Theme.WithAlpha(Vector4.One, 0.9f) : Theme.OrnamentU32);
    }

    private static Vector2 Inset()
    {
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

    /// <summary>The width a <see cref="Chip"/> with this label takes at the current font, for callers that flow chips onto lines.</summary>
    public static float ChipWidth(string label)
    {
        var padX = UiMetrics.Px(ChipPadX);
        return padX + ImGui.CalcTextSize(label).X + padX * 0.7f + UiMetrics.Px(ChipGlyph) + padX * 0.7f;
    }

    /// <summary>The height a <see cref="Chip"/> takes at the current font, for callers that flow chips onto lines.</summary>
    public static float ChipHeightPx() =>
        MathF.Max(MathF.Max(UiMetrics.Px(ChipHeight), ImGui.GetTextLineHeight() + UiMetrics.Px(4f)), UiMetrics.MinTarget);

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
        var fill = actionable ? Theme.MoonU32 : Theme.U32(Theme.Surface.StrongLine);
        var ink = actionable ? Theme.NightU32 : Theme.U32(Theme.Surface.Text);
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
    /// <see cref="Hairline"/> in the Moon Road style (R3 #9, #10): at Flair Full and Quiet a brass rule fading out to
    /// the right (<see cref="Ornament.Rule"/>, solid under high contrast), under Plain the hairline. One item, as tall.
    /// </summary>
    public static void Rule(float width = 0f, float alpha = Ornament.RuleAlpha)
    {
        if (!Theme.ShowRules)
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
        var top = light ? Theme.WithAlpha(Vector4.One, 0.5f) : Theme.WithAlpha(Theme.Silver, 0.08f);
        var bottom = light ? Theme.WithAlpha(Vector4.UnitW, 0.12f) : Theme.WithAlpha(Theme.Night, 0.6f);
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
    public static void ImageCoverAt(ImDrawListPtr dl, ImTextureID texture, Vector2 min, Vector2 max, Vector2 textureSize, float rounding = 0f, float focusX = 0.5f, float focusY = 0.5f)
    {
        var (uv0, uv1) = Core.Ui.ImageCover.Uv(max.X - min.X, max.Y - min.Y, textureSize.X, textureSize.Y, focusX, focusY);
        dl.AddImageRounded(texture, min, max, uv0, uv1, 0xFFFFFFFFu, rounding);
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

    /// <summary>
    /// A round icon button (ui-revamp §2.1 right cluster), <see cref="UiMetrics.MinTarget"/> across so it never drops
    /// under 24 px (accessibility B4): transparent at rest, the hover fill easing in, the icon in the secondary tone
    /// turning primary on hover. <paramref name="active"/> keeps a neutral wash (an open panel). Disabled buttons still
    /// show their tooltip. Returns true when clicked.
    /// </summary>
    /// <param name="icon">A FontAwesome string (<see cref="Icon"/>).</param>
    public static bool IconButtonRound(string id, string icon, string? tooltip = null, bool active = false, bool enabled = true)
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
        ImGui.PushFont(UiBuilder.IconFont);
        var iconSize = ImGui.CalcTextSize(icon);
        dl.AddText(center - iconSize * 0.5f, Theme.U32(ink), icon);
        ImGui.PopFont();

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
    /// A button guarded by a <see cref="ConfirmGate"/> (T7, accessibility §2.4): Shift and a click confirm at once;
    /// otherwise the button must be held for the gate's duration while a Moon arc, stroked along the outline, closes
    /// clockwise from the top centre over a line-tone track. Under Reduce motion the arc gives way to a countdown in the
    /// label ("Hold… (0.4 s)"). <paramref name="label"/> must end in <see cref="HoldIdSuffix"/> so the countdown can
    /// replace it without changing the id. Returns true on the frame the action is confirmed; the button stays the last
    /// item so the caller can hang a tooltip on it.
    /// </summary>
    public static bool HoldButton(string label, ConfirmGate gate)
    {
        ArgumentNullException.ThrowIfNull(gate);
        var io = ImGui.GetIO();
        var reduceMotion = UiMetrics.ReduceMotion;

        // A fixed width keeps the row still when the countdown text replaces the label.
        var padding = ImGui.GetStyle().FramePadding;
        var longest = Countdown[^1];
        var width = MathF.Max(ImGui.CalcTextSize(label, true, -1f).X, ImGui.CalcTextSize(longest, true, -1f).X) + padding.X * 2f;
        var text = reduceMotion && gate.Holding ? Countdown[Math.Clamp(gate.RemainingTenths, 1, Countdown.Length - 1)] : label;
        ImGui.Button(text, new Vector2(width, 0f));
        var active = ImGui.IsItemActive();

        var confirmed = gate.Update(io.KeyShift, active, io.DeltaTime);
        if (!reduceMotion && gate.Holding)
        {
            DrawHoldArc(ImGui.GetWindowDrawList(), ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.GetStyle().FrameRounding, gate.Progress);
        }

        return confirmed;
    }

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

        dl.PathStroke(Theme.MoonU32, ImDrawFlags.None, thickness);
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
        var gate = new ConfirmGate();
        var labels = new string[gate.HoldTenths + 1];
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
