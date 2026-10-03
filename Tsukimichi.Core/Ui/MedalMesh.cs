using System.Numerics;

namespace Tsukimichi.Core.Ui;

/// <summary>A colour at a point, straight alpha, in the space a <see cref="MeshBuilder"/> draws in.</summary>
public delegate Vector4 Paint(Vector2 point);

/// <summary>
/// A pre-triangulated, pre-coloured vector drawing (the medals of feature plan v6 G1, their badges and the badge glyphs
/// a row draws at text height), built once from the shapes in <see cref="MedalArt"/> and drawn every frame by scaling
/// and offsetting its vertices into an ImGui draw list. Nothing is evaluated at draw time: each vertex carries its
/// colour (packed IM_COL32) and an anti-aliasing offset in device pixels, so a part's outline is feathered by one
/// pixel at any size, as ImGui's own anti-aliased fills are (the inner edge pulled in half a pixel, a transparent
/// fringe half a pixel out). A mesh built for one drawn size (<see cref="MeshBuilder(float, float)"/>) also knows which of
/// its shapes are thinner than a pixel there and draws them as coverage: collapsed to their centreline, their alpha
/// scaled by their thickness, feathered a pixel either side.
///
/// <para>The parts are in draw order. A part with a <see cref="MeshPart.MinSizePx"/> is detail that only reads from that
/// size (the drawn box, device px) up, and is skipped below it.</para>
/// </summary>
public sealed class MedalMesh
{
    // The brightest vertex's luminance, worked out on first use (-1 until then); a racing first use writes the same value.
    private float brightest = -1f;

    public MedalMesh(IReadOnlyList<MeshPart> parts) => Parts = parts;

    /// <summary>The parts in draw order.</summary>
    public IReadOnlyList<MeshPart> Parts { get; }

    /// <summary>
    /// The <see cref="Luminance"/> of the brightest vertex colour in any part (alpha ignored; 0 for an empty mesh): what a
    /// glyph drawn in one ink scales each vertex's brightness by. Worked out once, then free.
    /// </summary>
    public float Brightest
    {
        get
        {
            if (brightest < 0f)
            {
                var max = 0f;
                for (var p = 0; p < Parts.Count; p++)
                {
                    var colors = Parts[p].Colors;
                    for (var i = 0; i < colors.Length; i++)
                    {
                        max = MathF.Max(max, Luminance(colors[i]));
                    }
                }

                brightest = max;
            }

            return brightest;
        }
    }

    /// <summary>A packed IM_COL32 colour's (0xAABBGGRR) Rec. 709 luminance on its 0–255 channels, alpha ignored.</summary>
    public static float Luminance(uint color) =>
        ((color & 0xFFu) * 0.2126f) + (((color >> 8) & 0xFFu) * 0.7152f) + (((color >> 16) & 0xFFu) * 0.0722f);

    /// <summary>Vertices and indices of the parts drawn at <paramref name="sizePx"/>.</summary>
    public (int Vertices, int Indices) Count(float sizePx)
    {
        var v = 0;
        var i = 0;
        foreach (var part in Parts)
        {
            if (sizePx >= part.MinSizePx)
            {
                v += part.Positions.Length;
                i += part.Indices.Length;
            }
        }

        return (v, i);
    }
}

/// <summary>One part of a <see cref="MedalMesh"/>: triangles in drawing units, one colour and AA offset per vertex.</summary>
/// <param name="Positions">Vertex positions in the builder's units (the medal's 128-unit box).</param>
/// <param name="Offsets">Each vertex's anti-aliasing shift in device pixels (zero for interior vertices).</param>
/// <param name="Colors">Packed IM_COL32 colours (0xAABBGGRR).</param>
/// <param name="Indices">Triangle list into this part's vertices.</param>
/// <param name="MinSizePx">The smallest drawn box (device px) this part is drawn at.</param>
public sealed record MeshPart(Vector2[] Positions, Vector2[] Offsets, uint[] Colors, ushort[] Indices, float MinSizePx);

/// <summary>
/// Builds <see cref="MedalMesh"/>es from filled shapes: polygons (ear-clipped, so any simple outline), discs and
/// ellipses, bands (rings of vertices at chosen radii, for rims and arcs), strips between two curves, and row grids for
/// gradient scenes. Every fill takes a <see cref="Paint"/> evaluated once per vertex, before the builder's transform
/// (so a gradient moves with a shape, as an SVG gradient inside a transformed group does), and can feather its
/// outline (<c>aa</c>). Allocates while building only.
/// </summary>
public sealed class MeshBuilder
{
    private const float Epsilon = 1e-6f;

    private readonly List<MeshPart> parts = [];
    private readonly List<Vector2> positions = [];
    private readonly List<Vector2> offsets = [];
    private readonly List<uint> colors = [];
    private readonly List<ushort> indices = [];
    private readonly float detail;
    private readonly float pixels;
    private float minSize;
    private Matrix3x2 transform = Matrix3x2.Identity;

    /// <summary>
    /// A builder whose discs, bands and row grids take <paramref name="levelOfDetail"/> of the segments they ask for
    /// (at least 8): the row tier's meshes are built at 0.4, where a 72-segment rim is 29 and still under a tenth of a
    /// pixel off the circle at 31 px. With <paramref name="sizePx"/> (the device px its <see cref="BoxUnits"/>-unit box
    /// is drawn at) the mesh is for that size only, and every feathered shape thinner than a pixel there is drawn as
    /// coverage (see <see cref="Fringe"/>); 0 builds a mesh for any size.
    /// </summary>
    public MeshBuilder(float levelOfDetail = 1f, float sizePx = 0f)
    {
        detail = Math.Clamp(levelOfDetail, 0.1f, 1f);
        pixels = sizePx > 0f ? sizePx / BoxUnits : 0f;
    }

    /// <summary>The side of the box a mesh is drawn in, in its own units: a mesh drawn <c>s</c> px across scales by <c>s / BoxUnits</c>.</summary>
    public const float BoxUnits = 128f;

    /// <summary>Whether this builder's mesh is for one drawn size, so a shape finer than a pixel there draws as its coverage.</summary>
    public bool Coverage => pixels > 0f;

    /// <summary>Device px per unit of the one size this builder's mesh is for (0: any size).</summary>
    public float PixelsPerUnit => pixels;

    /// <summary>Starts a new part drawn only from <paramref name="minSizePx"/> up (0: always).</summary>
    public MeshBuilder Detail(float minSizePx)
    {
        if (minSizePx != minSize)
        {
            Flush();
            minSize = minSizePx;
        }

        return this;
    }

    /// <summary>Maps every following shape through <paramref name="matrix"/> (paints still see the untransformed point).</summary>
    public MeshBuilder Transform(Matrix3x2 matrix)
    {
        transform = matrix;
        return this;
    }

    /// <summary>The finished mesh.</summary>
    public MedalMesh Build()
    {
        Flush();
        return new MedalMesh(parts.ToArray());
    }

    // ------------------------------------------------------------------ paints

    /// <summary>A flat colour, its alpha multiplied by <paramref name="alpha"/>.</summary>
    public static Paint Solid(Vector4 color, float alpha = 1f)
    {
        var c = color with { W = color.W * alpha };
        return _ => c;
    }

    /// <summary>A linear gradient from <paramref name="from"/> (offset 0) to <paramref name="to"/> (offset 1), like SVG's userSpaceOnUse.</summary>
    public static Paint Linear(Vector2 from, Vector2 to, params (float Offset, Vector4 Color)[] stops)
    {
        var d = to - from;
        var len2 = d.LengthSquared();
        return p => Stop(stops, len2 > 0f ? Vector2.Dot(p - from, d) / len2 : 0f);
    }

    /// <summary>A radial gradient round <paramref name="center"/>: offset 1 at <paramref name="radii"/> (an ellipse when the two differ).</summary>
    public static Paint Radial(Vector2 center, Vector2 radii, params (float Offset, Vector4 Color)[] stops) =>
        p => Stop(stops, ((p - center) / radii).Length());

    /// <summary>A radial gradient with a circular radius.</summary>
    public static Paint Radial(Vector2 center, float radius, params (float Offset, Vector4 Color)[] stops) =>
        Radial(center, new Vector2(radius), stops);

    /// <summary>
    /// <paramref name="top"/> composited over <paramref name="bottom"/> (both straight alpha): the "over" operator, so
    /// layered SVG fills (a gradient, a glow over it, a sheen over both) become one paint.
    /// </summary>
    public static Paint Over(Paint top, Paint bottom) =>
        p =>
        {
            var t = top(p);
            var b = bottom(p);
            var a = t.W + b.W * (1f - t.W);
            if (!(a > 0f))
            {
                return Vector4.Zero;
            }

            var rgb = (new Vector3(t.X, t.Y, t.Z) * t.W + new Vector3(b.X, b.Y, b.Z) * b.W * (1f - t.W)) / a;
            return new Vector4(rgb, a);
        };

    /// <summary>The colour at <paramref name="t"/> along SVG gradient stops (pad spread), straight alpha interpolated.</summary>
    public static Vector4 Stop(ReadOnlySpan<(float Offset, Vector4 Color)> stops, float t)
    {
        if (stops.Length == 0)
        {
            return Vector4.Zero;
        }

        if (!(t > stops[0].Offset))
        {
            return stops[0].Color;
        }

        for (var i = 1; i < stops.Length; i++)
        {
            if (t <= stops[i].Offset)
            {
                var span = stops[i].Offset - stops[i - 1].Offset;
                var k = span > 0f ? (t - stops[i - 1].Offset) / span : 1f;
                return Vector4.Lerp(stops[i - 1].Color, stops[i].Color, k);
            }
        }

        return stops[^1].Color;
    }

    /// <summary>A colour with its alpha set.</summary>
    public static Vector4 Alpha(Vector4 color, float alpha) => color with { W = alpha };

    /// <summary>Hermite smoothstep of <paramref name="x"/> from <paramref name="edge0"/> to <paramref name="edge1"/>.</summary>
    public static float Smooth(float edge0, float edge1, float x)
    {
        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    // ------------------------------------------------------------------ fills

    /// <summary>A simple polygon (either winding, no self-intersections), ear-clipped, its outline feathered when <paramref name="aa"/>.</summary>
    public MeshBuilder Polygon(IReadOnlyList<Vector2> loop, Paint paint, bool aa = true)
    {
        var points = Clean(loop);
        if (points.Count < 3)
        {
            return this;
        }

        var start = Begin(points.Count);
        foreach (var p in points)
        {
            Add(p, paint);
        }

        foreach (var (a, b, c) in Triangulate(points))
        {
            Tri(start + a, start + b, start + c);
        }

        if (aa)
        {
            Fringe(Range(start, points.Count));
        }

        return this;
    }

    /// <summary>A disc of <paramref name="rings"/> concentric rings (so radial and multi-stop paints interpolate well).</summary>
    public MeshBuilder Disc(Vector2 center, float radius, Paint paint, int segments = 48, int rings = 3, bool aa = true) =>
        Ellipse(center, new Vector2(radius), 0f, paint, segments, rings, aa);

    /// <summary>An ellipse with radii <paramref name="radii"/> turned <paramref name="degrees"/> (clockwise on screen), as rings round its centre.</summary>
    public MeshBuilder Ellipse(Vector2 center, Vector2 radii, float degrees, Paint paint, int segments = 48, int rings = 3, bool aa = true)
    {
        var (s, c) = MathF.SinCos(degrees * MathF.PI / 180f);
        segments = Segments(segments);
        var start = Begin(1 + rings * segments);
        Add(center, paint);
        for (var ring = 1; ring <= rings; ring++)
        {
            var k = (float)ring / rings;
            for (var j = 0; j < segments; j++)
            {
                var d = Dir(j * 360f / segments) * radii * k;
                Add(center + new Vector2(d.X * c - d.Y * s, d.X * s + d.Y * c), paint);
            }
        }

        for (var j = 0; j < segments; j++)
        {
            Tri(start, start + 1 + j, start + 1 + ((j + 1) % segments));
        }

        for (var ring = 1; ring < rings; ring++)
        {
            var inner = start + 1 + (ring - 1) * segments;
            var outer = inner + segments;
            for (var j = 0; j < segments; j++)
            {
                var next = (j + 1) % segments;
                Quad(inner + j, inner + next, outer + next, outer + j);
            }
        }

        if (aa)
        {
            Fringe(Range(start + 1 + (rings - 1) * segments, segments));
        }

        return this;
    }

    /// <summary>
    /// A band round <paramref name="center"/>: one ring of vertices at each of <paramref name="radii"/> (ascending; a
    /// radius repeated with two different paints is a crisp edge, like a rim's crest), each coloured by its own paint,
    /// over the arc <paramref name="fromDegrees"/>..<paramref name="toDegrees"/> (screen degrees, clockwise from 3 o'clock;
    /// a full turn closes it). Its inner and outer rings are feathered when <paramref name="aa"/>.
    /// </summary>
    public MeshBuilder Band(Vector2 center, IReadOnlyList<float> radii, IReadOnlyList<Paint> paints, float fromDegrees = 0f, float toDegrees = 360f, int segments = 64, bool aa = true)
    {
        var full = MathF.Abs(toDegrees - fromDegrees) >= 359.999f;
        segments = Segments(segments);
        var count = full ? segments : segments + 1;
        var rows = radii.Count;
        var start = Begin(rows * count);
        for (var j = 0; j < rows; j++)
        {
            for (var k = 0; k < count; k++)
            {
                Add(center + Dir(fromDegrees + (toDegrees - fromDegrees) * k / segments) * radii[j], paints[Math.Min(j, paints.Count - 1)]);
            }
        }

        for (var j = 0; j < rows - 1; j++)
        {
            var a = start + j * count;
            var b = a + count;
            for (var k = 0; k < segments; k++)
            {
                var next = full ? (k + 1) % count : k + 1;
                Quad(a + k, a + next, b + next, b + k);
            }
        }

        if (aa)
        {
            // Across the band at each ring vertex: radially to the matching vertex of the other ring.
            var outerRing = Range(start + (rows - 1) * count, count);
            var innerRing = Range(start, count);
            var inwardAcross = new List<Vector2>(count);
            var outwardAcross = new List<Vector2>(count);
            for (var k = 0; k < count; k++)
            {
                inwardAcross.Add(positions[innerRing[k]] - positions[outerRing[k]]);
                outwardAcross.Add(positions[outerRing[k]] - positions[innerRing[k]]);
            }

            for (var j = 1; j < rows - 1; j++)
            {
                for (var k = 0; k < count; k++)
                {
                    var a = positions[innerRing[k]];
                    var c = positions[outerRing[k]];
                    Collapse(start + j * count + k, (a + c) * 0.5f, Vector2.Distance(a, c));
                }
            }

            if (full)
            {
                Fringe(outerRing, across: inwardAcross);
                Fringe(innerRing, inward: true, across: outwardAcross);
            }
            else
            {
                // One loop: the outer ring forward, then the inner ring back.
                innerRing.Reverse();
                outwardAcross.Reverse();
                Fringe([.. outerRing, .. innerRing], across: [.. inwardAcross, .. outwardAcross]);
            }
        }

        return this;
    }

    /// <summary>An annulus (or an arc of one) in one paint, <paramref name="steps"/> rings across its width.</summary>
    public MeshBuilder Annulus(Vector2 center, float inner, float outer, Paint paint, float fromDegrees = 0f, float toDegrees = 360f, int segments = 64, int steps = 1, bool aa = true)
    {
        var radii = new float[steps + 1];
        for (var j = 0; j <= steps; j++)
        {
            radii[j] = inner + (outer - inner) * j / steps;
        }

        return Band(center, radii, [paint], fromDegrees, toDegrees, segments, aa);
    }

    /// <summary>
    /// A strip between two curves sampled at matching points (<paramref name="side"/>[i] faces <paramref name="other"/>[i]),
    /// with a column of vertices at each fraction of <paramref name="columns"/> across (default: just the two edges);
    /// its outline is both curves and both ends. Matching points may coincide (a crescent's horns).
    /// </summary>
    public MeshBuilder Strip(IReadOnlyList<Vector2> side, IReadOnlyList<Vector2> other, Paint paint, IReadOnlyList<float>? columns = null, bool aa = true)
    {
        var n = Math.Min(side.Count, other.Count);
        if (n < 2)
        {
            return this;
        }

        columns ??= [0f, 1f];
        var cols = columns.Count;
        var start = Begin(n * cols);
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < cols; j++)
            {
                Add(Vector2.Lerp(side[i], other[i], columns[j]), paint);
            }
        }

        for (var i = 0; i < n - 1; i++)
        {
            for (var j = 0; j < cols - 1; j++)
            {
                var a = start + i * cols + j;
                Quad(a, a + 1, a + cols + 1, a + cols);
            }
        }

        if (aa)
        {
            // Across the strip at each outline vertex: to its matching point on the other curve.
            var ids = new List<int>(2 * n);
            var across = new List<Vector2>(2 * n);
            for (var i = 0; i < n; i++)
            {
                var a = start + i * cols;
                ids.Add(a);
                across.Add(positions[a + cols - 1] - positions[a]);
            }

            for (var i = n - 1; i >= 0; i--)
            {
                var a = start + i * cols;
                ids.Add(a + cols - 1);
                across.Add(positions[a] - positions[a + cols - 1]);
            }

            for (var i = 0; i < n; i++)
            {
                var a = positions[start + i * cols];
                var c = positions[start + i * cols + cols - 1];
                for (var j = 1; j < cols - 1; j++)
                {
                    Collapse(start + i * cols + j, (a + c) * 0.5f, Vector2.Distance(a, c));
                }
            }

            Fringe(ids, across: across);
        }

        return this;
    }

    /// <summary>
    /// A disc as rows of vertices at <paramref name="rows"/> heights (each row spans the circle's width there, with
    /// <paramref name="columns"/> + 1 vertices), so vertical gradients and a hard horizon interpolate exactly: a height
    /// listed twice is a break, its first row painted with <paramref name="above"/> and its second with
    /// <paramref name="below"/>. Not feathered: it is drawn under the rim. Rows must be ascending.
    /// </summary>
    public MeshBuilder Rows(Vector2 center, float radius, IReadOnlyList<float> rows, Paint above, Paint below, float breakY, int columns = 16)
    {
        columns = Segments(columns);
        var cols = columns + 1;
        var start = Begin(rows.Count * cols);
        for (var i = 0; i < rows.Count; i++)
        {
            var y = rows[i];
            var dy = Math.Clamp(y - center.Y, -radius, radius);
            var half = MathF.Sqrt(MathF.Max(0f, radius * radius - dy * dy));
            var second = i > 0 && rows[i - 1] == y;
            var paint = y < breakY || (y == breakY && !second) ? above : below;
            for (var j = 0; j < cols; j++)
            {
                Add(new Vector2(center.X - half + 2f * half * j / columns, y), paint);
            }
        }

        for (var i = 0; i < rows.Count - 1; i++)
        {
            if (rows[i] == rows[i + 1])
            {
                continue;
            }

            for (var j = 0; j < columns; j++)
            {
                var a = start + i * cols + j;
                Quad(a, a + 1, a + cols + 1, a + cols);
            }
        }

        return this;
    }

    // ------------------------------------------------------------------ outlines

    /// <summary>A circle's outline, clockwise on screen from 3 o'clock.</summary>
    public static List<Vector2> Circle(Vector2 center, float radius, int segments = 48)
    {
        var list = new List<Vector2>(segments);
        for (var k = 0; k < segments; k++)
        {
            list.Add(center + Dir(k * 360f / segments) * radius);
        }

        return list;
    }

    /// <summary>Points on an arc from <paramref name="fromDegrees"/> to <paramref name="toDegrees"/>, both ends included.</summary>
    public static IEnumerable<Vector2> Arc(Vector2 center, float radius, float fromDegrees, float toDegrees, int segments)
    {
        for (var k = 0; k <= segments; k++)
        {
            yield return center + Dir(fromDegrees + (toDegrees - fromDegrees) * k / segments) * radius;
        }
    }

    /// <summary>Samples of a cubic Bézier, excluding its start point.</summary>
    public static IEnumerable<Vector2> Cubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, int segments)
    {
        for (var k = 1; k <= segments; k++)
        {
            var t = (float)k / segments;
            var u = 1f - t;
            yield return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
        }
    }

    /// <summary>
    /// The outline of a polyline stroked <paramref name="width"/> wide with round caps and round joins (SVG
    /// <c>stroke-linecap="round" stroke-linejoin="round"</c>) as one simple polygon: one side out, a cap, the other side
    /// back, a cap. Gentle bends are mitred; sharp corners get a round outer join.
    /// </summary>
    public static List<Vector2> Stroke(IReadOnlyList<Vector2> line, float width, int capSegments = 8)
    {
        var h = width * 0.5f;
        var n = line.Count;
        var left = new List<Vector2>();
        var right = new List<Vector2>();
        for (var i = 0; i < n; i++)
        {
            var dIn = Vector2.Normalize(i > 0 ? line[i] - line[i - 1] : line[1] - line[0]);
            var dOut = i < n - 1 ? Vector2.Normalize(line[i + 1] - line[i]) : dIn;
            var nIn = new Vector2(-dIn.Y, dIn.X);
            var nOut = new Vector2(-dOut.Y, dOut.X);
            var turn = dIn.X * dOut.Y - dIn.Y * dOut.X;
            var miter = Vector2.Normalize(nIn + nOut);
            var inner = miter * (h / MathF.Max(0.3f, Vector2.Dot(miter, nIn)));
            if (i > 0 && i < n - 1 && MathF.Abs(turn) > 0.25f)
            {
                var a0 = MathF.Atan2(nIn.Y, nIn.X);
                var a1 = MathF.Atan2(nOut.Y, nOut.X);
                if (turn > 0f)
                {
                    // Turning toward +normal: that side is the inside of the corner.
                    left.Add(line[i] + inner);
                    right.AddRange(ArcRadians(line[i], h, a0 + MathF.PI, a1 + MathF.PI, capSegments));
                }
                else
                {
                    right.Add(line[i] - inner);
                    left.AddRange(ArcRadians(line[i], h, a0, a1, capSegments));
                }
            }
            else
            {
                left.Add(line[i] + inner);
                right.Add(line[i] - inner);
            }
        }

        var loop = new List<Vector2>(left.Count + right.Count + 2 * capSegments);
        loop.AddRange(left);
        var dEnd = Vector2.Normalize(line[n - 1] - line[n - 2]);
        var aEnd = MathF.Atan2(dEnd.X, -dEnd.Y);
        loop.AddRange(ArcRadians(line[n - 1], h, aEnd, aEnd - MathF.PI, capSegments).Skip(1).SkipLast(1));
        for (var i = right.Count - 1; i >= 0; i--)
        {
            loop.Add(right[i]);
        }

        var dStart = Vector2.Normalize(line[1] - line[0]);
        var aStart = MathF.Atan2(-dStart.X, dStart.Y);
        loop.AddRange(ArcRadians(line[0], h, aStart, aStart - MathF.PI, capSegments).Skip(1).SkipLast(1));
        return loop;
    }

    /// <summary>
    /// The polygon <paramref name="loop"/> grown by <paramref name="distance"/> along its vertex normals: a keyline
    /// stroke drawn behind a shape (<c>paint-order="stroke"</c>). Good for smooth or gently cornered outlines.
    /// </summary>
    public static List<Vector2> Grow(IReadOnlyList<Vector2> loop, float distance)
    {
        var points = Clean(loop);
        var n = points.Count;
        var sign = SignedArea(points) >= 0f ? 1f : -1f;
        var result = new List<Vector2>(n);
        for (var i = 0; i < n; i++)
        {
            var n0 = Outward(points[(i + n - 1) % n], points[i], sign);
            var n1 = Outward(points[i], points[(i + 1) % n], sign);
            var m = n0 + n1;
            m = m.LengthSquared() > Epsilon ? Vector2.Normalize(m) : n0;
            result.Add(points[i] + m * (distance / MathF.Max(0.35f, Vector2.Dot(m, n0))));
        }

        return result;
    }

    /// <summary>Twice the signed area (shoelace); positive for a loop clockwise on a y-down screen.</summary>
    public static float SignedArea(IReadOnlyList<Vector2> loop)
    {
        var sum = 0f;
        for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++)
        {
            sum += loop[j].X * loop[i].Y - loop[i].X * loop[j].Y;
        }

        return sum;
    }

    /// <summary>
    /// Ear-clipping triangulation of a simple polygon: triangles as index triples into <paramref name="loop"/>. Falls back
    /// to a fan for what is left if the outline is not simple, so a bad outline draws something rather than nothing.
    /// </summary>
    public static List<(int A, int B, int C)> Triangulate(IReadOnlyList<Vector2> loop)
    {
        var n = loop.Count;
        var result = new List<(int, int, int)>(Math.Max(0, n - 2));
        if (n < 3)
        {
            return result;
        }

        var clockwise = SignedArea(loop) > 0f;
        var remaining = new List<int>(n);
        for (var i = 0; i < n; i++)
        {
            remaining.Add(i);
        }

        while (remaining.Count > 3)
        {
            var clipped = false;
            for (var k = 0; k < remaining.Count; k++)
            {
                var ia = remaining[(k + remaining.Count - 1) % remaining.Count];
                var ib = remaining[k];
                var ic = remaining[(k + 1) % remaining.Count];
                var a = loop[ia];
                var b = loop[ib];
                var c = loop[ic];
                var cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
                if (clockwise ? cross <= 0f : cross >= 0f)
                {
                    continue;
                }

                var ear = true;
                foreach (var other in remaining)
                {
                    if (other != ia && other != ib && other != ic && InTriangle(loop[other], a, b, c))
                    {
                        ear = false;
                        break;
                    }
                }

                if (!ear)
                {
                    continue;
                }

                result.Add((ia, ib, ic));
                remaining.RemoveAt(k);
                clipped = true;
                break;
            }

            if (!clipped)
            {
                for (var k = 1; k < remaining.Count - 1; k++)
                {
                    result.Add((remaining[0], remaining[k], remaining[k + 1]));
                }

                return result;
            }
        }

        result.Add((remaining[0], remaining[1], remaining[2]));
        return result;
    }

    /// <summary>The unit vector at screen angle <paramref name="degrees"/> (clockwise from 3 o'clock on a y-down screen).</summary>
    public static Vector2 Dir(float degrees)
    {
        var (s, c) = MathF.SinCos(degrees * MathF.PI / 180f);
        return new Vector2(c, s);
    }

    /// <summary>IM_COL32 packing (0xAABBGGRR), the same as the plugin's <c>Theme</c>.</summary>
    public static uint Pack(Vector4 c)
    {
        static uint Channel(float v) => (uint)Math.Clamp((int)MathF.Round((float.IsFinite(v) ? v : 0f) * 255f), 0, 255);
        return Channel(c.X) | (Channel(c.Y) << 8) | (Channel(c.Z) << 16) | (Channel(c.W) << 24);
    }

    // ------------------------------------------------------------------ internals

    /// <summary>Points on an arc in radians (screen angles), endpoints included: the short way, or exactly the given half turn.</summary>
    private static IEnumerable<Vector2> ArcRadians(Vector2 center, float radius, float from, float to, int segments)
    {
        var sweep = to - from;
        if (MathF.Abs(MathF.Abs(sweep) - MathF.PI) > 1e-3f)
        {
            while (sweep > MathF.PI) sweep -= 2f * MathF.PI;
            while (sweep < -MathF.PI) sweep += 2f * MathF.PI;
        }

        for (var k = 0; k <= segments; k++)
        {
            var a = from + sweep * k / segments;
            yield return center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
        }
    }

    private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        var d1 = (p.X - b.X) * (a.Y - b.Y) - (a.X - b.X) * (p.Y - b.Y);
        var d2 = (p.X - c.X) * (b.Y - c.Y) - (b.X - c.X) * (p.Y - c.Y);
        var d3 = (p.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (p.Y - a.Y);
        var neg = d1 < 0f || d2 < 0f || d3 < 0f;
        var pos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(neg && pos);
    }

    /// <summary>The outline without repeated neighbours (the closing point included), so normals and ears stay defined.</summary>
    private static List<Vector2> Clean(IReadOnlyList<Vector2> loop)
    {
        var list = new List<Vector2>(loop.Count);
        foreach (var p in loop)
        {
            if (list.Count == 0 || Vector2.DistanceSquared(list[^1], p) > Epsilon)
            {
                list.Add(p);
            }
        }

        while (list.Count > 1 && Vector2.DistanceSquared(list[0], list[^1]) <= Epsilon)
        {
            list.RemoveAt(list.Count - 1);
        }

        return list;
    }

    private static Vector2 Outward(Vector2 a, Vector2 b, float sign)
    {
        var d = b - a;
        var len = d.Length();
        return len < 1e-5f ? Vector2.Zero : sign * new Vector2(d.Y, -d.X) / len;
    }

    private static List<int> Range(int start, int count)
    {
        var list = new List<int>(count);
        for (var i = 0; i < count; i++)
        {
            list.Add(start + i);
        }

        return list;
    }

    /// <summary>The segments this builder's level of detail gives a shape that asks for <paramref name="requested"/> (at least 8).</summary>
    public int Segments(int requested) => requested <= 8 ? requested : Math.Max(8, (int)MathF.Round(requested * detail));

    private int Begin(int vertices)
    {
        // A part's indices are 16-bit; the fringe can double a shape's vertices.
        if (positions.Count + vertices * 2 > ushort.MaxValue)
        {
            Flush();
        }

        return positions.Count;
    }

    private void Add(Vector2 p, Paint paint)
    {
        positions.Add(Vector2.Transform(p, transform));
        offsets.Add(Vector2.Zero);
        colors.Add(Pack(paint(p)));
    }

    private void Tri(int a, int b, int c)
    {
        indices.Add((ushort)a);
        indices.Add((ushort)b);
        indices.Add((ushort)c);
    }

    private void Quad(int a, int b, int c, int d)
    {
        Tri(a, b, c);
        Tri(a, c, d);
    }

    /// <summary>
    /// Feathers the closed outline through the vertices <paramref name="ids"/> (in order, as drawn): each is pulled in half
    /// a pixel along its normal and a transparent copy is added half a pixel out, joined by a quad per edge. With
    /// <paramref name="inward"/> the shape lies outside the loop (the hole of a ring). Coincident neighbours (a horn's tip)
    /// take the normal of their nearest distinct neighbours.
    ///
    /// <para>In a mesh built for one size, a vertex where the shape is thinner than a pixel is not pulled in half a pixel:
    /// for such a shape that crosses the far side's pull and draws it about a pixel wide at full strength, however fine it
    /// is. Its thickness <c>t</c> (device px) is the length of its <paramref name="across"/> vector, the way to the
    /// shape's far side, or found by casting a ray inward (<see cref="Thickness"/>) when that is not given. The vertex
    /// moves onto the shape's centreline instead, its alpha scaled by <c>t</c>, and its transparent copy goes a pixel out
    /// from there (half a pixel past the edge at <c>t</c> = 1, where the two schemes meet): a tent a pixel either side of
    /// the centreline, which carries <c>t</c> of coverage wherever the line falls between pixel centres.</para>
    /// </summary>
    private void Fringe(List<int> ids, bool inward = false, IReadOnlyList<Vector2>? across = null)
    {
        var n = ids.Count;
        if (n < 3)
        {
            return;
        }

        var loop = new Vector2[n];
        for (var i = 0; i < n; i++)
        {
            loop[i] = positions[ids[i]];
        }

        var sign = (SignedArea(loop) >= 0f ? 1f : -1f) * (inward ? -1f : 1f);
        var normals = new Vector2[n];
        var sides = new (Vector2 In, Vector2 Out)[n];
        for (var i = 0; i < n; i++)
        {
            var prev = (i + n - 1) % n;
            while (prev != i && Vector2.DistanceSquared(loop[prev], loop[i]) <= Epsilon)
            {
                prev = (prev + n - 1) % n;
            }

            var next = (i + 1) % n;
            while (next != i && Vector2.DistanceSquared(loop[next], loop[i]) <= Epsilon)
            {
                next = (next + 1) % n;
            }

            sides[i] = (Outward(loop[prev], loop[i], sign), Outward(loop[i], loop[next], sign));
            var m = (sides[i].In + sides[i].Out) * 0.5f;

            // ImGui's IM_FIXNORMAL2F: lengthen the averaged normal at corners, capped.
            normals[i] = m * (1f / MathF.Max(m.LengthSquared(), 0.5f));
        }

        var outer = positions.Count;
        for (var i = 0; i < n; i++)
        {
            var id = ids[i];
            if (pixels > 0f)
            {
                var unit = normals[i].LengthSquared() > Epsilon ? Vector2.Normalize(normals[i]) : Vector2.Zero;
                var way = across?[i] ?? Across(loop, i, unit, sides[i].In, sides[i].Out);
                var thickness = way.Length() * pixels;
                if (thickness < 1f)
                {
                    var centre = 0.5f * pixels * way;
                    offsets[id] = centre;
                    colors[id] = Fade(colors[id], thickness);
                    positions.Add(loop[i]);
                    offsets.Add(centre + unit);
                    colors.Add(colors[id] & 0x00FFFFFFu);
                    continue;
                }
            }

            offsets[id] = -0.5f * normals[i];
            positions.Add(loop[i]);
            offsets.Add(0.5f * normals[i]);
            colors.Add(colors[id] & 0x00FFFFFFu);
        }

        for (var i = 0; i < n; i++)
        {
            var next = (i + 1) % n;
            Quad(ids[i], ids[next], outer + next, outer + i);
        }
    }

    /// <summary>
    /// An interior vertex of a shape <paramref name="width"/> units across there (a strip's middle column, a band's middle
    /// ring): in a mesh built for one size, where that is under a pixel, it joins the outline on the centreline
    /// <paramref name="centre"/> and fades with it (see <see cref="Fringe"/>).
    /// </summary>
    private void Collapse(int vertex, Vector2 centre, float width)
    {
        var thickness = width * pixels;
        if (pixels > 0f && thickness < 1f)
        {
            offsets[vertex] = (centre - positions[vertex]) * pixels;
            colors[vertex] = Fade(colors[vertex], thickness);
        }
    }

    /// <summary>
    /// The shortest way across the shape from <paramref name="loop"/>[<paramref name="i"/>]: the nearest far side
    /// straight in along the vertex's normal or either edge's (so a corner of a thin bar measures the bar, not its
    /// diagonal), as a vector; at most <see cref="BoxUnits"/> long.
    /// </summary>
    private static Vector2 Across(IReadOnlyList<Vector2> loop, int i, Vector2 normal, Vector2 edgeIn, Vector2 edgeOut)
    {
        var best = new Vector2(0f, BoxUnits);
        var length = BoxUnits;
        foreach (var outward in (ReadOnlySpan<Vector2>)[normal, edgeIn, edgeOut])
        {
            var t = Thickness(loop, i, -outward);
            if (t < length)
            {
                length = t;
                best = -outward * t;
            }
        }

        return best;
    }

    /// <summary>A packed colour with its alpha multiplied by <paramref name="k"/> (0..1).</summary>
    private static uint Fade(uint color, float k) =>
        (color & 0x00FFFFFFu) | ((uint)MathF.Round((color >> 24) * Math.Clamp(k, 0f, 1f)) << 24);

    /// <summary>
    /// How far a ray from <paramref name="loop"/>[<paramref name="i"/>] along <paramref name="direction"/> runs before it
    /// meets an edge of the loop not at that point: the shape's thickness there. Infinite if it meets none.
    /// </summary>
    public static float Thickness(IReadOnlyList<Vector2> loop, int i, Vector2 direction)
    {
        var best = float.PositiveInfinity;
        if (direction.LengthSquared() <= Epsilon)
        {
            return best;
        }

        var p = loop[i];
        for (var j = 0; j < loop.Count; j++)
        {
            var a = loop[j];
            var b = loop[(j + 1) % loop.Count];
            if (Vector2.DistanceSquared(a, p) <= Epsilon || Vector2.DistanceSquared(b, p) <= Epsilon)
            {
                continue;
            }

            // p + s·direction = a + u·(b - a), with s > 0 and u in 0..1.
            var e = b - a;
            var denom = direction.X * e.Y - direction.Y * e.X;
            if (MathF.Abs(denom) <= Epsilon)
            {
                continue;
            }

            var w = a - p;
            var s = (w.X * e.Y - w.Y * e.X) / denom;
            var u = (w.X * direction.Y - w.Y * direction.X) / denom;
            if (s > Epsilon && u is >= 0f and <= 1f && s < best)
            {
                best = s;
            }
        }

        return best;
    }

    private void Flush()
    {
        if (positions.Count > 0)
        {
            parts.Add(new MeshPart(positions.ToArray(), offsets.ToArray(), colors.ToArray(), indices.ToArray(), minSize));
        }

        positions.Clear();
        offsets.Clear();
        colors.Clear();
        indices.Clear();
    }
}
