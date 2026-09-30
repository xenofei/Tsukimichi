using System;
using System.Numerics;

namespace Tsukimichi.Core.Ui;

/// <summary>A colour-vision simulation (accessibility panel Appendix C).</summary>
public enum ColorVision
{
    /// <summary>Colours as drawn.</summary>
    None = 0,

    /// <summary>Achromatopsia, a greyscale stream or a Windows greyscale filter: WCAG relative luminance as a neutral grey.</summary>
    Greyscale = 1,

    /// <summary>Deuteranopia (green-blind), Machado 2009 at severity 1.0.</summary>
    Deuteranopia = 2,

    /// <summary>Protanopia (red-blind), Machado 2009 at severity 1.0.</summary>
    Protanopia = 3,

    /// <summary>Tritanopia (blue-blind), Machado 2009 at severity 1.0.</summary>
    Tritanopia = 4,
}

/// <summary>
/// Colour-vision-deficiency simulation: Machado, Oliveira and Fernandes, "A Physiologically-based Model for Simulation
/// of Color Vision Deficiency" (IEEE TVCG 2009) at severity 1.0, applied to linearised sRGB and re-encoded; greyscale
/// is the WCAG relative luminance mapped to a neutral grey (docs/review/panel/accessibility.md Appendix C). The glyph
/// window re-colours its draw list through it, and the glyph palette tests check that the states stay apart under it.
/// </summary>
public static class ColorVisionSimulation
{
    /// <summary>Every simulation, in the order the glyph window lists them.</summary>
    public static readonly ColorVision[] All = [ColorVision.None, ColorVision.Greyscale, ColorVision.Deuteranopia, ColorVision.Protanopia, ColorVision.Tritanopia];

    /// <summary>The deficiencies only (no <see cref="ColorVision.None"/>).</summary>
    public static readonly ColorVision[] Deficiencies = [ColorVision.Greyscale, ColorVision.Deuteranopia, ColorVision.Protanopia, ColorVision.Tritanopia];

    // Machado 2009, severity 1.0, row-major, applied to linear RGB.
    private static readonly float[] Deuteranopia = [0.367322f, 0.860646f, -0.227968f, 0.280085f, 0.672501f, 0.047413f, -0.011820f, 0.042940f, 0.968881f];
    private static readonly float[] Protanopia = [0.152286f, 1.052583f, -0.204868f, 0.114503f, 0.786281f, 0.099216f, -0.003882f, -0.048116f, 1.051998f];
    private static readonly float[] Tritanopia = [1.255528f, -0.076749f, -0.178779f, -0.078411f, 0.930809f, 0.147602f, 0.004733f, 0.691367f, 0.303900f];

    /// <summary>The name the glyph window shows.</summary>
    public static string Name(ColorVision mode) => mode switch
    {
        ColorVision.None => "None",
        ColorVision.Greyscale => "Greyscale",
        ColorVision.Deuteranopia => "Deuteranopia",
        ColorVision.Protanopia => "Protanopia",
        ColorVision.Tritanopia => "Tritanopia",
        _ => mode.ToString(),
    };

    /// <summary>The colour as a viewer with the given deficiency sees it; alpha is untouched.</summary>
    public static Vector4 Simulate(Vector4 color, ColorVision mode)
    {
        if (mode == ColorVision.None)
        {
            return color;
        }

        var r = ToLinear(color.X);
        var g = ToLinear(color.Y);
        var b = ToLinear(color.Z);

        float r2, g2, b2;
        switch (mode)
        {
            case ColorVision.Greyscale:
                r2 = g2 = b2 = 0.2126f * r + 0.7152f * g + 0.0722f * b;
                break;
            case ColorVision.Deuteranopia:
                (r2, g2, b2) = Multiply(Deuteranopia, r, g, b);
                break;
            case ColorVision.Protanopia:
                (r2, g2, b2) = Multiply(Protanopia, r, g, b);
                break;
            case ColorVision.Tritanopia:
                (r2, g2, b2) = Multiply(Tritanopia, r, g, b);
                break;
            default:
                return color;
        }

        return new Vector4(ToSrgb(r2), ToSrgb(g2), ToSrgb(b2), color.W);
    }

    /// <summary>sRGB channel (0..1) to linear light.</summary>
    public static float ToLinear(float channel)
    {
        channel = float.IsFinite(channel) ? Math.Clamp(channel, 0f, 1f) : 0f;
        return channel <= 0.04045f ? channel / 12.92f : MathF.Pow((channel + 0.055f) / 1.055f, 2.4f);
    }

    /// <summary>Linear light to an sRGB channel (0..1), clamped.</summary>
    public static float ToSrgb(float linear)
    {
        linear = float.IsFinite(linear) ? Math.Clamp(linear, 0f, 1f) : 0f;
        return linear <= 0.0031308f ? 12.92f * linear : 1.055f * MathF.Pow(linear, 1f / 2.4f) - 0.055f;
    }

    private static (float R, float G, float B) Multiply(float[] m, float r, float g, float b) => (
        m[0] * r + m[1] * g + m[2] * b,
        m[3] * r + m[4] * g + m[5] * b,
        m[6] * r + m[7] * g + m[8] * b);
}
