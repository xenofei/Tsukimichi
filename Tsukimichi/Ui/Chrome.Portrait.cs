using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Tsukimichi.Core.Portraits;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The giver portrait plate (1.15 design spec A4–A7): every portrait, avatar and fallback on the same plate, drawn per
/// Decoration level. Full: the medal's well (a vertical gradient), the face clipped to its circle, from 32 px the lip
/// shadow and the moonlight wash (one baked sprite), then a 1 px brass keyline lit from the upper left with an Abyss
/// ring outside it. Quiet: the same well and face, a silver hairline. Plain: a flat well and a 1 px line. The face is the
/// night-graded copy (<see cref="PortraitGrading"/>), or the source under the night multiply until the copy lands; a
/// delivery portrait shows only through its keep mask, its fallback until then. Nothing allocates per frame.
/// </summary>
public static partial class Chrome
{
    private static readonly Vector4 WellTop = ColorMath.FromHex(0x1D2B5A);
    private static readonly Vector4 WellFoot = ColorMath.FromHex(0x131C40);
    private static readonly Vector4 PlainWell = ColorMath.FromHex(0x1C2237);
    private static readonly Vector4 PlainKeyline = ColorMath.FromHex(0x3A4050);
    private static readonly Vector4 QuietKeyline = ColorMath.FromHex(0xC3CBDF);
    private static readonly Vector4 InitialsInk = ColorMath.FromHex(PortraitPlate.InitialsHex);

    // The brass keyline's stops along its light (spec A4: lit upper left), from (8, 6) to (64, 68) in the 72-unit box.
    private static readonly Vector4 BrassLit = ColorMath.FromHex(0xE6CF98);
    private static readonly Vector4 BrassMid = ColorMath.FromHex(0x9A7E4A);
    private static readonly Vector4 BrassLow = ColorMath.FromHex(0x7C6236);
    private static readonly Vector4 BrassDark = ColorMath.FromHex(0x5C4724);

    /// <summary>
    /// Draws the plate for <paramref name="request"/> at <paramref name="min"/>, <paramref name="size"/> px across, on
    /// <paramref name="dl"/> (no item: the caller lays out and hovers its own). The face is drawn at
    /// <paramref name="faceAlpha"/> (the Giver card's fade); the plate and any fallback never fade. Returns whether the
    /// face could be drawn this frame (its texture had loaded), so the card can start its fade on arrival; false when a
    /// fallback shows.
    /// </summary>
    public static bool Portrait(ImDrawListPtr dl, Vector2 min, float size, in PortraitRequest request, float faceAlpha = 1f)
    {
        if (!(size > 1f))
        {
            return false;
        }

        var flair = Theme.Flair;
        var logical = size / MathF.Max(0.01f, UiMetrics.Scale);
        var center = min + new Vector2(size * 0.5f);
        var unit = size / PortraitPlate.Units;

        // The well.
        if (flair == Flair.Plain)
        {
            dl.AddCircleFilled(center, PortraitPlate.WellRadius * unit, Theme.U32(PlainWell));
        }
        else
        {
            GradientCircle(dl, center, PortraitPlate.WellRadius * unit, WellTop, WellFoot);
        }

        var portrait = request.Portrait;
        var show = PortraitPlate.Choose(portrait, request.FaceAllowed, logical);
        var faceDrawn = false;
        if (show == PortraitShow.Face)
        {
            faceDrawn = ResolveFace(portrait, size, logical, faceAlpha, out var image, out var graded, out var fallbackUnder);
            if (fallbackUnder != PortraitShow.Face)
            {
                DrawFallback(dl, min, size, logical, fallbackUnder, portrait.Fallback, faceDrawn ? 1f - faceAlpha : 1f);
            }

            if (faceDrawn && image is not null)
            {
                var (faceMin, faceMax) = PortraitPlate.FaceRect(min, size);
                var rounding = (faceMax.X - faceMin.X) * 0.5f;
                if (graded)
                {
                    dl.AddImageRounded(image.Handle, faceMin, faceMax, Vector2.Zero, Vector2.One, Theme.U32(Vector4.One with { W = faceAlpha }), rounding);
                }
                else
                {
                    dl.AddImageRounded(image.Handle, faceMin, faceMax, portrait.Crop.Uv0, portrait.Crop.Uv1, PortraitGrading.TintFor(portrait.Source, faceAlpha), rounding);
                }
            }
        }
        else
        {
            DrawFallback(dl, min, size, logical, show, portrait.Fallback, 1f);
        }

        // Full's lip shadow and moonlight wash, over the face, from 32 px.
        if (flair == Flair.Full && logical >= PortraitPlate.ShadeMinLogical)
        {
            PortraitAtlas.TryDraw(dl, PortraitSprite.PlateShade, min, min + new Vector2(size));
        }

        // The keyline.
        var hairline = UiMetrics.Hairline;
        switch (flair)
        {
            case Flair.Full when !Theme.Glyphs.HighContrast:
                dl.AddCircle(center, PortraitPlate.OuterRingRadius * unit, Theme.WithAlpha(Theme.Abyss, PortraitPlate.OuterRingAlpha), 0, hairline);
                BrassRing(dl, min, size, PortraitPlate.KeylineRadius * unit, hairline);
                break;
            case Flair.Full or Flair.Quiet:
                dl.AddCircle(center, PortraitPlate.KeylineRadius * unit, Theme.WithAlpha(QuietKeyline, PortraitPlate.QuietKeylineAlpha), 0, hairline);
                break;
            default:
                dl.AddCircle(center, PortraitPlate.KeylineRadius * unit, Theme.U32(PlainKeyline), 0, hairline);
                break;
        }

        return faceDrawn;
    }

    /// <summary>
    /// The hover tooltip of a plate (spec A5): the plate at <see cref="PortraitPlate.TooltipSize"/>, then the giver's
    /// name in the Title role, the source line ("Portrait: Triple Triad card art") when the face is drawn (not while a
    /// fallback stands in for it), and the place.
    /// In the level's tooltip frame. Call while the plate's item is hovered.
    /// </summary>
    public static void PortraitTooltip(in PortraitRequest request, string name, string? place)
    {
        using var tooltip = Theme.Tooltip();
        UiMetrics.ApplyFontScale();
        var face = request.ShowsFace(PortraitPlate.TooltipMax);
        var plate = UiMetrics.Px(PortraitPlate.TooltipSize(request.Portrait, face));
        var width = MathF.Max(plate, UiMetrics.Px(PortraitTooltipMinLogical));
        var left = ImGui.GetCursorScreenPos().X;

        var at = new Vector2(left + ((width - plate) * 0.5f), ImGui.GetCursorScreenPos().Y);
        ImGui.Dummy(new Vector2(width, plate));
        var drawn = Portrait(ImGui.GetWindowDrawList(), at, plate, request) && face;
        ImGui.Dummy(new Vector2(1f, UiMetrics.Px(4f)));

        using (Typography.Title(name))
        {
            CenteredLine(name, left, width, Theme.Surface.Text);
        }

        if (drawn)
        {
            CenteredLine(Strings.PortraitSource(request.Portrait.Source), left, width, Theme.Surface.TextTertiary);
        }

        if (!string.IsNullOrEmpty(place))
        {
            CenteredLine(place, left, width, Theme.Surface.TextSecondary);
        }
    }

    /// <summary>The tooltip's least content width, logical px, so a small plate's lines still read.</summary>
    private const float PortraitTooltipMinLogical = 172f;

    /// <summary>One line centred in <paramref name="width"/> from <paramref name="left"/>, ellipsised when longer.</summary>
    private static void CenteredLine(string text, float left, float width, Vector4 ink)
    {
        var textWidth = ImGui.CalcTextSize(text).X;
        var pos = new Vector2(left + MathF.Max(0f, (width - textWidth) * 0.5f), ImGui.GetCursorScreenPos().Y);
        ImGui.Dummy(new Vector2(width, ImGui.GetTextLineHeight()));
        EllipsisTextAt(ImGui.GetWindowDrawList(), pos, width, text, Theme.U32(ink), textWidth);
    }

    /// <summary>
    /// The face to draw: the graded copy when it is ready (<paramref name="graded"/>), else the source to draw under the
    /// night tint; a delivery portrait only through its keep mask. <paramref name="fallbackUnder"/> is the fallback to draw
    /// instead of (or, while the face fades in, under) the face: a delivery portrait whose keyed copy has not landed, an
    /// icon the game does not have; else <see cref="PortraitShow.Face"/> (nothing under it). Returns whether there is a
    /// face to draw this frame.
    /// </summary>
    private static bool ResolveFace(in PortraitRef portrait, float size, float logical, float alpha, out IDalamudTextureWrap? image, out bool graded, out PortraitShow fallbackUnder)
    {
        image = null;
        graded = false;
        fallbackUnder = PortraitShow.Face;
        var textures = Plugin.TextureProvider;
        var delivery = portrait.Mask is not null;
        var fallback = PortraitPlate.Fallback(portrait.Fallback.Kind, portrait.Fallback, logical);
        if (textures is null || !textures.TryGetFromGameIcon(new GameIconLookup(portrait.Icon, false, true), out var shared))
        {
            // The game has no such icon (a patch dropped it): the fallback, as for a giver without art.
            fallbackUnder = fallback;
            return false;
        }

        if (!shared.TryGetWrap(out var source, out _))
        {
            // Loading: the bare well (the face fades in on arrival); a delivery portrait's fallback, never its unkeyed art.
            fallbackUnder = delivery ? fallback : PortraitShow.Face;
            return false;
        }

        var state = PortraitGrading.TryGet(source, portrait, PortraitPlate.UseSmallCopy(size), out var copy);
        if (state == PortraitGrading.State.Ready && copy is not null)
        {
            // A keyed face replaces the fallback the plate showed while it was made.
            fallbackUnder = delivery && alpha < 1f ? fallback : PortraitShow.Face;
            image = copy;
            graded = true;
            return true;
        }

        if (delivery)
        {
            fallbackUnder = fallback;
            return false;
        }

        image = source;
        return true;
    }

    /// <summary>A fallback on the plate (spec A6) at <paramref name="alpha"/>.</summary>
    private static void DrawFallback(ImDrawListPtr dl, Vector2 min, float size, float logical, PortraitShow show, in PortraitFallback fallback, float alpha)
    {
        if (!(alpha > 0.004f))
        {
            return;
        }

        var tint = Theme.U32(Vector4.One with { W = alpha });
        var max = min + new Vector2(size);
        switch (show)
        {
            case PortraitShow.SocietyEmblem:
                if (!DrawEmblem(dl, min, size, logical, fallback.SocietyIcon, alpha))
                {
                    PortraitAtlas.TryDraw(dl, PortraitSprite.MoonDisc, min, max, tint);
                }

                break;
            case PortraitShow.Silhouette:
                PortraitAtlas.TryDraw(dl, PortraitAtlasLayout.Silhouette(fallback.Race, fallback.Gender), min, max, tint);
                break;
            case PortraitShow.Initials:
                DrawInitials(dl, min, size, logical, fallback.Initials, alpha);
                break;
            default:
                PortraitAtlas.TryDraw(dl, PortraitSprite.MoonDisc, min, max, tint);
                break;
        }
    }

    /// <summary>The society's emblem as a square tile at 64 %, ungraded, with a soft down-right shadow from 32 px; false while it loads or when the game has none.</summary>
    private static bool DrawEmblem(ImDrawListPtr dl, Vector2 min, float size, float logical, uint icon, float alpha)
    {
        var textures = Plugin.TextureProvider;
        var (tileMin, tileMax) = PortraitPlate.EmblemRect(min, size);
        if (textures is null || !GameIcon.TryGetWrap(textures, icon, tileMax.X - tileMin.X, out var wrap))
        {
            return false;
        }

        if (logical >= PortraitPlate.ShadeMinLogical)
        {
            // The shadow as a tinted copy, offset down-right; two copies a quarter-pixel apart soften its edge.
            var offset = PortraitPlate.EmblemShadowOffset * UiMetrics.Scale;
            var shadow = Theme.WithAlpha(Theme.Abyss, PortraitPlate.EmblemShadowAlpha * 0.5f * alpha);
            var soft = new Vector2(UiMetrics.Px(0.5f));
            dl.AddImage(wrap.Handle, tileMin + offset, tileMax + offset, Vector2.Zero, Vector2.One, shadow);
            dl.AddImage(wrap.Handle, tileMin + offset + soft, tileMax + offset + soft, Vector2.Zero, Vector2.One, shadow);
        }

        dl.AddImage(wrap.Handle, tileMin, tileMax, Vector2.Zero, Vector2.One, Theme.U32(Vector4.One with { W = PortraitPlate.EmblemAlpha * alpha }));
        return true;
    }

    /// <summary>One or two initials in the Title face (Jupiter), centred; the moon disc when there is no room for 9 px caps.</summary>
    private static void DrawInitials(ImDrawListPtr dl, Vector2 min, float size, float logical, string initials, float alpha)
    {
        var letters = PortraitPlate.InitialLetters(initials, logical);
        if (letters == 0)
        {
            PortraitAtlas.TryDraw(dl, PortraitSprite.MoonDisc, min, min + new Vector2(size), Theme.U32(Vector4.One with { W = alpha }));
            return;
        }

        var text = initials.AsSpan(0, letters);
        using var role = Typography.Title(text);
        var px = UiMetrics.Px(PortraitPlate.InitialsFontLogical(letters, logical));
        var measured = ImGui.CalcTextSize(text) * (px / MathF.Max(1f, ImGui.GetFontSize()));
        var center = min + new Vector2(size * 0.5f);

        // A text box carries the descender below the caps: nudge down a little so the capitals sit on the centre.
        var pos = new Vector2(MathF.Round(center.X - (measured.X * 0.5f)), MathF.Round(center.Y - (measured.Y * 0.5f) + (px * 0.04f)));
        dl.AddText(ImGui.GetFont(), px, pos, Theme.U32(InitialsInk with { W = PortraitPlate.InitialsAlpha * alpha }), text);
    }

    /// <summary>A filled circle shaded top to bottom from <paramref name="top"/> to <paramref name="foot"/> (the medal's well).</summary>
    private static void GradientCircle(ImDrawListPtr dl, Vector2 center, float radius, Vector4 top, Vector4 foot)
    {
        var first = dl.VtxBuffer.Size;
        dl.AddCircleFilled(center, radius, 0xFFFFFFFFu);
        var vertices = dl.VtxBuffer;
        var span = MathF.Max(1f, radius * 2f);
        for (var i = first; i < vertices.Size; i++)
        {
            var vertex = vertices[i];
            var c = Vector4.Lerp(top, foot, Math.Clamp((vertex.Pos.Y - (center.Y - radius)) / span, 0f, 1f));
            c.W *= (vertex.Col >> 24) / 255f;
            vertex.Col = Theme.U32(c);
            vertices[i] = vertex;
        }
    }

    /// <summary>
    /// The brass keyline (spec A4, a per-vertex mesh like <see cref="Ornament.BrassBorder"/>): a 1 px ring lit from the
    /// upper left, #E6CF98 → #9A7E4A → #7C6236 → #5C4724 along the line from (8, 6) to (64, 68) of the 72-unit box.
    /// </summary>
    private static void BrassRing(ImDrawListPtr dl, Vector2 min, float size, float radius, float thickness)
    {
        var first = dl.VtxBuffer.Size;
        dl.AddCircle(min + new Vector2(size * 0.5f), radius, 0xFFFFFFFFu, 0, thickness);
        var vertices = dl.VtxBuffer;
        var unit = size / PortraitPlate.Units;
        var from = min + (new Vector2(8f, 6f) * unit);
        var dir = new Vector2(56f, 62f) * unit;
        var length = MathF.Max(1f, dir.LengthSquared());
        for (var i = first; i < vertices.Size; i++)
        {
            var vertex = vertices[i];
            var t = Math.Clamp(Vector2.Dot(vertex.Pos - from, dir) / length, 0f, 1f);
            var c = t < 0.45f ? Vector4.Lerp(BrassLit, BrassMid, t / 0.45f)
                : t < 0.8f ? Vector4.Lerp(BrassMid, BrassLow, (t - 0.45f) / 0.35f)
                : Vector4.Lerp(BrassLow, BrassDark, (t - 0.8f) / 0.2f);
            c.W = (vertex.Col >> 24) / 255f;
            vertex.Col = Theme.U32(c);
            vertices[i] = vertex;
        }
    }
}
