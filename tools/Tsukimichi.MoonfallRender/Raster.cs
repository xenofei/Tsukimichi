using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Tsukimichi.MoonfallRender;

/// <summary>An RGBA texture the rasterizer samples (straight alpha), by its ImGui texture id.</summary>
internal sealed record Texture(int Width, int Height, byte[] Rgba);

/// <summary>
/// A small software rasterizer for ImGui's draw data, as Dalamud's DX11 backend draws it: indexed triangles with
/// per-vertex colour and texture coordinates, the texture sampled bilinearly (clamped), the vertex colour multiplying it,
/// blended "over" with straight alpha in sRGB, each command clipped to its rectangle. ImGui feathers its own edges, so no
/// further anti-aliasing is applied; pixels are sampled at their centres with a top-left rule.
/// </summary>
internal sealed class Raster
{
    private readonly float[] px;

    public Raster(int width, int height)
    {
        Width = width;
        Height = height;
        px = new float[width * height * 3];
    }

    public int Width { get; }

    public int Height { get; }

    public Dictionary<ulong, Texture> Textures { get; } = [];

    public void Clear(Vector3 colour)
    {
        for (var i = 0; i < Width * Height; i++)
        {
            px[i * 3] = colour.X;
            px[(i * 3) + 1] = colour.Y;
            px[(i * 3) + 2] = colour.Z;
        }
    }

    public unsafe void Draw(ImDrawDataPtr data)
    {
        for (var l = 0; l < data.CmdListsCount; l++)
        {
            ImDrawListPtr list = data.CmdLists[l];
            var vtx = list.VtxBuffer;
            var idx = list.IdxBuffer;
            for (var c = 0; c < list.CmdBuffer.Size; c++)
            {
                var cmd = list.CmdBuffer[c];
                if (cmd.ElemCount == 0)
                {
                    continue;
                }

                Textures.TryGetValue(cmd.TextureId.Handle, out var tex);
                var clip = cmd.ClipRect;
                for (var e = 0; e < cmd.ElemCount; e += 3)
                {
                    var a = vtx[(int)(cmd.VtxOffset + idx[(int)(cmd.IdxOffset + e)])];
                    var b = vtx[(int)(cmd.VtxOffset + idx[(int)(cmd.IdxOffset + e + 1)])];
                    var d = vtx[(int)(cmd.VtxOffset + idx[(int)(cmd.IdxOffset + e + 2)])];
                    Triangle(a, b, d, tex, clip);
                }
            }
        }
    }

    private void Triangle(in ImDrawVert a, in ImDrawVert b, in ImDrawVert c, Texture? tex, Vector4 clip)
    {
        var area = Edge(a.Pos, b.Pos, c.Pos);
        if (MathF.Abs(area) < 1e-6f)
        {
            return;
        }

        var minX = Math.Max((int)MathF.Floor(MathF.Min(a.Pos.X, MathF.Min(b.Pos.X, c.Pos.X))), Math.Max(0, (int)MathF.Floor(clip.X)));
        var maxX = Math.Min((int)MathF.Ceiling(MathF.Max(a.Pos.X, MathF.Max(b.Pos.X, c.Pos.X))), Math.Min(Width, (int)MathF.Ceiling(clip.Z)));
        var minY = Math.Max((int)MathF.Floor(MathF.Min(a.Pos.Y, MathF.Min(b.Pos.Y, c.Pos.Y))), Math.Max(0, (int)MathF.Floor(clip.Y)));
        var maxY = Math.Min((int)MathF.Ceiling(MathF.Max(a.Pos.Y, MathF.Max(b.Pos.Y, c.Pos.Y))), Math.Min(Height, (int)MathF.Ceiling(clip.W)));
        var ca = Colour(a.Col);
        var cb = Colour(b.Col);
        var cc = Colour(c.Col);
        for (var y = minY; y < maxY; y++)
        {
            for (var x = minX; x < maxX; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                if (p.X < clip.X || p.X >= clip.Z || p.Y < clip.Y || p.Y >= clip.W)
                {
                    continue;
                }

                var w0 = Edge(b.Pos, c.Pos, p) / area;
                var w1 = Edge(c.Pos, a.Pos, p) / area;
                var w2 = Edge(a.Pos, b.Pos, p) / area;
                // A pixel centre exactly on an edge two triangles share is drawn by the one it falls strictly inside of first.
                if (w0 < 0 || w1 < 0 || w2 < 0 || (w0 == 0 && w1 == 0))
                {
                    continue;
                }

                var col = (ca * w0) + (cb * w1) + (cc * w2);
                if (tex is not null)
                {
                    var uv = (a.Uv * w0) + (b.Uv * w1) + (c.Uv * w2);
                    col *= Sample(tex, uv);
                }

                var al = Math.Clamp(col.W, 0f, 1f);
                if (al <= 0)
                {
                    continue;
                }

                var i = ((y * Width) + x) * 3;
                px[i] = (px[i] * (1 - al)) + (Math.Clamp(col.X, 0f, 1f) * al);
                px[i + 1] = (px[i + 1] * (1 - al)) + (Math.Clamp(col.Y, 0f, 1f) * al);
                px[i + 2] = (px[i + 2] * (1 - al)) + (Math.Clamp(col.Z, 0f, 1f) * al);
            }
        }
    }

    private static float Edge(Vector2 a, Vector2 b, Vector2 p) => ((b.X - a.X) * (p.Y - a.Y)) - ((b.Y - a.Y) * (p.X - a.X));

    private static Vector4 Colour(uint abgr) =>
        new((abgr & 0xFF) / 255f, ((abgr >> 8) & 0xFF) / 255f, ((abgr >> 16) & 0xFF) / 255f, ((abgr >> 24) & 0xFF) / 255f);

    private static Vector4 Sample(Texture t, Vector2 uv)
    {
        var x = (uv.X * t.Width) - 0.5f;
        var y = (uv.Y * t.Height) - 0.5f;
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var fx = x - x0;
        var fy = y - y0;
        var a = Texel(t, x0, y0);
        var b = Texel(t, x0 + 1, y0);
        var c = Texel(t, x0, y0 + 1);
        var d = Texel(t, x0 + 1, y0 + 1);
        return Vector4.Lerp(Vector4.Lerp(a, b, fx), Vector4.Lerp(c, d, fx), fy);
    }

    private static Vector4 Texel(Texture t, int x, int y)
    {
        x = Math.Clamp(x, 0, t.Width - 1);
        y = Math.Clamp(y, 0, t.Height - 1);
        var i = ((y * t.Width) + x) * 4;
        return new Vector4(t.Rgba[i] / 255f, t.Rgba[i + 1] / 255f, t.Rgba[i + 2] / 255f, t.Rgba[i + 3] / 255f);
    }

    /// <summary>The picture as RGBA bytes.</summary>
    public byte[] ToRgba()
    {
        var bytes = new byte[Width * Height * 4];
        for (var i = 0; i < Width * Height; i++)
        {
            bytes[i * 4] = (byte)Math.Clamp((int)((px[i * 3] * 255) + 0.5f), 0, 255);
            bytes[(i * 4) + 1] = (byte)Math.Clamp((int)((px[(i * 3) + 1] * 255) + 0.5f), 0, 255);
            bytes[(i * 4) + 2] = (byte)Math.Clamp((int)((px[(i * 3) + 2] * 255) + 0.5f), 0, 255);
            bytes[(i * 4) + 3] = 255;
        }

        return bytes;
    }
}
