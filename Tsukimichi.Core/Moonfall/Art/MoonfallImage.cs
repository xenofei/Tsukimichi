namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>
/// One float channel of a picture, row-major (<see cref="Width"/> × <see cref="Height"/>): a mask, a lightness or one
/// colour channel. The scene and chrome grades (<see cref="MoonfallGrade"/>, <see cref="MoonfallSceneBuilder"/>) work
/// on these off the framework thread, as the design's Python does on numpy arrays (docs/design/v9/rich2/src).
/// </summary>
public sealed class MoonfallPlane
{
    public MoonfallPlane(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        Width = width;
        Height = height;
        Data = new float[width * height];
    }

    public MoonfallPlane(int width, int height, float[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        if (data.Length != width * height)
        {
            throw new ArgumentException("The data is not width × height long.", nameof(data));
        }

        Width = width;
        Height = height;
        Data = data;
    }

    public int Width { get; }

    public int Height { get; }

    public float[] Data { get; }

    public float this[int x, int y]
    {
        get => Data[(y * Width) + x];
        set => Data[(y * Width) + x] = value;
    }

    /// <summary>The value at (x, y), the nearest edge pixel outside the plane.</summary>
    public float Clamped(int x, int y) => Data[(Math.Clamp(y, 0, Height - 1) * Width) + Math.Clamp(x, 0, Width - 1)];

    /// <summary>Bilinear sample at pixel coordinates (pixel centres at +0.5), clamped at the edges.</summary>
    public float Sample(float x, float y)
    {
        x -= 0.5f;
        y -= 0.5f;
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var fx = x - x0;
        var fy = y - y0;
        var a = Clamped(x0, y0) + ((Clamped(x0 + 1, y0) - Clamped(x0, y0)) * fx);
        var b = Clamped(x0, y0 + 1) + ((Clamped(x0 + 1, y0 + 1) - Clamped(x0, y0 + 1)) * fx);
        return a + ((b - a) * fy);
    }

    public MoonfallPlane Copy() => new(Width, Height, (float[])Data.Clone());

    /// <summary>A plane of the same size filled with <paramref name="value"/>.</summary>
    public static MoonfallPlane Filled(int width, int height, float value)
    {
        var plane = new MoonfallPlane(width, height);
        Array.Fill(plane.Data, value);
        return plane;
    }
}

/// <summary>
/// A picture as three (or four) float planes in sRGB 0..1, straight alpha: the working form of every grade. Converts
/// from and to the BGRA bytes a game texture decodes to and the RGBA bytes a texture is made from.
/// </summary>
public sealed class MoonfallImage
{
    public MoonfallImage(int width, int height, bool alpha = false)
    {
        R = new MoonfallPlane(width, height);
        G = new MoonfallPlane(width, height);
        B = new MoonfallPlane(width, height);
        A = alpha ? MoonfallPlane.Filled(width, height, 1f) : null;
    }

    public MoonfallImage(MoonfallPlane r, MoonfallPlane g, MoonfallPlane b, MoonfallPlane? a = null)
    {
        ArgumentNullException.ThrowIfNull(r);
        ArgumentNullException.ThrowIfNull(g);
        ArgumentNullException.ThrowIfNull(b);
        if (g.Width != r.Width || b.Width != r.Width || g.Height != r.Height || b.Height != r.Height
            || (a is not null && (a.Width != r.Width || a.Height != r.Height)))
        {
            throw new ArgumentException("The planes differ in size.");
        }

        R = r;
        G = g;
        B = b;
        A = a;
    }

    public int Width => R.Width;

    public int Height => R.Height;

    public MoonfallPlane R { get; }

    public MoonfallPlane G { get; }

    public MoonfallPlane B { get; }

    /// <summary>Straight alpha, or null for an opaque picture.</summary>
    public MoonfallPlane? A { get; set; }

    public MoonfallImage Copy() => new(R.Copy(), G.Copy(), B.Copy(), A?.Copy());

    /// <summary>A picture from 8-bit pixels, BGRA (a decoded game texture) or RGBA (a PNG), rows <paramref name="pitch"/> bytes apart.</summary>
    public static MoonfallImage FromBytes(ReadOnlySpan<byte> pixels, int width, int height, int pitch, bool bgra, bool keepAlpha)
    {
        if (pitch < width * 4 || pixels.Length < (pitch * (height - 1)) + (width * 4))
        {
            throw new ArgumentException("The pixels are shorter than width × height.", nameof(pixels));
        }

        var image = new MoonfallImage(width, height, keepAlpha);
        var (ri, bi) = bgra ? (2, 0) : (0, 2);
        const float Inv = 1f / 255f;
        for (var y = 0; y < height; y++)
        {
            var row = pixels.Slice(y * pitch, width * 4);
            var o = y * width;
            for (var x = 0; x < width; x++)
            {
                var p = x * 4;
                image.R.Data[o + x] = row[p + ri] * Inv;
                image.G.Data[o + x] = row[p + 1] * Inv;
                image.B.Data[o + x] = row[p + bi] * Inv;
                if (image.A is { } a)
                {
                    a.Data[o + x] = row[p + 3] * Inv;
                }
            }
        }

        return image;
    }

    /// <summary>The picture as RGBA bytes (alpha 255 when opaque), the form a texture is made from.</summary>
    public byte[] ToRgba()
    {
        var bytes = new byte[Width * Height * 4];
        var n = Width * Height;
        for (var i = 0; i < n; i++)
        {
            bytes[(i * 4) + 0] = ToByte(R.Data[i]);
            bytes[(i * 4) + 1] = ToByte(G.Data[i]);
            bytes[(i * 4) + 2] = ToByte(B.Data[i]);
            bytes[(i * 4) + 3] = A is { } a ? ToByte(a.Data[i]) : (byte)255;
        }

        return bytes;
    }

    /// <summary>A rectangle of the picture (in pixels), as a new picture.</summary>
    public MoonfallImage Crop(int x0, int y0, int width, int height)
    {
        if (x0 < 0 || y0 < 0 || width < 1 || height < 1 || x0 + width > Width || y0 + height > Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x0), "The crop is outside the picture.");
        }

        var out_ = new MoonfallImage(width, height, A is not null);
        for (var y = 0; y < height; y++)
        {
            Array.Copy(R.Data, ((y0 + y) * Width) + x0, out_.R.Data, y * width, width);
            Array.Copy(G.Data, ((y0 + y) * Width) + x0, out_.G.Data, y * width, width);
            Array.Copy(B.Data, ((y0 + y) * Width) + x0, out_.B.Data, y * width, width);
            if (A is { } a)
            {
                Array.Copy(a.Data, ((y0 + y) * Width) + x0, out_.A!.Data, y * width, width);
            }
        }

        return out_;
    }

    /// <summary>The picture mirrored left to right.</summary>
    public MoonfallImage MirrorX()
    {
        var out_ = new MoonfallImage(Width, Height, A is not null);
        foreach (var (from, to) in Pairs(out_))
        {
            for (var y = 0; y < Height; y++)
            {
                var o = y * Width;
                for (var x = 0; x < Width; x++)
                {
                    to.Data[o + x] = from.Data[o + Width - 1 - x];
                }
            }
        }

        return out_;
    }

    /// <summary>The picture mirrored top to bottom.</summary>
    public MoonfallImage MirrorY()
    {
        var out_ = new MoonfallImage(Width, Height, A is not null);
        foreach (var (from, to) in Pairs(out_))
        {
            for (var y = 0; y < Height; y++)
            {
                Array.Copy(from.Data, (Height - 1 - y) * Width, to.Data, y * Width, Width);
            }
        }

        return out_;
    }

    private IEnumerable<(MoonfallPlane From, MoonfallPlane To)> Pairs(MoonfallImage other)
    {
        yield return (R, other.R);
        yield return (G, other.G);
        yield return (B, other.B);
        if (A is { } a && other.A is { } b)
        {
            yield return (a, b);
        }
    }

    /// <summary>A 0..1 value as a byte, rounded (as the design's <c>to_u8</c>: × 255 + 0.5).</summary>
    public static byte ToByte(float v) => (byte)Math.Clamp((int)((v * 255f) + 0.5f), 0, 255);
}
