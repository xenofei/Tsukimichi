using System.Numerics;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// The job badge's rules (concept.md, "Job-badge frame spec" and round 5 §1): the game icon for a job, the seat its
/// role takes, and where the icon's glyph optically sits in its own canvas, so the badge can centre it on the seat.
/// </summary>
public static class JobBadgeRules
{
    /// <summary>The unframed gold job glyphs: 062000 + ClassJob row (Paladin 062019, Bard 062023, White Mage 062024).</summary>
    public const uint JobGlyphIconBase = 62000;

    /// <summary>The icon side (device px) from which the badge reads the hi-res (<c>_hr1</c>) texture.</summary>
    public const float HiResFromPx = 28f;

    /// <summary>
    /// The glyph offset used until a job's icon has been measured, as a fraction of the icon's side: the game's job
    /// glyphs all sit high in their canvases (round 5 measured Paladin, Bard and White Mage at +2.6 to +3.7 units of a
    /// 35.5-unit slot), so the default lowers the glyph by their mean.
    /// </summary>
    public static readonly Vector2 DefaultOffset = new(0.010f, -0.085f);

    /// <summary>The icon of <paramref name="classJob"/>'s glyph; 0 for none.</summary>
    public static uint IconFor(byte classJob) => classJob == 0 ? 0u : JobGlyphIconBase + classJob;

    /// <summary>
    /// The seat for a job by its ClassJob sheet role (1 tank, 4 healer, 2 and 3 DPS) and whether it is a Disciple of the
    /// Hand or Land, which have no combat role.
    /// </summary>
    public static JobSeat SeatFor(byte role, bool handOrLand) =>
        handOrLand ? JobSeat.Hand : role switch
        {
            1 => JobSeat.Tank,
            4 => JobSeat.Healer,
            2 or 3 => JobSeat.Dps,
            _ => JobSeat.Hand,
        };

    /// <summary>
    /// Where the glyph's optical centre sits relative to the canvas centre, as a fraction of the canvas side (gen5
    /// <c>job_optical_offset</c>): from the pixels whose alpha is above one half (so a soft glow is ignored), the mean
    /// of the bounding box's centre and the alpha-weighted centroid. The badge draws the icon shifted by minus this.
    /// <paramref name="pixels"/> is 4 bytes a pixel with alpha at <paramref name="alphaIndex"/> (3 for RGBA and BGRA);
    /// a blank canvas gives <see cref="DefaultOffset"/>.
    /// </summary>
    public static Vector2 OpticalOffset(ReadOnlySpan<byte> pixels, int width, int height, int stride, int alphaIndex = 3)
    {
        if (width <= 0 || height <= 0 || stride < width * 4 || pixels.Length < stride * (height - 1) + width * 4)
        {
            return DefaultOffset;
        }

        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        double sum = 0, sx = 0, sy = 0;
        for (var y = 0; y < height; y++)
        {
            var row = pixels.Slice(y * stride, width * 4);
            for (var x = 0; x < width; x++)
            {
                var a = row[x * 4 + alphaIndex];
                if (a <= 127)
                {
                    continue;
                }

                var w = a / 255.0;
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
                sum += w;
                sx += x * w;
                sy += y * w;
            }
        }

        if (maxX < 0 || sum <= 0)
        {
            return DefaultOffset;
        }

        var bx = (minX + maxX) / 2.0;
        var by = (minY + maxY) / 2.0;
        var cx = (width - 1) / 2.0;
        var cy = (height - 1) / 2.0;
        return new Vector2((float)(((bx + sx / sum) / 2 - cx) / width), (float)(((by + sy / sum) / 2 - cy) / height));
    }
}
