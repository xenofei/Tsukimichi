using System;

namespace Tsukimichi.Ui;

/// <summary>
/// A fixed slot's text that changes in place (a status segment, a reserved status strip): when the text changes the
/// new one fades in by alpha alone over <see cref="Seconds"/>, so nothing slides or reflows (feature plan v6, U4). With
/// motion off, or for the slot's very first text, it is simply there.
/// </summary>
internal sealed class TextFade
{
    /// <summary>How long a new text takes to fade in.</summary>
    public const float Seconds = 0.18f;

    private const uint Tag = 0x5446_4144; // "TFAD"

    private static uint nextId;

    private readonly ulong key = Motion.Key(Tag, ++nextId);
    private string? shown;
    private bool seen;

    /// <summary>The alpha 0..1 to draw <paramref name="text"/> at this frame; a change from last frame's text starts the fade.</summary>
    public float Alpha(string? text)
    {
        if (!string.Equals(text, shown, StringComparison.Ordinal))
        {
            shown = text;
            if (seen && text is not null)
            {
                Motion.Trigger(key);
            }
        }

        seen = true;

        var progress = Motion.Pulse(key, Seconds);
        return progress < 0f ? 1f : Math.Clamp(progress, 0f, 1f);
    }
}
