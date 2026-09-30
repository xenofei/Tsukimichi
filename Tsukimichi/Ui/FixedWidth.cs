using System;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// A fixed table column's width measured from its header and its widest cell (feature plan v4 L6), kept until the font,
/// its size, the UI language or the rows change, so the measuring runs once rather than every frame: a width guessed in
/// pixels clipped "3123/5402" and the "Completed" of a state cell mid-glyph. Never below the content. Hold one per column
/// as a field; before the table's setup, <see cref="Stale"/> says whether to measure again, and <see cref="Fit"/> and
/// <see cref="Store"/> keep the result.
/// </summary>
internal sealed class FixedWidth
{
    private nint font;
    private float size = -1f;
    private int language = -1;
    private object? rows;
    private int stamp;
    private float width;

    /// <summary>The kept width, in pixels.</summary>
    public float Value => width;

    /// <summary>
    /// Whether the width must be measured again: the font, its size or the UI language changed, or the rows did
    /// (<paramref name="source"/> is the rows' owner, compared by reference, and <paramref name="version"/> any number
    /// the caller bumps when they change in place).
    /// </summary>
    public bool Stale(object? source, int version = 0) =>
        !ReferenceEquals(source, rows) || version != stamp || language != Loc.Version || size != ImGui.GetFontSize() || font != FontId();

    /// <summary>Keeps <paramref name="measured"/> for these rows, font and language; returns it.</summary>
    public float Store(object? source, float measured, int version = 0)
    {
        rows = source;
        stamp = version;
        language = Loc.Version;
        size = ImGui.GetFontSize();
        font = FontId();
        width = measured;
        return measured;
    }

    /// <summary>The widest of <paramref name="current"/> and <paramref name="text"/>'s width in the current font.</summary>
    public static float Fit(float current, string text) => MathF.Max(current, ImGui.CalcTextSize(text).X);

    private static unsafe nint FontId() => (nint)ImGui.GetFont().Handle;
}
