using Tsukimichi.Core.Evaluation;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// Pure layout rules of the quest table's rows (feature plan v3 T15): the state moon sized from the row height, and
/// the Status cell's fit, where the state word is never cut (P1, game UX panel finding 3) and only the reason after
/// it gives way to an ellipsis.
/// </summary>
public static class TableGeometry
{
    /// <summary>Moon radius per pixel of row content: r 9 in a Comfortable row (28 px of content), r 6.4 in a Dense one (20 px), ui-revamp §2.4.</summary>
    public const float GlyphRadiusPerContent = 0.32f;

    /// <summary>Side of the square a row moon is centred in, per unit of radius (room for the Ready glow).</summary>
    public const float GlyphBoxPerRadius = 2.4f;

    /// <summary>
    /// The row moon's radius for a row with <paramref name="rowContent"/> pixels of content: a share of the row, never
    /// under <paramref name="minRadius"/> (the icon-scaled radius). The box it needs (radius × <see cref="GlyphBoxPerRadius"/>)
    /// always fits a row whose content already holds the minimum moon.
    /// </summary>
    public static float GlyphRadius(float rowContent, float minRadius)
    {
        var floor = float.IsFinite(minRadius) ? MathF.Max(0f, minRadius) : 0f;
        var content = float.IsFinite(rowContent) ? MathF.Max(0f, rowContent) : 0f;
        return MathF.Max(floor, content * GlyphRadiusPerContent);
    }

    /// <summary>
    /// Length of the state word at the start of a status line (<see cref="BlockerText.StatusText"/>): everything before
    /// the first <see cref="BlockerText.Separator"/>, or the whole line when it has no reason.
    /// </summary>
    public static int StateWordLength(string? status)
    {
        if (string.IsNullOrEmpty(status))
        {
            return 0;
        }

        var split = status.IndexOf(BlockerText.Separator, StringComparison.Ordinal);
        return split <= 0 ? status.Length : split;
    }

    /// <summary>
    /// Width the reason may take after the state word in a Status cell <paramref name="cellWidth"/> wide: whatever the
    /// state word leaves, never negative. The state word itself always keeps <paramref name="stateWidth"/>; when the
    /// reason is wider than this it is ellipsised, the state word never (P1).
    /// </summary>
    public static float ReasonWidth(float cellWidth, float stateWidth)
    {
        if (!float.IsFinite(cellWidth) || !float.IsFinite(stateWidth))
        {
            return 0f;
        }

        return MathF.Max(0f, cellWidth - MathF.Max(0f, stateWidth));
    }

    /// <summary>Whether a reason <paramref name="reasonWidth"/> wide must be ellipsised to fit <paramref name="room"/>.</summary>
    public static bool ReasonNeedsEllipsis(float reasonWidth, float room) => reasonWidth > room + 0.5f;
}
