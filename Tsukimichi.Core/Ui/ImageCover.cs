using System;
using System.Numerics;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// Texture coordinates for an image shown "object-fit: cover" in a box: the image keeps its own aspect, fills the
/// box, and the excess is cropped around a focus point (the centre unless told otherwise). <c>AddImageRounded</c>
/// only takes <c>uv0 / uv1</c>, so the crop has to be computed by the caller (ui-revamp §6.3); <c>Chrome.ImageCover</c>
/// draws with these.
/// </summary>
public static class ImageCover
{
    /// <summary>
    /// The visible window of a <paramref name="textureWidth"/> × <paramref name="textureHeight"/> image in a
    /// <paramref name="boxWidth"/> × <paramref name="boxHeight"/> box. The trimmed axis keeps the part around
    /// <paramref name="focusX"/> / <paramref name="focusY"/> (0 = left / top, 1 = right / bottom, clamped; a non-finite
    /// focus reads as the centre). Non-positive or non-finite sizes yield the full image.
    /// </summary>
    public static (Vector2 Uv0, Vector2 Uv1) Uv(float boxWidth, float boxHeight, float textureWidth, float textureHeight, float focusX = 0.5f, float focusY = 0.5f)
    {
        if (!(boxWidth > 0f) || !(boxHeight > 0f) || !(textureWidth > 0f) || !(textureHeight > 0f)
            || !float.IsFinite(boxWidth) || !float.IsFinite(boxHeight) || !float.IsFinite(textureWidth) || !float.IsFinite(textureHeight))
        {
            return (Vector2.Zero, Vector2.One);
        }

        var boxAspect = boxWidth / boxHeight;
        var textureAspect = textureWidth / textureHeight;
        if (textureAspect > boxAspect)
        {
            // Image wider than the box: keep the full height, trim the sides.
            var visible = boxAspect / textureAspect;
            var start = (1f - visible) * Focus(focusX);
            return (new Vector2(start, 0f), new Vector2(start + visible, 1f));
        }

        if (textureAspect < boxAspect)
        {
            // Image taller than the box: keep the full width, trim top and bottom.
            var visible = textureAspect / boxAspect;
            var start = (1f - visible) * Focus(focusY);
            return (new Vector2(0f, start), new Vector2(1f, start + visible));
        }

        return (Vector2.Zero, Vector2.One);
    }

    private static float Focus(float focus) => float.IsFinite(focus) ? Math.Clamp(focus, 0f, 1f) : 0.5f;
}
