using System.Numerics;

namespace Tsukimichi.Core.Portraits;

/// <summary>
/// The face window of a portrait texture, in texture coordinates (0 to 1, left/top to right/bottom), resolution
/// independent so the same crop fits the normal and the <c>_hr1</c> texture. <c>AddImageRounded</c> takes it as
/// <see cref="Uv0"/> / <see cref="Uv1"/>, so no new texture is made. The windows are close to square; the drawing
/// code fits them into its circle (<see cref="Ui.ImageCover"/> over the window if it is not exactly square).
/// </summary>
public readonly record struct PortraitCrop(float U0, float V0, float U1, float V1)
{
    /// <summary>The whole texture.</summary>
    public static readonly PortraitCrop Full = new(0f, 0f, 1f, 1f);

    public Vector2 Uv0 => new(U0, V0);

    public Vector2 Uv1 => new(U1, V1);

    /// <summary>Every coordinate finite and within 0–1, and the window not empty.</summary>
    public bool IsValid =>
        float.IsFinite(U0) && float.IsFinite(V0) && float.IsFinite(U1) && float.IsFinite(V1)
        && U0 >= 0f && V0 >= 0f && U1 <= 1f && V1 <= 1f && U0 < U1 && V0 < V1;

    /// <summary>The window's aspect (width over height) on a <paramref name="textureWidth"/> × <paramref name="textureHeight"/> texture.</summary>
    public float Aspect(float textureWidth, float textureHeight) =>
        textureHeight > 0f && V1 > V0 ? (U1 - U0) * textureWidth / ((V1 - V0) * textureHeight) : 1f;

    /// <summary>
    /// A square box as the design spec and the curated file write it: <paramref name="x"/>, <paramref name="y"/> and
    /// <paramref name="side"/> in px of <paramref name="source"/>'s hr texture (<see cref="PortraitSources.TextureSize"/>).
    /// Invalid (outside the texture, or an unknown family) when the box does not fit.
    /// </summary>
    public static PortraitCrop FromBox(PortraitSource source, float x, float y, float side)
    {
        var (width, height) = PortraitSources.TextureSize(source);
        return width == 0 || !(side > 0f)
            ? default
            : new PortraitCrop(x / width, y / height, (x + side) / width, (y + side) / height);
    }

    /// <summary>The box in hr px of <paramref name="source"/>'s texture: (x, y, side), the side taken across.</summary>
    public (float X, float Y, float Side) ToBox(PortraitSource source)
    {
        var (width, height) = PortraitSources.TextureSize(source);
        return (U0 * width, V0 * height, (U1 - U0) * width);
    }
}

/// <summary>
/// A face measured on its texture, in texture coordinates: the midpoint between the eyes and the chin's height. The
/// crop follows from the framing rule (<see cref="PortraitFraming.CropFor(PortraitSource, PortraitLandmarks)"/>), so
/// every face moves with the rule when the rule is retuned.
/// </summary>
public readonly record struct PortraitLandmarks(float EyeU, float EyeV, float ChinV);

/// <summary>
/// One crop per portrait family, tunable without code: <see cref="Defaults"/> holds the family boxes, and the curated
/// file's <c>crops</c> object replaces any of them (<see cref="PortraitCuration.Crops"/>). A single icon carries its own
/// box on top (<see cref="PortraitCuration.IconCrops"/>), which for the top givers is the normal case: compositions vary
/// within a family.
/// </summary>
public sealed class PortraitCrops
{
    /// <summary>
    /// The family default boxes of the 1.15 design spec (<c>docs/design/v7/ui/spec-1.15.md</c> A2.2), square, in hr
    /// source px (x, y, side), each meeting the framing rule for a typical face of its family:
    /// <list type="bullet">
    /// <item>Trust bust (188 × 480): 6, 86, 140;</item>
    /// <item>battle-talk face (640 × 512): 164, 154, 172;</item>
    /// <item>Triple Triad card (208 × 256): 28, 20, 135;</item>
    /// <item>delivery portrait (400 × 480): 104, 154, 158, drawn through its keep mask (<see cref="PortraitMask"/>);</item>
    /// <item>Trust strip (640 × 180, not in the spec): the face at the strip's right end, measured with the DataGen
    /// contact sheet and framed by the rule.</item>
    /// </list>
    /// </summary>
    public static readonly IReadOnlyDictionary<PortraitSource, PortraitCrop> Defaults = new Dictionary<PortraitSource, PortraitCrop>
    {
        [PortraitSource.TrustBust] = PortraitCrop.FromBox(PortraitSource.TrustBust, 6, 86, 140),
        [PortraitSource.BattleTalk] = PortraitCrop.FromBox(PortraitSource.BattleTalk, 164, 154, 172),
        [PortraitSource.TripleTriadCard] = PortraitCrop.FromBox(PortraitSource.TripleTriadCard, 28, 20, 135),
        [PortraitSource.Delivery] = PortraitCrop.FromBox(PortraitSource.Delivery, 104, 154, 158),
        [PortraitSource.TrustStrip] = PortraitFraming.CropFor(PortraitSource.TrustStrip, new PortraitLandmarks(0.78f, 0.46f, 0.8f)),
    };

    /// <summary>The defaults with nothing replaced.</summary>
    public static readonly PortraitCrops Default = new(null, null);

    private readonly Dictionary<PortraitSource, PortraitCrop> bySource;
    private readonly IReadOnlyDictionary<uint, PortraitCrop> byIcon;

    /// <param name="sourceOverrides">Family crops replacing the defaults; invalid ones are ignored.</param>
    /// <param name="iconOverrides">Crops for single icons, ahead of their family's; invalid ones are ignored.</param>
    public PortraitCrops(IReadOnlyDictionary<PortraitSource, PortraitCrop>? sourceOverrides, IReadOnlyDictionary<uint, PortraitCrop>? iconOverrides)
    {
        bySource = new Dictionary<PortraitSource, PortraitCrop>(Defaults);
        foreach (var (source, crop) in sourceOverrides ?? new Dictionary<PortraitSource, PortraitCrop>())
        {
            if (source != PortraitSource.None && crop.IsValid)
            {
                bySource[source] = crop;
            }
        }

        byIcon = iconOverrides?.Where(kv => kv.Value.IsValid).ToDictionary(kv => kv.Key, kv => kv.Value) ?? new Dictionary<uint, PortraitCrop>();
    }

    /// <summary>The family's crop; the whole texture for <see cref="PortraitSource.None"/>.</summary>
    public PortraitCrop For(PortraitSource source) => bySource.GetValueOrDefault(source, PortraitCrop.Full);

    /// <summary>The icon's own crop when it has one, else its family's.</summary>
    public PortraitCrop For(PortraitSource source, uint icon) => byIcon.TryGetValue(icon, out var crop) ? crop : For(source);
}

/// <summary>
/// The one framing rule every portrait crop follows (1.15 design review), as fractions of the plate's height from the
/// top: the crown, eye line and chin bands, and the face (brow to chin) as a share of the circle's diameter. The DataGen
/// contact sheet draws these as guides over every crop, so each crop is checked against the same lines.
/// </summary>
public static class PortraitFraming
{
    public const float CrownMin = 0.08f;
    public const float CrownMax = 0.12f;
    public const float EyeLineMin = 0.42f;
    public const float EyeLineMax = 0.46f;
    public const float ChinMin = 0.78f;
    public const float ChinMax = 0.84f;

    /// <summary>Brow to chin, as a share of the circle's diameter.</summary>
    public const float FaceShare = 0.55f;

    /// <summary>Where <see cref="CropFor(float, float, float, float, float, PortraitCrop?)"/> puts the eyes: the middle of the eye-line band.</summary>
    public const float EyeLine = (EyeLineMin + EyeLineMax) / 2f;

    /// <summary>Where the crop puts the chin: the middle of the chin band.</summary>
    public const float Chin = (ChinMin + ChinMax) / 2f;

    /// <summary>
    /// The crop for a face of <paramref name="source"/>'s family, on its texture size and inside its art
    /// (<see cref="PortraitSources.ArtBounds"/>: a card's gold frame stays out). <see cref="PortraitCrop.Full"/> for
    /// <see cref="PortraitSource.None"/> or a face that cannot be framed.
    /// </summary>
    public static PortraitCrop CropFor(PortraitSource source, PortraitLandmarks face)
    {
        var (width, height) = PortraitSources.TextureSize(source);
        return CropFor(face.EyeU, face.EyeV, face.ChinV, width, height, PortraitSources.ArtBounds(source));
    }

    /// <summary>
    /// The square crop that frames a face by the rule: the eyes' midpoint (<paramref name="eyeU"/>,
    /// <paramref name="eyeV"/>) centred across and on the eye line, the chin (<paramref name="chinV"/>) on the chin line,
    /// all in texture coordinates of a <paramref name="textureWidth"/> × <paramref name="textureHeight"/> texture. A
    /// plate larger than <paramref name="bounds"/> (the whole texture when null) shrinks to fit, and one past an edge
    /// slides back inside (the face then sits off the guides rather than the crop showing what is not art). Invalid
    /// input yields <see cref="PortraitCrop.Full"/>.
    /// </summary>
    public static PortraitCrop CropFor(float eyeU, float eyeV, float chinV, float textureWidth, float textureHeight, PortraitCrop? bounds = null)
    {
        var area = bounds is { IsValid: true } inside ? inside : PortraitCrop.Full;
        if (!(textureWidth > 0f) || !(textureHeight > 0f) || !float.IsFinite(eyeU) || !float.IsFinite(eyeV) || !float.IsFinite(chinV)
            || !(chinV > eyeV) || eyeU < 0f || eyeU > 1f || eyeV < 0f || chinV > 1f)
        {
            return PortraitCrop.Full;
        }

        float minX = area.U0 * textureWidth, maxX = area.U1 * textureWidth, minY = area.V0 * textureHeight, maxY = area.V1 * textureHeight;
        var size = Math.Min((chinV - eyeV) * textureHeight / (Chin - EyeLine), Math.Min(maxX - minX, maxY - minY));
        var left = Math.Clamp((eyeU * textureWidth) - (size / 2f), minX, maxX - size);
        var top = Math.Clamp((eyeV * textureHeight) - (EyeLine * size), minY, maxY - size);
        return new PortraitCrop(left / textureWidth, top / textureHeight, (left + size) / textureWidth, (top + size) / textureHeight);
    }
}

