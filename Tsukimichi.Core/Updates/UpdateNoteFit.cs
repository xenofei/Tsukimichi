namespace Tsukimichi.Core.Updates;

/// <summary>
/// How the status bar's "Tsukimichi 1.23.0 is ready" note (spec-1.22 U1, "The status-bar note") fits a narrow main
/// window: it never runs into the version. Its words end in an ellipsis down to <see cref="MinTextLogical"/>; below
/// that the note is left out (Update stays in Settings › About and on the moon icon's card). The note's dot, Update and
/// × always keep their size, so nothing in it moves. Pure.
/// </summary>
public static class UpdateNoteFit
{
    /// <summary>The fewest logical px of words the note keeps before it is left out.</summary>
    public const float MinTextLogical = 48f;

    /// <summary>The room for the note's words, or a negative number when the note is left out.</summary>
    /// <param name="room">The room between what comes before the note and the gap before the version.</param>
    /// <param name="fixedWidth">The note without its words: the gap before it, its padding, the dot, Update and ×.</param>
    /// <param name="textWidth">Its words' full width.</param>
    /// <param name="scale">Px per logical px.</param>
    public static float TextRoom(float room, float fixedWidth, float textWidth, float scale)
    {
        if (!float.IsFinite(room) || !float.IsFinite(fixedWidth) || !float.IsFinite(textWidth))
        {
            return -1f;
        }

        var words = MathF.Max(0f, textWidth);
        var left = room - MathF.Max(0f, fixedWidth);
        if (left >= words)
        {
            return words;
        }

        return left >= MathF.Min(words, MinTextLogical * MathF.Max(0f, scale)) ? MathF.Floor(left) : -1f;
    }
}
